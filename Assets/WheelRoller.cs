using UnityEngine;

public class WheelRoller : MonoBehaviour
{
   
    void Start()
    {
       
    }

     private void Update()  
    {
       
    }


   private void OnTriggerEnter2D(Collider2D other) {
    if (other.gameObject.tag == "WheelGround") {
        gameObject.tag = "Ground";
        gameObject.layer = LayerMask.NameToLayer("Ground");
    }
}
}