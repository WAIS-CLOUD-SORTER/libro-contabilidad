import { createHash, randomBytes, scryptSync, timingSafeEqual } from "node:crypto";
import { cache } from "react";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { one, run } from "./db";
import { can, type Permission, type Role, type SessionUser } from "./permissions";

const COOKIE_NAME = "contabilidad_sesion";
const SESSION_DAYS = 30;

function sha256(value: string): string {
  return createHash("sha256").update(value).digest("hex");
}

export function hashPassword(password: string): string {
  const salt = randomBytes(16);
  const hash = scryptSync(password, salt, 64, { N: 16384, r: 8, p: 1 });
  return `scrypt$16384$8$1$${salt.toString("hex")}$${hash.toString("hex")}`;
}

export function verifyPassword(password: string, stored: string): boolean {
  try {
    const [scheme, n, r, p, saltHex, hashHex] = stored.split("$");
    if (scheme !== "scrypt" || !saltHex || !hashHex) return false;
    const expected = Buffer.from(hashHex, "hex");
    const actual = scryptSync(password, Buffer.from(saltHex, "hex"), expected.length, {
      N: Number(n),
      r: Number(r),
      p: Number(p),
    });
    return expected.length === actual.length && timingSafeEqual(expected, actual);
  } catch {
    return false;
  }
}

type SessionRow = {
  id: number;
  name: string;
  email: string;
  role: Role;
};

export const getCurrentUser = cache(async (): Promise<SessionUser | null> => {
  const store = await cookies();
  const token = store.get(COOKIE_NAME)?.value;
  if (!token) return null;

  const row = one<SessionRow>(
    `SELECT u.id, u.name, u.email, u.role
     FROM sessions s
     JOIN users u ON u.id = s.user_id
     WHERE s.token_hash = ? AND s.expires_at > ? AND u.active = 1`,
    [sha256(token), new Date().toISOString()],
  );
  if (!row) return null;
  return { id: row.id, name: row.name, email: row.email, role: row.role };
});

export async function requireUser(): Promise<SessionUser> {
  const user = await getCurrentUser();
  if (!user) redirect("/login");
  return user;
}

export async function requirePermission(permission: Permission): Promise<SessionUser> {
  const user = await requireUser();
  if (!can(user.role, permission)) redirect("/panel?denegado=1");
  return user;
}

export async function createSession(userId: number): Promise<void> {
  const token = randomBytes(32).toString("base64url");
  const expiresAt = new Date(Date.now() + SESSION_DAYS * 86_400_000);
  run("DELETE FROM sessions WHERE expires_at <= ?", [new Date().toISOString()]);
  run(
    `INSERT INTO sessions (token_hash, user_id, created_at, expires_at)
     VALUES (?, ?, ?, ?)`,
    [sha256(token), userId, new Date().toISOString(), expiresAt.toISOString()],
  );

  const store = await cookies();
  store.set(COOKIE_NAME, token, {
    httpOnly: true,
    sameSite: "lax",
    secure: process.env.COOKIE_SECURE === "1",
    path: "/",
    expires: expiresAt,
  });
}

export async function destroySession(): Promise<void> {
  const store = await cookies();
  const token = store.get(COOKIE_NAME)?.value;
  if (token) run("DELETE FROM sessions WHERE token_hash = ?", [sha256(token)]);
  store.delete(COOKIE_NAME);
}

export function userCount(): number {
  const row = one<{ n: number }>("SELECT COUNT(*) AS n FROM users");
  return Number(row?.n ?? 0);
}

export async function changeOwnPassword(
  userId: number,
  currentPassword: string,
  newPassword: string,
): Promise<string | null> {
  const user = one<{ password_hash: string }>("SELECT password_hash FROM users WHERE id = ?", [
    userId,
  ]);
  if (!user) return "Usuario no encontrado.";
  if (!verifyPassword(currentPassword, user.password_hash)) {
    return "La contraseña actual no es correcta.";
  }
  if (newPassword.length < 8) return "La nueva contraseña debe tener al menos 8 caracteres.";
  run("UPDATE users SET password_hash = ? WHERE id = ?", [hashPassword(newPassword), userId]);
  // Cierra las demás sesiones del usuario.
  run("DELETE FROM sessions WHERE user_id = ?", [userId]);
  return null;
}
