using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SceneBuilder
{
    const string ScenePath = "Assets/RaceGame.unity";
    const string BuildPath = "Builds/RaceGame.exe";

    // Lane centre paths (must match OvalTrack fields — L is shared so lanes are parallel)
    const float L   = 7f;              // half-length of straights (same for both lanes)
    const float OR_ = 4f;              // outer lane curve radius
    const float IR  = 2.5f;           // inner lane curve radius
    const float OL  = L;              // aliases for readability
    const float IL  = L;

    // Half-width of each lane
    const float HLW = 0.75f;

    public static void BuildGame()
    {
        BuildScene();
        string dir = Path.GetDirectoryName(BuildPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var r = BuildPipeline.BuildPlayer(new[] { ScenePath }, BuildPath,
                    BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log("[SceneBuilder] " + r.summary.result + " errors:" + r.summary.totalErrors);
    }

    static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AddLighting();
        AddGround();
        AddTrackSurface();
        AddLaneMarkings();
        AddOvalTrackScript();
        var player = AddPlayerCar();
        var npcs   = AddNPCs();
        AddCamera();
        var (scoreTxt, panelGO, finalTxt, btn) = AddUI();
        var gm = AddGameManager(player, npcs, scoreTxt, panelGO, finalTxt);
        UnityEventTools.AddPersistentListener(btn.onClick, gm.RestartGame);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.Refresh();
        Debug.Log("[SceneBuilder] scene saved.");
    }

    // ── Environment ──────────────────────────────────────────────────────────

    static void AddLighting()
    {
        var go = new GameObject("Sun");
        var l  = go.AddComponent<Light>();
        l.type = LightType.Directional; l.intensity = 1.15f;
        l.color = new Color(1f, 0.96f, 0.88f);
        go.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        RenderSettings.ambientLight = new Color(0.3f, 0.32f, 0.35f);
    }

    static void AddGround()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "Ground";
        go.transform.localScale = new Vector3(5f, 1f, 4f);
        go.transform.position   = new Vector3(0f, -0.01f, 0f);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().material =
            new Material(Shader.Find("Standard")) { color = new Color(0.17f, 0.25f, 0.13f) };
    }

    static void AddTrackSurface()
    {
        // Full asphalt ring from outer edge to inner edge
        CreateStadiumRing("Track_Asphalt",
            OL + HLW, OR_ + HLW,       // outer boundary
            IL - HLW, IR  - HLW,       // inner boundary
            new Color(0.24f, 0.24f, 0.24f), 0f);

        // Inner field — match grass colour so it reads as non-track
        CreateStadiumDisk("Track_InnerField",
            IL - HLW, IR - HLW,
            new Color(0.17f, 0.25f, 0.13f), 0.003f);
    }

    // ── Lane markings — 3 lines only (outer edge, centre divider, inner edge) ─

    static void AddLaneMarkings()
    {
        float y = 0.05f;

        // Outer solid white edge
        DrawStadiumLine("Mark_OuterEdge",
            OL + HLW, OR_ + HLW, Color.white, 0.14f, y);

        // Centre dashed white divider (separates outer lane from inner lane)
        float divL = (OL + IL) * 0.5f;
        float divR = (OR_ + IR) * 0.5f;
        DrawStadiumDash("Mark_CentreDivider",
            divL, divR, Color.white, 0.10f, y);

        // Inner solid white edge
        DrawStadiumLine("Mark_InnerEdge",
            IL - HLW, IR - HLW, Color.white, 0.14f, y);
    }

    static void AddOvalTrackScript()
    {
        var go = new GameObject("OvalTrack");
        var t  = go.AddComponent<OvalTrack>();
        t.outerL = L; t.outerR = OR_;
        t.innerL = L; t.innerR = IR;
    }

    // ── Cars ─────────────────────────────────────────────────────────────────

    static PlayerCar AddPlayerCar()
    {
        var root = new GameObject("PlayerCar");
        BuildCarMesh(root, new Color(0.1f, 0.35f, 1f), new Color(0.05f, 0.2f, 0.65f));
        var c = root.AddComponent<PlayerCar>(); c.speed = 5f;
        return c;
    }

    static NPCCar[] AddNPCs()
    {
        // Player goes CW (angle+) from 0; NPCs go CCW (angle-).
        // Layout: outer / inner / outer, evenly spaced so when an outer NPC arrives
        // the inner lane is always clear, and vice-versa — there is always a safe dodge.
        // Each zone is randomised ±0.25 rad so restarts feel different.
        float base1 = 3.1f, base2 = 4.4f, base3 = 5.7f;
        float[] startAngles = {
            base1 + Random.Range(-0.25f, 0.25f),
            base2 + Random.Range(-0.25f, 0.25f),
            base3 + Random.Range(-0.25f, 0.25f),
        };
        bool[] lanes = { true, false, true };   // outer, inner, outer — fixed, fair
        float[] speeds = {
            Random.Range(2.8f, 3.4f),
            Random.Range(2.9f, 3.5f),
            Random.Range(2.7f, 3.3f),
        };

        (Color body, Color cabin)[] cols =
        {
            (new Color(0.92f,0.10f,0.10f), new Color(0.55f,0.04f,0.04f)),
            (new Color(0.90f,0.12f,0.25f), new Color(0.55f,0.04f,0.12f)),
            (new Color(0.88f,0.08f,0.15f), new Color(0.50f,0.03f,0.08f)),
        };

        var npcs = new NPCCar[3];
        for (int i = 0; i < 3; i++)
        {
            var root = new GameObject("NPC_" + (i + 1));
            BuildCarMesh(root, cols[i].body, cols[i].cabin);
            var n = root.AddComponent<NPCCar>();
            n.speed          = speeds[i];
            n.IsOnOuterLane  = lanes[i];
            n.startAngle     = startAngles[i];
            npcs[i] = n;
        }
        return npcs;
    }

    static void BuildCarMesh(GameObject root, Color body, Color cabin)
    {
        Part(root, "Body",  Vector3.zero,                   new Vector3(0.76f,0.17f,1.45f), body);
        Part(root, "Cabin", new Vector3(0f, 0.17f, 0.08f), new Vector3(0.52f,0.19f,0.62f), cabin);
        Part(root, "Wind",  new Vector3(0f, 0.22f, 0.37f), new Vector3(0.5f, 0.06f,0.2f),  new Color(0.45f,0.75f,1f));
        Color w = new Color(0.1f, 0.1f, 0.1f);
        Wheel(root, -0.44f, -0.065f,  0.5f, w);
        Wheel(root,  0.44f, -0.065f,  0.5f, w);
        Wheel(root, -0.44f, -0.065f, -0.5f, w);
        Wheel(root,  0.44f, -0.065f, -0.5f, w);
    }

    static void Part(GameObject root, string name, Vector3 pos, Vector3 scale, Color col)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(root.transform);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = col };
    }

    static void Wheel(GameObject root, float x, float y, float z, Color col)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Wheel"; go.transform.SetParent(root.transform);
        go.transform.localPosition = new Vector3(x, y, z);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        go.transform.localScale    = new Vector3(0.22f, 0.08f, 0.22f);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = col };
    }

    // ── Camera ────────────────────────────────────────────────────────────────

    static void AddCamera()
    {
        var go = new GameObject("Main Camera"); go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView     = 52f;
        cam.backgroundColor = new Color(0.07f, 0.09f, 0.07f);
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.nearClipPlane   = 0.3f; cam.farClipPlane = 200f;
        go.transform.position = new Vector3(0f, 16f, -10f);
        go.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        go.AddComponent<AudioListener>();
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    static (Text score, GameObject panel, Text final, Button restart) AddUI()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                 ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        var cGO = new GameObject("Canvas");
        var cv  = cGO.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay;
        var sc  = cGO.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        cGO.AddComponent<GraphicRaycaster>();

        var ev = new GameObject("EventSystem");
        ev.AddComponent<UnityEngine.EventSystems.EventSystem>();
        ev.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // Score box — dark pill behind the text
        var sbGO = new GameObject("ScoreBox"); sbGO.transform.SetParent(cGO.transform, false);
        var sbImg = sbGO.AddComponent<Image>(); sbImg.color = new Color(0f, 0f, 0f, 0.55f);
        var sbR = sbGO.GetComponent<RectTransform>();
        sbR.anchorMin = sbR.anchorMax = sbR.pivot = new Vector2(0f, 1f);
        sbR.anchoredPosition = new Vector2(16f, -16f); sbR.sizeDelta = new Vector2(230f, 62f);

        var stGO = MakeTxt(cGO, "ScoreText", "Score: 0", font, 44, new Color(1f, 0.88f, 0.2f), FontStyle.Bold);
        var stR  = stGO.GetComponent<RectTransform>();
        stR.anchorMin = stR.anchorMax = stR.pivot = new Vector2(0f, 1f);
        stR.anchoredPosition = new Vector2(24f, -20f); stR.sizeDelta = new Vector2(220f, 58f);
        stGO.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);

        // Hint box — bottom-centre, subtle
        var hbGO = new GameObject("HintBox"); hbGO.transform.SetParent(cGO.transform, false);
        var hbImg = hbGO.AddComponent<Image>(); hbImg.color = new Color(0f, 0f, 0f, 0.45f);
        var hbR = hbGO.GetComponent<RectTransform>();
        hbR.anchorMin = hbR.anchorMax = hbR.pivot = new Vector2(0.5f, 0f);
        hbR.anchoredPosition = new Vector2(0f, 20f); hbR.sizeDelta = new Vector2(320f, 42f);

        var hGO = MakeTxt(cGO, "Hint", "[ SPACE ]  Switch Lane", font, 24, new Color(0.85f, 0.85f, 0.85f), FontStyle.Normal);
        var hR = hGO.GetComponent<RectTransform>();
        hR.anchorMin = hR.anchorMax = hR.pivot = new Vector2(0.5f, 0f);
        hR.anchoredPosition = new Vector2(0f, 22f); hR.sizeDelta = new Vector2(320f, 40f);
        hGO.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

        // Rules box — top-right corner
        var rbGO = new GameObject("RulesBox"); rbGO.transform.SetParent(cGO.transform, false);
        var rbImg = rbGO.AddComponent<Image>(); rbImg.color = new Color(0f, 0f, 0f, 0.55f);
        var rbR = rbGO.GetComponent<RectTransform>();
        rbR.anchorMin = rbR.anchorMax = rbR.pivot = new Vector2(1f, 1f);
        rbR.anchoredPosition = new Vector2(-16f, -16f); rbR.sizeDelta = new Vector2(300f, 118f);

        var rtGO = MakeTxt(cGO, "RulesTitle", "Game Rules:", font, 24, Color.white, FontStyle.Bold);
        var rtR = rtGO.GetComponent<RectTransform>();
        rtR.anchorMin = rtR.anchorMax = rtR.pivot = new Vector2(1f, 1f);
        rtR.anchoredPosition = new Vector2(-24f, -26f); rtR.sizeDelta = new Vector2(280f, 30f);
        rtGO.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

        var r1GO = MakeTxt(cGO, "Rule1", "✔  Avoid NPC  =  +Score", font, 21, new Color(0.4f, 1f, 0.4f), FontStyle.Normal);
        var r1R = r1GO.GetComponent<RectTransform>();
        r1R.anchorMin = r1R.anchorMax = r1R.pivot = new Vector2(1f, 1f);
        r1R.anchoredPosition = new Vector2(-24f, -62f); r1R.sizeDelta = new Vector2(280f, 28f);
        r1GO.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

        var r2GO = MakeTxt(cGO, "Rule2", "✖  Collision  =  Game Over", font, 21, new Color(1f, 0.4f, 0.4f), FontStyle.Normal);
        var r2R = r2GO.GetComponent<RectTransform>();
        r2R.anchorMin = r2R.anchorMax = r2R.pivot = new Vector2(1f, 1f);
        r2R.anchoredPosition = new Vector2(-24f, -94f); r2R.sizeDelta = new Vector2(280f, 28f);
        r2GO.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

        var pGO = new GameObject("GameOverPanel"); pGO.transform.SetParent(cGO.transform, false);
        var pI  = pGO.AddComponent<Image>(); pI.color = new Color(0.05f, 0.05f, 0.1f, 0.92f);
        var pR  = pGO.GetComponent<RectTransform>();
        pR.anchorMin = pR.anchorMax = pR.pivot = new Vector2(.5f, .5f);
        pR.sizeDelta = new Vector2(500, 300); pR.anchoredPosition = Vector2.zero;

        var hdGO = MakeTxt(pGO, "Heading", "GAME OVER", font, 62, new Color(1f, 0.22f, 0.22f), FontStyle.Bold);
        Stretch(hdGO, 0, .56f, 1, 1); hdGO.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
        hdGO.AddComponent<Outline>().effectColor = new Color(0.6f, 0f, 0f, 1f);

        var fsGO = MakeTxt(pGO, "FinalScoreText", "Final Score: 0", font, 32, Color.white, FontStyle.Normal);
        Stretch(fsGO, 0, .32f, 1, .58f); fsGO.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

        var bGO = new GameObject("RestartButton"); bGO.transform.SetParent(pGO.transform, false);
        var bI  = bGO.AddComponent<Image>(); bI.color = new Color(.1f, .6f, .1f);
        var btn = bGO.AddComponent<Button>(); btn.targetGraphic = bI;
        Stretch(bGO, .25f, .04f, .75f, .28f);
        var btGO = MakeTxt(bGO, "Label", "RESTART", font, 28, Color.white, FontStyle.Bold);
        Stretch(btGO, 0, 0, 1, 1); btGO.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

        return (stGO.GetComponent<Text>(), pGO, fsGO.GetComponent<Text>(), btn);
    }

    static GameManager AddGameManager(PlayerCar player, NPCCar[] npcs,
                                      Text score, GameObject panel, Text final)
    {
        var go = new GameObject("GameManager");
        var gm = go.AddComponent<GameManager>();
        gm.playerCar = player; gm.npcCars = npcs;
        gm.scoreText = score; gm.gameOverPanel = panel; gm.finalScoreText = final;
        gm.collisionDistance = 1.3f; gm.scoringDistance = 2.4f;
        return gm;
    }

    // ── Mesh helpers ──────────────────────────────────────────────────────────

    static void CreateStadiumRing(string name,
        float oL, float oR, float iL, float iR, Color color, float y)
    {
        const int N = 96;
        var verts = new Vector3[N * 2];
        var tris  = new int[N * 6];
        for (int i = 0; i < N; i++)
        {
            float t = i / (float)N * Mathf.PI * 2f;
            var op = OvalTrack.StadiumPoint(t, oL, oR); op.y = y; verts[i]     = op;
            var ip = OvalTrack.StadiumPoint(t, iL, iR); ip.y = y; verts[i + N] = ip;
        }
        for (int i = 0; i < N; i++)
        {
            int nx = (i + 1) % N, b = i * 6;
            tris[b]   = i;     tris[b+1] = nx;      tris[b+2] = nx + N;
            tris[b+3] = i;     tris[b+4] = nx + N;  tris[b+5] = i  + N;
        }
        MakeMeshGO(name, verts, tris, color);
    }

    static void CreateStadiumDisk(string name, float L, float r, Color color, float y)
    {
        const int N = 96;
        var verts = new Vector3[N + 1];
        var tris  = new int[N * 3];
        verts[0] = new Vector3(0, y, 0);
        for (int i = 0; i < N; i++)
        {
            var p = OvalTrack.StadiumPoint(i / (float)N * Mathf.PI * 2f, L, r);
            p.y = y; verts[i + 1] = p;
        }
        for (int i = 0; i < N; i++)
        { tris[i*3] = 0; tris[i*3+1] = i+1; tris[i*3+2] = (i+1)%N+1; }
        MakeMeshGO(name, verts, tris, color);
    }

    static void MakeMeshGO(string name, Vector3[] verts, int[] tris, Color color)
    {
        var go = new GameObject(name);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.material = new Material(Shader.Find("Standard")) { color = color };
        var mesh = new Mesh { name = name, vertices = verts, triangles = tris };
        mesh.RecalculateNormals(); mf.sharedMesh = mesh;
    }

    // ── Line helpers ──────────────────────────────────────────────────────────

    static void DrawStadiumLine(string name, float L, float r, Color color, float width, float y)
    {
        const int N = 96;
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.loop = true; lr.positionCount = N;
        lr.startWidth = lr.endWidth = width; lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.material = new Material(Shader.Find("Unlit/Color")) { color = color };
        for (int i = 0; i < N; i++)
        {
            var p = OvalTrack.StadiumPoint(i / (float)N * Mathf.PI * 2f, L, r);
            p.y = y; lr.SetPosition(i, p);
        }
    }

    static void DrawStadiumDash(string name, float L, float r, Color color, float width, float y)
    {
        const int   segs     = 44;
        const float dashFrac = 0.52f;
        var parent = new GameObject(name);
        for (int i = 0; i < segs; i++)
        {
            float t0 = i / (float)segs * Mathf.PI * 2f;
            float t1 = (i + dashFrac) / segs * Mathf.PI * 2f;
            var go = new GameObject("d"); go.transform.SetParent(parent.transform);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.startWidth = lr.endWidth = width;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.material = new Material(Shader.Find("Unlit/Color")) { color = color };
            var p0 = OvalTrack.StadiumPoint(t0, L, r); p0.y = y;
            var p1 = OvalTrack.StadiumPoint(t1, L, r); p1.y = y;
            lr.SetPosition(0, p0); lr.SetPosition(1, p1);
        }
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    static GameObject MakeTxt(GameObject parent, string name, string text,
                               Font font, int size, Color color, FontStyle style)
    {
        var go = new GameObject(name); go.transform.SetParent(parent.transform, false);
        var t = go.AddComponent<Text>();
        t.text = text; t.font = font; t.fontSize = size;
        t.color = color; t.fontStyle = style; t.supportRichText = false;
        return go;
    }

    static void Stretch(GameObject go, float x0, float y0, float x1, float y1)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
