"use server";

import { redirect } from "next/navigation";
import { revalidatePath } from "next/cache";
import { z } from "zod";
import { audit } from "@/lib/audit";
import { hashPassword, requireUser } from "@/lib/auth";
import { one, run } from "@/lib/db";
import { can, type ActionState } from "@/lib/permissions";
import { emailSchema, fdId, fdString, firstIssue, passwordSchema } from "@/lib/validation";

async function admin(): Promise<{ userId: number } | { error: string }> {
  const user = await requireUser();
  if (!can(user.role, "users.manage")) {
    return { error: "No tienes permiso para administrar usuarios." };
  }
  return { userId: user.id };
}

const baseSchema = z.object({
  name: z.string().trim().min(1, "Escribe el nombre del usuario.").max(120),
  email: emailSchema,
  role: z.enum(["admin", "contador", "capturista", "consulta"]),
});

function activeAdminCount(excludeId: number): number {
  const row = one<{ n: number }>(
    "SELECT COUNT(*) AS n FROM users WHERE role = 'admin' AND active = 1 AND id <> ?",
    [excludeId],
  );
  return Number(row?.n ?? 0);
}

export async function createUserAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await admin();
  if ("error" in auth) return auth;

  const parsed = baseSchema
    .extend({
      password: passwordSchema,
      confirm: z.string(),
    })
    .refine((data) => data.password === data.confirm, {
      message: "Las contraseñas no coinciden.",
      path: ["confirm"],
    })
    .safeParse({
      name: fdString(formData, "name"),
      email: fdString(formData, "email"),
      role: fdString(formData, "role"),
      password: fdString(formData, "password"),
      confirm: fdString(formData, "confirm"),
    });
  if (!parsed.success) return { error: firstIssue(parsed.error) };
  const data = parsed.data;

  try {
    const result = run(
      "INSERT INTO users (name, email, password_hash, role, active) VALUES (?, ?, ?, ?, 1)",
      [data.name, data.email, hashPassword(data.password), data.role],
    );
    audit(auth.userId, "create", "user", result.lastInsertRowid, {
      email: data.email,
      role: data.role,
    });
  } catch (error) {
    if (error instanceof Error && error.message.includes("UNIQUE")) {
      return { error: "Ya existe un usuario con ese correo." };
    }
    return { error: "No se pudo crear el usuario." };
  }

  revalidatePath("/usuarios");
  redirect("/usuarios?creado=1");
}

export async function updateUserAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await admin();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Usuario inválido." };

  const password = fdString(formData, "password");
  const parsed = baseSchema.safeParse({
    name: fdString(formData, "name"),
    email: fdString(formData, "email"),
    role: fdString(formData, "role"),
  });
  if (!parsed.success) return { error: firstIssue(parsed.error) };
  const data = parsed.data;
  const active = fdString(formData, "active") === "1" || fdString(formData, "active") === "on";

  if (password && password.length < 8) {
    return { error: "La nueva contraseña debe tener al menos 8 caracteres." };
  }
  if (password && id === auth.userId) {
    return { error: "Cambia tu propia contraseña desde Mi cuenta." };
  }
  if (id === auth.userId && (!active || data.role !== "admin")) {
    return { error: "No puedes quitarte tu propio acceso de administrador." };
  }
  if ((!active || data.role !== "admin") && activeAdminCount(id) === 0) {
    return { error: "Debe quedar al menos un administrador activo." };
  }

  try {
    run(
      "UPDATE users SET name = ?, email = ?, role = ?, active = ? WHERE id = ?",
      [data.name, data.email, data.role, active ? 1 : 0, id],
    );
    if (password) {
      run("UPDATE users SET password_hash = ? WHERE id = ?", [hashPassword(password), id]);
      run("DELETE FROM sessions WHERE user_id = ?", [id]);
    }
    if (!active) run("DELETE FROM sessions WHERE user_id = ?", [id]);
    audit(auth.userId, "update", "user", id, { email: data.email, role: data.role, active });
  } catch (error) {
    if (error instanceof Error && error.message.includes("UNIQUE")) {
      return { error: "Ya existe un usuario con ese correo." };
    }
    return { error: "No se pudo actualizar el usuario." };
  }

  revalidatePath("/usuarios");
  redirect("/usuarios?actualizado=1");
}

export async function toggleUserAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const auth = await admin();
  if ("error" in auth) return auth;
  const id = fdId(formData, "id");
  if (!id) return { error: "Usuario inválido." };
  if (id === auth.userId) return { error: "No puedes desactivar tu propia cuenta." };

  const user = one<{ role: string; active: number }>(
    "SELECT role, active FROM users WHERE id = ?",
    [id],
  );
  if (!user) return { error: "El usuario no existe." };
  const nextActive = Number(user.active) === 1 ? 0 : 1;
  if (nextActive === 0 && user.role === "admin" && activeAdminCount(id) === 0) {
    return { error: "Debe quedar al menos un administrador activo." };
  }

  try {
    run("UPDATE users SET active = ? WHERE id = ?", [nextActive, id]);
    if (nextActive === 0) run("DELETE FROM sessions WHERE user_id = ?", [id]);
    audit(auth.userId, nextActive === 1 ? "restore" : "deactivate", "user", id, null);
  } catch {
    return { error: "No se pudo actualizar el usuario." };
  }

  revalidatePath("/usuarios");
  return { success: nextActive === 1 ? "Usuario activado." : "Usuario desactivado." };
}
