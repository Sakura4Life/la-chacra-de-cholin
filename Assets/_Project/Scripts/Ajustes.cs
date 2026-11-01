using UnityEngine;

public static class Ajustes
{
    const string ClaveVibracion = "ajuste_vibracion";

    public static bool Vibracion
    {
        get => PlayerPrefs.GetInt(ClaveVibracion, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(ClaveVibracion, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static void Vibrar()
    {
        if (Vibracion) Handheld.Vibrate();
    }
}