using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ItauCorretora.Domain.Entities;
using ItauCorretora.Domain.Events;
using ItauCorretora.Domain.Interfaces;

namespace ItauCorretora.Application.Services
{
    public class MotorCompraService
    {
        private readonly IClienteRepository _clienteRepository;
        private readonly ICustodiaRepository _custodiaRepository;
        private readonly IOrdemCompraRepository _ordemCompraRepository;
        private readonly ICotacaoService _cotacaoService;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<MotorCompraService> _logger;


        private static readonly int[] DiasDeExecucao = { 5, 15, 25 };

        private const decimal LimiteIsencaoIrVendas = 20_000m;

        private const decimal AliquotaIrVendas = 0.20m;

        private const decimal AliquotaIrDedoDuro = 0.00005m; 

        public MotorCompraService(
            IClienteRepository clienteRepository,
            ICustodiaRepository custodiaRepository,
            IOrdemCompraRepository ordemCompraRepository,
            ICotacaoService cotacaoService,
            IEventPublisher eventPublisher,
            ILogger<MotorCompraService> logger)
        {
            _clienteRepository = clienteRepository;
            _custodiaRepository = custodiaRepository;
            _ordemCompraRepository = ordemCompraRepository;
            _cotacaoService = cotacaoService;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        public async Task ExecutarCicloDeCompraAsync(
            IEnumerable<string> tickers,
            DateTime dataReferencia,
            CancellationToken ct = default)
        {
            if (!DiasDeExecucao.Contains(dataReferencia.Day))
            {
                _logger.LogWarning(
                    "Ciclo de compra chamado em data inválida: {Data}. " +
                    "Execução ocorre apenas nos dias 5, 15 e 25.", dataReferencia);
                return;
            }

            _logger.LogInformation(
                "=== INÍCIO DO CICLO DE COMPRA — Dia {Dia}/{Mes}/{Ano} ===",
                dataReferencia.Day, dataReferencia.Month, dataReferencia.Year);


            var clientesAtivos = (await _clienteRepository.ObterClientesAtivosAsync(ct)).ToList();

            if (!clientesAtivos.Any())
            {
                _logger.LogWarning("Nenhum cliente ativo encontrado. Ciclo encerrado.");
                return;
            }

            _logger.LogInformation("{Total} cliente(s) ativo(s) encontrado(s).", clientesAtivos.Count);

            // Processa cada ativo de forma independente
            foreach (var ticker in tickers)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    await ProcessarAtivoAsync(ticker, clientesAtivos, dataReferencia, ct);
                }
                catch (Exception ex)
                {
                    // Erros em um ativo NÃO devem parar o processamento dos demais
                    _logger.LogError(ex,
                        "Erro ao processar ativo {Ticker}. Continuando com os demais.", ticker);
                }
            }

            _logger.LogInformation("=== FIM DO CICLO DE COMPRA ===");
        }

        private async Task ProcessarAtivoAsync(
            string ticker,
            List<Cliente> clientesAtivos,
            DateTime dataReferencia,
            CancellationToken ct)
        {
            _logger.LogInformation("--- Processando ativo: {Ticker} ---", ticker);

            var precoFechamento = await _cotacaoService.ObterPrecoFechamentoAsync(ticker, dataReferencia, ct);

            if (precoFechamento is null or <= 0)
            {
                _logger.LogWarning(
                    "Preço de fechamento não disponível para {Ticker} em {Data}. Pulando ativo.",
                    ticker, dataReferencia.ToShortDateString());
                return;
            }

            _logger.LogInformation("Preço de fechamento {Ticker}: R$ {Preco:F2}", ticker, precoFechamento);

            var aportesPorCliente = CalcularAportesPorCliente(clientesAtivos);
            var caixaTotalMaster = aportesPorCliente.Values.Sum();

            if (caixaTotalMaster <= 0)
            {
                _logger.LogWarning("Caixa total zerado para {Ticker}. Nenhuma compra realizada.", ticker);
                return;
            }

            _logger.LogInformation(
                "Caixa total disponível para {Ticker}: R$ {Caixa:F2}", ticker, caixaTotalMaster);

 
            var (qtdLotePadrao, qtdFracionario) =
                CalcularQuantidadeCompra(caixaTotalMaster, precoFechamento.Value);

            _logger.LogInformation(
                "Compra calculada — Lote Padrão: {LP} ações | Fracionário: {FR} ações",
                qtdLotePadrao, qtdFracionario);


            if (qtdLotePadrao > 0)
            {
                var ordemLP = OrdemCompra.CriarParaLotePadrao(
                    ticker, qtdLotePadrao, precoFechamento.Value, dataReferencia.Day);

                await ExecutarOrdemERatearAsync(
                    ordemLP, clientesAtivos, aportesPorCliente,
                    caixaTotalMaster, dataReferencia, ct);
            }

            if (qtdFracionario > 0)
            {
                var ordemFrac = OrdemCompra.CriarParaMercadoFracionario(
                    ticker, qtdFracionario, precoFechamento.Value, dataReferencia.Day);

                await ExecutarOrdemERatearAsync(
                    ordemFrac, clientesAtivos, aportesPorCliente,
                    caixaTotalMaster, dataReferencia, ct);
            }
        }

