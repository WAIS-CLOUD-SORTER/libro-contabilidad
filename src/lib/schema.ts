export const SCHEMA_SQL = `
CREATE TABLE IF NOT EXISTS company (
  id INTEGER PRIMARY KEY CHECK (id = 1),
  name TEXT NOT NULL,
  rfc TEXT NOT NULL DEFAULT '',
  regimen_fiscal TEXT NOT NULL DEFAULT '',
  address TEXT NOT NULL DEFAULT '',
  phone TEXT NOT NULL DEFAULT '',
  email TEXT NOT NULL DEFAULT '',
  iva_rate REAL NOT NULL DEFAULT 0.16,
  invoice_series TEXT NOT NULL DEFAULT 'A',
  next_invoice_number INTEGER NOT NULL DEFAULT 1,
  imss_rate REAL NOT NULL DEFAULT 0.025,
  isr_brackets TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS users (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  email TEXT NOT NULL COLLATE NOCASE UNIQUE,
  password_hash TEXT NOT NULL,
  role TEXT NOT NULL CHECK (role IN ('admin','contador','capturista','consulta')),
  active INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  last_login_at TEXT
);

CREATE TABLE IF NOT EXISTS sessions (
  token_hash TEXT PRIMARY KEY,
  user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  created_at TEXT NOT NULL,
  expires_at TEXT NOT NULL,
  user_agent TEXT NOT NULL DEFAULT ''
);
CREATE INDEX IF NOT EXISTS idx_sessions_expires ON sessions(expires_at);

CREATE TABLE IF NOT EXISTS audit_log (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  user_id INTEGER REFERENCES users(id) ON DELETE SET NULL,
  action TEXT NOT NULL,
  entity TEXT NOT NULL,
  entity_id TEXT NOT NULL DEFAULT '',
  detail TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_audit_created ON audit_log(created_at);

CREATE TABLE IF NOT EXISTS accounts (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL UNIQUE,
  type TEXT NOT NULL CHECK (type IN ('efectivo','banco','tarjeta')),
  opening_balance_cents INTEGER NOT NULL DEFAULT 0,
  active INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS categories (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  kind TEXT NOT NULL CHECK (kind IN ('ingreso','gasto','costo')),
  archived INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  UNIQUE (name, kind)
);

CREATE TABLE IF NOT EXISTS contacts (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  type TEXT NOT NULL CHECK (type IN ('cliente','proveedor','ambos')),
  name TEXT NOT NULL,
  rfc TEXT NOT NULL DEFAULT '',
  email TEXT NOT NULL DEFAULT '',
  phone TEXT NOT NULL DEFAULT '',
  address TEXT NOT NULL DEFAULT '',
  notes TEXT NOT NULL DEFAULT '',
  archived INTEGER NOT NULL DEFAULT 0,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS products (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  sku TEXT NOT NULL DEFAULT '',
  name TEXT NOT NULL,
  description TEXT NOT NULL DEFAULT '',
  unit TEXT NOT NULL DEFAULT 'pieza',
  price_cents INTEGER NOT NULL DEFAULT 0,
  cost_cents INTEGER NOT NULL DEFAULT 0,
  iva_rate REAL NOT NULL DEFAULT 0.16,
  stock REAL NOT NULL DEFAULT 0,
  min_stock REAL NOT NULL DEFAULT 0,
  track_stock INTEGER NOT NULL DEFAULT 1,
  active INTEGER NOT NULL DEFAULT 1,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_products_sku ON products(sku) WHERE sku <> '';

CREATE TABLE IF NOT EXISTS transactions (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  date TEXT NOT NULL,
  type TEXT NOT NULL CHECK (type IN ('ingreso','gasto')),
  category_id INTEGER REFERENCES categories(id) ON DELETE SET NULL,
  contact_id INTEGER REFERENCES contacts(id) ON DELETE SET NULL,
  account_id INTEGER REFERENCES accounts(id) ON DELETE SET NULL,
  description TEXT NOT NULL,
  subtotal_cents INTEGER NOT NULL,
  iva_cents INTEGER NOT NULL DEFAULT 0,
  total_cents INTEGER NOT NULL,
  iva_rate REAL NOT NULL DEFAULT 0,
  method TEXT NOT NULL DEFAULT '',
  reference TEXT NOT NULL DEFAULT '',
  source TEXT NOT NULL DEFAULT 'manual',
  source_id INTEGER,
  created_by INTEGER REFERENCES users(id) ON DELETE SET NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_tx_date ON transactions(date);
CREATE INDEX IF NOT EXISTS idx_tx_type ON transactions(type);

CREATE TABLE IF NOT EXISTS inventory_movements (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  product_id INTEGER NOT NULL REFERENCES products(id) ON DELETE CASCADE,
  date TEXT NOT NULL,
  type TEXT NOT NULL CHECK (type IN ('entrada','salida','ajuste')),
  qty REAL NOT NULL,
  unit_cost_cents INTEGER NOT NULL DEFAULT 0,
  reason TEXT NOT NULL DEFAULT '',
  reference TEXT NOT NULL DEFAULT '',
  account_id INTEGER REFERENCES accounts(id) ON DELETE SET NULL,
  transaction_id INTEGER REFERENCES transactions(id) ON DELETE SET NULL,
  created_by INTEGER REFERENCES users(id) ON DELETE SET NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_inv_product ON inventory_movements(product_id, date);

CREATE TABLE IF NOT EXISTS invoices (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  series TEXT NOT NULL,
  number INTEGER,
  contact_id INTEGER NOT NULL REFERENCES contacts(id),
  date TEXT NOT NULL,
  due_date TEXT NOT NULL,
  status TEXT NOT NULL CHECK (status IN ('borrador','emitida','pagada','cancelada')),
  subtotal_cents INTEGER NOT NULL DEFAULT 0,
  iva_cents INTEGER NOT NULL DEFAULT 0,
  total_cents INTEGER NOT NULL DEFAULT 0,
  paid_cents INTEGER NOT NULL DEFAULT 0,
  notes TEXT NOT NULL DEFAULT '',
  payment_terms TEXT NOT NULL DEFAULT '',
  created_by INTEGER REFERENCES users(id) ON DELETE SET NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at TEXT NOT NULL DEFAULT (datetime('now')),
  cancelled_reason TEXT NOT NULL DEFAULT '',
  cancelled_at TEXT,
  UNIQUE (series, number)
);
CREATE INDEX IF NOT EXISTS idx_inv_date ON invoices(date);
CREATE INDEX IF NOT EXISTS idx_inv_status ON invoices(status);

CREATE TABLE IF NOT EXISTS invoice_items (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  invoice_id INTEGER NOT NULL REFERENCES invoices(id) ON DELETE CASCADE,
  product_id INTEGER REFERENCES products(id) ON DELETE SET NULL,
  position INTEGER NOT NULL DEFAULT 0,
  description TEXT NOT NULL,
  qty REAL NOT NULL,
  unit_price_cents INTEGER NOT NULL,
  iva_rate REAL NOT NULL DEFAULT 0.16,
  subtotal_cents INTEGER NOT NULL,
  iva_cents INTEGER NOT NULL DEFAULT 0,
  total_cents INTEGER NOT NULL,
  unit_cost_cents INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_items_invoice ON invoice_items(invoice_id);

CREATE TABLE IF NOT EXISTS payments (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  invoice_id INTEGER NOT NULL REFERENCES invoices(id) ON DELETE CASCADE,
  date TEXT NOT NULL,
  amount_cents INTEGER NOT NULL,
  account_id INTEGER REFERENCES accounts(id) ON DELETE SET NULL,
  method TEXT NOT NULL DEFAULT '',
  reference TEXT NOT NULL DEFAULT '',
  notes TEXT NOT NULL DEFAULT '',
  created_by INTEGER REFERENCES users(id) ON DELETE SET NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_payments_invoice ON payments(invoice_id);

CREATE TABLE IF NOT EXISTS employees (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  name TEXT NOT NULL,
  rfc TEXT NOT NULL DEFAULT '',
  curp TEXT NOT NULL DEFAULT '',
  nss TEXT NOT NULL DEFAULT '',
  position TEXT NOT NULL DEFAULT '',
  department TEXT NOT NULL DEFAULT '',
  salary_cents INTEGER NOT NULL DEFAULT 0,
  start_date TEXT NOT NULL,
  end_date TEXT,
  status TEXT NOT NULL DEFAULT 'activo' CHECK (status IN ('activo','inactivo')),
  bank_name TEXT NOT NULL DEFAULT '',
  bank_account TEXT NOT NULL DEFAULT '',
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS payroll_periods (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  year INTEGER NOT NULL,
  month INTEGER NOT NULL,
  period_type TEXT NOT NULL CHECK (period_type IN ('mensual','quincenal')),
  quincena INTEGER NOT NULL DEFAULT 0,
  start_date TEXT NOT NULL,
  end_date TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'borrador' CHECK (status IN ('borrador','pagada')),
  account_id INTEGER REFERENCES accounts(id) ON DELETE SET NULL,
  transaction_id INTEGER REFERENCES transactions(id) ON DELETE SET NULL,
  paid_at TEXT,
  created_by INTEGER REFERENCES users(id) ON DELETE SET NULL,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  UNIQUE (year, month, period_type, quincena)
);

CREATE TABLE IF NOT EXISTS payroll_items (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  period_id INTEGER NOT NULL REFERENCES payroll_periods(id) ON DELETE CASCADE,
  employee_id INTEGER NOT NULL REFERENCES employees(id),
  days REAL NOT NULL DEFAULT 0,
  base_cents INTEGER NOT NULL DEFAULT 0,
  extra_perceptions_cents INTEGER NOT NULL DEFAULT 0,
  isr_cents INTEGER NOT NULL DEFAULT 0,
  imss_cents INTEGER NOT NULL DEFAULT 0,
  infonavit_cents INTEGER NOT NULL DEFAULT 0,
  other_deductions_cents INTEGER NOT NULL DEFAULT 0,
  net_cents INTEGER NOT NULL DEFAULT 0,
  notes TEXT NOT NULL DEFAULT '',
  UNIQUE (period_id, employee_id)
);
`;
