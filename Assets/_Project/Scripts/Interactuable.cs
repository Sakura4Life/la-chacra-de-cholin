using UnityEngine;

public class Interactuable : MonoBehaviour
{
    public enum Tipo { Pizarra, Pozo, Parcela, Gallina, Caja }

    [SerializeField] Tipo tipo;
    public Tipo TipoObjeto => tipo;

    Resaltado resaltado;
    Parcela parcela;
    Gallina gallina;

    void Awake()
    {
        resaltado = GetComponent<Resaltado>();
        parcela = GetComponent<Parcela>();
        gallina = GetComponent<Gallina>();
    }

    void Start()
    {
        if (tipo == Tipo.Pizarra || tipo == Tipo.Pozo)
        {
            if (TutorialController.Instance != null)
                TutorialController.Instance.Registrar(this);
        }
        if (tipo == Tipo.Pizarra || tipo == Tipo.Caja)
        {
            if (MisionController.Instance != null)
                MisionController.Instance.Registrar(this);
        }
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

            case Tipo.Gallina:
                if (gallina != null) gallina.Tocar();
                break;

            case Tipo.Caja:
                Entregar();
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

    void Entregar()
    {
        var juego = EstadoJuego.Instance;
        var tutorial = TutorialController.Instance;
        if (juego == null || tutorial == null) return;

        if (!juego.HayParaVender)
        {
            tutorial.Avisar("No tienes nada para entregar. Cosecha tomates o recoge huevos.");
            return;
        }

        int monedas = juego.VenderTodo(out int tomates, out int huevos);
        tutorial.Avisar($"Entregaste {tomates} tomate(s) y {huevos} huevo(s): +{monedas} monedas");
    }
}