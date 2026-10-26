import { NextResponse } from "next/server";

export const dynamic = "force-dynamic";

export function GET(): NextResponse {
  return NextResponse.json(
    { ok: true, app: "libro", name: "Libro · Contabilidad" },
    { headers: { "Cache-Control": "no-store" } },
  );
}
