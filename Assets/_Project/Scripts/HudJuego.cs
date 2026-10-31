using TMPro;
using UnityEngine;

public class HudJuego : MonoBehaviour
{
    [SerializeField] TMP_Text textoMonedas;
    [SerializeField] TMP_Text textoAgua;
    [SerializeField] TMP_Text textoInventario;

    void Start()
    {
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio += Refrescar;
        Refrescar();
    }

    void OnDestroy()
    {
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio -= Refrescar;
    }

    void Refrescar()
    {
        var j = EstadoJuego.Instance;
        if (j == null) return;

        textoMonedas.text = $"Monedas: {j.Monedas}";
        textoAgua.text = $"Agua: {j.Agua}/{j.AguaMax}";

        string inventario = $"Tomates: {j.Tomates}   Huevos: {j.Huevos}";
        if (j.SemillasLegendarias > 0)
            inventario += $"   Semilla legendaria: {j.SemillasLegendarias}";
        textoInventario.text = inventario;
    }
}