// GeneradorP47: tren de aterrizaje retráctil, detalles exteriores, colliders y puntos de anclaje.
#if UNITY_EDITOR
using System.Collections.Generic;
using Charly.FlightController;
using UnityEditor;
using UnityEngine;

public partial class GeneradorP47
{
    // Tren principal (lado derecho; el izquierdo es espejo). La pierna se acorta 23 cm al subir (telescópica)
    // y se guarda hacia adentro con la rueda acostada en un pozo del ala.
    public const float EjeTrenX = 3.10f;
    const float PivoteTrenY = 1.93f, Abertura = 4f, RadioRueda = 0.432f, AnchoRueda = 0.229f, Aplastado = 0.025f,
                Telescopio = 0.23f, DesvioRueda = 0.18f, RadioPozo = 0.47f, AnchoCanal = 0.12f, RadioPuertaPozo = 0.55f;
    // El hueco se corta por triángulos (borde dentado): forro y compuertas un poco más grandes para taparlo.
    const float RadioForro = 0.53f, MargenCanal = 0.05f;

    struct GeoTren
    {
        public Vector3 pivote, dirExt, ejeRueda, fondo, centroRueda, centroRetraido, ejeRetraido;
        public float largo;
        public Quaternion retraccion;
    }

    static GeoTren CalcularTren(float lado)
    {
        var g = new GeoTren();
        float s = PivoteTrenY, a = Abertura * Mathf.Deg2Rad;
        float zp = Ala.Punto(s, Ala.Xi(s, EjeTrenX), -1f).y + 0.06f;
        float zEje = -AlturaEmpuje + RadioRueda - Aplastado;
        g.largo = (zp - zEje - DesvioRueda * Mathf.Sin(a)) / Mathf.Cos(a);
        g.pivote = P(EjeTrenX, lado * s, zp);
        g.dirExt = new Vector3(lado * Mathf.Sin(a), -Mathf.Cos(a), 0f);
        g.ejeRueda = new Vector3(-lado * Mathf.Cos(a), -Mathf.Sin(a), 0f);   // hacia adentro
        g.fondo = g.pivote + g.dirExt * g.largo;
        g.centroRueda = g.fondo + g.ejeRueda * DesvioRueda;
        float d = Ala.diedro * Mathf.Deg2Rad;
        g.retraccion = Quaternion.FromToRotation(g.dirExt, new Vector3(-lado * Mathf.Cos(d), -Mathf.Sin(d), 0f));
        g.centroRetraido = g.pivote + g.retraccion * (g.dirExt * (g.largo - Telescopio) + g.ejeRueda * DesvioRueda);
        g.ejeRetraido = g.retraccion * g.ejeRueda;
        return g;
    }

    // Altura del intradós del ala en (x, y) del avión, como punto de Unity.
    static Vector3 Intrados(float x, float y, float desplazar = 0f)
    {
        float s = Mathf.Abs(y), lado = Mathf.Sign(y);
        return Ala.Punto(s, Ala.Xi(s, x), -1f, lado) + Vector3.up * desplazar;
    }

    static LandingGearAnimator.Pieza Pieza(Transform t, LandingGearAnimator.TipoPieza tipo, Vector2 tramo,
                                           Vector3 posArriba, Quaternion rotArriba, Vector3 posAbajo, Quaternion rotAbajo) =>
        new LandingGearAnimator.Pieza
        {
            pieza = t, tipo = tipo, tramo = tramo, posicionArriba = posArriba, rotacionArriba = rotArriba,
            posicionAbajo = posAbajo, rotacionAbajo = rotAbajo
        };

