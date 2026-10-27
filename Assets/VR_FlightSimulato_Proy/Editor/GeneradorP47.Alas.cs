// GeneradorP47: alas (con alerones, flaps y pozos del tren), estabilizador, deriva y sus timones.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Charly.FlightController;
using UnityEngine;

public partial class GeneradorP47
{
    // Ala elíptica: 12.43 m de envergadura, 27.87 m² (cuerda en la raíz 2.76 m), diedro 4°, torsión +1° a -1°.
    static readonly Sustentadora Ala = new Sustentadora
    {
        semi = 6.215f, c0 = 2.76f, nPlanta = 2.2f, fRef = 0.36f, xRef = 2.55f + 0.36f * 2.76f, zRef = -0.72f, diedro = 4f,
        incRaiz = 1f, incPunta = -1f, espRaiz = 0.16f, espPunta = 0.09f, sEspRaiz = 0.6f, curvatura = 0.02f, sMax = 0.9995f
    };
    static readonly Sustentadora Estab = new Sustentadora
    {
        semi = 2.35f, c0 = 1.62f, nPlanta = 2.4f, fRef = 0.6f, xRef = 8.90f + 0.6f * 1.62f, zRef = 0.12f, espRaiz = 0.10f, espPunta = 0.08f, sMax = 0.999f
    };
    static readonly Sustentadora Deriva = new Sustentadora
    {
        semi = 1.72f, c0 = 1.75f, nPlanta = 2.4f, fRef = 0.62f, xRef = 10.38f, zRef = 0.22f, flecha = -0.03f,
        espRaiz = 0.10f, espPunta = 0.08f, vertical = true, sMax = 0.999f
    };

    // Superficies de control: tramo de envergadura y fracción de cuerda de la bisagra.
    const float FlapS0 = 0.50f, FlapS1 = 3.85f, FlapXi = 0.76f, AlerS0 = 3.90f, AlerS1 = 5.80f, AlerXi = 0.73f;
    const float ElevS0 = 0.10f, ElevS1 = 2.25f, TimonS1 = 1.60f, Luz = 0.006f;
    const int NA = 18, ND = 8, NNariz = 8;

    static Vector2 UVAla(float s, float xi, float cara, float lado) =>
        new Vector2(0.5f + lado * s / (2f * Ala.semi), cara < 0f ? 0.5f * (1f - xi) : 0.5f + 0.5f * xi);
    // Textura de cola: mitad izquierda = estabilizador, mitad derecha = deriva.
    static Vector2 UVEstab(float s, float xi, float cara, float lado) =>
        new Vector2(0.25f + lado * s / (4f * Estab.semi), cara < 0f ? 0.5f * (1f - xi) : 0.5f + 0.5f * xi);
    static Vector2 UVDeriva(float s, float xi, float cara) =>
        new Vector2(0.5f + 0.5f * s / Deriva.semi, cara < 0f ? 0.5f * (1f - xi) : 0.5f + 0.5f * xi);

    static float XLineaBisagra(float s, float s0, float s1, float xi0, float xi1)
    {
        float x0 = Ala.BordeAtaque(s0) + xi0 * Ala.Cuerda(s0), x1 = Ala.BordeAtaque(s1) + xi1 * Ala.Cuerda(s1);
        return Mathf.Lerp(x0, x1, (s - s0) / (s1 - s0));
    }
    static float XiFlap(float s) => Ala.Xi(s, XLineaBisagra(s, FlapS0, FlapS1, FlapXi, FlapXi));
    static float XiAleron(float s) => Ala.Xi(s, XLineaBisagra(s, AlerS0, AlerS1, AlerXi, AlerXi));
    static float XiCorteAla(float s)
    {
        if (s <= FlapS0) return XiFlap(FlapS0);
        if (s <= FlapS1) return XiFlap(s);
        if (s < AlerS0) return Mathf.Lerp(XiFlap(FlapS1), XiAleron(AlerS0), (s - FlapS1) / (AlerS0 - FlapS1));
        if (s <= AlerS1) return XiAleron(s);
        return XiAleron(AlerS1);
    }

