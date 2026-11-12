using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LineaPedido
{
    public int producto;    // valor del enum Producto
    public int requerido;
    public int cargado;
    public Producto P => (Producto)producto;
}

// Estado del pedido de la caja (también es lo que se guarda en la partida).
[Serializable]
public class DatosPedido
{
    public bool existe;
    public bool entregado;      // pagado, esperando a que cierres el panel para que la caja se renueve
    public bool tutorial;       // es el pedido del tutorial (el de la pizarra)
    public LineaPedido[] lineas = new LineaPedido[0];
}

// La caja de entrega. Va en CajaEntrega (junto a Interactuable Tipo Caja).
// Tocarla abre el panel de pedidos (PedidosUI): cada línea pide una cantidad de un producto,
// que se pasa del inventario a la caja con Llenar y se devuelve con Quitar. Cuando todas las
// líneas están llenas se puede Entregar: paga el doble del precio de venta. Al cerrar el panel,
// los productos caen en la caja; tras entregar, la caja y sus productos desaparecen y vuelve vacía
// con un pedido nuevo.
public class PedidosCaja : MonoBehaviour
{
    public static PedidosCaja Instance { get; private set; }

    // Estado cargado de la partida mientras la granja todavía no está colocada.
    static DatosPedido pendiente;

    [Header("Modelos que caen en la caja")]
    [SerializeField] GameObject modeloMaiz;
    [SerializeField] GameObject modeloTomate;
    [SerializeField] GameObject modeloPapa;
    [SerializeField] GameObject modeloArroz;
    [SerializeField] GameObject modeloHuevo;
    [Tooltip("Tamaño del lado mayor de cada producto, en unidades de granja. Se ajusta solo, sin importar el tamaño original del modelo.")]
    [SerializeField] float tamanoModelo = 0.024f;
    [SerializeField] float tamanoHuevo = 0.018f;

    [Header("Dónde caen (se ven como esferas amarillas al seleccionar la caja)")]
    [Tooltip("El modelo de la caja. Si lo dejas vacío se busca un hijo llamado ModeloCaja.")]
    [SerializeField] Transform modeloCaja;
    [Tooltip("Qué parte de la caja ocupan los productos (0.6 = el 60 % central).")]
    [SerializeField, Range(0.2f, 1f)] float ocupacionArea = 0.6f;
    [Tooltip("Altura del fondo de la caja, como fracción de su altura.")]
    [SerializeField, Range(0f, 0.6f)] float alturaFondo = 0.12f;
    [Tooltip("Desde qué altura caen, en unidades de granja.")]
    [SerializeField] float alturaCaida = 0.12f;
    [SerializeField] float segundosCaida = 0.6f;
    [SerializeField] float separacionCaida = 0.12f;

    [Header("Pedidos nuevos")]
    [SerializeField] int maxPorCultivo = 3;
    [SerializeField] int maxHuevos = 2;

    [Header("Animación al entregar")]
    [SerializeField] float segundosSalida = 0.5f;
    [SerializeField] float segundosEspera = 0.6f;
    [SerializeField] float segundosEntrada = 0.45f;
    [Tooltip("Cuánto sube la caja al desaparecer, en unidades de granja.")]
    [SerializeField] float alturaSalida = 0.08f;

    const int Columnas = 3, Filas = 3, Capas = 2;
    const int MaxModelos = Columnas * Filas * Capas;
    const int MultiplicadorPedido = 2;

    class Item
    {
        public Producto producto;
        public Transform t;
        public int slot;
    }

    DatosPedido pedido;
    readonly List<Item> items = new List<Item>();
    readonly bool[] slotOcupado = new bool[MaxModelos];
    bool animando;
    Vector3 posBase, escalaBase;
    Collider colision;

    public event Action OnCambio;

    public DatosPedido Pedido => pedido;
    public bool Entregado => pedido != null && pedido.entregado;
    public int UltimaRecompensa { get; private set; }

    public bool TodoLleno
    {
        get
        {
            if (pedido == null || pedido.lineas.Length == 0) return false;
            foreach (var l in pedido.lineas) if (l.cargado < l.requerido) return false;
            return true;
        }
    }

