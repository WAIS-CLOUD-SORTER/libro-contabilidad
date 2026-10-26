import { z } from "zod";
import { formatMoney, formatQty, lineTotals } from "./money";
import { all, one, run, tx } from "./db";
import { todayISO } from "./dates";
import type { InvoiceItem, InvoiceRow, Product } from "./types";

export type InvoiceLineInput = {
  productId: number | null;
  description: string;
  qty: number;
  unitPriceCents: number;
  ivaRate: number;
};

const lineSchema = z.object({
  productId: z.number().int().positive().nullable(),
  description: z.string().trim().min(1, "Cada concepto necesita una descripción.").max(300),
  qty: z.number().positive("La cantidad debe ser mayor a cero.").max(1_000_000),
  unitPriceCents: z.number().int().min(0, "El precio no puede ser negativo."),
  ivaRate: z.number().min(0).max(1),
});

const linesSchema = z
  .array(lineSchema)
  .min(1, "Agrega al menos un concepto.")
  .max(100, "Demasiados conceptos en una factura.");

export function parseInvoiceLines(raw: string): { lines?: InvoiceLineInput[]; error?: string } {
  if (!raw.trim()) return { error: "Agrega al menos un concepto." };
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw);
  } catch {
    return { error: "No se pudieron leer los conceptos de la factura." };
  }
  const result = linesSchema.safeParse(parsed);
  if (!result.success) {
    return { error: result.error.issues[0]?.message ?? "Revisa los conceptos de la factura." };
  }
  return { lines: result.data };
}

type InvoiceInput = {
  contactId: number;
  date: string;
  dueDate: string;
  notes: string;
  paymentTerms: string;
  lines: InvoiceLineInput[];
  userId: number | null;
};

function contactExists(contactId: number): boolean {
  return Boolean(one<{ id: number }>("SELECT id FROM contacts WHERE id = ?", [contactId]));
}

export function createInvoice(input: InvoiceInput, emit: boolean): number {
  if (!contactExists(input.contactId)) throw new Error("El cliente seleccionado no existe.");
  if (input.dueDate < input.date) throw new Error("La fecha de vencimiento no puede ser anterior a la fecha de la factura.");

  return tx(() => {
    const company = one<{ invoice_series: string }>("SELECT invoice_series FROM company WHERE id = 1");
    const series = company?.invoice_series?.trim() || "A";
    const totals = input.lines.reduce(
      (acc, line) => {
        const lineTotal = lineTotals(line.qty, line.unitPriceCents, line.ivaRate);
        acc.subtotal += lineTotal.subtotal;
        acc.iva += lineTotal.iva;
        acc.total += lineTotal.total;
        return acc;
      },
      { subtotal: 0, iva: 0, total: 0 },
    );

    const invoiceResult = run(
      `INSERT INTO invoices
        (series, number, contact_id, date, due_date, status, subtotal_cents, iva_cents, total_cents, notes, payment_terms, created_by)
       VALUES (?, NULL, ?, ?, ?, 'borrador', ?, ?, ?, ?, ?, ?)`,
      [
        series,
        input.contactId,
        input.date,
        input.dueDate,
        totals.subtotal,
        totals.iva,
        totals.total,
        input.notes,
        input.paymentTerms,
        input.userId,
      ],
    );
    const invoiceId = invoiceResult.lastInsertRowid;
    insertItems(invoiceId, input.lines);
    if (emit) emitInvoice(invoiceId);
    return invoiceId;
  });
}