    static List<float> EstacionesAla()
    {
        GeoTren g = CalcularTren(1f);
        float yPozo = g.centroRetraido.x;
        var l = Estaciones(0f, 2.40f, 0.08f, FlapS0, yPozo - RadioPozo, yPozo, yPozo + RadioPozo, 2.06f);
        l.RemoveAt(l.Count - 1);
        l.AddRange(Estaciones(2.40f, 5.90f, 0.14f, FlapS1, AlerS0, AlerS1));
        foreach (float s in new[] { 6.00f, 6.08f, 6.14f, 6.18f, 6.20f, 6.21f, Ala.semi * Ala.sMax }) l.Add(s);
        return l;
    }

    // ¿El punto (x, |y|) del intradós cae en un hueco del tren (pozo de la rueda o canal de la pierna)?
    static bool HuecoTren(float x, float y)
    {
        GeoTren g = CalcularTren(1f);
        float yPozo = g.centroRetraido.x;
        float dx = x - XAvion(g.centroRetraido), dy = Mathf.Abs(y) - yPozo;
        if (dx * dx + dy * dy < RadioPozo * RadioPozo) return true;
        return Mathf.Abs(x - XCanal(y)) < AnchoCanal && Mathf.Abs(y) > yPozo && Mathf.Abs(y) < PivoteTrenY + 0.12f;
    }

    static float XAvion(Vector3 p) => EstacionOrigen - p.z;

    void ConstruirAlas()
    {
        var ss = EstacionesAla();
        var go = Vacio(tBody, "Alas");
        var m = MallaDe(go);
        int nc = NA + ND, cols = 2 * nc + 1;
        foreach (float lado in new[] { 1f, -1f })
        {
            var Pw = new Vector3[ss.Count, cols];
            var Uw = new Vector2[ss.Count, cols];
            for (int i = 0; i < ss.Count; i++)
            {
                float s = ss[i];
                var f = FraccionesCuerda(NA, ND, XiCorteAla(s));
                for (int k = 0; k < cols; k++)
                {
                    float cara = k < nc ? -1f : 1f;
                    float xi = k <= nc ? f[nc - k] : f[k - nc];
                    Pw[i, k] = Ala.Punto(s, xi, cara, lado);
                    Uw[i, k] = UVAla(s, xi, cara, lado);
                }
            }
            m.Rejilla(Pw, Uw, i => Ala.Camber(ss[i], 0.3f, lado), incluir: (i, k) =>
            {
                float sm = (ss[i] + ss[i + 1]) * 0.5f;
                bool corte = (sm > FlapS0 && sm < FlapS1) || (sm > AlerS0 && sm < AlerS1);
                if (corte && (k < ND || k >= nc + NA)) return false;
                if (k < nc)
                {
                    Vector3 c = (Pw[i, k] + Pw[i + 1, k] + Pw[i, k + 1] + Pw[i + 1, k + 1]) * 0.25f;
                    if (HuecoTren(XAvion(c), c.x)) return false;
                }
                return true;
            });
            // Punta
            var punta = new List<Vector3>();
            for (int k = 0; k < cols; k++) punta.Add(Pw[ss.Count - 1, k]);
            m.Tapa(punta, new Vector3(lado, 0f, 0f), UVAla(Ala.semi, 0.5f, 1f, lado));
            // Cajeado de flaps y alerones (pared curva donde gira la nariz de la superficie)
            Cajeado(m, Ala, FlapS0, FlapS1, XiFlap, lado, ss, (s, xi, cara) => UVAla(s, xi, cara, lado));
            Cajeado(m, Ala, AlerS0, AlerS1, XiAleron, lado, ss, (s, xi, cara) => UVAla(s, xi, cara, lado));
        }
        Poner(go, m, mAla);

        // Superficies móviles. Alerón: Roll +1 (alabeo a la derecha) = izquierdo abajo, derecho arriba.
        var izq = SuperficieMovil(Ala, "Hinge_Aleron_Izq", "Aleron_Izq", AlerS0, AlerS1, XiAleron, -1f, ss, (s, xi, c) => UVAla(s, xi, c, -1f), mAla);
        var der = SuperficieMovil(Ala, "Hinge_Aleron_Der", "Aleron_Der", AlerS0, AlerS1, XiAleron, 1f, ss, (s, xi, c) => UVAla(s, xi, c, 1f), mAla);
        Superficie(izq.transform, ControlSurfaceAnimator.ControlInput.Roll, true, Vector3.right, 16f, 12f);
        Superficie(der.transform, ControlSurfaceAnimator.ControlInput.Roll, false, Vector3.right, 16f, 12f);
        // Flaps: valor 0..1 hacia abajo hasta 40°.
        var fi = SuperficieMovil(Ala, "Hinge_Flap_Izq", "Flap_Izq", FlapS0, FlapS1, XiFlap, -1f, ss, (s, xi, c) => UVAla(s, xi, c, -1f), mAla);
        var fd = SuperficieMovil(Ala, "Hinge_Flap_Der", "Flap_Der", FlapS0, FlapS1, XiFlap, 1f, ss, (s, xi, c) => UVAla(s, xi, c, 1f), mAla);
        Superficie(fi.transform, ControlSurfaceAnimator.ControlInput.Flaps, true, Vector3.right, 40f, 40f);
        Superficie(fd.transform, ControlSurfaceAnimator.ControlInput.Flaps, true, Vector3.right, 40f, 40f);
    }

