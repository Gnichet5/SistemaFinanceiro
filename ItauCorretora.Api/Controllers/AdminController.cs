using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ItauCorretora.Application.Interfaces;
using ItauCorretora.Domain.Interfaces;
using ItauCorretora.Domain.Entities;

namespace ItauCorretora.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IRebalanceamentoService _rebalanceamentoService;
        private readonly IHistoricoCestaRepository _historicoRepository;

        public AdminController(
            IRebalanceamentoService rebalanceamentoService, 
            IHistoricoCestaRepository historicoRepository)
        {
            _rebalanceamentoService = rebalanceamentoService;
            _historicoRepository = historicoRepository;
        }

        [HttpPost("cesta")]
        public async Task<IActionResult> CadastrarCesta([FromBody] NovaCestaRequest request, CancellationToken ct)
        {
            if (request.Itens.Count != 5)
                return BadRequest(new { erro = "A cesta deve conter exatamente 5 ativos.", codigo = "QUANTIDADE_ATIVOS_INVALIDA" });

            var tickersString = string.Join(",", request.Itens.Select(x => x.Ticker));
            var historico = new HistoricoCesta(tickersString);
            await _historicoRepository.SalvarAsync(historico);

            await _rebalanceamentoService.ExecutarRebalanceamentoPorMudancaCestaAsync(ct);

            return Created("", new 
            { 
                mensagem = "Cesta atualizada e persistida com sucesso.",
                data = historico.DataCriacao
            });
        }

        [HttpGet("cesta/historico")]
        public async Task<IActionResult> ObterHistorico()
        {
            var historico = await _historicoRepository.ObterTodosAsync();
            return Ok(historico);
        }

        [HttpGet("cesta/atual")]
        public IActionResult ObterCestaAtual()
        {
            // Mock da cesta atual para o frontend
            return Ok(new { nome = "Top Five Março 2026", ativos = new[] { "PETR4", "VALE3", "ITUB4", "BBDC4", "WEGE3" } });
        }
    }

    public class NovaCestaRequest
    {
        public List<ItemCestaRequest> Itens { get; set; } = new();
    }

    public class ItemCestaRequest
    {
        public string Ticker { get; set; } = string.Empty;
        public decimal Percentual { get; set; }
    }
}