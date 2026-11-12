// Varianta B: evită obstacolele
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

public sealed class ObstacleRunnerScene : Scene
{
    // Acțiuni specifice scenei (apar în meniul H)
    private const string MoveLeft = "schimbă banda la stânga";
    private const string MoveRight = "schimbă banda la dreapta";
    private const string Jump = "salt peste obstacol";
    private const string Restart = "joc nou (după coliziune / reset)";

    // Configurația drumului cu 3 benzi (-1, 0, +1)
    private const float LaneWidth = 2.4f;
    private const float PlayerZ = 4.0f;
    private const float PlayerGroundY = 0.5f;
    private const float SpawnZ = -48.0f;
    private const float DespawnZ = 12.0f;

    // Fizica saltului
    private const float Gravity = 28.0f;
    private const float JumpSpeed = 10.5f;

    // Jucătorul (ierarhie părinte-copil) și mișcarea pe benzi (Tween)
    private SceneObject _playerRoot = null!;
    private SceneObject _playerHead = null!;
    private SceneObject _playerShadow = null!;
    private int _currentLane; // -1 = stânga, 0 = centru, 1 = dreapta
    private Tween _laneTween = null!;
    private float _playerY = PlayerGroundY;
    private float _verticalSpeed;

    // Obstacole active în scenă
    private sealed class Obstacle
    {
        public SceneObject Root = null!;
        public bool IsTall;
        public Vector3 HalfExtents; // jumătățile dimensiunilor pentru coliziunea AABB
    }

    private readonly List<Obstacle> _obstacles = new();
    private readonly List<SceneObject> _roadMarkers = new();
    private readonly Random _rng = new();

    private Mesh _cubeMesh = null!;
    private Mesh _sphereMesh = null!;

    // Starea jocului, viteza lumii și scorul
    private float _worldSpeed;
    private float _spawnTimer;
    private float _spawnInterval;
    private float _score;
    private float _bestScore;
    private bool _gameOver;

    public override string Name => "Varianta B – Evită obstacolele";

    public override string Status => _gameOver
        ? $"COLIZIUNE! Scor: {_score:F1} s | Record: {_bestScore:F1} s (apăsați R)"
        : $"Scor: {_score:F1} s | Record: {_bestScore:F1} s | Viteză: {_worldSpeed:F1}";

    public override IEnumerable<string> ActionNames => new[]
    {
        MoveLeft, MoveRight, Jump, Restart
    };

    public override void Load(Input input)
    {
        // Camera priveste de sus și din spate spre drum
        CameraPosition = new Vector3(0.0f, 5.2f, 10.8f);
        CameraTarget = new Vector3(0.0f, 1.0f, -6.0f);

        // Legarea acțiunilor (niciodată KeyboardState direct)
        input.Bind(MoveLeft, Keys.A, Keys.Left);
        input.Bind(MoveRight, Keys.D, Keys.Right);
        input.Bind(Jump, Keys.Space, Keys.W, Keys.Up);
        input.Bind(Restart, Keys.R);

        _cubeMesh = AddMesh(Shapes.ShadedCube());
        _sphereMesh = AddMesh(Shapes.ShadedSphere(0.5f, 24, 16));
        Mesh diskMesh = AddMesh(Shapes.ShadedCylinder(0.55f, 0.02f, 24));

        BuildRoad();
        BuildPlayer(diskMesh);

        ResetGame();
    }

