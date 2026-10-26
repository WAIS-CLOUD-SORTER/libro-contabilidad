import { formatMoney, formatMoneyCompact } from "@/lib/money";

export function BarPairChart({
  rows,
  incomeLabel = "Ingresos",
  expenseLabel = "Gastos",
  height = 150,
}: {
  rows: Array<{ label: string; incomeCents: number; expenseCents: number }>;
  incomeLabel?: string;
  expenseLabel?: string;
  height?: number;
}) {
  const max = Math.max(1, ...rows.flatMap((row) => [row.incomeCents, row.expenseCents]));
  return (
    <div>
      <div className="flex items-center gap-4 text-[11px] font-semibold text-muted">
        <span className="inline-flex items-center gap-1.5">
          <span className="h-2.5 w-2.5 rounded-sm bg-[var(--accent)]" aria-hidden="true" />
          {incomeLabel}
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="h-2.5 w-2.5 rounded-sm bg-[var(--expense)]" aria-hidden="true" />
          {expenseLabel}
        </span>
      </div>
      <div
        className="mt-3 flex items-end gap-2 sm:gap-4"
        style={{ height }}
        role="img"
        aria-label={`${incomeLabel} y ${expenseLabel} por mes`}
      >
        {rows.map((row) => {
          const incomeHeight = Math.max(0, Math.round((row.incomeCents / max) * 100));
          const expenseHeight = Math.max(0, Math.round((row.expenseCents / max) * 100));
          return (
            <div key={row.label} className="flex h-full min-w-0 flex-1 flex-col justify-end">
              <div className="flex h-full items-end justify-center gap-1">
                <div
                  className="w-full max-w-[22px] rounded-t-[3px] bg-[var(--accent)] transition-[height] duration-300"
                  style={{ height: `${incomeHeight}%`, minHeight: row.incomeCents > 0 ? 2 : 0 }}
                  title={`${row.label} · ${incomeLabel}: ${formatMoney(row.incomeCents)}`}
                />
                <div
                  className="w-full max-w-[22px] rounded-t-[3px] bg-[var(--expense)] opacity-80 transition-[height] duration-300"
                  style={{ height: `${expenseHeight}%`, minHeight: row.expenseCents > 0 ? 2 : 0 }}
                  title={`${row.label} · ${expenseLabel}: ${formatMoney(row.expenseCents)}`}
                />
              </div>
              <p className="mt-2 truncate text-center text-[10.5px] font-medium capitalize text-muted">
                {row.label}
              </p>
            </div>
          );
        })}
      </div>
      <p className="mt-2 text-[10.5px] text-muted">
        Escala máxima: {formatMoneyCompact(max)}
      </p>
    </div>
  );
}
