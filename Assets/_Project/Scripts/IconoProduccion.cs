using TMPro;
using UnityEngine;

// Icono flotante sobre un corral con lo que ya se puede recoger (huevos). Tocarlo recoge.
public class IconoProduccion : MonoBehaviour
{
    TextMeshPro texto;
    Transform modelo;
    Vector3 posicionModelo;
    float tamano;
    Camera camara;

    public static IconoProduccion Crear(Corral corral, GameObject prefab, float tamano, Transform punto, Vector3 desplazamiento)
    {
        var contenedor = ModeloUtil.CrearNormalizado(prefab, corral.transform, tamano, "IconoProduccion");
        contenedor.position = punto.TransformPoint(desplazamiento);

        var icono = contenedor.gameObject.AddComponent<IconoProduccion>();
        icono.Construir(tamano);

        var colision = contenedor.gameObject.AddComponent<SphereCollider>();
        colision.radius = tamano * 1.8f;
        colision.center = new Vector3(0f, tamano * 0.5f, 0f);
        Interactuable.Crear(contenedor.gameObject, Interactuable.Tipo.Corral);
        return icono;
    }

    void Construir(float tam)
    {
        tamano = tam;
        modelo = transform.childCount > 0 ? transform.GetChild(0) : transform;
        posicionModelo = modelo.localPosition;

        var go = new GameObject("Cantidad");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, tamano * 1.7f, 0f);
        texto = go.AddComponent<TextMeshPro>();
        texto.alignment = TextAlignmentOptions.Center;
        texto.fontSize = Mathf.Max(0.05f, tamano * 10f);
        texto.color = Color.white;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.rectTransform.sizeDelta = new Vector2(tamano * 6f, tamano * 3f);
        texto.outlineWidth = 0.25f;
        texto.outlineColor = Color.black;
    }

    public void Fijar(int cantidad)
    {
        if (texto != null) texto.text = cantidad > 0 ? "x" + cantidad : "";
    }

    void LateUpdate()
    {
        if (modelo != null)
        {
            modelo.localPosition = posicionModelo + Vector3.up * (Mathf.Sin(Time.time * 3f) * tamano * 0.15f);
            modelo.Rotate(0f, 70f * Time.deltaTime, 0f, Space.Self);
        }

        if (texto == null) return;
        if (camara == null) camara = Camera.main;
        if (camara == null) return;
        Vector3 d = texto.transform.position - camara.transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.00001f)
            texto.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
    }
}
