// GeneradorP47: fuselaje, cofia (aletas de enfriamiento, compuertas del intercooler), motor visible, hélice y cabina.
#if UNITY_EDITOR
using System.Collections.Generic;
using Charly.FlightController;
using UnityEngine;

public partial class GeneradorP47
{
    // Huecos del fuselaje (estaciones en m y ángulos en grados: 0 = derecha, 90 = arriba).
    const float CowlX0 = 2.18f, CowlX1 = 2.42f, CowlT0 = -35f, CowlT1 = 215f;
    const float IntX0 = 2.62f, IntX1 = 3.02f;
    const float CabX0 = 3.62f, CabX1 = 5.20f, CabFrente = 3.95f, CabAtras = 5.62f, AnguloBorde = 55f;
    const float ColaX0 = 9.15f, ColaX1 = 9.90f;
    const float PlanoHelice = 0.42f, RadioHelice = 1.98f, RecorridoCabina = 0.62f;
    const int NJ = 72;   // divisiones del contorno del fuselaje (5°)

    static float TGrados(int j) => -90f + 360f * j / NJ;
    static readonly Vector2 UVPintura = new Vector2(0.4f, 0.5f);   // zona del lomo en la textura del fuselaje (color base)
    static Vector2 UVFus(float x, float v) => new Vector2(0.01f + 0.99f * (x - FusX0) / (FusX1 - FusX0), v);
    static Vector3 Atras => new Vector3(0f, 0f, -1f);

    static bool HuecoFus(float x, float t)
    {
        if (x > CabX0 && x < CabX1 && EnRangoAngulo(t, AnguloBorde, 180f - AnguloBorde)) return true;
        if (x > CowlX0 && x < CowlX1 && EnRangoAngulo(t, CowlT0, CowlT1)) return true;
        if (x > IntX0 && x < IntX1 && (EnRangoAngulo(t, -55f, -25f) || EnRangoAngulo(t, 205f, 235f))) return true;
        if (x > ColaX0 && x < ColaX1 && EnRangoAngulo(t, 250f, 290f)) return true;
        return false;
    }

    List<float> xsFus;

    void ConstruirFuselaje()
    {
        xsFus = Estaciones(0.70f, FusX1, 0.10f, 1.0f, 1.5f, 2.0f, CowlX0, CowlX1, IntX0, IntX1, CabX0, CabFrente, CabX1, CabAtras, ColaX0, ColaX1);
        var xs = xsFus;
        var Pm = new Vector3[xs.Count, NJ + 1];
        var U = new Vector2[xs.Count, NJ + 1];
        for (int i = 0; i < xs.Count; i++)
            for (int j = 0; j <= NJ; j++)
            {
                Pm[i, j] = PuntoFus(xs[i], TGrados(j) * Mathf.Deg2Rad);
                U[i, j] = UVFus(xs[i], (float)j / NJ);
            }
        var go = Vacio(tBody, "Fuselaje");
        var m = MallaDe(go);
        m.Rejilla(Pm, U, costura: true,
            incluir: (i, j) => !HuecoFus((xs[i] + xs[i + 1]) * 0.5f, TGrados(j) + 180f / NJ));
        var anillo = new List<Vector3>();
        for (int j = 0; j <= NJ; j++) anillo.Add(Pm[xs.Count - 1, j]);
        m.Tapa(anillo, Atras, UVFus(FusX1, 0.5f));

        // Labio de la cofia: entra hacia el motor (interior oscuro) y sale a la piel exterior.
        float[] lx = { 1.26f, 1.00f, 0.86f, 0.74f, 0.66f, 0.625f, 0.62f, 0.635f, 0.665f, 0.70f };
        float[] le = { 0.86f, 0.86f, 0.865f, 0.875f, 0.895f, 0.925f, 0.955f, 0.982f, 0.995f, 1f };
        var Pl = new Vector3[lx.Length, NJ + 1];
        var Ul = new Vector2[lx.Length, NJ + 1];
        for (int i = 0; i < lx.Length; i++)
            for (int j = 0; j <= NJ; j++)
            {
                var s = SeccionFus(0.70f, TGrados(j) * Mathf.Deg2Rad, le[i]);
                Pl[i, j] = P(lx[i], s.x, s.y);
                Ul[i, j] = new Vector2(i < 4 ? 0.004f : (i == lx.Length - 1 ? UVFus(0.70f, 0f).x : UVFus(FusX0, 0f).x), (float)j / NJ);
            }
        m.Rejilla(Pl, Ul, costura: true);
        Poner(go, m, mFus);
        // Mampara oscura al fondo de la cofia (detrás de los cilindros): no deja ver el interior vacío.
        var mampara = Vacio(tBody, "Mampara_Motor");
        var mmp = MallaDe(mampara);
        var fondoCofia = new List<Vector3>();
        for (int j = 0; j <= NJ; j++) fondoCofia.Add(Pl[0, j]);
        mmp.Tapa(fondoCofia, Vector3.forward, Vector2.zero);
        Poner(mampara, mmp, mMetalOscuro, null, false);

        ConstruirAletasCofia();
    }

