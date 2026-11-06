using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// TEMA: camera orbită. Privește mereu spre un punct (ținta) și se mișcă pe o sferă în jurul lui,
/// ca în editoarele 3D. Poziția pe sferă este dată de distanță (raza) și de două unghiuri:
/// azimutul (în jurul axei Y, ca yaw) și elevația (deasupra planului orizontal, ca pitch).
/// </summary>
public class OrbitCamera : Camera
{
    // --- Starea camerei ---------------------------------------------------------

    /// <summary>Punctul spre care privește camera, în coordonate de lume.</summary>
    public Vector3 Target { get; private set; }

    /// <summary>Distanța de la țintă la cameră (raza sferei pe care se mișcă).</summary>
    public float Distance { get; private set; }

    /// <summary>Azimutul, în grade: unghiul în jurul axei Y. 90° = camera pe axa +Z față de țintă.</summary>
    public float Azimuth { get; private set; }

    /// <summary>Elevația, în grade: unghiul deasupra planului orizontal. Limitată la ±MaxElevation.</summary>
    public float Elevation { get; private set; }

    // --- Parametri ----------------------------------------------------------------

    // Elevația se limitează la ±89°, din același motiv ca pitch-ul camerei FPS (secțiunea 5.3): la ±90° direcția
    // privirii ar fi paralelă cu (0, 1, 0), iar LookAt n-ar mai avea o direcție „sus” validă (imaginea s-ar răsturna).
    public const float MaxElevation = 89.0f;   // grade
    public const float MinDistance = 1.5f;     // nu intrăm în țintă (și nici în obiectul din jurul ei)
    public const float MaxDistance = 40.0f;    // rămânem în interiorul planului far (100)

    public float MouseSensitivity = 0.3f;      // grade pe pixel, la tragerea cu clic stânga
    public float KeyRotateSpeed = 90.0f;       // grade pe secundă, cu săgețile
    public float ZoomFactor = 1.1f;            // un pas al rotiței = distanța ÷ / × 1,1
    public float TargetMoveSpeed = 3.0f;       // bonus: unități de lume pe secundă (W A S D mută ținta)

    private static readonly Vector3 WorldUp = Vector3.UnitY;

    // Starea inițială: privim cubul din centrul scenei, ușor de deasupra, din aceeași parte ca FPS-ul (dinspre +Z).
    private static readonly Vector3 StartTarget = new(0.0f, 1.2f, -2.0f);
    private const float StartDistance = 8.0f;
    private const float StartAzimuth = 90.0f;
    private const float StartElevation = 20.0f;

    public OrbitCamera()
    {
        Reset();
    }

    public override string Name => "orbită";

    /// <summary>
    /// Poziția = țintă + distanță · (cos el · cos az, sin el, cos el · sin az):
    /// aceeași structură ca formula lui front din camera FPS (azimutul ține locul lui yaw, elevația a lui pitch).
    /// </summary>
    public override Vector3 Position
    {
        get
        {
            float az = MathHelper.DegreesToRadians(Azimuth);
            float el = MathHelper.DegreesToRadians(Elevation);
            var offset = new Vector3(MathF.Cos(el) * MathF.Cos(az), MathF.Sin(el), MathF.Cos(el) * MathF.Sin(az));
            return Target + Distance * offset;
        }
    }

    /// <summary>Camera privește mereu ținta: direcția este de la cameră spre țintă.</summary>
    public override Vector3 Front => Vector3.Normalize(Target - Position);

    /// <summary>Matricea view: LookAt(poziție, țintă, (0, 1, 0)).</summary>
    public override Matrix4 ViewMatrix => Matrix4.LookAt(Position, Target, WorldUp);

