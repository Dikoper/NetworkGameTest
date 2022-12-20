using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] Vector3 offset;
    [SerializeField] float distance = 2;
    [SerializeField] float vLimit = 45;
    [SerializeField] float sense = 100;

    public Transform target;
    public Vector2 rotation = Vector2.zero;
    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void LateUpdate()
    {
        rotation.x += Input.GetAxis("Mouse X") * sense * Time.deltaTime;
        rotation.y += Input.GetAxis("Mouse Y") * sense * Time.deltaTime;
        rotation.y = Mathf.Clamp(rotation.y, -vLimit, vLimit);

        var r = Quaternion.Euler(-rotation.y,rotation.x,0);
        if (target != null)
        {
            transform.position = target.position + offset + r * new Vector3(0, 0, -distance);
            transform.LookAt(target.position + offset);
        }
    }
}
