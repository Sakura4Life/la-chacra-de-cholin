using System;
using UnityEngine;

public enum Producto { Ninguno, Tomate, Huevo, Pollo, SemillaLegendaria }

[Serializable]
public class DatosEstado
{
    public bool llave, tienePollo, polloVendido, polloAdulto;
    public int monedas, agua, tomates, huevos, semillas, cosechados, entregados;
    public int productoBonus;
    public int[] semillasCultivo;   // una entrada por Cultivo
    public float segundosBonus;
}
public class EstadoJuego : MonoBehaviour
{
    public static EstadoJuego Instance { get; private set; }

    [Header("Agua")]
    [SerializeField] int aguaMaxima = 3;

    [Header("Precios base (provisionales)")]
    [SerializeField] int precioTomate = 5;
    [SerializeField] int precioHuevo = 8;
    [SerializeField] int precioPollo = 50;
    [SerializeField] int precioSemilla = 40;

    [Header("Bonificador")]
    [SerializeField] float segundosBonus = 120f;

    public bool TieneLlave { get; private set; }
    public int Monedas { get; private set; }
    public int Agua { get; private set; }
    public int Tomates { get; private set; }
    public int Huevos { get; private set; }
    public int SemillasLegendarias { get; private set; }
    public int TomatesCosechados { get; private set; }
    public int TomatesEntregados { get; private set; }
    public bool TienePollo { get; private set; }
    public bool PolloVendido { get; private set; }
    public bool PolloAdulto { get; private set; }
    public Producto ProductoBonus { get; private set; } = Producto.Ninguno;

    public int AguaMax => aguaMaxima;
    public bool HayParaVender => Tomates > 0 || Huevos > 0;
    public bool BonusActivo => ProductoBonus != Producto.Ninguno;
    public float SegundosRestantesBonus => BonusActivo ? Mathf.Max(0f, finBonus - Time.time) : 0f;

    float finBonus;
    readonly int[] semillasCultivo = new int[System.Enum.GetValues(typeof(Cultivo)).Length];

    public event Action OnCambio;

    void Awake() { Instance = this; }
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
        switch (p)
        {
            case Producto.Tomate: return "tomates";
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

    public void SumarTomates(int cantidad)
    {
        Tomates += cantidad;
        TomatesCosechados += cantidad;
        OnCambio?.Invoke();
    }

    public bool GastarTomate()
    {
        if (Tomates <= 0) return false;
        Tomates--;
        OnCambio?.Invoke();
        return true;
    }

    // ---------- Semillas ----------

    public int Semillas(Cultivo c) { return semillasCultivo[(int)c]; }

    public void SumarSemillas(Cultivo c, int cantidad)
    {
        semillasCultivo[(int)c] += cantidad;
        OnCambio?.Invoke();
    }

    public bool GastarSemilla(Cultivo c)
    {
        if (semillasCultivo[(int)c] <= 0) return false;
        semillasCultivo[(int)c]--;
        OnCambio?.Invoke();
        return true;
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
            tomates = Tomates,
            huevos = Huevos,
            semillas = SemillasLegendarias,
            cosechados = TomatesCosechados,
            entregados = TomatesEntregados,
            productoBonus = (int)ProductoBonus,
            semillasCultivo = (int[])semillasCultivo.Clone(),
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
        Tomates = d.tomates;
        Huevos = d.huevos;
        SemillasLegendarias = d.semillas;
        TomatesCosechados = d.cosechados;
        TomatesEntregados = d.entregados;
        ProductoBonus = (Producto)d.productoBonus;
        System.Array.Clear(semillasCultivo, 0, semillasCultivo.Length);
        if (d.semillasCultivo != null)
            for (int i = 0; i < semillasCultivo.Length && i < d.semillasCultivo.Length; i++)
                semillasCultivo[i] = d.semillasCultivo[i];
        finBonus = Time.time + d.segundosBonus;
        OnCambio?.Invoke();
    }

    // ---------- Ventas y bonificador ----------

    public int Cantidad(Producto p)
    {
        switch (p)
        {
            case Producto.Tomate: return Tomates;
            case Producto.Huevo: return Huevos;
            case Producto.Pollo: return TienePollo ? 1 : 0;
            case Producto.SemillaLegendaria: return SemillasLegendarias;
            default: return 0;
        }
    }

    public int PrecioBase(Producto p)
    {
        switch (p)
        {
            case Producto.Tomate: return precioTomate;
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
        cantidad = Cantidad(p);
        if (cantidad <= 0) return 0;

        int total = cantidad * PrecioBase(p) * Multiplicador(p);

        switch (p)
        {
            case Producto.Tomate: Tomates = 0; break;
            case Producto.Huevo: Huevos = 0; break;
            case Producto.Pollo: TienePollo = false; PolloVendido = true; break;
            case Producto.SemillaLegendaria: SemillasLegendarias = 0; break;
        }

        Monedas += total;
        OnCambio?.Invoke();
        return total;
    }

    // Venta de la caja de entrega (pedidos): tomates y huevos, con el bonificador si corresponde.
    public int VenderTodo(out int tomates, out int huevos)
    {
        tomates = Tomates;
        huevos = Huevos;
        int total = tomates * precioTomate * Multiplicador(Producto.Tomate)
                  + huevos * precioHuevo * Multiplicador(Producto.Huevo);
        Tomates = 0;
        Huevos = 0;
        Monedas += total;
        TomatesEntregados += tomates;
        OnCambio?.Invoke();
        return total;
    }

    // Gasta 1 semilla legendaria y duplica las ganancias de un producto durante un tiempo.
    public bool ActivarBonus(Producto p, out string motivo)
    {
        motivo = "";
        if (p != Producto.Tomate && p != Producto.Huevo && p != Producto.Pollo)
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