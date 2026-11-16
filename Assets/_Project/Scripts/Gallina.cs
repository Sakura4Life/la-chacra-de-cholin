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

public class Gallina : MonoBehaviour, IReproductor
{
    enum Estado { Hambrienta, Comiendo }

    [Header("Modelos y animación")]
    [SerializeField] GameObject modeloPollito;
    [SerializeField] GameObject modeloGallina;
    [SerializeField] AnimacionAve animacion;
    [SerializeField] SphereCollider colision;
    [Tooltip("Ya no se usa: los huevos se muestran con el icono del Corral. Puedes borrar este objeto del prefab.")]
    [SerializeField] GameObject huevo;
    [Tooltip("Opcional: partículas al convertirse en gallina.")]
    [SerializeField] ParticleSystem efectoCrecer;

    [Header("Comer y crecer")]
    [SerializeField] float segundosParaHuevo = 10f;
    [SerializeField] float crecimientoPollito = 1.35f;
    [SerializeField] float radioColisionPollito = 0.05f;
    [SerializeField] float radioColisionGallina = 0.08f;

    [Header("Huevo")]
    [Tooltip("Resaltado del gallinero: solo brilla con el primer huevo (tutorial).")]
    [SerializeField] Resaltado resaltadoGallinero;

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
    bool adulto;
    bool ocupada;
    bool arriba;
    bool completo;
    int ultimoPuesto = -1;
    bool reservada;          // el corral la tiene esperando o emparejándose
    bool moviendoPropio;     // dentro de Mover (su paseo normal)
    bool moviendoExterno;    // yendo a donde la manda el corral
    bool saltando;
    int pendientes;
    double inicioComer;   // hora real (RelojReal.Ahora) en que empezó a comer

    static Gallina activa;
    static DatosGallina pendiente;   // datos cargados, a la espera de que la gallina se coloque en el gallinero

    // Lo usan el huevo y el gallinero para saber si aceptan el toque.
    public bool HuevoListo => enabled && !ocupada && pendientes > 0;

    // Huevos esperando; los muestra el icono del Corral junto con los de las demás gallinas.
    public int Pendientes => enabled ? pendientes : 0;
    public bool Comiendo => estado == Estado.Comiendo;