        // =========================================================
        // CÁLCULO DE APORTES (1/3 do Mensal + Resíduo)
        // =========================================================


        private Dictionary<Guid, decimal> CalcularAportesPorCliente(List<Cliente> clientes)
        {
            var aportes = new Dictionary<Guid, decimal>();

            foreach (var cliente in clientes)
            {
                // CalcularAporteEfetivoDoEvento já inclui: (AporteMensal / 3) + SaldoResidual
                var aporteEfetivo = cliente.CalcularAporteEfetivoDoEvento();

                if (aporteEfetivo > 0)
                    aportes[cliente.Id] = aporteEfetivo;

                _logger.LogDebug(
                    "Cliente {Nome}: Aporte/3 = R$ {A:F2}, Resíduo = R$ {R:F2}, Efetivo = R$ {E:F2}",
                    cliente.Nome,
                    Math.Round(cliente.AporteMensal / 3m, 2),
                    cliente.SaldoResidual,
                    aporteEfetivo);
            }

            return aportes;
        }

        // =========================================================
        // CÁLCULO DE QUANTIDADE (LOTE PADRÃO + FRACIONÁRIO)
        // =========================================================

        private (long qtdLotePadrao, long qtdFracionario) CalcularQuantidadeCompra(
            decimal caixaTotal, decimal precoUnitario)
        {
            if (precoUnitario <= 0)
                return (0, 0);

            var qtdTotal = (long)Math.Floor(caixaTotal / precoUnitario);

            if (qtdTotal <= 0)
                return (0, 0);

            var qtdLotePadrao = (qtdTotal / 100) * 100;

            var qtdFracionario = qtdTotal - qtdLotePadrao;

            return (qtdLotePadrao, qtdFracionario);
        }

        // =========================================================
        // EXECUÇÃO DA ORDEM E RATEIO PARA CONTAS FILHOTE
        // =========================================================

        private async Task ExecutarOrdemERatearAsync(
            OrdemCompra ordem,
            List<Cliente> clientes,
            Dictionary<Guid, decimal> aportesPorCliente,
            decimal caixaTotalMaster,
            DateTime dataReferencia,
            CancellationToken ct)
        {
            await _ordemCompraRepository.SalvarAsync(ordem, ct);

            ordem.MarcarComoExecutada();
            await _eventPublisher.PublicarCompraExecutadaAsync(new CompraExecutadaEvent
            {
                OrdemCompraId = ordem.Id,
                Ticker = ordem.Ticker,
                QuantidadeTotal = ordem.QuantidadeTotal,
                PrecoUnitario = ordem.PrecoUnitario,
                ValorTotal = ordem.ValorTotalOrdem,
                TipoMercado = ordem.TipoMercado.ToString(),
                DiaCiclo = ordem.DiaCiclo
            }, ct);

            _logger.LogInformation(
                "Ordem {Id} executada: {Qtd} x {Ticker} @ R$ {Preco:F2} = R$ {Total:F2}",
                ordem.Id, ordem.QuantidadeTotal, ordem.Ticker,
                ordem.PrecoUnitario, ordem.ValorTotalOrdem);


            await RatearAcoesParaClientesAsync(
                ordem, clientes, aportesPorCliente, caixaTotalMaster, ct);

            ordem.MarcarComoRateada();
            await _ordemCompraRepository.AtualizarAsync(ordem, ct);
        }

