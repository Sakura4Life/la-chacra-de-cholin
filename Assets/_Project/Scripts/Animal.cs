using UnityEngine;

// Los valores no se pueden reordenar: se guardan como números (los nuevos van al final).
// Gallo no tiene corral propio: vive en el gallinero (ver EspecieCorral).
public enum Animal { Gallina, Cuy, Cerdo, Llama, Vaca, Gallo }
public enum Alimento { Heno, Maiz, Tomate, Papa, Arroz }
// Los valores se guardan como bits: los nuevos van siempre al final.
public enum Edificio { CriaderoCuy, Ampliacion }

// Datos de cada especie. Los precios y tiempos son de prueba (se afinan en el balance final).
public static class AnimalDatos
{
    public const int PrecioHeno = 2;

    // Gallos que puede haber a la vez en el gallinero (comprados + nacidos).
    public const int MaxGallos = 2;
    // Tamaño de una cría recién nacida respecto al adulto (cuando comparte modelo con él).
    public const float EscalaCria = 0.55f;
    // Raciones tras las que un animal que engorda llega a su tamaño máximo (y a su valor máximo al venderlo).
    public const int MaxRaciones = 8;
    // Cuánto sube el valor de venta con cada ración (7,5 % del precio base): con MaxRaciones vale x1,6.
    public const float ValorPorRacion = 0.075f;

    // Especies que ya se pueden comprar (las demás llegan en fases siguientes).
    public static readonly Animal[] Disponibles = { Animal.Gallina, Animal.Gallo, Animal.Cuy };

    public static string Nombre(Animal a)
    {
        switch (a)
        {
            case Animal.Gallina: return "gallina";
            case Animal.Gallo: return "gallo";
            case Animal.Cuy: return "cuy";
            case Animal.Cerdo: return "cerdo";
            case Animal.Llama: return "llama";
            default: return "vaca";
        }
    }

    public static string Plural(Animal a)
    {
        switch (a)
        {
            case Animal.Gallina: return "gallinas";
            case Animal.Gallo: return "gallos";
            case Animal.Cuy: return "cuyes";
            case Animal.Cerdo: return "cerdos";
            case Animal.Llama: return "llamas";
            default: return "vacas";
        }
    }

    public static string Titulo(Animal a)
    {
        string n = Nombre(a);
        return char.ToUpper(n[0]) + n.Substring(1);
    }

    public static string TituloPlural(Animal a)
    {
        string n = Plural(a);
        return char.ToUpper(n[0]) + n.Substring(1);
    }

    public static bool Femenino(Animal a)
    {
        return a == Animal.Gallina || a == Animal.Llama || a == Animal.Vaca;
    }

    // El gallo vive en el gallinero.
    public static Animal EspecieCorral(Animal a)
    {
        return a == Animal.Gallo ? Animal.Gallina : a;
    }

    public static int PrecioCompra(Animal a)
    {
        switch (a)
        {
            case Animal.Gallina: return 20;
            case Animal.Gallo: return 30;
            case Animal.Cuy: return 25;
            case Animal.Cerdo: return 40;
            case Animal.Llama: return 50;
            default: return 80;
        }
    }

    // Precio de un animal sin engorde (0 raciones). Lo que paga la tienda es Valor().
    public static int PrecioVenta(Animal a)
    {
        switch (a)
        {
            case Animal.Gallina: return 12;
            case Animal.Gallo: return 20;
            case Animal.Cuy: return 15;
            case Animal.Cerdo: return 28;
            case Animal.Llama: return 35;
            default: return 55;
        }
    }

    // Animales que se pueden COMPRAR para un corral (no cuenta la gallina del tutorial ni las crías).
    public static int Capacidad(Animal a)
    {
        Animal e = EspecieCorral(a);
        return (e == Animal.Gallina || e == Animal.Cuy) ? 4 : 2;
    }

    // Crías que puede haber nacido en el corral (cupo aparte del de compra).
    public static int MaxCrias(Animal a)
    {
        Animal e = EspecieCorral(a);
        return (e == Animal.Gallina || e == Animal.Cuy) ? 2 : 1;
    }

    // ¿Engorda al comer? (gallina, gallo, cuy y cerdo: su tamaño y su valor suben con cada ración).
    public static bool Engorda(Animal a)
    {
        return a == Animal.Gallina || a == Animal.Gallo || a == Animal.Cuy || a == Animal.Cerdo;
    }