function insertItems(invoiceId: number, lines: InvoiceLineInput[]): void {
  lines.forEach((line, index) => {
    const product = line.productId
      ? one<Product>("SELECT * FROM products WHERE id = ?", [line.productId])
      : undefined;
    const lineTotal = lineTotals(line.qty, line.unitPriceCents, line.ivaRate);
    run(
      `INSERT INTO invoice_items
        (invoice_id, product_id, position, description, qty, unit_price_cents, iva_rate,
         subtotal_cents, iva_cents, total_cents, unit_cost_cents)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
      [
        invoiceId,
        line.productId,
        index,
        line.description,
        line.qty,
        line.unitPriceCents,
        line.ivaRate,
        lineTotal.subtotal,
        lineTotal.iva,
        lineTotal.total,
        product?.cost_cents ?? 0,
      ],
    );
  });
}

export function updateInvoiceDraft(invoiceId: number, input: InvoiceInput, emit: boolean): void {
  if (!contactExists(input.contactId)) throw new Error("El cliente seleccionado no existe.");
  if (input.dueDate < input.date) throw new Error("La fecha de vencimiento no puede ser anterior a la fecha de la factura.");

  tx(() => {
    const invoice = one<InvoiceRow>("SELECT * FROM invoices WHERE id = ?", [invoiceId]);
    if (!invoice) throw new Error("La factura no existe.");
    if (invoice.status !== "borrador") throw new Error("Solo puedes editar facturas en borrador.");

    const totals = input.lines.reduce(
      (acc, line) => {
        const lineTotal = lineTotals(line.qty, line.unitPriceCents, line.ivaRate);
        acc.subtotal += lineTotal.subtotal;
        acc.iva += lineTotal.iva;
        acc.total += lineTotal.total;
        return acc;
      },
      { subtotal: 0, iva: 0, total: 0 },
    );

    run(
      `UPDATE invoices SET contact_id = ?, date = ?, due_date = ?, payment_terms = ?, notes = ?,
         subtotal_cents = ?, iva_cents = ?, total_cents = ?, updated_at = datetime('now')
       WHERE id = ?`,
      [
        input.contactId,
        input.date,
        input.dueDate,
        input.paymentTerms,
        input.notes,
        totals.subtotal,
        totals.iva,
        totals.total,
        invoiceId,
      ],
    );
    run("DELETE FROM invoice_items WHERE invoice_id = ?", [invoiceId]);
    insertItems(invoiceId, input.lines);
    if (emit) emitInvoice(invoiceId);
  });
}

export function emitInvoice(invoiceId: number): void {
  tx(() => {
    const invoice = one<InvoiceRow>("SELECT * FROM invoices WHERE id = ?", [invoiceId]);
    if (!invoice) throw new Error("La factura no existe.");
    if (invoice.status !== "borrador") throw new Error("Esta factura ya no se puede emitir.");
    if (!contactExists(invoice.contact_id)) throw new Error("La factura no tiene un cliente válido.");

    const items = one<{ n: number }>(
      "SELECT COUNT(*) AS n FROM invoice_items WHERE invoice_id = ?",
      [invoiceId],
    );
    if (!items || Number(items.n) === 0) throw new Error("Agrega al menos un concepto antes de emitir.");

    const lines = one<{ subtotal: number; iva: number; total: number }>(
      `SELECT COALESCE(SUM(subtotal_cents),0) AS subtotal,
              COALESCE(SUM(iva_cents),0) AS iva,
              COALESCE(SUM(total_cents),0) AS total
       FROM invoice_items WHERE invoice_id = ?`,
      [invoiceId],
    );

    const stockRows = one<{ bad: number }>(
      `SELECT COUNT(*) AS bad FROM (
         SELECT ii.product_id, SUM(ii.qty) AS needed, p.stock, p.track_stock
         FROM invoice_items ii JOIN products p ON p.id = ii.product_id
         WHERE ii.invoice_id = ? AND p.track_stock = 1
         GROUP BY ii.product_id
         HAVING SUM(ii.qty) > p.stock
       )`,
      [invoiceId],
    );
    if (stockRows && Number(stockRows.bad) > 0) {
      const missing = one<{ name: string; stock: number; needed: number }>(
        `SELECT p.name AS name, p.stock AS stock, SUM(ii.qty) AS needed
         FROM invoice_items ii JOIN products p ON p.id = ii.product_id
         WHERE ii.invoice_id = ? AND p.track_stock = 1
         GROUP BY ii.product_id
         HAVING SUM(ii.qty) > p.stock
         LIMIT 1`,
        [invoiceId],
      );
      if (missing) {
        throw new Error(
          `Stock insuficiente de ${missing.name}: disponible ${formatQty(Number(missing.stock))}, solicitado ${formatQty(Number(missing.needed))}.`,
        );
      }
      throw new Error("Stock insuficiente en uno de los productos.");
    }

    const company = one<{ next_invoice_number: number; invoice_series: string }>(
      "SELECT next_invoice_number, invoice_series FROM company WHERE id = 1",
    );
    let number = Number(company?.next_invoice_number ?? 1);
    const series = invoice.series || company?.invoice_series || "A";
    while (
      one<{ id: number }>("SELECT id FROM invoices WHERE series = ? AND number = ?", [series, number])
    ) {
      number += 1;
    }

    run(
      `UPDATE invoices SET number = ?, status = 'emitida', subtotal_cents = ?, iva_cents = ?,
         total_cents = ?, updated_at = datetime('now') WHERE id = ?`,
      [number, Number(lines?.subtotal ?? 0), Number(lines?.iva ?? 0), Number(lines?.total ?? 0), invoiceId],
    );
    run("UPDATE company SET next_invoice_number = ? WHERE id = 1", [number + 1]);

    const productItems = all<{ product_id: number; qty: number; unit_cost_cents: number }>(
      `SELECT product_id, qty, unit_cost_cents FROM invoice_items
       WHERE invoice_id = ? AND product_id IS NOT NULL`,
      [invoiceId],
    ).map((item) => ({
      productId: item.product_id,
      qty: Number(item.qty),
      unitCost: item.unit_cost_cents,
    }));

    for (const item of productItems) {
      const product = one<Product>("SELECT * FROM products WHERE id = ?", [item.productId]);
      if (!product) continue;
      if (Number(product.track_stock) === 1) {
        run("UPDATE products SET stock = stock - ? WHERE id = ?", [item.qty, item.productId]);
      }
      run(
        `INSERT INTO inventory_movements
          (product_id, date, type, qty, unit_cost_cents, reason, reference, created_by)
         VALUES (?, ?, 'salida', ?, ?, 'Venta facturada', ?, ?)`,
        [
          item.productId,
          invoice.date,
          -Math.abs(item.qty),
          item.unitCost,
          `Factura ${series}-${String(number).padStart(4, "0")}`,
          invoice.created_by,
        ],
      );
    }
  });
}

export function cancelInvoice(invoiceId: number, reason: string, userId: number | null): void {
  if (!reason.trim()) throw new Error("Escribe el motivo de la cancelación.");
  tx(() => {
    const invoice = one<InvoiceRow>("SELECT * FROM invoices WHERE id = ?", [invoiceId]);
    if (!invoice) throw new Error("La factura no existe.");
    if (invoice.status === "cancelada") throw new Error("Esta factura ya está cancelada.");
    if (invoice.paid_cents > 0) {
      throw new Error("No puedes cancelar una factura con pagos registrados. Elimina los pagos primero.");
    }

    if (invoice.status !== "borrador") {
      const productItems = all<{ product_id: number; qty: number; unit_cost_cents: number }>(
        `SELECT product_id, qty, unit_cost_cents FROM invoice_items
         WHERE invoice_id = ? AND product_id IS NOT NULL`,
        [invoiceId],
      ).map((item) => ({
        productId: item.product_id,
        qty: Number(item.qty),
        unitCost: item.unit_cost_cents,
      }));
      for (const item of productItems) {
        const product = one<Product>("SELECT * FROM products WHERE id = ?", [item.productId]);
        if (!product) continue;
        if (Number(product.track_stock) === 1) {
          run("UPDATE products SET stock = stock + ? WHERE id = ?", [item.qty, item.productId]);
        }
        run(
          `INSERT INTO inventory_movements
            (product_id, date, type, qty, unit_cost_cents, reason, reference, created_by)
           VALUES (?, ?, 'entrada', ?, ?, 'Cancelación de factura', ?, ?)`,
          [
            item.productId,
            todayISO(),
            Math.abs(item.qty),
            item.unitCost,
            `Factura ${invoice.series}-${String(invoice.number ?? 0).padStart(4, "0")}`,
            userId,
          ],
        );
      }
    }

    run(
      `UPDATE invoices SET status = 'cancelada', cancelled_reason = ?, cancelled_at = datetime('now'),
         updated_at = datetime('now') WHERE id = ?`,
      [reason.trim(), invoiceId],
    );
  });
}

export function deleteDraftInvoice(invoiceId: number): void {
  tx(() => {
    const invoice = one<InvoiceRow>("SELECT * FROM invoices WHERE id = ?", [invoiceId]);
    if (!invoice) throw new Error("La factura no existe.");
    if (invoice.status !== "borrador") throw new Error("Solo puedes eliminar facturas en borrador.");
    run("DELETE FROM invoices WHERE id = ?", [invoiceId]);
  });
}

export function registerPayment(input: {
  invoiceId: number;
  date: string;
  amountCents: number;
  accountId: number | null;
  method: string;
  reference: string;
  notes: string;
  userId: number | null;
}): void {
  tx(() => {
    const invoice = one<InvoiceRow>("SELECT * FROM invoices WHERE id = ?", [input.invoiceId]);
    if (!invoice) throw new Error("La factura no existe.");
    if (invoice.status !== "emitida" && invoice.status !== "pagada") {
      throw new Error("Solo puedes registrar pagos de facturas emitidas.");
    }
    if (input.amountCents <= 0) throw new Error("El pago debe ser mayor a cero.");
    const outstanding = invoice.total_cents - invoice.paid_cents;
    if (input.amountCents > outstanding) {
      throw new Error(`El pago excede el saldo pendiente (${formatMoney(outstanding)}).`);
    }
    if (input.accountId) {
      const account = one<{ id: number }>("SELECT id FROM accounts WHERE id = ?", [input.accountId]);
      if (!account) throw new Error("La cuenta de efectivo no existe.");
    }

    run(
      `INSERT INTO payments (invoice_id, date, amount_cents, account_id, method, reference, notes, created_by)
       VALUES (?, ?, ?, ?, ?, ?, ?, ?)`,
      [
        input.invoiceId,
        input.date,
        input.amountCents,
        input.accountId,
        input.method,
        input.reference,
        input.notes,
        input.userId,
      ],
    );
    const newPaid = invoice.paid_cents + input.amountCents;
    run("UPDATE invoices SET paid_cents = ?, status = ?, updated_at = datetime('now') WHERE id = ?", [
      newPaid,
      newPaid >= invoice.total_cents ? "pagada" : "emitida",
      input.invoiceId,
    ]);
  });
}

export function deletePayment(paymentId: number): void {
  tx(() => {
    const payment = one<{ id: number; invoice_id: number; amount_cents: number }>(
      "SELECT id, invoice_id, amount_cents FROM payments WHERE id = ?",
      [paymentId],
    );
    if (!payment) throw new Error("El pago no existe.");
    const invoice = one<InvoiceRow>("SELECT * FROM invoices WHERE id = ?", [payment.invoice_id]);
    if (!invoice) throw new Error("La factura no existe.");
    if (invoice.status === "cancelada") throw new Error("La factura está cancelada.");

    run("DELETE FROM payments WHERE id = ?", [paymentId]);
    const newPaid = Math.max(0, invoice.paid_cents - payment.amount_cents);
    run("UPDATE invoices SET paid_cents = ?, status = ?, updated_at = datetime('now') WHERE id = ?", [
      newPaid,
      newPaid >= invoice.total_cents && invoice.total_cents > 0 ? "pagada" : "emitida",
      invoice.id,
    ]);
  });
}

export type InvoiceWithItems = { invoice: InvoiceRow; items: InvoiceItem[] };
