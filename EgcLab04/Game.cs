using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Fereastra aplicației și bucla principală (main game loop).
/// Față de laboratorul 3: o scenă cu mai multe obiecte, fiecare cu matricea lui model,
/// o cameră cu matricele view și projection, tastatura și mouse-ul.
/// </summary>
public class Game : GameWindow
{
    // Obiecte care NU depind de OpenGL pot fi create imediat.
    private readonly Camera _camera = new();

    // Obiectele care folosesc OpenGL se creează în OnLoad (abia atunci există contextul OpenGL).
    private Shader _shader = null!;
    private readonly List<SceneObject> _scene = new();
    private readonly List<Transform2D> _initialTransforms = new();   // pentru tasta R

    // Starea aplicației (se modifică în OnUpdateFrame, se folosește în OnRenderFrame).
    private int _selected;            // indexul obiectului selectat în _scene (tasta Tab)
    private bool _wireframe;          // contur (true) sau plin (false)

    // T4 (facultativ): pivotul stabilit cu clic dreapta (null = încă nestabilit) și un marcaj mic desenat în el.
    private Vector2? _pivot;
    private Mesh _pivotMarker = null!;

    // T5 (facultativ): satelitul obiectului selectat și unghiul lui pe orbită (în grade).
    private Mesh _satellite = null!;
    private float _orbitAngle;

    // Contoare afișate în titlul ferestrei.
    private readonly string _baseTitle;
    private double _secondTimer;
    private int _frames, _lastFrames;
    private bool _titleDirty = true;

    public Game(int width, int height, string title)
        : base(GameWindowSettings.Default,
               new NativeWindowSettings
               {
                   ClientSize = new Vector2i(width, height),
                   Title = title,
                   APIVersion = new Version(3, 3),          // cerem OpenGL 3.3 ...
                   Profile = ContextProfile.Core,           // ... profil Core (fără modul imediat)
                   Flags = ContextFlags.ForwardCompatible
               })
    {
        _baseTitle = title;
        VSync = VSyncMode.On;   // swap-ul bufferelor așteaptă reîmprospătarea monitorului
    }

    /// <summary>Apelată O SINGURĂ DATĂ, după crearea ferestrei și a contextului OpenGL.</summary>
    protected override void OnLoad()
    {
        base.OnLoad();

        Console.WriteLine($"OpenGL  : {GL.GetString(StringName.Version)}");
        Console.WriteLine($"Renderer: {GL.GetString(StringName.Renderer)}");

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        _camera.Resize(ClientSize.X, ClientSize.Y);

        _shader = Shader.FromFiles("Shaders/shader.vert", "Shaders/shader.frag");

        // Scena: aceleași forme din laboratorul 3, acum plasate în lume prin Transform2D.
        // Depth (z) contează doar în perspectivă (tasta O, secțiunea 7.2).
        AddObject("pătrat", Shapes.Quad(),
                  new Transform2D { Position = new Vector2(-1.8f, -1.0f), Depth = -3.0f });
        AddObject("triunghi", Shapes.Triangle(),
                  new Transform2D { Position = new Vector2(-1.2f, 1.0f) });
        AddObject("hexagon", Shapes.RegularPolygon(6, 0.5f),
                  new Transform2D { Position = new Vector2(2.0f, 1.0f) });
        AddObject("cerc", Shapes.RegularPolygon(64, 0.5f),
                  new Transform2D { Position = new Vector2(0.8f, -0.6f), Depth = 2.0f });

        // T4: marcajul pivotului (un cerc mic); nu face parte din _scene, deci nu poate fi selectat cu Tab.
        _pivotMarker = Shapes.RegularPolygon(16, 0.05f);

        // T5: satelitul este un triunghi; nu face parte din _scene (nu se selectează cu Tab, nu are Transform2D).
        _satellite = Shapes.Triangle();
    }

