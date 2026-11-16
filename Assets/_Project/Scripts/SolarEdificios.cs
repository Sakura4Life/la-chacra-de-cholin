using System.Collections;
using UnityEngine;

// El solar donde estaba el heno. Va en el objeto Heno de la granja.
// - Hace el heno tocable: al terminar la misión del pollito, Cholin pide retirarlo.
// - Al retirarlo queda libre para el edificio que se compre en la tienda (por ahora, el criadero de cuy).
// Al añadirlo en el Inspector busca solo el objeto CriaderoCuy (menú ⋮ → Reset).
public class SolarEdificios : MonoBehaviour
{
    public static SolarEdificios Instance { get; private set; }

    [Tooltip("El contenedor del criadero de cuy, apagado en el prefab. Se enciende al comprarlo.")]
    [SerializeField] GameObject criaderoCuy;

    bool henoOculto;
    bool criaderoColocado;
    Collider colisionHeno;

    void Reset()
    {
        var t = transform.root.Find("CriaderoCuy");
        if (t != null) criaderoCuy = t.gameObject;
    }

    void Awake()
    {
        Instance = this;

        if (criaderoCuy != null) criaderoCuy.SetActive(false);

        // El heno se toca y brilla cuando Cholin pide retirarlo.
        if (GetComponent<Resaltado>() == null) gameObject.AddComponent<Resaltado>();
        ModeloUtil.AsegurarCollider(gameObject);
        colisionHeno = GetComponentInChildren<Collider>();
        if (GetComponent<Interactuable>() == null) Interactuable.Crear(gameObject, Interactuable.Tipo.Heno);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        var mision = MisionController.Instance;
        var juego = EstadoJuego.Instance;

        if (!henoOculto && mision != null && mision.SolarLibre) OcultarHeno();
        if (!criaderoColocado && juego != null && juego.TieneEdificio(Edificio.CriaderoCuy)) Colocar(false);
    }

    void OcultarHeno()
    {
        henoOculto = true;
        foreach (Transform hijo in transform) hijo.gameObject.SetActive(false);
        if (colisionHeno != null) colisionHeno.enabled = false;
        var interactuable = GetComponent<Interactuable>();
        if (interactuable != null) interactuable.Resaltar(false);
    }

    // La llama la tienda. Devuelve false (y el motivo) si no se pudo comprar.
    public bool Comprar(Edificio e, out string mensaje)
    {
        var juego = EstadoJuego.Instance;
        var mision = MisionController.Instance;
        if (juego == null) { mensaje = "No se pudo comprar."; return false; }
        if (mision == null || !mision.SolarLibre)
        {
            mensaje = "Primero termina la misión del pollito y retira el heno.";
            return false;
        }
        if (juego.TieneEdificio(e)) { mensaje = "Ya tienes este edificio."; return false; }
        if (criaderoCuy == null)
        {
            mensaje = "Falta asignar el criadero en SolarEdificios.";
            return false;
        }

        int precio = EdificioDatos.Precio(e);
        if (!juego.GastarMonedas(precio)) { mensaje = "No te alcanzan las monedas."; return false; }

        juego.MarcarEdificio(e);
        Colocar(true);
        Guardado.GuardarAhora();
        mensaje = "¡Compraste el " + EdificioDatos.Nombre(e).ToLower() + "! -" + precio + " monedas.";
        return true;
    }

    void Colocar(bool animar)
    {
        if (criaderoCuy == null || criaderoColocado) return;
        criaderoColocado = true;

        var t = criaderoCuy.transform;
        Vector3 escala = t.localScale;
        criaderoCuy.SetActive(true);    // Corral ajusta el modelo al encenderse
        if (animar) StartCoroutine(Aparecer(t, escala));
    }

    IEnumerator Aparecer(Transform t, Vector3 escalaFinal)
    {
        const float duracion = 0.5f;
        float x = 0f;
        while (x < duracion)
        {
            x += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(x / duracion) - 1f;
            float s = 1f + 2.70158f * k * k * k + 1.70158f * k * k;   // easeOutBack
            t.localScale = escalaFinal * Mathf.Max(0.01f, s);
            yield return null;
        }
        t.localScale = escalaFinal;
    }
}
