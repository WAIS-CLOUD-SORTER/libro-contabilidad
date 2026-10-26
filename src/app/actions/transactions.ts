"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { z } from "zod";
import { audit } from "@/lib/audit";
import { requireUser } from "@/lib/auth";
import { one, run, tx } from "@/lib/db";
import { parseAmount } from "@/lib/money";
import { can, type ActionState } from "@/lib/permissions";
import { fdId, fdString, firstIssue, isoDateSchema } from "@/lib/validation";

const transactionSchema = z.object({
  date: isoDateSchema,
  type: z.enum(["ingreso", "gasto"]),
  categoryId: z.number().int().positive("Selecciona una categoría."),
  contactId: z.number().int().positive().nullable(),
  accountId: z.number().int().positive("Selecciona la cuenta de efectivo o banco."),
  description: z.string().trim().min(1, "Escribe una descripción.").max(300, "La descripción es demasiado larga."),
  subtotalCents: z.number().int().positive("El monto debe ser mayor a cero."),
  ivaRate: z.number().min(0).max(1),
  method: z.string().trim().max(80),
  reference: z.string().trim().max(120),
});

function parseBody(formData: FormData):
  | {
      data: {
        date: string;
        type: "ingreso" | "gasto";
        categoryId: number;
        contactId: number | null;
        accountId: number;
        description: string;
        subtotalCents: number;
        ivaRate: number;
        method: string;
        reference: string;
      };
    }
  | { error: string } {
  const subtotalCents = parseAmount(formData.get("subtotal"));
  if (subtotalCents === null || subtotalCents <= 0) {
    return { error: "Escribe un monto mayor a cero." };
  }

  const parsed = transactionSchema.safeParse({
    date: fdString(formData, "date"),
    type: fdString(formData, "type"),
    categoryId: fdId(formData, "categoryId") ?? 0,
    contactId: fdId(formData, "contactId"),
    accountId: fdId(formData, "accountId") ?? 0,
    description: fdString(formData, "description"),
    subtotalCents,
    ivaRate: Number(fdString(formData, "ivaRate")),
    method: fdString(formData, "method"),
    reference: fdString(formData, "reference"),
  });
  if (!parsed.success) return { error: firstIssue(parsed.error) };

  const category = one<{ kind: string }>("SELECT kind FROM categories WHERE id = ?", [
    parsed.data.categoryId,
  ]);
  if (!category) return { error: "La categoría seleccionada no existe." };
  if (parsed.data.type === "ingreso" && category.kind !== "ingreso") {
    return { error: "Un ingreso debe usar una categoría de ingreso." };
  }
  if (parsed.data.type === "gasto" && category.kind === "ingreso") {
    return { error: "Un gasto debe usar una categoría de gasto o costo." };
  }

  const account = one<{ id: number }>("SELECT id FROM accounts WHERE id = ? AND active = 1", [
    parsed.data.accountId,
  ]);
  if (!account) return { error: "La cuenta de efectivo no existe o está inactiva." };

  if (parsed.data.contactId) {
    const contact = one<{ id: number }>("SELECT id FROM contacts WHERE id = ?", [
      parsed.data.contactId,
    ]);
    if (!contact) return { error: "El contacto seleccionado no existe." };
  }

  return { data: parsed.data };
}

function revalidateTransactions(): void {
  revalidatePath("/movimientos");
  revalidatePath("/panel");
  revalidatePath("/reportes");
  revalidatePath("/impuestos");
}

async function writer(): Promise<{ userId: number } | { error: string }> {
  const user = await requireUser();
  if (!can(user.role, "tx.write")) return { error: "No tienes permiso para modificar movimientos." };
  return { userId: user.id };
}

export async function createTransactionAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;
  const parsed = parseBody(formData);
  if ("error" in parsed) return parsed;

  const { data } = parsed;
  const iva = Math.round(data.subtotalCents * data.ivaRate);
  const total = data.subtotalCents + iva;

  try {
    const id = tx(() => {
      const result = run(
        `INSERT INTO transactions
          (date, type, category_id, contact_id, account_id, description, subtotal_cents, iva_cents,
           total_cents, iva_rate, method, reference, source, created_by)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'manual', ?)`,
        [
          data.date,
          data.type,
          data.categoryId,
          data.contactId,
          data.accountId,
          data.description,
          data.subtotalCents,
          iva,
          total,
          data.ivaRate,
          data.method,
          data.reference,
          auth.userId,
        ],
      );
      audit(auth.userId, "create", "transaction", result.lastInsertRowid, { total });
      return result.lastInsertRowid;
    });
    void id;
  } catch {
    return { error: "No se pudo guardar el movimiento." };
  }

  revalidateTransactions();
  redirect("/movimientos");
}

export async function updateTransactionAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Movimiento inválido." };
  const parsed = parseBody(formData);
  if ("error" in parsed) return parsed;

  const { data } = parsed;
  const iva = Math.round(data.subtotalCents * data.ivaRate);
  const total = data.subtotalCents + iva;

  try {
    tx(() => {
      const current = one<{ source: string }>("SELECT source FROM transactions WHERE id = ?", [id]);
      if (!current) throw new Error("El movimiento no existe.");
      if (current.source !== "manual") {
        throw new Error("Este movimiento se generó automáticamente y no se puede editar.");
      }
      run(
        `UPDATE transactions SET date = ?, type = ?, category_id = ?, contact_id = ?, account_id = ?,
           description = ?, subtotal_cents = ?, iva_cents = ?, total_cents = ?, iva_rate = ?,
           method = ?, reference = ?, updated_at = datetime('now')
         WHERE id = ?`,
        [
          data.date,
          data.type,
          data.categoryId,
          data.contactId,
          data.accountId,
          data.description,
          data.subtotalCents,
          iva,
          total,
          data.ivaRate,
          data.method,
          data.reference,
          id,
        ],
      );
      audit(auth.userId, "update", "transaction", id, { total });
    });
  } catch (error) {
    return {
      error: error instanceof Error && error.message ? error.message : "No se pudo actualizar.",
    };
  }

  revalidateTransactions();
  redirect("/movimientos");
}

export async function deleteTransactionAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Movimiento inválido." };

  try {
    tx(() => {
      const current = one<{ source: string; total_cents: number }>(
        "SELECT source, total_cents FROM transactions WHERE id = ?",
        [id],
      );
      if (!current) throw new Error("El movimiento no existe.");
      if (current.source !== "manual") {
        throw new Error("Este movimiento se generó automáticamente y no se puede eliminar.");
      }
      run("DELETE FROM transactions WHERE id = ?", [id]);
      audit(auth.userId, "delete", "transaction", id, { total: current.total_cents });
    });
  } catch (error) {
    return {
      error: error instanceof Error && error.message ? error.message : "No se pudo eliminar.",
    };
  }

  revalidateTransactions();
  redirect("/movimientos");
}
