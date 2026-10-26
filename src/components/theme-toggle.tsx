"use client";

import { useEffect, useState } from "react";
import { Moon, Sun } from "lucide-react";

type Theme = "light" | "dark";

export function ThemeToggle() {
  const [theme, setTheme] = useState<Theme>("light");
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    const current = document.documentElement.dataset.theme;
    setTheme(current === "dark" ? "dark" : "light");
    setMounted(true);
  }, []);

  function toggle() {
    const next: Theme = theme === "dark" ? "light" : "dark";
    document.documentElement.dataset.theme = next;
    try {
      localStorage.setItem("tema", next);
    } catch {
      // Sin almacenamiento disponible: el cambio dura la sesión.
    }
    setTheme(next);
  }

  const label = theme === "dark" ? "Activar modo claro" : "Activar modo oscuro";

  return (
    <button
      type="button"
      className="btn btn-ghost btn-icon"
      onClick={toggle}
      aria-label={label}
      title={label}
      aria-pressed={theme === "dark"}
    >
      {mounted && theme === "dark" ? <Sun size={18} /> : <Moon size={18} />}
    </button>
  );
}
