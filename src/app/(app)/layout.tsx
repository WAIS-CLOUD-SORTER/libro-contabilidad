import { AppShell } from "@/components/app-shell";
import { requireUser } from "@/lib/auth";
import { getCompany } from "@/lib/data";
import { formatDate, todayISO } from "@/lib/dates";

export const dynamic = "force-dynamic";

export default async function AppLayout({ children }: { children: React.ReactNode }) {
  const user = await requireUser();
  const company = getCompany();

  return (
    <AppShell user={user} companyName={company.name} todayLabel={formatDate(todayISO())}>
      {children}
    </AppShell>
  );
}
