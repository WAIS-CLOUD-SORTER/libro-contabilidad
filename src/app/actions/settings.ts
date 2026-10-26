"use server";

import { revalidatePath } from "next/cache";
import fs from "node:fs";
import { z } from "zod";
import { audit } from "@/lib/audit";
import { requireUser } from "@/lib/auth";
import { createBackup, resolveBackup } from "@/lib/backups";
import { one, run } from "@/lib/db";
import { parseAmount } from "@/lib/money";
import { DEFAULT_ISR_BRACKETS } from "@/lib/payroll";
import { can, type ActionState } from "@/lib/permissions";
import { fdId, fdString, firstIssue } from "@/lib/validation";

async function settingsWriter(): Promise<{ userId: number } | { error: string }> {
  const user = await requireUser();
  if (!can(user.role, "settings.manage")) {
    return { error: "No tienes permiso para modificar la configuración." };
  }
  return { userId: user.id };
}

async function backupWriter(): Promise<{ userId: number } | { error: string }> {
  const user = await requireUser();
  if (!can(user.role, "backup.manage")) {
    return { error: "No tienes permiso para administrar respaldos." };
  }
  return { userId: user.id };
}

const companySchema = z.object({
  name: z.string().trim().min(1, "Escribe el nombre del negocio.").max(120),
  rfc: z.string().trim().max(20, "El RFC es demasiado largo."),
  regimenFiscal: z.string().trim().max(120, "El régimen fiscal es demasiado largo."),
  address: z.string().trim().max(500, "La dirección es demasiado larga."),
  phone: z.string().trim().max(60, "El teléfono es demasiado largo."),
  email: z.string().trim().max(160, "El correo es demasiado largo."),
  ivaRate: z.number().min(0).max(1),
  invoiceSeries: z.string().trim().min(1, "Escribe la serie de facturas.").max(8),
  nextInvoiceNumber: z.number().int().min(1, "El siguiente folio debe ser mayor a cero."),
  imssRate: z.number().min(0).max(0.5),
  isrBrackets: z.string().trim().optional(),
});

function parseBrackets(raw: string): string | null {
  try {
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed) || parsed.length === 0) return null;
    for (const item of parsed) {
      if (typeof item !== "object" || item === null) return null;
      const row = item as Record<string, unknown>;
      if (
        typeof row.lower !== "number" ||
        (typeof row.upper !== "number" && row.upper !== null) ||
        typeof row.rate !== "number" ||
        typeof row.fixed !== "number"
      ) {
        return null;
      }
      if (row.lower < 0 || row.rate < 0 || row.rate > 1 || row.fixed < 0) return null;
      if (typeof row.upper === "number" && row.upper <= row.lower) return null;
    }
    return JSON.stringify(parsed);
  } catch {
    return null;
  }
}

export async function saveCompanyAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await settingsWriter();
  if ("error" in auth) return auth;

  const parsed = companySchema.safeParse({
    name: fdString(formData, "name"),
    rfc: fdString(formData, "rfc").toUpperCase(),
    regimenFiscal: fdString(formData, "regimenFiscal"),
    address: fdString(formData, "address"),
    phone: fdString(formData, "phone"),
    email: fdString(formData, "email"),
    ivaRate: Number(fdString(formData, "ivaRate")),
    invoiceSeries: fdString(formData, "invoiceSeries").toUpperCase(),
    nextInvoiceNumber: Number(fdString(formData, "nextInvoiceNumber")),
    imssRate: Number(fdString(formData, "imssRate")),
    isrBrackets: fdString(formData, "isrBrackets"),
  });
  if (!parsed.success) return { error: firstIssue(parsed.error) };
  const data = parsed.data;

  const current = one<{ isr_brackets: string }>("SELECT isr_brackets FROM company WHERE id = 1");
  let brackets = current?.isr_brackets?.trim() || JSON.stringify(DEFAULT_ISR_BRACKETS);
  if (formData.has("isrBrackets")) {
    if (!data.isrBrackets) {
      return { error: "La tabla de ISR no puede estar vacía." };
    }
    const parsedBrackets = parseBrackets(data.isrBrackets);
    if (!parsedBrackets) {
      return {
        error:
          "La tabla de ISR debe ser un arreglo JSON válido con lower, upper, rate y fixed. Revisa el formato.",
      };
    }
    brackets = parsedBrackets;
  }

  const maxFolio = one<{ n: number }>(
    "SELECT COALESCE(MAX(number),0) AS n FROM invoices WHERE series = ?",
    [data.invoiceSeries],
  );
  if (data.nextInvoiceNumber <= Number(maxFolio?.n ?? 0)) {
    return {
      error: `El siguiente folio debe ser mayor a ${Number(maxFolio?.n ?? 0)} para la serie ${data.invoiceSeries}.`,
    };
  }

  try {
    run(
      `UPDATE company SET name = ?, rfc = ?, regimen_fiscal = ?, address = ?, phone = ?, email = ?,
         iva_rate = ?, invoice_series = ?, next_invoice_number = ?, imss_rate = ?, isr_brackets = ?
       WHERE id = 1`,
      [
        data.name,
        data.rfc,
        data.regimenFiscal,
        data.address,
        data.phone,
        data.email,
        data.ivaRate,
        data.invoiceSeries,
        data.nextInvoiceNumber,
        data.imssRate,
        brackets,
      ],
    );
    audit(auth.userId, "update", "company", 1, { name: data.name });
  } catch {
    return { error: "No se pudo guardar la configuración del negocio." };
  }

  revalidatePath("/configuracion");
  revalidatePath("/panel");
  revalidatePath("/facturas");
  revalidatePath("/nomina");
  return { success: "Configuración del negocio guardada." };
}

