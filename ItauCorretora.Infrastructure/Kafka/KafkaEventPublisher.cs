using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Azure.Core;
using Azure.Identity;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ItauCorretora.Domain.Events;
using ItauCorretora.Domain.Interfaces;

namespace ItauCorretora.Infrastructure.Kafka
{
    /// <summary>
    /// KafkaEventPublisher — Implementação do IEventPublisher usando Apache Kafka.
    ///
    /// Tópicos utilizados:
    ///   - "fiscal.ir.dedoduro"       → IR Dedo-Duro (retenção na fonte em compras)
    ///   - "fiscal.ir.venda"          → IR sobre lucro em vendas (rebalanceamento)
    ///   - "operacoes.compra.executada" → Confirmação de compra na Conta Master
    ///
    /// Configuração via appsettings.json (injetada por IOptions):
    /// {
    ///   "Kafka": {
    ///     "BootstrapServers": "<cluster-bootstrap-servers>",
    ///     "TopicIrDedoDuro": "fiscal.ir.dedoduro",
    ///     "TopicIrVenda": "fiscal.ir.venda",
    ///     "TopicCompraExecutada": "operacoes.compra.executada",
    ///     // Azure Confluent Cloud requires SASL/SSL parameters below. Use KeyVault or secrets for production.
    ///     "ApiKey": "<confluent-api-key>",
    ///     "ApiSecret": "<confluent-api-secret>",
    ///     "SecurityProtocol": "SaslSsl",
    ///     "SaslMechanism": "Plain"
    ///   }
    /// }
    /// </summary>
    public class KafkaEventPublisher : IEventPublisher, IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly KafkaSettings _settings;
        private readonly ILogger<KafkaEventPublisher> _logger;
        private bool _disposed;

        // Opções de serialização JSON (nomes em camelCase para interop com outros sistemas)
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public KafkaEventPublisher(
            IOptions<KafkaSettings> settings,
            ILogger<KafkaEventPublisher> logger)
        {
            _settings = settings.Value;
            _logger = logger;

            // Configuração base do produtor Kafka com garantias de entrega
            var config = new ProducerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                // Acks.All garante que todos os brokers in-sync confirmem antes de retornar
                Acks = Acks.All,
                // Retentativas em caso de erros transientes
                MessageSendMaxRetries = 3,
                RetryBackoffMs = 500,
                // Garante ordem das mensagens dentro da mesma partição
                EnableIdempotence = true,
                // Compressão para reduzir tráfego de rede
                CompressionType = CompressionType.Snappy
            };

            // Se tiver chaves explícitas, usar SASL Plain (para compatibilidade backward).
            if (!string.IsNullOrEmpty(_settings.ApiKey) && !string.IsNullOrEmpty(_settings.ApiSecret))
            {
                config.SecurityProtocol = SecurityProtocol.SaslSsl;
                config.SaslMechanism = SaslMechanism.Plain;
                config.SaslUsername = _settings.ApiKey;
                config.SaslPassword = _settings.ApiSecret;
            }
            else
            {
                // Caso contrário assume-se Confluent Cloud com autenticação OAuthBearer via Azure Managed Identity
                config.SecurityProtocol = SecurityProtocol.SaslSsl;
                config.SaslMechanism = SaslMechanism.OAuthBearer;
                // Oidc method and additional settings may be set via environment variables
                config.SaslOauthbearerMethod = SaslOauthbearerMethod.Oidc;
                config.SaslOauthbearerTokenEndpointUrl = _settings.SaslOauthbearerTokenEndpointUrl;
                config.SaslOauthbearerConfig = _settings.SaslOauthbearerConfig;
            }

            // registra callback de renovação de token quando usar OAuthBearer
            if (config.SaslMechanism == SaslMechanism.OAuthBearer)
            {
                var builder = new ProducerBuilder<string, string>(config);
                builder.SetOAuthBearerTokenRefreshHandler(OAuthBearerTokenRefreshCallback);
                _producer = builder.Build();
            }
            else
            {
                _producer = new ProducerBuilder<string, string>(config).Build();
            }

