using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeonY.TheFrontierWorldIsometricPixelArt 
{
public class MouseWheelPan : MonoBehaviour
{
    public float panSpeed = 3f; // Speed of pan movement

    private bool isPanning = false;
    private Vector3 lastMousePosition;

    void Update()
    {
        // Check if mouse wheel is pressed down
        if (Input.GetMouseButtonDown(2))
        {
            isPanning = true;
            lastMousePosition = Input.mousePosition;
        }

        // Check if mouse wheel is released
        if (Input.GetMouseButtonUp(2))
        {
            isPanning = false;
        }

        // If panning is active, move the camera
        if (isPanning)
        {
            Vector3 deltaMousePosition = Input.mousePosition - lastMousePosition;
            Vector3 move = new Vector3(deltaMousePosition.x, deltaMousePosition.y, 0) * panSpeed * Time.deltaTime;
            transform.Translate(-move);

            lastMousePosition = Input.mousePosition;
        }
    }
}
}