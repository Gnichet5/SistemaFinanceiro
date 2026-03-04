// src/app/layout.tsx
import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import Link from 'next/link';
import './globals.css'; 
const inter = Inter({ subsets: ['latin'] });

export const metadata: Metadata = {
  title: 'Itaú Corretora | Compras Programadas',
  description: 'Sistema de compras programadas e gestão de cestas de ativos.',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="pt-BR">
      <body className={`${inter.className} bg-itau-bg min-h-screen flex flex-col antialiased text-gray-800`}>
        
        <header className="bg-itau-blue shadow-md sticky top-0 z-50">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="flex justify-between items-center h-16">
              
              <div className="flex-shrink-0 flex items-center gap-2 cursor-pointer">
                <span className="text-itau-orange font-bold text-2xl tracking-tight">
                  Itaú
                </span>
                <span className="text-white font-medium text-lg hidden sm:block border-l border-gray-500 pl-2 ml-1">
                  Corretora
                </span>
              </div>
              <nav className="hidden md:flex space-x-8">
                <Link href="/" className="text-gray-300 hover:text-white transition-colors text-sm font-medium">
                  Início
                </Link>
                <Link href="/adesao" className="text-gray-300 hover:text-white transition-colors text-sm font-medium">
                  Nova Adesão
                </Link>
                <Link href="/admin" className="text-gray-300 hover:text-white transition-colors text-sm font-medium">
                  Administração (Motor & Cestas)
                </Link>
              </nav>

              <div className="flex items-center">
                <div className="w-8 h-8 rounded-full bg-itau-orange text-white flex items-center justify-center font-bold text-sm shadow-sm">
                  G
                </div>
              </div>

            </div>
          </div>
        </header>
        <main className="flex-grow max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 w-full">
          {children}
        </main>

        <footer className="bg-white border-t border-gray-200 mt-auto">
          <div className="max-w-7xl mx-auto px-4 py-4 text-center text-sm text-gray-500">
            © {new Date().getFullYear()} Itaú Corretora - Desafio Técnico
          </div>
        </footer>

      </body>
    </html>
  );
}