    private void BuildRoad()
    {
        // Asfaltul (3 benzi)
        SceneObject road = AddObject("drum", _cubeMesh);
        road.Transform.Position = new Vector3(0.0f, -0.05f, -18.0f);
        road.Transform.Scale = new Vector3(LaneWidth * 3.2f, 0.1f, 68.0f);
        road.Tint = new Color4(0.18f, 0.20f, 0.24f, 1.0f);

        // Borduri laterale
        foreach (float side in new[] { -1.0f, 1.0f })
        {
            SceneObject border = AddObject("bordură", _cubeMesh);
            border.Transform.Position = new Vector3(side * (LaneWidth * 1.65f), 0.12f, -18.0f);
            border.Transform.Scale = new Vector3(0.25f, 0.25f, 68.0f);
            border.Tint = new Color4(0.85f, 0.35f, 0.25f, 1.0f);
        }

        // Bonus: marcaje discontinue între benzi care se mișcă odată cu lumea
        for (int i = 0; i < 14; i++)
        {
            float z = SpawnZ + i * 4.5f;
            foreach (float dividerX in new[] { -LaneWidth * 0.5f, LaneWidth * 0.5f })
            {
                SceneObject dash = AddObject("marcaj", _cubeMesh);
                dash.Transform.Position = new Vector3(dividerX, 0.005f, z);
                dash.Transform.Scale = new Vector3(0.12f, 0.01f, 2.0f);
                dash.Tint = new Color4(0.92f, 0.92f, 0.95f, 1.0f);
                _roadMarkers.Add(dash);
            }
        }
    }

    private void BuildPlayer(Mesh diskMesh)
    {
        // Părintele jucătorului (corpul)
        _playerRoot = AddObject("jucător_corp", _cubeMesh);
        _playerRoot.Transform.Position = new Vector3(0.0f, PlayerGroundY, PlayerZ);
        _playerRoot.Transform.Scale = new Vector3(0.9f, 1.0f, 0.9f);
        _playerRoot.Tint = new Color4(0.25f, 0.75f, 1.0f, 1.0f);

        // Copil 1: capul (moștenește poziția X, Y, Z de la _playerRoot și are rotație proprie)
        _playerHead = AddObject("jucător_cap", _sphereMesh, _playerRoot);
        _playerHead.Transform.Position = new Vector3(0.0f, 0.85f, 0.0f);
        _playerHead.Transform.Scale = new Vector3(0.75f, 0.75f, 0.75f);
        _playerHead.Tint = new Color4(1.0f, 0.85f, 0.3f, 1.0f);
        _playerHead.Spin = new Vector3(0.0f, 180.0f, 0.0f);

        // Copil 2: vizieră atașată capului (ierarhie pe 3 niveluri: corp -> cap -> vizieră)
        SceneObject visor = AddObject("jucător_vizieră", _cubeMesh, _playerHead);
        visor.Transform.Position = new Vector3(0.0f, 0.05f, -0.42f);
        visor.Transform.Scale = new Vector3(0.65f, 0.25f, 0.25f);
        visor.Tint = new Color4(0.1f, 0.15f, 0.25f, 1.0f);

        // Umbra de pe sol (urmărește X-ul jucătorului, dar rămâne la Y = 0.01)
        _playerShadow = AddObject("jucător_umbră", diskMesh);
        _playerShadow.Tint = new Color4(0.05f, 0.05f, 0.07f, 1.0f);

        // Tween pentru schimbarea lină a benzii (< 0.2 s, EaseOut)
        _laneTween = new Tween(Vector3.Zero, Vector3.Zero, 0.15f, Easing.EaseOut);
    }

    private void ResetGame()
    {
        // Ștergem obstacolele vechi din scenă
        foreach (Obstacle obs in _obstacles)
        {
            RemoveHierarchy(obs.Root);
        }
        _obstacles.Clear();

        _currentLane = 0;
        _laneTween = new Tween(Vector3.Zero, Vector3.Zero, 0.15f, Easing.EaseOut);
        _playerY = PlayerGroundY;
        _verticalSpeed = 0.0f;

        _playerRoot.Transform.Position = new Vector3(0.0f, _playerY, PlayerZ);
        _playerRoot.Tint = new Color4(0.25f, 0.75f, 1.0f, 1.0f);

        _worldSpeed = 14.0f;
        _spawnInterval = 1.35f;
        _spawnTimer = 0.4f;
        _score = 0.0f;
        _gameOver = false;
    }

    private void RemoveHierarchy(SceneObject parent)
    {
        // Eliminăm atât părintele cât și eventualii copii din lista de obiecte a scenei
        Objects.RemoveAll(o => o == parent || o.Parent == parent);
    }

