using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class DatosGuardado
{
    public int version;          // 0 = partida anterior a las semillas
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
            version = 3,
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

    // Adapta partidas antiguas al formato actual.
    static void Migrar(DatosGuardado d)
    {
        if (d.version < 2)
        {
            // Se insertó el paso "BuscarSemillas" después de "Inactiva" en la misión.
            if (d.mision >= 1) d.mision++;

            // Quien ya tenía la llave jugaba con tomates ilimitados: se le dan semillas para seguir.
            if (d.estado != null && d.estado.semillasCultivo == null)
            {
                d.estado.semillasCultivo = new int[System.Enum.GetValues(typeof(Cultivo)).Length];
                if (d.estado.llave) d.estado.semillasCultivo[(int)Cultivo.Tomate] = 10;
            }
            d.version = 2;
        }

        if (d.version < 3)
        {
            // Fase 12b: el cultivo inicial pasó a ser el choclo y la gallina solo come choclo.
            // Quien ya tenía la llave recibe 12 semillas de choclo (las mismas que da Cholin).
            // Las cosechas por cultivo y los cultivos desbloqueados los calcula EstadoJuego.Importar.
            if (d.estado != null && d.estado.llave)
            {
                if (d.estado.semillasCultivo == null || d.estado.semillasCultivo.Length < System.Enum.GetValues(typeof(Cultivo)).Length)
                    d.estado.semillasCultivo = new int[System.Enum.GetValues(typeof(Cultivo)).Length];
                d.estado.semillasCultivo[(int)Cultivo.Maiz] += 12;
            }
            d.version = 3;
        }
    }

    void Cargar()
    {
        if (!PlayerPrefs.HasKey(Clave)) return;

        var datos = JsonUtility.FromJson<DatosGuardado>(PlayerPrefs.GetString(Clave));
        if (datos == null) return;
        Migrar(datos);

        if (EstadoJuego.Instance != null) EstadoJuego.Instance.Importar(datos.estado);
        if (TutorialController.Instance != null) TutorialController.Instance.Restaurar(datos.tutorial);
        if (MisionController.Instance != null) MisionController.Instance.Restaurar(datos.mision, datos.entregadosBase);
        if (HoraVirtual.Instance != null) HoraVirtual.Instance.Restaurar(datos.hora, datos.dia);

        if (TutorialController.Instance != null)
            TutorialController.Instance.Avisar("Partida cargada. Escanea el piso para colocar tu granja.", 4f);
    }
}