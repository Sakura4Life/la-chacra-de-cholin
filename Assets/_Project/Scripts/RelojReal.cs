using System;
using UnityEngine;

// Reloj de pared (hora real del teléfono). Los cultivos y la gallina miden su tiempo con él,
// así siguen avanzando mientras el juego está en segundo plano o cerrado.
// Ahora (reloj de juego) se congela mientras el juego está en pausa (menú, pérdida de seguimiento...),
// de modo que una pausa detiene también cultivos y gallina.
public static class RelojReal
{
    // Máximo de tiempo "fuera" que se reconoce al volver (Guardado lo ajusta desde el Inspector).
    public static float TopeSegundos = 24f * 3600f;

    // Verdadero cuando se cargó una partida guardada con el menú de pausa abierto: ese tiempo fuera no cuenta.
    public static bool IgnorarFuera;

    static bool pausado;
    static long inicioPausaTicks;
    static long pausadoTicks;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        pausado = false;
        IgnorarFuera = false;
        inicioPausaTicks = 0;
        pausadoTicks = 0;
    }

    // Reloj de juego en segundos: hora real menos el tiempo que estuvo en pausa. Solo sirve para diferencias.
    public static double Ahora
    {
        get
        {
            long t = pausado ? inicioPausaTicks : DateTime.UtcNow.Ticks;
            return (t - pausadoTicks) / (double)TimeSpan.TicksPerSecond;
        }
    }

    // Marca de hora real (UTC) para guardar y calcular el tiempo fuera del juego.
    public static long TicksAhora => DateTime.UtcNow.Ticks;

    public static void Pausar()
    {
        if (pausado) return;
        pausado = true;
        inicioPausaTicks = DateTime.UtcNow.Ticks;
    }

    public static void Reanudar()
    {
        if (!pausado) return;
        pausadoTicks += DateTime.UtcNow.Ticks - inicioPausaTicks;
        pausado = false;
    }

    // Segundos que pasaron desde 'ticks' (momento del guardado), sin pasar del tope.
    public static float Transcurrido(long ticks)
    {
        if (ticks <= 0 || IgnorarFuera) return 0f;
        double s = (DateTime.UtcNow.Ticks - ticks) / (double)TimeSpan.TicksPerSecond;
        return (float)Math.Min(Math.Max(0.0, s), TopeSegundos);
    }
}
