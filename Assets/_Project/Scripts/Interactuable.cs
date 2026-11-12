using UnityEngine;

public class Interactuable : MonoBehaviour
{
    // Los tipos nuevos van al final para no alterar los valores ya guardados en el prefab.
    public enum Tipo { Pizarra, Pozo, Parcela, Gallina, Caja, Pollito, Huevo, Gallinero, Granero, Terreno }

    [SerializeField] Tipo tipo;
    public Tipo TipoObjeto => tipo;

    [Tooltip("Solo para Huevo y Gallinero: la gallina a la que pertenecen.")]
    [SerializeField] Gallina gallinaVinculada;

    Resaltado resaltado;
    Parcela parcela;
    Gallina gallina;
    Pollito pollito;
    Terreno terreno;

    // El gallinero y el huevo solo aceptan el toque si hay un huevo listo;
    // si no, el toque pasa de largo hacia lo que haya detrás (la gallina).
    public bool AceptaToque
    {
        get
        {
            if (tipo == Tipo.Huevo || tipo == Tipo.Gallinero)
                return gallinaVinculada != null && gallinaVinculada.HuevoListo;
            if (tipo == Tipo.Parcela)
                return parcela == null || parcela.AceptaToque;
            return true;
        }
    }

    void Awake()
    {
        resaltado = GetComponent<Resaltado>();
        parcela = GetComponent<Parcela>();
        gallina = GetComponent<Gallina>();
        pollito = GetComponent<Pollito>();
        terreno = GetComponent<Terreno>();
    }

    void Start()
    {
        if (tipo == Tipo.Pizarra || tipo == Tipo.Pozo)
        {
            if (TutorialController.Instance != null)
                TutorialController.Instance.Registrar(this);
        }
        if (tipo == Tipo.Pizarra || tipo == Tipo.Caja || tipo == Tipo.Granero)
        {
            if (MisionController.Instance != null)
                MisionController.Instance.Registrar(this);
        }
    }

    public void Resaltar(bool valor)
    {
        if (resaltado != null) resaltado.Activar(valor);
    }

    public void Tocar() { Tocar(transform.position); }

    // 'punto' es donde el rayo golpeó el collider (el Terreno lo usa para elegir el hueco).
    public void Tocar(Vector3 punto)
    {
        var tutorial = TutorialController.Instance;
        var juego = EstadoJuego.Instance;

        switch (tipo)
        {
            case Tipo.Parcela:
                if (parcela != null) parcela.Tocar();
                break;

            case Tipo.Terreno:
                if (terreno != null) terreno.Tocar(punto);
                break;

            case Tipo.Gallina:
                if (gallina != null) gallina.Tocar();
                break;

            case Tipo.Pollito:
                if (pollito != null) pollito.Tocar();
                break;

            case Tipo.Huevo:
            case Tipo.Gallinero:
                if (gallinaVinculada != null) gallinaVinculada.TocarHuevo();
                break;

            case Tipo.Granero:
                bool atendido = MisionController.Instance != null && MisionController.Instance.AlTocarGranero();
                if (!atendido && tutorial != null) tutorial.Avisar("Es el granero de la chacra.");
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

    // La caja abre el panel de pedidos (PedidosCaja, en este mismo objeto).
    void Entregar()
    {
        var caja = GetComponent<PedidosCaja>();
        if (caja != null) { caja.Tocar(); return; }

        if (TutorialController.Instance != null)
            TutorialController.Instance.Avisar("A la caja le falta el componente PedidosCaja.");
    }
}