    // Parche de la piel del fuselaje (para aletas y compuertas que deben quedar al ras).
    void ParchePiel(Malla exterior, Malla interior, float x0, float x1, float t0, float t1, float escalaInt = 0.994f)
    {
        var xs = xsFus.FindAll(x => x >= x0 - 1e-4f && x <= x1 + 1e-4f);
        int nt = Mathf.Max(1, Mathf.RoundToInt((t1 - t0) / 5f));
        var Pe = new Vector3[xs.Count, nt + 1]; var Ue = new Vector2[xs.Count, nt + 1];
        var Pi = new Vector3[xs.Count, nt + 1];
        for (int i = 0; i < xs.Count; i++)
            for (int k = 0; k <= nt; k++)
            {
                float t = Mathf.Lerp(t0, t1, (float)k / nt);
                Pe[i, k] = PuntoFus(xs[i], t * Mathf.Deg2Rad);
                Ue[i, k] = UVFus(xs[i], (t + 90f) / 360f);   // sin envolver: la textura repite en V
                Pi[i, nt - k] = PuntoFus(xs[i], t * Mathf.Deg2Rad, escalaInt);   // orden invertido: mira hacia adentro
            }
        exterior.Rejilla(Pe, Ue);
        interior?.Rejilla(Pi, null);
    }

    // Aleta con bisagra en su borde delantero. Devuelve el pivote ya registrado en el CowlFlapsAnimator.
    void AletaFus(Transform padre, string nombreAleta, float x0, float x1, float t0, float t1, float angulo)
    {
        Vector3 a = PuntoFus(x0, t0 * Mathf.Deg2Rad), b = PuntoFus(x0, t1 * Mathf.Deg2Rad);
        float tm = (t0 + t1) * 0.5f;
        Vector3 fuera = (PuntoFus(x0, tm * Mathf.Deg2Rad) - P(x0, 0f, FusMed[x0])).normalized;
        var bisagra = Vacio(padre, nombreAleta, (a + b) * 0.5f, Ejes((b - a).normalized, fuera));
        var malla = Vacio(bisagra.transform, nombreAleta + "_Malla", bisagra.transform.position, bisagra.transform.rotation);
        Malla ext = MallaDe(malla), inte = MallaDe(malla);
        ParchePiel(ext, inte, x0, x1, t0, t1);
        Poner(malla, ext, mFus);
        Poner(malla, inte, mMetalOscuro, "Interior", false);

        // Sentido de apertura: el borde trasero debe salir hacia afuera.
        Vector3 local = bisagra.transform.InverseTransformPoint(PuntoFus(x1, tm * Mathf.Deg2Rad));
        Vector3 girado = Quaternion.AngleAxis(10f, Vector3.right) * local;
        Vector3 eje = girado.y > local.y ? Vector3.right : Vector3.left;
        aletasMotor.Add(new CowlFlapsAnimator.Aleta { aleta = bisagra.transform, eje = eje, anguloAbierta = angulo });
    }

    void ConstruirAletasCofia()
    {
        var grupo = Vacio(tAnim, "Aletas_Enfriamiento");
        const int n = 10;
        float paso = (CowlT1 - CowlT0) / n;
        for (int k = 0; k < n; k++)
            AletaFus(grupo.transform, $"Aleta_Cofia_{k:00}", CowlX0, CowlX1, CowlT0 + paso * k, CowlT0 + paso * (k + 1), 16f);
        AletaFus(grupo.transform, "Compuerta_Intercooler_Der", IntX0, IntX1, -55f, -25f, 24f);
        AletaFus(grupo.transform, "Compuerta_Intercooler_Izq", IntX0, IntX1, 205f, 235f, 24f);

        // Fondo oscuro bajo las aletas: con ellas abiertas no se ve el interior vacío del fuselaje.
        var fondo = Vacio(tBody, "Fondo_Aletas");
        var m = MallaDe(fondo);
        Franja(m, 2.10f, 2.46f, CowlT0 - 5f, CowlT1 + 5f, 0.94f);
        Franja(m, 2.56f, 3.06f, -60f, -20f, 0.95f);
        Franja(m, 2.56f, 3.06f, 200f, 240f, 0.95f);
        Poner(fondo, m, mMetalOscuro, null, false);
    }

