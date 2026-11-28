using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// El solar donde estaba el heno y el sitio de los edificios que se compran en la tienda. Va en el objeto Heno.
// - Hace el heno tocable: al terminar la misión del pollito, Cholin pide retirarlo.
// - Al retirarlo quedan disponibles los edificios de la tienda: el criadero de cuy, la ampliación de granja
//   y los corrales grandes (estos piden haber comprado la ampliación, porque van sobre el terreno nuevo).
// Cada edificio es un contenedor apagado en el prefab que se enciende (con rebote) al comprarlo.
// Al añadirlo en el Inspector busca solos los objetos CriaderoCuy, Ampliacion y CorralCerdo (menú ⋮ → Reset).
public class SolarEdificios : MonoBehaviour
{
    public static SolarEdificios Instance { get; private set; }

    static readonly Edificio[] todos = (Edificio[])System.Enum.GetValues(typeof(Edificio));

    [Tooltip("El contenedor del criadero de cuy, apagado en el prefab. Se enciende al comprarlo.")]
    [SerializeField] GameObject criaderoCuy;
    [Tooltip("El contenedor del terreno nuevo junto al granero, apagado en el prefab. Se enciende al comprar la ampliación. " +
             "Su pivote debe estar en el centro del terreno nuevo (así crece desde el centro).")]
    [SerializeField] GameObject ampliacion;
    [Tooltip("El corral de cerdos (cuatro vallas + el componente Corral), apagado en el prefab. Se enciende al comprarlo, " +
             "y para comprarlo hay que tener la ampliación. Colócalo sobre el terreno nuevo.")]
    [SerializeField] GameObject corralCerdo;

    bool henoOculto;
    readonly HashSet<Edificio> colocados = new HashSet<Edificio>();
    Collider colisionHeno;

    void Reset()
    {
        var raiz = transform.root;
        var t = raiz.Find("CriaderoCuy");
        if (t != null) criaderoCuy = t.gameObject;
        t = raiz.Find("Ampliacion");
        if (t != null) ampliacion = t.gameObject;
        t = raiz.Find("CorralCerdo");
        if (t != null) corralCerdo = t.gameObject;
    }

    GameObject Contenedor(Edificio e)
    {
        switch (e)
        {
            case Edificio.Ampliacion: return ampliacion;
            case Edificio.CorralCerdo: return corralCerdo;
            default: return criaderoCuy;
        }
    }

    void Awake()
    {
        Instance = this;

        foreach (var e in todos)
        {
            var c = Contenedor(e);
            if (c != null) c.SetActive(false);
        }

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

        // Edificios comprados (también al cargar una partida): se colocan sin animación.
        if (juego == null) return;
        foreach (var e in todos)
            if (!colocados.Contains(e) && juego.TieneEdificio(e)) Colocar(e, false);
    }

    void OcultarHeno()
    {
        henoOculto = true;
        foreach (Transform hijo in transform) hijo.gameObject.SetActive(false);
        if (colisionHeno != null) colisionHeno.enabled = false;
        var interactuable = GetComponent<Interactuable>();
        if (interactuable != null) interactuable.Resaltar(false);
    }

    // ¿Se puede comprar ahora este edificio? Si no, 'motivo' dice por qué (lo usa la tienda).
    public static bool Disponible(Edificio e, out string motivo)
    {
        var solar = Instance;
        var juego = EstadoJuego.Instance;
        var mision = MisionController.Instance;

        if (solar == null || juego == null) { motivo = "Coloca primero tu granja."; return false; }
        if (juego.TieneEdificio(e)) { motivo = "Ya tienes este edificio."; return false; }
        if (mision == null || !mision.SolarLibre)
        {
            motivo = "Termina la misión del pollito y retira el heno para tener sitio.";
            return false;
        }
        if (EdificioDatos.RequiereAmpliacion(e) && !juego.TieneEdificio(Edificio.Ampliacion))
        {
            motivo = "Compra primero la ampliación de granja: el corral va sobre el terreno nuevo.";
            return false;
        }
        if (solar.Contenedor(e) == null)
        {
            motivo = "Falta asignar «" + EdificioDatos.Nombre(e) + "» en SolarEdificios.";
            return false;
        }
        motivo = "";
        return true;
    }

    // La llama la tienda. Devuelve false (y el motivo) si no se pudo comprar.
    public bool Comprar(Edificio e, out string mensaje)
    {
        if (!Disponible(e, out mensaje)) return false;

        var juego = EstadoJuego.Instance;
        int precio = EdificioDatos.Precio(e);
        if (!juego.GastarMonedas(precio)) { mensaje = "No te alcanzan las monedas."; return false; }

        juego.MarcarEdificio(e);
        Colocar(e, true);
        Guardado.GuardarAhora();
        mensaje = EdificioDatos.MensajeCompra(e, precio);
        return true;
    }

    void Colocar(Edificio e, bool animar)
    {
        var contenedor = Contenedor(e);
        if (contenedor == null || !colocados.Add(e)) return;

        var t = contenedor.transform;
        Vector3 escala = t.localScale;
        contenedor.SetActive(true);    // Corral ajusta el modelo al encenderse
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
