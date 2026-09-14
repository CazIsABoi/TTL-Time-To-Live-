using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class OrbitCamera : MonoBehaviour
{
    public enum MouseButton { Left, Right, Middle }

    public enum ConfineMode
    {
        /// <summary>Look-at pivot stays over the board. You can still peek past the edges.</summary>
        PivotOnBoard,
        /// <summary>Keep the visible ground rectangle on the board. Zoomed-out view locks to center.</summary>
        ViewOnBoard
    }
    [Header("Intro")]
    [SerializeField] bool playIntroOnEnable = true;
    [SerializeField] float introYawStart = 20f;
    [SerializeField] float introYawEnd = 45f;
    [SerializeField] float introPitchStart = 58f;
    [SerializeField] float introPitchEnd = 45f;
    [SerializeField] float introOrthoStart = 18f;
    [SerializeField] float introOrthoEnd = 12f;
    [Tooltip("Degrees per second during the spawn. 35–50 is a slow orbit.")]
    [SerializeField] float introSpinSpeed = 40f;
    [Tooltip("Keep orbiting this long after the last tile lands, then settle.")]
    [SerializeField] float introPostReadySeconds = 1.25f;
    [SerializeField] float introSettleSeconds = 1.4f;
    [SerializeField] AnimationCurve introCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    bool introPlaying;
    public bool IntroPlaying => introPlaying;
    public event System.Action OnIntroFinished;

    [Header("Orbit")]
    [SerializeField] Transform target;
    [SerializeField] float yaw = 45f;
    [SerializeField] float pitch = 45f;
    [SerializeField] float minPitch = 15f;
    [SerializeField] float maxPitch = 80f;

    [Tooltip("How far the camera sits behind the look-at point. Does not zoom an ortho camera; keep it large enough that the board stays between the clip planes.")]
    [SerializeField] float rigDistance = 40f;
    [SerializeField] bool autoRigDistance = true;
    [SerializeField] float rigDistancePadding = 20f;

    [Header("Ortho zoom")]
    [SerializeField] float orthoSize = 12f;
    [SerializeField] float minOrthoSize = 4f;
    [SerializeField] float maxOrthoSize = 40f;
    [Tooltip("Cap max zoom-out so the whole board (+ padding) still fits.")]
    [SerializeField] bool fitMaxSizeToBoard = true;
    [SerializeField] float fitPadding = 1.15f;

    [Header("Bounds")]
    [SerializeField] ConfineMode confineMode = ConfineMode.ViewOnBoard;
    [Tooltip("World-space skirt around the board. Positive lets you peek off the edge a little.")]
    [SerializeField] float boundsPadding = 1.5f;
    [SerializeField] float boardHeightPadding = 6f;

    [Header("Speeds")]
    [SerializeField] float moveSpeed = 20f;
    [Tooltip("Scale pan speed with current zoom so one key-hold covers a similar screen distance.")]
    [SerializeField] bool scalePanWithZoom = true;
    [SerializeField] float panSpeedReferenceSize = 12f;
    [SerializeField] float lookSensitivity = 0.15f;
    [SerializeField] float zoomSensitivity = 0.0015f;

    [Header("Look requires hold (strategy-cam)")]
    [SerializeField] bool requireMouseHold = true;
    [SerializeField] MouseButton lookButton = MouseButton.Middle;

    [Header("Input")]
    [SerializeField] InputActionReference moveAction;
    [SerializeField] InputActionReference lookAction;
    [SerializeField] InputActionReference zoomAction;

    Camera cam;
    ExpandingIsland boundIsland;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
    }

    void OnEnable()
    {
        moveAction?.action.Enable();
        lookAction?.action.Enable();
        zoomAction?.action.Enable();
        BindIsland(ExpandingIsland.Instance);

        if (playIntroOnEnable)
            StartCoroutine(PlayIntro());
    }

    void OnDisable()
    {
        BindIsland(null);
        moveAction?.action.Disable();
        lookAction?.action.Disable();
        zoomAction?.action.Disable();
    }

    void BindIsland(ExpandingIsland island)
    {
        if (boundIsland != null)
        {
            boundIsland.OnReady -= OnBoardChanged;
            boundIsland.OnExpanded -= OnBoardExpanded;
        }

        boundIsland = island;
        if (boundIsland == null) return;

        boundIsland.OnReady += OnBoardChanged;
        boundIsland.OnExpanded += OnBoardExpanded;
    }

    void OnBoardExpanded(int _) => OnBoardChanged();

    void OnBoardChanged()
    {
        if (introPlaying) return; 
        if (target != null && TryGetBoardBounds(out Bounds board))
            target.position = ClampTarget(target.position, board);
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (boundIsland == null && ExpandingIsland.Instance != null)
            BindIsland(ExpandingIsland.Instance);

        if (!introPlaying)
            ReadGameplayInput();

        ApplyRig();
    }

    void ReadGameplayInput()
    {
        Vector2 move = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        Vector2 look = lookAction != null ? lookAction.action.ReadValue<Vector2>() : Vector2.zero;
        float zoom = zoomAction != null ? zoomAction.action.ReadValue<float>() : 0f;
        bool hasBoard = TryGetBoardBounds(out Bounds board);

        if (move.sqrMagnitude > 0f && moveSpeed > 0f)
        {
            float pan = moveSpeed;
            if (scalePanWithZoom && panSpeedReferenceSize > 0.01f)
                pan *= cam.orthographicSize / panSpeedReferenceSize;
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            target.position += (forward * move.y + right * move.x) * pan * Time.deltaTime;
        }

        bool looking = !requireMouseHold || IsLookHeld();
        if (looking)
        {
            yaw += look.x * lookSensitivity;
            pitch -= look.y * lookSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        float minSize = minOrthoSize;
        float maxSize = maxOrthoSize;
        if (hasBoard && fitMaxSizeToBoard)
            maxSize = Mathf.Min(maxSize, SizeToFitBoard(board));
        maxSize = Mathf.Max(minSize, maxSize);

        if (Mathf.Abs(zoom) > 0.0001f)
            orthoSize = Mathf.Clamp(orthoSize * Mathf.Exp(-zoom * zoomSensitivity), minSize, maxSize);
        else
            orthoSize = Mathf.Clamp(orthoSize, minSize, maxSize);

        if (hasBoard)
            target.position = ClampTarget(target.position, board);
    }

    void ApplyRig()
    {
        bool hasBoard = TryGetBoardBounds(out Bounds board);
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;

        float distance = autoRigDistance && hasBoard
            ? Mathf.Max(rigDistance, RequiredRigDistance(board))
            : rigDistance;

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(
            target.position + rotation * (Vector3.back * distance),
            rotation);

        if (hasBoard)
            FitClipPlanes(board);
    }

    IEnumerator PlayIntro()
    {
        introPlaying = true;
        yaw = introYawStart;
        pitch = introPitchStart;
        orthoSize = introOrthoStart;
        if (cam != null) cam.orthographicSize = orthoSize;

        yield return null;

        ExpandingIsland island = ExpandingIsland.Instance;
        while (island == null)
        {
            island = ExpandingIsland.Instance;
            yield return null;
        }

        while (!island.Ready)
        {
            yaw += introSpinSpeed * Time.deltaTime;
            pitch = introPitchStart;
            FrameBoardForIntro(lockOrtho: false);
            yield return null;
        }

        float hold = introPostReadySeconds;
        while (hold > 0f)
        {
            hold -= Time.deltaTime;
            yaw += introSpinSpeed * Time.deltaTime;
            FrameBoardForIntro(lockOrtho: false);
            yield return null;
        }

        float startYaw = yaw;
        float startPitch = pitch;
        float startSize = orthoSize; // whatever the fit settled on — no snap
        float t = 0f;
        while (t < introSettleSeconds)
        {
            t += Time.deltaTime;
            float k = introCurve.Evaluate(Mathf.Clamp01(t / introSettleSeconds));
            yaw = Mathf.LerpAngle(startYaw, introYawEnd, k);
            pitch = Mathf.Lerp(startPitch, introPitchEnd, k);
            orthoSize = Mathf.Lerp(startSize, introOrthoEnd, k);
            FrameBoardForIntro(lockOrtho: true); // pivot only, do not touch size
            yield return null;
        }

        yaw = introYawEnd;
        pitch = introPitchEnd;
        orthoSize = introOrthoEnd;
        introPlaying = false;
        OnIntroFinished?.Invoke();
    }

    void FrameBoardForIntro(bool lockOrtho)
    {
        if (target == null || !TryGetBoardBounds(out Bounds board)) return;
        target.position = new Vector3(board.center.x, target.position.y, board.center.z);
        board.Encapsulate(board.center + Vector3.up * 24f);
        if (!lockOrtho)
            orthoSize = Mathf.Max(introOrthoStart, SizeToFitBoard(board) * 0.92f);
    }

    Vector3 ClampTarget(Vector3 pos, Bounds board)
    {
        float pitchRad = Mathf.Max(0.05f, pitch * Mathf.Deg2Rad);
        float halfW = cam.orthographicSize * cam.aspect;
        float halfD = cam.orthographicSize / Mathf.Sin(pitchRad);

        Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        float minX, maxX, minZ, maxZ;

        if (confineMode == ConfineMode.ViewOnBoard)
        {
            float extX = halfW * Mathf.Abs(right.x) + halfD * Mathf.Abs(fwd.x);
            float extZ = halfW * Mathf.Abs(right.z) + halfD * Mathf.Abs(fwd.z);
            minX = board.min.x + extX;
            maxX = board.max.x - extX;
            minZ = board.min.z + extZ;
            maxZ = board.max.z - extZ;

            pos.x = minX > maxX ? board.center.x : Mathf.Clamp(pos.x, minX, maxX);
            pos.z = minZ > maxZ ? board.center.z : Mathf.Clamp(pos.z, minZ, maxZ);
        }
        else
        {
            pos.x = Mathf.Clamp(pos.x, board.min.x, board.max.x);
            pos.z = Mathf.Clamp(pos.z, board.min.z, board.max.z);
        }

        return pos;
    }

    float SizeToFitBoard(Bounds board)
    {
        float pitchRad = Mathf.Max(0.05f, pitch * Mathf.Deg2Rad);
        float sinP = Mathf.Sin(pitchRad);

        Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        float bw = board.size.x;
        float bd = board.size.z;

        // Screen-space half extents of the board AABB at this yaw/pitch.
        float screenHalfW = 0.5f * (bw * Mathf.Abs(right.x) + bd * Mathf.Abs(right.z));
        float screenHalfH = 0.5f * (bw * Mathf.Abs(fwd.x) + bd * Mathf.Abs(fwd.z)) * sinP;

        float sizeForHeight = screenHalfH;
        float sizeForWidth = screenHalfW / Mathf.Max(0.01f, cam.aspect);
        return Mathf.Max(sizeForHeight, sizeForWidth) * fitPadding;
    }

    float RequiredRigDistance(Bounds board)
    {
        // Sit far enough that the farthest board corner is well in front of the camera.
        Vector3 farthest = board.center + board.extents;
        float radius = Vector3.Distance(
            new Vector3(target.position.x, board.center.y, target.position.z),
            new Vector3(farthest.x, board.max.y, farthest.z));
        return radius + rigDistancePadding;
    }

    void FitClipPlanes(Bounds board)
    {
        Matrix4x4 w2c = cam.worldToCameraMatrix;
        float zMin = float.PositiveInfinity;
        float zMax = float.NegativeInfinity;

        Vector3 e = board.extents;
        Vector3 c = board.center;
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);
            float z = -w2c.MultiplyPoint3x4(p).z;
            if (z < zMin) zMin = z;
            if (z > zMax) zMax = z;
        }

        const float pad = 4f;
        cam.nearClipPlane = Mathf.Max(0.05f, zMin - pad);
        cam.farClipPlane = Mathf.Max(cam.nearClipPlane + 1f, zMax + pad);
    }

    bool TryGetBoardBounds(out Bounds board)
    {
        var island = ExpandingIsland.Instance;
        var grid = island != null && island.grid != null ? island.grid : GridManager.Instance;

        int cols;
        int rows;
        if (island != null)
        {
            cols = Mathf.Max(1, island.Columns);
            rows = Mathf.Max(1, island.Rows);
        }
        else if (grid != null)
        {
            cols = Mathf.Max(1, grid.width);
            rows = Mathf.Max(1, grid.height);
        }
        else
        {
            board = default;
            return false;
        }

        Vector3 a = CellWorld(grid, island, 0, 0);
        Vector3 b = CellWorld(grid, island, cols - 1, rows - 1);
        float cs = grid != null ? grid.cellSize : 1f;
        float half = cs * 0.5f;

        Vector3 min = new Vector3(
            Mathf.Min(a.x, b.x) - half - boundsPadding,
            Mathf.Min(a.y, b.y) - 1f,
            Mathf.Min(a.z, b.z) - half - boundsPadding);
        Vector3 max = new Vector3(
            Mathf.Max(a.x, b.x) + half + boundsPadding,
            Mathf.Max(a.y, b.y) + boardHeightPadding,
            Mathf.Max(a.z, b.z) + half + boundsPadding);

        board = new Bounds((min + max) * 0.5f, max - min);
        return board.size.x > 0.01f && board.size.z > 0.01f;
    }

    static Vector3 CellWorld(GridManager grid, ExpandingIsland island, int col, int row)
    {
        if (island != null)
            return island.CellCenter(col, row);
        if (grid != null)
            return grid.CellToWorld(col, row);
        return new Vector3(col + 0.5f, 0f, row + 0.5f);
    }

    bool IsLookHeld()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return false;
        return lookButton switch
        {
            MouseButton.Left => mouse.leftButton.isPressed,
            MouseButton.Right => mouse.rightButton.isPressed,
            _ => mouse.middleButton.isPressed
        };
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!TryGetBoardBounds(out Bounds board)) return;
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireCube(board.center, board.size);
    }
#endif
}