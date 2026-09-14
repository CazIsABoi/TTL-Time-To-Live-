using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class GridCell
{
    public Vector2Int coords;
    public Vector3 worldPosition;
    public bool walkable = true;

    public GridCell(int x, int y, Vector3 worldPos)
    {
        coords = new Vector2Int(x, y);
        worldPosition = worldPos;
        walkable = true;
    }
}
