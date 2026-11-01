using System.Collections.Generic;
using UnityEngine;

public class Resaltado : MonoBehaviour
{
    [SerializeField] Color colorBrillo = new Color(1f, 0.92f, 0.2f);
    [SerializeField] float velocidad = 3f;
    [SerializeField, Range(0f, 1f)] float intensidad = 0.8f;

    // Nombres posibles de la propiedad de color, en orden de prioridad:
    // URP Lit, glTFast (modelos .glb) y shaders antiguos.
    static readonly int[] IdsColor =
    {
        Shader.PropertyToID("_BaseColor"),
        Shader.PropertyToID("baseColorFactor"),
        Shader.PropertyToID("_Color"),
    };

    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<int> ids = new List<int>();
    readonly List<Color> originales = new List<Color>();
    MaterialPropertyBlock bloque;

    void Awake()
    {
        bloque = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var mat = r.sharedMaterial;
            if (mat == null) continue;

            foreach (int id in IdsColor)
            {
                if (mat.HasProperty(id))
                {
                    renderers.Add(r);
                    ids.Add(id);
                    originales.Add(mat.GetColor(id));
                    break;
                }
            }
        }
        enabled = false;
    }

    public void Activar(bool valor)
    {
        enabled = valor;
        if (!valor)
            foreach (var r in renderers) r.SetPropertyBlock(null);
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * velocidad) + 1f) * 0.5f * intensidad;
        for (int i = 0; i < renderers.Count; i++)
        {
            bloque.Clear();
            bloque.SetColor(ids[i], Color.Lerp(originales[i], colorBrillo, t));
            renderers[i].SetPropertyBlock(bloque);
        }
    }
}