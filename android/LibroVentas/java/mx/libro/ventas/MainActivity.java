package mx.libro.ventas;

import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.os.Bundle;
import android.text.InputType;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.Menu;
import android.view.MenuItem;
import android.view.View;
import android.view.ViewGroup;
import android.view.inputmethod.EditorInfo;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;

/**
 * Envoltorio nativo de la interfaz de ventas de Libro.
 *
 * La aplicación no reimplementa nada: carga la página que sirve Libro en la
 * computadora por la red local. Todo el estado de la venta vive en la base
 * cifrada de la PC; el teléfono solo es una pantalla.
 *
 * Se usa WebView y no un navegador externo para que Android no bloquee el
 * tráfico sin cifrar (usesCleartextTraffic) y para conservar el token de
 * emparejamiento en el almacenamiento propio de la app.
 */
public class MainActivity extends Activity {

    private static final String PREFS = "libro";
    private static final String KEY_URL = "server_url";
    private static final int MENU_CHANGE_SERVER = 1;

    private WebView webView;
    private LinearLayout setupPanel;
    private EditText urlInput;
    private TextView hintText;
    private String currentUrl = "";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setTitle("Libro Ventas");

        FrameLayout root = new FrameLayout(this);

        webView = new WebView(this);
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        // La página guarda el token de emparejamiento en localStorage.
        settings.setDomStorageEnabled(true);
        settings.setAllowFileAccess(false);
        settings.setMediaPlaybackRequiresUserGesture(true);
        webView.setBackgroundColor(Color.parseColor("#F5F5F1"));

        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
                if (request == null || !request.isForMainFrame()) return;
                CharSequence description = error == null ? "" : error.getDescription();
                showSetup("No se pudo conectar con la computadora.\n" + description
                        + "\n\nVerifica que Libro esté abierto y que el teléfono esté en la misma red Wi-Fi.");
            }
        });

        root.addView(webView, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        setupPanel = buildSetupPanel();
        root.addView(setupPanel, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));

        setContentView(root);

        String saved = prefs().getString(KEY_URL, "");
        if (saved == null || saved.trim().isEmpty()) {
            showSetup(null);
        } else {
            currentUrl = saved;
            urlInput.setText(saved);
            setupPanel.setVisibility(View.GONE);
            webView.setVisibility(View.VISIBLE);
            webView.loadUrl(saved);
        }
    }

    // ------------------------------------------------------------ interfaz

    private LinearLayout buildSetupPanel() {
        LinearLayout panel = new LinearLayout(this);
        panel.setOrientation(LinearLayout.VERTICAL);
        panel.setGravity(Gravity.CENTER);
        panel.setBackgroundColor(Color.parseColor("#F5F5F1"));
        panel.setPadding(dp(28), dp(28), dp(28), dp(28));

        TextView title = new TextView(this);
        title.setText("Libro Ventas");
        title.setTextSize(24);
        title.setTextColor(Color.parseColor("#10231C"));
        title.setGravity(Gravity.CENTER);
        panel.addView(title);

        hintText = new TextView(this);
        hintText.setText("Escribe la dirección que muestra Libro en la computadora:\n"
                + "Configuración → Ventas por celular.");
        hintText.setTextSize(14);
        hintText.setTextColor(Color.parseColor("#667085"));
        hintText.setGravity(Gravity.CENTER);
        hintText.setPadding(0, dp(12), 0, dp(20));
        panel.addView(hintText);

        urlInput = new EditText(this);
        urlInput.setSingleLine(true);
        urlInput.setInputType(InputType.TYPE_TEXT_VARIATION_URI);
        urlInput.setHint("http://192.168.1.10:8787");
        urlInput.setImeOptions(EditorInfo.IME_ACTION_DONE);
        panel.addView(urlInput, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        Button connect = new Button(this);
        connect.setText("Conectar");
        connect.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                connect();
            }
        });
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        params.topMargin = dp(16);
        panel.addView(connect, params);

        return panel;
    }

    private void showSetup(String message) {
        setupPanel.setVisibility(View.VISIBLE);
        webView.setVisibility(View.INVISIBLE);
        if (!currentUrl.isEmpty()) urlInput.setText(currentUrl);
        hintText.setText(message != null ? message
                : "Escribe la dirección que muestra Libro en la computadora:\n"
                  + "Configuración → Ventas por celular.");
    }

    private void connect() {
        String url = normalize(urlInput.getText().toString());
        if (url == null) {
            hintText.setText("La dirección no parece válida.\nEjemplo: http://192.168.1.10:8787");
            return;
        }
        currentUrl = url;
        prefs().edit().putString(KEY_URL, url).apply();
        setupPanel.setVisibility(View.GONE);
        webView.setVisibility(View.VISIBLE);
        webView.loadUrl(url);
    }

    private static String normalize(String raw) {
        if (raw == null) return null;
        String value = raw.trim();
        if (value.isEmpty()) return null;
        if (!value.startsWith("http://") && !value.startsWith("https://")) value = "http://" + value;
        while (value.length() > 8 && value.endsWith("/")) {
            value = value.substring(0, value.length() - 1);
        }
        return value;
    }

    private int dp(int value) {
        return Math.round(TypedValue.applyDimension(
                TypedValue.COMPLEX_UNIT_DIP, value, getResources().getDisplayMetrics()));
    }

    private SharedPreferences prefs() {
        return getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    // ------------------------------------------------------------- menú y atrás

    @Override
    public boolean onCreateOptionsMenu(Menu menu) {
        menu.add(0, MENU_CHANGE_SERVER, 0, "Cambiar servidor");
        return true;
    }

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        if (item.getItemId() == MENU_CHANGE_SERVER) {
            showSetup(null);
            return true;
        }
        return super.onOptionsItemSelected(item);
    }

    @Override
    public void onBackPressed() {
        if (setupPanel.getVisibility() == View.VISIBLE) {
            finish();
            return;
        }
        if (webView.canGoBack()) {
            webView.goBack();
            return;
        }
        super.onBackPressed();
    }

    @Override
    protected void onDestroy() {
        if (webView != null) {
            webView.loadUrl("about:blank");
            webView.destroy();
        }
        super.onDestroy();
    }
}
