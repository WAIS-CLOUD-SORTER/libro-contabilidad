"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState, type ReactNode } from "react";
import {
  ArrowLeftRight,
  BarChart3,
  Briefcase,
  ChevronDown,
  Database,
  FileText,
  Landmark,
  LayoutDashboard,
  LogOut,
  Menu,
  Package,
  Settings,
  ShieldCheck,
  UserRound,
  Users,
  Wallet,
  X,
} from "lucide-react";
import { logoutAction } from "@/app/actions/auth";
import { cn } from "@/lib/cn";
import { can, ROLE_LABELS, type Permission, type Role } from "@/lib/permissions";
import { Brand } from "./brand";
import { SubmitButton } from "./action-form";
import { ThemeToggle } from "./theme-toggle";

type NavIcon = typeof LayoutDashboard;

type NavItem = {
  href: string;
  label: string;
  icon: NavIcon;
  permission?: Permission;
};

const NAV_GROUPS: Array<{ label: string; items: NavItem[] }> = [
  {
    label: "General",
    items: [{ href: "/panel", label: "Panel", icon: LayoutDashboard }],
  },
  {
    label: "Operación",
    items: [
      { href: "/movimientos", label: "Movimientos", icon: ArrowLeftRight },
      { href: "/facturas", label: "Facturas", icon: FileText },
      { href: "/contactos", label: "Contactos", icon: Users },
    ],
  },
  {
    label: "Inventario",
    items: [{ href: "/inventario", label: "Productos y stock", icon: Package }],
  },
  {
    label: "Personal",
    items: [
      { href: "/empleados", label: "Empleados", icon: Briefcase, permission: "payroll.view" },
      { href: "/nomina", label: "Nómina", icon: Wallet, permission: "payroll.view" },
    ],
  },
  {
    label: "Análisis",
    items: [
      { href: "/impuestos", label: "Impuestos", icon: Landmark, permission: "tax.view" },
      { href: "/reportes", label: "Reportes", icon: BarChart3, permission: "reports.view" },
    ],
  },
  {
    label: "Administración",
    items: [
      { href: "/configuracion", label: "Configuración", icon: Settings, permission: "settings.manage" },
      { href: "/usuarios", label: "Usuarios", icon: ShieldCheck, permission: "users.manage" },
    ],
  },
];

function initials(name: string): string {
  const parts = name.trim().split(/\s+/).slice(0, 2);
  return parts.map((part) => part.charAt(0).toUpperCase()).join("") || "?";
}