    /// <summary>Adaugă un obiect în scenă și reține transformarea lui inițială (pentru tasta R).</summary>
    private void AddObject(string name, Mesh mesh, Transform2D transform)
    {
        _scene.Add(new SceneObject(name, mesh, transform));
        _initialTransforms.Add(transform.Clone());
    }

    /// <summary>Apelată când se schimbă dimensiunea ferestrei.</summary>
    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        GL.Viewport(0, 0, e.Width, e.Height);   // zona ferestrei în care se desenează
        _camera.Resize(e.Width, e.Height);      // proiecția se adaptează la noul raport de aspect
    }

    /// <summary>Apelată la fiecare iterație a buclei: LOGICA aplicației (intrări, mișcare, timp).</summary>
    protected override void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);

        if (KeyboardState.IsKeyDown(Keys.Escape))
        {
            Close();
        }

        // --- Obiectul selectat: tastatura (secțiunea 8) ------------------------------

        float dt = (float)args.Time;
        SceneObject sel = _scene[_selected];

        // T2: Shift stânga ținut apăsat face toate cele trei mișcări ale obiectului de 3 ori mai rapide.
        //     Pentru scalare, „de 3 ori mai rapid” înseamnă factorul 1,5 aplicat de 3 ori pe secundă: 1,5^(3·dt).
        float speedUp = KeyboardState.IsKeyDown(Keys.LeftShift) ? 3.0f : 1.0f;

        const float moveSpeed = 2.0f;   // unități de lume pe secundă
        if (KeyboardState.IsKeyDown(Keys.Left))  sel.Transform.Position.X -= moveSpeed * speedUp * dt;
        if (KeyboardState.IsKeyDown(Keys.Right)) sel.Transform.Position.X += moveSpeed * speedUp * dt;
        if (KeyboardState.IsKeyDown(Keys.Down))  sel.Transform.Position.Y -= moveSpeed * speedUp * dt;
        if (KeyboardState.IsKeyDown(Keys.Up))    sel.Transform.Position.Y += moveSpeed * speedUp * dt;

        // T2: Q / E = rotație cu 90° pe secundă (grade pe secundă, adunate la unghi).
        //     Q = sens trigonometric (unghi pozitiv), E = sens orar (unghi negativ).
        const float rotateSpeed = 90.0f;   // grade pe secundă
        if (KeyboardState.IsKeyDown(Keys.Q)) sel.Transform.Rotation += rotateSpeed * speedUp * dt;
        if (KeyboardState.IsKeyDown(Keys.E)) sel.Transform.Rotation -= rotateSpeed * speedUp * dt;

        // T2: Z / X = micșorare / mărire uniformă cu factorul 1,5 pe secundă: pe un cadru de durată dt,
        //     scara se înmulțește cu 1,5^dt (sau se împarte la 1,5^dt), apoi se limitează între 0,1 și 10.
        // T2: scalarea se face prin înmulțire și nu prin adunare pentru că scara este un raport (de câte ori
        //     e mai mare obiectul), nu o distanță. Prin adunare (de ex. −0,5 pe secundă), scara ar ajunge la 0
        //     (obiectul dispare) și apoi la valori negative (forma se oglindește, „se întoarce”); în plus, viteza
        //     aparentă ar depinde de mărimea curentă. Prin înmulțire, scara se apropie de 0 fără să-l atingă,
        //     iar mărirea și micșorarea sunt simetrice: o secundă de X urmată de o secundă de Z readuce exact
        //     mărimea inițială (1,5 · 1/1,5 = 1). Puterea f^dt face rezultatul independent de numărul de cadre.
        const float scaleFactor = 1.5f;    // factor pe secundă
        float scaleStep = MathF.Pow(scaleFactor, speedUp * dt);
        if (KeyboardState.IsKeyDown(Keys.X)) sel.Transform.Scale *= scaleStep;
        if (KeyboardState.IsKeyDown(Keys.Z)) sel.Transform.Scale /= scaleStep;
        sel.Transform.Scale.X = Math.Clamp(sel.Transform.Scale.X, 0.1f, 10.0f);
        sel.Transform.Scale.Y = Math.Clamp(sel.Transform.Scale.Y, 0.1f, 10.0f);

        if (KeyboardState.IsKeyPressed(Keys.Tab)) _selected = (_selected + 1) % _scene.Count;
        if (KeyboardState.IsKeyPressed(Keys.O))   _camera.IsPerspective = !_camera.IsPerspective;
        if (KeyboardState.IsKeyPressed(Keys.F))   _wireframe = !_wireframe;
        if (KeyboardState.IsKeyPressed(Keys.R))   ResetScene();

        if (KeyboardState.IsKeyPressed(Keys.Tab) || KeyboardState.IsKeyPressed(Keys.O) ||
            KeyboardState.IsKeyPressed(Keys.R))
        {
            _titleDirty = true;
        }

        // --- Obiectul selectat: mouse-ul (secțiunea 9) -------------------------------

        // T3: la clic stânga, obiectul selectat se mută în punctul din lume aflat sub cursor.
        //     Se schimbă doar Position (x, y); adâncimea (Depth) obiectului rămâne aceeași.
        if (MouseState.IsButtonPressed(MouseButton.Left))
        {
            sel.Transform.Position = ScreenToWorld(MouseState.Position);
        }

        // T4 (facultativ): clic dreapta stabilește pivotul în punctul din lume aflat sub cursor (ScreenToWorld din T3).
        if (MouseState.IsButtonPressed(MouseButton.Right))
        {
            _pivot = ScreenToWorld(MouseState.Position);
            Console.WriteLine($"Pivot: ({_pivot.Value.X:F2}; {_pivot.Value.Y:F2})");
        }

        // T4: cât timp P este ținută, obiectul selectat se rotește în jurul pivotului cu 90° pe secundă.
        //     Poziția se rotește în jurul pivotului cu matricea din secțiunea 5.2, T(−p) · R(θ) · T(p),
        //     iar unghiul propriu crește cu același θ, astfel încât obiectul „privește” mereu spre pivot
        //     în același fel (ca Luna față de Pământ: îi arată mereu aceeași față).
        if (_pivot is Vector2 pivot && KeyboardState.IsKeyDown(Keys.P))
        {
            const float pivotSpeed = 90.0f;   // grade pe secundă
            float angle = pivotSpeed * dt;

            Matrix4 aroundPivot = Matrix4.CreateTranslation(-pivot.X, -pivot.Y, 0.0f)
                                * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(angle))
                                * Matrix4.CreateTranslation(pivot.X, pivot.Y, 0.0f);

            Vector4 p = new Vector4(sel.Transform.Position.X, sel.Transform.Position.Y, 0.0f, 1.0f) * aroundPivot;
            sel.Transform.Position = p.Xy;
            sel.Transform.Rotation += angle;
        }

        // T5: o rotație completă (360°) la 4 secunde = 90° pe secundă, independent de numărul de cadre.
        const float orbitPeriod = 4.0f;   // secunde pentru o rotație completă
        _orbitAngle = (_orbitAngle + 360.0f / orbitPeriod * dt) % 360.0f;

        // --- Camera: tastatura și rotița (secțiunea 6.2) -----------------------------

        float camSpeed = 3.0f / _camera.Zoom;                    // unități de lume pe secundă
        if (KeyboardState.IsKeyDown(Keys.A)) _camera.Position.X -= camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.D)) _camera.Position.X += camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.S)) _camera.Position.Y -= camSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.W)) _camera.Position.Y += camSpeed * dt;

        _camera.Zoom *= MathF.Pow(1.1f, MouseState.ScrollDelta.Y);   // un pas al rotiței = ±10 %
        _camera.Zoom = Math.Clamp(_camera.Zoom, 0.1f, 10.0f);

        // --- Titlul ferestrei: obiectul selectat, proiecția, cadre pe secundă --------

        _secondTimer += args.Time;
        if (_secondTimer >= 1.0)
        {
            _lastFrames = _frames;
            _frames = 0;
            _secondTimer -= 1.0;
            _titleDirty = true;
        }

        if (_titleDirty)
        {
            string projection = _camera.IsPerspective ? "perspectivă" : "ortografică";
            Title = $"{_baseTitle} | selectat: {_scene[_selected].Name} | proiecție {projection} | FPS: {_lastFrames}";
            _titleDirty = false;
        }
    }

    /// <summary>Apelată la fiecare iterație a buclei: DESENAREA unui cadru.</summary>
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        _frames++;

        // 1. Ștergem imaginea veche din back buffer.
        GL.ClearColor(0.12f, 0.14f, 0.18f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        // 2. Starea de rasterizare și programul shader.
        SetWireframe(_wireframe);
        _shader.Use();

        // 3. View și projection sunt aceleași pentru toate obiectele: le trimitem o singură dată.
        _shader.SetMatrix4("uView", _camera.ViewMatrix);
        _shader.SetMatrix4("uProjection", _camera.ProjectionMatrix);

        // 4. Fiecare obiect: matricea lui model, nuanța, apoi desenarea.
        foreach (SceneObject obj in _scene)
        {
            _shader.SetMatrix4("uModel", obj.Transform.ModelMatrix);
            _shader.SetColor("uTint", obj.Tint);
            obj.Mesh.Draw();
        }

        // T4: marcajul pivotului, desenat alb, la adâncimea obiectului selectat (pe același plan cu el).
        if (_pivot is Vector2 pv)
        {
            _shader.SetMatrix4("uModel", Matrix4.CreateTranslation(pv.X, pv.Y, _scene[_selected].Transform.Depth));
            _shader.SetColor("uTint", Color4.White);
            _pivotMarker.Draw();
        }

        // T5 (facultativ): satelitul obiectului selectat (nota din secțiunea 5.3: modelCopil = localCopil * modelParinte).
        //     Transformarea locală, față de obiectul selectat: scalare cu 0,3, translație cu (1,2; 0), apoi rotație
        //     cu unghiul orbitei (rotația DUPĂ translație îl duce pe un cerc de rază 1,2 în jurul centrului părintelui,
        //     ca în Fig. 3b). Înmulțită cu matricea model a părintelui, satelitul urmează automat obiectul selectat
        //     când acesta este mutat, rotit sau scalat; cu Tab, „părintele” devine noul obiect selectat.
        // T5: când obiectul selectat este scalat, orbita se scalează și ea: raza devine 1,2 · scara părintelui
        //     (la X orbita se lărgește, la Z se strânge), iar satelitul însuși devine 0,3 · scara părintelui.
        //     Motivul: matricea părintelui (care conține scalarea lui) se aplică DUPĂ transformarea locală, deci
        //     asupra tuturor punctelor satelitului, inclusiv asupra translației (1,2; 0) care dă raza orbitei.
        //     Distanța 1,2 este măsurată în unitățile părintelui, nu în unitățile lumii. La fel, rotația părintelui
        //     (Q / E) se adună la unghiul orbitei, iar dacă scara ar fi neuniformă orbita ar deveni o elipsă.
        Matrix4 satelliteLocal = Matrix4.CreateScale(0.3f)
                               * Matrix4.CreateTranslation(1.2f, 0.0f, 0.0f)
                               * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(_orbitAngle));
        Matrix4 satelliteModel = satelliteLocal * _scene[_selected].Transform.ModelMatrix;

        _shader.SetMatrix4("uModel", satelliteModel);
        _shader.SetColor("uTint", Color4.White);
        _satellite.Draw();

        // 5. Afișăm cadrul terminat: back buffer <-> front buffer.
        SwapBuffers();
    }

    /// <summary>Apelată O SINGURĂ DATĂ, la închiderea ferestrei: eliberăm resursele GPU.</summary>
    protected override void OnUnload()
    {
        foreach (SceneObject obj in _scene)
        {
            obj.Dispose();
        }
        _pivotMarker.Dispose();
        _satellite.Dispose();
        _shader.Dispose();

        base.OnUnload();
    }

    /// <summary>Transformă poziția mouse-ului (pixeli, origine stânga-sus) în coordonate de lume.</summary>
    private Vector2 ScreenToWorld(Vector2 mousePixels)
    {
        // T3: pasul 1, pixel → NDC (inversa transformării viewport). Se folosește ClientSize (zona de desenare,
        //     fără bordură), aceeași cu viewport-ul setat în OnLoad / OnResize.
        float w = ClientSize.X;
        float h = ClientSize.Y;
        float xNdc = 2.0f * mousePixels.X / w - 1.0f;
        // T3: semnul minus (forma 1 − ...) apare pentru că cele două sisteme au axa y în sensuri opuse:
        //     la mouse y crește în JOS (originea e în colțul stânga-sus, y = 0 sus și y = H jos), iar în NDC
        //     y crește în SUS (−1 jos, +1 sus). Fără întoarcerea axei, obiectul ar ajunge în punctul simetric
        //     față de axa orizontală (clic sus → obiect jos).
        float yNdc = 1.0f - 2.0f * mousePixels.Y / h;

        // T3: pasul 2, NDC → lume. Lume → NDC este ViewMatrix * ProjectionMatrix (în C#, de la stânga la dreapta),
        //     deci drumul invers este inversa produsului. Inversa se calculează la fiecare clic, ca să țină cont
        //     de poziția curentă a camerei, de zoom și de raportul de aspect al ferestrei.
        Matrix4 inverse = (_camera.ViewMatrix * _camera.ProjectionMatrix).Inverted();

        if (!_camera.IsPerspective)
        {
            // Ortografic: z_ndc nu influențează x și y din lume, deci z_ndc = 0 este potrivit.
            Vector4 world = new Vector4(xNdc, yNdc, 0.0f, 1.0f) * inverse;
            return world.Xy / world.W;   // la ortografic W = 1, împărțirea nu schimbă nimic
        }

        // Extra (peste cerința T3, nota din secțiunea 9.2): în perspectivă un pixel este o rază. Se transformă
        // invers două puncte ale razei (z_ndc = −1 pe planul near și z_ndc = 1 pe planul far), se împart la w,
        // apoi se intersectează dreapta cu planul z = Depth al obiectului selectat, ca obiectul să ajungă exact
        // sub cursor și în perspectivă (tasta O).
        Vector4 nearH = new Vector4(xNdc, yNdc, -1.0f, 1.0f) * inverse;
        Vector4 farH  = new Vector4(xNdc, yNdc,  1.0f, 1.0f) * inverse;
        Vector3 nearP = nearH.Xyz / nearH.W;
        Vector3 farP  = farH.Xyz / farH.W;

        float planeZ = _scene[_selected].Transform.Depth;
        Vector3 dir = farP - nearP;
        if (MathF.Abs(dir.Z) < 1e-6f) return nearP.Xy;   // rază paralelă cu planul (nu apare la camera din șablon)
        float t = (planeZ - nearP.Z) / dir.Z;
        return (nearP + t * dir).Xy;
    }

    /// <summary>Tasta R: obiectele revin la transformările inițiale, camera la starea inițială.</summary>
    private void ResetScene()
    {
        for (int i = 0; i < _scene.Count; i++)
        {
            _scene[i].Transform = _initialTransforms[i].Clone();
        }
        _camera.Reset();
        _pivot = null;   // T4: și pivotul se șterge
    }

    /// <summary>
    /// Contur / plin. Dacă Visual Studio semnalează o eroare la primul parametru,
    /// folosiți aici tipul acceptat de versiunea voastră (vezi laboratorul 2, Problema 3):
    /// TriangleFace.FrontAndBack sau MaterialFace.FrontAndBack.
    /// </summary>
    private static void SetWireframe(bool wireframe)
    {
        GL.PolygonMode(TriangleFace.FrontAndBack, wireframe ? PolygonMode.Line : PolygonMode.Fill);
    }
}
