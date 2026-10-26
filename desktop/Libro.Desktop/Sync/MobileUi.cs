namespace Libro.Desktop.Sync;

/// <summary>
/// Interfaz de ventas para el teléfono. Se sirve como una sola página desde el
/// propio servidor local: no hay compilación ni dependencias de JavaScript, así
/// que funciona en cualquier Android con Chrome sin instalar nada.
/// </summary>
internal static class MobileUi
{
    public const string Html = """
<!DOCTYPE html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<meta name="theme-color" content="#10231C">
<title>Libro · Ventas</title>
<style>
  :root{--canvas:#F5F5F1;--surface:#fff;--ink:#171A1C;--muted:#667085;--line:#E4E4DC;
        --accent:#0F6B4F;--accent-soft:#E7F3ED;--danger:#B42318;--sidebar:#10231C}
  *{box-sizing:border-box;-webkit-tap-highlight-color:transparent}
  html,body{margin:0;height:100%}
  body{background:var(--canvas);color:var(--ink);font:16px/1.45 "Segoe UI",system-ui,-apple-system,sans-serif;
       display:flex;flex-direction:column;overflow-x:hidden;padding-bottom:env(safe-area-inset-bottom)}
  header{background:var(--sidebar);color:#fff;padding:16px 18px;position:sticky;top:0;z-index:5}
  header .row{display:flex;justify-content:space-between;align-items:center;gap:12px}
  header h1{font-size:16px;margin:0;font-weight:600}
  header .sub{font-size:12px;color:#A8BDB4;margin-top:2px}
  .pill{background:#1C3A30;border-radius:999px;padding:6px 12px;font-size:13px;font-weight:600;white-space:nowrap}
  main{flex:1;overflow-y:auto;overflow-x:hidden;min-width:0;padding:14px;
       display:flex;flex-direction:column;gap:12px}
  /* min-width:0 evita que un flex item no pueda encogerse por debajo del
     ancho de su contenido, que era lo que desbordaba la tarjeta. */
  .card{background:var(--surface);border:1px solid var(--line);border-radius:14px;padding:14px;min-width:0}
  label{display:block;font-size:12px;color:var(--muted);margin:0 0 5px;font-weight:600}
  input,select,button{font:inherit}
  input,select{width:100%;padding:12px;border:1px solid var(--line);border-radius:10px;background:#fff;color:var(--ink)}
  input:focus,select:focus{outline:2px solid var(--accent);outline-offset:1px}
  button{border:0;border-radius:11px;padding:13px 16px;font-weight:600;cursor:pointer}
  .primary{background:var(--accent);color:#fff;width:100%}
  .primary:disabled{opacity:.45}
  .ghost{background:transparent;color:var(--muted);border:1px solid var(--line)}
  .prod{display:flex;justify-content:space-between;align-items:center;gap:10px;
        padding:12px 0;border-bottom:1px solid var(--line);min-width:0}
  .prod>div{min-width:0}
  .grid2>div{min-width:0}
  .prod:last-child{border-bottom:0}
  .prod b{font-size:15px;font-weight:600;display:block}
  .prod small{color:var(--muted);font-size:12px}
  .price{font-weight:700;color:var(--accent);white-space:nowrap}
  .add{background:var(--accent-soft);color:var(--accent);border-radius:9px;padding:8px 14px;font-weight:700}
  .cart{position:sticky;bottom:0;background:var(--surface);border-top:1px solid var(--line);
        padding:12px 14px calc(12px + env(safe-area-inset-bottom));box-shadow:0 -6px 20px rgba(16,35,28,.07)}
  .line{display:flex;justify-content:space-between;align-items:center;gap:8px;padding:7px 0;font-size:14px}
  .qty{display:flex;align-items:center;gap:8px}
  .qty button{width:30px;height:30px;padding:0;background:var(--accent-soft);color:var(--accent);border-radius:8px;font-weight:700}
  .total{display:flex;justify-content:space-between;align-items:baseline;margin:8px 0 10px;font-size:15px}
  .total b{font-size:21px}
  .error{color:var(--danger);font-size:13px;margin-top:8px;min-height:18px}
  .ok{color:var(--accent);font-size:13px;margin-top:8px}
  .hide{display:none!important}
  .muted{color:var(--muted);font-size:13px}
  .grid2{display:grid;grid-template-columns:1fr 1fr;gap:10px}
</style>
</head>
<body>
<header>
  <div class="row">
    <div>
      <h1 id="company">Libro</h1>
      <div class="sub" id="user">Conectando…</div>
    </div>
    <div class="pill" id="totalPill">$0.00</div>
  </div>
</header>

<main>
  <!-- Emparejamiento -->
  <section id="pairView" class="card hide">
    <h2 style="font-size:17px;margin:0 0 4px">Conectar este teléfono</h2>
    <p class="muted" style="margin:0 0 14px">
      En la computadora abre Libro → Configuración → Ventas por celular y escribe el PIN de 6 dígitos.
    </p>
    <label for="pin">PIN</label>
    <input id="pin" inputmode="numeric" maxlength="6" placeholder="000000" autocomplete="one-time-code">
    <label for="deviceName" style="margin-top:12px">Nombre del teléfono</label>
    <input id="deviceName" placeholder="Teléfono de mostrador" maxlength="40">
    <div class="error" id="pairError"></div>
    <button class="primary" id="pairBtn" style="margin-top:8px">Emparejar</button>
  </section>

  <!-- Venta -->
  <section id="saleView" class="hide">
    <div class="card" style="padding:10px 14px">
      <input id="search" placeholder="Buscar producto…" autocomplete="off">
    </div>
    <div class="card" id="productList"><p class="muted">Cargando catálogo…</p></div>

    <div class="card hide" id="checkoutCard">
      <label for="contact">Cliente</label>
      <select id="contact"></select>
      <div class="grid2" style="margin-top:12px">
        <div>
          <label for="method">Pago</label>
          <select id="method">
            <option value="efectivo">Efectivo</option>
            <option value="tarjeta">Tarjeta (terminal)</option>
          </select>
        </div>
        <div>
          <label for="account">Cuenta</label>
          <select id="account"></select>
        </div>
      </div>
      <div class="error" id="saleError"></div>
      <div class="ok" id="saleOk"></div>
      <button class="primary" id="sellBtn" style="margin-top:10px">Cobrar</button>
    </div>
  </section>
</main>

<div class="cart hide" id="cartBar">
  <div id="cartLines"></div>
  <div class="total"><span>Total</span><b id="cartTotal">$0.00</b></div>
  <button class="primary" id="goCheckout">Continuar</button>
</div>

<script>
(function () {
  var token = localStorage.getItem("libro.token") || "";
  var catalog = { products: [], contacts: [], accounts: [] };
  var cart = [];

  var $ = function (id) { return document.getElementById(id); };
  var money = function (cents) {
    return "$" + (cents / 100).toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ",");
  };

  function request(path, options) {
    options = options || {};
    options.headers = Object.assign({ "Content-Type": "application/json" }, options.headers || {});
    if (token) options.headers.Authorization = "Bearer " + token;
    return fetch(path, options).then(function (response) {
      return response.json().catch(function () { return {}; }).then(function (body) {
        if (!response.ok) throw new Error(body.error || ("Error " + response.status));
        return body;
      });
    });
  }

  function showPairing() {
    $("pairView").classList.remove("hide");
    $("saleView").classList.add("hide");
    $("cartBar").classList.add("hide");
  }

  function renderCart() {
    var bar = $("cartBar");
    if (!cart.length) {
      bar.classList.add("hide");
      // Al cerrar la venta el total del encabezado debe volver a cero.
      $("totalPill").textContent = money(0);
      return;
    }
    bar.classList.remove("hide");
    var total = 0;
    var html = cart.map(function (line) {
      total += Math.round(line.qty * line.unitPriceCents * (1 + line.ivaRate));
      return '<div class="line"><span>' + line.description + '</span>' +
        '<span class="qty"><button data-dec="' + line.productId + '">−</button>' +
        '<span>' + line.qty + '</span>' +
        '<button data-inc="' + line.productId + '">+</button></span></div>';
    }).join("");
    $("cartLines").innerHTML = html;
    $("cartTotal").textContent = money(total);
    $("totalPill").textContent = money(total);
  }

  function addToCart(product) {
    var line = cart.filter(function (item) { return item.productId === product.id; })[0];
    if (line) { line.qty += 1; }
    else {
      cart.push({
        productId: product.id,
        description: product.name,
        qty: 1,
        unitPriceCents: product.priceCents,
        ivaRate: product.ivaRate
      });
    }
    $("checkoutCard").classList.remove("hide");
    renderCart();
  }

  function renderProducts(filter) {
    var needle = (filter || "").toLowerCase();
    var rows = catalog.products.filter(function (product) {
      return !needle || product.name.toLowerCase().indexOf(needle) >= 0 ||
             (product.sku || "").toLowerCase().indexOf(needle) >= 0;
    });
    if (!rows.length) {
      $("productList").innerHTML = '<p class="muted">Sin productos. Agrégalos en Libro → Inventario.</p>';
      return;
    }
    $("productList").innerHTML = rows.slice(0, 60).map(function (product) {
      var stock = product.trackStock ? " · " + product.stock + " en stock" : "";
      return '<div class="prod"><div><b>' + product.name + '</b>' +
        '<small>' + (product.sku || "Sin SKU") + stock + '</small></div>' +
        '<div style="display:flex;align-items:center;gap:12px">' +
        '<span class="price">' + money(product.priceCents) + '</span>' +
        '<button class="add" data-add="' + product.id + '">Agregar</button></div></div>';
    }).join("");
  }

  function fillSelect(id, items, placeholder) {
    var select = $(id);
    select.innerHTML = items.map(function (item) {
      return '<option value="' + item.id + '">' + item.name + '</option>';
    }).join("");
    if (!items.length) select.innerHTML = '<option value="0">' + placeholder + '</option>';
  }

  function loadCatalog() {
    return request("/api/catalog").then(function (data) {
      catalog = data;
      $("company").textContent = data.company || "Libro";
      $("user").textContent = (data.user || "") + " · " + data.products.length +
        (data.products.length === 1 ? " producto" : " productos");
      fillSelect("contact", data.contacts, "Sin clientes registrados");
      fillSelect("account", data.accounts, "Sin cuentas");
      renderProducts("");
      $("pairView").classList.add("hide");
      $("saleView").classList.remove("hide");
    }).catch(function (error) {
      if (String(error.message).indexOf("emparejado") >= 0) {
        localStorage.removeItem("libro.token");
        token = "";
        showPairing();
      } else {
        $("user").textContent = error.message;
      }
    });
  }

  $("pairBtn").addEventListener("click", function () {
    $("pairError").textContent = "";
    request("/api/pair", {
      method: "POST",
      body: JSON.stringify({
        pin: $("pin").value.trim(),
        deviceName: $("deviceName").value.trim() || "Teléfono"
      })
    }).then(function (data) {
      token = data.token;
      localStorage.setItem("libro.token", token);
      return loadCatalog();
    }).catch(function (error) {
      $("pairError").textContent = error.message;
    });
  });

  $("search").addEventListener("input", function (event) { renderProducts(event.target.value); });

  document.addEventListener("click", function (event) {
    var add = event.target.getAttribute && event.target.getAttribute("data-add");
    if (add) {
      var product = catalog.products.filter(function (item) { return String(item.id) === add; })[0];
      if (product) addToCart(product);
      return;
    }
    var inc = event.target.getAttribute && event.target.getAttribute("data-inc");
    if (inc) {
      cart.forEach(function (line) { if (String(line.productId) === inc) line.qty += 1; });
      renderCart();
      return;
    }
    var dec = event.target.getAttribute && event.target.getAttribute("data-dec");
    if (dec) {
      cart = cart.filter(function (line) {
        if (String(line.productId) !== dec) return true;
        line.qty -= 1;
        return line.qty > 0;
      });
      if (!cart.length) $("checkoutCard").classList.add("hide");
      renderCart();
    }
  });

  $("goCheckout").addEventListener("click", function () {
    $("checkoutCard").scrollIntoView({ behavior: "smooth" });
  });

  $("sellBtn").addEventListener("click", function () {
    $("saleError").textContent = "";
    $("saleOk").textContent = "";
    if (!cart.length) { $("saleError").textContent = "Agrega al menos un producto."; return; }
    $("sellBtn").disabled = true;

    var method = $("method").value;
    request("/api/sales", {
      method: "POST",
      body: JSON.stringify({
        contactId: Number($("contact").value),
        lines: cart.map(function (line) {
          return {
            productId: line.productId,
            description: line.description,
            qty: line.qty,
            unitPriceCents: line.unitPriceCents,
            ivaRate: line.ivaRate
          };
        }),
        payment: {
          method: method,
          accountId: Number($("account").value) || null,
          requireTerminal: false
        }
      })
    }).then(function (data) {
      $("saleOk").textContent = "Venta " + data.folio + " por " + money(data.totalCents) +
        (data.paid ? " · cobrada" : " · pendiente de cobro") +
        (data.terminal ? " (" + data.terminal + ")" : "");
      cart = [];
      renderCart();
      $("checkoutCard").classList.add("hide");
    }).catch(function (error) {
      $("saleError").textContent = error.message;
    }).finally(function () {
      $("sellBtn").disabled = false;
    });
  });

  $("deviceName").value = "Teléfono";
  if (token) loadCatalog(); else showPairing();
})();
</script>
</body>
</html>
""";
}
