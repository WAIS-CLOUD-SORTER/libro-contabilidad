"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { z } from "zod";
import { audit } from "@/lib/audit";
import { requireUser } from "@/lib/auth";
import {
  cancelInvoice,
  createInvoice,
  deleteDraftInvoice,
  deletePayment,
  emitInvoice,
  parseInvoiceLines,
  registerPayment,
  updateInvoiceDraft,
} from "@/lib/invoices";
import { parseAmount } from "@/lib/money";
import { can, type ActionState } from "@/lib/permissions";
import {
  fdId,
  fdString,
  firstIssue,
  isoDateSchema,
  optionalIsoDateSchema,
} from "@/lib/validation";

const headerSchema = z.object({
  contactId: z.coerce.number().int().positive("Selecciona un cliente."),
  date: isoDateSchema,
  dueDate: isoDateSchema,
  notes: z.string().trim().max(2000, "Las notas son demasiado largas."),
  paymentTerms: z.string().trim().max(200, "Las condiciones son demasiado largas."),
});

function errorMessage(error: unknown): string {
  if (error instanceof Error && error.message && !error.message.includes("SQLITE")) {
    return error.message;
  }
  return "No se pudo guardar la factura. Revisa los datos.";
}

function revalidateInvoices(id?: number): void {
  revalidatePath("/facturas");
  revalidatePath("/panel");
  revalidatePath("/reportes");
  revalidatePath("/impuestos");
  revalidatePath("/inventario");
  if (id) revalidatePath(`/facturas/${id}`);
}

async function invoiceWriter(): Promise<{ userId: number } | { error: string }> {
  const user = await requireUser();
  if (!can(user.role, "invoice.write")) {
    return { error: "No tienes permiso para modificar facturas." };
  }
  return { userId: user.id };
}

export async function createInvoiceAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await invoiceWriter();
  if ("error" in auth) return auth;

  const parsedHeader = headerSchema.safeParse({
    contactId: fdString(formData, "contactId"),
    date: fdString(formData, "date"),
    dueDate: fdString(formData, "dueDate"),
    notes: fdString(formData, "notes"),
    paymentTerms: fdString(formData, "paymentTerms"),
  });
  if (!parsedHeader.success) return { error: firstIssue(parsedHeader.error) };

  const parsedLines = parseInvoiceLines(fdString(formData, "lines"));
  if (parsedLines.error) return { error: parsedLines.error };

  const intent = fdString(formData, "intent");
  const emit = intent === "emitir";

  let invoiceId = 0;
  try {
    invoiceId = createInvoice(
      {
        ...parsedHeader.data,
        lines: parsedLines.lines ?? [],
        userId: auth.userId,
      },
      emit,
    );
    audit(auth.userId, emit ? "emit" : "create", "invoice", invoiceId, {
      total: parsedLines.lines?.reduce(
        (sum, line) => sum + Math.round(line.qty * line.unitPriceCents * (1 + line.ivaRate)),
        0,
      ),
      emit,
    });
  } catch (error) {
    return { error: errorMessage(error) };
  }

  revalidateInvoices(invoiceId);
  redirect(`/facturas/${invoiceId}`);
}

export async function updateInvoiceAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await invoiceWriter();
  if ("error" in auth) return auth;

  const id = fdId(formData, "id");
  if (!id) return { error: "Factura inválida." };

  const parsedHeader = headerSchema.safeParse({
    contactId: fdString(formData, "contactId"),
    date: fdString(formData, "date"),
    dueDate: fdString(formData, "dueDate"),
    notes: fdString(formData, "notes"),
    paymentTerms: fdString(formData, "paymentTerms"),
  });
  if (!parsedHeader.success) return { error: firstIssue(parsedHeader.error) };

  const parsedLines = parseInvoiceLines(fdString(formData, "lines"));
  if (parsedLines.error) return { error: parsedLines.error };

  const emit = fdString(formData, "intent") === "emitir";
  try {
    updateInvoiceDraft(
      id,
      { ...parsedHeader.data, lines: parsedLines.lines ?? [], userId: auth.userId },
      emit,
    );
    audit(auth.userId, emit ? "emit" : "update", "invoice", id, { emit });
  } catch (error) {
    return { error: errorMessage(error) };
  }

  revalidateInvoices(id);
  redirect(`/facturas/${id}`);
}

export async function emitInvoiceAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await invoiceWriter();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Factura inválida." };

  try {
    emitInvoice(id);
    audit(auth.userId, "emit", "invoice", id, null);
  } catch (error) {
    return { error: errorMessage(error) };
  }

  revalidateInvoices(id);
  redirect(`/facturas/${id}`);
}

export async function cancelInvoiceAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await invoiceWriter();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Factura inválida." };
  const reason = fdString(formData, "reason");
  if (reason.length < 3) return { error: "Escribe el motivo de la cancelación." };

  try {
    cancelInvoice(id, reason, auth.userId);
    audit(auth.userId, "cancel", "invoice", id, { reason });
  } catch (error) {
    return { error: errorMessage(error) };
  }

  revalidateInvoices(id);
  redirect(`/facturas/${id}`);
}

export async function deleteInvoiceAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await invoiceWriter();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Factura inválida." };

  try {
    deleteDraftInvoice(id);
    audit(auth.userId, "delete", "invoice", id, null);
  } catch (error) {
    return { error: errorMessage(error) };
  }

  revalidateInvoices();
  redirect("/facturas");
}

export async function registerPaymentAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await invoiceWriter();
  if ("error" in auth) return auth;

  const id = fdId(formData, "id");
  if (!id) return { error: "Factura inválida." };
  const amount = parseAmount(formData.get("amount"));
  if (amount === null || amount <= 0) return { error: "Escribe un monto de pago válido." };
  const accountId = fdId(formData, "accountId");
  if (!accountId) return { error: "Selecciona la cuenta donde entró el pago." };

  const dateSchema = optionalIsoDateSchema;
  const parsedDate = dateSchema.safeParse(fdString(formData, "date"));
  if (!parsedDate.success) return { error: "Escribe una fecha de pago válida." };

  try {
    registerPayment({
      invoiceId: id,
      date: parsedDate.data,
      amountCents: amount,
      accountId,
      method: fdString(formData, "method"),
      reference: fdString(formData, "reference"),
      notes: fdString(formData, "notes"),
      userId: auth.userId,
    });
    audit(auth.userId, "payment", "invoice", id, { amount });
  } catch (error) {
    return { error: errorMessage(error) };
  }

  revalidateInvoices(id);
  revalidatePath("/movimientos");
  redirect(`/facturas/${id}`);
}

export async function deletePaymentAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await invoiceWriter();
  if ("error" in auth) return auth;
  const paymentId = fdId(formData, "paymentId");
  const invoiceId = fdId(formData, "invoiceId");
  if (!paymentId || !invoiceId) return { error: "Pago inválido." };

  try {
    deletePayment(paymentId);
    audit(auth.userId, "delete_payment", "invoice", invoiceId, { paymentId });
  } catch (error) {
    return { error: errorMessage(error) };
  }

  revalidateInvoices(invoiceId);
  redirect(`/facturas/${invoiceId}`);
}
