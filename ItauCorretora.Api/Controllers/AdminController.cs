using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ItauCorretora.Application.Interfaces;

namespace ItauCorretora.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IRebalanceamentoService _rebalanceamentoService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IRebalanceamentoService rebalanceamentoService,
            ILogger<AdminController> logger)
        {
            _rebalanceamentoService = rebalanceamentoService;
            _logger = logger;
        }

        /// <summary>
        /// Cadastra uma nova Cesta Top Five e dispara o rebalanceamento automático.
        /// </summary>
        [HttpPost("cesta")]
        public async Task<IActionResult> CadastrarCesta([FromBody] NovaCestaRequest request, CancellationToken ct)
        {
            _logger.LogInformation("Recebida requisição para cadastrar nova Cesta Top Five: {Nome}", request.Nome);

            try
            {
                // Validação Básica (RN-014 e RN-015)
                if (request.Itens.Count != 5)
                    return BadRequest(new { erro = "A cesta deve conter exatamente 5 ativos.", codigo = "QUANTIDADE_ATIVOS_INVALIDA" });

                decimal somaPercentual = 0;
                foreach(var item in request.Itens) somaPercentual += item.Percentual;
                
                if (somaPercentual != 100m)
                    return BadRequest(new { erro = $"A soma dos percentuais deve ser 100%. Soma atual: {somaPercentual}%.", codigo = "PERCENTUAIS_INVALIDOS" });

                _logger.LogInformation("Nova cesta validada e salva no banco de dados.");

                _logger.LogInformation("Disparando o Motor de Rebalanceamento para os clientes...");
                await _rebalanceamentoService.ExecutarRebalanceamentoPorMudancaCestaAsync(ct);

                return Created("", new 
                { 
                    mensagem = "Cesta atualizada. Rebalanceamento disparado para os clientes ativos.",
                    rebalanceamentoDisparado = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao cadastrar cesta e disparar rebalanceamento.");
                return StatusCode(500, new { erro = "Erro interno no servidor.", codigo = "ERRO_INTERNO" });
            }
        }
    }

    public class NovaCestaRequest
    {
        public string Nome { get; set; } = string.Empty;
        public List<ItemCestaRequest> Itens { get; set; } = new();
    }

    public class ItemCestaRequest
    {
        public string Ticker { get; set; } = string.Empty;
        public decimal Percentual { get; set; }
    }
}