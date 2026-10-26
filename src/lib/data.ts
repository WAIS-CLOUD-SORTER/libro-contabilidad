import { all, one } from "./db";
import { currentMonthRange, daysInMonth, diffDays, lastMonths, monthLabel, monthRange, todayISO } from "./dates";
import { DEFAULT_ISR_BRACKETS, parseIsrBrackets } from "./payroll";
import type {
  Account,
  Category,
  Company,
  Contact,
  Employee,
  InvoiceItem,
  InvoiceRow,
  Payment,
  PayrollItem,
  PayrollPeriod,
  Product,
  TransactionRow,
  UserRow,
} from "./types";

const FALLBACK_COMPANY: Company = {
  id: 1,
  name: "Mi negocio",
  rfc: "",
  regimen_fiscal: "",
  address: "",
  phone: "",
  email: "",
  iva_rate: 0.16,
  invoice_series: "A",
  next_invoice_number: 1,
  imss_rate: 0.025,
  isr_brackets: JSON.stringify(DEFAULT_ISR_BRACKETS),
  created_at: new Date().toISOString(),
};

export function getCompany(): Company {
  return one<Company>("SELECT * FROM company WHERE id = 1") ?? FALLBACK_COMPANY;
}

export function getAccounts(includeInactive = false): Account[] {
  return all<Account>(
    `SELECT * FROM accounts ${includeInactive ? "" : "WHERE active = 1"} ORDER BY name`,
  );
}

export function getCategories(includeArchived = false): Category[] {
  return all<Category>(
    `SELECT * FROM categories ${includeArchived ? "" : "WHERE archived = 0"} ORDER BY kind, name`,
  );
}

export function getContacts(includeArchived = false): Contact[] {
  return all<Contact>(
    `SELECT * FROM contacts ${includeArchived ? "" : "WHERE archived = 0"} ORDER BY name`,
  );
}

export function getProducts(includeInactive = false): Product[] {
  return all<Product>(
    `SELECT * FROM products ${includeInactive ? "" : "WHERE active = 1"} ORDER BY name`,
  );
}

export function getEmployees(includeInactive = false): Employee[] {
  return all<Employee>(
    `SELECT * FROM employees ${includeInactive ? "" : "WHERE status = 'activo'"} ORDER BY name`,
  );
}

export function getEmployee(id: number): Employee | undefined {
  return one<Employee>("SELECT * FROM employees WHERE id = ?", [id]);
}

export function getUserById(id: number): UserRow | undefined {
  return one<UserRow>(
    "SELECT id, name, email, role, active, created_at, last_login_at FROM users WHERE id = ?",
    [id],
  );
}

export function getUsers(): UserRow[] {
  return all<UserRow>(
    "SELECT id, name, email, role, active, created_at, last_login_at FROM users ORDER BY name",
  );
}

export type AccountBalance = Account & { balance_cents: number };

export function getAccountBalances(includeInactive = false): AccountBalance[] {
  return all<AccountBalance>(
    `SELECT a.*,
       a.opening_balance_cents
       + COALESCE((SELECT SUM(CASE WHEN t.type = 'ingreso' THEN t.total_cents ELSE -t.total_cents END)
                   FROM transactions t WHERE t.account_id = a.id), 0)
       + COALESCE((SELECT SUM(p.amount_cents) FROM payments p WHERE p.account_id = a.id), 0)
       AS balance_cents
     FROM accounts a
     ${includeInactive ? "" : "WHERE a.active = 1"}
     ORDER BY a.name`,
  );
}

export function getTransactions(filters: {
  from?: string;
  to?: string;
  type?: string;
  categoryId?: number;
  q?: string;
  limit?: number;
  offset?: number;
}): { rows: TransactionRow[]; total: number } {
  const where: string[] = [];
  const params: Array<string | number> = [];
  if (filters.from) {
    where.push("t.date >= ?");
    params.push(filters.from);
  }
  if (filters.to) {
    where.push("t.date <= ?");
    params.push(filters.to);
  }
  if (filters.type === "ingreso" || filters.type === "gasto") {
    where.push("t.type = ?");
    params.push(filters.type);
  }
  if (filters.categoryId) {
    where.push("t.category_id = ?");
    params.push(filters.categoryId);
  }
  if (filters.q) {
    where.push("(t.description LIKE ? OR t.reference LIKE ? OR c.name LIKE ?)");
    params.push(`%${filters.q}%`, `%${filters.q}%`, `%${filters.q}%`);
  }
  const whereSql = where.length ? `WHERE ${where.join(" AND ")}` : "";
  const count = one<{ n: number }>(
    `SELECT COUNT(*) AS n FROM transactions t
     LEFT JOIN contacts c ON c.id = t.contact_id ${whereSql}`,
    params,
  );
  const limit = filters.limit ?? 50;
  const offset = filters.offset ?? 0;

  const rows = all<TransactionRow>(
    `SELECT t.*, cat.name AS category_name, cat.kind AS category_kind,
            c.name AS contact_name, a.name AS account_name, u.name AS created_by_name
     FROM transactions t
     LEFT JOIN categories cat ON cat.id = t.category_id
     LEFT JOIN contacts c ON c.id = t.contact_id
     LEFT JOIN accounts a ON a.id = t.account_id
     LEFT JOIN users u ON u.id = t.created_by
     ${whereSql}
     ORDER BY t.date DESC, t.id DESC
     LIMIT ? OFFSET ?`,
    [...params, limit, offset],
  );
  return { rows, total: Number(count?.n ?? 0) };
}

