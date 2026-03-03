using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Storage.Blobs;
using Azure.Identity;
using ItauCorretora.Domain.Interfaces;
using ItauCorretora.Infrastructure.Parsers;

namespace ItauCorretora.Infrastructure
{
    /// <summary>
    /// CotacaoService — Implementação de ICotacaoService usando o parser COTAHIST da B3.
    ///
    /// Estratégia:
    ///   1. Tenta encontrar o arquivo COTAHIST do dia de referência no diretório configurado.
    ///   2. Faz o parse (ou usa cache em memória se já foi carregado neste dia).
    ///   3. Retorna o preço de fechamento do ticker solicitado.
    ///
    /// Padrão de nomenclatura dos arquivos B3:
    ///   - Arquivo diário: COTAHIST_D{DDMMAAAA}.TXT
    ///   - Arquivo mensal: COTAHIST_M{MMAAAA}.TXT
    ///   - Arquivo anual:  COTAHIST_A{AAAA}.TXT
    /// </summary>
    public class CotacaoService : ICotacaoService
    {
        private readonly CotahistParser _parser;
        private readonly CotacaoSettings _settings;
        private readonly AzureCotacaoSettings? _azureSettings;
        private readonly BlobContainerClient? _blobContainerClient;
        private readonly ILogger<CotacaoService> _logger;

        // Cache em memória por data de pregão para evitar múltiplos parses do mesmo arquivo
        private readonly ConcurrentDictionary<DateTime, Dictionary<string, decimal>> _cache = new();

        /// <summary>
        /// Construtor com suporte a Azure Blob Storage (recomendado)
        /// </summary>
        public CotacaoService(
            CotahistParser parser,
            IOptions<CotacaoSettings> settings,
            IOptions<AzureCotacaoSettings>? azureSettings,
            BlobContainerClient? blobContainerClient,
            ILogger<CotacaoService> logger)
        {
            _parser = parser;
            _settings = settings.Value;
            _azureSettings = azureSettings?.Value;
            _blobContainerClient = blobContainerClient;
            _logger = logger;
        }

        /// <summary>
        /// Construtor com suporte apenas a File IO (compatibilidade retroativa)
        /// </summary>
        public CotacaoService(
            CotahistParser parser,
            IOptions<CotacaoSettings> settings,
            ILogger<CotacaoService> logger)
        {
            _parser = parser;
            _settings = settings.Value;
            _azureSettings = null;
            _blobContainerClient = null;
            _logger = logger;
        }

        /// <summary>
        /// Retorna o preço de fechamento do ticker na data especificada.
        /// Tenta usar Azure Blob Storage primeiro, com fallback para File IO local.
        /// Usa cache em memória para evitar re-parse do mesmo arquivo.
        /// </summary>
        public async Task<decimal?> ObterPrecoFechamentoAsync(
            string ticker,
            DateTime data,
            CancellationToken ct = default)
        {
            // Verifica cache
            if (_cache.TryGetValue(data.Date, out var cotacoesCached))
            {
                return cotacoesCached.TryGetValue(ticker, out var precoCached) ? precoCached : null;
            }

            Dictionary<string, decimal> cotacoes;

            // Tenta usar Azure Blob Storage
            if (_blobContainerClient != null)
            {
                cotacoes = await ObterCotacoesDoAzureAsync(data, ct);
            }
            else
            {
                // Fallback para File IO
                cotacoes = await ObterCotacoesDoSistemaArquivosAsync(data, ct);
            }

            if (cotacoes == null || cotacoes.Count == 0)
            {
                _logger.LogWarning(
                    "Nenhuma cotação encontrada para a data {Data}.",
                    data.ToShortDateString());
                return null;
            }

            // Armazena no cache para o dia
            _cache.TryAdd(data.Date, cotacoes);

            return cotacoes.TryGetValue(ticker, out var preco) ? preco : null;
        }

