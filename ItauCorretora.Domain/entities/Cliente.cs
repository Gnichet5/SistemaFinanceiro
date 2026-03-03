using System;
using System.Collections.Generic;
//using ItauCorretora.Domain.ValueObjects;

namespace ItauCorretora.Domain.Entities
{
    /// <summary>
    /// Entidade Rica: Cliente
    /// Representa o investidor que participa do plano de compra programada.
    /// Encapsula as regras de negócio relacionadas ao aporte mensal e status de participação.
    /// </summary>
    public class Cliente
    {
        // --- Propriedades de Identidade e Dados Básicos ---
        public Guid Id { get; private set; }
        public string Nome { get; private set; }
        public string Cpf { get; private set; }
        public string ContaFilhote { get; private set; } // Conta vinculada na corretora
        public StatusCliente Status { get; private set; }

        // --- Propriedades Financeiras ---
        /// <summary>
        /// Aporte mensal configurado pelo cliente.
        /// Utilizado para calcular 1/3 por evento de compra (dias 5, 15 e 25).
        /// </summary>
        public decimal AporteMensal { get; private set; }

        /// <summary>
        /// Saldo residual em reais na conta do cliente, proveniente de
        /// frações de ações que não puderam ser distribuídas no rateio.
        /// Esse valor é abatido no próximo ciclo de compra.
        /// </summary>
        public decimal SaldoResidual { get; private set; }

        // --- Custódias do Cliente (posições em ações) ---
        private readonly List<Custodia> _custodias = new();
        public IReadOnlyCollection<Custodia> Custodias => _custodias.AsReadOnly();

        // Construtor privado para forçar uso de métodos de fábrica (DDD)
        private Cliente() { }

        /// <summary>
        /// Método de fábrica para criar um novo cliente.
        /// </summary>
        public static Cliente Criar(string nome, string cpf, string contaFilhote, decimal aporteMensal)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new ArgumentException("Nome do cliente é obrigatório.", nameof(nome));
            if (aporteMensal <= 0)
                throw new ArgumentException("Aporte mensal deve ser positivo.", nameof(aporteMensal));

            return new Cliente
            {
                Id = Guid.NewGuid(),
                Nome = nome,
                Cpf = cpf,
                ContaFilhote = contaFilhote,
                AporteMensal = aporteMensal,
                Status = StatusCliente.Ativo,
                SaldoResidual = 0m
            };
        }

        // --- Comportamentos de Negócio (Métodos Ricos) ---

        /// <summary>
        /// Calcula o valor de aporte para um evento específico de compra (1/3 do mensal).
        /// Regra: o aporte mensal é dividido em 3 parcelas iguais: dias 5, 15 e 25.
        /// </summary>
        public decimal CalcularAporteDoEvento()
        {
            if (!EstaAtivo())
                return 0m;

            return Math.Round(AporteMensal / 3m, 2, MidpointRounding.ToZero);
        }

        /// <summary>
        /// Retorna o valor efetivo a ser usado na compra, descontando o saldo residual acumulado.
        /// O resíduo é abatido do próximo aporte para maximizar o uso do capital.
        /// </summary>
        public decimal CalcularAporteEfetivoDoEvento()
        {
            var aporteEvento = CalcularAporteDoEvento();
            // O saldo residual aumenta o poder de compra do cliente
            return aporteEvento + SaldoResidual;
        }

        /// <summary>
        /// Atualiza o saldo residual após um ciclo de compra e rateio.
        /// O resíduo é o valor em reais que "sobrou" após a compra por não ser
        /// suficiente para adquirir mais uma fração de ação.
        /// </summary>
        public void AtualizarSaldoResidual(decimal novoResiduo)
        {
            if (novoResiduo < 0)
                throw new ArgumentException("Saldo residual não pode ser negativo.");

            SaldoResidual = Math.Round(novoResiduo, 2);
        }

        /// <summary>
        /// Zera o saldo residual após ser consumido no ciclo de compra.
        /// </summary>
        public void ConsumirSaldoResidual()
        {
            SaldoResidual = 0m;
        }

        public bool EstaAtivo() => Status == StatusCliente.Ativo;

        public void Ativar() => Status = StatusCliente.Ativo;
        public void Inativar() => Status = StatusCliente.Inativo;

        public void AlterarAporteMensal(decimal novoAporte)
        {
            if (novoAporte <= 0)
                throw new ArgumentException("Aporte mensal deve ser positivo.");
            AporteMensal = novoAporte;
        }

        /// <summary>
        /// Adiciona ou atualiza a custódia de um ativo na carteira do cliente.
        /// </summary>
        public void AdicionarOuAtualizarCustodia(Custodia custodia)
        {
            _custodias.RemoveAll(c => c.Ticker == custodia.Ticker);
            _custodias.Add(custodia);
        }
    }

    public enum StatusCliente
    {
        Ativo = 1,
        Inativo = 2,
        Bloqueado = 3
    }
}
