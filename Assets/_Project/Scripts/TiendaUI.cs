using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class TiendaUI : MonoBehaviour
{
    [Header("Abrir y cerrar")]
    [SerializeField] Button botonTienda;
    [SerializeField] GameObject panel;
    [SerializeField] Button botonCerrar;

    [Header("Filas de venta")]
    [SerializeField] FilaVenta filaTomates;
    [SerializeField] FilaVenta filaHuevos;
    [SerializeField] FilaVenta filaSemilla;
    [SerializeField] FilaVenta filaPollo;

    [Header("Bonificador x2")]
    [SerializeField] TMP_Text textoBonus;
    [SerializeField] Button botonBonusTomate;
    [SerializeField] Button botonBonusHuevo;
    [SerializeField] Button botonBonusPollo;

    [Header("Mensajes")]
    [SerializeField] TMP_Text textoMensaje;

    Producto pendiente = Producto.Ninguno;
    float vencePendiente;
    float proximoRefresco;

    void Start()
    {
        panel.SetActive(false);
        botonTienda.gameObject.SetActive(false);

        botonTienda.onClick.AddListener(Abrir);
        botonCerrar.onClick.AddListener(Cerrar);

        filaTomates.boton.onClick.AddListener(() => Vender(Producto.Tomate));
        filaHuevos.boton.onClick.AddListener(() => Vender(Producto.Huevo));
        filaSemilla.boton.onClick.AddListener(() => Vender(Producto.SemillaLegendaria));
        filaPollo.boton.onClick.AddListener(() => Vender(Producto.Pollo));

        botonBonusTomate.onClick.AddListener(() => ActivarBonus(Producto.Tomate));
        botonBonusHuevo.onClick.AddListener(() => ActivarBonus(Producto.Huevo));
        botonBonusPollo.onClick.AddListener(() => ActivarBonus(Producto.Pollo));

        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio += Refrescar;
        Refrescar();
    }

    void OnDestroy()
    {
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio -= Refrescar;
    }

    void Update()
    {
        if (!panel.activeSelf || Time.unscaledTime < proximoRefresco) return;
        proximoRefresco = Time.unscaledTime + 0.25f;
        Refrescar();
    }

    void Abrir()
    {
        textoMensaje.text = "";
        pendiente = Producto.Ninguno;
        panel.SetActive(true);
        Refrescar();
    }

    void Cerrar()
    {
        pendiente = Producto.Ninguno;
        panel.SetActive(false);
    }

    void Vender(Producto p)
    {
        var j = EstadoJuego.Instance;
        if (j == null) return;

        // El pollo y la semilla piden confirmación: tocar dos veces seguidas.
        if (p == Producto.Pollo || p == Producto.SemillaLegendaria)
        {
            bool confirmando = pendiente == p && Time.unscaledTime <= vencePendiente;
            if (!confirmando)
            {
                pendiente = p;
                vencePendiente = Time.unscaledTime + 3f;
                textoMensaje.text = $"Toca otra vez para vender: {EstadoJuego.NombreProducto(p)}.";
                Refrescar();
                return;
            }
            pendiente = Producto.Ninguno;
        }

        int total = j.VenderProducto(p, out int cantidad);
        textoMensaje.text = total > 0
            ? $"Vendiste {cantidad} de {EstadoJuego.NombreProducto(p)}: +{total} monedas"
            : "No tienes nada de eso para vender.";
        Refrescar();
    }

    void ActivarBonus(Producto p)
    {
        var j = EstadoJuego.Instance;
        if (j == null) return;

        if (j.ActivarBonus(p, out string motivo))
            textoMensaje.text = $"¡Bonificador activo! Lo que vendas de {EstadoJuego.NombreProducto(p)} vale el doble.";
        else
            textoMensaje.text = motivo;

        Refrescar();
    }

    void Refrescar()
    {
        var j = EstadoJuego.Instance;
        if (j == null) return;

        botonTienda.gameObject.SetActive(j.TieneLlave);
        if (!panel.activeSelf) return;

        Pintar(filaTomates, "Tomates", Producto.Tomate);
        Pintar(filaHuevos, "Huevos", Producto.Huevo);
        Pintar(filaSemilla, "Semilla legendaria", Producto.SemillaLegendaria);
        Pintar(filaPollo, "Pollo", Producto.Pollo);

        if (j.BonusActivo)
        {
            int s = Mathf.CeilToInt(j.SegundosRestantesBonus);
            textoBonus.text = $"Bonificador activo: {EstadoJuego.NombreProducto(j.ProductoBonus)} x2  ({s / 60}:{s % 60:00})";
        }
        else if (j.SemillasLegendarias > 0)
        {
            textoBonus.text = "Gasta una semilla legendaria para duplicar las ganancias de un producto durante un tiempo:";
        }
        else
        {
            textoBonus.text = "Necesitas una semilla legendaria para activar un bonificador.";
        }

        bool puede = !j.BonusActivo && j.SemillasLegendarias > 0;
        botonBonusTomate.interactable = puede;
        botonBonusHuevo.interactable = puede;
        botonBonusPollo.interactable = puede && j.TienePollo;
    }

    void Pintar(FilaVenta fila, string nombre, Producto p)
    {
        var j = EstadoJuego.Instance;
        int cantidad = j.Cantidad(p);
        int mult = j.Multiplicador(p);
        int unidad = j.PrecioBase(p) * mult;

        fila.texto.text = $"{nombre}: {cantidad}\n{unidad} monedas c/u" + (mult > 1 ? "  (x2)" : "");

        bool confirmando = pendiente == p && Time.unscaledTime <= vencePendiente;
        fila.textoBoton.text = confirmando ? "¿Seguro?" : "Vender";
        fila.boton.interactable = cantidad > 0;
    }
}