using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Estado de la gallina para la partida guardada.
[System.Serializable]
public class DatosGallina
{
    public bool valido;      // false = no hay nada que restaurar
    public bool comiendo;
    public float comido;     // segundos que lleva comiendo
    public int pendientes;   // huevos esperando en el gallinero
    public long ticks;       // hora real del guardado
}

public class Gallina : MonoBehaviour
{
    enum Estado { Hambrienta, Comiendo }

    [Header("Modelos y animación")]
    [SerializeField] GameObject modeloPollito;
    [SerializeField] GameObject modeloGallina;
    [SerializeField] AnimacionAve animacion;
    [SerializeField] SphereCollider colision;
    [SerializeField] GameObject huevo;
    [Tooltip("Opcional: partículas al convertirse en gallina.")]
    [SerializeField] ParticleSystem efectoCrecer;

    [Header("Comer y crecer")]
    [SerializeField] float segundosParaHuevo = 10f;
    [SerializeField] float crecimientoPollito = 1.35f;
    [SerializeField] float radioColisionPollito = 0.05f;
    [SerializeField] float radioColisionGallina = 0.08f;

    [Header("Huevo")]
    [Tooltip("Punto sobre el gallinero donde aparece el huevo.")]
    [SerializeField] Transform puntoHuevo;
    [Tooltip("Resaltado del gallinero: brilla mientras haya un huevo esperando.")]
    [SerializeField] Resaltado resaltadoGallinero;
    [Tooltip("Radio del área de toque del huevo, en unidades de granja. Más grande = más fácil de tocar.")]
    [SerializeField] float radioToqueHuevo = 0.05f;
    [SerializeField] float duracionAparicion = 0.35f;
    [Tooltip("Cuántos huevos se pueden acumular en el gallinero antes de pedir que los recojas.")]
    [SerializeField] int maxHuevosAcumulados = 6;
    [Tooltip("Separación entre los huevos acumulados, en unidades de granja.")]
    [SerializeField] float separacionHuevos = 0.03f;

    [Header("Recorrido por el gallinero")]
    [Tooltip("Pie de la rampa (donde se suelta al pollito).")]
    [SerializeField] Transform puntoGallinero;
    [Tooltip("Parte alta de la rampa, en la puerta.")]
    [SerializeField] Transform puntoRampa;
    [Tooltip("Puntos del corral donde pasea la gallina.")]
    [SerializeField] Transform[] puestos;
    [Tooltip("Velocidad al caminar, en unidades de granja por segundo.")]
    [SerializeField] float velocidad = 0.10f;
    [SerializeField] float giroGrados = 360f;

    [Header("Pruebas en el editor")]
    [Tooltip("Solo para probar en Play: no gasta choclos. Desmárcalo al terminar.")]
    [SerializeField] bool modoPrueba;

    Estado estado = Estado.Hambrienta;
    Vector3 escalaPollito = Vector3.one;
    Vector3 escalaHuevo = Vector3.one;
    bool adulto;
    bool ocupada;
    bool arriba;
    bool completo;
    int ultimoPuesto = -1;
    int pendientes;
    readonly List<GameObject> huevos = new List<GameObject>();
    double inicioComer;   // hora real (RelojReal.Ahora) en que empezó a comer

    static Gallina activa;
    static DatosGallina pendiente;   // datos cargados, a la espera de que la gallina se coloque en el gallinero

    // Lo usan el huevo y el gallinero para saber si aceptan el toque.
    public bool HuevoListo => enabled && !ocupada && pendientes > 0;

    void Awake()
    {
        activa = this;
        escalaPollito = modeloPollito.transform.localScale;
        escalaHuevo = huevo.transform.localScale;
        huevos.Add(huevo);
        modeloPollito.SetActive(true);
        modeloGallina.SetActive(false);
        huevo.SetActive(false);
        PonerColision(radioColisionPollito);
    }

    void OnEnable()
    {
        StartCoroutine(Vivir());
    }

    void OnDestroy()
    {
        if (activa == this) activa = null;
    }

    // ---------- Guardado ----------

    public static void Limpiar() { pendiente = null; }

    public static void Preparar(DatosGallina datos)
    {
        pendiente = datos != null && datos.valido ? datos : null;
    }

    public static DatosGallina Exportar()
    {
        if (activa != null && activa.enabled) return activa.Crear();
        return pendiente ?? new DatosGallina();
    }