export function getTransaction(id: number): TransactionRow | undefined {
  return one<TransactionRow>(
    `SELECT t.*, cat.name AS category_name, cat.kind AS category_kind,
            c.name AS contact_name, a.name AS account_name, u.name AS created_by_name
     FROM transactions t
     LEFT JOIN categories cat ON cat.id = t.category_id
     LEFT JOIN contacts c ON c.id = t.contact_id
     LEFT JOIN accounts a ON a.id = t.account_id
     LEFT JOIN users u ON u.id = t.created_by
     WHERE t.id = ?`,
    [id],
  );
}

export function getTransactionTotals(filters: {
  from?: string;
  to?: string;
  type?: string;
  categoryId?: number;
  q?: string;
}): { incomeSubtotal: number; incomeIva: number; expenseSubtotal: number; expenseIva: number; count: number } {
  const where: string[] = [];
  const params: Array<string | number> = [];
  if (filters.from) {
    where.push("t.date >= ?");
    params.push(filters.from);
  }
  if (filters.to) {
    where.push("t.date <= ?");
    params.push(filters.to);
  }
  if (filters.type === "ingreso" || filters.type === "gasto") {
    where.push("t.type = ?");
    params.push(filters.type);
  }
  if (filters.categoryId) {
    where.push("t.category_id = ?");
    params.push(filters.categoryId);
  }
  if (filters.q) {
    where.push("(t.description LIKE ? OR t.reference LIKE ? OR c.name LIKE ?)");
    params.push(`%${filters.q}%`, `%${filters.q}%`, `%${filters.q}%`);
  }
  const whereSql = where.length ? `WHERE ${where.join(" AND ")}` : "";
  const row = one<{
    income_subtotal: number;
    income_iva: number;
    expense_subtotal: number;
    expense_iva: number;
    count: number;
  }>(
    `SELECT
       COALESCE(SUM(CASE WHEN t.type = 'ingreso' THEN t.subtotal_cents ELSE 0 END), 0) AS income_subtotal,
       COALESCE(SUM(CASE WHEN t.type = 'ingreso' THEN t.iva_cents ELSE 0 END), 0) AS income_iva,
       COALESCE(SUM(CASE WHEN t.type = 'gasto' THEN t.subtotal_cents ELSE 0 END), 0) AS expense_subtotal,
       COALESCE(SUM(CASE WHEN t.type = 'gasto' THEN t.iva_cents ELSE 0 END), 0) AS expense_iva,
       COUNT(*) AS count
     FROM transactions t
     LEFT JOIN contacts c ON c.id = t.contact_id
     ${whereSql}`,
    params,
  );
  return {
    incomeSubtotal: Number(row?.income_subtotal ?? 0),
    incomeIva: Number(row?.income_iva ?? 0),
    expenseSubtotal: Number(row?.expense_subtotal ?? 0),
    expenseIva: Number(row?.expense_iva ?? 0),
    count: Number(row?.count ?? 0),
  };
}

