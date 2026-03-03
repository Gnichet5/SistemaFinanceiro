using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ItauCorretora.Infrastructure.Parsers
{
    public class CotahistParser
    {
        private readonly ILogger<CotahistParser> _logger;

        // Tipos de registro de interesse
        private const string TipoRegistroCotacao = "01"; // O TIPREG correto da B3 para cotações (tamanho 2)

        // Tipos de mercado de interesse (usados para identificar fracionários)
        private const string TipoMercadoFracionario = "070";

        // Tipo de registro do cabeçalho e rodapé (ignorar)
        private const string TipoRegistroCabecalho = "00";
        private const string TipoRegistroRodape = "99";

        private static class Layout
        {
            public const int TipoRegistroInicio = 0;
            public const int TipoRegistroTamanho = 2;

            public const int DataPregaoInicio = 2;
            public const int DataPregaoTamanho = 8; // YYYYMMDD

            public const int CodigoBdiInicio = 10;
            public const int CodigoBdiTamanho = 2;

            public const int CodigoNegociacaoInicio = 12; // Ticker
            public const int CodigoNegociacaoTamanho = 12;

            public const int TipoMercadoInicio = 24;
            public const int TipoMercadoTamanho = 3;

            // PREULT: Preço de Fechamento — campo de 13 dígitos, 2 casas decimais implícitas
            public const int PrecoFechamentoInicio = 108; // Posição 109 - 1 (base 0)
            public const int PrecoFechamentoTamanho = 13;

            // Tamanho mínimo da linha de dados para evitar IndexOutOfRange
            public const int TamanhoMinimoLinhaValida = 125;
        }

        public CotahistParser(ILogger<CotahistParser> logger)
        {
            _logger = logger;
        }

        public async Task<Dictionary<string, decimal>> ParsearAsync(
            string caminhoArquivo,
            IEnumerable<string>? filtroTickers = null,
            CancellationToken ct = default)
        {
            if (!File.Exists(caminhoArquivo))
                throw new FileNotFoundException(
                    $"Arquivo COTAHIST não encontrado: {caminhoArquivo}");

            var resultado = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var filtroSet = filtroTickers is not null
                ? new HashSet<string>(filtroTickers, StringComparer.OrdinalIgnoreCase)
                : null;

            long linhasProcessadas = 0;
            long linhasValidas = 0;

            _logger.LogInformation("Iniciando parse do arquivo COTAHIST: {Arquivo}", caminhoArquivo);

            using var reader = new StreamReader(
                path: caminhoArquivo,
                encoding: Encoding.Latin1,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 65536);

            while (!reader.EndOfStream)
            {
                if (ct.IsCancellationRequested)
                {
                    _logger.LogWarning("Parse cancelado após {N} linhas.", linhasProcessadas);
                    break;
                }

                var linha = await reader.ReadLineAsync();
                linhasProcessadas++;

                if (linha is null || linha.Length < Layout.TamanhoMinimoLinhaValida)
                    continue;

                // Extrai TIPREG (tamanho 2)
                var tipoRegistro = linha.Substring(Layout.TipoRegistroInicio, Layout.TipoRegistroTamanho);

                if (tipoRegistro is TipoRegistroCabecalho or TipoRegistroRodape)
                    continue;

                // Aqui estava o bug: Agora validamos corretamente contra "01"
                if (tipoRegistro != TipoRegistroCotacao)
                    continue;

                var ticker = linha
                    .Substring(Layout.CodigoNegociacaoInicio, Layout.CodigoNegociacaoTamanho)
                    .Trim();

                if (filtroSet is not null &&
                    !filtroSet.Contains(ticker) &&
                    !filtroSet.Contains(ticker.TrimEnd('F')))
                    continue;

                var precoRaw = linha.Substring(
                    Layout.PrecoFechamentoInicio,
                    Layout.PrecoFechamentoTamanho);

                if (!TentarConverterPreco(precoRaw, out var precoFechamento))
                {
                    _logger.LogWarning(
                        "Linha {N}: não foi possível converter PREULT '{Valor}' para {Ticker}.",
                        linhasProcessadas, precoRaw, ticker);
                    continue;
                }

                var tipoMercado = linha
                    .Substring(Layout.TipoMercadoInicio, Layout.TipoMercadoTamanho)
                    .Trim();

                var tickerFinal = DeterminarTickerFinal(ticker, tipoMercado);

                resultado[tickerFinal] = precoFechamento;
                linhasValidas++;

                _logger.LogDebug(
                    "Ticker: {Ticker} | Preço Fechamento: R$ {Preco:F2} | Tipo: {Tipo}",
                    tickerFinal, precoFechamento, tipoRegistro);
            }

            _logger.LogInformation(
                "Parse concluído. Linhas lidas: {Total} | Cotações extraídas: {Validas}",
                linhasProcessadas, linhasValidas);

            return resultado;
        }

        private static bool TentarConverterPreco(string precoRaw, out decimal preco)
        {
            preco = 0m;

            if (string.IsNullOrWhiteSpace(precoRaw))
                return false;

            if (!long.TryParse(precoRaw.Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var valorCentavos))
                return false;

            if (valorCentavos <= 0)
                return false;

            preco = Math.Round(valorCentavos / 100m, 2);
            return true;
        }

        private static string DeterminarTickerFinal(string ticker, string tipoMercado)
        {
            var ehFracionario = tipoMercado == TipoMercadoFracionario;

            if (ehFracionario && !ticker.EndsWith("F", StringComparison.OrdinalIgnoreCase))
                return ticker + "F";

            return ticker;
        }

        public async Task<decimal?> ObterPrecoFechamentoAsync(
            string caminhoArquivo,
            string ticker,
            CancellationToken ct = default)
        {
            var cotacoes = await ParsearAsync(
                caminhoArquivo,
                filtroTickers: new[] { ticker },
                ct: ct);

            return cotacoes.TryGetValue(ticker, out var preco) ? preco : null;
        }
    }

    public record CotacaoCotahist(
        string Ticker,
        decimal PrecoFechamento,
        DateTime DataPregao,
        string TipoMercado
    );
}