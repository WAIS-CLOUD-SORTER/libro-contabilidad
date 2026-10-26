"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { z } from "zod";
import { audit } from "@/lib/audit";
import { requireUser } from "@/lib/auth";
import { getCompany } from "@/lib/data";
import { all, one, run, tx } from "@/lib/db";
import { monthRange, nowISO } from "@/lib/dates";
import { parseAmount, parseQty } from "@/lib/money";
import { calculatePayrollItem, parseIsrBrackets, payrollPeriodLabel } from "@/lib/payroll";
import { can, type ActionState } from "@/lib/permissions";
import type { Employee, PayrollItem, PayrollPeriod } from "@/lib/types";
import { fdId, fdString, firstIssue, isoDateSchema } from "@/lib/validation";

function revalidatePayroll(id?: number): void {
  revalidatePath("/nomina");
  revalidatePath("/panel");
  revalidatePath("/movimientos");
  revalidatePath("/reportes");
  if (id) revalidatePath(`/nomina/${id}`);
}

async function writer(): Promise<{ userId: number } | { error: string }> {
  const user = await requireUser();
  if (!can(user.role, "payroll.write")) {
    return { error: "No tienes permiso para modificar la nómina." };
  }
  return { userId: user.id };
}

function periodDates(
  year: number,
  month: number,
  periodType: "mensual" | "quincenal",
  quincena: number,
): { start: string; end: string } {
  const range = monthRange(year, month);
  if (periodType === "mensual") return range;
  const mm = String(month).padStart(2, "0");
  if (quincena === 1) return { start: `${year}-${mm}-01`, end: `${year}-${mm}-15` };
  return { start: `${year}-${mm}-16`, end: range.end };
}

function activeEmployees(start: string, end: string): Employee[] {
  return all<Employee>(
    `SELECT * FROM employees
     WHERE status = 'activo' AND start_date <= ? AND (end_date IS NULL OR end_date >= ?)
     ORDER BY name`,
    [end, start],
  );
}

export async function createPayrollPeriodAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;

  const rawPeriod = fdString(formData, "period");
  const match = /^(\d{4})-(\d{2})$/.exec(rawPeriod);
  if (!match) return { error: "Selecciona el mes de la nómina." };
  const year = Number(match[1]);
  const month = Number(match[2]);
  if (month < 1 || month > 12) return { error: "Selecciona un mes válido." };

  const schema = z.object({
    periodType: z.enum(["mensual", "quincenal"]),
    quincena: z.coerce.number().int().min(0).max(2),
  });
  const parsed = schema.safeParse({
    periodType: fdString(formData, "periodType"),
    quincena: fdString(formData, "quincena") || 0,
  });
  if (!parsed.success) return { error: firstIssue(parsed.error) };
  const { periodType, quincena } = parsed.data;
  if (periodType === "quincenal" && (quincena < 1 || quincena > 2)) {
    return { error: "Selecciona la quincena." };
  }
  const q = periodType === "quincenal" ? quincena : 0;
  const { start, end } = periodDates(year, month, periodType, q);

  const company = getCompany();
  const brackets = parseIsrBrackets(company.isr_brackets);

  let periodId = 0;
  try {
    periodId = tx(() => {
      const result = run(
        `INSERT INTO payroll_periods
           (year, month, period_type, quincena, start_date, end_date, status, created_by)
         VALUES (?, ?, ?, ?, ?, ?, 'borrador', ?)`,
        [year, month, periodType, q, start, end, auth.userId],
      );
      const id = result.lastInsertRowid;

      for (const employee of activeEmployees(start, end)) {
        const calculated = calculatePayrollItem(
          {
            id: employee.id,
            name: employee.name,
            salaryCents: employee.salary_cents,
            startDate: employee.start_date,
            endDate: employee.end_date,
          },
          { start, end, periodType },
          { imssRate: company.imss_rate, brackets },
        );
        if (!calculated) continue;
        run(
          `INSERT INTO payroll_items
             (period_id, employee_id, days, base_cents, extra_perceptions_cents, isr_cents,
              imss_cents, infonavit_cents, other_deductions_cents, net_cents, notes)
           VALUES (?, ?, ?, ?, 0, ?, ?, 0, 0, ?, '')`,
          [
            id,
            employee.id,
            calculated.days,
            calculated.baseCents,
            calculated.isrCents,
            calculated.imssCents,
            calculated.netCents,
          ],
        );
      }

      audit(auth.userId, "create", "payroll", id, { year, month, periodType, quincena: q });
      return id;
    });
  } catch (error) {
    if (error instanceof Error && error.message.includes("UNIQUE")) {
      return { error: "Ya existe una nómina para ese periodo." };
    }
    return { error: "No se pudo crear la nómina." };
  }

  revalidatePayroll(periodId);
  redirect(`/nomina/${periodId}`);
}

