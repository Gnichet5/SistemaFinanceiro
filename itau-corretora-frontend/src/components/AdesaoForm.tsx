"use client";

import { useState } from "react";
import api from "@/services/api";
import { maskCPF, maskCurrencyInput, unmaskCurrency } from "@/utils/formatters";

export default function AdesaoForm() {
  const [formData, setFormData] = useState({
    nome: "",
    cpf: "",
    contaFilhote: "",
    valorMensal: 0,
  });

  const [loading, setLoading] = useState(false);
  const [status, setStatus] = useState<{ type: 'error' | 'success', msg: string } | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setStatus(null);

    // SANITIZAÇÃO DE DADOS
    const payload = {
      ...formData,
      cpf: formData.cpf.replace(/\D/g, ''), // Envia apenas números
      valorMensal: Number(formData.valorMensal) // Garante que é um Number
    };

    try {
      // Rota atualizada: /api/Clientes/adesao
      await api.post("/Clientes/adesao", payload);
      setStatus({ type: 'success', msg: 'Adesão realizada com sucesso!' });
      setFormData({ nome: "", cpf: "", contaFilhote: "", valorMensal: 0 });
    } catch (err: any) {
      setStatus({ 
        type: 'error', 
        msg: err.response?.data?.mensagem || "Verifique os dados informados (mínimo R$ 100,00)." 
      });
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="bg-white p-8 rounded-xl shadow-sm border border-gray-100 max-w-lg mx-auto mt-10">
      <h2 className="text-2xl font-bold text-itau-blue mb-6">Comece a Investir</h2>

      {status && (
        <div className={`mb-6 p-4 rounded-lg text-sm font-medium border ${status.type === 'success' ? 'bg-green-50 text-green-700 border-green-100' : 'bg-red-50 text-red-700 border-red-100'}`}>
          {status.msg}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-5">
        <div>
          <label className="block text-xs font-bold text-gray-400 uppercase mb-1">Nome Completo</label>
          <input
            type="text"
            required
            className="w-full p-3 bg-gray-50 border border-gray-200 rounded-lg focus:ring-2 focus:ring-itau-orange outline-none transition-all"
            value={formData.nome}
            onChange={(e) => setFormData({ ...formData, nome: e.target.value })}
          />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-xs font-bold text-gray-400 uppercase mb-1">CPF</label>
            <input
              type="text"
              required
              maxLength={14}
              placeholder="000.000.000-00"
              className="w-full p-3 bg-gray-50 border border-gray-200 rounded-lg focus:ring-2 focus:ring-itau-orange outline-none"
              value={formData.cpf}
              onChange={(e) => setFormData({ ...formData, cpf: maskCPF(e.target.value) })}
            />
          </div>
          <div>
            <label className="block text-xs font-bold text-gray-400 uppercase mb-1">Conta Filhote</label>
            <input
              type="text"
              required
              placeholder="AG-CC"
              className="w-full p-3 bg-gray-50 border border-gray-200 rounded-lg focus:ring-2 focus:ring-itau-orange outline-none"
              value={formData.contaFilhote}
              onChange={(e) => setFormData({ ...formData, contaFilhote: e.target.value })}
            />
          </div>
        </div>

        <div>
          <label className="block text-xs font-bold text-gray-400 uppercase mb-1">Aporte Mensal (Mín. R$ 100)</label>
          <input
            type="text"
            required
            placeholder="R$ 0,00"
            className="w-full p-3 bg-gray-50 border border-gray-200 rounded-lg focus:ring-2 focus:ring-itau-orange outline-none"
            value={formData.valorMensal ? maskCurrencyInput(formData.valorMensal.toString().replace('.', '')) : ""}
            onChange={(e) => setFormData({ ...formData, valorMensal: unmaskCurrency(e.target.value) })}
          />
        </div>

        <button
          type="submit"
          disabled={loading}
          className="w-full bg-itau-orange hover:bg-orange-600 text-white font-bold py-4 rounded-lg shadow-lg shadow-orange-200 transition-all disabled:opacity-50 active:scale-[0.98]"
        >
          {loading ? "Processando..." : "Confirmar Adesão"}
        </button>
      </form>
    </div>
  );
}