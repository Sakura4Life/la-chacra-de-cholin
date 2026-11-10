using TMPro;
using UnityEngine;

public class HudJuego : MonoBehaviour
{
    [SerializeField] TMP_Text textoMonedas;
    [SerializeField] TMP_Text textoAgua;
    [SerializeField] TMP_Text textoInventario;

    int ultimoSegundo = -1;

    void Start()
    {
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio += Refrescar;
        Refrescar();
    }

    void OnDestroy()
    {
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio -= Refrescar;
    }

    // Mientras hay bonificador, actualiza la cuenta atrás una vez por segundo.
    void Update()
    {
        var j = EstadoJuego.Instance;
        if (j == null || !j.BonusActivo) return;

        int s = Mathf.CeilToInt(j.SegundosRestantesBonus);
        if (s != ultimoSegundo)
        {
            ultimoSegundo = s;
            Refrescar();
        }
    }

    void Refrescar()
    {
        var j = EstadoJuego.Instance;
        if (j == null) return;

        textoMonedas.text = $"Monedas: {j.Monedas}";
        textoAgua.text = $"Agua: {j.Agua}/{j.AguaMax}";

        string inventario = $"Tomates: {j.Tomates}   Huevos: {j.Huevos}";
        if (j.TieneLlave)
            inventario += $"   Semillas: {j.Semillas(Cultivo.Tomate)}";
        if (j.SemillasLegendarias > 0)
            inventario += $"   Semilla legendaria: {j.SemillasLegendarias}";
        if (j.BonusActivo)
        {
            int s = Mathf.CeilToInt(j.SegundosRestantesBonus);
            inventario += $"   x2 {EstadoJuego.NombreProducto(j.ProductoBonus)} {s / 60}:{s % 60:00}";
        }
        textoInventario.text = inventario;
    }
}