using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ItauCorretora.Application.Services;
using ItauCorretora.Domain.Interfaces;
using ItauCorretora.Infrastructure;
using ItauCorretora.Infrastructure.Kafka;
using ItauCorretora.Infrastructure.Parsers;
using ItauCorretora.Infrastructure.Repositories;

namespace ItauCorretora
{
    /// <summary>
    /// Ponto de entrada e configuração de Injeção de Dependência.
    ///
    /// Para usar com ASP.NET Core ou Worker Service:
    ///   builder.Services.AdicionarSistemaComprasProgramadas(builder.Configuration);
    ///
    /// Exemplo de appsettings.json:
    /// {
    ///   "Kafka": {
    ///     "BootstrapServers": "kafka:9092",
    ///     "TopicIrDedoDuro": "fiscal.ir.dedoduro",
    ///     "TopicIrVenda": "fiscal.ir.venda",
    ///     "TopicCompraExecutada": "operacoes.compra.executada"
    ///   },
    ///   "Cotacao": {
    ///     "DiretorioArquivos": "/data/cotahist"
    ///   }
    /// }
    /// </summary>
    public static class DependencyInjectionExtensions
    {
        public static IServiceCollection AdicionarSistemaComprasProgramadas(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // --- Configurações via Options Pattern ---
            services.Configure<KafkaSettings>(configuration.GetSection("Kafka"));
            services.Configure<CotacaoSettings>(configuration.GetSection("Cotacao"));

            // --- Infraestrutura: Kafka ---
            services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

            // --- Infraestrutura: Parser COTAHIST ---
            services.AddSingleton<CotahistParser>();
            services.AddScoped<ICotacaoService, CotacaoService>();

            // --- Application: Motor de Compra (core do sistema) ---
            services.AddScoped<MotorCompraService>();

            // --- Repositórios (implementar com EF Core + MySQL) ---
            services.AddScoped<IClienteRepository, ClienteRepository>();
            services.AddScoped<ICustodiaRepository, CustodiaRepository>();
            services.AddScoped<IOrdemCompraRepository, OrdemCompraRepository>();

            return services;
        }
    }
}
