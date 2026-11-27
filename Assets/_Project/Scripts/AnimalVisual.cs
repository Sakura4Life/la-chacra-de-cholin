using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Un animal dentro de su corral: lo crea Corral por código. Come, pasea por el corral esquivando a los demás,
// entra en celo, se empareja, crece (si es cría y entonces sigue a uno de sus padres), engorda (cuy, cerdo)...
// Todo el movimiento se hace en coordenadas locales del corral, así que sigue bien si se mueve o escala la granja.
public class AnimalVisual : MonoBehaviour, IAnimalVendible
{
    public Corral Corral { get; private set; }
    public Animal Tipo { get; private set; }          // lo que es (o será, si es cría)
    public bool ModeloAparte { get; private set; }    // la cría usa otro modelo (pollito) que se cambia al crecer
    public float Tamano { get; private set; }         // tamaño normalizado del modelo (unidades de granja)

    public bool Cria;          // todavía no creció
    public bool Nacido;        // nació en la granja (ocupa un cupo de crías)
    public int Raciones { get; set; }   // raciones comidas (engorde)
    public int Puesto = -1;    // puesto donde apareció (también cuenta para los cupos)
    public int Variante;       // 0 = normal; 1 = otro diseño (gallina2) o color (cuy mostaza)
    public bool Reservado;     // el corral lo tiene esperando o emparejándose
    public Transform Padre;    // solo crías: a quién sigue mientras crece

    // Celo: se activa al darle de comer (gallo, cuy...). Mientras dura busca o espera pareja.
    public bool EnCelo;
    public double FinCelo;
    public double LibreDesde;          // hasta esa hora descansa y no entra en celo
    public float ProximoCorazon;

    public bool Comiendo { get; private set; }
    public bool Moviendo { get; private set; }
    public bool EnRampa { get; private set; }         // en la rampa del gallinero o yendo hacia ella
    public bool Saltando { get { return saltosPendientes > 0; } }
    public bool Ocupado { get { return Reservado || Saltando; } }

    // IReproductor
    public Transform Cuerpo { get { return transform; } }
    public bool Arriba { get { return EnRampa; } }
    public bool EnJuego { get { return enabled; } }

    // IAnimalVendible
    public Animal TipoAnimal { get { return Tipo; } }
    public bool EsAdulto { get { return !Cria; } }

    // Lo vende la tienda o se lo lleva un pedido: sale del corral sin importar lo que estuviera haciendo
    // (comer, emparejarse, la rampa...). Devuelve sus datos para poder devolverlo igual a la caja.
    public DatosAnimal Sacar(bool definitivo)
    {
        var datos = new DatosAnimal
        {
            cria = false,
            gallo = Tipo == Animal.Gallo,
            nacido = Nacido,
            raciones = Raciones,
            variante = Variante,
            tienePuesto = false
        };
        Corral.Quitar(this);
        return datos;
    }

    // Segundos que lleva creciendo (solo crías).
    public float Crecido
    {
        get { return Cria ? Mathf.Max(0f, (float)(RelojReal.Ahora - inicioCrecer)) : 0f; }
    }

    public float ComidoSegundos
    {
        get { return Comiendo ? Mathf.Max(0f, (float)(RelojReal.Ahora - inicioComer)) : 0f; }
    }

    // Radio aproximado de su cuerpo, para no pisar a los demás.
    public float Radio { get { return Tamano * 0.4f * escActual; } }

    // Hacia dónde va (último punto de su camino) o dónde está si está quieto.
    public Vector3 DestinoFinal
    {
        get { return Moviendo && ruta.Count > 0 ? ruta[ruta.Count - 1] : transform.localPosition; }
    }

    double inicioComer;
    double inicioCrecer;
    float segundosComer = 10f;
    bool engorda;
    float escalaIni = 1f, escalaFin = 1f;     // tamaño de la cría al nacer y al terminar de crecer
    float escActual = 1f;
    float pop = 1f;                            // escala de la animación de aparición

    Transform modelo;
    Vector3 posBase;                              // posición y giro del modelo dentro del contenedor
    Quaternion rotBase = Quaternion.identity;     // (incluye la corrección de orientación del modelo)

    float proximoGiro;
    float proximoPaseo;
    float proximaRevisionSeguir;
    bool modoSeguir;
    Quaternion giroObjetivo = Quaternion.identity;   // giro local deseado

