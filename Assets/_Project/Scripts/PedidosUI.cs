using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Panel del pedido de la caja. Va en un objeto vacío (PanelPedidos) dentro del Canvas del HUD;
// el panel se construye por código y empieza oculto. Se abre al tocar la caja de entrega.
public class PedidosUI : MonoBehaviour
{
    public static PedidosUI Instance { get; private set; }

    [Header("Zona del panel dentro de la pantalla (unidades del Canvas)")]
    [SerializeField] float margenLateral = 40f;
    [SerializeField] float margenSuperior = 260f;
    [SerializeField] float margenInferior = 330f;

    [Header("Aspecto")]
    [SerializeField] Color colorVelo = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField] Color colorPanel = new Color(0.20f, 0.12f, 0.07f, 0.97f);
    [SerializeField] Color colorFila = new Color(0.30f, 0.19f, 0.11f, 1f);
    [SerializeField] Color colorBoton = new Color(0.96f, 0.92f, 0.84f, 1f);
    [SerializeField] Color colorTextoBoton = new Color(0.12f, 0.08f, 0.04f, 1f);
    [SerializeField] Color colorTexto = Color.white;
    [SerializeField] Color colorCompleta = new Color(0.55f, 0.90f, 0.45f, 1f);

    class FilaUI
    {
        public TMP_Text etiqueta;
        public Button llenar, quitar;
    }

    GameObject raiz;
    RectTransform lista;
    TMP_Text info, mensaje, recompensa, textoEntregar;
    Button botonEntregar;
    readonly List<FilaUI> filas = new List<FilaUI>();
    PedidosCaja caja;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        Construir();
        raiz.SetActive(false);
    }

    // ---------- Abrir y cerrar ----------

    public void Abrir(PedidosCaja nueva)
    {
        if (raiz == null || nueva == null) return;
        caja = nueva;
        caja.OnCambio += Refrescar;
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio += Refrescar;

        mensaje.text = "";
        ReconstruirFilas();
        raiz.transform.SetAsLastSibling();
        raiz.SetActive(true);
        Refrescar();
    }

    void Cerrar()
    {
        if (caja == null) return;
        caja.OnCambio -= Refrescar;
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio -= Refrescar;
        raiz.SetActive(false);

        var c = caja;
        caja = null;
        c.AlCerrarPanel();
    }

    // ---------- Construcción ----------

    void Construir()
    {
        var canvas = GetComponentInParent<Canvas>();
        Transform padre = canvas != null ? canvas.rootCanvas.transform : transform;

        // Velo que tapa toda la pantalla (así no se toca el mundo por detrás).
        var velo = Hijo("Pedidos_Velo", padre);
        Estirar(velo, 0f);
        var imagenVelo = velo.gameObject.AddComponent<Image>();
        imagenVelo.color = colorVelo;
        raiz = velo.gameObject;

        var panel = Hijo("Panel", velo);
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = new Vector2(margenLateral, margenInferior);
        panel.offsetMax = new Vector2(-margenLateral, -margenSuperior);
        panel.gameObject.AddComponent<Image>().color = colorPanel;

        var vertical = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(24, 24, 24, 24);
        vertical.spacing = 14f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        var titulo = Texto(panel, "Titulo", 46f, TextAlignmentOptions.Center, colorTexto);
        titulo.text = "Pedido de la caja";
        titulo.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;

        info = Texto(panel, "Info", 30f, TextAlignmentOptions.Center, colorTexto);
        info.gameObject.AddComponent<LayoutElement>().preferredHeight = 90f;

        lista = Hijo("Lineas", panel);
        var capaLista = lista.gameObject.AddComponent<VerticalLayoutGroup>();
        capaLista.spacing = 12f;
        capaLista.childControlWidth = true;
        capaLista.childControlHeight = true;
        capaLista.childForceExpandWidth = true;
        capaLista.childForceExpandHeight = false;
        var espacio = lista.gameObject.AddComponent<LayoutElement>();
        espacio.flexibleHeight = 1f;

        mensaje = Texto(panel, "Mensaje", 28f, TextAlignmentOptions.Center, colorCompleta);
        mensaje.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;

        recompensa = Texto(panel, "Recompensa", 34f, TextAlignmentOptions.Center, colorTexto);
        recompensa.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;

        var botones = Hijo("Botones", panel);
        var horizontal = botones.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 16f;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = true;
        horizontal.childForceExpandHeight = true;
        botones.gameObject.AddComponent<LayoutElement>().preferredHeight = 100f;

        botonEntregar = Boton(botones, "Entregar", "Entregar", 0f, 100f, Entregar, out textoEntregar);
        Boton(botones, "Cerrar", "Cerrar", 0f, 100f, Cerrar, out _);
    }

    void ReconstruirFilas()
    {
        for (int i = lista.childCount - 1; i >= 0; i--)
        {
            var hijo = lista.GetChild(i).gameObject;
            hijo.SetActive(false);
            Destroy(hijo);
        }
        filas.Clear();

        var pedido = caja.Pedido;
        if (pedido == null) return;

        for (int i = 0; i < pedido.lineas.Length; i++)
        {
            int indice = i;
            var fila = new FilaUI();

            var rt = Hijo("Linea_" + i, lista);
            rt.gameObject.AddComponent<Image>().color = colorFila;
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(16, 16, 10, 10);
            h.spacing = 10f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            fila.etiqueta = Texto(rt, "Etiqueta", 36f, TextAlignmentOptions.MidlineLeft, colorTexto);
            fila.etiqueta.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            fila.quitar = Boton(rt, "Quitar", "Quitar", 170f, 0f, () => Quitar(indice), out _);
            fila.llenar = Boton(rt, "Llenar", "Llenar", 170f, 0f, () => Llenar(indice), out _);
            filas.Add(fila);
        }
    }

    static RectTransform Hijo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    static void Estirar(RectTransform rt, float margen)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margen, margen);
        rt.offsetMax = new Vector2(-margen, -margen);
    }

    static TMP_Text Texto(Transform padre, string nombre, float tamano, TextAlignmentOptions alineacion, Color color)
    {
        var rt = Hijo(nombre, padre);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.fontSize = tamano;
        t.alignment = alineacion;
        t.color = color;
        t.raycastTarget = false;
        t.enableAutoSizing = true;
        t.fontSizeMax = tamano;
        t.fontSizeMin = Mathf.Max(14f, tamano * 0.55f);
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    // ancho 0 -> se reparte el espacio libre; alto 0 -> toma el de la fila.
    Button Boton(Transform padre, string nombre, string texto, float ancho, float alto,
                 UnityAction alPulsar, out TMP_Text etiqueta)
    {
        var rt = Hijo(nombre, padre);
        var imagen = rt.gameObject.AddComponent<Image>();
        imagen.color = colorBoton;
        var boton = rt.gameObject.AddComponent<Button>();
        boton.targetGraphic = imagen;

        var medida = rt.gameObject.AddComponent<LayoutElement>();
        if (ancho > 0f) medida.preferredWidth = ancho; else medida.flexibleWidth = 1f;
        if (alto > 0f) medida.preferredHeight = alto;

        etiqueta = Texto(rt, "Texto", 36f, TextAlignmentOptions.Center, colorTextoBoton);
        Estirar(etiqueta.rectTransform, 6f);
        etiqueta.text = texto;

        boton.onClick.AddListener(alPulsar);
        return boton;
    }

    // ---------- Acciones ----------

    void Llenar(int indice)
    {
        if (caja == null) return;
        var linea = caja.Pedido.lineas[indice];
        int n = caja.Llenar(indice);
        mensaje.text = n > 0
            ? $"Pusiste {n} de {EstadoJuego.NombreProducto(linea.P)} en la caja."
            : $"No tienes {EstadoJuego.NombreProducto(linea.P)} para poner.";
    }

    void Quitar(int indice)
    {
        if (caja == null) return;
        var linea = caja.Pedido.lineas[indice];
        int n = caja.Quitar(indice);
        mensaje.text = n > 0 ? $"Devolviste {n} de {EstadoJuego.NombreProducto(linea.P)} a tu inventario." : "";
    }

    void Entregar()
    {
        if (caja == null) return;
        if (caja.Entregar(out int monedas))
            mensaje.text = $"¡Pedido entregado! +{monedas} monedas";
    }

    // ---------- Textos ----------

    static string Titulo(Producto p)
    {
        string n = EstadoJuego.NombreProducto(p);
        return char.ToUpper(n[0]) + n.Substring(1);
    }

    void Refrescar()
    {
        var j = EstadoJuego.Instance;
        if (caja == null || j == null) return;
        var pedido = caja.Pedido;
        if (pedido == null) return;

        // La caja se renovó mientras el panel estaba abierto: se vuelven a crear las líneas.
        if (filas.Count != pedido.lineas.Length) ReconstruirFilas();

        for (int i = 0; i < filas.Count && i < pedido.lineas.Length; i++)
        {
            var l = pedido.lineas[i];
            var f = filas[i];
            int tiene = j.Cantidad(l.P);
            bool completa = l.cargado >= l.requerido;

            f.etiqueta.text = $"{Titulo(l.P)}  {l.cargado}/{l.requerido}   <size=65%>(tienes {tiene})</size>";
            f.etiqueta.color = completa ? colorCompleta : colorTexto;
            f.llenar.interactable = !pedido.entregado && !completa && tiene > 0;
            f.quitar.interactable = !pedido.entregado && l.cargado > 0;
        }

        int premio = pedido.entregado ? caja.UltimaRecompensa : caja.Recompensa();
        recompensa.text = pedido.entregado
            ? $"Recibiste {premio} monedas"
            : $"Recompensa: {premio} monedas (el doble de su precio)";

        info.text = pedido.entregado
            ? "Cierra el panel: la caja se llevará el pedido y llegará uno nuevo."
            : pedido.tutorial
                ? "Pedido de la pizarra: llena la línea con tus cosechas y entrégalo."
                : "Llena cada línea con tus cosechas y entrega el pedido completo.";

        botonEntregar.interactable = !pedido.entregado && caja.TodoLleno;
        textoEntregar.text = pedido.entregado ? "Entregado" : "Entregar";
    }
}
