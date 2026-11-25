// Varianta B: evită obstacolele (tema laboratorului 6)
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// TEMA, varianta B: un drum cu trei benzi. Jucătorul stă pe loc (pe Z), iar obstacolele vin spre el.
///  - stânga / dreapta (APĂSARE) mută jucătorul pe banda vecină, lin: Tween cu EaseOut, 0,15 s;
///  - Space = salt cu gravitație (Euler semi-implicit, secțiunea 5.4);
///  - obstacole joase (se pot sări) și înalte (se ocolesc);
///  - viteza lumii și frecvența obstacolelor cresc în timp;
///  - coliziunea se testează cu cutii aliniate cu axele (AABB);
///  - la coliziune jocul se oprește; titlul arată scorul (timpul supraviețuit) și recordul; R = joc nou;
///  - bonus: marcajele benzilor și stâlpii de pe margine se deplasează odată cu lumea (senzația de viteză).
/// Ierarhii: jucătorul (rădăcină -> pivotul saltului -> corp -> vizor; umbra este copilul rădăcinii, deci
/// rămâne pe podea) și marcajele drumului (toate copiii unui singur pivot care se deplasează pe Z).
/// Toate animațiile folosesc doar dt (timpul de joc), deci P, [ ], N și B funcționează și aici.
/// </summary>
public sealed class ObstacleRunnerScene : Scene
{
    // Acțiunile scenei (afișate la H). Input-ul se citește doar prin ele, niciodată din KeyboardState.
    private const string LaneLeft = "runner: banda din stânga";
    private const string LaneRight = "runner: banda din dreapta";
    private const string RunJump = "runner: salt";
    private const string NewGame = "runner: joc nou (după coliziune)";

    // Drumul.
    private const float LaneWidth = 2.0f;                 // benzile sunt la x = -2, 0, +2
    private const float PlayerZ = 3.0f;                   // jucătorul stă pe loc, aici
    private const float SpawnZ = -45.0f;                  // obstacolele apar departe, în ceață
    private const float DespawnZ = 8.0f;                  // ... și dispar după ce trec de cameră
    private const float RoadStartZ = -46.0f, RoadEndZ = 7.0f;
    private const float MarkPeriod = 3.0f;                // distanța dintre două marcaje consecutive

    // Viteza lumii și frecvența obstacolelor (cresc în timp, până la o limită).
    private const float StartSpeed = 9.0f, MaxSpeed = 28.0f, SpeedGain = 0.4f;        // unități/s, unități/s²
    private const float StartInterval = 1.3f, MinInterval = 0.45f, IntervalDrop = 0.02f;

    // Jucătorul: banda se schimbă cu un Tween EaseOut, sub 0,2 s; saltul folosește gravitația.
    private const float LaneChangeTime = 0.15f;
    private const float JumpSpeed = 9.5f, Gravity = 28.0f; // înălțimea maximă v₀² / 2g ≈ 1,6 unități
    private const float BodySize = 0.8f;
    private const float HitboxHalf = 0.36f;               // puțin mai mică decât corpul: coliziuni „corecte”

    private enum ObstacleKind { Low, Tall }

    private sealed class Obstacle
    {
        public SceneObject Object = null!;
        public ObstacleKind Kind;
        public bool Active;
        public Vector3 Half;                              // jumătățile dimensiunilor cutiei (AABB)
    }

    private readonly List<Obstacle> _obstacles = new();
    private readonly Random _random = new();

    // Ierarhia jucătorului.
    private SceneObject _playerRoot = null!;   // x = banda (din Tween), pe podea
    private SceneObject _jumpPivot = null!;    // y = înălțimea saltului
    private SceneObject _body = null!;         // cubul, se înclină la schimbarea benzii
    private SceneObject _shadow = null!;       // copilul rădăcinii: rămâne pe podea
    private SceneObject _roadMarks = null!;    // pivotul marcajelor, deplasat pe Z cu viteza lumii

    private readonly Tween _laneTween = new(Vector3.Zero, Vector3.Zero, LaneChangeTime, Easing.EaseOut);
    private int _lane;                         // -1, 0, +1
    private float _height, _verticalSpeed;

    // Starea jocului.
    private bool _gameOver;
    private float _time;                       // timpul supraviețuit (scorul), în secunde de joc
    private float _record;
    private float _speed = StartSpeed;
    private float _spawnTimer = 1.0f;
    private int _passed;                       // obstacole lăsate în urmă

    public override string Name => "tema 6B: evită obstacolele";

    public override IEnumerable<string> ActionNames => new[] { LaneLeft, LaneRight, RunJump, NewGame };

    public override string Status => _gameOver
        ? $"COLIZIUNE! scor {_time:F1} s | record {_record:F1} s | R = joc nou"
        : $"scor {_time:F1} s | record {_record:F1} s | viteza {_speed:F1}";

