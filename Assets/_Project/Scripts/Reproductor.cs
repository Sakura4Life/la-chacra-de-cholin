using UnityEngine;

// Lo que el Corral necesita de un animal para emparejarlo: lo cumplen AnimalVisual (animales del corral)
// y Gallina (la gallina del tutorial), así los dos pueden ser pareja o madre.
public interface IReproductor
{
    Transform Cuerpo { get; }
    bool Moviendo { get; }
    bool Saltando { get; }
    bool Arriba { get; }                                   // está en la rampa del gallinero
    void Reservar(bool valor);                             // true = se queda quieto esperando; false = vuelve a su vida
    void IrAMundo(Vector3 mundo, float velocidadLocal);    // la gallina del tutorial usa su propia velocidad
    void MirarMundo(Vector3 punto);
    void Saltitos(int veces);
}
