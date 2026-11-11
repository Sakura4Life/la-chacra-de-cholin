using System.Text;
using TMPro;
using UnityEngine;

public class HudJuego : MonoBehaviour
{
    [SerializeField] TMP_Text textoMonedas;
    [SerializeField] TMP_Text textoAgua;
    [SerializeField] TMP_Text textoInventario;

    int ultimoSegundo = -1;
    readonly StringBuilder sb = new StringBuilder();

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

        // Cosechas: solo los cultivos que ya conoces (las semillas están en el selector).
        sb.Clear();
        foreach (var c in CultivoDatos.Todos)
        {
            if (!j.Desbloqueado(c) && j.Cosecha(c) <= 0) continue;
            sb.Append(CultivoDatos.TituloPlural(c)).Append(": ").Append(j.Cosecha(c)).Append("   ");
        }
        sb.Append("Huevos: ").Append(j.Huevos);

        if (j.SemillasLegendarias > 0)
            sb.Append("   Semilla legendaria: ").Append(j.SemillasLegendarias);
        if (j.BonusActivo)
        {
            int s = Mathf.CeilToInt(j.SegundosRestantesBonus);
            sb.Append($"   x2 {EstadoJuego.NombreProducto(j.ProductoBonus)} {s / 60}:{s % 60:00}");
        }
        textoInventario.text = sb.ToString();
    }
}
