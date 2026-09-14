using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Legacy definition, kept only so previously-serialized scene data doesn't
// break; buildable data now comes from CardData (via HandManager) instead.
[System.Serializable]
public class BuildableDefinition
{
    public string name;
    public GameObject prefab;
    public int sizeX = 1;
    public int sizeY = 1;
    public int cost = 0;
    [Tooltip("Vertical offset applied on top of the grid surface, e.g. half the prefab's height if its pivot is centered, so it doesn't sink into the ground.")]
    public float yOffset = 0f;
}

// Handles building placement on the shared enemy pathfinding grid: preview
// while a hand card is selected (via click or 1-5 keys, through HandManager),
// footprint validation (walkable, in bounds, not near the core, not
// overlapping an existing building), confirm placement with left click
// (which consumes/discards the card), cancel with Deselect, and demolish
// existing buildings. Placement is only allowed while GameController.IsBetweenWaves is true.
public class GridBuildPlacer : MonoBehaviour
{
    [SerializeField] private InputActionReference leftClick;
    [SerializeField] private InputActionReference mousePosition;
    [SerializeField] private InputActionReference deselect;
    [SerializeField] private InputActionReference demolish;
    [SerializeField] private InputActionReference rotate;
    [SerializeField] private InputActionReference object1;
    [SerializeField] private InputActionReference object2;
    [SerializeField] private InputActionReference object3;
    [SerializeField] private InputActionReference object4;
    [SerializeField] private InputActionReference object5;

    [SerializeField] private HandManager handManager;
    [SerializeField] private GameController gameController;
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Vertical offset applied to every placed/previewed object so it doesn't sink into the ground.")]
    [SerializeField] private float yOffset = 0.5f;
    [SerializeField] private Material previewValidMaterial;
    [SerializeField] private Material previewInvalidMaterial;
    [SerializeField] private Material stackHoverMaterial;
    PlacedBuilding hoveredStack;
    Renderer[] hoveredRenderers;
    Material[][] hoveredOriginalMats;

    private readonly List<PlacedBuilding> placedBuildings = new List<PlacedBuilding>();
    private int selectedIndex = -1;
    private CardData selectedCard;
    private GameObject previewInstance;
    private Renderer[] previewRenderers;
    private readonly List<Vector2Int> previewCells = new List<Vector2Int>();
    private bool previewValid;
    private int rotationSteps; // 0..3

    private float RotationY => rotationSteps * 90f;

    private void OnEnable()
    {
        if (leftClick != null) leftClick.action.performed += OnLeftClick;
        if (deselect != null) deselect.action.performed += OnDeselect;
        if (demolish != null) demolish.action.performed += OnDemolish;
        if (rotate != null) rotate.action.performed += OnRotate;
        if (object1 != null) object1.action.performed += OnObject1;
        if (object2 != null) object2.action.performed += OnObject2;
        if (object3 != null) object3.action.performed += OnObject3;
        if (object4 != null) object4.action.performed += OnObject4;
        if (object5 != null) object5.action.performed += OnObject5;
        if (handManager != null) handManager.SelectionChanged += OnHandSelectionChanged;
    }

    private void OnDisable()
    {
        if (leftClick != null) leftClick.action.performed -= OnLeftClick;
        if (deselect != null) deselect.action.performed -= OnDeselect;
        if (demolish != null) demolish.action.performed -= OnDemolish;
        if (rotate != null) rotate.action.performed -= OnRotate;
        if (object1 != null) object1.action.performed -= OnObject1;
        if (object2 != null) object2.action.performed -= OnObject2;
        if (object3 != null) object3.action.performed -= OnObject3;
        if (object4 != null) object4.action.performed -= OnObject4;
        if (object5 != null) object5.action.performed -= OnObject5;
        if (handManager != null) handManager.SelectionChanged -= OnHandSelectionChanged;
    }

    private void OnObject1(InputAction.CallbackContext context) => Select(0);
    private void OnObject2(InputAction.CallbackContext context) => Select(1);
    private void OnObject3(InputAction.CallbackContext context) => Select(2);
    private void OnObject4(InputAction.CallbackContext context) => Select(3);
    private void OnObject5(InputAction.CallbackContext context) => Select(4);

