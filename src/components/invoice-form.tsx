"use client";

import { useActionState, useState } from "react";
import Link from "next/link";
import { Info, Plus, Trash2 } from "lucide-react";
import { SubmitButton } from "./action-form";
import { SectionHeader } from "./ui";
import { cn } from "@/lib/cn";
import { centsToInput, formatMoney, formatQty, lineTotals, parseAmount, parseQty } from "@/lib/money";
import type { ActionState } from "@/lib/permissions";

type ServerAction = (state: ActionState, formData: FormData) => Promise<ActionState>;

export type InvoiceFormContact = { id: number; name: string; rfc: string };
export type InvoiceFormProduct = {
  id: number;
  name: string;
  sku: string;
  unit: string;
  price_cents: number;
  iva_rate: number;
  stock: number;
  track_stock: number;
};

type Row = {
  key: string;
  productId: string;
  description: string;
  qty: string;
  price: string;
  ivaRate: string;
};

const IVA_OPTIONS = [
  { value: "0.16", label: "16%" },
  { value: "0.08", label: "8% frontera" },
  { value: "0", label: "0% / exento" },
];

function newRow(defaultIva: number): Row {
  return {
    key: `row-${Math.random().toString(36).slice(2, 10)}`,
    productId: "",
    description: "",
    qty: "1",
    price: "",
    ivaRate: defaultIva > 0 ? String(defaultIva) : "0",
  };
}

