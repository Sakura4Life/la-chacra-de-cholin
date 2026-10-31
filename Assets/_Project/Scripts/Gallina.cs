using System.Collections;
using UnityEngine;

public class Gallina : MonoBehaviour
{
    enum Estado { Hambrienta, Comiendo, ConHuevo }

    [SerializeField] Renderer cuerpo;
    [SerializeField] GameObject huevo;
    [SerializeField] float segundosParaHuevo = 10f;
    [Tooltip("Cuánto crece la primera vez que come (pollito a adulto).")]
    [SerializeField] float factorAdulto = 1.7f;
    [Tooltip("Altura del suelo donde se apoya (parte alta del gallinero).")]
    [SerializeField] float alturaSuelo = 0.03f;

    static readonly int IdColor = Shader.PropertyToID("_BaseColor");

    MaterialPropertyBlock bloque;
    Vector3 escalaBase;
    Estado estado = Estado.Hambrienta;
    bool adulto;

    void Awake()
    {
        bloque = new MaterialPropertyBlock();
        escalaBase = transform.localScale;
    }

    void Start() { Aplicar(); }

    public void Tocar()
    {
        var juego = EstadoJuego.Instance;
        switch (estado)
        {
            case Estado.Hambrienta:
                if (juego != null && juego.GastarTomate())
                {
                    estado = Estado.Comiendo;
                    Aplicar();
                    StartCoroutine(Comer());
                    Avisar("Está comiendo y creciendo. En un rato pondrá un huevo.");
                }
                else Avisar("Tiene hambre. Cosecha un tomate para alimentarlo.");
                break;
            case Estado.Comiendo:
                Avisar("Está comiendo...");
                break;
            case Estado.ConHuevo:
                if (juego != null) juego.SumarHuevo();
                estado = Estado.Hambrienta;
                Aplicar();
                Avisar("¡Recogiste un huevo!");
                if (MisionController.Instance != null) MisionController.Instance.AlRecogerHuevo();
                break;
        }
    }

    IEnumerator Comer()
    {
        Vector3 inicio = escalaBase;
        Vector3 fin = adulto ? escalaBase : escalaBase * factorAdulto;

        float t = 0f;
        while (t < segundosParaHuevo)
        {
            t += Time.deltaTime;
            float avance = Mathf.Clamp01(t / segundosParaHuevo);
            float rebote = 1f + 0.08f * Mathf.Sin(t * 10f);
            transform.localScale = Vector3.Lerp(inicio, fin, avance) * rebote;
            Apoyar();
            yield return null;
        }

        escalaBase = fin;
        adulto = true;
        transform.localScale = escalaBase;
        Apoyar();

        estado = Estado.ConHuevo;
        Aplicar();
        Avisar("¡Puso un huevo! Tócalo para recogerlo.");
    }

    // Se llama cuando el animal se vende: detiene todo y oculta el huevo.
    public void Retirar()
    {
        StopAllCoroutines();
        huevo.SetActive(false);
        enabled = false;
    }

    // Mantiene el cuerpo apoyado sobre el suelo mientras crece.
    void Apoyar()
    {
        Vector3 p = transform.localPosition;
        p.y = alturaSuelo + transform.localScale.x * 0.5f;
        transform.localPosition = p;
    }

    void Aplicar()
    {
        Color color =
            estado == Estado.Hambrienta ? new Color(0.65f, 0.60f, 0.40f) :
            estado == Estado.Comiendo ? new Color(1.00f, 0.85f, 0.20f) :
                                          new Color(1.00f, 0.95f, 0.60f);
        bloque.SetColor(IdColor, color);
        cuerpo.SetPropertyBlock(bloque);
        huevo.SetActive(estado == Estado.ConHuevo);
    }

    static void Avisar(string texto)
    {
        if (TutorialController.Instance != null) TutorialController.Instance.Avisar(texto);
    }
}