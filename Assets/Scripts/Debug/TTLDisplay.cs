using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(WorldSpaceUIBillboard))]
public class TTLDisplay : MonoBehaviour
{
    private Canvas canvas;
    private Text label;

    private void Awake()
    {
        // create world-space canvas
        var go = new GameObject("TTLUI");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 50);

        var textGO = new GameObject("TTLText");
        textGO.transform.SetParent(go.transform, false);
        var txtRect = textGO.AddComponent<RectTransform>();
        txtRect.anchorMin = new Vector2(0, 0);
        txtRect.anchorMax = new Vector2(1, 1);
        txtRect.sizeDelta = Vector2.zero;

        label = textGO.AddComponent<Text>();
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 24;
        label.color = Color.white;
        // try default font
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        // add billboard to parent (WorldSpaceUIBillboard requirement)
        var bb = GetComponent<WorldSpaceUIBillboard>();
        if (bb == null) bb = gameObject.AddComponent<WorldSpaceUIBillboard>();
    }

    public void SetText(string text)
    {
        if (label != null)
            label.text = text;
    }
}
