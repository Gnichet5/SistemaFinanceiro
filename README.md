#  Itaú Corretora - Sistema de Compras Programadas

Este projeto é a solução Full Stack para o Desafio Técnico de **Compras Programadas da Itaú Corretora**. O sistema automatiza o recolhimento de aportes mensais, executa a compra de ativos (Lote Padrão e Fracionário) com base em uma cesta "Top Five", e distribui os ativos para contas filhote de forma proporcional, calculando preço médio, IR "Dedo-Duro" e saldo residual.

---

##  Tecnologias e Arquitetura

O ecossistema foi dividido em duas aplicações principais, garantindo separação de responsabilidades e escalabilidade:

### Frontend (Interface do Cliente e Admin)
Construído com foco em **Clean UI**, performance e tipagem estrita para segurança financeira:
* **Framework:** Next.js 14 (App Router)
* **Linguagem:** TypeScript
* **Estilização:** Tailwind CSS (Paleta de cores customizada)
* **Visualização de Dados:** Recharts (Gráficos interativos de alocação)
* **Ícones:** Lucide React
* **Comunicação HTTP:** Axios (com tratamento de exceções e CORS)

### Backend (Motor de Regras de Negócio)
Uma Web API robusta focada em resiliência e processamento assíncrono:
* **Framework:** .NET 8 (C#)
* **ORM:** Entity Framework Core (MySQL)
* **Logs e Rastreabilidade:** Serilog
* **Design Patterns:** Repository Pattern, Injeção de Dependência, Event-Driven (Simulação de mensageria).

---

##  Funcionalidades Desenvolvidas

### 1. Módulo de Adesão de Clientes (`/adesao`)
* Captura de dados do investidor (Nome, CPF, Conta Filhote, Valor Mensal).
* Validação de aporte mínimo (R$ 100,00) no front e back.
* Sanitização de dados em tempo real (Máscaras interativas de CPF e formatação de Moeda BRL).

### 2. Dashboard Dinâmico do Investidor (`/dashboard/[cpf]`)
* Rota dinâmica que consome os dados processados pelo motor.
* **Resumo Financeiro:** Exibição do Valor Total Investido, Saldo Residual e Rentabilidade Total.
* **Composição de Carteira:** Gráfico de pizza (Recharts) renderizado sob demanda.
* **Tabela de Custódia:** Listagem detalhada de ativos (Tickers), quantidades consolidadas, Preço Médio e Valor Atual.
* **Empty States Inteligentes:** Feedback visual amigável para clientes que aderiram mas ainda não passaram pelo primeiro ciclo de compra.

### 3. Painel Administrativo & Motor (`/admin`)
* **Gestão da Cesta (Top Five):** Formulário de administração com trava de segurança em tempo real (só permite envio se a soma percentual dos 5 ativos for exatamente 100%).
* **Simulador de Ciclo de Compras:** Ferramenta exclusiva para avaliação. Permite forçar o disparo do gatilho do motor de compras (`POST /api/motor/executar-compra`) de forma manual, validando o processamento sem a necessidade de aguardar os dias 5, 15 ou 25 do mês.

---

##  Como Executar o Projeto Localmente

Para testar a integração de ponta a ponta, é necessário rodar ambas as aplicações simultaneamente.

### Passo 1: Iniciando a Web API (.NET 8)
1. Navegue até o diretório do backend (`ItauCorretora.Api`).
2. Certifique-se de ter configurado a Connection String do MySQL no `appsettings.json`.
3. Execute o comando:
   ```bash
   dotnet run
A API estará escutando na porta http://localhost:5254.

Passo 2: Iniciando o Frontend (Next.js)
Navegue até o diretório do frontend.

Instale as dependências:

Bash
npm install
Inicie o servidor de desenvolvimento:

Bash
npm run dev
O frontend estará disponível em http://localhost:3000.

Roteiro de Teste Sugerido para Avaliação
Adesão: Acesse o Frontend e navegue até a aba Nova Adesão. Cadastre um cliente fictício com aporte superior a R$ 100,00.

Definição da Cesta: Acesse a aba Administração. Defina 5 ativos (ex: ITUB4, PETR4, VALE3, BBDC4, WEGE3) e distribua os pesos até somar 100%. Salve a cesta.

Mock de Dados B3 (Importante): Para que o motor tenha cotações para trabalhar, acesse o Swagger da API (http://localhost:5254/swagger) e execute o endpoint POST /api/Investimentos/gerar-mock-b3.

Execução do Motor: Ainda na tela de Administração (ou via Swagger), clique no botão Executar Motor. Observe os logs no terminal do backend processando as compras, o rateio fracionário e os cálculos de saldo residual.

Validação: Volte para a Página Inicial e faça a busca pelo CPF cadastrado no passo 1. Valide no Dashboard os ativos comprados, o preço médio aplicado e o resíduo financeiro remanescente.

Desenvolvido por Guilherme Peniche Cordeiro - 2026
