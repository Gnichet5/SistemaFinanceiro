// src/app/dashboard/[cpf]/page.tsx
import DashboardView from "@/components/DashboardView";

interface DashboardPageProps {
  params: {
    cpf: string;
  };
}

export default function DashboardPage({ params }: DashboardPageProps) {
  // A página atua como Server Component e passa o parâmetro para a View
  return (
    <div className="py-4">
      <DashboardView cpf={params.cpf} />
    </div>
  );
}