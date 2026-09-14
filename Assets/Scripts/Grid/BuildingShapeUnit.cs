using UnityEngine;

/// <summary>
/// Optional marker for a prefab's true visual/footprint center. Place an
/// empty child GameObject (e.g. named "Center") wherever the artist intends
/// the object to actually be centered, and add this component to it.
///
/// Placement code (GridBuildPlacer, ExpandingIsland) looks for this marker
/// first when it needs to align a prefab to a grid point. If present, its
/// world position is used directly instead of guessing from Renderer bounds
/// or collider centers - which is more explicit and doesn't break for
/// multi-submesh meshes, decorative overhangs, or mismatched colliders.
///
/// If a prefab has no BuildingShapeUnit, callers fall back to Renderer
/// bounds so existing prefabs keep working without changes.
/// </summary>
public class BuildingShapeUnit : MonoBehaviour
{
    [Tooltip("Draw a gizmo at this marker's position so it's easy to see/place in the Scene view.")]
    public bool drawGizmo = true;
    public float gizmoRadius = 0.15f;
    public Color gizmoColor = Color.cyan;

    /// <summary>
    /// Looks for a BuildingShapeUnit anywhere under (or on) root and, if
    /// found, returns its world position as the object's true center.
    /// </summary>
    public static bool TryGetCenter(GameObject root, out Vector3 center)
    {
        if (root == null)
        {
            center = default;
            return false;
        }

        var marker = root.GetComponentInChildren<BuildingShapeUnit>();
        if (marker == null)
        {
            center = default;
            return false;
        }

        center = marker.transform.position;
        return true;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmo) return;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, gizmoRadius);
        Gizmos.DrawLine(transform.position + Vector3.up * gizmoRadius * 2f, transform.position - Vector3.up * gizmoRadius * 2f);
    }
}
