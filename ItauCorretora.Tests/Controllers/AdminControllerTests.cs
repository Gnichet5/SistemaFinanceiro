using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ItauCorretora.Api.Controllers;
using ItauCorretora.Application.Interfaces;

namespace ItauCorretora.Tests.Controllers
{
    public class AdminControllerTests
    {
        [Fact]
        public async Task CadastrarCesta_DeveRetornarBadRequest_QuandoNaoTiver5Ativos()
        {
            // Arrange
            var mockRebalanceamento = new Mock<IRebalanceamentoService>();
            var mockLogger = new Mock<ILogger<AdminController>>();
            var controller = new AdminController(mockRebalanceamento.Object, mockLogger.Object);

            // Cesta inválida: só tem 1 ação e não soma 100%
            var request = new NovaCestaRequest 
            { 
                Nome = "Cesta Falha", 
                Itens = new List<ItemCestaRequest> 
                { 
                    new ItemCestaRequest { Ticker = "ITUB4", Percentual = 100m } 
                } 
            };

            // Act
            var result = await controller.CadastrarCesta(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            
            // Garante que o motor de rebalanceamento NÃO foi acionado por engano
            mockRebalanceamento.Verify(r => r.ExecutarRebalanceamentoPorMudancaCestaAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}