import { cn } from "@/lib/cn";

export function BrandMark({ className }: { className?: string }) {
  return (
    <span
      className={cn(
        "inline-flex h-8 w-8 flex-none items-center justify-center rounded-[9px] bg-accent text-accent-ink",
        className,
      )}
      aria-hidden="true"
    >
      <svg
        viewBox="0 0 24 24"
        width="18"
        height="18"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      >
        <path d="M5 4.5h10.5A3.5 3.5 0 0 1 19 8v11.5H8.5A3.5 3.5 0 0 1 5 16V4.5Z" />
        <path d="M5 16a3.5 3.5 0 0 1 3.5-3.5H19" />
        <path d="M9 8h6" />
      </svg>
    </span>
  );
}

export function Brand({ compact = false, inverted = false }: { compact?: boolean; inverted?: boolean }) {
  return (
    <span className="inline-flex items-center gap-2.5">
      <BrandMark
        className={inverted ? "border border-white/15 bg-white/10 text-white" : undefined}
      />
      {!compact && (
        <span className="flex flex-col leading-none">
          <span
            className={
              inverted
                ? "text-[17px] font-bold tracking-[-0.02em] text-white"
                : "text-[17px] font-bold tracking-[-0.02em] text-ink"
            }
          >
            Libro
          </span>
          <span
            className={
              inverted
                ? "mt-0.5 text-[10px] font-semibold uppercase tracking-[0.14em] text-white/50"
                : "mt-0.5 text-[10px] font-semibold uppercase tracking-[0.14em] text-muted"
            }
          >
            Contabilidad
          </span>
        </span>
      )}
    </span>
  );
}
