import { redirect } from "next/navigation";
import { getCurrentUser, userCount } from "@/lib/auth";

export const dynamic = "force-dynamic";

export default async function HomePage() {
  const user = await getCurrentUser();
  if (user) redirect("/panel");
  if (userCount() === 0) redirect("/setup");
  redirect("/login");
}