    void ConstruirTren()
    {
        var fisica = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(RutaFisicaTren);
        var pozos = Vacio(tBody, "Pozos_Tren");
        var mPozos = MallaDe(pozos);
        foreach (float lado in new[] { -1f, 1f })
        {
            string L = lado < 0f ? "Izq" : "Der";
            var g = CalcularTren(lado);
            Vector3 adelante = Vector3.forward;
            Quaternion rExt = Quaternion.LookRotation(Vector3.ProjectOnPlane(adelante, g.dirExt).normalized, -g.dirExt);

            // Pierna (amortiguador exterior y muñón)
            var pierna = Vacio(tGear, "TrenAterrizaje" + L, g.pivote, rExt);
            var mp = MallaDe(pierna);
            mp.Tubo(g.pivote - g.dirExt * 0.05f, g.pivote + g.dirExt * 0.78f, 0.075f, 0.07f, 14);
            mp.Tubo(g.pivote + adelante * 0.13f, g.pivote - adelante * 0.13f, 0.05f, 0.05f, 10);
            Poner(pierna, mp, mMetal, "Pierna");
            piezasTren.Add(Pieza(pierna.transform, LandingGearAnimator.TipoPieza.Recorrido, new Vector2(0.15f, 0.85f),
                g.pivote, g.retraccion * rExt, g.pivote, rExt));

            // Pistón telescópico con eje y tijera
            var piston = Vacio(pierna.transform, "Piston", g.pivote, rExt);
            var mpi = MallaDe(piston);
            mpi.Tubo(g.pivote + g.dirExt * 0.70f, g.fondo + g.dirExt * 0.03f, 0.052f, 0.052f, 12);
            mpi.Tubo(g.fondo - g.ejeRueda * 0.03f, g.fondo + g.ejeRueda * (DesvioRueda - AnchoRueda * 0.5f + 0.02f), 0.042f, 0.042f, 10);
            Poner(piston, mpi, mCromo, "Piston_Malla");
            var mt = MallaDe(piston);
            Vector3 arriba = g.pivote + g.dirExt * 0.74f + adelante * 0.07f, abajo = g.fondo - g.dirExt * 0.06f + adelante * 0.07f;
            Vector3 codo = (arriba + abajo) * 0.5f + adelante * 0.11f;
            Barra(mt, arriba, codo, 0.035f); Barra(mt, codo, abajo, 0.035f);
            Poner(piston, mt, mMetal, "Tijera");
            piezasTren.Add(Pieza(piston.transform, LandingGearAnimator.TipoPieza.Recorrido, new Vector2(0.45f, 0.85f),
                new Vector3(0f, Telescopio, 0f), Quaternion.identity, Vector3.zero, Quaternion.identity));

            // Rueda: X local hacia la derecha del avión (girar en + = rodar hacia adelante)
            Vector3 ejeX = g.ejeRueda.x > 0f ? g.ejeRueda : -g.ejeRueda;
            var rueda = Vacio(piston.transform, "Llanta_" + L, g.centroRueda, Ejes(ejeX, Vector3.up));
            var ml = MallaDe(rueda);
            Neumatico(ml, g.centroRueda, rueda.transform.rotation, RadioRueda, AnchoRueda, 0.27f);
            Poner(rueda, ml, mLlanta);
            var mr = MallaDe(rueda);
            Rin(mr, g.centroRueda, rueda.transform.rotation, 0.27f, AnchoRueda);
            Poner(rueda, mr, mRin, "Rin");
            var esfera = rueda.AddComponent<SphereCollider>();
            esfera.radius = RadioRueda;
            esfera.sharedMaterial = fisica;
            collidersTren.Add(esfera);
            ruedasTren.Add(new LandingGearAnimator.Rueda { rueda = rueda.transform, radio = RadioRueda, eje = Vector3.right });

            // Compuerta de la pierna: parche del intradós sobre el canal, armado con la pierna arriba.
            float yPozo = Mathf.Abs(g.centroRetraido.x);
            var compuerta = Vacio(pierna.transform, "Compuerta_Pierna", g.pivote, rExt);
            var matArriba = Matrix4x4.TRS(g.pivote, g.retraccion * rExt, Vector3.one).inverse;
            Malla ext = new Malla { Matriz = matArriba }, inte = new Malla { Matriz = matArriba };
            ParcheIntrados(ext, inte, EjeTrenX - AnchoCanal - MargenCanal - 0.01f, EjeTrenX + AnchoCanal + MargenCanal + 0.01f,
                           yPozo + RadioPozo - 0.03f, PivoteTrenY + 0.18f, lado);
            Poner(compuerta, ext, mAla);
            Poner(compuerta, inte, mInterior, "Interior", false);

            // Compuerta del pozo: bisagra a lo largo del borde interior; se abre hacia abajo.
            float yBis = yPozo - RadioPuertaPozo;
            Vector3 bis = Intrados(EjeTrenX, lado * yBis);
            var puertaPozo = Vacio(tGear, "Compuerta_Rueda_" + L, bis);
            Malla pe = MallaDe(puertaPozo), pi = MallaDe(puertaPozo);
            DiscoIntrados(pe, pi, EjeTrenX, lado * yPozo, RadioPuertaPozo, lado);
            Poner(puertaPozo, pe, mAla);
            Poner(puertaPozo, pi, mInterior, "Interior", false);
            Vector3 centroPuerta = Intrados(EjeTrenX, lado * yPozo) - bis;
            float signo = (Quaternion.AngleAxis(10f, adelante) * centroPuerta).y < centroPuerta.y ? 1f : -1f;
            piezasTren.Add(Pieza(puertaPozo.transform, LandingGearAnimator.TipoPieza.Compuerta, new Vector2(0f, 0.18f),
                bis, Quaternion.identity, bis, Quaternion.AngleAxis(signo * 95f, adelante)));

            // Pozo de la rueda y canal de la pierna (forro interior)
            Pozo(mPozos, g);
            Canal(mPozos, yPozo + RadioPozo - 0.06f, PivoteTrenY + 0.17f, lado);
        }
        Poner(pozos, mPozos, mInterior);

        ConstruirRuedaCola(fisica);
    }

