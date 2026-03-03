using Microsoft.AspNetCore.Mvc;
using ItauCorretora.Application.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ItauCorretora.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvestimentosController : ControllerBase
    {
        private readonly MotorCompraService _motor;

        public InvestimentosController(MotorCompraService motor)
        {
            _motor = motor;
        }

        /// <summary>
        /// Dispara o ciclo de compra para o dia informado (5, 15 ou 25).
        /// </summary>
        [HttpPost("processar-ciclo/{dia}")]
        public async Task<IActionResult> Processar(int dia)
        {
            if (dia != 5 && dia != 15 && dia != 25)
                return BadRequest("O ciclo de compra só ocorre nos dias 5, 15 ou 25.");

            // O Motor espera uma lista de tickers e a data completa
            var tickersParaComprar = new List<string> { "ITUB4", "ITSA4" };
            var dataReferencia = new DateTime(DateTime.Now.Year, DateTime.Now.Month, dia);

            // Note o nome correto: ExecutarCicloDeCompraAsync
            await _motor.ExecutarCicloDeCompraAsync(tickersParaComprar, dataReferencia);
            
            return Ok(new { Mensagem = $"Processamento do ciclo dia {dia} para {string.Join(", ", tickersParaComprar)} disparado com sucesso!" });
        }

        [HttpPost("gerar-mock-b3")]
        public IActionResult GerarMockB3()
        {
            var caminho = @"C:\B3\COTAHIST_D05032026.TXT";
            
            // Montando a string cirurgicamente baseada nos índices do seu Layout
            // 012026030502 (12) + Ticker (12) + 010 (3) = 27 caracteres
            // Preço começa na posição 108. Então precisamos de 81 espaços em branco (108 - 27)
            var espacos = new string(' ', 81);
            
            var linhaItub4 = "012026030502ITUB4       010" + espacos + "0000000003500";
            var linhaItsa4 = "012026030502ITSA4       010" + espacos + "0000000001000";

            var linhas = new string[]
            {
                "00COTAHIST.20260305".PadRight(245, ' '), // Preenche com espaços até 245
                linhaItub4.PadRight(245, ' '),
                linhaItsa4.PadRight(245, ' '),
                "99COTAHIST.202603050000002".PadRight(245, ' ')
            };

            // Escreve usando EXATAMENTE o mesmo Encoding que o parser vai usar para ler
            System.IO.File.WriteAllLines(caminho, linhas, System.Text.Encoding.Latin1);

            return Ok(new { Mensagem = $"Arquivo perfeito gerado em {caminho}!" });
        }
    }
}