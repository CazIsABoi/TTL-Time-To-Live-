using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GridAgent))]
public class PathVisualizer : MonoBehaviour
{
    public Color lineColor = Color.yellow;
    public float lineWidth = 1f;
    public bool showPath = true;

    private GridAgent agent;
    private LineRenderer lr;

    private void Awake()
    {
        agent = GetComponent<GridAgent>();
        lr = gameObject.GetComponent<LineRenderer>();
        if (lr == null) lr = gameObject.AddComponent<LineRenderer>();
        lr.material = MakeMat(lineColor);
        lr.widthMultiplier = lineWidth;
        lr.positionCount = 0;
        lr.loop = false;
        lr.startColor = lr.endColor = lineColor;
        lr.useWorldSpace = true;
    }

    private static Material MakeMat(Color c)
    {
        Shader s =
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("HDRP/Unlit") ??
            Shader.Find("Sprites/Default");

        if (s == null) s = Shader.Find("Standard");
        var mat = new Material(s);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        else if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        else mat.color = c;
        return mat;
    }

    private void Update()
    {
        if (!showPath || agent == null)
        {
            lr.positionCount = 0;
            return;
        }

        var path = agent.GetPathWorld();
        if (path == null || path.Count == 0)
        {
            lr.positionCount = 0;
            return;
        }

        lr.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
            lr.SetPosition(i, new Vector3(path[i].x, path[i].y + 0.1f, path[i].z));
    }
}