    // Pared curva (media caña) donde termina la parte fija y gira la nariz de la superficie, más las tapas laterales.
    static void Cajeado(Malla m, Sustentadora sup, float s0, float s1, Func<float, float> xiCorte, float lado,
                        List<float> ss, Func<float, float, float, Vector2> uv)
    {
        var filas = ss.FindAll(s => s >= s0 - 1e-4f && s <= s1 + 1e-4f);
        if (filas.Count < 2) return;
        var Pc = new Vector3[filas.Count, NNariz + 1];
        var Uc = new Vector2[filas.Count, NNariz + 1];
        for (int i = 0; i < filas.Count; i++)
        {
            float s = filas[i], xc = xiCorte(s);
            MarcoCorte(sup, s, xc, lado, out Vector3 c, out Vector3 cuerda, out Vector3 esp);
            float r = sup.MedioEspesor(s, xc) + 0.004f;
            for (int k = 0; k <= NNariz; k++)
            {
                float phi = Mathf.PI * (0.5f + (float)k / NNariz);   // de arriba, por delante, hasta abajo
                Pc[i, k] = c + r * (Mathf.Cos(phi) * cuerda + Mathf.Sin(phi) * esp);
                Uc[i, k] = uv(s, xc, 1f);
            }
        }
        m.Rejilla(Pc, Uc, i => sup.Camber(filas[i], Mathf.Max(0f, xiCorte(filas[i]) - 0.4f), lado));
        // Tapas laterales del hueco (perfil detrás de la bisagra + media caña)
        Vector3 haciaS1 = (sup.Camber(s1, 0.5f, lado) - sup.Camber(s0, 0.5f, lado)).normalized;
        foreach (var (s, fuera) in new[] { (s0, haciaS1), (s1, -haciaS1) })
        {
            float xc = xiCorte(s);
            MarcoCorte(sup, s, xc, lado, out Vector3 c, out Vector3 cuerda, out Vector3 esp);
            float r = sup.MedioEspesor(s, xc) + 0.004f;
            var perfil = new List<Vector3>();
            for (int k = 0; k <= ND; k++) perfil.Add(sup.Punto(s, Mathf.Lerp(xc, 1f, (float)k / ND), 1f, lado));
            for (int k = ND - 1; k >= 0; k--) perfil.Add(sup.Punto(s, Mathf.Lerp(xc, 1f, (float)k / ND), -1f, lado));
            for (int k = NNariz; k >= 0; k--)
            {
                float phi = Mathf.PI * (0.5f + (float)k / NNariz);
                perfil.Add(c + r * (Mathf.Cos(phi) * cuerda + Mathf.Sin(phi) * esp));
            }
            m.Tapa(perfil, fuera, uv(s, xc, 1f));
        }
    }

