using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// Camera FPS (first-person): orientarea vine din două unghiuri Euler, yaw și pitch,
/// controlate cu mouse-ul; deplasarea se face cu W A S D.
/// În modul "mers" (implicit) camera rămâne la înălțimea ochilor; în modul "zbor liber"
/// (free-fly, tasta G) se deplasează pe direcția privirii.
/// </summary>
public class FpsCamera : Camera
{
    // --- Starea camerei ---------------------------------------------------------

    private Vector3 _position;
    public override Vector3 Position => _position;

    /// <summary>Girația (yaw), în grade: rotația stânga-dreapta, în jurul axei Y. -90° = privire spre -Z.</summary>
    public float Yaw { get; private set; }

    /// <summary>Tangajul (pitch), în grade: rotația sus-jos. Limitat la ±MaxPitch.</summary>
    public float Pitch { get; private set; }

    /// <summary>Zbor liber (true) sau mers pe sol (false). Comutat cu tasta G (facultativ, TODO 5.7).</summary>
    public bool FreeFly
    {
        get => _freeFly;
        set
        {
            // P6: la revenirea din zbor liber în modul mers, camera „aterizează” la înălțimea ochilor (1,70).
            if (_freeFly && !value)
            {
                _position.Y = StartPosition.Y;
            }
            _freeFly = value;
        }
    }
    private bool _freeFly;

    // Baza camerei: trei vectori unitate, perpendiculari doi câte doi (secțiunea 5).
    private Vector3 _front = -Vector3.UnitZ;
    private Vector3 _right = Vector3.UnitX;
    private Vector3 _up = Vector3.UnitY;

    public override Vector3 Front => _front;

    // --- Parametri ----------------------------------------------------------------

    public const float MaxPitch = 89.0f;               // grade; de ce nu 90, vezi secțiunea 5.3
    public float MoveSpeed = 3.0f;                     // unități de lume pe secundă
    public float SprintFactor = 2.5f;                  // Shift ținut apăsat
    public float MouseSensitivity = 0.1f;              // grade pe pixel
    public float FovStep = 2.0f;                       // grade pentru un pas al rotiței

    private static readonly Vector3 WorldUp = Vector3.UnitY;
    private static readonly Vector3 StartPosition = new(0.0f, 1.7f, 6.0f);   // 1,7 m = înălțimea ochilor
    private const float StartYaw = -90.0f;
    private const float StartPitch = 0.0f;

    public FpsCamera()
    {
        Reset();
    }

    public override string Name => FreeFly ? "FPS – zbor liber" : "FPS – mers";

    /// <summary>Matricea view: camera aflată în Position privește spre punctul Position + Front.</summary>
    public override Matrix4 ViewMatrix =>
        Matrix4.LookAt(_position, _position + _front, _up);

    public override void Update(KeyboardState keyboard, MouseState mouse, Vector2 mouseDelta, float dt)
    {
        // --- 1. Orientarea: mouse-ul modifică yaw și pitch -------------------------

        // P4 (TODO 5.5 a): deplasarea pe orizontală modifică yaw, cea pe verticală modifică pitch (grade pe pixel).
        //     Pe ecran Y crește în jos, iar pitch pozitiv înseamnă privire în sus, deci pentru pitch semnul se inversează.
        Yaw += mouseDelta.X * MouseSensitivity;
        Pitch -= mouseDelta.Y * MouseSensitivity;

        // P4: pitch-ul se limitează la ±89° (secțiunea 5.3). La ±90°, Front devine paralel cu WorldUp, produsul
        //     vectorial Front × WorldUp devine (aproape) nul și nu mai poate fi normalizat: Right și Up devin
        //     nedefinite / instabile, iar imaginea se răstoarnă sau se rotește haotic (experimentul cu MaxPitch = 90).
        Pitch = Math.Clamp(Pitch, -MaxPitch, MaxPitch);

        UpdateVectors();

        // --- 2. FOV: rotița mouse-ului --------------------------------------------

        // P1 (TODO 5.1 c): rotița în sus (ScrollDelta.Y > 0) micșorează FOV-ul (efect de zoom), rotița în jos îl mărește.
        //     ChangeFov îl păstrează între MinFov (10°) și MaxFov (120°).
        if (mouse.ScrollDelta.Y != 0.0f)
        {
            ChangeFov(-mouse.ScrollDelta.Y * FovStep);
        }


        // --- 3. Deplasarea: W A S D ---------------------------------------------------

        // TODO 5.6: direcția "înainte" (în modul mers: proiecția lui Front pe planul orizontal),
        //           apoi deplasarea cu viteză constantă, independentă de numărul de cadre pe secundă.
        //           Pași: (1) forward; (2) vectorul "move" = suma direcțiilor tastelor apăsate
        //           (W +forward, S -forward, D +_right, A -_right); (3) normalizați "move";
        //           (4) _position += move * viteză * dt (viteza crește de SprintFactor ori cu Shift stânga).

        // P5 (1): în modul mers, „înainte” este Front proiectat pe planul orizontal (y = 0, apoi normalizat):
        //     privind în jos și apăsând W, mergeți înainte, nu intrați în podea, iar înălțimea rămâne 1,70.
        //     Pitch-ul fiind limitat la ±89°, componenta orizontală a lui Front nu ajunge niciodată la zero.
        // P6 (TODO 5.7, facultativ): în zbor liber, „înainte” este chiar Front (se urcă / coboară privind în sus / jos).
        Vector3 forward = FreeFly ? _front : Vector3.Normalize(new Vector3(_front.X, 0.0f, _front.Z));

        // P5 (2): suma direcțiilor tastelor apăsate. _right este deja orizontal (Front × WorldUp).
        Vector3 move = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) move += forward;
        if (keyboard.IsKeyDown(Keys.S)) move -= forward;
        if (keyboard.IsKeyDown(Keys.D)) move += _right;
        if (keyboard.IsKeyDown(Keys.A)) move -= _right;

