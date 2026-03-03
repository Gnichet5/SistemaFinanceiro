using Microsoft.EntityFrameworkCore;
using ItauCorretora.Domain.Entities;
using ItauCorretora.Domain.Interfaces;
using ItauCorretora.Infrastructure.Data;

namespace ItauCorretora.Infrastructure.Repositories
{
    public class ClienteRepository : IClienteRepository
    {
        private readonly ApplicationDbContext _context;
        public ClienteRepository(ApplicationDbContext context) => _context = context;

        public async Task<IEnumerable<Cliente>> ObterClientesAtivosAsync(CancellationToken ct = default) =>
            await _context.Clientes.Include(c => c.Custodias).Where(c => c.Status == StatusCliente.Ativo).ToListAsync(ct);

        public async Task<Cliente?> ObterPorIdAsync(Guid id, CancellationToken ct = default) =>
            await _context.Clientes.Include(c => c.Custodias).FirstOrDefaultAsync(c => c.Id == id, ct);

        public async Task SalvarAsync(Cliente cliente, CancellationToken ct = default)
        {
            await _context.Clientes.AddAsync(cliente, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task AtualizarAsync(Cliente cliente, CancellationToken ct = default)
        {
            _context.Clientes.Update(cliente);
            await _context.SaveChangesAsync(ct);
        }
    }
}