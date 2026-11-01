using TMPro;
using UnityEngine;

public class HoraVirtual : MonoBehaviour
{
    public static HoraVirtual Instance { get; private set; }

    [SerializeField] TMP_Text textoHora;
    [SerializeField] Light sol;
    [SerializeField] float minutosPorDia = 15f;
    [SerializeField] float horaInicio = 6f;
    [SerializeField] float horaFin = 18f;

    [Header("Luz")]
    [SerializeField] Color colorAmanecer = new Color(1.00f, 0.75f, 0.50f);
    [SerializeField] Color colorMediodia = new Color(1.00f, 0.97f, 0.90f);
    [SerializeField] Color colorAtardecer = new Color(1.00f, 0.55f, 0.30f);

    float progreso;   // 0 = amanecer, 1 = atardecer

    public float Progreso => progreso;
    public int Dia { get; private set; } = 1;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start() { Aplicar(); }

    void Update()
    {
        // El tiempo solo corre cuando la granja está colocada.
        if (!ControladorGranja.GranjaColocada) return;

        progreso += Time.deltaTime / (minutosPorDia * 60f);
        if (progreso >= 1f)
        {
            progreso = 0f;
            Dia++;
            if (TutorialController.Instance != null)
                TutorialController.Instance.Avisar($"Amanece el día {Dia}.", 4f);
        }
        Aplicar();
    }

    public void Restaurar(float nuevoProgreso, int dia)
    {
        progreso = Mathf.Clamp01(nuevoProgreso);
        Dia = Mathf.Max(1, dia);
        Aplicar();
    }

    void Aplicar()
    {
        float hora = Mathf.Lerp(horaInicio, horaFin, progreso);
        int h = (int)hora;
        int m = (int)((hora - h) * 60f);
        if (textoHora != null) textoHora.text = $"{h:00}:{m:00}";

        if (sol != null)
        {
            sol.transform.rotation = Quaternion.Euler(Mathf.Lerp(20f, 160f, progreso), -30f, 0f);
            sol.color = progreso < 0.5f
                ? Color.Lerp(colorAmanecer, colorMediodia, progreso * 2f)
                : Color.Lerp(colorMediodia, colorAtardecer, (progreso - 0.5f) * 2f);
        }
    }
}