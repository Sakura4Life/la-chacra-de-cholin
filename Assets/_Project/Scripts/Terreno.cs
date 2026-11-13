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
    public bool comprada;  // la parcela con id 0 siempre cuenta como comprada
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

    [Header("Compra (las parcelas con id distinto de 0 empiezan bloqueadas)")]
    [Tooltip("Monedas que cuesta esta parcela.")]
    [SerializeField] int precio = 30;
    [Tooltip("Altura del suelo de la parcela donde se clava el cartel de 'En venta' (unidades de granja).")]
    [SerializeField] float alturaCartel = 0.05f;
    [Tooltip("Tamaño del cartel (1 = normal).")]
    [SerializeField] float escalaCartel = 1f;

    Parcela[] huecos;
    bool comprada;
    GameObject cartel;
    Vector3 escalaOriginal;
    DatosTerreno enRestauracion;
    readonly List<GameObject> ocultos = new List<GameObject>();

    public bool Comprada => comprada;
    public int Id => idTerreno;

    // Parcelas vivas y datos cargados esperando a que su parcela exista (la granja se coloca después de cargar).
    static readonly List<Terreno> activos = new List<Terreno>();
    static readonly Dictionary<int, DatosTerreno> pendientes = new Dictionary<int, DatosTerreno>();

    void Awake()
    {
        huecos = GetComponentsInChildren<Parcela>(true);
        escalaOriginal = transform.localScale;
        comprada = idTerreno == 0;
        foreach (var otro in activos)
            if (otro != null && otro.idTerreno == idTerreno)
                Debug.LogWarning("Terreno: hay dos parcelas con el mismo 'Id Terreno' (" + idTerreno + "). Cada una necesita un número distinto.", this);
        activos.Add(this);
        if (!comprada) Bloquear();
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
        // Si justo se está restaurando, se guarda lo que se cargó (los huecos aún no lo aplicaron).
        if (enRestauracion != null) return enRestauracion;

        var d = new DatosTerreno
        {
            id = idTerreno,
            ticks = RelojReal.TicksAhora,
            comprada = comprada,
            huecos = new DatosHueco[comprada ? huecos.Length : 0]
        };
        if (comprada)
            for (int i = 0; i < huecos.Length; i++) d.huecos[i] = huecos[i] != null ? huecos[i].Exportar() : null;
        return d;
    }

    void Aplicar(DatosTerreno d)
    {
        if (!comprada && d.comprada) Desbloquear(false);
        if (!comprada) return;
        enRestauracion = d;
        StartCoroutine(RestaurarHuecos(d));
    }

    // Espera un fotograma para que los huecos recién activados hagan su Start.
    IEnumerator RestaurarHuecos(DatosTerreno d)
    {
        yield return null;
        float fuera = RelojReal.Transcurrido(d.ticks);
        for (int i = 0; i < huecos.Length && i < d.huecos.Length; i++)
            if (huecos[i] != null) huecos[i].Restaurar(d.huecos[i], fuera);
        enRestauracion = null;
    }

    // ---------- Compra ----------

    // Parcela bloqueada: oculta todo lo que cuelga de ella y deja solo el cartel de "En venta".
    void Bloquear()
    {
        ocultos.Clear();
        foreach (Transform hijo in transform)
            if (hijo.gameObject.activeSelf) ocultos.Add(hijo.gameObject);
        foreach (var g in ocultos) g.SetActive(false);
        cartel = CartelTerreno.Crear(transform, precio, alturaCartel, escalaCartel);
    }

    void Desbloquear(bool animar)
    {
        comprada = true;
        if (cartel != null) Destroy(cartel);
        cartel = null;
        foreach (var g in ocultos) if (g != null) g.SetActive(true);
        ocultos.Clear();
        if (animar) StartCoroutine(Aparecer());
    }

    IEnumerator Aparecer()
    {
        const float duracion = 0.5f;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duracion) - 1f;
            float s = 1f + 2.70158f * k * k * k + 1.70158f * k * k;   // easeOutBack
            transform.localScale = escalaOriginal * Mathf.Max(0.01f, s);
            yield return null;
        }
        transform.localScale = escalaOriginal;
    }

    void SolicitarCompra()
    {
        if (EstadoJuego.Instance == null) return;
        ConfirmarCompraUI.Mostrar("Parcela en venta", precio, EstadoJuego.Instance.Monedas, Comprar);
    }

    void Comprar()
    {
        var juego = EstadoJuego.Instance;
        if (comprada || juego == null) return;
        if (!juego.GastarMonedas(precio))
        {
            Avisar("No te alcanzan las monedas para esta parcela.");
            return;
        }
        Desbloquear(true);
        Avisar("¡Parcela comprada! Ya puedes sembrar en ella.");
        Guardado.GuardarAhora();
    }

    // Lo llama Interactuable cuando el toque cae sobre la tierra (no sobre una planta).
    public void Tocar(Vector3 punto)
    {
        if (!comprada) { SolicitarCompra(); return; }
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