    // Caminar y saltar (coordenadas locales del corral)
    readonly List<Vector3> ruta = new List<Vector3>();
    readonly List<Vector3> unico = new List<Vector3>();
    readonly List<Vector3> plan = new List<Vector3>();
    int rutaIdx;
    bool rampaFinal;
    Vector3 posSueloLocal;
    float velocidad = 0.05f;
    int saltosPendientes;
    float tSalto;
    const float DuracionSalto = 0.32f;

    // Al caminar gira poco a poco (no de golpe al esquivar a otro): como mucho estos grados por segundo.
    const float GiroMaximoMov = 240f;
    Vector3 dirMov;                   // dirección con la que camina ahora (suavizada); cero = aún sin dirección

    // Esquivar (lo usa Corral.Esquivar): lado elegido para rodear a quien tiene delante y hasta cuándo lo mantiene.
    [System.NonSerialized] public int LadoEsquive;
    [System.NonSerialized] public float FinEsquive;

    Vector3 empujeVel;                // velocidad para apartarse de quien lo pisa (la calcula Separar)
    float solapeFraccion;             // cuánto lo pisan, en radios (0 = nada)
    Vector3 dirFiltrada;              // dirección deseada ya filtrada (cero = sin dirección aún)
    float rapidezMarcha;              // fracción de su velocidad a la que avanza de verdad (0 = no avanza)
    float proximoRepaso;              // cuándo vuelve a comprobar que no tiene una parcela o edificio en medio
    float tBloqueado;                 // segundos seguidos que lleva casi sin avanzar
    readonly List<Vector3> rodeo = new List<Vector3>();

    // Caminar con saltitos, como la gallina del tutorial (AnimacionAve, anim. Caminar): rebota, se balancea de
    // lado a lado y se aplasta un poco en cada paso. Solo se anima el modelo (y el tamaño del contenedor); la
    // posición real del animal no cambia, así que no afecta a choques, paredes ni rampa.
    const float AlturaPaso = 0.165f;      // altura del rebote, como fracción de su tamaño
    const float FrecuenciaPaso = 8f;
    const float BalanceoGrados = 8f;
    float marcha;                         // 0 = quieto, 1 = andando del todo
    float desfaseMarcha;
    float marchaAncho = 1f, marchaAlto = 1f;
    bool marchaPuesta;

    // Todos los animales caminan así menos el cuy (también los futuros: cerdo, llama, vaca...).
    static bool CaminaConSaltitos(Animal tipo) { return tipo != Animal.Cuy; }

    // Llamar a Cargar ANTES de Iniciar (Cargar fija si es cría y cuánto lleva creciendo).
    public void Cargar(bool cria, float crecido, int raciones)
    {
        Cria = cria;
        inicioCrecer = RelojReal.Ahora - Mathf.Max(0f, crecido);
        Raciones = raciones;
    }

    public void Iniciar(Corral corral, Animal tipo, float segundos, bool modeloAparte, float tamano,
                        float escalaInicial, float escalaFinal)
    {
        Corral = corral;
        Tipo = tipo;
        segundosComer = segundos;
        ModeloAparte = modeloAparte;
        Tamano = tamano;
        escalaIni = escalaInicial;
        escalaFin = escalaFinal;
        engorda = AnimalDatos.Engorda(tipo);

        modelo = transform.childCount > 0 ? transform.GetChild(0) : null;
        if (modelo != null)
        {
            posBase = modelo.localPosition;
            rotBase = modelo.localRotation;
        }
        giroObjetivo = transform.localRotation;
        proximoGiro = Time.time + Random.Range(2f, 6f);
        proximoPaseo = Time.time + Random.Range(1f, 4f);
        desfaseMarcha = Random.Range(0f, 10f);

        escActual = ObjetivoEscala();
        AplicarEscala();
    }

    // 'yaComido' > 0 al restaurar una partida. Se mide con la hora real y se congela en pausa.
    public void Comer(float yaComido = 0f)
    {
        Frenar();
        marcha = 0f;
        RestaurarMarcha();
        Comiendo = true;
        inicioComer = RelojReal.Ahora - yaComido;
    }

    // Pequeño salto de aparición al comprarlo o nacer.
    public void Aparecer()
    {
        pop = 0.01f;
        AplicarEscala();
        StartCoroutine(Pop());
    }

