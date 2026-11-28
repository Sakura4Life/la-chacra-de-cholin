using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Un animal de un corral para la partida guardada.
[System.Serializable]
public class DatosAnimal
{
    public bool cria;
    public float crecido;
    public bool comiendo;
    public float comido;
    public int raciones;
    public bool gallo;        // gallinero: es un gallo (o lo será, si es cría)
    public bool nacido;       // nació en la granja (ocupa un cupo de crías)
    public bool tienePuesto;  // false en partidas antiguas: se le da el primer puesto libre
    public int puesto;
    public int variante;      // 0 = normal; 1 = gallina2 / cuy mostaza
    public float faltaLibre;  // segundos que le faltan para poder entrar en celo otra vez
    public bool principal;    // es la gallina del tutorial (solo se usa dentro de la caja de pedidos)

    // ¿Gasta una plaza de compra o de crías del corral? La gallina del tutorial tiene la suya aparte.
    public bool OcupaPlaza { get { return !principal; } }
}

// Estado de un corral para la partida guardada.
[System.Serializable]
public class DatosCorral
{
    public int id;
    public long ticks;              // hora real del guardado
    public int produccion;          // huevos esperando
    public float faltaLibrePrincipal;   // lo mismo que faltaLibre, para la gallina del tutorial
    public DatosAnimal[] animales = new DatosAnimal[0];
}

// Fila de la ventana de alimentar.
public class InfoAnimal
{
    public string nombre;
    public string estado;
    public bool puedeComer;
    public int indice;       // -1 = la gallina principal del gallinero
}

// Un adulto que se puede vender en la tienda: la tienda los lista para elegir exactamente cuál.
public class AnimalEnVenta
{
    public IAnimalVendible animal;
    public int id;           // identificador estable mientras el animal exista
    public int orden;        // posición en la lista del corral (desempata el orden)
    public string nombre;    // el mismo que en la ventana de alimentar
    public int raciones;
    public int valor;        // lo que paga la tienda según su engorde
    public bool engorda;
}

// Un corral: gallinero o criadero de cuy. Guarda los animales comprados y nacidos (la gallina del tutorial
// cuenta aparte en el gallinero), deja alimentarlos, acumula lo que producen, los hace pasear sin chocar
// y organiza las crías.
// Va en el objeto del edificio. Al añadirlo en el Inspector rellena casi todo solo (menú ⋮ → Reset).
public class Corral : MonoBehaviour
{
    [Header("Qué vive aquí")]
    [SerializeField] Animal especie = Animal.Gallina;
    [Tooltip("Número único del corral: gallinero 0, criadero de cuy 1.")]
    [SerializeField] int idCorral;
    [Tooltip("Modelo del animal (gallina.glb, cuy.glb...).")]
    [SerializeField] GameObject modeloAnimal;
    [Tooltip("Solo gallinero: modelo del gallo (gallo.glb).")]
    [SerializeField] GameObject modeloGallo;
    [Tooltip("Solo gallinero: segundo diseño de gallina (gallina2.glb). Sale al azar al comprar.")]
    [SerializeField] GameObject modeloGallina2;
    [Tooltip("Modelo de la cría (pollito.glb en el gallinero). Vacío = el mismo modelo del animal, más pequeño.")]
    [SerializeField] GameObject modeloCria;
    [Tooltip("Tamaño del animal en unidades de granja.")]
    [SerializeField] float tamanoAnimal = 0.06f;
    [Tooltip("El gallo es este número de veces más grande que la gallina.")]
    [SerializeField] float escalaGallo = 1.25f;
    [Tooltip("Dónde aparecen los animales al comprarlos o nacer (uno por cada cupo de compra más uno por cada cría). En el criadero, además, los puestos delimitan por dónde pasean los cuyes. Si faltan se reparten en una cuadrícula.")]
    [SerializeField] Transform[] puestos;
    [Tooltip("Dejar APAGADO. (Antes el gallinero ocupaba los puestos desde el último.)")]
    [SerializeField] bool puestosDesdeElFinal;
    [Tooltip("Solo gallinero: la gallina del tutorial (también cría, pasea por su ruta y cuenta como una gallina más).")]
    [SerializeField] Gallina gallinaPrincipal;

    [Header("Variantes")]
    [Tooltip("Probabilidad (0 a 1) de que un animal comprado o nacido sea de la variante: gallina2 en el gallinero, cuy mostaza en el criadero.")]
    [Range(0f, 1f)]
    [SerializeField] float probabilidadVariante = 0.35f;
    [Tooltip("Color del cuy de la variante (mostaza).")]
    [SerializeField] Color colorVariante = new Color(0.85f, 0.62f, 0.12f);

    [Header("Crías")]
    [Tooltip("Segundos de descanso de cada padre antes de poder entrar en celo otra vez (valor de prueba).")]
    [SerializeField] float segundosEntreCrias = 60f;
    [Tooltip("Segundos que dura el celo desde que se le da de comer (valor de prueba).")]
    [SerializeField] float segundosCelo = 30f;
    [Tooltip("Segundos que tarda una cría en crecer (valor de prueba).")]
    [SerializeField] float segundosCrecer = 40f;
    [Tooltip("Solo gallinero: probabilidad de que el pollito sea gallo (hasta el máximo de 2 gallos).")]
    [Range(0f, 1f)]
    [SerializeField] float probabilidadGallo = 0.3f;

    [Header("Paseo")]
    [Tooltip("Velocidad al caminar, en tamaños de animal por segundo.")]
    [SerializeField] float velocidadCaminata = 1.6f;
    [Tooltip("Solo gallinero: probabilidad de que un adulto, al decidir adónde ir, suba la rampa hasta la puerta.")]
    [Range(0f, 1f)]
    [SerializeField] float probabilidadRampa = 0.2f;
    [Tooltip("Si los animales caminan hundidos en el piso (o flotando), sube o baja su altura con este valor.")]
    [SerializeField] float alturaSueloExtra;
    [Tooltip("Gallinero: puntos, EN ORDEN, de una vuelta grande por la granja (frente al criadero y las parcelas). Las gallinas y los gallos la recorren de vez en cuando. Vacío = solo pasean junto al gallinero.")]
    [SerializeField] Transform[] rutaAmplia;
    [Tooltip("Probabilidad de que, al decidir adónde ir, un adulto haga la vuelta grande.")]
    [Range(0f, 1f)]
    [SerializeField] float probabilidadVuelta = 0.35f;
    [Tooltip("Corrales cerrados (criadero): distancia, en tamaños de animal, entre el borde del edificio (techo incluido) y el borde del cuerpo de los animales.")]
    [SerializeField] float margenParedes = 0.45f;

    [Header("Producción (huevos)")]
    [SerializeField] GameObject modeloProduccion;
    [Tooltip("Punto del edificio sobre el que flota el icono de lo que hay para recoger.")]
    [SerializeField] Transform puntoProduccion;
    [SerializeField] Vector3 desplazamientoIcono = new Vector3(0f, 0.07f, 0f);
    [SerializeField] float tamanoProduccion = 0.025f;
    [Tooltip("Tope único de huevos esperando en el gallinero (suma de todas las gallinas).")]
    [SerializeField] int maxHuevos = 12;

    [Header("Solo edificios que se compran (criadero)")]
    [Tooltip("Al encenderse, escala el modelo del edificio a este ancho y pone su base en el origen.")]
    [SerializeField] bool ajustarModelo;
    [Tooltip("El modelo del edificio. Si lo dejas vacío se usa el primer hijo que no se llame Puesto.")]
    [SerializeField] Transform modeloEdificio;
    [Tooltip("Marcado: el edificio queda exactamente como lo colocaste en el prefab (tamaño, giro y posición). Desmárcalo para ajustarlo al Ancho Edificio.")]
    [SerializeField] bool conservarTamano = true;
    [Tooltip("Solo si no conservas el tamaño: ancho final del edificio en unidades de granja.")]
    [SerializeField] float anchoEdificio = 0.22f;

    [Header("Animales")]
    [Tooltip("Giro extra (grados) del modelo del animal, por si sale mirando hacia otro lado. Normalmente (0, 0, 0).")]
    [SerializeField] Vector3 rotacionExtra;

    readonly List<AnimalVisual> animales = new List<AnimalVisual>();
    int produccion;
    int mostrado = -1;
    IconoProduccion icono;
    int contadorNombres;

    bool criando;                     // hay una pareja organizándose ahora mismo
    double libreGallinaPrincipal;     // hora (RelojReal) desde la que la gallina del tutorial puede volver a criar
    double proximaRevision;
    string avisoCelo;                 // mensaje que deja AlEmpezarAComer para quien alimentó

    static readonly List<Corral> activos = new List<Corral>();
    static readonly Dictionary<int, DatosCorral> pendientes = new Dictionary<int, DatosCorral>();

    public Animal Especie { get { return especie; } }
    public float TamanoAnimal { get { return tamanoAnimal; } }
    public float SegundosCrecer { get { return Mathf.Max(1f, segundosCrecer); } }
    public float VelocidadPaseo { get { return tamanoAnimal * velocidadCaminata; } }
    public bool TieneModelo { get { return modeloAnimal != null; } }
    public bool TieneModeloGallo { get { return modeloGallo != null; } }

    // Animales que se pueden comprar para este corral.
    public int Capacidad { get { return AnimalDatos.Capacidad(especie); } }
    // Crías que pueden haber nacido aquí.
    public int MaxCrias { get { return AnimalDatos.MaxCrias(especie); } }
    // Plazas sin animal (compras y crías comparten el mismo total: una cría puede ocupar una plaza de compra libre).
    // Los animales que están en la caja de pedidos siguen guardando su plaza (para poder devolverlos).
    public int PlazasLibres
    {
        get
        {
            PedidosCaja.EnCaja(especie, out int c, out int n, out _);
            return Mathf.Max(0, Capacidad + MaxCrias - animales.Count - c - n);
        }
    }

    int Principal { get { return (gallinaPrincipal != null && gallinaPrincipal.EnGallinero) ? 1 : 0; } }
    public int Cantidad { get { return animales.Count + Principal; } }
    // Todos los animales que caben: comprados + crías + la gallina del tutorial.
    public int CapacidadTotal { get { return Capacidad + MaxCrias + (gallinaPrincipal != null ? 1 : 0); } }

    public int Compras
    {
        get
        {
            PedidosCaja.EnCaja(especie, out int enCaja, out _, out _);
            int n = enCaja;
            foreach (var a in animales) if (a != null && !a.Nacido) n++;
            return n;
        }
    }

    public int Nacidos
    {
        get
        {
            PedidosCaja.EnCaja(especie, out _, out int enCaja, out _);
            int n = enCaja;
            foreach (var a in animales) if (a != null && a.Nacido) n++;
            return n;
        }
    }

    // Gallos del gallinero, contando los pollitos que lo serán (y los que están en la caja de pedidos).
    public int Gallos
    {
        get
        {
            PedidosCaja.EnCaja(especie, out _, out _, out int enCaja);
            int n = enCaja;
            foreach (var a in animales) if (a != null && a.Tipo == Animal.Gallo) n++;
            return n;
        }
    }

    public bool HayLugarCompra { get { return Compras < Capacidad; } }

    // Huevos por recoger: los de las gallinas compradas más los de la gallina del tutorial.
    int ProduccionTotal
    {
        get { return produccion + (gallinaPrincipal != null ? gallinaPrincipal.Pendientes : 0); }
    }

    // Gallinas que están comiendo y pondrán un huevo: su lugar ya está reservado.
    int HuevosEnCamino
    {
        get
        {
            int n = (gallinaPrincipal != null && gallinaPrincipal.EnGallinero && gallinaPrincipal.Comiendo) ? 1 : 0;
            foreach (var a in animales)
                if (a != null && a.Tipo == Animal.Gallina && !a.Cria && a.Comiendo) n++;
            return n;
        }
    }

    // Tope único de huevos: los esperando más los que están por poner.
    public bool HuevosLleno { get { return especie == Animal.Gallina && ProduccionTotal + HuevosEnCamino >= maxHuevos; } }

    // Lo usa la gallina del tutorial para saber si puede comer.
    public static bool GallineroLleno
    {
        get { var c = De(Animal.Gallina); return c != null && c.HuevosLleno; }
    }
    public const int TopeSinCorral = 12;

    // El corral de una especie (el gallo vive en el gallinero).
    public static Corral De(Animal a)
    {
        Animal e = AnimalDatos.EspecieCorral(a);
        foreach (var c in activos) if (c != null && c.especie == e) return c;
        return null;
    }

