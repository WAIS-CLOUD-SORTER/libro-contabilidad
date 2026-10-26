import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { PrintButton } from "@/components/print-button";
import { requireUser } from "@/lib/auth";
import { getCompany, getInvoice, getInvoiceItems, getPayments } from "@/lib/data";
import { formatDate, formatDateTime } from "@/lib/dates";
import { formatMoney, formatPercent, formatQty } from "@/lib/money";

export const dynamic = "force-dynamic";

export const metadata = { title: "Imprimir factura" };

export default async function InvoicePrintPage({ params }: { params: Promise<{ id: string }> }) {
  await requireUser();
  const { id } = await params;
  const invoiceId = Number(id);
  if (!Number.isInteger(invoiceId) || invoiceId <= 0) notFound();

  const invoice = getInvoice(invoiceId);
  if (!invoice) notFound();
  const items = getInvoiceItems(invoiceId);
  const payments = getPayments(invoiceId);
  const company = getCompany();
  const outstanding = invoice.total_cents - invoice.paid_cents;
  const folio = invoice.number
    ? `${invoice.series}-${String(invoice.number).padStart(4, "0")}`
    : `BORRADOR-${invoice.id}`;

  return (
    <div className="mx-auto w-full max-w-[880px] px-4 py-6 sm:px-6 print:max-w-none print:p-0">
      <div className="no-print mb-4 flex flex-wrap items-center justify-between gap-3">
        <Link href={`/facturas/${invoice.id}`} className="btn btn-secondary">
          <ArrowLeft size={15} />
          Volver a la factura
        </Link>
        <PrintButton />
      </div>

      <article className="card p-8 sm:p-10 print:rounded-none print:border-0 print:p-0 print:shadow-none">
        <header className="flex flex-wrap items-start justify-between gap-6 border-b-2 border-ink pb-6">
          <div className="max-w-[52%]">
            <p className="text-[22px] font-bold tracking-[-0.02em] text-ink">{company.name}</p>
            <div className="mt-2 space-y-0.5 text-[11.5px] leading-relaxed text-muted">
              {company.rfc && <p>RFC: {company.rfc}</p>}
              {company.regimen_fiscal && <p>{company.regimen_fiscal}</p>}
              {company.address && <p className="whitespace-pre-line">{company.address}</p>}
              {company.phone && <p>Tel. {company.phone}</p>}
              {company.email && <p>{company.email}</p>}
            </div>
          </div>
          <div className="text-right">
            <p className="text-[11px] font-bold uppercase tracking-[0.16em] text-muted">
              {invoice.status === "borrador" ? "Borrador de factura" : "Factura"}
            </p>
            <p className="mt-1 text-[24px] font-bold tracking-[-0.02em] text-ink">{folio}</p>
            <div className="mt-2 text-[11.5px] leading-relaxed text-muted">
              <p>Emisión: {formatDate(invoice.date)}</p>
              <p>Vencimiento: {formatDate(invoice.due_date)}</p>
              <p>Estado: {invoice.status}</p>
            </div>
          </div>
        </header>

        <section className="mt-6 grid gap-4 sm:grid-cols-2">
          <div>
            <p className="text-[10px] font-bold uppercase tracking-[0.12em] text-muted">Cliente</p>
            <p className="mt-1 text-[15px] font-semibold text-ink">{invoice.contact_name}</p>
            <div className="mt-1 text-[11.5px] leading-relaxed text-muted">
              {invoice.contact_rfc && <p>RFC: {invoice.contact_rfc}</p>}
            </div>
          </div>
          <div className="sm:text-right">
            <p className="text-[10px] font-bold uppercase tracking-[0.12em] text-muted">
              Condiciones de pago
            </p>
            <p className="mt-1 text-[13px] text-ink">{invoice.payment_terms || "—"}</p>
            {invoice.created_by_name && (
              <p className="mt-1 text-[11px] text-muted">Atendió: {invoice.created_by_name}</p>
            )}
          </div>
        </section>

        <section className="mt-6 overflow-x-auto">
          <table className="w-full border-collapse text-[12px]">
            <thead>
              <tr className="border-y border-line-strong bg-elevated text-[10px] uppercase tracking-[0.06em] text-muted">
                <th className="px-2 py-2 text-left font-bold">Descripción</th>
                <th className="px-2 py-2 text-right font-bold">Cant.</th>
                <th className="px-2 py-2 text-right font-bold">Precio</th>
                <th className="px-2 py-2 text-right font-bold">IVA</th>
                <th className="px-2 py-2 text-right font-bold">Importe</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.id} className="border-b border-line align-top">
                  <td className="px-2 py-2.5">
                    <p className="font-medium text-ink">{item.description}</p>
                    {item.sku && <p className="mt-0.5 text-[10px] text-muted">{item.sku}</p>}
                  </td>
                  <td className="px-2 py-2.5 text-right tabular-nums">{formatQty(item.qty)}</td>
                  <td className="px-2 py-2.5 text-right tabular-nums">
                    {formatMoney(item.unit_price_cents)}
                  </td>
                  <td className="px-2 py-2.5 text-right tabular-nums text-muted">
                    {formatPercent(item.iva_rate)}
                  </td>
                  <td className="px-2 py-2.5 text-right font-medium tabular-nums">
                    {formatMoney(item.subtotal_cents)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>

        <section className="mt-5 flex flex-wrap justify-end">
          <dl className="w-full max-w-[300px] space-y-1.5 text-[12.5px]">
            <div className="flex justify-between gap-6">
              <dt className="text-muted">Subtotal</dt>
              <dd className="tabular-nums">{formatMoney(invoice.subtotal_cents)}</dd>
            </div>
            <div className="flex justify-between gap-6">
              <dt className="text-muted">IVA</dt>
              <dd className="tabular-nums">{formatMoney(invoice.iva_cents)}</dd>
            </div>
            <div className="flex justify-between gap-6 border-t border-ink pt-2 text-[15px] font-bold">
              <dt>Total</dt>
              <dd className="tabular-nums">{formatMoney(invoice.total_cents)}</dd>
            </div>
            {invoice.paid_cents > 0 && (
              <>
                <div className="flex justify-between gap-6 text-muted">
                  <dt>Pagado</dt>
                  <dd className="tabular-nums">{formatMoney(invoice.paid_cents)}</dd>
                </div>
                <div className="flex justify-between gap-6 font-semibold">
                  <dt>Saldo</dt>
                  <dd className="tabular-nums">{formatMoney(outstanding)}</dd>
                </div>
              </>
            )}
          </dl>
        </section>

        {payments.length > 0 && (
          <section className="mt-6 border-t border-line pt-4">
            <p className="text-[10px] font-bold uppercase tracking-[0.12em] text-muted">
              Pagos recibidos
            </p>
            <ul className="mt-2 space-y-1 text-[11px] text-muted">
              {payments.map((payment) => (
                <li key={payment.id} className="flex flex-wrap justify-between gap-2">
                  <span>
                    {formatDate(payment.date)} · {payment.method || "Pago"}
                    {payment.reference ? ` · ${payment.reference}` : ""}
                  </span>
                  <span className="tabular-nums">{formatMoney(payment.amount_cents)}</span>
                </li>
              ))}
            </ul>
          </section>
        )}

        {invoice.notes && (
          <section className="mt-6 border-t border-line pt-4">
            <p className="text-[10px] font-bold uppercase tracking-[0.12em] text-muted">Notas</p>
            <p className="mt-1 whitespace-pre-wrap text-[11.5px] leading-relaxed text-muted">
              {invoice.notes}
            </p>
          </section>
        )}

        <footer className="mt-8 flex flex-wrap items-end justify-between gap-3 border-t border-line pt-4 text-[10px] leading-relaxed text-muted">
          <p className="max-w-[62ch]">
            Documento interno de control. No es un CFDI ni un comprobante fiscal digital. Generado el{" "}
            {formatDateTime(new Date().toISOString())}
          </p>
          <p className="font-semibold text-ink">{company.name}</p>
        </footer>
      </article>
    </div>
  );
}
