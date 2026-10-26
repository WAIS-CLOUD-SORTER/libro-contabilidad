import { redirect } from "next/navigation";
import { loginAction } from "@/app/actions/auth";
import { ActionForm } from "@/components/action-form";
import { AuthLayout } from "@/components/auth-layout";
import { Field } from "@/components/ui";
import { getCurrentUser, userCount } from "@/lib/auth";

export const dynamic = "force-dynamic";

export const metadata = { title: "Entrar" };

export default async function LoginPage() {
  if (await getCurrentUser()) redirect("/panel");
  if (userCount() === 0) redirect("/setup");

  return (
    <AuthLayout
      title="Entra a tu contabilidad"
      subtitle="Usa la cuenta que creaste al configurar el sistema."
      footer={
        <span>
          ¿Olvidaste tu contraseña? Pídele al administrador que la restablezca desde Usuarios.
        </span>
      }
    >
      <ActionForm
        action={loginAction}
        submitLabel="Entrar"
        pendingLabel="Entrando…"
        className="space-y-4"
        submitClassName="mt-6 [&>button]:w-full"
      >
        <Field label="Correo" htmlFor="email" required>
          <input
            className="input"
            id="email"
            name="email"
            type="email"
            autoComplete="email"
            placeholder="tu@negocio.mx"
            required
            autoFocus
          />
        </Field>
        <Field label="Contraseña" htmlFor="password" required>
          <input
            className="input"
            id="password"
            name="password"
            type="password"
            autoComplete="current-password"
            placeholder="••••••••"
            required
          />
        </Field>
      </ActionForm>
    </AuthLayout>
  );
}