    // Texto de la tienda: cuántos se han comprado y cuántas crías hay.
    public string TextoCupo(Animal a)
    {
        // Corral grande: los 4 lugares los comparten los comprados y las crías.
        if (AnimalDatos.CorralGrande(especie))
            return "corral " + (Capacidad + MaxCrias - PlazasLibres) + "/" + (Capacidad + MaxCrias);
        string corral = "corral " + Compras + "/" + Capacidad;
        if (a == Animal.Gallo) return "gallos " + Gallos + "/" + AnimalDatos.MaxGallos + "  ·  " + corral;
        return corral + (MaxCrias > 0 ? "  ·  plazas libres " + PlazasLibres : "");
    }

    void Awake()
    {
        activos.Add(this);
        if (ajustarModelo) AjustarEdificio();
        if (AnimalDatos.CorralGrande(especie)) CrearZonaDeToque();
        ModeloUtil.AsegurarCollider(gameObject);
        if (GetComponent<Interactuable>() == null) Interactuable.Crear(gameObject, Interactuable.Tipo.Corral);
    }

    // Espera un fotograma y aplica la partida guardada de este corral, si la hay.
    IEnumerator Start()
    {
        yield return null;

        int necesarios = Capacidad + MaxCrias;
        int validos = 0;
        if (puestos != null) foreach (var p in puestos) if (p != null) validos++;
        // Un corral grande con vallas reparte solo a sus animales dentro de ellas aunque no tenga puestos.
        bool conVallas = AnimalDatos.CorralGrande(especie) && LimitesRecinto(0f, out _, out _);
        if (validos < necesarios && !conVallas)
            Debug.LogWarning("Corral '" + name + "': tiene " + validos + " puestos y necesita " + necesarios
                             + " (" + Capacidad + " de compra + " + MaxCrias + " de crías). Los que falten se repartirán en una cuadrícula.", this);

        if (pendientes.TryGetValue(idCorral, out DatosCorral datos))
        {
            pendientes.Remove(idCorral);
            Aplicar(datos);
        }
    }

    void OnDestroy() { activos.Remove(this); }

    // Si el corral se apaga en medio de una cría, se libera a todos.
    void OnDisable()
    {
        criando = false;
        foreach (var a in animales) if (a != null) a.Detener();
    }

    // Mantiene el icono al día y gestiona el celo y las parejas.
    void Update()
    {
        if (ProduccionTotal != mostrado) ActualizarIcono();

        if (RelojReal.Ahora >= proximaRevision)
        {
            proximaRevision = RelojReal.Ahora + 0.4;
            GestionarCelo();
        }
    }

    // ---------- Zona de toque (corrales grandes) ----------

    // Cerdo, llama y vaca: se abre la ventana de alimentar tocando en CUALQUIER parte dentro de las vallas. Una losa
    // plana, a ras del suelo, cubre todo el interior. Los animales tienen su propio collider (más arriba), así que
    // al tocar a uno se alimenta a ese con heno, y al tocar el suelo vacío se abre la ventana. El criadero de cuy no
    // lleva esta zona. Si el objeto ya tiene un collider propio, no se crea nada.
    void CrearZonaDeToque()
    {
        if (GetComponent<Collider>() != null) return;
        if (!huellaLista) CalcularHuella();
        if (!huellaValida) return;

        float y = (ZonaPuestos(out _, out _, out float ys) ? ys : 0f) + alturaSueloExtra;
        const float grosor = 0.012f;
        var caja = gameObject.AddComponent<BoxCollider>();
        caja.center = new Vector3((huellaMin.x + huellaMax.x) * 0.5f, y + grosor * 0.25f, (huellaMin.y + huellaMax.y) * 0.5f);
        caja.size = new Vector3(huellaMax.x - huellaMin.x, grosor, huellaMax.y - huellaMin.y);
    }

    // ---------- Edificio nuevo ----------

    void AjustarEdificio()
    {
        if (conservarTamano) return;   // se respeta lo que colocaste en el prefab

        if (modeloEdificio == null)
        {
            foreach (Transform h in transform)
                if (!h.name.StartsWith("Puesto")) { modeloEdificio = h; break; }
        }
        if (modeloEdificio == null) return;

        if (ModeloUtil.LimitesLocales(transform, out Bounds b))
        {
            float ancho = Mathf.Max(b.size.x, b.size.z);
            if (ancho > 1e-6f) modeloEdificio.localScale *= anchoEdificio / ancho;
        }
        if (ModeloUtil.LimitesLocales(transform, out b))
            modeloEdificio.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
    }

    // ---------- Guardado ----------

    public static void Limpiar()
    {
        pendientes.Clear();
        activos.Clear();
    }

    public static void Preparar(DatosCorral[] datos)
    {
        pendientes.Clear();
        if (datos == null) return;
        foreach (var d in datos)
        {
            if (d == null) continue;
            var corral = activos.Find(c => c != null && c.idCorral == d.id);
            if (corral != null) corral.Aplicar(d);
            else pendientes[d.id] = d;
        }
    }

    public static DatosCorral[] ExportarTodos()
    {
        var lista = new List<DatosCorral>();
        var vistos = new HashSet<int>();
        foreach (var c in activos)
        {
            if (c == null) continue;
            lista.Add(c.Crear());
            vistos.Add(c.idCorral);
        }
        foreach (var par in pendientes)
            if (!vistos.Contains(par.Key)) lista.Add(par.Value);
        return lista.ToArray();
    }

    DatosCorral Crear()
    {
        // Datos cargados que aún no se aplicaron: se guardan tal cual.
        if (pendientes.TryGetValue(idCorral, out DatosCorral sinAplicar)) return sinAplicar;

        double ahora = RelojReal.Ahora;
        var d = new DatosCorral
        {
            id = idCorral,
            ticks = RelojReal.TicksAhora,
            produccion = produccion,
            faltaLibrePrincipal = Mathf.Max(0f, (float)(libreGallinaPrincipal - ahora)),
            animales = new DatosAnimal[animales.Count]
        };
        for (int i = 0; i < animales.Count; i++)
        {
            var v = animales[i];
            d.animales[i] = new DatosAnimal
            {
                cria = v.Cria,
                crecido = v.Crecido,
                comiendo = v.Comiendo,
                comido = v.ComidoSegundos,
                raciones = v.Raciones,
                gallo = v.Tipo == Animal.Gallo,
                nacido = v.Nacido,
                tienePuesto = true,
                puesto = v.Puesto,
                variante = v.Variante,
                faltaLibre = Mathf.Max(0f, (float)(v.LibreDesde - ahora))
            };
        }
        return d;
    }

    void Aplicar(DatosCorral d)
    {
        float fuera = RelojReal.Transcurrido(d.ticks);
        double ahora = RelojReal.Ahora;
        produccion = Mathf.Clamp(d.produccion, 0, maxHuevos);
        libreGallinaPrincipal = ahora + Mathf.Max(0f, d.faltaLibrePrincipal - fuera);

        int cupo = Capacidad + MaxCrias;
        foreach (var a in d.animales)
        {
            if (a == null || animales.Count >= cupo) continue;

            Animal tipo = (especie == Animal.Gallina && a.gallo) ? Animal.Gallo : especie;

            // Una cría que creció mientras no estabas nace ya adulta.
            bool cria = a.cria;
            float crecido = a.crecido;
            if (cria)
            {
                crecido += fuera;
                if (crecido >= SegundosCrecer) { cria = false; crecido = 0f; }
            }

            // Partidas antiguas (sin puesto guardado) o puestos repetidos: el primer puesto libre.
            int puesto = (a.tienePuesto && a.puesto >= 0 && a.puesto < cupo && !PuestoOcupado(a.puesto)) ? a.puesto : PuestoLibre();

            var v = CrearAnimal(tipo, cria, a.nacido, puesto, null, crecido, a.raciones, a.variante);
            if (v == null) break;
            v.LibreDesde = ahora + Mathf.Max(0f, a.faltaLibre - fuera);
            // Si ya pasó el tiempo de la comida, termina en su primer fotograma y pone el huevo.
            if (a.comiendo && !cria) v.Comer(a.comido + fuera);
        }

        // Las crías que siguen creciendo vuelven a seguir a uno de los adultos.
        foreach (var v in animales) if (v != null && v.Cria) v.Padre = ElegirPadre(v);
        ActualizarIcono();
    }

    // ---------- Puestos y zona de paseo ----------

    bool PuestoOcupado(int k)
    {
        foreach (var a in animales) if (a != null && a.Puesto == k) return true;
        return false;
    }

    // Primer puesto sin animal, o -1 si no queda ninguno.
    int PuestoLibre()
    {
        int cupo = Capacidad + MaxCrias;
        for (int k = 0; k < cupo; k++)
            if (!PuestoOcupado(k)) return k;
        return -1;
    }

    // Posición en el mundo del puesto 'i'.
    Vector3 PosicionPuesto(int i)
    {
        if (puestos != null && i >= 0 && i < puestos.Length)
        {
            int k = puestosDesdeElFinal ? puestos.Length - 1 - i : i;
            if (puestos[k] != null) return puestos[k].position;
        }
        const float s = 0.06f;
        int n = Mathf.Max(0, i);
        if (AnimalDatos.CorralGrande(especie) && LimitesRecinto(tamanoAnimal * 0.4f, out Vector2 rmin, out Vector2 rmax))
        {
            // Corral con vallas y sin puestos: cuadrícula de 2 x 2 repartida dentro de ellas.
            float fx = ((n % 2) + 0.5f) / 2f, fz = (((n / 2) % 2) + 0.5f) / 2f;
            return transform.TransformPoint(new Vector3(Mathf.Lerp(rmin.x, rmax.x, fx), alturaSueloExtra,
                                                        Mathf.Lerp(rmin.y, rmax.y, fz)));
        }
        return transform.TransformPoint(new Vector3(((n % 3) - 1) * s, 0f, (((n / 3) % 3) - 1) * s));
    }

    // Rectángulo (en local del corral) que forman los puestos y altura media del suelo.
    bool ZonaPuestos(out Vector3 min, out Vector3 max, out float y)
    {
        min = Vector3.zero;
        max = Vector3.zero;
        y = 0f;
        if (puestos == null) return false;

        int n = 0;
        float sumaY = 0f;
        foreach (var p in puestos)
        {
            if (p == null) continue;
            Vector3 l = transform.InverseTransformPoint(p.position);
            if (n == 0) { min = l; max = l; }
            else
            {
                min = Vector3.Min(min, l);
                max = Vector3.Max(max, l);
            }
            sumaY += l.y;
            n++;
        }
        if (n < 2) return false;
        y = sumaY / n;
        return true;
    }

    // ¿Hay ruta de paseo del gallinero (los puntos de la gallina del tutorial)?
    bool TieneRuta()
    {
        if (gallinaPrincipal == null || gallinaPrincipal.Ruta == null) return false;
        foreach (var r in gallinaPrincipal.Ruta) if (r != null) return true;
        return false;
    }

    bool TieneRampa()
    {
        return gallinaPrincipal != null && gallinaPrincipal.PuntoGallinero != null && gallinaPrincipal.PuntoRampa != null;
    }

    // Pie de la rampa del gallinero (en local del corral).
    public bool PieRampaLocal(out Vector3 pie)
    {
        pie = Vector3.zero;
        if (!TieneRampa()) return false;
        pie = transform.InverseTransformPoint(gallinaPrincipal.PuntoGallinero.position);
        return true;
    }

    Vector3 PuertaRampaLocal()
    {
        return transform.InverseTransformPoint(gallinaPrincipal.PuntoRampa.position);
    }

    // Perfil de la rampa del gallinero, medido sobre el modelo: altura de la superficie (en local del corral,
    // sobre el pie de la rampa) cada 2,5 % del recorrido pie -> puerta (41 valores). Se midió con rayos lanzados
    // desde justo encima de la tabla (0,09), así NO cuenta el travesaño que hay más arriba (0,115): los animales
    // pasan por debajo, y tomarlo por suelo los hacía dar un brinco al entrar y salir. El resalte de la tabla
    // (0,065) se ve como un leve escalón. Los dos últimos valores coinciden con la altura de la puerta.
    static readonly float[] perfilRampa =
    {
        0f, 0f, 0f, 0.003f, 0.009f, 0.012f, 0.019f, 0.031f, 0.035f, 0.040f,
        0.047f, 0.059f, 0.063f, 0.065f, 0.065f, 0.063f, 0.062f, 0.061f, 0.053f, 0.045f,
        0.045f, 0.045f, 0.045f, 0.045f, 0.045f, 0.045f, 0.045f, 0.045f, 0.045f, 0.045f,
        0.045f, 0.045f, 0.045f, 0.045f, 0.045f, 0.045f, 0.045f, 0.045f, 0.046f, 0.047f, 0.049f
    };

