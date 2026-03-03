using ItauCorretora.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ItauCorretora.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MotorController : ControllerBase
    {
        private readonly MotorCompraService _motor;

        public MotorController(MotorCompraService motor) => _motor = motor;

        [HttpPost("executar-compra")]
        public async Task<IActionResult> ExecutarCompraManual([FromBody] ExecutarCompraRequest request, CancellationToken ct)
        {
            var tickers = new List<string> { "PETR4", "VALE3", "ITUB4", "BBDC4", "WEGE3" };
            await _motor.ExecutarCicloDeCompraAsync(tickers, request.DataReferencia ?? DateTime.UtcNow, ct);
            return Ok(new { mensagem = "Motor executado com sucesso." });
        }
    }

    public class ExecutarCompraRequest { public DateTime? DataReferencia { get; set; } }
}