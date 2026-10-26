import Link from "next/link";
import { CalendarRange, CheckCircle2, Clock3, Plus, Wallet } from "lucide-react";
import { createPayrollPeriodAction } from "@/app/actions/payroll";
import { ActionForm } from "@/components/action-form";
import { Badge, EmptyState, Field, Money, PageHeader, Stat } from "@/components/ui";
import { requirePermission } from "@/lib/auth";
import { getPayrollPeriods } from "@/lib/data";
import { currentMonth, formatDate, todayISO } from "@/lib/dates";
import { formatMoney } from "@/lib/money";
import { payrollPeriodLabel } from "@/lib/payroll";
import { can } from "@/lib/permissions";

export const dynamic = "force-dynamic";

export const metadata = { title: "Nómina" };

export default async function PayrollPage({
  searchParams,
}: {
  searchParams: Promise<{ nuevo?: string }>;
}) {
  const user = await requirePermission("payroll.view");
  const params = await searchParams;
  const canWrite = can(user.role, "payroll.write");
  const showForm = canWrite && params.nuevo === "1";
  const periods = getPayrollPeriods();
  const current = currentMonth();
  const monthValue = todayISO().slice(0, 7);

  const drafts = periods.filter((period) => period.status === "borrador");
  const pendingNet = drafts.reduce((sum, period) => sum + Number(period.net_cents), 0);
  const paidThisYear = periods
    .filter((period) => period.status === "pagada" && period.year === current.year)
    .reduce((sum, period) => sum + Number(period.net_cents), 0);
  const paidPeriods = periods.filter((period) => period.status === "pagada").length;

  return (
    <div>
      <PageHeader
        title="Nómina"
        description="Calcula periodos, ajusta percepciones y deducciones, y registra el pago como gasto. Los cálculos son estimados."
        actions={
          canWrite && !showForm ? (
            <Link href="/nomina?nuevo=1" className="btn btn-primary">
              <Plus size={16} />
              Nuevo periodo
            </Link>
          ) : undefined
        }
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat
          label="Periodos"
          value={String(periods.length)}
          hint={`${paidPeriods} pagado(s)`}
          icon={<CalendarRange size={17} />}
        />
        <Stat
          label="Pendiente de pago"
          value={formatMoney(pendingNet)}
          hint={`${drafts.length} periodo(s) en borrador`}
          icon={<Clock3 size={17} />}
          tone={drafts.length > 0 ? "warn" : "default"}
        />
        <Stat
          label={`Pagado en ${current.year}`}
          value={formatMoney(paidThisYear)}
          hint="Neto entregado"
          icon={<CheckCircle2 size={17} />}
          tone="accent"
        />
        <Stat
          label="Periodo sugerido"
          value={periods.length === 0 ? "Crear" : "—"}
          hint="Mensual o quincenal"
          icon={<Wallet size={17} />}
        />
      </div>

      {showForm && (
        <section className="card mt-5 p-5">
          <div className="flex items-start justify-between gap-3">
            <div>
              <h2 className="section-title">Nuevo periodo de nómina</h2>
              <p className="mt-0.5 text-xs text-muted">
                Se incluyen los empleados activos con sueldo y se calculan ISR e IMSS estimados.
              </p>
            </div>
            <Link href="/nomina" className="btn btn-ghost btn-sm">
              Cerrar
            </Link>
          </div>

          <ActionForm
            action={createPayrollPeriodAction}
            submitLabel="Crear y calcular"
            pendingLabel="Calculando…"
            className="mt-4"
          >
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <Field label="Mes" htmlFor="period" required>
                <input
                  className="input"
                  id="period"
                  name="period"
                  type="month"
                  defaultValue={monthValue}
                  required
                />
              </Field>
              <Field label="Tipo de periodo" htmlFor="periodType" required>
                <select className="select" id="periodType" name="periodType" defaultValue="mensual">
                  <option value="mensual">Mensual</option>
                  <option value="quincenal">Quincenal</option>
                </select>
              </Field>
              <Field label="Quincena" htmlFor="quincena" hint="Solo aplica si es quincenal.">
                <select className="select" id="quincena" name="quincena" defaultValue="0">
                  <option value="0">No aplica</option>
                  <option value="1">Primera (días 1–15)</option>
                  <option value="2">Segunda (días 16–fin)</option>
                </select>
              </Field>
              <div className="flex items-end">
                <p className="text-xs leading-relaxed text-muted">
                  Puedes ajustar días, percepciones y deducciones después de crear el periodo.
                </p>
              </div>
            </div>
          </ActionForm>
        </section>
      )}

      <div className="card mt-5 overflow-hidden">
        {periods.length === 0 ? (
          <div className="p-5">
            <EmptyState
              icon={<Wallet size={18} />}
              title="Aún no hay periodos de nómina"
              description="Crea un periodo para calcular el sueldo de tu equipo, ISR e IMSS estimados."
              action={
                canWrite ? (
                  <Link href="/nomina?nuevo=1" className="btn btn-primary btn-sm">
                    Nuevo periodo
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
                  <th>Periodo</th>
                  <th>Fechas</th>
                  <th className="num">Empleados</th>
                  <th className="num">Bruto</th>
                  <th className="num">Deducciones</th>
                  <th className="num">Neto</th>
                  <th>Estatus</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {periods.map((period) => (
                  <tr key={period.id}>
                    <td className="whitespace-nowrap font-medium">
                      <Link href={`/nomina/${period.id}`} className="hover:text-accent">
                        {payrollPeriodLabel(period)}
                      </Link>
                    </td>
                    <td className="whitespace-nowrap text-muted">
                      {formatDate(period.start_date)} – {formatDate(period.end_date)}
                    </td>
                    <td className="num">{period.employee_count}</td>
                    <td className="num">
                      <Money cents={Number(period.gross_cents)} />
                    </td>
                    <td className="num text-muted">
                      <Money cents={Number(period.deductions_cents)} />
                    </td>
                    <td className="num font-semibold">
                      <Money cents={Number(period.net_cents)} />
                    </td>
                    <td>
                      <Badge tone={period.status === "pagada" ? "success" : "warn"}>
                        {period.status === "pagada" ? "Pagada" : "Borrador"}
                      </Badge>
                    </td>
                    <td className="actions">
                      <Link href={`/nomina/${period.id}`} className="btn btn-secondary btn-sm">
                        Ver
                      </Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <p className="mt-4 text-[11px] leading-relaxed text-muted">
        Estimación de apoyo. El ISR se calcula con la tarifa mensual configurada (sin subsidio al
        empleo) y el IMSS con una tasa simplificada. Confirma los importes con tu contador antes de
        pagar.
      </p>
    </div>
  );
}
