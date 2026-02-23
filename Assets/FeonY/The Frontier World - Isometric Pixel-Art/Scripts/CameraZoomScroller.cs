using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeonY.TheFrontierWorldIsometricPixelArt 
{
public class CameraZoomScroller : MonoBehaviour
{
    public float zoomSpeed = 5.0f;
    public float minZoom = 1.0f;
    public float maxZoom = 10.0f;
    public float smoothTime = 0.2f; // Smooth time for zooming

    private Vector3 velocity = Vector3.zero;

    void Update()
    {
        // Check for mouse scroll input
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        // Zoom the camera
        ZoomCamera(scroll);
    }

    void ZoomCamera(float scroll)
    {
        // Calculate new zoom level
        float newZoom = transform.localPosition.z + scroll * zoomSpeed;

        // Clamp the zoom level within minZoom and maxZoom range
        newZoom = Mathf.Clamp(newZoom, minZoom, maxZoom);

        // Calculate target position based on new zoom level
        Vector3 targetPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, newZoom);

        // Smoothly move the camera towards the target position
        transform.localPosition = Vector3.SmoothDamp(transform.localPosition, targetPosition, ref velocity, smoothTime);
    }
}
}