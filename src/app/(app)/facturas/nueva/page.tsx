import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { createInvoiceAction } from "@/app/actions/invoices";
import { InvoiceForm } from "@/components/invoice-form";
import { PageHeader } from "@/components/ui";
import { requirePermission } from "@/lib/auth";
import { getCompany, getContacts, getProducts } from "@/lib/data";
import { addDays, todayISO } from "@/lib/dates";

export const dynamic = "force-dynamic";

export const metadata = { title: "Nueva factura" };

export default async function NewInvoicePage() {
  await requirePermission("invoice.write");
  const company = getCompany();
  const contacts = getContacts()
    .filter((contact) => contact.type === "cliente" || contact.type === "ambos")
    .map((contact) => ({ id: contact.id, name: contact.name, rfc: contact.rfc }));
  const products = getProducts().map((product) => ({
    id: product.id,
    name: product.name,
    sku: product.sku,
    unit: product.unit,
    price_cents: product.price_cents,
    iva_rate: product.iva_rate,
    stock: product.stock,
    track_stock: product.track_stock,
  }));
  const today = todayISO();

  return (
    <div>
      <PageHeader
        eyebrow="Facturación"
        title="Nueva factura"
        description="Captura los conceptos y guarda como borrador o emite para asignar folio."
        actions={
          <Link href="/facturas" className="btn btn-secondary">
            <ArrowLeft size={15} />
            Volver
          </Link>
        }
      />
      <InvoiceForm
        action={createInvoiceAction}
        mode="create"
        contacts={contacts}
        products={products}
        defaultIva={company.iva_rate}
        today={today}
        defaultDueDate={addDays(today, 30)}
      />
    </div>
  );
}
