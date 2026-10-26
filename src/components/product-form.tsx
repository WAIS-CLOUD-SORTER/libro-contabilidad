import type { Product } from "@/lib/types";
import { saveProductAction } from "@/app/actions/products";
import { ActionForm } from "./action-form";
import { Field } from "./ui";

export function ProductForm({ product }: { product?: Product }) {
  return (
    <section className="card p-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="section-title">{product ? "Editar producto" : "Nuevo producto"}</h2>
          <p className="mt-0.5 text-xs text-muted">
            {product
              ? "El stock no se edita aquí: usa un movimiento de inventario."
              : "El stock inicial se registra como saldo de apertura."}
          </p>
        </div>
      </div>

      <ActionForm
        action={saveProductAction}
        submitLabel={product ? "Guardar cambios" : "Crear producto"}
        className="mt-4"
      >
        {product && <input type="hidden" name="id" value={product.id} />}
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <Field label="Nombre" htmlFor="name" required className="sm:col-span-2">
            <input
              className="input"
              id="name"
              name="name"
              defaultValue={product?.name ?? ""}
              maxLength={160}
              required
            />
          </Field>
          <Field label="SKU" htmlFor="sku">
            <input
              className="input"
              id="sku"
              name="sku"
              defaultValue={product?.sku ?? ""}
              maxLength={80}
              placeholder="Opcional"
            />
          </Field>
          <Field label="Unidad" htmlFor="unit" hint="pieza, kg, hora, servicio…" required>
            <input
              className="input"
              id="unit"
              name="unit"
              defaultValue={product?.unit ?? "pieza"}
              maxLength={40}
              required
            />
          </Field>
          <Field label="Precio de venta" htmlFor="price" hint="Sin IVA.">
            <input
              className="input text-right tabular-nums"
              id="price"
              name="price"
              inputMode="decimal"
              defaultValue={product ? (product.price_cents / 100).toFixed(2) : ""}
              placeholder="0.00"
            />
          </Field>
          <Field label="Costo unitario" htmlFor="cost" hint="Sin IVA. Base para el costo promedio.">
            <input
              className="input text-right tabular-nums"
              id="cost"
              name="cost"
              inputMode="decimal"
              defaultValue={product ? (product.cost_cents / 100).toFixed(2) : ""}
              placeholder="0.00"
            />
          </Field>
          <Field label="IVA" htmlFor="ivaRate">
            <select className="select" id="ivaRate" name="ivaRate" defaultValue={String(product?.iva_rate ?? 0.16)}>
              <option value="0.16">16%</option>
              <option value="0.08">8% frontera</option>
              <option value="0">0% / exento</option>
            </select>
          </Field>
          <Field label="Stock mínimo" htmlFor="minStock" hint="Alerta de reposición.">
            <input
              className="input text-right tabular-nums"
              id="minStock"
              name="minStock"
              inputMode="decimal"
              defaultValue={product ? String(product.min_stock) : "0"}
            />
          </Field>
          {!product && (
            <Field label="Stock inicial" htmlFor="initialStock">
              <input
                className="input text-right tabular-nums"
                id="initialStock"
                name="initialStock"
                inputMode="decimal"
                defaultValue="0"
              />
            </Field>
          )}
          <Field label="Descripción" htmlFor="description" className="sm:col-span-2 lg:col-span-3">
            <textarea
              className="textarea"
              id="description"
              name="description"
              defaultValue={product?.description ?? ""}
              maxLength={1000}
              rows={2}
            />
          </Field>
          <div className="sm:col-span-2 lg:col-span-3">
            <label className="flex cursor-pointer items-center gap-2 text-sm font-medium text-ink">
              <input
                type="checkbox"
                name="trackStock"
                value="1"
                defaultChecked={product ? product.track_stock === 1 : true}
                className="h-4 w-4 accent-[var(--accent)]"
              />
              Controlar inventario y descontar stock al facturar
            </label>
            {product && (
              <p className="mt-1 text-xs text-muted">
                Stock actual: {product.stock} {product.unit}
              </p>
            )}
          </div>
        </div>
      </ActionForm>
    </section>
  );
}
