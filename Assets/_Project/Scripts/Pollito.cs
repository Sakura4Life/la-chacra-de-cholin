using UnityEngine;

public class Pollito : MonoBehaviour
{
    [Header("Cuerpo")]
    [SerializeField] Renderer cuerpo;
    [SerializeField] Collider colision;

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

    static readonly int IdColor = Shader.PropertyToID("_BaseColor");

    AudioSource fuente;
    Transform camara;
    float proximoPiar;

    void Awake()
    {
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f;                      // sonido 3D
        fuente.rolloffMode = AudioRolloffMode.Linear;
        fuente.minDistance = 0.2f;
        fuente.maxDistance = 4f;

        if (clipPiar == null) clipPiar = CrearPioSintetico();

        var bloque = new MaterialPropertyBlock();
        bloque.SetColor(IdColor, new Color(1f, 0.9f, 0.2f));
        cuerpo.SetPropertyBlock(bloque);

        cuerpo.enabled = false;
        colision.enabled = false;
        plumas.SetActive(false);
    }

    void Update()
    {
        var mision = MisionController.Instance;
        bool buscando = mision != null && mision.PollitoBuscandose;
        bool visible = buscando || (mision != null && mision.PollitoYaEncontrado);

        if (visible != cuerpo.enabled)
        {
            cuerpo.enabled = visible;
            colision.enabled = visible;
        }

        if (!buscando)
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
            proximoPiar = Time.time + Mathf.Lerp(intervaloLejos, intervaloCerca, cercania);
        }
    }

    public void Tocar()
    {
        if (MisionController.Instance == null || !MisionController.Instance.PollitoBuscandose) return;
        MisionController.Instance.AlEncontrarPollito();
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