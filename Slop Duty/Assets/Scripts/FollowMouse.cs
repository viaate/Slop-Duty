using UnityEngine;

public class FollowMouse : MonoBehaviour
{
    private Camera cam;
    private float zDepth;

    void Start()
    {
        cam = Camera.main;
        zDepth = transform.position.z; // keep whatever z/sorting depth this object was placed at
    }

    void Update()
    {
        Vector3 mouseScreenPos = Input.mousePosition;

        // Input.mousePosition.z is always 0, which ScreenToWorldPoint treats as
        // "right at the camera lens." You need to tell it how far from the camera
        // to project the point, otherwise you get garbage/near-clip coordinates.
        mouseScreenPos.z = zDepth - cam.transform.position.z;

        Vector3 worldPos = cam.ScreenToWorldPoint(mouseScreenPos);
        worldPos.z = zDepth;

        transform.position = worldPos;
    }
}
