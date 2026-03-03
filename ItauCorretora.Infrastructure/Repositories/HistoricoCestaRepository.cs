using ItauCorretora.Domain.Entities;
using ItauCorretora.Domain.Interfaces;
using ItauCorretora.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ItauCorretora.Infrastructure.Repositories
{
    public class HistoricoCestaRepository : IHistoricoCestaRepository
    {
        private readonly ApplicationDbContext _context;
        public HistoricoCestaRepository(ApplicationDbContext context) => _context = context;

        public async Task SalvarAsync(HistoricoCesta historico)
        {
            await _context.HistoricoCestas.AddAsync(historico);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<HistoricoCesta>> ObterTodosAsync() =>
            await _context.HistoricoCestas.OrderByDescending(x => x.DataCriacao).ToListAsync();
    }
}