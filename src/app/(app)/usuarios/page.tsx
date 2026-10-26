import Link from "next/link";
import { notFound } from "next/navigation";
import { Plus, ShieldCheck, UserCheck, UserRound, Users } from "lucide-react";
import { createUserAction, toggleUserAction, updateUserAction } from "@/app/actions/users";
import { ActionForm } from "@/components/action-form";
import { Badge, EmptyState, Field, PageHeader, Stat } from "@/components/ui";
import { requirePermission } from "@/lib/auth";
import { getUserById, getUsers } from "@/lib/data";
import { formatDateTime } from "@/lib/dates";
import { ROLE_DESCRIPTIONS, ROLE_LABELS, type Role } from "@/lib/permissions";

export const dynamic = "force-dynamic";

export const metadata = { title: "Usuarios" };

const ROLES: Role[] = ["admin", "contador", "capturista", "consulta"];

export default async function UsersPage({
  searchParams,
}: {
  searchParams: Promise<{
    nuevo?: string;
    editar?: string;
    creado?: string;
    actualizado?: string;
  }>;
}) {
  const current = await requirePermission("users.manage");
  const params = await searchParams;
  const editing = params.editar ? getUserById(Number(params.editar)) : undefined;
  if (params.editar && !editing) notFound();
  const showForm = params.nuevo === "1" || Boolean(editing);
  const users = getUsers();
  const active = users.filter((user) => user.active === 1);
  const admins = active.filter((user) => user.role === "admin");

  return (
    <div className="max-w-[1000px]">
      <PageHeader
        title="Usuarios"
        description="Accesos al sistema y roles. Cada persona debe tener su propio correo y contraseña."
        actions={
          !showForm ? (
            <Link href="/usuarios?nuevo=1" className="btn btn-primary">
              <Plus size={16} />
              Nuevo usuario
            </Link>
          ) : (
            <Link href="/usuarios" className="btn btn-secondary">
              Cerrar
            </Link>
          )
        }
      />

      {params.creado === "1" && (
        <p className="form-ok mb-5" role="status">
          Usuario creado. Comparte la contraseña por un medio seguro.
        </p>
      )}
      {params.actualizado === "1" && (
        <p className="form-ok mb-5" role="status">
          Usuario actualizado.
        </p>
      )}

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat label="Usuarios" value={String(users.length)} icon={<Users size={17} />} />
        <Stat label="Activos" value={String(active.length)} icon={<UserCheck size={17} />} tone="accent" />
        <Stat label="Administradores" value={String(admins.length)} icon={<ShieldCheck size={17} />} />
        <Stat
          label="Inactivos"
          value={String(users.length - active.length)}
          icon={<UserRound size={17} />}
        />
      </div>

      {showForm && (
        <section className="card mt-5 p-5">
          <h2 className="section-title">{editing ? "Editar usuario" : "Nuevo usuario"}</h2>
          <p className="mt-0.5 text-xs text-muted">
            {editing
              ? "Puedes cambiar nombre, correo, rol, estatus y resetear la contraseña."
              : "Se creará con la contraseña temporal que escribas aquí."}
          </p>

          <ActionForm
            action={editing ? updateUserAction : createUserAction}
            submitLabel={editing ? "Guardar cambios" : "Crear usuario"}
            className="mt-4"
          >
            {editing && <input type="hidden" name="id" value={editing.id} />}
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Nombre" htmlFor="name" required>
                <input
                  className="input"
                  id="name"
                  name="name"
                  defaultValue={editing?.name ?? ""}
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
                  defaultValue={editing?.email ?? ""}
                  maxLength={160}
                  autoComplete="off"
                  required
                />
              </Field>
              <Field label="Rol" htmlFor="role" required>
                <select className="select" id="role" name="role" defaultValue={editing?.role ?? "capturista"}>
                  {ROLES.map((role) => (
                    <option key={role} value={role}>
                      {ROLE_LABELS[role]}
                    </option>
                  ))}
                </select>
              </Field>
              {editing ? (
                <Field
                  label="Nueva contraseña"
                  htmlFor="password"
                  hint={
                    editing.id === current.id
                      ? "Para tu propia cuenta, usa Mi cuenta."
                      : "Opcional. Si la escribes, se cierran sus sesiones."
                  }
                >
                  <input
                    className="input"
                    id="password"
                    name="password"
                    type="password"
                    autoComplete="new-password"
                    minLength={8}
                    disabled={editing.id === current.id}
                    placeholder="Dejar en blanco para conservar"
                  />
                </Field>
              ) : (
                <>
                  <Field label="Contraseña temporal" htmlFor="password" hint="Mínimo 8 caracteres." required>
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
                </>
              )}
            </div>

            {editing && (
              <label className="mt-4 flex cursor-pointer items-center gap-2 text-sm font-medium text-ink">
                <input
                  type="checkbox"
                  name="active"
                  value="1"
                  defaultChecked={editing.active === 1}
                  className="h-4 w-4 accent-[var(--accent)]"
                />
                Usuario activo (puede iniciar sesión)
              </label>
            )}

            <div className="mt-4 rounded-xl border border-line bg-elevated p-4">
              <p className="text-[11px] font-semibold uppercase tracking-wide text-muted">
                Qué puede hacer cada rol
              </p>
              <dl className="mt-2 space-y-1.5 text-[11px] leading-relaxed">
                {ROLES.map((role) => (
                  <div key={role} className="flex gap-2">
                    <dt className="w-24 flex-none font-semibold text-ink">{ROLE_LABELS[role]}</dt>
                    <dd className="text-muted">{ROLE_DESCRIPTIONS[role]}</dd>
                  </div>
                ))}
              </dl>
            </div>
          </ActionForm>
        </section>
      )}

      <div className="card mt-5 overflow-hidden">
        {users.length === 0 ? (
          <div className="p-5">
            <EmptyState
              icon={<Users size={18} />}
              title="Sin usuarios"
              description="Crea el primer acceso para tu equipo."
            />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="table">
              <thead>
                <tr>
                  <th>Usuario</th>
                  <th>Rol</th>
                  <th>Último acceso</th>
                  <th>Estatus</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {users.map((user) => (
                  <tr key={user.id}>
                    <td>
                      <p className="font-medium text-ink">
                        {user.name}
                        {user.id === current.id && (
                          <span className="ml-2 text-[11px] font-semibold text-muted">(tú)</span>
                        )}
                      </p>
                      <p className="mt-0.5 break-all text-[11px] text-muted">{user.email}</p>
                    </td>
                    <td>
                      <Badge tone={user.role === "admin" ? "accent" : "neutral"}>
                        {ROLE_LABELS[user.role]}
                      </Badge>
                    </td>
                    <td className="whitespace-nowrap text-xs text-muted">
                      {user.last_login_at ? formatDateTime(user.last_login_at) : "Nunca"}
                    </td>
                    <td>
                      <Badge tone={user.active === 1 ? "success" : "neutral"}>
                        {user.active === 1 ? "Activo" : "Inactivo"}
                      </Badge>
                    </td>
                    <td className="actions">
                      <div className="flex items-center justify-end gap-1">
                        <Link href={`/usuarios?editar=${user.id}`} className="btn btn-secondary btn-sm">
                          Editar
                        </Link>
                        {user.id !== current.id && (
                          <ActionForm
                            action={toggleUserAction}
                            submitLabel={user.active === 1 ? "Desactivar" : "Activar"}
                            variant="ghost"
                            small
                            confirm={
                              user.active === 1
                                ? "¿Desactivar este usuario? Se cerrarán sus sesiones."
                                : "¿Activar este usuario?"
                            }
                            submitClassName="mt-0 justify-end"
                          >
                            <input type="hidden" name="id" value={user.id} />
                          </ActionForm>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <p className="mt-4 text-[11px] leading-relaxed text-muted">
        Los usuarios comparten la misma base local. Si acceden por red, usa contraseñas únicas y
        mantén la computadora encendida y protegida.
      </p>
    </div>
  );
}
