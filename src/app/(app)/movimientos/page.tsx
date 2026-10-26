import Link from "next/link";
import {
  ArrowDownLeft,
  ArrowLeftRight,
  ArrowUpRight,
  Download,
  Plus,
  Scale,
} from "lucide-react";
import { deleteTransactionAction } from "@/app/actions/transactions";
import { ActionForm } from "@/components/action-form";
import {
  Badge,
  EmptyState,
  Money,
  PageHeader,
  Pagination,
  Stat,
} from "@/components/ui";
import { requireUser } from "@/lib/auth";
import { getCategories, getTransactions, getTransactionTotals } from "@/lib/data";
import { currentMonthRange, formatDate } from "@/lib/dates";
import { formatMoney } from "@/lib/money";
import { can } from "@/lib/permissions";

export const dynamic = "force-dynamic";

export const metadata = { title: "Movimientos" };

export default async function TransactionsPage({
  searchParams,
}: {
  searchParams: Promise<{
    desde?: string;
    hasta?: string;
    tipo?: string;
    categoria?: string;
    q?: string;
    page?: string;
    rango?: string;
  }>;
}) {
  const user = await requireUser();
  const params = await searchParams;
  const month = currentMonthRange();
  const allTime = params.rango === "todo";
  const from = allTime ? undefined : params.desde || month.start;
  const to = allTime ? undefined : params.hasta || month.end;
  const type = params.tipo === "ingreso" || params.tipo === "gasto" ? params.tipo : undefined;
  const q = (params.q ?? "").trim();
  const categoryId = Number(params.categoria) || undefined;
  const page = Math.max(1, Number(params.page ?? 1) || 1);
  const perPage = 40;

  const filters = {
    from,
    to,
    type,
    categoryId,
    q: q || undefined,
  };
  const { rows, total } = getTransactions({
    ...filters,
    limit: perPage,
    offset: (page - 1) * perPage,
  });
  const totals = getTransactionTotals(filters);
  const categories = getCategories();
  const net = totals.incomeSubtotal - totals.expenseSubtotal;
  const ivaNet = totals.incomeIva - totals.expenseIva;
  const pageCount = Math.max(1, Math.ceil(total / perPage));

  function makeHref(nextPage: number): string {
    const query = new URLSearchParams();
    if (allTime) query.set("rango", "todo");
    else {
      if (from) query.set("desde", from);
      if (to) query.set("hasta", to);
    }
    if (type) query.set("tipo", type);
    if (categoryId) query.set("categoria", String(categoryId));
    if (q) query.set("q", q);
    if (nextPage > 1) query.set("page", String(nextPage));
    const suffix = query.toString();
    return `/movimientos${suffix ? `?${suffix}` : ""}`;
  }

  const exportQuery = new URLSearchParams({ tipo: "movimientos" });
  if (from) exportQuery.set("desde", from);
  if (to) exportQuery.set("hasta", to);
  if (type) exportQuery.set("tipo_mov", type);

  return (
    <div>
      <PageHeader
        title="Movimientos"
        description="Ingresos no facturados y gastos. Las ventas con factura viven en Facturas para no duplicar."
        actions={
          <>
            <Link className="btn btn-secondary" href={`/api/exportar?${exportQuery.toString()}`}>
              <Download size={15} />
              CSV
            </Link>
            {can(user.role, "tx.write") && (
              <Link href="/movimientos/nuevo" className="btn btn-primary">
                <Plus size={16} />
                Nuevo movimiento
              </Link>
            )}
          </>
        }
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat
          label="Ingresos"
          value={formatMoney(totals.incomeSubtotal)}
          hint="Sin IVA"
          icon={<ArrowDownLeft size={17} />}
          tone="accent"
        />
        <Stat
          label="Gastos"
          value={formatMoney(totals.expenseSubtotal)}
          hint="Sin IVA"
          icon={<ArrowUpRight size={17} />}
        />
        <Stat
          label="Neto"
          value={formatMoney(net)}
          hint="Ingresos − gastos"
          icon={<Scale size={17} />}
          tone={net < 0 ? "danger" : "default"}
        />
        <Stat
          label="IVA neto"
          value={formatMoney(ivaNet)}
          hint="Trasladado − acreditable"
          icon={<ArrowLeftRight size={17} />}
          tone={ivaNet > 0 ? "warn" : "default"}
        />
      </div>

      <form
        action="/movimientos"
        className="card mt-5 grid gap-3 p-4 sm:grid-cols-2 lg:grid-cols-[1fr_1fr_1fr_1.4fr_auto]"
      >
        <div>
          <label className="label" htmlFor="desde">
            Desde
          </label>
          <input
            className="input"
            id="desde"
            name="desde"
            type="date"
            defaultValue={from ?? ""}
            disabled={allTime}
          />
        </div>
        <div>
          <label className="label" htmlFor="hasta">
            Hasta
          </label>
          <input
            className="input"
            id="hasta"
            name="hasta"
            type="date"
            defaultValue={to ?? ""}
            disabled={allTime}
          />
        </div>
        <div>
          <label className="label" htmlFor="tipo">
            Tipo
          </label>
          <select className="select" id="tipo" name="tipo" defaultValue={type ?? ""}>
            <option value="">Todos</option>
            <option value="ingreso">Ingresos</option>
            <option value="gasto">Gastos</option>
          </select>
        </div>
        <div>
          <label className="label" htmlFor="q">
            Buscar
          </label>
          <input
            className="input"
            id="q"
            name="q"
            defaultValue={q}
            placeholder="Descripción, referencia o contacto"
          />
        </div>
        <div className="flex items-end">
          <button type="submit" className="btn btn-secondary w-full">
            Filtrar
          </button>
        </div>
        <div className="sm:col-span-2 lg:col-span-5">
          <div className="flex flex-wrap items-center gap-2">
            <label className="text-xs font-semibold text-muted" htmlFor="categoria">
              Categoría
            </label>
            <select
              className="select w-auto max-w-[260px]"
              id="categoria"
              name="categoria"
              defaultValue={categoryId ? String(categoryId) : ""}
            >
              <option value="">Todas</option>
              {categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
            <Link
              href="/movimientos"
              className="text-xs font-semibold text-accent hover:underline"
            >
              Este mes
            </Link>
            <Link
              href="/movimientos?rango=todo"
              className="text-xs font-semibold text-accent hover:underline"
            >
              Ver todo
            </Link>
          </div>
        </div>
      </form>

      <div className="card mt-4 overflow-hidden">
        {rows.length === 0 ? (
          <div className="p-5">
            <EmptyState
              icon={<ArrowLeftRight size={18} />}
              title="Sin movimientos en este periodo"
              description="Registra un gasto o un ingreso no facturado para empezar."
              action={
                can(user.role, "tx.write") ? (
                  <Link href="/movimientos/nuevo" className="btn btn-primary btn-sm">
                    Nuevo movimiento
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
                    <th>Fecha</th>
                    <th>Descripción</th>
                    <th>Contacto</th>
                    <th>Cuenta</th>
                    <th>Tipo</th>
                    <th className="num">Subtotal</th>
                    <th className="num">IVA</th>
                    <th className="num">Total</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr key={row.id}>
                      <td className="whitespace-nowrap text-muted">{formatDate(row.date)}</td>
                      <td className="max-w-[280px]">
                        <p className="truncate font-medium text-ink">{row.description}</p>
                        <p className="mt-0.5 flex flex-wrap items-center gap-1.5 text-[11px] text-muted">
                          {row.category_name ?? "Sin categoría"}
                          {row.source !== "manual" && (
                            <Badge tone="neutral">
                              {row.source === "nomina" ? "Nómina" : "Inventario"}
                            </Badge>
                          )}
                        </p>
                      </td>
                      <td className="max-w-[160px] truncate text-muted">{row.contact_name || "—"}</td>
                      <td className="whitespace-nowrap text-muted">{row.account_name || "—"}</td>
                      <td>
                        <Badge tone={row.type === "ingreso" ? "success" : "neutral"}>
                          {row.type === "ingreso" ? "Ingreso" : "Gasto"}
                        </Badge>
                      </td>
                      <td className="num">
                        <Money cents={row.subtotal_cents} />
                      </td>
                      <td className="num text-muted">
                        <Money cents={row.iva_cents} />
                      </td>
                      <td className="num font-semibold">
                        <Money cents={row.total_cents} />
                      </td>
                      <td className="actions">
                        {row.source === "manual" && can(user.role, "tx.write") ? (
                          <div className="flex items-center justify-end gap-1">
                            <Link
                              href={`/movimientos/${row.id}/editar`}
                              className="btn btn-secondary btn-sm"
                            >
                              Editar
                            </Link>
                            <ActionForm
                              action={deleteTransactionAction}
                              submitLabel="Eliminar"
                              variant="danger-ghost"
                              small
                              confirm="¿Eliminar este movimiento?"
                              submitClassName="mt-0 justify-end"
                            >
                              <input type="hidden" name="id" value={row.id} />
                            </ActionForm>
                          </div>
                        ) : (
                          <span className="text-[11px] text-muted">Automático</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="border-t border-line px-4 py-3">
              <Pagination
                page={page}
                pageCount={pageCount}
                makeHref={makeHref}
                totalLabel={`${total} movimiento(s) · ${formatMoney(totals.incomeSubtotal - totals.expenseSubtotal)} neto`}
              />
            </div>
          </>
        )}
      </div>
    </div>
  );
}
