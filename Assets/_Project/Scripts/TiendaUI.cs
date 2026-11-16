using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Tienda con 5 pestañas: Vender, Semillas, Bonus, Edificios y Animales. Todo el contenido se crea por código
// dentro del panel que ya tienes; los elementos antiguos de la tienda se ocultan solos.
public class TiendaUI : MonoBehaviour
{
    enum Pestana { Vender, Semillas, Bonus, Edificios, Animales }

    [Header("Abrir y cerrar")]
    [SerializeField] Button botonTienda;
    [SerializeField] GameObject panel;
    [SerializeField] Button botonCerrar;

    [Header("Mensajes")]
    [SerializeField] TMP_Text textoMensaje;

    [Header("Zona del contenido dentro del panel (unidades del Canvas)")]
    [Tooltip("Espacio libre arriba (para el título 'Tienda').")]
    [SerializeField] float margenSuperior = 130f;
    [Tooltip("Espacio libre abajo (para el mensaje y el botón Cerrar).")]
    [SerializeField] float margenInferior = 260f;
    [SerializeField] float margenLateral = 30f;

    [Header("Aspecto")]
    [SerializeField] Color colorFila = new Color(0.16f, 0.10f, 0.06f, 0.92f);
    [SerializeField] Color colorBoton = new Color(0.96f, 0.92f, 0.84f, 1f);
    [SerializeField] Color colorTextoBoton = new Color(0.12f, 0.08f, 0.04f, 1f);
    [SerializeField] Color colorTexto = Color.white;
    [SerializeField] Color colorPestana = new Color(0.35f, 0.22f, 0.12f, 1f);
    [SerializeField] Color colorPestanaElegida = new Color(0.96f, 0.76f, 0.20f, 1f);

    [Header("Elementos antiguos (se ocultan solos; puedes borrarlos del panel)")]
    [SerializeField] FilaVenta filaTomates;
    [SerializeField] FilaVenta filaHuevos;
    [SerializeField] FilaVenta filaSemilla;
    [SerializeField] FilaVenta filaPollo;
    [SerializeField] TMP_Text textoBonus;
    [SerializeField] Button botonBonusTomate;
    [SerializeField] Button botonBonusHuevo;
    [SerializeField] Button botonBonusPollo;

    class Fila
    {
        public Producto producto;
        public Cultivo cultivo;
        public bool compra;     // true = compra de semillas
        public bool unica;      // true = se vende de a una unidad (pollo, semilla legendaria)
        public TMP_Text etiqueta, cantidad, accion;
        public Button menos, mas, max, boton;
    }

    class FilaBonus
    {
        public Producto producto;
        public Button boton;
    }

    // Fila genérica de las pestañas Edificios y Animales: texto y botón calculados en cada refresco.
    class FilaGen
    {
        public TMP_Text etiqueta, accion;
        public Button boton;
        public System.Func<string> texto;
        public System.Func<string> textoAccion;
        public System.Func<bool> activa;
    }

    RectTransform contenido;
    ScrollRect scroll;
    TMP_Text estadoBonus;
    readonly List<Image> fondosPestana = new List<Image>();
    readonly List<TMP_Text> textosPestana = new List<TMP_Text>();
    readonly List<Fila> filas = new List<Fila>();
    readonly List<FilaBonus> filasBonus = new List<FilaBonus>();
    readonly List<FilaGen> filasGen = new List<FilaGen>();

    Pestana pestana = Pestana.Vender;
    string firma = "";
    readonly int[] cantVenta = new int[System.Enum.GetValues(typeof(Producto)).Length];
    readonly int[] cantCompra = new int[System.Enum.GetValues(typeof(Cultivo)).Length];

    Producto pendiente = Producto.Ninguno;
    float vencePendiente;
    float proximoRefresco;

    // ---------- Inicio ----------

