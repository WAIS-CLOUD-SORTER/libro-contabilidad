import Link from "next/link";
import { notFound } from "next/navigation";
import {
  Building2,
  Database,
  Download,
  FolderArchive,
  Landmark,
  Plus,
  ReceiptText,
  Server,
  ShieldCheck,
  Tags,
  Wallet,
} from "lucide-react";
import {
  createBackupAction,
  deleteBackupAction,
  restoreDefaultsAction,
  saveAccountAction,
  saveCategoryAction,
  saveCompanyAction,
  toggleAccountAction,
  toggleCategoryAction,
} from "@/app/actions/settings";
import { ActionForm } from "@/components/action-form";
import { Badge, EmptyState, Field, PageHeader, SectionHeader } from "@/components/ui";
import { requirePermission } from "@/lib/auth";
import { formatBytes, listBackups } from "@/lib/backups";
import { getAccountBalances, getAuditLog, getCategories, getCompany } from "@/lib/data";
import { BACKUP_DIR, DB_PATH } from "@/lib/db";
import { formatDateTime } from "@/lib/dates";
import { centsToInput } from "@/lib/money";
import { DEFAULT_ISR_BRACKETS } from "@/lib/payroll";

export const dynamic = "force-dynamic";

export const metadata = { title: "Configuración" };

const ACTION_LABELS: Record<string, string> = {
  create: "Creó",
  update: "Actualizó",
  delete: "Eliminó",
  archive: "Archivó",
  restore: "Restauró",
  login: "Inició sesión",
  logout: "Cerró sesión",
  login_failed: "Intento fallido",
  setup: "Configuró",
  emit: "Emitió",
  cancel: "Canceló",
  payment: "Registró pago",
  delete_payment: "Eliminó pago",
  backup: "Creó respaldo",
  delete_backup: "Eliminó respaldo",
  pay: "Pagó nómina",
  recalculate: "Recalculó nómina",
  revert_payment: "Revirtió pago de nómina",
  deactivate: "Desactivó",
  change_password: "Cambió contraseña",
  entrada: "Entrada de inventario",
  salida: "Salida de inventario",
  ajuste: "Ajustó inventario",
};

const ENTITY_LABELS: Record<string, string> = {
  user: "Usuario",
  company: "Negocio",
  account: "Cuenta",
  category: "Categoría",
  transaction: "Movimiento",
  invoice: "Factura",
  contact: "Contacto",
  product: "Producto",
  inventory: "Inventario",
  employee: "Empleado",
  payroll: "Nómina",
  payroll_item: "Concepto de nómina",
  database: "Base de datos",
  isr_brackets: "Tarifa de ISR",
};