export function InvoiceForm({
  action,
  mode,
  contacts,
  products,
  defaultIva,
  today,
  defaultDueDate,
  initial,
}: {
  action: ServerAction;
  mode: "create" | "edit";
  contacts: InvoiceFormContact[];
  products: InvoiceFormProduct[];
  defaultIva: number;
  today: string;
  defaultDueDate: string;
  initial?: {
    id: number;
    contactId: number;
    date: string;
    dueDate: string;
    paymentTerms: string;
    notes: string;
    lines: Array<{
      productId: number | null;
      description: string;
      qty: number;
      unitPriceCents: number;
      ivaRate: number;
    }>;
  };
}) {
  const [state, formAction] = useActionState(action, null);
  const [rows, setRows] = useState<Row[]>(() =>
    initial && initial.lines.length > 0
      ? initial.lines.map((line) => ({
          key: `row-${Math.random().toString(36).slice(2, 10)}`,
          productId: line.productId ? String(line.productId) : "",
          description: line.description,
          qty: String(line.qty),
          price: centsToInput(line.unitPriceCents),
          ivaRate: String(line.ivaRate),
        }))
      : [newRow(defaultIva)],
  );

  const totals = rows.reduce(
    (acc, row) => {
      const qty = parseQty(row.qty) ?? 0;
      const price = parseAmount(row.price) ?? 0;
      const rate = Number(row.ivaRate) || 0;
      if (qty > 0) {
        const line = lineTotals(qty, price, rate);
        acc.subtotal += line.subtotal;
        acc.iva += line.iva;
        acc.total += line.total;
      }
      return acc;
    },
    { subtotal: 0, iva: 0, total: 0 },
  );

  const serialized = rows
    .filter((row) => row.productId || row.description.trim() || row.qty || row.price)
    .map((row) => ({
      productId: row.productId ? Number(row.productId) : null,
      description: row.description.trim(),
      qty: parseQty(row.qty) ?? 0,
      unitPriceCents: parseAmount(row.price) ?? 0,
      ivaRate: Number(row.ivaRate) || 0,
    }));

  function updateRow(key: string, patch: Partial<Row>): void {
    setRows((current) => current.map((row) => (row.key === key ? { ...row, ...patch } : row)));
  }

  function onProductChange(key: string, value: string): void {
    const product = products.find((item) => String(item.id) === value);
    if (!product) {
      updateRow(key, { productId: "" });
      return;
    }
    updateRow(key, {
      productId: value,
      description: product.name,
      price: centsToInput(product.price_cents),
      ivaRate: String(product.iva_rate),
    });
  }

  return (
    <form action={formAction} className="grid items-start gap-5 lg:grid-cols-[minmax(0,1.55fr)_minmax(300px,1fr)]">
      {initial && <input type="hidden" name="id" value={initial.id} />}
      <input type="hidden" name="lines" value={JSON.stringify(serialized)} readOnly />

      <div className="space-y-5">
        {contacts.length === 0 && (
          <p className="form-error" role="alert">
            <span>
              No hay clientes.{" "}
              <Link className="underline" href="/contactos?nuevo=1">
                Registra un cliente
              </Link>{" "}
              antes de facturar.
            </span>
          </p>
        )}

        <section className="card p-5">
          <h2 className="section-title">Datos de la factura</h2>
          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <label className="label" htmlFor="contactId">
                Cliente <span className="font-normal text-danger">*</span>
              </label>
              <select
                className="select"
                id="contactId"
                name="contactId"
                defaultValue={initial?.contactId ? String(initial.contactId) : ""}
                required
              >
                <option value="" disabled>
                  Selecciona un cliente…
                </option>
                {contacts.map((contact) => (
                  <option key={contact.id} value={contact.id}>
                    {contact.name}
                    {contact.rfc ? ` · ${contact.rfc}` : ""}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="label" htmlFor="date">
                Fecha de emisión <span className="font-normal text-danger">*</span>
              </label>
              <input
                className="input"
                id="date"
                name="date"
                type="date"
                defaultValue={initial?.date ?? today}
                required
              />
            </div>
            <div>
              <label className="label" htmlFor="dueDate">
                Vencimiento <span className="font-normal text-danger">*</span>
              </label>
              <input
                className="input"
                id="dueDate"
                name="dueDate"
                type="date"
                defaultValue={initial?.dueDate ?? defaultDueDate}
                required
              />
            </div>
            <div className="sm:col-span-2">
              <label className="label" htmlFor="paymentTerms">
                Condiciones de pago
              </label>
              <input
                className="input"
                id="paymentTerms"
                name="paymentTerms"
                defaultValue={initial?.paymentTerms ?? "Pago a 30 días"}
                maxLength={200}
                placeholder="Ej. Pago a 30 días, transferencia"
              />
            </div>
          </div>
        </section>

        <section className="card">
          <SectionHeader
            title="Conceptos"
            description="Precios sin IVA. El IVA se calcula por concepto."
            action={
              <button
                type="button"
                className="btn btn-secondary btn-sm"
                onClick={() => setRows((current) => [...current, newRow(defaultIva)])}
              >
                <Plus size={15} />
                Agregar
              </button>
            }
          />

          <div className="hidden gap-2 border-b border-line px-5 py-2 text-[10.5px] font-bold uppercase tracking-[0.06em] text-muted sm:grid sm:grid-cols-[minmax(0,1fr)_86px_112px_108px_34px]">
            <span>Concepto</span>
            <span className="text-right">Cantidad</span>
            <span className="text-right">Precio unit.</span>
            <span className="text-right">IVA</span>
            <span />
          </div>

          <div className="space-y-4 p-4 sm:space-y-2 sm:p-5">
            {rows.map((row, index) => {
              const product = products.find((item) => String(item.id) === row.productId);
              const qty = parseQty(row.qty) ?? 0;
              const lowStock = Boolean(product && product.track_stock === 1 && qty > product.stock);
              return (
                <div
                  key={row.key}
                  className="rounded-xl border border-line p-3 sm:border-0 sm:p-0"
                >
                  <div className="grid gap-2 sm:grid-cols-[minmax(0,1fr)_86px_112px_108px_34px] sm:items-start">
                    <div className="space-y-2">
                      <label className="text-[11px] font-semibold text-muted sm:sr-only" htmlFor={`product-${row.key}`}>
                        Producto o servicio
                      </label>
                      <select
                        className="select"
                        id={`product-${row.key}`}
                        value={row.productId}
                        onChange={(event) => onProductChange(row.key, event.target.value)}
                        aria-label={`Producto del concepto ${index + 1}`}
                      >
                        <option value="">Sin producto / servicio libre</option>
                        {products.map((item) => (
                          <option key={item.id} value={item.id}>
                            {item.name}
                            {item.sku ? ` · ${item.sku}` : ""}
                            {item.track_stock === 1 ? ` · stock ${formatQty(item.stock)}` : ""}
                          </option>
                        ))}
                      </select>
                      <input
                        className="input"
                        value={row.description}
                        onChange={(event) => updateRow(row.key, { description: event.target.value })}
                        placeholder="Descripción del concepto"
                        aria-label={`Descripción del concepto ${index + 1}`}
                        maxLength={300}
                      />
                      {product && product.track_stock === 1 && (
                        <p className={cn("text-[11px]", lowStock ? "font-semibold text-danger" : "text-muted")}>
                          Stock disponible: {formatQty(product.stock)} {product.unit}
                          {lowStock ? " · insuficiente para esta cantidad" : ""}
                        </p>
                      )}
                    </div>
                    <div>
                      <label className="mb-1 block text-[11px] font-semibold text-muted sm:hidden" htmlFor={`qty-${row.key}`}>
                        Cantidad
                      </label>
                      <input
                        className="input text-right tabular-nums"
                        id={`qty-${row.key}`}
                        inputMode="decimal"
                        value={row.qty}
                        onChange={(event) => updateRow(row.key, { qty: event.target.value })}
                        aria-label={`Cantidad del concepto ${index + 1}`}
                      />
                    </div>
                    <div>
                      <label className="mb-1 block text-[11px] font-semibold text-muted sm:hidden" htmlFor={`price-${row.key}`}>
                        Precio unitario
                      </label>
                      <input
                        className="input text-right tabular-nums"
                        id={`price-${row.key}`}
                        inputMode="decimal"
                        value={row.price}
                        onChange={(event) => updateRow(row.key, { price: event.target.value })}
                        placeholder="0.00"
                        aria-label={`Precio unitario del concepto ${index + 1}`}
                      />
                    </div>
                    <div>
                      <label className="mb-1 block text-[11px] font-semibold text-muted sm:hidden" htmlFor={`iva-${row.key}`}>
                        IVA
                      </label>
                      <select
                        className="select"
                        id={`iva-${row.key}`}
                        value={row.ivaRate}
                        onChange={(event) => updateRow(row.key, { ivaRate: event.target.value })}
                        aria-label={`IVA del concepto ${index + 1}`}
                      >
                        {IVA_OPTIONS.map((option) => (
                          <option key={option.value} value={option.value}>
                            {option.label}
                          </option>
                        ))}
                      </select>
                    </div>
                    <div className="flex justify-end sm:pt-0.5">
                      <button
                        type="button"
                        className="btn btn-ghost btn-icon btn-sm text-muted hover:text-danger"
                        onClick={() =>
                          setRows((current) =>
                            current.length > 1 ? current.filter((item) => item.key !== row.key) : current,
                          )
                        }
                        disabled={rows.length === 1}
                        aria-label={`Quitar concepto ${index + 1}`}
                        title="Quitar concepto"
                      >
                        <Trash2 size={15} />
                      </button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </section>

        <section className="card p-5">
          <label className="label" htmlFor="notes">
            Notas
          </label>
          <textarea
            className="textarea"
            id="notes"
            name="notes"
            defaultValue={initial?.notes ?? ""}
            maxLength={2000}
            placeholder="Notas visibles en la factura (opcional)."
          />
        </section>
      </div>

      <aside className="space-y-5 lg:sticky lg:top-[76px]">
        <section className="card p-5">
          <h2 className="section-title">Resumen</h2>
          <dl className="mt-4 space-y-2.5 text-sm">
            <div className="flex items-center justify-between gap-3">
              <dt className="text-muted">Subtotal</dt>
              <dd className="tabular-nums">{formatMoney(totals.subtotal)}</dd>
            </div>
            <div className="flex items-center justify-between gap-3">
              <dt className="text-muted">IVA</dt>
              <dd className="tabular-nums">{formatMoney(totals.iva)}</dd>
            </div>
            <div className="flex items-center justify-between gap-3 border-t border-line pt-3 text-base font-bold">
              <dt>Total</dt>
              <dd className="tabular-nums">{formatMoney(totals.total)}</dd>
            </div>
          </dl>

          <div className="mt-4 flex items-start gap-2 rounded-lg bg-elevated p-2.5 text-[11px] leading-relaxed text-muted">
            <Info size={14} className="mt-0.5 flex-none" />
            <span>
              {mode === "create"
                ? "Al emitir se asigna folio y se descuenta inventario. Como borrador no afecta reportes."
                : "Solo los borradores se pueden editar. Al emitir se asigna folio y se descuenta inventario."}
            </span>
          </div>

          {state?.error && (
            <p className="form-error mt-4" role="alert">
              {state.error}
            </p>
          )}

          <div className="mt-5 space-y-2">
            <SubmitButton
              name="intent"
              value="emitir"
              label={mode === "create" ? "Emitir factura" : "Guardar y emitir"}
              pendingLabel="Emitiendo…"
              className="w-full"
            />
            <SubmitButton
              name="intent"
              value={mode === "create" ? "borrador" : "guardar"}
              label={mode === "create" ? "Guardar borrador" : "Guardar cambios"}
              pendingLabel="Guardando…"
              variant="secondary"
              className="w-full"
            />
            <Link className="btn btn-ghost w-full" href={initial ? `/facturas/${initial.id}` : "/facturas"}>
              Cancelar
            </Link>
          </div>
          <p className="mt-3 text-center text-[11px] text-muted">
            Documento interno. No genera CFDI.
          </p>
        </section>
      </aside>
    </form>
  );
}
