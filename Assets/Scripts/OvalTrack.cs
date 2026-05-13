using UnityEngine;

// Stadium-shaped (rounded-rectangle) track:
//   two straight sections of half-length L, connected by semicircular ends of radius R.
// The angle parameter t maps uniformly to arc-length so cars move at constant speed.
public class OvalTrack : MonoBehaviour
{
    public static OvalTrack Instance { get; private set; }

    [Header("Outer Lane")]
    public float outerL = 7f;   // half-length of straight sections
    public float outerR = 4f;   // radius of curved ends

    [Header("Inner Lane")]
    public float innerL = 7f;   // same L keeps lanes parallel on straights
    public float innerR = 2.5f;

    void Awake() => Instance = this;

    public Vector3 GetPosition(float t, bool outerLane)
        => StadiumPoint(t, outerLane ? outerL : innerL, outerLane ? outerR : innerR);

    // Constant arc-factor = P / (2π) for uniform parametrisation.
    public float GetArcFactor(float t, bool outerLane)
    {
        float L = outerLane ? outerL : innerL;
        float r = outerLane ? outerR : innerR;
        return (4f * L + 2f * Mathf.PI * r) / (Mathf.PI * 2f);
    }

    public Quaternion GetRotation(float t, bool outerLane, bool clockwise)
    {
        Vector3 tan = StadiumTangent(t, outerLane ? outerL : innerL,
                                        outerLane ? outerR : innerR);
        if (!clockwise) tan = -tan;
        return Quaternion.LookRotation(tan, Vector3.up);
    }

    // ── Public static helpers (used by SceneBuilder for meshes / lines) ──────

    public static Vector3 StadiumPoint(float t, float L, float r)
    {
        float P  = 4f * L + 2f * Mathf.PI * r;
        float s  = NormAngle(t) / (Mathf.PI * 2f) * P;

        float s1 = Mathf.PI * r * 0.5f;
        float s2 = s1 + 2f * L;
        float s3 = s2 + Mathf.PI * r;
        float s4 = s3 + 2f * L;

        if (s < s1) { float θ = -s / r;                              return new Vector3( L + r * Mathf.Cos(θ), 0f,  r * Mathf.Sin(θ)); }
        if (s < s2)                                                   return new Vector3( L - (s - s1),         0f, -r);
        if (s < s3) { float θ = -Mathf.PI * .5f - (s - s2) / r;     return new Vector3(-L + r * Mathf.Cos(θ), 0f,  r * Mathf.Sin(θ)); }
        if (s < s4)                                                   return new Vector3(-L + (s - s3),         0f,  r);
        {           float θ =  Mathf.PI * .5f - (s - s4) / r;       return new Vector3( L + r * Mathf.Cos(θ), 0f,  r * Mathf.Sin(θ)); }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    static Vector3 StadiumTangent(float t, float L, float r)
    {
        float P = 4f * L + 2f * Mathf.PI * r;
        float s = NormAngle(t) / (Mathf.PI * 2f) * P;

        float s1 = Mathf.PI * r * 0.5f;
        float s2 = s1 + 2f * L;
        float s3 = s2 + Mathf.PI * r;
        float s4 = s3 + 2f * L;

        if (s < s1) { float θ = -s / r;                              return new Vector3( Mathf.Sin(θ), 0f, -Mathf.Cos(θ)); }
        if (s < s2)                                                   return new Vector3(-1f, 0f, 0f);
        if (s < s3) { float θ = -Mathf.PI * .5f - (s - s2) / r;     return new Vector3( Mathf.Sin(θ), 0f, -Mathf.Cos(θ)); }
        if (s < s4)                                                   return new Vector3( 1f, 0f, 0f);
        {           float θ =  Mathf.PI * .5f - (s - s4) / r;       return new Vector3( Mathf.Sin(θ), 0f, -Mathf.Cos(θ)); }
    }

    static float NormAngle(float t)
        => (t % (Mathf.PI * 2f) + Mathf.PI * 2f) % (Mathf.PI * 2f);
}
