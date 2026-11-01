using UnityEngine;

// Marca la raíz de la granja para que otros scripts la encuentren.
public class RaizGranja : MonoBehaviour
{
    public static RaizGranja Instance { get; private set; }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }
}