export async function recalculatePayrollAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Nómina inválida." };

  const period = one<PayrollPeriod>("SELECT * FROM payroll_periods WHERE id = ?", [id]);
  if (!period) return { error: "La nómina no existe." };
  if (period.status === "pagada") return { error: "La nómina ya fue pagada; no se puede recalcular." };

  const company = getCompany();
  const brackets = parseIsrBrackets(company.isr_brackets);
  const existing = all<PayrollItem>("SELECT * FROM payroll_items WHERE period_id = ?", [id]);
  const existingByEmployee = new Map(existing.map((item) => [item.employee_id, item]));

  try {
    tx(() => {
      for (const employee of activeEmployees(period.start_date, period.end_date)) {
        const current = existingByEmployee.get(employee.id);
        const extra = current?.extra_perceptions_cents ?? 0;
        const infonavit = current?.infonavit_cents ?? 0;
        const other = current?.other_deductions_cents ?? 0;
        const notes = current?.notes ?? "";
        const calculated = calculatePayrollItem(
          {
            id: employee.id,
            name: employee.name,
            salaryCents: employee.salary_cents,
            startDate: employee.start_date,
            endDate: employee.end_date,
          },
          {
            start: period.start_date,
            end: period.end_date,
            periodType: period.period_type,
          },
          { imssRate: company.imss_rate, brackets, extraPerceptionsCents: extra },
        );
        if (!calculated) continue;
        const net = Math.max(
          0,
          calculated.baseCents +
            extra -
            calculated.isrCents -
            calculated.imssCents -
            infonavit -
            other,
        );

        if (current) {
          run(
            `UPDATE payroll_items SET days = ?, base_cents = ?, extra_perceptions_cents = ?,
               isr_cents = ?, imss_cents = ?, infonavit_cents = ?, other_deductions_cents = ?,
               net_cents = ?, notes = ?
             WHERE id = ?`,
            [
              calculated.days,
              calculated.baseCents,
              extra,
              calculated.isrCents,
              calculated.imssCents,
              infonavit,
              other,
              net,
              notes,
              current.id,
            ],
          );
        } else {
          run(
            `INSERT INTO payroll_items
               (period_id, employee_id, days, base_cents, extra_perceptions_cents, isr_cents,
                imss_cents, infonavit_cents, other_deductions_cents, net_cents, notes)
             VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, '')`,
            [
              id,
              employee.id,
              calculated.days,
              calculated.baseCents,
              extra,
              calculated.isrCents,
              calculated.imssCents,
              infonavit,
              other,
              net,
            ],
          );
        }
      }
      audit(auth.userId, "recalculate", "payroll", id, null);
    });
  } catch {
    return { error: "No se pudo recalcular la nómina." };
  }

  revalidatePayroll(id);
  redirect(`/nomina/${id}`);
}

