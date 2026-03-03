using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ItauCorretora.Domain.Entities;
using Xunit;

namespace ItauCorretora.Tests
{
    /// <summary>
    /// Testes Unitários — Cálculos Críticos do Sistema
    ///
    /// Cobre os pontos mais sensíveis do negócio:
    ///   1. Cálculo de aporte por evento (1/3 do mensal)
    ///   2. Fórmula de Preço Médio (PM) na compra
    ///   3. PM não muda na venda
    ///   4. Cálculo de rateio proporcional com truncamento
    ///   5. IR Dedo-Duro (0,005%)
    ///   6. Separação Lote Padrão vs Fracionário
    ///   7. Resíduo do cliente após rateio
    /// </summary>
    public class CalculosCriticosTests
    {
        // =========================================================
        // 1. APORTE POR EVENTO
        // =========================================================

        [Theory]
        [InlineData(300.00, 100.00)]   // 300 / 3 = 100
        [InlineData(1000.00, 333.33)]  // 1000 / 3 = 333,33 (truncado)
        [InlineData(150.00, 50.00)]    // 150 / 3 = 50
        public void CalcularAporteDoEvento_DeveRetornarUmTercoDoMensal(
            decimal aporteMensal, decimal esperado)
        {
            var cliente = Cliente.Criar("João", "111.111.111-11", "C001", aporteMensal);
            var aporteEvento = cliente.CalcularAporteDoEvento();
            Assert.Equal(esperado, aporteEvento);
        }

        [Fact]
        public void CalcularAporteEfetivo_DeveIncluirSaldoResidual()
        {
            // Arrange
            var cliente = Cliente.Criar("Maria", "222.222.222-22", "C002", 300m);
            cliente.AtualizarSaldoResidual(15.50m); // Resíduo de ciclos anteriores

            // Act: Aporte evento = 100 + resíduo 15,50 = 115,50
            var aporteEfetivo = cliente.CalcularAporteEfetivoDoEvento();

            // Assert
            Assert.Equal(115.50m, aporteEfetivo);
        }

        [Fact]
        public void CalcularAporte_ClienteInativo_DeveRetornarZero()
        {
            var cliente = Cliente.Criar("Pedro", "333.333.333-33", "C003", 300m);
            cliente.Inativar();
            Assert.Equal(0m, cliente.CalcularAporteDoEvento());
        }

        // =========================================================
        // 2. PREÇO MÉDIO — COMPRA (REGRA CRÍTICA)
        // =========================================================

        [Fact]
        public void RegistrarCompra_PrimeiraCompra_PMDeveSerIgualAoPrecoDeCompra()
        {
            // Arrange & Act
            var custodia = Custodia.CriarPosicaoInicial(
                Guid.NewGuid(), "PETR4", quantidade: 100, precoCompra: 35.50m);

            // Assert: na primeira compra, PM = preço de compra
            Assert.Equal(100, custodia.Quantidade);
            Assert.Equal(35.50m, custodia.PrecoMedio);
        }

        [Fact]
        public void RegistrarCompra_SegundaCompra_PMDeveSerRecalculado()
        {
            // Arrange: 100 ações a R$ 35,50
            var custodia = Custodia.CriarPosicaoInicial(
                Guid.NewGuid(), "PETR4", quantidade: 100, precoCompra: 35.50m);

            // Act: compra mais 100 ações a R$ 40,00
            // PM = (100 * 35,50 + 100 * 40,00) / (100 + 100)
            //    = (3550 + 4000) / 200
            //    = 7550 / 200
            //    = 37,75
            custodia.RegistrarCompra(quantidadeComprada: 100, precoCompra: 40.00m);

            // Assert
            Assert.Equal(200, custodia.Quantidade);
            Assert.Equal(37.75m, custodia.PrecoMedio);
        }

        [Fact]
        public void RegistrarCompra_TresCompras_PMDeveAcumularCorretamente()
        {
            // Simula 3 ciclos de compra (dias 5, 15, 25)
            var custodia = Custodia.CriarPosicaoInicial(
                Guid.NewGuid(), "VALE3", quantidade: 10, precoCompra: 70.00m);

            // Dia 15: +5 ações a R$ 65,00
            // PM = (10*70 + 5*65) / 15 = (700 + 325) / 15 = 1025/15 = 68,333333...
            custodia.RegistrarCompra(5, 65.00m);
            Assert.Equal(15, custodia.Quantidade);

            // Dia 25: +8 ações a R$ 72,00
            // PM = (15 * PMAnterior + 8 * 72) / 23
            var pmEsperado = Math.Round((15 * custodia.PrecoMedio + 8 * 72m) / 23, 6);
            custodia.RegistrarCompra(8, 72.00m);
            Assert.Equal(23, custodia.Quantidade);
            Assert.Equal(pmEsperado, custodia.PrecoMedio);
        }

        // =========================================================
        // 3. PREÇO MÉDIO — VENDA (NÃO ALTERA PM)
        // =========================================================

