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

    [Header("Ajustes")]
    [SerializeField] private float escalaInicial = 0.5f; // 0.5 = 50 cm

    private static readonly List<ARRaycastHit> impactos = new List<ARRaycastHit>();
    private GameObject ancla;

    public Estado EstadoActual { get; private set; } = Estado.Escaneando;
    public Transform Granja { get; private set; }

    private void Start()
    {
        EntrarEnModoColocar();
    }

    private void Update()
    {
        if (EstadoActual != Estado.Escaneando) return;

        var pantalla = Touchscreen.current;
        if (pantalla == null) return;

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

    // Lo llama el botón "Reubicar".
    public void Reubicar()
    {
        if (Granja != null) EntrarEnModoColocar();
    }

    private void Colocar(Vector3 posicion)
    {
        // Conservamos rotación y escala si ya existía la granja.
        Quaternion rotacion = Granja != null ? Granja.rotation : MirarACamara(posicion);
        Vector3 escala = Granja != null ? Granja.localScale : Vector3.one * escalaInicial;

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
        MostrarPlanos(false);
        if (botonReubicar != null) botonReubicar.SetActive(true);
        if (mensaje != null) mensaje.text = "Pellizca para cambiar el tamaño. Gira con dos dedos.";
    }

    private void EntrarEnModoColocar()
    {
        EstadoActual = Estado.Escaneando;
        MostrarPlanos(true);
        if (botonReubicar != null) botonReubicar.SetActive(false);
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