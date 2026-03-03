using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ItauCorretora.Application.Services;
using ItauCorretora.Domain.Interfaces;

namespace ItauCorretora.Tests
{
    public class MotorCompraServiceTests
    {
        [Fact]
        public async Task ExecutarCicloDeCompra_NaoDeveProcessar_SeDataForInvalida()
        {
            // 1. ARRANGE
            var mockClienteRepo = new Mock<IClienteRepository>();
            var mockCustodiaRepo = new Mock<ICustodiaRepository>();
            var mockOrdemRepo = new Mock<IOrdemCompraRepository>();
            var mockCotacaoService = new Mock<ICotacaoService>();
            var mockPublisher = new Mock<IEventPublisher>();
            var mockLogger = new Mock<ILogger<MotorCompraService>>();

            var motor = new MotorCompraService(
                mockClienteRepo.Object,
                mockCustodiaRepo.Object,
                mockOrdemRepo.Object,
                mockCotacaoService.Object,
                mockPublisher.Object,
                mockLogger.Object
            );

            // Simulamos o dia 10 (que NÃO é dia de compra programada, pois as regras são 5, 15 e 25)
            var dataInvalida = new DateTime(2026, 3, 10); 
            var tickers = new List<string> { "ITUB4" };

            // 2. ACT
            await motor.ExecutarCicloDeCompraAsync(tickers, dataInvalida);

            // 3. ASSERT
                mockClienteRepo.Verify(x => x.ObterClientesAtivosAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}