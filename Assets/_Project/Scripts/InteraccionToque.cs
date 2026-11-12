using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InteraccionToque : MonoBehaviour
{
    [SerializeField] Camera camara;
    [SerializeField] float distanciaMaxima = 10f;
    [SerializeField] float tiempoMaximo = 0.35f;
    [SerializeField] float movimientoMaximoPx = 25f;

    Vector2 posInicial;
    float tiempoInicial;
    bool toqueValido;

    void Awake()
    {
        if (camara == null) camara = Camera.main;
    }

    void Update()
    {
        var pantalla = Touchscreen.current;
        if (pantalla == null) return;
        var toque = pantalla.primaryTouch;

        if (toque.press.wasPressedThisFrame)
        {
            posInicial = toque.position.ReadValue();
            tiempoInicial = Time.unscaledTime;
            int id = toque.touchId.ReadValue();
            toqueValido = !EventSystem.current.IsPointerOverGameObject(id);
        }

        if (ContarDedos(pantalla) > 1) toqueValido = false;

        if (toque.press.wasReleasedThisFrame && toqueValido)
        {
            toqueValido = false;
            Vector2 posFinal = toque.position.ReadValue();
            bool rapido = Time.unscaledTime - tiempoInicial <= tiempoMaximo;
            bool quieto = (posFinal - posInicial).magnitude <= movimientoMaximoPx;
            if (rapido && quieto) Tocar(posFinal);
        }
    }

    static int ContarDedos(Touchscreen pantalla)
    {
        int n = 0;
        foreach (var dedo in pantalla.touches)
            if (dedo.isInProgress) n++;
        return n;
    }

    void Tocar(Vector2 posicionPantalla)
    {
        Ray rayo = camara.ScreenPointToRay(posicionPantalla);
        RaycastHit[] golpes = Physics.RaycastAll(rayo, distanciaMaxima);
        System.Array.Sort(golpes, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var golpe in golpes)
        {
            var objeto = golpe.collider.GetComponentInParent<Interactuable>();
            if (objeto == null) break;            // algo sólido tapa lo de atrás
            if (!objeto.AceptaToque) continue;    // gallinero sin huevo: deja pasar el toque
            objeto.Tocar(golpe.point);
            return;
        }
    }
}