    // Number keys just forward into HandManager's own selection so the
    // .card.selected animation and click-selection stay in sync with keys.
    private void Select(int index)
    {
        if (gameController != null && !gameController.IsBetweenWaves)
        {
            handManager?.Deselect();
            return;
        }
        handManager?.ToggleSelect(index);
    }

    // Called whenever HandManager's selected card changes (via click or key).
    private void OnHandSelectionChanged(int index, CardData card)
    {
        bool sameCard = card != null && card == selectedCard;
        selectedIndex = index;
        selectedCard = card;

        if (selectedIndex < 0 || selectedCard == null || selectedCard.prefab == null)
        {
            CancelPreview();
            return;
        }

        if (!sameCard)
            rotationSteps = 0;

        SpawnPreview(selectedCard);
    }

    private void OnDeselect(InputAction.CallbackContext context)
    {
        handManager?.Deselect();
        CancelPreview();
    }

    private void OnRotate(InputAction.CallbackContext context)
    {
        TryRotate(1);
    }

    private void TryRotate(int deltaSteps)
    {
        if (selectedCard == null || previewInstance == null) return;
        if (!selectedCard.canRotate) return;
        if (gameController != null && !gameController.IsBetweenWaves) return;

        rotationSteps = (rotationSteps + deltaSteps) & 3;
        ApplyPreviewRotation();
    }

    private void CancelPreview()
    {
        ClearStackHover();
        selectedIndex = -1;
        selectedCard = null;
        rotationSteps = 0;
        if (previewInstance != null)
            Destroy(previewInstance);
        previewInstance = null;
        previewRenderers = null;
    }

    private void SpawnPreview(CardData card)
    {
        if (previewInstance != null)
            Destroy(previewInstance);
        if (card.prefab == null) return;

        previewInstance = Instantiate(card.prefab);
        SetCollidersEnabled(previewInstance, false);
        previewRenderers = previewInstance.GetComponentsInChildren<Renderer>();
        ApplyPreviewRotation();
    }

    private void ApplyPreviewRotation()
    {
        if (previewInstance == null) return;
        previewInstance.transform.rotation = Quaternion.Euler(0f, RotationY, 0f);
    }

    private static void SetCollidersEnabled(GameObject go, bool enabled)
    {
        foreach (var col in go.GetComponentsInChildren<Collider>())
            col.enabled = enabled;
    }

    // Returns one world position per occupied grid cell for this instance,
    // in its current position/rotation. Prefers explicit BuildingShapeUnit
    // markers - one per cell of the footprint (e.g. a 2x1 building has two
    // markers, each centered on its own cell) - since that's exact and
    // rotates naturally with the object. Falls back to synthesizing a
    // sizeX * sizeY rectangle of cell-sized points for prefabs that don't
    // have markers yet.
    private static List<Vector3> GetShapeWorldPositions(GameObject instance, CardData card)
    {
        var units = instance.GetComponentsInChildren<BuildingShapeUnit>();
        if (units != null && units.Length > 0)
        {
            var found = new List<Vector3>(units.Length);
            foreach (var u in units)
                found.Add(u.transform.position);
            return found;
        }

        float cs = GridManager.Instance != null ? GridManager.Instance.cellSize : 1f;
        int sx = Mathf.Max(1, card.sizeX);
        int sy = Mathf.Max(1, card.sizeY);
        var pts = new List<Vector3>(sx * sy);
        for (int y = 0; y < sy; y++)
        {
            for (int x = 0; x < sx; x++)
            {
                float lx = (x - (sx - 1) * 0.5f) * cs;
                float lz = (y - (sy - 1) * 0.5f) * cs;
                pts.Add(instance.transform.TransformPoint(new Vector3(lx, 0f, lz)));
            }
        }
        return pts;
    }

