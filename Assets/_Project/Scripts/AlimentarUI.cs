using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ventana para elegir qué animal de un corral alimentar y con qué. Se crea sola, por código,
// dentro del Canvas del HUD la primera vez que se abre un corral.
public class AlimentarUI : MonoBehaviour
{
    static AlimentarUI instancia;

    class Fila
    {
        public TMP_Text etiqueta;
        public readonly List<TMP_Text> textos = new List<TMP_Text>();
        public readonly List<Alimento> alimentos = new List<Alimento>();
    }

    static readonly Color ColorVelo = new Color(0f, 0f, 0f, 0.6f);
    static readonly Color ColorPanel = new Color(0.20f, 0.12f, 0.07f, 0.97f);
    static readonly Color ColorFila = new Color(0.30f, 0.19f, 0.11f, 1f);
    static readonly Color ColorBoton = new Color(0.96f, 0.92f, 0.84f, 1f);
    static readonly Color ColorTextoBoton = new Color(0.12f, 0.08f, 0.04f, 1f);
    static readonly Color ColorMensaje = new Color(0.55f, 0.90f, 0.45f, 1f);

    GameObject raiz;
    TMP_Text titulo, mensaje;
    RectTransform lista;
    Corral corral;
    readonly List<Fila> filas = new List<Fila>();
    int cantidadFilas = -1;
    float proximoRefresco;

    public static void Abrir(Corral c)
    {
        if (instancia == null) instancia = CrearInstancia();
        if (instancia == null) return;
        instancia.Mostrar(c);
    }

    static AlimentarUI CrearInstancia()
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