export async function updatePayrollItemAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;
  const itemId = fdId(formData, "itemId");
  const periodId = fdId(formData, "periodId");
  if (!itemId || !periodId) return { error: "Concepto de nómina inválido." };

  const period = one<PayrollPeriod>("SELECT * FROM payroll_periods WHERE id = ?", [periodId]);
  if (!period) return { error: "La nómina no existe." };
  if (period.status === "pagada") return { error: "La nómina ya fue pagada; no se puede editar." };

  const item = one<PayrollItem>(
    "SELECT * FROM payroll_items WHERE id = ? AND period_id = ?",
    [itemId, periodId],
  );
  if (!item) return { error: "El concepto no pertenece a esta nómina." };

  const baseCents = parseAmount(formData.get("base")) ?? 0;
  const extraCents = parseAmount(formData.get("extra")) ?? 0;
  const isrCents = parseAmount(formData.get("isr")) ?? 0;
  const imssCents = parseAmount(formData.get("imss")) ?? 0;
  const infonavitCents = parseAmount(formData.get("infonavit")) ?? 0;
  const otherCents = parseAmount(formData.get("other")) ?? 0;
  const days = parseQty(formData.get("days")) ?? item.days;

  const values = [baseCents, extraCents, isrCents, imssCents, infonavitCents, otherCents];
  if (values.some((value) => value < 0)) {
    return { error: "Los importes no pueden ser negativos." };
  }
  if (days < 0) return { error: "Los días trabajados no pueden ser negativos." };

  const net = Math.max(
    0,
    baseCents + extraCents - isrCents - imssCents - infonavitCents - otherCents,
  );

  try {
    run(
      `UPDATE payroll_items SET days = ?, base_cents = ?, extra_perceptions_cents = ?,
         isr_cents = ?, imss_cents = ?, infonavit_cents = ?, other_deductions_cents = ?,
         net_cents = ?, notes = ?
       WHERE id = ?`,
      [
        days,
        baseCents,
        extraCents,
        isrCents,
        imssCents,
        infonavitCents,
        otherCents,
        net,
        fdString(formData, "notes").slice(0, 500),
        itemId,
      ],
    );
    audit(auth.userId, "update", "payroll_item", itemId, { net });
  } catch {
    return { error: "No se pudo guardar el concepto." };
  }

  revalidatePayroll(periodId);
  redirect(`/nomina/${periodId}`);
}

export async function deletePayrollItemAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;
  const itemId = fdId(formData, "itemId");
  const periodId = fdId(formData, "periodId");
  if (!itemId || !periodId) return { error: "Concepto de nómina inválido." };

  const period = one<PayrollPeriod>("SELECT * FROM payroll_periods WHERE id = ?", [periodId]);
  if (!period) return { error: "La nómina no existe." };
  if (period.status === "pagada") return { error: "La nómina ya fue pagada; no se puede editar." };

  try {
    run("DELETE FROM payroll_items WHERE id = ? AND period_id = ?", [itemId, periodId]);
    audit(auth.userId, "delete", "payroll_item", itemId, { periodId });
  } catch {
    return { error: "No se pudo quitar el concepto." };
  }

  revalidatePayroll(periodId);
  redirect(`/nomina/${periodId}`);
}

