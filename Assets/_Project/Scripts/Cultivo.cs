using UnityEngine;

// El orden de este enum NO se debe cambiar: las partidas guardadas usan su posición.
public enum Cultivo { Tomate, Maiz, Arroz, Papa }

// Datos de balance de cada cultivo. Para ajustar precios o tiempos se edita aquí.
public static class CultivoDatos
{
    // Orden en el que se muestran (selector y tienda): el choclo es el cultivo inicial.
    public static readonly Cultivo[] Todos = { Cultivo.Maiz, Cultivo.Tomate, Cultivo.Papa, Cultivo.Arroz };

    public static string Nombre(Cultivo c)
    {
        switch (c)
        {
            case Cultivo.Maiz: return "choclo";
            case Cultivo.Tomate: return "tomate";
            case Cultivo.Arroz: return "arroz";
            default: return "papa";
        }
    }

    public static string Plural(Cultivo c)
    {
        switch (c)
        {
            case Cultivo.Maiz: return "choclos";
            case Cultivo.Tomate: return "tomates";
            case Cultivo.Arroz: return "arroz";
            default: return "papas";
        }
    }

    public static string Titulo(Cultivo c)
    {
        string n = Nombre(c);
        return char.ToUpper(n[0]) + n.Substring(1);
    }

    public static string TituloPlural(Cultivo c)
    {
        string n = Plural(c);
        return char.ToUpper(n[0]) + n.Substring(1);
    }

    // Monedas que cuesta una semilla en la tienda.
    public static int PrecioSemilla(Cultivo c)
    {
        switch (c)
        {
            case Cultivo.Maiz: return 2;
            case Cultivo.Tomate: return 3;
            case Cultivo.Papa: return 5;
            default: return 6;
        }
    }

    // Monedas que vale una unidad cosechada.
    public static int PrecioVenta(Cultivo c)
    {
        switch (c)
        {
            case Cultivo.Maiz: return 4;
            case Cultivo.Tomate: return 8;
            case Cultivo.Papa: return 12;
            default: return 15;
        }
    }

    // Segundos de crecimiento (tiempos de prueba; los reales se ajustan en el balance final).
    public static float SegundosCrecimiento(Cultivo c)
    {
        switch (c)
        {
            case Cultivo.Maiz: return 8f;
            case Cultivo.Tomate: return 10f;
            case Cultivo.Papa: return 12f;
            default: return 15f;
        }
    }

    public static Producto ProductoDe(Cultivo c)
    {
        switch (c)
        {
            case Cultivo.Maiz: return Producto.Maiz;
            case Cultivo.Tomate: return Producto.Tomate;
            case Cultivo.Arroz: return Producto.Arroz;
            default: return Producto.Papa;
        }
    }

    public static bool EsCultivo(Producto p, out Cultivo c)
    {
        switch (p)
        {
            case Producto.Maiz: c = Cultivo.Maiz; return true;
            case Producto.Tomate: c = Cultivo.Tomate; return true;
            case Producto.Arroz: c = Cultivo.Arroz; return true;
            case Producto.Papa: c = Cultivo.Papa; return true;
        }
        c = Cultivo.Maiz;
        return false;
    }
}
