"use client";
import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { maskCPF } from '@/utils/formatters';

export default function Home() {
  const [searchCpf, setSearchCpf] = useState("");
  const router = useRouter();

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    const cleanCpf = searchCpf.replace(/\D/g, "");
    if (cleanCpf) router.push(`/dashboard/${cleanCpf}`);
  };

  return (
    <div className="flex flex-col items-center justify-center min-h-[75vh] text-center space-y-12 animate-fade-in">
      <div className="space-y-4 max-w-2xl">
        <h1 className="text-4xl font-extrabold text-itau-blue sm:text-5xl">
          Itaú <span className="text-itau-orange">Corretora</span>
        </h1>
        <p className="text-lg text-gray-600">Gestão inteligente de compras programadas.</p>
      </div>

      <form onSubmit={handleSearch} className="w-full max-w-sm flex gap-2 p-2 bg-white rounded-lg shadow-sm border border-gray-200">
        <input 
          type="text" 
          placeholder="Consultar CPF (ex: 650.014...)"
          className="flex-1 p-2 outline-none text-sm"
          value={searchCpf}
          onChange={(e) => setSearchCpf(maskCPF(e.target.value))}
        />
        <button type="submit" className="bg-itau-blue text-white px-4 py-2 rounded text-sm font-bold">Consultar</button>
      </form>

      <div className="flex gap-4">
        <Link href="/adesao" className="px-8 py-3 bg-itau-orange text-white rounded-md font-bold shadow-md hover:bg-orange-600 transition-all">Nova Adesão</Link>
        <Link href="/admin" className="px-8 py-3 border-2 border-itau-blue text-itau-blue rounded-md font-bold hover:bg-blue-50 transition-all">Administração</Link>
      </div>
    </div>
  );
}