export function getInvoices(filters: {
  status?: string;
  q?: string;
  from?: string;
  to?: string;
  limit?: number;
  offset?: number;
}): { rows: InvoiceRow[]; total: number } {
  const where: string[] = [];
  const params: Array<string | number> = [];
  if (filters.status && ["borrador", "emitida", "pagada", "cancelada"].includes(filters.status)) {
    where.push("i.status = ?");
    params.push(filters.status);
  }
  if (filters.from) {
    where.push("i.date >= ?");
    params.push(filters.from);
  }
  if (filters.to) {
    where.push("i.date <= ?");
    params.push(filters.to);
  }
  if (filters.q) {
    where.push("(c.name LIKE ? OR CAST(i.number AS TEXT) LIKE ? OR i.notes LIKE ?)");
    params.push(`%${filters.q}%`, `%${filters.q}%`, `%${filters.q}%`);
  }
  const whereSql = where.length ? `WHERE ${where.join(" AND ")}` : "";
  const count = one<{ n: number }>(
    `SELECT COUNT(*) AS n FROM invoices i JOIN contacts c ON c.id = i.contact_id ${whereSql}`,
    params,
  );
  const limit = filters.limit ?? 50;
  const offset = filters.offset ?? 0;

  const rows = all<InvoiceRow>(
    `SELECT i.*, c.name AS contact_name, c.rfc AS contact_rfc, u.name AS created_by_name
     FROM invoices i
     JOIN contacts c ON c.id = i.contact_id
     LEFT JOIN users u ON u.id = i.created_by
     ${whereSql}
     ORDER BY i.date DESC, i.id DESC
     LIMIT ? OFFSET ?`,
    [...params, limit, offset],
  );
  return { rows, total: Number(count?.n ?? 0) };
}

export function getInvoice(id: number): InvoiceRow | undefined {
  return one<InvoiceRow>(
    `SELECT i.*, c.name AS contact_name, c.rfc AS contact_rfc, u.name AS created_by_name
     FROM invoices i
     JOIN contacts c ON c.id = i.contact_id
     LEFT JOIN users u ON u.id = i.created_by
     WHERE i.id = ?`,
    [id],
  );
}

export function getInvoiceItems(invoiceId: number): InvoiceItem[] {
  return all<InvoiceItem>(
    `SELECT ii.*, p.sku AS sku, p.unit AS unit
     FROM invoice_items ii
     LEFT JOIN products p ON p.id = ii.product_id
     WHERE ii.invoice_id = ?
     ORDER BY ii.position, ii.id`,
    [invoiceId],
  );
}

export function getPayments(invoiceId: number): Payment[] {
  return all<Payment>(
    `SELECT p.*, a.name AS account_name, u.name AS created_by_name
     FROM payments p
     LEFT JOIN accounts a ON a.id = p.account_id
     LEFT JOIN users u ON u.id = p.created_by
     WHERE p.invoice_id = ?
     ORDER BY p.date, p.id`,
    [invoiceId],
  );
}

export function getInvoiceOverview(): {
  receivable: number;
  overdue: number;
  overdueCount: number;
  monthTotal: number;
  monthCount: number;
  draftCount: number;
} {
  const { start, end } = currentMonthRange();
  const today = todayISO();
  const row = one<{
    receivable: number;
    overdue: number;
    overdue_count: number;
    month_total: number;
    month_count: number;
    draft_count: number;
  }>(
    `SELECT
       COALESCE(SUM(CASE WHEN status = 'emitida' THEN total_cents - paid_cents ELSE 0 END), 0) AS receivable,
       COALESCE(SUM(CASE WHEN status = 'emitida' AND due_date < ? THEN total_cents - paid_cents ELSE 0 END), 0) AS overdue,
       COALESCE(SUM(CASE WHEN status = 'emitida' AND due_date < ? THEN 1 ELSE 0 END), 0) AS overdue_count,
       COALESCE(SUM(CASE WHEN status IN ('emitida','pagada') AND date BETWEEN ? AND ? THEN total_cents ELSE 0 END), 0) AS month_total,
       COALESCE(SUM(CASE WHEN status IN ('emitida','pagada') AND date BETWEEN ? AND ? THEN 1 ELSE 0 END), 0) AS month_count,
       COALESCE(SUM(CASE WHEN status = 'borrador' THEN 1 ELSE 0 END), 0) AS draft_count
     FROM invoices`,
    [today, today, start, end, start, end],
  );
  return {
    receivable: Number(row?.receivable ?? 0),
    overdue: Number(row?.overdue ?? 0),
    overdueCount: Number(row?.overdue_count ?? 0),
    monthTotal: Number(row?.month_total ?? 0),
    monthCount: Number(row?.month_count ?? 0),
    draftCount: Number(row?.draft_count ?? 0),
  };
}

export type DashboardData = {
  range: { start: string; end: string };
  revenueCents: number;
  otherIncomeCents: number;
  expenseCents: number;
  profitCents: number;
  ivaTrasladadoCents: number;
  ivaAcreditableCents: number;
  receivableCents: number;
  overdueCount: number;
  cashCents: number;
  inventoryCents: number;
  lowStock: Product[];
  recentInvoices: InvoiceRow[];
  recentTransactions: TransactionRow[];
  monthly: Array<{ label: string; incomeCents: number; expenseCents: number }>;
};

