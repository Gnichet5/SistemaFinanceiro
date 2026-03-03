using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ItauCorretora.Application.Services;
using ItauCorretora.Domain.Entities;
using ItauCorretora.Domain.Interfaces;

namespace ItauCorretora.Tests
{
    public class RentabilidadeServiceTests
    {
        [Fact]
        public async Task CalcularRentabilidadeAsync_DeveCalcularLucroEPercentualCorretamente()
        {
            // 1. ARRANGE (Preparação)
            // Usamos o seu método de fábrica do DDD!
            var clienteMock = Cliente.Criar("Guilherme", "12345678900", "AG1234-C5678", 1000m);
            var clienteId = clienteMock.Id; // Pegamos o Guid que a sua entidade gerou sozinha
            
            var mockClienteRepo = new Mock<IClienteRepository>();
            var mockCustodiaRepo = new Mock<ICustodiaRepository>();
            var mockCotacaoService = new Mock<ICotacaoService>();
            var mockLogger = new Mock<ILogger<RentabilidadeService>>();

            mockClienteRepo.Setup(repo => repo.ObterPorIdAsync(clienteId, It.IsAny<CancellationToken>()))
                           .ReturnsAsync(clienteMock);

            // Simulamos a custódia: 100 ações de ITSA4 compradas a R$ 10,00 (Total Investido: R$ 1.000)
            var custodiasMock = new List<Custodia>
            {
                Custodia.CriarPosicaoInicial(clienteId, "ITSA4", 100, 10m)
            };
            mockCustodiaRepo.Setup(repo => repo.ObterPorClienteAsync(clienteId, It.IsAny<CancellationToken>()))
                            .ReturnsAsync(custodiasMock);

            // Simulamos a B3: Hoje a ITSA4 está valendo R$ 12,00
            mockCotacaoService.Setup(cot => cot.ObterPrecoFechamentoAsync("ITSA4", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                              .ReturnsAsync(12m);

            var service = new RentabilidadeService(
                mockClienteRepo.Object, 
                mockCustodiaRepo.Object, 
                mockCotacaoService.Object, 
                mockLogger.Object);

            // 2. ACT (Ação)
            var response = await service.CalcularRentabilidadeAsync(clienteId);

            // 3. ASSERT (Verificação)
            Assert.NotNull(response);
            Assert.Equal(1000m, response.Rentabilidade.ValorTotalInvestido);
            Assert.Equal(1200m, response.Rentabilidade.ValorAtualCarteira);
            Assert.Equal(200m, response.Rentabilidade.PlTotal); // R$ 200 de lucro
            Assert.Equal(20m, response.Rentabilidade.RentabilidadePercentual); // 20% de lucro
            
            Assert.Single(response.Ativos);
            Assert.Equal(100m, response.Ativos[0].ComposicaoCarteira);
        }
    }
}