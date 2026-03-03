using System;
using System.Collections.Generic;

namespace ItauCorretora.Domain.Entities
{
    /// <summary>
    /// Entidade Rica: OrdemCompra
    /// Representa a ordem de compra consolidada gerada pelo motor no dia de execução.
    ///
    /// CONCEITO: A compra é realizada na Conta Master (corretora) e as ações resultantes
    /// são rateadas proporcionalmente para as Contas Filhote (clientes).
    ///
    /// Tipos de Mercado:
    ///   - Lote Padrão: múltiplos de 100 ações, ticker normal (ex: PETR4)
    ///   - Mercado Fracionário: 1 a 99 ações, ticker com sufixo "F" (ex: PETR4F)
    /// </summary>
    public class OrdemCompra
    {
        public Guid Id { get; private set; }
        public DateTime DataExecucao { get; private set; }
        public int DiaCiclo { get; private set; } // 5, 15 ou 25
        public string Ticker { get; private set; }
        public TipoMercado TipoMercado { get; private set; }
        public StatusOrdem Status { get; private set; }

        // --- Dados da Compra na Conta Master ---
        public long QuantidadeTotal { get; private set; }       // Total comprado na Master
        public decimal PrecoUnitario { get; private set; }      // Preço de fechamento B3
        public decimal ValorTotalOrdem { get; private set; }    // QuantidadeTotal * PrecoUnitario

        // --- Dados do Rateio para Contas Filhote ---
        private readonly List<RateioOrdem> _rateios = new();
        public IReadOnlyCollection<RateioOrdem> Rateios => _rateios.AsReadOnly();

        // --- Rastreabilidade ---
        public string? MotivoRejeicao { get; private set; }
        public DateTime? DataLiquidacao { get; private set; }

        private OrdemCompra() { }

        /// <summary>
        /// Cria uma ordem de compra para Lote Padrão.
        /// A quantidade deve ser múltipla de 100.
        /// O ticker é utilizado sem modificação.
        /// </summary>
        public static OrdemCompra CriarParaLotePadrao(
            string ticker,
            long quantidade,
            decimal precoFechamento,
            int diaCiclo)
        {
            if (quantidade <= 0 || quantidade % 100 != 0)
                throw new ArgumentException(
                    $"Lote Padrão exige quantidade múltipla de 100. Recebido: {quantidade}.");

            return new OrdemCompra
            {
                Id = Guid.NewGuid(),
                DataExecucao = DateTime.UtcNow,
                DiaCiclo = diaCiclo,
                Ticker = ticker.ToUpperInvariant(),
                TipoMercado = TipoMercado.LotePadrao,
                Status = StatusOrdem.Pendente,
                QuantidadeTotal = quantidade,
                PrecoUnitario = precoFechamento,
                ValorTotalOrdem = Math.Round(quantidade * precoFechamento, 2)
            };
        }

        /// <summary>
        /// Cria uma ordem de compra para Mercado Fracionário.
        /// A quantidade deve estar entre 1 e 99.
        /// O ticker é automaticamente sufixado com "F".
        /// </summary>
        public static OrdemCompra CriarParaMercadoFracionario(
            string ticker,
            long quantidade,
            decimal precoFechamento,
            int diaCiclo)
        {
            if (quantidade < 1 || quantidade > 99)
                throw new ArgumentException(
                    $"Mercado Fracionário exige quantidade entre 1 e 99. Recebido: {quantidade}.");

            // Garante o sufixo "F" no ticker fracionário
            var tickerFracionario = ticker.ToUpperInvariant().TrimEnd('F') + "F";

            return new OrdemCompra
            {
                Id = Guid.NewGuid(),
                DataExecucao = DateTime.UtcNow,
                DiaCiclo = diaCiclo,
                Ticker = tickerFracionario,
                TipoMercado = TipoMercado.MercadoFracionario,
                Status = StatusOrdem.Pendente,
                QuantidadeTotal = quantidade,
                PrecoUnitario = precoFechamento,
                ValorTotalOrdem = Math.Round(quantidade * precoFechamento, 2)
            };
        }

        // --- Comportamentos de Negócio ---