    public override void Update(float dt, Input input)
    {
        if (input.WasPressed(Restart))
        {
            ResetGame();
            ConsolePanel.PrintMessage("joc nou pornit");
            return;
        }

        // La coliziune, animațiile jocului îngheață până la apăsarea tastei R
        if (_gameOver)
        {
            return;
        }

        // Actualizează rotațiile automate (Spin) cu timpul de joc dt
        base.Update(dt, input);

        // 1. Progresia dificultății: scorul, viteza lumii și frecvența obstacolelor cresc în timp
        _score += dt;
        _worldSpeed += 0.45f * dt;
        _spawnInterval = MathF.Max(0.55f, 1.35f - _score * 0.018f);

        // 2. Schimbarea benzii cu Tween + EaseOut (durată 0.15 s < 0.2 s)
        if (input.WasPressed(MoveLeft) && _currentLane > -1)
        {
            _currentLane--;
            _laneTween.GoTo(new Vector3(_currentLane * LaneWidth, 0.0f, 0.0f), 0.15f);
        }
        if (input.WasPressed(MoveRight) && _currentLane < 1)
        {
            _currentLane++;
            _laneTween.GoTo(new Vector3(_currentLane * LaneWidth, 0.0f, 0.0f), 0.15f);
        }
        _laneTween.Update(dt);

        // 3. Saltul cu gravitație (secțiunea 5.4)
        bool onGround = _playerY <= PlayerGroundY + 1e-4f;
        if (onGround && input.WasPressed(Jump))
        {
            _verticalSpeed = JumpSpeed;
        }

        if (!onGround || _verticalSpeed > 0.0f)
        {
            _verticalSpeed -= Gravity * dt;
            _playerY += _verticalSpeed * dt;
            if (_playerY <= PlayerGroundY)
            {
                _playerY = PlayerGroundY;
                _verticalSpeed = 0.0f;
            }
        }

        float currentX = _laneTween.Value.X;
        _playerRoot.Transform.Position = new Vector3(currentX, _playerY, PlayerZ);

        // Actualizarea umbrei în funcție de înălțimea saltului
        float jumpHeight = _playerY - PlayerGroundY;
        float shadowScale = 1.0f / (1.0f + 0.35f * jumpHeight);
        _playerShadow.Transform.Position = new Vector3(currentX, 0.01f, PlayerZ);
        _playerShadow.Transform.Scale = new Vector3(shadowScale, 1.0f, shadowScale);

        // 4. Bonus: deplasarea marcajelor de pe drum odată cu lumea
        float roadLength = DespawnZ - SpawnZ;
        foreach (SceneObject dash in _roadMarkers)
        {
            Vector3 p = dash.Transform.Position;
            p.Z += _worldSpeed * dt;
            if (p.Z > DespawnZ)
            {
                p.Z -= roadLength;
            }
            dash.Transform.Position = p;
        }

        // 5. Generarea obstacolelor noi
        _spawnTimer -= dt;
        if (_spawnTimer <= 0.0f)
        {
            SpawnWave();
            _spawnTimer = _spawnInterval;
        }

        // 6. Deplasarea obstacolelor și testarea coliziunilor AABB
        Vector3 playerCenter = _playerRoot.Transform.Position;
        // Cutia de coliziune a jucătorului (puțin permisivă pentru gameplay plăcut)
        Vector3 playerHalf = new(0.38f, 0.48f, 0.38f);

        for (int i = _obstacles.Count - 1; i >= 0; i--)
        {
            Obstacle obs = _obstacles[i];
            Vector3 pos = obs.Root.Transform.Position;
            pos.Z += _worldSpeed * dt;
            obs.Root.Transform.Position = pos;

            // Test AABB: suprapunere simultană pe X, Y și Z
            if (CheckAabbOverlap(playerCenter, playerHalf, pos, obs.HalfExtents))
            {
                _gameOver = true;
                if (_score > _bestScore)
                {
                    _bestScore = _score;
                }
                _playerRoot.Tint = new Color4(1.0f, 0.2f, 0.2f, 1.0f);
                ConsolePanel.PrintMessage($"Coliziune! Scor: {_score:F2} s (Record: {_bestScore:F2} s). Apăsați R.");
                break;
            }

            if (pos.Z > DespawnZ)
            {
                RemoveHierarchy(obs.Root);
                _obstacles.RemoveAt(i);
            }
        }
    }

