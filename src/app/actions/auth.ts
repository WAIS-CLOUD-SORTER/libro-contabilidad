"use server";

import { redirect } from "next/navigation";
import { z } from "zod";
import { audit } from "@/lib/audit";
import {
  changeOwnPassword,
  createSession,
  destroySession,
  getCurrentUser,
  hashPassword,
  userCount,
  verifyPassword,
} from "@/lib/auth";
import { one, run, tx } from "@/lib/db";
import { DEFAULT_ISR_BRACKETS } from "@/lib/payroll";
import type { ActionState } from "@/lib/permissions";
import { emailSchema, firstIssue, passwordSchema } from "@/lib/validation";

const LOGIN_WINDOW_MS = 10 * 60 * 1000;
const LOGIN_MAX_ATTEMPTS = 10;

type Attempt = { count: number; resetAt: number };

const globalAuth = globalThis as unknown as { __loginAttempts?: Map<string, Attempt> };
const attempts = (globalAuth.__loginAttempts ??= new Map<string, Attempt>());

const DUMMY_HASH = hashPassword("contraseña-que-nunca-coincide");

function canAttempt(key: string): boolean {
  const now = Date.now();
  const current = attempts.get(key);
  if (!current || current.resetAt < now) {
    attempts.set(key, { count: 0, resetAt: now + LOGIN_WINDOW_MS });
    return true;
  }
  return current.count < LOGIN_MAX_ATTEMPTS;
}

function recordFailure(key: string): void {
  const now = Date.now();
  const current = attempts.get(key);
  if (!current || current.resetAt < now) {
    attempts.set(key, { count: 1, resetAt: now + LOGIN_WINDOW_MS });
    return;
  }
  current.count += 1;
}

export async function setupAction(_prev: ActionState, formData: FormData): Promise<ActionState> {
  if (userCount() > 0) redirect("/login");

  const schema = z
    .object({
      company: z.string().trim().min(1, "Escribe el nombre de tu negocio.").max(120),
      name: z.string().trim().min(1, "Escribe tu nombre.").max(120),
      email: emailSchema,
      password: passwordSchema,
      confirm: z.string(),
    })
    .refine((data) => data.password === data.confirm, {
      message: "Las contraseñas no coinciden.",
      path: ["confirm"],
    });

  const parsed = schema.safeParse(Object.fromEntries(formData));
  if (!parsed.success) return { error: firstIssue(parsed.error) };
  const data = parsed.data;

  const adminId = tx(() => {
    run(
      `INSERT INTO company (id, name, isr_brackets)
       VALUES (1, ?, ?)
       ON CONFLICT(id) DO UPDATE SET name = excluded.name`,
      [data.company, JSON.stringify(DEFAULT_ISR_BRACKETS)],
    );
    const result = run(
      `INSERT INTO users (name, email, password_hash, role, last_login_at)
       VALUES (?, ?, ?, 'admin', ?)`,
      [data.name, data.email, hashPassword(data.password), new Date().toISOString()],
    );
    audit(result.lastInsertRowid, "setup", "company", 1, { company: data.company });
    return result.lastInsertRowid;
  });

  await createSession(adminId);
  redirect("/panel?bienvenido=1");
}

export async function loginAction(_prev: ActionState, formData: FormData): Promise<ActionState> {
  const schema = z.object({
    email: emailSchema,
    password: z.string().min(1, "Escribe tu contraseña."),
  });
  const parsed = schema.safeParse(Object.fromEntries(formData));
  if (!parsed.success) return { error: firstIssue(parsed.error) };
  const { email, password } = parsed.data;

  if (!canAttempt(email)) {
    return { error: "Demasiados intentos. Espera unos minutos e intenta de nuevo." };
  }

  const user = one<{ id: number; password_hash: string; active: number; role: string }>(
    "SELECT id, password_hash, active, role FROM users WHERE email = ?",
    [email],
  );

  const valid = user ? verifyPassword(password, user.password_hash) : verifyPassword(password, DUMMY_HASH);
  if (!user || !valid || Number(user.active) !== 1) {
    recordFailure(email);
    audit(null, "login_failed", "user", null, { email });
    return { error: "Correo o contraseña incorrectos." };
  }

  attempts.delete(email);
  run("UPDATE users SET last_login_at = ? WHERE id = ?", [new Date().toISOString(), user.id]);
  run("DELETE FROM sessions WHERE user_id = ? AND expires_at <= ?", [
    user.id,
    new Date().toISOString(),
  ]);
  audit(user.id, "login", "user", user.id, null);
  await createSession(user.id);
  redirect("/panel");
}

export async function logoutAction(): Promise<void> {
  const user = await getCurrentUser();
  if (user) audit(user.id, "logout", "user", user.id, null);
  await destroySession();
  redirect("/login");
}

export async function changePasswordAction(
  _prev: ActionState,
  formData: FormData,
): Promise<ActionState> {
  const user = await getCurrentUser();
  if (!user) redirect("/login");

  const schema = z
    .object({
      current: z.string().min(1, "Escribe tu contraseña actual."),
      password: passwordSchema,
      confirm: z.string(),
    })
    .refine((data) => data.password === data.confirm, {
      message: "Las contraseñas nuevas no coinciden.",
      path: ["confirm"],
    });

  const parsed = schema.safeParse(Object.fromEntries(formData));
  if (!parsed.success) return { error: firstIssue(parsed.error) };

  const error = await changeOwnPassword(user.id, parsed.data.current, parsed.data.password);
  if (error) return { error };

  audit(user.id, "change_password", "user", user.id, null);
  await createSession(user.id);
  redirect("/cuenta?ok=1");
}
