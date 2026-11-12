using System.Collections;
using UnityEngine;

// Un hueco de cultivo. Varios huecos forman un Terreno.
// Ciclo: sembrar -> regar -> crece -> cosechar.
// Sembrar y regar lo dispara el Terreno (toque sobre la tierra); regar y cosechar, el toque sobre la planta.
// Los modelos (semilla y etapas) NO están en el prefab: se crean al sembrar desde el CatalogoVisual
// y se destruyen al cosechar, según el cultivo elegido en el selector.
public class Parcela : MonoBehaviour
{
    enum Estado { Bloqueada, Vacia, Seca, Creciendo, Lista }

    [Header("Zona de toque")]
    [Tooltip("Collider del hueco. Si lo dejas vacío usa el que tenga este objeto. Solo está activo cuando hay semilla o planta.")]
    [SerializeField] Collider colision;
    [Tooltip("Altura de la zona de toque con la semilla recién sembrada, respecto a la planta madura (solo Capsule Collider vertical).")]
    [SerializeField, Range(0.2f, 1f)] float factorAlturaSemilla = 0.5f;

    [Header("Crecimiento")]
    [Tooltip("Multiplica el tiempo de crecimiento de cada cultivo (CultivoDatos). 1 = tiempos normales, 0.5 = el doble de rápido.")]
    [SerializeField] float multiplicadorTiempo = 1f;
    [Tooltip("Tamaño con el que empieza cada etapa, de 0 a 1 respecto a su tamaño final.")]
    [SerializeField, Range(0.05f, 1f)] float escalaInicialEtapa = 0.6f;
    [SerializeField] int cosechaPorHueco = 1;
    [SerializeField] float duracionAparicion = 0.3f;

    // Nombres de los hijos que antes se colocaban a mano en cada hueco (versión anterior del prefab).
    static readonly string[] HijosAntiguos = { "Marcador", "Semilla", "Brote", "Joven", "Madura" };

    CatalogoVisual catalogo;
    GameObject semilla;
    Transform brote, joven, madura;
    Vector3 escalaBrote = Vector3.one;
    Vector3 escalaJoven = Vector3.one;
    Vector3 escalaMadura = Vector3.one;
    CapsuleCollider capsula;
    float alturaCapsula;
    float centroYCapsula;
    Cultivo cultivo = Cultivo.Maiz;
    Estado estado = Estado.Bloqueada;

    // Los usa el Terreno para elegir dónde sembrar.
    public bool Libre => estado == Estado.Vacia;
    public bool SinRegar => estado == Estado.Seca;
    // Mientras crece no tiene nada que hacer: el toque pasa de largo hacia la tierra (y siembra o riega otro hueco).
    public bool AceptaToque => estado != Estado.Creciendo;
    public bool Bloqueada => estado == Estado.Bloqueada;

    void Awake()
    {
        LimpiarHijosAntiguos();

        catalogo = GetComponentInParent<CatalogoVisual>(true);
        if (catalogo == null) catalogo = FindFirstObjectByType<CatalogoVisual>();
        if (catalogo == null)
            Debug.LogWarning("[Parcela] No hay un CatalogoVisual en la escena: los cultivos no se verán.", this);

        if (colision == null) colision = GetComponent<Collider>();
        capsula = colision as CapsuleCollider;
        if (capsula != null)
        {
            alturaCapsula = capsula.height;
            centroYCapsula = capsula.center.y;
        }
    }

    void Start()
    {
        var juego = EstadoJuego.Instance;
        if (juego != null)
        {
            juego.OnCambio += AlCambiarJuego;
            if (juego.TieneLlave) estado = Estado.Vacia;
        }
        Aplicar(0f);
    }

    void OnDestroy()
    {
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio -= AlCambiarJuego;
    }

    void AlCambiarJuego()
    {
        if (estado == Estado.Bloqueada && EstadoJuego.Instance.TieneLlave)
        {
            estado = Estado.Vacia;
            Aplicar(0f);
        }
    }

