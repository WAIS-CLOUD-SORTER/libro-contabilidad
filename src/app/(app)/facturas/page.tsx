import Link from "next/link";
import { CircleDollarSign, Clock3, FilePlus2, ReceiptText } from "lucide-react";
import { requireUser } from "@/lib/auth";
import { getInvoiceOverview, getInvoices } from "@/lib/data";
import { formatDate, todayISO } from "@/lib/dates";
import { formatMoney } from "@/lib/money";
import { can } from "@/lib/permissions";
import {
  EmptyState,
  InvoiceBadge,
  Money,
  PageHeader,
  Pagination,
  Stat,
} from "@/components/ui";

export const dynamic = "force-dynamic";

export const metadata = { title: "Facturas" };

const STATUS_TABS = [
  { value: "", label: "Todas" },
  { value: "borrador", label: "Borradores" },
  { value: "emitida", label: "Emitidas" },
  { value: "pagada", label: "Pagadas" },
  { value: "cancelada", label: "Canceladas" },
];

export default async function InvoicesPage({
  searchParams,
}: {
  searchParams: Promise<{ status?: string; q?: string; page?: string }>;
}) {
  const user = await requireUser();
  const params = await searchParams;
  const q = (params.q ?? "").trim();
  const status = params.status ?? "";
  const page = Math.max(1, Number(params.page ?? 1) || 1);
  const perPage = 25;
  const { rows, total } = getInvoices({
    status: status || undefined,
    q: q || undefined,
    limit: perPage,
    offset: (page - 1) * perPage,
  });
  const overview = getInvoiceOverview();
  const pageCount = Math.max(1, Math.ceil(total / perPage));

  function makeHref(nextPage: number): string {
    const query = new URLSearchParams();
    if (q) query.set("q", q);
    if (status) query.set("status", status);
    if (nextPage > 1) query.set("page", String(nextPage));
    const suffix = query.toString();
    return `/facturas${suffix ? `?${suffix}` : ""}`;
  }

  return (
    <div>
      <PageHeader
        title="Facturas"
        description="Controla lo que facturas, lo que te deben y los pagos recibidos. Documento interno; no genera CFDI."
        actions={
          can(user.role, "invoice.write") ? (
            <Link href="/facturas/nueva" className="btn btn-primary">
              <FilePlus2 size={16} />
              Nueva factura
            </Link>
          ) : undefined
        }
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat
          label="Por cobrar"
          value={formatMoney(overview.receivable)}
          hint={`${overview.overdueCount} factura(s) vencida(s)`}
          icon={<CircleDollarSign size={17} />}
          tone={overview.overdueCount > 0 ? "warn" : "default"}
        />
        <Stat
          label="Vencido"
          value={formatMoney(overview.overdue)}
          hint="Requiere seguimiento"
          icon={<Clock3 size={17} />}
          tone={overview.overdue > 0 ? "danger" : "default"}
        />
        <Stat
          label="Emitido este mes"
          value={formatMoney(overview.monthTotal)}
          hint={`${overview.monthCount} factura(s)`}
          icon={<ReceiptText size={17} />}
        />
        <Stat
          label="Borradores"
          value={String(overview.draftCount)}
          hint="Aún no afectan reportes"
          icon={<FilePlus2 size={17} />}
        />
      </div>

      <div className="mt-6 flex flex-wrap items-center justify-between gap-3">
        <div className="segmented max-w-full overflow-x-auto">
          {STATUS_TABS.map((tab) => {
            const query = new URLSearchParams();
            if (q) query.set("q", q);
            if (tab.value) query.set("status", tab.value);
            const suffix = query.toString();
            return (
              <Link
                key={tab.value || "todas"}
                href={`/facturas${suffix ? `?${suffix}` : ""}`}
                aria-current={status === tab.value ? "page" : undefined}
              >
                {tab.label}
              </Link>
            );
          })}
        </div>

        <form className="flex w-full gap-2 sm:w-auto" action="/facturas">
          {status && <input type="hidden" name="status" value={status} />}
          <input
            className="input sm:w-64"
            name="q"
            defaultValue={q}
            placeholder="Buscar cliente, folio o nota…"
            aria-label="Buscar facturas"
          />
          <button type="submit" className="btn btn-secondary">
            Buscar
          </button>
        </form>
      </div>

      <div className="card mt-4 overflow-hidden">
        {rows.length === 0 ? (
          <div className="p-5">
            <EmptyState
              icon={<ReceiptText size={18} />}
              title={q || status ? "Sin resultados" : "Aún no hay facturas"}
              description={
                q || status
                  ? "Prueba con otro filtro o término de búsqueda."
                  : "Crea tu primera factura para controlar cobros y descontar inventario."
              }
              action={
                can(user.role, "invoice.write") ? (
                  <Link href="/facturas/nueva" className="btn btn-primary btn-sm">
                    Nueva factura
                  </Link>
                ) : undefined
              }
            />
          </div>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="table">
                <thead>
                  <tr>
                    <th>Folio</th>
                    <th>Fecha</th>
                    <th>Cliente</th>
                    <th>Vence</th>
                    <th className="num">Total</th>
                    <th className="num">Pagado</th>
                    <th className="num">Saldo</th>
                    <th>Estado</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((invoice) => {
                    const outstanding = invoice.total_cents - invoice.paid_cents;
                    return (
                      <tr key={invoice.id}>
                        <td className="whitespace-nowrap font-semibold">
                          <Link href={`/facturas/${invoice.id}`} className="hover:text-accent">
                            {invoice.number
                              ? `${invoice.series}-${String(invoice.number).padStart(4, "0")}`
                              : `Borrador #${invoice.id}`}
                          </Link>
                        </td>
                        <td className="whitespace-nowrap text-muted">{formatDate(invoice.date)}</td>
                        <td className="max-w-[220px] truncate">{invoice.contact_name}</td>
                        <td
                          className={
                            invoice.status === "emitida" && invoice.due_date < todayISO()
                              ? "whitespace-nowrap font-medium text-danger"
                              : "whitespace-nowrap text-muted"
                          }
                        >
                          {formatDate(invoice.due_date)}
                        </td>
                        <td className="num">
                          <Money cents={invoice.total_cents} />
                        </td>
                        <td className="num text-muted">
                          <Money cents={invoice.paid_cents} />
                        </td>
                        <td className="num font-semibold">
                          <Money cents={outstanding} />
                        </td>
                        <td>
                          <InvoiceBadge status={invoice.status} dueDate={invoice.due_date} />
                        </td>
                        <td className="actions">
                          <Link href={`/facturas/${invoice.id}`} className="btn btn-secondary btn-sm">
                            Ver
                          </Link>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <div className="border-t border-line px-4 py-3">
              <Pagination
                page={page}
                pageCount={pageCount}
                makeHref={makeHref}
                totalLabel={`${total} factura(s)`}
              />
            </div>
          </>
        )}
      </div>
    </div>
  );
}
