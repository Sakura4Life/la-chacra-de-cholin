using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ControladorGranja : MonoBehaviour
{
    public enum Estado { Escaneando, Colocada }

    [Header("Referencias")]
    [SerializeField] private GameObject granjaPrefab;
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Transform camara;
    [SerializeField] private TMP_Text mensaje;
    [SerializeField] private GameObject botonReubicar;
    [SerializeField] private GameObject botonReiniciar;

    [Header("Ajustes")]
    [SerializeField] private float escalaInicial = 0.5f; // 0.5 = 50 cm

    private static readonly List<ARRaycastHit> impactos = new List<ARRaycastHit>();
    private GameObject ancla;
    private ARSession sesion;
    private bool reiniciando;
    private bool hayEscalaGuardada;
    private Vector3 escalaGuardada;

    public Estado EstadoActual { get; private set; } = Estado.Escaneando;
    public Transform Granja { get; private set; }

    // true solo mientras la granja está colocada y visible (otros scripts lo consultan).
    public static bool GranjaColocada { get; private set; }

    private void Start()
    {
        sesion = FindFirstObjectByType<ARSession>();
        EntrarEnModoColocar();
    }

    private void Update()
    {
        if (EstadoActual != Estado.Escaneando) return;

        var pantalla = Touchscreen.current;
        if (pantalla == null)
        {
#if UNITY_EDITOR
            ColocarSolaEnEditor();
#endif
            return;
        }

        var toque = pantalla.primaryTouch;
        if (!toque.press.wasPressedThisFrame) return;

        // Si el toque cae sobre un botón de la interfaz, lo ignoramos.
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject(toque.touchId.ReadValue())) return;

        if (raycastManager.Raycast(toque.position.ReadValue(), impactos, TrackableType.PlaneWithinPolygon))
        {
            Colocar(impactos[0].pose.position);
        }
    }

#if UNITY_EDITOR
    // Solo en el Editor (no entra en la APK): sin pantalla táctil nadie puede tocar el plano, así que
    // la granja se coloca sola en el centro de la pantalla cuando XR Simulation detecta un plano
    // (o, tras 6 s sin planos, delante de la cámara). Sirve para probar el juego sin teléfono.
    private float esperaEditor;

    private void ColocarSolaEnEditor()
    {
        if (Granja != null || granjaPrefab == null) return;
        esperaEditor += Time.unscaledDeltaTime;
        if (esperaEditor < 1.5f) return;

        var centro = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (raycastManager != null && raycastManager.Raycast(centro, impactos, TrackableType.PlaneWithinPolygon))
        {
            Colocar(impactos[0].pose.position);
        }
        else if (esperaEditor > 6f && camara != null)
        {
            Vector3 p = camara.position + camara.forward * 0.8f;
            p.y = camara.position.y - 0.9f;
            Colocar(p);
        }
    }
#endif

    // Lo llama el botón "Reubicar": mueve la granja conservando los planos ya detectados.
    public void Reubicar()
    {
        if (Granja != null) EntrarEnModoColocar();
    }

    // Lo llama el botón "Reiniciar": borra los planos y escanea de cero.
    public void ReiniciarEscaneo()
    {
        if (Granja == null || reiniciando) return;
        StartCoroutine(RutinaReinicio());
    }

    private IEnumerator RutinaReinicio()
    {
        reiniciando = true;

        // Guardamos el tamaño y escondemos la granja SIN desactivarla:
        // si se desactiva, los cultivos que están creciendo se quedarían detenidos.
        escalaGuardada = Granja.localScale;
        hayEscalaGuardada = true;
        Granja.SetParent(null, true);
        Granja.localScale = Vector3.one * 0.0001f;

        if (ancla != null)
        {
            Destroy(ancla);
            ancla = null;
        }

        EstadoActual = Estado.Escaneando;
        GranjaColocada = false;
        if (botonReubicar != null) botonReubicar.SetActive(false);
        if (botonReiniciar != null) botonReiniciar.SetActive(false);
        if (mensaje != null) mensaje.text = "Reiniciando el escaneo...";

        // Reinicia la sesión de AR: se borran todos los planos y se empieza de cero.
        if (sesion != null) sesion.Reset();
        yield return null;
        yield return null;

        MostrarPlanos(true);
        if (mensaje != null)
            mensaje.text = "Mueve el celular despacio para escanear el piso y toca para colocar la granja.";
    }

    private void Colocar(Vector3 posicion)
    {
        bool trasReinicio = reiniciando;

        // Tras un reinicio la orientación anterior ya no vale: la granja mira hacia la cámara.
        Quaternion rotacion = (Granja != null && !trasReinicio) ? Granja.rotation : MirarACamara(posicion);
        Vector3 escala = hayEscalaGuardada ? escalaGuardada
                       : Granja != null ? Granja.localScale
                       : Vector3.one * escalaInicial;
        hayEscalaGuardada = false;
        reiniciando = false;

        // Quitamos el ancla anterior sin destruir la granja.
        if (ancla != null)
        {
            if (Granja != null) Granja.SetParent(null, true);
            Destroy(ancla);
        }

        // Nueva ancla en el punto tocado: mantiene la granja fija aunque ARCore corrija su mapa.
        ancla = new GameObject("Ancla Granja");
        ancla.transform.position = posicion;
        ancla.AddComponent<ARAnchor>();

        if (Granja == null)
        {
            Granja = Instantiate(granjaPrefab).transform;
        }

        Granja.SetParent(ancla.transform, false);
        Granja.localPosition = Vector3.zero;
        Granja.rotation = rotacion;
        Granja.localScale = escala;

        EstadoActual = Estado.Colocada;
        GranjaColocada = true;
        MostrarPlanos(false);
        if (botonReubicar != null) botonReubicar.SetActive(true);
        if (botonReiniciar != null) botonReiniciar.SetActive(true);
        if (mensaje != null) mensaje.text = "Pellizca para cambiar el tamaño. Gira con dos dedos.";
    }

    private void EntrarEnModoColocar()
    {
        EstadoActual = Estado.Escaneando;
        GranjaColocada = false;
        MostrarPlanos(true);
        if (botonReubicar != null) botonReubicar.SetActive(false);
        if (botonReiniciar != null) botonReiniciar.SetActive(Granja != null && !reiniciando);
        if (mensaje != null)
        {
            mensaje.text = Granja == null
                ? "Mueve el celular para escanear el piso y toca para colocar la granja."
                : "Toca el nuevo lugar para mover la granja.";
        }
    }

    private Quaternion MirarACamara(Vector3 desde)
    {
        Vector3 haciaCamara = camara.position - desde;
        haciaCamara.y = 0f;
        return haciaCamara.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(haciaCamara)
            : Quaternion.identity;
    }

    private void MostrarPlanos(bool visible)
    {
        planeManager.enabled = visible;
        planeManager.SetTrackablesActive(visible);
    }
}