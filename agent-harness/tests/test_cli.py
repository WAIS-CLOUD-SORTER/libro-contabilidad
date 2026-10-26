#!/usr/bin/env python3
"""Pruebas end-to-end del harness CLI de Libro.

No usan la base real del usuario. Cada ejecución crea una carpeta temporal
única y pasa --data-dir en todas las invocaciones.

Ejecutar:
    python -m unittest discover -s tests -v
    python tests/test_cli.py -v

Si se define LIBRO_KEEP_TEST_DATA=1, la carpeta temporal se conserva para
inspección después de las pruebas.
"""

from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from datetime import date, timedelta
from pathlib import Path

HARNESS = Path(__file__).resolve().parents[1]
DLL = HARNESS / "Libro.Cli" / "bin" / "Release" / "net8.0-windows" / "libro-cli.dll"

LOCALAPPDATA = Path(os.environ.get("LOCALAPPDATA", ""))
CANDIDATE_DOTNET = LOCALAPPDATA / "Microsoft" / "dotnet" / "dotnet.exe"
DOTNET = CANDIDATE_DOTNET if CANDIDATE_DOTNET.exists() else Path(shutil.which("dotnet") or "dotnet")

ADMIN_EMAIL = "admin@libro.test"
ADMIN_PASSWORD = "AdminLibro123!"
CONSULTA_EMAIL = "consulta@libro.test"
CONSULTA_PASSWORD = "ConsultaLibro123!"
CONSULTA_PASSWORD_2 = "ConsultaLibro456!"

DEFAULT_DATA_DIR = LOCALAPPDATA / "Libro" / "data"


class LibroCliTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        if not DLL.exists():
            raise unittest.SkipTest(
                f"No existe {DLL}. Compila primero con:\n"
                f'  "{DOTNET}" build "{HARNESS / "Libro.Cli" / "Libro.Cli.csproj"}" -c Release'
            )

        cls.data_dir = Path(tempfile.mkdtemp(prefix="libro-cli-tests-"))
        if cls.data_dir.resolve() == DEFAULT_DATA_DIR.resolve():
            raise RuntimeError("La carpeta de pruebas coincide con los datos reales de Libro.")
        if not str(cls.data_dir.resolve()).lower().startswith(str(Path(tempfile.gettempdir()).resolve()).lower()):
            raise RuntimeError(f"La carpeta de pruebas no está en TEMP: {cls.data_dir}")

        cls.env = os.environ.copy()
        for name in ("LIBRO_DATA_DIR", "LIBRO_EMAIL", "LIBRO_PASSWORD"):
            cls.env.pop(name, None)
        cls.env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
        cls.env["DOTNET_NOLOGO"] = "1"

        cls.base = ["--data-dir", str(cls.data_dir), "--json"]
        cls.admin = [*cls.base, "--email", ADMIN_EMAIL, "--password", ADMIN_PASSWORD]
        cls.human_admin = [
            "--data-dir", str(cls.data_dir),
            "--email", ADMIN_EMAIL,
            "--password", ADMIN_PASSWORD,
        ]

        cls.contact_id: int | None = None
        cls.product_id: int | None = None
        cls.invoice_id: int | None = None
        cls.employee_id: int | None = None
        cls.payroll_id: int | None = None
        cls.account_id: int | None = None
        cls.expense_category_id: int | None = None
        cls.backup_path: Path | None = None
        cls.tx_path = cls.data_dir / "transaction.json"
        cls.invalid_json_path = cls.data_dir / "invalid.json"

        cls.invalid_json_path.write_text("{ esto no es json", encoding="utf-8")
        print(f"\nDatos temporales de prueba: {cls.data_dir}", file=sys.stderr)

    @classmethod
    def tearDownClass(cls) -> None:
        if os.environ.get("LIBRO_KEEP_TEST_DATA") == "1":
            print(f"Se conserva la carpeta de pruebas: {cls.data_dir}", file=sys.stderr)
            return
        shutil.rmtree(cls.data_dir, ignore_errors=True)

    # ------------------------------------------------------------------ helpers
    def _run(
        self,
        args: list[str],
        *,
        expect: int = 0,
        input_text: str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        command = [str(DOTNET), str(DLL), *args]
        kwargs = {
            "capture_output": True,
            "text": True,
            "encoding": "utf-8",
            "errors": "replace",
            "timeout": 120,
            "env": self.env,
        }
        if input_text is not None:
            kwargs["input"] = input_text
        else:
            kwargs["stdin"] = subprocess.DEVNULL

        try:
            proc = subprocess.run(command, **kwargs)
        except subprocess.TimeoutExpired as ex:
            self.fail(f"El comando excedió el tiempo límite: {self._display(args)}\n{ex}")

        if proc.returncode != expect:
            self.fail(
                f"Se esperaba código {expect}, se obtuvo {proc.returncode}: {self._display(args)}\n"
                f"STDOUT:\n{proc.stdout}\nSTDERR:\n{proc.stderr}\nDatos: {self.data_dir}"
            )
        return proc

    @staticmethod
    def _display(args: list[str]) -> str:
        safe = []
        hide_next = False
        for token in args:
            if hide_next:
                safe.append("***")
                hide_next = False
                continue
            safe.append(token)
            if token in ("--password", "--new-password"):
                hide_next = True
        return " ".join(safe)

    def _json_ok(self, args: list[str], *, input_text: str | None = None):
        proc = self._run(args, input_text=input_text)
        try:
            return json.loads(proc.stdout)
        except json.JSONDecodeError as ex:
            self.fail(
                f"La salida no es JSON válido: {self._display(args)}\n"
                f"STDOUT:\n{proc.stdout}\nSTDERR:\n{proc.stderr}\nError: {ex}"
            )

    def _json_error(self, args: list[str], *, contains: str | None = None, input_text: str | None = None):
        proc = self._run(args, expect=1, input_text=input_text)
        self.assertEqual(proc.stdout.strip(), "", f"Un error no debe escribir datos en stdout: {self._display(args)}")
        try:
            payload = json.loads(proc.stderr)
        except json.JSONDecodeError as ex:
            self.fail(
                f"El error no es JSON válido: {self._display(args)}\n"
                f"STDOUT:\n{proc.stdout}\nSTDERR:\n{proc.stderr}\nError: {ex}"
            )
        self.assertIs(payload.get("ok"), False, payload)
        self.assertTrue(payload.get("error"), payload)
        if contains:
            self.assertIn(contains.lower(), payload["error"].lower(), payload)
        return payload

    def _write_json(self, name: str, value: object) -> Path:
        path = self.data_dir / name
        path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
        return path

    @staticmethod
    def _month_range() -> tuple[str, str]:
        today = date.today()
        start = today.replace(day=1)
        if today.month == 12:
            next_month = date(today.year + 1, 1, 1)
        else:
            next_month = date(today.year, today.month + 1, 1)
        end = next_month - timedelta(days=1)
        return start.isoformat(), end.isoformat()

    # ------------------------------------------------------------------- tests
    def test_00_help_and_version(self):
        help_proc = self._run([*self.base, "help"])
        self.assertIn("Libro CLI", help_proc.stdout)
        self.assertIn("transactions", help_proc.stdout)

        # Regresión: --help nunca debe ejecutar la operación ni terminar con
        # una excepción no controlada.
        global_help = self._run([*self.base, "--help"])
        self.assertIn("Reglas:", global_help.stdout)
        group_help = self._run([*self.base, "accounts", "--help"])
        self.assertIn("accounts list|balances", group_help.stdout)
        action_help = self._run([*self.base, "invoices", "pay", "--help"])
        self.assertIn("--amount-cents", action_help.stdout)

        json_help = self._json_ok([*self.base, "--help"])
        self.assertIn("Libro CLI", json_help["help"])

        version = self._json_ok([*self.base, "version"])
        self.assertIn("cliVersion", version)
        self.assertIn("appVersion", version)

        schema = self._json_ok([*self.base, "schema", "transaction.save"])
        self.assertIn("subtotalCents", schema)
        self.assertIn("categoryId", schema)

    def test_01_doctor_before_setup(self):
        doctor = self._json_ok([*self.base, "doctor"])
        self.assertFalse(doctor["databaseExists"])
        self.assertFalse(doctor["keyExists"])
        self.assertEqual(doctor["databaseKind"], "missing")
        self.assertFalse(doctor["opened"])

    def test_02_setup_and_auth(self):
        company = "Libro CLI Test"
        setup = self._json_ok(
            [
                *self.base, "auth", "setup",
                "--company", company,
                "--name", "Admin de Prueba",
                "--email", ADMIN_EMAIL,
                "--password", ADMIN_PASSWORD,
            ]
        )
        self.assertTrue(setup["authenticated"])
        self.assertEqual(setup["user"]["email"], ADMIN_EMAIL)
        self.assertEqual(setup["user"]["role"], "admin")
        self.assertTrue(setup["user"]["lastLoginAt"])
        self.assertEqual(setup["company"], company)

        # Sin credenciales, status no debe inventar una sesión.
        anonymous = self._json_ok([*self.base, "auth", "status"])
        self.assertFalse(anonymous["authenticated"])

        explicit_login = self._json_ok(
            [*self.base, "auth", "login", "--email", ADMIN_EMAIL, "--password", ADMIN_PASSWORD]
        )
        self.assertTrue(explicit_login["authenticated"])
        self.assertEqual(explicit_login["user"]["email"], ADMIN_EMAIL)
        self.assertTrue(explicit_login["user"]["lastLoginAt"])

        authed = self._json_ok([*self.admin, "auth", "status"])
        self.assertTrue(authed["authenticated"])
        self.assertTrue(authed["user"]["lastLoginAt"])

        doctor = self._json_ok([*self.admin, "doctor"])
        self.assertTrue(doctor["opened"])
        self.assertEqual(doctor["databaseKind"], "encrypted")
        self.assertTrue(doctor["keyExists"])
        self.assertEqual(doctor["counts"]["users"], 1)
        self.assertGreaterEqual(doctor["counts"]["accounts"], 2)
        self.assertGreaterEqual(doctor["counts"]["categories"], 1)

    def test_03_seed_data(self):
        accounts = self._json_ok([*self.admin, "accounts", "list"])
        self.assertGreaterEqual(len(accounts), 2)
        active_accounts = [row for row in accounts if row["active"] == 1]
        self.assertTrue(active_accounts)
        self.__class__.account_id = int(active_accounts[0]["id"])

        categories = self._json_ok([*self.admin, "categories", "list"])
        gastos = [row for row in categories if row["kind"] == "gasto" and row["archived"] == 0]
        self.assertTrue(gastos)
        self.__class__.expense_category_id = int(gastos[0]["id"])

        contacts = self._json_ok([*self.admin, "contacts", "list"])
        self.assertEqual(contacts, [])

    def test_04_write_workflow(self):
        today = date.today().isoformat()
        due = (date.today() + timedelta(days=30)).isoformat()
        month_from, month_to = self._month_range()

        contact_file = self._write_json(
            "contact.json",
            {
                "type": "cliente",
                "name": "Cliente Demo",
                "rfc": "XAXX010101000",
                "email": "cliente@demo.mx",
                "phone": "5555555555",
                "address": "Calle Demo 1",
                "notes": "Prueba CLI",
            },
        )
        contact = self._json_ok([*self.admin, "contacts", "save", "--input", str(contact_file)])
        self.assertEqual(contact["name"], "Cliente Demo")
        self.__class__.contact_id = int(contact["id"])

        product_file = self._write_json(
            "product.json",
            {
                "name": "Servicio Demo",
                "sku": "SERV-001",
                "description": "Servicio de prueba",
                "unit": "servicio",
                "priceCents": 100000,
                "costCents": 20000,
                "ivaRate": 0.16,
                "minStock": 1,
                "trackStock": True,
                "initialStock": 10,
            },
        )
        product = self._json_ok([*self.admin, "products", "save", "--input", str(product_file)])
        self.assertEqual(product["sku"], "SERV-001")
        self.assertEqual(int(product["stock"]), 10)
        self.__class__.product_id = int(product["id"])

        transaction = {
            "date": today,
            "type": "gasto",
            "categoryId": self.expense_category_id,
            "accountId": self.account_id,
            "description": "Renta de prueba",
            "subtotalCents": 10000,
            "ivaRate": 0.16,
            "method": "Efectivo",
            "reference": "TX-001",
        }
        self.tx_path.write_text(json.dumps(transaction, ensure_ascii=False, indent=2), encoding="utf-8")
        saved_tx = self._json_ok([*self.admin, "transactions", "save", "--input", str(self.tx_path)])
        self.assertEqual(saved_tx["totalCents"], 11600)
        self.assertEqual(saved_tx["description"], "Renta de prueba")

        invoice_file = self._write_json(
            "invoice.json",
            {
                "contactId": self.contact_id,
                "date": today,
                "dueDate": due,
                "notes": "Factura interna de prueba",
                "paymentTerms": "Contado",
                "lines": [
                    {
                        "productId": self.product_id,
                        "description": "Servicio Demo",
                        "qty": 1,
                        "unitPriceCents": 100000,
                        "ivaRate": 0.16,
                    }
                ],
            },
        )
        invoice = self._json_ok([*self.admin, "invoices", "save", "--input", str(invoice_file), "--emit"])
        self.assertEqual(invoice["status"], "emitida")
        self.assertEqual(invoice["totalCents"], 116000)
        self.assertEqual(invoice["balanceCents"], 116000)
        self.assertTrue(invoice["displayFolio"].startswith("A-"))
        self.__class__.invoice_id = int(invoice["id"])

        paid = self._json_ok(
            [
                *self.admin, "invoices", "pay",
                "--id", str(self.invoice_id),
                "--date", today,
                "--amount-cents", "116000",
                "--account", str(self.account_id),
                "--method", "Transferencia",
            ]
        )
        self.assertEqual(paid["status"], "pagada")
        self.assertEqual(paid["paidCents"], 116000)
        self.assertEqual(paid["balanceCents"], 0)

        shown_invoice = self._json_ok([*self.admin, "invoices", "show", "--id", str(self.invoice_id)])
        self.assertEqual(len(shown_invoice["items"]), 1)
        self.assertEqual(len(shown_invoice["payments"]), 1)
        self.assertEqual(shown_invoice["payments"][0]["amountCents"], 116000)

        movement_file = self._write_json(
            "movement.json",
            {
                "productId": self.product_id,
                "date": today,
                "type": "entrada",
                "qty": 5,
                "unitCostCents": 20000,
                "ivaRate": 0.16,
                "reason": "Compra de prueba",
                "accountId": self.account_id,
            },
        )
        movement = self._json_ok([*self.admin, "inventory", "movement", "--input", str(movement_file)])
        self.assertEqual(int(movement["product"]["stock"]), 14)

        shown_product = self._json_ok([*self.admin, "products", "show", "--id", str(self.product_id)])
        self.assertEqual(int(shown_product["product"]["stock"]), 14)
        self.assertGreaterEqual(len(shown_product["movements"]), 2)

        inventory_summary = self._json_ok([*self.admin, "inventory", "summary"])
        self.assertGreaterEqual(inventory_summary["productCount"], 1)
        self.assertGreaterEqual(inventory_summary["valueCents"], 1)

        employee_file = self._write_json(
            "employee.json",
            {
                "name": "Empleado Demo",
                "rfc": "XAXX010101000",
                "position": "Auxiliar",
                "department": "Administración",
                "salaryCents": 800000,
                "startDate": f"{date.today().year}-01-01",
                "status": "activo",
            },
        )
        employee = self._json_ok([*self.admin, "employees", "save", "--input", str(employee_file)])
        self.assertEqual(employee["name"], "Empleado Demo")
        self.__class__.employee_id = int(employee["id"])

        payroll = self._json_ok(
            [
                *self.admin, "payroll", "create",
                "--year", str(date.today().year),
                "--month", str(date.today().month),
                "--type", "mensual",
            ]
        )
        self.assertEqual(payroll["status"], "borrador")
        self.assertGreaterEqual(payroll["employeeCount"], 1)
        self.assertGreater(payroll["totalNetCents"], 0)
        self.__class__.payroll_id = int(payroll["id"])

        payroll_shown = self._json_ok([*self.admin, "payroll", "show", "--id", str(self.payroll_id)])
        self.assertGreaterEqual(len(payroll_shown["items"]), 1)

        payroll_paid = self._json_ok(
            [
                *self.admin, "payroll", "pay",
                "--id", str(self.payroll_id),
                "--account", str(self.account_id),
                "--date", today,
            ]
        )
        self.assertEqual(payroll_paid["status"], "pagada")
        self.assertGreater(payroll_paid["transactionId"], 0)

        payroll_summary = self._json_ok([*self.admin, "payroll", "summary"])
        self.assertGreaterEqual(payroll_summary["paidCount"], 1)

        income = self._json_ok(
            [*self.admin, "reports", "income", "--from", month_from, "--to", month_to]
        )
        self.assertGreaterEqual(income["invoicedRevenueCents"], 100000)
        self.assertGreater(income["totalExpensesCents"], 0)
        self.assertTrue(income["expensesByCategory"])

        cashflow = self._json_ok(
            [*self.admin, "reports", "cashflow", "--from", month_from, "--to", month_to]
        )
        self.assertIn("netCents", cashflow)
        self.assertTrue(cashflow["monthly"])

        aging = self._json_ok([*self.admin, "reports", "aging"])
        self.assertIn("buckets", aging)
        self.assertIn("invoices", aging)

        monthly = self._json_ok([*self.admin, "reports", "monthly"])
        self.assertTrue(monthly)

        balances = self._json_ok([*self.admin, "reports", "balances"])
        self.assertGreaterEqual(len(balances), 1)

        taxes = self._json_ok(
            [*self.admin, "taxes", "summary", "--from", month_from, "--to", month_to]
        )
        self.assertGreaterEqual(taxes["invoiceCount"], 1)
        self.assertGreaterEqual(taxes["invoiceIvaCents"], 1600)
        self.assertTrue(taxes["incomeRows"])

        annual = self._json_ok([*self.admin, "taxes", "annual", "--year", str(date.today().year)])
        self.assertGreaterEqual(len(annual), 1)

        dashboard = self._json_ok([*self.admin, "dashboard"])
        self.assertGreaterEqual(dashboard["inventoryCents"], 1)
        self.assertIn("recentInvoices", dashboard)

        overview = self._json_ok([*self.admin, "invoices", "overview"])
        self.assertGreaterEqual(overview["monthCount"], 1)

        listed_tx = self._json_ok([*self.admin, "transactions", "list"])
        self.assertGreaterEqual(listed_tx["total"], 1)
        self.assertGreaterEqual(len(listed_tx["rows"]), 1)

        totals = self._json_ok([*self.admin, "transactions", "totals"])
        self.assertGreaterEqual(totals["count"], 1)

    def test_04b_help_is_side_effect_free(self):
        # `--help` se intercepta antes de abrir la base: no debe alternar el
        # producto ni exigir credenciales.
        before = self._json_ok([*self.admin, "products", "show", "--id", str(self.product_id)])
        self.assertEqual(int(before["product"]["active"]), 1)

        proc = self._run(
            [*self.base, "products", "toggle", "--id", str(self.product_id), "--help"]
        )
        self.assertIn("products list|low-stock", proc.stdout)

        after = self._json_ok([*self.admin, "products", "show", "--id", str(self.product_id)])
        self.assertEqual(int(after["product"]["active"]), 1)
        self.assertEqual(int(after["product"]["id"]), int(self.product_id))

    def test_05_permissions_and_users(self):
        created = self._json_ok(
            [
                *self.admin, "users", "save",
                "--name", "Consulta Demo",
                "--email", CONSULTA_EMAIL,
                "--role", "consulta",
                "--new-password", CONSULTA_PASSWORD,
            ]
        )
        self.assertEqual(created["email"], CONSULTA_EMAIL)
        self.assertEqual(created["role"], "consulta")
        consulta_id = int(created["id"])

        users = self._json_ok([*self.admin, "users", "list"])
        self.assertGreaterEqual(len(users), 2)
        self.assertNotIn("passwordHash", json.dumps(users, ensure_ascii=False))
        self.assertNotIn("password_hash", json.dumps(users, ensure_ascii=False))

        roles = self._json_ok([*self.admin, "users", "roles"])
        self.assertIn("admin", {row["key"] for row in roles})
        self.assertIn("consulta", {row["key"] for row in roles})

        consulta = [
            *self.base,
            "--email", CONSULTA_EMAIL,
            "--password", CONSULTA_PASSWORD,
        ]

        status = self._json_ok([*consulta, "auth", "status"])
        self.assertTrue(status["authenticated"])
        self.assertEqual(status["user"]["role"], "consulta")

        month_from, month_to = self._month_range()
        reports = self._json_ok(
            [*consulta, "reports", "income", "--from", month_from, "--to", month_to]
        )
        self.assertIn("totalIncomeCents", reports)

        taxes = self._json_ok(
            [*consulta, "taxes", "summary", "--from", month_from, "--to", month_to]
        )
        self.assertIn("ivaNetoCents", taxes)

        products = self._json_ok([*consulta, "products", "list"])
        self.assertGreaterEqual(len(products), 1)

        self._json_error(
            [*consulta, "transactions", "save", "--input", str(self.tx_path)],
            contains="permiso",
        )
        self._json_error([*consulta, "users", "list"], contains="permiso")
        self._json_error([*consulta, "payroll", "list"], contains="permiso")
        self._json_error([*consulta, "backup", "create"], contains="permiso")
        self._json_error(
            [
                *consulta, "contacts", "save",
                "--input", str(self._write_json("denied-contact.json", {"type": "cliente", "name": "No"}))
            ],
            contains="permiso",
        )

        reset = self._json_ok(
            [
                *self.admin, "users", "reset-password",
                "--id", str(consulta_id),
                "--new-password", CONSULTA_PASSWORD_2,
            ]
        )
        self.assertTrue(reset["changed"])

        new_status = self._json_ok(
            [
                *self.base,
                "--email", CONSULTA_EMAIL,
                "--password", CONSULTA_PASSWORD_2,
                "auth", "status",
            ]
        )
        self.assertTrue(new_status["authenticated"])

        self._json_error(
            [*self.admin, "users", "save", "--name", "Sin Clave", "--email", "sinclave@libro.test", "--role", "consulta"],
            contains="new-password",
        )

    def test_06_backup_restore(self):
        backup = self._json_ok([*self.admin, "backup", "create"])
        path = Path(backup["path"])
        self.assertTrue(path.exists())
        self.assertEqual(len(backup["sha256"]), 64)
        self.__class__.backup_path = path

        listed = self._json_ok([*self.admin, "backup", "list"])
        self.assertTrue(any(row["path"] == str(path) for row in listed))

        marker_file = self._write_json(
            "marker.json",
            {"type": "cliente", "name": "Marcador Restore", "rfc": "XAXX010101000"},
        )
        marker = self._json_ok([*self.admin, "contacts", "save", "--input", str(marker_file)])
        self.assertEqual(marker["name"], "Marcador Restore")
        before = self._json_ok([*self.admin, "contacts", "list"])
        self.assertEqual(len(before), 2)

        self._json_error(
            [*self.admin, "backup", "restore", "--file", str(path)],
            contains="--yes",
        )

        restored = self._json_ok(
            [*self.admin, "backup", "restore", "--file", str(path), "--yes"]
        )
        self.assertEqual(Path(restored["restoredFrom"]), path)
        self.assertTrue(Path(restored["safetyBackup"]).exists())

        after = self._json_ok([*self.admin, "contacts", "list"])
        self.assertEqual([row["name"] for row in after], ["Cliente Demo"])

        doctor = self._json_ok([*self.admin, "doctor"])
        self.assertTrue(doctor["opened"])
        self.assertEqual(doctor["counts"]["contacts"], 1)
        self.assertGreaterEqual(doctor["backupCount"], 2)

    def test_07_json_errors(self):
        self._json_error([*self.admin, "contacts", "save"], contains="--input")
        self._json_error(
            [*self.admin, "contacts", "save", "--input", str(self.invalid_json_path)],
            contains="json",
        )
        self._json_error([*self.admin, "schema", "no-existe"], contains="esquema")
        self._json_error([*self.admin, "transactions", "show", "--id", "999999"], contains="no existe")
        self._json_error(
            [*self.base, "auth", "login", "--email", ADMIN_EMAIL, "--password", "Incorrecta123!"],
            contains="incorrect",
        )
        self._json_error(
            [
                *self.base, "auth", "setup",
                "--company", "Otra", "--name", "Otro",
                "--email", "otro@libro.test", "--password", "OtraClave123!",
            ],
            contains="administrador",
        )
        self._json_error(
            [*self.admin, "backup", "restore", "--file", str(self.data_dir / "no-existe.libro"), "--yes"],
            contains="no",
        )

    def test_08_repl(self):
        # Regresión: una ruta Windows entre comillas debe conservar sus
        # backslashes al tokenizar la línea del REPL.
        repl_contact = self._write_json(
            "repl-contact.json",
            {"type": "cliente", "name": "Repl Path"},
        )
        human = self._run(
            self.human_admin,
            input_text=f'contacts save --input "{repl_contact}"\ncontacts list\nexit\n',
        )
        self.assertIn("modo interactivo", human.stdout)
        self.assertIn("Cliente Demo", human.stdout)
        self.assertIn("Repl Path", human.stdout)
        self.assertIn("libro", human.stdout.lower())

        machine = self._run(
            self.base,
            input_text="version\nexit\n",
        )
        data = json.loads(machine.stdout)
        self.assertIn("cliVersion", data)

        status = self._run(self.human_admin, input_text="auth status\nexit\n")
        self.assertIn("Sesión activa", status.stdout)

    def test_09_doctor_orphan_database(self):
        orphan = self.data_dir / "orphan"
        orphan.mkdir()
        shutil.copy2(self.data_dir / "contabilidad.db", orphan / "contabilidad.db")
        self.assertFalse((orphan / "clave.bin").exists())

        doctor = self._json_ok(["--data-dir", str(orphan), "--json", "doctor"])
        self.assertTrue(doctor["databaseExists"])
        self.assertFalse(doctor["keyExists"])
        self.assertFalse(doctor["opened"])
        self.assertIn("clave.bin", doctor["note"])
        self.assertFalse((orphan / "clave.bin").exists(), "doctor no debe crear una llave nueva")


if __name__ == "__main__":
    unittest.main(verbosity=2)
