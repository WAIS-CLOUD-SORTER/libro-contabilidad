"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { z } from "zod";
import { audit } from "@/lib/audit";
import { requireUser } from "@/lib/auth";
import { one, run } from "@/lib/db";
import { parseAmount } from "@/lib/money";
import { can, type ActionState } from "@/lib/permissions";
import type { Employee } from "@/lib/types";
import {
  fdId,
  fdString,
  firstIssue,
  isoDateSchema,
  optionalIsoDateSchema,
} from "@/lib/validation";

const employeeSchema = z
  .object({
    name: z.string().trim().min(1, "Escribe el nombre del empleado.").max(160),
    rfc: z.string().trim().max(20, "El RFC es demasiado largo."),
    curp: z.string().trim().max(30, "La CURP es demasiado larga."),
    nss: z.string().trim().max(30, "El NSS es demasiado largo."),
    position: z.string().trim().max(120, "El puesto es demasiado largo."),
    department: z.string().trim().max(120, "El departamento es demasiado largo."),
    salaryCents: z.number().int().min(0),
    startDate: isoDateSchema,
    endDate: optionalIsoDateSchema,
    status: z.enum(["activo", "inactivo"]),
    bankName: z.string().trim().max(120, "El banco es demasiado largo."),
    bankAccount: z.string().trim().max(40, "La cuenta es demasiado larga."),
  })
  .refine(
    (data) => !data.endDate || data.endDate >= data.startDate,
    "La fecha de baja no puede ser anterior a la de ingreso.",
  );

function revalidateEmployees(id?: number): void {
  revalidatePath("/empleados");
  revalidatePath("/nomina");
  revalidatePath("/panel");
  if (id) revalidatePath(`/empleados/${id}`);
}

async function writer(): Promise<{ userId: number } | { error: string }> {
  const user = await requireUser();
  if (!can(user.role, "payroll.write")) {
    return { error: "No tienes permiso para modificar empleados." };
  }
  return { userId: user.id };
}

export async function saveEmployeeAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;

  const id = fdId(formData, "id");
  const salaryCents = parseAmount(formData.get("salary")) ?? 0;

  const parsed = employeeSchema.safeParse({
    name: fdString(formData, "name"),
    rfc: fdString(formData, "rfc").toUpperCase(),
    curp: fdString(formData, "curp").toUpperCase(),
    nss: fdString(formData, "nss"),
    position: fdString(formData, "position"),
    department: fdString(formData, "department"),
    salaryCents: Math.max(0, salaryCents),
    startDate: fdString(formData, "startDate"),
    endDate: fdString(formData, "endDate"),
    status: fdString(formData, "status") || "activo",
    bankName: fdString(formData, "bankName"),
    bankAccount: fdString(formData, "bankAccount"),
  });
  if (!parsed.success) return { error: firstIssue(parsed.error) };
  const data = parsed.data;

  try {
    if (id) {
      const current = one<Employee>("SELECT * FROM employees WHERE id = ?", [id]);
      if (!current) return { error: "El empleado no existe." };
      run(
        `UPDATE employees SET name = ?, rfc = ?, curp = ?, nss = ?, position = ?, department = ?,
           salary_cents = ?, start_date = ?, end_date = ?, status = ?, bank_name = ?, bank_account = ?
         WHERE id = ?`,
        [
          data.name,
          data.rfc,
          data.curp,
          data.nss,
          data.position,
          data.department,
          data.salaryCents,
          data.startDate,
          data.endDate || null,
          data.status,
          data.bankName,
          data.bankAccount,
          id,
        ],
      );
      audit(auth.userId, "update", "employee", id, { name: data.name });
    } else {
      const result = run(
        `INSERT INTO employees
           (name, rfc, curp, nss, position, department, salary_cents, start_date, end_date,
            status, bank_name, bank_account)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
        [
          data.name,
          data.rfc,
          data.curp,
          data.nss,
          data.position,
          data.department,
          data.salaryCents,
          data.startDate,
          data.endDate || null,
          data.status,
          data.bankName,
          data.bankAccount,
        ],
      );
      audit(auth.userId, "create", "employee", result.lastInsertRowid, { name: data.name });
    }
  } catch {
    return { error: "No se pudo guardar el empleado." };
  }

  revalidateEmployees(id ?? undefined);
  redirect("/empleados");
}

export async function toggleEmployeeAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Empleado inválido." };
  const status = fdString(formData, "status") === "activo" ? "activo" : "inactivo";

  try {
    run("UPDATE employees SET status = ? WHERE id = ?", [status, id]);
    audit(auth.userId, status === "activo" ? "restore" : "deactivate", "employee", id, null);
  } catch {
    return { error: "No se pudo actualizar el empleado." };
  }

  revalidateEmployees(id);
  redirect("/empleados");
}
