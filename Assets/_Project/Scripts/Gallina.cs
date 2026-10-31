using System.Collections;
using UnityEngine;

public class Gallina : MonoBehaviour
{
    enum Estado { Hambrienta, Comiendo, ConHuevo }

    [SerializeField] Renderer cuerpo;
    [SerializeField] GameObject huevo;
    [SerializeField] float segundosParaHuevo = 10f;

    static readonly int IdColor = Shader.PropertyToID("_BaseColor");

    MaterialPropertyBlock bloque;
    Vector3 escalaBase;
    Estado estado = Estado.Hambrienta;

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
                    Avisar("La gallina come. En un rato pondrá un huevo.");
                }
                else Avisar("Tiene hambre. Cosecha un tomate para alimentarla.");
                break;
            case Estado.Comiendo:
                Avisar("Está comiendo...");
                break;
            case Estado.ConHuevo:
                if (juego != null) juego.SumarHuevo();
                estado = Estado.Hambrienta;
                Aplicar();
                Avisar("¡Recogiste un huevo!");
                break;
        }
    }

    IEnumerator Comer()
    {
        float t = 0f;
        while (t < segundosParaHuevo)
        {
            t += Time.deltaTime;
            transform.localScale = escalaBase * (1f + 0.08f * Mathf.Sin(t * 10f));
            yield return null;
        }
        transform.localScale = escalaBase;
        estado = Estado.ConHuevo;
        Aplicar();
        Avisar("¡La gallina puso un huevo! Tócala para recogerlo.");
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