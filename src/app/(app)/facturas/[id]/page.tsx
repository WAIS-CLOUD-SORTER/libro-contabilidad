import Link from "next/link";
import { notFound } from "next/navigation";
import {
  ArrowLeft,
  Ban,
  CreditCard,
  FileText,
  Pencil,
  Printer,
  Send,
  Trash2,
} from "lucide-react";
import {
  cancelInvoiceAction,
  deleteInvoiceAction,
  deletePaymentAction,
  emitInvoiceAction,
  registerPaymentAction,
} from "@/app/actions/invoices";
import { ActionForm } from "@/components/action-form";
import {
  Badge,
  EmptyState,
  InvoiceBadge,
  Money,
  PageHeader,
  SectionHeader,
} from "@/components/ui";
import { requireUser } from "@/lib/auth";
import { getAccounts, getInvoice, getInvoiceItems, getPayments } from "@/lib/data";
import { formatDate, todayISO } from "@/lib/dates";
import { centsToInput, formatMoney, formatPercent, formatQty } from "@/lib/money";
import { can } from "@/lib/permissions";

export const dynamic = "force-dynamic";

export const metadata = { title: "Factura" };

export default async function InvoiceDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const user = await requireUser();
  const { id } = await params;
  const invoiceId = Number(id);
  if (!Number.isInteger(invoiceId) || invoiceId <= 0) notFound();

  const invoice = getInvoice(invoiceId);
  if (!invoice) notFound();

  const items = getInvoiceItems(invoiceId);
  const payments = getPayments(invoiceId);
  const accounts = getAccounts();
  const canWrite = can(user.role, "invoice.write");
  const outstanding = invoice.total_cents - invoice.paid_cents;
  const folio = invoice.number
    ? `${invoice.series}-${String(invoice.number).padStart(4, "0")}`
    : `Borrador #${invoice.id}`;
  const isDraft = invoice.status === "borrador";
  const isCancelled = invoice.status === "cancelada";
  const canPay = (invoice.status === "emitida" || invoice.status === "pagada") && outstanding > 0;

  return (
    <div>
      <PageHeader
        eyebrow="Factura"
        title={folio}
        description={`${invoice.contact_name ?? "Cliente"} · ${formatDate(invoice.date)}`}
        actions={
          <>
            <Link href="/facturas" className="btn btn-secondary">
              <ArrowLeft size={15} />
              Volver
            </Link>
            {!isDraft && (
              <Link href={`/imprimir/factura/${invoice.id}`} target="_blank" className="btn btn-secondary">
                <Printer size={15} />
                Imprimir
              </Link>
            )}
            {isDraft && canWrite && (
              <Link href={`/facturas/${invoice.id}/editar`} className="btn btn-secondary">
                <Pencil size={15} />
                Editar
              </Link>
            )}
          </>
        }
      />

      {isCancelled && (
        <div className="mb-5 rounded-xl border border-danger/30 bg-danger-soft p-4 text-sm text-danger">
          <p className="flex items-center gap-2 font-semibold">
            <Ban size={16} />
            Factura cancelada
          </p>
          <p className="mt-1 text-xs">
            {invoice.cancelled_reason || "Sin motivo registrado."}
            {invoice.cancelled_at ? ` · ${formatDate(invoice.cancelled_at)}` : ""}
          </p>
        </div>
      )}

      <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1.6fr)_minmax(300px,1fr)]">
        <div className="space-y-5">
          <section className="card p-5">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div>
                <p className="text-[11px] font-bold uppercase tracking-[0.08em] text-muted">Cliente</p>
                <p className="mt-1 text-[18px] font-semibold text-ink">{invoice.contact_name}</p>
                <p className="text-xs text-muted">{invoice.contact_rfc || "Sin RFC registrado"}</p>
              </div>
              <InvoiceBadge status={invoice.status} dueDate={invoice.due_date} />
            </div>

            <dl className="mt-5 grid gap-4 border-t border-line pt-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
              <div>
                <dt className="text-[11px] font-semibold text-muted">Emisión</dt>
                <dd className="mt-0.5">{formatDate(invoice.date)}</dd>
              </div>
              <div>
                <dt className="text-[11px] font-semibold text-muted">Vencimiento</dt>
                <dd className="mt-0.5">{formatDate(invoice.due_date)}</dd>
              </div>
              <div>
                <dt className="text-[11px] font-semibold text-muted">Condiciones</dt>
                <dd className="mt-0.5">{invoice.payment_terms || "—"}</dd>
              </div>
              <div>
                <dt className="text-[11px] font-semibold text-muted">Registró</dt>
                <dd className="mt-0.5">{invoice.created_by_name || "—"}</dd>
              </div>
            </dl>
          </section>

          <section className="card overflow-hidden">
            <SectionHeader title="Conceptos" description={`${items.length} partida(s)`} />
            <div className="overflow-x-auto">
              <table className="table">
                <thead>
                  <tr>
                    <th>Descripción</th>
                    <th className="num">Cant.</th>
                    <th className="num">Precio</th>
                    <th className="num">IVA</th>
                    <th className="num">Importe</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr key={item.id}>
                      <td>
                        <p className="font-medium text-ink">{item.description}</p>
                        <p className="mt-0.5 text-[11px] text-muted">
                          {item.sku ? `${item.sku} · ` : ""}
                          {formatQty(item.qty)} {item.unit ?? ""}
                        </p>
                      </td>
                      <td className="num text-muted">{formatQty(item.qty)}</td>
                      <td className="num">
                        <Money cents={item.unit_price_cents} />
                      </td>
                      <td className="num text-muted">{formatPercent(item.iva_rate)}</td>
                      <td className="num font-medium">
                        <Money cents={item.subtotal_cents} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <section className="card overflow-hidden">
            <SectionHeader
              title="Pagos recibidos"
              description={payments.length > 0 ? `${payments.length} pago(s)` : "Sin pagos registrados"}
            />
            {payments.length === 0 ? (
              <div className="p-5">
                <EmptyState
                  icon={<CreditCard size={18} />}
                  title="Aún no hay pagos"
                  description={
                    canPay
                      ? "Registra el primer pago desde el panel de la derecha."
                      : "Esta factura no tiene pagos registrados."
                  }
                />
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Fecha</th>
                      <th>Método</th>
                      <th>Cuenta</th>
                      <th>Referencia</th>
                      <th className="num">Monto</th>
                      {canWrite && <th />}
                    </tr>
                  </thead>
                  <tbody>
                    {payments.map((payment) => (
                      <tr key={payment.id}>
                        <td className="whitespace-nowrap">{formatDate(payment.date)}</td>
                        <td>{payment.method || "—"}</td>
                        <td className="text-muted">{payment.account_name || "—"}</td>
                        <td className="max-w-[180px] truncate text-muted">{payment.reference || "—"}</td>
                        <td className="num font-medium">
                          <Money cents={payment.amount_cents} />
                        </td>
                        {canWrite && (
                          <td className="actions">
                            <ActionForm
                              action={deletePaymentAction}
                              submitLabel="Eliminar"
                              variant="danger-ghost"
                              small
                              confirm="¿Eliminar este pago? El saldo de la factura se recalcula."
                              submitClassName="mt-0 justify-end"
                            >
                              <input type="hidden" name="paymentId" value={payment.id} />
                              <input type="hidden" name="invoiceId" value={invoice.id} />
                            </ActionForm>
                          </td>
                        )}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {invoice.notes && (
            <section className="card p-5">
              <h2 className="section-title">Notas</h2>
              <p className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-muted">{invoice.notes}</p>
            </section>
          )}
        </div>

        <aside className="space-y-5 lg:sticky lg:top-[76px]">
          <section className="card p-5">
            <h2 className="section-title">Totales</h2>
            <dl className="mt-4 space-y-2.5 text-sm">
              <div className="flex items-center justify-between gap-3">
                <dt className="text-muted">Subtotal</dt>
                <dd>
                  <Money cents={invoice.subtotal_cents} />
                </dd>
              </div>
              <div className="flex items-center justify-between gap-3">
                <dt className="text-muted">IVA</dt>
                <dd>
                  <Money cents={invoice.iva_cents} />
                </dd>
              </div>
              <div className="flex items-center justify-between gap-3 border-t border-line pt-3 text-base font-bold">
                <dt>Total</dt>
                <dd>
                  <Money cents={invoice.total_cents} />
                </dd>
              </div>
              <div className="flex items-center justify-between gap-3 text-success">
                <dt>Pagado</dt>
                <dd>
                  <Money cents={invoice.paid_cents} />
                </dd>
              </div>
              <div className="flex items-center justify-between gap-3 rounded-lg bg-elevated px-3 py-2 text-base font-bold">
                <dt>Saldo</dt>
                <dd>
                  <Money cents={outstanding} />
                </dd>
              </div>
            </dl>
            <p className="mt-4 border-t border-line pt-3 text-[11px] leading-relaxed text-muted">
              Documento interno de control. No es un CFDI ni comprobante fiscal.
            </p>
          </section>

          {isDraft && canWrite && (
            <section className="card border-warn/30 bg-warn-soft/40 p-5">
              <h2 className="section-title flex items-center gap-2">
                <FileText size={16} className="text-warn" />
                Borrador
              </h2>
              <p className="mt-1.5 text-xs leading-relaxed text-muted">
                Al emitir se asigna folio y se descuenta el inventario de los productos incluidos.
              </p>
              <div className="mt-4 space-y-2">
                <ActionForm
                  action={emitInvoiceAction}
                  submitLabel="Emitir factura"
                  pendingLabel="Emitiendo…"
                  confirm="¿Emitir esta factura? Se asignará folio y se descontará inventario."
                  submitClassName="mt-0 [&>button]:w-full"
                >
                  <input type="hidden" name="id" value={invoice.id} />
                </ActionForm>
                <ActionForm
                  action={deleteInvoiceAction}
                  submitLabel="Eliminar borrador"
                  pendingLabel="Eliminando…"
                  variant="danger-ghost"
                  confirm="¿Eliminar este borrador definitivamente?"
                  submitClassName="mt-0 [&>button]:w-full"
                >
                  <input type="hidden" name="id" value={invoice.id} />
                </ActionForm>
              </div>
            </section>
          )}

          {canPay && canWrite && accounts.length > 0 && (
            <section className="card p-5">
              <h2 className="section-title flex items-center gap-2">
                <CreditCard size={16} className="text-accent" />
                Registrar pago
              </h2>
              <p className="mt-1 text-xs text-muted">
                Saldo pendiente: <strong className="text-ink">{formatMoney(outstanding)}</strong>
              </p>
              <ActionForm
                action={registerPaymentAction}
                submitLabel="Registrar pago"
                pendingLabel="Registrando…"
                className="mt-4 space-y-3"
                submitClassName="mt-4 [&>button]:w-full"
              >
                <input type="hidden" name="id" value={invoice.id} />
                <div>
                  <label className="label" htmlFor="amount">
                    Monto
                  </label>
                  <input
                    className="input text-right tabular-nums"
                    id="amount"
                    name="amount"
                    inputMode="decimal"
                    defaultValue={centsToInput(outstanding)}
                    required
                  />
                </div>
                <div className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <label className="label" htmlFor="paymentDate">
                      Fecha
                    </label>
                    <input
                      className="input"
                      id="paymentDate"
                      name="date"
                      type="date"
                      defaultValue={todayISO()}
                      required
                    />
                  </div>
                  <div>
                    <label className="label" htmlFor="accountId">
                      Cuenta
                    </label>
                    <select className="select" id="accountId" name="accountId" required>
                      {accounts.map((account) => (
                        <option key={account.id} value={account.id}>
                          {account.name}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
                <div className="grid gap-3 sm:grid-cols-2">
                  <div>
                    <label className="label" htmlFor="method">
                      Método
                    </label>
                    <select className="select" id="method" name="method" defaultValue="Transferencia">
                      <option>Transferencia</option>
                      <option>Efectivo</option>
                      <option>Tarjeta</option>
                      <option>Cheque</option>
                      <option>Otro</option>
                    </select>
                  </div>
                  <div>
                    <label className="label" htmlFor="reference">
                      Referencia
                    </label>
                    <input
                      className="input"
                      id="reference"
                      name="reference"
                      placeholder="Folio o clave"
                      maxLength={120}
                    />
                  </div>
                </div>
                <div>
                  <label className="label" htmlFor="paymentNotes">
                    Notas
                  </label>
                  <input className="input" id="paymentNotes" name="notes" maxLength={300} />
                </div>
              </ActionForm>
            </section>
          )}

          {canWrite && !isDraft && !isCancelled && invoice.paid_cents === 0 && (
            <details className="card p-4">
              <summary className="cursor-pointer text-sm font-semibold text-danger">Cancelar factura</summary>
              <p className="mt-2 text-xs leading-relaxed text-muted">
                Se repondrá el inventario vendido. No se puede deshacer.
              </p>
              <ActionForm
                action={cancelInvoiceAction}
                submitLabel="Cancelar factura"
                pendingLabel="Cancelando…"
                variant="danger"
                confirm="¿Cancelar esta factura? Se repondrá el inventario."
                className="mt-3"
                submitClassName="mt-3 [&>button]:w-full"
              >
                <input type="hidden" name="id" value={invoice.id} />
                <label className="label" htmlFor="reason">
                  Motivo
                </label>
                <input
                  className="input"
                  id="reason"
                  name="reason"
                  placeholder="Ej. Error en el monto"
                  maxLength={300}
                  required
                />
              </ActionForm>
            </details>
          )}

          {invoice.paid_cents > 0 && !isCancelled && (
            <p className="rounded-xl border border-line bg-elevated p-3 text-[11px] leading-relaxed text-muted">
              Para cancelar esta factura, primero elimina los pagos registrados.
            </p>
          )}

          {isCancelled && (
            <div className="card p-4">
              <Badge tone="danger">Cancelada</Badge>
              <p className="mt-2 text-xs text-muted">
                {invoice.cancelled_reason || "Sin motivo registrado."}
              </p>
            </div>
          )}
        </aside>
      </div>
    </div>
  );
}