export function getDashboard(): DashboardData {
  const range = currentMonthRange();
  const today = todayISO();

  const invoiced = one<{ subtotal: number; iva: number }>(
    `SELECT COALESCE(SUM(subtotal_cents),0) AS subtotal, COALESCE(SUM(iva_cents),0) AS iva
     FROM invoices WHERE status IN ('emitida','pagada') AND date BETWEEN ? AND ?`,
    [range.start, range.end],
  );
  const otherIncome = one<{ subtotal: number; iva: number }>(
    `SELECT COALESCE(SUM(subtotal_cents),0) AS subtotal, COALESCE(SUM(iva_cents),0) AS iva
     FROM transactions WHERE type = 'ingreso' AND date BETWEEN ? AND ?`,
    [range.start, range.end],
  );
  const expenses = one<{ subtotal: number; iva: number }>(
    `SELECT COALESCE(SUM(subtotal_cents),0) AS subtotal, COALESCE(SUM(iva_cents),0) AS iva
     FROM transactions WHERE type = 'gasto' AND date BETWEEN ? AND ?`,
    [range.start, range.end],
  );
  const receivable = one<{ total: number; count: number }>(
    `SELECT COALESCE(SUM(total_cents - paid_cents),0) AS total, COUNT(*) AS count
     FROM invoices WHERE status = 'emitida' AND date <= ?`,
    [today],
  );
  const overdue = one<{ n: number }>(
    `SELECT COUNT(*) AS n FROM invoices
     WHERE status = 'emitida' AND due_date < ? AND (total_cents - paid_cents) > 0`,
    [today],
  );
  const cash = getAccountBalances();
  const cashCents = cash.reduce((sum, account) => sum + Number(account.balance_cents), 0);
  const inventory = one<{ value: number }>(
    `SELECT COALESCE(SUM(CAST(ROUND(stock * cost_cents) AS INTEGER)),0) AS value
     FROM products WHERE active = 1 AND track_stock = 1`,
  );

  const monthly = lastMonths(6).map((month) => {
    const income = one<{ total: number }>(
      `SELECT COALESCE(SUM(subtotal_cents),0) AS total FROM invoices
       WHERE status IN ('emitida','pagada') AND date BETWEEN ? AND ?`,
      [month.start, month.end],
    );
    const other = one<{ total: number }>(
      `SELECT COALESCE(SUM(subtotal_cents),0) AS total FROM transactions
       WHERE type = 'ingreso' AND date BETWEEN ? AND ?`,
      [month.start, month.end],
    );
    const expense = one<{ total: number }>(
      `SELECT COALESCE(SUM(subtotal_cents),0) AS total FROM transactions
       WHERE type = 'gasto' AND date BETWEEN ? AND ?`,
      [month.start, month.end],
    );
    return {
      label: month.label,
      incomeCents: Number(income?.total ?? 0) + Number(other?.total ?? 0),
      expenseCents: Number(expense?.total ?? 0),
    };
  });

  const revenueCents = Number(invoiced?.subtotal ?? 0);
  const otherIncomeCents = Number(otherIncome?.subtotal ?? 0);
  const expenseCents = Number(expenses?.subtotal ?? 0);

  return {
    range,
    revenueCents,
    otherIncomeCents,
    expenseCents,
    profitCents: revenueCents + otherIncomeCents - expenseCents,
    ivaTrasladadoCents: Number(invoiced?.iva ?? 0) + Number(otherIncome?.iva ?? 0),
    ivaAcreditableCents: Number(expenses?.iva ?? 0),
    receivableCents: Number(receivable?.total ?? 0),
    overdueCount: Number(overdue?.n ?? 0),
    cashCents,
    inventoryCents: Number(inventory?.value ?? 0),
    lowStock: all<Product>(
      `SELECT * FROM products
       WHERE active = 1 AND track_stock = 1 AND stock <= min_stock
       ORDER BY (stock - min_stock) ASC LIMIT 5`,
    ),
    recentInvoices: all<InvoiceRow>(
      `SELECT i.*, c.name AS contact_name, c.rfc AS contact_rfc, NULL AS created_by_name
       FROM invoices i JOIN contacts c ON c.id = i.contact_id
       ORDER BY i.date DESC, i.id DESC LIMIT 5`,
    ),
    recentTransactions: all<TransactionRow>(
      `SELECT t.*, cat.name AS category_name, cat.kind AS category_kind,
              c.name AS contact_name, a.name AS account_name, NULL AS created_by_name
       FROM transactions t
       LEFT JOIN categories cat ON cat.id = t.category_id
       LEFT JOIN contacts c ON c.id = t.contact_id
       LEFT JOIN accounts a ON a.id = t.account_id
       ORDER BY t.date DESC, t.id DESC LIMIT 5`,
    ),
    monthly,
  };
}

