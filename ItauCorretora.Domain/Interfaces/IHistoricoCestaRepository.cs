using System.Collections.Generic;
using System.Threading.Tasks;
using ItauCorretora.Domain.Entities; // <-- ESSA LINHA É A CHAVE!

namespace ItauCorretora.Domain.Interfaces
{
    public interface IHistoricoCestaRepository
    {
        Task SalvarAsync(HistoricoCesta historico);
        Task<IEnumerable<HistoricoCesta>> ObterTodosAsync();
    }
}