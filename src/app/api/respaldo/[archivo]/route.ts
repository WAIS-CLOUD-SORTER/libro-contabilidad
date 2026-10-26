import fs from "node:fs";
import { getCurrentUser } from "@/lib/auth";
import { resolveBackup } from "@/lib/backups";
import { can } from "@/lib/permissions";

export const dynamic = "force-dynamic";

export async function GET(
  _request: Request,
  { params }: { params: Promise<{ archivo: string }> },
): Promise<Response> {
  const user = await getCurrentUser();
  if (!user) return new Response("No autenticado.", { status: 401 });
  if (!can(user.role, "backup.manage")) return new Response("Sin permiso.", { status: 403 });

  const { archivo } = await params;
  const resolved = resolveBackup(decodeURIComponent(archivo));
  if (!resolved) return new Response("Respaldo no encontrado.", { status: 404 });

  try {
    const file = fs.readFileSync(resolved);
    return new Response(new Uint8Array(file), {
      headers: {
        "Content-Type": "application/octet-stream",
        "Content-Disposition": `attachment; filename="${resolved.split(/[\\/]/).pop()}"`,
        "Content-Length": String(file.byteLength),
        "Cache-Control": "no-store",
      },
    });
  } catch {
    return new Response("No se pudo leer el respaldo.", { status: 500 });
  }
}
