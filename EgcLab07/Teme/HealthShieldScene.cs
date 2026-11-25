// Varianta B: bara de viață și scutul (tema laboratorului 7)
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

/// <summary>
/// TEMA, varianta B: bara de viață și scutul.
///  - bara de viață (Shaders/healthbar.frag), desenată în întregime de shader pe un Quad: dreptunghi cu
///    colțuri rotunjite (distanță), uSegments segmente, culoare verde -> galben -> roșu după valoare,
///    pulsează sub 25%; se golește / umple lin (Tween), iar porțiunea pierdută rămâne câteva clipe ca o urmă
///    deschisă la culoare, apoi dispare și ea (al doilea Tween, pornit după o pauză);
///  - la lovitură bara clipește: uFlash = 1, apoi flash *= exp(-k · dt) (același rezultat la orice FPS);
///  - scutul (Shaders/shield.vert + shield.frag): o sferă transparentă cu benzi de energie animate, pornită /
///    oprită lin cu Space (Tween pe uniforma uStrength); cu scutul pornit, loviturile fac mai puțin rău, iar
///    scutul clipește;
///  - bonus: bara stă deasupra personajului și îl urmează (copiii aceluiași pivot), iar personajul se
///    plimbă stânga-dreapta (Tween dus-întors), pornit / oprit cu M.
/// C# calculează doar valori (viață, urmă, clipire, tăria scutului) și le trimite ca uniforme.
/// Toate animațiile folosesc timpul de joc: dt în C#, uTime în shadere.
/// </summary>
public sealed class HealthShieldScene : Scene
{
    // Acțiunile scenei (afișate la H). Săgețile, Backspace, R și U sunt deja folosite de Game.
    private const string Hit = "tema: lovitură (-20% viață)";
    private const string Heal = "tema: vindecare (+15% viață)";
    private const string ToggleShield = "tema: scut pornit / oprit";
    private const string ToggleWalk = "tema: personajul se plimbă / stă";

    private const float HitDamage = 0.20f, HealAmount = 0.15f;
    private const float ShieldReduction = 0.7f;   // cu scutul pornit complet, lovitura face doar 30% din rău
    private const float ValueTime = 0.35f;        // cât durează golirea / umplerea barei
    private const float TrailDelay = 0.6f;        // cât rămâne urma pe loc după lovitură
    private const float TrailTime = 0.5f;         // ... și în cât timp dispare apoi
    private const float FlashDecay = 7.0f;        // k din flash *= exp(-k · dt)
    private const float ShieldTime = 0.5f;        // pornirea / oprirea scutului
    private const float CriticalLevel = 0.25f;

    private static readonly Vector2 BarSize = new(2.4f, 0.34f);

    private Material _bar = null!, _shield = null!;
    private SceneObject _root = null!, _body = null!;

    // Valorile animate (Tween-uri pe un singur float, trimise ca uniforme).
    private Tween _value = null!;      // viața afișată (uValue)
    private Tween _trail = null!;      // capătul urmei (uTrail)
    private Tween _strength = null!;   // tăria scutului (uStrength)
    private Tween _walk = null!;       // poziția personajului (dus-întors)

    private float _health = 1.0f;      // viața "logică", ținta tween-ului _value
    private float _trailWait;          // cât mai stă urma pe loc
    private float _barFlash, _shieldFlash;
    private bool _shieldOn, _walking = true;
    private int _hits;

    public override string Name => "tema 7B: bara de viață și scutul";

    public override IEnumerable<string> ActionNames => new[] { Hit, Heal, ToggleShield, ToggleWalk };

    public override string Status =>
        $"viață {_health:P0}{(_health <= 0.0f ? " (KO)" : "")} | scut {(_shieldOn ? "pornit" : "oprit")}";

