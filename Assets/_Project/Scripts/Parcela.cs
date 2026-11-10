using System.Collections;
using UnityEngine;

// Un hueco de cultivo. Varios huecos forman un Terreno.
// Ciclo: sembrar -> regar -> crece -> cosechar.
// Sembrar lo dispara el Terreno (toque sobre la tierra); regar y cosechar, el toque sobre la planta.
public class Parcela : MonoBehaviour
{
    enum Estado { Bloqueada, Vacia, Seca, Creciendo, Lista }

    [Header("Visual del hueco")]
    [Tooltip("Opcional. Si lo dejas vacío no pasa nada.")]
    [SerializeField] Renderer marcador;
    [Tooltip("Semilla que se ve al sembrar, antes de regar.")]
    [SerializeField] GameObject semilla;
    [Header("Etapas de la planta (modelos hijos del hueco)")]
    [Tooltip("Etapa 1: brote. Se ve en la primera mitad del crecimiento.")]
    [SerializeField] Transform brote;
    [Tooltip("Etapa 2: planta joven. Se ve en la segunda mitad del crecimiento.")]
    [SerializeField] Transform joven;
    [Tooltip("Etapa 3: planta con fruto. Se ve cuando está lista para cosechar.")]
    [SerializeField] Transform madura;

    [Header("Zona de toque")]
    [Tooltip("Collider del hueco. Si lo dejas vacío usa el que tenga este objeto. Solo está activo cuando hay semilla o planta.")]
    [SerializeField] Collider colision;
    [Tooltip("Altura de la zona de toque con la semilla recién sembrada, respecto a la planta madura (solo Capsule Collider vertical).")]
    [SerializeField, Range(0.2f, 1f)] float factorAlturaSemilla = 0.5f;

    [Header("Crecimiento")]
    [SerializeField] float segundosCrecimiento = 8f;
    [Tooltip("Tamaño con el que empieza a crecer, de 0 a 1 respecto al tamaño final.")]
    [SerializeField, Range(0.05f, 1f)] float escalaInicialEtapa = 0.6f;
    [SerializeField] int tomatesPorCosecha = 1;
    [SerializeField] float duracionAparicion = 0.3f;

    // Por ahora todos los huecos siembran tomate; en la 12b se elegirá desde el selector.
    const Cultivo cultivo = Cultivo.Tomate;

    static readonly int[] IdsColor =
    {
        Shader.PropertyToID("_BaseColor"),
        Shader.PropertyToID("baseColorFactor"),
        Shader.PropertyToID("_Color"),
    };

    MaterialPropertyBlock bloque;
    Vector3 escalaBrote = Vector3.one;
    Vector3 escalaJoven = Vector3.one;
    Vector3 escalaMadura = Vector3.one;
    CapsuleCollider capsula;
    float alturaCapsula;
    float centroYCapsula;
    int idColor = -1;
    Estado estado = Estado.Bloqueada;

    // Los usa el Terreno para elegir dónde sembrar.
    public bool Libre => estado == Estado.Vacia;
    public bool SinRegar => estado == Estado.Seca;
    // Mientras crece no tiene nada que hacer: el toque pasa de largo hacia la tierra (y siembra o riega otro hueco).
    public bool AceptaToque => estado != Estado.Creciendo;
    public bool Bloqueada => estado == Estado.Bloqueada;

    void Awake()
    {
        bloque = new MaterialPropertyBlock();
        if (brote != null) escalaBrote = brote.localScale;
        if (joven != null) escalaJoven = joven.localScale;
        if (madura != null) escalaMadura = madura.localScale;

        if (colision == null) colision = GetComponent<Collider>();
        capsula = colision as CapsuleCollider;
        if (capsula != null)
        {
            alturaCapsula = capsula.height;
            centroYCapsula = capsula.center.y;
        }

        if (marcador != null && marcador.sharedMaterial != null)
        {
            foreach (int id in IdsColor)
                if (marcador.sharedMaterial.HasProperty(id)) { idColor = id; break; }
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
                if (juego == null || !juego.GastarSemilla(cultivo))
                {
                    bool buscando = MisionController.Instance != null && MisionController.Instance.BuscandoSemillas;
                    Avisar(buscando
                        ? "Primero toca el granero que brilla: allí están tus semillas."
                        : "No tienes semillas de tomate.");
                    break;
                }
                estado = Estado.Seca;
                Aplicar(0f);
                Avisar(juego.Agua > 0
                    ? "Sembraste. Toca la semilla para regarla."
                    : "Sembraste. Ahora toca el pozo para llenar la regadera.");
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
                if (juego != null) juego.SumarTomates(tomatesPorCosecha);
                estado = Estado.Vacia;
                Aplicar(0f);
                Avisar($"¡Cosechaste {tomatesPorCosecha} tomate!");
                break;
        }
    }

    IEnumerator Crecer()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.1f, segundosCrecimiento);
            Aplicar(Mathf.Clamp01(t));
            yield return null;
        }
        estado = Estado.Lista;
        Aplicar(1f);
        if (madura != null) StartCoroutine(Aparecer(madura, escalaMadura));
        Avisar("¡Está lista! Tócala para cosechar.");
    }

    IEnumerator Aparecer(Transform t, Vector3 escalaFinal)
    {
        float d = Mathf.Max(0.05f, duracionAparicion);
        float x = 0f;
        while (x < d)
        {
            x += Time.deltaTime;
            if (estado != Estado.Lista) yield break;
            float k = Mathf.Clamp01(x / d);
            t.localScale = escalaFinal * (Mathf.Sin(k * Mathf.PI * 0.5f) + 0.15f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        t.localScale = escalaFinal;
    }

    void Aplicar(float progreso)
    {
        Color colorMarcador =
            estado == Estado.Bloqueada ? new Color(0.30f, 0.30f, 0.30f) :
            estado == Estado.Creciendo ? new Color(0.20f, 0.12f, 0.06f) :
                                         new Color(0.40f, 0.25f, 0.12f);
        if (marcador != null && idColor != -1)
        {
            bloque.Clear();
            bloque.SetColor(idColor, colorMarcador);
            marcador.SetPropertyBlock(bloque);
        }

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
