using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Aviso de seguridad al iniciar y aviso de descanso cada cierto tiempo de juego.
public class AvisosController : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text textoTitulo;
    [SerializeField] TMP_Text textoCuerpo;
    [SerializeField] Button botonAceptar;
    [SerializeField] TMP_Text textoBotonAceptar;
    [SerializeField] float minutosParaDescanso = 30f;

    float tiempoJugado;
    float proximoDescanso;
    bool mostrando;

    void Start()
    {
        proximoDescanso = minutosParaDescanso * 60f;
        botonAceptar.onClick.AddListener(Cerrar);

        Mostrar("Antes de empezar",
                "Revisa tu entorno y no camines mirando solo la pantalla.\n\nAparta muebles y objetos con los que puedas tropezar.",
                "Entendido");
    }

    void Update()
    {
        if (mostrando) return;

        tiempoJugado += Time.deltaTime;   // en pausa no avanza
        if (tiempoJugado >= proximoDescanso)
        {
            proximoDescanso += minutosParaDescanso * 60f;
            Mostrar("Hora de descansar",
                    $"Llevas {minutosParaDescanso:0} minutos jugando.\n\nEstira los brazos, mueve el cuello y descansa la vista un momento.",
                    "Seguir jugando");
        }
    }

    void Mostrar(string titulo, string cuerpo, string textoBoton)
    {
        textoTitulo.text = titulo;
        textoCuerpo.text = cuerpo;
        textoBotonAceptar.text = textoBoton;
        panel.SetActive(true);
        mostrando = true;
    }

    void Cerrar()
    {
        panel.SetActive(false);
        mostrando = false;
    }
}