using UnityEngine;

public class Interactuable : MonoBehaviour
{
    public enum Tipo { Pizarra, Pozo }

    [SerializeField] Tipo tipo;
    public Tipo TipoObjeto => tipo;

    Resaltado resaltado;

    void Awake() { resaltado = GetComponent<Resaltado>(); }

    void Start()
    {
        if (TutorialController.Instance != null)
            TutorialController.Instance.Registrar(this);
    }

    public void Resaltar(bool valor)
    {
        if (resaltado != null) resaltado.Activar(valor);
    }

    public void Tocar()
    {
        if (TutorialController.Instance != null)
            TutorialController.Instance.AlTocar(this);
    }
}