using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ventana de confirmación de compra ("Comprar por X monedas"). Se crea sola, por código,
// dentro del Canvas del HUD la primera vez que se necesita: no hay nada que colocar en la escena.
public class ConfirmarCompraUI : MonoBehaviour
{
    static ConfirmarCompraUI instancia;

    GameObject raiz;
    TMP_Text titulo, detalle;
    Button botonComprar;
    Action alConfirmar;

    static readonly Color ColorVelo = new Color(0f, 0f, 0f, 0.6f);
    static readonly Color ColorPanel = new Color(0.20f, 0.12f, 0.07f, 0.97f);
    static readonly Color ColorBoton = new Color(0.96f, 0.92f, 0.84f, 1f);
    static readonly Color ColorTextoBoton = new Color(0.12f, 0.08f, 0.04f, 1f);

    public static void Mostrar(string titulo, int precio, int monedas, Action alConfirmar)
    {
        if (instancia == null) instancia = CrearInstancia();
        if (instancia == null) return;
        instancia.Abrir(titulo, precio, monedas, alConfirmar);
    }

    static ConfirmarCompraUI CrearInstancia()
    {
        Canvas canvas = null;
        var hud = FindFirstObjectByType<HudJuego>();
        if (hud != null) canvas = hud.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.renderMode != RenderMode.WorldSpace) { canvas = c; break; }
        }
        if (canvas == null) return null;

        var go = new GameObject("ConfirmarCompraUI", typeof(RectTransform));
        go.transform.SetParent(canvas.rootCanvas.transform, false);
        var ui = go.AddComponent<ConfirmarCompraUI>();
        ui.Construir();
        return ui;
    }

    void OnDestroy() { if (instancia == this) instancia = null; }

    void Abrir(string textoTitulo, int precio, int monedas, Action confirmar)
    {
        alConfirmar = confirmar;
        titulo.text = textoTitulo;
        bool alcanza = monedas >= precio;
        detalle.text = alcanza
            ? "Precio: " + precio + " monedas\nTienes: " + monedas + " monedas"
            : "Precio: " + precio + " monedas\nTienes: " + monedas + ". Te faltan " + (precio - monedas) + ".";
        botonComprar.interactable = alcanza;
        raiz.transform.SetAsLastSibling();
        raiz.SetActive(true);
    }

    void Cerrar() { raiz.SetActive(false); }

    void Comprar()
    {
        var accion = alConfirmar;
        alConfirmar = null;
        Cerrar();
        if (accion != null) accion();
    }

    void Construir()
    {
        var velo = Hijo("Velo", transform);
        velo.anchorMin = Vector2.zero;
        velo.anchorMax = Vector2.one;
        velo.offsetMin = Vector2.zero;
        velo.offsetMax = Vector2.zero;
        velo.gameObject.AddComponent<Image>().color = ColorVelo;
        raiz = velo.gameObject;

        var panel = Hijo("Panel", velo);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(860f, 520f);
        panel.gameObject.AddComponent<Image>().color = ColorPanel;

        var vertical = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(32, 32, 32, 32);
        vertical.spacing = 20f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        titulo = Texto(panel, "Titulo", 50f, Color.white);
        titulo.gameObject.AddComponent<LayoutElement>().preferredHeight = 80f;

        detalle = Texto(panel, "Detalle", 36f, Color.white);
        detalle.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        var botones = Hijo("Botones", panel);
        var horizontal = botones.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 20f;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = true;
        horizontal.childForceExpandHeight = true;
        botones.gameObject.AddComponent<LayoutElement>().preferredHeight = 110f;

        botonComprar = Boton(botones, "Comprar", "Comprar", Comprar);
        Boton(botones, "Cancelar", "Cancelar", Cerrar);

        raiz.SetActive(false);
    }

    static RectTransform Hijo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    static TMP_Text Texto(Transform padre, string nombre, float tamano, Color color)
    {
        var rt = Hijo(nombre, padre);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.fontSize = tamano;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.raycastTarget = false;
        t.enableAutoSizing = true;
        t.fontSizeMax = tamano;
        t.fontSizeMin = Mathf.Max(14f, tamano * 0.55f);
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    static Button Boton(Transform padre, string nombre, string texto, UnityEngine.Events.UnityAction alPulsar)
    {
        var rt = Hijo(nombre, padre);
        var imagen = rt.gameObject.AddComponent<Image>();
        imagen.color = ColorBoton;
        var boton = rt.gameObject.AddComponent<Button>();
        boton.targetGraphic = imagen;
        rt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var etiqueta = Texto(rt, "Texto", 40f, ColorTextoBoton);
        etiqueta.rectTransform.anchorMin = Vector2.zero;
        etiqueta.rectTransform.anchorMax = Vector2.one;
        etiqueta.rectTransform.offsetMin = new Vector2(6f, 6f);
        etiqueta.rectTransform.offsetMax = new Vector2(-6f, -6f);
        etiqueta.text = texto;

        boton.onClick.AddListener(alPulsar);
        return boton;
    }
}