export function AppShell({
  user,
  companyName,
  todayLabel,
  children,
}: {
  user: { id: number; name: string; email: string; role: Role };
  companyName: string;
  todayLabel: string;
  children: ReactNode;
}) {
  const pathname = usePathname();
  const [open, setOpen] = useState(false);

  useEffect(() => {
    setOpen(false);
  }, [pathname]);

  useEffect(() => {
    document.body.style.overflow = open ? "hidden" : "";
    return () => {
      document.body.style.overflow = "";
    };
  }, [open]);

  const groups = NAV_GROUPS.map((group) => ({
    ...group,
    items: group.items.filter((item) => !item.permission || can(user.role, item.permission)),
  })).filter((group) => group.items.length > 0);

  const current =
    NAV_GROUPS.flatMap((group) => group.items).find(
      (item) => pathname === item.href || pathname.startsWith(`${item.href}/`),
    ) ?? null;

  function isActive(href: string): boolean {
    return pathname === href || pathname.startsWith(`${href}/`);
  }

  return (
    <div className="min-h-dvh">
      <a
        href="#contenido"
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-[70] focus:rounded-lg focus:bg-surface focus:px-4 focus:py-2 focus:text-sm focus:font-semibold focus:shadow-lg"
      >
        Saltar al contenido
      </a>

      <button
        type="button"
        aria-label="Cerrar menú"
        className={cn(
          "fixed inset-0 z-40 bg-black/45 backdrop-blur-[2px] lg:hidden",
          open ? "block" : "hidden",
        )}
        onClick={() => setOpen(false)}
        tabIndex={open ? 0 : -1}
      />

      <aside
        id="menu-lateral"
        aria-label="Navegación principal"
        className={cn(
          "fixed inset-y-0 left-0 z-50 flex w-[264px] flex-col border-r border-line bg-surface transition-transform duration-200 ease-out lg:translate-x-0",
          open ? "translate-x-0" : "-translate-x-full",
          open ? "visible" : "invisible lg:visible",
        )}
      >
        <div className="flex h-[60px] items-center justify-between gap-2 border-b border-line px-4">
          <Link href="/panel" aria-label="Libro, ir al panel">
            <Brand />
          </Link>
          <button
            type="button"
            className="btn btn-ghost btn-icon btn-sm lg:hidden"
            onClick={() => setOpen(false)}
            aria-label="Cerrar menú"
          >
            <X size={18} />
          </button>
        </div>

        <div className="border-b border-line px-4 py-3">
          <p className="text-[10px] font-bold uppercase tracking-[0.12em] text-muted">Negocio</p>
          <p className="mt-0.5 truncate text-[13px] font-semibold text-ink" title={companyName}>
            {companyName}
          </p>
        </div>

        <nav className="flex-1 overflow-y-auto px-3 py-3">
          {groups.map((group) => (
            <div key={group.label}>
              <p className="nav-label">{group.label}</p>
              <div className="space-y-0.5">
                {group.items.map((item) => {
                  const Icon = item.icon;
                  const active = isActive(item.href);
                  return (
                    <Link
                      key={item.href}
                      href={item.href}
                      className="nav-item"
                      aria-current={active ? "page" : undefined}
                      onClick={() => setOpen(false)}
                    >
                      <Icon size={18} strokeWidth={1.8} aria-hidden="true" />
                      {item.label}
                    </Link>
                  );
                })}
              </div>
            </div>
          ))}
        </nav>

        <div className="border-t border-line p-3">
          <div className="rounded-[10px] bg-elevated p-3">
            <div className="flex items-center gap-2 text-[11px] font-semibold text-ink">
              <Database size={14} />
              Datos locales
            </div>
            <p className="mt-1 text-[11px] leading-relaxed text-muted">
              Respaldos y configuración del negocio en un solo lugar.
            </p>
            {can(user.role, "settings.manage") && (
              <Link href="/configuracion#respaldo" className="mt-1.5 inline-flex text-[11px] font-semibold text-accent">
                Crear respaldo
              </Link>
            )}
          </div>
        </div>
      </aside>

      <div className="lg:pl-[264px]">
        <header className="sticky top-0 z-30 flex h-[60px] items-center justify-between gap-3 border-b border-line bg-canvas/85 px-4 backdrop-blur-md sm:px-6 lg:px-8">
          <div className="flex min-w-0 items-center gap-2">
            <button
              type="button"
              className="btn btn-ghost btn-icon lg:hidden"
              onClick={() => setOpen(true)}
              aria-label="Abrir menú"
              aria-expanded={open}
              aria-controls="menu-lateral"
            >
              <Menu size={20} />
            </button>
            <p className="truncate text-[14px] font-semibold text-ink">{current?.label ?? "Libro"}</p>
            <span className="hidden text-xs text-muted xl:inline">· {companyName}</span>
          </div>

          <div className="flex items-center gap-1.5">
            <p className="mr-1 hidden text-xs text-muted md:block">{todayLabel}</p>
            <ThemeToggle />
            <details className="relative">
              <summary className="summary-clean flex cursor-pointer items-center gap-2 rounded-lg p-1 transition-colors hover:bg-elevated">
                <span className="avatar">{initials(user.name)}</span>
                <span className="hidden text-left leading-tight md:block">
                  <span className="block max-w-[150px] truncate text-[12.5px] font-semibold text-ink">
                    {user.name}
                  </span>
                  <span className="block text-[10px] font-semibold uppercase tracking-[0.06em] text-muted">
                    {ROLE_LABELS[user.role]}
                  </span>
                </span>
                <ChevronDown size={14} className="hidden text-muted md:block" />
              </summary>
              <div className="absolute right-0 z-50 mt-2 w-60 rounded-xl border border-line bg-surface p-1.5 shadow-xl">
                <div className="border-b border-line px-3 py-2 md:hidden">
                  <p className="truncate text-[13px] font-semibold text-ink">{user.name}</p>
                  <p className="truncate text-[11px] text-muted">{user.email}</p>
                </div>
                <Link
                  href="/cuenta"
                  className="flex h-9 items-center gap-2 rounded-lg px-3 text-[13px] font-medium text-ink hover:bg-elevated"
                >
                  <UserRound size={16} className="text-muted" />
                  Mi cuenta
                </Link>
                <form action={logoutAction}>
                  <SubmitButton
                    label="Cerrar sesión"
                    variant="ghost"
                    className="h-9 w-full justify-start px-3 font-medium"
                    icon={<LogOut size={16} />}
                  />
                </form>
              </div>
            </details>
          </div>
        </header>

        <main id="contenido" className="mx-auto w-full max-w-[1280px] px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          {children}
        </main>
      </div>
    </div>
  );
}