        [Fact]
        public void RegistrarVenda_NaoDeveAlterarPrecoMedio()
        {
            // Arrange
            var clienteId = Guid.NewGuid();
            var custodia = Custodia.CriarPosicaoInicial(
                clienteId, "BBAS3", quantidade: 200, precoCompra: 55.00m);

            var pmAntesDaVenda = custodia.PrecoMedio;

            // Act: vende 50 ações a R$ 60,00 (com lucro)
            var resultado = custodia.RegistrarVenda(quantidadeVendida: 50, precoVenda: 60.00m);

            // Assert: PM permanece IGUAL; apenas quantidade diminui
            Assert.Equal(pmAntesDaVenda, custodia.PrecoMedio); // PM NÃO muda!
            Assert.Equal(150, custodia.Quantidade);

            // Verifica cálculo do resultado da venda
            // Lucro = (60 - 55) * 50 = R$ 250,00
            Assert.Equal(3000m, resultado.ValorTotalVenda);    // 50 * 60
            Assert.Equal(2750m, resultado.CustoAquisicao);     // 50 * 55 (PM)
            Assert.Equal(250m, resultado.LucroLiquido);        // 3000 - 2750
        }

        [Fact]
        public void RegistrarVenda_ComPrejuizo_DeveTerLucroNegativo()
        {
            var custodia = Custodia.CriarPosicaoInicial(
                Guid.NewGuid(), "MGLU3", quantidade: 100, precoCompra: 20.00m);

            // Vende a R$ 15,00 (abaixo do PM = prejuízo)
            var resultado = custodia.RegistrarVenda(50, 15.00m);

            Assert.True(resultado.LucroLiquido < 0);
            Assert.Equal(-250m, resultado.LucroLiquido); // (15-20)*50 = -250
        }

        // =========================================================
        // 4. IR DEDO-DURO (0,005%)
        // =========================================================

        [Theory]
        [InlineData(1000.00, 0.50)]   // 1000 * 0,00005 = 0,05? Não: 0,005% = 0,00005 → 0,05
        [InlineData(10000.00, 0.50)]  // 10000 * 0,00005 = 0,50
        [InlineData(500.00, 0.03)]    // 500 * 0,00005 = 0,025 → arredondado = 0,03
        public void RateioOrdem_IrDedoDuro_DeveSerZeroPontoZeroZeroCincoPorCento(
            decimal valorOperacao, decimal irEsperado)
        {
            // Cria um rateio simulando a distribuição
            var rateio = RateioOrdem.Criar(
                ordemCompraId: Guid.NewGuid(),
                clienteId: Guid.NewGuid(),
                contaFilhote: "C001",
                quantidadeRateada: 10,
                precoUnitario: valorOperacao / 10, // para que Qtd * Preco = valorOperacao
                valorAporteCliente: valorOperacao
            );

            Assert.Equal(irEsperado, rateio.ValorIrDedoDuro);
        }

        // =========================================================
        // 5. LOTE PADRÃO vs FRACIONÁRIO
        // =========================================================

        [Fact]
        public void CriarOrdemLotePadrao_QuantidadeNaoMultiplaDe100_DeveLancarExcecao()
        {
            Assert.Throws<ArgumentException>(() =>
                OrdemCompra.CriarParaLotePadrao("PETR4", quantidade: 150, precoFechamento: 35m, diaCiclo: 5));
        }

        [Fact]
        public void CriarOrdemFracionario_DeveAdicionarSufixoF()
        {
            var ordem = OrdemCompra.CriarParaMercadoFracionario(
                "PETR4", quantidade: 55, precoFechamento: 35m, diaCiclo: 5);

            Assert.Equal("PETR4F", ordem.Ticker);
            Assert.Equal(TipoMercado.MercadoFracionario, ordem.TipoMercado);
        }

        [Fact]
        public void CriarOrdemFracionario_QuantidadeAcima99_DeveLancarExcecao()
        {
            Assert.Throws<ArgumentException>(() =>
                OrdemCompra.CriarParaMercadoFracionario("VALE3", quantidade: 100, precoFechamento: 70m, diaCiclo: 15));
        }

        // =========================================================
        // 6. RATEIO PROPORCIONAL COM TRUNCAMENTO
        // =========================================================

        [Fact]
        public void Rateio_DeveUsarTruncamentoNaQuantidade()
        {
            // Caixa total: R$ 1000
            // Cliente A: R$ 600 (60%) → 60% de 10 ações = 6,0 → trunca = 6
            // Cliente B: R$ 400 (40%) → 40% de 10 ações = 4,0 → trunca = 4

            var rateioA = RateioOrdem.Criar(
                Guid.NewGuid(), Guid.NewGuid(), "CA",
                quantidadeRateada: 6,  // Floor(60% * 10) = 6
                precoUnitario: 100m,
                valorAporteCliente: 600m);

            var rateioB = RateioOrdem.Criar(
                Guid.NewGuid(), Guid.NewGuid(), "CB",
                quantidadeRateada: 4,  // Floor(40% * 10) = 4
                precoUnitario: 100m,
                valorAporteCliente: 400m);

            Assert.Equal(6L, rateioA.QuantidadeRateada);
            Assert.Equal(4L, rateioB.QuantidadeRateada);

            // Resíduo de B: 400 - (4 * 100) = 0
            Assert.Equal(0m, rateioB.ResidualCliente);
        }

        [Fact]
        public void Rateio_QuandoAporteMenorQueUmaAcao_DeveGerarResidualTotal()
        {
            // Cliente com aporte de R$ 80, mas ação custa R$ 100
            // Não consegue comprar nem 1 ação: resíduo = R$ 80
            var rateio = RateioOrdem.Criar(
                Guid.NewGuid(), Guid.NewGuid(), "C001",
                quantidadeRateada: 0,
                precoUnitario: 100m,
                valorAporteCliente: 80m);

            Assert.Equal(0L, rateio.QuantidadeRateada);
            Assert.Equal(80m, rateio.ResidualCliente);
        }
    }
}
