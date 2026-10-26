"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { z } from "zod";
import { audit } from "@/lib/audit";
import { requireUser } from "@/lib/auth";
import { one, run, tx } from "@/lib/db";
import { todayISO } from "@/lib/dates";
import { parseAmount, parseQty } from "@/lib/money";
import { can, type ActionState } from "@/lib/permissions";
import type { Product } from "@/lib/types";
import { fdId, fdString, firstIssue, isoDateSchema } from "@/lib/validation";

const productSchema = z.object({
  name: z.string().trim().min(1, "Escribe el nombre del producto.").max(160),
  sku: z.string().trim().max(80, "El SKU es demasiado largo."),
  description: z.string().trim().max(1000, "La descripción es demasiado larga."),
  unit: z.string().trim().min(1, "Escribe la unidad.").max(40),
  priceCents: z.number().int().min(0),
  costCents: z.number().int().min(0),
  ivaRate: z.number().min(0).max(1),
  minStock: z.number().min(0),
  trackStock: z.boolean(),
  initialStock: z.number().min(0),
});

function revalidateInventory(productId?: number): void {
  revalidatePath("/inventario");
  revalidatePath("/panel");
  revalidatePath("/reportes");
  revalidatePath("/impuestos");
  if (productId) revalidatePath(`/inventario/${productId}`);
}

async function writer(): Promise<{ userId: number } | { error: string }> {
  const user = await requireUser();
  if (!can(user.role, "products.write") && !can(user.role, "inventory.write")) {
    return { error: "No tienes permiso para modificar inventario." };
  }
  return { userId: user.id };
}

export async function saveProductAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;

  const id = fdId(formData, "id");
  const priceCents = parseAmount(formData.get("price")) ?? 0;
  const costCents = parseAmount(formData.get("cost")) ?? 0;
  const minStock = parseQty(formData.get("minStock")) ?? 0;
  const initialStock = parseQty(formData.get("initialStock")) ?? 0;
  const trackStock = ["1", "on", "true"].includes(fdString(formData, "trackStock"));

  const parsed = productSchema.safeParse({
    name: fdString(formData, "name"),
    sku: fdString(formData, "sku"),
    description: fdString(formData, "description"),
    unit: fdString(formData, "unit") || "pieza",
    priceCents: Math.max(0, priceCents),
    costCents: Math.max(0, costCents),
    ivaRate: Number(fdString(formData, "ivaRate")),
    minStock: Math.max(0, minStock),
    trackStock,
    initialStock: Math.max(0, initialStock),
  });
  if (!parsed.success) return { error: firstIssue(parsed.error) };
  const data = parsed.data;

  let createdId: number | null = null;
  try {
    if (id) {
      const current = one<Product>("SELECT * FROM products WHERE id = ?", [id]);
      if (!current) return { error: "El producto no existe." };
      run(
        `UPDATE products SET sku = ?, name = ?, description = ?, unit = ?, price_cents = ?,
           cost_cents = ?, iva_rate = ?, min_stock = ?, track_stock = ? WHERE id = ?`,
        [
          data.sku,
          data.name,
          data.description,
          data.unit,
          data.priceCents,
          data.costCents,
          data.ivaRate,
          data.minStock,
          data.trackStock ? 1 : 0,
          id,
        ],
      );
      audit(auth.userId, "update", "product", id, { name: data.name });
    } else {
      createdId = tx(() => {
        const result = run(
          `INSERT INTO products (sku, name, description, unit, price_cents, cost_cents, iva_rate,
             stock, min_stock, track_stock, active)
           VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1)`,
          [
            data.sku,
            data.name,
            data.description,
            data.unit,
            data.priceCents,
            data.costCents,
            data.ivaRate,
            data.trackStock ? data.initialStock : 0,
            data.minStock,
            data.trackStock ? 1 : 0,
          ],
        );
        if (data.trackStock && data.initialStock > 0) {
          run(
            `INSERT INTO inventory_movements
              (product_id, date, type, qty, unit_cost_cents, reason, created_by)
             VALUES (?, ?, 'ajuste', ?, ?, 'Saldo inicial', ?)`,
            [result.lastInsertRowid, todayISO(), data.initialStock, data.costCents, auth.userId],
          );
        }
        audit(auth.userId, "create", "product", result.lastInsertRowid, { name: data.name });
        return result.lastInsertRowid;
      });
    }
  } catch (error) {
    if (error instanceof Error && error.message.includes("UNIQUE")) {
      return { error: "Ya existe un producto con ese SKU." };
    }
    return { error: "No se pudo guardar el producto." };
  }

  revalidateInventory(createdId ?? id ?? undefined);
  redirect(createdId ? `/inventario/${createdId}` : "/inventario");
}

export async function toggleProductAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Producto inválido." };
  const active = fdString(formData, "active") === "1" ? 1 : 0;

  try {
    run("UPDATE products SET active = ? WHERE id = ?", [active, id]);
    audit(auth.userId, active ? "restore" : "archive", "product", id, null);
  } catch {
    return { error: "No se pudo actualizar el producto." };
  }

  revalidateInventory(id);
  redirect("/inventario");
}

