import Link from "next/link";
import { AlertTriangle, Boxes, Download, Package, Plus, Warehouse } from "lucide-react";
import { toggleProductAction } from "@/app/actions/products";
import { ActionForm } from "@/components/action-form";
import { ProductForm } from "@/components/product-form";
import {
  Badge,
  EmptyState,
  Money,
  PageHeader,
  Stat,
} from "@/components/ui";
import { requireUser } from "@/lib/auth";
import { getProduct, getProducts } from "@/lib/data";
import { formatMoney, formatQty } from "@/lib/money";
import { can } from "@/lib/permissions";

export const dynamic = "force-dynamic";

export const metadata = { title: "Inventario" };

export default async function InventoryPage({
  searchParams,
}: {
  searchParams: Promise<{ q?: string; bajo?: string; nuevo?: string; editar?: string; inactivos?: string }>;
}) {
  const user = await requireUser();
  const params = await searchParams;
  const canWrite = can(user.role, "inventory.write");
  const editing = Number(params.editar) > 0 ? getProduct(Number(params.editar)) : undefined;
  const showForm = canWrite && (params.nuevo === "1" || Boolean(editing));
  const includeInactive = params.inactivos === "1";
  const onlyLow = params.bajo === "1";
  const q = (params.q ?? "").trim().toLowerCase();

  const all = getProducts(true);
  const activeProducts = all.filter((product) => product.active === 1);
  const lowStock = activeProducts.filter(
    (product) => product.track_stock === 1 && product.stock <= product.min_stock,
  );
  const outOfStock = activeProducts.filter(
    (product) => product.track_stock === 1 && product.stock <= 0,
  );
  const inventoryValue = activeProducts.reduce(
    (sum, product) => sum + (product.track_stock === 1 ? Math.round(product.stock * product.cost_cents) : 0),
    0,
  );

  const visible = all
    .filter((product) => includeInactive || product.active === 1)
    .filter((product) => (onlyLow ? product.track_stock === 1 && product.stock <= product.min_stock : true))
    .filter((product) => {
      if (!q) return true;
      return product.name.toLowerCase().includes(q) || product.sku.toLowerCase().includes(q);
    });

  return (
    <div>
      <PageHeader
        title="Inventario"
        description="Productos, existencias y costo promedio. El stock se mueve con entradas, salidas y ajustes."
        actions={
          <>
            <Link className="btn btn-secondary" href="/api/exportar?tipo=inventario">
              <Download size={15} />
              CSV
            </Link>
            {canWrite && !showForm && (
              <Link href="/inventario?nuevo=1" className="btn btn-primary">
                <Plus size={16} />
                Nuevo producto
              </Link>
            )}
          </>
        }
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat
          label="Productos activos"
          value={String(activeProducts.length)}
          icon={<Boxes size={17} />}
        />
        <Stat
          label="Valor del inventario"
          value={formatMoney(inventoryValue)}
          hint="Stock × costo promedio"
          icon={<Warehouse size={17} />}
          tone="accent"
        />
        <Stat
          label="Bajo stock"
          value={String(lowStock.length)}
          hint="En o bajo el mínimo"
          icon={<AlertTriangle size={17} />}
          tone={lowStock.length > 0 ? "warn" : "default"}
        />
        <Stat
          label="Agotados"
          value={String(outOfStock.length)}
          hint="Sin existencias"
          icon={<Package size={17} />}
          tone={outOfStock.length > 0 ? "danger" : "default"}
        />
      </div>

      {showForm && (
        <div className="mt-5">
          <ProductForm product={editing} />
        </div>
      )}

      <div className="mt-5 flex flex-wrap items-center justify-between gap-3">
        <form className="flex w-full flex-wrap gap-2 sm:w-auto" action="/inventario">
          <input
            className="input sm:w-64"
            name="q"
            defaultValue={params.q ?? ""}
            placeholder="Buscar producto o SKU…"
            aria-label="Buscar productos"
          />
          {includeInactive && <input type="hidden" name="inactivos" value="1" />}
          <label className="flex items-center gap-2 rounded-lg border border-line bg-surface px-3 text-xs font-medium text-muted">
            <input
              type="checkbox"
              name="bajo"
              value="1"
              defaultChecked={onlyLow}
              className="h-3.5 w-3.5 accent-[var(--accent)]"
            />
            Solo bajo stock
          </label>
          <button type="submit" className="btn btn-secondary">
            Filtrar
          </button>
        </form>
        <div className="flex items-center gap-3 text-xs font-semibold">
          {onlyLow && (
            <Link href="/inventario" className="text-accent hover:underline">
              Quitar filtro
            </Link>
          )}
          <Link
            href={includeInactive ? "/inventario" : "/inventario?inactivos=1"}
            className="text-accent hover:underline"
          >
            {includeInactive ? "Ocultar inactivos" : "Ver inactivos"}
          </Link>
        </div>
      </div>

      <div className="card mt-4 overflow-hidden">
        {visible.length === 0 ? (
          <div className="p-5">
            <EmptyState
              icon={<Package size={18} />}
              title="Sin productos"
              description="Agrega tus productos o servicios para controlar stock, costos y facturación."
              action={
                canWrite ? (
                  <Link href="/inventario?nuevo=1" className="btn btn-primary btn-sm">
                    Nuevo producto
                  </Link>
                ) : undefined
              }
            />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="table">
              <thead>
                <tr>
                  <th>Producto</th>
                  <th>SKU</th>
                  <th className="num">Stock</th>
                  <th className="num">Mínimo</th>
                  <th className="num">Costo</th>
                  <th className="num">Precio</th>
                  <th className="num">Valor</th>
                  <th>Estado</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {visible.map((product) => {
                  const low = product.track_stock === 1 && product.stock <= product.min_stock;
                  return (
                    <tr key={product.id}>
                      <td>
                        <Link
                          href={`/inventario/${product.id}`}
                          className="font-medium text-ink hover:text-accent"
                        >
                          {product.name}
                        </Link>
                        <p className="mt-0.5 text-[11px] text-muted">{product.unit}</p>
                      </td>
                      <td className="whitespace-nowrap text-muted">{product.sku || "—"}</td>
                      <td className="num font-semibold">
                        {product.track_stock === 1
                          ? `${formatQty(product.stock)} ${product.unit}`
                          : "—"}
                      </td>
                      <td className="num text-muted">{formatQty(product.min_stock)}</td>
                      <td className="num">
                        <Money cents={product.cost_cents} />
                      </td>
                      <td className="num">
                        <Money cents={product.price_cents} />
                      </td>
                      <td className="num text-muted">
                        {product.track_stock === 1
                          ? formatMoney(Math.round(product.stock * product.cost_cents))
                          : "—"}
                      </td>
                      <td>
                        {product.active === 0 ? (
                          <Badge tone="neutral">Inactivo</Badge>
                        ) : low ? (
                          <Badge tone={product.stock <= 0 ? "danger" : "warn"}>
                            {product.stock <= 0 ? "Agotado" : "Bajo stock"}
                          </Badge>
                        ) : product.track_stock === 0 ? (
                          <Badge tone="neutral">Sin stock</Badge>
                        ) : (
                          <Badge tone="success">Disponible</Badge>
                        )}
                      </td>
                      <td className="actions">
                        <div className="flex items-center justify-end gap-1">
                          <Link href={`/inventario/${product.id}`} className="btn btn-secondary btn-sm">
                            Ver
                          </Link>
                          {canWrite && (
                            <Link
                              href={`/inventario?editar=${product.id}`}
                              className="btn btn-ghost btn-sm"
                            >
                              Editar
                            </Link>
                          )}
                          {canWrite && (
                            <ActionForm
                              action={toggleProductAction}
                              submitLabel={product.active === 1 ? "Desactivar" : "Reactivar"}
                              variant="ghost"
                              small
                              confirm={
                                product.active === 1
                                  ? "¿Desactivar este producto? No aparecerá en facturas nuevas."
                                  : "¿Reactivar este producto?"
                              }
                              submitClassName="mt-0 justify-end"
                            >
                              <input type="hidden" name="id" value={product.id} />
                              <input type="hidden" name="active" value={product.active === 1 ? "0" : "1"} />
                            </ActionForm>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
