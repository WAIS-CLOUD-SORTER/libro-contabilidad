import Link from "next/link";
import { Briefcase, Download, Plus, UserRoundCheck, Users, Wallet } from "lucide-react";
import { toggleEmployeeAction } from "@/app/actions/employees";
import { ActionForm } from "@/components/action-form";
import { EmployeeForm } from "@/components/employee-form";
import { Badge, EmptyState, Money, PageHeader, Stat } from "@/components/ui";
import { requirePermission } from "@/lib/auth";
import { getEmployee, getEmployees } from "@/lib/data";
import { formatDate } from "@/lib/dates";
import { formatMoney } from "@/lib/money";
import { can } from "@/lib/permissions";

export const dynamic = "force-dynamic";

export const metadata = { title: "Empleados" };

export default async function EmployeesPage({
  searchParams,
}: {
  searchParams: Promise<{ q?: string; inactivos?: string; nuevo?: string; editar?: string }>;
}) {
  const user = await requirePermission("payroll.view");
  const params = await searchParams;
  const canWrite = can(user.role, "payroll.write");
  const editing = Number(params.editar) > 0 ? getEmployee(Number(params.editar)) : undefined;
  const showForm = canWrite && (params.nuevo === "1" || Boolean(editing));
  const includeInactive = params.inactivos === "1";
  const q = (params.q ?? "").trim().toLowerCase();

  const employees = getEmployees(true);
  const active = employees.filter((employee) => employee.status === "activo");
  const monthlyPayroll = active.reduce((sum, employee) => sum + employee.salary_cents, 0);
  const average = active.length > 0 ? Math.round(monthlyPayroll / active.length) : 0;
  const visible = employees
    .filter((employee) => includeInactive || employee.status === "activo")
    .filter((employee) => {
      if (!q) return true;
      return (
        employee.name.toLowerCase().includes(q) ||
        employee.position.toLowerCase().includes(q) ||
        employee.department.toLowerCase().includes(q) ||
        employee.rfc.toLowerCase().includes(q)
      );
    });

  return (
    <div>
      <PageHeader
        title="Empleados"
        description="Plantilla, sueldos y datos fiscales. La nómina se calcula y paga desde el módulo de Nómina."
        actions={
          <>
            <Link className="btn btn-secondary" href="/api/exportar?tipo=empleados">
              <Download size={15} />
              CSV
            </Link>
            {canWrite && !showForm && (
              <Link href="/empleados?nuevo=1" className="btn btn-primary">
                <Plus size={16} />
                Nuevo empleado
              </Link>
            )}
          </>
        }
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat
          label="Empleados activos"
          value={String(active.length)}
          hint={`${employees.length - active.length} inactivo(s)`}
          icon={<Users size={17} />}
        />
        <Stat
          label="Nómina mensual"
          value={formatMoney(monthlyPayroll)}
          hint="Sueldos brutos"
          icon={<Wallet size={17} />}
          tone="accent"
        />
        <Stat
          label="Sueldo promedio"
          value={formatMoney(average)}
          hint="Mensual bruto"
          icon={<Briefcase size={17} />}
        />
        <Stat
          label="Con cuenta bancaria"
          value={String(active.filter((employee) => employee.bank_account).length)}
          hint="Datos para dispersión"
          icon={<UserRoundCheck size={17} />}
        />
      </div>

      {showForm && (
        <div className="mt-5">
          <EmployeeForm employee={editing} />
        </div>
      )}

      <div className="mt-5 flex flex-wrap items-center justify-between gap-3">
        <form className="flex w-full flex-wrap gap-2 sm:w-auto" action="/empleados">
          <input
            className="input sm:w-64"
            name="q"
            defaultValue={params.q ?? ""}
            placeholder="Buscar nombre, puesto o RFC…"
            aria-label="Buscar empleados"
          />
          {includeInactive && <input type="hidden" name="inactivos" value="1" />}
          <button type="submit" className="btn btn-secondary">
            Filtrar
          </button>
        </form>
        <Link
          href={includeInactive ? "/empleados" : "/empleados?inactivos=1"}
          className="text-xs font-semibold text-accent hover:underline"
        >
          {includeInactive ? "Ocultar inactivos" : "Ver inactivos"}
        </Link>
      </div>

      <div className="card mt-4 overflow-hidden">
        {visible.length === 0 ? (
          <div className="p-5">
            <EmptyState
              icon={<Users size={18} />}
              title={q ? "Sin resultados" : "Aún no hay empleados"}
              description={
                q
                  ? "Prueba con otro término de búsqueda."
                  : "Agrega a tu equipo para calcular nómina, ISR e IMSS estimados."
              }
              action={
                canWrite ? (
                  <Link href="/empleados?nuevo=1" className="btn btn-primary btn-sm">
                    Nuevo empleado
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
                  <th>Empleado</th>
                  <th>Puesto</th>
                  <th>RFC</th>
                  <th>Ingreso</th>
                  <th className="num">Sueldo mensual</th>
                  <th>Estatus</th>
                  {canWrite && <th />}
                </tr>
              </thead>
              <tbody>
                {visible.map((employee) => (
                  <tr key={employee.id}>
                    <td>
                      <p className="font-medium text-ink">{employee.name}</p>
                      <p className="mt-0.5 text-[11px] text-muted">
                        {employee.department || "Sin departamento"}
                        {employee.end_date ? ` · Baja ${formatDate(employee.end_date)}` : ""}
                      </p>
                    </td>
                    <td className="text-muted">{employee.position || "—"}</td>
                    <td className="whitespace-nowrap text-muted">{employee.rfc || "—"}</td>
                    <td className="whitespace-nowrap text-muted">{formatDate(employee.start_date)}</td>
                    <td className="num font-semibold">
                      <Money cents={employee.salary_cents} />
                    </td>
                    <td>
                      <Badge tone={employee.status === "activo" ? "success" : "neutral"}>
                        {employee.status === "activo" ? "Activo" : "Inactivo"}
                      </Badge>
                    </td>
                    {canWrite && (
                      <td className="actions">
                        <div className="flex items-center justify-end gap-1">
                          <Link
                            href={`/empleados?editar=${employee.id}`}
                            className="btn btn-secondary btn-sm"
                          >
                            Editar
                          </Link>
                          <ActionForm
                            action={toggleEmployeeAction}
                            submitLabel={employee.status === "activo" ? "Dar de baja" : "Reactivar"}
                            variant="ghost"
                            small
                            confirm={
                              employee.status === "activo"
                                ? "¿Marcar como inactivo? No se incluirá en nóminas nuevas."
                                : "¿Reactivar a este empleado?"
                            }
                            submitClassName="mt-0 justify-end"
                          >
                            <input type="hidden" name="id" value={employee.id} />
                            <input
                              type="hidden"
                              name="status"
                              value={employee.status === "activo" ? "inactivo" : "activo"}
                            />
                          </ActionForm>
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
