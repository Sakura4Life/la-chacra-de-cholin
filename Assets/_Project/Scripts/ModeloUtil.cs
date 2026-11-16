using UnityEngine;

// Utilidades para modelos creados por código (animales, iconos) y áreas de toque.
public static class ModeloUtil
{
    // Crea el modelo dentro de un contenedor cuyo origen es el centro de su base y le da el tamaño pedido
    // (en unidades de granja: se compensa la escala del padre).
    // 'correccion' endereza modelos exportados con otro eje de arriba (ver AnimalDatos.Correccion).
    public static Transform CrearNormalizado(GameObject prefab, Transform padre, float tamano, string nombre,
        Quaternion? correccion = null)
    {
        var contenedor = new GameObject(nombre).transform;
        contenedor.SetParent(padre, false);

        var go = Object.Instantiate(prefab, contenedor, false);
        foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = correccion ?? Quaternion.identity;
        go.transform.localScale = Vector3.one;

        if (Limites(go, out Bounds b))
        {
            float extension = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (extension > 1e-6f)
                go.transform.localScale = Vector3.one * (tamano * padre.lossyScale.x / extension);

            if (Limites(go, out b))
            {
                Vector3 baseCentro = new Vector3(b.center.x, b.min.y, b.center.z);
                go.transform.position += contenedor.position - baseCentro;
            }
        }
        return contenedor;
    }

    // Límites en el espacio del mundo de todos los renderers.
    public static bool Limites(GameObject go, out Bounds b)
    {
        b = new Bounds();
        bool primero = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (primero) { b = r.bounds; primero = false; }
            else b.Encapsulate(r.bounds);
        }
        return !primero;
    }

    // Límites en el espacio local de 'raiz' (sirven aunque el objeto esté girado).
    public static bool LimitesLocales(Transform raiz, out Bounds local)
    {
        local = new Bounds();
        bool primero = true;
        foreach (var r in raiz.GetComponentsInChildren<Renderer>())
        {
            Bounds b = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var esquina = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                Vector3 p = raiz.InverseTransformPoint(esquina);
                if (primero) { local = new Bounds(p, Vector3.zero); primero = false; }
                else local.Encapsulate(p);
            }
        }
        return !primero;
    }

    // Si el objeto no tiene ningún collider (ni en sus hijos), le pone un Box Collider del tamaño de su modelo.
    public static void AsegurarCollider(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() != null) return;
        if (!LimitesLocales(go.transform, out Bounds local)) return;
        var caja = go.AddComponent<BoxCollider>();
        caja.center = local.center;
        caja.size = local.size;
    }
}
