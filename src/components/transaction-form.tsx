"use client";

import Link from "next/link";
import { useActionState, useState } from "react";
import { ArrowDownLeft, ArrowUpRight, Info } from "lucide-react";
import { SubmitButton } from "./action-form";
import { cn } from "@/lib/cn";
import { formatMoney, parseAmount } from "@/lib/money";
import type { ActionState } from "@/lib/permissions";

type ServerAction = (state: ActionState, formData: FormData) => Promise<ActionState>;

type Option = { id: number; name: string };

const IVA_OPTIONS = [
  { value: "0.16", label: "16%" },
  { value: "0.08", label: "8% frontera" },
  { value: "0", label: "0% / exento" },
];

const METHODS = ["Transferencia", "Efectivo", "Tarjeta", "Cheque", "Domiciliación", "Otro"];

export function TransactionForm({
  action,
  mode,
  categories,
  contacts,
  accounts,
  today,
  defaultIva,
  initial,
}: {
  action: ServerAction;
  mode: "create" | "edit";
  categories: Array<{ id: number; name: string; kind: string }>;
  contacts: Option[];
  accounts: Option[];
  today: string;
  defaultIva: number;
  initial?: {
    id: number;
    date: string;
    type: "ingreso" | "gasto";
    categoryId: number | null;
    contactId: number | null;
    accountId: number | null;
    description: string;
    subtotalCents: number;
    ivaRate: number;
    method: string;
    reference: string;
  };
}) {
  const [state, formAction] = useActionState(action, null);
  const [type, setType] = useState<"ingreso" | "gasto">(initial?.type ?? "gasto");
  const [categoryId, setCategoryId] = useState<string>(
    initial?.categoryId ? String(initial.categoryId) : "",
  );
  const [subtotal, setSubtotal] = useState(
    initial ? (initial.subtotalCents / 100).toFixed(2) : "",
  );
  const [ivaRate, setIvaRate] = useState(String(initial?.ivaRate ?? defaultIva));
  const [accountId, setAccountId] = useState(
    initial?.accountId ? String(initial.accountId) : accounts[0] ? String(accounts[0].id) : "",
  );

  const filteredCategories = categories.filter((category) =>
    type === "ingreso" ? category.kind === "ingreso" : category.kind !== "ingreso",
  );

  const subtotalCents = parseAmount(subtotal) ?? 0;
  const ivaCents = Math.round(subtotalCents * (Number(ivaRate) || 0));
  const totalCents = subtotalCents + ivaCents;

  function changeType(next: "ingreso" | "gasto") {
    setType(next);
    const current = categories.find((category) => String(category.id) === categoryId);
    if (current && (next === "ingreso" ? current.kind !== "ingreso" : current.kind === "ingreso")) {
      setCategoryId("");
    }
  }

  return (
    <form action={formAction} className="grid items-start gap-5 lg:grid-cols-[minmax(0,1.5fr)_minmax(300px,1fr)]">
      {initial && <input type="hidden" name="id" value={initial.id} />}
      <input type="hidden" name="type" value={type} />
      <input type="hidden" name="categoryId" value={categoryId} />
      <input type="hidden" name="accountId" value={accountId} />

      <div className="space-y-5">
        <section className="card p-5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h2 className="section-title">Tipo de movimiento</h2>
            <div className="segmented">
              <button
                type="button"
                data-active={type === "ingreso"}
                aria-pressed={type === "ingreso"}
                onClick={() => changeType("ingreso")}
              >
                <ArrowDownLeft size={14} className="text-success" />
                Ingreso
              </button>
              <button
                type="button"
                data-active={type === "gasto"}
                aria-pressed={type === "gasto"}
                onClick={() => changeType("gasto")}
              >
                <ArrowUpRight size={14} className="text-expense" />
                Gasto
              </button>
            </div>
          </div>

          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            <div>
              <label className="label" htmlFor="date">
                Fecha <span className="font-normal text-danger">*</span>
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
              <label className="label" htmlFor="categoryIdField">
                Categoría <span className="font-normal text-danger">*</span>
              </label>
              <select
                className="select"
                id="categoryIdField"
                value={categoryId}
                onChange={(event) => setCategoryId(event.target.value)}
                required
              >
                <option value="" disabled>
                  Selecciona una categoría…
                </option>
                {filteredCategories.map((category) => (
                  <option key={category.id} value={category.id}>
                    {category.name}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="label" htmlFor="description">
                Descripción <span className="font-normal text-danger">*</span>
              </label>
              <input
                className="input"
                id="description"
                name="description"
                defaultValue={initial?.description ?? ""}
                placeholder={
                  type === "ingreso" ? "Ej. Venta de mostrador" : "Ej. Renta de julio"
                }
                maxLength={300}
                required
              />
            </div>
            <div>
              <label className="label" htmlFor="contactId">
                Contacto
              </label>
              <select
                className="select"
                id="contactId"
                name="contactId"
                defaultValue={initial?.contactId ? String(initial.contactId) : ""}
              >
                <option value="">Sin contacto</option>
                {contacts.map((contact) => (
                  <option key={contact.id} value={contact.id}>
                    {contact.name}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="label" htmlFor="accountField">
                Cuenta <span className="font-normal text-danger">*</span>
              </label>
              <select
                className="select"
                id="accountField"
                value={accountId}
                onChange={(event) => setAccountId(event.target.value)}
                required
              >
                {accounts.length === 0 && <option value="">Sin cuentas</option>}
                {accounts.map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.name}
                  </option>
                ))}
              </select>
              <p className="hint">De aquí sale o entra el dinero.</p>
            </div>
            <div>
              <label className="label" htmlFor="method">
                Método
              </label>
              <select className="select" id="method" name="method" defaultValue={initial?.method ?? "Transferencia"}>
                {METHODS.map((method) => (
                  <option key={method}>{method}</option>
                ))}
              </select>
            </div>
            <div className="sm:col-span-2">
              <label className="label" htmlFor="reference">
                Referencia
              </label>
              <input
                className="input"
                id="reference"
                name="reference"
                defaultValue={initial?.reference ?? ""}
                placeholder="Folio, factura o clave (opcional)"
                maxLength={120}
              />
            </div>
          </div>
        </section>

        <div className="rounded-xl border border-line bg-elevated p-4 text-xs leading-relaxed text-muted">
          <p className="flex items-center gap-2 font-semibold text-ink">
            <Info size={14} />
            ¿Factura o movimiento?
          </p>
          <p className="mt-1.5">
            Las ventas que emites con factura se capturan en <strong>Facturas</strong>. Usa este
            formulario para gastos y para ingresos que no facturas. Así evitas duplicar ingresos.
          </p>
        </div>
      </div>

      <aside className="space-y-5 lg:sticky lg:top-[76px]">
        <section className="card p-5">
          <h2 className="section-title">Importe</h2>
          <div className="mt-4 space-y-4">
            <div>
              <label className="label" htmlFor="subtotal">
                Subtotal (sin IVA) <span className="font-normal text-danger">*</span>
              </label>
              <input
                className="input text-right text-[17px] font-semibold tabular-nums"
                id="subtotal"
                name="subtotal"
                inputMode="decimal"
                value={subtotal}
                onChange={(event) => setSubtotal(event.target.value)}
                placeholder="0.00"
                required
              />
            </div>
            <div>
              <label className="label" htmlFor="ivaRate">
                IVA
              </label>
              <select
                className="select"
                id="ivaRate"
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
          </div>

          <dl className="mt-4 space-y-2 border-t border-line pt-3 text-sm">
            <div className="flex justify-between gap-3">
              <dt className="text-muted">IVA</dt>
              <dd className="tabular-nums">{formatMoney(ivaCents)}</dd>
            </div>
            <div className="flex justify-between gap-3 text-base font-bold">
              <dt>Total</dt>
              <dd className="tabular-nums">{formatMoney(totalCents)}</dd>
            </div>
          </dl>

          {state?.error && (
            <p className="form-error mt-4" role="alert">
              {state.error}
            </p>
          )}

          <div className="mt-5 space-y-2">
            <SubmitButton
              label={mode === "create" ? "Guardar movimiento" : "Guardar cambios"}
              pendingLabel="Guardando…"
              className="w-full"
              disabled={accounts.length === 0}
            />
            <Link className="btn btn-ghost w-full" href="/movimientos">
              Cancelar
            </Link>
          </div>
          {accounts.length === 0 && (
            <p className="mt-2 text-center text-[11px] text-danger">
              Crea una cuenta de efectivo en Configuración.
            </p>
          )}
        </section>
      </aside>
    </form>
  );
}