    // Parche rectangular del intradós (x0..x1, |y| entre y0 e y1): cara exterior (pintada) e interior.
    static void ParcheIntrados(Malla ext, Malla inte, float x0, float x1, float y0, float y1, float lado)
    {
        const int nx = 4, ny = 8;
        var Pe = new Vector3[ny + 1, nx + 1]; var Ue = new Vector2[ny + 1, nx + 1]; var Pi = new Vector3[ny + 1, nx + 1];
        for (int i = 0; i <= ny; i++)
            for (int k = 0; k <= nx; k++)
            {
                float y = Mathf.Lerp(y0, y1, (float)i / ny), x = Mathf.Lerp(x0, x1, (float)k / nx);
                Pe[i, k] = Intrados(x, lado * y, -0.004f);
                Pi[i, k] = Intrados(x, lado * y, 0.004f);
                Ue[i, k] = UVAla(y, Ala.Xi(y, x), -1f, lado);
            }
        ext.Rejilla(Pe, Ue, i => Pe[i, nx / 2] + Vector3.up);
        inte.Rejilla(Pi, null, i => Pi[i, nx / 2] - Vector3.up);
    }

    // Parche circular del intradós (compuerta del pozo).
    static void DiscoIntrados(Malla ext, Malla inte, float xc, float yc, float radio, float lado)
    {
        const int nr = 4, na = 32;
        var Pe = new Vector3[nr + 1, na + 1]; var Ue = new Vector2[nr + 1, na + 1]; var Pi = new Vector3[nr + 1, na + 1];
        for (int i = 0; i <= nr; i++)
            for (int k = 0; k <= na; k++)
            {
                float r = radio * i / nr, a = Mathf.PI * 2f * k / na;
                float x = xc + r * Mathf.Cos(a), y = yc + r * Mathf.Sin(a);
                Pe[i, k] = Intrados(x, y, -0.004f);
                Pi[i, k] = Intrados(x, y, 0.004f);
                Ue[i, k] = UVAla(Mathf.Abs(y), Ala.Xi(Mathf.Abs(y), x), -1f, lado);
            }
        Vector3 c = Intrados(xc, yc);
        ext.Rejilla(Pe, Ue, i => c + Vector3.up, costura: true);
        inte.Rejilla(Pi, null, i => c - Vector3.up, costura: true);
    }

