using UnityEngine;

// Anima por código al objeto "Visual" (padre de los modelos): respirar, caminar, picotear...
public class AnimacionAve : MonoBehaviour
{
    public enum Anim { Reposo, Caminar, Picotear, Colgado, Temblar }

    [Header("Objeto que se anima")]
    [Tooltip("Vacío 'Visual', padre de los modelos. Debe estar en Position 0,0,0 y Scale 1,1,1.")]
    [SerializeField] Transform visual;

    [Header("Reposo")]
    [SerializeField] float respiracion = 0.03f;
    [SerializeField] float velocidadRespiracion = 2.5f;

    [Header("Caminar")]
    [SerializeField] float alturaPaso = 0.012f;
    [SerializeField] float frecuenciaPaso = 8f;
    [SerializeField] float balanceo = 8f;

    [Header("Picotear")]
    [SerializeField] float inclinacionPico = 40f;
    [SerializeField] float frecuenciaPico = 6f;

    [Header("Saltito")]
    [SerializeField] float alturaSalto = 0.03f;
    [SerializeField] float duracionSalto = 0.35f;

    Anim actual = Anim.Reposo;
    float inicioSalto = -100f;
    float inicioPulso = -100f;
    float desfase;

    void Awake() { desfase = Random.Range(0f, 10f); }

    public void Poner(Anim animacion) { actual = animacion; }
    public void Saltar() { inicioSalto = Time.time; }
    public void Pulso() { inicioPulso = Time.time; }

    void Update()
    {
        if (visual == null) return;

        float t = Time.time + desfase;
        float y = 0f, pitch = 0f, roll = 0f, ancho = 1f, alto = 1f;

        switch (actual)
        {
            case Anim.Reposo:
                {
                    float r = Mathf.Sin(t * velocidadRespiracion);
                    alto = 1f + respiracion * r;
                    ancho = 1f - respiracion * 0.5f * r;
                    roll = Mathf.Sin(t * 0.8f) * 2f;
                    break;
                }
            case Anim.Caminar:
                {
                    float onda = Mathf.Sin(t * frecuenciaPaso);
                    float paso = Mathf.Abs(onda);
                    y = paso * alturaPaso;
                    roll = onda * balanceo;
                    alto = 1f - 0.06f * (1f - paso);
                    ancho = 1f + 0.03f * (1f - paso);
                    break;
                }
            case Anim.Picotear:
                {
                    float p = Mathf.Max(0f, Mathf.Sin(t * frecuenciaPico));
                    pitch = inclinacionPico * p * p;
                    break;
                }
            case Anim.Colgado:
                {
                    roll = Mathf.Sin(t * 12f) * 15f;
                    pitch = Mathf.Sin(t * 9f) * 10f;
                    alto = 1f + 0.05f * Mathf.Sin(t * 10f);
                    break;
                }
            case Anim.Temblar:
                {
                    roll = Mathf.Sin(t * 45f) * 12f;
                    y = Mathf.Abs(Mathf.Sin(t * 14f)) * alturaPaso * 1.5f;
                    alto = 1f + 0.12f * Mathf.Sin(t * 14f);
                    break;
                }
        }

        // Saltito puntual
        float ts = Time.time - inicioSalto;
        if (ts >= 0f && ts < duracionSalto)
        {
            float k = Mathf.Sin(ts / duracionSalto * Mathf.PI);
            y += k * alturaSalto;
            alto *= 1f + 0.08f * k;
        }

        // Pulso puntual (al piar o al transformarse)
        float tp = Time.time - inicioPulso;
        if (tp >= 0f && tp < 0.25f)
        {
            float k = Mathf.Sin(tp / 0.25f * Mathf.PI);
            alto *= 1f + 0.25f * k;
            ancho *= 1f - 0.12f * k;
        }

        visual.localPosition = new Vector3(0f, y, 0f);
        visual.localRotation = Quaternion.Euler(pitch, 0f, roll);
        visual.localScale = new Vector3(ancho, alto, ancho);
    }
}