using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PausaUI : MonoBehaviour
{
    [SerializeField] Button botonPausa;
    [SerializeField] GameObject panel;
    [SerializeField] Button botonContinuar;
    [SerializeField] Button botonVibracion;
    [SerializeField] TMP_Text textoVibracion;
    [SerializeField] Button botonTamanoMesa;
    [SerializeField] Button botonNuevaPartida;
    [SerializeField] TMP_Text textoNuevaPartida;
    [SerializeField] float escalaMesa = 0.3f;

    const string Motivo = PausaJuego.MotivoMenu;

    bool confirmandoBorrado;
    float venceConfirmacion;

    void Start()
    {
        panel.SetActive(false);
        botonPausa.onClick.AddListener(Abrir);
        botonContinuar.onClick.AddListener(Continuar);
        botonVibracion.onClick.AddListener(AlternarVibracion);
        botonTamanoMesa.onClick.AddListener(TamanoMesa);
        botonNuevaPartida.onClick.AddListener(NuevaPartida);
        Refrescar();
    }

    void Update()
    {
        if (PausaJuego.PendienteReabrir && ControladorGranja.GranjaColocada && !panel.activeSelf)
        {
            PausaJuego.PendienteReabrir = false;
            Abrir();
        }

        if (panel.activeSelf && confirmandoBorrado && Time.unscaledTime > venceConfirmacion)
        {
            confirmandoBorrado = false;
            Refrescar();
        }
    }

    void Abrir()
    {
        confirmandoBorrado = false;
        panel.SetActive(true);
        PausaJuego.Pedir(Motivo);
        Guardado.GuardarAhora();
        Refrescar();
    }

    void Continuar()
    {
        panel.SetActive(false);
        PausaJuego.Soltar(Motivo);
    }

    void AlternarVibracion()
    {
        Ajustes.Vibracion = !Ajustes.Vibracion;
        if (Ajustes.Vibracion) Handheld.Vibrate();
        Refrescar();
    }

    void TamanoMesa()
    {
        bool hayGranja = RaizGranja.Instance != null && ControladorGranja.GranjaColocada;
        if (hayGranja) RaizGranja.Instance.transform.localScale = Vector3.one * escalaMesa;

        Continuar();
        if (TutorialController.Instance != null)
            TutorialController.Instance.Avisar(hayGranja ? "Tamaño de mesa aplicado." : "Primero coloca tu granja.");
    }

    void NuevaPartida()
    {
        if (!confirmandoBorrado || Time.unscaledTime > venceConfirmacion)
        {
            confirmandoBorrado = true;
            venceConfirmacion = Time.unscaledTime + 3f;
            Refrescar();
            return;
        }

        Guardado.Borrar();
        PausaJuego.Limpiar();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Refrescar()
    {
        textoVibracion.text = Ajustes.Vibracion ? "Vibración: Sí" : "Vibración: No";
        textoNuevaPartida.text = confirmandoBorrado ? "¿Seguro? Toca otra vez" : "Nueva partida";
    }
}