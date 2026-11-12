using UnityEngine;

// Una parcela: agrupa sus huecos de cultivo.
// - Sembrar y regar: el jugador toca la tierra y la acción va al hueco más cercano al toque
//   que esté libre (siembra) o con semilla sin regar (riega).
// - Regar y cosechar también se pueden hacer tocando la planta (cada hueco ocupado tiene su collider).
// Necesita en este mismo objeto: Interactuable (Tipo Terreno) y un Box Collider sobre la tierra.
public class Terreno : MonoBehaviour
{
    [Header("Solo para el menú 'Ordenar huecos en cuadrícula'")]
    [SerializeField] int columnas = 5;
    [Tooltip("Separación entre columnas, en unidades de granja.")]
    [SerializeField] float separacionX = 0.075f;
    [Tooltip("Separación entre filas, en unidades de granja.")]
    [SerializeField] float separacionZ = 0.12f;

    Parcela[] huecos;

    void Awake()
    {
        huecos = GetComponentsInChildren<Parcela>(true);
    }

    // Lo llama Interactuable cuando el toque cae sobre la tierra (no sobre una planta).
    public void Tocar(Vector3 punto)
    {
        if (huecos == null || huecos.Length == 0) return;

        Vector3 local = transform.InverseTransformPoint(punto);
        Parcela mejor = null;
        Parcela cerrada = null;
        float mejorDistancia = float.MaxValue;

        foreach (var hueco in huecos)
        {
            if (hueco == null) continue;
            if (hueco.Bloqueada) cerrada = hueco;
            // Sembrar (hueco libre) o regar (semilla sin regar): gana el más cercano al toque.
            if (!hueco.Libre && !hueco.SinRegar) continue;

            Vector3 p = transform.InverseTransformPoint(hueco.transform.position);
            float dx = p.x - local.x;
            float dz = p.z - local.z;
            float distancia = dx * dx + dz * dz;
            if (distancia < mejorDistancia)
            {
                mejorDistancia = distancia;
                mejor = hueco;
            }
        }

        if (mejor != null) { mejor.Tocar(); return; }          // siembra o riega el más cercano
        if (cerrada != null) { cerrada.Tocar(); return; }      // da el aviso de "huerto cerrado"

        Avisar("No hay nada que sembrar ni regar. Toca las plantas listas para cosecharlas.");
    }

    [ContextMenu("Ordenar huecos en cuadrícula")]
    void Ordenar()
    {
#if UNITY_EDITOR
        if (!UnityEditor.EditorUtility.DisplayDialog(
                "Ordenar huecos",
                "Esto recoloca TODOS los huecos en una cuadrícula y pierde las posiciones que pusiste a mano. ¿Continuar?",
                "Ordenar", "Cancelar"))
            return;
#endif
        var lista = GetComponentsInChildren<Parcela>(true);
        if (lista.Length == 0) return;

        int cols = Mathf.Max(1, columnas);
        int filas = Mathf.CeilToInt(lista.Length / (float)cols);
        float y = lista[0].transform.localPosition.y;

        for (int i = 0; i < lista.Length; i++)
        {
            int c = i % cols;
            int f = i / cols;
            int enFila = Mathf.Min(cols, lista.Length - f * cols);

            float x = (c - (enFila - 1) * 0.5f) * separacionX;
            float z = (f - (filas - 1) * 0.5f) * separacionZ;

            var t = lista[i].transform;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(t, "Ordenar huecos");
            UnityEditor.Undo.RecordObject(lista[i].gameObject, "Ordenar huecos");
#endif
            t.localPosition = new Vector3(x, y, z);
            lista[i].gameObject.name = $"Hueco{i + 1:00}";
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(t);
#endif
        }
    }

    static void Avisar(string texto)
    {
        if (TutorialController.Instance != null) TutorialController.Instance.Avisar(texto);
    }
}