    IEnumerator Pop()
    {
        const float duracion = 0.4f;
        float t = 0f;
        while (t < duracion)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duracion) - 1f;
            float s = 1f + 2.70158f * k * k * k + 1.70158f * k * k;   // easeOutBack
            pop = Mathf.Max(0.01f, s);
            AplicarEscala();
            yield return null;
        }
        pop = 1f;
        AplicarEscala();
    }

    // ---------- Tamaño ----------

    float ObjetivoEscala()
    {
        if (Cria)
        {
            float total = Corral != null ? Corral.SegundosCrecer : 40f;
            float k = Mathf.Clamp01(Crecido / Mathf.Max(1f, total));
            return Mathf.Lerp(escalaIni, escalaFin, k);
        }
        return engorda ? AnimalDatos.FactorEngorde(Raciones) : 1f;
    }

    void AplicarEscala()
    {
        transform.localScale = new Vector3(marchaAncho, marchaAlto, marchaAncho) * (pop * escActual);
    }

    // Anima el caminar con saltitos (ver arriba). Se llama una vez por fotograma, después de mover al animal.
    void AplicarMarcha()
    {
        // Solo da saltitos si de verdad avanza (bloqueado no "camina en el sitio").
        bool anda = Moviendo && !Comiendo && CaminaConSaltitos(Tipo) && rapidezMarcha > 0.3f;
        marcha = Mathf.MoveTowards(marcha, anda ? 1f : 0f, Time.deltaTime * 6f);   // entra y sale con suavidad
        if (marcha <= 0f)
        {
            if (marchaPuesta) RestaurarMarcha();
            return;
        }

        float t = Time.time + desfaseMarcha;
        float onda = Mathf.Sin(t * FrecuenciaPaso);
        float paso = Mathf.Abs(onda);
        float y = paso * AlturaPaso * Tamano * marcha;
        Quaternion giro = Quaternion.Euler(0f, 0f, onda * BalanceoGrados * marcha);
        marchaAlto = 1f - 0.06f * (1f - paso) * marcha;
        marchaAncho = 1f + 0.03f * (1f - paso) * marcha;
        if (modelo != null)
        {
            modelo.localRotation = giro * rotBase;
            modelo.localPosition = giro * posBase + Vector3.up * y;
        }
        marchaPuesta = true;
        AplicarEscala();
    }

    // Deja el modelo y el tamaño tal como estaban antes de empezar a caminar.
    void RestaurarMarcha()
    {
        marchaPuesta = false;
        marchaAlto = 1f;
        marchaAncho = 1f;
        if (modelo != null && !Comiendo)
        {
            modelo.localRotation = rotBase;
            modelo.localPosition = posBase;
        }
        AplicarEscala();
    }

    // ---------- Movimiento (lo piden desde Corral o lo decide el propio animal) ----------

    // Camina por los puntos de 'puntos' (locales del corral) a 'vel' unidades de granja por segundo.
    public void MoverRuta(IList<Vector3> puntos, float vel, bool terminaEnRampa)
    {
        if (puntos == null || puntos.Count == 0) return;
        if (saltosPendientes > 0)
        {
            saltosPendientes = 0;
            transform.localPosition = posSueloLocal;
        }
        if (!Moviendo)
        {
            posSueloLocal = transform.localPosition;
            rapidezMarcha = 1f;
        }

        // Cada punto se corrige para quedar dentro de las paredes / fuera de parcelas y edificios, y entre punto y
        // punto el camino rodea esas zonas (así nunca cruza una parcela ni el cuerpo del gallinero).
        ruta.Clear();
        Vector3 anterior = posSueloLocal;
        for (int i = 0; i < puntos.Count; i++)
        {
            if (Corral == null) { ruta.Add(puntos[i]); continue; }
            Vector3 q = Corral.Limitar(puntos[i], Radio);
            Corral.CaminoLibre(anterior, q, Radio, ruta);
            anterior = q;
        }
        rutaIdx = 0;
        tControl = 0f;
        // Arranca mirando hacia donde ya mira y gira poco a poco hacia el camino (sin girar de golpe). Si ya
        // caminaba, conserva su dirección.
        if (!Moviendo)
        {
            Vector3 f = transform.localRotation * Vector3.forward;
            f.y = 0f;
            dirMov = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.zero;
            dirFiltrada = Vector3.zero;
            tBloqueado = 0f;
        }
        velocidad = Mathf.Max(0.001f, vel);
        rampaFinal = terminaEnRampa;
        if (terminaEnRampa) EnRampa = true;
        Moviendo = true;
    }

    public void MoverLocal(Vector3 destino, float vel)
    {
        unico.Clear();
        unico.Add(destino);
        MoverRuta(unico, vel, false);
    }

    public void MirarLocal(Vector3 punto)
    {
        Vector3 d = punto - transform.localPosition;
        d.y = 0f;
        if (d.sqrMagnitude > 1e-8f) giroObjetivo = Quaternion.LookRotation(d.normalized, Vector3.up);
        proximoGiro = Time.time + 4f;
    }

    public void Saltitos(int veces)
    {
        if (Moviendo || Comiendo || veces <= 0) return;
        posSueloLocal = transform.localPosition;
        saltosPendientes = veces;
        tSalto = 0f;
    }

    // Se queda donde está.
    public void Frenar()
    {
        if (Moviendo)
        {
            Moviendo = false;
            ruta.Clear();
            transform.localPosition = ConRampa(posSueloLocal);
        }
        if (saltosPendientes > 0)
        {
            saltosPendientes = 0;
            transform.localPosition = ConRampa(posSueloLocal);
        }
        modoSeguir = false;
    }

    // Si está sobre la rampa del gallinero, sube su altura a la de la superficie para no atravesarla.
    Vector3 ConRampa(Vector3 p)
    {
        if (Corral != null && Corral.AlturaRampa(p, out float y)) p.y = y;
        return p;
    }

    // Corta lo que estuviera haciendo y lo libera.
    public void Detener()
    {
        Frenar();
        Reservado = false;
    }

    // IReproductor (coordenadas del mundo)
    public void Reservar(bool valor)
    {
        Reservado = valor;
        if (valor) Frenar();
    }

    public void IrAMundo(Vector3 mundo, float velocidadLocal)
    {
        Transform p = transform.parent;
        MoverLocal(p != null ? p.InverseTransformPoint(mundo) : mundo, velocidadLocal);
    }

    public void MirarMundo(Vector3 punto)
    {
        Transform p = transform.parent;
        MirarLocal(p != null ? p.InverseTransformPoint(punto) : punto);
    }

    // Cada segundo y medio comprueba que se está acercando a su meta; si no (se le cruzan otros animales y
    // se bloquean), abandona esa ruta y elige otra.
    float tControl;
    float distControl;

    void AvanzarMovimiento()
    {
        // Si al esquivar o al apartarse de otros quedó una parcela o un edificio entre él y su siguiente punto,
        // rehace el rodeo desde donde está (antes seguía de frente y caminaba contra el borde).
        if (Corral != null && Time.time >= proximoRepaso)
        {
            proximoRepaso = Time.time + 0.3f;
            if (Corral.CaminoBloqueado(posSueloLocal, ruta[rutaIdx], Radio))
            {
                rodeo.Clear();
                Corral.CaminoLibre(posSueloLocal, ruta[rutaIdx], Radio, rodeo);
                if (rodeo.Count > 1)
                {
                    ruta.RemoveAt(rutaIdx);
                    ruta.InsertRange(rutaIdx, rodeo);
                    tControl = 0f;
                }
            }
        }

        Vector3 destino = ruta[rutaIdx];
        Vector3 d = destino - posSueloLocal;
        Vector3 planoXZ = new Vector3(d.x, 0f, d.z);
        float dist = planoXZ.magnitude;
        float paso = velocidad * Time.deltaTime;

        // Llega al punto. Los puntos intermedios (esquinas del rodeo) se dan por pasados un poco antes, y la meta
        // final también si ya está casi encima (sin dar vueltas a su alrededor cuando otro lo empuja). Solo la
        // puerta de la rampa exige llegar exacto.
        bool ultimo = rutaIdx >= ruta.Count - 1;
        bool exacto = dist <= paso || dist < 1e-6f;
        bool basta = !exacto && dist < Tamano * (ultimo ? 0.15f : 0.25f) && !(ultimo && rampaFinal);
        if (exacto || basta)
        {
            if (exacto) posSueloLocal = destino;
            rutaIdx++;
            tControl = 0f;
            if (rutaIdx >= ruta.Count)
            {
                Moviendo = false;
                EnRampa = rampaFinal;
                ruta.Clear();
                modoSeguir = false;
            }
            transform.localPosition = ConRampa(posSueloLocal);
            return;
        }

        if (!Reservado && !modoSeguir)
        {
            if (tControl <= 0f) { tControl = Time.time + 1.5f; distControl = dist; }
            else if (Time.time >= tControl)
            {
                if (distControl - dist < Tamano * 0.2f)
                {
                    // Atascado: se queda un momento y vuelve a decidir adónde ir.
                    if (rampaFinal) EnRampa = false;   // iba a subir (estaba en el suelo): aún no está arriba
                    Frenar();
                    proximoPaseo = Time.time + Random.Range(0.2f, 1.2f);
                    return;
                }
                tControl = Time.time + 1.5f;
                distControl = dist;
            }
        }

        Vector3 dir = planoXZ / dist;
        if (!Reservado && Corral != null) dir = Corral.Esquivar(this, posSueloLocal, dir, dist);

        // Pegado a una parcela, un edificio o la pared del recinto: sigue el borde (y lo mira) en vez de ir contra él.
        if (Corral != null)
        {
            Vector3 libre = Corral.Deslizar(posSueloLocal, dir, Radio);
            if (libre.sqrMagnitude > 0.04f) dir = libre.normalized;
        }

        // Gira poco a poco hacia donde debe ir (sin cambios bruscos al esquivar). Cerca de la meta gira más rápido,
        // para no dar vueltas a su alrededor (antes allí giraba de golpe y se le veía sacudirse).
        // La dirección deseada se filtra un poco (~0,12 s): cuando esquivar o deslizarse por un borde la hacen
        // oscilar de un lado a otro, el animal ya no se sacude; solo sigue la tendencia.
        if (dirFiltrada.sqrMagnitude < 0.01f || dist < Tamano * 0.5f) dirFiltrada = dir;
        else
        {
            dirFiltrada = Vector3.Lerp(dirFiltrada, dir, 1f - Mathf.Exp(-Time.deltaTime * 8f));
            dirFiltrada = dirFiltrada.sqrMagnitude > 1e-4f ? dirFiltrada.normalized : dir;
        }
        dir = dirFiltrada;

        Vector3 deseada = dir;
        if (dirMov.sqrMagnitude < 0.25f) dirMov = dir;
        else
        {
            float giro = GiroMaximoMov * (dist < Tamano * 0.5f ? 3f : 1f);
            dirMov = Vector3.RotateTowards(dirMov, dir, giro * Mathf.Deg2Rad * Time.deltaTime, 0f);
        }
        dir = dirMov;

        // Su paso más lo que se aparta de quien lo pisa, pero nunca más rápido que su velocidad de paseo (antes
        // se sumaban y al chocar salía disparado) y sin empujar contra parcelas, edificios ni paredes.
        // Mientras gira mucho (p. ej. al dar media vuelta) avanza más despacio, como un animal de verdad.
        // Si otro lo pisa mucho, cede: camina menos y se aparta más (sin pasar nunca de su velocidad).
        float cede = Mathf.Clamp01((solapeFraccion - 0.3f) / 0.7f);
        Vector3 vel = dir * (velocidad * Mathf.Clamp(Vector3.Dot(dir, deseada), 0.35f, 1f) * (1f - 0.7f * cede));
        if (Corral != null)
        {
            vel = Vector3.ClampMagnitude(vel + empujeVel, velocidad);
            vel = Corral.Deslizar(posSueloLocal, vel, Radio);
        }
        rapidezMarcha = Mathf.MoveTowards(rapidezMarcha, vel.magnitude / velocidad, Time.deltaTime * 4f);

        // Casi no avanza (otro le corta el paso de frente): en vez de quedarse empujando, a los 0,8 s se para y
        // enseguida elige otro sitio adonde ir.
        if (!Reservado && !modoSeguir)
        {
            tBloqueado = vel.magnitude < velocidad * 0.2f ? tBloqueado + Time.deltaTime : 0f;
            if (tBloqueado > 0.8f)
            {
                tBloqueado = 0f;
                if (rampaFinal) EnRampa = false;
                Frenar();
                proximoPaseo = Time.time + Random.Range(0.3f, 1f);
                return;
            }
        }

        float k = paso / dist;
        posSueloLocal += vel * Time.deltaTime;
        posSueloLocal.y = Mathf.Lerp(posSueloLocal.y, destino.y, k);
        if (Corral != null) posSueloLocal = Corral.Limitar(posSueloLocal, Radio);
        giroObjetivo = Quaternion.LookRotation(dir, Vector3.up);

        // Los que caminan con saltitos (AplicarMarcha) rebotan con el modelo; solo el cuy lo hace moviendo todo el cuerpo.
        float rebote = CaminaConSaltitos(Tipo) ? 0f
            : Mathf.Abs(Mathf.Sin(Time.time * 14f)) * Tamano * 0.05f * Mathf.Clamp01(rapidezMarcha * 2f);
        transform.localPosition = ConRampa(posSueloLocal) + Vector3.up * rebote;
    }

    void AvanzarSaltos()
    {
        tSalto += Time.deltaTime;
        float fase = tSalto / DuracionSalto;
        if (fase >= 1f)
        {
            saltosPendientes--;
            tSalto = 0f;
            fase = 0f;
            if (saltosPendientes <= 0)
            {
                transform.localPosition = posSueloLocal;
                return;
            }
        }
        transform.localPosition = posSueloLocal + Vector3.up * (Mathf.Sin(fase * Mathf.PI) * Tamano * 0.3f);
    }

    // Se aparta de los animales (y de la gallina del tutorial) que tiene demasiado cerca.
    // Caminando, solo calcula 'empujeVel' (AvanzarMovimiento lo suma a su paso sin pasar de su velocidad).
    // Quieto: si lo pisan un poco se aparta despacio; si lo pisan bastante, da unos pasos para apartarse
    // (caminando, en vez de salir deslizado de lado).
    void Separar()
    {
        empujeVel = Vector3.zero;
        solapeFraccion = 0f;
        if (Reservado || Corral == null) return;
        Vector3 p = Moviendo ? posSueloLocal : transform.localPosition;
        Vector3 solape = Corral.Empuje(this, p);
        float cuanto = solape.magnitude;
        solapeFraccion = cuanto / Mathf.Max(1e-5f, Radio);

        // Un roce pequeño se tolera: si no, se corrigen sin parar y tiemblan.
        if (cuanto > Radio * 0.12f)
        {
            // Deshace el solape en ~0,3 s, como mucho a 0,8 veces la velocidad de paseo, y nunca contra una pared.
            empujeVel = Vector3.ClampMagnitude(solape * 3.5f, Corral.VelocidadPaseo * 0.8f);
            empujeVel = Corral.Deslizar(p, empujeVel, Radio);
        }

        if (Moviendo)
        {
            posSueloLocal = Corral.Limitar(posSueloLocal, Radio);
            return;
        }
        if (saltosPendientes > 0) return;

        if (cuanto > Radio * 0.45f && !EnRampa)
        {
            Vector3 lejos = solape / cuanto;
            Vector3 meta = Corral.Limitar(p + lejos * (cuanto + Radio * 0.6f), Radio);
            meta.y = p.y;
            MoverLocal(meta, Corral.VelocidadPaseo * 0.8f);
            proximoPaseo = Mathf.Max(proximoPaseo, Time.time + Random.Range(1f, 2.5f));
            return;
        }

        Vector3 q = transform.localPosition;
        Vector3 c = Corral.Limitar(q + empujeVel * (0.5f * Time.deltaTime), Radio);
        c.y = q.y;
        if (c != q) transform.localPosition = c;
    }

    // ---------- Decidir qué hacer cuando está libre ----------

    void Pensar()
    {
        bool sigue = Cria && SeguirPadre();
        if (Moviendo) return;

        if (!sigue && Time.time >= proximoPaseo)
        {
            proximoPaseo = Time.time + Random.Range(2.5f, 7f);
            plan.Clear();
            bool rampa;
            if (Corral != null && Corral.PlanPaseo(this, plan, out rampa) && plan.Count > 0)
            {
                MoverRuta(plan, Corral.VelocidadPaseo, rampa);
                return;
            }
        }

        // Quieto: gira de vez en cuando.
        if (Time.time >= proximoGiro)
        {
            giroObjetivo = transform.localRotation * Quaternion.Euler(0f, Random.Range(-120f, 120f), 0f);
            proximoGiro = Time.time + Random.Range(3f, 7f);
        }
    }

    // Punto junto al padre al que debe ir la cría (si el padre está en la rampa, la cría espera al pie).
    Vector3 MetaDelPadre(out bool conRampa)
    {
        conRampa = false;
        if (Corral.EstaArriba(Padre) && Corral.PieRampaLocal(out Vector3 pie))
        {
            conRampa = true;
            Vector3 aqui = transform.localPosition;
            Vector3 lado = new Vector3(aqui.x - pie.x, 0f, aqui.z - pie.z);
            return pie + (lado.sqrMagnitude > 1e-8f ? lado.normalized : Vector3.right) * (Tamano * 0.8f);
        }

        // Cada cría que sigue al mismo padre ocupa su propio sitio detrás de él (así no se amontonan ni dan vueltas).
        return Corral.PuestoDeSeguidor(this, Padre, out _);
    }

    // Devuelve true si la cría tiene a quién seguir (aunque ya esté a su lado).
    bool SeguirPadre()
    {
        if (Padre == null)
        {
            Padre = Corral != null ? Corral.ElegirPadre(this) : null;
            if (Padre == null) return false;
        }
        if (Time.time < proximaRevisionSeguir) return true;
        proximaRevisionSeguir = Time.time + 0.3f;

        Vector3 pPadre = Corral.transform.InverseTransformPoint(Padre.position);
        Vector3 yo = transform.localPosition;
        float dist = new Vector2(pPadre.x - yo.x, pPadre.z - yo.z).magnitude;
        bool padreEnRampa = Corral.EstaArriba(Padre);
        if (!padreEnRampa)
        {
            Corral.PuestoDeSeguidor(this, Padre, out int k);
            if (dist <= Tamano * (2.0f + 0.6f * (k / 2))) return true;
        }

        Vector3 meta = MetaDelPadre(out _);
        plan.Clear();
        if (EnRampa && Corral.PieRampaLocal(out Vector3 pie)) plan.Add(pie);
        plan.Add(meta);
        modoSeguir = true;
        MoverRuta(plan, Corral.VelocidadPaseo * 1.15f, false);
        return true;
    }

    // Mientras camina tras su padre, corrige el destino para no quedarse atrás.
    void ActualizarSeguir()
    {
        if (Time.time < proximaRevisionSeguir) return;
        proximaRevisionSeguir = Time.time + 0.3f;
        if (Padre == null || ruta.Count == 0) return;
        Vector3 meta = MetaDelPadre(out _);
        ruta[ruta.Count - 1] = Corral.Limitar(meta, Radio);   // nunca una meta dentro de una parcela o edificio
    }

    // ---------- Cada fotograma ----------

    void Update()
    {
        // La cría termina de crecer.
        if (Cria && Crecido >= (Corral != null ? Corral.SegundosCrecer : 40f))
        {
            Cria = false;
            if (Corral != null) Corral.AlCrecer(this);
            if (!enabled) return;     // Corral la reemplazó por el adulto
        }

        escActual = Mathf.MoveTowards(escActual, ObjetivoEscala(), Time.deltaTime * 0.3f);
        AplicarEscala();

        if (Comiendo)
        {
            float t = (float)(RelojReal.Ahora - inicioComer);
            if (t >= segundosComer)
            {
                Comiendo = false;
                Raciones++;
                if (modelo != null)
                {
                    modelo.localRotation = rotBase;
                    modelo.localPosition = posBase;
                }
                if (Corral != null) Corral.AlTerminarDeComer(this);
            }
            else if (modelo != null)
            {
                if (marchaPuesta) { marcha = 0f; RestaurarMarcha(); }
                // Picotea: se inclina hacia delante una y otra vez, girando sobre el centro de su base
                // y conservando la orientación base del modelo (el cuy se volvía a echar al comer).
                Quaternion inclinacion = Quaternion.Euler(Mathf.Abs(Mathf.Sin(Time.time * 7f)) * 22f, 0f, 0f);
                modelo.localRotation = inclinacion * rotBase;
                modelo.localPosition = inclinacion * posBase;
            }
            return;
        }

        Separar();   // primero se aparta de los demás; así lo que se dibuja ya cuenta con ese ajuste

        if (Moviendo)
        {
            if (modoSeguir && Cria) ActualizarSeguir();
            AvanzarMovimiento();
        }
        else if (saltosPendientes > 0) AvanzarSaltos();
        else if (!Reservado) Pensar();

        AplicarMarcha();

        float velGiro = (Moviendo || Ocupado) ? 420f : 60f;
        transform.localRotation = Quaternion.RotateTowards(transform.localRotation, giroObjetivo, velGiro * Time.deltaTime);
    }

    // Toque sobre el animal (lo llama Interactuable).
    public void Tocar()
    {
        if (Corral != null) Corral.AlimentarDirecto(this);
    }
}
