import Link from "next/link";
import { Plus, Users } from "lucide-react";
import { toggleContactArchiveAction } from "@/app/actions/contacts";
import { ActionForm } from "@/components/action-form";
import { ContactForm } from "@/components/contact-form";
import { Badge, EmptyState, Money, PageHeader, Stat } from "@/components/ui";
import { requireUser } from "@/lib/auth";
import { getContact, getContactBalances, getContacts } from "@/lib/data";
import { can } from "@/lib/permissions";
import { formatMoney } from "@/lib/money";

export const dynamic = "force-dynamic";

export const metadata = { title: "Contactos" };

const TYPE_LABELS = {
  cliente: "Cliente",
  proveedor: "Proveedor",
  ambos: "Cliente y proveedor",
} as const;

export default async function ContactsPage({
  searchParams,
}: {
  searchParams: Promise<{ q?: string; tipo?: string; nuevo?: string; editar?: string; archivados?: string }>;
}) {
  const user = await requireUser();
  const params = await searchParams;
  const canWrite = can(user.role, "contacts.write");
  const includeArchived = params.archivados === "1";
  const q = (params.q ?? "").trim().toLowerCase();
  const tipo = params.tipo === "cliente" || params.tipo === "proveedor" ? params.tipo : "";
  const editing = Number(params.editar) > 0 ? getContact(Number(params.editar)) : undefined;
  const showForm = canWrite && (params.nuevo === "1" || Boolean(editing));

  const all = getContacts(true);
  const balances = getContactBalances();

  const visible = all
    .filter((contact) => includeArchived || contact.archived === 0)
    .filter((contact) => {
      if (!tipo) return true;
      return contact.type === tipo || contact.type === "ambos";
    })
    .filter((contact) => {
      if (!q) return true;
      return (
        contact.name.toLowerCase().includes(q) ||
        contact.rfc.toLowerCase().includes(q) ||
        contact.email.toLowerCase().includes(q)
      );
    });

  const active = all.filter((contact) => contact.archived === 0);
  const clientCount = active.filter(
    (contact) => contact.type === "cliente" || contact.type === "ambos",
  ).length;
  const providerCount = active.filter(
    (contact) => contact.type === "proveedor" || contact.type === "ambos",
  ).length;
  const receivable = Array.from(balances.values()).reduce((sum, value) => sum + value, 0);

  return (
    <div>
      <PageHeader
        title="Contactos"
        description="Clientes y proveedores con su RFC, datos de contacto y saldo pendiente."
        actions={
          canWrite && !showForm ? (
            <Link href="/contactos?nuevo=1" className="btn btn-primary">
              <Plus size={16} />
              Nuevo contacto
            </Link>
          ) : undefined
        }
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat label="Clientes" value={String(clientCount)} icon={<Users size={17} />} />
        <Stat label="Proveedores" value={String(providerCount)} icon={<Users size={17} />} />
        <Stat
          label="Por cobrar"
          value={formatMoney(receivable)}
          hint="Facturas emitidas"
          tone={receivable > 0 ? "accent" : "default"}
        />
        <Stat label="Archivados" value={String(all.filter((contact) => contact.archived === 1).length)} />
      </div>

      {showForm && (
        <div className="mt-5">
          <ContactForm contact={editing} />
        </div>
      )}

      <div className="mt-5 flex flex-wrap items-center justify-between gap-3">
        <form className="flex w-full flex-wrap gap-2 sm:w-auto" action="/contactos">
          <input
            className="input sm:w-64"
            name="q"
            defaultValue={params.q ?? ""}
            placeholder="Buscar nombre, RFC o correo…"
            aria-label="Buscar contactos"
          />
          <select className="select w-auto" name="tipo" defaultValue={tipo} aria-label="Tipo">
            <option value="">Todos</option>
            <option value="cliente">Clientes</option>
            <option value="proveedor">Proveedores</option>
          </select>
          {includeArchived && <input type="hidden" name="archivados" value="1" />}
          <button type="submit" className="btn btn-secondary">
            Filtrar
          </button>
        </form>
        <Link
          href={includeArchived ? "/contactos" : "/contactos?archivados=1"}
          className="text-xs font-semibold text-accent hover:underline"
        >
          {includeArchived ? "Ocultar archivados" : "Ver archivados"}
        </Link>
      </div>

      <div className="card mt-4 overflow-hidden">
        {visible.length === 0 ? (
          <div className="p-5">
            <EmptyState
              icon={<Users size={18} />}
              title="Sin contactos"
              description="Agrega clientes para facturar y proveedores para tus gastos."
              action={
                canWrite ? (
                  <Link href="/contactos?nuevo=1" className="btn btn-primary btn-sm">
                    Nuevo contacto
                  </Link>
                ) : undefined
              }
            />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="table">
              <thead>
                <tr>
                  <th>Nombre</th>
                  <th>Tipo</th>
                  <th>RFC</th>
                  <th>Contacto</th>
                  <th className="num">Por cobrar</th>
                  <th>Estado</th>
                  {canWrite && <th />}
                </tr>
              </thead>
              <tbody>
                {visible.map((contact) => (
                  <tr key={contact.id}>
                    <td>
                      <p className="font-medium text-ink">{contact.name}</p>
                      {contact.address && (
                        <p className="mt-0.5 max-w-[260px] truncate text-[11px] text-muted">
                          {contact.address}
                        </p>
                      )}
                    </td>
                    <td>
                      <Badge tone={contact.type === "proveedor" ? "neutral" : "accent"}>
                        {TYPE_LABELS[contact.type]}
                      </Badge>
                    </td>
                    <td className="whitespace-nowrap text-muted">{contact.rfc || "—"}</td>
                    <td>
                      <p className="text-[13px]">{contact.email || "—"}</p>
                      {contact.phone && <p className="text-[11px] text-muted">{contact.phone}</p>}
                    </td>
                    <td className="num">
                      <Money cents={balances.get(contact.id) ?? 0} />
                    </td>
                    <td>
                      {contact.archived ? (
                        <Badge tone="neutral">Archivado</Badge>
                      ) : (
                        <Badge tone="success">Activo</Badge>
                      )}
                    </td>
                    {canWrite && (
                      <td className="actions">
                        <div className="flex items-center justify-end gap-1">
                          <Link
                            href={`/contactos?editar=${contact.id}`}
                            className="btn btn-secondary btn-sm"
                          >
                            Editar
                          </Link>
                          <ActionForm
                            action={toggleContactArchiveAction}
                            submitLabel={contact.archived ? "Reactivar" : "Archivar"}
                            variant="ghost"
                            small
                            confirm={
                              contact.archived
                                ? "¿Reactivar este contacto?"
                                : "¿Archivar este contacto? No se borra su historial."
                            }
                            submitClassName="mt-0 justify-end"
                          >
                            <input type="hidden" name="id" value={contact.id} />
                            <input type="hidden" name="archived" value={contact.archived ? "0" : "1"} />
                          </ActionForm>
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
