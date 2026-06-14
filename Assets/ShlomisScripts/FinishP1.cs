using UnityEngine;

public class FinishP1 : MonoBehaviour
{
    [Header("Target Object")]
    [SerializeField] private SpriteOutline targetOutline;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (targetOutline == null) return;

        targetOutline.LockOutlineOff();
    }
}