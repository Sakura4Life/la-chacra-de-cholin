using UnityEngine;

public class MisionController : MonoBehaviour
{
    public static MisionController Instance { get; private set; }

    // El orden importa: los estados del pollito se comparan con >=.
    enum Paso
    {
        Inactiva, BuscarSemillas, EsperandoCosecha, AvisoPedido, LeyendoPedido,
        EntregandoPedido, AvisoPollito, LeyendoPollito,
        PollitoActiva, PollitoEncontrado, PollitoEnGallinero, Libre
    }

    [Tooltip("Choclos que pide el primer pedido de la pizarra.")]
    [SerializeField] int choclosPedido = 3;
    [Tooltip("Semillas de choclo que Cholin entrega al tocar el granero.")]
    [SerializeField] int semillasChocloIniciales = 12;

    [TextArea(3, 6)]
    [SerializeField]
    string textoSemillasIniciales =
        "Cholin:\n\nAquí tienes tus primeras {0} semillas de choclo. Siémbralas tocando la tierra del huerto, riégalas con la regadera y espera a que crezcan.";

    [TextArea(3, 6)]
    [SerializeField]
    string textoPedidoTutorial =
        "Pedido de la pizarra:\n\nEntrega {0} choclos en la caja de entrega.";

    [TextArea(3, 6)]
    [SerializeField]
    string textoPollito =
        "Nota nueva:\n\nSe escapó el pollito de la granja. Escucha con atención: debe estar cerca.";

    [TextArea(3, 6)]
    [SerializeField]
    string textoGallinero =
        "Nota:\n\nEl pollito está a salvo contigo. Llévalo al gallinero para que crezca.";

    [TextArea(3, 6)]
    [SerializeField]
    string textoLibre =
        "Nota:\n\nLa chacra está viva otra vez. Cuida tus cultivos y a tu pollito.";

    Paso paso = Paso.Inactiva;
    Interactuable pizarra, caja, granero;
    int entregadosBase;

    public bool BuscandoSemillas => paso == Paso.BuscarSemillas;
    public bool PollitoBuscandose => paso == Paso.PollitoActiva;
    public bool PollitoYaEncontrado => paso == Paso.PollitoEncontrado;
    public bool PollitoEnGallinero => paso >= Paso.PollitoEnGallinero;
    public bool PollitoVisible => paso >= Paso.PollitoActiva;
    public int PasoActual => (int)paso;
    public int EntregadosBase => entregadosBase;

    // Al cargar una partida: retoma el paso guardado sin repetir recompensas.
    public void Restaurar(int valor, int baseEntregados)
    {
        var p = (Paso)valor;
        if (p == Paso.LeyendoPedido) p = Paso.AvisoPedido;     // lectura transitoria
        if (p == Paso.LeyendoPollito) p = Paso.AvisoPollito;

        FijarPista("");
        entregadosBase = baseEntregados;

        if (p == Paso.Inactiva || p == Paso.EsperandoCosecha)
        {
            paso = p;
            AplicarResaltado();
            return;
        }
        if (p == Paso.EntregandoPedido)
        {
            paso = p;
            AplicarResaltado();
            Evaluar();
            return;
        }
        IrA(p);
    }

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
        else if (objeto.TipoObjeto == Interactuable.Tipo.Granero) granero = objeto;
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
                Nota(string.Format(textoPedidoTutorial, choclosPedido), null);
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
            case Paso.PollitoEnGallinero:
            case Paso.Libre:
                Nota(textoLibre, null);
                return true;
            default:
                return false;
        }
    }

    // La llama Interactuable al tocar el granero. Devuelve true si la misión se encargó del toque.
    public bool AlTocarGranero()
    {
        if (paso != Paso.BuscarSemillas) return false;

        // Primero se entregan las semillas y se cambia de paso, para que un doble toque no las duplique.
        if (EstadoJuego.Instance != null)
            EstadoJuego.Instance.SumarSemillas(Cultivo.Maiz, semillasChocloIniciales);
        IrA(Paso.EsperandoCosecha);
        Nota(string.Format(textoSemillasIniciales, semillasChocloIniciales), null);
        Ajustes.Vibrar();
        return true;
    }

    // La llama Pollito cuando lo tocas estando escondido.
    public void AlEncontrarPollito()
    {
        if (paso != Paso.PollitoActiva) return;

        if (EstadoJuego.Instance != null) EstadoJuego.Instance.DarSemillaLegendaria();
        IrA(Paso.PollitoEncontrado);

        if (TutorialController.Instance != null)
            TutorialController.Instance.Avisar("¡Encontraste al pollito! Recibiste la semilla legendaria.", 5f);
        Ajustes.Vibrar();
    }

    // La llama Pollito cuando lo sueltas sobre el gallinero.
    public void AlLlegarAlGallinero()
    {
        if (paso != Paso.PollitoEncontrado) return;
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.PonerPolloEnGallinero();
        IrA(Paso.PollitoEnGallinero);
    }

    // La llama Gallina cada vez que recoges un huevo; solo la primera vez cambia el paso.
    public void AlRecogerHuevo()
    {
        if (paso != Paso.PollitoEnGallinero) return;
        IrA(Paso.Libre);
    }

    void Evaluar()
    {
        var juego = EstadoJuego.Instance;
        if (juego == null) return;

        switch (paso)
        {
            case Paso.Inactiva:
                if (juego.TieneLlave) IrA(Paso.BuscarSemillas);
                break;

            case Paso.EsperandoCosecha:
                if (juego.CosechasTotales >= 1) IrA(Paso.AvisoPedido);
                break;

            case Paso.PollitoEnGallinero:
                if (juego.PolloVendido) IrA(Paso.Libre);
                break;

            case Paso.EntregandoPedido:
                {
                    int entregados = juego.CultivosEntregados - entregadosBase;
                    if (entregados >= choclosPedido) IrA(Paso.AvisoPollito);
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
            case Paso.BuscarSemillas:
                FijarPista("Cholin te espera en el granero que brilla. Tócalo para recibir tus semillas.");
                break;
            case Paso.EsperandoCosecha:
                FijarPista("Siembra: toca la tierra del huerto, riega la semilla y espera a que crezca el choclo.");
                break;
            case Paso.AvisoPedido:
                FijarPista("Hay un pedido nuevo en la pizarra. Tócala.");
                break;
            case Paso.LeyendoPedido:
                FijarPista("");
                Nota(string.Format(textoPedidoTutorial, choclosPedido), () => IrA(Paso.EntregandoPedido));
                break;
            case Paso.EntregandoPedido:
                entregadosBase = EstadoJuego.Instance != null ? EstadoJuego.Instance.CultivosEntregados : 0;
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
                FijarPista("Arrastra al pollito con el dedo hasta el gallinero que brilla.");
                break;
            case Paso.PollitoEnGallinero:
                FijarPista("Dale un choclo al pollito: tócalo para alimentarlo.");
                break;
            case Paso.Libre:
                FijarPista("");
                break;
        }
    }

    void AplicarResaltado()
    {
        if (paso == Paso.Inactiva) return;
        if (pizarra != null)
            pizarra.Resaltar(paso == Paso.AvisoPedido || paso == Paso.AvisoPollito);
        if (caja != null)
            caja.Resaltar(paso == Paso.EntregandoPedido);
        if (granero != null)
            granero.Resaltar(paso == Paso.BuscarSemillas);
    }

    string TextoProgreso(int entregados)
    {
        return $"Pedido: entrega {choclosPedido} choclos en la caja ({entregados}/{choclosPedido})";
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