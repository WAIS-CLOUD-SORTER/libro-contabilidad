import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { updateInvoiceAction } from "@/app/actions/invoices";
import { InvoiceForm } from "@/components/invoice-form";
import { PageHeader } from "@/components/ui";
import { requirePermission } from "@/lib/auth";
import { getCompany, getContacts, getInvoice, getInvoiceItems, getProducts } from "@/lib/data";
import { todayISO } from "@/lib/dates";

export const dynamic = "force-dynamic";

export const metadata = { title: "Editar factura" };

export default async function EditInvoicePage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  await requirePermission("invoice.write");
  const { id } = await params;
  const invoiceId = Number(id);
  if (!Number.isInteger(invoiceId) || invoiceId <= 0) notFound();

  const invoice = getInvoice(invoiceId);
  if (!invoice) notFound();
  if (invoice.status !== "borrador") redirect(`/facturas/${invoice.id}`);

  const items = getInvoiceItems(invoiceId);
  const company = getCompany();
  const contacts = getContacts(true)
    .filter((contact) => contact.type === "cliente" || contact.type === "ambos" || contact.id === invoice.contact_id)
    .map((contact) => ({ id: contact.id, name: contact.name, rfc: contact.rfc }));
  const products = getProducts(true).map((product) => ({
    id: product.id,
    name: product.name,
    sku: product.sku,
    unit: product.unit,
    price_cents: product.price_cents,
    iva_rate: product.iva_rate,
    stock: product.stock,
    track_stock: product.track_stock,
  }));

  return (
    <div>
      <PageHeader
        eyebrow="Facturación"
        title={`Editar borrador #${invoice.id}`}
        description="Puedes ajustar conceptos y datos mientras la factura no se haya emitido."
        actions={
          <Link href={`/facturas/${invoice.id}`} className="btn btn-secondary">
            <ArrowLeft size={15} />
            Volver
          </Link>
        }
      />
      <InvoiceForm
        action={updateInvoiceAction}
        mode="edit"
        contacts={contacts}
        products={products}
        defaultIva={company.iva_rate}
        today={todayISO()}
        defaultDueDate={invoice.due_date}
        initial={{
          id: invoice.id,
          contactId: invoice.contact_id,
          date: invoice.date,
          dueDate: invoice.due_date,
          paymentTerms: invoice.payment_terms,
          notes: invoice.notes,
          lines: items.map((item) => ({
            productId: item.product_id,
            description: item.description,
            qty: item.qty,
            unitPriceCents: item.unit_price_cents,
            ivaRate: item.iva_rate,
          })),
        }}
      />
    </div>
  );
}
