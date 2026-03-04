"use client";
import { useState } from "react";
import { Play, CheckCircle2, AlertTriangle } from "lucide-react";
import api from "@/services/api";

export default function MotorSimulador() {
  const [loading, setLoading] = useState(false);
  const [status, setStatus] = useState<'idle' | 'success' | 'error'>('idle');

  const handleExecutarMotor = async () => {
    setLoading(true);
    setStatus('idle');

    try {
      await api.post("/motor/executar-compra", { 
        dataReferencia: "2026-03-05T10:00:00Z" 
        });
      setStatus('success');
      
      setTimeout(() => setStatus('idle'), 5000);
    } catch (error) {
      setStatus('error');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="bg-white p-6 rounded-lg shadow-sm border border-itau-blue max-w-2xl mx-auto mt-8 relative overflow-hidden">
      <div className="absolute top-0 left-0 w-2 h-full bg-itau-orange"></div>
      
      <div className="flex flex-col sm:flex-row items-center justify-between gap-4 ml-4">
        <div>
          <h3 className="text-lg font-bold text-gray-900 flex items-center gap-2">
            Simulador de Ciclo de Compras
          </h3>
          <p className="text-sm text-gray-500 mt-1 max-w-md">
            Execute manualmente o motor do backend para processar os aportes e comprar os ativos da Top cinco para todos os clientes ativos.
          </p>
        </div>

        <button
          onClick={handleExecutarMotor}
          disabled={loading}
          className="flex-shrink-0 flex items-center gap-2 bg-itau-orange hover:bg-orange-600 text-white font-semibold py-3 px-6 rounded-md transition-all disabled:opacity-50"
        >
          {loading ? (
            <div className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin"></div>
          ) : (
            <Play size={20} />
          )}
          {loading ? "Processando..." : "Executar Motor"}
        </button>
      </div>

      {status === 'success' && (
        <div className="mt-4 ml-4 p-3 bg-green-50 text-green-700 rounded-md text-sm flex items-center gap-2 animate-fade-in">
          <CheckCircle2 size={18} />
          Ciclo executado com sucesso! O motor realizou as compras.
        </div>
      )}
      {status === 'error' && (
        <div className="mt-4 ml-4 p-3 bg-red-50 text-red-600 rounded-md text-sm flex items-center gap-2 animate-fade-in">
          <AlertTriangle size={18} />
          Erro ao executar o motor. Verifique a conexão com a API.
        </div>
      )}
    </div>
  );
}