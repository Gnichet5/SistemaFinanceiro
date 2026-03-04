using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ItauCorretora.Application.Interfaces;
using ItauCorretora.Domain.Interfaces;
using ItauCorretora.Domain.Entities;

namespace ItauCorretora.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientesController : ControllerBase
    {
        private readonly IClienteRepository _clienteRepository;
        private readonly IRentabilidadeService _rentabilidadeService;
        private readonly ILogger<ClientesController> _logger;

        public ClientesController(
            IClienteRepository clienteRepository,
            IRentabilidadeService rentabilidadeService,
            ILogger<ClientesController> logger)
        {
            _clienteRepository = clienteRepository;
            _rentabilidadeService = rentabilidadeService;
            _logger = logger;
        }

        /// <summary>
        /// Aderir ao Produto (RN-001 a RN-006)
        /// </summary>
        [HttpPost("adesao")]
        public async Task<IActionResult> Aderir([FromBody] AdesaoRequest request, CancellationToken ct)
        {
            _logger.LogInformation("Recebida requisição de adesão para o CPF {Cpf}", request.Cpf);

            if (request.ValorMensal < 100m)
                return BadRequest(new { erro = "O valor mensal minimo e de R$ 100,00.", codigo = "VALOR_MENSAL_INVALIDO" });

            try
            {
                // Criamos a conta filhote fictícia e usamos o seu método de fábrica!
                var contaFilhote = $"AG1234-C{new Random().Next(1000, 9999)}";
                var cliente = Cliente.Criar(request.Nome, request.Cpf, contaFilhote, request.ValorMensal);
                
                // Salvando de verdade no banco de dados
                await _clienteRepository.SalvarAsync(cliente, ct);

                return StatusCode(201, new 
                { 
                    mensagem = "Adesão realizada com sucesso.",
                    clienteId = cliente.Id,
                    contaFilhote = cliente.ContaFilhote,
                    valorMensal = cliente.AporteMensal,
                    ativo = cliente.EstaAtivo(),
                    dataAdesao = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao realizar adesão do cliente.");
                return StatusCode(500, new { erro = "Erro interno do servidor.", codigo = "ERRO_INTERNO" });
            }
        }

       [HttpGet("dashboard/{cpf}")]
        public async Task<IActionResult> GetDashboardPorCpf(string cpf, CancellationToken ct)
        {
            _logger.LogInformation("Buscando dashboard para o CPF {Cpf}", cpf);

            var clientes = await _clienteRepository.ObterClientesAtivosAsync(ct);
            var cliente = clientes.FirstOrDefault(c => c.Cpf == cpf);

            if (cliente == null)
                return NotFound(new { mensagem = "Cliente não encontrado com este CPF." });

                try
                    {
                        var rentabilidadeResponse = await _rentabilidadeService.CalcularRentabilidadeAsync(cliente.Id, ct);
                        return Ok(new 
                        {
                            nome = cliente.Nome,
                            valorTotalInvestido = rentabilidadeResponse.Rentabilidade.ValorTotalInvestido,
                            saldoResidual = cliente.SaldoResidual,
                            rentabilidadeTotal = rentabilidadeResponse.Rentabilidade.RentabilidadePercentual,
                            custodia = rentabilidadeResponse.Ativos.Select(a => new {
                                ticker = a.Ticker,
                                quantidade = a.Quantidade,
                                precoMedio = a.PrecoMedio,
                                valorAtual = a.ValorAtual
                            })
                        });
                    }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao calcular dashboard para o CPF {Cpf}", cpf);
                return StatusCode(500, new { erro = "Erro ao processar dados do dashboard." });
            }
        }

        [HttpPost("{clienteId}/saida")]
        public async Task<IActionResult> Sair(Guid clienteId, CancellationToken ct)
        {
            _logger.LogInformation("Recebida solicitação de saída para o cliente {ClienteId}", clienteId);
            
            var cliente = await _clienteRepository.ObterPorIdAsync(clienteId, ct);
            if (cliente == null)
                return NotFound(new { erro = "Cliente não encontrado.", codigo = "CLIENTE_NAO_ENCONTRADO" });

            // Usando o comportamento rico da sua entidade
            cliente.Inativar();
            await _clienteRepository.AtualizarAsync(cliente, ct);

            return Ok(new 
            { 
                clienteId = clienteId,
                ativo = cliente.EstaAtivo(),
                dataSaida = DateTime.UtcNow,
                mensagem = "Adesao encerrada. Sua posicao em custodia foi mantida." 
            });
        }

        [HttpPut("{clienteId}/valor-mensal")]
        public async Task<IActionResult> AlterarValorMensal(Guid clienteId, [FromBody] AlterarValorRequest request, CancellationToken ct)
        {
            _logger.LogInformation("Alterando valor do cliente {ClienteId} para {NovoValor}", clienteId, request.NovoValorMensal);
            
            if (request.NovoValorMensal < 100m)
                return BadRequest(new { erro = "O valor mensal minimo e de R$ 100,00.", codigo = "VALOR_MENSAL_INVALIDO" });

            var cliente = await _clienteRepository.ObterPorIdAsync(clienteId, ct);
            if (cliente == null)
                return NotFound(new { erro = "Cliente não encontrado.", codigo = "CLIENTE_NAO_ENCONTRADO" });

            // Validando e alterando através da entidade
            cliente.AlterarAporteMensal(request.NovoValorMensal);
            await _clienteRepository.AtualizarAsync(cliente, ct);

            return Ok(new 
            { 
                clienteId = clienteId,
                valorMensalNovo = cliente.AporteMensal,
                dataAlteracao = DateTime.UtcNow,
                mensagem = "Valor mensal atualizado. O novo valor sera considerado a partir da proxima data de compra."
            });
        }
        [HttpGet("{clienteId}/rentabilidade")]
        public async Task<IActionResult> ObterRentabilidade(Guid clienteId, CancellationToken ct)
        {
            _logger.LogInformation("Recebida requisição GET para rentabilidade do cliente {ClienteId}", clienteId);
            try
            {
                var response = await _rentabilidadeService.CalcularRentabilidadeAsync(clienteId, ct);
                return Ok(response);
            }
            catch (Exception ex) when (ex.Message.Contains("CLIENTE_NAO_ENCONTRADO"))
            {
                return NotFound(new { erro = "Cliente nao encontrado.", codigo = "CLIENTE_NAO_ENCONTRADO" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar rentabilidade do cliente {ClienteId}", clienteId);
                return StatusCode(500, new { erro = "Erro interno ao processar a rentabilidade.", codigo = "ERRO_INTERNO" });
            }
        }
    }

    // DTOs para o Controller
    public class AdesaoRequest
    {
        public string Nome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal ValorMensal { get; set; }
    }

    public class AlterarValorRequest
    {
        public decimal NovoValorMensal { get; set; }
    }
}