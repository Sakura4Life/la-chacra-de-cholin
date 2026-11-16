using UnityEngine;
using UnityEngine.Rendering;

// Cambia el color de un animal creado por código (el cuy mostaza). Se pinta solo la pieza más grande del modelo
// (el cuerpo), así los ojos y la nariz conservan su color.
public static class VarianteVisual
{
    static readonly string[] NombresColor = { "baseColorFactor", "_BaseColor", "_Color" };

    public static void Colorear(Transform raiz, Color color)
    {
        Renderer cuerpo = null;
        int mejor = -1;
        foreach (var r in raiz.GetComponentsInChildren<Renderer>())
        {
            int n = Vertices(r);
            if (n > mejor) { mejor = n; cuerpo = r; }
        }
        if (cuerpo == null) return;

        // 'materials' entrega copias propias de este animal: no se tocan las de los demás.
        var materiales = cuerpo.materials;
        bool alguno = false;
        for (int i = 0; i < materiales.Length; i++)
            if (Pintar(materiales[i], color)) alguno = true;
        cuerpo.materials = materiales;

        if (!alguno)
            Debug.LogWarning("VarianteVisual: no encontré la propiedad de color del material de '" + raiz.name + "'.");
    }

    static int Vertices(Renderer r)
    {
        var skin = r as SkinnedMeshRenderer;
        if (skin != null && skin.sharedMesh != null) return skin.sharedMesh.vertexCount;
        var filtro = r.GetComponent<MeshFilter>();
        return filtro != null && filtro.sharedMesh != null ? filtro.sharedMesh.vertexCount : 0;
    }

    static bool Pintar(Material m, Color color)
    {
        if (m == null) return false;
        foreach (var nombre in NombresColor)
        {
            if (m.HasProperty(nombre)) { m.SetColor(nombre, color); return true; }
        }

        // Plan B: la primera propiedad de tipo color que se llame "color" (sin emisión ni brillo).
        var sombreador = m.shader;
        for (int i = 0; i < sombreador.GetPropertyCount(); i++)
        {
            if (sombreador.GetPropertyType(i) != ShaderPropertyType.Color) continue;
            string nombre = sombreador.GetPropertyName(i);
            string bajo = nombre.ToLowerInvariant();
            if (bajo.Contains("color") && !bajo.Contains("emis") && !bajo.Contains("spec"))
            {
                m.SetColor(nombre, color);
                return true;
            }
        }
        return false;
    }
}