    // Cilindro del pozo alineado con la rueda guardada: el borde de abajo sigue el intradós.
    static void Pozo(Malla m, GeoTren g)
    {
        Vector3 n = g.ejeRetraido.normalized;
        if (n.y < 0f) n = -n;
        Vector3 tope = g.centroRetraido + n * (AnchoRueda * 0.5f + 0.025f);
        Vector3 a = Vector3.ProjectOnPlane(Vector3.right, n).normalized, b = Vector3.Cross(n, a);
        const int na = 32;
        var Pp = new Vector3[2, na + 1];
        var anillo = new List<Vector3>();
        for (int k = 0; k <= na; k++)
        {
            float ang = Mathf.PI * 2f * k / na;
            Vector3 arriba = tope + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * RadioForro;
            Vector3 suelo = Intrados(XAvion(arriba), arriba.x, -0.01f);
            Pp[0, k] = new Vector3(arriba.x, suelo.y, arriba.z);
            Pp[1, k] = arriba;
            anillo.Add(arriba);
        }
        m.Rejilla(Pp, null, costura: true);   // filas hacia arriba, columnas alrededor de n: mira hacia el eje
        m.Tapa(anillo, -n, Vector2.zero);
    }

    // Canal donde se guarda la pierna: paredes delantera y trasera, techo y fondo.
    static void Canal(Malla m, float y0, float y1, float lado)
    {
        const int ny = 6;
        const float alto = 0.17f;
        float xa = EjeTrenX - AnchoCanal - MargenCanal, xb = EjeTrenX + AnchoCanal + MargenCanal;
        foreach (float xw in new[] { xa, xb })
        {
            var Pw = new Vector3[ny + 1, 2];
            for (int i = 0; i <= ny; i++)
            {
                float y = Mathf.Lerp(y0, y1, (float)i / ny);
                Pw[i, 0] = Intrados(xw, lado * y, -0.01f);
                Pw[i, 1] = Intrados(xw, lado * y, alto);
            }
            float xFuera = xw < EjeTrenX ? xw - 1f : xw + 1f;
            m.Rejilla(Pw, null, i => P(xFuera, lado * Mathf.Lerp(y0, y1, (float)i / ny), Pw[i, 0].y));
        }
        var Pt = new Vector3[ny + 1, 2];
        for (int i = 0; i <= ny; i++)
        {
            float y = Mathf.Lerp(y0, y1, (float)i / ny);
            Pt[i, 0] = Intrados(xa, lado * y, alto); Pt[i, 1] = Intrados(xb, lado * y, alto);
        }
        m.Rejilla(Pt, null, i => Pt[i, 0] + Vector3.up);
        Vector3 f0 = Intrados(xa, lado * y1, -0.01f), f1 = Intrados(xb, lado * y1, -0.01f);
        m.Quad(f0, f1, f1 + Vector3.up * (alto + 0.01f), f0 + Vector3.up * (alto + 0.01f), new Vector3(-lado, 0f, 0f));
    }

