import Link from "next/link";
import {
  ArrowDownLeft,
  ArrowUpRight,
  BarChart3,
  Download,
  Landmark,
  Package,
  Scale,
  Wallet,
} from "lucide-react";
import { BarPairChart } from "@/components/bar-chart";
import {
  Badge,
  EmptyState,
  Money,
  PageHeader,
  Progress,
  SectionHeader,
  Stat,
} from "@/components/ui";
import { requirePermission } from "@/lib/auth";
import {
  getAccountBalances,
  getCashflow,
  getIncomeStatement,
  getInventorySummary,
  getMonthlyPerformance,
  getReceivableAging,
} from "@/lib/data";
import { currentMonth, formatDate, isValidISODate, monthRange, yearRange } from "@/lib/dates";
import { formatMoney } from "@/lib/money";

export const dynamic = "force-dynamic";

export const metadata = { title: "Reportes" };

export default async function ReportsPage({
  searchParams,
}: {
  searchParams: Promise<{ desde?: string; hasta?: string }>;
}) {
  await requirePermission("reports.view");
  const params = await searchParams;
  const today = new Date();
  const defaultRange = yearRange(today.getFullYear());
  let from = params.desde && isValidISODate(params.desde) ? params.desde : defaultRange.start;
  let to = params.hasta && isValidISODate(params.hasta) ? params.hasta : defaultRange.end;
  if (from > to) [from, to] = [to, from];

  const statement = getIncomeStatement(from, to);
  const cashflow = getCashflow(from, to);
  const aging = getReceivableAging();
  const performance = getMonthlyPerformance(from, to);
  const inventory = getInventorySummary();
  const accounts = getAccountBalances();
  const current = currentMonth();
  const month = monthRange(current.year, current.month);
  const exportQuery = `desde=${from}&hasta=${to}`;

  const maxExpense = statement.expensesByCategory[0]?.amount ?? 0;

  return (
    <div>
      <PageHeader
        title="Reportes"
        description="Estado de resultados, flujo de efectivo y antigüedad de saldos. Cifras sin IVA en resultados; con IVA en flujo."
        actions={
          <>
            <Link className="btn btn-secondary" href={`/api/exportar?tipo=movimientos&${exportQuery}`}>
              <Download size={15} />
              Movimientos
            </Link>
            <Link className="btn btn-secondary" href={`/api/exportar?tipo=facturas&${exportQuery}`}>
              <Download size={15} />
              Facturas
            </Link>
          </>
        }
      />

      <form
        className="card grid gap-3 p-4 sm:grid-cols-[1fr_1fr_auto]"
        action="/reportes"
      >
        <div>
          <label className="label" htmlFor="desde">
            Desde
          </label>
          <input className="input" id="desde" name="desde" type="date" defaultValue={from} />
        </div>
        <div>
          <label className="label" htmlFor="hasta">
            Hasta
          </label>
          <input className="input" id="hasta" name="hasta" type="date" defaultValue={to} />
        </div>
        <div className="flex items-end gap-2">
          <button type="submit" className="btn btn-secondary">
            Aplicar
          </button>
          <Link href={`/reportes?desde=${month.start}&hasta=${month.end}`} className="btn btn-ghost">
            Este mes
          </Link>
          <Link href={`/reportes?desde=${current.year}-01-01&hasta=${current.year}-12-31`} className="btn btn-ghost">
            Año
          </Link>
        </div>
      </form>

      <div className="mt-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat
          label="Ingresos"
          value={formatMoney(statement.totalIncome)}
          hint="Facturado + otros ingresos, sin IVA"
          icon={<ArrowDownLeft size={17} />}
          tone="accent"
        />
        <Stat
          label="Gastos"
          value={formatMoney(statement.totalExpenses)}
          hint="Sin IVA"
          icon={<ArrowUpRight size={17} />}
        />
        <Stat
          label="Utilidad operativa"
          value={formatMoney(statement.operatingProfit)}
          hint="Ingresos − gastos"
          icon={<Scale size={17} />}
          tone={statement.operatingProfit < 0 ? "danger" : "default"}
        />
        <Stat
          label="Flujo neto"
          value={formatMoney(cashflow.net)}
          hint="Cobros + ingresos − gastos"
          icon={<Wallet size={17} />}
          tone={cashflow.net < 0 ? "danger" : "default"}
        />
      </div>

      <div className="mt-5 grid items-start gap-5 xl:grid-cols-[minmax(0,1.35fr)_minmax(320px,1fr)]">
        <section className="card p-5">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h2 className="section-title">Ingresos vs. gastos</h2>
              <p className="mt-0.5 text-xs text-muted">
                Base contable sin IVA · {formatDate(from)} a {formatDate(to)}
              </p>
            </div>
            <Badge tone="neutral">{performance.length} mes(es)</Badge>
          </div>
          <div className="mt-5">
            <BarPairChart rows={performance} />
          </div>
        </section>

        <section className="card overflow-hidden">
          <SectionHeader title="Estado de resultados" description="Periodo seleccionado" />
          <div className="p-5">
            <dl className="space-y-2.5 text-sm">
              <div className="flex items-center justify-between gap-3">
                <dt className="text-muted">Ventas facturadas</dt>
                <dd>
                  <Money cents={statement.invoicedRevenue} />
                </dd>
              </div>
              <div className="flex items-center justify-between gap-3">
                <dt className="text-muted">Otros ingresos</dt>
                <dd>
                  <Money cents={statement.otherIncome} />
                </dd>
              </div>
              <div className="flex items-center justify-between gap-3 border-t border-line pt-2.5 font-semibold">
                <dt>Total ingresos</dt>
                <dd>
                  <Money cents={statement.totalIncome} />
                </dd>
              </div>
              <div className="flex items-center justify-between gap-3 border-t border-line pt-2.5">
                <dt className="text-muted">Total gastos</dt>
                <dd className="text-danger">
                  <Money cents={-statement.totalExpenses} />
                </dd>
              </div>
              <div className="flex items-center justify-between gap-3 rounded-lg bg-elevated px-3 py-2 text-base font-bold">
                <dt>Utilidad operativa</dt>
                <dd>
                  <Money cents={statement.operatingProfit} colored />
                </dd>
              </div>
            </dl>

            <div className="mt-5 border-t border-line pt-4">
              <p className="text-[11px] font-semibold uppercase tracking-wide text-muted">
                Referencia de costo de ventas
              </p>
              <dl className="mt-2 space-y-1.5 text-xs">
                <div className="flex justify-between gap-3">
                  <dt className="text-muted">Costo de productos facturados</dt>
                  <dd>
                    <Money cents={statement.cogsReference} />
                  </dd>
                </div>
                <div className="flex justify-between gap-3">
                  <dt className="text-muted">Margen bruto de referencia</dt>
                  <dd className="font-semibold">
                    <Money cents={statement.grossMarginReference} />
                  </dd>
                </div>
              </dl>
              <p className="mt-2 text-[10.5px] leading-relaxed text-muted">
                Referencia calculada con el costo promedio de los productos al momento de facturar.
                No descuenta compras de inventario no vendidas.
              </p>
            </div>
          </div>
        </section>
      </div>

      <div className="mt-5 grid items-start gap-5 xl:grid-cols-2">
        <section className="card overflow-hidden">
          <SectionHeader
            title="Gastos por categoría"
            description={`${statement.expensesByCategory.length} categoría(s)`}
          />
          {statement.expensesByCategory.length === 0 ? (
            <div className="p-5">
              <EmptyState
                icon={<BarChart3 size={18} />}
                title="Sin gastos en el periodo"
                description="Registra gastos para ver su distribución."
              />
            </div>
          ) : (
            <div className="space-y-4 p-5">
              {statement.expensesByCategory.map((category) => (
                <div key={category.name}>
                  <div className="mb-1.5 flex items-center justify-between gap-3 text-xs">
                    <span className="font-medium text-ink">{category.name}</span>
                    <span className="tabular-nums text-muted">{formatMoney(category.amount)}</span>
                  </div>
                  <Progress value={category.amount} max={maxExpense} />
                </div>
              ))}
            </div>
          )}
        </section>

        <section className="card overflow-hidden">
          <SectionHeader title="Flujo de efectivo" description="Cobros y pagos reales del periodo" />
          <div className="p-5">
            <dl className="space-y-2.5 text-sm">
              <div className="flex justify-between gap-3">
                <dt className="text-muted">Pagos de facturas</dt>
                <dd>
                  <Money cents={cashflow.invoicePayments} />
                </dd>
              </div>
              <div className="flex justify-between gap-3">
                <dt className="text-muted">Otros ingresos</dt>
                <dd>
                  <Money cents={cashflow.otherIncome} />
                </dd>
              </div>
              <div className="flex justify-between gap-3">
                <dt className="text-muted">Gastos pagados</dt>
                <dd className="text-danger">
                  <Money cents={-cashflow.expenses} />
                </dd>
              </div>
              <div className="flex justify-between gap-3 rounded-lg bg-elevated px-3 py-2 text-base font-bold">
                <dt>Flujo neto</dt>
                <dd>
                  <Money cents={cashflow.net} colored />
                </dd>
              </div>
            </dl>

            <div className="mt-5 border-t border-line pt-4">
              <p className="text-[11px] font-semibold uppercase tracking-wide text-muted">
                Saldos por cuenta
              </p>
              <div className="mt-2 space-y-2">
                {accounts.map((account) => (
                  <div key={account.id} className="flex items-center justify-between gap-3 text-xs">
                    <span className="flex items-center gap-2 text-muted">
                      <span className="icon-chip h-6 w-6 rounded-md">
                        <Wallet size={12} />
                      </span>
                      {account.name}
                    </span>
                    <span className="font-semibold tabular-nums">
                      {formatMoney(account.balance_cents)}
                    </span>
                  </div>
                ))}
              </div>
              <Link href="/configuracion#cuentas" className="mt-3 inline-flex text-[11px] font-semibold text-accent">
                Administrar cuentas
              </Link>
            </div>
          </div>
        </section>
      </div>

      <div className="mt-5 grid items-start gap-5 xl:grid-cols-[minmax(0,1.4fr)_minmax(300px,1fr)]">
        <section className="card overflow-hidden">
          <SectionHeader
            title="Antigüedad de saldos por cobrar"
            description={`${aging.invoices.length} factura(s) pendiente(s)`}
          />
          <div className="grid grid-cols-2 gap-3 border-b border-line p-4 sm:grid-cols-5">
            {aging.buckets.map((bucket) => (
              <div key={bucket.label} className="rounded-lg bg-elevated px-3 py-2.5">
                <p className="text-[10.5px] font-semibold text-muted">{bucket.label}</p>
                <p className="mt-1 text-sm font-bold tabular-nums">{formatMoney(bucket.amount)}</p>
                <p className="text-[10.5px] text-muted">{bucket.count} factura(s)</p>
              </div>
            ))}
          </div>
          {aging.invoices.length === 0 ? (
            <div className="p-5">
              <EmptyState
                icon={<Landmark size={18} />}
                title="Nada por cobrar"
                description="Todas las facturas emitidas están pagadas."
              />
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="table">
                <thead>
                  <tr>
                    <th>Folio</th>
                    <th>Cliente</th>
                    <th>Vence</th>
                    <th className="num">Saldo</th>
                    <th>Estado</th>
                  </tr>
                </thead>
                <tbody>
                  {aging.invoices.slice(0, 10).map((invoice) => (
                    <tr key={invoice.id}>
                      <td className="whitespace-nowrap font-medium">
                        <Link href={`/facturas/${invoice.id}`} className="hover:text-accent">
                          {invoice.series}-{String(invoice.number ?? invoice.id).padStart(4, "0")}
                        </Link>
                      </td>
                      <td className="max-w-[180px] truncate">{invoice.contact_name}</td>
                      <td className="whitespace-nowrap text-muted">{formatDate(invoice.due_date)}</td>
                      <td className="num font-semibold">
                        <Money cents={invoice.outstanding} />
                      </td>
                      <td>
                        {invoice.overdueDays > 0 ? (
                          <Badge tone={invoice.overdueDays > 60 ? "danger" : "warn"}>
                            {invoice.overdueDays} día(s)
                          </Badge>
                        ) : (
                          <Badge tone="neutral">Por vencer</Badge>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>

        <section className="card overflow-hidden">
          <SectionHeader title="Inventario" description="Valor y alertas al día de hoy" />
          <div className="p-5">
            <div className="flex items-center gap-3">
              <span className="icon-chip">
                <Package size={17} />
              </span>
              <div>
                <p className="text-[11px] font-semibold text-muted">Valor del inventario</p>
                <p className="text-xl font-bold tracking-tight">
                  {formatMoney(inventory.valueCents)}
                </p>
              </div>
            </div>
            <dl className="mt-4 space-y-2 border-t border-line pt-3 text-xs">
              <div className="flex justify-between gap-3">
                <dt className="text-muted">Productos activos</dt>
                <dd className="font-semibold">{inventory.productCount}</dd>
              </div>
              <div className="flex justify-between gap-3">
                <dt className="text-muted">Unidades en existencia</dt>
                <dd className="font-semibold">{inventory.unitCount.toLocaleString("es-MX")}</dd>
              </div>
              <div className="flex justify-between gap-3">
                <dt className="text-muted">Bajo stock</dt>
                <dd className={inventory.lowStockCount > 0 ? "font-semibold text-warn" : "font-semibold"}>
                  {inventory.lowStockCount}
                </dd>
              </div>
              <div className="flex justify-between gap-3">
                <dt className="text-muted">Agotados</dt>
                <dd className={inventory.outOfStockCount > 0 ? "font-semibold text-danger" : "font-semibold"}>
                  {inventory.outOfStockCount}
                </dd>
              </div>
            </dl>
            <Link href="/inventario?bajo=1" className="mt-4 inline-flex text-xs font-semibold text-accent">
              Revisar inventario bajo
            </Link>
          </div>
        </section>
      </div>

      <p className="mt-5 text-[11px] leading-relaxed text-muted">
        Reporte de apoyo contable. Los resultados usan subtotales sin IVA; el flujo de efectivo usa
        importes cobrados/pagados con IVA. Facturas canceladas y borradores se excluyen. No sustituye
        estados financieros dictaminados.
      </p>
    </div>
  );
}
