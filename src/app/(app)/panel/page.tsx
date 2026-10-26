import Link from "next/link";
import {
  AlertTriangle,
  ArrowDownLeft,
  ArrowUpRight,
  Banknote,
  CalendarClock,
  CircleDollarSign,
  FilePlus2,
  FileText,
  Landmark,
  Package,
  Plus,
  ReceiptText,
  Scale,
  TrendingUp,
  Wallet,
} from "lucide-react";
import { BarPairChart } from "@/components/bar-chart";
import { Badge, EmptyState, InvoiceBadge, Money, PageHeader, SectionHeader, Stat } from "@/components/ui";
import { requireUser } from "@/lib/auth";
import {
  getAccountBalances,
  getCompany,
  getDashboard,
  getInvoiceOverview,
  getPayrollPeriods,
} from "@/lib/data";
import { currentMonth, formatDate, monthLabel, todayISO } from "@/lib/dates";
import { formatMoney } from "@/lib/money";
import { can } from "@/lib/permissions";

export const dynamic = "force-dynamic";

export const metadata = { title: "Panel" };

export default async function PanelPage({
  searchParams,
}: {
  searchParams: Promise<{ bienvenido?: string; denegado?: string }>;
}) {
  const user = await requireUser();
  const params = await searchParams;
  const company = getCompany();
  const data = getDashboard();
  const overview = getInvoiceOverview();
  const accounts = getAccountBalances();
  const payroll = can(user.role, "payroll.view") ? getPayrollPeriods() : [];
  const pendingPayroll = payroll.filter((period) => period.status === "borrador");
  const pendingPayrollNet = pendingPayroll.reduce((sum, period) => sum + Number(period.net_cents), 0);
  const current = currentMonth();
  const monthName = monthLabel(current.month);
  const ivaNet = data.ivaTrasladadoCents - data.ivaAcreditableCents;
  const totalIncome = data.revenueCents + data.otherIncomeCents;

  const alerts = [
    overview.overdueCount > 0
      ? {
          tone: "danger" as const,
          icon: CalendarClock,
          title: `${overview.overdueCount} factura(s) vencida(s)`,
          detail: `${formatMoney(overview.overdue)} por cobrar fuera de plazo.`,
          href: "/facturas?status=emitida",
          cta: "Revisar cobros",
        }
      : null,
    data.lowStock.length > 0
      ? {
          tone: "warn" as const,
          icon: Package,
          title: `${data.lowStock.length} producto(s) con bajo stock`,
          detail: "Revisa existencias antes de vender o comprar.",
          href: "/inventario?bajo=1",
          cta: "Ver inventario",
        }
      : null,
    overview.draftCount > 0
      ? {
          tone: "info" as const,
          icon: FileText,
          title: `${overview.draftCount} borrador(es) de factura`,
          detail: "Emítelas o elimínalas para limpiar la operación.",
          href: "/facturas?status=borrador",
          cta: "Ver borradores",
        }
      : null,
    pendingPayroll.length > 0
      ? {
          tone: "warn" as const,
          icon: Wallet,
          title: `${pendingPayroll.length} nómina(s) sin pagar`,
          detail: `${formatMoney(pendingPayrollNet)} neto pendiente.`,
          href: "/nomina",
          cta: "Ver nómina",
        }
      : null,
  ].filter((alert) => alert !== null);

  return (
    <div>
      <PageHeader
        eyebrow={formatDate(todayISO())}
        title={`Hola, ${user.name.split(" ")[0]}`}
        description={`Resumen de ${company.name} para ${monthName} de ${current.year}.`}
        actions={
          <>
            {can(user.role, "invoice.write") && (
              <Link href="/facturas/nueva" className="btn btn-secondary">
                <FilePlus2 size={16} />
                Factura
              </Link>
            )}
            {can(user.role, "tx.write") && (
              <Link href="/movimientos/nuevo" className="btn btn-primary">
                <Plus size={16} />
                Movimiento
              </Link>
            )}
          </>
        }
      />

      {params.bienvenido === "1" && (
        <div className="mb-5 rounded-xl border border-accent/25 bg-accent-soft p-4 text-sm text-accent">
          <p className="font-semibold">Tu sistema está listo</p>
          <p className="mt-1 text-xs leading-relaxed">
            Configura tus cuentas, agrega contactos y productos, y empieza a registrar movimientos.
            Crea un respaldo desde Configuración cuando tengas datos importantes.
          </p>
        </div>
      )}
      {params.denegado === "1" && (
        <p className="form-error mb-5" role="alert">
          No tienes permiso para acceder a esa sección.
        </p>
      )}

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat
          label="Ingresos del mes"
          value={formatMoney(totalIncome)}
          hint={`${formatMoney(data.revenueCents)} facturado + otros`}
          icon={<ArrowDownLeft size={17} />}
          tone="accent"
        />
        <Stat
          label="Gastos del mes"
          value={formatMoney(data.expenseCents)}
          hint="Sin IVA"
          icon={<ArrowUpRight size={17} />}
        />
        <Stat
          label="Utilidad estimada"
          value={formatMoney(data.profitCents)}
          hint="Ingresos − gastos del mes"
          icon={<TrendingUp size={17} />}
          tone={data.profitCents < 0 ? "danger" : "default"}
        />
        <Stat
          label="Por cobrar"
          value={formatMoney(data.receivableCents)}
          hint={
            overview.overdueCount > 0
              ? `${overview.overdueCount} factura(s) vencida(s)`
              : "Sin facturas vencidas"
          }
          icon={<CircleDollarSign size={17} />}
          tone={overview.overdueCount > 0 ? "warn" : "default"}
        />
      </div>

      <div className="mt-5 grid items-start gap-5 xl:grid-cols-[minmax(0,1.5fr)_minmax(300px,1fr)]">
        <section className="card p-5">
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div>
              <h2 className="section-title">Últimos 6 meses</h2>
              <p className="mt-0.5 text-xs text-muted">Ingresos y gastos sin IVA</p>
            </div>
            <Link href="/reportes" className="text-xs font-semibold text-accent hover:underline">
              Ver reportes
            </Link>
          </div>
          <div className="mt-5">
            <BarPairChart rows={data.monthly} />
          </div>
        </section>

        <section className="card overflow-hidden">
          <SectionHeader
            title="Caja y patrimonio"
            description="Saldos calculados al día de hoy"
            action={
              <Link href="/configuracion#cuentas" className="text-xs font-semibold text-accent">
                Cuentas
              </Link>
            }
          />
          <div className="divide-y divide-line">
            {accounts.map((account) => (
              <div key={account.id} className="flex items-center justify-between gap-3 px-5 py-3">
                <span className="flex items-center gap-2 text-sm text-muted">
                  <span className="icon-chip h-7 w-7 rounded-md">
                    <Banknote size={13} />
                  </span>
                  {account.name}
                </span>
                <span className="text-sm font-semibold tabular-nums">
                  {formatMoney(account.balance_cents)}
                </span>
              </div>
            ))}
            <div className="flex items-center justify-between gap-3 bg-elevated px-5 py-3">
              <span className="text-xs font-semibold uppercase tracking-wide text-muted">
                Total efectivo
              </span>
              <span className="text-base font-bold tabular-nums">{formatMoney(data.cashCents)}</span>
            </div>
            <div className="grid grid-cols-2 gap-3 p-5 text-xs">
              <div>
                <p className="text-muted">Inventario</p>
                <p className="mt-0.5 text-sm font-semibold">{formatMoney(data.inventoryCents)}</p>
              </div>
              <div>
                <p className="text-muted">IVA neto del mes</p>
                <p className={`mt-0.5 text-sm font-semibold ${ivaNet > 0 ? "text-warn" : "text-success"}`}>
                  {formatMoney(ivaNet)}
                </p>
              </div>
            </div>
          </div>
        </section>
      </div>

      {alerts.length > 0 && (
        <div className="mt-5 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {alerts.map((alert) => {
            const Icon = alert.icon;
            return (
              <Link
                key={alert.title}
                href={alert.href}
                className={`card flex flex-col gap-2 p-4 transition-colors hover:border-line-strong ${
                  alert.tone === "danger"
                    ? "border-danger/30 bg-danger-soft/30"
                    : alert.tone === "warn"
                      ? "border-warn/30 bg-warn-soft/30"
                      : alert.tone === "info"
                        ? "border-info/25 bg-info-soft/30"
                        : ""
                }`}
              >
                <span className="flex items-center gap-2 text-[12.5px] font-semibold text-ink">
                  <Icon
                    size={16}
                    className={
                      alert.tone === "danger"
                        ? "text-danger"
                        : alert.tone === "warn"
                          ? "text-warn"
                          : "text-info"
                    }
                  />
                  {alert.title}
                </span>
                <span className="text-[11px] leading-relaxed text-muted">{alert.detail}</span>
                <span className="mt-auto pt-1 text-[11px] font-semibold text-accent">{alert.cta} →</span>
              </Link>
            );
          })}
        </div>
      )}

      <div className="mt-5 grid items-start gap-5 xl:grid-cols-[minmax(0,1.3fr)_minmax(0,1fr)]">
        <section className="card overflow-hidden">
          <SectionHeader
            title="Facturas recientes"
            description="Últimos documentos registrados"
            action={
              <Link href="/facturas" className="text-xs font-semibold text-accent">
                Ver todas
              </Link>
            }
          />
          {data.recentInvoices.length === 0 ? (
            <div className="p-5">
              <EmptyState
                icon={<ReceiptText size={18} />}
                title="Aún no hay facturas"
                description="Crea tu primera factura para controlar cobros y descontar inventario."
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
            <div className="overflow-x-auto">
              <table className="table">
                <thead>
                  <tr>
                    <th>Folio</th>
                    <th>Cliente</th>
                    <th>Fecha</th>
                    <th className="num">Total</th>
                    <th>Estado</th>
                  </tr>
                </thead>
                <tbody>
                  {data.recentInvoices.map((invoice) => (
                    <tr key={invoice.id}>
                      <td className="whitespace-nowrap font-medium">
                        <Link href={`/facturas/${invoice.id}`} className="hover:text-accent">
                          {invoice.number
                            ? `${invoice.series}-${String(invoice.number).padStart(4, "0")}`
                            : `Borrador #${invoice.id}`}
                        </Link>
                      </td>
                      <td className="max-w-[180px] truncate text-muted">{invoice.contact_name}</td>
                      <td className="whitespace-nowrap text-xs text-muted">{formatDate(invoice.date)}</td>
                      <td className="num">
                        <Money cents={invoice.total_cents} />
                      </td>
                      <td>
                        <InvoiceBadge status={invoice.status} dueDate={invoice.due_date} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <div className="border-t border-line px-5 py-3 text-xs text-muted">
            {overview.monthCount} factura(s) emitida(s) este mes por {formatMoney(overview.monthTotal)}
          </div>
        </section>

        <section className="card overflow-hidden">
          <SectionHeader
            title="Movimientos recientes"
            description="Ingresos no facturados y gastos"
            action={
              <Link href="/movimientos" className="text-xs font-semibold text-accent">
                Ver todos
              </Link>
            }
          />
          {data.recentTransactions.length === 0 ? (
            <div className="p-5">
              <EmptyState
                icon={<Scale size={18} />}
                title="Sin movimientos"
                description="Registra gastos o ingresos que no pasan por factura."
              />
            </div>
          ) : (
            <ul className="divide-y divide-line">
              {data.recentTransactions.map((row) => (
                <li key={row.id} className="flex items-start justify-between gap-3 px-5 py-3.5">
                  <div className="min-w-0">
                    <p className="truncate text-[13px] font-medium text-ink">{row.description}</p>
                    <p className="mt-0.5 text-[11px] text-muted">
                      {formatDate(row.date)} · {row.category_name ?? "Sin categoría"}
                    </p>
                  </div>
                  <span
                    className={`whitespace-nowrap text-[13px] font-semibold tabular-nums ${
                      row.type === "ingreso" ? "text-success" : "text-expense"
                    }`}
                  >
                    {row.type === "ingreso" ? "+" : "−"}
                    {formatMoney(row.total_cents)}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      {data.lowStock.length > 0 && (
        <section className="card mt-5 overflow-hidden">
          <SectionHeader
            title="Inventario bajo"
            description="Productos en o bajo el mínimo"
            action={
              <Link href="/inventario?bajo=1" className="text-xs font-semibold text-accent">
                Ver inventario
              </Link>
            }
          />
          <div className="grid gap-3 p-5 sm:grid-cols-2 lg:grid-cols-3">
            {data.lowStock.map((product) => (
              <Link
                key={product.id}
                href={`/inventario/${product.id}`}
                className="flex items-center justify-between gap-3 rounded-xl border border-line bg-elevated px-4 py-3 transition-colors hover:border-line-strong"
              >
                <div className="min-w-0">
                  <p className="truncate text-[13px] font-medium text-ink">{product.name}</p>
                  <p className="text-[11px] text-muted">
                    Stock {product.stock} {product.unit} · mín. {product.min_stock}
                  </p>
                </div>
                <Badge tone={product.stock <= 0 ? "danger" : "warn"}>
                  {product.stock <= 0 ? "Agotado" : "Bajo"}
                </Badge>
              </Link>
            ))}
          </div>
        </section>
      )}

      <div className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {can(user.role, "invoice.write") && (
          <Link href="/facturas/nueva" className="card flex items-center gap-3 p-4 hover:border-line-strong">
            <span className="icon-chip">
              <FilePlus2 size={17} />
            </span>
            <span>
              <span className="block text-[13px] font-semibold text-ink">Nueva factura</span>
              <span className="block text-[11px] text-muted">Venta con o sin inventario</span>
            </span>
          </Link>
        )}
        {can(user.role, "tx.write") && (
          <Link href="/movimientos/nuevo" className="card flex items-center gap-3 p-4 hover:border-line-strong">
            <span className="icon-chip">
              <ArrowUpRight size={17} />
            </span>
            <span>
              <span className="block text-[13px] font-semibold text-ink">Registrar gasto</span>
              <span className="block text-[11px] text-muted">Con IVA acreditable</span>
            </span>
          </Link>
        )}
        {can(user.role, "products.write") && (
          <Link href="/inventario?nuevo=1" className="card flex items-center gap-3 p-4 hover:border-line-strong">
            <span className="icon-chip">
              <Package size={17} />
            </span>
            <span>
              <span className="block text-[13px] font-semibold text-ink">Nuevo producto</span>
              <span className="block text-[11px] text-muted">Stock, costo y precio</span>
            </span>
          </Link>
        )}
        {can(user.role, "payroll.view") && (
          <Link href="/nomina" className="card flex items-center gap-3 p-4 hover:border-line-strong">
            <span className="icon-chip">
              <Wallet size={17} />
            </span>
            <span>
              <span className="block text-[13px] font-semibold text-ink">Nómina</span>
              <span className="block text-[11px] text-muted">
                {pendingPayroll.length > 0
                  ? `${pendingPayroll.length} periodo(s) pendiente(s)`
                  : "Periodos y pagos"}
              </span>
            </span>
          </Link>
        )}
        {can(user.role, "tax.view") && (
          <Link href="/impuestos" className="card flex items-center gap-3 p-4 hover:border-line-strong">
            <span className="icon-chip">
              <Landmark size={17} />
            </span>
            <span>
              <span className="block text-[13px] font-semibold text-ink">IVA del mes</span>
              <span className="block text-[11px] text-muted">
                {ivaNet >= 0 ? "A cargo" : "A favor"}: {formatMoney(Math.abs(ivaNet))}
              </span>
            </span>
          </Link>
        )}
      </div>

      <p className="mt-6 flex items-center gap-2 text-[11px] text-muted">
        <AlertTriangle size={13} />
        Cifras estimadas de control interno. No son declaraciones fiscales.
      </p>
    </div>
  );
}
