using TMPro;
using UnityEngine;

// Cartel de "En venta" de una parcela bloqueada. Lo crea Terreno por código (poste, tablero y texto)
// y siempre se gira hacia la cámara. Se destruye al comprar la parcela.
public class CartelTerreno : MonoBehaviour
{
    Camera camara;
    static Material plantilla;

    // Toma un material que ya funcione en esta granja (URP Lit u otro con color base),
    // porque el material por defecto de los cubos creados por código sale rosado en el teléfono.
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

    public static GameObject Crear(Transform padre, int precio, float altura, float escala)
    {
        var raiz = new GameObject("Cartel");
        raiz.transform.SetParent(padre, false);
        raiz.transform.localPosition = new Vector3(0f, altura, 0f);
        raiz.transform.localScale = Vector3.one * Mathf.Max(0.1f, escala);
        var plantillaMaterial = BuscarPlantilla(padre);
        raiz.AddComponent<CartelTerreno>().Construir(precio, plantillaMaterial);
        return raiz;
    }

    void Construir(int precio, Material material)
    {
        Pieza("Poste", new Vector3(0f, 0.025f, 0f), new Vector3(0.012f, 0.05f, 0.008f), new Color(0.36f, 0.22f, 0.10f), material);
        Pieza("Tablero", new Vector3(0f, 0.0825f, 0f), new Vector3(0.12f, 0.065f, 0.008f), new Color(0.96f, 0.90f, 0.74f), material);

        var go = new GameObject("Texto");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.0825f, -0.0052f);
        var texto = go.AddComponent<TextMeshPro>();
        texto.text = "EN VENTA\n<b>" + precio + " monedas</b>";
        texto.alignment = TextAlignmentOptions.Center;
        texto.color = new Color(0.20f, 0.10f, 0.04f);
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.enableAutoSizing = true;
        texto.fontSizeMin = 0.05f;
        texto.fontSizeMax = 0.5f;
        texto.fontSize = 0.3f;
        texto.rectTransform.sizeDelta = new Vector2(0.11f, 0.055f);
    }

    void Pieza(string nombre, Vector3 posicion, Vector3 escala, Color color, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = posicion;
        go.transform.localScale = escala;
        var colisionador = go.GetComponent<Collider>();
        if (colisionador != null) Destroy(colisionador);
        var renderer = go.GetComponent<Renderer>();
        if (material != null)
        {
            var nuevo = new Material(material);
            nuevo.SetColor("_BaseColor", color);
            if (nuevo.HasProperty("_Color")) nuevo.SetColor("_Color", color);
            renderer.sharedMaterial = nuevo;
        }
        else
        {
            renderer.material.color = color;
        }
    }

    // El texto se lee desde -Z: el cartel mira hacia la cámara (solo giro horizontal).
    void LateUpdate()
    {
        if (camara == null) camara = Camera.main;
        if (camara == null) return;
        Vector3 d = transform.position - camara.transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.00001f) return;
        transform.rotation = Quaternion.LookRotation(d, Vector3.up);
    }
}
