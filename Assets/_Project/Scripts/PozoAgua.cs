using System.Collections;
using UnityEngine;

public class PozoAgua : MonoBehaviour
{
    [Header("Objetos (hijos del Pozo, desactivados en el prefab)")]
    [SerializeField] Transform agua;
    [SerializeField] Transform llave;

    [Header("Alturas (Position Y local)")]
    [SerializeField] float aguaVacio = -0.05f;
    [SerializeField] float aguaLleno = 0.05f;
    [SerializeField] float llaveFondo = -0.04f;
    [SerializeField] float llaveFlotando = 0.06f;

    [Header("Tiempos (segundos)")]
    [SerializeField] float duracionSubida = 2.5f;
    [SerializeField] float duracionFlotando = 2f;
    [SerializeField] float duracionRecoger = 0.6f;

    [Header("Movimiento de la llave")]
    [SerializeField] float giroPorSegundo = 120f;
    [SerializeField] float amplitudBalanceo = 0.006f;
    [SerializeField] float velocidadBalanceo = 4f;

    bool llaveVista;
    float inicio;
    Coroutine rutina;
    Vector3 escalaLlave = Vector3.one;

    void Start()
    {
        inicio = Time.time;
        if (llave != null) escalaLlave = llave.localScale;

        var estado = EstadoJuego.Instance;
        llaveVista = estado != null && estado.TieneLlave;
        PonerEstadoFinal(llaveVista);

        if (estado != null) estado.OnCambio += AlCambiar;
    }

    void OnDestroy()
    {
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio -= AlCambiar;
    }

    void AlCambiar()
    {
        bool tiene = EstadoJuego.Instance != null && EstadoJuego.Instance.TieneLlave;
        if (tiene == llaveVista) return;
        llaveVista = tiene;

        if (rutina != null) { StopCoroutine(rutina); rutina = null; }

        // Si el cambio llega casi al instante de aparecer la granja, es una partida cargada: sin animación.
        if (tiene && Time.time - inicio > 1.5f) rutina = StartCoroutine(Animar());
        else PonerEstadoFinal(tiene);
    }

    void PonerEstadoFinal(bool tieneLlave)
    {
        if (agua == null || llave == null) return;
        agua.gameObject.SetActive(tieneLlave);
        PonerY(agua, tieneLlave ? aguaLleno : aguaVacio);
        llave.gameObject.SetActive(false);
        llave.localScale = escalaLlave;
    }

    IEnumerator Animar()
    {
        if (agua == null || llave == null) yield break;

        agua.gameObject.SetActive(true);
        llave.gameObject.SetActive(true);
        llave.localScale = escalaLlave;
        llave.localRotation = Quaternion.identity;
        PonerY(agua, aguaVacio);
        PonerY(llave, llaveFondo);

        // 1) El agua sube y la llave sube con ella.
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duracionSubida);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            PonerY(agua, Mathf.Lerp(aguaVacio, aguaLleno, k));
            PonerY(llave, Mathf.Lerp(llaveFondo, llaveFlotando, k));
            llave.Rotate(0f, giroPorSegundo * Time.deltaTime, 0f, Space.Self);
            yield return null;
        }

        // 2) La llave flota meciéndose.
        float tiempo = 0f;
        while (tiempo < duracionFlotando)
        {
            tiempo += Time.deltaTime;
            PonerY(llave, llaveFlotando + Mathf.Sin(tiempo * velocidadBalanceo) * amplitudBalanceo);
            llave.Rotate(0f, giroPorSegundo * Time.deltaTime, 0f, Space.Self);
            yield return null;
        }

        // 3) La llave se recoge: sube un poco, gira rápido y se encoge.
        float yInicial = llave.localPosition.y;
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duracionRecoger);
            float k = Mathf.Clamp01(t);
            PonerY(llave, yInicial + k * 0.04f);
            llave.localScale = escalaLlave * (1f - k);
            llave.Rotate(0f, giroPorSegundo * 3f * Time.deltaTime, 0f, Space.Self);
            yield return null;
        }

        llave.gameObject.SetActive(false);
        llave.localScale = escalaLlave;
        PonerY(agua, aguaLleno);
        rutina = null;
    }

    static void PonerY(Transform t, float y)
    {
        var p = t.localPosition;
        p.y = y;
        t.localPosition = p;
    }

    // ---------- Ayudas para configurar en el editor (menú ⋮ del componente) ----------
    void Marcar(string nombre)
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(this, nombre);
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("1. Guardar altura: agua VACIO (usa la Y actual del Agua)")]
    void GuardarAguaVacio() { if (agua == null) return; Marcar("agua vacio"); aguaVacio = agua.localPosition.y; }

    [ContextMenu("2. Guardar altura: agua LLENO (usa la Y actual del Agua)")]
    void GuardarAguaLleno() { if (agua == null) return; Marcar("agua lleno"); aguaLleno = agua.localPosition.y; }

    [ContextMenu("3. Guardar altura: llave FONDO (usa la Y actual de la Llave)")]
    void GuardarLlaveFondo() { if (llave == null) return; Marcar("llave fondo"); llaveFondo = llave.localPosition.y; }

    [ContextMenu("4. Guardar altura: llave FLOTANDO (usa la Y actual de la Llave)")]
    void GuardarLlaveFlotando() { if (llave == null) return; Marcar("llave flotando"); llaveFlotando = llave.localPosition.y; }

    [ContextMenu("Probar animación (solo en Play)")]
    void Probar()
    {
        if (!Application.isPlaying) return;
        if (rutina != null) StopCoroutine(rutina);
        rutina = StartCoroutine(Animar());
    }

    [ContextMenu("Reiniciar visual (solo en Play)")]
    void ReiniciarVisual()
    {
        if (!Application.isPlaying) return;
        if (rutina != null) { StopCoroutine(rutina); rutina = null; }
        PonerEstadoFinal(false);
    }
}