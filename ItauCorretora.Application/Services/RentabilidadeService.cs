using ItauCorretora.Application.DTOs;
using ItauCorretora.Application.Interfaces;
using ItauCorretora.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ItauCorretora.Application.Services
{
    public class RentabilidadeService : IRentabilidadeService
    {
        private readonly IClienteRepository _clienteRepository;
        private readonly ICustodiaRepository _custodiaRepository;
        private readonly ICotacaoService _cotacaoService; // <-- Usando a Interface correta da Arquitetura Limpa
        private readonly ILogger<RentabilidadeService> _logger;

        public RentabilidadeService(
            IClienteRepository clienteRepository,
            ICustodiaRepository custodiaRepository,
            ICotacaoService cotacaoService,
            ILogger<RentabilidadeService> logger)
        {
            _clienteRepository = clienteRepository;
            _custodiaRepository = custodiaRepository;
            _cotacaoService = cotacaoService;
            _logger = logger;
        }

        public async Task<RentabilidadeResponse> CalcularRentabilidadeAsync(Guid clienteId, CancellationToken ct = default)
        {
            _logger.LogInformation("Calculando rentabilidade para o cliente {ClienteId}", clienteId);

            var cliente = await _clienteRepository.ObterPorIdAsync(clienteId, ct);
            if (cliente == null)
                throw new Exception("CLIENTE_NAO_ENCONTRADO"); 

            var custodias = await _custodiaRepository.ObterPorClienteAsync(clienteId, ct);

            var response = new RentabilidadeResponse
            {
                ClienteId = cliente.Id,
                Nome = cliente.Nome,
                DataConsulta = DateTime.UtcNow
            };

            decimal valorTotalInvestido = 0;
            decimal valorAtualCarteira = 0;
            var dataHoje = DateTime.Now;

            foreach (var custodia in custodias)
            {
                // Usando o serviço abstraído ao invés do parser direto
                decimal? precoFechamento = await _cotacaoService.ObterPrecoFechamentoAsync(custodia.Ticker, dataHoje, ct);
                decimal cotacaoAtual = precoFechamento ?? custodia.PrecoMedio; 

                decimal valorInvestidoAtivo = custodia.Quantidade * custodia.PrecoMedio;
                decimal valorAtualAtivo = custodia.Quantidade * cotacaoAtual;
                decimal plAtivo = valorAtualAtivo - valorInvestidoAtivo; 

                valorTotalInvestido += valorInvestidoAtivo;
                valorAtualCarteira += valorAtualAtivo;

                response.Ativos.Add(new AtivoRentabilidade
                {
                    Ticker = custodia.Ticker,
                    Quantidade = custodia.Quantidade,
                    PrecoMedio = custodia.PrecoMedio,
                    CotacaoAtual = cotacaoAtual,
                    ValorAtual = valorAtualAtivo,
                    Pl = plAtivo
                });
            }

            response.Rentabilidade.ValorTotalInvestido = valorTotalInvestido;
            response.Rentabilidade.ValorAtualCarteira = valorAtualCarteira;
            response.Rentabilidade.PlTotal = valorAtualCarteira - valorTotalInvestido;

            if (valorTotalInvestido > 0)
            {
                response.Rentabilidade.RentabilidadePercentual = Math.Round(((valorAtualCarteira - valorTotalInvestido) / valorTotalInvestido) * 100m, 2);
            }

            if (valorAtualCarteira > 0)
            {
                foreach (var ativo in response.Ativos)
                {
                    ativo.ComposicaoCarteira = Math.Round((ativo.ValorAtual / valorAtualCarteira) * 100m, 2);
                }
            }

            return response;
        }
    }
}