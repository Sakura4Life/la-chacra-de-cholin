using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class DatosGuardado
{
    public DatosEstado estado;
    public int tutorial;
    public int mision;
    public int entregadosBase;
    public float hora;
    public int dia;
}

public class Guardado : MonoBehaviour
{
    const string Clave = "guardado_v1";
    static bool bloqueado;

    [SerializeField] float segundosAutoguardado = 20f;

    float proximo;
    bool listo;

    void Awake()
    {
        PausaJuego.Limpiar();
        bloqueado = false;
    }

    IEnumerator Start()
    {
        yield return null;   // deja que los demás scripts hagan su Start primero
        Cargar();
        listo = true;
        proximo = Time.unscaledTime + segundosAutoguardado;
    }

    void Update()
    {
        if (!listo || Time.unscaledTime < proximo) return;
        proximo = Time.unscaledTime + segundosAutoguardado;
        GuardarAhora();
    }

    void OnApplicationPause(bool pausada) { if (pausada && listo) GuardarAhora(); }
    void OnApplicationQuit() { if (listo) GuardarAhora(); }

    public static void GuardarAhora()
    {
        if (bloqueado) return;

        var juego = EstadoJuego.Instance;
        var tutorial = TutorialController.Instance;
        var mision = MisionController.Instance;
        var hora = HoraVirtual.Instance;
        if (juego == null || tutorial == null || mision == null) return;

        var datos = new DatosGuardado
        {
            estado = juego.Exportar(),
            tutorial = tutorial.PasoActual,
            mision = mision.PasoActual,
            entregadosBase = mision.EntregadosBase,
            hora = hora != null ? hora.Progreso : 0f,
            dia = hora != null ? hora.Dia : 1
        };

        PlayerPrefs.SetString(Clave, JsonUtility.ToJson(datos));
        PlayerPrefs.Save();
    }

    public static void Borrar()
    {
        bloqueado = true;
        PlayerPrefs.DeleteKey(Clave);
        PlayerPrefs.Save();
    }

    void Cargar()
    {
        if (!PlayerPrefs.HasKey(Clave)) return;

        var datos = JsonUtility.FromJson<DatosGuardado>(PlayerPrefs.GetString(Clave));
        if (datos == null) return;

        if (EstadoJuego.Instance != null) EstadoJuego.Instance.Importar(datos.estado);
        if (TutorialController.Instance != null) TutorialController.Instance.Restaurar(datos.tutorial);
        if (MisionController.Instance != null) MisionController.Instance.Restaurar(datos.mision, datos.entregadosBase);
        if (HoraVirtual.Instance != null) HoraVirtual.Instance.Restaurar(datos.hora, datos.dia);

        if (TutorialController.Instance != null)
            TutorialController.Instance.Avisar("Partida cargada. Escanea el piso para colocar tu granja.", 4f);
    }
}