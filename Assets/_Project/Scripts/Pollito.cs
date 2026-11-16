using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class Pollito : MonoBehaviour
{
    [Header("Cuerpo")]
    [Tooltip("El vacío 'Visual', padre de los modelos del pollito y la gallina.")]
    [SerializeField] GameObject visual;
    [SerializeField] Collider colision;
    [SerializeField] AnimacionAve animacion;

    [Header("Sonido (frío-caliente)")]
    [Tooltip("Si lo dejas vacío se genera un pío sintético.")]
    [SerializeField] AudioClip clipPiar;
    [Tooltip("Distancias en 'metros de granja': se ajustan solas al tamaño de la granja.")]
    [SerializeField] float distanciaLejos = 3f;
    [SerializeField] float distanciaCerca = 0.6f;
    [SerializeField] float intervaloLejos = 2f;
    [SerializeField] float intervaloCerca = 0.3f;

    [Header("Pista visual")]
    [SerializeField] GameObject plumas;
    [SerializeField] float distanciaPlumas = 1.8f;

    [Header("Llevarlo al gallinero")]
    [SerializeField] Transform puntoGallinero;
    [SerializeField] Resaltado resaltadoGallinero;
    [SerializeField] float radioSoltar = 0.2f;
    [SerializeField] float alturaArrastre = 0.07f;
    [SerializeField] float alturaSuelo = 0f;
    [SerializeField] float limite = 0.45f;

    [Header("Pruebas en el editor")]
    [Tooltip("Solo para probar en Play sin hacer toda la misión. Desmárcalo al terminar.")]
    [SerializeField] bool modoPrueba;

    AudioSource fuente;
    Transform camara;
    Gallina gallina;
    float proximoPiar;
    float proximoSalto;
    bool arrastrando;
    bool gallineroBrilla;
    bool retirado;
    bool posicionado;

    void Awake()
    {
        gallina = GetComponent<Gallina>();

        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f;                      // sonido 3D
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 0.2f;
        fuente.maxDistance = 4f;

        if (clipPiar == null) clipPiar = CrearPioSintetico();

        visual.SetActive(false);
        colision.enabled = false;
        plumas.SetActive(false);
    }

    void Update()
    {
        var mision = MisionController.Instance;
        if (mision == null) return;

        var juego = EstadoJuego.Instance;

        if (modoPrueba && !posicionado)
        {
            posicionado = true;
            transform.localPosition = PosicionGallinero();
            if (gallina != null) gallina.enabled = true;
        }

        if (!retirado && juego != null && juego.PolloVendido)
        {
            retirado = true;
            if (gallina != null) gallina.Retirar();
        }

        bool visible = modoPrueba || (mision.PollitoVisible && !retirado);
        if (visible != visual.activeSelf)
        {
            visual.SetActive(visible);
            colision.enabled = visible;
        }

        // Al cargar una partida: si el pollito ya estaba en el gallinero, lo coloca ahí.
        if (!posicionado && mision.PollitoEnGallinero && juego != null && juego.TienePollo)
        {
            posicionado = true;
            if (gallina != null)
            {
                gallina.Restaurar(juego.PolloAdulto);
                gallina.enabled = true;
            }
            else transform.localPosition = PosicionGallinero();
        }

        bool encontrado = mision.PollitoYaEncontrado;
        if (encontrado != gallineroBrilla)
        {
            gallineroBrilla = encontrado;
            if (resaltadoGallinero != null) resaltadoGallinero.Activar(encontrado);
        }

        if (encontrado) ManejarArrastre();
        else arrastrando = false;

        // Mientras espera escondido o se arrastra, esta clase decide su animación.
        if (visible && !posicionado)
        {
            Poner(arrastrando ? AnimacionAve.Anim.Colgado : AnimacionAve.Anim.Reposo);
            if (!arrastrando && Time.time >= proximoSalto)
            {
                if (animacion != null) animacion.Saltar();
                proximoSalto = Time.time + Random.Range(3f, 6f);
            }
        }

        if (!mision.PollitoBuscandose || !ControladorGranja.GranjaColocada)
        {
            if (plumas.activeSelf) plumas.SetActive(false);
            return;
        }

        if (camara == null)
        {
            if (Camera.main == null) return;
            camara = Camera.main.transform;
        }

        float escalaGranja = transform.parent != null ? transform.parent.lossyScale.x : 1f;
        float d = Vector3.Distance(camara.position, transform.position) / escalaGranja;
        float cercania = Mathf.InverseLerp(distanciaLejos, distanciaCerca, d); // 0 lejos, 1 cerca

        plumas.SetActive(d <= distanciaPlumas);

        if (Time.time >= proximoPiar)
        {
            fuente.pitch = Mathf.Lerp(1f, 1.35f, cercania);
            fuente.PlayOneShot(clipPiar, Mathf.Lerp(0.35f, 1f, cercania));
            if (animacion != null) animacion.Pulso();
            proximoPiar = Time.time + Mathf.Lerp(intervaloLejos, intervaloCerca, cercania);
        }
    }

    void Poner(AnimacionAve.Anim a)
    {
        if (animacion != null) animacion.Poner(a);
    }

    // Toque rápido sobre el pollito (lo llama Interactuable).
    public void Tocar()
    {
        var mision = MisionController.Instance;
        if (mision == null) return;

        if (mision.PollitoBuscandose)
            mision.AlEncontrarPollito();
        else if (mision.PollitoYaEncontrado)
            Avisar("Arrastra al pollito con el dedo hasta el gallinero, el que brilla.");
        else if ((mision.PollitoEnGallinero || modoPrueba) && gallina != null)
            gallina.Tocar();
    }

    // ---------- Arrastrar ----------

    void ManejarArrastre()
    {
        var pantalla = Touchscreen.current;
        if (pantalla == null || Camera.main == null) return;
        var toque = pantalla.primaryTouch;

        if (toque.press.wasPressedThisFrame && !arrastrando)
        {
            int id = toque.touchId.ReadValue();
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(id)) return;

            Ray rayo = Camera.main.ScreenPointToRay(toque.position.ReadValue());
            foreach (var golpe in Physics.RaycastAll(rayo, 10f))
            {
                if (golpe.collider == colision) { arrastrando = true; break; }
            }
        }

        if (!arrastrando) return;

        if (ContarDedos(pantalla) > 1)
        {
            arrastrando = false;
            return;
        }

        if (toque.press.isPressed)
            Mover(toque.position.ReadValue());

        if (toque.press.wasReleasedThisFrame)
        {
            arrastrando = false;
            Soltar();
        }
    }

    void Mover(Vector2 posicionPantalla)
    {
        var padre = transform.parent;
        if (padre == null) return;

        Ray rayo = Camera.main.ScreenPointToRay(posicionPantalla);
        var plano = new Plane(padre.up, padre.TransformPoint(new Vector3(0f, alturaArrastre, 0f)));
        if (plano.Raycast(rayo, out float distancia))
        {
            Vector3 local = padre.InverseTransformPoint(rayo.GetPoint(distancia));
            local.x = Mathf.Clamp(local.x, -limite, limite);
            local.z = Mathf.Clamp(local.z, -limite, limite);
            local.y = alturaArrastre;
            transform.localPosition = local;
        }
    }

    // Posición de PuntoGallinero en el espacio de la granja, esté donde esté en la jerarquía
    // (suelto en la raíz o como hijo del Gallinero).
    Vector3 PosicionGallinero()
    {
        return transform.parent != null
            ? transform.parent.InverseTransformPoint(puntoGallinero.position)
            : puntoGallinero.position;
    }

    void Soltar()
    {
        Vector3 p = transform.localPosition;
        Vector3 destino = PosicionGallinero();
        float distancia = new Vector2(p.x - destino.x, p.z - destino.z).magnitude;

        if (distancia <= radioSoltar)
        {
            transform.localPosition = destino;
            posicionado = true;
            Poner(AnimacionAve.Anim.Reposo);
            if (gallina != null) gallina.enabled = true;
            MisionController.Instance.AlLlegarAlGallinero();
        }
        else
        {
            p.y = alturaSuelo;
            transform.localPosition = p;
            Avisar("Llévalo hasta el gallinero, el que brilla.");
        }
    }

    static int ContarDedos(Touchscreen pantalla)
    {
        int n = 0;
        foreach (var dedo in pantalla.touches)
            if (dedo.isInProgress) n++;
        return n;
    }

    static void Avisar(string texto)
    {
        if (TutorialController.Instance != null) TutorialController.Instance.Avisar(texto);
    }

    static AudioClip CrearPioSintetico()
    {
        const int frecuencia = 44100;
        int muestras = (int)(frecuencia * 0.18f);
        var datos = new float[muestras];
        float fase = 0f;
        for (int i = 0; i < muestras; i++)
        {
            float t = i / (float)muestras;
            float f = Mathf.Lerp(2200f, 3400f, t);
            fase += 2f * Mathf.PI * f / frecuencia;
            datos[i] = Mathf.Sin(fase) * Mathf.Sin(Mathf.PI * t) * 0.5f;
        }
        var clip = AudioClip.Create("PioSintetico", muestras, 1, frecuencia, false);
        clip.SetData(datos, 0);
        return clip;
    }
}