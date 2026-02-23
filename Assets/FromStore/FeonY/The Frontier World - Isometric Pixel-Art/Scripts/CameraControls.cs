using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FeonY.TheFrontierWorldIsometricPixelArt 
{
public class CameraControls : MonoBehaviour
{
    public float speed = 3f; // Speed of camera movement
    public float smoothTime = 0.2f; // Smooth time for camera movement
    private Vector3 velocity = Vector3.zero;

    void Update()
    {
        // Get input from WASD keys
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        // Calculate target position based on input
        Vector3 targetPosition = transform.position + new Vector3(horizontalInput, verticalInput, 0) * speed * Time.deltaTime;
    
        // Smoothly move the camera towards the target position
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);
    }
}
}