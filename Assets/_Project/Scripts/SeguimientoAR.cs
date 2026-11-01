using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Si ARCore pierde el seguimiento con la granja colocada, pausa el juego y orienta al jugador.
// El botón Reubicar sigue disponible: el panel no bloquea toques.
public class SeguimientoAR : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text texto;
    [SerializeField] float segundosParaAvisar = 0.6f;

    const string Motivo = "seguimiento";

    float tiempoPerdido;
    bool perdido;

    void Start() { panel.SetActive(false); }

    void Update()
    {
        bool granjaColocada = ControladorGranja.GranjaColocada;
        bool sinSeguimiento = granjaColocada && ARSession.state != ARSessionState.SessionTracking;

        // Tiempo real: con el juego en pausa el tiempo normal no avanza.
        tiempoPerdido = sinSeguimiento ? tiempoPerdido + Time.unscaledDeltaTime : 0f;

        bool debePausar = sinSeguimiento && tiempoPerdido >= segundosParaAvisar;
        if (debePausar != perdido)
        {
            perdido = debePausar;
            panel.SetActive(perdido);
            if (perdido) PausaJuego.Pedir(Motivo);
            else PausaJuego.Soltar(Motivo);
        }

        if (perdido)
            texto.text = "Mira a tu alrededor\n" + Consejo(ARSession.notTrackingReason)
                         + "\nSi no se recupera, toca Reubicar.";
    }

    static string Consejo(NotTrackingReason razon)
    {
        switch (razon)
        {
            case NotTrackingReason.InsufficientLight:
                return "Hay poca luz. Busca un lugar más iluminado.";
            case NotTrackingReason.ExcessiveMotion:
                return "Te moviste muy rápido. Mueve el celular más despacio.";
            case NotTrackingReason.InsufficientFeatures:
                return "Apunta a una superficie con textura (evita paredes lisas).";
            case NotTrackingReason.Relocalizing:
                return "Reconociendo el lugar. Apunta hacia donde estaba tu granja.";
            default:
                return "Mueve el celular despacio hacia el suelo.";
        }
    }
}