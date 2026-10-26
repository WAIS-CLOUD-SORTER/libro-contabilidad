import Link from "next/link";
import type { Contact } from "@/lib/types";
import { saveContactAction } from "@/app/actions/contacts";
import { ActionForm } from "./action-form";
import { Field } from "./ui";

export function ContactForm({ contact }: { contact?: Contact }) {
  return (
    <section className="card p-5">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h2 className="section-title">{contact ? "Editar contacto" : "Nuevo contacto"}</h2>
          <p className="mt-0.5 text-xs text-muted">
            Clientes para facturar; proveedores para tus gastos.
          </p>
        </div>
        <Link href="/contactos" className="btn btn-ghost btn-sm">
          Cerrar
        </Link>
      </div>

      <ActionForm
        action={saveContactAction}
        submitLabel={contact ? "Guardar cambios" : "Crear contacto"}
        className="mt-4"
      >
        {contact && <input type="hidden" name="id" value={contact.id} />}
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Tipo" htmlFor="type" required>
            <select className="select" id="type" name="type" defaultValue={contact?.type ?? "cliente"}>
              <option value="cliente">Cliente</option>
              <option value="proveedor">Proveedor</option>
              <option value="ambos">Cliente y proveedor</option>
            </select>
          </Field>
          <Field label="Nombre o razón social" htmlFor="name" required>
            <input
              className="input"
              id="name"
              name="name"
              defaultValue={contact?.name ?? ""}
              maxLength={160}
              required
            />
          </Field>
          <Field label="RFC" htmlFor="rfc" hint="Opcional. Se usa en la factura.">
            <input
              className="input uppercase"
              id="rfc"
              name="rfc"
              defaultValue={contact?.rfc ?? ""}
              maxLength={20}
              placeholder="XAXX010101000"
            />
          </Field>
          <Field label="Correo" htmlFor="email">
            <input
              className="input"
              id="email"
              name="email"
              type="email"
              defaultValue={contact?.email ?? ""}
              maxLength={160}
            />
          </Field>
          <Field label="Teléfono" htmlFor="phone">
            <input
              className="input"
              id="phone"
              name="phone"
              defaultValue={contact?.phone ?? ""}
              maxLength={60}
            />
          </Field>
          <Field label="Dirección" htmlFor="address">
            <input
              className="input"
              id="address"
              name="address"
              defaultValue={contact?.address ?? ""}
              maxLength={500}
            />
          </Field>
          <Field label="Notas" htmlFor="notes" className="sm:col-span-2">
            <textarea
              className="textarea"
              id="notes"
              name="notes"
              defaultValue={contact?.notes ?? ""}
              maxLength={1000}
              rows={3}
            />
          </Field>
        </div>
      </ActionForm>
    </section>
  );
}
