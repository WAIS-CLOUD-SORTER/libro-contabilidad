import { run } from "./db";

export function audit(
  userId: number | null,
  action: string,
  entity: string,
  entityId: string | number | null,
  detail?: unknown,
): void {
  let serialized = "";
  if (detail !== undefined && detail !== null) {
    try {
      serialized = JSON.stringify(detail).slice(0, 4000);
    } catch {
      serialized = "";
    }
  }
  run(
    `INSERT INTO audit_log (user_id, action, entity, entity_id, detail)
     VALUES (?, ?, ?, ?, ?)`,
    [userId, action, entity, entityId === null ? "" : String(entityId), serialized],
  );
}