    // Si 'p' (local del corral) está sobre la rampa, devuelve la altura a la que debe ir el animal para no
    // atravesarla. En los bordes se mezcla con la altura normal para que no pegue saltos.
    public bool AlturaRampa(Vector3 p, out float y)
    {
        y = p.y;
        if (!PieRampaLocal(out Vector3 pie)) return false;

        Vector3 pu = PuertaRampaLocal();
        Vector2 a = new Vector2(pie.x, pie.z);
        Vector2 ab = new Vector2(pu.x, pu.z) - a;
        float l2 = ab.sqrMagnitude;
        if (l2 < 1e-8f) return false;

        Vector2 q = new Vector2(p.x, p.z);
        float t = Vector2.Dot(q - a, ab) / l2;
        if (t <= 0f || t >= 1f) return false;

        // Medio ancho de la rampa: la tabla real mide ~0,022 a cada lado del eje; con 0,6 el animal conserva
        // la altura de la tabla hasta 0,022 y la pierde del todo a 0,036 (antes flotaba junto a la tabla).
        float ancho = tamanoAnimal * 0.6f;
        float lateral = (q - (a + ab * t)).magnitude;
        if (lateral >= ancho) return false;

        // (Mathf.SmoothStep interpola entre dos valores; no es el smoothstep de los shaders, por eso se escribe a mano)
        float borde = Mathf.Clamp01((lateral / ancho - 0.6f) / 0.4f);
        float peso = 1f - borde * borde * (3f - 2f * borde);
        float f = t * (perfilRampa.Length - 1);
        int i = Mathf.Min(perfilRampa.Length - 2, (int)f);
        float h = pie.y + Mathf.Lerp(perfilRampa[i], perfilRampa[i + 1], f - i);
        y = Mathf.Lerp(p.y, h, peso);
        return true;
    }

    // ---------- Paredes (corrales cerrados) ----------

    bool huellaLista;
    bool huellaValida;
    Vector2 huellaMin, huellaMax;

