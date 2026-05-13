using UnityEngine;

public class NPCCar : MonoBehaviour
{
    public float speed         = 3.5f;
    public bool  IsOnOuterLane = true;
    public float startAngle    = -1f;   // -1 = random; set by SceneBuilder

    [HideInInspector] public bool PlayerWasClose = false;

    public float Angle => angle;   // read by GameManager for crossing detection

    float     angle;
    OvalTrack track;

    void Start()
    {
        track = OvalTrack.Instance;
        angle = startAngle >= 0f ? startAngle : Random.Range(0f, Mathf.PI * 2f);
        ApplyTransform();
    }

    void Update()
    {
        if (GameManager.Instance.IsGameOver()) return;
        // Counter-clockwise = angle decreasing
        angle -= (speed / track.GetArcFactor(angle, IsOnOuterLane)) * Time.deltaTime;
        ApplyTransform();
    }

    void ApplyTransform()
    {
        transform.position = track.GetPosition(angle, IsOnOuterLane);
        transform.rotation = track.GetRotation(angle, IsOnOuterLane, clockwise: false);
    }
}