        // =========================================================
        // ALGORITMO DE RATEIO PROPORCIONAL (Com Truncamento)
        // =========================================================

        private async Task RatearAcoesParaClientesAsync(
            OrdemCompra ordem,
            List<Cliente> clientes,
            Dictionary<Guid, decimal> aportesPorCliente,
            decimal caixaTotalMaster,
            CancellationToken ct)
        {
            _logger.LogInformation(
                "Iniciando rateio de {Qtd} ações ({Ticker}) para {N} clientes.",
                ordem.QuantidadeTotal, ordem.Ticker, aportesPorCliente.Count);

            var clientesComAporte = clientes
                .Where(c => aportesPorCliente.ContainsKey(c.Id))
                .ToList();

            foreach (var cliente in clientesComAporte)
            {
                if (ct.IsCancellationRequested) break;

                var aporteCliente = aportesPorCliente[cliente.Id];

                var proporcao = aporteCliente / caixaTotalMaster;

                var qtdBruta = proporcao * ordem.QuantidadeTotal;

                var qtdRateada = (long)Math.Floor(qtdBruta);

                if (qtdRateada <= 0)
                {
                    _logger.LogDebug(
                        "Cliente {Nome}: aporte insuficiente para 1 ação de {Ticker}. " +
                        "Saldo residual atualizado.", cliente.Nome, ordem.Ticker);

                    cliente.AtualizarSaldoResidual(aporteCliente);
                    await _clienteRepository.AtualizarAsync(cliente, ct);
                    continue;
                }
                var rateio = RateioOrdem.Criar(
                    ordemCompraId: ordem.Id,
                    clienteId: cliente.Id,
                    contaFilhote: cliente.ContaFilhote,
                    quantidadeRateada: qtdRateada,
                    precoUnitario: ordem.PrecoUnitario,
                    valorAporteCliente: aporteCliente
                );

                ordem.AdicionarRateio(rateio);
                await AtualizarCustodiaClienteAsync(
                    cliente, ordem.Ticker, qtdRateada, ordem.PrecoUnitario, ct);

                await PublicarIrDedoDuroAsync(rateio, ordem.Ticker, ct);

                cliente.ConsumirSaldoResidual(); 
                cliente.AtualizarSaldoResidual(rateio.ResidualCliente);
                await _clienteRepository.AtualizarAsync(cliente, ct);

                _logger.LogInformation(
                    "Cliente {Nome} ({Conta}): +{Qtd} {Ticker} | " +
                    "Valor: R$ {Valor:F2} | IR Dedo-Duro: R$ {IR:F4} | Resíduo: R$ {Res:F2}",
                    cliente.Nome, cliente.ContaFilhote,
                    qtdRateada, ordem.Ticker,
                    rateio.ValorFinanceiroRateio,
                    rateio.ValorIrDedoDuro,
                    rateio.ResidualCliente);
            }

            var totalRateado = ordem.CalcularTotalRateado();
            var residualMaster = ordem.CalcularQuantidadeResidual();

            _logger.LogInformation(
                "Rateio concluído. Distribuído: {Rat} ações | Resíduo Master: {Res} ações (R$ {ResVal:F2})",
                totalRateado, residualMaster, ordem.CalcularValorResidual());
        }

        // =========================================================
        // ATUALIZAÇÃO DE CUSTÓDIA (com Preço Médio)
        // =========================================================

