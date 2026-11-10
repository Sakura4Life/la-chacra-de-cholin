using System.Collections.Generic;
using UnityEngine;

public class Resaltado : MonoBehaviour
{
    [SerializeField] Color colorBrillo = new Color(1f, 0.92f, 0.2f);
    [SerializeField] float velocidad = 3f;
    [SerializeField, Range(0f, 1f)] float intensidad = 0.8f;

    // Propiedades de color posibles: URP Lit, glTFast (.glb) y shaders antiguos.
    static readonly int[] IdsColor =
    {
        Shader.PropertyToID("_BaseColor"),
        Shader.PropertyToID("baseColorFactor"),
        Shader.PropertyToID("_Color"),
    };

    struct Ranura
    {
        public Renderer renderer;
        public int material;
        public int id;
        public Color original;
    }

    readonly List<Ranura> ranuras = new List<Ranura>();
    MaterialPropertyBlock bloque;

    void Awake()
    {
        bloque = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var materiales = r.sharedMaterials;
            for (int i = 0; i < materiales.Length; i++)
            {
                var mat = materiales[i];
                if (mat == null) continue;

                foreach (int id in IdsColor)
                {
                    if (!mat.HasProperty(id)) continue;
                    ranuras.Add(new Ranura
                    {
                        renderer = r,
                        material = i,
                        id = id,
                        original = mat.GetColor(id)
                    });
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
            foreach (var s in ranuras) s.renderer.SetPropertyBlock(null, s.material);
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * velocidad) + 1f) * 0.5f * intensidad;
        foreach (var s in ranuras)
        {
            bloque.Clear();
            bloque.SetColor(s.id, Color.Lerp(s.original, colorBrillo, t));
            s.renderer.SetPropertyBlock(bloque, s.material);
        }
    }
}