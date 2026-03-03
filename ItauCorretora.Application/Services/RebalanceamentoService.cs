using ItauCorretora.Application.Interfaces;
using ItauCorretora.Domain.Interfaces;
using ItauCorretora.Domain.Events; // Necessário para o IrVendaEvent
using Microsoft.Extensions.Logging;

namespace ItauCorretora.Application.Services
{
    public class RebalanceamentoService : IRebalanceamentoService
    {
        private readonly IClienteRepository _clienteRepository;
        private readonly ICustodiaRepository _custodiaRepository;
        private readonly IOrdemCompraRepository _cestaRepository; // Usando a interface que você já possui
        private readonly ICotacaoService _cotacaoService; // Injetando o serviço de cotação
        private readonly IEventPublisher _eventPublisher; 
        private readonly ILogger<RebalanceamentoService> _logger;

        public RebalanceamentoService(
            IClienteRepository clienteRepository,
            ICustodiaRepository custodiaRepository,
            IOrdemCompraRepository cestaRepository,
            ICotacaoService cotacaoService,
            IEventPublisher eventPublisher,
            ILogger<RebalanceamentoService> logger)
        {
            _clienteRepository = clienteRepository;
            _custodiaRepository = custodiaRepository;
            _cestaRepository = cestaRepository;
            _cotacaoService = cotacaoService;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        public async Task ExecutarRebalanceamentoPorMudancaCestaAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Iniciando rebalanceamento por mudança de cesta.");

            // Pegando a data de hoje para as cotações
            var dataHoje = DateTime.Now;

            var clientes = await _clienteRepository.ObterClientesAtivosAsync(ct);

            foreach (var cliente in clientes)
            {
                var custodias = await _custodiaRepository.ObterPorClienteAsync(cliente.Id, ct);
                
                // Buscando a soma real das vendas no mês (vamos ajustar o repo logo abaixo)
                decimal valorVendidoNoMes = await _custodiaRepository.SomarVendasDoMesAsync(cliente.Id, dataHoje.Month, dataHoje.Year, ct);
                
                decimal lucroAcumuladoNestaOperacao = 0;
                decimal totalVendaOperacao = 0;

                foreach (var custodia in custodias)
                {
                    // Lógica simplificada: Se não estiver na nova cesta, vende
                    // (Aqui você implementará a lógica de comparação de tickers)
                    
                    var precoFechamento = await _cotacaoService.ObterPrecoFechamentoAsync(custodia.Ticker, dataHoje, ct);
                    
                    if (precoFechamento.HasValue)
                    {
                        var valorVenda = custodia.Quantidade * precoFechamento.Value;
                        var custoAquisicao = custodia.Quantidade * custodia.PrecoMedio;
                        
                        lucroAcumuladoNestaOperacao += (valorVenda - custoAquisicao);
                        totalVendaOperacao += valorVenda;
                    }
                }

                // RN-059: Regra dos R$ 20.000,00
                if ((valorVendidoNoMes + totalVendaOperacao) > 20000m && lucroAcumuladoNestaOperacao > 0)
                {
                    await _eventPublisher.PublicarIrVendaAsync(new IrVendaEvent 
                    {
                        ClienteId = cliente.Id,
                        ContaFilhote = cliente.ContaFilhote,
                        Mes = dataHoje.Month,
                        Ano = dataHoje.Year,
                        LucroLiquidoMes = lucroAcumuladoNestaOperacao,
                        ValorIrApurado = lucroAcumuladoNestaOperacao * 0.20m,
                        TotalVendasMes = valorVendidoNoMes + totalVendaOperacao
                    }, ct);
                }
            }
        }
    }
}