        private async Task AtualizarCustodiaClienteAsync(
            Cliente cliente,
            string ticker,
            long qtdComprada,
            decimal precoCompra,
            CancellationToken ct)
        {
            var custodiaExistente = await _custodiaRepository
                .ObterPorClienteETickerAsync(cliente.Id, ticker, ct);

            if (custodiaExistente is null)
            {
                // Primeira compra: cria nova custódia
                var novaCustodia = Custodia.CriarPosicaoInicial(
                    cliente.Id, ticker, qtdComprada, precoCompra);

                await _custodiaRepository.SalvarAsync(novaCustodia, ct);

                _logger.LogDebug(
                    "Nova custódia criada: {Cliente} | {Ticker} | PM: R$ {PM:F6}",
                    cliente.Nome, ticker, novaCustodia.PrecoMedio);
            }
            else
            {
                var pmAnterior = custodiaExistente.PrecoMedio;
                custodiaExistente.RegistrarCompra(qtdComprada, precoCompra);

                await _custodiaRepository.AtualizarAsync(custodiaExistente, ct);

                _logger.LogDebug(
                    "Custódia atualizada: {Cliente} | {Ticker} | " +
                    "PM Anterior: R$ {PMA:F6} | PM Novo: R$ {PMN:F6}",
                    cliente.Nome, ticker, pmAnterior, custodiaExistente.PrecoMedio);
            }
        }

        // =========================================================
        // PUBLICAÇÃO DE IR DEDO-DURO NO KAFKA
        // =========================================================

        private async Task PublicarIrDedoDuroAsync(
            RateioOrdem rateio, string ticker, CancellationToken ct)
        {
            var evento = new IrDedoDuroEvent
            {
                Ticker = ticker,
                ClienteId = rateio.ClienteId,
                ContaFilhote = rateio.ContaFilhote,
                QuantidadeOperacao = rateio.QuantidadeRateada,
                ValorBrutoOperacao = rateio.ValorFinanceiroRateio,
                BaseCalculo = rateio.ValorFinanceiroRateio,
                AliquotaPercentual = 0.005m,
                ValorIr = rateio.ValorIrDedoDuro
            };

            await _eventPublisher.PublicarIrDedoDuroAsync(evento, ct);
        }

        // =========================================================
        // IR SOBRE VENDAS (REBALANCEAMENTO)
        // =========================================================


        public async Task ApurarIrSobreVendaMensalAsync(
            Cliente cliente,
            ResultadoVenda resultadoVenda,
            CancellationToken ct = default)
        {
            var hoje = DateTime.UtcNow;

            // Busca o total de vendas do cliente no mês corrente
            var totalVendasMes = await _custodiaRepository
                .SomarVendasDoMesAsync(cliente.Id, hoje.Month, hoje.Year, ct);

            totalVendasMes += resultadoVenda.ValorTotalVenda;

            _logger.LogInformation(
                "IR Vendas | Cliente: {Nome} | Total Vendas Mês: R$ {Total:F2} | " +
                "Limite Isenção: R$ {Limite:F2}",
                cliente.Nome, totalVendasMes, LimiteIsencaoIrVendas);

            var isento = totalVendasMes <= LimiteIsencaoIrVendas;

            if (isento)
            {
                _logger.LogInformation(
                    "Cliente {Nome} está ISENTO de IR sobre vendas neste mês " +
                    "(total R$ {Total:F2} ≤ R$ {Limite:F2}).",
                    cliente.Nome, totalVendasMes, LimiteIsencaoIrVendas);
                return;
            }

            var lucroLiquido = resultadoVenda.LucroLiquido;

            if (lucroLiquido <= 0)
            {
                _logger.LogInformation(
                    "Operação de venda sem lucro para {Nome}. IR = R$ 0,00.", cliente.Nome);
                return;
            }

            var irApurado = Math.Round(lucroLiquido * AliquotaIrVendas, 2);

            _logger.LogInformation(
                "IR Apurado | Cliente: {Nome} | Lucro: R$ {Lucro:F2} | " +
                "IR (20%%): R$ {IR:F2}",
                cliente.Nome, lucroLiquido, irApurado);

            await _eventPublisher.PublicarIrVendaAsync(new IrVendaEvent
            {
                ClienteId = cliente.Id,
                ContaFilhote = cliente.ContaFilhote,
                Mes = hoje.Month,
                Ano = hoje.Year,
                TotalVendasMes = totalVendasMes,
                TotalCustoAquisicao = resultadoVenda.CustoAquisicao,
                LucroLiquidoMes = lucroLiquido,
                AliquotaPercentual = AliquotaIrVendas * 100, 
                ValorIrApurado = irApurado,
                IsencaoAplicada = false
            }, ct);
        }
    }
}
