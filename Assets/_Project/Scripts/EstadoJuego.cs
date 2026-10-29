using System;
using UnityEngine;

public class EstadoJuego : MonoBehaviour
{
    public static EstadoJuego Instance { get; private set; }

    [SerializeField] int aguaMaxima = 3;

    public bool TieneLlave { get; private set; }
    public int Monedas { get; private set; }
    public int Agua { get; private set; }
    public int AguaMax => aguaMaxima;

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

    public void SumarMonedas(int cantidad) { Monedas += cantidad; OnCambio?.Invoke(); }
}