export async function payPayrollAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Nómina inválida." };
  const accountId = fdId(formData, "accountId");
  if (!accountId) return { error: "Selecciona la cuenta de pago." };

  const parsedDate = isoDateSchema.safeParse(fdString(formData, "date"));
  if (!parsedDate.success) return { error: "Escribe una fecha de pago válida." };
  const date = parsedDate.data;

  const period = one<PayrollPeriod>("SELECT * FROM payroll_periods WHERE id = ?", [id]);
  if (!period) return { error: "La nómina no existe." };
  if (period.status === "pagada") return { error: "La nómina ya fue pagada." };

  const totals = one<{ net: number; count: number }>(
    "SELECT COALESCE(SUM(net_cents),0) AS net, COUNT(*) AS count FROM payroll_items WHERE period_id = ?",
    [id],
  );
  const net = Number(totals?.net ?? 0);
  if (Number(totals?.count ?? 0) === 0) {
    return { error: "Agrega al menos un empleado antes de pagar la nómina." };
  }
  if (net <= 0) return { error: "El neto de la nómina debe ser mayor a cero." };

  const account = one<{ id: number; name: string }>(
    "SELECT id, name FROM accounts WHERE id = ? AND active = 1",
    [accountId],
  );
  if (!account) return { error: "La cuenta de pago no existe o está inactiva." };

  try {
    tx(() => {
      let category = one<{ id: number }>(
        "SELECT id FROM categories WHERE name = 'Nómina' AND kind = 'gasto'",
      );
      if (!category) {
        const created = run("INSERT INTO categories (name, kind) VALUES ('Nómina', 'gasto')");
        category = { id: created.lastInsertRowid };
      }

      const transaction = run(
        `INSERT INTO transactions
           (date, type, category_id, account_id, description, subtotal_cents, iva_cents,
            total_cents, iva_rate, method, reference, source, source_id, created_by)
         VALUES (?, 'gasto', ?, ?, ?, ?, 0, ?, 0, 'Nómina', ?, 'nomina', ?, ?)`,
        [
          date,
          category.id,
          account.id,
          `Nómina ${payrollPeriodLabel(period)}`,
          net,
          net,
          `Periodo ${period.start_date} a ${period.end_date}`,
          period.id,
          auth.userId,
        ],
      );

      run(
        `UPDATE payroll_periods
         SET status = 'pagada', account_id = ?, transaction_id = ?, paid_at = ?
         WHERE id = ?`,
        [account.id, transaction.lastInsertRowid, nowISO(), id],
      );
      audit(auth.userId, "pay", "payroll", id, { net, accountId: account.id, date });
    });
  } catch {
    return { error: "No se pudo registrar el pago de la nómina." };
  }

  revalidatePayroll(id);
  redirect(`/nomina/${id}`);
}

export async function deletePayrollPeriodAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Nómina inválida." };

  const period = one<PayrollPeriod>("SELECT * FROM payroll_periods WHERE id = ?", [id]);
  if (!period) return { error: "La nómina no existe." };
  if (period.status === "pagada") {
    return { error: "No se puede eliminar una nómina pagada. Elimina el movimiento de gasto si necesitas corregir." };
  }

  try {
    run("DELETE FROM payroll_periods WHERE id = ?", [id]);
    audit(auth.userId, "delete", "payroll", id, null);
  } catch {
    return { error: "No se pudo eliminar la nómina." };
  }

  revalidatePayroll();
  redirect("/nomina");
}

export async function revertPayrollPaymentAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Nómina inválida." };

  const period = one<PayrollPeriod>("SELECT * FROM payroll_periods WHERE id = ?", [id]);
  if (!period) return { error: "La nómina no existe." };
  if (period.status !== "pagada") return { error: "La nómina no está pagada." };

  try {
    tx(() => {
      if (period.transaction_id) {
        const transaction = one<{ source: string; source_id: number | null }>(
          "SELECT source, source_id FROM transactions WHERE id = ?",
          [period.transaction_id],
        );
        if (
          transaction &&
          transaction.source === "nomina" &&
          Number(transaction.source_id) === period.id
        ) {
          run("DELETE FROM transactions WHERE id = ?", [period.transaction_id]);
        } else if (transaction) {
          throw new Error("El gasto vinculado ya no corresponde a esta nómina.");
        }
      }
      run(
        `UPDATE payroll_periods
         SET status = 'borrador', account_id = NULL, transaction_id = NULL, paid_at = NULL
         WHERE id = ?`,
        [id],
      );
      audit(auth.userId, "revert_payment", "payroll", id, { transactionId: period.transaction_id });
    });
  } catch (error) {
    return {
      error:
        error instanceof Error && error.message
          ? error.message
          : "No se pudo revertir el pago de la nómina.",
    };
  }

  revalidatePayroll(id);
  redirect(`/nomina/${id}`);
}
