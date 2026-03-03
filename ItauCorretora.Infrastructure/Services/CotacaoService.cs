using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private readonly ILogger<CotacaoService> _logger;

        // Cache em memória por data de pregão para evitar múltiplos parses do mesmo arquivo
        private readonly ConcurrentDictionary<DateTime, Dictionary<string, decimal>> _cache = new();

        public CotacaoService(
            CotahistParser parser,
            IOptions<CotacaoSettings> settings,
            ILogger<CotacaoService> logger)
        {
            _parser = parser;
            _settings = settings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Retorna o preço de fechamento do ticker na data especificada.
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

            // Localiza e faz o parse do arquivo COTAHIST
            var caminhoArquivo = LocalizarArquivoCotahist(data);

            if (caminhoArquivo is null)
            {
                _logger.LogWarning(
                    "Arquivo COTAHIST não encontrado para a data {Data} no diretório {Dir}.",
                    data.ToShortDateString(), _settings.DiretorioArquivos);
                return null;
            }

            var cotacoes = await _parser.ParsearAsync(caminhoArquivo, ct: ct);

            // Armazena no cache para o dia
            _cache.TryAdd(data.Date, cotacoes);

            return cotacoes.TryGetValue(ticker, out var preco) ? preco : null;
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
}
