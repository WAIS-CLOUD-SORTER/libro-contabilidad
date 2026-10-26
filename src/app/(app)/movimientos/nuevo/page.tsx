import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { createTransactionAction } from "@/app/actions/transactions";
import { TransactionForm } from "@/components/transaction-form";
import { PageHeader } from "@/components/ui";
import { requirePermission } from "@/lib/auth";
import { getAccounts, getCategories, getCompany, getContacts } from "@/lib/data";
import { todayISO } from "@/lib/dates";

export const dynamic = "force-dynamic";

export const metadata = { title: "Nuevo movimiento" };

export default async function NewTransactionPage() {
  await requirePermission("tx.write");
  const company = getCompany();
  const categories = getCategories().map((category) => ({
    id: category.id,
    name: category.name,
    kind: category.kind,
  }));
  const contacts = getContacts().map((contact) => ({ id: contact.id, name: contact.name }));
  const accounts = getAccounts().map((account) => ({ id: account.id, name: account.name }));

  return (
    <div>
      <PageHeader
        eyebrow="Operación"
        title="Nuevo movimiento"
        description="Registra un gasto o un ingreso que no pasa por facturación."
        actions={
          <Link href="/movimientos" className="btn btn-secondary">
            <ArrowLeft size={15} />
            Volver
          </Link>
        }
      />
      <TransactionForm
        action={createTransactionAction}
        mode="create"
        categories={categories}
        contacts={contacts}
        accounts={accounts}
        today={todayISO()}
        defaultIva={company.iva_rate}
      />
    </div>
  );
}