export type TaxSummary = {
  from: string;
  to: string;
  invoiceCount: number;
  invoiceSubtotal: number;
  invoiceIva: number;
  otherIncomeSubtotal: number;
  otherIncomeIva: number;
  expenseSubtotal: number;
  expenseIva: number;
  ivaTrasladado: number;
  ivaAcreditable: number;
  ivaNeto: number;
  invoices: InvoiceRow[];
  incomeTransactions: TransactionRow[];
  expenses: TransactionRow[];
};

export function getTaxSummary(from: string, to: string): TaxSummary {
  const invoices = all<InvoiceRow>(
    `SELECT i.*, c.name AS contact_name, c.rfc AS contact_rfc, NULL AS created_by_name
     FROM invoices i JOIN contacts c ON c.id = i.contact_id
     WHERE i.status IN ('emitida','pagada') AND i.date BETWEEN ? AND ?
     ORDER BY i.date, i.id`,
    [from, to],
  );
  const incomeTx = all<TransactionRow>(
    `SELECT t.*, cat.name AS category_name, cat.kind AS category_kind,
            c.name AS contact_name, a.name AS account_name, NULL AS created_by_name
     FROM transactions t
     LEFT JOIN categories cat ON cat.id = t.category_id
     LEFT JOIN contacts c ON c.id = t.contact_id
     LEFT JOIN accounts a ON a.id = t.account_id
     WHERE t.type = 'ingreso' AND t.date BETWEEN ? AND ?
     ORDER BY t.date, t.id`,
    [from, to],
  );
  const expenses = all<TransactionRow>(
    `SELECT t.*, cat.name AS category_name, cat.kind AS category_kind,
            c.name AS contact_name, a.name AS account_name, NULL AS created_by_name
     FROM transactions t
     LEFT JOIN categories cat ON cat.id = t.category_id
     LEFT JOIN contacts c ON c.id = t.contact_id
     LEFT JOIN accounts a ON a.id = t.account_id
     WHERE t.type = 'gasto' AND t.date BETWEEN ? AND ?
     ORDER BY t.date, t.id`,
    [from, to],
  );

  const invoiceSubtotal = invoices.reduce((sum, invoice) => sum + invoice.subtotal_cents, 0);
  const invoiceIva = invoices.reduce((sum, invoice) => sum + invoice.iva_cents, 0);
  const otherIncomeSubtotal = incomeTx.reduce((sum, row) => sum + row.subtotal_cents, 0);
  const otherIncomeIva = incomeTx.reduce((sum, row) => sum + row.iva_cents, 0);
  const expenseSubtotal = expenses.reduce((sum, row) => sum + row.subtotal_cents, 0);
  const expenseIva = expenses.reduce((sum, row) => sum + row.iva_cents, 0);

  return {
    from,
    to,
    invoiceCount: invoices.length,
    invoiceSubtotal,
    invoiceIva,
    otherIncomeSubtotal,
    otherIncomeIva,
    expenseSubtotal,
    expenseIva,
    ivaTrasladado: invoiceIva + otherIncomeIva,
    ivaAcreditable: expenseIva,
    ivaNeto: invoiceIva + otherIncomeIva - expenseIva,
    invoices,
    incomeTransactions: incomeTx,
    expenses,
  };
}

export type MonthlyTaxRow = {
  month: number;
  label: string;
  ivaTrasladado: number;
  ivaAcreditable: number;
  ivaNeto: number;
};

export function getAnnualTax(year: number): MonthlyTaxRow[] {
  const rows: MonthlyTaxRow[] = [];
  for (let month = 1; month <= 12; month += 1) {
    const { start, end } = monthRange(year, month);
    const summary = getTaxSummary(start, end);
    rows.push({
      month,
      label: monthLabel(month),
      ivaTrasladado: summary.ivaTrasladado,
      ivaAcreditable: summary.ivaAcreditable,
      ivaNeto: summary.ivaNeto,
    });
  }
  return rows;
}

export type IncomeStatement = {
  invoicedRevenue: number;
  otherIncome: number;
  totalIncome: number;
  expensesByCategory: Array<{ name: string; amount: number }>;
  totalExpenses: number;
  operatingProfit: number;
  cogsReference: number;
  grossMarginReference: number;
};

