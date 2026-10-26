import Link from "next/link";
import { ArrowDownLeft, ArrowUpRight, Download, Landmark, Scale } from "lucide-react";
import { Badge, EmptyState, Money, PageHeader, SectionHeader, Stat } from "@/components/ui";
import { requirePermission } from "@/lib/auth";
import { getAnnualTax, getTaxSummary } from "@/lib/data";
import { formatDate, monthLabel, monthRange, parseMonthParam, todayISO } from "@/lib/dates";
import { formatMoney } from "@/lib/money";

export const dynamic = "force-dynamic";

export const metadata = { title: "Impuestos" };

function cap(value: string): string {
  return `${value.charAt(0).toUpperCase()}${value.slice(1)}`;
}

export default async function TaxesPage({
  searchParams,
}: {
  searchParams: Promise<{ mes?: string }>;
}) {
  await requirePermission("tax.view");
  const params = await searchParams;
  const { year, month } = parseMonthParam(params.mes);
  const { start, end } = monthRange(year, month);
  const summary = getTaxSummary(start, end);
  const annual = getAnnualTax(year);
  const currentMonth = todayISO().slice(0, 7);
  const selectedMonth = `${year}-${String(month).padStart(2, "0")}`;
  const periodLabel = `${cap(monthLabel(month))} ${year}`;

  const incomeRows = [
    ...summary.invoices.map((invoice) => ({
      key: `f-${invoice.id}`,
      kind: "Factura" as const,
      folio: invoice.number
        ? `${invoice.series}-${String(invoice.number).padStart(4, "0")}`
        : `Borrador ${invoice.id}`,
      date: invoice.date,
      name: invoice.contact_name ?? "—",
      detail: invoice.notes || "Venta facturada",
      subtotal: invoice.subtotal_cents,
      iva: invoice.iva_cents,
      total: invoice.total_cents,
    })),
    ...summary.incomeTransactions.map((row) => ({
      key: `t-${row.id}`,
      kind: "Ingreso" as const,
      folio: `MOV-${row.id}`,
      date: row.date,
      name: row.contact_name ?? "—",
      detail: row.description,
      subtotal: row.subtotal_cents,
      iva: row.iva_cents,
      total: row.total_cents,
    })),
  ].sort((a, b) => (a.date < b.date ? -1 : a.date > b.date ? 1 : 0));

  const annualTotals = annual.reduce(
    (totals, row) => ({
      trasladado: totals.trasladado + row.ivaTrasladado,
      acreditable: totals.acreditable + row.ivaAcreditable,
      neto: totals.neto + row.ivaNeto,
    }),
    { trasladado: 0, acreditable: 0, neto: 0 },
  );

  return (
    <div>
      <PageHeader
        title="Impuestos"
        description="IVA trasladado, acreditable y neto por mes. Cifras de trabajo para tu declaración, no un acuse fiscal."
        actions={
          <Link
            className="btn btn-secondary"
            href={`/api/exportar?tipo=iva&desde=${start}&hasta=${end}`}
          >
            <Download size={15} />
            CSV del mes
          </Link>
        }
      />

      <div className="card flex flex-wrap items-end gap-3 p-4">
        <form className="flex flex-wrap items-end gap-2" action="/impuestos">
          <div>
            <label className="label" htmlFor="mes">
              Periodo
            </label>
            <input
              className="input"
              id="mes"
              name="mes"
              type="month"
              defaultValue={selectedMonth}
            />
          </div>
          <button type="submit" className="btn btn-secondary">
            Consultar
          </button>
        </form>
        <Link
          href={`/impuestos?mes=${currentMonth}`}
          className="text-xs font-semibold text-accent hover:underline"
        >
          Mes actual
        </Link>
        <Link
          href={`/impuestos?mes=${year}-01`}
          className="text-xs font-semibold text-accent hover:underline"
        >
          Enero {year}
        </Link>
        <p className="ml-auto text-xs text-muted">{periodLabel}</p>
      </div>

      <div className="mt-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat
          label="IVA trasladado"
          value={formatMoney(summary.ivaTrasladado)}
          hint="Cobrado en ventas"
          icon={<ArrowUpRight size={17} />}
        />
        <Stat
          label="IVA acreditable"
          value={formatMoney(summary.ivaAcreditable)}
          hint="Pagado en gastos"
          icon={<ArrowDownLeft size={17} />}
        />
        <Stat
          label={summary.ivaNeto >= 0 ? "IVA a cargo" : "IVA a favor"}
          value={formatMoney(Math.abs(summary.ivaNeto))}
          hint="Trasladado − acreditable"
          icon={<Scale size={17} />}
          tone={summary.ivaNeto > 0 ? "warn" : "accent"}
        />
        <Stat
          label="Facturas del mes"
          value={String(summary.invoiceCount)}
          hint={`Base ${formatMoney(summary.invoiceSubtotal)}`}
          icon={<Landmark size={17} />}
        />
      </div>

      <div className="mt-5 grid items-start gap-5 xl:grid-cols-2">
        <section className="card overflow-hidden">
          <SectionHeader
            title="IVA trasladado"
            description={`${incomeRows.length} ingreso(s) · base ${formatMoney(
              summary.invoiceSubtotal + summary.otherIncomeSubtotal,
            )}`}
          />
          {incomeRows.length === 0 ? (
            <div className="p-5">
              <EmptyState
                icon={<ArrowUpRight size={18} />}
                title="Sin ingresos en el periodo"
                description="Emite facturas o registra ingresos no facturados para calcular el IVA trasladado."
              />
            </div>
          ) : (
            <div className="max-h-[480px] overflow-auto">
              <table className="table">
                <thead>
                  <tr>
                    <th>Fecha</th>
                    <th>Tipo</th>
                    <th>Concepto</th>
                    <th className="num">Base</th>
                    <th className="num">IVA</th>
                  </tr>
                </thead>
                <tbody>
                  {incomeRows.map((row) => (
                    <tr key={row.key}>
                      <td className="whitespace-nowrap text-muted">{formatDate(row.date)}</td>
                      <td>
                        <Badge tone={row.kind === "Factura" ? "info" : "neutral"}>{row.kind}</Badge>
                      </td>
                      <td className="max-w-[240px]">
                        <p className="truncate font-medium text-ink">{row.name}</p>
                        <p className="truncate text-[11px] text-muted">
                          {row.folio} · {row.detail}
                        </p>
                      </td>
                      <td className="num">
                        <Money cents={row.subtotal} />
                      </td>
                      <td className="num font-medium">
                        <Money cents={row.iva} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>

        <section className="card overflow-hidden">
          <SectionHeader
            title="IVA acreditable"
            description={`${summary.expenses.length} gasto(s) · base ${formatMoney(
              summary.expenseSubtotal,
            )}`}
          />
          {summary.expenses.length === 0 ? (
            <div className="p-5">
              <EmptyState
                icon={<ArrowDownLeft size={18} />}
                title="Sin gastos en el periodo"
                description="Registra gastos con IVA para acreditarlo contra el trasladado."
              />
            </div>
          ) : (
            <div className="max-h-[480px] overflow-auto">
              <table className="table">
                <thead>
                  <tr>
                    <th>Fecha</th>
                    <th>Concepto</th>
                    <th>Categoría</th>
                    <th className="num">Base</th>
                    <th className="num">IVA</th>
                  </tr>
                </thead>
                <tbody>
                  {summary.expenses.map((row) => (
                    <tr key={row.id}>
                      <td className="whitespace-nowrap text-muted">{formatDate(row.date)}</td>
                      <td className="max-w-[240px]">
                        <p className="truncate font-medium text-ink">{row.description}</p>
                        <p className="truncate text-[11px] text-muted">
                          {row.contact_name || "Sin proveedor"}
                          {row.reference ? ` · ${row.reference}` : ""}
                        </p>
                      </td>
                      <td className="max-w-[140px] truncate text-muted">
                        {row.category_name ?? "Sin categoría"}
                      </td>
                      <td className="num">
                        <Money cents={row.subtotal_cents} />
                      </td>
                      <td className="num font-medium">
                        <Money cents={row.iva_cents} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </div>

      <section className="card mt-5 overflow-hidden">
        <SectionHeader
          title={`Resumen anual ${year}`}
          description="IVA por mes. Haz clic en un mes para revisar su detalle."
        />
        <div className="overflow-x-auto">
          <table className="table">
            <thead>
              <tr>
                <th>Mes</th>
                <th className="num">Trasladado</th>
                <th className="num">Acreditable</th>
                <th className="num">Neto</th>
                <th>Resultado</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {annual.map((row) => {
                const value = `${year}-${String(row.month).padStart(2, "0")}`;
                return (
                  <tr
                    key={row.month}
                    className={value === selectedMonth ? "bg-accent-soft/40" : undefined}
                  >
                    <td className="whitespace-nowrap font-medium">{cap(row.label)}</td>
                    <td className="num">
                      <Money cents={row.ivaTrasladado} />
                    </td>
                    <td className="num text-muted">
                      <Money cents={row.ivaAcreditable} />
                    </td>
                    <td className="num font-semibold">
                      <Money cents={row.ivaNeto} colored />
                    </td>
                    <td>
                      {row.ivaTrasladado === 0 && row.ivaAcreditable === 0 ? (
                        <Badge tone="neutral">Sin movimientos</Badge>
                      ) : row.ivaNeto >= 0 ? (
                        <Badge tone="warn">A cargo</Badge>
                      ) : (
                        <Badge tone="accent">A favor</Badge>
                      )}
                    </td>
                    <td className="actions">
                      <Link href={`/impuestos?mes=${value}`} className="btn btn-ghost btn-sm">
                        Ver
                      </Link>
                    </td>
                  </tr>
                );
              })}
            </tbody>
            <tfoot>
              <tr>
                <td className="px-3 py-3 text-xs font-semibold uppercase tracking-wide text-muted">
                  Total {year}
                </td>
                <td className="num px-3 py-3 font-semibold">
                  {formatMoney(annualTotals.trasladado)}
                </td>
                <td className="num px-3 py-3 font-semibold text-muted">
                  {formatMoney(annualTotals.acreditable)}
                </td>
                <td className="num px-3 py-3 font-bold">{formatMoney(annualTotals.neto)}</td>
                <td colSpan={2} />
              </tr>
            </tfoot>
          </table>
        </div>
      </section>

      <div className="mt-5 rounded-xl border border-warn/30 bg-warn-soft/50 p-4 text-xs leading-relaxed text-muted">
        <p className="font-semibold text-ink">Lectura importante</p>
        <p className="mt-1">
          Este reporte agrupa facturas emitidas/pagadas e ingresos no facturados del periodo, y todos
          los gastos capturados con IVA. Los borradores y facturas canceladas no se incluyen. No
          considera IVA retenido, importaciones, notas de crédito fiscales ni el esquema real de
          declaraciones; úsalo como apoyo y valida con tu contador antes de presentar tu declaración.
        </p>
      </div>
    </div>
  );
}
