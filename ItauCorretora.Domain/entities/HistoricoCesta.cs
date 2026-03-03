namespace ItauCorretora.Domain.Entities
{
    public class HistoricoCesta
    {
        public Guid Id { get; private set; }
        public DateTime DataCriacao { get; private set; }
        public string Tickers { get; private set; }

        private HistoricoCesta() { }

        public HistoricoCesta(string tickers)
        {
            Id = Guid.NewGuid();
            DataCriacao = DateTime.UtcNow;
            Tickers = tickers;
        }
    }
}