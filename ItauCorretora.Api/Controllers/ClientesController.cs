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

        [HttpPost("adesao")]
        public async Task<IActionResult> Aderir([FromBody] AdesaoRequest request, CancellationToken ct)
        {
            if (request.ValorMensal < 100m)
                return BadRequest(new { erro = "O valor mensal minimo e de R$ 100,00.", codigo = "VALOR_MENSAL_INVALIDO" });

            var contaFilhote = $"AG1234-C{new Random().Next(1000, 9999)}";
            var cliente = Cliente.Criar(request.Nome, request.Cpf, contaFilhote, request.ValorMensal);
            
            await _clienteRepository.SalvarAsync(cliente, ct);

            return StatusCode(201, new { mensagem = "Adesão realizada com sucesso.", clienteId = cliente.Id });
        }

        [HttpPost("{clienteId}/saida")]
        public async Task<IActionResult> Sair(Guid clienteId, CancellationToken ct)
        {
            var cliente = await _clienteRepository.ObterPorIdAsync(clienteId, ct);
            if (cliente == null) return NotFound(new { erro = "Cliente não encontrado." });

            cliente.Inativar();
            await _clienteRepository.AtualizarAsync(cliente, ct);

            return Ok(new { mensagem = "Adesao encerrada." });
        }

        [HttpGet("{clienteId}/rentabilidade")]
        public async Task<IActionResult> ObterRentabilidade(Guid clienteId, CancellationToken ct)
        {
            var response = await _rentabilidadeService.CalcularRentabilidadeAsync(clienteId, ct);
            return Ok(response);
        }
    }

    public class AdesaoRequest
    {
        public string Nome { get; set; } = string.Empty;
        public string Cpf { get; set; } = string.Empty;
        public decimal ValorMensal { get; set; }
    }
}