    public override void Load(Input input)
    {
        CameraPosition = new Vector3(0.0f, 4.2f, 10.5f);
        CameraTarget = new Vector3(0.0f, 0.6f, -6.0f);

        input.Bind(LaneLeft, Keys.A, Keys.Left);
        input.Bind(LaneRight, Keys.D, Keys.Right);
        input.Bind(RunJump, Keys.Space, Keys.W, Keys.Up);
        input.Bind(NewGame, Keys.R);

        Mesh cube = AddMesh(Shapes.ShadedCube());
        Mesh disk = AddMesh(Shapes.ShadedCylinder(0.5f, 0.02f, 24));

        BuildRoad(cube);
        BuildPlayer(cube, disk);
        BuildObstaclePool(cube, 24);
    }

    // --- Construcția scenei -----------------------------------------------------------------------

    private void BuildRoad(Mesh cube)
    {
        float length = RoadEndZ - RoadStartZ;
        float middle = (RoadStartZ + RoadEndZ) / 2.0f;

        SceneObject road = AddObject("drum", cube);
        road.Transform.Position = new Vector3(0.0f, -0.05f, middle);
        road.Transform.Scale = new Vector3(3.0f * LaneWidth + 0.4f, 0.1f, length);
        road.Tint = new Color4(0.22f, 0.23f, 0.27f, 1.0f);

        foreach (float side in new[] { -1.0f, 1.0f })
        {
            SceneObject curb = AddObject("bordură", cube);
            curb.Transform.Position = new Vector3(side * (1.5f * LaneWidth + 0.35f), 0.1f, middle);
            curb.Transform.Scale = new Vector3(0.3f, 0.2f, length);
            curb.Tint = new Color4(0.75f, 0.75f, 0.8f, 1.0f);
        }

        // Bonus: marcajele (linii întrerupte între benzi) și stâlpii de pe margine sunt copiii unui singur
        // pivot. Pivotul înaintează cu viteza lumii și revine cu o perioadă (MarkPeriod) când a parcurs-o:
        // modelul se repetă, deci saltul înapoi nu se vede, iar marcajele par să curgă la nesfârșit.
        _roadMarks = AddObject("marcaje", null);
        for (float z = RoadStartZ + 1.0f; z < RoadEndZ - MarkPeriod; z += MarkPeriod)
        {
            foreach (float x in new[] { -0.5f * LaneWidth, 0.5f * LaneWidth })
            {
                SceneObject dash = AddObject("marcaj", cube, _roadMarks);
                dash.Transform.Position = new Vector3(x, 0.005f, z);
                dash.Transform.Scale = new Vector3(0.12f, 0.02f, 1.2f);
                dash.Tint = new Color4(0.95f, 0.9f, 0.55f, 1.0f);
            }
        }
        for (float z = RoadStartZ + 1.0f; z < RoadEndZ - 2.0f * MarkPeriod; z += 2.0f * MarkPeriod)
        {
            foreach (float side in new[] { -1.0f, 1.0f })
            {
                SceneObject post = AddObject("stâlp", cube, _roadMarks);
                post.Transform.Position = new Vector3(side * (1.5f * LaneWidth + 0.9f), 0.6f, z);
                post.Transform.Scale = new Vector3(0.15f, 1.2f, 0.15f);
                post.Tint = new Color4(0.4f, 0.75f, 1.0f, 1.0f);
            }
        }
    }

    private void BuildPlayer(Mesh cube, Mesh disk)
    {
        _playerRoot = AddObject("jucător", null);
        _playerRoot.Transform.Position = new Vector3(0.0f, 0.0f, PlayerZ);

        _shadow = AddObject("umbra", disk, _playerRoot);
        _shadow.Transform.Position = new Vector3(0.0f, 0.01f, 0.0f);
        _shadow.Tint = new Color4(0.05f, 0.05f, 0.08f, 1.0f);

        _jumpPivot = AddObject("pivotul saltului", null, _playerRoot);

        _body = AddObject("corp", cube, _jumpPivot);
        _body.Transform.Position = new Vector3(0.0f, BodySize / 2.0f, 0.0f);
        _body.Transform.Scale = new Vector3(BodySize);

        // Vizorul este copilul corpului: urmează și saltul, și înclinarea. Coordonatele sunt relative
        // la corp (cub cu latura 1 înainte de scalare), pe fața dinspre obstacole (-Z).
        SceneObject visor = AddObject("vizor", cube, _body);
        visor.Transform.Position = new Vector3(0.0f, 0.15f, -0.5f);
        visor.Transform.Scale = new Vector3(0.75f, 0.22f, 0.06f);
        visor.Tint = new Color4(0.15f, 0.2f, 0.3f, 1.0f);

        ResetPlayer();
    }

