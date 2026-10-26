import { changePasswordAction } from "@/app/actions/auth";
import { ActionForm } from "@/components/action-form";
import { Badge, Field, PageHeader } from "@/components/ui";
import { requireUser } from "@/lib/auth";
import { ROLE_DESCRIPTIONS, ROLE_LABELS } from "@/lib/permissions";

export const dynamic = "force-dynamic";

export const metadata = { title: "Mi cuenta" };

export default async function CuentaPage({
  searchParams,
}: {
  searchParams: Promise<{ ok?: string }>;
}) {
  const user = await requireUser();
  const params = await searchParams;

  return (
    <div className="max-w-[760px]">
      <PageHeader
        title="Mi cuenta"
        description="Tus datos de acceso y seguridad dentro del sistema."
      />

      {params.ok === "1" && (
        <p className="form-ok mb-5" role="status">
          Contraseña actualizada. Tus otras sesiones se cerraron.
        </p>
      )}

      <div className="grid gap-5 md:grid-cols-[1fr_1.2fr]">
        <section className="card p-5">
          <h2 className="section-title">Perfil</h2>
          <dl className="mt-4 space-y-3 text-sm">
            <div>
              <dt className="text-xs font-semibold text-muted">Nombre</dt>
              <dd className="mt-0.5 font-medium text-ink">{user.name}</dd>
            </div>
            <div>
              <dt className="text-xs font-semibold text-muted">Correo</dt>
              <dd className="mt-0.5 break-all font-medium text-ink">{user.email}</dd>
            </div>
            <div>
              <dt className="text-xs font-semibold text-muted">Rol</dt>
              <dd className="mt-1">
                <Badge tone="accent">{ROLE_LABELS[user.role]}</Badge>
              </dd>
            </div>
          </dl>
          <p className="mt-4 border-t border-line pt-3 text-xs leading-relaxed text-muted">
            {ROLE_DESCRIPTIONS[user.role]}
          </p>
        </section>

        <section className="card p-5">
          <h2 className="section-title">Cambiar contraseña</h2>
          <p className="mt-1 text-xs text-muted">
            Al guardar, se cerrarán las demás sesiones abiertas con tu cuenta.
          </p>
          <ActionForm
            action={changePasswordAction}
            submitLabel="Actualizar contraseña"
            pendingLabel="Actualizando…"
            className="mt-4 space-y-4"
          >
            <Field label="Contraseña actual" htmlFor="current" required>
              <input
                className="input"
                id="current"
                name="current"
                type="password"
                autoComplete="current-password"
                required
              />
            </Field>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Nueva contraseña" htmlFor="password" hint="Mínimo 8 caracteres." required>
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
              <Field label="Confirmar" htmlFor="confirm" required>
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
        </section>
      </div>
    </div>
  );
}
