using UnityEngine;

public class Interactuable : MonoBehaviour
{
    public enum Tipo { Pizarra, Pozo, Parcela }

    [SerializeField] Tipo tipo;
    public Tipo TipoObjeto => tipo;

    Resaltado resaltado;
    Parcela parcela;

    void Awake()
    {
        resaltado = GetComponent<Resaltado>();
        parcela = GetComponent<Parcela>();
    }

    void Start()
    {
        if (tipo == Tipo.Parcela) return;
        if (TutorialController.Instance != null)
            TutorialController.Instance.Registrar(this);
    }

    public void Resaltar(bool valor)
    {
        if (resaltado != null) resaltado.Activar(valor);
    }

    public void Tocar()
    {
        var tutorial = TutorialController.Instance;
        var juego = EstadoJuego.Instance;

        switch (tipo)
        {
            case Tipo.Parcela:
                if (parcela != null) parcela.Tocar();
                break;

            case Tipo.Pozo:
                bool teniaLlave = juego != null && juego.TieneLlave;
                if (tutorial != null) tutorial.AlTocar(this);
                if (teniaLlave)
                {
                    juego.LlenarAgua();
                    if (tutorial != null)
                        tutorial.Avisar($"Regadera llena: {juego.Agua}/{juego.AguaMax}");
                }
                break;

            default:
                if (tutorial != null) tutorial.AlTocar(this);
                break;
        }
    }
}