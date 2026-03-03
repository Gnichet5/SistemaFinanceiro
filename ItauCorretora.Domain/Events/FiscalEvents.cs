using System;

namespace ItauCorretora.Domain.Events
{
    /// <summary>
    /// Evento de Domínio: IR Dedo-Duro
    /// Publicado no Kafka após cada distribuição de ações para uma conta filhote.
    /// Alíquota: 0,005% sobre o valor bruto da operação.
    /// Tópico Kafka: "fiscal.ir.dedoduro"
    /// </summary>
    public record IrDedoDuroEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string Ticker { get; init; } = string.Empty;
        public Guid ClienteId { get; init; }
        public string ContaFilhote { get; init; } = string.Empty;
        public long QuantidadeOperacao { get; init; }
        public decimal ValorBrutoOperacao { get; init; }   // Qtd * Preco
        public decimal BaseCalculo { get; init; }          // = ValorBrutoOperacao
        public decimal AliquotaPercentual { get; init; } = 0.005m; // 0,005%
        public decimal ValorIr { get; init; }              // 0,005% * ValorBrutoOperacao
        public string TipoEvento { get; init; } = "DEDO_DURO_COMPRA";
    }

    /// <summary>
    /// Evento de Domínio: IR sobre Lucro em Vendas (Rebalanceamento)
    /// Publicado quando o total de vendas no mês supera R$ 20.000,00.
    /// Alíquota: 20% sobre o lucro líquido.
    /// Tópico Kafka: "fiscal.ir.venda"
    /// </summary>
    public record IrVendaEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public Guid ClienteId { get; init; }
        public string ContaFilhote { get; init; } = string.Empty;
        public int Mes { get; init; }
        public int Ano { get; init; }
        public decimal TotalVendasMes { get; init; }         // Soma de todas as vendas no mês
        public decimal TotalCustoAquisicao { get; init; }    // Soma dos custos (Qtd * PM)
        public decimal LucroLiquidoMes { get; init; }        // TotalVendas - TotalCusto
        public decimal AliquotaPercentual { get; init; } = 20m; // 20% sobre lucro
        public decimal ValorIrApurado { get; init; }         // 20% * LucroLiquido
        public bool IsencaoAplicada { get; init; }           // true se vendas <= 20.000
        public string TipoEvento { get; init; } = "IR_VENDA_REBALANCEAMENTO";
    }

    /// <summary>
    /// Evento de Domínio: Compra Consolidada Executada
    /// Publicado quando a Conta Master realiza a compra no mercado.
    /// Tópico Kafka: "operacoes.compra.executada"
    /// </summary>
    public record CompraExecutadaEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public Guid OrdemCompraId { get; init; }
        public string Ticker { get; init; } = string.Empty;
        public long QuantidadeTotal { get; init; }
        public decimal PrecoUnitario { get; init; }
        public decimal ValorTotal { get; init; }
        public string TipoMercado { get; init; } = string.Empty; // "LOTE_PADRAO" ou "FRACIONARIO"
        public int DiaCiclo { get; init; }
    }
}
