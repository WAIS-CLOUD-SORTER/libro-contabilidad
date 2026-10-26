import { getCurrentUser } from "@/lib/auth";
import {
  getCompany,
  getEmployees,
  getInvoices,
  getPayrollPeriods,
  getProducts,
  getTaxSummary,
  getTransactions,
} from "@/lib/data";
import { all } from "@/lib/db";
import { currentMonthRange, isValidISODate, todayISO } from "@/lib/dates";
import { can, isRole, type Permission } from "@/lib/permissions";

export const dynamic = "force-dynamic";

function money(cents: number): string {
  return (Number(cents || 0) / 100).toFixed(2);
}

function csvValue(value: unknown): string {
  const text = value === null || value === undefined ? "" : String(value);
  if (/[",\r\n]/.test(text)) return `"${text.replaceAll('"', '""')}"`;
  return text;
}

function csv(rows: unknown[][]): string {
  return `\uFEFF${rows.map((row) => row.map(csvValue).join(",")).join("\r\n")}\r\n`;
}

function response(body: string, filename: string): Response {
  return new Response(body, {
    headers: {
      "Content-Type": "text/csv; charset=utf-8",
      "Content-Disposition": `attachment; filename="${filename}"`,
      "Cache-Control": "no-store",
    },
  });
}

function allowed(role: string, permissions: Permission[]): boolean {
  if (!isRole(role)) return false;
  return permissions.some((permission) => can(role, permission));
}

export async function GET(request: Request): Promise<Response> {
  const user = await getCurrentUser();
  if (!user) return new Response("No autenticado.", { status: 401 });

  const url = new URL(request.url);
  const tipo = url.searchParams.get("tipo") ?? "movimientos";
  const rawFrom = url.searchParams.get("desde");
  const rawTo = url.searchParams.get("hasta");
  const from = rawFrom && isValidISODate(rawFrom) ? rawFrom : undefined;
  const to = rawTo && isValidISODate(rawTo) ? rawTo : undefined;
  const suffix = `${from ?? "inicio"}_${to ?? todayISO()}`;

  if (tipo === "movimientos") {
    if (!allowed(user.role, ["reports.view", "tx.write"])) {
      return new Response("Sin permiso.", { status: 403 });
    }
    const { rows } = getTransactions({
      from,
      to,
      type:
        url.searchParams.get("tipo_mov") === "ingreso" || url.searchParams.get("tipo_mov") === "gasto"
          ? (url.searchParams.get("tipo_mov") as string)
          : undefined,
      q: url.searchParams.get("q") ?? undefined,
      limit: 10000,
      offset: 0,
    });
    const body = csv([
      [
        "Fecha",
        "Tipo",
        "Categoría",
        "Contacto",
        "Cuenta",
        "Descripción",
        "Subtotal",
        "IVA",
        "Total",
        "Método",
        "Referencia",
        "Origen",
      ],
      ...rows.map((row) => [
        row.date,
        row.type,
        row.category_name ?? "",
        row.contact_name ?? "",
        row.account_name ?? "",
        row.description,
        money(row.subtotal_cents),
        money(row.iva_cents),
        money(row.total_cents),
        row.method,
        row.reference,
        row.source,
      ]),
    ]);
    return response(body, `movimientos_${suffix}.csv`);
  }

  if (tipo === "facturas") {
    if (!allowed(user.role, ["reports.view", "invoice.write"])) {
      return new Response("Sin permiso.", { status: 403 });
    }
    const { rows } = getInvoices({ from, to, limit: 10000, offset: 0 });
    const body = csv([
      [
        "Folio",
        "Fecha",
        "Vencimiento",
        "Cliente",
        "RFC",
        "Estado",
        "Subtotal",
        "IVA",
        "Total",
        "Pagado",
        "Saldo",
        "Notas",
      ],
      ...rows.map((row) => [
        row.number ? `${row.series}-${String(row.number).padStart(4, "0")}` : `Borrador-${row.id}`,
        row.date,
        row.due_date,
        row.contact_name ?? "",
        row.contact_rfc ?? "",
        row.status,
        money(row.subtotal_cents),
        money(row.iva_cents),
        money(row.total_cents),
        money(row.paid_cents),
        money(row.total_cents - row.paid_cents),
        row.notes,
      ]),
    ]);
    return response(body, `facturas_${suffix}.csv`);
  }

  if (tipo === "inventario") {
    if (!allowed(user.role, ["reports.view", "inventory.write", "products.write"])) {
      return new Response("Sin permiso.", { status: 403 });
    }
    const products = getProducts(true);
    const body = csv([
      [
        "SKU",
        "Producto",
        "Unidad",
        "Precio",
        "Costo",
        "Stock",
        "Mínimo",
        "Valor inventario",
        "Controla stock",
        "Activo",
      ],
      ...products.map((product) => [
        product.sku,
        product.name,
        product.unit,
        money(product.price_cents),
        money(product.cost_cents),
        product.stock,
        product.min_stock,
        money(Math.round(product.stock * product.cost_cents)),
        product.track_stock === 1 ? "Sí" : "No",
        product.active === 1 ? "Sí" : "No",
      ]),
    ]);
    return response(body, `inventario_${todayISO()}.csv`);
  }

  if (tipo === "empleados") {
    if (!allowed(user.role, ["payroll.view"])) {
      return new Response("Sin permiso.", { status: 403 });
    }
    const employees = getEmployees(true);
    const body = csv([
      [
        "Nombre",
        "RFC",
        "CURP",
        "NSS",
        "Puesto",
        "Departamento",
        "Sueldo mensual",
        "Ingreso",
        "Baja",
        "Estatus",
        "Banco",
        "Cuenta",
      ],
      ...employees.map((employee) => [
        employee.name,
        employee.rfc,
        employee.curp,
        employee.nss,
        employee.position,
        employee.department,
        money(employee.salary_cents),
        employee.start_date,
        employee.end_date ?? "",
        employee.status,
        employee.bank_name,
        employee.bank_account,
      ]),
    ]);
    return response(body, `empleados_${todayISO()}.csv`);
  }

  if (tipo === "nomina") {
    if (!allowed(user.role, ["payroll.view"])) {
      return new Response("Sin permiso.", { status: 403 });
    }
    const periodId = Number(url.searchParams.get("periodo")) || null;
    const rows = all<{
      year: number;
      month: number;
      period_type: string;
      quincena: number;
      start_date: string;
      end_date: string;
      status: string;
      employee_name: string;
      position: string;
      days: number;
      base_cents: number;
      extra_perceptions_cents: number;
      isr_cents: number;
      imss_cents: number;
      infonavit_cents: number;
      other_deductions_cents: number;
      net_cents: number;
    }>(
      `SELECT p.year, p.month, p.period_type, p.quincena, p.start_date, p.end_date, p.status,
              e.name AS employee_name, e.position, pi.days, pi.base_cents,
              pi.extra_perceptions_cents, pi.isr_cents, pi.imss_cents, pi.infonavit_cents,
              pi.other_deductions_cents, pi.net_cents
       FROM payroll_items pi
       JOIN payroll_periods p ON p.id = pi.period_id
       JOIN employees e ON e.id = pi.employee_id
       ${periodId ? "WHERE p.id = ?" : ""}
       ORDER BY p.start_date DESC, e.name`,
      periodId ? [periodId] : [],
    );
    const body = csv([
      [
        "Año",
        "Mes",
        "Tipo",
        "Quincena",
        "Inicio",
        "Fin",
        "Estado",
        "Empleado",
        "Puesto",
        "Días",
        "Base",
        "Extra",
        "ISR",
        "IMSS",
        "Infonavit",
        "Otras deducciones",
        "Neto",
      ],
      ...rows.map((row) => [
        row.year,
        row.month,
        row.period_type,
        row.quincena,
        row.start_date,
        row.end_date,
        row.status,
        row.employee_name,
        row.position,
        row.days,
        money(row.base_cents),
        money(row.extra_perceptions_cents),
        money(row.isr_cents),
        money(row.imss_cents),
        money(row.infonavit_cents),
        money(row.other_deductions_cents),
        money(row.net_cents),
      ]),
    ]);
    return response(body, `nomina_${todayISO()}.csv`);
  }

  if (tipo === "iva") {
    if (!allowed(user.role, ["tax.view"])) {
      return new Response("Sin permiso.", { status: 403 });
    }
    const fallback = currentMonthRange();
    const start = from ?? fallback.start;
    const end = to ?? fallback.end;
    const summary = getTaxSummary(start, end);
    const body = csv([
      ["Fecha", "Tipo", "Folio", "Contraparte", "Concepto", "Base", "IVA", "Total"],
      ...summary.invoices.map((row) => [
        row.date,
        "Factura",
        row.number ? `${row.series}-${String(row.number).padStart(4, "0")}` : `Borrador-${row.id}`,
        row.contact_name ?? "",
        row.notes || "Venta facturada",
        money(row.subtotal_cents),
        money(row.iva_cents),
        money(row.total_cents),
      ]),
      ...summary.incomeTransactions.map((row) => [
        row.date,
        "Ingreso",
        `MOV-${row.id}`,
        row.contact_name ?? "",
        row.description,
        money(row.subtotal_cents),
        money(row.iva_cents),
        money(row.total_cents),
      ]),
      ...summary.expenses.map((row) => [
        row.date,
        "Gasto",
        `MOV-${row.id}`,
        row.contact_name ?? "",
        row.description,
        money(row.subtotal_cents),
        money(row.iva_cents),
        money(row.total_cents),
      ]),
    ]);
    return response(body, `iva_${start}_${end}.csv`);
  }

  if (tipo === "resumen") {
    if (!allowed(user.role, ["reports.view"])) {
      return new Response("Sin permiso.", { status: 403 });
    }
    const periods = getPayrollPeriods();
    const company = getCompany();
    const body = csv([
      ["Concepto", "Valor"],
      ["Negocio", company.name],
      ["RFC", company.rfc],
      ["Generado", new Date().toISOString()],
      ["Periodos de nómina", periods.length],
      ["Nómina pagada (histórico)", money(periods.filter((p) => p.status === "pagada").reduce((s, p) => s + Number(p.net_cents), 0))],
    ]);
    return response(body, `resumen_${todayISO()}.csv`);
  }

  return new Response("Tipo de exportación no válido.", { status: 400 });
}