    // Positions instance at anchorWorld, reads off its shape-unit cells, and
    // nudges it so every shape unit lands exactly on its cell's center
    // (averaging the correction if cells disagree slightly due to prefab
    // pivot offsets). Returns the resulting (deduplicated) footprint cells.
    private static bool TryComputeFootprint(GameObject instance, CardData card, Vector3 anchorWorld, List<Vector2Int> cells, out Vector3 snappedPosition)
    {
        cells.Clear();
        snappedPosition = anchorWorld;

        var gm = GridManager.Instance;
        if (gm == null) return false;

        instance.transform.position = anchorWorld;
        var positions = GetShapeWorldPositions(instance, card);
        if (positions.Count == 0) return false;

        Vector3 correction = Vector3.zero;
        foreach (var p in positions)
        {
            // Unclamped: a marker sitting past the board edge must resolve
            // to an actual out-of-bounds cell (which IsFootprintBuildable
            // will then reject) rather than being silently snapped onto the
            // nearest edge cell and treated as a valid placement.
            Vector2Int cell = gm.WorldToCellUnclamped(p);
            if (!cells.Contains(cell))
                cells.Add(cell);

            Vector3 cellCenter = gm.CellToWorld(cell.x, cell.y);
            correction += new Vector3(cellCenter.x - p.x, 0f, cellCenter.z - p.z);
        }
        correction /= positions.Count;

        snappedPosition = anchorWorld + correction;
        return true;
    }
    PlacedBuilding FindExactOccupant(List<Vector2Int> cells)
    {
        if (cells == null || cells.Count == 0) return null;
        for (int i = 0; i < placedBuildings.Count; i++)
        {
            var b = placedBuildings[i];
            if (b == null || b.cells == null || b.cells.Count != cells.Count)
                continue;
            bool match = true;
            for (int c = 0; c < cells.Count; c++)
            {
                if (!b.cells.Contains(cells[c]))
                {
                    match = false;
                    break;
                }
            }
            if (match) return b;
        }
        return null;
    }

    bool TryGetPlacement(out PlacedBuilding stackOnto)
    {
        stackOnto = null;
        var gm = GridManager.Instance;
        if (gm == null || selectedCard == null) return false;

        if (gm.IsFootprintBuildable(previewCells))
            return true;

        if (!selectedCard.stackable) return false;

        var occupant = FindExactOccupant(previewCells);
        if (occupant == null || !occupant.CanStack(selectedCard))
            return false;

        stackOnto = occupant;
        return true;
    }

    private void Update()
    {
        if (selectedIndex < 0 || selectedCard == null || previewInstance == null) return;
        if (gameController != null && !gameController.IsBetweenWaves)
        {
            handManager?.Deselect();
            CancelPreview();
            return;
        }

        if (!TryGetMouseWorldPoint(out Vector3 mouseWorld))
        {
            previewInstance.SetActive(false);
            return;
        }

        previewInstance.SetActive(true);
        Vector3 anchor = mouseWorld + Vector3.up * yOffset;
        if (!TryComputeFootprint(previewInstance, selectedCard, anchor, previewCells, out Vector3 snapped))
        {
            previewInstance.SetActive(false);
            return;
        }

        float surfaceY = GridManager.Instance.CellToWorld(previewCells[0].x, previewCells[0].y).y;
        previewInstance.transform.position = snapped;
        SnapToCellSurface(previewInstance, surfaceY, selectedCard.yOffset);

        previewValid = TryGetPlacement(out PlacedBuilding stackOnto);
        ApplyPreviewMaterial(previewValid);
        ApplyStackHover(stackOnto);

        if (stackOnto != null)
        {
            var pos = previewInstance.transform.position;
            pos.y = stackOnto.transform.position.y + yOffset;
            previewInstance.transform.position = pos;
        }
    }

    private void ApplyPreviewMaterial(bool valid)
    {
        if (previewRenderers == null) return;
        Material mat = valid ? previewValidMaterial : previewInvalidMaterial;
        if (mat == null) return;
        foreach (var r in previewRenderers)
            r.sharedMaterial = mat;
    }
    void ClearStackHover()
    {
        if (hoveredRenderers != null && hoveredOriginalMats != null)
        {
            for (int i = 0; i < hoveredRenderers.Length; i++)
            {
                if (hoveredRenderers[i] != null)
                    hoveredRenderers[i].sharedMaterials = hoveredOriginalMats[i];
            }
        }
        hoveredStack = null;
        hoveredRenderers = null;
        hoveredOriginalMats = null;
    }

