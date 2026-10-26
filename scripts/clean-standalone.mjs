import { readdir, rm } from "node:fs/promises";
import path from "node:path";

// Next copia data/ al standalone porque el código usa fs con rutas dinámicas.
// Esa carpeta contiene la base real durante el desarrollo; nunca debe viajar
// dentro del instalador.
const standalone = path.join(process.cwd(), ".next", "standalone");

await rm(path.join(standalone, "data"), { recursive: true, force: true });

try {
  const entries = await readdir(standalone, { withFileTypes: true });
  await Promise.all(
    entries
      .filter((entry) => entry.isFile() && /\.db(?:-wal|-shm)?$/.test(entry.name))
      .map((entry) => rm(path.join(standalone, entry.name), { force: true })),
  );
} catch {
  // Si no hay standalone, el build no era para el instalador.
}