    DatosGallina Crear()
    {
        bool comiendoAhora = estado == Estado.Comiendo;
        return new DatosGallina
        {
            valido = true,
            comiendo = comiendoAhora,
            comido = comiendoAhora ? Mathf.Max(0f, (float)(RelojReal.Ahora - inicioComer)) : 0f,
            pendientes = pendientes,
            ticks = RelojReal.TicksAhora
        };
    }

    void AplicarDatos(DatosGallina d)
    {
        float fuera = RelojReal.Transcurrido(d.ticks);

        for (int i = 0; i < d.pendientes && pendientes < maxHuevosAcumulados; i++)
        {
            pendientes++;
            AgregarHuevo();
        }

        // Si estaba comiendo, retoma desde donde iba; si el tiempo ya pasó, termina enseguida y pone el huevo.
        if (d.comiendo) StartCoroutine(Comer(d.comido + fuera));
    }

    // ---------- Toque ----------

    // Toque sobre la gallina (lo llama Pollito).
    public void Tocar()
    {
        var juego = EstadoJuego.Instance;

        if (ocupada)
        {
            Avisar("Se está transformando...");
            return;
        }

        switch (estado)
        {
            case Estado.Hambrienta:
                if (pendientes >= maxHuevosAcumulados)
                {
                    Avisar("Hay muchos huevos esperando. Recógelos tocando el gallinero o un huevo.");
                    break;
                }
                // La gallina solo come choclo.
                bool hayChoclo = modoPrueba || (juego != null && juego.GastarCosecha(Cultivo.Maiz));
                if (hayChoclo)
                {
                    StartCoroutine(Comer());
                    Avisar("Está comiendo y creciendo. En un rato pondrá un huevo.");
                }
                else Avisar("Tiene hambre. Cosecha un choclo para alimentarlo.");
                break;

            case Estado.Comiendo:
                Avisar("Está comiendo...");
                break;
        }
    }

    // Toque sobre el huevo o sobre el gallinero (lo llama Interactuable).
    public void TocarHuevo()
    {
        if (HuevoListo) Recoger();
    }

    // Recoge todos los huevos acumulados de una vez.
    void Recoger()
    {
        int n = pendientes;
        var juego = EstadoJuego.Instance;
        if (juego != null)
            for (int i = 0; i < n; i++) juego.SumarHuevo();

        pendientes = 0;
        OcultarHuevos();
        Avisar(n == 1 ? "¡Recogiste un huevo!" : $"¡Recogiste {n} huevos!");
        if (MisionController.Instance != null) MisionController.Instance.AlRecogerHuevo();
    }

    // ---------- Huevos ----------

    GameObject ObtenerHuevo(int i)
    {
        while (huevos.Count <= i)
        {
            var copia = Instantiate(huevo, huevo.transform.parent);
            copia.name = huevo.name + (huevos.Count + 1);
            copia.SetActive(false);
            huevos.Add(copia);
        }
        return huevos[i];
    }

    Vector3 PosicionHuevo(int i)
    {
        if (i == 0) return Vector3.zero;
        float ang = (i - 1) * 72f * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * separacionHuevos;
    }

