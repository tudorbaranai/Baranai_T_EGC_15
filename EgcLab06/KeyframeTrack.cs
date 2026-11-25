using OpenTK.Mathematics;

namespace Egc;

/// <summary>
/// NOU: o pistă de cadre-cheie (secțiunea 4): o listă de valori-cheie, la intervale EGALE de timp
/// (SecondsPerKey). Între două chei valoarea se interpolează. După ultima cheie pista revine
/// la prima și o ia de la capăt (bucla se închide singură).
/// </summary>
public sealed class KeyframeTrack
{
    private readonly List<Vector3> _keys = new();

    /// <summary>Timpul dintre două chei consecutive, în secunde.</summary>
    public float SecondsPerKey { get; }

    /// <summary>Easing-ul aplicat între două chei (implicit liniar).</summary>
    public EasingFunction Ease = Easing.Linear;

    public KeyframeTrack(float secondsPerKey)
    {
        SecondsPerKey = MathF.Max(secondsPerKey, 0.001f);
    }

    public int Count => _keys.Count;
    public IReadOnlyList<Vector3> Keys => _keys;

    /// <summary>Durata unui tur complet (inclusiv drumul de la ultima cheie înapoi la prima).</summary>
    public float Duration => _keys.Count * SecondsPerKey;

    /// <summary>Adaugă o cheie la sfârșitul pistei.</summary>
    public KeyframeTrack Add(Vector3 value)
    {
        _keys.Add(value);
        return this;   // permite scrierea în lanț: track.Add(...).Add(...)
    }

    public void Clear() => _keys.Clear();

    /// <summary>Valoarea pistei la momentul <paramref name="time"/> (în secunde).</summary>
    public Vector3 Sample(float time)
    {
        if (_keys.Count == 0) return Vector3.Zero;
        if (_keys.Count == 1) return _keys[0];

        // P3 (TODO 6.3 - 1): câte intervale au trecut. Cu % poziția rămâne în [0, Count): după ultima cheie
        //     o luăm de la capăt (bucla). Pentru un timp negativ aducem restul înapoi în interval.
        float position = time / SecondsPerKey % _keys.Count;
        if (position < 0.0f) position += _keys.Count;

        // P3 (TODO 6.3 - 2): partea întreagă = cheia de plecare, partea fracționară = cât din drumul dintre
        //     cele două chei s-a parcurs (de exemplu 1.5 -> între cheia 1 și cheia 2, la jumătate).
        int i = Math.Min((int)MathF.Floor(position), _keys.Count - 1);
        float u = position - i;

        // P3 (TODO 6.3 - 3): cheia următoare este (i + 1) % Count: după ultima urmează prima, deci bucla
        //     se închide fără salt. Lerp cu easing-ul pistei aplicat pe u (EaseInOut = oprire în fiecare cheie).
        int next = (i + 1) % _keys.Count;
        return Vector3.Lerp(_keys[i], _keys[next], Ease(u));
    }
}
