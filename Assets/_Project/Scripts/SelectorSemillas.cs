using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Selector de semillas del HUD. Va en un objeto vacío (con RectTransform) dentro del Canvas.
// Crea por código un botón por cultivo y muestra SOLO los cultivos de los que tienes semillas,
// así que aparece solo cuando Cholin te da las primeras. El botón elegido se resalta;
// al sembrar se usa ese cultivo. Si se acaban las semillas del elegido, pasa solo a otro.
[RequireComponent(typeof(RectTransform))]
public class SelectorSemillas : MonoBehaviour
{
    [Header("Tamaño")]
    [SerializeField] Vector2 tamanoBoton = new Vector2(250f, 130f);
    [SerializeField] float separacion = 14f;
    [SerializeField] float tamanoTexto = 34f;

    [Header("Colores")]
    [SerializeField] Color colorNormal = new Color(0.10f, 0.10f, 0.10f, 0.65f);
    [SerializeField] Color colorElegido = new Color(0.96f, 0.76f, 0.20f, 0.95f);
    [SerializeField] Color textoNormal = Color.white;
    [SerializeField] Color textoElegido = new Color(0.12f, 0.08f, 0.02f, 1f);

    class Boton
    {
        public Cultivo cultivo;
        public GameObject objeto;
        public Image fondo;
        public TMP_Text texto;
    }

    readonly List<Boton> botones = new List<Boton>();

    void Start()
    {
        var capa = GetComponent<HorizontalLayoutGroup>();
        if (capa == null) capa = gameObject.AddComponent<HorizontalLayoutGroup>();
        capa.childAlignment = TextAnchor.MiddleCenter;
        capa.spacing = separacion;
        capa.childControlWidth = true;
        capa.childControlHeight = true;
        capa.childForceExpandWidth = false;
        capa.childForceExpandHeight = false;

        foreach (var c in CultivoDatos.Todos) botones.Add(CrearBoton(c));

        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio += Refrescar;
        Refrescar();
    }

    void OnDestroy()
    {
        if (EstadoJuego.Instance != null) EstadoJuego.Instance.OnCambio -= Refrescar;
    }

    Boton CrearBoton(Cultivo c)
    {
        var go = new GameObject("Semilla_" + CultivoDatos.Titulo(c), typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var fondo = go.AddComponent<Image>();
        var boton = go.AddComponent<Button>();
        boton.targetGraphic = fondo;

        var medida = go.AddComponent<LayoutElement>();
        medida.preferredWidth = tamanoBoton.x;
        medida.preferredHeight = tamanoBoton.y;

        var hijo = new GameObject("Texto", typeof(RectTransform));
        hijo.transform.SetParent(go.transform, false);
        var rt = (RectTransform)hijo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(6f, 6f);
        rt.offsetMax = new Vector2(-6f, -6f);

        var texto = hijo.AddComponent<TextMeshProUGUI>();
        texto.alignment = TextAlignmentOptions.Center;
        texto.fontSize = tamanoTexto;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.raycastTarget = false;

        boton.onClick.AddListener(() =>
        {
            if (EstadoJuego.Instance != null) EstadoJuego.Instance.SeleccionarCultivo(c);
        });

        return new Boton { cultivo = c, objeto = go, fondo = fondo, texto = texto };
    }

    void Refrescar()
    {
        var j = EstadoJuego.Instance;
        if (j == null) return;

        foreach (var b in botones)
        {
            int semillas = j.Semillas(b.cultivo);
            bool visible = semillas > 0;
            if (b.objeto.activeSelf != visible) b.objeto.SetActive(visible);
            if (!visible) continue;

            bool elegido = j.CultivoSeleccionado == b.cultivo;
            b.fondo.color = elegido ? colorElegido : colorNormal;
            b.texto.color = elegido ? textoElegido : textoNormal;
            b.texto.text = $"{CultivoDatos.Titulo(b.cultivo)}\n{semillas}";
        }
    }
}
