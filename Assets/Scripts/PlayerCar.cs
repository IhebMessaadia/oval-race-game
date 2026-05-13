using UnityEngine;

public class PlayerCar : MonoBehaviour
{
    public float speed = 5f;

    public bool IsOnOuterLane { get; private set; } = true;

    public float Angle => angle;   // read by GameManager for crossing detection

    float angle = 0f;
    OvalTrack track;

    void Start()
    {
        track = OvalTrack.Instance;
        ApplyTransform();
    }

    void Update()
    {
        if (GameManager.Instance.IsGameOver()) return;

        if (Input.GetKeyDown(KeyCode.Space))
            IsOnOuterLane = !IsOnOuterLane;

        // Clockwise = angle increasing in this parametrisation
        angle += (speed / track.GetArcFactor(angle, IsOnOuterLane)) * Time.deltaTime;
        ApplyTransform();
    }

    void ApplyTransform()
    {
        transform.position = track.GetPosition(angle, IsOnOuterLane);
        transform.rotation = track.GetRotation(angle, IsOnOuterLane, clockwise: true);
    }
}
