using UnityEngine;

public class Rotate : MonoBehaviour
{
    public float speed = 1f;
    public Vector3 vector = Vector3.right;

    private void Update()
    {
        transform.Rotate(vector * speed);
    } 
}
