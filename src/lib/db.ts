import fs from "node:fs";
import path from "node:path";
import { DatabaseSync } from "node:sqlite";
import { SCHEMA_SQL } from "./schema";

export const DATA_DIR = process.env.DATA_DIR?.trim()
  ? path.resolve(process.env.DATA_DIR)
  : path.join(process.cwd(), "data");

export const DB_PATH = process.env.DB_PATH?.trim()
  ? path.resolve(process.env.DB_PATH)
  : path.join(DATA_DIR, "contabilidad.db");

export const BACKUP_DIR = path.join(DATA_DIR, "respaldos");

type Param = string | number | null;

function openDatabase(): DatabaseSync {
  fs.mkdirSync(path.dirname(DB_PATH), { recursive: true });
  fs.mkdirSync(BACKUP_DIR, { recursive: true });

  const database = new DatabaseSync(DB_PATH, { timeout: 5000 });
  database.exec("PRAGMA journal_mode = WAL");
  database.exec("PRAGMA foreign_keys = ON");
  database.exec("PRAGMA synchronous = NORMAL");
  database.exec(SCHEMA_SQL);
  database.exec("PRAGMA user_version = 1");

  seedDefaults(database);
  return database;
}

function seedDefaults(database: DatabaseSync): void {
  const categories: Array<[string, string]> = [
    ["Ventas", "ingreso"],
    ["Servicios", "ingreso"],
    ["Otros ingresos", "ingreso"],
    ["Renta", "gasto"],
    ["Servicios (luz, agua, internet)", "gasto"],
    ["Papelería y oficina", "gasto"],
    ["Publicidad", "gasto"],
    ["Transporte", "gasto"],
    ["Nómina", "gasto"],
    ["Impuestos y cuotas", "gasto"],
    ["Compras de inventario", "gasto"],
    ["Mantenimiento", "gasto"],
    ["Otros gastos", "gasto"],
  ];

  const categoriesCount = database.prepare("SELECT COUNT(*) AS n FROM categories").get() as {
    n: number;
  };
  if (Number(categoriesCount.n) === 0) {
    const insertCategory = database.prepare("INSERT INTO categories (name, kind) VALUES (?, ?)");
    for (const [name, kind] of categories) insertCategory.run(name, kind);
  }

  const accountsCount = database.prepare("SELECT COUNT(*) AS n FROM accounts").get() as {
    n: number;
  };
  if (Number(accountsCount.n) === 0) {
    const insertAccount = database.prepare(
      "INSERT INTO accounts (name, type, opening_balance_cents) VALUES (?, ?, 0)",
    );
    insertAccount.run("Caja", "efectivo");
    insertAccount.run("Banco", "banco");
  }
}

const globalForDb = globalThis as unknown as { __contabilidadDb?: DatabaseSync };

export const db: DatabaseSync = globalForDb.__contabilidadDb ?? openDatabase();

if (process.env.NODE_ENV !== "production") {
  globalForDb.__contabilidadDb = db;
}

export function all<T>(sql: string, params: Param[] = []): T[] {
  return db.prepare(sql).all(...params) as unknown as T[];
}

export function one<T>(sql: string, params: Param[] = []): T | undefined {
  return db.prepare(sql).get(...params) as unknown as T | undefined;
}

export function run(
  sql: string,
  params: Param[] = [],
): { changes: number; lastInsertRowid: number } {
  const result = db.prepare(sql).run(...params);
  return {
    changes: Number(result.changes),
    lastInsertRowid: Number(result.lastInsertRowid),
  };
}

let txDepth = 0;

/** Transacción atómica. Si ya hay una activa, reutiliza la existente. */
export function tx<T>(fn: () => T): T {
  if (txDepth > 0) return fn();
  db.exec("BEGIN IMMEDIATE");
  txDepth += 1;
  try {
    const result = fn();
    db.exec("COMMIT");
    return result;
  } catch (error) {
    db.exec("ROLLBACK");
    throw error;
  } finally {
    txDepth -= 1;
  }
}