    private void SpawnWave()
    {
        // Alegem 1 sau 2 benzi pe care punem obstacole (lăsăm mereu cel puțin o bandă liberă)
        int freeLane = _rng.Next(-1, 2);
        bool doubleObstacle = _score > 8.0f && _rng.NextDouble() < 0.45;
        int secondFreeLane = _rng.Next(-1, 2);

        for (int lane = -1; lane <= 1; lane++)
        {
            if (lane == freeLane) continue;
            if (!doubleObstacle && lane == secondFreeLane) continue;

            bool isTall = _rng.NextDouble() < 0.45;
            CreateObstacle(lane, isTall);
        }
    }

    private void CreateObstacle(int lane, bool isTall)
    {
        float x = lane * LaneWidth;
        float width = 1.55f;
        float depth = 1.0f;
        float height = isTall ? 2.5f : 0.85f;

        SceneObject root = AddObject(isTall ? "obstacol_înalt" : "obstacol_jos", _cubeMesh);
        root.Transform.Position = new Vector3(x, height * 0.5f, SpawnZ);
        root.Transform.Scale = new Vector3(width, height, depth);
        root.Tint = isTall
            ? new Color4(0.95f, 0.25f, 0.30f, 1.0f)   // Roșu: trebuie ocolit
            : new Color4(1.0f, 0.65f, 0.15f, 1.0f);   // Portocaliu: se poate sări peste el

        // Copil decorativ atașat obstacolului înalt (demonstrează suplimentar ierarhia)
        if (isTall)
        {
            SceneObject beacon = AddObject("baliză_obstacol", _sphereMesh, root);
            beacon.Transform.Position = new Vector3(0.0f, 0.6f, 0.0f);
            beacon.Transform.Scale = new Vector3(0.25f, 0.15f, 0.35f);
            beacon.Tint = new Color4(1.0f, 0.95f, 0.4f, 1.0f);
        }

        _obstacles.Add(new Obstacle
        {
            Root = root,
            IsTall = isTall,
            HalfExtents = new Vector3(width * 0.48f, height * 0.48f, depth * 0.48f)
        });
    }

    /// <summary>Două cutii AABB se suprapun dacă și numai dacă se suprapun pe toate cele 3 axe.</summary>
    private static bool CheckAabbOverlap(Vector3 centerA, Vector3 halfA, Vector3 centerB, Vector3 halfB)
    {
        bool overlapX = MathF.Abs(centerA.X - centerB.X) <= (halfA.X + halfB.X);
        bool overlapY = MathF.Abs(centerA.Y - centerB.Y) <= (halfA.Y + halfB.Y);
        bool overlapZ = MathF.Abs(centerA.Z - centerB.Z) <= (halfA.Z + halfB.Z);
        return overlapX && overlapY && overlapZ;
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;
        string laneName = _currentLane switch { -1 => "stânga", 0 => "centru", _ => "dreapta" };
        yield return $"Stare joc      : {(_gameOver ? "OPRIT (Coliziune)" : "În desfășurare")}";
        yield return $"Scor / Record  : {_score:F2} s / {_bestScore:F2} s";
        yield return $"Viteză lume    : {_worldSpeed:F2} u/s   (interval obstacole: {_spawnInterval:F2} s)";
        yield return $"Jucător        : banda {laneName} (x = {_playerRoot.Transform.Position.X:F2}), y = {_playerY:F2}";
        yield return $"Obstacole      : {_obstacles.Count} active pe drum";
    }
}