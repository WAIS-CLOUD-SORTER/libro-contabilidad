"use client";

import { Printer } from "lucide-react";

export function PrintButton() {
  return (
    <button type="button" className="btn btn-primary no-print" onClick={() => window.print()}>
      <Printer size={16} />
      Imprimir / Guardar PDF
    </button>
  );
}
