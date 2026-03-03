using System;

namespace ItauCorretora.Domain.Entities
{
    /// <summary>
    /// Entidade Rica: Custodia
    /// Representa a posição de um cliente em um determinado ativo (ação).
    /// Contém a lógica crítica de cálculo e atualização do Preço Médio (PM).
    ///
    /// REGRA CRÍTICA DE PM:
    ///   Na COMPRA: PM = (QtdAnt * PMAnt + QtdNova * PrecoNova) / (QtdAnt + QtdNova)
    ///   Na VENDA:  PM NÃO é alterado. Apenas a quantidade é reduzida.
    /// </summary>
    public class Custodia
    {
        public Guid Id { get; private set; }
        public Guid ClienteId { get; private set; }

        /// <summary>
        /// Ticker do ativo. Ex: "PETR4" para lote padrão, "PETR4F" para fracionário.
        /// </summary>
        public string Ticker { get; private set; }

        /// <summary>
        /// Quantidade total de ações custodiadas.
        /// </summary>
        public long Quantidade { get; private set; }

        /// <summary>
        /// Preço Médio de Aquisição — calculado apenas nas compras.
        /// Usado como base de custo para cálculo do IR sobre lucro.
        /// </summary>
        public decimal PrecoMedio { get; private set; }

        /// <summary>
        /// Data da última movimentação nesta custódia.
        /// </summary>
        public DateTime DataUltimaMovimentacao { get; private set; }

        private Custodia() { }

        /// <summary>
        /// Cria uma nova posição de custódia (primeira compra do ativo).
        /// </summary>
        public static Custodia CriarPosicaoInicial(Guid clienteId, string ticker, long quantidade, decimal precoCompra)
        {
            if (quantidade <= 0) throw new ArgumentException("Quantidade inicial deve ser positiva.");
            if (precoCompra <= 0) throw new ArgumentException("Preço de compra deve ser positivo.");

            return new Custodia
            {
                Id = Guid.NewGuid(),
                ClienteId = clienteId,
                Ticker = ticker.ToUpperInvariant(),
                Quantidade = quantidade,
                PrecoMedio = Math.Round(precoCompra, 6), // 6 casas para PM mais preciso
                DataUltimaMovimentacao = DateTime.UtcNow
            };
        }

        // =========================================================
        // OPERAÇÃO DE COMPRA — Atualiza o Preço Médio
        // =========================================================

        /// <summary>
        /// Registra a compra de novas ações e recalcula o Preço Médio (PM).
        ///
        /// FÓRMULA OFICIAL:
        ///   PM_novo = (QtdAnt * PMAnt + QtdNova * PrecoNova) / (QtdAnt + QtdNova)
        ///
        /// O PM sobe quando se compra caro e cai quando se compra barato (preço médio real).
        /// </summary>
        /// <param name="quantidadeComprada">Quantidade de ações adquiridas nesta operação.</param>
        /// <param name="precoCompra">Preço unitário pago nesta operação (preço de fechamento da B3).</param>
        public void RegistrarCompra(long quantidadeComprada, decimal precoCompra)
        {
            if (quantidadeComprada <= 0)
                throw new ArgumentException("Quantidade comprada deve ser positiva.", nameof(quantidadeComprada));
            if (precoCompra <= 0)
                throw new ArgumentException("Preço de compra deve ser positivo.", nameof(precoCompra));

            // Numerador: valor financeiro total acumulado
            var valorAcumuladoAnterior = Quantidade * PrecoMedio;
            var valorNovo = quantidadeComprada * precoCompra;

            var novaQuantidadeTotal = Quantidade + quantidadeComprada;

            // PM = (QtdAnt * PMAnt + QtdNova * PrecoNova) / (QtdAnt + QtdNova)
            PrecoMedio = Math.Round(
                (valorAcumuladoAnterior + valorNovo) / novaQuantidadeTotal,
                6, // 6 casas decimais para precisão máxima
                MidpointRounding.AwayFromZero
            );

            Quantidade = novaQuantidadeTotal;
            DataUltimaMovimentacao = DateTime.UtcNow;
        }

        // =========================================================
        // OPERAÇÃO DE VENDA — PM NÃO É ALTERADO
        // =========================================================

        /// <summary>
        /// Registra a venda de ações.
        /// REGRA CRÍTICA: O Preço Médio (PM) NÃO é recalculado na venda.
        /// Apenas a quantidade é reduzida.
        /// Retorna o lucro/prejuízo da operação para cálculo de IR.
        /// </summary>
        /// <param name="quantidadeVendida">Quantidade de ações vendidas.</param>
        /// <param name="precoVenda">Preço unitário de venda.</param>
        /// <returns>Lucro líquido da operação (pode ser negativo = prejuízo).</returns>
        public ResultadoVenda RegistrarVenda(long quantidadeVendida, decimal precoVenda)
        {
            if (quantidadeVendida <= 0)
                throw new ArgumentException("Quantidade vendida deve ser positiva.");
            if (quantidadeVendida > Quantidade)
                throw new InvalidOperationException(
                    $"Quantidade vendida ({quantidadeVendida}) maior que custódia ({Quantidade}) para {Ticker}.");
            if (precoVenda <= 0)
                throw new ArgumentException("Preço de venda deve ser positivo.");

            // Calcula o valor financeiro da venda
            var valorTotalVenda = quantidadeVendida * precoVenda;

            // Calcula o custo de aquisição (usando o PM atual — que NÃO muda)
            var custoAquisicao = quantidadeVendida * PrecoMedio;

            // Lucro líquido = Valor Venda - (Qtd * PM)
            var lucroLiquido = valorTotalVenda - custoAquisicao;

            // Atualiza APENAS a quantidade; PM permanece inalterado
            Quantidade -= quantidadeVendida;
            DataUltimaMovimentacao = DateTime.UtcNow;

            return new ResultadoVenda(
                Ticker,
                quantidadeVendida,
                precoVenda,
                valorTotalVenda,
                custoAquisicao,
                lucroLiquido
            );
        }

        /// <summary>
        /// Verifica se a custódia está zerada (posição liquidada).
        /// </summary>
        public bool EstaZerada() => Quantidade == 0;

        /// <summary>
        /// Calcula o valor de mercado atual da posição.
        /// </summary>
        public decimal CalcularValorMercado(decimal precoAtual) =>
            Math.Round(Quantidade * precoAtual, 2);

        /// <summary>
        /// Calcula o lucro/prejuízo latente (não realizado) baseado no preço atual.
        /// </summary>
        public decimal CalcularLucroLatente(decimal precoAtual) =>
            Math.Round((precoAtual - PrecoMedio) * Quantidade, 2);
    }

    /// <summary>
    /// Value Object: Resultado de uma operação de venda.
    /// Carrega as informações necessárias para cálculo de IR.
    /// </summary>
    public record ResultadoVenda(
        string Ticker,
        long QuantidadeVendida,
        decimal PrecoVenda,
        decimal ValorTotalVenda,       // Qtd * PrecoVenda
        decimal CustoAquisicao,        // Qtd * PM (base de custo)
        decimal LucroLiquido           // ValorVenda - CustoAquisicao
    );
}
