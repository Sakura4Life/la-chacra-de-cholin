using UnityEngine;
using UnityEngine.Rendering;

// El modelo de la lana, hecho con las esferas de Unity (no hay modelo 3D): un copo de varias bolas blancas.
// Lo usan el icono que gira sobre el corral de llamas y la caja de pedidos. Plantilla() devuelve un objeto
// de muestra, escondido bajo el mapa, que se copia con ModeloUtil.CrearNormalizado como cualquier otro modelo.
public static class ModeloLana
{
    static GameObject plantilla;

    public static GameObject Plantilla()
    {
        if (plantilla != null) return plantilla;

        var raiz = new GameObject("ModeloLana (plantilla)");
        raiz.hideFlags = HideFlags.HideInHierarchy;
        raiz.transform.position = new Vector3(0f, -9000f, 0f);   // muy por debajo de la granja: nadie la ve
        Object.DontDestroyOnLoad(raiz);

        Material material = CrearMaterial();
        // (posición del centro, diámetro) en unidades del copo; la base queda a ras de y = 0.
        Esfera(raiz.transform, material, new Vector3(0.00f, 0.50f, 0.00f), 1.00f);
        Esfera(raiz.transform, material, new Vector3(0.42f, 0.40f, 0.10f), 0.70f);
        Esfera(raiz.transform, material, new Vector3(-0.40f, 0.42f, 0.12f), 0.70f);
        Esfera(raiz.transform, material, new Vector3(0.10f, 0.40f, -0.40f), 0.65f);
        Esfera(raiz.transform, material, new Vector3(0.05f, 0.32f, 0.40f), 0.60f);
        Esfera(raiz.transform, material, new Vector3(-0.08f, 0.88f, 0.15f), 0.60f);

        plantilla = raiz;
        return plantilla;
    }

    static void Esfera(Transform padre, Material material, Vector3 centro, float diametro)
    {
        var bola = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var colision = bola.GetComponent<Collider>();
        if (colision != null) Object.Destroy(colision);
        bola.GetComponent<Renderer>().sharedMaterial = material;
        bola.transform.SetParent(padre, false);
        bola.transform.localPosition = centro;
        bola.transform.localScale = Vector3.one * diametro;
    }

    // Material blanco. Primero prueba el material por defecto de URP; si no es de URP (saldría rosa), busca un shader de URP por nombre.
    static Material CrearMaterial()
    {
        Material origen = null;
        var rp = GraphicsSettings.currentRenderPipeline;
        if (rp != null && rp.defaultMaterial != null && EsUrp(rp.defaultMaterial.shader)) origen = rp.defaultMaterial;

        Material material = null;
        if (origen != null)
        {
            material = new Material(origen);
        }
        else
        {
            string[] nombres = { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Unlit", "Sprites/Default", "Standard" };
            foreach (var nombre in nombres)
            {
                var shader = Shader.Find(nombre);
                if (shader != null) { material = new Material(shader); break; }
            }
            if (material == null) material = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        Color blanco = Color.white;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", blanco);
        if (material.HasProperty("_Color")) material.SetColor("_Color", blanco);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        return material;
    }

    static bool EsUrp(Shader shader)
    {
        return shader != null && shader.name.StartsWith("Universal Render Pipeline");
    }
}