    // ---------- Ciclo de vida y guardado ----------

    void Awake()
    {
        Instance = this;
        posBase = transform.localPosition;
        escalaBase = transform.localScale;
        colision = GetComponent<Collider>();
    }

    void Start()
    {
        if (pendiente != null)
        {
            Aplicar(pendiente);
            pendiente = null;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Limpiar() { pendiente = null; }

    // La llama Guardado al cargar. Si la granja aún no existe, queda en espera.
    public static void Preparar(DatosPedido datos)
    {
        if (Instance != null) { Instance.Aplicar(datos); pendiente = null; }
        else pendiente = datos;
    }

    public static DatosPedido Exportar()
    {
        if (Instance != null) return Instance.Crear();
        return pendiente ?? new DatosPedido();
    }

    DatosPedido Crear()
    {
        return pedido ?? new DatosPedido();
    }

    void Aplicar(DatosPedido datos)
    {
        pedido = datos != null && datos.existe ? datos : null;
        // Si se cerró el juego justo tras entregar, la caja vuelve renovada.
        if (pedido != null && pedido.entregado) pedido = GenerarNormal();
        SincronizarModelos(false);
        OnCambio?.Invoke();
    }

    // ---------- Toque ----------

    public void Tocar()
    {
        if (animando)
        {
            Avisar("La caja se está preparando...");
            return;
        }

        var mision = MisionController.Instance;
        if (mision != null && !mision.PedidosAbiertos)
        {
            Avisar("Todavía no hay pedidos. Revisa la pizarra de la casa.");
            return;
        }

        AsegurarPedido();

        if (PedidosUI.Instance == null)
        {
            Avisar("Falta el panel de pedidos en la escena (objeto PanelPedidos).");
            return;
        }
        PedidosUI.Instance.Abrir(this);
    }

    void AsegurarPedido()
    {
        if (pedido != null) return;
        var mision = MisionController.Instance;
        pedido = mision != null && mision.PedidoTutorialActivo
            ? CrearTutorial(mision.ChoclosPedido)
            : GenerarNormal();
        OnCambio?.Invoke();
    }

    // ---------- Pedidos ----------

    static DatosPedido CrearTutorial(int choclos)
    {
        return new DatosPedido
        {
            existe = true,
            tutorial = true,
            lineas = new[] { new LineaPedido { producto = (int)Producto.Maiz, requerido = Mathf.Max(1, choclos) } }
        };
    }

    // Pedido pequeño y mezclado con los productos que el jugador ya conoce.
    DatosPedido GenerarNormal()
    {
        var j = EstadoJuego.Instance;
        var posibles = new List<Producto>();
        if (j != null)
        {
            foreach (var c in CultivoDatos.Todos)
                if (j.Desbloqueado(c)) posibles.Add(CultivoDatos.ProductoDe(c));
            if (j.PolloAdulto || j.TienePollo) posibles.Add(Producto.Huevo);
        }
        if (posibles.Count == 0) posibles.Add(Producto.Maiz);

        // Mezcla
        for (int i = posibles.Count - 1; i > 0; i--)
        {
            int k = UnityEngine.Random.Range(0, i + 1);
            var tmp = posibles[i]; posibles[i] = posibles[k]; posibles[k] = tmp;
        }

        int cuantas = posibles.Count == 1 ? 1 : Mathf.Min(posibles.Count, UnityEngine.Random.Range(2, 4));
        var lineas = new LineaPedido[cuantas];
        for (int i = 0; i < cuantas; i++)
        {
            int tope = posibles[i] == Producto.Huevo ? maxHuevos : maxPorCultivo;
            lineas[i] = new LineaPedido
            {
                producto = (int)posibles[i],
                requerido = UnityEngine.Random.Range(1, Mathf.Max(1, tope) + 1)
            };
        }
        return new DatosPedido { existe = true, lineas = lineas };
    }

    public int Recompensa()
    {
        var j = EstadoJuego.Instance;
        if (pedido == null || j == null) return 0;
        int total = 0;
        foreach (var l in pedido.lineas)
            total += l.requerido * j.PrecioBase(l.P) * MultiplicadorPedido * j.Multiplicador(l.P);
        return total;
    }

    // ---------- Acciones del panel ----------

    // Pasa del inventario a la caja lo que falte en esa línea. Devuelve cuántas unidades pasó.
    public int Llenar(int indice)
    {
        var j = EstadoJuego.Instance;
        if (j == null || pedido == null || pedido.entregado || indice < 0 || indice >= pedido.lineas.Length) return 0;

        var l = pedido.lineas[indice];
        int movidas = 0;
        while (l.cargado < l.requerido && Gastar(j, l.P))
        {
            l.cargado++;
            movidas++;
        }
        if (movidas > 0) OnCambio?.Invoke();
        return movidas;
    }

    // Devuelve al inventario todo lo que esa línea tenía en la caja.
    public int Quitar(int indice)
    {
        var j = EstadoJuego.Instance;
        if (j == null || pedido == null || pedido.entregado || indice < 0 || indice >= pedido.lineas.Length) return 0;

        var l = pedido.lineas[indice];
        int n = l.cargado;
        if (n <= 0) return 0;
        l.cargado = 0;
        Devolver(j, l.P, n);
        OnCambio?.Invoke();
        return n;
    }

    public bool Entregar(out int monedas)
    {
        monedas = 0;
        var j = EstadoJuego.Instance;
        if (j == null || pedido == null || pedido.entregado || !TodoLleno) return false;

        monedas = Recompensa();
        int cultivos = 0;
        foreach (var l in pedido.lineas)
            if (CultivoDatos.EsCultivo(l.P, out _)) cultivos += l.requerido;

        pedido.entregado = true;      // antes de cobrar, porque cobrar avisa a la misión
        UltimaRecompensa = monedas;
        j.CobrarPedido(monedas, cultivos);
        OnCambio?.Invoke();
        Ajustes.Vibrar();
        return true;
    }

    // La llama el panel al cerrarse.
    public void AlCerrarPanel()
    {
        if (pedido != null && pedido.entregado) StartCoroutine(AnimarEntrega());
        else SincronizarModelos(true);
    }

    static bool Gastar(EstadoJuego j, Producto p)
    {
        if (CultivoDatos.EsCultivo(p, out Cultivo c)) return j.GastarCosecha(c);
        return p == Producto.Huevo && j.GastarHuevo();
    }

    static void Devolver(EstadoJuego j, Producto p, int n)
    {
        if (CultivoDatos.EsCultivo(p, out Cultivo c)) j.DevolverCosecha(c, n);
        else if (p == Producto.Huevo) j.DevolverHuevo(n);
    }

    // ---------- Modelos dentro de la caja ----------

    GameObject ModeloDe(Producto p)
    {
        switch (p)
        {
            case Producto.Maiz: return modeloMaiz;
            case Producto.Tomate: return modeloTomate;
            case Producto.Papa: return modeloPapa;
            case Producto.Arroz: return modeloArroz;
            case Producto.Huevo: return modeloHuevo;
            default: return null;
        }
    }

    // Deja en la caja un modelo por cada unidad cargada. Los nuevos caen desde arriba.
    void SincronizarModelos(bool animar)
    {
        if (pedido == null)
        {
            QuitarTodos();
            return;
        }

        float demora = 0f;
        foreach (var l in pedido.lineas)
        {
            Producto p = l.P;
            int tiene = 0;
            foreach (var it in items) if (it.producto == p) tiene++;

            while (tiene > l.cargado)
            {
                for (int i = items.Count - 1; i >= 0; i--)
                    if (items[i].producto == p) { Retirar(items[i], animar); break; }
                tiene--;
            }
            while (tiene < l.cargado && items.Count < MaxModelos)
            {
                if (!Agregar(p, animar, demora)) break;
                demora += separacionCaida;
                tiene++;
            }
        }
    }

    bool Agregar(Producto p, bool animar, float demora)
    {
        GameObject modelo = ModeloDe(p);
        if (modelo == null) return false;

        int slot = -1;
        for (int i = 0; i < slotOcupado.Length; i++)
            if (!slotOcupado[i]) { slot = i; break; }
        if (slot < 0) return false;
        if (!CalcularSlot(slot, out Vector3 destino)) return false;

        Transform t = CrearModelo(modelo, p == Producto.Huevo ? tamanoHuevo : tamanoModelo);
        t.rotation = Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), transform.up) * t.rotation;

        slotOcupado[slot] = true;
        var item = new Item { producto = p, t = t, slot = slot };
        items.Add(item);

        if (animar) StartCoroutine(Caer(t, destino, demora));
        else t.position = destino;
        return true;
    }

