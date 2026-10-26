"use client";

import { useActionState, useState } from "react";
import { ArrowDownToLine, ArrowUpFromLine, SlidersHorizontal } from "lucide-react";
import { SubmitButton } from "./action-form";
import { cn } from "@/lib/cn";
import { formatMoney, formatQty, parseQty } from "@/lib/money";
import type { ActionState } from "@/lib/permissions";

type ServerAction = (state: ActionState, formData: FormData) => Promise<ActionState>;

const IVA_OPTIONS = [
  { value: "0.16", label: "16%" },
  { value: "0.08", label: "8% frontera" },
  { value: "0", label: "0% / exento" },
];

export function InventoryMovementForm({
  action,
  product,
  accounts,
  today,
}: {
  action: ServerAction;
  product: { id: number; name: string; stock: number; costCents: number; ivaRate: number; unit: string };
  accounts: Array<{ id: number; name: string }>;
  today: string;
}) {
  const [state, formAction] = useActionState(action, null);
  const [type, setType] = useState<"entrada" | "salida" | "ajuste">("entrada");
  const [qty, setQty] = useState("");
  const [unitCost, setUnitCost] = useState((product.costCents / 100).toFixed(2));
  const [ivaRate, setIvaRate] = useState(String(product.ivaRate));
  const [accountId, setAccountId] = useState("");

  const qtyNumber = parseQty(qty) ?? 0;
  const delta = type === "entrada" ? qtyNumber : type === "salida" ? -qtyNumber : qtyNumber - product.stock;
  const resultStock = type === "ajuste" ? qtyNumber : product.stock + delta;
  const negative = resultStock < 0;
  const purchase = type === "entrada" && accountId !== "";

  function changeType(next: "entrada" | "salida" | "ajuste") {
    setType(next);
    if (next === "ajuste") setQty(String(product.stock));
    else setQty("");
  }

  return (
    <section className="card p-5">
      <h2 className="section-title">Registrar movimiento</h2>
      <div className="segmented mt-3">
        <button
          type="button"
          data-active={type === "entrada"}
          aria-pressed={type === "entrada"}
          onClick={() => changeType("entrada")}
        >
          <ArrowDownToLine size={14} className="text-success" />
          Entrada
        </button>
        <button
          type="button"
          data-active={type === "salida"}
          aria-pressed={type === "salida"}
          onClick={() => changeType("salida")}
        >
          <ArrowUpFromLine size={14} className="text-expense" />
          Salida
        </button>
        <button
          type="button"
          data-active={type === "ajuste"}
          aria-pressed={type === "ajuste"}
          onClick={() => changeType("ajuste")}
        >
          <SlidersHorizontal size={14} />
          Ajuste
        </button>
      </div>

      <form action={formAction} className="mt-4 space-y-3">
        <input type="hidden" name="productId" value={product.id} />
        <input type="hidden" name="type" value={type} />
        <input type="hidden" name="accountId" value={accountId} />

        <div className="grid gap-3 sm:grid-cols-2">
          <div>
            <label className="label" htmlFor="movementDate">
              Fecha
            </label>
            <input
              className="input"
              id="movementDate"
              name="date"
              type="date"
              defaultValue={today}
              required
            />
          </div>
          <div>
            <label className="label" htmlFor="movementQty">
              {type === "ajuste" ? "Stock real contado" : "Cantidad"}
            </label>
            <input
              className="input text-right tabular-nums"
              id="movementQty"
              name="qty"
              inputMode="decimal"
              value={qty}
              onChange={(event) => setQty(event.target.value)}
              placeholder={type === "ajuste" ? String(product.stock) : "0"}
              required
            />
            <p className={cn("hint", negative && "font-semibold text-danger")}>
              Stock resultante: {formatQty(negative ? resultStock : resultStock)} {product.unit}
              {type === "ajuste" && qty ? ` (ajuste ${delta > 0 ? "+" : ""}${formatQty(delta)})` : ""}
            </p>
          </div>
        </div>

        {type === "entrada" && (
          <>
            <div className="grid gap-3 sm:grid-cols-2">
              <div>
                <label className="label" htmlFor="unitCost">
                  Costo unitario
                </label>
                <input
                  className="input text-right tabular-nums"
                  id="unitCost"
                  name="unitCost"
                  inputMode="decimal"
                  value={unitCost}
                  onChange={(event) => setUnitCost(event.target.value)}
                />
                <p className="hint">Sin IVA. Recalcula el costo promedio.</p>
              </div>
              <div>
                <label className="label" htmlFor="movementAccount">
                  ¿Pagaste con una cuenta?
                </label>
                <select
                  className="select"
                  id="movementAccount"
                  value={accountId}
                  onChange={(event) => setAccountId(event.target.value)}
                >
                  <option value="">No registrar salida de efectivo</option>
                  {accounts.map((account) => (
                    <option key={account.id} value={account.id}>
                      {account.name}
                    </option>
                  ))}
                </select>
              </div>
            </div>
            {purchase && (
              <div className="grid gap-3 sm:grid-cols-2">
                <div>
                  <label className="label" htmlFor="movementIva">
                    IVA de la compra
                  </label>
                  <select
                    className="select"
                    id="movementIva"
                    name="ivaRate"
                    value={ivaRate}
                    onChange={(event) => setIvaRate(event.target.value)}
                  >
                    {IVA_OPTIONS.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="rounded-lg bg-elevated p-2.5 text-[11px] leading-relaxed text-muted sm:mt-7">
                  Se creará un gasto de compra por{" "}
                  <strong className="text-ink">
                    {formatMoney(
                      Math.round(
                        qtyNumber * ((parseQty(unitCost) ?? 0) * 100) * (1 + Number(ivaRate)),
                      ),
                    )}
                  </strong>{" "}
                  con IVA. Así el flujo de caja y el IVA acreditable quedan completos.
                </div>
              </div>
            )}
          </>
        )}

        <div>
          <label className="label" htmlFor="movementReason">
            Motivo
          </label>
          <input
            className="input"
            id="movementReason"
            name="reason"
            placeholder={
              type === "entrada" ? "Compra a proveedor" : type === "salida" ? "Merma o uso interno" : "Conteo físico"
            }
            maxLength={300}
          />
        </div>
        <div>
          <label className="label" htmlFor="movementReference">
            Referencia
          </label>
          <input
            className="input"
            id="movementReference"
            name="reference"
            placeholder="Factura del proveedor, nota, etc."
            maxLength={120}
          />
        </div>

        {state?.error && (
          <p className="form-error" role="alert">
            {state.error}
          </p>
        )}

        <SubmitButton
          label={
            type === "entrada"
              ? "Registrar entrada"
              : type === "salida"
                ? "Registrar salida"
                : "Ajustar stock"
          }
          pendingLabel="Guardando…"
          className="w-full"
          disabled={negative || !qty}
        />
      </form>
    </section>
  );
}
