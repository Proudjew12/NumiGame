using UnityEngine;

public class RisePlatAnimation : MonoBehaviour
{

    public Animator animator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    private void OnCollisionEnter2D(Collision2D other) {
        if(other.gameObject.tag == "Bell")
        {
            animator.SetTrigger("Rise");
        }
    }
}
