import fs from "node:fs";
import path from "node:path";
import { backup } from "node:sqlite";
import { BACKUP_DIR, db } from "./db";
import { TZ } from "./dates";

export type BackupFile = {
  name: string;
  size: number;
  modifiedAt: string;
};

const NAME_RE = /^[A-Za-z0-9._-]+\.db$/;

export function listBackups(): BackupFile[] {
  if (!fs.existsSync(BACKUP_DIR)) return [];
  return fs
    .readdirSync(BACKUP_DIR, { withFileTypes: true })
    .filter((entry) => entry.isFile() && NAME_RE.test(entry.name))
    .map((entry) => {
      const stats = fs.statSync(path.join(BACKUP_DIR, entry.name));
      return {
        name: entry.name,
        size: stats.size,
        modifiedAt: stats.mtime.toISOString(),
      };
    })
    .sort((a, b) => (a.name < b.name ? 1 : -1));
}

export function backupTimestamp(date = new Date()): string {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: TZ,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    hour12: false,
  })
    .formatToParts(date)
    .filter((part) => part.type !== "literal")
    .reduce<Record<string, string>>((acc, part) => {
      acc[part.type] = part.value;
      return acc;
    }, {});
  return `${parts.year}-${parts.month}-${parts.day}_${parts.hour}${parts.minute}${parts.second}`;
}

/** Crea un respaldo consistente del archivo SQLite (incluye WAL). */
export async function createBackup(): Promise<string> {
  fs.mkdirSync(BACKUP_DIR, { recursive: true });
  const name = `contabilidad-${backupTimestamp()}.db`;
  const destination = path.join(BACKUP_DIR, name);
  await backup(db, destination);
  return name;
}

export function resolveBackup(name: string): string | null {
  if (!NAME_RE.test(name)) return null;
  const resolved = path.resolve(BACKUP_DIR, name);
  const root = path.resolve(BACKUP_DIR) + path.sep;
  if (!resolved.startsWith(root)) return null;
  if (!fs.existsSync(resolved)) return null;
  return resolved;
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
}
