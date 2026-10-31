using UnityEngine;

public class MisionController : MonoBehaviour
{
    public static MisionController Instance { get; private set; }

    enum Paso
    {
        Inactiva, EsperandoCosecha, AvisoPedido, LeyendoPedido,
        EntregandoPedido, AvisoPollito, LeyendoPollito, PollitoActiva, PollitoEncontrado
    }

    [SerializeField] int tomatesPedido = 3;

    [TextArea(3, 6)]
    [SerializeField]
    string textoPedido =
        "Pedido de la pizarra:\n\nEntrega {0} tomates en la caja de entrega.";

    [TextArea(3, 6)]
    [SerializeField]
    string textoPollito =
        "Nota nueva:\n\nSe escapó el pollito de la granja. Escucha con atención: debe estar cerca.";

    [TextArea(3, 6)]
    [SerializeField]
    string textoGallinero =
        "Nota:\n\nEl pollito está a salvo contigo. Llévalo al gallinero para que crezca.";

    Paso paso = Paso.Inactiva;
    Interactuable pizarra, caja;
    int entregadosBase;

    public bool PollitoBuscandose => paso == Paso.PollitoActiva;
    public bool PollitoYaEncontrado => paso == Paso.PollitoEncontrado;

    void Awake() { Instance = this; }

    void Start()
    {
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio += Evaluar;
        Evaluar();
    }

    void OnDestroy()
    {
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio -= Evaluar;
        if (Instance == this) Instance = null;
    }

    public void Registrar(Interactuable objeto)
    {
        if (objeto.TipoObjeto == Interactuable.Tipo.Pizarra) pizarra = objeto;
        else if (objeto.TipoObjeto == Interactuable.Tipo.Caja) caja = objeto;
        AplicarResaltado();
    }

    // La llama TutorialController cuando tocas la pizarra con el tutorial ya completo.
    // Devuelve true si la misión se encargó de ese toque.
    public bool AlTocarPizarra()
    {
        switch (paso)
        {
            case Paso.AvisoPedido:
                IrA(Paso.LeyendoPedido);
                return true;
            case Paso.EntregandoPedido:
                Nota(string.Format(textoPedido, tomatesPedido), null);
                return true;
            case Paso.AvisoPollito:
                IrA(Paso.LeyendoPollito);
                return true;
            case Paso.PollitoActiva:
                Nota(textoPollito, null);
                return true;
            case Paso.PollitoEncontrado:
                Nota(textoGallinero, null);
                return true;
            default:
                return false;
        }
    }

    // La llama Pollito cuando lo tocas estando escondido.
    public void AlEncontrarPollito()
    {
        if (paso != Paso.PollitoActiva) return;

        if (EstadoJuego.Instance != null) EstadoJuego.Instance.DarSemillaLegendaria();
        IrA(Paso.PollitoEncontrado);

        if (TutorialController.Instance != null)
            TutorialController.Instance.Avisar("¡Encontraste al pollito! Recibiste la semilla legendaria.", 5f);
        Handheld.Vibrate();
    }

    void Evaluar()
    {
        var juego = EstadoJuego.Instance;
        if (juego == null) return;

        switch (paso)
        {
            case Paso.Inactiva:
                if (juego.TieneLlave)
                {
                    paso = Paso.EsperandoCosecha;
                    Evaluar();
                }
                break;

            case Paso.EsperandoCosecha:
                if (juego.TomatesCosechados >= 1) IrA(Paso.AvisoPedido);
                break;

            case Paso.EntregandoPedido:
                {
                    int entregados = juego.TomatesEntregados - entregadosBase;
                    if (entregados >= tomatesPedido) IrA(Paso.AvisoPollito);
                    else FijarPista(TextoProgreso(entregados));
                    break;
                }
        }
    }

    void IrA(Paso nuevo)
    {
        paso = nuevo;
        AplicarResaltado();

        switch (paso)
        {
            case Paso.AvisoPedido:
                FijarPista("Hay un pedido nuevo en la pizarra. Tócala.");
                break;
            case Paso.LeyendoPedido:
                FijarPista("");
                Nota(string.Format(textoPedido, tomatesPedido), () => IrA(Paso.EntregandoPedido));
                break;
            case Paso.EntregandoPedido:
                entregadosBase = EstadoJuego.Instance != null ? EstadoJuego.Instance.TomatesEntregados : 0;
                FijarPista(TextoProgreso(0));
                break;
            case Paso.AvisoPollito:
                FijarPista("Hay una nota nueva en la pizarra. Tócala.");
                break;
            case Paso.LeyendoPollito:
                FijarPista("");
                Nota(textoPollito, () => IrA(Paso.PollitoActiva));
                break;
            case Paso.PollitoActiva:
                FijarPista("Misión: encuentra al pollito. Escucha su piar (mejor con audífonos).");
                break;
            case Paso.PollitoEncontrado:
                FijarPista("Llevar al pollito al gallinero llegará en la próxima fase.");
                break;
        }
    }

    void AplicarResaltado()
    {
        if (pizarra != null)
            pizarra.Resaltar(paso == Paso.AvisoPedido || paso == Paso.AvisoPollito);
        if (caja != null)
            caja.Resaltar(paso == Paso.EntregandoPedido);
    }

    string TextoProgreso(int entregados)
    {
        return $"Pedido: entrega {tomatesPedido} tomates en la caja ({entregados}/{tomatesPedido})";
    }

    static void FijarPista(string texto)
    {
        if (TutorialController.Instance != null) TutorialController.Instance.FijarPista(texto);
    }

    static void Nota(string texto, System.Action alCerrar)
    {
        if (TutorialController.Instance != null) TutorialController.Instance.MostrarNota(texto, alCerrar);
    }
}