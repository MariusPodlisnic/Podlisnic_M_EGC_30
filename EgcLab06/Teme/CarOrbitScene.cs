using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

public sealed class CarOrbitScene : Scene
{
    private const string Restart = "repornește mașina";

    // Conducere
    private const float MoveSpeed = 6.0f;          // unități / s
    private const float TurnSpeedDeg = 140.0f;     // grade / s (rotație când virezi)
    private const float CarY = 0.55f;
    private static readonly Vector2 PlayMin = new(-10.0f, -8.0f);
    private static readonly Vector2 PlayMax = new(10.0f, 8.0f);

    // Orbită (kinematică, urmează mașina prin ierarhie)
    private const float OrbitRadius = 2.4f;
    private const float OrbitSpeedDeg = 100.0f;    // grade / s

    private SceneObject _carRoot = null!;
    private SceneObject _orbitPivot = null!;
    private SceneObject _sphere = null!;
    private readonly List<SceneObject> _wheels = new();

    private float _headingDeg;   // orientarea mașinii (0 = +X, 90 = +Z ...)
    private float _speed;        // viteza curentă (pentru roți + status)

    public override string Name => "Conduci mașina + sferă pe orbită";

    public override string Status =>
        $"viteză {_speed:F1}  |  direcție {_headingDeg:F0}°  |  orbită {_orbitPivot.Transform.Rotation.Y:F0}°";

    public override IEnumerable<string> ActionNames => new[]
    {
        Actions.Forward, Actions.Back, Actions.Left, Actions.Right, Restart
    };

    public override void Load(Input input)
    {
        CameraPosition = new Vector3(0.0f, 9.0f, 14.0f);
        CameraTarget = new Vector3(0.0f, 0.5f, 0.0f);

        // Controale de conducere (aceleași acțiuni ca în laborator)
        input.Bind(Actions.Forward, Keys.W, Keys.Up);
        input.Bind(Actions.Back, Keys.S, Keys.Down);
        input.Bind(Actions.Left, Keys.A, Keys.Left);
        input.Bind(Actions.Right, Keys.D, Keys.Right);
        input.Bind(Restart, Keys.R);

        Mesh grid = AddMesh(Shapes.Grid(12, 1.0f, new Color4(0.28f, 0.30f, 0.35f, 1.0f)));
        Mesh cube = AddMesh(Shapes.ShadedCube());
        Mesh sphere = AddMesh(Shapes.ShadedSphere(0.45f, 28, 16));
        Mesh cylinder = AddMesh(Shapes.ShadedCylinder(0.35f, 0.22f, 20));

        AddObject("podea", grid);

        BuildCar(cube, cylinder);
        BuildOrbitingSphere(sphere);
        ResetCar();
    }

    private void BuildCar(Mesh cube, Mesh cylinder)
    {
        _carRoot = AddObject("mașină", null);
        _carRoot.Transform.Position = new Vector3(0.0f, CarY, 0.0f);

        // Caroserie – lungimea pe X (fața mașinii = +X local)
        SceneObject body = AddObject("caroserie", cube, _carRoot);
        body.Transform.Position = new Vector3(0.0f, 0.15f, 0.0f);
        body.Transform.Scale = new Vector3(2.4f, 0.55f, 1.15f);
        body.Tint = new Color4(0.85f, 0.18f, 0.15f, 1.0f);

        // Cabină
        SceneObject cabin = AddObject("cabină", cube, _carRoot);
        cabin.Transform.Position = new Vector3(-0.2f, 0.65f, 0.0f);
        cabin.Transform.Scale = new Vector3(1.15f, 0.55f, 1.05f);
        cabin.Tint = new Color4(0.25f, 0.45f, 0.85f, 1.0f);

        // Faruri (în față = +X local)
        SceneObject headL = AddObject("far stânga", cube, _carRoot);
        headL.Transform.Position = new Vector3(1.15f, 0.25f, 0.38f);
        headL.Transform.Scale = new Vector3(0.18f, 0.18f, 0.18f);
        headL.Tint = new Color4(1.0f, 0.95f, 0.55f, 1.0f);

        SceneObject headR = AddObject("far dreapta", cube, _carRoot);
        headR.Transform.Position = new Vector3(1.15f, 0.25f, -0.38f);
        headR.Transform.Scale = new Vector3(0.18f, 0.18f, 0.18f);
        headR.Tint = new Color4(1.0f, 0.95f, 0.55f, 1.0f);

        // Roți
        float[] wheelX = { 0.75f, -0.75f };
        float[] wheelZ = { 0.62f, -0.62f };
        foreach (float wx in wheelX)
        {
            foreach (float wz in wheelZ)
            {
                SceneObject wheel = AddObject("roată", cylinder, _carRoot);
                wheel.Transform.Position = new Vector3(wx, -0.25f, wz);
                wheel.Transform.Rotation = new Vector3(90.0f, 0.0f, 0.0f); // culcat
                wheel.Transform.Scale = new Vector3(0.9f);
                wheel.Tint = new Color4(0.12f, 0.12f, 0.14f, 1.0f);
                _wheels.Add(wheel);
            }
        }
    }

