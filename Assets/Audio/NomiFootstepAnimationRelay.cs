using UnityEngine;

[DisallowMultipleComponent]
public sealed class NomiFootstepAnimationRelay : MonoBehaviour
{
    [SerializeField] private NomiFootstepAudio footstepAudio;

    private void Awake()
    {
        FindReferences();
    }

    private void Reset()
    {
        FindReferences();
    }

    public void footseps_animation()
    {
        FindReferences();
        footstepAudio?.PlayFootstepFromAnimation();
    }

    public void footseps_animation_force()
    {
        FindReferences();
        footstepAudio?.PlayFootstepFromAnimationForce();
    }

    private void FindReferences()
    {
        if (footstepAudio == null)
        {
            footstepAudio = GetComponentInParent<NomiFootstepAudio>();
        }
    }
}
