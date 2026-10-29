using UnityEngine;
using UnityEngine.InputSystem;

public class GestosGranja : MonoBehaviour
{
    [SerializeField] private ControladorGranja controlador;
    [SerializeField] private float escalaMinima = 0.3f;
    [SerializeField] private float escalaMaxima = 2f;

    private bool gestoActivo;
    private float distanciaAnterior;
    private float anguloAnterior;

    private void Update()
    {
        if (controlador.EstadoActual != ControladorGranja.Estado.Colocada || controlador.Granja == null)
        {
            gestoActivo = false;
            return;
        }

        var pantalla = Touchscreen.current;
        if (pantalla == null) return;

        var dedo0 = pantalla.touches[0];
        var dedo1 = pantalla.touches[1];
        if (!dedo0.isInProgress || !dedo1.isInProgress)
        {
            gestoActivo = false;
            return;
        }

        Vector2 p0 = dedo0.position.ReadValue();
        Vector2 p1 = dedo1.position.ReadValue();
        float distancia = Vector2.Distance(p0, p1);
        float angulo = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;

        if (!gestoActivo)
        {
            gestoActivo = true;
            distanciaAnterior = distancia;
            anguloAnterior = angulo;
            return;
        }

        Transform granja = controlador.Granja;

        // Pellizcar: escala proporcional a lo que se separan o juntan los dedos.
        if (distanciaAnterior > 1f)
        {
            float nueva = granja.localScale.x * (distancia / distanciaAnterior);
            granja.localScale = Vector3.one * Mathf.Clamp(nueva, escalaMinima, escalaMaxima);
        }

        // Girar: la diferencia de ángulo entre los dedos rota la granja sobre su eje vertical.
        float giro = Mathf.DeltaAngle(anguloAnterior, angulo);
        granja.Rotate(0f, -giro, 0f, Space.World);

        distanciaAnterior = distancia;
        anguloAnterior = angulo;
    }
}