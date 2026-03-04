import CestaAdminForm from "@/components/CestaAdminForm";
import MotorSimulador from "@/components/MotorSimulador";

export default function AdminPage() {
  return (
    <div className="py-8 animate-fade-in space-y-8">
      <div>
        <h1 className="text-3xl font-bold text-itau-blue tracking-tight text-center mb-2">
          Administração
        </h1>
        <p className="text-gray-500 text-center max-w-2xl mx-auto mb-8">
          Área restrita para definição da Cesta Top Five e execução manual do motor de compras programadas.
        </p>
      </div>
      
      <CestaAdminForm />
      <MotorSimulador />
    </div>
  );
}