    void Retirar(Item item, bool animar)
    {
        items.Remove(item);
        slotOcupado[item.slot] = false;
        if (item.t == null) return;
        if (animar) StartCoroutine(Encoger(item.t));
        else Destroy(item.t.gameObject);
    }

    void QuitarTodos()
    {
        foreach (var it in items) if (it.t != null) Destroy(it.t.gameObject);
        items.Clear();
        Array.Clear(slotOcupado, 0, slotOcupado.Length);
    }

    // Crea el modelo dentro de un contenedor cuyo origen es el centro de su base y le da el tamaño pedido.
    Transform CrearModelo(GameObject prefab, float tamano)
    {
        var contenedor = new GameObject("Pedido_" + prefab.name).transform;
        contenedor.SetParent(transform, false);

        var go = Instantiate(prefab, contenedor, false);
        foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        if (TryBounds(go, out Bounds b))
        {
            float extension = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (extension > 1e-6f)
                go.transform.localScale = Vector3.one * (tamano * transform.lossyScale.x / extension);

            if (TryBounds(go, out b))
            {
                Vector3 baseCentro = new Vector3(b.center.x, b.min.y, b.center.z);
                go.transform.position += contenedor.position - baseCentro;
            }
        }
        return contenedor;
    }

    static bool TryBounds(GameObject go, out Bounds b)
    {
        b = new Bounds();
        bool primero = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (primero) { b = r.bounds; primero = false; }
            else b.Encapsulate(r.bounds);
        }
        return !primero;
    }

    // Rejilla de posiciones dentro de la caja, en el espacio del modelo de la caja (así sigue su giro).
    bool CalcularSlot(int slot, out Vector3 mundo)
    {
        mundo = Vector3.zero;
        if (!InteriorCaja(out Transform marco, out Bounds local)) return false;

        int capa = slot / (Columnas * Filas);
        int resto = slot % (Columnas * Filas);
        int col = resto % Columnas;
        int fila = resto / Columnas;

        float x = local.center.x + (col - (Columnas - 1) * 0.5f) * (local.size.x * ocupacionArea / Columnas);
        float z = local.center.z + (fila - (Filas - 1) * 0.5f) * (local.size.z * ocupacionArea / Filas);
        float y = local.min.y + local.size.y * alturaFondo;

        mundo = marco.TransformPoint(new Vector3(x, y, z));
        mundo += transform.up * (capa * tamanoModelo * 0.8f * transform.lossyScale.x);
        return true;
    }

    // Caja del modelo en su propio espacio local (se calcula con las mallas del modelo de la caja).
    bool InteriorCaja(out Transform marco, out Bounds local)
    {
        local = new Bounds();
        marco = modeloCaja != null ? modeloCaja : transform.Find("ModeloCaja");

        if (marco != null)
        {
            bool primero = true;
            foreach (var mf in marco.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 esquina = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 p = marco.InverseTransformPoint(mf.transform.TransformPoint(esquina));
                    if (primero) { local = new Bounds(p, Vector3.zero); primero = false; }
                    else local.Encapsulate(p);
                }
            }
            if (!primero) return true;
        }

        // Sin modelo: usa el Box Collider de la caja.
        var caja = GetComponent<BoxCollider>();
        if (caja == null) return false;
        marco = transform;
        local = new Bounds(caja.center, caja.size);
        return true;
    }

    // ---------- Animaciones ----------

    IEnumerator Caer(Transform t, Vector3 destino, float demora)
    {
        Vector3 arriba = destino + transform.up * (alturaCaida * transform.lossyScale.x);
        t.position = arriba;
        t.gameObject.SetActive(false);
        if (demora > 0f) yield return new WaitForSeconds(demora);
        if (t == null) yield break;

        t.gameObject.SetActive(true);
        float dur = Mathf.Max(0.1f, segundosCaida);
        float e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            if (t == null) yield break;
            t.position = Vector3.LerpUnclamped(arriba, destino, EaseOutBounce(Mathf.Clamp01(e / dur)));
            yield return null;
        }
        if (t != null) t.position = destino;
    }

    static IEnumerator Encoger(Transform t)
    {
        Vector3 inicio = t.localScale;
        float e = 0f;
        while (e < 0.2f)
        {
            e += Time.deltaTime;
            if (t == null) yield break;
            t.localScale = inicio * (1f - Mathf.Clamp01(e / 0.2f));
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }

    // La caja sube y se encoge con sus productos, desaparece, y vuelve vacía con un pedido nuevo.
    IEnumerator AnimarEntrega()
    {
        animando = true;
        if (colision != null) colision.enabled = false;

        float dur = Mathf.Max(0.1f, segundosSalida);
        for (float e = 0f; e < dur;)
        {
            e += Time.deltaTime;
            float k = EaseInBack(Mathf.Clamp01(e / dur));
            transform.localScale = escalaBase * Mathf.Max(0f, 1f - k);
            transform.localPosition = posBase + Vector3.up * (alturaSalida * Mathf.Clamp01(k));
            yield return null;
        }
        transform.localScale = Vector3.zero;

        QuitarTodos();
        pedido = GenerarNormal();
        OnCambio?.Invoke();

        yield return new WaitForSeconds(segundosEspera);

        dur = Mathf.Max(0.1f, segundosEntrada);
        for (float e = 0f; e < dur;)
        {
            e += Time.deltaTime;
            float k = EaseOutBack(Mathf.Clamp01(e / dur));
            transform.localScale = escalaBase * Mathf.Max(0f, k);
            transform.localPosition = posBase;
            yield return null;
        }
        transform.localScale = escalaBase;
        transform.localPosition = posBase;

        if (colision != null) colision.enabled = true;
        animando = false;
        Avisar("Llegó un pedido nuevo a la caja.");
    }

    static float EaseOutBounce(float x)
    {
        const float n1 = 7.5625f, d1 = 2.75f;
        if (x < 1f / d1) return n1 * x * x;
        if (x < 2f / d1) { x -= 1.5f / d1; return n1 * x * x + 0.75f; }
        if (x < 2.5f / d1) { x -= 2.25f / d1; return n1 * x * x + 0.9375f; }
        x -= 2.625f / d1;
        return n1 * x * x + 0.984375f;
    }

    static float EaseInBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return c3 * x * x * x - c1 * x * x;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float y = x - 1f;
        return 1f + c3 * y * y * y + c1 * y * y;
    }

    static void Avisar(string texto)
    {
        if (TutorialController.Instance != null) TutorialController.Instance.Avisar(texto);
    }