    void ApplyStackHover(PlacedBuilding target)
    {
        if (target == hoveredStack) return;
        ClearStackHover();
        if (target == null || stackHoverMaterial == null) return;

        hoveredStack = target;
        hoveredRenderers = target.GetComponentsInChildren<Renderer>();
        hoveredOriginalMats = new Material[hoveredRenderers.Length][];
        for (int i = 0; i < hoveredRenderers.Length; i++)
        {
            hoveredOriginalMats[i] = hoveredRenderers[i].sharedMaterials;
            var swapped = new Material[hoveredOriginalMats[i].Length];
            for (int m = 0; m < swapped.Length; m++)
                swapped[m] = stackHoverMaterial;
            hoveredRenderers[i].sharedMaterials = swapped;
        }
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = default;
        if (mousePosition == null) return false;

        Vector2 screenPos = mousePosition.action.ReadValue<Vector2>();
        Camera cam = Camera.main;
        if (cam == null) return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, groundMask)) return false;

        worldPoint = hit.point;
        return true;
    }

    private void OnLeftClick(InputAction.CallbackContext context)
    {
        if (selectedIndex < 0 || selectedCard == null || previewInstance == null) return;
        if (gameController != null && !gameController.IsBetweenWaves) return;
        if (!TryGetPlacement(out PlacedBuilding stackOnto)) return;

        var gm = GridManager.Instance;
        var card = selectedCard;
        if (gm == null) return;

        if (stackOnto != null)
        {
            stackOnto.AddStack(card);
            ClearStackHover();
        }
        else
        {
            Vector3 anchor = previewInstance.transform.position;
            Quaternion rot = previewInstance.transform.rotation;
            GameObject instance = Instantiate(card.prefab, anchor, rot);

            var cells = new List<Vector2Int>();
            if (!TryComputeFootprint(instance, card, anchor, cells, out Vector3 snapped)
                || !gm.IsFootprintBuildable(cells))
            {
                Destroy(instance);
                return;
            }

            instance.transform.position = snapped;
            var placed = instance.GetComponent<PlacedBuilding>();
            if (placed == null) placed = instance.AddComponent<PlacedBuilding>();
            placed.cells = cells;
            placed.sourceCard = card;
            placed.stackCount = 1;
            placedBuildings.Add(placed);

            var obstacle = instance.GetComponent<Obstacle>() ?? instance.GetComponentInChildren<Obstacle>();
            foreach (var cell in cells)
                gm.SetWalkable(cell.x, cell.y, false);
            if (obstacle != null)
                gm.RegisterObstacleCells(cells, obstacle);
            Obstacle.RaiseChanged();
        }

        handManager?.ConsumeSelectedCard();
        if (handManager == null || handManager.SelectedIndex == -1)
            CancelPreview();
    }
    static void SnapToCellSurface(GameObject instance, float surfaceY, float extraY)
    {
        var rends = instance.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0)
        {
            var p = instance.transform.position;
            instance.transform.position = new Vector3(p.x, surfaceY + extraY, p.z);
            return;
        }

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            if (rends[i] != null) b.Encapsulate(rends[i].bounds);

        float delta = (surfaceY + extraY) - b.min.y;
        instance.transform.position += Vector3.up * delta;
    }

    private void OnDemolish(InputAction.CallbackContext context)
    {
        if (gameController != null && !gameController.IsBetweenWaves) return;
        if (mousePosition == null || GridManager.Instance == null) return;

        Vector2 screenPos = mousePosition.action.ReadValue<Vector2>();
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;

        var placed = hit.collider.GetComponentInParent<PlacedBuilding>();
        if (placed == null) return;

        var gm = GridManager.Instance;
        var obstacle = placed.GetComponent<Obstacle>();
        foreach (var cell in placed.cells)
            gm.SetWalkable(cell.x, cell.y, true);
        if (placed.cells != null)
            gm.UnregisterObstacleCells(placed.cells, obstacle);

        placedBuildings.Remove(placed);
        Destroy(placed.gameObject);
        Obstacle.RaiseChanged();
    }
}

public interface IStackableBuilding
{
    bool CanAcceptStack(CardData incoming);
    void OnStacked(CardData incoming);
}