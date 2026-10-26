import { diffDays, monthLabel } from "./dates";

export type IsrBracket = {
  lower: number;
  upper: number | null;
  rate: number;
  fixed: number;
};

/**
 * Tarifa mensual de referencia (pesos). Es editable desde Configuración.
 * No incluye subsidio al empleo; verifica siempre con tu contador.
 */
export const DEFAULT_ISR_BRACKETS: IsrBracket[] = [
  { lower: 0.01, upper: 746.04, rate: 0.0, fixed: 0 },
  { lower: 746.05, upper: 6332.05, rate: 0.064, fixed: 0 },
  { lower: 6332.06, upper: 11128.01, rate: 0.1088, fixed: 357.69 },
  { lower: 11128.02, upper: 12935.82, rate: 0.16, fixed: 879.25 },
  { lower: 12935.83, upper: 15487.71, rate: 0.1792, fixed: 1168.51 },
  { lower: 15487.72, upper: 18599.48, rate: 0.2136, fixed: 1625.93 },
  { lower: 18599.49, upper: 37443.06, rate: 0.2352, fixed: 2371.29 },
  { lower: 37443.07, upper: 59049.0, rate: 0.3, fixed: 6531.66 },
  { lower: 59049.01, upper: 89299.0, rate: 0.32, fixed: 13113.44 },
  { lower: 89299.01, upper: 181998.0, rate: 0.34, fixed: 22793.44 },
  { lower: 181998.01, upper: null, rate: 0.35, fixed: 54331.1 },
];

export function parseIsrBrackets(raw: string): IsrBracket[] {
  if (!raw.trim()) return DEFAULT_ISR_BRACKETS;
  try {
    const parsed: unknown = JSON.parse(raw);
    if (!Array.isArray(parsed)) return DEFAULT_ISR_BRACKETS;
    const brackets = parsed.filter((item): item is IsrBracket => {
      if (typeof item !== "object" || item === null) return false;
      const row = item as Record<string, unknown>;
      return (
        typeof row.lower === "number" &&
        (typeof row.upper === "number" || row.upper === null) &&
        typeof row.rate === "number" &&
        typeof row.fixed === "number"
      );
    });
    return brackets.length > 0 ? brackets : DEFAULT_ISR_BRACKETS;
  } catch {
    return DEFAULT_ISR_BRACKETS;
  }
}

/** ISR estimado en centavos sobre una base gravable en centavos. */
export function computeIsr(
  taxableCents: number,
  brackets: IsrBracket[],
  periodType: "mensual" | "quincenal",
): number {
  const factor = periodType === "quincenal" ? 0.5 : 1;
  const taxable = Math.max(0, taxableCents / 100);
  const scaled = brackets.map((bracket) => ({
    lower: bracket.lower * factor,
    upper: bracket.upper === null ? null : bracket.upper * factor,
    rate: bracket.rate,
    fixed: bracket.fixed * factor,
  }));

  const bracket =
    scaled.find((item) => item.upper !== null && taxable <= item.upper) ?? scaled[scaled.length - 1];
  if (!bracket) return 0;
  if (taxable < bracket.lower) return Math.round(bracket.fixed * 100);
  const isr = bracket.fixed + (taxable - bracket.lower) * bracket.rate;
  return Math.max(0, Math.round(isr * 100));
}

export type PayrollEmployeeInput = {
  id: number;
  name: string;
  salaryCents: number;
  startDate: string;
  endDate: string | null;
};

export type PayrollCalculation = {
  employeeId: number;
  employeeName: string;
  days: number;
  baseCents: number;
  extraPerceptionsCents: number;
  isrCents: number;
  imssCents: number;
  infonavitCents: number;
  otherDeductionsCents: number;
  netCents: number;
};

export function calculatePayrollItem(
  employee: PayrollEmployeeInput,
  period: { start: string; end: string; periodType: "mensual" | "quincenal" },
  options: { imssRate: number; brackets: IsrBracket[]; extraPerceptionsCents?: number },
): PayrollCalculation | null {
  const start = employee.startDate > period.start ? employee.startDate : period.start;
  const end = employee.endDate && employee.endDate < period.end ? employee.endDate : period.end;
  if (start > end) return null;

  const periodDays = diffDays(period.start, period.end) + 1;
  const workedDays = diffDays(start, end) + 1;
  const fullSalary = employee.salaryCents;
  const periodSalary =
    period.periodType === "quincenal" ? Math.round(fullSalary / 2) : fullSalary;
  const baseCents = Math.round((periodSalary * workedDays) / periodDays);
  const extraPerceptionsCents = options.extraPerceptionsCents ?? 0;
  const taxable = baseCents + extraPerceptionsCents;
  const isrCents = computeIsr(taxable, options.brackets, period.periodType);
  const imssCents = Math.round(taxable * Math.max(0, options.imssRate));
  const deductions = isrCents + imssCents;
  const netCents = Math.max(0, taxable - deductions);

  return {
    employeeId: employee.id,
    employeeName: employee.name,
    days: workedDays,
    baseCents,
    extraPerceptionsCents,
    isrCents,
    imssCents,
    infonavitCents: 0,
    otherDeductionsCents: 0,
    netCents,
  };
}

export function payrollPeriodLabel(period: {
  year: number;
  month: number;
  period_type: string;
  quincena: number;
}): string {
  const month = monthLabel(period.month);
  const label = `${month.charAt(0).toUpperCase()}${month.slice(1)} ${period.year}`;
  return period.period_type === "quincenal"
    ? `${label} · ${period.quincena === 1 ? "1.ª" : "2.ª"} quincena`
    : label;
}