    protected override void Load(Input input)
    {
        CameraPosition = new Vector3(0.0f, 2.3f, 7.0f);
        CameraTarget = new Vector3(0.0f, 1.2f, 0.0f);

        input.Bind(Hit, Keys.X);
        input.Bind(Heal, Keys.V);
        input.Bind(ToggleShield, Keys.Space);
        input.Bind(ToggleWalk, Keys.M);

        Mesh grid = AddMesh(Shapes.Grid(10, 1.0f, new Color4(0.30f, 0.33f, 0.38f, 1.0f)));
        Mesh quad = AddMesh(Shapes.Quad(BarSize.X, BarSize.Y));
        Mesh sphere = AddMesh(Shapes.ShadedSphere(1.0f, 48, 24));
        Mesh cube = AddMesh(Shapes.ShadedCube());

        // --- Materialele: 10 + 5 uniforme proprii, de tipurile float, int, bool, vec2, vec3 ------------
        // Param(...) = reglabil cu săgețile (↑ ↓ alege, ← → schimbă).
        _bar = AddMaterial("bară", "procedural.vert", "healthbar.frag")
            .Param("uSegments", 1, 20, 10)
            .Param("uCorner", 0.001f, 0.17f, 0.12f);
        _bar.Transparent = true;   // în afara dreptunghiului rotunjit alfa este 0
        _bar.Uniforms.Set("uBarSize", BarSize);
        _bar.Uniforms.Set("uColorFull", new Vector3(0.25f, 0.85f, 0.35f));
        _bar.Uniforms.Set("uColorMid", new Vector3(0.98f, 0.82f, 0.20f));
        _bar.Uniforms.Set("uColorLow", new Vector3(0.95f, 0.22f, 0.20f));

        _shield = AddMaterial("scut", "shield.vert", "shield.frag")
            .Param("uBandCount", 2.0f, 20.0f, 7.0f)
            .Param("uBandSpeed", 0.0f, 3.0f, 0.6f);
        _shield.Transparent = true;
        _shield.Uniforms.Set("uShieldColor", new Vector3(0.35f, 0.75f, 1.0f));

        _value = Tween.Float(1.0f, 1.0f, ValueTime, Easing.EaseOut);
        _trail = Tween.Float(1.0f, 1.0f, TrailTime, Easing.EaseInOut);
        _strength = Tween.Float(0.0f, 0.0f, ShieldTime, Easing.EaseInOut);
        _walk = new Tween(new Vector3(-2.2f, 0.0f, 0.0f), new Vector3(2.2f, 0.0f, 0.0f), 4.0f, Easing.EaseInOut, pingPong: true);

        // --- Obiectele -------------------------------------------------------------------------------
        AddObject("podea", grid);

        // Ierarhia: pivotul personajului poartă corpul, scutul și bara; toate îl urmează.
        _root = AddObject("personaj", null);

        _body = AddObject("corp", cube, _root);
        _body.Transform.Position = new Vector3(0.0f, 0.6f, 0.0f);
        _body.Transform.Scale = new Vector3(0.9f);
        _body.Spin = new Vector3(0.0f, 25.0f, 0.0f);

        // Scutul înaintea barei: obiectele transparente se desenează în ordinea listei.
        SceneObject shield = AddObject("scut", sphere, _root);
        shield.Material = _shield;
        shield.Transform.Position = new Vector3(0.0f, 0.6f, 0.0f);
        shield.Transform.Scale = new Vector3(1.05f);

        // Bara nu este copilul corpului (care se rotește), ci al pivotului: urmează doar deplasarea.
        SceneObject bar = AddObject("bară de viață", quad, _root);
        bar.Material = _bar;
        bar.Transform.Position = new Vector3(0.0f, 2.05f, 0.0f);

        PushUniforms();
    }