export function getIncomeStatement(from: string, to: string): IncomeStatement {
  const invoiced = one<{ total: number }>(
    `SELECT COALESCE(SUM(subtotal_cents),0) AS total FROM invoices
     WHERE status IN ('emitida','pagada') AND date BETWEEN ? AND ?`,
    [from, to],
  );
  const other = one<{ total: number }>(
    `SELECT COALESCE(SUM(subtotal_cents),0) AS total FROM transactions
     WHERE type = 'ingreso' AND date BETWEEN ? AND ?`,
    [from, to],
  );
  const expenseRows = all<{ name: string; amount: number }>(
    `SELECT COALESCE(cat.name, 'Sin categoría') AS name,
            COALESCE(SUM(t.subtotal_cents),0) AS amount
     FROM transactions t
     LEFT JOIN categories cat ON cat.id = t.category_id
     WHERE t.type = 'gasto' AND t.date BETWEEN ? AND ?
     GROUP BY COALESCE(cat.name, 'Sin categoría')
     ORDER BY amount DESC`,
    [from, to],
  );
  const cogs = one<{ total: number }>(
    `SELECT COALESCE(SUM(CAST(ROUND(ii.qty * ii.unit_cost_cents) AS INTEGER)),0) AS total
     FROM invoice_items ii
     JOIN invoices i ON i.id = ii.invoice_id
     WHERE i.status IN ('emitida','pagada') AND i.date BETWEEN ? AND ?`,
    [from, to],
  );

  const invoicedRevenue = Number(invoiced?.total ?? 0);
  const otherIncome = Number(other?.total ?? 0);
  const totalExpenses = expenseRows.reduce((sum, row) => sum + Number(row.amount), 0);
  const cogsReference = Number(cogs?.total ?? 0);

  return {
    invoicedRevenue,
    otherIncome,
    totalIncome: invoicedRevenue + otherIncome,
    expensesByCategory: expenseRows.map((row) => ({ name: row.name, amount: Number(row.amount) })),
    totalExpenses,
    operatingProfit: invoicedRevenue + otherIncome - totalExpenses,
    cogsReference,
    grossMarginReference: invoicedRevenue - cogsReference,
  };
}

export type CashflowSummary = {
  from: string;
  to: string;
  invoicePayments: number;
  otherIncome: number;
  expenses: number;
  net: number;
  monthly: Array<{ label: string; inflow: number; outflow: number }>;
};

export function getCashflow(from: string, to: string): CashflowSummary {
  const payments = one<{ total: number }>(
    `SELECT COALESCE(SUM(amount_cents),0) AS total FROM payments WHERE date BETWEEN ? AND ?`,
    [from, to],
  );
  const income = one<{ total: number }>(
    `SELECT COALESCE(SUM(total_cents),0) AS total FROM transactions
     WHERE type = 'ingreso' AND date BETWEEN ? AND ?`,
    [from, to],
  );
  const expenses = one<{ total: number }>(
    `SELECT COALESCE(SUM(total_cents),0) AS total FROM transactions
     WHERE type = 'gasto' AND date BETWEEN ? AND ?`,
    [from, to],
  );

  const months: Array<{ label: string; inflow: number; outflow: number }> = [];
  const start = new Date(`${from}T12:00:00Z`);
  const end = new Date(`${to}T12:00:00Z`);
  const cursor = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth(), 1));
  while (cursor <= end) {
    const year = cursor.getUTCFullYear();
    const month = cursor.getUTCMonth() + 1;
    const range = monthRange(year, month);
    const rangeStart = range.start < from ? from : range.start;
    const rangeEnd = range.end > to ? to : range.end;
    const pay = one<{ total: number }>(
      `SELECT COALESCE(SUM(amount_cents),0) AS total FROM payments WHERE date BETWEEN ? AND ?`,
      [rangeStart, rangeEnd],
    );
    const inc = one<{ total: number }>(
      `SELECT COALESCE(SUM(total_cents),0) AS total FROM transactions
       WHERE type = 'ingreso' AND date BETWEEN ? AND ?`,
      [rangeStart, rangeEnd],
    );
    const exp = one<{ total: number }>(
      `SELECT COALESCE(SUM(total_cents),0) AS total FROM transactions
       WHERE type = 'gasto' AND date BETWEEN ? AND ?`,
      [rangeStart, rangeEnd],
    );
    months.push({
      label: new Intl.DateTimeFormat("es-MX", { month: "short", timeZone: "UTC" }).format(cursor),
      inflow: Number(pay?.total ?? 0) + Number(inc?.total ?? 0),
      outflow: Number(exp?.total ?? 0),
    });
    cursor.setUTCMonth(cursor.getUTCMonth() + 1);
  }

  return {
    from,
    to,
    invoicePayments: Number(payments?.total ?? 0),
    otherIncome: Number(income?.total ?? 0),
    expenses: Number(expenses?.total ?? 0),
    net:
      Number(payments?.total ?? 0) + Number(income?.total ?? 0) - Number(expenses?.total ?? 0),
    monthly: months,
  };
}