            // produção já inicializada acima de acordo com mecanismo de autenticação
        }

        // =========================================================
        // PUBLICAÇÃO DE IR DEDO-DURO
        // =========================================================

        /// <summary>
        /// Publica o evento de IR Dedo-Duro após cada distribuição de ações.
        /// A chave da mensagem é o ClienteId para garantir ordenação por cliente.
        /// </summary>
        public async Task PublicarIrDedoDuroAsync(
            IrDedoDuroEvent evento,
            CancellationToken ct = default)
        {
            await PublicarMensagemAsync(
                topico: _settings.TopicIrDedoDuro,
                chave: evento.ClienteId.ToString(),
                payload: evento,
                ct: ct);
        }

        // =========================================================
        // PUBLICAÇÃO DE IR SOBRE VENDA
        // =========================================================

        /// <summary>
        /// Publica o evento de apuração de IR sobre lucro em vendas mensais.
        /// A chave é ClienteId + Ano + Mês para idempotência na reapuração.
        /// </summary>
        public async Task PublicarIrVendaAsync(
            IrVendaEvent evento,
            CancellationToken ct = default)
        {
            var chave = $"{evento.ClienteId}_{evento.Ano}_{evento.Mes:D2}";

            await PublicarMensagemAsync(
                topico: _settings.TopicIrVenda,
                chave: chave,
                payload: evento,
                ct: ct);
        }

        // =========================================================
        // PUBLICAÇÃO DE COMPRA EXECUTADA
        // =========================================================

        public async Task PublicarCompraExecutadaAsync(
            CompraExecutadaEvent evento,
            CancellationToken ct = default)
        {
            await PublicarMensagemAsync(
                topico: _settings.TopicCompraExecutada,
                chave: evento.OrdemCompraId.ToString(),
                payload: evento,
                ct: ct);
        }

        // =========================================================
        // MÉTODO INTERNO DE PUBLICAÇÃO
        // =========================================================

        private async Task PublicarMensagemAsync<T>(
            string topico,
            string chave,
            T payload,
            CancellationToken ct)
        {
            try
            {
                var mensagem = new Message<string, string>
                {
                    Key = chave,
                    Value = JsonSerializer.Serialize(payload, JsonOptions),
                    // Headers com metadados para rastreabilidade
                    Headers = new Headers
                    {
                        { "eventType", System.Text.Encoding.UTF8.GetBytes(typeof(T).Name) },
                        { "timestamp", System.Text.Encoding.UTF8.GetBytes(
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()) },
                        { "source", System.Text.Encoding.UTF8.GetBytes("MotorCompraService") }
                    }
                };

                var resultado = await _producer.ProduceAsync(topico, mensagem, ct);

                _logger.LogDebug(
                    "Evento publicado no Kafka | Tópico: {Topico} | Chave: {Chave} | " +
                    "Partição: {Particao} | Offset: {Offset}",
                    topico, chave, resultado.Partition.Value, resultado.Offset.Value);
            }
            catch (ProduceException<string, string> ex)
            {
                _logger.LogError(ex,
                    "Falha ao publicar evento no Kafka | Tópico: {Topico} | Chave: {Chave} | " +
                    "Erro: {Erro}",
                    topico, chave, ex.Error.Reason);
                throw; // Re-lança para que o chamador decida sobre retry/compensação
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            // Aguarda confirmação de todas as mensagens pendentes antes de fechar
            _producer.Flush(TimeSpan.FromSeconds(10));
            _producer.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Callback responsável por renovar tokens OAuthBearer usando Azure Managed Identity
        /// Este método será invocado pelo cliente Kafka sempre que um novo token for necessário.
        /// </summary>
        private static void OAuthBearerTokenRefreshCallback(IClient client, string config)
        {
            try
            {
                var credential = new DefaultAzureCredential();
                var scope = Environment.GetEnvironmentVariable("KAFKA_SCOPE") ?? string.Empty;
                var tokenRequestContext = new TokenRequestContext(new[] { scope });
                AccessToken accessTokenResponse = credential.GetToken(tokenRequestContext);

                var tokenValue = accessTokenResponse.Token;
                var expirationMs = DateTimeOffset.UtcNow.AddMinutes(55).ToUnixTimeMilliseconds();

                // extensões opcionais para Confluent logical cluster / identity pool
                var extensions = new Dictionary<string, string>();
                var logical = Environment.GetEnvironmentVariable("KAFKA_LOGICAL_CLUSTER_ID");
                var pool = Environment.GetEnvironmentVariable("KAFKA_IDENTITY_POOL_ID");
                if (!string.IsNullOrEmpty(logical)) extensions["logicalCluster"] = logical;
                if (!string.IsNullOrEmpty(pool)) extensions["identityPoolId"] = pool;

                client.OAuthBearerSetToken(tokenValue, expirationMs, "bearer", extensions);
            }
            catch (Exception ex)
            {
                client.OAuthBearerSetTokenFailure(ex.ToString());
            }
        }
    }

    /// <summary>
    /// Configurações do Kafka (injetadas via IOptions&lt;KafkaSettings&gt;).
    /// Mapeadas da seção "Kafka" do appsettings.json.
    /// </summary>
    public class KafkaSettings
    {
        public string BootstrapServers { get; set; } = string.Empty;
        public string TopicIrDedoDuro { get; set; } = "fiscal.ir.dedoduro";
        public string TopicIrVenda { get; set; } = "fiscal.ir.venda";
        public string TopicCompraExecutada { get; set; } = "operacoes.compra.executada";

        // Confluent Cloud / Azure Kafka authentication
        public string? ApiKey { get; set; }
        public string? ApiSecret { get; set; }
        public string? SecurityProtocol { get; set; }
        public string? SaslMechanism { get; set; }

        // In case oauth settings are stored in config instead of env
        public string? SaslOauthbearerTokenEndpointUrl { get; set; }
        public string? SaslOauthbearerConfig { get; set; }
    }
}
