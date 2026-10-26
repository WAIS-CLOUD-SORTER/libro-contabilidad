"use client";

import { useActionState, type ReactNode } from "react";
import { useFormStatus } from "react-dom";
import { cn } from "@/lib/cn";
import type { ActionState } from "@/lib/permissions";

type ServerAction = (state: ActionState, formData: FormData) => Promise<ActionState>;

type Variant = "primary" | "secondary" | "ghost" | "danger" | "danger-ghost";

export function SubmitButton({
  label = "Guardar",
  pendingLabel = "Guardando…",
  variant = "primary",
  small = false,
  className,
  confirm,
  disabled,
  title,
  icon,
  name,
  value,
}: {
  label?: string;
  pendingLabel?: string;
  variant?: Variant;
  small?: boolean;
  className?: string;
  confirm?: string;
  disabled?: boolean;
  title?: string;
  icon?: ReactNode;
  name?: string;
  value?: string;
}) {
  const { pending } = useFormStatus();
  return (
    <button
      type="submit"
      name={name}
      value={value}
      className={cn("btn", `btn-${variant}`, small && "btn-sm", className)}
      disabled={pending || disabled}
      title={title}
      onClick={
        confirm
          ? (event) => {
              if (!window.confirm(confirm)) event.preventDefault();
            }
          : undefined
      }
    >
      {icon}
      {pending ? pendingLabel : label}
    </button>
  );
}

export function ActionForm({
  action,
  children,
  submitLabel = "Guardar",
  pendingLabel = "Guardando…",
  variant = "primary",
  small = false,
  className,
  submitClassName,
  footer,
  confirm,
  hideSubmit = false,
  id,
  footnote,
}: {
  action: ServerAction;
  children?: ReactNode;
  submitLabel?: string;
  pendingLabel?: string;
  variant?: Variant;
  small?: boolean;
  className?: string;
  submitClassName?: string;
  footer?: ReactNode;
  confirm?: string;
  hideSubmit?: boolean;
  id?: string;
  footnote?: ReactNode;
}) {
  const [state, formAction] = useActionState(action, null);

  return (
    <form action={formAction} className={className} id={id}>
      {children}
      {state?.error && (
        <p className="form-error mt-4" role="alert">
          {state.error}
        </p>
      )}
      {state?.success && (
        <p className="form-ok mt-4" role="status">
          {state.success}
        </p>
      )}
      {!hideSubmit && (
        <div className={cn("mt-5 flex flex-wrap items-center justify-between gap-3", submitClassName)}>
          <div className="text-xs text-muted">{footer}</div>
          <SubmitButton
            label={submitLabel}
            pendingLabel={pendingLabel}
            variant={variant}
            small={small}
            confirm={confirm}
          />
        </div>
      )}
      {hideSubmit && footnote}
    </form>
  );
}
