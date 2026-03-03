using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; // Importante para o Logger
using Moq;
using Xunit;
using ItauCorretora.Api.Controllers;
using ItauCorretora.Domain.Interfaces;
using ItauCorretora.Domain.Entities;
using ItauCorretora.Application.Interfaces;

namespace ItauCorretora.Tests.Controllers
{
    public class ClientesControllerTests
    {
        [Fact]
        public async Task Aderir_DeveRetornarBadRequest_QuandoValorMensalForMenorQueCem()
        {
            // 1. ARRANGE
            var mockRepo = new Mock<IClienteRepository>();
            var mockRentabilidade = new Mock<IRentabilidadeService>();
            var mockLogger = new Mock<ILogger<ClientesController>>(); // Criamos o mock do logger

            // Passamos exatamente os 3 argumentos que o seu controller exige agora
            var controller = new ClientesController(
                mockRepo.Object, 
                mockRentabilidade.Object, 
                mockLogger.Object);
            
            var request = new AdesaoRequest { Cpf = "12345678900", Nome = "Gui", ValorMensal = 50m };

            // 2. ACT
            var result = await controller.Aderir(request, CancellationToken.None);

            // 3. ASSERT
            Assert.IsType<BadRequestObjectResult>(result);
            mockRepo.Verify(r => r.SalvarAsync(It.IsAny<Cliente>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}