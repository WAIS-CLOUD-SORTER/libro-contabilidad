import type { ReactNode } from "react";
import Link from "next/link";
import { LockKeyhole } from "lucide-react";
import { Brand } from "./brand";

export function AuthLayout({
  title,
  subtitle,
  children,
  footer,
  wide = false,
}: {
  title: string;
  subtitle: string;
  children: ReactNode;
  footer?: ReactNode;
  wide?: boolean;
}) {
  return (
    <main className="grid min-h-dvh lg:grid-cols-[1.02fr_1fr]">
      <section className="auth-panel relative hidden flex-col justify-between overflow-hidden p-10 lg:flex xl:p-14">
        <Link href="/" className="relative z-10 w-fit" aria-label="Libro, inicio">
          <Brand inverted />
        </Link>

        <div className="relative z-10 max-w-[520px] py-12">
          <p className="mb-4 text-[11px] font-bold uppercase tracking-[0.16em] text-white/55">
            Contabilidad · México · MXN
          </p>
          <h1 className="text-[38px] font-bold leading-[1.08] tracking-[-0.035em] text-white xl:text-[44px]">
            Las cuentas claras,
            <br />
            sin hojas de cálculo.
          </h1>
          <p className="mt-5 max-w-[46ch] text-[15px] leading-relaxed text-white/70">
            Ingresos, gastos, facturas, inventario, nómina e IVA en un solo lugar. Todo se guarda en
            tu computadora.
          </p>

          <ul className="mt-8 grid gap-3 text-sm text-white/80">
            {[
              "Facturación con IVA y control de cobros",
              "Inventario valuado y costo por producto",
              "Nómina estimada con ISR editable",
              "Reportes, IVA y respaldos locales",
            ].map((feature) => (
              <li key={feature} className="flex items-start gap-3">
                <span className="mt-0.5 flex h-5 w-5 flex-none items-center justify-center rounded-full bg-white/10 text-[11px] font-bold text-white">
                  ✓
                </span>
                {feature}
              </li>
            ))}
          </ul>
        </div>

        <div className="relative z-10 flex items-center gap-2 text-xs text-white/45">
          <LockKeyhole size={14} />
          Sistema local. Tus datos no salen de este equipo.
        </div>
      </section>

      <section className="flex items-center justify-center px-5 py-10 sm:px-8">
        <div className={wide ? "w-full max-w-[480px]" : "w-full max-w-[400px]"}>
          <div className="mb-8 lg:hidden">
            <Brand />
          </div>
          <h2 className="text-[26px] font-bold leading-tight tracking-[-0.03em] text-ink">{title}</h2>
          <p className="mt-1.5 text-sm leading-relaxed text-muted">{subtitle}</p>
          <div className="mt-7">{children}</div>
          {footer && <div className="mt-6 text-xs leading-relaxed text-muted">{footer}</div>}
          <div className="mt-8 flex items-center gap-2 border-t border-line pt-4 text-[11px] text-muted lg:hidden">
            <LockKeyhole size={13} />
            Sistema local. Tus datos no salen de este equipo.
          </div>
        </div>
      </section>
    </main>
  );
}
