using Microsoft.EntityFrameworkCore;
using ItauCorretora.Domain.Entities; 
using ItauCorretora.Domain.Interfaces; 
using ItauCorretora.Infrastructure.Data;

namespace ItauCorretora.Infrastructure.Repositories
{
    public class CustodiaRepository : ICustodiaRepository
    {
        private readonly ApplicationDbContext _context;
        public CustodiaRepository(ApplicationDbContext context) => _context = context;

        public async Task<Custodia?> ObterPorClienteETickerAsync(Guid clienteId, string ticker, CancellationToken ct = default) =>
            await _context.Custodias.FirstOrDefaultAsync(c => c.ClienteId == clienteId && c.Ticker == ticker, ct);

        public async Task<IEnumerable<Custodia>> ObterPorClienteAsync(Guid clienteId, CancellationToken ct = default) =>
            await _context.Custodias.Where(c => c.ClienteId == clienteId).ToListAsync(ct);

        public async Task SalvarAsync(Custodia custodia, CancellationToken ct = default)
        {
            await _context.Custodias.AddAsync(custodia, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task AtualizarAsync(Custodia custodia, CancellationToken ct = default)
        {
            _context.Custodias.Update(custodia);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<decimal> SomarVendasDoMesAsync(Guid clienteId, int mes, int ano, CancellationToken ct = default)
        {
            return await _context.Set<RateioOrdem>()
                .Where(r => r.ClienteId == clienteId)
                .Where(r => _context.OrdensCompra.Any(o => o.Id == r.OrdemCompraId && 
                                                        o.DataExecucao.Month == mes && 
                                                        o.DataExecucao.Year == ano))
                .SumAsync(r => r.ValorFinanceiroRateio, ct);
        }
    }
}