    static void Franja(Malla m, float x0, float x1, float t0, float t1, float escala)
    {
        int nt = Mathf.Max(1, Mathf.RoundToInt((t1 - t0) / 5f));
        var Pf = new Vector3[2, nt + 1];
        for (int k = 0; k <= nt; k++)
        {
            float t = Mathf.Lerp(t0, t1, (float)k / nt) * Mathf.Deg2Rad;
            Pf[0, k] = PuntoFus(x0, t, escala); Pf[1, k] = PuntoFus(x1, t, escala);
        }
        m.Rejilla(Pf, null);
    }

    // ---------- Motor R-2800 visible por la cofia y hélice Curtiss Electric ----------
    static readonly Curva CuerdaPala = new Curva(0.09f, 0.15f, 0.15f, 0.24f, 0.22f, 0.40f, 0.30f, 0.37f, 0.40f, 0.33f,
        0.55f, 0.35f, 0.70f, 0.345f, 0.82f, 0.32f, 0.90f, 0.28f, 0.95f, 0.22f, 0.985f, 0.13f, 1.0f, 0.04f);
    static readonly Curva EspesorPala = new Curva(0.09f, 0.85f, 0.15f, 0.6f, 0.22f, 0.22f, 0.30f, 0.14f, 0.50f, 0.09f, 0.80f, 0.065f, 1.0f, 0.05f);
    static readonly Curva PasoPala = new Curva(0.09f, 62f, 0.25f, 48f, 0.50f, 36f, 0.75f, 28f, 1.0f, 22f);