export type AgingBucket = { label: string; amount: number; count: number };

export function getReceivableAging(): {
  buckets: AgingBucket[];
  invoices: Array<InvoiceRow & { overdueDays: number; outstanding: number }>;
} {
  const today = todayISO();
  const rows = all<InvoiceRow>(
    `SELECT i.*, c.name AS contact_name, c.rfc AS contact_rfc, NULL AS created_by_name
     FROM invoices i JOIN contacts c ON c.id = i.contact_id
     WHERE i.status = 'emitida' AND (i.total_cents - i.paid_cents) > 0
     ORDER BY i.due_date`,
  );
  const labels = ["Por vencer", "1 a 30 días", "31 a 60 días", "61 a 90 días", "Más de 90 días"];
  const buckets = labels.map((label) => ({ label, amount: 0, count: 0 }));
  const enriched = rows.map((invoice) => {
    const overdueDays = diffDays(invoice.due_date, today);
    const outstanding = invoice.total_cents - invoice.paid_cents;
    let index = 0;
    if (overdueDays > 90) index = 4;
    else if (overdueDays > 60) index = 3;
    else if (overdueDays > 30) index = 2;
    else if (overdueDays > 0) index = 1;
    buckets[index].amount += outstanding;
    buckets[index].count += 1;
    return { ...invoice, overdueDays, outstanding };
  });
  return { buckets, invoices: enriched };
}

export type PayrollPeriodRow = PayrollPeriod & {
  gross_cents: number;
  deductions_cents: number;
  net_cents: number;
  employee_count: number;
};

export function getPayrollPeriods(): PayrollPeriodRow[] {
  return all<PayrollPeriodRow>(
    `SELECT p.*,
       COALESCE(SUM(pi.base_cents + pi.extra_perceptions_cents),0) AS gross_cents,
       COALESCE(SUM(pi.isr_cents + pi.imss_cents + pi.infonavit_cents + pi.other_deductions_cents),0) AS deductions_cents,
       COALESCE(SUM(pi.net_cents),0) AS net_cents,
       COUNT(pi.id) AS employee_count
     FROM payroll_periods p
     LEFT JOIN payroll_items pi ON pi.period_id = p.id
     GROUP BY p.id
     ORDER BY p.start_date DESC, p.id DESC`,
  );
}

export function getPayrollPeriod(id: number): PayrollPeriodRow | undefined {
  return one<PayrollPeriodRow>(
    `SELECT p.*,
       COALESCE(SUM(pi.base_cents + pi.extra_perceptions_cents),0) AS gross_cents,
       COALESCE(SUM(pi.isr_cents + pi.imss_cents + pi.infonavit_cents + pi.other_deductions_cents),0) AS deductions_cents,
       COALESCE(SUM(pi.net_cents),0) AS net_cents,
       COUNT(pi.id) AS employee_count
     FROM payroll_periods p
     LEFT JOIN payroll_items pi ON pi.period_id = p.id
     WHERE p.id = ?
     GROUP BY p.id`,
    [id],
  );
}

export function getPayrollItems(periodId: number): PayrollItem[] {
  return all<PayrollItem>(
    `SELECT pi.*, e.name AS employee_name, e.position AS employee_position
     FROM payroll_items pi JOIN employees e ON e.id = pi.employee_id
     WHERE pi.period_id = ? ORDER BY e.name`,
    [periodId],
  );
}

export function getAuditLog(limit = 30): Array<{
  id: number;
  action: string;
  entity: string;
  entity_id: string;
  detail: string;
  created_at: string;
  user_name: string | null;
}> {
  return all(
    `SELECT a.id, a.action, a.entity, a.entity_id, a.detail, a.created_at, u.name AS user_name
     FROM audit_log a LEFT JOIN users u ON u.id = a.user_id
     ORDER BY a.id DESC LIMIT ?`,
    [limit],
  );
}

export function getInventoryMovements(productId: number): Array<{
  id: number;
  date: string;
  type: string;
  qty: number;
  unit_cost_cents: number;
  reason: string;
  reference: string;
  created_at: string;
  account_name: string | null;
  user_name: string | null;
}> {
  return all(
    `SELECT m.id, m.date, m.type, m.qty, m.unit_cost_cents, m.reason, m.reference, m.created_at,
            a.name AS account_name, u.name AS user_name
     FROM inventory_movements m
     LEFT JOIN accounts a ON a.id = m.account_id
     LEFT JOIN users u ON u.id = m.created_by
     WHERE m.product_id = ? ORDER BY m.date DESC, m.id DESC`,
    [productId],
  );
}

