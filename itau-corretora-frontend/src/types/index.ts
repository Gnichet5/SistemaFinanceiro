
export interface AdesaoRequest {
  nome: string;
  cpf: string;
  contaFilhote: string;
  valorMensal: number;
}

export interface ItemCesta {
  ticker: string;
  percentual: number;
}

export interface NovaCestaRequest {
  itens: ItemCesta[];
}

export interface RespostaPadrao<T = void> {
  sucesso: boolean;
  mensagem?: string;
  dados?: T;
}
export interface ResumoFinanceiro {
  totalInvestido: number;
  saldoResidual: number;
  rentabilidade: number;
}

export interface AtivoCustodia {
  ticker: string;
  quantidade: number;
  precoMedio: number;
  valorAtual: number;
}

export interface DashboardResponse {
  nome: string;
  valorTotalInvestido: number;
  saldoResidual: number;
  rentabilidadeTotal: number;
  custodia: AtivoCustodia[];
}