    // Huella del edificio en el plano local del corral, medida con las mallas del modelo (orientada; la caja
    // del mundo se agranda cuando la granja está girada). Solo cuenta lo que cuelga de un hijo "Modelo..." o del
    // modeloEdificio: ni los animales ni los iconos.
    void CalcularHuella()
    {
        huellaLista = true;
        huellaValida = false;
        bool primero = true;
        foreach (var mf in GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.GetComponentInParent<AnimalVisual>() != null) continue;

            Transform h = mf.transform;
            while (h.parent != null && h.parent != transform) h = h.parent;
            // Cuentan como edificio los hijos "Modelo...", el modeloEdificio y, en los corrales grandes, las vallas
            // (cualquier hijo cuyo nombre empiece por "valla", con mayúsculas o sin ellas: vallaCorral, Valla1...).
            bool esValla = h.name.StartsWith("valla", System.StringComparison.OrdinalIgnoreCase);
            if (!(h.name.StartsWith("Modelo") || h == modeloEdificio || esValla)) continue;

            Bounds b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 e = b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 l = transform.InverseTransformPoint(mf.transform.TransformPoint(e));
                var q = new Vector2(l.x, l.z);
                if (primero) { huellaMin = q; huellaMax = q; primero = false; }
                else { huellaMin = Vector2.Min(huellaMin, q); huellaMax = Vector2.Max(huellaMax, q); }
            }
        }
        huellaValida = !primero;
    }

    // Corral cerrado (criadero...): rectángulo local por donde puede andar el centro de un animal de radio 'radio'.
    bool LimitesRecinto(float radio, out Vector2 min, out Vector2 max)
    {
        min = Vector2.zero;
        max = Vector2.zero;
        if (especie == Animal.Gallina) return false;   // el gallinero no tiene paredes: pasean por todo el patio
        if (!huellaLista) CalcularHuella();
        if (!huellaValida) return false;

        float m = tamanoAnimal * margenParedes + radio;
        min = new Vector2(huellaMin.x + m, huellaMin.y + m);
        max = new Vector2(huellaMax.x - m, huellaMax.y - m);
        if (max.x < min.x) min.x = max.x = (min.x + max.x) * 0.5f;
        if (max.y < min.y) min.y = max.y = (min.y + max.y) * 0.5f;
        return true;
    }

    // Mantiene un punto (local del corral) dentro de las paredes (corral cerrado) o fuera de las zonas por donde
    // no se puede pasar (corral abierto: parcelas, otros edificios y el cuerpo del gallinero). Solo cambia x y z.
    public Vector3 Limitar(Vector3 p, float radio)
    {
        if (LimitesRecinto(radio, out Vector2 mn, out Vector2 mx))
        {
            p.x = Mathf.Clamp(p.x, mn.x, mx.x);
            p.z = Mathf.Clamp(p.z, mn.y, mx.y);
            return p;
        }
        return FueraDeProhibidas(p, radio);
    }

    // ---------- Zonas prohibidas (corral abierto) ----------

    // Rectángulo orientado en el plano local del corral: centro, ejes (unitarios) y medias longitudes.
    struct Caja
    {
        public Vector2 c, u, v;
        public float hu, hv;
    }

    readonly List<Caja> prohibidas = new List<Caja>();
    float proximoRefrescoProhibidas = -1f;
    bool cuerpoListo;
    bool cuerpoValido;
    Caja cuerpoCaja;

    // Caja que sale de tres esquinas: 'a' (origen), 'b' (a + eje u completo) y 'd' (a + eje v completo).
    static Caja CajaDeEsquinas(Vector2 a, Vector2 b, Vector2 d)
    {
        Vector2 eu = b - a, ev = d - a;
        Caja k;
        k.hu = eu.magnitude * 0.5f;
        k.hv = ev.magnitude * 0.5f;
        k.u = eu.sqrMagnitude > 1e-10f ? eu.normalized : Vector2.right;
        k.v = ev.sqrMagnitude > 1e-10f ? ev.normalized : Vector2.up;
        k.c = a + eu * 0.5f + ev * 0.5f;
        return k;
    }

    // La caja de una parcela (su Box Collider), en el plano local de este corral.
    bool CajaDeBoxCollider(BoxCollider bc, out Caja k)
    {
        Vector3 e = bc.size * 0.5f;
        Vector3 ctr = transform.InverseTransformPoint(bc.transform.TransformPoint(bc.center));
        Vector3 ux = transform.InverseTransformVector(bc.transform.TransformVector(new Vector3(e.x, 0f, 0f)));
        Vector3 uz = transform.InverseTransformVector(bc.transform.TransformVector(new Vector3(0f, 0f, e.z)));
        Vector2 c = new Vector2(ctr.x, ctr.z);
        Vector2 a = new Vector2(ux.x, ux.z);
        Vector2 b = new Vector2(uz.x, uz.z);
        k = CajaDeEsquinas(c - a - b, c + a - b, c - a + b);
        return a.sqrMagnitude + b.sqrMagnitude > 1e-10f;
    }

    // La huella del edificio de ESTE corral vista desde el plano local de 'marco' (otro corral).
    bool CajaEnMarco(Transform marco, out Caja k)
    {
        k = default(Caja);
        if (!huellaLista) CalcularHuella();
        if (!huellaValida) return false;
        Vector3 p0 = marco.InverseTransformPoint(transform.TransformPoint(new Vector3(huellaMin.x, 0f, huellaMin.y)));
        Vector3 p1 = marco.InverseTransformPoint(transform.TransformPoint(new Vector3(huellaMax.x, 0f, huellaMin.y)));
        Vector3 p2 = marco.InverseTransformPoint(transform.TransformPoint(new Vector3(huellaMin.x, 0f, huellaMax.y)));
        k = CajaDeEsquinas(new Vector2(p0.x, p0.z), new Vector2(p1.x, p1.z), new Vector2(p2.x, p2.z));
        return true;
    }

    // Gallinero: el cuerpo del edificio por detrás de la puerta. La rampa queda fuera para poder subir.
    bool CajaCuerpoRampa(out Caja k)
    {
        if (cuerpoListo) { k = cuerpoCaja; return cuerpoValido; }
        k = default(Caja);
        if (!PieRampaLocal(out Vector3 pie)) return false;     // (no se guarda: puede que aún no esté listo)
        cuerpoListo = true;
        cuerpoValido = false;

        Vector3 pu = PuertaRampaLocal();
        Vector2 a = new Vector2(pie.x, pie.z);
        Vector2 ab = new Vector2(pu.x, pu.z) - a;
        float largo = ab.magnitude;
        if (largo < 1e-4f) return false;
        Vector2 n = ab / largo;
        Vector2 m = new Vector2(-n.y, n.x);

        float sMax = float.MinValue, pMin = float.MaxValue, pMax = float.MinValue;
        bool alguno = false;
        foreach (var mf in GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.GetComponentInParent<AnimalVisual>() != null) continue;
            Transform h = mf.transform;
            while (h.parent != null && h.parent != transform) h = h.parent;
            if (!(h.name.StartsWith("Modelo") || h == modeloEdificio)) continue;

            Bounds b = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 e = b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 l = transform.InverseTransformPoint(mf.transform.TransformPoint(e));
                Vector2 q = new Vector2(l.x, l.z) - a;
                float s = Vector2.Dot(q, n), pp = Vector2.Dot(q, m);
                sMax = Mathf.Max(sMax, s);
                pMin = Mathf.Min(pMin, pp);
                pMax = Mathf.Max(pMax, pp);
                alguno = true;
            }
        }

        // Empieza un poco después de la puerta (el animal que llega a ella tiene el cuerpo medio fuera).
        float sMin = largo + tamanoAnimal * 0.6f;
        if (!alguno || sMax <= sMin) return false;

        cuerpoCaja.c = a + n * ((sMin + sMax) * 0.5f) + m * ((pMin + pMax) * 0.5f);
        cuerpoCaja.u = n;
        cuerpoCaja.v = m;
        cuerpoCaja.hu = (sMax - sMin) * 0.5f;
        cuerpoCaja.hv = (pMax - pMin) * 0.5f;
        cuerpoValido = true;
        k = cuerpoCaja;
        return true;
    }

    // Vuelve a reunir (cada segundo) lo que no se puede pisar: parcelas, edificios de otros corrales y el cuerpo
    // del gallinero.
    void RefrescarProhibidas()
    {
        if (Time.time < proximoRefrescoProhibidas) return;
        proximoRefrescoProhibidas = Time.time + 1f;
        prohibidas.Clear();

        foreach (var t in FindObjectsByType<Terreno>(FindObjectsSortMode.None))
        {
            var bc = t.GetComponent<BoxCollider>();
            if (bc != null && bc.enabled && CajaDeBoxCollider(bc, out Caja kt)) prohibidas.Add(kt);
        }
        foreach (var o in activos)
            if (o != null && o != this && o.isActiveAndEnabled && o.CajaEnMarco(transform, out Caja ko)) prohibidas.Add(ko);
        if (CajaCuerpoRampa(out Caja kc)) prohibidas.Add(kc);
    }

    // Saca el punto 'q' de la caja (ensanchada con el radio del animal) por el lado más cercano.
    static bool EmpujarFuera(Caja b, ref Vector2 q, float r)
    {
        Vector2 d = q - b.c;
        float a = Vector2.Dot(d, b.u), c = Vector2.Dot(d, b.v);
        float hu = b.hu + r, hv = b.hv + r;
        if (Mathf.Abs(a) >= hu || Mathf.Abs(c) >= hv) return false;
        if (hu - Mathf.Abs(a) < hv - Mathf.Abs(c)) a = a >= 0f ? hu : -hu;
        else c = c >= 0f ? hv : -hv;
        q = b.c + b.u * a + b.v * c;
        return true;
    }

    Vector3 FueraDeProhibidas(Vector3 p, float radio)
    {
        RefrescarProhibidas();
        if (prohibidas.Count == 0) return p;
        Vector2 q = new Vector2(p.x, p.z);
        if (!DentroDeAlguna(q, radio)) return p;

        // El punto libre más cercano en el borde de alguna zona. Sacarlo zona por zona fallaba cuando las zonas se
        // tocan (las 4 parcelas forman un bloque): lo pasaba de una a otra y el punto se quedaba dentro, así que
        // el animal iba hacia él y caminaba contra el borde sin llegar nunca.
        Vector2 mejor = q;
        float mejorD = float.MaxValue;
        const float sobra = 1e-4f;
        for (int i = 0; i < prohibidas.Count; i++)
        {
            Caja k = prohibidas[i];
            Vector2 d = q - k.c;
            float a = Vector2.Dot(d, k.u), c = Vector2.Dot(d, k.v);
            float hu = k.hu + radio + sobra, hv = k.hv + radio + sobra;
            float ca = Mathf.Clamp(a, -hu, hu), cc = Mathf.Clamp(c, -hv, hv);
            for (int s = 0; s < 4; s++)
            {
                float pa = s == 0 ? hu : s == 1 ? -hu : ca;
                float pc = s == 2 ? hv : s == 3 ? -hv : cc;
                Vector2 w = k.c + k.u * pa + k.v * pc;
                float dist = (w - q).sqrMagnitude;
                if (dist < mejorD && !DentroDeAlguna(w, radio)) { mejorD = dist; mejor = w; }
            }
        }
        if (mejorD < float.MaxValue)
        {
            p.x = mejor.x;
            p.z = mejor.y;
            return p;
        }

        for (int pasada = 0; pasada < 3; pasada++)
        {
            bool movido = false;
            for (int i = 0; i < prohibidas.Count; i++)
                if (EmpujarFuera(prohibidas[i], ref q, radio)) movido = true;
            if (!movido) break;
        }
        p.x = q.x;
        p.z = q.y;
        return p;
    }

    bool DentroDeAlguna(Vector2 q, float radio)
    {
        for (int i = 0; i < prohibidas.Count; i++)
        {
            Vector2 copia = q;
            if (EmpujarFuera(prohibidas[i], ref copia, radio)) return true;
        }
        return false;
    }

    // ¿Ese punto está dentro de una zona prohibida (con el radio del animal)?
    bool EnProhibida(Vector3 p, float radio)
    {
        if (LimitesRecinto(radio, out _, out _)) return false;
        RefrescarProhibidas();
        Vector2 q = new Vector2(p.x, p.z);
        for (int i = 0; i < prohibidas.Count; i++)
        {
            Vector2 copia = q;
            if (EmpujarFuera(prohibidas[i], ref copia, radio)) return true;
        }
        return false;
    }

    // ¿El segmento a-b atraviesa la caja? (ensanchada con el radio; un poco más pequeña para que rozar el borde
    // no cuente). Si a o b ya están dentro, no cuenta: de eso se encarga Limitar.
    static bool SegmentoCruza(Caja k, Vector3 a, Vector3 b, float r)
    {
        float hu = (k.hu + r) * 0.97f, hv = (k.hv + r) * 0.97f;
        Vector2 pa = new Vector2(a.x, a.z) - k.c, pb = new Vector2(b.x, b.z) - k.c;
        float au = Vector2.Dot(pa, k.u), av = Vector2.Dot(pa, k.v);
        float bu = Vector2.Dot(pb, k.u), bv = Vector2.Dot(pb, k.v);
        if (Mathf.Abs(au) < hu && Mathf.Abs(av) < hv) return false;
        if (Mathf.Abs(bu) < hu && Mathf.Abs(bv) < hv) return false;
        float t0 = 0f, t1 = 1f;
        if (!Losa(au, bu - au, hu, ref t0, ref t1)) return false;
        if (!Losa(av, bv - av, hv, ref t0, ref t1)) return false;
        return true;
    }

    static bool Losa(float o, float d, float h, ref float t0, ref float t1)
    {
        if (Mathf.Abs(d) < 1e-7f) return Mathf.Abs(o) < h;
        float ta = (-h - o) / d, tb = (h - o) / d;
        if (ta > tb) { float x = ta; ta = tb; tb = x; }
        t0 = Mathf.Max(t0, ta);
        t1 = Mathf.Min(t1, tb);
        return t0 < t1;
    }

    // Esquinas (algo separadas del borde) por las que rodear la caja yendo de a a b: una o dos, la opción más corta.
    int EsquinasDeRodeo(Caja k, Vector3 a, Vector3 b, float r, out Vector3 e1, out Vector3 e2)
    {
        float hu = (k.hu + r) * 1.05f, hv = (k.hv + r) * 1.05f;
        float y = (a.y + b.y) * 0.5f;
        var esq = new Vector3[4];
        Vector2[] signos = { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(-1f, -1f), new Vector2(1f, -1f) };
        for (int i = 0; i < 4; i++)
        {
            Vector2 w = k.c + k.u * (signos[i].x * hu) + k.v * (signos[i].y * hv);
            Vector3 c = FueraDeProhibidas(new Vector3(w.x, y, w.y), r);
            esq[i] = c;
        }

        e1 = e2 = Vector3.zero;
        int mejorN = 0;
        float mejor = float.MaxValue;

        for (int i = 0; i < 4; i++)
        {
            if (!SegmentoCruza(k, a, esq[i], r) && !SegmentoCruza(k, esq[i], b, r))
            {
                float l = Dist2D(a, esq[i]) + Dist2D(esq[i], b);
                if (l < mejor) { mejor = l; mejorN = 1; e1 = esq[i]; }
            }
        }
        if (mejorN == 1) return 1;

        for (int i = 0; i < 4; i++)
        {
            for (int d = -1; d <= 1; d += 2)
            {
                int j = (i + d + 4) % 4;
                if (SegmentoCruza(k, a, esq[i], r) || SegmentoCruza(k, esq[j], b, r)) continue;
                float l = Dist2D(a, esq[i]) + Dist2D(esq[i], esq[j]) + Dist2D(esq[j], b);
                if (l < mejor) { mejor = l; mejorN = 2; e1 = esq[i]; e2 = esq[j]; }
            }
        }
        return mejorN;
    }

    static float Dist2D(Vector3 a, Vector3 b)
    {
        return new Vector2(a.x - b.x, a.z - b.z).magnitude;
    }

    // Añade a 'res' el camino de a hasta b rodeando las zonas prohibidas (termina en b). En un corral cerrado,
    // o sin zonas, solo añade b.
    public void CaminoLibre(Vector3 a, Vector3 b, float radio, List<Vector3> res)
    {
        if (LimitesRecinto(radio, out _, out _)) { res.Add(b); return; }
        RefrescarProhibidas();
        if (!CruzaAlguna(a, b, radio)) { res.Add(b); return; }

        // Camino más corto entre esquinas de las zonas (grafo de visibilidad). Rodear caja por caja fallaba con
        // parcelas juntas: la esquina caía dentro de la parcela vecina, no había rodeo y el animal seguía de frente
        // contra el borde.
        if (RodeoPorEsquinas(a, b, radio, res)) return;
        Segmento(a, b, radio, res, 0);   // (sin camino posible: el rodeo de antes, caja por caja)
    }

    bool CruzaAlguna(Vector3 a, Vector3 b, float r)
    {
        for (int i = 0; i < prohibidas.Count; i++)
            if (SegmentoCruza(prohibidas[i], a, b, r)) return true;
        return false;
    }

    readonly List<Vector3> nodos = new List<Vector3>();
    float[] costeNodo = new float[0];
    int[] previoNodo = new int[0];
    bool[] cerradoNodo = new bool[0];

    // Dijkstra desde 'a' (nodo 0) hasta 'b' (nodo 1) pasando por las esquinas (algo separadas) de todas las
    // zonas que no caen dentro de otra. Añade a 'res' las esquinas del camino y 'b'. False si no hay camino.
    bool RodeoPorEsquinas(Vector3 a, Vector3 b, float r, List<Vector3> res)
    {
        nodos.Clear();
        nodos.Add(a);
        nodos.Add(b);
        float y = (a.y + b.y) * 0.5f;
        for (int i = 0; i < prohibidas.Count; i++)
        {
            Caja k = prohibidas[i];
            float hu = (k.hu + r) * 1.08f, hv = (k.hv + r) * 1.08f;
            for (int s = 0; s < 4; s++)
            {
                float su = (s == 0 || s == 3) ? 1f : -1f, sv = s < 2 ? 1f : -1f;
                Vector2 w = k.c + k.u * (su * hu) + k.v * (sv * hv);
                Vector3 e = new Vector3(w.x, y, w.y);
                if (!EnProhibida(e, r)) nodos.Add(e);
            }
        }

        int n = nodos.Count;
        if (costeNodo.Length < n)
        {
            costeNodo = new float[n * 2];
            previoNodo = new int[n * 2];
            cerradoNodo = new bool[n * 2];
        }
        for (int i = 0; i < n; i++) { costeNodo[i] = float.MaxValue; previoNodo[i] = -1; cerradoNodo[i] = false; }
        costeNodo[0] = 0f;

        while (true)
        {
            int u = -1;
            float mejor = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (!cerradoNodo[i] && costeNodo[i] < mejor) { mejor = costeNodo[i]; u = i; }
            if (u < 0) return false;
            if (u == 1) break;
            cerradoNodo[u] = true;
            for (int v = 0; v < n; v++)
            {
                if (cerradoNodo[v] || v == u) continue;
                float c = mejor + Dist2D(nodos[u], nodos[v]);
                if (c >= costeNodo[v]) continue;
                if (CruzaAlguna(nodos[u], nodos[v], r)) continue;
                costeNodo[v] = c;
                previoNodo[v] = u;
            }
        }

        int inicio = res.Count;
        for (int i = 1; i > 0; i = previoNodo[i]) res.Add(nodos[i]);
        res.Reverse(inicio, res.Count - inicio);
        return true;
    }

    void Segmento(Vector3 a, Vector3 b, float r, List<Vector3> res, int prof)
    {
        if (prof < 4)
        {
            for (int i = 0; i < prohibidas.Count; i++)
            {
                if (!SegmentoCruza(prohibidas[i], a, b, r)) continue;
                int n = EsquinasDeRodeo(prohibidas[i], a, b, r, out Vector3 e1, out Vector3 e2);
                if (n == 0) break;
                Segmento(a, e1, r, res, prof + 1);
                if (n == 2)
                {
                    Segmento(e1, e2, r, res, prof + 1);
                    Segmento(e2, b, r, res, prof + 1);
                }
                else Segmento(e1, b, r, res, prof + 1);
                return;
            }
        }
        res.Add(b);
    }

    // ¿El camino recto de a a b atraviesa alguna zona prohibida? (Lo usa el animal mientras camina: si los
    // demás lo apartaron de su camino y ahora tiene una parcela delante, rehace el rodeo desde donde está.)
    public bool CaminoBloqueado(Vector3 a, Vector3 b, float radio)
    {
        if (LimitesRecinto(radio, out _, out _)) return false;
        RefrescarProhibidas();
        for (int i = 0; i < prohibidas.Count; i++)
            if (SegmentoCruza(prohibidas[i], a, b, radio)) return true;
        return false;
    }

    // Quita a 'vel' (en el plano) la parte que empuja contra una parcela, un edificio o la pared del recinto
    // cuando el animal ya está pegado a ella: así se desliza por el borde en vez de caminar contra él, y los
    // empujones de los demás no lo aplastan contra la pared (lo que lo hacía temblar).
    public Vector3 Deslizar(Vector3 p, Vector3 vel, float radio)
    {
        float margen = tamanoAnimal * 0.1f;
        if (LimitesRecinto(radio, out Vector2 mn, out Vector2 mx))
        {
            if (p.x <= mn.x + margen && vel.x < 0f) vel.x = 0f;
            if (p.x >= mx.x - margen && vel.x > 0f) vel.x = 0f;
            if (p.z <= mn.y + margen && vel.z < 0f) vel.z = 0f;
            if (p.z >= mx.y - margen && vel.z > 0f) vel.z = 0f;
            return vel;
        }
        RefrescarProhibidas();
        Vector2 q = new Vector2(p.x, p.z);
        Vector2 v2 = new Vector2(vel.x, vel.z);
        for (int i = 0; i < prohibidas.Count; i++)
        {
            if (!NormalCercana(prohibidas[i], q, radio, margen, out Vector2 n)) continue;
            float hacia = Vector2.Dot(v2, n);
            if (hacia < 0f) v2 -= n * hacia;
        }
        vel.x = v2.x;
        vel.z = v2.y;
        return vel;
    }

    // Si 'q' está pegado a la caja (a menos de 'margen' del borde ensanchado con el radio, o dentro), devuelve
    // la normal hacia fuera de la cara más cercana.
    static bool NormalCercana(Caja k, Vector2 q, float r, float margen, out Vector2 n)
    {
        n = Vector2.zero;
        Vector2 d = q - k.c;
        float a = Vector2.Dot(d, k.u), c = Vector2.Dot(d, k.v);
        float fa = Mathf.Abs(a) - (k.hu + r), fc = Mathf.Abs(c) - (k.hv + r);   // cuánto queda por fuera de cada par de caras
        if (fa >= margen || fc >= margen) return false;
        n = fa > fc ? k.u * (a >= 0f ? 1f : -1f) : k.v * (c >= 0f ? 1f : -1f);
        return true;
    }

    // ---------- Vuelta grande (gallinero) ----------

    // Puntos de la vuelta grande en local del corral (sin los que falten).
    bool PuntosVuelta(List<Vector3> pts)
    {
        pts.Clear();
        if (rutaAmplia == null) return false;
        foreach (var t in rutaAmplia)
        {
            if (t == null) continue;
            Vector3 l = transform.InverseTransformPoint(t.position);
            l.y += alturaSueloExtra;
            pts.Add(l);
        }
        return pts.Count >= 3;
    }

    static int MasCercano(List<Vector3> pts, Vector3 p)
    {
        int mejor = 0;
        float md = float.MaxValue;
        for (int i = 0; i < pts.Count; i++)
        {
            float d = new Vector2(pts[i].x - p.x, pts[i].z - p.z).sqrMagnitude;
            if (d < md) { md = d; mejor = i; }
        }
        return mejor;
    }

    // Añade a 'salida' el camino por la vuelta grande desde donde está 'v' (o desde el último punto de 'salida')
    // hasta el punto 'destino' de la vuelta (índice). Va por los puntos intermedios para rodear lo que haya.
    void CaminoPorVuelta(AnimalVisual v, List<Vector3> pts, List<Vector3> salida, int destino, bool desvio)
    {
        int n = pts.Count;
        Vector3 yo = salida.Count > 0 ? salida[salida.Count - 1] : v.transform.localPosition;
        int i = MasCercano(pts, yo);
        salida.Add(pts[i]);

        int adelante = (destino - i + n) % n;
        int paso = adelante <= n - adelante ? 1 : -1;
        if (desvio && Random.value < 0.25f) paso = -paso;   // de vez en cuando da la vuelta por el otro lado

        int guardia = n;
        while (i != destino && guardia-- > 0)
        {
            i = (i + paso + n) % n;
            salida.Add(pts[i]);
        }
    }

    // Un punto cualquiera por donde pasear (local del corral).
    Vector3 PuntoPaseoAleatorio()
    {
        if (especie == Animal.Gallina && TieneRuta())
        {
            var ruta = gallinaPrincipal.Ruta;
            Transform t = null;
            for (int i = 0; i < 8 && t == null; i++) t = ruta[Random.Range(0, ruta.Length)];
            if (t != null)
            {
                Vector3 l = transform.InverseTransformPoint(t.position);
                Vector2 c = Random.insideUnitCircle * (tamanoAnimal * 1.2f);
                return new Vector3(l.x + c.x, l.y + alturaSueloExtra, l.z + c.y);
            }
        }

        // Corral cerrado: pasean por todo el interior (dentro de las paredes), no solo por los puestos.
        if (LimitesRecinto(tamanoAnimal * 0.4f, out Vector2 rmin, out Vector2 rmax))
        {
            float yr = ZonaPuestos(out _, out _, out float ys) ? ys : 0f;
            return new Vector3(Mathf.Lerp(rmin.x, rmax.x, Random.value), yr + alturaSueloExtra,
                               Mathf.Lerp(rmin.y, rmax.y, Random.value));
        }

        if (ZonaPuestos(out Vector3 min, out Vector3 max, out float y))
        {
            float margen = tamanoAnimal * 0.5f;
            float ancho = Mathf.Max(0f, (max.x - min.x) - 2f * margen);
            float fondo = Mathf.Max(0f, (max.z - min.z) - 2f * margen);
            return new Vector3(min.x + margen + Random.value * ancho, y + alturaSueloExtra,
                               min.z + margen + Random.value * fondo);
        }

        Vector2 q = Random.insideUnitCircle * 0.08f;
        return new Vector3(q.x, alturaSueloExtra, q.y);
    }

    // Un punto de paseo donde no haya nadie cerca (ni yendo hacia allí).
    Vector3 ElegirPuntoLibre(AnimalVisual v)
    {
        Vector3 candidato = PuntoPaseoAleatorio();
        for (int i = 0; i < 8; i++)
        {
            if (EstaLibre(candidato, v)) return candidato;
            candidato = PuntoPaseoAleatorio();
        }
        return candidato;
    }

    bool EstaLibre(Vector3 p, AnimalVisual yo)
    {
        float minimo = tamanoAnimal * 1.1f;
        if (EnProhibida(p, tamanoAnimal * 0.5f)) return false;     // no se pasea por encima de parcelas ni edificios
        foreach (var o in animales)
        {
            if (o == null || o == yo) continue;
            Vector3 q = o.DestinoFinal;
            if (new Vector2(p.x - q.x, p.z - q.z).magnitude < minimo) return false;
            q = o.transform.localPosition;
            if (new Vector2(p.x - q.x, p.z - q.z).magnitude < minimo) return false;
        }
        if (PosicionPrincipalLocal(out Vector3 g) && new Vector2(p.x - g.x, p.z - g.z).magnitude < minimo) return false;
        return true;
    }

    bool PosicionPrincipalLocal(out Vector3 pos)
    {
        pos = Vector3.zero;
        if (gallinaPrincipal == null || !gallinaPrincipal.EnGallinero || !gallinaPrincipal.EsAdulta) return false;
        pos = transform.InverseTransformPoint(gallinaPrincipal.transform.position);
        return true;
    }

    // Lo llama AnimalVisual cuando está libre: dónde ir ahora (puntos locales). Devuelve false si se queda quieto.
    public bool PlanPaseo(AnimalVisual v, List<Vector3> salida, out bool terminaEnRampa)
    {
        terminaEnRampa = false;
        bool hayRampa = PieRampaLocal(out Vector3 pie);
        bool hayVuelta = especie == Animal.Gallina && PuntosVuelta(vuelta);

        // A veces un adulto sube la rampa hasta la puerta y se queda un rato (solo uno a la vez).
        if (hayRampa && !v.EnRampa && !v.Cria && Random.value < probabilidadRampa && !AlguienEnRampa(v))
        {
            // Si anda lejos (vuelta grande), vuelve primero junto al gallinero por los puntos de la vuelta.
            if (hayVuelta && new Vector2(v.transform.localPosition.x - pie.x, v.transform.localPosition.z - pie.z).magnitude > tamanoAnimal * 4f)
                CaminoPorVuelta(v, vuelta, salida, MasCercano(vuelta, pie), false);
            salida.Add(pie);
            salida.Add(PuertaRampaLocal());
            terminaEnRampa = true;
            return true;
        }

        if (v.EnRampa && hayRampa) salida.Add(pie);   // primero baja por la rampa

        // A veces (adultos) hacen la vuelta grande: pasan por los puntos de la ruta hasta uno al azar.
        if (hayVuelta && !v.Cria && Random.value < probabilidadVuelta)
        {
            CaminoPorVuelta(v, vuelta, salida, Random.Range(0, vuelta.Count), true);
            Vector2 c = Random.insideUnitCircle * (tamanoAnimal * 0.8f);
            Vector3 ultimo = salida[salida.Count - 1];
            salida[salida.Count - 1] = new Vector3(ultimo.x + c.x, ultimo.y, ultimo.z + c.y);
            return true;
        }

        salida.Add(ElegirPuntoLibre(v));
        return true;
    }

    readonly List<Vector3> vuelta = new List<Vector3>();

    bool AlguienEnRampa(AnimalVisual yo)
    {
        foreach (var o in animales) if (o != null && o != yo && o.EnRampa) return true;
        return gallinaPrincipal != null && gallinaPrincipal.Arriba;
    }

    // ¿Ese cuerpo (animal o gallina del tutorial) está en la rampa?
    public bool EstaArriba(Transform t)
    {
        if (t == null) return false;
        var v = t.GetComponent<AnimalVisual>();
        if (v != null) return v.EnRampa;
        var g = t.GetComponent<Gallina>();
        return g != null && g.Arriba;
    }

    // Cuánto se debe apartar 'v' (en el plano) para no pisar a los demás estando en 'p' (local del corral).
    public Vector3 Empuje(AnimalVisual v, Vector3 p)
    {
        Vector3 total = Vector3.zero;
        foreach (var o in animales)
        {
            if (o == null || o == v) continue;
            Vector3 q = o.transform.localPosition;
            Vector3 d = new Vector3(p.x - q.x, 0f, p.z - q.z);
            float minimo = v.Radio + o.Radio;
            float dist = d.magnitude;
            if (dist >= minimo) continue;

            Vector3 dir = dist > 1e-4f ? d / dist : new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            // Si el otro no se mueve (come o espera pareja) me aparto yo entero; si no, cada uno la mitad.
            float parte = (o.Comiendo || o.Reservado || !o.Moviendo) ? 1f : 0.5f;
            total += dir * ((minimo - dist) * parte);
        }

        if (PosicionPrincipalLocal(out Vector3 g))
        {
            Vector3 d = new Vector3(p.x - g.x, 0f, p.z - g.z);
            float minimo = v.Radio + tamanoAnimal * 0.4f;
            float dist = d.magnitude;
            if (dist < minimo && dist > 1e-4f) total += d / dist * (minimo - dist);
        }
        return total;
    }

    // Hacia dónde debe girar 'v' (en el plano) al ir en la dirección 'dir' (unitaria) para rodear a quien se le
    // cruza: cada uno se aparta por su derecha, así dos animales de frente se esquivan sin quedarse bloqueados.
    // 'falta' es lo que le queda hasta su meta (no esquiva a quien está más allá).
    public Vector3 Esquivar(AnimalVisual v, Vector3 p, Vector3 dir, float falta)
    {
        Vector3 derecha = new Vector3(dir.z, 0f, -dir.x);
        Vector3 giro = Vector3.zero;
        float alcance = tamanoAnimal * 3f;

        // El lado por el que rodea a quien tiene de frente se mantiene mientras dure el cruce. Antes se decidía en
        // cada fotograma y, con el otro casi en línea, cambiaba de lado una y otra vez: se veía tambalearse.
        if (Time.time > v.FinEsquive) v.LadoEsquive = 0;
        bool alguno = false;

        foreach (var o in animales)
        {
            if (o == null || o == v || o.transform == v.Padre) continue;
            giro += Obstaculo(v, o.Radio, o.transform.localPosition, p, dir, derecha, alcance, falta, ref alguno);
        }
        if (PosicionPrincipalLocal(out Vector3 g) && (gallinaPrincipal == null || gallinaPrincipal.transform != v.Padre))
            giro += Obstaculo(v, tamanoAnimal * 0.4f, g, p, dir, derecha, alcance, falta, ref alguno);

        if (alguno) v.FinEsquive = Time.time + 0.6f;
        if (giro.sqrMagnitude < 1e-8f) return dir;
        return (dir + giro * 1.6f).normalized;
    }

    // Cuánto y hacia qué lado debe girar 'v' por un cuerpo de radio 'ro' en 'q'. La fuerza crece de forma
    // continua (0 en los bordes de la zona que mira), para que el giro no pegue tirones al entrar o salir.
    Vector3 Obstaculo(AnimalVisual v, float ro, Vector3 q, Vector3 p, Vector3 dir, Vector3 derecha, float alcance,
                      float falta, ref bool alguno)
    {
        Vector3 r = new Vector3(q.x - p.x, 0f, q.z - p.z);
        float hace = v.Radio + ro + tamanoAnimal * 0.15f;     // distancia mínima entre centros para no rozarse
        float delante = Vector3.Dot(r, dir);
        float atras = hace * 0.3f;
        if (delante <= -atras || delante >= alcance + hace || delante >= falta + hace) return Vector3.zero;

        float lado = Vector3.Dot(r, derecha);
        if (Mathf.Abs(lado) >= hace) return Vector3.zero;
        alguno = true;

        float cerca = 1f - Mathf.Clamp01(delante / Mathf.Min(alcance + hace, falta + hace));
        float centrado = 1f - Mathf.Abs(lado) / hace;
        float fuerza = Mathf.Sqrt(Mathf.Clamp01(cerca * 1.5f)) * Mathf.Sqrt(centrado);
        if (delante < 0f) fuerza *= 1f + delante / atras;   // ya lo está dejando atrás: se desvanece

        // Claramente a un lado: me aparto hacia el otro. Casi de frente: el lado que eligió al empezar a esquivar
        // (por defecto la derecha; así dos que vienen de frente se cruzan sin bloquearse).
        float s;
        if (Mathf.Abs(lado) > hace * 0.45f) s = lado > 0f ? -1f : 1f;
        else
        {
            if (v.LadoEsquive == 0) v.LadoEsquive = lado > hace * 0.15f ? -1 : 1;
            s = v.LadoEsquive;
        }
        return derecha * (s * fuerza);
    }

    // Dónde se coloca una cría que sigue a 'padre': detrás de él, repartida con las demás crías que lo siguen
    // (de dos en dos, cada fila más atrás). 'k' = su orden entre ellas.
    public Vector3 PuestoDeSeguidor(AnimalVisual cria, Transform padre, out int k)
    {
        k = 0;
        int id = cria.GetInstanceID();
        foreach (var a in animales)
            if (a != null && a != cria && a.Cria && a.Padre == padre && a.GetInstanceID() < id) k++;

        Vector3 pp = transform.InverseTransformPoint(padre.position);
        Vector3 fwd = transform.InverseTransformDirection(padre.forward);
        fwd.y = 0f;
        fwd = fwd.sqrMagnitude < 1e-6f ? Vector3.forward : fwd.normalized;
        Vector3 der = new Vector3(fwd.z, 0f, -fwd.x);

        float atras = tamanoAnimal * (1.1f + 0.6f * (k / 2));
        float lado = (k % 2 == 0 ? 1f : -1f) * tamanoAnimal * 0.45f;
        Vector3 s = pp - fwd * atras + der * lado;
        s.y = pp.y;
        return Limitar(s, cria.Radio);
    }

    // A quién sigue una cría mientras crece: una gallina (gallinero) o cualquier adulto.
    public Transform ElegirPadre(AnimalVisual cria)
    {
        var candidatos = new List<Transform>();
        foreach (var a in animales)
        {
            if (a == null || a == cria || a.Cria) continue;
            if (especie == Animal.Gallina && a.Tipo != Animal.Gallina) continue;
            candidatos.Add(a.transform);
        }
        if (especie == Animal.Gallina && gallinaPrincipal != null && gallinaPrincipal.EnGallinero && gallinaPrincipal.EsAdulta)
            candidatos.Add(gallinaPrincipal.transform);

        if (candidatos.Count == 0)
        {
            // Sin gallinas: cualquier adulto sirve.
            foreach (var a in animales) if (a != null && a != cria && !a.Cria) candidatos.Add(a.transform);
        }
        return candidatos.Count == 0 ? null : candidatos[Random.Range(0, candidatos.Count)];
    }

    // ---------- Crear animales ----------

    // Variante al azar de un animal nuevo (0 = normal).
    int ElegirVariante(Animal tipo)
    {
        if (Random.value >= probabilidadVariante) return 0;
        if (tipo == Animal.Gallina) return modeloGallina2 != null ? 1 : 0;
        if (tipo == Animal.Cuy) return 1;
        return 0;
    }

    AnimalVisual CrearAnimal(Animal tipo, bool cria, bool nacido, int puesto, Vector3? posicionMundo = null,
                             float crecido = 0f, int raciones = 0, int variante = 0)
    {
        GameObject prefab;
        bool modeloAparte = false;
        bool sinCorreccion = false;
        if (cria && modeloCria != null) { prefab = modeloCria; modeloAparte = true; sinCorreccion = true; }
        else if (tipo == Animal.Gallo && modeloGallo != null) { prefab = modeloGallo; variante = 0; }
        else if (tipo == Animal.Gallina && variante == 1 && modeloGallina2 != null)
        {
            prefab = modeloGallina2;
            sinCorreccion = true;     // gallina2 ya mira hacia delante
        }
        else
        {
            prefab = modeloAnimal;
            if (tipo == Animal.Gallina) variante = 0;
        }

        if (prefab == null)
        {
            Debug.LogWarning("Corral '" + name + "': falta asignar el Modelo Animal.", this);
            return null;
        }
        if (puesto < 0) puesto = PuestoLibre();
        if (puesto < 0) return null;

        // La cría con modelo propio nace pequeña; la que comparte modelo con el adulto crece desde EscalaCria.
        float tam = modeloAparte ? tamanoAnimal * AnimalDatos.EscalaCria : tamanoAnimal;
        if (tipo == Animal.Gallo && !cria) tam *= escalaGallo;
        float escalaIni = 1f, escalaFin = 1f;
        if (cria) { if (modeloAparte) escalaFin = 1.35f; else escalaIni = AnimalDatos.EscalaCria; }

        var correccion = Quaternion.Euler(rotacionExtra) * AnimalDatos.Correccion(tipo, sinCorreccion);
        string nombre = AnimalDatos.Titulo(tipo) + (cria ? "Cria" : "") + (++contadorNombres);
        var contenedor = ModeloUtil.CrearNormalizado(prefab, transform, tam, nombre, correccion);
        contenedor.position = posicionMundo ?? PosicionPuesto(puesto);
        contenedor.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        if (tipo == Animal.Cuy && variante == 1) VarianteVisual.Colorear(contenedor, colorVariante);

        var colision = contenedor.gameObject.AddComponent<SphereCollider>();
        colision.radius = Mathf.Max(tam * 0.7f, tamanoAnimal * 0.45f);
        colision.center = new Vector3(0f, tam * 0.4f, 0f);

        var visual = contenedor.gameObject.AddComponent<AnimalVisual>();
        Interactuable.Crear(contenedor.gameObject, Interactuable.Tipo.Animal);
        visual.Nacido = nacido;
        visual.Puesto = puesto;
        visual.Variante = variante;
        visual.Cargar(cria, crecido, raciones);
        visual.Iniciar(this, tipo, AnimalDatos.SegundosComer(especie), modeloAparte, tam, escalaIni, escalaFin);
        animales.Add(visual);
        return visual;
    }

    // ---------- Animales para los pedidos de la caja ----------

    // Todos los adultos de ese tipo, estén haciendo lo que estén haciendo, en el orden en que salen: se sacan desde
    // el final, así que la gallina del tutorial (que va primero) es la última en irse. La gallina del tutorial es
    // un animal más: aquí no hay nada propio de ella. No cuenta lo que está dentro de la caja de pedidos.
    List<IAnimalVendible> AdultosDe(Animal tipo)
    {
        var lista = new List<IAnimalVendible>();
        if (gallinaPrincipal != null) lista.Add(gallinaPrincipal);
        foreach (var v in animales) if (v != null) lista.Add(v);
        lista.RemoveAll(a => !a.EnJuego || !a.EsAdulto || a.TipoAnimal != tipo);
        return lista;
    }

    public int Adultos(Animal tipo)
    {
        return AdultosDe(tipo).Count;
    }

    // Saca un adulto del corral sin importar lo que esté haciendo (comer, emparejarse, subir la rampa...).
    // definitivo = se vende; si no, va a la caja de pedidos y 'datos' sirve para devolverlo igual.
    bool SacarAdulto(Animal tipo, bool definitivo, out DatosAnimal datos)
    {
        datos = null;
        var lista = AdultosDe(tipo);
        if (lista.Count == 0) return false;

        var elegido = lista[lista.Count - 1];

        // Las crías que lo seguían se buscan otro padre.
        Transform cuerpo = elegido.Cuerpo;
        foreach (var a in animales) if (a != null && a.Padre == cuerpo) a.Padre = null;

        datos = elegido.Sacar(definitivo);
        return true;
    }

    // Saca un adulto para ponerlo en la caja de pedidos y devuelve sus datos (para devolverlo igual).
    public bool TomarAdulto(Animal tipo, out DatosAnimal datos)
    {
        return SacarAdulto(tipo, false, out datos);
    }

    // Los adultos de ese tipo que se pueden vender, del más gordo al más flaco, con el mismo nombre que en la
    // ventana de alimentar. La gallina del tutorial es una más. No cuenta lo que está dentro de la caja de pedidos.
    public List<AnimalEnVenta> AdultosEnVenta(Animal tipo)
    {
        var salida = new List<AnimalEnVenta>();
        int orden = 0;

        if (tipo == Animal.Gallina && gallinaPrincipal != null && gallinaPrincipal.EnJuego && gallinaPrincipal.EsAdulto)
            salida.Add(NuevoEnVenta(gallinaPrincipal, "Gallina (la primera)", orden++));

        // Misma numeración que Listar().
        int nGallina = Principal, nGallo = 0, nOtro = 0;
        foreach (var v in animales)
        {
            if (v == null || v.Cria) continue;
            string nombre;
            if (v.Tipo == Animal.Gallina) nombre = "Gallina " + (++nGallina);
            else if (v.Tipo == Animal.Gallo) nombre = "Gallo " + (++nGallo);
            else nombre = AnimalDatos.Titulo(v.Tipo) + " " + (++nOtro);
            if (v.Tipo == tipo && v.EnJuego) salida.Add(NuevoEnVenta(v, nombre, orden++));
        }

        salida.Sort((x, y) => x.raciones != y.raciones ? y.raciones.CompareTo(x.raciones) : x.orden.CompareTo(y.orden));
        return salida;
    }

    AnimalEnVenta NuevoEnVenta(IAnimalVendible a, string nombre, int orden)
    {
        var objeto = a as Object;
        return new AnimalEnVenta
        {
            animal = a,
            id = objeto != null ? objeto.GetInstanceID() : 0,
            orden = orden,
            nombre = nombre,
            raciones = a.Raciones,
            valor = AnimalDatos.Valor(a.TipoAnimal, a.Raciones),
            engorda = AnimalDatos.Engorda(a.TipoAnimal)
        };
    }

    // Vende a ese animal en concreto (el que el jugador eligió en la tienda), sin importar lo que estuviera
    // haciendo (comer, emparejarse, la rampa...). Devuelve lo que vale según su engorde, o 0 si ya no se puede
    // vender. Las monedas las cobra EstadoJuego.
    public int VenderAnimal(IAnimalVendible elegido)
    {
        if (elegido == null || !elegido.EnJuego || !elegido.EsAdulto) return 0;

        bool esMio = ReferenceEquals(elegido, gallinaPrincipal);
        if (!esMio)
        {
            var visual = elegido as AnimalVisual;
            esMio = visual != null && animales.Contains(visual);
        }
        if (!esMio) return 0;

        int valor = AnimalDatos.Valor(elegido.TipoAnimal, elegido.Raciones);

        // Las crías que lo seguían se buscan otro padre.
        Transform cuerpo = elegido.Cuerpo;
        foreach (var a in animales) if (a != null && a.Padre == cuerpo) a.Padre = null;

        elegido.Sacar(true);
        return valor;
    }

    // Lo llama AnimalVisual.Sacar: el animal deja el corral y se destruye, cortando lo que hiciera
    // (si estaba emparejándose, la pareja se cancela y su compañero queda libre).
    public void Quitar(AnimalVisual v)
    {
        animales.Remove(v);
        v.Detener();
        v.enabled = false;
        Destroy(v.gameObject);
    }

    // Huevos que se quedan esperando en el gallinero (los que dejó una gallina que ya no está).
    public void SumarProduccion(int n)
    {
        if (n <= 0) return;
        produccion += n;
        ActualizarIcono();
    }

    // El pedido entregó este animal: ya no vuelve al corral.
    public void Despedir(DatosAnimal datos)
    {
        if (datos != null && datos.principal && gallinaPrincipal != null) gallinaPrincipal.Vender();
    }

    // Devuelve al corral un animal que estaba en la caja. False si ya no hay un puesto libre.
    public bool DevolverAdulto(Animal tipo, DatosAnimal datos)
    {
        if (datos == null) return false;

        // La gallina del tutorial vuelve a su sitio sin ocupar una plaza de las demás.
        if (datos.principal)
        {
            if (gallinaPrincipal == null) return false;
            gallinaPrincipal.VolverDeCaja();
            return true;
        }

        if (PuestoLibre() < 0) return false;
        var v = CrearAnimal(tipo, false, datos.nacido, -1, null, 0f, datos.raciones, datos.variante);
        if (v == null) return false;
        v.Aparecer();
        return true;
    }

    // Modelo, giro de corrección y tamaño (en el mundo) de un adulto, para dibujarlo dentro de la caja.
    public bool ModeloAdulto(Animal tipo, int variante, int raciones,
                             out GameObject prefab, out Quaternion correccion, out float tamanoMundo)
    {
        bool sinCorreccion = false;
        if (tipo == Animal.Gallo && modeloGallo != null) prefab = modeloGallo;
        else if (tipo == Animal.Gallina && variante == 1 && modeloGallina2 != null)
        {
            prefab = modeloGallina2;
            sinCorreccion = true;
        }
        else prefab = modeloAnimal;

        float tam = tamanoAnimal;
        if (tipo == Animal.Gallo) tam *= escalaGallo;
        // La caja se dibuja como antes: las aves no se agrandan dentro de ella (solo cuy y cerdo).
        if (AnimalDatos.Engorda(tipo) && tipo != Animal.Gallina && tipo != Animal.Gallo)
            tam *= AnimalDatos.FactorEngorde(raciones);
        tamanoMundo = tam * transform.lossyScale.x;
        correccion = Quaternion.Euler(rotacionExtra) * AnimalDatos.Correccion(tipo, sinCorreccion);
        return prefab != null;
    }

    // Pinta de mostaza al cuy de la variante (lo mismo que hace CrearAnimal).
    public void AplicarVariante(Transform contenedor, Animal tipo, int variante)
    {
        if (tipo == Animal.Cuy && variante == 1) VarianteVisual.Colorear(contenedor, colorVariante);
    }

    // ---------- Comprar ----------

    public static bool PuedeComprar(Animal a, out string motivo)
    {
        motivo = "";
        var mision = MisionController.Instance;
        if (mision == null || !mision.SolarLibre)
        {
            motivo = "Termina la misión del pollito y retira el heno primero.";
            return false;
        }

        var corral = De(a);
        if (corral == null)
        {
            if (a == Animal.Cuy) motivo = "Necesitas comprar el criadero de cuy.";
            else if (a == Animal.Cerdo) motivo = "Necesitas comprar el corral de cerdos (pestaña Edificios).";
            else motivo = "Aún no tienes dónde criar " + AnimalDatos.Plural(a) + ".";
            return false;
        }
        if (!corral.TieneModelo || (a == Animal.Gallo && !corral.TieneModeloGallo))
        {
            motivo = "Al corral le falta el modelo del animal.";
            return false;
        }
        if (a == Animal.Gallo && corral.Gallos >= AnimalDatos.MaxGallos)
        {
            motivo = "Ya tienes el máximo de " + AnimalDatos.MaxGallos + " gallos.";
            return false;
        }
        if (AnimalDatos.CorralGrande(a) && corral.PlazasLibres <= 0)
        {
            motivo = "El corral ya tiene sus " + (corral.Capacidad + corral.MaxCrias) + " animales (comprados y crías).";
            return false;
        }
        if (!corral.HayLugarCompra)
        {
            motivo = "Ya compraste el máximo de este corral (" + corral.Capacidad + " animales).";
            return false;
        }
        if (corral.PuestoLibre() < 0 || corral.PlazasLibres <= 0)
        {
            motivo = "El corral está lleno: las crías y los animales de la caja de pedidos ocupan las plazas libres.";
            return false;
        }
        return true;
    }

    public static bool Comprar(Animal a, out string mensaje)
    {
        if (!PuedeComprar(a, out mensaje)) return false;

        var juego = EstadoJuego.Instance;
        int precio = AnimalDatos.PrecioCompra(a);
        if (juego == null || !juego.GastarMonedas(precio))
        {
            mensaje = "No te alcanzan las monedas.";
            return false;
        }

        var corral = De(a);
        var visual = corral.CrearAnimal(a, false, false, -1, null, 0f, 0, corral.ElegirVariante(a));
        if (visual != null) visual.Aparecer();
        bool primerCerdo = a == Animal.Cerdo && juego.PrimeraVez(Aviso.EngordeCerdo);
        Guardado.GuardarAhora();

        string nombre = AnimalDatos.Nombre(a);
        if (a == Animal.Cuy && visual != null && visual.Variante == 1) nombre = "cuy mostaza";
        mensaje = "Compraste " + (AnimalDatos.Femenino(a) ? "una " : "un ") + nombre + ": -" + precio + " monedas.";
        if (primerCerdo)
        {
            // Cholin lo explica abajo (como en el tutorial) y se repite en la tienda por si la tapa.
            const string consejo = "¡Buena compra! Mientras más coma tu cerdo, más engorda y más monedas vale al venderlo.";
            Avisar(consejo, 8f);
            mensaje += "\n" + consejo;
        }
        return true;
    }

    // ---------- Tocar y alimentar ----------

    // Toque sobre el edificio (o sobre el icono de producción): recoger tiene prioridad sobre alimentar.
    public void Tocar()
    {
        if (RecogerTodo()) return;

        if (Cantidad == 0)
        {
            Avisar(especie == Animal.Gallina
                ? "Todavía no hay gallinas aquí."
                : "Aún no hay animales aquí. Compra uno en la tienda, pestaña Animales.");
            return;
        }
        AlimentarUI.Abrir(this);
    }

    // Recoge de una vez los huevos de todas las gallinas del gallinero.
    bool RecogerTodo()
    {
        int n = produccion;
        produccion = 0;
        if (gallinaPrincipal != null) n += gallinaPrincipal.TomarHuevos();
        if (n <= 0) return false;

        ActualizarIcono();
        var juego = EstadoJuego.Instance;
        if (juego != null) for (int i = 0; i < n; i++) juego.SumarHuevo();
        Avisar(n == 1 ? "¡Recogiste un huevo!" : "¡Recogiste " + n + " huevos!");
        if (MisionController.Instance != null) MisionController.Instance.AlRecogerHuevo();
        return true;
    }

    public List<InfoAnimal> Listar()
    {
        var lista = new List<InfoAnimal>();
        if (Principal > 0)
        {
            string texto;
            bool puede = gallinaPrincipal.PuedeComer(out texto);
            lista.Add(new InfoAnimal
            {
                nombre = gallinaPrincipal.EsAdulta ? "Gallina (la primera)" : "Pollito",
                estado = gallinaPrincipal.EsAdulta
                    ? texto + " · engorde " + Mathf.Min(gallinaPrincipal.Raciones, AnimalDatos.MaxRaciones) + "/" + AnimalDatos.MaxRaciones
                    : texto,
                puedeComer = puede,
                indice = -1
            });
        }

        int totalCrias = 0;
        foreach (var a in animales) if (a != null && a.Cria) totalCrias++;

        double ahora = RelojReal.Ahora;
        bool lleno = HuevosLleno;
        int nGallina = Principal, nGallo = 0, nOtro = 0, nCria = 0;
        for (int i = 0; i < animales.Count; i++)
        {
            var v = animales[i];
            string nombre;
            if (v.Cria)
            {
                nCria++;
                string baseNombre = especie == Animal.Gallina ? "Pollito" : "Cría de " + AnimalDatos.Nombre(especie);
                nombre = totalCrias > 1 ? baseNombre + " " + nCria : baseNombre;
            }
            else if (v.Tipo == Animal.Gallina) nombre = "Gallina " + (++nGallina);
            else if (v.Tipo == Animal.Gallo) nombre = "Gallo " + (++nGallo);
            else nombre = AnimalDatos.Titulo(v.Tipo) + " " + (++nOtro);

            string estado;
            bool puede = false;
            if (v.Cria)
            {
                int falta = Mathf.CeilToInt(Mathf.Max(0f, SegundosCrecer - v.Crecido));
                estado = "crece sola (" + falta + " s)";
            }
            else if (v.Comiendo) estado = v.EnCelo ? "comiendo... · en celo" : "comiendo...";
            else if (v.Ocupado) estado = "con su pareja";
            else if (v.Tipo == Animal.Gallina && lleno) estado = "hay muchos huevos esperando";
            else
            {
                puede = true;
                if (v.EnCelo) estado = "en celo (" + Mathf.CeilToInt(Mathf.Max(0f, (float)(v.FinCelo - ahora))) + " s)";
                else if (ahora < v.LibreDesde && PuedeCriarEspecie(v))
                    estado = "descansando (" + Mathf.CeilToInt((float)(v.LibreDesde - ahora)) + " s)";
                else estado = "tiene hambre";
            }
            if (!v.Cria && AnimalDatos.Engorda(v.Tipo))
                estado += " · engorde " + Mathf.Min(v.Raciones, AnimalDatos.MaxRaciones) + "/" + AnimalDatos.MaxRaciones;

            lista.Add(new InfoAnimal { nombre = nombre, estado = estado, puedeComer = puede, indice = i });
        }
        return lista;
    }

    // ¿Este animal es de los que entran en celo al comer? (gallinero: solo el gallo)
    bool PuedeCriarEspecie(AnimalVisual v)
    {
        return especie != Animal.Gallina || v.Tipo == Animal.Gallo;
    }

    // Desde la ventana: alimenta al animal 'indice' (-1 = gallina principal) con 'alimento'.
    public bool Alimentar(int indice, Alimento alimento, out string mensaje)
    {
        if (!AnimalDatos.Acepta(especie, alimento))
        {
            mensaje = "Las " + AnimalDatos.Plural(especie) + " no comen " + AlimentoDatos.Nombre(alimento) + ".";
            return false;
        }

        if (indice < 0)
        {
            if (gallinaPrincipal == null) { mensaje = "No hay nadie ahí."; return false; }
            return gallinaPrincipal.Alimentar(alimento, out mensaje);
        }
        if (indice >= animales.Count) { mensaje = "No hay nadie ahí."; return false; }

        var v = animales[indice];
        if (v.Cria) { mensaje = "Es una cría: crece sola."; return false; }
        if (v.Comiendo) { mensaje = "Ya está comiendo."; return false; }
        if (v.Ocupado) { mensaje = "Está ocupado ahora mismo."; return false; }
        if (v.Tipo == Animal.Gallina && HuevosLleno)
        {
            mensaje = "Hay muchos huevos esperando. Recógelos primero.";
            return false;
        }

        var juego = EstadoJuego.Instance;
        if (juego == null || !juego.GastarAlimento(alimento))
        {
            mensaje = "No tienes " + AlimentoDatos.Nombre(alimento) + ".";
            return false;
        }

        v.Comer();
        mensaje = "Está comiendo." + AlEmpezarAComer(v);
        return true;
    }

    // Toque directo sobre un animal: gasta heno por defecto.
    public void AlimentarDirecto(AnimalVisual v)
    {
        if (v.Cria) { Avisar("Es una cría: crece sola."); return; }
        if (v.Comiendo) { Avisar("Está comiendo..."); return; }
        if (v.Ocupado) { Avisar("Está ocupado ahora mismo."); return; }

        int indice = animales.IndexOf(v);
        if (Alimentar(indice, Alimento.Heno, out string mensaje))
        {
            Avisar(mensaje);
            return;
        }

        var juego = EstadoJuego.Instance;
        if (juego != null && juego.Heno <= 0) Avisar("No tienes heno. " + Sugerencia());
        else Avisar(mensaje);
    }

    string Sugerencia()
    {
        var otros = new List<string>();
        foreach (var a in AnimalDatos.Alimentos(especie))
            if (a != Alimento.Heno) otros.Add(AlimentoDatos.Nombre(a));
        if (otros.Count == 0) return "Compra heno en la tienda.";
        return "Puedes darle " + string.Join(" o ", otros) + " tocando el edificio, o comprar heno en la tienda.";
    }

    // La llama AnimalVisual cuando un animal termina de comer.
    public void AlTerminarDeComer(AnimalVisual v)
    {
        if (v.Cria) return;

        if (v.Tipo == Animal.Gallina)
        {
            produccion++;   // su lugar ya estaba reservado
            ActualizarIcono();
            Avisar("¡Una gallina puso un huevo! Toca el gallinero para recogerlo.");
        }
        else if (v.Tipo == Animal.Gallo)
        {
            Avisar(v.EnCelo ? "El gallo terminó de comer y va a buscar gallina." : "El gallo terminó de comer.");
        }
        else
        {
            Avisar("El " + AnimalDatos.Nombre(v.Tipo) + " terminó de comer.");
        }
    }

    // ---------- Celo y crías ----------

    // Al empezar a comer: el gallo (gallinero) o cualquier animal de otro corral entra en celo.
    // Devuelve un texto para añadir al mensaje (vacío si no pasa nada).
    string AlEmpezarAComer(AnimalVisual v)
    {
        if (v.Cria || !PuedeCriarEspecie(v)) return "";
        double ahora = RelojReal.Ahora;
        if (ahora < v.LibreDesde) return " Está descansando: aún no entrará en celo.";
        if (PuestoLibre() < 0 || PlazasLibres <= 0) return " El corral está lleno: no hay plaza para una cría.";

        v.EnCelo = true;
        v.FinCelo = ahora + Mathf.Max(1f, segundosCelo);
        v.ProximoCorazon = Time.time + 2.5f;
        Corazones(v.transform, 3);

        if (especie == Animal.Gallina) return " Cuando termine, buscará a la gallina más cercana.";
        foreach (var o in animales)
            if (o != null && o != v && o.EnCelo) return " ¡Los dos están en celo!";
        return " ¡En celo! Alimenta a otro " + AnimalDatos.Nombre(especie) + " para que críen.";
    }

    // Corazones sobre un animal.
    void Corazones(Transform sobre, int cantidad)
    {
        float escala = transform.lossyScale.x;
        float t = tamanoAnimal * escala;
        CorazonVisual.Lanzar(transform, sobre.position + transform.up * (t * 1.1f), cantidad, t * 0.45f, t * 0.9f);
    }

    // Cada 0.4 s: apaga los celos vencidos, repite los corazones de quien espera y busca parejas.
    void GestionarCelo()
    {
        double ahora = RelojReal.Ahora;
        foreach (var v in animales)
        {
            if (v == null || !v.EnCelo) continue;
            if (ahora > v.FinCelo) { v.EnCelo = false; continue; }
            if (Time.time >= v.ProximoCorazon)
            {
                v.ProximoCorazon = Time.time + 2.5f;
                Corazones(v.transform, 1);
            }
        }

        if (criando || modeloAnimal == null) return;
        if (PuestoLibre() < 0 || PlazasLibres <= 0)
        {
            foreach (var v in animales) if (v != null) v.EnCelo = false;
            return;
        }

        if (especie == Animal.Gallina) EmparejarGallinero();
        else EmparejarGeneral();
    }

    // Gallinero: el gallo en celo (que ya terminó de comer) va a por la gallina libre más cercana,
    // sea una comprada, nacida o la del tutorial.
    void EmparejarGallinero()
    {
        double ahora = RelojReal.Ahora;
        foreach (var gallo in animales)
        {
            if (gallo == null || gallo.Tipo != Animal.Gallo || gallo.Cria || !gallo.EnCelo || gallo.Comiendo || gallo.Ocupado)
                continue;

            IReproductor mejor = null;
            float mejorD = float.MaxValue;
            Vector3 pg = gallo.transform.position;

            foreach (var h in animales)
            {
                if (h == null || h.Tipo != Animal.Gallina || h.Cria || h.Comiendo || h.Ocupado || h.EnRampa) continue;
                if (ahora < h.LibreDesde) continue;
                float d = (h.transform.position - pg).sqrMagnitude;
                if (d < mejorD) { mejorD = d; mejor = h; }
            }
            if (gallinaPrincipal != null && ahora >= libreGallinaPrincipal && gallinaPrincipal.DisponibleParaCriar)
            {
                float d = (gallinaPrincipal.transform.position - pg).sqrMagnitude;
                if (d < mejorD) { mejorD = d; mejor = gallinaPrincipal; }
            }

            if (mejor != null)
            {
                StartCoroutine(Criar(gallo, mejor));
                return;
            }
        }
    }

    // Otros corrales: dos animales en celo. El que se alimentó primero va hacia el segundo.
    void EmparejarGeneral()
    {
        var celo = new List<AnimalVisual>();
        foreach (var v in animales)
            if (v != null && v.EnCelo && !v.Cria && !v.Ocupado) celo.Add(v);
        if (celo.Count < 2) return;

        celo.Sort((x, y) => x.FinCelo.CompareTo(y.FinCelo));
        AnimalVisual a = celo[0], b = celo[1];
        AnimalVisual mover = !a.Comiendo ? a : (!b.Comiendo ? b : null);
        if (mover == null) return;   // los dos están comiendo: espera a que uno termine
        StartCoroutine(Criar(mover, mover == a ? b : a));
    }

    // Existe y sigue en el corral (si lo vendieron o se lo llevó la caja a mitad de la pareja, se cancela).
    static bool Vivo(IReproductor r)
    {
        var o = r as Object;
        return o != null && r.EnJuego;
    }

    // 'mover' camina hasta 'objetivo', se miran, saltan con corazones y nace la cría a su lado.
    IEnumerator Criar(IReproductor mover, IReproductor objetivo)
    {
        criando = true;
        mover.Reservar(true);
        objetivo.Reservar(true);
        try
        {
            // 1. El objetivo se queda quieto; el otro se acerca.
            double limite = RelojReal.Ahora + 3.0;
            while (Vivo(objetivo) && objetivo.Moviendo && RelojReal.Ahora < limite) yield return null;
            if (!Vivo(mover) || !Vivo(objetivo)) yield break;

            float escalaMundo = transform.lossyScale.x;
            Vector3 po = objetivo.Cuerpo.position;
            Vector3 eje = Vector3.ProjectOnPlane(mover.Cuerpo.position - po, transform.up);
            if (eje.sqrMagnitude < 1e-10f) eje = transform.right;
            eje.Normalize();
            Vector3 meta = po + eje * (tamanoAnimal * escalaMundo * 1.0f);

            mover.IrAMundo(meta, VelocidadPaseo);
            limite = RelojReal.Ahora + 30.0;
            while (Vivo(mover) && mover.Moviendo && RelojReal.Ahora < limite) yield return null;
            if (!Vivo(mover) || !Vivo(objetivo)) yield break;

            // 2. Se miran.
            mover.MirarMundo(objetivo.Cuerpo.position);
            objetivo.MirarMundo(mover.Cuerpo.position);
            float t0 = Time.time;
            while (Time.time - t0 < 0.35f) yield return null;
            if (!Vivo(mover) || !Vivo(objetivo)) yield break;

            // 3. Saltitos y corazones.
            Vector3 medio = (mover.Cuerpo.position + objetivo.Cuerpo.position) * 0.5f;
            Corazones(mover.Cuerpo, 3);
            Corazones(objetivo.Cuerpo, 3);
            mover.Saltitos(2);
            objetivo.Saltitos(2);
            while ((Vivo(mover) && mover.Saltando) || (Vivo(objetivo) && objetivo.Saltando)) yield return null;
            if (!Vivo(mover) || !Vivo(objetivo)) yield break;

            // 4. Nace la cría, al lado y a la altura del suelo de sus padres.
            int puesto = PuestoLibre();
            if (puesto < 0 || PlazasLibres <= 0) yield break;

            Animal tipoCria = ElegirTipoCria();
            var av = objetivo as AnimalVisual;
            var mv = mover as AnimalVisual;
            int variante;
            if (especie == Animal.Gallina) variante = av != null ? av.Variante : 0;      // hereda el diseño de su madre
            else
            {
                // Cuy: hereda el color de uno de sus padres al azar.
                var heredado = Random.value < 0.5f ? mv : av;
                variante = heredado != null ? heredado.Variante : 0;
            }

            Vector3 lado = Vector3.Cross(transform.up, eje);
            Vector3 local = transform.InverseTransformPoint(medio + lado * (tamanoAnimal * escalaMundo * 0.7f));
            local.y = transform.InverseTransformPoint(objetivo.Cuerpo.position).y;
            local = Limitar(local, tamanoAnimal * 0.4f);   // en un corral cerrado, siempre dentro de las paredes
            var cria = CrearAnimal(tipoCria, true, true, puesto, transform.TransformPoint(local), 0f, 0, variante);
            if (cria == null) yield break;
            cria.Padre = objetivo.Cuerpo;
            cria.Aparecer();

            // 5. Los padres descansan antes de poder criar otra vez.
            double libre = RelojReal.Ahora + Mathf.Max(1f, segundosEntreCrias);
            Descansar(mover, libre);
            Descansar(objetivo, libre);

            Avisar(especie == Animal.Gallina ? "¡Nació un pollito!" : "¡Nació una cría de " + AnimalDatos.Nombre(especie) + "!");
            Guardado.GuardarAhora();
        }
        finally
        {
            if (Vivo(mover)) mover.Reservar(false);
            if (Vivo(objetivo)) objetivo.Reservar(false);
            criando = false;
        }
    }

    void Descansar(IReproductor r, double hasta)
    {
        var v = r as AnimalVisual;
        if (v != null)
        {
            v.LibreDesde = hasta;
            v.EnCelo = false;
        }
        else libreGallinaPrincipal = hasta;
    }

    // Gallinero: a veces el pollito será gallo (sin pasar de 2 gallos en total).
    Animal ElegirTipoCria()
    {
        if (especie == Animal.Gallina && modeloGallo != null && Gallos < AnimalDatos.MaxGallos
            && Random.value < probabilidadGallo)
            return Animal.Gallo;
        return especie;
    }

    // La llama AnimalVisual cuando una cría termina de crecer.
    public void AlCrecer(AnimalVisual v)
    {
        int idx = animales.IndexOf(v);
        if (idx < 0) return;

        if (v.ModeloAparte)
        {
            // El pollito se cambia por el modelo adulto (gallina o gallo) en el mismo lugar.
            Animal tipo = v.Tipo;
            var nuevo = CrearAnimal(tipo, false, v.Nacido, v.Puesto, v.transform.position, 0f, 0, v.Variante);
            if (nuevo != null)
            {
                animales.RemoveAt(animales.Count - 1);   // CrearAnimal lo añadió al final
                animales[idx] = nuevo;
                nuevo.transform.localRotation = v.transform.localRotation;
                nuevo.Aparecer();
                v.enabled = false;
                Destroy(v.gameObject);
            }
            Avisar(tipo == Animal.Gallo ? "¡El pollito creció: es un gallo!" : "¡El pollito creció: es una gallina!");
        }
        else
        {
            Avisar("La cría de " + AnimalDatos.Nombre(v.Tipo) + " ya creció.");
        }
        Guardado.GuardarAhora();
    }

    // ---------- Icono de producción ----------

    void ActualizarIcono()
    {
        int total = ProduccionTotal;
        mostrado = total;
        if (modeloProduccion == null) return;
        if (total <= 0)
        {
            if (icono != null) icono.gameObject.SetActive(false);
            return;
        }

        if (icono == null)
            icono = IconoProduccion.Crear(this, modeloProduccion, tamanoProduccion,
                                          puntoProduccion != null ? puntoProduccion : transform, desplazamientoIcono);
        icono.gameObject.SetActive(true);
        icono.Fijar(total);
    }

    static void Avisar(string texto, float segundos = 3f)
    {
        if (TutorialController.Instance != null) TutorialController.Instance.Avisar(texto, segundos);
    }

    // ---------- Ayuda del editor ----------