        // P6: în zbor liber, Space urcă și Ctrl stânga coboară, pe axa Y a lumii (WorldUp), nu pe Up-ul camerei.
        if (FreeFly)
        {
            if (keyboard.IsKeyDown(Keys.Space))       move += WorldUp;
            if (keyboard.IsKeyDown(Keys.LeftControl)) move -= WorldUp;
        }

        // P5 (3): normalizarea, ca pe diagonală (W + D) viteza să nu fie de √2 ori mai mare.
        if (move.LengthSquared > 0.0f)
        {
            move = Vector3.Normalize(move);

            // P5 (4): viteză × dt: aceeași distanță pe secundă la 30 sau la 300 de cadre (VSync pornit / oprit, tasta L).
            float speed = MoveSpeed * (keyboard.IsKeyDown(Keys.LeftShift) ? SprintFactor : 1.0f);
            _position += move * speed * dt;
        }

        // TODO 5.7 (facultativ): în zbor liber (FreeFly), "forward" este chiar _front,
        //           iar Space urcă și Ctrl stânga coboară (pe axa Y a lumii, WorldUp).
    }

    /// <summary>
    /// Recalculează baza camerei din yaw și pitch (unghiuri Euler -> vector de direcție).
    /// </summary>
    private void UpdateVectors()
    {
        // P4 (TODO 5.5 b): front = (cos yaw · cos pitch, sin pitch, sin yaw · cos pitch), cu unghiurile în radiani.
        //     La yaw = −90°, pitch = 0° rezultă (0, 0, −1): camera privește spre −Z, ca la pornire.
        float yaw = MathHelper.DegreesToRadians(Yaw);
        float pitch = MathHelper.DegreesToRadians(Pitch);

        _front = Vector3.Normalize(new Vector3(
            MathF.Cos(yaw) * MathF.Cos(pitch),
            MathF.Sin(pitch),
            MathF.Sin(yaw) * MathF.Cos(pitch)));

        // P4: baza camerei prin produse vectoriale (secțiunea 5.1): right = front × worldUp, up = right × front.
        _right = Vector3.Normalize(Vector3.Cross(_front, WorldUp));
        _up = Vector3.Normalize(Vector3.Cross(_right, _front));
    }

    /// <summary>
    /// Bonus (comutare fără „salt”, orbită → FPS): așază camera în <paramref name="position"/>, privind pe direcția
    /// <paramref name="front"/>. Yaw și pitch se calculează cu formulele inverse ale lui front (secțiunea 5.2):
    /// pitch = asin(front.y), yaw = atan2(front.z, front.x).
    /// </summary>
    public void PlaceAt(Vector3 position, Vector3 front)
    {
        front = Vector3.Normalize(front);
        _position = position;
        Pitch = Math.Clamp(MathHelper.RadiansToDegrees(MathF.Asin(front.Y)), -MaxPitch, MaxPitch);
        Yaw = MathHelper.RadiansToDegrees(MathF.Atan2(front.Z, front.X));
        UpdateVectors();
    }

    public override void Reset()
    {
        _position = StartPosition;
        Yaw = StartYaw;
        Pitch = StartPitch;
        FieldOfView = 60.0f;
        UpdateVectors();
    }

    public override IEnumerable<string> Describe()
    {
        yield return $"Poziție        : ({_position.X,6:F2}, {_position.Y,6:F2}, {_position.Z,6:F2})";
        yield return $"Yaw / pitch    : {NormalizeAngle(Yaw),7:F1}° / {Pitch,6:F1}°";
        yield return $"Front          : ({_front.X,6:F2}, {_front.Y,6:F2}, {_front.Z,6:F2})";
        yield return $"Right          : ({_right.X,6:F2}, {_right.Y,6:F2}, {_right.Z,6:F2})";
        yield return $"Up             : ({_up.X,6:F2}, {_up.Y,6:F2}, {_up.Z,6:F2})";
    }

    /// <summary>Aduce un unghi în intervalul (-180°, 180°] (doar pentru afișare).</summary>
    private static float NormalizeAngle(float degrees)
    {
        degrees %= 360.0f;
        if (degrees > 180.0f) degrees -= 360.0f;
        if (degrees <= -180.0f) degrees += 360.0f;
        return degrees;
    }
}
