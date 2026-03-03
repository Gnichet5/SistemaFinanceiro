using Microsoft.EntityFrameworkCore;
using ItauCorretora.Domain.Entities;
using ItauCorretora.Domain.Interfaces;
using ItauCorretora.Infrastructure.Data;

namespace ItauCorretora.Infrastructure.Repositories
{
    public class OrdemCompraRepository : IOrdemCompraRepository
    {
        private readonly ApplicationDbContext _context;
        public OrdemCompraRepository(ApplicationDbContext context) => _context = context;

        public async Task SalvarAsync(OrdemCompra ordem, CancellationToken ct = default)
        {
            await _context.OrdensCompra.AddAsync(ordem, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task AtualizarAsync(OrdemCompra ordem, CancellationToken ct = default)
        {
            _context.OrdensCompra.Update(ordem);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<IEnumerable<OrdemCompra>> ObterPorDataAsync(DateTime data, CancellationToken ct = default) =>
            await _context.OrdensCompra.Where(o => o.DataExecucao.Date == data.Date).ToListAsync(ct);
    }
}