#if UNITY_EDITOR
    // Al seleccionar la caja muestra dónde caerán los productos (capa baja en amarillo, alta en naranja).
    void OnDrawGizmosSelected()
    {
        for (int i = 0; i < MaxModelos; i++)
        {
            if (!CalcularSlot(i, out Vector3 p)) return;
            Gizmos.color = i < Columnas * Filas ? Color.yellow : new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(p + transform.up * (tamanoModelo * 0.5f * transform.lossyScale.x),
                                  tamanoModelo * 0.5f * transform.lossyScale.x);
        }
    }

    [ContextMenu("Cargar modelos por defecto")]
    void CargarModelosPorDefecto()
    {
        UnityEditor.Undo.RecordObject(this, "Cargar modelos por defecto");
        modeloMaiz = Cargar("Cultivos/Choclo/chocloComida.glb");
        modeloTomate = Cargar("Cultivos/Tomate/tomateComida.glb");
        modeloPapa = Cargar("Cultivos/Papa/papa.glb");
        modeloArroz = Cargar("Cultivos/Arroz/arrozComida.glb");
        modeloHuevo = Cargar("Animales/Avicola/huevo.glb");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    static GameObject Cargar(string ruta)
    {
        var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Modelos/" + ruta);
        if (go == null) Debug.LogWarning("[PedidosCaja] No encuentro el modelo: " + ruta);
        return go;
    }
#endif
}