    // Punto de la bisagra (sobre la línea de curvatura) y ejes locales de la sección: cuerda hacia atrás, espesor hacia arriba.
    static void MarcoCorte(Sustentadora sup, float s, float xc, float lado, out Vector3 c, out Vector3 cuerda, out Vector3 esp)
    {
        c = sup.Camber(s, xc, lado);
        cuerda = (sup.Camber(s, Mathf.Min(1f, xc + 0.02f), lado) - c).normalized;
        esp = (sup.Seccion(s, xc, sup.Cuerda(s) * NacaCurvatura(xc, sup.curvatura, sup.posCurvatura) + 0.01f, lado) - c).normalized;
    }

    // Superficie móvil con nariz redonda, en un pivote sobre la línea de bisagra (X local a lo largo de ella).
    GameObject SuperficieMovil(Sustentadora sup, string nombreBisagra, string nombreMalla, float s0, float s1,
                               Func<float, float> xiCorte, float lado, List<float> ss, Func<float, float, float, Vector2> uv,
                               Material mat, Transform padre = null, GameObject bisagraExistente = null)
    {
        float a = s0 + Luz, b = s1 - Luz;
        var filas = new List<float> { a };
        filas.AddRange(ss.FindAll(s => s > a + 0.01f && s < b - 0.01f));
        filas.Add(b);

        GameObject piv = bisagraExistente;
        if (piv == null)
        {
            Vector3 h0 = sup.Camber(s0, xiCorte(s0), lado), h1 = sup.Camber(s1, xiCorte(s1), lado);
            Vector3 eje = (h1 - h0).normalized;
            float sm = (s0 + s1) * 0.5f;
            MarcoCorte(sup, sm, xiCorte(sm), lado, out _, out _, out Vector3 arriba);
            if (sup.vertical) piv = Vacio(padre ?? tAnim, nombreBisagra, (h0 + h1) * 0.5f, Quaternion.LookRotation(Vector3.Cross(Vector3.right, eje).normalized, eje));
            else
            {
                if (eje.x < 0f) eje = -eje;
                piv = Vacio(padre ?? tAnim, nombreBisagra, (h0 + h1) * 0.5f, Ejes(eje, arriba));
            }
        }
        var go = Vacio(piv.transform, nombreMalla, piv.transform.position, piv.transform.rotation);
        var m = MallaDe(go);
        int cols = (ND + 1) + (NNariz - 1) + (ND + 1);
        var Pm = new Vector3[filas.Count, cols];
        var Um = new Vector2[filas.Count, cols];
        for (int i = 0; i < filas.Count; i++)
        {
            float s = filas[i], xc = xiCorte(s);
            MarcoCorte(sup, s, xc, lado, out Vector3 c, out Vector3 cuerda, out Vector3 esp);
            float r = sup.MedioEspesor(s, xc);
            int k = 0;
            for (int q = 0; q <= ND; q++, k++)
            {
                float xi = Mathf.Lerp(1f, xc, (float)q / ND);
                Pm[i, k] = sup.Punto(s, xi, -1f, lado); Um[i, k] = uv(s, xi, -1f);
            }
            for (int q = 1; q < NNariz; q++, k++)
            {
                float phi = Mathf.PI * (1.5f - (float)q / NNariz);   // de abajo, por delante, hasta arriba
                Pm[i, k] = c + r * (Mathf.Cos(phi) * cuerda + Mathf.Sin(phi) * esp); Um[i, k] = uv(s, xc, 1f);
            }
            for (int q = 0; q <= ND; q++, k++)
            {
                float xi = Mathf.Lerp(xc, 1f, (float)q / ND);
                Pm[i, k] = sup.Punto(s, xi, 1f, lado); Um[i, k] = uv(s, xi, 1f);
            }
        }
        m.Rejilla(Pm, Um, i => sup.Camber(filas[i], (xiCorte(filas[i]) + 1f) * 0.5f, lado));
        Vector3 haciaB = (sup.Camber(b, 0.8f, lado) - sup.Camber(a, 0.8f, lado)).normalized;
        var t0 = new List<Vector3>(); var t1 = new List<Vector3>();
        for (int k = 0; k < cols; k++) { t0.Add(Pm[0, k]); t1.Add(Pm[filas.Count - 1, k]); }
        m.Tapa(t0, -haciaB, uv(a, 0.9f, 1f));
        m.Tapa(t1, haciaB, uv(b, 0.9f, 1f));
        Poner(go, m, mat);
        return piv;
    }