    // Borra los modelos que quedaron como hijos fijos del hueco; ahora se crean por código.
    void LimpiarHijosAntiguos()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform hijo = transform.GetChild(i);
            if (System.Array.IndexOf(HijosAntiguos, hijo.name) < 0) continue;
            hijo.gameObject.SetActive(false);
            Destroy(hijo.gameObject);
        }
    }

    // En un hueco vacío lo llama el Terreno (siembra); en uno ocupado, el toque directo sobre la planta.
    public void Tocar()
    {
        var juego = EstadoJuego.Instance;
        switch (estado)
        {
            case Estado.Bloqueada:
                Avisar("El huerto está cerrado. Busca la llave en el pozo.");
                break;

            case Estado.Vacia:
                Sembrar(juego);
                break;

            case Estado.Seca:
                if (juego != null && juego.GastarAgua())
                {
                    estado = Estado.Creciendo;
                    StartCoroutine(Crecer());
                    Avisar("¡Regado! Espera a que crezca.");
                }
                else Avisar("No tienes agua. Toca el pozo para llenar la regadera.");
                break;

            case Estado.Creciendo:
                Avisar("Todavía está creciendo.");
                break;

            case Estado.Lista:
                Cosechar(juego);
                break;
        }
    }

    void Sembrar(EstadoJuego juego)
    {
        if (juego == null) return;

        Cultivo elegido = juego.CultivoSeleccionado;
        if (!juego.GastarSemilla(elegido))
        {
            bool buscando = MisionController.Instance != null && MisionController.Instance.BuscandoSemillas;
            Avisar(buscando
                ? "Primero toca el granero que brilla: allí están tus semillas."
                : $"No tienes semillas de {CultivoDatos.Nombre(elegido)}.");
            return;
        }

        cultivo = elegido;
        CrearVisual();
        estado = Estado.Seca;
        Aplicar(0f);
        Avisar(juego.Agua > 0
            ? "Sembraste. Toca la semilla para regarla."
            : "Sembraste. Ahora toca el pozo para llenar la regadera.");
    }

    void Cosechar(EstadoJuego juego)
    {
        if (juego != null) juego.SumarCosecha(cultivo, cosechaPorHueco);
        string nombre = cosechaPorHueco == 1 ? CultivoDatos.Nombre(cultivo) : CultivoDatos.Plural(cultivo);
        Avisar($"¡Cosechaste {cosechaPorHueco} {nombre}!");

        DestruirVisual();
        estado = Estado.Vacia;
        Aplicar(0f);
    }

    // ---------- Modelos ----------

    void CrearVisual()
    {
        DestruirVisual();
        var datos = catalogo != null ? catalogo.De(cultivo) : null;
        if (datos == null) return;

        Transform s = CatalogoVisual.Crear(datos.semilla, transform, "Visual_Semilla");
        semilla = s != null ? s.gameObject : null;

        brote = CatalogoVisual.Crear(datos.brote, transform, "Visual_Brote");
        joven = CatalogoVisual.Crear(datos.joven, transform, "Visual_Joven");
        madura = CatalogoVisual.Crear(datos.madura, transform, "Visual_Madura");

        if (brote != null) escalaBrote = brote.localScale;
        if (joven != null) escalaJoven = joven.localScale;
        if (madura != null) escalaMadura = madura.localScale;
    }

    void DestruirVisual()
    {
        if (semilla != null) Destroy(semilla);
        if (brote != null) Destroy(brote.gameObject);
        if (joven != null) Destroy(joven.gameObject);
        if (madura != null) Destroy(madura.gameObject);
        semilla = null;
        brote = joven = madura = null;
    }

    IEnumerator Crecer()
    {
        float duracion = Mathf.Max(0.1f, CultivoDatos.SegundosCrecimiento(cultivo) * multiplicadorTiempo);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duracion;
            Aplicar(Mathf.Clamp01(t));
            yield return null;
        }
        estado = Estado.Lista;
        Aplicar(1f);
        if (madura != null) StartCoroutine(Aparecer(madura, escalaMadura));
        Avisar($"¡Está lista! Toca el {CultivoDatos.Nombre(cultivo)} para cosecharlo.");
    }

    IEnumerator Aparecer(Transform t, Vector3 escalaFinal)
    {
        float d = Mathf.Max(0.05f, duracionAparicion);
        float x = 0f;
        while (x < d)
        {
            x += Time.deltaTime;
            if (estado != Estado.Lista || t == null) yield break;
            float k = Mathf.Clamp01(x / d);
            t.localScale = escalaFinal * (Mathf.Sin(k * Mathf.PI * 0.5f) + 0.15f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        if (t != null) t.localScale = escalaFinal;
    }

    void Aplicar(float progreso)
    {
        if (semilla != null) semilla.SetActive(estado == Estado.Seca);

        bool creciendo = estado == Estado.Creciendo;
        bool primeraMitad = creciendo && progreso < 0.5f;
        bool segundaMitad = creciendo && progreso >= 0.5f;

        if (brote != null)
        {
            brote.gameObject.SetActive(primeraMitad);
            if (primeraMitad)
                brote.localScale = escalaBrote * Mathf.Lerp(escalaInicialEtapa, 1f, progreso / 0.5f);
        }
        if (joven != null)
        {
            joven.gameObject.SetActive(segundaMitad);
            if (segundaMitad)
                joven.localScale = escalaJoven * Mathf.Lerp(escalaInicialEtapa, 1f, (progreso - 0.5f) / 0.5f);
        }
        if (madura != null)
        {
            bool lista = estado == Estado.Lista;
            if (!lista) madura.gameObject.SetActive(false);
            else if (!madura.gameObject.activeSelf)
            {
                madura.localScale = Vector3.zero;
                madura.gameObject.SetActive(true);
            }
        }

        AjustarColision(progreso);
    }

    // El collider solo existe cuando hay algo que tocar (semilla o planta); el hueco vacío
    // lo atiende el Terreno. Con Capsule Collider vertical, la zona crece junto con la planta.
    void AjustarColision(float progreso)
    {
        if (colision == null) return;

        bool activo = estado == Estado.Seca || estado == Estado.Creciendo || estado == Estado.Lista;
        if (colision.enabled != activo) colision.enabled = activo;
        if (!activo || capsula == null || capsula.direction != 1) return;

        float factor =
            estado == Estado.Seca ? factorAlturaSemilla :
            estado == Estado.Creciendo ? Mathf.Lerp(factorAlturaSemilla, 1f, progreso) : 1f;

        float alto = Mathf.Max(capsula.radius * 2f, alturaCapsula * factor);
        float baseY = centroYCapsula - alturaCapsula * 0.5f;
        capsula.height = alto;
        capsula.center = new Vector3(capsula.center.x, baseY + alto * 0.5f, capsula.center.z);
    }

    static void Avisar(string texto)
    {
        if (TutorialController.Instance != null) TutorialController.Instance.Avisar(texto);
    }
}
