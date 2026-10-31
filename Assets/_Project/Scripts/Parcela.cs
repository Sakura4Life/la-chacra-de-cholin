using System.Collections;
using UnityEngine;

public class Parcela : MonoBehaviour
{
    enum Estado { Bloqueada, Vacia, Seca, Creciendo, Lista }

    [SerializeField] Renderer tierra;
    [SerializeField] Transform planta;
    [SerializeField] float segundosCrecimiento = 8f;
    [SerializeField] int tomatesPorCosecha = 1;

    const float AlturaTierra = 0.04f;
    const float TamanoSeca = 0.04f;
    const float TamanoMaximo = 0.10f;

    static readonly int IdColor = Shader.PropertyToID("_BaseColor");

    MaterialPropertyBlock bloque;
    Renderer rendPlanta;
    Estado estado = Estado.Bloqueada;

    void Awake()
    {
        bloque = new MaterialPropertyBlock();
        rendPlanta = planta.GetComponent<Renderer>();
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

    public void Tocar()
    {
        var juego = EstadoJuego.Instance;
        switch (estado)
        {
            case Estado.Bloqueada:
                Avisar("El huerto está cerrado. Busca la llave en el pozo.");
                break;
            case Estado.Vacia:
                estado = Estado.Seca;
                Aplicar(0f);
                Avisar(juego != null && juego.Agua > 0
                    ? "Sembraste. Toca la planta otra vez para regarla."
                    : "Sembraste. Ahora toca el pozo para llenar la regadera.");
                break;
            case Estado.Seca:
                if (juego != null && juego.GastarAgua())
                {
                    estado = Estado.Creciendo;
                    StartCoroutine(Crecer());
                    Avisar("¡Regada! Espera a que crezca.");
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
            t += Time.deltaTime / segundosCrecimiento;
            Aplicar(Mathf.Clamp01(t));
            yield return null;
        }
        estado = Estado.Lista;
        Aplicar(1f);
        Avisar("¡Está lista! Tócala para cosechar.");
    }

    void Aplicar(float progreso)
    {
        Color colorTierra =
            estado == Estado.Bloqueada ? new Color(0.30f, 0.30f, 0.30f) :
            estado == Estado.Creciendo ? new Color(0.20f, 0.12f, 0.06f) :
                                         new Color(0.40f, 0.25f, 0.12f);
        Pintar(tierra, colorTierra);

        bool hayPlanta = estado != Estado.Bloqueada && estado != Estado.Vacia;
        planta.gameObject.SetActive(hayPlanta);
        if (!hayPlanta) return;

        float tamano;
        Color color;
        switch (estado)
        {
            case Estado.Seca:
                tamano = TamanoSeca;
                color = new Color(0.70f, 0.60f, 0.25f);
                break;
            case Estado.Creciendo:
                tamano = Mathf.Lerp(TamanoSeca, TamanoMaximo, progreso);
                color = new Color(0.20f, 0.75f, 0.20f);
                break;
            default:
                tamano = TamanoMaximo;
                color = new Color(0.90f, 0.15f, 0.10f);
                break;
        }
        planta.localScale = Vector3.one * tamano;
        planta.localPosition = new Vector3(0f, AlturaTierra + tamano * 0.5f, 0f);
        Pintar(rendPlanta, color);
    }

    void Pintar(Renderer r, Color c)
    {
        bloque.SetColor(IdColor, c);
        r.SetPropertyBlock(bloque);
    }

    static void Avisar(string texto)
    {
        if (TutorialController.Instance != null) TutorialController.Instance.Avisar(texto);
    }
}