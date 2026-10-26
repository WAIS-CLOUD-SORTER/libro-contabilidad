import { z } from "zod";
import { isValidISODate } from "./dates";

export function fdString(formData: FormData, key: string): string {
  const value = formData.get(key);
  return typeof value === "string" ? value.trim() : "";
}

export function fdNumber(formData: FormData, key: string): number {
  return Number(fdString(formData, key));
}

export function fdId(formData: FormData, key: string): number | null {
  const value = Number(fdString(formData, key));
  return Number.isInteger(value) && value > 0 ? value : null;
}

export function firstIssue(error: z.ZodError): string {
  return error.issues[0]?.message ?? "Revisa los datos e intenta de nuevo.";
}

export const emailSchema = z
  .string()
  .trim()
  .min(1, "Escribe un correo.")
  .email("Escribe un correo válido.")
  .max(160, "El correo es demasiado largo.")
  .transform((value) => value.toLowerCase());

export const passwordSchema = z
  .string()
  .min(8, "La contraseña debe tener al menos 8 caracteres.")
  .max(200, "La contraseña es demasiado larga.");

export const isoDateSchema = z
  .string()
  .trim()
  .refine((value) => isValidISODate(value), "Escribe una fecha válida.");

export const optionalIsoDateSchema = z
  .string()
  .trim()
  .refine((value) => value === "" || isValidISODate(value), "Escribe una fecha válida.");

export const optionalEmailSchema = z
  .string()
  .trim()
  .max(160, "El correo es demasiado largo.")
  .refine(
    (value) => value === "" || z.string().email().safeParse(value).success,
    "Escribe un correo válido.",
  );
