import Link from "next/link";
import type { ReactNode } from "react";
import { cn } from "@/lib/cn";
import { formatMoney } from "@/lib/money";
import { todayISO } from "@/lib/dates";
import type { InvoiceRow } from "@/lib/types";

export function PageHeader({
  title,
  description,
  actions,
  eyebrow,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
  eyebrow?: string;
}) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div className="min-w-0">
        {eyebrow && (
          <p className="mb-1 text-[11px] font-bold uppercase tracking-[0.1em] text-muted">{eyebrow}</p>
        )}
        <h1 className="text-[26px] font-bold leading-tight tracking-[-0.03em] text-ink sm:text-[30px]">
          {title}
        </h1>
        {description && <p className="mt-1.5 max-w-[70ch] text-sm text-muted">{description}</p>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}

export function SectionHeader({
  title,
  description,
  action,
}: {
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-b border-line px-5 py-4">
      <div>
        <h2 className="section-title text-ink">{title}</h2>
        {description && <p className="mt-0.5 text-xs text-muted">{description}</p>}
      </div>
      {action}
    </div>
  );
}

type Tone = "neutral" | "accent" | "success" | "warn" | "danger" | "info";

export function Badge({ tone = "neutral", children }: { tone?: Tone; children: ReactNode }) {
  return <span className={cn("badge", `badge-${tone}`)}>{children}</span>;
}

export function InvoiceBadge({
  status,
  dueDate,
}: {
  status: InvoiceRow["status"];
  dueDate?: string;
}) {
  if (status === "emitida" && dueDate && dueDate < todayISO()) {
    return <Badge tone="danger">Vencida</Badge>;
  }
  switch (status) {
    case "borrador":
      return <Badge tone="neutral">Borrador</Badge>;
    case "emitida":
      return <Badge tone="info">Emitida</Badge>;
    case "pagada":
      return <Badge tone="success">Pagada</Badge>;
    case "cancelada":
      return <Badge tone="danger">Cancelada</Badge>;
  }
}

export function Stat({
  label,
  value,
  hint,
  icon,
  tone = "default",
}: {
  label: string;
  value: ReactNode;
  hint?: ReactNode;
  icon?: ReactNode;
  tone?: "default" | "accent" | "danger" | "warn";
}) {
  const valueClass =
    tone === "accent"
      ? "text-accent"
      : tone === "danger"
        ? "text-danger"
        : tone === "warn"
          ? "text-warn"
          : "text-ink";
  return (
    <div className="card p-4 sm:p-5">
      <div className="flex items-start justify-between gap-3">
        <p className="text-[12.5px] font-semibold text-muted">{label}</p>
        {icon && <span className="icon-chip">{icon}</span>}
      </div>
      <p className={cn("kpi-value mt-2.5", valueClass)}>{value}</p>
      {hint && <div className="mt-1.5 text-xs leading-relaxed text-muted">{hint}</div>}
    </div>
  );
}

export function Money({
  cents,
  className,
  colored = false,
}: {
  cents: number;
  className?: string;
  colored?: boolean;
}) {
  return (
    <span
      className={cn(
        "tabular-nums",
        colored && cents > 0 && "text-success",
        colored && cents < 0 && "text-danger",
        className,
      )}
    >
      {formatMoney(cents)}
    </span>
  );
}

export function EmptyState({
  title,
  description,
  action,
  icon,
}: {
  title: string;
  description?: string;
  action?: ReactNode;
  icon?: ReactNode;
}) {
  return (
    <div className="empty">
      {icon && <span className="icon-chip mb-1">{icon}</span>}
      <p className="text-sm font-semibold text-ink">{title}</p>
      {description && <p className="max-w-[48ch] text-xs leading-relaxed text-muted">{description}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}

export function Field({
  label,
  htmlFor,
  hint,
  children,
  required,
  className,
}: {
  label: string;
  htmlFor?: string;
  hint?: ReactNode;
  children: ReactNode;
  required?: boolean;
  className?: string;
}) {
  return (
    <div className={cn("field", className)}>
      <label className="label" htmlFor={htmlFor}>
        {label}
        {required && (
          <span className="ml-1 font-normal text-danger" aria-hidden="true">
            *
          </span>
        )}
      </label>
      {children}
      {hint && <p className="hint">{hint}</p>}
    </div>
  );
}

export function Pagination({
  page,
  pageCount,
  makeHref,
  totalLabel,
}: {
  page: number;
  pageCount: number;
  makeHref: (page: number) => string;
  totalLabel?: string;
}) {
  if (pageCount <= 1) {
    return totalLabel ? <p className="text-xs text-muted">{totalLabel}</p> : null;
  }
  return (
    <div className="flex flex-wrap items-center justify-between gap-3">
      {totalLabel ? <p className="text-xs text-muted">{totalLabel}</p> : <span />}
      <div className="flex items-center gap-2">
        {page > 1 ? (
          <Link className="btn btn-secondary btn-sm" href={makeHref(page - 1)}>
            Anterior
          </Link>
        ) : (
          <span className="btn btn-secondary btn-sm pointer-events-none opacity-40">Anterior</span>
        )}
        <span className="px-1 text-xs font-medium text-muted">
          {page} / {pageCount}
        </span>
        {page < pageCount ? (
          <Link className="btn btn-secondary btn-sm" href={makeHref(page + 1)}>
            Siguiente
          </Link>
        ) : (
          <span className="btn btn-secondary btn-sm pointer-events-none opacity-40">Siguiente</span>
        )}
      </div>
    </div>
  );
}

export function Progress({ value, max }: { value: number; max: number }) {
  const pct = max > 0 ? Math.min(100, Math.max(0, (value / max) * 100)) : 0;
  return (
    <div
      className="progress-track"
      role="progressbar"
      aria-valuenow={Math.round(pct)}
      aria-valuemin={0}
      aria-valuemax={100}
    >
      <div className="progress-fill" style={{ width: `${pct}%` }} />
    </div>
  );
}