    void ConstruirRuedaCola(PhysicsMaterial fisica)
    {
        const float xPiv = 9.30f, xEje = 9.42f, rC = 0.205f, wC = 0.12f;
        float th = AnguloTresPuntos * Mathf.Deg2Rad;
        float xCont = xEje + rC * Mathf.Sin(th);
        float zEje = -AlturaEmpuje + (xCont - EjeTrenX) * Mathf.Tan(th) + rC * Mathf.Cos(th);
        Vector3 piv = P(xPiv, 0f, FusInf[xPiv] + 0.06f), eje = P(xEje, 0f, zEje);
        Vector3 dS = (eje - piv).normalized;
        float largo = (eje - piv).magnitude;
        Quaternion rS = Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, dS).normalized, -dS);

        var cola = Vacio(tGear, "TrenAterrizajeTrasero", piv, rS);
        var horquilla = Vacio(cola.transform, "Horquilla", piv, rS);
        var mh = MallaDe(horquilla);
        Vector3 corona = piv + dS * (largo - 0.24f), lat = Vector3.right * (wC * 0.5f + 0.025f);
        mh.Tubo(piv - dS * 0.03f, corona, 0.035f, 0.035f, 10);
        mh.Caja(corona, new Vector3(wC + 0.08f, 0.04f, 0.10f), rS);
        Barra(mh, corona + lat, eje + lat, 0.03f);
        Barra(mh, corona - lat, eje - lat, 0.03f);
        Poner(horquilla, mh, mMetal, "Horquilla_Malla");
        Superficie(horquilla.transform, ControlSurfaceAnimator.ControlInput.Yaw, true, Vector3.up, 30f, 30f);

        var llanta = Vacio(horquilla.transform, "LlantaCola", eje);
        var ml = MallaDe(llanta);
        Neumatico(ml, eje, Quaternion.identity, rC, wC, 0.11f);
        Poner(llanta, ml, mLlanta);
        var mr = MallaDe(llanta);
        Rin(mr, eje, Quaternion.identity, 0.11f, wC);
        Poner(llanta, mr, mRin, "Rin");
        var esfera = llanta.AddComponent<SphereCollider>();
        esfera.radius = rC;
        esfera.sharedMaterial = fisica;
        collidersTren.Add(esfera);
        ruedasTren.Add(new LandingGearAnimator.Rueda { rueda = llanta.transform, radio = rC, eje = Vector3.right });

        // Se guarda hacia atrás y arriba dentro del fuselaje
        var ret = Quaternion.FromToRotation(dS, new Vector3(0f, Mathf.Sin(40f * Mathf.Deg2Rad), -Mathf.Cos(40f * Mathf.Deg2Rad)));
        piezasTren.Add(Pieza(cola.transform, LandingGearAnimator.TipoPieza.Recorrido, new Vector2(0.15f, 0.85f), piv, ret * rS, piv, rS));

        foreach (var (nombreP, t0, t1, tb) in new[] { ("Compuerta_Cola_Izq", 250f, 270f, 250f), ("Compuerta_Cola_Der", 270f, 290f, 290f) })
        {
            Vector3 b0 = PuntoFus(ColaX0, tb * Mathf.Deg2Rad), b1 = PuntoFus(ColaX1, tb * Mathf.Deg2Rad);
            Vector3 pos = (b0 + b1) * 0.5f;
            var puerta = Vacio(tGear, nombreP, pos);
            Malla ext = MallaDe(puerta), inte = MallaDe(puerta);
            ParchePiel(ext, inte, ColaX0, ColaX1, t0, t1, 0.985f);
            Poner(puerta, ext, mFus);
            Poner(puerta, inte, mInterior, "Interior", false);
            Vector3 eje2 = (b1 - b0).normalized;
            Vector3 centro = PuntoFus((ColaX0 + ColaX1) * 0.5f, (t0 + t1) * 0.5f * Mathf.Deg2Rad) - pos;
            float signo = (Quaternion.AngleAxis(10f, eje2) * centro).y < centro.y ? 1f : -1f;
            piezasTren.Add(Pieza(puerta.transform, LandingGearAnimator.TipoPieza.Compuerta, new Vector2(0f, 0.18f),
                pos, Quaternion.identity, pos, Quaternion.AngleAxis(signo * 80f, eje2)));
        }
    }

    // Llanta: perfil redondeado girado alrededor del eje X de 'rot'.
    static void Neumatico(Malla m, Vector3 c, Quaternion rot, float R, float W, float rRin)
    {
        const int nt = 16, np = 32;
        float rc = (R + rRin) * 0.5f, hr = (R - rRin) * 0.5f, hw = W * 0.5f;
        var Pn = new Vector3[nt + 1, np + 1];
        var ws = new float[nt + 1];
        for (int i = 0; i <= nt; i++)
        {
            float th = Mathf.PI * 2f * i / nt, cw = Mathf.Cos(th), sw = Mathf.Sin(th);
            float w = hw * Mathf.Sign(cw) * Mathf.Pow(Mathf.Abs(cw), 0.6f), r = rc + hr * Mathf.Sign(sw) * Mathf.Pow(Mathf.Abs(sw), 0.6f);
            ws[i] = w;
            for (int j = 0; j <= np; j++)
            {
                float ps = Mathf.PI * 2f * j / np;
                Pn[i, j] = c + rot * new Vector3(w, r * Mathf.Cos(ps), r * Mathf.Sin(ps));
            }
        }
        m.Rejilla(Pn, null, i => c + rot * new Vector3(ws[i], 0f, 0f), costura: true);
    }

    static void Rin(Malla m, Vector3 c, Quaternion rot, float rRin, float W)
    {
        Vector3 x = rot * Vector3.right;
        m.Tubo(c - x * (W * 0.42f), c + x * (W * 0.42f), rRin, rRin, 24, false);
        m.Disco(c + x * (W * 0.40f), x, rRin, 24);
        m.Disco(c - x * (W * 0.40f), -x, rRin, 24);
        m.Tubo(c - x * (W * 0.52f), c + x * (W * 0.52f), rRin * 0.3f, rRin * 0.3f, 12, true);
    }

    static void Barra(Malla m, Vector3 a, Vector3 b, float grosor)
    {
        Vector3 d = b - a;
        if (d.sqrMagnitude < 1e-8f) return;
        m.Caja((a + b) * 0.5f, new Vector3(grosor, grosor, d.magnitude),
               Quaternion.LookRotation(d, Mathf.Abs(d.normalized.y) > 0.95f ? Vector3.forward : Vector3.up));
    }

    // ---------- Detalles: ametralladoras, pitot, antena, escapes, luces ----------
    readonly List<(string nombre, Vector3 punta)> bocas = new List<(string, Vector3)>();

    void ConstruirDetalles()
    {
        bocas.Clear();
        var go = Vacio(tBody, "Detalles_Metal");
        var m = MallaDe(go);
        foreach (float lado in new[] { -1f, 1f })
            for (int i = 0; i < 4; i++)
            {
                // 8 ametralladoras M2 de 12.7 mm, 4 por ala, escalonadas (la de adentro sobresale más)
                float s = 2.55f + 0.21f * i, c = Ala.Cuerda(s);
                Vector3 baseCanon = Ala.Camber(s, 0.12f, lado);
                float alBorde = 0.12f * c, sobresale = 0.50f - 0.08f * i;
                Vector3 punta = baseCanon + Vector3.forward * (alBorde + sobresale);
                m.Tubo(baseCanon, punta, 0.021f, 0.019f, 10, true);
                m.Tubo(baseCanon + Vector3.forward * (alBorde - 0.04f), baseCanon + Vector3.forward * (alBorde + 0.07f), 0.032f, 0.03f, 10, true);
                bocas.Add(((lado < 0f ? "Left_Muzzle_" : "Right_Muzzle_") + (i + 1), punta));
            }
        // Tubo pitot bajo el ala izquierda
        Vector3 pitotRaiz = Ala.Punto(5.30f, 0.25f, -1f, -1f), pitotBase = pitotRaiz + Vector3.down * 0.07f;
        m.Tubo(pitotRaiz + Vector3.up * 0.02f, pitotBase, 0.009f, 0.009f, 6, false);
        m.Tubo(pitotBase - Vector3.forward * 0.1f, pitotBase + Vector3.forward * 0.5f, 0.012f, 0.01f, 8, true);
        // Antena de látigo detrás de la cabina
        Vector3 antena = P(6.20f, 0f, FusSup[6.20f] - 0.01f);
        m.Tubo(antena, antena + Vector3.up * 0.05f, 0.02f, 0.016f, 8, true);
        m.Tubo(antena + Vector3.up * 0.05f, antena + new Vector3(0f, 0.92f, -0.16f), 0.006f, 0.003f, 6, true);
        Poner(go, m, mMetalOscuro);

        // Escapes: compuertas de desahogo bajo la cofia y salida del turbo bajo el fuselaje
        var escapes = Vacio(tBody, "Escapes");
        var me = MallaDe(escapes);
        foreach (float lado in new[] { -1f, 1f })
            me.Tubo(P(2.48f, lado * 0.30f, FusInf[2.48f] + 0.07f), P(2.82f, lado * 0.30f, FusInf[2.82f] + 0.03f), 0.055f, 0.05f, 10, true);
        me.Caja(P(6.25f, 0f, FusInf[6.25f] - 0.025f), new Vector3(0.32f, 0.08f, 0.55f), Quaternion.identity);
        Poner(escapes, me, mEscape);

        // Luces de navegación en las puntas
        foreach (float lado in new[] { -1f, 1f })
        {
            var luz = Vacio(tBody, lado < 0f ? "Luz_Izq" : "Luz_Der");
            var mlz = MallaDe(luz);
            mlz.Elipsoide(Ala.Camber(6.17f, 0.3f, lado) + new Vector3(lado * 0.02f, 0f, 0f), new Vector3(0.05f, 0.03f, 0.07f), Quaternion.identity, 6, 10);
            Poner(luz, mlz, lado < 0f ? mLuzRoja : mLuzVerde, null, false);
        }
    }

    // ---------- Colliders (cajas simples, como el Thunderbolt V3) ----------
    void ConstruirColliders()
    {
        Caja("motor", P(0.85f, 0f, -0.12f), new Vector3(1.42f, 1.62f, 1.75f), Quaternion.identity);
        Caja("body", P(4.10f, 0f, -0.12f), new Vector3(1.25f, 1.65f, 4.80f), Quaternion.identity);
        Caja("tail", P(8.45f, 0f, 0.0f), new Vector3(0.75f, 0.95f, 3.90f), Quaternion.identity);
        foreach (float lado in new[] { -1f, 1f })
        {
            string L = lado < 0f ? "left" : "right";
            var giro = Quaternion.Euler(0f, 0f, lado * Ala.diedro);
            Caja(L + "_wing_a", Ala.Camber(1.85f, 0.4f, lado), new Vector3(2.7f, 0.32f, 2.4f), giro);
            Caja(L + "_wing_b", Ala.Camber(4.60f, 0.4f, lado), new Vector3(2.8f, 0.22f, 1.8f), giro);
        }
        Caja("horizontal_stabilizer", P(9.75f, 0f, 0.12f), new Vector3(4.6f, 0.16f, 1.3f), Quaternion.identity);
        Caja("vertical_stabilizer", P(10.20f, 0f, 1.05f), new Vector3(0.16f, 1.7f, 1.4f), Quaternion.identity);
    }

    void Caja(string nombreCaja, Vector3 centro, Vector3 tam, Quaternion rot)
    {
        var go = Vacio(tColliders, nombreCaja, centro, rot);
        go.AddComponent<BoxCollider>().size = tam;
    }

    // ---------- Puntos de anclaje para configurarlo como el Thunderbolt V3 ----------
    void ConstruirPuntos()
    {
        Vacio(raiz.transform, "Seat", P(4.85f, 0f, 0.06f));
        Vacio(raiz.transform, "PuntoVistaPiloto", P(4.55f, 0f, 0.98f));
        Vacio(raiz.transform, "Cabin", P(3.86f, 0f, 0.31f));
        var guns = Vacio(raiz.transform, "Guns", P(3.0f, 0f, -0.62f));
        foreach (var (nombreBoca, punta) in bocas) Vacio(guns.transform, nombreBoca, punta);
        var audios = Vacio(raiz.transform, "Audios");
        Vacio(audios.transform, "Audio Motor", P(1.30f, 0f, 0f));
        Vacio(audios.transform, "AudioCabinDoor", P(4.30f, 0f, 0.80f));
    }
}
#endif
