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
    // Solo líneas de animales: los datos de cada animal que está en la caja (para devolverlo igual al corral).
    public List<DatosAnimal> animales = new List<DatosAnimal>();
    public Producto P => (Producto)producto;

    public List<DatosAnimal> Animales
    {
        get
        {
            if (animales == null) animales = new List<DatosAnimal>();
            return animales;
        }
    }
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
// Tocarla abre el panel de pedidos (PedidosUI): cada línea pide una cantidad de un producto (cultivos, huevos o
// animales adultos: gallinas, gallos y cuyes), que se pasa del inventario o del corral a la caja con Llenar y se
// devuelve con Quitar. Cuando todas las líneas están llenas se puede Entregar: paga el doble del precio de venta.
// Al cerrar el panel, los productos caen en la caja y se apilan; tras entregar, caen los últimos, la caja se lleva
// el pedido y vuelve vacía con un pedido nuevo.
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
    [SerializeField] float tamanoModelo = 0.03f;
    [SerializeField] float tamanoHuevo = 0.018f;
    [Tooltip("Multiplica el tamaño de cultivos y huevos que caen en la caja (1 = el tamaño de antes).")]
    [SerializeField] float factorTamano = 1.4f;
    [Tooltip("Multiplica el tamaño de los animales dentro de la caja (1 = su tamaño en el corral).")]
    [SerializeField] float factorTamanoAnimal = 1.3f;

    [Header("Dónde caen (el amarillo que se ve al seleccionar la caja es su interior)")]
    [Tooltip("El modelo de la caja. Si lo dejas vacío se busca un hijo llamado ModeloCaja.")]
    [SerializeField] Transform modeloCaja;
    [Tooltip("Cuánto se agranda la caja al empezar la partida (1 = su tamaño original). Crece desde su base, sin moverse del sitio.")]
    [SerializeField, Range(1f, 2f)] float escalaCaja = 1.4f;
    [Tooltip("Qué parte del interior de la caja usan los productos al caer (0.75 = el 75 % central).")]
    [SerializeField, Range(0.2f, 1f)] float areaApilado = 0.75f;
    [Tooltip("Altura del fondo de la caja, como fracción de su altura.")]
    [SerializeField, Range(0f, 0.6f)] float alturaFondo = 0.12f;
    [Tooltip("Desde qué altura caen, en unidades de granja.")]
    [SerializeField] float alturaCaida = 0.12f;
    [SerializeField] float segundosCaida = 0.6f;
    [SerializeField] float separacionCaida = 0.12f;

    [Header("Pedidos nuevos")]
    [SerializeField] int maxPorCultivo = 3;
    [SerializeField] int maxHuevos = 2;
    [Tooltip("Máximo de animales que puede pedir una línea (y solo habrá una línea de animales por pedido).")]
    [SerializeField] int maxAnimalesPorLinea = 2;
    [Tooltip("Probabilidad de que un pedido nuevo incluya una línea de animales (0.12 = 1 de cada 8 pedidos). El resto son cultivos y huevos.")]
    [SerializeField, Range(0f, 1f)] float probabilidadAnimal = 0.12f;

    [Header("Animación al entregar")]
    [Tooltip("Pausa, con todo ya dentro de la caja, antes de que la caja se lleve el pedido.")]
    [SerializeField] float segundosAsentar = 0.5f;
    [SerializeField] float segundosSalida = 0.5f;
    [SerializeField] float segundosEspera = 0.6f;
    [SerializeField] float segundosEntrada = 0.45f;
    [Tooltip("Cuánto sube la caja al desaparecer, en unidades de granja.")]
    [SerializeField] float alturaSalida = 0.08f;

    const int MaxModelos = 24;
    const int MultiplicadorPedido = 2;
    const int IntentosPosicion = 40;
    const float FactorRadioAnimal = 0.8f;   // un animal es alargado: su círculo de choque es menor que su lado mayor

    // Un modelo dentro de la caja. 'pl' es su posición (base del modelo) en el espacio local de la caja.
    class Item
    {
        public Producto producto;
        public Transform t;
        public Vector3 pl;
        public float suelo;       // altura (local de la caja) del fondo donde se apoyaría
        public float radio;       // radio horizontal aproximado
        public float alto;
        public bool cayendo;
    }

    DatosPedido pedido;
    readonly List<Item> items = new List<Item>();
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
        AgrandarCaja();
    }

    // Agranda el modelo de la caja (y su collider) desde el centro de su base, para que no se mueva del sitio.
    void AgrandarCaja()
    {
        if (escalaCaja <= 1.001f) return;

        Transform marco = ModeloCajaTransform();
        if (marco != null && InteriorCaja(out Interior antes))
        {
            Vector3 baseAntes = antes.centro - antes.ejeV;
            marco.localScale *= escalaCaja;
            if (InteriorCaja(out Interior despues))
            {
                Vector3 desplazamiento = baseAntes - (despues.centro - despues.ejeV);
                marco.localPosition += marco.parent == transform
                    ? desplazamiento
                    : marco.parent.InverseTransformVector(transform.TransformVector(desplazamiento));
            }
        }

        var caja = GetComponent<BoxCollider>();
        if (caja != null)
        {
            float suelo = caja.center.y - caja.size.y * 0.5f;
            caja.size *= escalaCaja;
            caja.center = new Vector3(caja.center.x, suelo + caja.size.y * 0.5f, caja.center.z);
        }
    }

    Transform ModeloCajaTransform()
    {
        return modeloCaja != null ? modeloCaja : transform.Find("ModeloCaja");
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

    // Animales de esa especie de corral que están ahora dentro de la caja (siguen ocupando su plaza en el corral).
    public static int EnCaja(Animal especieCorral, out int comprados, out int nacidos, out int gallos)
    {
        comprados = 0;
        nacidos = 0;
        gallos = 0;
        DatosPedido d = Exportar();
        if (d == null || d.lineas == null) return 0;

        foreach (var l in d.lineas)
        {
            if (l == null || !PedidoAnimales.EsAnimal(l.P, out Animal a)) continue;
            if (AnimalDatos.EspecieCorral(a) != especieCorral) continue;
            foreach (var animal in l.Animales)
            {
                // La gallina del tutorial tiene su propia plaza: no gasta las del corral.
                if (animal == null || animal.principal) continue;
                if (animal.nacido) nacidos++; else comprados++;
                if (a == Animal.Gallo) gallos++;
            }
        }
        return comprados + nacidos;
    }

    // ¿Está la gallina del tutorial dentro de la caja (en un pedido que todavía no se entregó)?
    public static bool PrincipalEnCaja()
    {
        DatosPedido d = Exportar();
        if (d == null || !d.existe || d.entregado || d.lineas == null) return false;
        foreach (var l in d.lineas)
        {
            if (l == null) continue;
            foreach (var animal in l.Animales)
                if (animal != null && animal.principal) return true;
        }
        return false;
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

    // Pedido pequeño y mezclado con lo que el jugador ya conoce. Lo normal son cultivos y huevos; solo de vez en
    // cuando (probabilidadAnimal) una de las líneas pide animales adultos, y nunca más de una.
    DatosPedido GenerarNormal()
    {
        var j = EstadoJuego.Instance;
        var posibles = new List<Producto>();
        var animales = new List<Producto>();
        if (j != null)
        {
            foreach (var c in CultivoDatos.Todos)
                if (j.Desbloqueado(c)) posibles.Add(CultivoDatos.ProductoDe(c));
            if (j.PolloAdulto || j.TienePollo) posibles.Add(Producto.Huevo);
            foreach (var a in AnimalDatos.Disponibles)
            {
                var corral = Corral.De(a);
                if (corral != null && corral.Adultos(a) > 0) animales.Add(PedidoAnimales.De(a));
            }
        }
        if (posibles.Count == 0 && animales.Count == 0) posibles.Add(Producto.Maiz);

        bool conAnimal = animales.Count > 0 && UnityEngine.Random.value < probabilidadAnimal;
        int total = posibles.Count + (conAnimal ? 1 : 0);
        int cuantas = total == 1 ? 1 : Mathf.Min(total, UnityEngine.Random.Range(2, 4));
        int normales = conAnimal ? cuantas - 1 : cuantas;

        // Mezcla de cultivos y huevos
        for (int i = posibles.Count - 1; i > 0; i--)
        {
            int k = UnityEngine.Random.Range(0, i + 1);
            var tmp = posibles[i]; posibles[i] = posibles[k]; posibles[k] = tmp;
        }

        var lineas = new List<LineaPedido>();
        for (int i = 0; i < posibles.Count && lineas.Count < normales; i++)
        {
            Producto p = posibles[i];
            int tope = p == Producto.Huevo ? maxHuevos : maxPorCultivo;
            lineas.Add(new LineaPedido { producto = (int)p, requerido = UnityEngine.Random.Range(1, Mathf.Max(1, tope) + 1) });
        }

        if (conAnimal)
        {
            Producto p = animales[UnityEngine.Random.Range(0, animales.Count)];
            PedidoAnimales.EsAnimal(p, out Animal animal);
            var corral = Corral.De(animal);
            int adultos = corral != null ? corral.Adultos(animal) : 1;
            int tope = Mathf.Max(1, Mathf.Min(maxAnimalesPorLinea, adultos));
            var linea = new LineaPedido { producto = (int)p, requerido = UnityEngine.Random.Range(1, tope + 1) };
            lineas.Insert(UnityEngine.Random.Range(0, lineas.Count + 1), linea);
        }
        return new DatosPedido { existe = true, lineas = lineas.ToArray() };
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

    // Pasa del inventario (o del corral, si son animales) a la caja lo que falte en esa línea.
    // Devuelve cuántas unidades pasó.
    public int Llenar(int indice)
    {
        var j = EstadoJuego.Instance;
        if (j == null || pedido == null || pedido.entregado || indice < 0 || indice >= pedido.lineas.Length) return 0;

        var l = pedido.lineas[indice];
        int movidas = 0;

        if (PedidoAnimales.EsAnimal(l.P, out Animal animal))
        {
            var corral = Corral.De(animal);
            while (corral != null && l.cargado < l.requerido && corral.TomarAdulto(animal, out DatosAnimal datos))
            {
                l.Animales.Add(datos);
                l.cargado = l.Animales.Count;
                movidas++;
            }
        }
        else
        {
            while (l.cargado < l.requerido && Gastar(j, l.P))
            {
                l.cargado++;
                movidas++;
            }
        }
        if (movidas > 0) OnCambio?.Invoke();
        return movidas;
    }

    // Devuelve al inventario (o al corral) todo lo que esa línea tenía en la caja.
    public int Quitar(int indice)
    {
        var j = EstadoJuego.Instance;
        if (j == null || pedido == null || pedido.entregado || indice < 0 || indice >= pedido.lineas.Length) return 0;

        var l = pedido.lineas[indice];
        int n = l.cargado;
        if (n <= 0) return 0;

        if (PedidoAnimales.EsAnimal(l.P, out Animal animal))
        {
            var corral = Corral.De(animal);
            var lista = l.Animales;
            int devueltos = 0;
            while (corral != null && lista.Count > 0 && corral.DevolverAdulto(animal, lista[lista.Count - 1]))
            {
                lista.RemoveAt(lista.Count - 1);
                devueltos++;
            }
            l.cargado = lista.Count;
            if (devueltos > 0) OnCambio?.Invoke();
            return devueltos;
        }

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

        // Si entre los animales va la gallina del tutorial, se va con el pedido (como si la vendieras).
        bool llevaPrincipal = PrincipalEnCaja();

        pedido.entregado = true;      // antes de cobrar, porque cobrar avisa a la misión
        UltimaRecompensa = monedas;
        if (llevaPrincipal) j.EntregarPrincipal();
        j.CobrarPedido(monedas, cultivos);
        OnCambio?.Invoke();
        Ajustes.Vibrar();
        return true;
    }

    // La llama el panel al cerrarse. Los productos nuevos caen en la caja (también los de un pedido recién
    // entregado) y, si el pedido está entregado, la caja se lo lleva cuando el último termina de caer.
    public void AlCerrarPanel()
    {
        float espera = SincronizarModelos(true);
        if (pedido != null && pedido.entregado) StartCoroutine(AnimarEntrega(espera));
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

    int Contar(Producto p)
    {
        int n = 0;
        foreach (var it in items) if (it.producto == p) n++;
        return n;
    }

    // Deja en la caja un modelo por cada unidad cargada. Los nuevos caen desde arriba y se apilan.
    // Devuelve cuántos segundos faltan para que el último termine de caer (0 si no se animó).
    float SincronizarModelos(bool animar)
    {
        if (pedido == null)
        {
            QuitarTodos();
            return 0f;
        }

        // 1. Se retiran los que sobran y los que quedaron encima se asientan.
        bool retiro = false;
        foreach (var l in pedido.lineas)
        {
            Producto p = l.P;
            int tiene = Contar(p);
            while (tiene > l.cargado)
            {
                for (int i = items.Count - 1; i >= 0; i--)
                    if (items[i].producto == p) { Retirar(items[i], animar); break; }
                tiene--;
                retiro = true;
            }
        }
        if (retiro) Reposar(animar);

        // 2. Se añaden los que faltan.
        float demora = 0f;
        float fin = 0f;
        foreach (var l in pedido.lineas)
        {
            Producto p = l.P;
            bool esAnimal = PedidoAnimales.EsAnimal(p, out _);
            int tiene = Contar(p);
            while (tiene < l.cargado && items.Count < MaxModelos)
            {
                DatosAnimal datos = esAnimal && tiene < l.Animales.Count ? l.Animales[tiene] : null;
                if (!Agregar(p, datos, animar, demora)) break;
                if (animar)
                {
                    fin = Mathf.Max(fin, demora + Mathf.Max(0.1f, segundosCaida));
                    demora += separacionCaida;
                }
                tiene++;
            }
        }
        return fin;
    }

    bool Agregar(Producto p, DatosAnimal datos, bool animar, float demora)
    {
        Transform t;
        bool esAnimal = PedidoAnimales.EsAnimal(p, out Animal animal);
        if (esAnimal)
        {
            var corral = Corral.De(animal);
            int variante = datos != null ? datos.variante : 0;
            int raciones = datos != null ? datos.raciones : 0;
            if (corral == null || !corral.ModeloAdulto(animal, variante, raciones,
                    out GameObject prefabAnimal, out Quaternion correccion, out float tamanoMundo))
                return false;

            float tamano = tamanoMundo / Mathf.Max(1e-5f, transform.lossyScale.x) * factorTamanoAnimal;
            t = ModeloUtil.CrearNormalizado(prefabAnimal, transform, tamano, "Pedido_" + AnimalDatos.Nombre(animal), correccion);
            corral.AplicarVariante(t, animal, variante);
        }
        else
        {
            GameObject modelo = ModeloDe(p);
            if (modelo == null) return false;
            float tamano = (p == Producto.Huevo ? tamanoHuevo : tamanoModelo) * factorTamano;
            t = ModeloUtil.CrearNormalizado(modelo, transform, tamano, "Pedido_" + modelo.name);
        }

        float radio = 0.02f, alto = 0.02f;
        float escala = Mathf.Max(1e-5f, transform.lossyScale.x);
        if (ModeloUtil.Limites(t.gameObject, out Bounds b))
        {
            radio = Mathf.Max(b.size.x, b.size.z) * 0.5f / escala;
            if (esAnimal) radio *= FactorRadioAnimal;
            alto = b.size.y / escala;
        }

        if (!Colocar(radio, out Vector3 pl, out float suelo))
        {
            Destroy(t.gameObject);
            return false;
        }

        // Giro al azar sobre sí mismo; cultivos y huevos además quedan un poco ladeados, como al caer.
        Quaternion giro = Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), transform.up);
        if (!esAnimal)
            giro *= Quaternion.AngleAxis(UnityEngine.Random.Range(-10f, 10f), transform.right);
        t.rotation = giro * t.rotation;

        var item = new Item { producto = p, t = t, pl = pl, suelo = suelo, radio = radio, alto = alto };
        items.Add(item);

        if (animar) StartCoroutine(Caer(item, demora));
        else t.position = transform.TransformPoint(item.pl);
        return true;
    }

    void Retirar(Item item, bool animar)
    {
        items.Remove(item);
        if (item.t == null) return;
        if (animar) StartCoroutine(Encoger(item.t));
        else Destroy(item.t.gameObject);
    }

    void QuitarTodos()
    {
        foreach (var it in items) if (it.t != null) Destroy(it.t.gameObject);
        items.Clear();
    }

    // Altura a la que se apoyaría algo de ese radio en 'c', encima de los 'hasta' primeros modelos de la pila.
    float AlturaApoyo(Vector3 c, float radio, float suelo, int hasta)
    {
        float y = suelo;
        for (int i = 0; i < hasta && i < items.Count; i++)
        {
            var o = items[i];
            float dx = o.pl.x - c.x, dz = o.pl.z - c.z;
            float limite = (o.radio + radio) * 0.7f;
            if (dx * dx + dz * dz < limite * limite)
                y = Mathf.Max(y, o.pl.y + o.alto * 0.8f);
        }
        return y;
    }

    // Busca dónde cae: prueba varios puntos al azar dentro de la caja y se queda con el más bajo (primero se
    // llena el fondo y, cuando no queda sitio libre, los nuevos se apoyan encima de los que ya están).
    bool Colocar(float radio, out Vector3 pl, out float suelo)
    {
        pl = Vector3.zero;
        suelo = 0f;
        if (!InteriorCaja(out Interior caja)) return false;

        // Todo en el espacio local de la caja. Los puntos se reparten por el suelo de la caja, que puede estar
        // girado respecto a los ejes: se avanza a lo largo de sus propios ejes (a y b).
        float a = caja.ejeA.magnitude, b = caja.ejeB.magnitude;
        Vector3 ua = caja.ejeA / Mathf.Max(1e-6f, a), ub = caja.ejeB / Mathf.Max(1e-6f, b);
        float ra = Mathf.Max(0f, a * areaApilado - radio * 0.6f);
        float rb = Mathf.Max(0f, b * areaApilado - radio * 0.6f);
        float fondoY = caja.centro.y - caja.ejeV.y + 2f * caja.ejeV.y * alturaFondo;

        // Se prueban varios puntos: gana el más bajo y, entre los que quedan igual de bajos, el más despejado
        // (el que menos se pisa con los que ya están).
        var pos = new Vector3[IntentosPosicion];
        var alt = new float[IntentosPosicion];
        var hol = new float[IntentosPosicion];
        float minY = float.MaxValue;
        for (int k = 0; k < IntentosPosicion; k++)
        {
            float u = UnityEngine.Random.value * 2f - 1f;
            float v = UnityEngine.Random.value * 2f - 1f;
            Vector3 c = caja.centro + ua * (u * ra) + ub * (v * rb);
            c.y = fondoY;
            pos[k] = c;
            alt[k] = AlturaApoyo(c, radio, fondoY, items.Count);
            hol[k] = Holgura(c, radio);
            if (alt[k] < minY) minY = alt[k];
        }

        float margen = Mathf.Max(0.002f, radio * 0.25f);
        int mejor = -1;
        for (int k = 0; k < IntentosPosicion; k++)
        {
            if (alt[k] > minY + margen) continue;
            if (mejor < 0 || hol[k] > hol[mejor]) mejor = k;
        }
        if (mejor < 0) return false;

        pl = new Vector3(pos[mejor].x, alt[mejor], pos[mejor].z);
        suelo = fondoY;
        return true;
    }

    // Distancia libre entre un modelo de ese radio puesto en 'c' y el más cercano de los que ya están
    // (negativa = se pisan, con el mismo criterio que AlturaApoyo).
    float Holgura(Vector3 c, float radio)
    {
        float h = float.MaxValue;
        foreach (var o in items)
        {
            float dx = o.pl.x - c.x, dz = o.pl.z - c.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz) - (o.radio + radio) * 0.7f;
            if (d < h) h = d;
        }
        return h;
    }

    // Vuelve a calcular a qué altura queda cada modelo (cuando se retira uno, los de arriba bajan).
    void Reposar(bool animar)
    {
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            float y = AlturaApoyo(it.pl, it.radio, it.suelo, i);
            if (Mathf.Abs(y - it.pl.y) < 1e-5f) continue;
            it.pl.y = y;
            if (it.t == null || it.cayendo) continue;   // el que cae ya sigue su destino
            if (animar) StartCoroutine(Bajar(it));
            else it.t.position = transform.TransformPoint(it.pl);
        }
    }

    // El interior de la caja como una caja orientada (puede estar girada respecto a los ejes), en el espacio
    // local de CajaEntrega. Los tres ejes son medias longitudes; ejeV mira hacia arriba.
    struct Interior
    {
        public Vector3 centro, ejeA, ejeB, ejeV;
    }

    // Matriz que lleva un punto del espacio local de 't' al espacio local de la caja de entrega, subiendo por los
    // padres (sin pasar por coordenadas de mundo, que pierden precisión con la granja tan pequeña).
    Matrix4x4 MatrizHaciaCaja(Transform t)
    {
        Matrix4x4 m = Matrix4x4.identity;
        while (t != null && t != transform)
        {
            m = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * m;
            t = t.parent;
        }
        return m;
    }

    // Mide la caja a partir de la malla más grande del modelo: usa sus propios ejes (el modelo viene girado dentro
    // del .glb) y no una caja alineada con los ejes, que sobraba por las esquinas.
    bool InteriorCaja(out Interior r)
    {
        r = default;
        Transform marco = ModeloCajaTransform();
        if (marco != null)
        {
            MeshFilter mejor = null;
            float volMejor = 0f;
            foreach (var mf in marco.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 m = MatrizHaciaCaja(mf.transform);
                float vol = m.MultiplyVector(new Vector3(mb.size.x, 0f, 0f)).magnitude
                          * m.MultiplyVector(new Vector3(0f, mb.size.y, 0f)).magnitude
                          * m.MultiplyVector(new Vector3(0f, 0f, mb.size.z)).magnitude;
                if (vol > volMejor) { volMejor = vol; mejor = mf; }
            }

            if (mejor != null)
            {
                Bounds mb = mejor.sharedMesh.bounds;
                Matrix4x4 m = MatrizHaciaCaja(mejor.transform);
                return Armar(m.MultiplyPoint3x4(mb.center),
                    m.MultiplyVector(new Vector3(mb.extents.x, 0f, 0f)),
                    m.MultiplyVector(new Vector3(0f, mb.extents.y, 0f)),
                    m.MultiplyVector(new Vector3(0f, 0f, mb.extents.z)),
                    out r);
            }
        }

        // Sin modelo: usa el Box Collider de la caja.
        var caja = GetComponent<BoxCollider>();
        if (caja == null) return false;
        return Armar(caja.center,
            new Vector3(caja.size.x * 0.5f, 0f, 0f),
            new Vector3(0f, caja.size.y * 0.5f, 0f),
            new Vector3(0f, 0f, caja.size.z * 0.5f),
            out r);
    }

    // De los tres ejes de la malla, el más vertical es la altura; los otros dos forman el suelo.
    static bool Armar(Vector3 centro, Vector3 e0, Vector3 e1, Vector3 e2, out Interior r)
    {
        Vector3[] ejes = { e0, e1, e2 };
        int v = 0;
        float mejor = -1f;
        for (int i = 0; i < 3; i++)
        {
            float vertical = Mathf.Abs(ejes[i].y) / Mathf.Max(1e-9f, ejes[i].magnitude);
            if (vertical > mejor) { mejor = vertical; v = i; }
        }

        r = new Interior { centro = centro };
        r.ejeV = ejes[v].y < 0f ? -ejes[v] : ejes[v];
        r.ejeA = ejes[(v + 1) % 3];
        r.ejeB = ejes[(v + 2) % 3];
        return r.ejeV.y > 1e-6f;
    }

    // ---------- Animaciones ----------

    // Cae desde arriba hasta su sitio en la pila (si el sitio cambia mientras cae, lo sigue).
    IEnumerator Caer(Item it, float demora)
    {
        Transform t = it.t;
        it.cayendo = true;
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
            Vector3 destino = transform.TransformPoint(it.pl);
            Vector3 arriba = destino + transform.up * (alturaCaida * transform.lossyScale.x);
            t.position = Vector3.LerpUnclamped(arriba, destino, EaseOutBounce(Mathf.Clamp01(e / dur)));
            yield return null;
        }
        if (t != null) t.position = transform.TransformPoint(it.pl);
        it.cayendo = false;
    }

    // Un modelo ya asentado baja a su nuevo sitio cuando se retira el que tenía debajo.
    IEnumerator Bajar(Item it)
    {
        Transform t = it.t;
        if (t == null) yield break;
        Vector3 inicio = t.position;
        const float dur = 0.18f;
        for (float e = 0f; e < dur;)
        {
            e += Time.deltaTime;
            if (t == null || it.cayendo) yield break;
            t.position = Vector3.Lerp(inicio, transform.TransformPoint(it.pl), Mathf.Clamp01(e / dur));
            yield return null;
        }
        if (t != null && !it.cayendo) t.position = transform.TransformPoint(it.pl);
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

    // Cuando ya cayeron todos, la caja sube y se encoge con sus productos, desaparece, y vuelve vacía con un pedido nuevo.
    IEnumerator AnimarEntrega(float espera)
    {
        animando = true;
        if (colision != null) colision.enabled = false;

        // Los productos que faltaban por caer terminan de caer antes de que la caja se vaya.
        yield return new WaitForSeconds(Mathf.Max(0f, espera) + Mathf.Max(0f, segundosAsentar));

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
    // Al seleccionar la caja muestra su interior (en amarillo), que es donde caen los productos.
    void OnDrawGizmosSelected()
    {
        if (!InteriorCaja(out Interior caja)) return;

        // Marco con los ejes de la caja (girado respecto a CajaEntrega si el modelo viene girado).
        float a = caja.ejeA.magnitude, b = caja.ejeB.magnitude, h = caja.ejeV.magnitude;
        Quaternion giro = Quaternion.LookRotation(caja.ejeB / Mathf.Max(1e-6f, b), caja.ejeV / Mathf.Max(1e-6f, h));

        Matrix4x4 anterior = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix * Matrix4x4.TRS(caja.centro, giro, Vector3.one);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(2f * a, 2f * h, 2f * b));
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireCube(new Vector3(0f, -h + 2f * h * alturaFondo, 0f),
                            new Vector3(2f * a * areaApilado, 0f, 2f * b * areaApilado));
        Gizmos.matrix = anterior;
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
