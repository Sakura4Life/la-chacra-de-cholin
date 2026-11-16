using UnityEngine;

// Corazoncito que sube y se desvanece cuando dos animales se emparejan. Se arma con tres piezas simples
// (dos esferas y un cubo girado) y siempre mira a la cámara. Se destruye solo.
public class CorazonVisual : MonoBehaviour
{
    static Material plantilla;

    Material material;
    Vector3 inicio;
    float altura;
    float tamanoMundo;
    float retraso;
    float nacimiento;
    const float Duracion = 1.6f;
    Camera camara;

    // Toma un material que ya funcione en esta granja: el material por defecto de las piezas creadas
    // por código sale rosado en el teléfono (igual que con el cartel de "En venta").
    static Material BuscarPlantilla(Transform raiz)
    {
        if (plantilla != null) return plantilla;
        foreach (var r in raiz.root.GetComponentsInChildren<Renderer>(true))
        {
            var m = r.sharedMaterial;
            if (m != null && m.shader != null && m.HasProperty("_BaseColor") && !(r is ParticleSystemRenderer))
            {
                plantilla = m;
                return m;
            }
        }
        return null;
    }

    // 'posicion' en el mundo; 'tamano' = ancho del corazón y 'altura' = cuánto sube, ambos en unidades del mundo.
    public static void Lanzar(Transform padre, Vector3 posicion, int cantidad, float tamano, float altura)
    {
        var mat = BuscarPlantilla(padre);
        var cam = Camera.main;
        Vector3 derecha = cam != null ? cam.transform.right : padre.right;

        for (int i = 0; i < cantidad; i++)
        {
            float lado = (i - (cantidad - 1) * 0.5f) * tamano * 1.1f;
            var go = new GameObject("Corazon");
            go.transform.SetParent(padre, true);
            var c = go.AddComponent<CorazonVisual>();
            c.Construir(mat, posicion + derecha * lado, tamano, altura, i * 0.18f);
        }
    }

    void Construir(Material plant, Vector3 posicion, float tamano, float alturaMundo, float espera)
    {
        inicio = posicion;
        altura = alturaMundo;
        tamanoMundo = tamano;
        retraso = espera;
        nacimiento = Time.time;
        transform.position = posicion;

        Color rosa = new Color(0.95f, 0.22f, 0.40f);
        if (plant != null)
        {
            material = new Material(plant);
            material.SetColor("_BaseColor", rosa);
            if (material.HasProperty("_Color")) material.SetColor("_Color", rosa);
        }

        // Corazón: un cubo girado 45° (la punta de abajo) y dos esferas sobre sus lados de arriba.
        float r = 1f / (2f * Mathf.Sqrt(2f));
        Pieza(PrimitiveType.Sphere, new Vector3(-r, r, 0f), new Vector3(1f, 1f, 0.5f), Quaternion.identity, rosa);
        Pieza(PrimitiveType.Sphere, new Vector3(r, r, 0f), new Vector3(1f, 1f, 0.5f), Quaternion.identity, rosa);
        Pieza(PrimitiveType.Cube, Vector3.zero, new Vector3(1f, 1f, 0.5f), Quaternion.Euler(0f, 0f, 45f), rosa);

        Aplicar(0f);
    }

    void Pieza(PrimitiveType tipo, Vector3 posicion, Vector3 escala, Quaternion giro, Color color)
    {
        var go = GameObject.CreatePrimitive(tipo);
        go.name = "Pieza";
        go.transform.SetParent(transform, false);
        go.transform.localPosition = posicion;
        go.transform.localRotation = giro;
        go.transform.localScale = escala;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var rend = go.GetComponent<Renderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (material != null) rend.sharedMaterial = material;
        else rend.material.color = color;
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }

    void LateUpdate()
    {
        float t = Time.time - nacimiento - retraso;
        if (t >= Duracion) { Destroy(gameObject); return; }
        Aplicar(Mathf.Max(0f, t));

        if (camara == null) camara = Camera.main;
        if (camara != null)
            transform.rotation = Quaternion.LookRotation(transform.position - camara.transform.position, camara.transform.up);
    }

    // Sube, aparece con un pequeño rebote y se encoge al final.
    void Aplicar(float t)
    {
        float k = Mathf.Clamp01(t / Duracion);
        float subida = 1f - (1f - k) * (1f - k);
        transform.position = inicio + Vector3.up * (altura * subida);

        float escala;
        if (t <= 0f) escala = 0f;
        else if (k < 0.15f)
        {
            float a = k / 0.15f - 1f;
            escala = 1f + 2.70158f * a * a * a + 1.70158f * a * a;     // easeOutBack
        }
        else if (k > 0.65f) escala = Mathf.Clamp01(1f - (k - 0.65f) / 0.35f);
        else escala = 1f;

        // El corazón mide 1,7 unidades de ancho con piezas de lado 1: se ajusta al tamaño pedido.
        float lado = tamanoMundo / 1.707f * escala;
        Transform padre = transform.parent;
        float compensa = padre != null ? padre.lossyScale.x : 1f;
        transform.localScale = Vector3.one * (lado / Mathf.Max(1e-6f, compensa));
    }
}
