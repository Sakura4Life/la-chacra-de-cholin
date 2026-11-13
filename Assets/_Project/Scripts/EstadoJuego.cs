using System;
using UnityEngine;

// Los productos nuevos van al final para no alterar los valores ya guardados.
public enum Producto { Ninguno, Tomate, Huevo, Pollo, SemillaLegendaria, Maiz, Arroz, Papa }

[Serializable]
public class DatosEstado
{
    public bool llave, tienePollo, polloVendido, polloAdulto;
    public int monedas, agua, tomates, huevos, semillas, cosechados, entregados;
    public int productoBonus;
    public int[] semillasCultivo;   // una entrada por Cultivo
    public int[] cosecha;           // una entrada por Cultivo (null = partida anterior a la 12b)
    public int desbloqueados;       // bits: un 1 en la posición de cada cultivo que ya se obtuvo
    public int cultivoSeleccionado;
    public float segundosBonus;
}

public class EstadoJuego : MonoBehaviour
{
    public static EstadoJuego Instance { get; private set; }

    [Header("Agua")]
    [SerializeField] int aguaMaxima = 3;

    [Header("Precios base (provisionales). Los cultivos están en CultivoDatos")]
    [SerializeField] int precioHuevo = 8;
    [SerializeField] int precioPollo = 50;
    [SerializeField] int precioSemilla = 40;

    [Header("Bonificador")]
    [SerializeField] float segundosBonus = 120f;

    [Header("Pruebas en el editor y en el celular")]
    [Tooltip("Solo para probar: al iniciar suma 5 semillas de cada cultivo (se guardan en la partida). Desmárcalo al terminar.")]
    [SerializeField] bool semillasDePrueba;

    static readonly int NumCultivos = Enum.GetValues(typeof(Cultivo)).Length;

    public bool TieneLlave { get; private set; }
    public int Monedas { get; private set; }
    public int Agua { get; private set; }
    public int Huevos { get; private set; }
    public int SemillasLegendarias { get; private set; }
    public int CosechasTotales { get; private set; }
    public int CultivosEntregados { get; private set; }
    public bool TienePollo { get; private set; }
    public bool PolloVendido { get; private set; }
    public bool PolloAdulto { get; private set; }
    public Producto ProductoBonus { get; private set; } = Producto.Ninguno;
    public Cultivo CultivoSeleccionado { get; private set; } = Cultivo.Maiz;

    // Compatibilidad con la tienda actual (se reemplaza en la 12b-2).
    public int Tomates => Cosecha(Cultivo.Tomate);

    public int AguaMax => aguaMaxima;
    public bool BonusActivo => ProductoBonus != Producto.Ninguno;
    public float SegundosRestantesBonus => BonusActivo ? Mathf.Max(0f, finBonus - Time.time) : 0f;


    float finBonus;
    readonly int[] semillasCultivo = new int[NumCultivos];
    readonly int[] cosecha = new int[NumCultivos];
    int desbloqueados;

    public event Action OnCambio;

    void Awake() { Instance = this; }