    // Muestra sobre el gallinero el huevo recién puesto (el número 'pendientes').
    void AgregarHuevo()
    {
        int i = pendientes - 1;
        var g = ObtenerHuevo(i);
        var t = g.transform;

        if (puntoHuevo != null)
        {
            t.SetParent(puntoHuevo, false);
            t.localPosition = PosicionHuevo(i);
            t.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
        t.localScale = Vector3.zero;

        // Área de toque generosa, creada por código.
        var col = g.GetComponent<SphereCollider>();
        if (col == null) col = g.AddComponent<SphereCollider>();
        col.center = Vector3.zero;
        col.radius = radioToqueHuevo / Mathf.Max(0.0001f, escalaHuevo.x);

        g.SetActive(true);
        var res = g.GetComponent<Resaltado>();
        if (res != null) res.Activar(true);
        if (resaltadoGallinero != null) resaltadoGallinero.Activar(true);
        StartCoroutine(Aparecer(t));
    }

    void OcultarHuevos()
    {
        foreach (var g in huevos)
        {
            if (g == null) continue;
            var res = g.GetComponent<Resaltado>();
            if (res != null) res.Activar(false);
            g.SetActive(false);
        }
        if (resaltadoGallinero != null) resaltadoGallinero.Activar(false);
    }

    IEnumerator Aparecer(Transform t)
    {
        float d = Mathf.Max(0.05f, duracionAparicion);
        float x = 0f;
        while (x < d)
        {
            x += Time.deltaTime;
            if (t == null || !t.gameObject.activeSelf) yield break;
            float k = Mathf.Clamp01(x / d);
            float s = Mathf.Sin(k * Mathf.PI * 0.5f) + 0.15f * Mathf.Sin(k * Mathf.PI);
            t.localScale = escalaHuevo * s;
            yield return null;
        }
        t.localScale = escalaHuevo;
    }

    // ---------- Comer, crecer y subir la rampa ----------

    // 'yaComido' > 0 al restaurar una partida. Se mide con la hora real (sigue con el juego en segundo plano).
    IEnumerator Comer(float yaComido = 0f)
    {
        estado = Estado.Comiendo;
        inicioComer = RelojReal.Ahora - yaComido;
        Poner(AnimacionAve.Anim.Picotear);

        var tr = modeloPollito.transform;
        while (true)
        {
            float t = (float)(RelojReal.Ahora - inicioComer);
            if (!adulto)
                tr.localScale = Vector3.Lerp(escalaPollito, escalaPollito * crecimientoPollito,
                                             Mathf.Clamp01(t / segundosParaHuevo));
            if (t >= segundosParaHuevo) break;
            yield return null;
        }

        if (!adulto)
        {
            ocupada = true;
            yield return StartCoroutine(Transformar());
            ocupada = false;
        }

        // Vuelve a tener hambre y sigue paseando; el huevo espera en el gallinero.
        pendientes++;
        estado = Estado.Hambrienta;
        AgregarHuevo();
        Poner(AnimacionAve.Anim.Reposo);
        Avisar(pendientes == 1
            ? "¡Puso un huevo! Tócalo, o toca el gallinero, para recogerlo."
            : $"¡Otro huevo! Ya hay {pendientes} esperando en el gallinero.");
    }

    IEnumerator Transformar()
    {
        Poner(AnimacionAve.Anim.Temblar);
        yield return new WaitForSeconds(1.2f);

        modeloPollito.transform.localScale = escalaPollito;
        HacerAdulta();
        if (animacion != null) animacion.Pulso();
        if (efectoCrecer != null) efectoCrecer.Play();
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.MarcarPolloAdulto();

        Poner(AnimacionAve.Anim.Reposo);
        yield return new WaitForSeconds(0.8f);
        Avisar("¡Creció y ya es una gallina! Sube a su gallinero.");

        yield return StartCoroutine(SubirRampa(false));
        Poner(AnimacionAve.Anim.Reposo);
    }

    void HacerAdulta()
    {
        adulto = true;
        modeloPollito.SetActive(false);
        modeloGallina.SetActive(true);
        PonerColision(radioColisionGallina);
    }

    // Al cargar una partida: la deja como estaba (pollito o gallina adulta).
    public void Restaurar(bool adulta)
    {
        arriba = false;
        if (adulta)
        {
            HacerAdulta();
            transform.localPosition = (puestos != null && puestos.Length > 0)
                ? Local(puestos[0])
                : Local(puntoGallinero);
        }
        else
        {
            transform.localPosition = Local(puntoGallinero);
        }

        // Huevos pendientes y comida en curso de la partida guardada.
        if (pendiente != null)
        {
            var datos = pendiente;
            pendiente = null;
            AplicarDatos(datos);
        }
    }

    // Se llama cuando el animal se vende: detiene todo y oculta el huevo.
    public void Retirar()
    {
        StopAllCoroutines();
        pendiente = null;
        ocupada = false;
        pendientes = 0;
        OcultarHuevos();
        enabled = false;
    }

    // ---------- Vida diaria ----------

    IEnumerator Vivir()
    {
        yield return null; // deja que Pollito termine de colocarlo
        if (!adulto && puntoRampa != null) Orientar(Local(puntoRampa));

        while (true)
        {
            if (Interrumpida())
            {
                yield return null;
                continue;
            }

            if (!adulto)
            {
                // Pollito esperando comida: respira y picotea.
                Poner(AnimacionAve.Anim.Reposo);
                yield return StartCoroutine(Esperar(Random.Range(1.5f, 3.5f)));
                if (Interrumpida()) continue;
                Poner(AnimacionAve.Anim.Picotear);
                yield return StartCoroutine(Esperar(Random.Range(1f, 2f)));
                continue;
            }

            yield return StartCoroutine(Pasear());
        }
    }

    IEnumerator Pasear()
    {
        Poner(AnimacionAve.Anim.Reposo);
        yield return StartCoroutine(Esperar(Random.Range(1f, 3f)));
        if (Interrumpida()) yield break;

        // A veces sube a la puerta del gallinero un ratito.
        if (puntoRampa != null && !arriba && Random.value < 0.2f)
        {
            yield return StartCoroutine(SubirRampa(true));
            if (Interrumpida()) yield break;
            Poner(AnimacionAve.Anim.Reposo);
            yield return StartCoroutine(Esperar(Random.Range(2f, 4f)));
            yield break;
        }

        yield return StartCoroutine(IrAPuesto(ElegirPuesto()));
    }

    IEnumerator SubirRampa(bool interrumpible)
    {
        if (puntoRampa == null) yield break;

        yield return StartCoroutine(Mover(Local(puntoGallinero), interrumpible));
        if (!completo) yield break;

        arriba = true;
        yield return StartCoroutine(Mover(Local(puntoRampa), interrumpible));
    }

    IEnumerator IrAPuesto(Vector3 destino)
    {
        if (arriba)
        {
            // Primero baja por la rampa.
            yield return StartCoroutine(Mover(Local(puntoGallinero), true));
            if (!completo) yield break;
            arriba = false;
        }
        yield return StartCoroutine(Mover(destino, true));
    }

    IEnumerator Mover(Vector3 destino, bool interrumpible)
    {
        completo = false;
        Poner(AnimacionAve.Anim.Caminar);

        while (true)
        {
            if (interrumpible && Interrumpida()) yield break;

            Vector3 pos = transform.localPosition;
            Vector3 resto = destino - pos;
            if (resto.magnitude < 0.004f) break;

            Vector3 plano = new Vector3(resto.x, 0f, resto.z);
            if (plano.sqrMagnitude > 0.00001f)
            {
                Quaternion objetivo = Quaternion.LookRotation(plano.normalized, Vector3.up);
                transform.localRotation = Quaternion.RotateTowards(
                    transform.localRotation, objetivo, giroGrados * Time.deltaTime);
            }

            transform.localPosition = Vector3.MoveTowards(pos, destino, velocidad * Time.deltaTime);
            yield return null;
        }

        completo = true;
        Poner(AnimacionAve.Anim.Reposo);
    }

    IEnumerator Esperar(float segundos)
    {
        float t = 0f;
        while (t < segundos && !Interrumpida())
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    bool Interrumpida() { return ocupada || estado != Estado.Hambrienta; }

    Vector3 ElegirPuesto()
    {
        if (puestos != null && puestos.Length > 0)
        {
            int i = Random.Range(0, puestos.Length);
            if (puestos.Length > 1 && i == ultimoPuesto) i = (i + 1) % puestos.Length;
            ultimoPuesto = i;
            return Local(puestos[i]);
        }

        Vector2 c = Random.insideUnitCircle * 0.1f;
        Vector3 p = Local(puntoGallinero);
        return new Vector3(p.x + c.x, p.y, p.z + c.y);
    }

    void Orientar(Vector3 hacia)
    {
        Vector3 d = hacia - transform.localPosition;
        d.y = 0f;
        if (d.sqrMagnitude > 0.00001f)
            transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
    }

    Vector3 Local(Transform t)
    {
        return transform.parent != null ? transform.parent.InverseTransformPoint(t.position) : t.position;
    }

    void PonerColision(float radio)
    {
        if (colision == null) return;
        colision.radius = radio;
        colision.center = new Vector3(0f, radio, 0f);
    }

    void Poner(AnimacionAve.Anim a)
    {
        if (animacion != null) animacion.Poner(a);
    }

    static void Avisar(string texto)
    {
        if (TutorialController.Instance != null) TutorialController.Instance.Avisar(texto);
    }

    [ContextMenu("Probar: tocar la gallina (comer o recoger huevo)")]
    void ProbarTocar()
    {
        if (Application.isPlaying) Tocar();
    }

    [ContextMenu("Probar: tocar el huevo o el gallinero")]
    void ProbarTocarHuevo()
    {
        if (Application.isPlaying) TocarHuevo();
    }
}