        /// <summary>
        /// Adiciona um rateio de ações para uma conta filhote específica.
        /// Valida que o total rateado não ultrapasse o total comprado.
        /// </summary>
        public void AdicionarRateio(RateioOrdem rateio)
        {
            var totalJaRateado = CalcularTotalRateado();
            if (totalJaRateado + rateio.QuantidadeRateada > QuantidadeTotal)
                throw new InvalidOperationException(
                    $"Rateio excede a quantidade total da ordem. " +
                    $"Total: {QuantidadeTotal}, Já rateado: {totalJaRateado}, " +
                    $"Tentando adicionar: {rateio.QuantidadeRateada}.");

            _rateios.Add(rateio);
        }

        public long CalcularTotalRateado() =>
            _rateios.Aggregate(0L, (acc, r) => acc + r.QuantidadeRateada);

        /// <summary>
        /// Calcula a quantidade residual na Conta Master após o rateio.
        /// Ações residuais permanecem na Master para abater no próximo ciclo.
        /// </summary>
        public long CalcularQuantidadeResidual() =>
            QuantidadeTotal - CalcularTotalRateado();

        public decimal CalcularValorResidual() =>
            Math.Round(CalcularQuantidadeResidual() * PrecoUnitario, 2);

        public void MarcarComoExecutada()
        {
            Status = StatusOrdem.Executada;
            DataLiquidacao = DateTime.UtcNow;
        }

        public void MarcarComoRejeitada(string motivo)
        {
            Status = StatusOrdem.Rejeitada;
            MotivoRejeicao = motivo;
        }

        public void MarcarComoRateada()
        {
            Status = StatusOrdem.Rateada;
        }
    }

    /// <summary>
    /// Value Object: Representa a distribuição de ações para uma Conta Filhote.
    /// O rateio usa TRUNCAMENTO (floor) para garantir que só inteiros sejam distribuídos.
    /// O resíduo financeiro volta para o saldo do cliente.
    /// </summary>
    public class RateioOrdem
    {
        public Guid OrdemCompraId { get; private set; }
        public Guid ClienteId { get; private set; }
        public string ContaFilhote { get; private set; }
        public long QuantidadeRateada { get; private set; }    // Ações distribuídas (truncadas)
        public decimal ValorFinanceiroRateio { get; private set; } // QuantidadeRateada * Preco
        public decimal ResidualCliente { get; private set; }   // Valor que sobrou (< 1 ação)
        public decimal ValorIrDedoDuro { get; private set; }   // 0,005% sobre ValorFinanceiroRateio

        private RateioOrdem() { }

        /// <summary>
        /// Cria um rateio para uma conta filhote.
        /// </summary>
        /// <param name="ordemCompraId">ID da ordem de compra consolidada.</param>
        /// <param name="clienteId">ID do cliente beneficiário.</param>
        /// <param name="contaFilhote">Código da conta filhote na corretora.</param>
        /// <param name="quantidadeRateada">Ações distribuídas (já truncadas).</param>
        /// <param name="precoUnitario">Preço unitário da ação (para calcular valor financeiro e IR).</param>
        /// <param name="valorAporteCliente">Aporte do cliente neste evento (para calcular resíduo).</param>
        public static RateioOrdem Criar(
            Guid ordemCompraId,
            Guid clienteId,
            string contaFilhote,
            long quantidadeRateada,
            decimal precoUnitario,
            decimal valorAporteCliente)
        {
            var valorFinanceiro = Math.Round(quantidadeRateada * precoUnitario, 2);

            // Resíduo = valor do aporte que não foi suficiente para mais 1 ação
            var residual = Math.Round(valorAporteCliente - valorFinanceiro, 2);
            if (residual < 0) residual = 0; // Proteção

            // IR Dedo-Duro: 0,005% sobre o valor financeiro da operação
            // Alíquota: 0,005% = 0.00005
            var irDedoDuro = Math.Round(valorFinanceiro * 0.00005m, 2);

            return new RateioOrdem
            {
                OrdemCompraId = ordemCompraId,
                ClienteId = clienteId,
                ContaFilhote = contaFilhote,
                QuantidadeRateada = quantidadeRateada,
                ValorFinanceiroRateio = valorFinanceiro,
                ResidualCliente = residual,
                ValorIrDedoDuro = irDedoDuro
            };
        }
    }

    public enum TipoMercado
    {
        LotePadrao = 1,       // Múltiplos de 100, ticker normal
        MercadoFracionario = 2 // 1 a 99, ticker + "F"
    }

    public enum StatusOrdem
    {
        Pendente = 1,
        Executada = 2,
        Rateada = 3,
        Rejeitada = 4
    }
}
