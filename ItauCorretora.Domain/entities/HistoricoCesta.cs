using System;

namespace ItauCorretora.Domain.Entities
{
    public class HistoricoCesta
    {
        public Guid Id { get; private set; }
        public DateTime DataCriacao { get; private set; }
        
        // Inicializamos com string.Empty para resolver o Warning CS8618
        public string Tickers { get; private set; } = string.Empty; 

        // Construtor privado para o EF
        private HistoricoCesta() { }

        public HistoricoCesta(string tickers)
        {
            Id = Guid.NewGuid();
            DataCriacao = DateTime.UtcNow;
            Tickers = tickers;
        }
    }
}