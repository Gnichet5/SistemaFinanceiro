"use client";

import { useEffect, useState } from "react";
import { Wallet, PiggyBank, TrendingUp, AlertCircle, Loader2, Calendar, PieChart as PieChartIcon } from "lucide-react";
import { PieChart, Pie, Cell, Tooltip, ResponsiveContainer } from "recharts";
import { formatarMoeda } from "@/utils/formatters";
import api from "@/services/api";
import { DashboardResponse } from "@/types";

const CORES_GRAFICO = ["#002B5E", "#EC7000", "#1E4B82", "#FF9E4A", "#4A78B0"];

export default function DashboardView({ cpf }: { cpf: string }) {
  const [dados, setDados] = useState<DashboardResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    const buscarDados = async () => {
      try {
        setLoading(true);
        setErro(null);
        // Rota atualizada: /api/Clientes/dashboard/{cpf}
        const response = await api.get<DashboardResponse>(`/Clientes/dashboard/${cpf}`);
        setDados(response.data);
      } catch (err: any) {
        setErro(err.response?.data?.mensagem || "Não foi possível carregar o portfólio.");
      } finally {
        setLoading(false);
      }
    };

    if (cpf) buscarDados();
  }, [cpf]);

  if (loading) return (
    <div className="flex flex-col items-center justify-center min-h-[50vh] text-itau-blue">
      <Loader2 className="animate-spin mb-4" size={48} />
      <p className="animate-pulse font-medium">Carregando investimentos de {cpf}...</p>
    </div>
  );

  if (erro || !dados) return (
    <div className="bg-red-50 border border-red-200 text-red-600 p-6 rounded-xl max-w-2xl mx-auto mt-8 flex gap-3">
      <AlertCircle size={24} />
      <div><h3 className="font-bold">Erro no Dashboard</h3><p className="text-sm">{erro}</p></div>
    </div>
  );

  // Mapeamento direto das novas chaves do payload
  const { valorTotalInvestido, saldoResidual, rentabilidadeTotal, custodia, nome } = dados;

  const dadosGrafico = custodia.map(item => ({
    name: item.ticker,
    value: item.valorAtual
  }));

  return (
    <div className="space-y-8 animate-fade-in">
      <div>
        <h1 className="text-3xl font-bold text-itau-blue tracking-tight">Olá, {nome}</h1>
        <p className="text-gray-500 mt-1">Confira o resumo das suas compras programadas.</p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="bg-white p-6 rounded-xl shadow-sm border border-gray-100 flex items-start gap-4">
          <div className="p-3 bg-blue-50 text-itau-blue rounded-lg"><Wallet size={24} /></div>
          <div>
            <p className="text-sm font-medium text-gray-500">Total Investido</p>
            <h2 className="text-2xl font-bold">{formatarMoeda(valorTotalInvestido || 0)}</h2>
          </div>
        </div>

        <div className="bg-white p-6 rounded-xl shadow-sm border border-gray-100 flex items-start gap-4">
          <div className="p-3 bg-orange-50 text-itau-orange rounded-lg"><PiggyBank size={24} /></div>
          <div>
            <p className="text-sm font-medium text-gray-500">Saldo Residual</p>
            <h2 className="text-2xl font-bold">{formatarMoeda(saldoResidual || 0)}</h2>
          </div>
        </div>

        <div className="bg-white p-6 rounded-xl shadow-sm border border-gray-100 flex items-start gap-4">
          <div className="p-3 bg-green-50 text-green-600 rounded-lg"><TrendingUp size={24} /></div>
          <div>
            <p className="text-sm font-medium text-gray-500">Rentabilidade</p>
            <h2 className={`text-2xl font-bold ${rentabilidadeTotal >= 0 ? 'text-green-600' : 'text-red-600'}`}>
              {rentabilidadeTotal >= 0 ? '+' : ''}{rentabilidadeTotal?.toFixed(2)}%
            </h2>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        <div className="bg-white p-6 rounded-xl shadow-sm border border-gray-100 lg:col-span-1 min-h-[350px] flex flex-col items-center justify-center text-center">
          <h3 className="text-lg font-semibold text-itau-blue mb-6 self-start">Composição</h3>
          {custodia.length === 0 ? (
            <div className="text-gray-400">
          <PieChartIcon size={64} className="mx-auto mb-2 opacity-20" /> 
          <p className="text-sm">Aguardando ciclo.</p>
        </div>
          ) : (
            <ResponsiveContainer width="100%" height={300}>
              <PieChart>
                <Pie data={dadosGrafico} cx="50%" cy="50%" innerRadius={70} outerRadius={100} paddingAngle={2} dataKey="value" stroke="none">
                  {dadosGrafico.map((_, i) => <Cell key={i} fill={CORES_GRAFICO[i % CORES_GRAFICO.length]} />)}
                </Pie>
                <Tooltip formatter={(v: any) => formatarMoeda(v || 0)} />
              </PieChart>
            </ResponsiveContainer>
          )}
        </div>

        <div className="bg-white p-6 rounded-xl shadow-sm border border-gray-100 lg:col-span-2">
          <h3 className="text-lg font-semibold text-itau-blue mb-6">Custódia Atual</h3>
          {custodia.length === 0 ? (
            <div className="flex flex-col items-center py-12 bg-gray-50 rounded-lg border-2 border-dashed border-gray-200">
              <Calendar size={32} className="text-itau-blue mb-2" />
              <p className="text-gray-600 font-medium text-center">Seu primeiro investimento será no próximo ciclo (dia 05, 15 ou 25)!</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm">
                <thead>
                  <tr className="border-b border-gray-100 text-gray-400 font-medium">
                    <th className="pb-3 px-4 uppercase tracking-wider">Ativo</th>
                    <th className="pb-3 px-4 text-right">Qtd</th>
                    <th className="pb-3 px-4 text-right">Preço Médio</th>
                    <th className="pb-3 px-4 text-right">Valor Atual</th>
                  </tr>
                </thead>
                <tbody>
                  {custodia.map((item, idx) => (
                    <tr key={idx} className="border-b border-gray-50 last:border-0 hover:bg-gray-50 transition-colors">
                      <td className="py-4 px-4 font-bold text-itau-blue">{item.ticker}</td>
                      <td className="py-4 px-4 text-right">{item.quantidade}</td>
                      <td className="py-4 px-4 text-right">{formatarMoeda(item.precoMedio)}</td>
                      <td className="py-4 px-4 text-right font-semibold">{formatarMoeda(item.valorAtual)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}