    public override void Update(float dt, Input input)
    {
        base.Update(dt, input);   // Spin (corpul)

        if (input.WasPressed(Hit)) TakeHit();
        if (input.WasPressed(Heal)) DoHeal();
        if (input.WasPressed(ToggleShield))
        {
            _shieldOn = !_shieldOn;
            _strength.GoTo(_shieldOn ? 1.0f : 0.0f, ShieldTime);   // Tween pe o uniformă, pornit de o tastă
            ConsolePanel.PrintMessage($"scut {(_shieldOn ? "pornit" : "oprit")}");
        }
        if (input.WasPressed(ToggleWalk))
        {
            _walking = !_walking;
        }

        _value.Update(dt);
        _strength.Update(dt);
        if (_walking) _walk.Update(dt);

        // Urma stă pe loc TrailDelay secunde, apoi coboară lin până la viața curentă.
        if (_trailWait > 0.0f)
        {
            _trailWait -= dt;
            if (_trailWait <= 0.0f) _trail.GoTo(_health, TrailTime);
        }
        _trail.Update(dt);

        // Clipirea scade exponențial: după t secunde rămâne e^(-k·t), oricâte cadre ar fi fost.
        float decay = MathF.Exp(-FlashDecay * dt);
        _barFlash *= decay;
        _shieldFlash *= decay;

        _root.Transform.Position = _walk.Value;
        _body.Tint = _health <= 0.0f
            ? new Color4(0.35f, 0.35f, 0.38f, 1.0f)
            : new Color4(0.95f, 0.75f - 0.5f * _barFlash, 0.45f - 0.3f * _barFlash, 1.0f);

        PushUniforms();
    }

    private void TakeHit()
    {
        float shield = _strength.Scalar;
        float damage = HitDamage * (1.0f - ShieldReduction * shield);
        float before = _value.Scalar;
        _health = MathF.Max(0.0f, _health - damage);
        _hits++;

        // Bara se golește lin; urma pornește din valoarea afișată înainte de lovitură (sau rămâne mai sus,
        // dacă o lovitură anterioară încă se vede) și stă pe loc o clipă.
        _value.GoTo(_health, ValueTime);
        // From = To = capătul urmei: tween-ul "ține" valoarea până când, după TrailDelay, pornește GoTo.
        float trailStart = MathF.Max(_trail.Scalar, before);
        _trail.From = _trail.To = new Vector3(trailStart, 0.0f, 0.0f);
        _trailWait = TrailDelay;

        _barFlash = 1.0f;
        if (shield > 0.05f) _shieldFlash = shield;
        ConsolePanel.PrintMessage($"lovitură: -{damage:P0} (scut {shield:P0}) -> viață {_health:P0}");
    }

    private void DoHeal()
    {
        _health = MathF.Min(1.0f, _health + HealAmount);
        _value.GoTo(_health, ValueTime);
        // La vindecare nu există o porțiune pierdută: urma merge odată cu bara spre noua valoare
        // (uTrail = max(urmă, bară), deci sub bară nu se vede).
        _trail.GoTo(_health, ValueTime);
        _trailWait = 0.0f;
        ConsolePanel.PrintMessage($"vindecare: +{HealAmount:P0} -> viață {_health:P0}");
    }

    /// <summary>Copiază valorile calculate în C# în uniformele materialelor (la fiecare cadru).</summary>
    private void PushUniforms()
    {
        float shown = Math.Clamp(_value.Scalar, 0.0f, 1.0f);
        _bar.Uniforms.Set("uValue", shown);
        _bar.Uniforms.Set("uTrail", MathF.Max(Math.Clamp(_trail.Scalar, 0.0f, 1.0f), shown));
        _bar.Uniforms.Set("uFlash", _barFlash);
        _bar.Uniforms.Set("uCritical", _health > 0.0f && _health < CriticalLevel);

        _shield.Uniforms.Set("uStrength", Math.Clamp(_strength.Scalar, 0.0f, 1.0f));
        _shield.Uniforms.Set("uHit", _shieldFlash);
    }

    public override IEnumerable<string> Describe()
    {
        foreach (string line in base.Describe()) yield return line;
        yield return $"Viață          : {_health:P0} (afișată {_value.Scalar:P0}, urmă {_trail.Scalar:P0})   " +
                     $"critică: {(_health > 0.0f && _health < CriticalLevel ? "da" : "nu")}   lovituri: {_hits}";
        yield return $"Clipire        : bară {_barFlash:F3}   scut {_shieldFlash:F3}";
        yield return $"Scut           : {(_shieldOn ? "pornit" : "oprit")}   tărie {_strength.Scalar:F2}   " +
                     $"reducerea loviturii {ShieldReduction * _strength.Scalar:P0}";
        yield return $"Personaj       : x = {_root.Transform.Position.X:F2}   {(_walking ? "se plimbă" : "stă")}";
    }
}
