using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Azure.Storage.Blobs;
using Azure.Identity;
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
    ///     "TopicCompraExecutada": "operacoes.compra.executada",
    ///     // se estiver usando Confluent Cloud/Azure Kafka, configure as credenciais:
    ///     "ApiKey": "<confluent-api-key>",
    ///     "ApiSecret": "<confluent-api-secret>",
    ///     "SecurityProtocol": "SaslSsl",
    ///     "SaslMechanism": "Plain"
    ///   },
    ///   "Cotacao": {
    ///     "DiretorioArquivos": "/data/cotahist"
    ///   },
    ///   "AzureStorageBlob": {
    ///     "Endpoint": "https://{StorageAccountName}.blob.core.windows.net",
    ///     "ContainerName": "cotahist"
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
            services.Configure<AzureCotacaoSettings>(configuration.GetSection("AzureStorageBlob"));

            // --- Infraestrutura: Azure Blob Storage (com Managed Identity) ---
            var azureSettings = configuration.GetSection("AzureStorageBlob").Get<AzureCotacaoSettings>();
            if (azureSettings?.Endpoint != null && !string.IsNullOrWhiteSpace(azureSettings.Endpoint))
            {
                // Registrar factory para criar BlobContainerClient quando Azure está configurado
                services.AddSingleton(sp =>
                {
                    var endPoint = azureSettings.Endpoint;
                    var containerName = azureSettings.ContainerName ?? "cotahist";
                    
                    var blobServiceClient = new BlobServiceClient(
                        new Uri(endPoint),
                        new DefaultAzureCredential());
                    
                    return blobServiceClient.GetBlobContainerClient(containerName) as BlobContainerClient;
                });
            }

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