    // Espera dos fotogramas para que la partida guardada ya esté cargada y no pise las semillas de prueba.
    System.Collections.IEnumerator Start()
    {
        yield return null;
        yield return null;
        if (!semillasDePrueba) yield break;
        foreach (var c in CultivoDatos.Todos) SumarSemillas(c, 5);
    }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (BonusActivo && Time.time >= finBonus)
        {
            ProductoBonus = Producto.Ninguno;
            OnCambio?.Invoke();
        }
    }

    public static string NombreProducto(Producto p)
    {
        if (CultivoDatos.EsCultivo(p, out Cultivo c)) return CultivoDatos.Plural(c);
        switch (p)
        {
            case Producto.Huevo: return "huevos";
            case Producto.Pollo: return "pollo";
            case Producto.SemillaLegendaria: return "semilla legendaria";
            default: return "nada";
        }
    }

    // ---------- Recursos ----------

    public void DarLlave() { TieneLlave = true; OnCambio?.Invoke(); }
    public void LlenarAgua() { Agua = aguaMaxima; OnCambio?.Invoke(); }

    public bool GastarAgua()
    {
        if (Agua <= 0) return false;
        Agua--;
        OnCambio?.Invoke();
        return true;
    }

    // ---------- Cosechas ----------

    public int Cosecha(Cultivo c) { return cosecha[(int)c]; }

    public void SumarCosecha(Cultivo c, int cantidad)
    {
        cosecha[(int)c] += cantidad;
        CosechasTotales += cantidad;
        OnCambio?.Invoke();
    }

    public bool GastarCosecha(Cultivo c)
    {
        if (cosecha[(int)c] <= 0) return false;
        cosecha[(int)c]--;
        OnCambio?.Invoke();
        return true;
    }

    // ---------- Semillas ----------

    public int Semillas(Cultivo c) { return semillasCultivo[(int)c]; }

    // Un cultivo se "desbloquea" la primera vez que se obtienen semillas suyas.
    public bool Desbloqueado(Cultivo c) { return (desbloqueados & (1 << (int)c)) != 0; }

    public void SumarSemillas(Cultivo c, int cantidad)
    {
        semillasCultivo[(int)c] += cantidad;
        if (cantidad > 0) desbloqueados |= 1 << (int)c;
        AjustarSeleccion();
        OnCambio?.Invoke();
    }

    public bool GastarSemilla(Cultivo c)
    {
        if (semillasCultivo[(int)c] <= 0) return false;
        semillasCultivo[(int)c]--;
        AjustarSeleccion();
        OnCambio?.Invoke();
        return true;
    }

    // Solo se puede elegir un cultivo del que tengas semillas.
    public void SeleccionarCultivo(Cultivo c)
    {
        if (Semillas(c) <= 0 || CultivoSeleccionado == c) return;
        CultivoSeleccionado = c;
        OnCambio?.Invoke();
    }

    // Si el cultivo elegido se queda sin semillas, pasa al primero que sí tenga (en el orden de CultivoDatos.Todos).
    void AjustarSeleccion()
    {
        if (semillasCultivo[(int)CultivoSeleccionado] > 0) return;
        foreach (var c in CultivoDatos.Todos)
        {
            if (semillasCultivo[(int)c] <= 0) continue;
            CultivoSeleccionado = c;
            return;
        }
    }

    // Movimientos del pedido de la caja: pasan productos del inventario a la caja y de vuelta.
    // Devolver no cuenta como cosecha nueva.
    public void DevolverCosecha(Cultivo c, int cantidad) { cosecha[(int)c] += cantidad; OnCambio?.Invoke(); }

    public bool GastarHuevo()
    {
        if (Huevos <= 0) return false;
        Huevos--;
        OnCambio?.Invoke();
        return true;
    }

    public void DevolverHuevo(int cantidad) { Huevos += cantidad; OnCambio?.Invoke(); }

    // Paga un pedido entregado y cuenta los cultivos entregados (los usa la misión del tutorial).
    public void CobrarPedido(int monedas, int cultivos)
    {
        Monedas += monedas;
        CultivosEntregados += cultivos;
        OnCambio?.Invoke();
    }

    public void SumarHuevo() { Huevos++; OnCambio?.Invoke(); }
    public void DarSemillaLegendaria() { SemillasLegendarias++; OnCambio?.Invoke(); }
    public void PonerPolloEnGallinero() { TienePollo = true; OnCambio?.Invoke(); }

    // ---------- Guardado ----------

    public void MarcarPolloAdulto() { PolloAdulto = true; OnCambio?.Invoke(); }

    public DatosEstado Exportar()
    {
        return new DatosEstado
        {
            llave = TieneLlave,
            tienePollo = TienePollo,
            polloVendido = PolloVendido,
            polloAdulto = PolloAdulto,
            monedas = Monedas,
            agua = Agua,
            tomates = Cosecha(Cultivo.Tomate),
            huevos = Huevos,
            semillas = SemillasLegendarias,
            cosechados = CosechasTotales,
            entregados = CultivosEntregados,
            productoBonus = (int)ProductoBonus,
            semillasCultivo = (int[])semillasCultivo.Clone(),
            cosecha = (int[])cosecha.Clone(),
            desbloqueados = desbloqueados,
            cultivoSeleccionado = (int)CultivoSeleccionado,
            segundosBonus = SegundosRestantesBonus
        };
    }

    public void Importar(DatosEstado d)
    {
        if (d == null) return;
        TieneLlave = d.llave;
        TienePollo = d.tienePollo;
        PolloVendido = d.polloVendido;
        PolloAdulto = d.polloAdulto;
        Monedas = d.monedas;
        Agua = d.agua;
        Huevos = d.huevos;
        SemillasLegendarias = d.semillas;
        CosechasTotales = d.cosechados;
        CultivosEntregados = d.entregados;
        ProductoBonus = (Producto)d.productoBonus;

        Array.Clear(semillasCultivo, 0, semillasCultivo.Length);
        if (d.semillasCultivo != null)
            for (int i = 0; i < semillasCultivo.Length && i < d.semillasCultivo.Length; i++)
                semillasCultivo[i] = d.semillasCultivo[i];

        Array.Clear(cosecha, 0, cosecha.Length);
        bool anterior = d.cosecha == null;     // partida guardada antes de la 12b
        if (anterior)
            cosecha[(int)Cultivo.Tomate] = d.tomates;
        else
            for (int i = 0; i < cosecha.Length && i < d.cosecha.Length; i++)
                cosecha[i] = d.cosecha[i];

        desbloqueados = d.desbloqueados;
        if (anterior)
        {
            // Se consideran desbloqueados los cultivos de los que ya había semillas o cosecha.
            for (int i = 0; i < NumCultivos; i++)
                if (semillasCultivo[i] > 0 || cosecha[i] > 0) desbloqueados |= 1 << i;
        }

        var elegido = (Cultivo)Mathf.Clamp(d.cultivoSeleccionado, 0, NumCultivos - 1);
        if (anterior || !Desbloqueado(elegido))
        {
            elegido = Cultivo.Maiz;
            foreach (var c in CultivoDatos.Todos)
                if (Desbloqueado(c)) { elegido = c; break; }
        }
        CultivoSeleccionado = elegido;
        AjustarSeleccion();

        finBonus = Time.time + d.segundosBonus;
        OnCambio?.Invoke();
    }

    // ---------- Ventas y bonificador ----------

    public int Cantidad(Producto p)
    {
        if (CultivoDatos.EsCultivo(p, out Cultivo c)) return Cosecha(c);
        switch (p)
        {
            case Producto.Huevo: return Huevos;
            case Producto.Pollo: return TienePollo ? 1 : 0;
            case Producto.SemillaLegendaria: return SemillasLegendarias;
            default: return 0;
        }
    }

    public int PrecioBase(Producto p)
    {
        if (CultivoDatos.EsCultivo(p, out Cultivo c)) return CultivoDatos.PrecioVenta(c);
        switch (p)
        {
            case Producto.Huevo: return precioHuevo;
            case Producto.Pollo: return precioPollo;
            case Producto.SemillaLegendaria: return precioSemilla;
            default: return 0;
        }
    }

    public int Multiplicador(Producto p)
    {
        return BonusActivo && ProductoBonus == p ? 2 : 1;
    }

    // Vende todo lo que tengas de ese producto (con el bonificador si corresponde).
    public int VenderProducto(Producto p, out int cantidad)
    {
        return VenderCantidad(p, Cantidad(p), out cantidad);
    }

    // Vende hasta 'cantidad' unidades de un producto (con el bonificador si corresponde).
    // El pollo se vende de a uno. Devuelve las monedas ganadas y, en 'vendidas', cuántas unidades salieron.
    public int VenderCantidad(Producto p, int cantidad, out int vendidas)
    {
        vendidas = Mathf.Clamp(cantidad, 0, Cantidad(p));
        if (p == Producto.Pollo) vendidas = Mathf.Min(vendidas, 1);
        if (vendidas <= 0) return 0;

        int total = vendidas * PrecioBase(p) * Multiplicador(p);

        if (CultivoDatos.EsCultivo(p, out Cultivo c)) cosecha[(int)c] -= vendidas;
        else
        {
            switch (p)
            {
                case Producto.Huevo: Huevos -= vendidas; break;
                case Producto.Pollo: TienePollo = false; PolloVendido = true; break;
                case Producto.SemillaLegendaria: SemillasLegendarias -= vendidas; break;
            }
        }

        Monedas += total;
        OnCambio?.Invoke();
        return total;
    }

    // Gasta monedas (por ejemplo, al comprar una parcela). Devuelve false si no alcanzan.
    public bool GastarMonedas(int cantidad)
    {
        if (cantidad < 0 || Monedas < cantidad) return false;
        Monedas -= cantidad;
        OnCambio?.Invoke();
        return true;
    }

    // Compra semillas de un cultivo con monedas. Devuelve false si no alcanzan las monedas.
    public bool ComprarSemillas(Cultivo c, int cantidad, out int costo)
    {
        costo = 0;
        if (cantidad <= 0) return false;
        costo = cantidad * CultivoDatos.PrecioSemilla(c);
        if (Monedas < costo) return false;

        Monedas -= costo;
        SumarSemillas(c, cantidad);   // desbloquea el cultivo y avisa del cambio
        return true;
    }

    // Gasta 1 semilla legendaria y duplica las ganancias de un producto durante un tiempo.
    public bool ActivarBonus(Producto p, out string motivo)
    {
        motivo = "";
        bool valido = p == Producto.Huevo || p == Producto.Pollo || CultivoDatos.EsCultivo(p, out _);
        if (!valido)
        {
            motivo = "Ese producto no admite bonificador.";
            return false;
        }
        if (BonusActivo)
        {
            motivo = "Ya hay un bonificador activo.";
            return false;
        }
        if (SemillasLegendarias <= 0)
        {
            motivo = "Necesitas una semilla legendaria.";
            return false;
        }

        SemillasLegendarias--;
        ProductoBonus = p;
        finBonus = Time.time + segundosBonus;
        OnCambio?.Invoke();
        return true;
    }
}