    void ConstruirMotorYHelice()
    {
        // Motor: caja de reducción, dos estrellas de 9 cilindros con aletas, mampara y toma inferior.
        var go = Vacio(tBody, "Motor_Radial");
        var m = MallaDe(go);
        m.Elipsoide(P(0.86f, 0f, 0f), new Vector3(0.36f, 0.36f, 0.21f), Quaternion.identity, 8, 20);
        m.Tubo(P(0.86f, 0f, 0f), P(1.04f, 0f, 0f), 0.36f, 0.40f, 20, false);
        m.Tubo(P(0.45f, 0f, 0f), P(0.70f, 0f, 0f), 0.06f, 0.06f, 12, false);   // eje de la hélice
        for (int fila = 0; fila < 2; fila++)
            for (int k = 0; k < 9; k++)
            {
                float ang = (90f + 40f * k + 20f * fila) * Mathf.Deg2Rad;
                float x = fila == 0 ? 1.00f : 1.15f;
                Vector3 radial = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                Vector3 c0 = P(x, 0f, 0f) + radial * 0.30f, c1 = P(x, 0f, 0f) + radial * 0.50f;
                m.Tubo(c0, c1, 0.07f, 0.065f, 10, true);
                for (int a = 0; a < 6; a++)
                {
                    Vector3 pa = Vector3.Lerp(c0, c1, 0.25f + a * 0.13f);
                    m.Tubo(pa, pa + radial * 0.009f, 0.1f, 0.1f, 12, true);
                }
                m.Caja(c1 + radial * 0.02f, new Vector3(0.12f, 0.06f, 0.12f), Quaternion.LookRotation(Vector3.forward, radial));
                m.Tubo(P(x - 0.05f, 0f, 0f) + radial * 0.33f, P(x - 0.05f, 0f, 0f) + radial * 0.52f, 0.012f, 0.012f, 6, false);
            }
        m.Caja(P(0.84f, 0f, -0.56f), new Vector3(0.88f, 0.02f, 0.40f), Quaternion.identity);   // separador de la toma
        Poner(go, m, mMetalOscuro);
        // La toma inferior es ovalada: tapa negra alargada
        var toma = Vacio(tBody, "Toma_Intercooler");
        var mt = MallaDe(toma);
        var ovalo = new List<Vector3>();
        for (int k = 0; k <= 32; k++)
        {
            float a = Mathf.PI * 2f * k / 32;
            ovalo.Add(P(1.06f, Mathf.Cos(a) * 0.46f, -0.71f + Mathf.Sin(a) * 0.17f));
        }
        mt.Tapa(ovalo, Vector3.forward, Vector2.zero);
        Poner(toma, mt, mNegro, null, false);

        // Hélice: pivote en el centro del buje, eje de giro = +Z local.
        Vector3 pivote = P(PlanoHelice, 0f, 0f);
        var motor = Vacio(tAnim, "Motor", pivote);
        GameObject aspas = Vacio(motor.transform, "Aspas", pivote), puntas = Vacio(motor.transform, "Puntas", pivote);
        Malla mAs = new Malla(), mPu = new Malla();
        float rCambio = RadioHelice - 0.10f;   // franja amarilla de 10 cm en la punta
        var radios1 = Estaciones(0.18f, rCambio, 0.06f);
        var radios2 = Estaciones(rCambio, RadioHelice, 0.02f);
        for (int k = 0; k < 4; k++)
        {
            var giro = Matrix4x4.TRS(pivote, Quaternion.AngleAxis(90f * k, Vector3.forward), Vector3.one) * Matrix4x4.Translate(-pivote);
            mAs.Matriz = aspas.transform.worldToLocalMatrix * giro;
            mPu.Matriz = puntas.transform.worldToLocalMatrix * giro;
            Pala(mAs, radios1, pivote, false);
            Pala(mPu, radios2, pivote, true);
        }
        rendAspas.Add(Poner(aspas, mAs, mNegro));
        rendAspas.Add(Poner(puntas, mPu, mAmarillo));

        // Buje (cubo con tapa redondeada)
        var buje = Vacio(motor.transform, "Buje", pivote);
        var mb = MallaDe(buje);
        float[] bx = { 0f, 0.01f, 0.03f, 0.06f, 0.10f, 0.15f, 0.22f, 0.30f, 0.40f, 0.50f, 0.55f, 0.66f };
        var Pb = new Vector3[bx.Length, 33];
        for (int i = 0; i < bx.Length; i++)
        {
            float r = bx[i] <= 0.55f ? 0.28f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((0.55f - bx[i]) / 0.55f, 2f))) : 0.28f;
            for (int j = 0; j <= 32; j++)
            {
                float a = Mathf.PI * 2f * j / 32;
                Pb[i, j] = P(bx[i], Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
        }
        mb.Rejilla(Pb, null, i => P(bx[i], 0f, 0f), costura: true);
        Poner(buje, mb, mBuje);

        // Disco borroso (lo maneja PropellerAnimator a RPM altas)
        var disco = Vacio(motor.transform, "Disco_Borroso", pivote);
        var md = MallaDe(disco);
        DiscoUV(md, pivote, Vector3.forward, RadioHelice, 64);
        DiscoUV(md, pivote, Vector3.back, RadioHelice, 64);
        rendDisco = Poner(disco, md, mDisco, null, false);
        rendDisco.enabled = false;
    }

    // Una pala apuntando hacia +Y desde el pivote, girando en sentido horario visto desde la cabina.
    static void Pala(Malla m, List<float> radios, Vector3 pivote, bool cerrarPunta)
    {
        var f = FraccionesCuerda(10, 0, 1f);
        int nc = f.Length, cols = 2 * nc - 1;
        var Pp = new Vector3[radios.Count, cols];
        for (int i = 0; i < radios.Count; i++)
        {
            float r = radios[i], rn = r / RadioHelice;
            float c = CuerdaPala[rn], tt = EspesorPala[rn], beta = PasoPala[rn] * Mathf.Deg2Rad;
            Vector3 dirCuerda = Mathf.Cos(beta) * Vector3.right + Mathf.Sin(beta) * Vector3.forward;   // de salida a ataque
            Vector3 dirEsp = -Mathf.Sin(beta) * Vector3.right + Mathf.Cos(beta) * Vector3.forward;    // cara curvada al frente
            for (int k = 0; k < cols; k++)
            {
                bool inferior = k < nc;
                float xi = inferior ? f[nc - 1 - k] : f[k - nc + 1];
                float h = (NacaCurvatura(xi, 0.04f, 0.3f) + (inferior ? -1f : 1f) * NacaEspesor(xi, tt)) * c;
                Pp[i, k] = pivote + Vector3.up * r + dirCuerda * ((0.35f - xi) * c) + dirEsp * h;
            }
        }
        m.Rejilla(Pp, null, i => pivote + Vector3.up * radios[i]);
        if (!cerrarPunta) return;
        var perfil = new List<Vector3>();
        for (int k = 0; k < cols; k++) perfil.Add(Pp[radios.Count - 1, k]);
        m.Tapa(perfil, Vector3.up, Vector2.zero);
    }