#if UNITY_EDITOR
    // Se ejecuta al añadir el componente: intenta rellenar los campos según el nombre del objeto.
    void Reset()
    {
        bool esGallinero = name.ToLower().Contains("gallinero");
        bool esCerdo = name.ToLower().Contains("cerdo");
        especie = esGallinero ? Animal.Gallina : (esCerdo ? Animal.Cerdo : Animal.Cuy);
        idCorral = esGallinero ? 0 : (esCerdo ? 2 : 1);
        puestosDesdeElFinal = false;
        ajustarModelo = !esGallinero && !esCerdo;
        if (esCerdo) { tamanoAnimal = 0.09f; margenParedes = 0.25f; }

        var propios = new List<Transform>();
        foreach (Transform h in transform)
            if (h.name.StartsWith("Puesto")) propios.Add(h);
        if (propios.Count == 0)
            foreach (Transform h in transform.root)
                if (esGallinero && h.name.StartsWith("Puesto")) propios.Add(h);
        propios.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        puestos = propios.ToArray();

        if (esGallinero)
        {
            gallinaPrincipal = FindFirstObjectByType<Gallina>(FindObjectsInactive.Include);
            puntoProduccion = transform.Find("PuntoHuevo");
        }
        CargarModelosPorDefecto();
    }

    [ContextMenu("Cargar modelos por defecto")]
    void CargarModelosPorDefecto()
    {
        string raiz = "Assets/_Project/Modelos/Animales/";
        bool gallinero = especie == Animal.Gallina;
        bool cerdo = especie == Animal.Cerdo;
        string animal = gallinero ? raiz + "Avicola/gallina.glb" : (cerdo ? raiz + "cerdo.fbx" : raiz + "Roedor/cuy.glb");
        modeloAnimal = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(animal);
        modeloProduccion = cerdo ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(raiz + "Avicola/huevo.glb");
        modeloGallo = gallinero ? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(raiz + "Avicola/gallo.glb") : null;
        modeloGallina2 = gallinero ? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(raiz + "Avicola/gallina2.glb") : null;
        modeloCria = gallinero ? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(raiz + "Avicola/pollito.glb") : null;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
