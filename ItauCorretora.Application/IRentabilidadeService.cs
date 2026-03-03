using ItauCorretora.Application.DTOs;

namespace ItauCorretora.Application.Interfaces
{
    public interface IRentabilidadeService
    {
        Task<RentabilidadeResponse> CalcularRentabilidadeAsync(Guid clienteId, CancellationToken ct = default);
    }
}