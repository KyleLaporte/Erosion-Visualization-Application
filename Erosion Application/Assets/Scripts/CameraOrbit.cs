using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    public Camera camera;
    public Transform target;
    private Vector3 previousPosition;
    void Update()
    {
        // Orbit around target based on touch position
        if (Input.touchCount > 0)
        {
            previousPosition = camera.ScreenToViewportPoint(Input.GetTouch(0).position);
        }

        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Moved)
        {
            Vector3 direction = previousPosition - camera.ScreenToViewportPoint(Input.GetTouch(0).position);
            camera.transform.position = target.position;
            
            camera.transform.Rotate(new Vector3(1,0,0), direction.y * 180);
            camera.transform.Rotate(new Vector3(0,1,0), -direction.x * 180, Space.World);
            camera.transform.Translate(new Vector3(0,0,-200));

            previousPosition = camera.ScreenToViewportPoint(Input.GetTouch(0).position);
        }
    }
}
