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

    // Contoare afișate în titlul ferestrei.
    private readonly string _baseTitle;
    private double _secondTimer;
    private int _frames, _lastFrames;
    private bool _titleDirty = true;
    private Vector2 _pivot; // Poziția pivotului în spațiul lume (T4)
    private SceneObject _satellite = null!; // Obiectul satelit (T5)
    private float _orbitAngle;               // Unghiul curent de rotație al satelitului în grade (T5)

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
        //_scene[1].Transform.Rotation = 45f;
        //_scene[1].Transform.Scale = new Vector2(1.5f, 0.5f);
        _satellite = new SceneObject("satelit", Shapes.Triangle(), new Transform2D());
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

        const float moveSpeed = 2.0f;   // unități de lume pe secundă
        if (KeyboardState.IsKeyDown(Keys.Left))  sel.Transform.Position.X -= moveSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.Right)) sel.Transform.Position.X += moveSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.Down))  sel.Transform.Position.Y -= moveSpeed * dt;
        if (KeyboardState.IsKeyDown(Keys.Up))    sel.Transform.Position.Y += moveSpeed * dt;

        // TODO (T2): Q / E rotesc obiectul, Z / X îl scalează
        float speedMultiplier = KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift) ? 3.0f : 1.0f;
        float deltaTime = dt * speedMultiplier;

        if (KeyboardState.IsKeyDown(Keys.Q))
        {
            sel.Transform.Rotation += 90.0f * deltaTime;
        }
        if (KeyboardState.IsKeyDown(Keys.E))
        {
            sel.Transform.Rotation -= 90.0f * deltaTime;
        }
        // T2 Comentariu: Scalarea se face prin înmulțire și nu prin adunare deoarece este o transformare relativă/proporțională. 
        // Dacă am adăuga o valoare fixă, raportul dintre dimensiunile obiectului s-ar modifica diferit în funcție de mărimea curentă.
        if (KeyboardState.IsKeyDown(Keys.Z))
        {
            float scaleFactor = MathF.Pow(1.5f, -deltaTime); // Micșorare
            sel.Transform.Scale *= scaleFactor;
        }
        if (KeyboardState.IsKeyDown(Keys.X))
        {
            float scaleFactor = MathF.Pow(1.5f, deltaTime);  // Mărire
            sel.Transform.Scale *= scaleFactor;
        }
        float clampedX = Math.Clamp(sel.Transform.Scale.X, 0.1f, 10.0f);
        float clampedY = Math.Clamp(sel.Transform.Scale.Y, 0.1f, 10.0f);
        sel.Transform.Scale = new Vector2(clampedX, clampedY);

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

        // TODO (T3): la clic stânga, obiectul selectat se mută în punctul din lume aflat sub cursor
        //            (folosiți ScreenToWorld; Depth nu se schimbă)
        if (MouseState.IsButtonPressed(MouseButton.Left))
        {
            Vector2 worldPos = ScreenToWorld(MouseState.Position);
            sel.Transform.Position = worldPos; 
        }

        // TODO (T4, facultativ): clic dreapta = pivot; tasta P ținută = rotație în jurul pivotului
        if (MouseState.IsButtonPressed(MouseButton.Right))
        {
            _pivot = ScreenToWorld(MouseState.Position);
        }

        
        if (KeyboardState.IsKeyDown(Keys.P))
        {
            
            float rotationDegrees = 90.0f * deltaTime;
            float rotationRadians = MathHelper.DegreesToRadians(rotationDegrees);

            
            sel.Transform.Rotation += rotationDegrees;

           
            Vector2 relativePos = sel.Transform.Position - _pivot;

            
            float cos = MathF.Cos(rotationRadians);
            float sin = MathF.Sin(rotationRadians);

            Vector2 rotatedPos = new Vector2(
                relativePos.X * cos - relativePos.Y * sin,
                relativePos.X * sin + relativePos.Y * cos
            );

            
            sel.Transform.Position = _pivot + rotatedPos;
        }

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
        _orbitAngle += 90.0f * dt;
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

        // TODO (T5, facultativ): satelitul obiectului selectat

        // T5 Comentariu: Când obiectul selectat este scalat, orbita satelitului este și ea deformată/ scalată(devine eliptică sau mai îndepărtată / apropiată).
        // Acest lucru se întâmplă deoarece matricea model a obiectului părinte conține deja transformarea de scalare, 
        // iar prin înmulțirea matricilor (localSatelliteMatrix * parentModelMatrix), toate transformările părintelui 
        // (inclusiv scalarea neuniformă sau uniformă) se aplică asupra întregului spațiu local al satelitului (afectând atât raza orbitei, cât și forma satelitului).
        Matrix4 localSatelliteMatrix =
        Matrix4.CreateScale(0.3f, 0.3f, 1.0f) *
        Matrix4.CreateTranslation(1.2f, 0.0f, 0.0f) *
        Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(_orbitAngle));

        SceneObject currentSelected = _scene[_selected];
        Matrix4 satelliteModelMatrix = localSatelliteMatrix * currentSelected.Transform.ModelMatrix;

        _shader.SetMatrix4("uModel", satelliteModelMatrix);
        _shader.SetColor("uTint", new Color4(1.0f, 0.8f, 0.2f, 1.0f)); // Nuanță galbenă/aurie pentru satelit
        _satellite.Mesh.Draw();
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
        _shader.Dispose();
        _satellite.Dispose();
        base.OnUnload();
    }

    /// <summary>Transformă poziția mouse-ului (pixeli, origine stânga-sus) în coordonate de lume.</summary>
    private Vector2 ScreenToWorld(Vector2 mousePixels)
    {
        // TODO (T3): pasul 1 (pixel → NDC), apoi pasul 2 (NDC → lume, cu inversa lui view * projection)
        // T3 Comentariu: Semnul minus (sau 1 - ...) apare la coordonata Y deoarece sistemul de coordonate al ferestrei
        // are originea (0,0) în colțul stânga-sus și axa Y îndreptată în JOS, în timp ce spațiul NDC și coordonatele de lume
        // au originea în centru/stânga-jos și axa Y îndreptată în SUS.
        float ndcX = (2.0f * mousePixels.X / ClientSize.X) - 1.0f;
        float ndcY = 1.0f - (2.0f * mousePixels.Y / ClientSize.Y);
        Vector4 ndcPos = new Vector4(ndcX, ndcY, 0.0f, 1.0f);
        Matrix4 viewProjection = _camera.ViewMatrix * _camera.ProjectionMatrix;
        Matrix4 invViewProjection = Matrix4.Invert(viewProjection);
        Vector4 worldPos = ndcPos * invViewProjection;
        if (worldPos.W != 0.0f)
        {
            worldPos.X /= worldPos.W;
            worldPos.Y /= worldPos.W;
        }

        return new Vector2(worldPos.X, worldPos.Y);
    }

    /// <summary>Tasta R: obiectele revin la transformările inițiale, camera la starea inițială.</summary>
    private void ResetScene()
    {
        for (int i = 0; i < _scene.Count; i++)
        {
            _scene[i].Transform = _initialTransforms[i].Clone();
        }
        _camera.Reset();
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