    // ---------- Cola ----------
    void ConstruirCola()
    {
        var go = Vacio(tBody, "Estabilizadores");
        var m = MallaDe(go);
        // Estabilizador horizontal: bisagra recta en el 60 % de la cuerda.
        var se = Estaciones(0f, Estab.semi * Estab.sMax, 0.08f, ElevS0, ElevS1, 2.30f);
        int nc = NA + ND, cols = 2 * nc + 1;
        foreach (float lado in new[] { 1f, -1f })
        {
            var Ps = new Vector3[se.Count, cols]; var Us = new Vector2[se.Count, cols];
            var f = FraccionesCuerda(NA, ND, Estab.fRef);
            for (int i = 0; i < se.Count; i++)
                for (int k = 0; k < cols; k++)
                {
                    float cara = k < nc ? -1f : 1f, xi = k <= nc ? f[nc - k] : f[k - nc];
                    Ps[i, k] = Estab.Punto(se[i], xi, cara, lado); Us[i, k] = UVEstab(se[i], xi, cara, lado);
                }
            m.Rejilla(Ps, Us, i => Estab.Camber(se[i], 0.3f, lado), incluir: (i, k) =>
            {
                float sm = (se[i] + se[i + 1]) * 0.5f;
                return !(sm > ElevS0 && sm < ElevS1 && (k < ND || k >= nc + NA));
            });
            var punta = new List<Vector3>();
            for (int k = 0; k < cols; k++) punta.Add(Ps[se.Count - 1, k]);
            m.Tapa(punta, new Vector3(lado, 0f, 0f), UVEstab(Estab.semi, 0.5f, 1f, lado));
            Cajeado(m, Estab, ElevS0, ElevS1, s => Estab.fRef, lado, se, (s, xi, c) => UVEstab(s, xi, c, lado));
        }
        // Deriva: la cara +1 es la derecha. Timón de dirección detrás de la bisagra (62 %) hasta 1.6 m de altura.
        var sd = Estaciones(0f, Deriva.semi * Deriva.sMax, 0.08f, TimonS1);
        {
            var Pd = new Vector3[sd.Count, cols]; var Ud = new Vector2[sd.Count, cols];
            var f = FraccionesCuerda(NA, ND, Deriva.fRef);
            for (int i = 0; i < sd.Count; i++)
                for (int k = 0; k < cols; k++)
                {
                    float cara = k < nc ? -1f : 1f, xi = k <= nc ? f[nc - k] : f[k - nc];
                    Pd[i, k] = Deriva.Punto(sd[i], xi, cara); Ud[i, k] = UVDeriva(sd[i], xi, cara);
                }
            m.Rejilla(Pd, Ud, i => Deriva.Camber(sd[i], 0.3f), incluir: (i, k) =>
            {
                float sm = (sd[i] + sd[i + 1]) * 0.5f;
                return !(sm < TimonS1 && (k < ND || k >= nc + NA));
            });
            var punta = new List<Vector3>();
            for (int k = 0; k < cols; k++) punta.Add(Pd[sd.Count - 1, k]);
            m.Tapa(punta, Vector3.up, UVDeriva(Deriva.semi, 0.5f, 1f));
            Cajeado(m, Deriva, 0f, TimonS1, s => Deriva.fRef, 1f, sd, (s, xi, c) => UVDeriva(s, xi, c));
        }
        Poner(go, m, mCola);

        // Aleta dorsal (P-47D-27 en adelante): cuña delgada sobre el lomo hasta el borde de ataque de la deriva.
        var dorsal = Vacio(tBody, "Aleta_Dorsal");
        var md = MallaDe(dorsal);
        const int nDor = 12;
        var Pdr = new Vector3[nDor + 1, 3]; var Udr = new Vector2[nDor + 1, 3];
        float xFin = Deriva.BordeAtaque(0.68f);
        for (int i = 0; i <= nDor; i++)
        {
            float u = (float)i / nDor;
            float x = Mathf.Lerp(7.6f, xFin + 0.05f, u), zTope = Mathf.Lerp(FusSup[7.6f] - 0.01f, 0.90f, Mathf.Pow(u, 1.3f));
            float zBase = ZSupFusEnY(x, 0.035f) - 0.01f;
            Pdr[i, 0] = P(x, -0.035f, zBase); Pdr[i, 1] = P(x - 0.02f, 0f, Mathf.Max(zTope, zBase + 0.005f)); Pdr[i, 2] = P(x, 0.035f, zBase);
            for (int k = 0; k < 3; k++) Udr[i, k] = UVFus(x, 0.5f);
        }
        md.Rejilla(Pdr, Udr, i => P(Mathf.Lerp(7.6f, xFin, (float)i / nDor), 0f, 0f));
        Poner(dorsal, md, mFus);

        // Timón de profundidad (Hinge_Timon): las dos mitades giran sobre la misma bisagra recta.
        // Pitch +1 (palanca adelante, morro abajo) = borde de salida abajo.
        var hTimon = Vacio(tAnim, "Hinge_Timon", P(Estab.xRef, 0f, Estab.zRef));
        SuperficieMovil(Estab, null, "TimonIzq", ElevS0, ElevS1, s => Estab.fRef, -1f, se, (s, xi, c) => UVEstab(s, xi, c, -1f), mCola, null, hTimon);
        SuperficieMovil(Estab, null, "TimonDer", ElevS0, ElevS1, s => Estab.fRef, 1f, se, (s, xi, c) => UVEstab(s, xi, c, 1f), mCola, null, hTimon);
        Superficie(hTimon.transform, ControlSurfaceAnimator.ControlInput.Pitch, true, Vector3.right, 25f, 15f);

        // Timón de dirección (Hinge_Cola): Y local a lo largo de la bisagra. Yaw +1 = borde de salida a la derecha.
        var hCola = SuperficieMovil(Deriva, "Hinge_Cola", "Timon_Cola", 0f, TimonS1, s => Deriva.fRef, 1f, sd, (s, xi, c) => UVDeriva(s, xi, c), mCola);
        Superficie(hCola.transform, ControlSurfaceAnimator.ControlInput.Yaw, true, Vector3.up, 25f, 25f);
        // Luz de navegación blanca en el borde de salida del timón
        var luz = Vacio(hCola.transform, "Luz_Cola", Deriva.Punto(0.25f, 0.985f, 0f), hCola.transform.rotation);
        var ml = MallaDe(luz);
        ml.Elipsoide(Deriva.Punto(0.25f, 0.985f, 0f), new Vector3(0.025f, 0.04f, 0.03f), Quaternion.identity, 6, 10);
        Poner(luz, ml, mLuzBlanca, null, false);
    }
}
#endif