    private void BuildOrbitingSphere(Mesh sphereMesh)
    {
        // Pivot copil al mașinii → sfera urmează automat mașina
        _orbitPivot = AddObject("pivot orbită", null, _carRoot);
        _orbitPivot.Spin = new Vector3(0.0f, OrbitSpeedDeg, 0.0f);

        _sphere = AddObject("sferă orbită", sphereMesh, _orbitPivot);
        _sphere.Transform.Position = new Vector3(OrbitRadius, 0.7f, 0.0f);
        _sphere.Transform.Scale = new Vector3(0.7f);
        _sphere.Tint = new Color4(0.95f, 0.75f, 0.15f, 1.0f);
        _sphere.Spin = new Vector3(0.0f, 200.0f, 0.0f); // rotație proprie
    }

    private void ResetCar()
    {
        _carRoot.Transform.Position = new Vector3(0.0f, CarY, 0.0f);
        _carRoot.Transform.Rotation = Vector3.Zero;
        _headingDeg = 0.0f;
        _speed = 0.0f;
        _orbitPivot.Transform.Rotation = Vector3.Zero;
        foreach (var w in _wheels)
            w.Transform.Rotation = new Vector3(90.0f, 0.0f, 0.0f);
    }

    public override void Update(float dt, Input input)
    {
        if (input.WasPressed(Restart))
        {
            ResetCar();
            ConsolePanel.PrintMessage("mașina a fost resetată");
            return;
        }

        // 1. Input de conducere
        float throttle = input.Axis(Actions.Back, Actions.Forward); // W = +1 (înainte)
        float steer = input.Axis(Actions.Left, Actions.Right);      // D = +1 (dreapta)

        // 2. Viraj doar când te miști (ca o mașină reală)
        if (MathF.Abs(throttle) > 0.01f)
        {
            _headingDeg += steer * TurnSpeedDeg * dt * MathF.Sign(throttle);
        }
        // Normalizare unghi
        while (_headingDeg > 180f) _headingDeg -= 360f;
        while (_headingDeg < -180f) _headingDeg += 360f;

        // 3. Direcția de mers din heading (0° = +X)
        float rad = MathHelper.DegreesToRadians(_headingDeg);
        Vector3 forward = new(MathF.Cos(rad), 0.0f, MathF.Sin(rad));

        // 4. Deplasare
        _speed = throttle * MoveSpeed;
        Vector3 pos = _carRoot.Transform.Position;
        pos += forward * _speed * dt;

        // Limite teren
        pos.X = Math.Clamp(pos.X, PlayMin.X, PlayMax.X);
        pos.Z = Math.Clamp(pos.Z, PlayMin.Y, PlayMax.Y);
        pos.Y = CarY;

        _carRoot.Transform.Position = pos;
        // Orientăm mașina după heading (rotație pe Y)
        _carRoot.Transform.Rotation = new Vector3(0.0f, -_headingDeg, 0.0f);

        // 5. Roțile se rotesc în funcție de viteză
        float wheelSpin = -_speed * 90.0f; // grade / s
        foreach (var w in _wheels)
            w.Spin = new Vector3(0.0f, 0.0f, wheelSpin);

        // 6. Spin-uri (orbită + roți + sferă)
        base.Update(dt, input);
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe())
            yield return line;

        yield return $"Mașină         : pos {Fmt(_carRoot.Transform.Position)}  heading {_headingDeg:F1}°  viteză {_speed:F2}";
        yield return $"Sferă pe orbită: rază {OrbitRadius:F1}  unghi {_orbitPivot.Transform.Rotation.Y:F1}°";
        yield return $"Poziție sferă  : {Fmt(_sphere.WorldPosition)}";
    }

    private static string Fmt(Vector3 v) => $"({v.X:F2}, {v.Y:F2}, {v.Z:F2})";
}
