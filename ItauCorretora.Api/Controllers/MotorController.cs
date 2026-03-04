using ItauCorretora.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ItauCorretora.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MotorController : ControllerBase
    {
        private readonly MotorCompraService _motorCompraService;
        private readonly ILogger<MotorController> _logger;

        public MotorController(MotorCompraService motorCompraService, ILogger<MotorController> logger)
        {
            _motorCompraService = motorCompraService;
            _logger = logger;
        }

        [HttpPost("executar-compra")]
        public async Task<IActionResult> ExecutarCompraManual([FromBody] ExecutarCompraRequest request, CancellationToken ct)
        {
            _logger.LogInformation("Recebida requisição manual para executar o motor de compra.");

            try
            {
                // Aqui você pode buscar os tickers ativos da cesta atual no banco.
                // Para simplificar o endpoint agora, passamos os 5 fixos da regra de negócio:
                var tickers = new List<string> { "PETR4", "VALE3", "ITUB4", "BBDC4", "WEGE3" }; 
                
                // Usamos a data enviada no request, ou a data de hoje se não vier nada
                var dataReferencia = request.DataReferencia ?? DateTime.UtcNow;

                await _motorCompraService.ExecutarCicloDeCompraAsync(tickers, dataReferencia, ct);

                return Ok(new 
                { 
                    mensagem = "Compra programada executada com sucesso.",
                    dataExecucao = dataReferencia
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao executar o motor manualmente.");
                return StatusCode(500, new { erro = "Erro interno ao processar a compra.", codigo = "ERRO_INTERNO" });
            }
        }
    }

    // DTO simples para receber a data no corpo do POST
    public class ExecutarCompraRequest
    {
        public DateTime? DataReferencia { get; set; }
    }
}