    void Awake()
    {
        activa = this;
        escalaPollito = modeloPollito.transform.localScale;
        modeloPollito.SetActive(true);
        modeloGallina.SetActive(false);
        if (huevo != null) huevo.SetActive(false);
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

        pendientes = Mathf.Clamp(d.pendientes, 0, Corral.TopeSinCorral);
        if (pendientes > 0) BrilloPrimerHuevo();

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
                if (Corral.GallineroLleno)
                {
                    Avisar("Hay muchos huevos esperando. Recógelos tocando el gallinero.");
                    break;
                }
                // Terminada la misión del pollito: por defecto come heno; el choclo se da desde el gallinero.
                var mision = MisionController.Instance;
                if (mision != null && mision.SolarLibre)
                {
                    Alimentar(Alimento.Heno, out string respuesta);
                    Avisar(respuesta);
                    break;
                }

                // Durante la misión solo come choclo.
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

    // ---------- Alimentar desde la ventana del corral ----------

    // La gallina del tutorial vive en el gallinero: cuenta como una gallina más del corral.
    public bool EnGallinero { get { return enabled; } }
    public bool EsAdulta { get { return adulto; } }

    public bool PuedeComer(out string texto)
    {
        if (!enabled) { texto = "no está"; return false; }
        if (ocupada) { texto = "creciendo..."; return false; }
        if (estado == Estado.Comiendo) { texto = "comiendo..."; return false; }
        if (Corral.GallineroLleno) { texto = "hay muchos huevos esperando"; return false; }
        texto = "tiene hambre";
        return true;
    }

    // Da de comer con 'alimento' (heno o choclo). Devuelve false y el motivo si no se pudo.
    public bool Alimentar(Alimento alimento, out string mensaje)
    {
        if (!PuedeComer(out string texto)) { mensaje = "Ahora no puede comer: " + texto + "."; return false; }
        if (!AnimalDatos.Acepta(Animal.Gallina, alimento))
        {
            mensaje = "La gallina no come " + AlimentoDatos.Nombre(alimento) + ".";
            return false;
        }

        var juego = EstadoJuego.Instance;
        if (!modoPrueba && (juego == null || !juego.GastarAlimento(alimento)))
        {
            mensaje = alimento == Alimento.Heno
                ? "No tienes heno. Puedes darle choclo tocando el gallinero, o comprar heno en la tienda."
                : "No tienes " + AlimentoDatos.Nombre(alimento) + ".";
            return false;
        }

        StartCoroutine(Comer());
        mensaje = "Está comiendo y en un rato pondrá un huevo.";
        return true;
    }

    // Toque sobre el huevo o sobre el gallinero (lo llama Interactuable).
    public void TocarHuevo()
    {
        if (HuevoListo) Recoger();
    }

    // Recoge todos los huevos acumulados de una vez (caso sin Corral).
    void Recoger()
    {
        int n = TomarHuevos();
        if (n <= 0) return;

        var juego = EstadoJuego.Instance;
        if (juego != null)
            for (int i = 0; i < n; i++) juego.SumarHuevo();

        Avisar(n == 1 ? "¡Recogiste un huevo!" : $"¡Recogiste {n} huevos!");
        if (MisionController.Instance != null) MisionController.Instance.AlRecogerHuevo();
    }

    // Entrega los huevos acumulados y los pone a cero. Lo usa el Corral para recoger todo junto.
    public int TomarHuevos()
    {
        if (!HuevoListo) return 0;
        int n = pendientes;
        pendientes = 0;
        QuitarBrillo();
        return n;
    }

    // ---------- Huevos ----------
    // El huevo ya no se dibuja aquí: lo muestra el icono del Corral (un modelo que gira con el número).

    // El gallinero solo brilla con el primer huevo (tutorial).
    void BrilloPrimerHuevo()
    {
        var mision = MisionController.Instance;
        if (resaltadoGallinero != null && mision != null && mision.EsperandoPrimerHuevo)
            resaltadoGallinero.Activar(true);
    }

    void QuitarBrillo()
    {
        if (resaltadoGallinero != null) resaltadoGallinero.Activar(false);
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
        BrilloPrimerHuevo();
        Poner(AnimacionAve.Anim.Reposo);
        Avisar(pendientes == 1
            ? "¡Puso un huevo! Toca el gallinero para recogerlo."
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
        reservada = false;
        moviendoPropio = false;
        moviendoExterno = false;
        saltando = false;
        pendientes = 0;
        QuitarBrillo();
        enabled = false;
    }

    // ---------- Cría (la gallina del tutorial también puede ser madre) ----------

    // Puntos del recorrido: el Corral los usa para que las demás gallinas paseen por el mismo sitio.
    public Transform PuntoGallinero { get { return puntoGallinero; } }
    public Transform PuntoRampa { get { return puntoRampa; } }
    public Transform[] Ruta { get { return puestos; } }

    // Adulta y libre: puede ser elegida por un gallo.
    public bool DisponibleParaCriar
    {
        get { return enabled && adulto && !ocupada && !reservada && !arriba && estado == Estado.Hambrienta; }
    }

    public Transform Cuerpo { get { return transform; } }
    public bool Moviendo { get { return moviendoPropio || moviendoExterno; } }
    public bool Saltando { get { return saltando; } }
    public bool Arriba { get { return arriba; } }

    // true = se queda quieta (su vida normal se detiene); false = vuelve a pasear.
    public void Reservar(bool valor) { reservada = valor; }

    public void IrAMundo(Vector3 mundo, float velocidadLocal)
    {
        StartCoroutine(IrExterno(mundo));
    }

    IEnumerator IrExterno(Vector3 mundo)
    {
        moviendoExterno = true;
        while (moviendoPropio) yield return null;   // su paseo normal se corta solo al estar reservada

        if (arriba)
        {
            yield return StartCoroutine(Mover(Local(puntoGallinero), false));
            arriba = false;
        }
        Vector3 destino = transform.parent != null ? transform.parent.InverseTransformPoint(mundo) : mundo;
        yield return StartCoroutine(Mover(destino, false));
        moviendoExterno = false;
    }

    public void MirarMundo(Vector3 punto)
    {
        Orientar(transform.parent != null ? transform.parent.InverseTransformPoint(punto) : punto);
    }

    public void Saltitos(int veces)
    {
        if (saltando || veces <= 0) return;
        StartCoroutine(Saltar(veces));
    }

    IEnumerator Saltar(int veces)
    {
        saltando = true;
        Vector3 suelo = transform.localPosition;
        for (int i = 0; i < veces; i++)
        {
            float t = 0f;
            while (t < 0.32f)
            {
                t += Time.deltaTime;
                transform.localPosition = suelo + Vector3.up * (Mathf.Sin(Mathf.Clamp01(t / 0.32f) * Mathf.PI) * 0.018f);
                yield return null;
            }
        }
        transform.localPosition = suelo;
        saltando = false;
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
        moviendoPropio = true;
        Poner(AnimacionAve.Anim.Caminar);

        while (true)
        {
            if (interrumpible && Interrumpida()) { moviendoPropio = false; yield break; }

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
            AjustarARampa();
            yield return null;
        }

        moviendoPropio = false;
        completo = true;
        Poner(AnimacionAve.Anim.Reposo);
    }

    // Sobre la rampa del gallinero sube o baja siguiendo su superficie (para no atravesarla).
    void AjustarARampa()
    {
        var corral = Corral.De(Animal.Gallina);
        if (corral == null) return;
        Vector3 l = corral.transform.InverseTransformPoint(transform.position);
        if (corral.AlturaRampa(l, out float y))
        {
            l.y = y;
            transform.position = corral.transform.TransformPoint(l);
        }
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

    bool Interrumpida() { return ocupada || reservada || estado != Estado.Hambrienta; }

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