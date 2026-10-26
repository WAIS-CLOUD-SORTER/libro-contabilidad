export type Company = {
  id: number;
  name: string;
  rfc: string;
  regimen_fiscal: string;
  address: string;
  phone: string;
  email: string;
  iva_rate: number;
  invoice_series: string;
  next_invoice_number: number;
  imss_rate: number;
  isr_brackets: string;
  created_at: string;
};

export type Account = {
  id: number;
  name: string;
  type: "efectivo" | "banco" | "tarjeta";
  opening_balance_cents: number;
  active: number;
  created_at: string;
};

export type Category = {
  id: number;
  name: string;
  kind: "ingreso" | "gasto" | "costo";
  archived: number;
  created_at: string;
};

export type Contact = {
  id: number;
  type: "cliente" | "proveedor" | "ambos";
  name: string;
  rfc: string;
  email: string;
  phone: string;
  address: string;
  notes: string;
  archived: number;
  created_at: string;
};

export type Product = {
  id: number;
  sku: string;
  name: string;
  description: string;
  unit: string;
  price_cents: number;
  cost_cents: number;
  iva_rate: number;
  stock: number;
  min_stock: number;
  track_stock: number;
  active: number;
  created_at: string;
};

export type TransactionRow = {
  id: number;
  date: string;
  type: "ingreso" | "gasto";
  category_id: number | null;
  contact_id: number | null;
  account_id: number | null;
  description: string;
  subtotal_cents: number;
  iva_cents: number;
  total_cents: number;
  iva_rate: number;
  method: string;
  reference: string;
  source: string;
  source_id: number | null;
  created_by: number | null;
  created_at: string;
  updated_at: string;
  category_name: string | null;
  category_kind: string | null;
  contact_name: string | null;
  account_name: string | null;
  created_by_name: string | null;
};

export type InvoiceRow = {
  id: number;
  series: string;
  number: number | null;
  contact_id: number;
  date: string;
  due_date: string;
  status: "borrador" | "emitida" | "pagada" | "cancelada";
  subtotal_cents: number;
  iva_cents: number;
  total_cents: number;
  paid_cents: number;
  notes: string;
  payment_terms: string;
  created_by: number | null;
  created_at: string;
  updated_at: string;
  cancelled_reason: string;
  cancelled_at: string | null;
  contact_name: string | null;
  contact_rfc: string | null;
  created_by_name: string | null;
};

export type InvoiceItem = {
  id: number;
  invoice_id: number;
  product_id: number | null;
  position: number;
  description: string;
  qty: number;
  unit_price_cents: number;
  iva_rate: number;
  subtotal_cents: number;
  iva_cents: number;
  total_cents: number;
  unit_cost_cents: number;
  sku: string | null;
  unit: string | null;
};

export type Payment = {
  id: number;
  invoice_id: number;
  date: string;
  amount_cents: number;
  account_id: number | null;
  method: string;
  reference: string;
  notes: string;
  created_by: number | null;
  created_at: string;
  account_name: string | null;
  created_by_name: string | null;
};

export type Employee = {
  id: number;
  name: string;
  rfc: string;
  curp: string;
  nss: string;
  position: string;
  department: string;
  salary_cents: number;
  start_date: string;
  end_date: string | null;
  status: "activo" | "inactivo";
  bank_name: string;
  bank_account: string;
  created_at: string;
};

export type PayrollPeriod = {
  id: number;
  year: number;
  month: number;
  period_type: "mensual" | "quincenal";
  quincena: number;
  start_date: string;
  end_date: string;
  status: "borrador" | "pagada";
  account_id: number | null;
  transaction_id: number | null;
  paid_at: string | null;
  created_by: number | null;
  created_at: string;
};

export type PayrollItem = {
  id: number;
  period_id: number;
  employee_id: number;
  days: number;
  base_cents: number;
  extra_perceptions_cents: number;
  isr_cents: number;
  imss_cents: number;
  infonavit_cents: number;
  other_deductions_cents: number;
  net_cents: number;
  notes: string;
  employee_name: string;
  employee_position: string;
};

export type UserRow = {
  id: number;
  name: string;
  email: string;
  role: "admin" | "contador" | "capturista" | "consulta";
  active: number;
  created_at: string;
  last_login_at: string | null;
};