        var go = new GameObject("AlimentarUI", typeof(RectTransform));
        go.transform.SetParent(canvas.rootCanvas.transform, false);
        var ui = go.AddComponent<AlimentarUI>();
        ui.Construir();
        return ui;
    }

    void OnDestroy() { if (instancia == this) instancia = null; }

    void Mostrar(Corral c)
    {
        corral = c;
        mensaje.text = "";
        cantidadFilas = -1;
        raiz.transform.SetAsLastSibling();
        raiz.SetActive(true);
        Refrescar();
    }

    void Cerrar()
    {
        corral = null;
        raiz.SetActive(false);
    }

    void Update()
    {
        if (!raiz.activeSelf || Time.unscaledTime < proximoRefresco) return;
        proximoRefresco = Time.unscaledTime + 0.25f;
        Refrescar();
    }

    // ---------- Contenido ----------

    void Refrescar()
    {
        if (corral == null) { raiz.SetActive(false); return; }

        var info = corral.Listar();
        titulo.text = "Alimentar " + AnimalDatos.Plural(corral.Especie) + " (" + corral.Cantidad + "/" + corral.CapacidadTotal + ")";

        if (info.Count != cantidadFilas) Reconstruir(info);

        var juego = EstadoJuego.Instance;
        for (int i = 0; i < filas.Count && i < info.Count; i++)
        {
            filas[i].etiqueta.text = info[i].nombre + ": " + info[i].estado;
            for (int k = 0; k < filas[i].alimentos.Count; k++)
            {
                Alimento al = filas[i].alimentos[k];
                int stock = juego != null ? juego.CantidadAlimento(al) : 0;
                filas[i].textos[k].text = AlimentoDatos.Titulo(al) + " (" + stock + ")";
                filas[i].textos[k].transform.parent.GetComponent<Button>().interactable = info[i].puedeComer;
            }
        }
    }

    void Reconstruir(List<InfoAnimal> info)
    {
        for (int i = lista.childCount - 1; i >= 0; i--)
        {
            var hijo = lista.GetChild(i).gameObject;
            hijo.SetActive(false);
            Destroy(hijo);
        }
        filas.Clear();
        cantidadFilas = info.Count;

        foreach (var animal in info)
        {
            var fila = new Fila();
            var rt = Hijo("Fila", lista);
            rt.gameObject.AddComponent<Image>().color = ColorFila;
            var vertical = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(16, 16, 10, 10);
            vertical.spacing = 8f;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            fila.etiqueta = Texto(rt, "Etiqueta", 32f, TextAlignmentOptions.MidlineLeft, Color.white);
            fila.etiqueta.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            var botones = Hijo("Botones", rt);
            var horizontal = botones.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 12f;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = true;
            horizontal.childForceExpandHeight = true;
            botones.gameObject.AddComponent<LayoutElement>().preferredHeight = 80f;

            int indice = animal.indice;
            foreach (var alimento in AnimalDatos.Alimentos(corral.Especie))
            {
                Alimento al = alimento;
                TMP_Text texto;
                Boton(botones, "Dar_" + al, AlimentoDatos.Titulo(al), () => Alimentar(indice, al), out texto);
                fila.alimentos.Add(al);
                fila.textos.Add(texto);
            }
            filas.Add(fila);
        }
    }

    void Alimentar(int indice, Alimento alimento)
    {
        if (corral == null) return;
        corral.Alimentar(indice, alimento, out string texto);
        mensaje.text = texto;
        Refrescar();
    }

    // ---------- Construcción ----------

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
        panel.sizeDelta = new Vector2(920f, 1000f);
        panel.gameObject.AddComponent<Image>().color = ColorPanel;

        var vertical = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(28, 28, 28, 28);
        vertical.spacing = 16f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        titulo = Texto(panel, "Titulo", 46f, TextAlignmentOptions.Center, Color.white);
        titulo.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

        lista = Hijo("Lista", panel);
        var capa = lista.gameObject.AddComponent<VerticalLayoutGroup>();
        capa.spacing = 12f;
        capa.childControlWidth = true;
        capa.childControlHeight = true;
        capa.childForceExpandWidth = true;
        capa.childForceExpandHeight = false;
        lista.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        mensaje = Texto(panel, "Mensaje", 30f, TextAlignmentOptions.Center, ColorMensaje);
        mensaje.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;

        var cerrar = Hijo("Cerrar", panel);
        var imagen = cerrar.gameObject.AddComponent<Image>();
        imagen.color = ColorBoton;
        var boton = cerrar.gameObject.AddComponent<Button>();
        boton.targetGraphic = imagen;
        cerrar.gameObject.AddComponent<LayoutElement>().preferredHeight = 100f;
        var etiqueta = Texto(cerrar, "Texto", 40f, TextAlignmentOptions.Center, ColorTextoBoton);
        etiqueta.rectTransform.anchorMin = Vector2.zero;
        etiqueta.rectTransform.anchorMax = Vector2.one;
        etiqueta.rectTransform.offsetMin = new Vector2(6f, 6f);
        etiqueta.rectTransform.offsetMax = new Vector2(-6f, -6f);
        etiqueta.text = "Cerrar";
        boton.onClick.AddListener(Cerrar);

        raiz.SetActive(false);
    }

    static RectTransform Hijo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
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

    static void Boton(Transform padre, string nombre, string texto, UnityEngine.Events.UnityAction alPulsar, out TMP_Text etiqueta)
    {
        var rt = Hijo(nombre, padre);
        var imagen = rt.gameObject.AddComponent<Image>();
        imagen.color = ColorBoton;
        var boton = rt.gameObject.AddComponent<Button>();
        boton.targetGraphic = imagen;
        rt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        etiqueta = Texto(rt, "Texto", 36f, TextAlignmentOptions.Center, ColorTextoBoton);
        etiqueta.rectTransform.anchorMin = Vector2.zero;
        etiqueta.rectTransform.anchorMax = Vector2.one;
        etiqueta.rectTransform.offsetMin = new Vector2(6f, 6f);
        etiqueta.rectTransform.offsetMax = new Vector2(-6f, -6f);
        etiqueta.text = texto;

        boton.onClick.AddListener(alPulsar);
    }
}