export function getProduct(id: number): Product | undefined {
  return one<Product>("SELECT * FROM products WHERE id = ?", [id]);
}

export function getContact(id: number): Contact | undefined {
  return one<Contact>("SELECT * FROM contacts WHERE id = ?", [id]);
}

export function getContactBalances(): Map<number, number> {
  const rows = all<{ contact_id: number; receivable: number }>(
    `SELECT i.contact_id AS contact_id,
            COALESCE(SUM(CASE WHEN i.status = 'emitida' THEN i.total_cents - i.paid_cents ELSE 0 END), 0) AS receivable
     FROM invoices i
     GROUP BY i.contact_id`,
  );
  return new Map(rows.map((row) => [row.contact_id, Number(row.receivable)]));
}

export function getDefaultIsrBrackets(raw: string) {
  return parseIsrBrackets(raw);
}

export function payrollPeriodDays(year: number, month: number, type: "mensual" | "quincenal"): number {
  return type === "quincenal" ? 15 : daysInMonth(year, month);
}

export type MonthlyPerformanceRow = {
  label: string;
  incomeCents: number;
  expenseCents: number;
  profitCents: number;
};

export function getMonthlyPerformance(from: string, to: string): MonthlyPerformanceRow[] {
  const rows: MonthlyPerformanceRow[] = [];
  const start = new Date(`${from}T12:00:00Z`);
  const end = new Date(`${to}T12:00:00Z`);
  const cursor = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth(), 1));
  while (cursor <= end) {
    const year = cursor.getUTCFullYear();
    const month = cursor.getUTCMonth() + 1;
    const range = monthRange(year, month);
    const rangeStart = range.start < from ? from : range.start;
    const rangeEnd = range.end > to ? to : range.end;

    const invoiced = one<{ total: number }>(
      `SELECT COALESCE(SUM(subtotal_cents),0) AS total FROM invoices
       WHERE status IN ('emitida','pagada') AND date BETWEEN ? AND ?`,
      [rangeStart, rangeEnd],
    );
    const other = one<{ total: number }>(
      `SELECT COALESCE(SUM(subtotal_cents),0) AS total FROM transactions
       WHERE type = 'ingreso' AND date BETWEEN ? AND ?`,
      [rangeStart, rangeEnd],
    );
    const expense = one<{ total: number }>(
      `SELECT COALESCE(SUM(subtotal_cents),0) AS total FROM transactions
       WHERE type = 'gasto' AND date BETWEEN ? AND ?`,
      [rangeStart, rangeEnd],
    );
    const incomeCents = Number(invoiced?.total ?? 0) + Number(other?.total ?? 0);
    const expenseCents = Number(expense?.total ?? 0);
    rows.push({
      label: new Intl.DateTimeFormat("es-MX", { month: "short", timeZone: "UTC" }).format(cursor),
      incomeCents,
      expenseCents,
      profitCents: incomeCents - expenseCents,
    });
    cursor.setUTCMonth(cursor.getUTCMonth() + 1);
  }
  return rows;
}

export function getInventorySummary(): {
  valueCents: number;
  productCount: number;
  unitCount: number;
  lowStockCount: number;
  outOfStockCount: number;
} {
  const row = one<{
    value: number;
    products: number;
    units: number;
    low: number;
    out: number;
  }>(
    `SELECT
       COALESCE(SUM(CASE WHEN track_stock = 1 THEN CAST(ROUND(stock * cost_cents) AS INTEGER) ELSE 0 END),0) AS value,
       COUNT(*) AS products,
       COALESCE(SUM(CASE WHEN track_stock = 1 THEN stock ELSE 0 END),0) AS units,
       COALESCE(SUM(CASE WHEN track_stock = 1 AND stock <= min_stock AND stock > 0 THEN 1 ELSE 0 END),0) AS low,
       COALESCE(SUM(CASE WHEN track_stock = 1 AND stock <= 0 THEN 1 ELSE 0 END),0) AS out
     FROM products WHERE active = 1`,
  );
  return {
    valueCents: Number(row?.value ?? 0),
    productCount: Number(row?.products ?? 0),
    unitCount: Number(row?.units ?? 0),
    lowStockCount: Number(row?.low ?? 0),
    outOfStockCount: Number(row?.out ?? 0),
  };
}