    static void DiscoUV(Malla m, Vector3 centro, Vector3 normal, float radio, int lados)
    {
        Vector3 a = Vector3.right, b = Vector3.up;
        int ic = m.V(centro, normal, new Vector2(0.5f, 0.5f));
        var ids = new int[lados + 1];
        for (int k = 0; k <= lados; k++)
        {
            float ang = Mathf.PI * 2f * k / lados;
            ids[k] = m.V(centro + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * radio, normal,
                         new Vector2(0.5f + 0.5f * Mathf.Cos(ang), 0.5f + 0.5f * Mathf.Sin(ang)));
        }
        bool haciaAdelante = Vector3.Dot(normal, Vector3.forward) > 0f;
        for (int k = 0; k < lados; k++)
        {
            // Cross(b-a, c-a) con a=centro, b=k, c=k+1 apunta a +Z (los puntos giran de X hacia Y)
            if (haciaAdelante) m.T(ic, ids[k], ids[k + 1]); else m.T(ic, ids[k + 1], ids[k]);
        }
    }

    // ---------- Cabina: parabrisas, burbuja deslizable e interior ----------
    static readonly Curva ZTopeCab = new Curva(3.95f, 1.12f, 4.20f, 1.235f, 4.55f, 1.28f, 5.0f, 1.245f, 5.30f, 1.12f,
        5.50f, 0.93f, 5.62f, 0.76f);

    static Vector2 BordeCab(float x) => SeccionFus(Mathf.Min(x, CabX1), AnguloBorde * Mathf.Deg2Rad);

    // Altura de la piel superior del fuselaje a la distancia lateral w.
    static float ZSupFusEnY(float x, float w)
    {
        float lo = 0f, hi = Mathf.PI * 0.5f;
        for (int k = 0; k < 30; k++)
        {
            float mid = (lo + hi) * 0.5f;
            if (SeccionFus(x, mid).x > w) lo = mid; else hi = mid;
        }
        return SeccionFus(x, (lo + hi) * 0.5f).y;
    }