const movementSchema = z.object({
  productId: z.number().int().positive(),
  date: isoDateSchema,
  type: z.enum(["entrada", "salida", "ajuste"]),
  qty: z.number().min(0),
  unitCostCents: z.number().int().min(0),
  ivaRate: z.number().min(0).max(1),
  reason: z.string().trim().max(300),
  reference: z.string().trim().max(120),
  accountId: z.number().int().positive().nullable(),
});

export async function addInventoryMovementAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;

  const qty = parseQty(formData.get("qty"));
  if (qty === null) return { error: "Escribe una cantidad válida." };
  const accountId = fdId(formData, "accountId");

  const parsed = movementSchema.safeParse({
    productId: fdId(formData, "productId") ?? 0,
    date: fdString(formData, "date"),
    type: fdString(formData, "type"),
    qty,
    unitCostCents: Math.max(0, parseAmount(formData.get("unitCost")) ?? 0),
    ivaRate: Number(fdString(formData, "ivaRate")),
    reason: fdString(formData, "reason"),
    reference: fdString(formData, "reference"),
    accountId,
  });
  if (!parsed.success) return { error: firstIssue(parsed.error) };
  const data = parsed.data;

  try {
    tx(() => {
      const product = one<Product>("SELECT * FROM products WHERE id = ?", [data.productId]);
      if (!product) throw new Error("El producto no existe.");
      if (Number(product.track_stock) !== 1) {
        throw new Error("Este producto no controla inventario.");
      }
      if (data.type !== "ajuste" && data.qty <= 0) {
        throw new Error("La cantidad debe ser mayor a cero.");
      }

      let delta = 0;
      let newStock = Number(product.stock);
      let newCost = Number(product.cost_cents);
      let unitCost = Number(product.cost_cents);

      if (data.type === "entrada") {
        delta = data.qty;
        unitCost = data.unitCostCents > 0 ? data.unitCostCents : Number(product.cost_cents);
        newStock = Number(product.stock) + delta;
        const baseStock = Math.max(0, Number(product.stock));
        newCost =
          newStock > 0
            ? Math.round((baseStock * Number(product.cost_cents) + delta * unitCost) / newStock)
            : unitCost;
      } else if (data.type === "salida") {
        if (data.qty > Number(product.stock)) {
          throw new Error(
            `Stock insuficiente: disponible ${Number(product.stock)}, solicitado ${data.qty}.`,
          );
        }
        delta = -data.qty;
        newStock = Number(product.stock) + delta;
      } else {
        delta = data.qty - Number(product.stock);
        if (delta === 0) throw new Error("El stock real es igual al actual; no hay ajuste.");
        newStock = data.qty;
      }

      if (newStock < 0) throw new Error("El movimiento dejaría el stock en negativo.");

      const movementResult = run(
        `INSERT INTO inventory_movements
          (product_id, date, type, qty, unit_cost_cents, reason, reference, account_id, created_by)
         VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)`,
        [
          data.productId,
          data.date,
          data.type,
          delta,
          unitCost,
          data.reason ||
            (data.type === "entrada"
              ? "Entrada de inventario"
              : data.type === "salida"
                ? "Salida manual"
                : "Ajuste de inventario"),
          data.reference,
          data.accountId,
          auth.userId,
        ],
      );

      run("UPDATE products SET stock = ?, cost_cents = ? WHERE id = ?", [
        newStock,
        newCost,
        data.productId,
      ]);

      if (data.type === "entrada" && data.accountId) {
        const subtotal = Math.round(data.qty * unitCost);
        if (subtotal <= 0) {
          throw new Error("Escribe el costo unitario para registrar la compra en efectivo.");
        }
        const account = one<{ id: number }>(
          "SELECT id FROM accounts WHERE id = ? AND active = 1",
          [data.accountId],
        );
        if (!account) throw new Error("La cuenta de pago no existe o está inactiva.");

        let category = one<{ id: number }>(
          "SELECT id FROM categories WHERE name = 'Compras de inventario' AND kind = 'gasto'",
        );
        if (!category) {
          const created = run(
            "INSERT INTO categories (name, kind) VALUES ('Compras de inventario', 'gasto')",
          );
          category = { id: created.lastInsertRowid };
        }
        const iva = Math.round(subtotal * data.ivaRate);
        const txResult = run(
          `INSERT INTO transactions
            (date, type, category_id, account_id, description, subtotal_cents, iva_cents, total_cents,
             iva_rate, method, reference, source, source_id, created_by)
           VALUES (?, 'gasto', ?, ?, ?, ?, ?, ?, ?, 'Compra', ?, 'inventario', ?, ?)`,
          [
            data.date,
            category.id,
            data.accountId,
            `Compra de inventario: ${product.name}`,
            subtotal,
            iva,
            subtotal + iva,
            data.ivaRate,
            data.reference,
            movementResult.lastInsertRowid,
            auth.userId,
          ],
        );
        run("UPDATE inventory_movements SET transaction_id = ? WHERE id = ?", [
          txResult.lastInsertRowid,
          movementResult.lastInsertRowid,
        ]);
      }

      audit(auth.userId, data.type, "inventory", movementResult.lastInsertRowid, {
        productId: data.productId,
        delta,
      });
    });
  } catch (error) {
    return {
      error: error instanceof Error && error.message ? error.message : "No se pudo registrar el movimiento.",
    };
  }

  revalidateInventory(data.productId);
  revalidatePath("/movimientos");
  redirect(`/inventario/${data.productId}`);
}
