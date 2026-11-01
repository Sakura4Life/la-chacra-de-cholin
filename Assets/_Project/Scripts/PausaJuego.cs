using System.Collections.Generic;
using UnityEngine;

// Varios motivos pueden pausar el juego a la vez (menú, pérdida de seguimiento...).
public static class PausaJuego
{
    static readonly HashSet<string> motivos = new HashSet<string>();

    public static bool EnPausa => motivos.Count > 0;

    public static void Pedir(string motivo) { motivos.Add(motivo); Aplicar(); }
    public static void Soltar(string motivo) { motivos.Remove(motivo); Aplicar(); }
    public static void Limpiar() { motivos.Clear(); Aplicar(); }

    static void Aplicar() { Time.timeScale = motivos.Count > 0 ? 0f : 1f; }
}