    void Start()
    {
        OcultarAntiguos();
        ConstruirContenido();

        panel.SetActive(false);
        botonTienda.gameObject.SetActive(false);
        botonTienda.onClick.AddListener(Abrir);
        botonCerrar.onClick.AddListener(Cerrar);

        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio += Refrescar;
        Refrescar();
    }

    void OnDestroy()
    {
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio -= Refrescar;
    }

    void OcultarAntiguos()
    {
        OcultarFila(filaTomates);
        OcultarFila(filaHuevos);
        OcultarFila(filaSemilla);
        OcultarFila(filaPollo);
        if (textoBonus != null) textoBonus.gameObject.SetActive(false);
        if (botonBonusTomate != null) botonBonusTomate.gameObject.SetActive(false);
        if (botonBonusHuevo != null) botonBonusHuevo.gameObject.SetActive(false);
        if (botonBonusPollo != null) botonBonusPollo.gameObject.SetActive(false);
    }

    static void OcultarFila(FilaVenta fila)
    {
        if (fila != null) fila.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!panel.activeSelf || Time.unscaledTime < proximoRefresco) return;
        proximoRefresco = Time.unscaledTime + 0.25f;
        Refrescar();
    }

    void Abrir()
    {
        textoMensaje.text = "";
        pendiente = Producto.Ninguno;
        for (int i = 0; i < cantVenta.Length; i++) cantVenta[i] = 1;
        for (int i = 0; i < cantCompra.Length; i++) cantCompra[i] = 1;
        panel.SetActive(true);
        ElegirPestana(Pestana.Vender);
    }

    void Cerrar()
    {
        pendiente = Producto.Ninguno;
        panel.SetActive(false);
    }

    // ---------- Construcción de la interfaz ----------

    void ConstruirContenido()
    {
        var raiz = Hijo("Contenido", panel.transform);
        raiz.anchorMin = Vector2.zero;
        raiz.anchorMax = Vector2.one;
        raiz.offsetMin = new Vector2(margenLateral, margenInferior);
        raiz.offsetMax = new Vector2(-margenLateral, -margenSuperior);

        // Pestañas
        var barra = Hijo("Pestanas", raiz);
        barra.anchorMin = new Vector2(0f, 1f);
        barra.anchorMax = new Vector2(1f, 1f);
        barra.pivot = new Vector2(0.5f, 1f);
        barra.anchoredPosition = Vector2.zero;
        barra.sizeDelta = new Vector2(0f, 90f);
        var capaBarra = barra.gameObject.AddComponent<HorizontalLayoutGroup>();
        capaBarra.spacing = 10f;
        capaBarra.childControlWidth = true;
        capaBarra.childControlHeight = true;
        capaBarra.childForceExpandWidth = true;
        capaBarra.childForceExpandHeight = true;

        string[] nombres = { "Vender", "Semillas", "Bonus", "Edificios", "Animales" };
        for (int i = 0; i < nombres.Length; i++)
        {
            var p = (Pestana)i;
            var b = CrearBoton(barra, "Pestana_" + nombres[i], nombres[i], 0f, 90f, colorPestana, colorTexto,
                () => ElegirPestana(p), out TMP_Text texto);
            fondosPestana.Add(b.GetComponent<Image>());
            textosPestana.Add(texto);
        }

        // Lista con desplazamiento
        var lista = Hijo("Lista", raiz);
        lista.anchorMin = Vector2.zero;
        lista.anchorMax = Vector2.one;
        lista.offsetMin = Vector2.zero;
        lista.offsetMax = new Vector2(0f, -100f);
        scroll = lista.gameObject.AddComponent<ScrollRect>();

        var vista = Hijo("Vista", lista);
        Estirar(vista, 0f);
        vista.gameObject.AddComponent<RectMask2D>();
        var capturaToque = vista.gameObject.AddComponent<Image>();
        capturaToque.color = new Color(0f, 0f, 0f, 0.01f);

        contenido = Hijo("Filas", vista);
        contenido.anchorMin = new Vector2(0f, 1f);
        contenido.anchorMax = new Vector2(1f, 1f);
        contenido.pivot = new Vector2(0.5f, 1f);
        contenido.anchoredPosition = Vector2.zero;
        contenido.sizeDelta = Vector2.zero;
        var capa = contenido.gameObject.AddComponent<VerticalLayoutGroup>();
        capa.spacing = 10f;
        capa.childControlWidth = true;
        capa.childControlHeight = true;
        capa.childForceExpandWidth = true;
        capa.childForceExpandHeight = false;
        var ajuste = contenido.gameObject.AddComponent<ContentSizeFitter>();
        ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vista;
        scroll.content = contenido;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
    }

    void ElegirPestana(Pestana nueva)
    {
        pestana = nueva;
        pendiente = Producto.Ninguno;
        firma = "";   // fuerza a reconstruir la lista
        for (int i = 0; i < fondosPestana.Count; i++)
        {
            bool elegida = i == (int)pestana;
            fondosPestana[i].color = elegida ? colorPestanaElegida : colorPestana;
            textosPestana[i].color = elegida ? colorTextoBoton : colorTexto;
        }
        Refrescar();
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
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

    TMP_Text CrearTexto(Transform padre, string nombre, float tamano, TextAlignmentOptions alineacion, Color color)
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

    // ancho = 0 -> el botón se reparte el espacio libre de la fila.
    Button CrearBoton(Transform padre, string nombre, string texto, float ancho, float alto,
                      Color fondo, Color colorLetra, UnityAction alPulsar, out TMP_Text etiqueta)
    {
        var rt = Hijo(nombre, padre);
        var imagen = rt.gameObject.AddComponent<Image>();
        imagen.color = fondo;
        var boton = rt.gameObject.AddComponent<Button>();
        boton.targetGraphic = imagen;

        var medida = rt.gameObject.AddComponent<LayoutElement>();
        if (ancho > 0f) medida.preferredWidth = ancho; else medida.flexibleWidth = 1f;
        medida.preferredHeight = alto;

        etiqueta = CrearTexto(rt, "Texto", 34f, TextAlignmentOptions.Center, colorLetra);
        Estirar(etiqueta.rectTransform, 6f);
        etiqueta.text = texto;

        boton.onClick.AddListener(alPulsar);
        return boton;
    }

    // Fila de venta o de compra: etiqueta arriba; abajo, [-] n [+] [Máx] y el botón de acción.
    Fila CrearFila(Producto p, Cultivo c, bool compra, bool unica)
    {
        var fila = new Fila { producto = p, cultivo = c, compra = compra, unica = unica };

        var rt = Hijo((compra ? "Compra_" : "Venta_") + (compra ? c.ToString() : p.ToString()), contenido);
        var fondo = rt.gameObject.AddComponent<Image>();
        fondo.color = colorFila;
        var vertical = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(16, 16, 10, 10);
        vertical.spacing = 8f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        fila.etiqueta = CrearTexto(rt, "Etiqueta", 32f, TextAlignmentOptions.MidlineLeft, colorTexto);
        fila.etiqueta.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

        var controles = Hijo("Controles", rt);
        var horizontal = controles.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 10f;
        horizontal.childAlignment = TextAnchor.MiddleLeft;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false;
        horizontal.childForceExpandHeight = false;
        controles.gameObject.AddComponent<LayoutElement>().preferredHeight = 90f;

        if (!unica)
        {
            fila.menos = CrearBoton(controles, "Menos", "-", 90f, 90f, colorBoton, colorTextoBoton,
                () => Cambiar(fila, -1), out _);

            fila.cantidad = CrearTexto(controles, "Cantidad", 40f, TextAlignmentOptions.Center, colorTexto);
            fila.cantidad.gameObject.AddComponent<LayoutElement>().preferredWidth = 100f;

            fila.mas = CrearBoton(controles, "Mas", "+", 90f, 90f, colorBoton, colorTextoBoton,
                () => Cambiar(fila, +1), out _);
            fila.max = CrearBoton(controles, "Max", "Máx", 120f, 90f, colorBoton, colorTextoBoton,
                () => PonerMaximo(fila), out _);
        }

        fila.boton = CrearBoton(controles, "Accion", "", 0f, 90f, colorBoton, colorTextoBoton,
            () => Pulsar(fila), out fila.accion);

        filas.Add(fila);
        return fila;
    }

    void CrearFilaBonus(Producto p)
    {
        var fb = new FilaBonus { producto = p };
        string nombre = TituloProducto(p);
        fb.boton = CrearBoton(contenido, "Bonus_" + p, "x2 " + nombre, 0f, 100f, colorBoton, colorTextoBoton,
            () => ActivarBonus(p), out _);
        filasBonus.Add(fb);
    }

    // ---------- Qué se muestra en cada pestaña ----------

    static bool Conocido(EstadoJuego j, Cultivo c) { return j.Desbloqueado(c) || j.Cosecha(c) > 0; }

    List<Producto> ProductosVisibles(EstadoJuego j, bool incluirSemilla)
    {
        var lista = new List<Producto>();
        foreach (var c in CultivoDatos.Todos)
            if (Conocido(j, c)) lista.Add(CultivoDatos.ProductoDe(c));
        lista.Add(Producto.Huevo);
        if (j.TienePollo) lista.Add(Producto.Pollo);
        if (incluirSemilla && j.SemillasLegendarias > 0) lista.Add(Producto.SemillaLegendaria);
        return lista;
    }

    string CalcularFirma(EstadoJuego j)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append((int)pestana).Append(':');
        if (pestana != Pestana.Vender && pestana != Pestana.Bonus) return sb.ToString();
        foreach (var p in ProductosVisibles(j, pestana == Pestana.Vender)) sb.Append((int)p).Append(',');
        return sb.ToString();
    }

    void Reconstruir(EstadoJuego j)
    {
        for (int i = contenido.childCount - 1; i >= 0; i--)
        {
            var hijo = contenido.GetChild(i).gameObject;
            hijo.SetActive(false);
            Destroy(hijo);
        }
        filas.Clear();
        filasBonus.Clear();
        filasGen.Clear();
        estadoBonus = null;

        switch (pestana)
        {
            case Pestana.Vender:
                foreach (var p in ProductosVisibles(j, true))
                {
                    bool unica = p == Producto.Pollo || p == Producto.SemillaLegendaria;
                    CrearFila(p, Cultivo.Maiz, false, unica);
                }
                break;

            case Pestana.Semillas:
                foreach (var c in CultivoDatos.Todos)
                    CrearFila(Producto.Ninguno, c, true, false);
                break;

            case Pestana.Bonus:
                estadoBonus = CrearTexto(contenido, "Estado", 32f, TextAlignmentOptions.MidlineLeft, colorTexto);
                estadoBonus.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;
                foreach (var p in ProductosVisibles(j, false)) CrearFilaBonus(p);
                break;

            case Pestana.Edificios:
                CrearFilasEdificios();
                break;

            case Pestana.Animales:
                CrearFilasAnimales();
                break;
        }
    }

    // ---------- Pestañas Edificios y Animales ----------

    FilaGen CrearFilaGen(string nombre, System.Func<string> texto, System.Func<string> textoAccion,
                         System.Func<bool> activa, UnityAction alPulsar)
    {
        var fila = new FilaGen { texto = texto, textoAccion = textoAccion, activa = activa };

        var rt = Hijo("Gen_" + nombre, contenido);
        rt.gameObject.AddComponent<Image>().color = colorFila;
        var vertical = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(16, 16, 10, 10);
        vertical.spacing = 8f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        fila.etiqueta = CrearTexto(rt, "Etiqueta", 30f, TextAlignmentOptions.MidlineLeft, colorTexto);
        fila.etiqueta.gameObject.AddComponent<LayoutElement>().preferredHeight = 110f;

        fila.boton = CrearBoton(rt, "Accion", "", 0f, 90f, colorBoton, colorTextoBoton, alPulsar, out fila.accion);

        filasGen.Add(fila);
        return fila;
    }

    void CrearFilasEdificios()
    {
        foreach (Edificio e in System.Enum.GetValues(typeof(Edificio)))
        {
            Edificio edificio = e;
            CrearFilaGen("Edificio_" + e,
                () =>
                {
                    var j = EstadoJuego.Instance;
                    var m = MisionController.Instance;
                    string n = EdificioDatos.Nombre(edificio);
                    if (j != null && j.TieneEdificio(edificio)) return n + ": ya lo tienes.";
                    if (m == null || !m.SolarLibre)
                        return n + " (" + EdificioDatos.Precio(edificio) + " monedas): termina la misión del pollito y retira el heno para tener sitio.";
                    return n + ": " + EdificioDatos.Precio(edificio) + " monedas. Se coloca donde estaba el heno.";
                },
                () =>
                {
                    var j = EstadoJuego.Instance;
                    return j != null && j.TieneEdificio(edificio) ? "Comprado" : "Comprar (" + EdificioDatos.Precio(edificio) + ")";
                },
                () =>
                {
                    var j = EstadoJuego.Instance;
                    var m = MisionController.Instance;
                    return j != null && m != null && m.SolarLibre && !j.TieneEdificio(edificio)
                           && SolarEdificios.Instance != null && j.Monedas >= EdificioDatos.Precio(edificio);
                },
                () => ComprarEdificio(edificio));
        }
    }

    void CrearFilasAnimales()
    {
        foreach (var a in AnimalDatos.Disponibles)
        {
            Animal animal = a;
            CrearFilaGen("Animal_" + a,
                () =>
                {
                    var c = Corral.De(animal);
                    string cupo = c != null ? c.TextoCupo(animal) : "sin corral";
                    string basico = AnimalDatos.TituloPlural(animal) + ": " + cupo + "  ·  " + AnimalDatos.PrecioCompra(animal)
                                    + " monedas c/u";
                    return Corral.PuedeComprar(animal, out string motivo) ? basico : basico + "\n" + motivo;
                },
                () => "Comprar (" + AnimalDatos.PrecioCompra(animal) + ")",
                () =>
                {
                    var j = EstadoJuego.Instance;
                    return j != null && Corral.PuedeComprar(animal, out _) && j.Monedas >= AnimalDatos.PrecioCompra(animal);
                },
                () => ComprarAnimal(animal));
        }

        foreach (int n in new[] { 1, 5 })
        {
            int raciones = n;
            CrearFilaGen("Heno_" + n,
                () =>
                {
                    var j = EstadoJuego.Instance;
                    var m = MisionController.Instance;
                    string basico = "Heno: tienes " + (j != null ? j.Heno : 0) + " raciones  ·  " + AnimalDatos.PrecioHeno + " monedas c/u";
                    return m != null && m.SolarLibre ? basico : basico + "\nTermina la misión del pollito primero.";
                },
                () => "Comprar x" + raciones + " (" + raciones * AnimalDatos.PrecioHeno + ")",
                () =>
                {
                    var j = EstadoJuego.Instance;
                    var m = MisionController.Instance;
                    return j != null && m != null && m.SolarLibre && j.Monedas >= raciones * AnimalDatos.PrecioHeno;
                },
                () => ComprarHeno(raciones));
        }
    }

    void PintarGenericas()
    {
        foreach (var f in filasGen)
        {
            f.etiqueta.text = f.texto();
            f.accion.text = f.textoAccion();
            f.boton.interactable = f.activa();
        }
    }

    void ComprarEdificio(Edificio e)
    {
        var solar = SolarEdificios.Instance;
        if (solar == null) { textoMensaje.text = "Coloca primero tu granja."; return; }
        solar.Comprar(e, out string mensaje);
        textoMensaje.text = mensaje;
        Refrescar();
    }

    void ComprarAnimal(Animal a)
    {
        Corral.Comprar(a, out string mensaje);
        textoMensaje.text = mensaje;
        Refrescar();
    }

    void ComprarHeno(int raciones)
    {
        var j = EstadoJuego.Instance;
        if (j == null) return;
        textoMensaje.text = j.ComprarHeno(raciones, out int costo)
            ? "Compraste " + raciones + " raciones de heno: -" + costo + " monedas."
            : "No te alcanzan las monedas.";
        Refrescar();
    }

    // ---------- Acciones ----------

    int Cantidad(Fila f) { return f.compra ? cantCompra[(int)f.cultivo] : cantVenta[(int)f.producto]; }

    void FijarCantidad(Fila f, int n)
    {
        if (f.compra) cantCompra[(int)f.cultivo] = n; else cantVenta[(int)f.producto] = n;
    }

    // Tope de unidades que se pueden elegir: lo que tienes (venta) o lo que alcanzan tus monedas (compra).
    static int Tope(EstadoJuego j, Fila f)
    {
        if (f.compra) return Mathf.Max(1, j.Monedas / CultivoDatos.PrecioSemilla(f.cultivo));
        return j.Cantidad(f.producto);
    }

    void Cambiar(Fila f, int delta)
    {
        var j = EstadoJuego.Instance;
        if (j == null) return;
        FijarCantidad(f, Mathf.Clamp(Cantidad(f) + delta, 1, Tope(j, f)));
        Refrescar();
    }

    void PonerMaximo(Fila f)
    {
        var j = EstadoJuego.Instance;
        if (j == null) return;
        FijarCantidad(f, Tope(j, f));
        Refrescar();
    }

    void Pulsar(Fila f)
    {
        if (f.compra) Comprar(f); else Vender(f);
    }

    void Vender(Fila f)
    {
        var j = EstadoJuego.Instance;
        if (j == null) return;
        Producto p = f.producto;

        // El pollo y la semilla legendaria piden confirmación: tocar dos veces seguidas.
        if (f.unica)
        {
            bool confirmando = pendiente == p && Time.unscaledTime <= vencePendiente;
            if (!confirmando)
            {
                pendiente = p;
                vencePendiente = Time.unscaledTime + 3f;
                textoMensaje.text = $"Toca otra vez para vender: {EstadoJuego.NombreProducto(p)}.";
                Refrescar();
                return;
            }
            pendiente = Producto.Ninguno;
        }

        int pedido = f.unica ? 1 : Cantidad(f);
        int total = j.VenderCantidad(p, pedido, out int vendidas);
        textoMensaje.text = total > 0
            ? $"Vendiste {vendidas} de {EstadoJuego.NombreProducto(p)}: +{total} monedas"
            : "No tienes nada de eso para vender.";
        Refrescar();
    }

    void Comprar(Fila f)
    {
        var j = EstadoJuego.Instance;
        if (j == null) return;

        int n = Cantidad(f);
        if (j.ComprarSemillas(f.cultivo, n, out int costo))
            textoMensaje.text = $"Compraste {n} semillas de {CultivoDatos.Nombre(f.cultivo)}: -{costo} monedas";
        else
            textoMensaje.text = "No te alcanzan las monedas.";
        Refrescar();
    }

    void ActivarBonus(Producto p)
    {
        var j = EstadoJuego.Instance;
        if (j == null) return;

        if (j.ActivarBonus(p, out string motivo))
            textoMensaje.text = $"¡Bonificador activo! Lo que vendas de {EstadoJuego.NombreProducto(p)} vale el doble.";
        else
            textoMensaje.text = motivo;
        Refrescar();
    }

    // ---------- Actualización de textos ----------

    void Refrescar()
    {
        var j = EstadoJuego.Instance;
        if (j == null) return;

        botonTienda.gameObject.SetActive(j.TieneLlave);
        if (!panel.activeSelf || contenido == null) return;

        string nueva = CalcularFirma(j);
        if (nueva != firma)
        {
            firma = nueva;
            Reconstruir(j);
        }

        foreach (var f in filas) Pintar(j, f);
        PintarBonus(j);
        PintarGenericas();
    }

    static string TituloProducto(Producto p)
    {
        if (CultivoDatos.EsCultivo(p, out Cultivo c)) return CultivoDatos.TituloPlural(c);
        switch (p)
        {
            case Producto.Huevo: return "Huevos";
            case Producto.Pollo: return "Pollo";
            case Producto.SemillaLegendaria: return "Semilla legendaria";
            default: return "";
        }
    }

    void Pintar(EstadoJuego j, Fila f)
    {
        if (f.compra)
        {
            int precio = CultivoDatos.PrecioSemilla(f.cultivo);
            int tope = Tope(j, f);
            int n = Mathf.Clamp(Cantidad(f), 1, tope);
            FijarCantidad(f, n);

            f.etiqueta.text = $"Semillas de {CultivoDatos.Nombre(f.cultivo)}: tienes {j.Semillas(f.cultivo)}  ·  {precio} monedas c/u";
            f.cantidad.text = n.ToString();
            f.menos.interactable = n > 1;
            f.mas.interactable = n < tope;
            f.max.interactable = n < tope;
            f.accion.text = $"Comprar ({n * precio})";
            f.boton.interactable = j.Monedas >= n * precio;
            return;
        }

        Producto p = f.producto;
        int stock = j.Cantidad(p);
        int mult = j.Multiplicador(p);
        int unidad = j.PrecioBase(p) * mult;
        string sufijo = mult > 1 ? "  (x2)" : "";

        f.etiqueta.text = $"{TituloProducto(p)}: {stock}  ·  {unidad} monedas c/u{sufijo}";

        if (f.unica)
        {
            bool confirmando = pendiente == p && Time.unscaledTime <= vencePendiente;
            f.accion.text = confirmando ? "¿Seguro?" : $"Vender (+{unidad})";
            f.boton.interactable = stock > 0;
            return;
        }

        int vender = stock > 0 ? Mathf.Clamp(Cantidad(f), 1, stock) : 0;
        FijarCantidad(f, Mathf.Max(1, vender));
        f.cantidad.text = vender.ToString();
        f.menos.interactable = vender > 1;
        f.mas.interactable = vender < stock;
        f.max.interactable = vender < stock;
        f.accion.text = $"Vender (+{vender * unidad})";
        f.boton.interactable = stock > 0;
    }

    void PintarBonus(EstadoJuego j)
    {
        if (pestana != Pestana.Bonus || estadoBonus == null) return;

        if (j.BonusActivo)
        {
            int s = Mathf.CeilToInt(j.SegundosRestantesBonus);
            estadoBonus.text = $"Bonificador activo: {EstadoJuego.NombreProducto(j.ProductoBonus)} x2  ({s / 60}:{s % 60:00})";
        }
        else if (j.SemillasLegendarias > 0)
            estadoBonus.text = "Gasta una semilla legendaria para duplicar las ganancias de un producto durante un tiempo:";
        else
            estadoBonus.text = "Necesitas una semilla legendaria para activar un bonificador.";

        bool puede = !j.BonusActivo && j.SemillasLegendarias > 0;
        foreach (var fb in filasBonus)
            fb.boton.interactable = puede && (fb.producto != Producto.Pollo || j.TienePollo);
    }
}
