using ItauCorretora.Infrastructure.Parsers;
using Microsoft.Extensions.Logging;
using Moq;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ItauCorretora.Tests
{
    public class CotahistParserTests
    {
        [Fact]
        public async Task ParsearAsync_DeveExtrairPrecoCorreto_QuandoLinhaForValida()
        {
            var mockLogger = new Mock<ILogger<CotahistParser>>();
            var parser = new CotahistParser(mockLogger.Object);
            
            var caminhoFake = Path.GetTempFileName();
            var espacos = new string(' ', 81);
            var linhaItub4 = "012026030502ITUB4       010" + espacos + "0000000003500";
            var linhaCompleta = linhaItub4.PadRight(245, ' ');
            
            await File.WriteAllLinesAsync(caminhoFake, new[] { linhaCompleta });

            var resultado = await parser.ParsearAsync(caminhoFake);

            Assert.True(resultado.ContainsKey("ITUB4"));
            Assert.Equal(35.00m, resultado["ITUB4"]);

            File.Delete(caminhoFake);
        }
    }
}