const accountSchema = z.object({
  name: z.string().trim().min(1, "Escribe el nombre de la cuenta.").max(80),
  type: z.enum(["efectivo", "banco", "tarjeta"]),
});

export async function saveAccountAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await settingsWriter();
  if ("error" in auth) return auth;

  const id = fdId(formData, "id");
  const openingCents = parseAmount(formData.get("openingBalance")) ?? 0;
  const parsed = accountSchema.safeParse({
    name: fdString(formData, "name"),
    type: fdString(formData, "type"),
  });
  if (!parsed.success) return { error: firstIssue(parsed.error) };
  const data = parsed.data;

  try {
    if (id) {
      run("UPDATE accounts SET name = ?, type = ?, opening_balance_cents = ? WHERE id = ?", [
        data.name,
        data.type,
        openingCents,
        id,
      ]);
      audit(auth.userId, "update", "account", id, { name: data.name });
    } else {
      const result = run(
        "INSERT INTO accounts (name, type, opening_balance_cents) VALUES (?, ?, ?)",
        [data.name, data.type, openingCents],
      );
      audit(auth.userId, "create", "account", result.lastInsertRowid, { name: data.name });
    }
  } catch (error) {
    if (error instanceof Error && error.message.includes("UNIQUE")) {
      return { error: "Ya existe una cuenta con ese nombre." };
    }
    return { error: "No se pudo guardar la cuenta." };
  }

  revalidatePath("/configuracion");
  revalidatePath("/panel");
  revalidatePath("/reportes");
  return { success: id ? "Cuenta actualizada." : "Cuenta creada." };
}

export async function toggleAccountAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await settingsWriter();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Cuenta inválida." };
  const active = fdString(formData, "active") === "1" ? 1 : 0;

  try {
    run("UPDATE accounts SET active = ? WHERE id = ?", [active, id]);
    audit(auth.userId, active ? "restore" : "archive", "account", id, null);
  } catch {
    return { error: "No se pudo actualizar la cuenta." };
  }

  revalidatePath("/configuracion");
  revalidatePath("/panel");
  return { success: active ? "Cuenta activada." : "Cuenta desactivada." };
}

const categorySchema = z.object({
  name: z.string().trim().min(1, "Escribe el nombre de la categoría.").max(80),
  kind: z.enum(["ingreso", "gasto", "costo"]),
});

export async function saveCategoryAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await settingsWriter();
  if ("error" in auth) return auth;

  const id = fdId(formData, "id");
  const parsed = categorySchema.safeParse({
    name: fdString(formData, "name"),
    kind: fdString(formData, "kind"),
  });
  if (!parsed.success) return { error: firstIssue(parsed.error) };
  const data = parsed.data;

  try {
    if (id) {
      run("UPDATE categories SET name = ?, kind = ? WHERE id = ?", [data.name, data.kind, id]);
      audit(auth.userId, "update", "category", id, { name: data.name });
    } else {
      const result = run("INSERT INTO categories (name, kind) VALUES (?, ?)", [
        data.name,
        data.kind,
      ]);
      audit(auth.userId, "create", "category", result.lastInsertRowid, { name: data.name });
    }
  } catch (error) {
    if (error instanceof Error && error.message.includes("UNIQUE")) {
      return { error: "Ya existe una categoría con ese nombre y tipo." };
    }
    return { error: "No se pudo guardar la categoría." };
  }

  revalidatePath("/configuracion");
  revalidatePath("/movimientos");
  return { success: id ? "Categoría actualizada." : "Categoría creada." };
}

export async function toggleCategoryAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await settingsWriter();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Categoría inválida." };
  const archived = fdString(formData, "archived") === "1" ? 1 : 0;

  try {
    run("UPDATE categories SET archived = ? WHERE id = ?", [archived, id]);
    audit(auth.userId, archived ? "archive" : "restore", "category", id, null);
  } catch {
    return { error: "No se pudo actualizar la categoría." };
  }

  revalidatePath("/configuracion");
  revalidatePath("/movimientos");
  return { success: archived ? "Categoría archivada." : "Categoría reactivada." };
}

export async function createBackupAction(
  _prev: ActionState,
  _formData: FormData,
): Promise<ActionState> {
  const auth = await backupWriter();
  if ("error" in auth) return auth;

  try {
    const name = await createBackup();
    audit(auth.userId, "backup", "database", name, null);
    revalidatePath("/configuracion");
    return { success: `Respaldo creado: ${name}` };
  } catch {
    return { error: "No se pudo crear el respaldo. Revisa el espacio en disco." };
  }
}

export async function deleteBackupAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await backupWriter();
  if ("error" in auth) return auth;
  const name = fdString(formData, "name");
  const resolved = resolveBackup(name);
  if (!resolved) return { error: "El respaldo no existe o el nombre no es válido." };

  try {
    fs.unlinkSync(resolved);
    audit(auth.userId, "delete_backup", "database", name, null);
    revalidatePath("/configuracion");
    return { success: "Respaldo eliminado." };
  } catch {
    return { error: "No se pudo eliminar el respaldo." };
  }
}

export async function restoreDefaultsAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await settingsWriter();
  if ("error" in auth) return auth;
  if (fdString(formData, "confirm") !== "restaurar") {
    return { error: 'Escribe "restaurar" para confirmar.' };
  }

  try {
    run("UPDATE company SET isr_brackets = ? WHERE id = 1", [
      JSON.stringify(DEFAULT_ISR_BRACKETS),
    ]);
    audit(auth.userId, "restore", "isr_brackets", null, null);
    revalidatePath("/configuracion");
    return { success: "Tarifa de ISR restaurada a los valores de referencia." };
  } catch {
    return { error: "No se pudo restaurar la tarifa." };
  }
}
