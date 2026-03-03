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
    /// <summary>
    /// MotorCompraService — O Caminho Crítico do Sistema
    ///
    /// Responsabilidade: Executar o ciclo de compra programada nos dias 5, 15 e 25 de cada mês.
    ///
    /// FLUXO DE EXECUÇÃO (por ativo):
    ///   1. Coletar clientes ativos e calcular aportes do evento (1/3 do mensal)
    ///   2. Somar aportes + saldos residuais = caixa total disponível
    ///   3. Obter preço de fechamento do ativo (COTAHIST / B3)
    ///   4. Calcular quantidade a comprar na Conta Master (separando Lote Padrão e Fracionário)
    ///   5. Executar compra consolidada na Conta Master
    ///   6. Ratear ações para Contas Filhote proporcionalmente ao aporte de cada cliente
    ///   7. Atualizar preço médio nas custódias
    ///   8. Publicar eventos de IR Dedo-Duro no Kafka
    ///   9. Atualizar saldos residuais dos clientes
    /// </summary>
    public class MotorCompraService
    {
        private readonly IClienteRepository _clienteRepository;
        private readonly ICustodiaRepository _custodiaRepository;
        private readonly IOrdemCompraRepository _ordemCompraRepository;
        private readonly ICotacaoService _cotacaoService;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<MotorCompraService> _logger;

        // Dias do mês em que o motor executa
        private static readonly int[] DiasDeExecucao = { 5, 15, 25 };

        // Limite de isenção de IR sobre vendas mensais
        private const decimal LimiteIsencaoIrVendas = 20_000m;

        // Alíquota de IR sobre lucro em vendas (rebalanceamento)
        private const decimal AliquotaIrVendas = 0.20m;

        // Alíquota de IR Dedo-Duro (retenção na fonte em compras)
        private const decimal AliquotaIrDedoDuro = 0.00005m; // 0,005%

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

        // =========================================================
        // PONTO DE ENTRADA PRINCIPAL
        // =========================================================

        /// <summary>
        /// Executa o ciclo de compra para todos os ativos elegíveis.
        /// Chamado por um Hosted Service / Scheduled Job nos dias 5, 15 e 25.
        /// </summary>
        /// <param name="tickers">Lista de tickers de ações a comprar neste ciclo.</param>
        /// <param name="dataReferencia">Data de execução (deve ser dia 5, 15 ou 25).</param>
        public async Task ExecutarCicloDeCompraAsync(
            IEnumerable<string> tickers,
            DateTime dataReferencia,
            CancellationToken ct = default)
        {
            // Valida se hoje é um dia de execução
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

            // Busca todos os clientes ativos no banco
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

        // =========================================================
        // PROCESSAMENTO POR ATIVO
        // =========================================================

        private async Task ProcessarAtivoAsync(
            string ticker,
            List<Cliente> clientesAtivos,
            DateTime dataReferencia,
            CancellationToken ct)
        {
            _logger.LogInformation("--- Processando ativo: {Ticker} ---", ticker);

            // PASSO 1: Obter preço de fechamento da B3 (via COTAHIST)
            var precoFechamento = await _cotacaoService.ObterPrecoFechamentoAsync(ticker, dataReferencia, ct);

            if (precoFechamento is null or <= 0)
            {
                _logger.LogWarning(
                    "Preço de fechamento não disponível para {Ticker} em {Data}. Pulando ativo.",
                    ticker, dataReferencia.ToShortDateString());
                return;
            }

            _logger.LogInformation("Preço de fechamento {Ticker}: R$ {Preco:F2}", ticker, precoFechamento);

            // PASSO 2: Calcular aportes de cada cliente para este evento
            var aportesPorCliente = CalcularAportesPorCliente(clientesAtivos);

            // PASSO 3: Somar o caixa total disponível para compra na Conta Master
            var caixaTotalMaster = aportesPorCliente.Values.Sum();

            if (caixaTotalMaster <= 0)
            {
                _logger.LogWarning("Caixa total zerado para {Ticker}. Nenhuma compra realizada.", ticker);
                return;
            }

            _logger.LogInformation(
                "Caixa total disponível para {Ticker}: R$ {Caixa:F2}", ticker, caixaTotalMaster);

            // PASSO 4: Calcular compra na Conta Master (Lote Padrão + Fracionário)
            var (qtdLotePadrao, qtdFracionario) =
                CalcularQuantidadeCompra(caixaTotalMaster, precoFechamento.Value);

            _logger.LogInformation(
                "Compra calculada — Lote Padrão: {LP} ações | Fracionário: {FR} ações",
                qtdLotePadrao, qtdFracionario);

            // PASSO 5: Criar e executar ordens na Conta Master
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

        /// <summary>
        /// Calcula o aporte efetivo de cada cliente para este evento de compra.
        /// Fórmula: Aporte = (AporteMensal / 3) + SaldoResidual acumulado
        /// O saldo residual é o "troco" de compras anteriores que ficou sem destino.
        /// </summary>
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

        /// <summary>
        /// Divide o caixa disponível em Lote Padrão (múltiplos de 100) e Fracionário (1-99).
        ///
        /// ALGORITMO:
        ///   1. Quantidade total acessível = Floor(Caixa / Preco)
        ///   2. Lotes padrão = Floor(qtdTotal / 100) * 100
        ///   3. Fracionário = qtdTotal - qtdLotePadrao (restante, 0 a 99)
        ///
        /// Exemplo: Caixa = R$ 1.550, Preço = R$ 10
        ///   qtdTotal = 155 ações
        ///   LotePadrao = 100 ações (1 lote)
        ///   Fracionário = 55 ações
        /// </summary>
        private (long qtdLotePadrao, long qtdFracionario) CalcularQuantidadeCompra(
            decimal caixaTotal, decimal precoUnitario)
        {
            if (precoUnitario <= 0)
                return (0, 0);

            // Quantidade total que o caixa permite comprar (truncamento = sem dívida)
            var qtdTotal = (long)Math.Floor(caixaTotal / precoUnitario);

            if (qtdTotal <= 0)
                return (0, 0);

            // Separa lotes padrão (arredonda para baixo no múltiplo de 100)
            var qtdLotePadrao = (qtdTotal / 100) * 100;

            // O fracionário é o que sobrou após tirar os lotes
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
            // Persiste a ordem como "Pendente"
            await _ordemCompraRepository.SalvarAsync(ordem, ct);

            // Simula execução na Conta Master (integração com mesa de operações)
            ordem.MarcarComoExecutada();

            // Publica evento de compra executada no Kafka
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

            // PASSO 6: Ratear ações para as Contas Filhote
            await RatearAcoesParaClientesAsync(
                ordem, clientes, aportesPorCliente, caixaTotalMaster, ct);

            ordem.MarcarComoRateada();
            await _ordemCompraRepository.AtualizarAsync(ordem, ct);
        }

        // =========================================================
        // ALGORITMO DE RATEIO PROPORCIONAL (Com Truncamento)
        // =========================================================

        /// <summary>
        /// Distribui as ações compradas na Conta Master proporcionalmente para as Contas Filhote.
        ///
        /// ALGORITMO DE RATEIO:
        ///   Para cada cliente:
        ///     ProporcaoCliente = AporteCliente / CaixaTotalMaster
        ///     QtdBruta = ProporcaoCliente * QuantidadeTotalComprada
        ///     QtdRateada = Floor(QtdBruta)  ← TRUNCAMENTO para não gerar fração de ação
        ///     ResidualValor = AporteCliente - (QtdRateada * PrecoUnitario)
        ///
        /// O resíduo em valor (não ação) é salvo no saldo do cliente e usado no próximo ciclo.
        ///
        /// IMPORTANTE: Ações que "sobram" após o truncamento ficam na Conta Master
        /// e são usadas como resíduo para abater o custo da próxima compra consolidada.
        /// </summary>
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

                // Proporção deste cliente no caixa total
                var proporcao = aporteCliente / caixaTotalMaster;

                // Quantidade bruta (pode ter decimais)
                var qtdBruta = proporcao * ordem.QuantidadeTotal;

                // TRUNCAMENTO: só ações inteiras podem ser distribuídas
                var qtdRateada = (long)Math.Floor(qtdBruta);

                if (qtdRateada <= 0)
                {
                    _logger.LogDebug(
                        "Cliente {Nome}: aporte insuficiente para 1 ação de {Ticker}. " +
                        "Saldo residual atualizado.", cliente.Nome, ordem.Ticker);

                    // O aporte vira resíduo total (não comprou nada)
                    cliente.AtualizarSaldoResidual(aporteCliente);
                    await _clienteRepository.AtualizarAsync(cliente, ct);
                    continue;
                }

                // Cria o registro de rateio com cálculo de IR Dedo-Duro e resíduo
                var rateio = RateioOrdem.Criar(
                    ordemCompraId: ordem.Id,
                    clienteId: cliente.Id,
                    contaFilhote: cliente.ContaFilhote,
                    quantidadeRateada: qtdRateada,
                    precoUnitario: ordem.PrecoUnitario,
                    valorAporteCliente: aporteCliente
                );

                ordem.AdicionarRateio(rateio);

                // PASSO 7: Atualizar (ou criar) custódia com novo Preço Médio
                await AtualizarCustodiaClienteAsync(
                    cliente, ordem.Ticker, qtdRateada, ordem.PrecoUnitario, ct);

                // PASSO 8: Publicar evento de IR Dedo-Duro no Kafka
                await PublicarIrDedoDuroAsync(rateio, ordem.Ticker, ct);

                // PASSO 9: Atualizar saldo residual do cliente para próximo ciclo
                // O resíduo é: Aporte - Valor efetivamente investido neste ciclo
                // Mas o SaldoResidual que somamos ao aporte JÁ foi consumido,
                // então zeramos primeiro e salvamos apenas o novo resíduo
                cliente.ConsumirSaldoResidual(); // Zera o resíduo anterior (já usado)
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

        /// <summary>
        /// Atualiza a custódia do cliente com as novas ações recebidas no rateio.
        /// Se não existir custódia, cria uma nova.
        /// O Preço Médio é sempre recalculado na compra:
        ///   PM = (QtdAnt * PMAnt + QtdNova * PrecoNova) / (QtdAnt + QtdNova)
        /// </summary>
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
                // Custódia existente: atualiza PM com a nova compra
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

        /// <summary>
        /// Verifica e apura o IR sobre lucro em vendas no mês.
        ///
        /// REGRA:
        ///   - Se total de vendas do mês > R$ 20.000,00: IR = 20% sobre o lucro líquido
        ///   - Se total de vendas do mês ≤ R$ 20.000,00: ISENTO
        ///   - Lucro Líquido = Valor Venda - (Qtd * PM)
        ///   - Vendas com prejuízo reduzem a base de cálculo
        /// </summary>
        public async Task ApurarIrSobreVendaMensalAsync(
            Cliente cliente,
            ResultadoVenda resultadoVenda,
            CancellationToken ct = default)
        {
            var hoje = DateTime.UtcNow;

            // Busca o total de vendas do cliente no mês corrente
            var totalVendasMes = await _custodiaRepository
                .SomarVendasDoMesAsync(cliente.Id, hoje.Month, hoje.Year, ct);

            // Acumula com a venda atual
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

            // Apura IR sobre o lucro líquido
            // Lucro Líquido = Valor Total Venda - (Qtd * Preço Médio)
            var lucroLiquido = resultadoVenda.LucroLiquido;

            // Prejuízo não gera IR (mas pode ser compensado em meses futuros — fora do escopo)
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

            // Publica evento de IR sobre venda no Kafka
            await _eventPublisher.PublicarIrVendaAsync(new IrVendaEvent
            {
                ClienteId = cliente.Id,
                ContaFilhote = cliente.ContaFilhote,
                Mes = hoje.Month,
                Ano = hoje.Year,
                TotalVendasMes = totalVendasMes,
                TotalCustoAquisicao = resultadoVenda.CustoAquisicao,
                LucroLiquidoMes = lucroLiquido,
                AliquotaPercentual = AliquotaIrVendas * 100, // Armazena como 20 (%)
                ValorIrApurado = irApurado,
                IsencaoAplicada = false
            }, ct);
        }
    }
}
