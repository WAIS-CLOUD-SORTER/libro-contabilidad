import Link from "next/link";
import { notFound } from "next/navigation";
import {
  ArrowLeft,
  Calculator,
  CheckCircle2,
  Pencil,
  Trash2,
  Users,
  Wallet,
} from "lucide-react";
import {
  deletePayrollItemAction,
  deletePayrollPeriodAction,
  payPayrollAction,
  recalculatePayrollAction,
  revertPayrollPaymentAction,
  updatePayrollItemAction,
} from "@/app/actions/payroll";
import { ActionForm } from "@/components/action-form";
import { Badge, EmptyState, Field, Money, PageHeader, SectionHeader, Stat } from "@/components/ui";
import { requirePermission } from "@/lib/auth";
import { getAccounts, getPayrollItems, getPayrollPeriod } from "@/lib/data";
import { formatDate, formatDateTime, todayISO } from "@/lib/dates";
import { centsToInput, formatMoney } from "@/lib/money";
import { payrollPeriodLabel } from "@/lib/payroll";
import { can } from "@/lib/permissions";

export const dynamic = "force-dynamic";

export const metadata = { title: "Nómina · Periodo" };

export default async function PayrollDetailPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ editar?: string }>;
}) {
  const user = await requirePermission("payroll.view");
  const { id } = await params;
  const query = await searchParams;
  const periodId = Number(id);
  if (!Number.isInteger(periodId) || periodId <= 0) notFound();

  const period = getPayrollPeriod(periodId);
  if (!period) notFound();
  const items = getPayrollItems(periodId);
  const accounts = getAccounts();
  const canWrite = can(user.role, "payroll.write");
  const isDraft = period.status === "borrador";
  const editing =
    canWrite && isDraft && Number(query.editar) > 0
      ? items.find((item) => item.id === Number(query.editar))
      : undefined;

  const gross = Number(period.gross_cents);
  const deductions = Number(period.deductions_cents);
  const net = Number(period.net_cents);

  return (
    <div>
      <PageHeader
        eyebrow="Nómina"
        title={payrollPeriodLabel(period)}
        description={`${formatDate(period.start_date)} al ${formatDate(period.end_date)} · ${
          isDraft ? "Borrador editable" : "Periodo pagado"
        }`}
        actions={
          <>
            <Link href="/nomina" className="btn btn-secondary">
              <ArrowLeft size={15} />
              Volver
            </Link>
            {canWrite && isDraft && items.length > 0 && (
              <ActionForm
                action={recalculatePayrollAction}
                submitLabel="Recalcular"
                pendingLabel="Recalculando…"
                variant="secondary"
                confirm="Se recalcularán sueldos, ISR e IMSS con los datos actuales de los empleados. Se conservan percepciones extra, deducciones manuales y notas."
                submitClassName="mt-0"
              >
                <input type="hidden" name="id" value={period.id} />
              </ActionForm>
            )}
          </>
        }
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat
          label="Empleados"
          value={String(items.length)}
          hint="Incluidos en el periodo"
          icon={<Users size={17} />}
        />
        <Stat
          label="Bruto"
          value={formatMoney(gross)}
          hint="Sueldo + percepciones"
          icon={<Wallet size={17} />}
        />
        <Stat
          label="Deducciones"
          value={formatMoney(deductions)}
          hint="ISR, IMSS y otras"
          icon={<Calculator size={17} />}
        />
        <Stat
          label="Neto a pagar"
          value={formatMoney(net)}
          hint="Importe que sale de la cuenta"
          icon={<CheckCircle2 size={17} />}
          tone="accent"
        />
      </div>

      {editing && (
        <section className="card mt-5 p-5">
          <div className="flex items-start justify-between gap-3">
            <div>
              <h2 className="section-title">Ajustar concepto</h2>
              <p className="mt-0.5 text-xs text-muted">
                {editing.employee_name} · El neto se recalcula al guardar.
              </p>
            </div>
            <Link href={`/nomina/${period.id}`} className="btn btn-ghost btn-sm">
              Cerrar
            </Link>
          </div>

          <ActionForm
            action={updatePayrollItemAction}
            submitLabel="Guardar concepto"
            className="mt-4"
          >
            <input type="hidden" name="itemId" value={editing.id} />
            <input type="hidden" name="periodId" value={period.id} />
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <Field label="Días trabajados" htmlFor="days">
                <input
                  className="input text-right tabular-nums"
                  id="days"
                  name="days"
                  inputMode="decimal"
                  defaultValue={String(editing.days)}
                />
              </Field>
              <Field label="Sueldo base" htmlFor="base" hint="Sin IVA.">
                <input
                  className="input text-right tabular-nums"
                  id="base"
                  name="base"
                  inputMode="decimal"
                  defaultValue={centsToInput(editing.base_cents)}
                />
              </Field>
              <Field label="Percepciones extra" htmlFor="extra">
                <input
                  className="input text-right tabular-nums"
                  id="extra"
                  name="extra"
                  inputMode="decimal"
                  defaultValue={centsToInput(editing.extra_perceptions_cents)}
                />
              </Field>
              <Field label="ISR" htmlFor="isr">
                <input
                  className="input text-right tabular-nums"
                  id="isr"
                  name="isr"
                  inputMode="decimal"
                  defaultValue={centsToInput(editing.isr_cents)}
                />
              </Field>
              <Field label="IMSS" htmlFor="imss">
                <input
                  className="input text-right tabular-nums"
                  id="imss"
                  name="imss"
                  inputMode="decimal"
                  defaultValue={centsToInput(editing.imss_cents)}
                />
              </Field>
              <Field label="Infonavit" htmlFor="infonavit">
                <input
                  className="input text-right tabular-nums"
                  id="infonavit"
                  name="infonavit"
                  inputMode="decimal"
                  defaultValue={centsToInput(editing.infonavit_cents)}
                />
              </Field>
              <Field label="Otras deducciones" htmlFor="other">
                <input
                  className="input text-right tabular-nums"
                  id="other"
                  name="other"
                  inputMode="decimal"
                  defaultValue={centsToInput(editing.other_deductions_cents)}
                />
              </Field>
              <Field label="Notas" htmlFor="notes">
                <input
                  className="input"
                  id="notes"
                  name="notes"
                  defaultValue={editing.notes}
                  maxLength={500}
                  placeholder="Opcional"
                />
              </Field>
            </div>
          </ActionForm>
        </section>
      )}

      <div className="mt-5 grid items-start gap-5 lg:grid-cols-[minmax(0,1.7fr)_minmax(300px,1fr)]">
        <section className="card overflow-hidden">
          <SectionHeader
            title="Conceptos"
            description={`${items.length} empleado(s) en el periodo`}
          />
          {items.length === 0 ? (
            <div className="p-5">
              <EmptyState
                icon={<Users size={18} />}
                title="Sin empleados en este periodo"
                description="Registra empleados activos y usa Recalcular para agregarlos, o elimina el periodo."
                action={
                  <Link href="/empleados?nuevo=1" className="btn btn-primary btn-sm">
                    Agregar empleado
                  </Link>
                }
              />
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="table">
                <thead>
                  <tr>
                    <th>Empleado</th>
                    <th className="num">Días</th>
                    <th className="num">Base</th>
                    <th className="num">Extra</th>
                    <th className="num">ISR</th>
                    <th className="num">IMSS</th>
                    <th className="num">Otras</th>
                    <th className="num">Neto</th>
                    {canWrite && isDraft && <th />}
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr key={item.id}>
                      <td>
                        <p className="font-medium text-ink">{item.employee_name}</p>
                        <p className="mt-0.5 text-[11px] text-muted">
                          {item.employee_position || "Sin puesto"}
                          {item.infonavit_cents > 0
                            ? ` · Infonavit ${formatMoney(item.infonavit_cents)}`
                            : ""}
                        </p>
                      </td>
                      <td className="num text-muted">{item.days}</td>
                      <td className="num">
                        <Money cents={item.base_cents} />
                      </td>
                      <td className="num text-muted">
                        <Money cents={item.extra_perceptions_cents} />
                      </td>
                      <td className="num text-muted">
                        <Money cents={item.isr_cents} />
                      </td>
                      <td className="num text-muted">
                        <Money cents={item.imss_cents} />
                      </td>
                      <td className="num text-muted">
                        <Money cents={item.infonavit_cents + item.other_deductions_cents} />
                      </td>
                      <td className="num font-semibold">
                        <Money cents={item.net_cents} />
                      </td>
                      {canWrite && isDraft && (
                        <td className="actions">
                          <div className="flex items-center justify-end gap-1">
                            <Link
                              href={`/nomina/${period.id}?editar=${item.id}`}
                              className="btn btn-secondary btn-sm"
                            >
                              <Pencil size={13} />
                              Ajustar
                            </Link>
                            <ActionForm
                              action={deletePayrollItemAction}
                              submitLabel="Quitar"
                              variant="danger-ghost"
                              small
                              confirm={`¿Quitar a ${item.employee_name} de esta nómina?`}
                              submitClassName="mt-0 justify-end"
                            >
                              <input type="hidden" name="itemId" value={item.id} />
                              <input type="hidden" name="periodId" value={period.id} />
                            </ActionForm>
                          </div>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr>
                    <td colSpan={7} className="px-3 py-3 text-right text-xs font-semibold text-muted">
                      Totales
                    </td>
                    <td className="num px-3 py-3 font-bold text-ink">{formatMoney(net)}</td>
                    {canWrite && isDraft && <td />}
                  </tr>
                </tfoot>
              </table>
            </div>
          )}
        </section>

        <aside className="space-y-5 lg:sticky lg:top-[76px]">
          <section className="card p-5">
            <div className="flex items-center justify-between gap-3">
              <h2 className="section-title">Estado</h2>
              <Badge tone={period.status === "pagada" ? "success" : "warn"}>
                {period.status === "pagada" ? "Pagada" : "Borrador"}
              </Badge>
            </div>
            <dl className="mt-4 space-y-2.5 text-sm">
              <div className="flex justify-between gap-3">
                <dt className="text-muted">Periodo</dt>
                <dd className="text-right">{payrollPeriodLabel(period)}</dd>
              </div>
              <div className="flex justify-between gap-3">
                <dt className="text-muted">Fechas</dt>
                <dd className="text-right text-xs">
                  {formatDate(period.start_date)} – {formatDate(period.end_date)}
                </dd>
              </div>
              <div className="flex justify-between gap-3 border-t border-line pt-3">
                <dt className="text-muted">Total neto</dt>
                <dd className="font-bold">{formatMoney(net)}</dd>
              </div>
              {period.paid_at && (
                <div className="flex justify-between gap-3">
                  <dt className="text-muted">Pagada</dt>
                  <dd className="text-right text-xs">{formatDateTime(period.paid_at)}</dd>
                </div>
              )}
            </dl>
            {period.status === "pagada" && period.transaction_id && (
              <div className="mt-4 border-t border-line pt-3">
                <p className="text-xs leading-relaxed text-muted">
                  El pago quedó registrado como gasto en{" "}
                  <Link href="/movimientos?rango=todo" className="link">
                    Movimientos
                  </Link>
                  . Si necesitas corregirlo, revierte el pago: se elimina el gasto y el periodo vuelve
                  a borrador.
                </p>
                {canWrite && (
                  <ActionForm
                    action={revertPayrollPaymentAction}
                    submitLabel="Revertir pago"
                    pendingLabel="Revirtiendo…"
                    variant="danger-ghost"
                    small
                    confirm="¿Revertir el pago? Se eliminará el gasto de nómina y el periodo volverá a borrador."
                    className="mt-3"
                    submitClassName="mt-0 [&>button]:w-full"
                  >
                    <input type="hidden" name="id" value={period.id} />
                  </ActionForm>
                )}
              </div>
            )}
          </section>

          {canWrite && isDraft && items.length > 0 && (
            <section className="card p-5">
              <h2 className="section-title flex items-center gap-2">
                <CheckCircle2 size={16} className="text-accent" />
                Registrar pago
              </h2>
              <p className="mt-1 text-xs leading-relaxed text-muted">
                Se creará un gasto por <strong className="text-ink">{formatMoney(net)}</strong> en la
                cuenta seleccionada. El periodo quedará bloqueado.
              </p>
              <ActionForm
                action={payPayrollAction}
                submitLabel="Marcar como pagada"
                pendingLabel="Registrando…"
                className="mt-4 space-y-3"
                submitClassName="mt-4 [&>button]:w-full"
                confirm={`¿Registrar el pago de ${formatMoney(net)}? Esta acción bloquea el periodo.`}
              >
                <input type="hidden" name="id" value={period.id} />
                <Field label="Fecha de pago" htmlFor="payDate">
                  <input
                    className="input"
                    id="payDate"
                    name="date"
                    type="date"
                    defaultValue={todayISO()}
                    required
                  />
                </Field>
                <Field label="Cuenta de pago" htmlFor="accountId">
                  <select className="select" id="accountId" name="accountId" required>
                    {accounts.map((account) => (
                      <option key={account.id} value={account.id}>
                        {account.name}
                      </option>
                    ))}
                  </select>
                </Field>
              </ActionForm>
            </section>
          )}

          {canWrite && isDraft && (
            <ActionForm
              action={deletePayrollPeriodAction}
              submitLabel="Eliminar periodo"
              pendingLabel="Eliminando…"
              variant="danger-ghost"
              confirm="¿Eliminar este periodo y sus conceptos? Esta acción no se puede deshacer."
              className="card p-4"
              submitClassName="mt-0 [&>button]:w-full"
            >
              <input type="hidden" name="id" value={period.id} />
              <p className="mb-3 flex items-center gap-2 text-xs text-muted">
                <Trash2 size={14} />
                Solo disponible en borradores.
              </p>
            </ActionForm>
          )}
        </aside>
      </div>

      <p className="mt-4 text-[11px] leading-relaxed text-muted">
        Cálculo estimado: ISR con tarifa mensual configurada sin subsidio al empleo e IMSS con tasa
        simplificada. No sustituye el cálculo oficial de nómina ni el CFDI de nómina.
      </p>
    </div>
  );
}
