export type Role = "admin" | "contador" | "capturista" | "consulta";

export type Permission =
  | "users.manage"
  | "settings.manage"
  | "backup.manage"
  | "audit.view"
  | "payroll.view"
  | "payroll.write"
  | "tax.view"
  | "reports.view"
  | "tx.write"
  | "invoice.write"
  | "inventory.write"
  | "contacts.write"
  | "products.write"
  | "accounts.write";

const MATRIX: Record<Role, Permission[]> = {
  admin: [
    "users.manage",
    "settings.manage",
    "backup.manage",
    "audit.view",
    "payroll.view",
    "payroll.write",
    "tax.view",
    "reports.view",
    "tx.write",
    "invoice.write",
    "inventory.write",
    "contacts.write",
    "products.write",
    "accounts.write",
  ],
  contador: [
    "payroll.view",
    "payroll.write",
    "tax.view",
    "reports.view",
    "tx.write",
    "invoice.write",
    "inventory.write",
    "contacts.write",
    "products.write",
    "accounts.write",
    "audit.view",
  ],
  capturista: ["tx.write", "invoice.write", "inventory.write", "contacts.write", "products.write"],
  consulta: ["tax.view", "reports.view"],
};

export const ROLE_LABELS: Record<Role, string> = {
  admin: "Administrador",
  contador: "Contador",
  capturista: "Capturista",
  consulta: "Consulta",
};

export const ROLE_DESCRIPTIONS: Record<Role, string> = {
  admin: "Acceso total, usuarios, respaldos y configuración.",
  contador: "Operación contable, facturas, nómina, impuestos y reportes.",
  capturista: "Captura de movimientos, facturas, contactos e inventario.",
  consulta: "Solo lectura de reportes, impuestos y operación.",
};

export function can(role: Role, permission: Permission): boolean {
  return MATRIX[role].includes(permission);
}

export function isRole(value: string): value is Role {
  return value === "admin" || value === "contador" || value === "capturista" || value === "consulta";
}

export type SessionUser = {
  id: number;
  name: string;
  email: string;
  role: Role;
};

export type ActionState = { error?: string; success?: string } | null;
