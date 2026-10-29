using System.Collections.Generic;
using UnityEngine;

public class Resaltado : MonoBehaviour
{
    [SerializeField] Color colorBrillo = new Color(1f, 0.92f, 0.2f);
    [SerializeField] float velocidad = 3f;
    [SerializeField, Range(0f, 1f)] float intensidad = 0.8f;

    static readonly int IdColor = Shader.PropertyToID("_BaseColor");

    readonly List<Renderer> renderers = new List<Renderer>();
    readonly List<Color> originales = new List<Color>();
    MaterialPropertyBlock bloque;

    void Awake()
    {
        bloque = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(IdColor))
            {
                renderers.Add(r);
                originales.Add(r.sharedMaterial.GetColor(IdColor));
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
            bloque.SetColor(IdColor, Color.Lerp(originales[i], colorBrillo, t));
            renderers[i].SetPropertyBlock(bloque);
        }
    }
}