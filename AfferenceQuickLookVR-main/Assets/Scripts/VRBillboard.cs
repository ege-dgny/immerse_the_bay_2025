using UnityEngine;

public class VRBillboard : MonoBehaviour
{
    [Header("Billboard Settings")]
    [Tooltip("The camera to face (if null, uses Camera.main)")]
    public Camera targetCamera;

    [Tooltip("If true, only rotates on Y axis (useful for UI panels)")]
    public bool lockYAxis = false;

    [Tooltip("If true, rotates to face away from camera (backwards)")]
    public bool faceAway = false;

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Camera>();
            }
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;

        Vector3 direction;

        if (faceAway)
        {
            direction = transform.position - targetCamera.transform.position;
        }
        else
        {
            direction = targetCamera.transform.position - transform.position;
        }

        if (lockYAxis)
        {
            direction.y = 0;
        }

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
