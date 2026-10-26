export const TZ = "America/Mexico_City";

const ISO_DATE_RE = /^\d{4}-\d{2}-\d{2}$/;

export function isValidISODate(value: string): boolean {
  if (!ISO_DATE_RE.test(value)) return false;
  const date = new Date(`${value}T12:00:00Z`);
  return !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === value;
}

export function todayISO(date = new Date()): string {
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: TZ,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(date);
}

export function nowISO(): string {
  return new Date().toISOString();
}

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return "—";
  const date = iso.length <= 10 ? new Date(`${iso}T12:00:00Z`) : new Date(iso);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat("es-MX", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    timeZone: iso.length <= 10 ? "UTC" : TZ,
  }).format(date);
}

export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return "—";
  const normalized = iso.includes("T") ? iso : `${iso.replace(" ", "T")}Z`;
  const date = new Date(normalized);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat("es-MX", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: TZ,
  }).format(date);
}

export function monthRange(year: number, month: number): { start: string; end: string } {
  const start = `${year}-${String(month).padStart(2, "0")}-01`;
  const lastDay = new Date(Date.UTC(year, month, 0)).getUTCDate();
  const end = `${year}-${String(month).padStart(2, "0")}-${String(lastDay).padStart(2, "0")}`;
  return { start, end };
}

export function currentMonth(): { year: number; month: number } {
  const [year, month] = todayISO().split("-").map(Number);
  return { year, month };
}

export function currentMonthRange(): { start: string; end: string } {
  const { year, month } = currentMonth();
  return monthRange(year, month);
}

export function yearRange(year: number): { start: string; end: string } {
  return { start: `${year}-01-01`, end: `${year}-12-31` };
}

export function lastMonths(count: number): Array<{ year: number; month: number; label: string; start: string; end: string }> {
  const now = currentMonth();
  const months: Array<{ year: number; month: number; label: string; start: string; end: string }> = [];
  for (let offset = count - 1; offset >= 0; offset -= 1) {
    const date = new Date(Date.UTC(now.year, now.month - 1 - offset, 1));
    const year = date.getUTCFullYear();
    const month = date.getUTCMonth() + 1;
    const { start, end } = monthRange(year, month);
    months.push({
      year,
      month,
      label: new Intl.DateTimeFormat("es-MX", { month: "short", timeZone: "UTC" }).format(date),
      start,
      end,
    });
  }
  return months;
}

export function monthLabel(month: number): string {
  return new Intl.DateTimeFormat("es-MX", { month: "long", timeZone: "UTC" }).format(
    new Date(Date.UTC(2024, month - 1, 1)),
  );
}

export function parseMonthParam(value: string | null | undefined): { year: number; month: number } {
  if (value && /^\d{4}-\d{2}$/.test(value)) {
    const [year, month] = value.split("-").map(Number);
    if (month >= 1 && month <= 12 && year >= 2000 && year <= 2100) return { year, month };
  }
  return currentMonth();
}

export function daysInMonth(year: number, month: number): number {
  return new Date(Date.UTC(year, month, 0)).getUTCDate();
}

export function diffDays(from: string, to: string): number {
  const a = new Date(`${from}T12:00:00Z`).getTime();
  const b = new Date(`${to}T12:00:00Z`).getTime();
  return Math.round((b - a) / 86_400_000);
}

export function addDays(iso: string, days: number): string {
  const date = new Date(`${iso}T12:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}
