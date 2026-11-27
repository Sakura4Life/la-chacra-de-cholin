using UnityEngine;

// Lo que el Corral necesita de un animal para emparejarlo: lo cumplen AnimalVisual (animales del corral)
// y Gallina (la gallina del tutorial), así los dos pueden ser pareja o madre.
public interface IReproductor
{
    Transform Cuerpo { get; }
    bool Moviendo { get; }
    bool Saltando { get; }
    bool Arriba { get; }                                   // está en la rampa del gallinero
    bool EnJuego { get; }                                  // sigue en el corral (no lo vendieron ni se lo llevó la caja)
    void Reservar(bool valor);                             // true = se queda quieto esperando; false = vuelve a su vida
    void IrAMundo(Vector3 mundo, float velocidadLocal);    // la gallina del tutorial usa su propia velocidad
    void MirarMundo(Vector3 punto);
    void Saltitos(int veces);
}

// Un adulto del corral que se puede vender en la tienda o llevar a la caja de pedidos, en cualquier estado
// (comiendo, con su pareja, en la rampa...). Lo cumplen AnimalVisual y Gallina: para el Corral, la tienda y los
// pedidos la gallina del tutorial es un animal más. Lo propio de ella vive en Gallina, no en quien la usa.
public interface IAnimalVendible : IReproductor
{
    Animal TipoAnimal { get; }
    bool EsAdulto { get; }
    int Raciones { get; }      // raciones comidas (engorde): de ellas depende lo que vale al venderlo

    // Lo saca del corral cortando lo que estuviera haciendo y devuelve sus datos.
    // definitivo = false: va a la caja de pedidos y puede volver. definitivo = true: se vende y no vuelve.
    DatosAnimal Sacar(bool definitivo);
}
