using System;
using UnityEngine;

public class EstadoJuego : MonoBehaviour
{
    public static EstadoJuego Instance { get; private set; }

    [SerializeField] int aguaMaxima = 3;
    [SerializeField] int precioTomate = 5;
    [SerializeField] int precioHuevo = 8;

    public bool TieneLlave { get; private set; }
    public int Monedas { get; private set; }
    public int Agua { get; private set; }
    public int Tomates { get; private set; }
    public int Huevos { get; private set; }
    public int SemillasLegendarias { get; private set; }
    public int TomatesCosechados { get; private set; }
    public int TomatesEntregados { get; private set; }
    public int AguaMax => aguaMaxima;
    public bool HayParaVender => Tomates > 0 || Huevos > 0;

    public event Action OnCambio;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

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

    public void SumarHuevo() { Huevos++; OnCambio?.Invoke(); }

    public void DarSemillaLegendaria() { SemillasLegendarias++; OnCambio?.Invoke(); }

    public int VenderTodo(out int tomates, out int huevos)
    {
        tomates = Tomates;
        huevos = Huevos;
        int total = tomates * precioTomate + huevos * precioHuevo;
        Tomates = 0;
        Huevos = 0;
        Monedas += total;
        TomatesEntregados += tomates;
        OnCambio?.Invoke();
        return total;
    }
}