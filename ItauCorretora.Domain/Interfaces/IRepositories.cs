using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ItauCorretora.Domain.Entities;
using ItauCorretora.Domain.Events;

namespace ItauCorretora.Domain.Interfaces
{
    // =========================================================
    // REPOSITÓRIOS (Contratos de Persistência)
    // =========================================================

    /// <summary>
    /// Contrato de repositório para a entidade Cliente.
    /// Implementado na camada de Infrastructure (MySQL).
    /// </summary>
    public interface IClienteRepository
    {
        Task<IEnumerable<Cliente>> ObterClientesAtivosAsync(CancellationToken ct = default);
        Task<Cliente?> ObterPorIdAsync(Guid clienteId, CancellationToken ct = default);
        Task SalvarAsync(Cliente cliente, CancellationToken ct = default);
        Task AtualizarAsync(Cliente cliente, CancellationToken ct = default);
    }

    /// <summary>
    /// Contrato de repositório para a entidade Custodia.
    /// </summary>
    public interface ICustodiaRepository
    {
        Task<Custodia?> ObterPorClienteETickerAsync(Guid clienteId, string ticker, CancellationToken ct = default);
        Task<IEnumerable<Custodia>> ObterPorClienteAsync(Guid clienteId, CancellationToken ct = default);
        Task SalvarAsync(Custodia custodia, CancellationToken ct = default);
        Task AtualizarAsync(Custodia custodia, CancellationToken ct = default);
        Task<decimal> SomarVendasDoMesAsync(Guid clienteId, int mes, int ano, CancellationToken ct = default);
    }

    /// <summary>
    /// Contrato de repositório para a entidade OrdemCompra.
    /// </summary>
    public interface IOrdemCompraRepository
    {
        Task SalvarAsync(OrdemCompra ordem, CancellationToken ct = default);
        Task AtualizarAsync(OrdemCompra ordem, CancellationToken ct = default);
        Task<IEnumerable<OrdemCompra>> ObterPorDataAsync(DateTime data, CancellationToken ct = default);
    }

    // =========================================================
    // SERVIÇOS DE INFRAESTRUTURA
    // =========================================================

    /// <summary>
    /// Contrato para publicação de eventos no Kafka.
    /// Implementado na camada de Infrastructure.
    /// </summary>
    public interface IEventPublisher
    {
        Task PublicarIrDedoDuroAsync(IrDedoDuroEvent evento, CancellationToken ct = default);
        Task PublicarIrVendaAsync(IrVendaEvent evento, CancellationToken ct = default);
        Task PublicarCompraExecutadaAsync(CompraExecutadaEvent evento, CancellationToken ct = default);
    }

    /// <summary>
    /// Contrato para obtenção da cotação de fechamento do ativo.
    /// Pode ser alimentado pelo parser COTAHIST ou por uma API de mercado.
    /// </summary>
    public interface ICotacaoService
    {
        /// <summary>
        /// Retorna o preço de fechamento do ativo na data especificada.
        /// </summary>
        Task<decimal?> ObterPrecoFechamentoAsync(string ticker, DateTime data, CancellationToken ct = default);
    }

    /// <summary>
    /// Contrato para o serviço de IR — cálculos fiscais.
    /// </summary>
    public interface ICalculoIrService
    {
        /// <summary>
        /// Verifica e gera o evento de IR sobre vendas se o limite mensal for superado.
        /// Limite: R$ 20.000,00 em vendas no mês.
        /// Alíquota: 20% sobre o lucro líquido.
        /// </summary>
        Task ProcessarIrVendaMensalAsync(
            Guid clienteId,
            ResultadoVenda resultadoVenda,
            CancellationToken ct = default);
    }
}
