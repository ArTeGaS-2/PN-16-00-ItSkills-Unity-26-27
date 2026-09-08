using UnityEngine;

public class Move : MonoBehaviour
{
    public float speed = 5;
    public float rotateSpeed = 120;

    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(0, 0, speed);
        }
        if (Input.GetKey(KeyCode.S))
        {
            transform.Translate(0, 0, -speed);
        }
        if (Input.GetKey(KeyCode.A))
        {
            transform.Translate(-speed, 0, 0);
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.Translate(speed, 0, 0);
        }
    }
}