    static Vector3 PuntoCab(float x, float phi)
    {
        float w, zb;
        if (x <= CabX1) { var b = BordeCab(x); w = b.x; zb = b.y; }
        else
        {
            float u = Mathf.SmoothStep(0f, 1f, (x - CabX1) / (CabAtras - CabX1));
            w = Mathf.Lerp(BordeCab(CabX1).x, 0.16f, u);
            zb = ZSupFusEnY(x, w) + 0.004f;
        }
        float zt = ZTopeCab[x];
        return P(x, w * Mathf.Cos(phi), zb + (zt - zb) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phi)), 0.75f));
    }

    void ConstruirCabina()
    {
        const int NC = 24;
        // Parabrisas: superficie reglada entre el borde del fuselaje en CabX0 y el arco delantero de la burbuja.
        var Pw = new Vector3[6, NC + 1];
        for (int i = 0; i < 6; i++)
            for (int k = 0; k <= NC; k++)
            {
                float s = (float)k / NC;
                Vector3 a = PuntoFus(CabX0, Mathf.Lerp(AnguloBorde, 180f - AnguloBorde, s) * Mathf.Deg2Rad);
                Vector3 b = PuntoCab(CabFrente, s * Mathf.PI);
                Pw[i, k] = Vector3.Lerp(a, b, i / 5f);
            }
        var vidrioPb = Vacio(tBody, "Parabrisas_Vidrio");
        var mv = MallaDe(vidrioPb);
        mv.Rejilla(Pw, null, i => P(CabX0 + 0.5f, 0f, 0.5f));
        Poner(vidrioPb, mv, mVidrio, null, false);
        // Marco del parabrisas: arco de arriba, poste, base y el otro poste en un solo tubo continuo (sin rendijas; una
        // esfera une el principio con el final). Los postes intermedios, más delgados, terminan dentro de ese tubo.
        var marcoPb = Vacio(tBody, "Parabrisas_Marco");
        var mm = MallaDe(marcoPb);
        var contorno = new List<Vector3>();
        for (int k = NC; k >= 0; k--) contorno.Add(Pw[5, k]);
        for (int k = 0; k <= NC; k++) contorno.Add(Pw[0, k]);
        contorno.Add(Pw[5, NC]);
        mm.TuboCurvo(contorno, 0.017f, 8, false, UVPintura);
        mm.Elipsoide(Pw[5, NC], Vector3.one * 0.017f, Quaternion.identity, 6, 8, UVPintura);
        foreach (int k in new[] { 7, 17 }) mm.Tubo(Pw[0, k], Pw[5, k], 0.015f, 0.015f, 8, false, UVPintura);
        Poner(marcoPb, mm, mFus);

        // Burbuja: PuertaCabina (riel fijo) > CabinaMovible (se desliza hacia atrás por su Z local).
        var puerta = Vacio(raiz.transform, "PuertaCabina", P(CabFrente, 0f, 0.70f));
        var movible = Vacio(puerta.transform, "CabinaMovible", puerta.transform.position);
        var xs = Estaciones(CabFrente, CabAtras, 0.07f);
        var Pc = new Vector3[xs.Count, NC + 1];
        for (int i = 0; i < xs.Count; i++)
            for (int k = 0; k <= NC; k++) Pc[i, k] = PuntoCab(xs[i], Mathf.PI * k / NC);
        var vidrio = Vacio(movible.transform, "Vidrio_Cabina", movible.transform.position);
        var mc = MallaDe(vidrio);
        mc.Rejilla(Pc, null, i => P(xs[i], 0f, 0.55f));
        var trasero = new List<Vector3>();
        for (int k = 0; k <= NC; k++) trasero.Add(Pc[xs.Count - 1, k]);
        trasero.Add(trasero[0]);   // cerrado: el abanico también cubre la base, entre los rieles
        mc.Tapa(trasero, Atras, Vector2.zero);
        Poner(vidrio, mc, mVidrio, null, false);
        // Marco: riel derecho, arco delantero y riel izquierdo en un solo tubo continuo.
        var marco = Vacio(movible.transform, "Marco_Cabina", movible.transform.position);
        var mf = MallaDe(marco);
        var trazo = new List<Vector3>();
        for (int i = xs.Count - 1; i > 0; i--) trazo.Add(Pc[i, 0]);
        for (int k = 0; k <= NC; k++) trazo.Add(Pc[0, k]);
        for (int i = 1; i < xs.Count; i++) trazo.Add(Pc[i, NC]);
        mf.TuboCurvo(trazo, 0.02f, 8, true, UVPintura);
        Poner(marco, mf, mFus);
        // Manija para agarrar y deslizar la burbuja (lado izquierdo del arco delantero, por dentro).
        Vector3 pm = PuntoCab(CabFrente + 0.06f, Mathf.PI * 0.80f) + new Vector3(0.05f, -0.02f, 0f);
        var manija = Vacio(movible.transform, "Manija", pm, Quaternion.identity);
        var mh = MallaDe(manija);
        mh.Tubo(pm + new Vector3(0f, 0f, 0.06f), pm + new Vector3(0f, 0f, -0.06f), 0.012f, 0.012f, 8, true);
        Poner(manija, mh, mPanel);
        var col = manija.AddComponent<BoxCollider>();
        col.size = new Vector3(0.08f, 0.08f, 0.18f);
        ConfigurarCabinaDeslizable(movible, RecorridoCabina);

        ConstruirInteriorCabina();
    }

    void ConstruirInteriorCabina()
    {
        const float x0 = 3.70f, x1 = CabX1, zPiso = -0.30f, escala = 0.94f;
        var go = Vacio(tBody, "Cabina_Interior");
        var m = MallaDe(go);
        var xs = Estaciones(x0, x1, 0.10f);
        int nt = Mathf.RoundToInt((360f - 2f * (90f - AnguloBorde)) / 5f);   // de un borde al otro por abajo
        var Pi = new Vector3[xs.Count, nt + 1];
        for (int i = 0; i < xs.Count; i++)
            for (int k = 0; k <= nt; k++)
            {
                var p = PuntoFus(xs[i], (AnguloBorde - 5f * k) * Mathf.Deg2Rad, escala);
                if (p.y < zPiso) p.y = zPiso;
                Pi[i, k] = p;
            }
        m.Rejilla(Pi, null);   // filas hacia atrás, columnas hacia abajo: mira hacia adentro
        // Bordes superiores (entre la piel y la pared interior)
        foreach (float t in new[] { AnguloBorde, 180f - AnguloBorde })
        {
            var xb = Estaciones(CabX0, CabX1, 0.10f);
            var Pbd = new Vector3[xb.Count, 2];
            for (int i = 0; i < xb.Count; i++)
            {
                Pbd[i, 0] = PuntoFus(xb[i], t * Mathf.Deg2Rad);
                Pbd[i, 1] = PuntoFus(xb[i], t * Mathf.Deg2Rad, escala);
            }
            m.Rejilla(Pbd, null, i => P(xb[i], 0f, -1f));
        }
        // Paredes de los extremos como contornos cerrados (Tapa no une el último punto con el primero). La de atrás
        // sube hasta el lomo por la piel exterior: es la mampara detrás del asiento, sin huecos hacia el fuselaje.
        var frente = new List<Vector3>(); var fondo = new List<Vector3>();
        for (int k = 0; k <= nt; k++) { frente.Add(Pi[0, k]); fondo.Add(Pi[xs.Count - 1, k]); }
        for (float t = 180f - AnguloBorde; t >= AnguloBorde - 0.1f; t -= 5f) fondo.Add(PuntoFus(CabX1, t * Mathf.Deg2Rad));
        frente.Add(frente[0]);
        fondo.Add(fondo[0]);
        m.Tapa(frente, Atras, Vector2.zero);
        m.Tapa(fondo, -Atras, Vector2.zero);
        Poner(go, m, mInterior);

        // Tablero, visera, mira, asiento con blindaje y consolas laterales
        var tablero = Vacio(tBody, "Tablero");
        var mt = MallaDe(tablero);
        mt.Caja(P(3.86f, 0f, 0.31f), new Vector3(1.0f, 0.58f, 0.03f), Quaternion.identity);
        mt.Caja(P(3.92f, 0f, 0.72f), new Vector3(0.09f, 0.10f, 0.16f), Quaternion.identity);   // mira K-14
        mt.Caja(P(4.85f, 0f, 0.03f), new Vector3(0.44f, 0.06f, 0.42f), Quaternion.identity);
        mt.Caja(P(5.05f, 0f, 0.36f), new Vector3(0.44f, 0.60f, 0.05f), Quaternion.Euler(-12f, 0f, 0f));
        mt.Caja(P(5.12f, 0f, 0.82f), new Vector3(0.34f, 0.36f, 0.03f), Quaternion.identity);
        mt.Caja(P(4.40f, -0.44f, 0.08f), new Vector3(0.12f, 0.10f, 0.70f), Quaternion.identity);
        mt.Caja(P(4.40f, 0.44f, 0.08f), new Vector3(0.12f, 0.10f, 0.70f), Quaternion.identity);
        Poner(tablero, mt, mPanel);
        // Visera del tablero: sigue el contorno del fuselaje desde la base del parabrisas (sin rendija hacia el morro hueco)
        var visera = Vacio(tBody, "Visera");
        var mvz = MallaDe(visera);
        const int nv = 14;
        var Pv = new Vector3[3, nv + 1];
        for (int k = 0; k <= nv; k++)
        {
            float t = Mathf.Lerp(AnguloBorde, 180f - AnguloBorde, (float)k / nv) * Mathf.Deg2Rad;
            Pv[0, k] = PuntoFus(CabX0, t);
            Pv[1, k] = PuntoFus(3.98f, t, escala);
            Pv[2, k] = Pv[1, k] + Vector3.down * 0.07f;
        }
        mvz.Rejilla(Pv, null, i => P(3.8f, 0f, -2f));
        Poner(visera, mvz, mPanel);
        var reflector = Vacio(tBody, "Mira_Reflector");
        var mr = MallaDe(reflector);
        mr.Quad(P(3.97f, -0.05f, 0.77f), P(3.97f, 0.05f, 0.77f), P(4.01f, 0.05f, 0.86f), P(4.01f, -0.05f, 0.86f), Atras);
        Poner(reflector, mr, mVidrio, null, false);
    }
}
#endif
