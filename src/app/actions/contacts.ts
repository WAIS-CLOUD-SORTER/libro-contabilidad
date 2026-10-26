"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { z } from "zod";
import { audit } from "@/lib/audit";
import { requireUser } from "@/lib/auth";
import { one, run } from "@/lib/db";
import { can, type ActionState } from "@/lib/permissions";
import { fdId, fdString, firstIssue, optionalEmailSchema } from "@/lib/validation";

const contactSchema = z.object({
  name: z.string().trim().min(1, "Escribe el nombre del contacto.").max(160),
  type: z.enum(["cliente", "proveedor", "ambos"]),
  rfc: z.string().trim().max(20, "El RFC es demasiado largo."),
  email: optionalEmailSchema,
  phone: z.string().trim().max(60, "El teléfono es demasiado largo."),
  address: z.string().trim().max(500, "La dirección es demasiado larga."),
  notes: z.string().trim().max(1000, "Las notas son demasiado largas."),
});

async function writer(): Promise<{ userId: number } | { error: string }> {
  const user = await requireUser();
  if (!can(user.role, "contacts.write")) {
    return { error: "No tienes permiso para modificar contactos." };
  }
  return { userId: user.id };
}

export async function saveContactAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;

  const id = fdId(formData, "id");
  const parsed = contactSchema.safeParse({
    name: fdString(formData, "name"),
    type: fdString(formData, "type"),
    rfc: fdString(formData, "rfc").toUpperCase(),
    email: fdString(formData, "email"),
    phone: fdString(formData, "phone"),
    address: fdString(formData, "address"),
    notes: fdString(formData, "notes"),
  });
  if (!parsed.success) return { error: firstIssue(parsed.error) };
  const data = parsed.data;

  try {
    if (id) {
      const current = one<{ id: number }>("SELECT id FROM contacts WHERE id = ?", [id]);
      if (!current) return { error: "El contacto no existe." };
      run(
        `UPDATE contacts SET type = ?, name = ?, rfc = ?, email = ?, phone = ?, address = ?, notes = ?
         WHERE id = ?`,
        [data.type, data.name, data.rfc, data.email, data.phone, data.address, data.notes, id],
      );
      audit(auth.userId, "update", "contact", id, { name: data.name });
    } else {
      const result = run(
        `INSERT INTO contacts (type, name, rfc, email, phone, address, notes)
         VALUES (?, ?, ?, ?, ?, ?, ?)`,
        [data.type, data.name, data.rfc, data.email, data.phone, data.address, data.notes],
      );
      audit(auth.userId, "create", "contact", result.lastInsertRowid, { name: data.name });
    }
  } catch {
    return { error: "No se pudo guardar el contacto." };
  }

  revalidatePath("/contactos");
  revalidatePath("/facturas/nueva");
  redirect("/contactos");
}

export async function toggleContactArchiveAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await writer();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Contacto inválido." };
  const archived = fdString(formData, "archived") === "1" ? 1 : 0;

  try {
    run("UPDATE contacts SET archived = ? WHERE id = ?", [archived, id]);
    audit(auth.userId, archived ? "archive" : "restore", "contact", id, null);
  } catch {
    return { error: "No se pudo actualizar el contacto." };
  }

  revalidatePath("/contactos");
  redirect("/contactos");
}
