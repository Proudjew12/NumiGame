using UnityEngine;

public class WheelRoller : MonoBehaviour
{
    public float torqueForce = 20f;
    public Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

     private void Update() 
     {
        rb.AddTorque(-torqueForce);
    }


   private void OnTriggerEnter2D(Collider2D other) {
    if (other.gameObject.tag == "WheelGround") {
        gameObject.tag = "Ground";
        gameObject.layer = LayerMask.NameToLayer("Ground");
    }
}
}