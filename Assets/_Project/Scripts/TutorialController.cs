using System.Collections;
using TMPro;
using UnityEngine;

public class TutorialController : MonoBehaviour
{
    public static TutorialController Instance { get; private set; }

    enum Paso { SinGranja, BuscarPizarra, LeerNota, BuscarPozo, Completo }

    [Header("Pista inferior")]
    [SerializeField] GameObject panelPista;
    [SerializeField] TMP_Text textoPista;

    [Header("Nota de la pizarra")]
    [SerializeField] GameObject panelNota;
    [SerializeField] TMP_Text textoNota;
    [TextArea(3, 6)]
    [SerializeField]
    string contenidoNota =
        "Querido nuevo dueño:\n\nEsta chacra necesita manos como las tuyas.\n\nRecuerda: el pozo guarda la llave del huerto.";

    [Header("HUD")]
    [SerializeField] CanvasGroup hud;

    Paso paso = Paso.SinGranja;
    Interactuable pizarra, pozo;
    string pistaBase = "";
    Coroutine aviso;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        panelNota.SetActive(false);
        hud.alpha = 0f;
        MostrarPista("");
    }

    public void Registrar(Interactuable objeto)
    {
        if (objeto.TipoObjeto == Interactuable.Tipo.Pizarra) pizarra = objeto;
        else if (objeto.TipoObjeto == Interactuable.Tipo.Pozo) pozo = objeto;

        if (pizarra == null || pozo == null) return;

        if (paso == Paso.SinGranja) IrA(Paso.BuscarPizarra);
        else AplicarResaltado();
    }

    public void AlTocar(Interactuable objeto)
    {
        if (panelNota.activeSelf) return;

        switch (paso)
        {
            case Paso.BuscarPizarra:
                if (objeto.TipoObjeto == Interactuable.Tipo.Pizarra) IrA(Paso.LeerNota);
                else Avisar("Primero lee la pizarra de la casa, la que brilla.");
                break;
            case Paso.BuscarPozo:
                if (objeto.TipoObjeto == Interactuable.Tipo.Pozo) IrA(Paso.Completo);
                else AbrirNota();
                break;
            case Paso.Completo:
                if (objeto.TipoObjeto == Interactuable.Tipo.Pizarra) AbrirNota();
                break;
        }
    }

    // Lo llama el botón "Entendido" del panel de la nota.
    public void CerrarNota()
    {
        panelNota.SetActive(false);
        if (paso == Paso.LeerNota) IrA(Paso.BuscarPozo);
    }

    // Mensaje temporal que luego vuelve a la pista del paso actual.
    public void Avisar(string texto, float segundos = 3f)
    {
        if (aviso != null) StopCoroutine(aviso);
        aviso = StartCoroutine(AvisoTemporal(texto, segundos));
    }

    IEnumerator AvisoTemporal(string texto, float segundos)
    {
        Escribir(texto);
        yield return new WaitForSeconds(segundos);
        Escribir(pistaBase);
        aviso = null;
    }

    void IrA(Paso nuevo)
    {
        paso = nuevo;
        AplicarResaltado();

        switch (paso)
        {
            case Paso.BuscarPizarra:
                MostrarPista("Camina hasta la casa y toca la pizarra que brilla.");
                break;
            case Paso.LeerNota:
                MostrarPista("");
                AbrirNota();
                break;
            case Paso.BuscarPozo:
                MostrarPista("La nota habla del pozo. Acércate y tócalo.");
                break;
            case Paso.Completo:
                MostrarPista("");
                Avisar("¡Conseguiste la llave del huerto! Se desbloqueó tu panel y las parcelas.", 5f);
                Handheld.Vibrate();
                if (EstadoJuego.Instance != null) EstadoJuego.Instance.DarLlave();
                StartCoroutine(MostrarHud());
                break;
        }
    }

    void AplicarResaltado()
    {
        if (pizarra != null) pizarra.Resaltar(paso == Paso.BuscarPizarra);
        if (pozo != null) pozo.Resaltar(paso == Paso.BuscarPozo);
    }

    void AbrirNota()
    {
        textoNota.text = contenidoNota;
        panelNota.SetActive(true);
    }

    void MostrarPista(string texto)
    {
        pistaBase = texto;
        Escribir(texto);
    }

    void Escribir(string texto)
    {
        textoPista.text = texto;
        panelPista.SetActive(!string.IsNullOrEmpty(texto));
    }

    IEnumerator MostrarHud()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.8f;
            hud.alpha = Mathf.Clamp01(t);
            yield return null;
        }
    }
}