    // Tamaño relativo según las raciones comidas (+3 % por ración, hasta MaxRaciones).
    public static float FactorEngorde(int raciones)
    {
        return 1f + 0.03f * Mathf.Min(Mathf.Max(0, raciones), MaxRaciones);
    }

    // Lo que vale un adulto al venderlo: el precio base sube un 7,5 % por cada ración comida (hasta MaxRaciones).
    public static int Valor(Animal a, int raciones)
    {
        int basico = PrecioVenta(a);
        if (!Engorda(a)) return basico;
        int r = Mathf.Min(Mathf.Max(0, raciones), MaxRaciones);
        return Mathf.RoundToInt(basico * (1f + ValorPorRacion * r));
    }

    // Segundos que tarda un animal en comer una ración.
    public static float SegundosComer(Animal a) { return 10f; }

    // Giro que hay que darle al modelo 3D (.glb) para que quede de pie y mire hacia delante (+Z).
    // - El cuy se exportó con el eje Z hacia arriba (salía echado de lado).
    // - La gallina se exportó mirando hacia un lado (+X en Unity): se gira 90° para que camine de frente.
    // - El gallo y el pollito ya miran hacia +Z.
    // 'modeloAparte' = el modelo de la cría es otro archivo (pollito): no necesita corrección.
    public static Quaternion Correccion(Animal a, bool modeloAparte)
    {
        if (modeloAparte) return Quaternion.identity;
        if (a == Animal.Cuy) return Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-90f, 0f, 0f);
        if (a == Animal.Gallina) return Quaternion.Euler(0f, -90f, 0f);
        return Quaternion.identity;
    }

    public static bool Acepta(Animal a, Alimento al)
    {
        if (al == Alimento.Heno) return true;       // el heno lo comen todos
        // la gallina y el gallo también comen choclo
        return (a == Animal.Gallina || a == Animal.Gallo) && al == Alimento.Maiz;
    }

    public static Alimento[] Alimentos(Animal a)
    {
        if (a == Animal.Gallina || a == Animal.Gallo) return new[] { Alimento.Heno, Alimento.Maiz };
        return new[] { Alimento.Heno };
    }
}

public static class AlimentoDatos
{
    public static string Nombre(Alimento a)
    {
        switch (a)
        {
            case Alimento.Heno: return "heno";
            case Alimento.Maiz: return "choclo";
            case Alimento.Tomate: return "tomate";
            case Alimento.Papa: return "papa";
            default: return "arroz";
        }
    }

    public static string Titulo(Alimento a)
    {
        string n = Nombre(a);
        return char.ToUpper(n[0]) + n.Substring(1);
    }

    public static bool EsCultivo(Alimento a, out Cultivo c)
    {
        switch (a)
        {
            case Alimento.Maiz: c = Cultivo.Maiz; return true;
            case Alimento.Tomate: c = Cultivo.Tomate; return true;
            case Alimento.Papa: c = Cultivo.Papa; return true;
            case Alimento.Arroz: c = Cultivo.Arroz; return true;
            default: c = Cultivo.Maiz; return false;
        }
    }
}

// Datos de cada edificio de la tienda. Los precios son de prueba (se afinan en el balance final).
public static class EdificioDatos
{
    public static string Nombre(Edificio e)
    {
        switch (e)
        {
            case Edificio.Ampliacion: return "Ampliación de granja";
            default: return "Criadero de cuy";
        }
    }

    public static int Precio(Edificio e)
    {
        switch (e)
        {
            case Edificio.Ampliacion: return 80;
            default: return 40;
        }
    }

    // Lo que dice la tienda debajo del nombre.
    public static string Descripcion(Edificio e)
    {
        switch (e)
        {
            case Edificio.Ampliacion: return "Suma terreno junto al granero para los corrales nuevos.";
            default: return "Se coloca donde estaba el heno.";
        }
    }

    // Mensaje al comprarlo.
    public static string MensajeCompra(Edificio e, int precio)
    {
        switch (e)
        {
            case Edificio.Ampliacion:
                return "¡Ampliaste la granja! Ya tienes terreno nuevo junto al granero. -" + precio + " monedas.";
            default:
                return "¡Compraste el " + Nombre(e).ToLower() + "! -" + precio + " monedas.";
        }
    }

    public static Animal Especie(Edificio e) { return Animal.Cuy; }
}
