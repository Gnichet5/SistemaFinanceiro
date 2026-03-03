namespace ItauCorretora.Application.Interfaces
{
    public interface IRebalanceamentoService
    {
        Task ExecutarRebalanceamentoPorMudancaCestaAsync(CancellationToken ct = default);
    }
}