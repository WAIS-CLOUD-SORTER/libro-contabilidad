import { redirect } from "next/navigation";
import { setupAction } from "@/app/actions/auth";
import { ActionForm } from "@/components/action-form";
import { AuthLayout } from "@/components/auth-layout";
import { Field } from "@/components/ui";
import { userCount } from "@/lib/auth";

export const dynamic = "force-dynamic";

export const metadata = { title: "Configuración inicial" };

export default function SetupPage() {
  if (userCount() > 0) redirect("/login");

  return (
    <AuthLayout
      wide
      title="Prepara tu negocio"
      subtitle="Crea la cuenta de administrador. Después podrás agregar usuarios, cuentas de efectivo y tu catálogo."
      footer={
        <span>
          Todo se guarda en <strong className="font-semibold text-ink">data/contabilidad.db</strong> en
          esta computadora. Haz respaldos con frecuencia.
        </span>
      }
    >
      <ActionForm
        action={setupAction}
        submitLabel="Crear sistema"
        pendingLabel="Creando…"
        className="space-y-4"
        submitClassName="mt-6 [&>button]:w-full"
      >
        <Field label="Nombre del negocio" htmlFor="company" required>
          <input
            className="input"
            id="company"
            name="company"
            placeholder="Ej. Taller García"
            maxLength={120}
            required
            autoFocus
          />
        </Field>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Tu nombre" htmlFor="name" required>
            <input
              className="input"
              id="name"
              name="name"
              placeholder="Nombre completo"
              autoComplete="name"
              maxLength={120}
              required
            />
          </Field>
          <Field label="Correo" htmlFor="email" required>
            <input
              className="input"
              id="email"
              name="email"
              type="email"
              placeholder="tu@negocio.mx"
              autoComplete="email"
              required
            />
          </Field>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Contraseña" htmlFor="password" hint="Mínimo 8 caracteres." required>
            <input
              className="input"
              id="password"
              name="password"
              type="password"
              autoComplete="new-password"
              minLength={8}
              required
            />
          </Field>
          <Field label="Confirmar contraseña" htmlFor="confirm" required>
            <input
              className="input"
              id="confirm"
              name="confirm"
              type="password"
              autoComplete="new-password"
              minLength={8}
              required
            />
          </Field>
        </div>
      </ActionForm>
    </AuthLayout>
  );
}