    private void BuildObstaclePool(Mesh cube, int count)
    {
        // Obstacolele se refolosesc (pool): nu creăm și nu ștergem obiecte în timpul jocului.
        for (int i = 0; i < count; i++)
        {
            SceneObject obj = AddObject($"obstacol {i}", cube);
            obj.Visible = false;
            _obstacles.Add(new Obstacle { Object = obj });
        }
    }

    // --- Actualizarea, la fiecare cadru ----------------------------------------------------------

    public override void Update(float dt, Input input)
    {
        base.Update(dt, input);

        if (_gameOver)
        {
            if (input.WasPressed(NewGame))
            {
                StartNewGame();
            }
            return;   // lumea stă pe loc după coliziune
        }

        UpdatePlayer(dt, input);
        UpdateWorld(dt);
        CheckCollisions(dt);
    }

    private void UpdatePlayer(float dt, Input input)
    {
        // Schimbarea benzii la APĂSARE (nu la ținere): o apăsare = o bandă. GoTo pornește din poziția
        // curentă, deci și o schimbare în timpul alteia este lină.
        int lane = _lane;
        if (input.WasPressed(LaneLeft)) lane = Math.Max(lane - 1, -1);
        if (input.WasPressed(LaneRight)) lane = Math.Min(lane + 1, 1);
        if (lane != _lane)
        {
            _lane = lane;
            _laneTween.GoTo(new Vector3(_lane * LaneWidth, 0.0f, 0.0f), LaneChangeTime);
        }
        _laneTween.Update(dt);
        float x = _laneTween.Value.X;

        // Saltul: doar de pe sol, la apăsare; apoi gravitația (întâi viteza, apoi poziția).
        bool onGround = _height <= 0.0f && _verticalSpeed <= 0.0f;
        if (onGround && input.WasPressed(RunJump))
        {
            _verticalSpeed = JumpSpeed;
        }
        if (!onGround || _verticalSpeed > 0.0f)
        {
            _verticalSpeed -= Gravity * dt;
            _height += _verticalSpeed * dt;
            if (_height <= 0.0f)
            {
                _height = 0.0f;
                _verticalSpeed = 0.0f;
            }
        }

        // Ierarhia face restul: rădăcina duce banda, pivotul saltului înălțimea.
        _playerRoot.Transform.Position = new Vector3(x, 0.0f, PlayerZ);
        _jumpPivot.Transform.Position = new Vector3(0.0f, _height, 0.0f);

        // Corpul se înclină în direcția mișcării, cât timp mai are drum până la bandă (revine lin la 0).
        float remaining = _lane * LaneWidth - x;
        _body.Transform.Rotation = new Vector3(0.0f, 0.0f, -12.0f * remaining);

        float shadowSize = 1.0f / (1.0f + 0.3f * _height);
        _shadow.Transform.Scale = new Vector3(shadowSize, 1.0f, shadowSize);
    }

    private void UpdateWorld(float dt)
    {
        _time += dt;
        _speed = MathF.Min(MaxSpeed, StartSpeed + SpeedGain * _time);
        float step = _speed * dt;

        // Bonus: marcajele curg cu viteza lumii; % MarkPeriod le aduce înapoi fără salt vizibil.
        Vector3 marks = _roadMarks.Transform.Position;
        marks.Z = (marks.Z + step) % MarkPeriod;
        _roadMarks.Transform.Position = marks;

        foreach (Obstacle o in _obstacles)
        {
            if (!o.Active) continue;
            Vector3 p = o.Object.Transform.Position;
            p.Z += step;
            o.Object.Transform.Position = p;
            if (p.Z - o.Half.Z > DespawnZ)
            {
                o.Active = false;
                o.Object.Visible = false;
                _passed++;
            }
        }

        // Obstacolele apar tot mai des: intervalul scade cu timpul, până la MinInterval.
        _spawnTimer -= dt;
        if (_spawnTimer <= 0.0f)
        {
            SpawnWave();
            _spawnTimer = MathF.Max(MinInterval, StartInterval - IntervalDrop * _time);
        }
    }

    private void SpawnWave()
    {
        int first = _random.Next(-1, 2);
        Spawn(first, RandomKind());

        // După 15 s apar uneori două obstacole deodată, pe benzi diferite. Cel puțin o bandă rămâne
        // liberă, deci valul are întotdeauna o rezolvare (ocolire sau salt).
        if (_time > 15.0f && _random.NextDouble() < 0.35)
        {
            int second = first + (_random.Next(2) == 0 ? 1 : 2);
            second = (second + 1) % 3 - 1;   // din nou în -1 .. 1, diferită de first
            Spawn(second, RandomKind());
        }
    }

    private ObstacleKind RandomKind() => _random.NextDouble() < 0.55 ? ObstacleKind.Low : ObstacleKind.Tall;

