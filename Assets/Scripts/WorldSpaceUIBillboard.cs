using UnityEngine;

public class WorldSpaceUIBillboard : MonoBehaviour
{
    public Camera targetCamera;
    public bool stayUpright = true;

    void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        if (stayUpright)
        {
            Vector3 toCamera = transform.position - targetCamera.transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(toCamera);
        }
        else
        {
            // Full look-at (can tilt when camera is above/below)
            transform.LookAt(targetCamera.transform);
        }
    }
}