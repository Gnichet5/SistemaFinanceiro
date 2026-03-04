"use client";

import { useState, useMemo } from "react";
import api from "@/services/api";
import { ItemCesta, NovaCestaRequest } from "@/types";

export default function CestaAdminForm() {
  // Inicializa com 5 posições vazias
  const [itens, setItens] = useState<ItemCesta[]>(
    Array(5).fill({ ticker: "", percentual: 0 })
  );
  const [loading, setLoading] = useState(false);
  const [mensagem, setMensagem] = useState<{ tipo: 'sucesso' | 'erro', texto: string } | null>(null);

  // Calcula o total do percentual em tempo real
  const totalPercentual = useMemo(() => {
    return itens.reduce((acc, item) => acc + (Number(item.percentual) || 0), 0);
  }, [itens]);

  const isValid = totalPercentual === 100 && itens.every(item => item.ticker.trim() !== "" && item.percentual > 0);

  const handleItemChange = (index: number, field: keyof ItemCesta, value: string | number) => {
    const novosItens = [...itens];
    novosItens[index] = { ...novosItens[index], [field]: value };
    setItens(novosItens);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isValid) return;

    setLoading(true);
    setMensagem(null);

    const payload: NovaCestaRequest = { itens };

    try {
      // Ajuste a rota para a criação da cesta no seu backend
      await api.post("/Admin/cesta", payload);
      setMensagem({ tipo: 'sucesso', texto: 'Cesta Top cinco atualizada com sucesso!' });
    } catch (err: any) {
      setMensagem({ tipo: 'erro', texto: 'Erro ao salvar a cesta. Verifique os dados.' });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="bg-white p-6 md:p-8 rounded-lg shadow-sm border border-gray-100 max-w-2xl mx-auto">
      <div className="flex justify-between items-end mb-6">
        <div>
          <h2 className="text-2xl font-bold text-itau-blue">Cesta Top cinco</h2>
          <p className="text-sm text-gray-500">Defina os 5 ativos e seus pesos.</p>
        </div>
        <div className={`text-lg font-bold ${totalPercentual === 100 ? 'text-green-600' : 'text-itau-orange'}`}>
          Total: {totalPercentual}%
        </div>
      </div>

      {mensagem && (
        <div className={`mb-4 p-3 rounded-md text-sm border ${mensagem.tipo === 'sucesso' ? 'bg-green-50 text-green-700 border-green-200' : 'bg-red-50 text-red-600 border-red-200'}`}>
          {mensagem.texto}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        {itens.map((item, index) => (
          <div key={index} className="flex gap-4 items-center">
            <div className="flex-1">
              <label className="sr-only">Ticker do Ativo</label>
              <input
                type="text"
                placeholder="Ex: ITUB4"
                required
                className="w-full p-2 border border-gray-300 rounded focus:ring-2 focus:ring-itau-blue outline-none uppercase"
                value={item.ticker}
                onChange={(e) => handleItemChange(index, "ticker", e.target.value.toUpperCase())}
              />
            </div>
            <div className="w-32 relative">
              <label className="sr-only">Percentual</label>
              <input
                type="number"
                min="1"
                max="100"
                required
                className="w-full p-2 pr-8 border border-gray-300 rounded focus:ring-2 focus:ring-itau-blue outline-none"
                value={item.percentual || ""}
                onChange={(e) => handleItemChange(index, "percentual", Number(e.target.value))}
              />
              <span className="absolute right-3 top-2 text-gray-500">%</span>
            </div>
          </div>
        ))}

        <div className="pt-4 border-t border-gray-100">
          <button
            type="submit"
            disabled={!isValid || loading}
            className="w-full bg-itau-blue hover:bg-blue-900 text-white font-semibold py-3 rounded-md transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {loading ? "Salvando..." : "Salvar Top cinco"}
          </button>
        </div>
        
        {!isValid && totalPercentual !== 0 && (
          <p className="text-xs text-center text-gray-500 mt-2">
            A soma dos percentuais deve ser exatamente 100% e todos os tickers devem ser preenchidos.
          </p>
        )}
      </form>
    </div>
  );
}