using System.Collections.Generic;
using UnityEngine;

// Varios motivos pueden pausar el juego a la vez (menú, pérdida de seguimiento...).
// Pausar detiene el tiempo de Unity (Time.timeScale) y el reloj de cultivos y gallina (RelojReal).
public static class PausaJuego
{
    static readonly HashSet<string> motivos = new HashSet<string>();

    public const string MotivoMenu = "menu";

    // Se guardó la partida con el menú de pausa abierto: hay que volver a abrirlo al colocar la granja.
    public static bool PendienteReabrir;

    public static bool EnPausa => motivos.Count > 0;
    public static bool MenuAbierto => motivos.Contains(MotivoMenu);

    public static void Pedir(string motivo) { motivos.Add(motivo); Aplicar(); }
    public static void Soltar(string motivo) { motivos.Remove(motivo); Aplicar(); }
    public static void Limpiar() { motivos.Clear(); PendienteReabrir = false; Aplicar(); }

    static void Aplicar()
    {
        bool pausa = motivos.Count > 0;
        Time.timeScale = pausa ? 0f : 1f;
        if (pausa) RelojReal.Pausar();
        else RelojReal.Reanudar();

        // El tiempo fuera del juego deja de ignorarse cuando ya no hay pausa ni pausa pendiente de reabrir.
        if (!pausa && !PendienteReabrir) RelojReal.IgnorarFuera = false;
    }
}
