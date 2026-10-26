import Link from "next/link";
import { notFound, redirect } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import { updateTransactionAction } from "@/app/actions/transactions";
import { TransactionForm } from "@/components/transaction-form";
import { PageHeader } from "@/components/ui";
import { requirePermission } from "@/lib/auth";
import { getAccounts, getCategories, getContacts, getTransaction } from "@/lib/data";
import { todayISO } from "@/lib/dates";

export const dynamic = "force-dynamic";

export const metadata = { title: "Editar movimiento" };

export default async function EditTransactionPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  await requirePermission("tx.write");
  const { id } = await params;
  const transactionId = Number(id);
  if (!Number.isInteger(transactionId) || transactionId <= 0) notFound();

  const transaction = getTransaction(transactionId);
  if (!transaction) notFound();
  if (transaction.source !== "manual") redirect("/movimientos");

  const categories = getCategories(true).map((category) => ({
    id: category.id,
    name: category.name,
    kind: category.kind,
  }));
  const contacts = getContacts(true).map((contact) => ({ id: contact.id, name: contact.name }));
  const accounts = getAccounts().map((account) => ({ id: account.id, name: account.name }));

  return (
    <div>
      <PageHeader
        eyebrow="Operación"
        title="Editar movimiento"
        description={`${transaction.description} · ${transaction.type === "ingreso" ? "Ingreso" : "Gasto"}`}
        actions={
          <Link href="/movimientos" className="btn btn-secondary">
            <ArrowLeft size={15} />
            Volver
          </Link>
        }
      />
      <TransactionForm
        action={updateTransactionAction}
        mode="edit"
        categories={categories}
        contacts={contacts}
        accounts={accounts}
        today={todayISO()}
        defaultIva={transaction.iva_rate}
        initial={{
          id: transaction.id,
          date: transaction.date,
          type: transaction.type,
          categoryId: transaction.category_id,
          contactId: transaction.contact_id,
          accountId: transaction.account_id,
          description: transaction.description,
          subtotalCents: transaction.subtotal_cents,
          ivaRate: transaction.iva_rate,
          method: transaction.method,
          reference: transaction.reference,
        }}
      />
    </div>
  );
}