export default async function SettingsPage({
  searchParams,
}: {
  searchParams: Promise<{ cuenta?: string; categoria?: string; nuevo?: string }>;
}) {
  await requirePermission("settings.manage");
  const params = await searchParams;
  const company = getCompany();
  const accounts = getAccountBalances(true);
  const categories = getCategories(true);
  const backups = listBackups();
  const auditLog = getAuditLog(20);
  const editingAccount = params.cuenta ? accounts.find((a) => a.id === Number(params.cuenta)) : undefined;
  const editingCategory = params.categoria
    ? categories.find((c) => c.id === Number(params.categoria))
    : undefined;
  if (params.cuenta && !editingAccount) notFound();
  if (params.categoria && !editingCategory) notFound();

  const bracketsJson = company.isr_brackets?.trim()
    ? company.isr_brackets
    : JSON.stringify(DEFAULT_ISR_BRACKETS, null, 2);

  return (
    <div className="max-w-[1100px]">
      <PageHeader
        title="Configuración"
        description="Datos del negocio, cuentas, categorías, respaldos y actividad reciente."
      />

      <div className="mb-5 flex flex-wrap gap-2">
        {[
          ["#empresa", "Empresa", Building2],
          ["#fiscal", "ISR", Landmark],
          ["#cuentas", "Cuentas", Wallet],
          ["#categorias", "Categorías", Tags],
          ["#respaldo", "Respaldos", Database],
          ["#actividad", "Actividad", ShieldCheck],
        ].map(([href, label, Icon]) => {
          const IconComponent = Icon as typeof Building2;
          return (
            <Link key={href as string} href={href as string} className="btn btn-secondary btn-sm">
              <IconComponent size={14} />
              {label as string}
            </Link>
          );
        })}
      </div>

      <section id="empresa" className="card scroll-mt-20 p-5">
        <SectionHeader
          title="Datos del negocio"
          description="Se usan en facturas y reportes internos."
          action={<Badge tone="neutral">Solo administradores</Badge>}
        />
        <ActionForm
          action={saveCompanyAction}
          submitLabel="Guardar empresa"
          pendingLabel="Guardando…"
          className="mt-5"
        >
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Nombre o razón social" htmlFor="name" required>
              <input className="input" id="name" name="name" defaultValue={company.name} maxLength={120} required />
            </Field>
            <Field label="RFC" htmlFor="rfc" hint="Opcional. No se valida contra el SAT.">
              <input
                className="input uppercase"
                id="rfc"
                name="rfc"
                defaultValue={company.rfc}
                maxLength={20}
                placeholder="XAXX010101000"
              />
            </Field>
            <Field label="Régimen fiscal" htmlFor="regimenFiscal" hint="Texto descriptivo, ej. RESICO, Actividad empresarial.">
              <input
                className="input"
                id="regimenFiscal"
                name="regimenFiscal"
                defaultValue={company.regimen_fiscal}
                maxLength={120}
              />
            </Field>
            <Field label="Teléfono" htmlFor="phone">
              <input className="input" id="phone" name="phone" defaultValue={company.phone} maxLength={60} />
            </Field>
            <Field label="Correo" htmlFor="email">
              <input className="input" id="email" name="email" type="email" defaultValue={company.email} maxLength={160} />
            </Field>
            <Field label="Dirección" htmlFor="address">
              <input className="input" id="address" name="address" defaultValue={company.address} maxLength={500} />
            </Field>
          </div>

          <div className="mt-5 grid gap-4 border-t border-line pt-5 sm:grid-cols-2 lg:grid-cols-4">
            <Field label="IVA predeterminado" htmlFor="ivaRate" hint="Para productos y facturas nuevas.">
              <select className="select" id="ivaRate" name="ivaRate" defaultValue={String(company.iva_rate)}>
                <option value="0.16">16%</option>
                <option value="0.08">8% frontera</option>
                <option value="0">0% / exento</option>
              </select>
            </Field>
            <Field label="Serie de facturas" htmlFor="invoiceSeries" required>
              <input
                className="input uppercase"
                id="invoiceSeries"
                name="invoiceSeries"
                defaultValue={company.invoice_series}
                maxLength={8}
                required
              />
            </Field>
            <Field label="Siguiente folio" htmlFor="nextInvoiceNumber" required>
              <input
                className="input text-right tabular-nums"
                id="nextInvoiceNumber"
                name="nextInvoiceNumber"
                type="number"
                min={1}
                step={1}
                defaultValue={company.next_invoice_number}
                required
              />
            </Field>
            <Field label="Tasa IMSS estimada" htmlFor="imssRate" hint="Ej. 0.025 = 2.5% del bruto.">
              <input
                className="input text-right tabular-nums"
                id="imssRate"
                name="imssRate"
                inputMode="decimal"
                defaultValue={String(company.imss_rate)}
              />
            </Field>
          </div>
        </ActionForm>
      </section>

      <section id="fiscal" className="card mt-5 scroll-mt-20 p-5">
        <SectionHeader
          title="Tarifa de ISR mensual"
          description="Arreglo JSON editable. Se usa para estimar ISR en nómina."
        />
        <div className="mt-4 grid gap-4 lg:grid-cols-[1.5fr_1fr]">
          <ActionForm
            action={saveCompanyAction}
            submitLabel="Guardar tarifa"
            pendingLabel="Guardando…"
            hideSubmit
          >
            <input type="hidden" name="name" value={company.name} />
            <input type="hidden" name="rfc" value={company.rfc} />
            <input type="hidden" name="regimenFiscal" value={company.regimen_fiscal} />
            <input type="hidden" name="address" value={company.address} />
            <input type="hidden" name="phone" value={company.phone} />
            <input type="hidden" name="email" value={company.email} />
            <input type="hidden" name="ivaRate" value={String(company.iva_rate)} />
            <input type="hidden" name="invoiceSeries" value={company.invoice_series} />
            <input type="hidden" name="nextInvoiceNumber" value={String(company.next_invoice_number)} />
            <input type="hidden" name="imssRate" value={String(company.imss_rate)} />
            <Field
              label="Tabla de ISR (JSON)"
              htmlFor="isrBrackets"
              hint="Cada renglón: lower (desde), upper (hasta o null), rate (tasa) y fixed (cuota fija). Valores en pesos."
            >
              <textarea
                className="textarea font-mono text-[12px] leading-relaxed"
                id="isrBrackets"
                name="isrBrackets"
                defaultValue={bracketsJson}
                rows={14}
                spellCheck={false}
              />
            </Field>
            <div className="mt-3 flex justify-end">
              <button type="submit" className="btn btn-secondary">
                Guardar tarifa de ISR
              </button>
            </div>
          </ActionForm>

          <div className="space-y-3">
            <div className="rounded-xl border border-line bg-elevated p-4 text-xs leading-relaxed text-muted">
              <p className="font-semibold text-ink">Cómo se calcula</p>
              <p className="mt-1">
                Se toma el sueldo del periodo (mensual o mitad para quincenal), se prorratea por días
                trabajados, se suma la percepción extra y se aplica la tarifa. No incluye subsidio al
                empleo, prima vacacional, aguinaldo ni cuotas obrero-patronales completas.
              </p>
            </div>
            <ActionForm
              action={restoreDefaultsAction}
              submitLabel="Restaurar tarifa de referencia"
              variant="secondary"
              confirm="¿Restaurar la tarifa de ISR de referencia? Se reemplazará la tabla actual."
              submitClassName="mt-0 [&>button]:w-full"
            >
              <Field label='Escribe "restaurar" para confirmar' htmlFor="confirmRestore">
                <input className="input" id="confirmRestore" name="confirm" placeholder="restaurar" />
              </Field>
            </ActionForm>
            <p className="text-[11px] leading-relaxed text-muted">
              Tarifa de referencia editable. Verifica siempre con tu contador o con las tablas
              vigentes del SAT.
            </p>
          </div>
        </div>
      </section>

      <section id="cuentas" className="card mt-5 scroll-mt-20 overflow-hidden">
        <SectionHeader
          title="Cuentas de efectivo y banco"
          description="El saldo se calcula con el saldo inicial, movimientos y pagos de facturas."
          action={
            !editingAccount ? (
              <Link href="/configuracion?nuevo=cuenta#cuentas" className="btn btn-secondary btn-sm">
                <Plus size={14} />
                Nueva cuenta
              </Link>
            ) : (
              <Link href="/configuracion#cuentas" className="btn btn-ghost btn-sm">
                Cerrar
              </Link>
            )
          }
        />
        {(params.nuevo === "cuenta" || editingAccount) && (
          <div className="border-b border-line bg-elevated/50 p-5">
            <ActionForm
              action={saveAccountAction}
              submitLabel={editingAccount ? "Guardar cuenta" : "Crear cuenta"}
              className="grid gap-4 sm:grid-cols-3"
            >
              {editingAccount && <input type="hidden" name="id" value={editingAccount.id} />}
              <Field label="Nombre" htmlFor="accountName" required>
                <input
                  className="input"
                  id="accountName"
                  name="name"
                  defaultValue={editingAccount?.name ?? ""}
                  maxLength={80}
                  required
                />
              </Field>
              <Field label="Tipo" htmlFor="accountType" required>
                <select
                  className="select"
                  id="accountType"
                  name="type"
                  defaultValue={editingAccount?.type ?? "banco"}
                >
                  <option value="efectivo">Efectivo</option>
                  <option value="banco">Banco</option>
                  <option value="tarjeta">Tarjeta</option>
                </select>
              </Field>
              <Field label="Saldo inicial" htmlFor="openingBalance" hint="Al empezar a usar el sistema.">
                <input
                  className="input text-right tabular-nums"
                  id="openingBalance"
                  name="openingBalance"
                  inputMode="decimal"
                  defaultValue={editingAccount ? centsToInput(editingAccount.opening_balance_cents) : "0.00"}
                />
              </Field>
            </ActionForm>
          </div>
        )}
        <div className="overflow-x-auto">
          <table className="table">
            <thead>
              <tr>
                <th>Cuenta</th>
                <th>Tipo</th>
                <th className="num">Saldo inicial</th>
                <th className="num">Saldo actual</th>
                <th>Estatus</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {accounts.map((account) => (
                <tr key={account.id}>
                  <td className="font-medium">{account.name}</td>
                  <td className="capitalize text-muted">{account.type}</td>
                  <td className="num text-muted">{centsToInput(account.opening_balance_cents)}</td>
                  <td className="num font-semibold tabular-nums">
                    {new Intl.NumberFormat("es-MX", {
                      style: "currency",
                      currency: "MXN",
                    }).format(account.balance_cents / 100)}
                  </td>
                  <td>
                    <Badge tone={account.active === 1 ? "success" : "neutral"}>
                      {account.active === 1 ? "Activa" : "Inactiva"}
                    </Badge>
                  </td>
                  <td className="actions">
                    <div className="flex items-center justify-end gap-1">
                      <Link
                        href={`/configuracion?cuenta=${account.id}#cuentas`}
                        className="btn btn-ghost btn-sm"
                      >
                        Editar
                      </Link>
                      <ActionForm
                        action={toggleAccountAction}
                        submitLabel={account.active === 1 ? "Desactivar" : "Activar"}
                        variant="ghost"
                        small
                        confirm={
                          account.active === 1
                            ? "¿Desactivar esta cuenta? No aparecerá en movimientos nuevos."
                            : "¿Activar esta cuenta?"
                        }
                        submitClassName="mt-0 justify-end"
                      >
                        <input type="hidden" name="id" value={account.id} />
                        <input type="hidden" name="active" value={account.active === 1 ? "0" : "1"} />
                      </ActionForm>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section id="categorias" className="card mt-5 scroll-mt-20 overflow-hidden">
        <SectionHeader
          title="Categorías de movimientos"
          description="Clasifican ingresos y gastos en reportes e IVA."
          action={
            !editingCategory ? (
              <Link href="/configuracion?nuevo=categoria#categorias" className="btn btn-secondary btn-sm">
                <Plus size={14} />
                Nueva categoría
              </Link>
            ) : (
              <Link href="/configuracion#categorias" className="btn btn-ghost btn-sm">
                Cerrar
              </Link>
            )
          }
        />
        {(params.nuevo === "categoria" || editingCategory) && (
          <div className="border-b border-line bg-elevated/50 p-5">
            <ActionForm
              action={saveCategoryAction}
              submitLabel={editingCategory ? "Guardar categoría" : "Crear categoría"}
              className="grid gap-4 sm:grid-cols-[1.5fr_1fr_auto]"
            >
              {editingCategory && <input type="hidden" name="id" value={editingCategory.id} />}
              <Field label="Nombre" htmlFor="categoryName" required>
                <input
                  className="input"
                  id="categoryName"
                  name="name"
                  defaultValue={editingCategory?.name ?? ""}
                  maxLength={80}
                  required
                />
              </Field>
              <Field label="Tipo" htmlFor="categoryKind" required>
                <select
                  className="select"
                  id="categoryKind"
                  name="kind"
                  defaultValue={editingCategory?.kind ?? "gasto"}
                >
                  <option value="ingreso">Ingreso</option>
                  <option value="gasto">Gasto</option>
                  <option value="costo">Costo</option>
                </select>
              </Field>
              <div className="flex items-end">
                <p className="pb-2 text-[11px] leading-relaxed text-muted">
                  Las categorías archivadas no aparecen en formularios nuevos.
                </p>
              </div>
            </ActionForm>
          </div>
        )}
        <div className="overflow-x-auto">
          <table className="table">
            <thead>
              <tr>
                <th>Categoría</th>
                <th>Tipo</th>
                <th>Estatus</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {categories.map((category) => (
                <tr key={category.id}>
                  <td className="font-medium">{category.name}</td>
                  <td>
                    <Badge
                      tone={
                        category.kind === "ingreso" ? "success" : category.kind === "costo" ? "warn" : "neutral"
                      }
                    >
                      {category.kind === "ingreso" ? "Ingreso" : category.kind === "gasto" ? "Gasto" : "Costo"}
                    </Badge>
                  </td>
                  <td>
                    <Badge tone={category.archived === 0 ? "success" : "neutral"}>
                      {category.archived === 0 ? "Activa" : "Archivada"}
                    </Badge>
                  </td>
                  <td className="actions">
                    <div className="flex items-center justify-end gap-1">
                      <Link
                        href={`/configuracion?categoria=${category.id}#categorias`}
                        className="btn btn-ghost btn-sm"
                      >
                        Editar
                      </Link>
                      <ActionForm
                        action={toggleCategoryAction}
                        submitLabel={category.archived === 0 ? "Archivar" : "Reactivar"}
                        variant="ghost"
                        small
                        submitClassName="mt-0 justify-end"
                      >
                        <input type="hidden" name="id" value={category.id} />
                        <input
                          type="hidden"
                          name="archived"
                          value={category.archived === 0 ? "1" : "0"}
                        />
                      </ActionForm>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section id="respaldo" className="card mt-5 scroll-mt-20 overflow-hidden">
        <SectionHeader
          title="Respaldos"
          description="Copias consistentes de la base de datos. Descárgalas a otra ubicación con frecuencia."
        />
        <div className="grid gap-5 p-5 lg:grid-cols-[1fr_1.4fr]">
          <div>
            <div className="rounded-xl border border-line bg-elevated p-4 text-xs leading-relaxed text-muted">
              <p className="flex items-center gap-2 font-semibold text-ink">
                <Server size={15} />
                Ubicación local
              </p>
              <p className="mt-2 break-all font-mono text-[11px]">{DB_PATH}</p>
              <p className="mt-3 flex items-center gap-2 font-semibold text-ink">
                <FolderArchive size={15} />
                Carpeta de respaldos
              </p>
              <p className="mt-2 break-all font-mono text-[11px]">{BACKUP_DIR}</p>
            </div>
            <ActionForm
              action={createBackupAction}
              submitLabel="Crear respaldo ahora"
              pendingLabel="Creando…"
              className="mt-4"
              submitClassName="mt-0"
            >
              <p className="text-xs leading-relaxed text-muted">
                Se genera un archivo .db con la información al momento. Puedes descargarlo y
                guardarlo en una USB o en la nube.
              </p>
            </ActionForm>
          </div>

          <div>
            {backups.length === 0 ? (
              <EmptyState
                icon={<Database size={18} />}
                title="Aún no hay respaldos"
                description="Crea el primero antes de hacer cambios importantes."
              />
            ) : (
              <div className="overflow-hidden rounded-xl border border-line">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Archivo</th>
                      <th className="num">Tamaño</th>
                      <th />
                    </tr>
                  </thead>
                  <tbody>
                    {backups.slice(0, 12).map((file) => (
                      <tr key={file.name}>
                        <td>
                          <p className="break-all font-medium">{file.name}</p>
                          <p className="text-[11px] text-muted">{formatDateTime(file.modifiedAt)}</p>
                        </td>
                        <td className="num text-muted">{formatBytes(file.size)}</td>
                        <td className="actions">
                          <div className="flex items-center justify-end gap-1">
                            <a
                              href={`/api/respaldo/${encodeURIComponent(file.name)}`}
                              className="btn btn-secondary btn-sm"
                            >
                              <Download size={13} />
                              Descargar
                            </a>
                            <ActionForm
                              action={deleteBackupAction}
                              submitLabel="Eliminar"
                              variant="danger-ghost"
                              small
                              confirm={`¿Eliminar el respaldo ${file.name}?`}
                              submitClassName="mt-0 justify-end"
                            >
                              <input type="hidden" name="name" value={file.name} />
                            </ActionForm>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            <p className="mt-3 text-[11px] leading-relaxed text-muted">
              Para restaurar: cierra la aplicación, reemplaza <code>data/contabilidad.db</code> por el
              archivo descargado y vuelve a iniciar. Conserva el archivo actual antes de hacerlo.
            </p>
          </div>
        </div>
      </section>

      <section id="actividad" className="card mt-5 scroll-mt-20 overflow-hidden">
        <SectionHeader
          title="Actividad reciente"
          description="Últimos 20 eventos registrados en la bitácora."
        />
        <div className="overflow-x-auto">
          <table className="table">
            <thead>
              <tr>
                <th>Fecha</th>
                <th>Usuario</th>
                <th>Acción</th>
                <th>Entidad</th>
                <th>Detalle</th>
              </tr>
            </thead>
            <tbody>
              {auditLog.map((entry) => (
                <tr key={entry.id}>
                  <td className="whitespace-nowrap text-muted">{formatDateTime(entry.created_at)}</td>
                  <td className="font-medium">{entry.user_name ?? "Sistema"}</td>
                  <td>
                    <Badge tone={entry.action.includes("delete") ? "danger" : "neutral"}>
                      {ACTION_LABELS[entry.action] ?? entry.action}
                    </Badge>
                  </td>
                  <td className="text-muted">
                    {ENTITY_LABELS[entry.entity] ?? entry.entity}
                    {entry.entity_id
                      ? Number.isNaN(Number(entry.entity_id))
                        ? ` ${entry.entity_id}`
                        : ` #${entry.entity_id}`
                      : ""}
                  </td>
                  <td className="max-w-[320px] truncate font-mono text-[10.5px] text-muted">
                    {entry.detail && entry.detail !== "null" ? entry.detail : "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <p className="mt-5 flex items-start gap-2 text-[11px] leading-relaxed text-muted">
        <ReceiptText size={14} className="mt-0.5 flex-none" />
        Este sistema es una herramienta interna de control. No timbra CFDI, no presenta declaraciones
        y no sustituye la asesoría de un contador.
      </p>
    </div>
  );
}
