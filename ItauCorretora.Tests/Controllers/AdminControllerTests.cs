using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using ItauCorretora.Api.Controllers;
using ItauCorretora.Application.Interfaces;
using ItauCorretora.Domain.Interfaces; // Adicionado para enxergar a Interface do repositório

namespace ItauCorretora.Tests.Controllers
{
    public class AdminControllerTests
    {
        [Fact]
        public async Task CadastrarCesta_DeveRetornarBadRequest_QuandoNaoTiver5Ativos()
        {
            var mockRebalanceamento = new Mock<IRebalanceamentoService>();
            var mockHistorico = new Mock<IHistoricoCestaRepository>(); // Criamos o Mock do novo repositório

           var controller = new AdminController(mockRebalanceamento.Object, mockHistorico.Object);

            var request = new NovaCestaRequest 
            { 
                Itens = new List<ItemCestaRequest> 
                { 
                    new ItemCestaRequest { Ticker = "ITUB4", Percentual = 100m } 
                } 
            };

            // Act
            var result = await controller.CadastrarCesta(request, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            
            // Garante que nem o rebalanceamento nem o histórico foram acionados
            mockRebalanceamento.Verify(r => r.ExecutarRebalanceamentoPorMudancaCestaAsync(It.IsAny<CancellationToken>()), Times.Never);
            mockHistorico.Verify(h => h.SalvarAsync(It.IsAny<ItauCorretora.Domain.Entities.HistoricoCesta>()), Times.Never);
        }
    }
}