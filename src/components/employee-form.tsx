import Link from "next/link";
import type { Employee } from "@/lib/types";
import { saveEmployeeAction } from "@/app/actions/employees";
import { todayISO } from "@/lib/dates";
import { centsToInput } from "@/lib/money";
import { ActionForm } from "./action-form";
import { Field } from "./ui";

export function EmployeeForm({ employee }: { employee?: Employee }) {
  return (
    <section className="card p-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="section-title">{employee ? "Editar empleado" : "Nuevo empleado"}</h2>
          <p className="mt-0.5 text-xs text-muted">
            El sueldo es mensual bruto. La nómina se calcula por periodo.
          </p>
        </div>
        <Link href="/empleados" className="btn btn-ghost btn-sm">
          Cerrar
        </Link>
      </div>

      <ActionForm
        action={saveEmployeeAction}
        submitLabel={employee ? "Guardar cambios" : "Crear empleado"}
        className="mt-4"
      >
        {employee && <input type="hidden" name="id" value={employee.id} />}
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <Field label="Nombre completo" htmlFor="name" required className="sm:col-span-2">
            <input
              className="input"
              id="name"
              name="name"
              defaultValue={employee?.name ?? ""}
              maxLength={160}
              required
            />
          </Field>
          <Field label="Estatus" htmlFor="estado">
            <select className="select" id="estado" name="status" defaultValue={employee?.status ?? "activo"}>
              <option value="activo">Activo</option>
              <option value="inactivo">Inactivo</option>
            </select>
          </Field>
          <Field label="Puesto" htmlFor="position">
            <input
              className="input"
              id="position"
              name="position"
              defaultValue={employee?.position ?? ""}
              maxLength={120}
              placeholder="Ej. Ventas"
            />
          </Field>
          <Field label="Departamento" htmlFor="department">
            <input
              className="input"
              id="department"
              name="department"
              defaultValue={employee?.department ?? ""}
              maxLength={120}
            />
          </Field>
          <Field label="Sueldo mensual bruto" htmlFor="salary" hint="Antes de ISR e IMSS." required>
            <input
              className="input text-right tabular-nums"
              id="salary"
              name="salary"
              inputMode="decimal"
              defaultValue={employee ? centsToInput(employee.salary_cents) : ""}
              placeholder="0.00"
              required
            />
          </Field>
          <Field label="Fecha de ingreso" htmlFor="startDate" required>
            <input
              className="input"
              id="startDate"
              name="startDate"
              type="date"
              defaultValue={employee?.start_date ?? todayISO()}
              required
            />
          </Field>
          <Field label="Fecha de baja" htmlFor="endDate" hint="Opcional.">
            <input
              className="input"
              id="endDate"
              name="endDate"
              type="date"
              defaultValue={employee?.end_date ?? ""}
            />
          </Field>
          <Field label="RFC" htmlFor="rfc">
            <input
              className="input uppercase"
              id="rfc"
              name="rfc"
              defaultValue={employee?.rfc ?? ""}
              maxLength={20}
            />
          </Field>
          <Field label="CURP" htmlFor="curp">
            <input
              className="input uppercase"
              id="curp"
              name="curp"
              defaultValue={employee?.curp ?? ""}
              maxLength={30}
            />
          </Field>
          <Field label="NSS" htmlFor="nss">
            <input className="input" id="nss" name="nss" defaultValue={employee?.nss ?? ""} maxLength={30} />
          </Field>
          <Field label="Banco" htmlFor="bankName">
            <input
              className="input"
              id="bankName"
              name="bankName"
              defaultValue={employee?.bank_name ?? ""}
              maxLength={120}
            />
          </Field>
          <Field label="Cuenta bancaria" htmlFor="bankAccount">
            <input
              className="input"
              id="bankAccount"
              name="bankAccount"
              defaultValue={employee?.bank_account ?? ""}
              maxLength={40}
            />
          </Field>
        </div>
      </ActionForm>
    </section>
  );
}
