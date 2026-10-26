const MXN = new Intl.NumberFormat("es-MX", {
  style: "currency",
  currency: "MXN",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

const MXN_COMPACT = new Intl.NumberFormat("es-MX", {
  style: "currency",
  currency: "MXN",
  notation: "compact",
  maximumFractionDigits: 1,
});

export function formatMoney(cents: number): string {
  return MXN.format((Number.isFinite(cents) ? cents : 0) / 100);
}

export function formatMoneyCompact(cents: number): string {
  return MXN_COMPACT.format((Number.isFinite(cents) ? cents : 0) / 100);
}

export function centsToInput(cents: number): string {
  return (Math.round(cents) / 100).toFixed(2);
}

/**
 * Acepta "1,234.56", "1.234,56", "$ 1234", "1234.5".
 * Regresa centavos o null si no es un número válido.
 */
export function parseAmount(raw: FormDataEntryValue | string | null | undefined): number | null {
  if (raw === null || raw === undefined) return null;
  let value = String(raw).trim();
  if (!value) return null;
  value = value.replace(/[^\d.,-]/g, "");
  if (!value) return null;

  const hasComma = value.includes(",");
  const hasDot = value.includes(".");
  if (hasComma && hasDot) {
    if (value.lastIndexOf(",") > value.lastIndexOf(".")) {
      value = value.replaceAll(".", "").replace(",", ".");
    } else {
      value = value.replaceAll(",", "");
    }
  } else if (hasComma) {
    const parts = value.split(",");
    if (parts.length === 2 && parts[1].length <= 2) {
      value = `${parts[0]}.${parts[1]}`;
    } else {
      value = value.replaceAll(",", "");
    }
  }

  const parsed = Number(value);
  if (!Number.isFinite(parsed)) return null;
  return Math.round(parsed * 100);
}

export function parseQty(raw: FormDataEntryValue | string | null | undefined): number | null {
  if (raw === null || raw === undefined) return null;
  const value = String(raw).trim().replace(/[^\d.,-]/g, "").replace(",", ".");
  if (!value) return null;
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) return null;
  return Math.round(parsed * 1000) / 1000;
}

export function formatQty(qty: number): string {
  return new Intl.NumberFormat("es-MX", { maximumFractionDigits: 3 }).format(qty);
}

export function formatPercent(rate: number): string {
  const pct = Math.round(rate * 10000) / 100;
  return `${new Intl.NumberFormat("es-MX", { maximumFractionDigits: 2 }).format(pct)}%`;
}

/** Redondeo monetario por línea: centavos enteros. */
export function lineTotals(qty: number, unitPriceCents: number, ivaRate: number) {
  const subtotal = Math.round(qty * unitPriceCents);
  const iva = Math.round(subtotal * ivaRate);
  return { subtotal, iva, total: subtotal + iva };
}