    private void Spawn(int lane, ObstacleKind kind)
    {
        Obstacle? o = _obstacles.FirstOrDefault(x => !x.Active);
        if (o == null) return;   // pool plin (nu se întâmplă la vitezele folosite)

        // Joase: se pot sări (0,6 înălțime < saltul de ≈ 1,6). Înalte: trebuie ocolite.
        Vector3 size = kind == ObstacleKind.Low ? new Vector3(1.5f, 0.6f, 0.6f) : new Vector3(1.4f, 2.4f, 0.8f);
        o.Kind = kind;
        o.Active = true;
        o.Half = size / 2.0f;
        o.Object.Visible = true;
        o.Object.Transform.Scale = size;
        o.Object.Transform.Position = new Vector3(lane * LaneWidth, size.Y / 2.0f, SpawnZ);
        o.Object.Tint = kind == ObstacleKind.Low
            ? new Color4(1.0f, 0.6f, 0.15f, 1.0f)
            : new Color4(0.85f, 0.2f, 0.35f, 1.0f);
    }

    // --- Coliziunea: cutii aliniate cu axele (AABB) ---------------------------------------------

    private void CheckCollisions(float dt)
    {
        Vector3 center = new(_playerRoot.Transform.Position.X, _height + BodySize / 2.0f, PlayerZ);
        Vector3 half = new(HitboxHalf);
        float step = _speed * dt;

        foreach (Obstacle o in _obstacles)
        {
            if (!o.Active) continue;
            Vector3 c = o.Object.Transform.Position;

            // Pe Z folosim tot drumul parcurs în acest cadru (de la c.Z - step la c.Z): la viteză mare,
            // după o blocare (B) pasul poate fi mai mare decât grosimea obstacolului, iar o testare doar
            // în poziția finală l-ar lăsa să „treacă prin” jucător.
            float minZ = c.Z - step - o.Half.Z, maxZ = c.Z + o.Half.Z;

            bool overlapX = MathF.Abs(center.X - c.X) < half.X + o.Half.X;
            bool overlapY = MathF.Abs(center.Y - c.Y) < half.Y + o.Half.Y;
            bool overlapZ = center.Z + half.Z > minZ && center.Z - half.Z < maxZ;

            // Două cutii se suprapun doar dacă se suprapun pe toate cele trei axe.
            if (overlapX && overlapY && overlapZ)
            {
                GameOver(o);
                return;
            }
        }
    }

    private void GameOver(Obstacle hit)
    {
        _gameOver = true;
        bool newRecord = _time > _record;
        _record = MathF.Max(_record, _time);
        _body.Tint = new Color4(1.0f, 0.25f, 0.25f, 1.0f);
        hit.Object.Tint = new Color4(1.0f, 1.0f, 1.0f, 1.0f);
        ConsolePanel.PrintMessage($"coliziune cu un obstacol {(hit.Kind == ObstacleKind.Low ? "jos" : "înalt")}: " +
                                  $"scor {_time:F1} s{(newRecord ? " (record nou!)" : "")}, R = joc nou");
    }

    private void StartNewGame()
    {
        foreach (Obstacle o in _obstacles)
        {
            o.Active = false;
            o.Object.Visible = false;
        }
        _gameOver = false;
        _time = 0.0f;
        _passed = 0;
        _speed = StartSpeed;
        _spawnTimer = 1.0f;
        ResetPlayer();
        ConsolePanel.PrintMessage("joc nou");
    }

    private void ResetPlayer()
    {
        _lane = 0;
        _laneTween.From = Vector3.Zero;
        _laneTween.To = Vector3.Zero;
        _height = 0.0f;
        _verticalSpeed = 0.0f;
        _playerRoot.Transform.Position = new Vector3(0.0f, 0.0f, PlayerZ);
        _jumpPivot.Transform.Position = Vector3.Zero;
        _body.Transform.Rotation = Vector3.Zero;
        _body.Tint = new Color4(0.35f, 0.85f, 1.0f, 1.0f);
        _shadow.Transform.Scale = Vector3.One;
    }

    // --- Tasta I -----------------------------------------------------------------------------------

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;
        yield return $"Stare          : {(_gameOver ? "oprit (coliziune)" : "în joc")}   scor {_time:F2} s   record {_record:F2} s";
        yield return $"Lumea          : viteza {_speed:F2} u/s   interval obstacole " +
                     $"{MathF.Max(MinInterval, StartInterval - IntervalDrop * _time):F2} s   " +
                     $"active {_obstacles.Count(o => o.Active)}   depășite {_passed}";
        yield return $"Jucător        : banda {_lane switch { -1 => "stânga", 0 => "mijloc", _ => "dreapta" }}   " +
                     $"x = {_playerRoot.Transform.Position.X:F2}   înălțime {_height:F2}   v_y = {_verticalSpeed:F2}";
    }
}
