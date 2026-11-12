using System;
using UnityEngine;

// Catálogo visual de los cultivos: qué modelo se ve en cada etapa y dónde se coloca dentro del hueco.
// Se pone UNA vez en el objeto Terreno1 (o en la raíz de la granja). Todos los huecos lo leen
// y crean sus hijos por código, así que ya no hay que colocar modelos a mano en cada hueco.
// Posición, rotación y escala son locales al hueco (igual que cuando los ponías como hijos).
public class CatalogoVisual : MonoBehaviour
{
    [Serializable]
    public class Etapa
    {
        public GameObject modelo;
        public Vector3 posicion;
        [Tooltip("Rotación en grados (Euler).")]
        public Vector3 rotacion;
        public Vector3 escala = Vector3.one;
    }

    [Serializable]
    public class VisualCultivo
    {
        [Tooltip("Solo es una etiqueta para reconocerlo en el Inspector.")]
        public string nombre;
        [Tooltip("Se ve al sembrar, antes de regar.")]
        public Etapa semilla = new Etapa();
        [Tooltip("Etapa 1: primera mitad del crecimiento.")]
        public Etapa brote = new Etapa();
        [Tooltip("Etapa 2: segunda mitad del crecimiento.")]
        public Etapa joven = new Etapa();
        [Tooltip("Etapa 3: lista para cosechar.")]
        public Etapa madura = new Etapa();
    }

    // Un elemento por cultivo, en el orden del enum Cultivo: 0 Tomate, 1 Maiz, 2 Arroz, 3 Papa.
    [SerializeField] VisualCultivo[] cultivos = new VisualCultivo[4];

    public VisualCultivo De(Cultivo c)
    {
        int i = (int)c;
        return cultivos != null && i >= 0 && i < cultivos.Length ? cultivos[i] : null;
    }

    // Crea el modelo de una etapa como hijo del hueco, apagado. Devuelve null si la etapa no tiene modelo.
    public static Transform Crear(Etapa e, Transform padre, string nombre)
    {
        if (e == null || e.modelo == null) return null;
        GameObject go = Instantiate(e.modelo, padre, false);
        go.name = nombre;
        Transform t = go.transform;
        t.localPosition = e.posicion;
        t.localRotation = Quaternion.Euler(e.rotacion);
        t.localScale = e.escala;
        go.SetActive(false);
        return t;
    }

#if UNITY_EDITOR
    const string Ruta = "Assets/_Project/Modelos/Cultivos/";

    [ContextMenu("Cargar valores por defecto")]
    void CargarPorDefecto()
    {
        if (!UnityEditor.EditorUtility.DisplayDialog(
                "Cargar valores por defecto",
                "Esto reemplaza TODOS los modelos, posiciones y escalas del catálogo por los valores iniciales. ¿Continuar?",
                "Cargar", "Cancelar"))
            return;

        UnityEditor.Undo.RecordObject(this, "Cargar valores por defecto");

        // Posición de las etapas de planta dentro del hueco (la que ya usabas con el tomate).
        var posPlanta = new Vector3(-0.0007451698f, -0.0030204528f, 0.0055f);
        // Semilla del tomate y el arroz (modelo compartido).
        var posSemilla = new Vector3(-0.00058011664f, 0.005842954f, 0.0048000664f);
        var rotSemilla = new Vector3(73.622f, -137.968f, -138.287f);

        cultivos = new VisualCultivo[4];

        cultivos[(int)Cultivo.Tomate] = new VisualCultivo
        {
            nombre = "Tomate",
            semilla = E("semillaTomateArroz.glb", posSemilla, rotSemilla, Vector3.one * 0.4f),
            brote = E("Tomate/TomateF1.glb", posPlanta, Vector3.zero, Vector3.one * 0.35f),
            joven = E("Tomate/TomateF2.glb", posPlanta, Vector3.zero, new Vector3(0.2f, 0.16f, 0.2f)),
            madura = E("Tomate/TomateF3.glb", posPlanta, Vector3.zero, new Vector3(0.2f, 0.16f, 0.2f)),
        };

        // El choclo usa dos modelos que generé a partir de chocloCultivo: verde (sin mazorca) y maduro (con mazorca).
        cultivos[(int)Cultivo.Maiz] = new VisualCultivo
        {
            nombre = "Choclo",
            semilla = E("Choclo/chocloSemilla.glb", new Vector3(0f, 0.004f, 0f), new Vector3(90f, 0f, 0f), Vector3.one * 0.4f),
            brote = E("Choclo/ChocloVerde.glb", posPlanta, Vector3.zero, Vector3.one * 0.03f),
            joven = E("Choclo/ChocloVerde.glb", posPlanta, Vector3.zero, Vector3.one * 0.06f),
            madura = E("Choclo/ChocloMadura.glb", posPlanta, Vector3.zero, Vector3.one * 0.10f),
        };

        cultivos[(int)Cultivo.Arroz] = new VisualCultivo
        {
            nombre = "Arroz",
            semilla = E("semillaTomateArroz.glb", posSemilla, rotSemilla, Vector3.one * 0.4f),
            brote = E("Arroz/ArrozF1.glb", posPlanta, Vector3.zero, Vector3.one * 0.22f),
            joven = E("Arroz/ArrozF2.glb", posPlanta, Vector3.zero, Vector3.one * 0.17f),
            madura = E("Arroz/ArrozF3.glb", posPlanta, Vector3.zero, Vector3.one * 0.17f),
        };

        cultivos[(int)Cultivo.Papa] = new VisualCultivo
        {
            nombre = "Papa",
            semilla = E("Papa/PapaSemilla.glb", new Vector3(0f, 0.007f, 0f), Vector3.zero, Vector3.one * 0.04f),
            brote = E("Papa/PapaF1.glb", posPlanta, Vector3.zero, Vector3.one * 0.30f),
            joven = E("Papa/PapaF2.glb", posPlanta, Vector3.zero, Vector3.one * 0.17f),
            madura = E("Papa/PapaF3.glb", posPlanta, Vector3.zero, Vector3.one * 0.17f),
        };

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[CatalogoVisual] Valores por defecto cargados. Si algún modelo salió vacío, revisa los avisos amarillos.");
    }

    static Etapa E(string archivo, Vector3 posicion, Vector3 rotacion, Vector3 escala)
    {
        var modelo = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Ruta + archivo);
        if (modelo == null) Debug.LogWarning("[CatalogoVisual] No encuentro el modelo: " + Ruta + archivo);
        return new Etapa { modelo = modelo, posicion = posicion, rotacion = rotacion, escala = escala };
    }
#endif
}
