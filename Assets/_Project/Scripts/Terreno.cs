using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Estado de una parcela para la partida guardada.
[Serializable]
public class DatosTerreno
{
    public int id;
    public long ticks;     // hora real del guardado (para calcular cuánto creció mientras estaba cerrado)
    public DatosHueco[] huecos = new DatosHueco[0];
}

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

    [Header("Guardado")]
    [Tooltip("Número único de esta parcela (la primera es 0). Las parcelas compradas usarán 1, 2 y 3.")]
    [SerializeField] int idTerreno;

    Parcela[] huecos;

    // Parcelas vivas y datos cargados esperando a que su parcela exista (la granja se coloca después de cargar).
    static readonly List<Terreno> activos = new List<Terreno>();
    static readonly Dictionary<int, DatosTerreno> pendientes = new Dictionary<int, DatosTerreno>();

    void Awake()
    {
        huecos = GetComponentsInChildren<Parcela>(true);
        activos.Add(this);
    }

    // Espera un fotograma para que los huecos ya hayan hecho su Start (saber si hay llave).
    IEnumerator Start()
    {
        yield return null;
        if (pendientes.TryGetValue(idTerreno, out DatosTerreno datos))
        {
            pendientes.Remove(idTerreno);
            Aplicar(datos);
        }
    }

    void OnDestroy() { activos.Remove(this); }

    // ---------- Guardado ----------

    public static void Limpiar()
    {
        pendientes.Clear();
        activos.Clear();
    }

    // La llama Guardado al cargar.
    public static void Preparar(DatosTerreno[] datos)
    {
        pendientes.Clear();
        if (datos == null) return;
        foreach (var d in datos)
        {
            if (d == null) continue;
            var terreno = activos.Find(t => t.idTerreno == d.id);
            if (terreno != null) terreno.Aplicar(d);
            else pendientes[d.id] = d;
        }
    }

    public static DatosTerreno[] ExportarTodos()
    {
        var lista = new List<DatosTerreno>();
        var vistos = new HashSet<int>();
        foreach (var t in activos)
        {
            if (t == null) continue;
            lista.Add(t.Crear());
            vistos.Add(t.idTerreno);
        }
        // Los que aún no tienen parcela en escena se guardan tal cual, con su hora original.
        foreach (var par in pendientes)
            if (!vistos.Contains(par.Key)) lista.Add(par.Value);
        return lista.ToArray();
    }

    DatosTerreno Crear()
    {
        var d = new DatosTerreno { id = idTerreno, ticks = RelojReal.TicksAhora, huecos = new DatosHueco[huecos.Length] };
        for (int i = 0; i < huecos.Length; i++) d.huecos[i] = huecos[i] != null ? huecos[i].Exportar() : null;
        return d;
    }

    void Aplicar(DatosTerreno d)
    {
        float fuera = RelojReal.Transcurrido(d.ticks);
        for (int i = 0; i < huecos.Length && i < d.huecos.Length; i++)
            if (huecos[i] != null) huecos[i].Restaurar(d.huecos[i], fuera);
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