    public override void Update(KeyboardState keyboard, MouseState mouse, Vector2 mouseDelta, float dt)
    {
        // --- 1. Rotirea cu mouse-ul: clic stânga ținut + mișcarea mouse-ului ----------
        // Ca la „apucarea” scenei: tragerea spre dreapta rotește scena spre dreapta (camera ocolește ținta spre
        // stânga), tragerea în jos ridică camera (scena se vede mai de sus).
        if (mouse.IsButtonDown(MouseButton.Left))
        {
            Azimuth += mouseDelta.X * MouseSensitivity;
            Elevation += mouseDelta.Y * MouseSensitivity;
        }

        // --- 2. Rotirea de la tastatură: săgețile -----------------------------------------
        // Săgeata dreapta mută camera spre dreapta în jurul țintei, săgeata sus o ridică.
        float step = KeyRotateSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Right)) Azimuth -= step;
        if (keyboard.IsKeyDown(Keys.Left))  Azimuth += step;
        if (keyboard.IsKeyDown(Keys.Up))    Elevation += step;
        if (keyboard.IsKeyDown(Keys.Down))  Elevation -= step;

        Azimuth %= 360.0f;   // doar ca valorile afișate să rămână mici; cos / sin nu se schimbă
        Elevation = Math.Clamp(Elevation, -MaxElevation, MaxElevation);

        // --- Bonus: W A S D mută ținta în planul orizontal ---------------------------------
        // „Înainte” = direcția privirii proiectată pe orizontală (y = 0, normalizată), ca la mersul camerei FPS;
        // „dreapta” = înainte × (0, 1, 0). Mișcarea este normalizată (pe diagonală nu e mai rapidă) și ×dt.
        // Camera se mută împreună cu ținta (distanța și unghiurile rămân aceleași), deci imaginea „alunecă”.
        Vector3 forward = Front;
        forward.Y = 0.0f;
        if (forward.LengthSquared > 1e-8f)
        {
            forward = Vector3.Normalize(forward);
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, WorldUp));

            Vector3 move = Vector3.Zero;
            if (keyboard.IsKeyDown(Keys.W)) move += forward;
            if (keyboard.IsKeyDown(Keys.S)) move -= forward;
            if (keyboard.IsKeyDown(Keys.D)) move += right;
            if (keyboard.IsKeyDown(Keys.A)) move -= right;

            if (move.LengthSquared > 0.0f)
            {
                float speed = TargetMoveSpeed * (keyboard.IsKeyDown(Keys.LeftShift) ? 2.5f : 1.0f);
                Target += Vector3.Normalize(move) * speed * dt;
            }
        }

        // --- 3. Rotița schimbă DISTANȚA (nu FOV-ul) ------------------------------------------
        // Prin înmulțire (ca zoom-ul din laboratorul 4): rotița în sus apropie camera, în jos o depărtează,
        // iar pașii sunt la fel de „mari” vizual și aproape, și departe.
        if (mouse.ScrollDelta.Y != 0.0f)
        {
            Distance *= MathF.Pow(ZoomFactor, -mouse.ScrollDelta.Y);
            Distance = Math.Clamp(Distance, MinDistance, MaxDistance);
        }
    }

    /// <summary>
    /// Bonus (comutare fără „salt”, FPS → orbită): camera orbită preia exact imaginea unei camere aflate în
    /// <paramref name="eye"/> și care privește pe direcția <paramref name="front"/>. Ținta devine punctul aflat la
    /// <paramref name="targetDistance"/> în fața ei, iar unghiurile se calculează din vectorul unitar țintă → cameră,
    /// o = −front: elevația = asin(o.y), azimutul = atan2(o.z, o.x).
    /// Verificare: țintă + distanță · o = eye + d · front − d · front = eye, deci poziția nu se schimbă.
    /// </summary>
    public void LookFrom(Vector3 eye, Vector3 front, float targetDistance)
    {
        Vector3 o = -Vector3.Normalize(front);
        Target = eye - targetDistance * o;
        Distance = targetDistance;
        Elevation = Math.Clamp(MathHelper.RadiansToDegrees(MathF.Asin(o.Y)), -MaxElevation, MaxElevation);
        Azimuth = MathHelper.RadiansToDegrees(MathF.Atan2(o.Z, o.X));
    }

    public override void Reset()
    {
        Target = StartTarget;
        Distance = StartDistance;
        Azimuth = StartAzimuth;
        Elevation = StartElevation;
        FieldOfView = 60.0f;
    }

    /// <summary>Starea camerei orbită, afișată în consolă la tasta I.</summary>
    public override IEnumerable<string> Describe()
    {
        Vector3 p = Position;
        yield return $"Țintă          : ({Target.X,6:F2}, {Target.Y,6:F2}, {Target.Z,6:F2})";
        yield return $"Distanță       : {Distance,6:F2}   (între {MinDistance} și {MaxDistance})";
        yield return $"Azimut / elev. : {Azimuth,7:F1}° / {Elevation,6:F1}°";
        yield return $"Poziție        : ({p.X,6:F2}, {p.Y,6:F2}, {p.Z,6:F2})";
    }
}
