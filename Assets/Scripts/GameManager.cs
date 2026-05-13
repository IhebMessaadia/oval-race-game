using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public PlayerCar playerCar;
    public NPCCar[]  npcCars;

    [Header("Collision Thresholds")]
    public float collisionDistance = 1.3f;
    public float scoringDistance   = 2.4f;

    [Header("UI")]
    public Text       scoreText;
    public GameObject gameOverPanel;
    public Text       finalScoreText;

    int   score;
    bool  gameOver;
    float graceTimer = 2.5f;   // invincible window at game start

    // Crossing detection: tracks signed angle offset (player→NPC) per NPC last frame.
    // When a same-lane NPC crosses from "ahead" to "behind" the player, they collided.
    float[] prevRelAngle;

    void Awake() => Instance = this;

    void Start()
    {
        gameOverPanel.SetActive(false);
        prevRelAngle = new float[npcCars.Length];
        // Seed with the NPC's start angle (player angle is 0 at start).
        // Raw diff = npc.Angle - player.Angle starts positive (NPC ahead of player)
        // and crosses 0 when they collide, so we just need to detect the sign change.
        for (int i = 0; i < npcCars.Length; i++)
            prevRelAngle[i] = npcCars[i].startAngle >= 0f ? npcCars[i].startAngle : 3f;
        RefreshHUD();
    }

    void Update()
    {
        if (gameOver) return;

        // Give cars time to spread out before collision checks begin
        if (graceTimer > 0f) { graceTimer -= Time.deltaTime; return; }

        CheckCollisionsAndScoring();
    }

    void CheckCollisionsAndScoring()
    {
        Vector3 pp     = playerCar.transform.position;
        float   pAngle = playerCar.Angle;

        for (int i = 0; i < npcCars.Length; i++)
        {
            NPCCar npc      = npcCars[i];
            float  dist     = Vector3.Distance(pp, npc.transform.position);
            bool   sameLane = (npc.IsOnOuterLane == playerCar.IsOnOuterLane);

            // ── Collision detection (two methods for robustness) ─────────────
            // 1. Distance: inter-lane gap is always 1.5 units, so 1.3 only fires same-lane.
            if (dist < collisionDistance) { TriggerGameOver(); return; }

            // 2. Crossing: player goes CW (angle+), NPCs go CCW (angle-).
            //    rawDiff decreases by 2π every relative orbit.  We detect a crossing
            //    each time floor(rawDiff / 2π) ticks down by 1 — works on every lap.
            float rawDiff = npc.Angle - pAngle;
            if (sameLane && dist < 4f)
            {
                int prevFloor = Mathf.FloorToInt(prevRelAngle[i] / (Mathf.PI * 2f));
                int currFloor = Mathf.FloorToInt(rawDiff       / (Mathf.PI * 2f));
                if (currFloor < prevFloor) { TriggerGameOver(); return; }
            }
            prevRelAngle[i] = rawDiff;

            // ── Scoring ──────────────────────────────────────────────────────
            if (sameLane && dist < scoringDistance && !npc.PlayerWasClose)
                npc.PlayerWasClose = true;

            if (npc.PlayerWasClose && (!sameLane || dist >= scoringDistance))
            {
                npc.PlayerWasClose = false;
                score++;
                RefreshHUD();
            }
        }
    }

    public void TriggerGameOver()
    {
        gameOver = true;
        gameOverPanel.SetActive(true);
        finalScoreText.text = "Final Score: " + score;
    }

    public bool IsGameOver() => gameOver;

    void RefreshHUD() => scoreText.text = "Score: " + score;

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
