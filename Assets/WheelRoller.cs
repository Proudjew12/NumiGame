using UnityEngine;

public class WheelRoller : MonoBehaviour
{
    public float torqueForce = 20f;
    public Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        rb.AddTorque(-torqueForce);
    }
}