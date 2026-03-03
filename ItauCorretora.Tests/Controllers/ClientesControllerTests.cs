using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ItauCorretora.Api.Controllers;
using ItauCorretora.Application.Interfaces;
using ItauCorretora.Domain.Interfaces;
using ItauCorretora.Domain.Entities;

namespace ItauCorretora.Tests.Controllers
{
    public class ClientesControllerTests
    {
        [Fact]
        public async Task Aderir_DeveRetornarBadRequest_QuandoValorMensalForMenorQueCem()
        {
            // Arrange
            var mockRepo = new Mock<IClienteRepository>();
            var mockRentabilidade = new Mock<IRentabilidadeService>();
            var mockLogger = new Mock<ILogger<ClientesController>>();

            var controller = new ClientesController(mockRepo.Object, mockRentabilidade.Object, mockLogger.Object);
            var request = new AdesaoRequest { Cpf = "12345678900", Nome = "Gui", ValorMensal = 50m }; // Inválido!

            // Act
            var result = await controller.Aderir(request, CancellationToken.None);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
            
            // Garante que o banco não foi chamado
            mockRepo.Verify(r => r.SalvarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Aderir_DeveRetornarCreatedESalvarNoBanco_QuandoRequestForValido()
        {
            // Arrange
            var mockRepo = new Mock<IClienteRepository>();
            var mockRentabilidade = new Mock<IRentabilidadeService>();
            var mockLogger = new Mock<ILogger<ClientesController>>();

            var controller = new ClientesController(mockRepo.Object, mockRentabilidade.Object, mockLogger.Object);
            var request = new AdesaoRequest { Cpf = "12345678900", Nome = "Gui", ValorMensal = 500m }; // Válido!

            // Act
            var result = await controller.Aderir(request, CancellationToken.None);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, objectResult.StatusCode);
            
            // Garante que o método de salvar no banco FOI chamado exatamente 1 vez!
            mockRepo.Verify(r => r.SalvarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}