        /// <summary>
        /// Obtém cotações do Azure Blob Storage
        /// </summary>
        private async Task<Dictionary<string, decimal>> ObterCotacoesDoAzureAsync(DateTime data, CancellationToken ct)
        {
            if (_blobContainerClient == null)
                throw new InvalidOperationException("BlobContainerClient não foi configurado");

            var nomeBlob = ObterNomeBlobCotahist(data);
            var blobClient = _blobContainerClient.GetBlobClient(nomeBlob);

            // Verifica se blob existe
            var exists = await blobClient.ExistsAsync(ct);
            if (!exists)
            {
                // Tenta versão mensal
                nomeBlob = $"COTAHIST_M{data:MMyyyy}.TXT";
                blobClient = _blobContainerClient.GetBlobClient(nomeBlob);
                exists = await blobClient.ExistsAsync(ct);

                if (!exists)
                {
                    _logger.LogWarning(
                        "Blob COTAHIST não encontrado no Azure para a data {Data}. Nomes tentados: COTAHIST_D{D}, COTAHIST_M{M}",
                        data.ToShortDateString(), $"{data:ddMMyyyy}", $"{data:MMyyyy}");
                    return new Dictionary<string, decimal>();
                }
            }

            _logger.LogInformation("Baixando arquivo COTAHIST do Azure Blob Storage: {Blob}", nomeBlob);

            // Faz download do blob como stream
            var download = await blobClient.DownloadAsync(ct);
            using var stream = download.Value.Content;

            // Faz o parse do stream
            var cotacoes = await _parser.ParsearAsync(stream, ct: ct);
            return cotacoes;
        }

        /// <summary>
        /// Obtém cotações do sistema de arquivos local
        /// </summary>
        private async Task<Dictionary<string, decimal>> ObterCotacoesDoSistemaArquivosAsync(DateTime data, CancellationToken ct)
        {
            var caminhoArquivo = LocalizarArquivoCotahist(data);

            if (caminhoArquivo is null)
            {
                _logger.LogWarning(
                    "Arquivo COTAHIST não encontrado para a data {Data} no diretório {Dir}.",
                    data.ToShortDateString(), _settings.DiretorioArquivos);
                return new Dictionary<string, decimal>();
            }

            var cotacoes = await _parser.ParsearAsync(caminhoArquivo, ct: ct);
            return cotacoes;
        }

        /// <summary>
        /// Retorna o nome do blob em padrão de nomenclatura B3
        /// </summary>
        private static string ObterNomeBlobCotahist(DateTime data)
        {
            return $"COTAHIST_D{data:ddMMyyyy}.TXT";
        }

        /// <summary>
        /// Localiza o arquivo COTAHIST para uma data específica.
        /// Tenta primeiro o arquivo diário, depois o mensal.
        /// </summary>
        private string? LocalizarArquivoCotahist(DateTime data)
        {
            // Padrão arquivo diário: COTAHIST_DDDMMAAAA.TXT
            var nomeArquivoDiario = $"COTAHIST_D{data:ddMMyyyy}.TXT";
            var caminhoDiario = Path.Combine(_settings.DiretorioArquivos, nomeArquivoDiario);

            if (File.Exists(caminhoDiario))
            {
                _logger.LogDebug("Arquivo COTAHIST diário encontrado: {Arquivo}", caminhoDiario);
                return caminhoDiario;
            }

            // Fallback: arquivo mensal
            var nomeArquivoMensal = $"COTAHIST_M{data:MMyyyy}.TXT";
            var caminhoMensal = Path.Combine(_settings.DiretorioArquivos, nomeArquivoMensal);

            if (File.Exists(caminhoMensal))
            {
                _logger.LogDebug("Arquivo COTAHIST mensal encontrado: {Arquivo}", caminhoMensal);
                return caminhoMensal;
            }

            return null;
        }
    }

    public class CotacaoSettings
    {
        public string DiretorioArquivos { get; set; } = "/data/cotahist";
    }

    /// <summary>
    /// Configurações para Azure Blob Storage
    /// </summary>
    public class AzureCotacaoSettings
    {
        public string Endpoint { get; set; } = "";
        public string ContainerName { get; set; } = "cotahist";
    }
}
