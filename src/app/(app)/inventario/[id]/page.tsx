import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft, Package, Pencil } from "lucide-react";
import { addInventoryMovementAction } from "@/app/actions/products";
import { InventoryMovementForm } from "@/components/inventory-movement-form";
import { Badge, Money, PageHeader, SectionHeader, Stat } from "@/components/ui";
import { requireUser } from "@/lib/auth";
import { getAccounts, getInventoryMovements, getProduct } from "@/lib/data";
import { formatDate, todayISO } from "@/lib/dates";
import { formatMoney, formatQty, formatPercent } from "@/lib/money";
import { can } from "@/lib/permissions";

export const dynamic = "force-dynamic";

export const metadata = { title: "Producto" };

const TYPE_LABELS: Record<string, { label: string; tone: "success" | "danger" | "info" | "neutral" }> = {
  entrada: { label: "Entrada", tone: "success" },
  salida: { label: "Salida", tone: "danger" },
  ajuste: { label: "Ajuste", tone: "info" },
};

export default async function ProductDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const user = await requireUser();
  const { id } = await params;
  const productId = Number(id);
  if (!Number.isInteger(productId) || productId <= 0) notFound();

  const product = getProduct(productId);
  if (!product) notFound();

  const movements = getInventoryMovements(productId);
  const accounts = getAccounts().map((account) => ({ id: account.id, name: account.name }));
  const canWrite = can(user.role, "inventory.write");
  const value = product.track_stock === 1 ? Math.round(product.stock * product.cost_cents) : 0;
  const margin =
    product.price_cents > 0
      ? (product.price_cents - product.cost_cents) / product.price_cents
      : 0;

  return (
    <div>
      <PageHeader
        eyebrow="Inventario"
        title={product.name}
        description={`${product.sku ? `${product.sku} · ` : ""}${product.unit}`}
        actions={
          <>
            <Link href="/inventario" className="btn btn-secondary">
              <ArrowLeft size={15} />
              Volver
            </Link>
            {canWrite && (
              <Link href={`/inventario?editar=${product.id}`} className="btn btn-secondary">
                <Pencil size={15} />
                Editar
              </Link>
            )}
          </>
        }
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat
          label="Stock actual"
          value={`${formatQty(product.stock)} ${product.unit}`}
          hint={`Mínimo ${formatQty(product.min_stock)}`}
          icon={<Package size={17} />}
          tone={
            product.track_stock === 1 && product.stock <= product.min_stock ? "warn" : "default"
          }
        />
        <Stat label="Costo promedio" value={formatMoney(product.cost_cents)} hint="Sin IVA" />
        <Stat label="Valor en inventario" value={formatMoney(value)} tone="accent" />
        <Stat
          label="Margen"
          value={formatPercent(margin)}
          hint={`Precio ${formatMoney(product.price_cents)}`}
        />
      </div>

      <div className="mt-5 grid items-start gap-5 lg:grid-cols-[minmax(0,1.5fr)_minmax(320px,1fr)]">
        <div className="space-y-5">
          <section className="card p-5">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <h2 className="section-title">Ficha del producto</h2>
              <div className="flex items-center gap-1.5">
                {product.active === 1 ? (
                  <Badge tone="success">Activo</Badge>
                ) : (
                  <Badge tone="neutral">Inactivo</Badge>
                )}
                {product.track_stock === 1 ? (
                  <Badge tone="info">Controla stock</Badge>
                ) : (
                  <Badge tone="neutral">Sin stock</Badge>
                )}
              </div>
            </div>
            <dl className="mt-4 grid gap-4 border-t border-line pt-4 text-sm sm:grid-cols-2">
              <div>
                <dt className="text-[11px] font-semibold text-muted">SKU</dt>
                <dd className="mt-0.5">{product.sku || "—"}</dd>
              </div>
              <div>
                <dt className="text-[11px] font-semibold text-muted">Unidad</dt>
                <dd className="mt-0.5">{product.unit}</dd>
              </div>
              <div>
                <dt className="text-[11px] font-semibold text-muted">Precio de venta</dt>
                <dd className="mt-0.5">
                  <Money cents={product.price_cents} /> · IVA {formatPercent(product.iva_rate)}
                </dd>
              </div>
              <div>
                <dt className="text-[11px] font-semibold text-muted">Costo</dt>
                <dd className="mt-0.5">
                  <Money cents={product.cost_cents} />
                </dd>
              </div>
              {product.description && (
                <div className="sm:col-span-2">
                  <dt className="text-[11px] font-semibold text-muted">Descripción</dt>
                  <dd className="mt-0.5 whitespace-pre-wrap leading-relaxed text-muted">
                    {product.description}
                  </dd>
                </div>
              )}
            </dl>
          </section>

          <section className="card overflow-hidden">
            <SectionHeader
              title="Movimientos de inventario"
              description={`${movements.length} registro(s)`}
            />
            {movements.length === 0 ? (
              <div className="p-5 text-sm text-muted">
                Aún no hay movimientos. Registra una entrada para comenzar.
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Fecha</th>
                      <th>Tipo</th>
                      <th className="num">Cantidad</th>
                      <th className="num">Costo unit.</th>
                      <th>Motivo</th>
                      <th>Cuenta</th>
                      <th>Registró</th>
                    </tr>
                  </thead>
                  <tbody>
                    {movements.map((movement) => {
                      const meta = TYPE_LABELS[movement.type] ?? TYPE_LABELS.ajuste;
                      return (
                        <tr key={movement.id}>
                          <td className="whitespace-nowrap text-muted">{formatDate(movement.date)}</td>
                          <td>
                            <Badge tone={meta.tone}>{meta.label}</Badge>
                          </td>
                          <td
                            className={
                              movement.qty > 0
                                ? "num font-semibold text-success"
                                : "num font-semibold text-danger"
                            }
                          >
                            {movement.qty > 0 ? "+" : ""}
                            {formatQty(movement.qty)}
                          </td>
                          <td className="num text-muted">
                            <Money cents={movement.unit_cost_cents} />
                          </td>
                          <td className="max-w-[220px]">
                            <p className="truncate">{movement.reason || "—"}</p>
                            {movement.reference && (
                              <p className="truncate text-[11px] text-muted">{movement.reference}</p>
                            )}
                          </td>
                          <td className="text-muted">{movement.account_name || "—"}</td>
                          <td className="text-muted">{movement.user_name || "—"}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </div>

        <aside className="space-y-5 lg:sticky lg:top-[76px]">
          {product.track_stock === 1 && canWrite ? (
            <InventoryMovementForm
              action={addInventoryMovementAction}
              product={{
                id: product.id,
                name: product.name,
                stock: product.stock,
                costCents: product.cost_cents,
                ivaRate: product.iva_rate,
                unit: product.unit,
              }}
              accounts={accounts}
              today={todayISO()}
            />
          ) : (
            <section className="card p-5">
              <h2 className="section-title">Sin control de stock</h2>
              <p className="mt-1 text-xs leading-relaxed text-muted">
                Este producto o servicio no descuenta inventario. Actívalo desde Editar si quieres
                controlar existencias.
              </p>
            </section>
          )}

          <section className="rounded-xl border border-line bg-elevated p-4 text-[11px] leading-relaxed text-muted">
            <p className="font-semibold text-ink">Cómo se calcula el costo</p>
            <p className="mt-1">
              Las entradas recalculan el costo promedio ponderado. Las salidas y ajustes no cambian
              el costo. Si registras la compra con una cuenta, también se crea el gasto y el IVA
              acreditable.
            </p>
          </section>
        </aside>
      </div>
    </div>
  );
}
