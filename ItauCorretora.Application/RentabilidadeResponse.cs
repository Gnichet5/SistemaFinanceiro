namespace ItauCorretora.Application.DTOs
{
    public class RentabilidadeResponse
    {
        public Guid ClienteId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public DateTime DataConsulta { get; set; }
        public RentabilidadeResumo Rentabilidade { get; set; } = new();
        public List<AtivoRentabilidade> Ativos { get; set; } = new();
    }

    public class RentabilidadeResumo
    {
        public decimal ValorTotalInvestido { get; set; }
        public decimal ValorAtualCarteira { get; set; }
        public decimal PlTotal { get; set; }
        public decimal RentabilidadePercentual { get; set; }
    }

public class AtivoRentabilidade
    {
        public string Ticker { get; set; } = string.Empty;
        public long Quantidade { get; set; } 
        public decimal PrecoMedio { get; set; }
        public decimal CotacaoAtual { get; set; }
        public decimal ValorAtual { get; set; }
        public decimal Pl { get; set; }
        public decimal PlPercentual { get; set; }
        public decimal ComposicaoCarteira { get; set; }
    }
}