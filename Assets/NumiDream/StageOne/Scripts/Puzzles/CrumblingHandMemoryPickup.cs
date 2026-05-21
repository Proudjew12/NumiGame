using System.Collections;
using UnityEngine;

namespace NumiDream.StageOne.Puzzles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class CrumblingHandMemoryPickup : MonoBehaviour
    {
        [Header("--------- Trigger ---------")]
        [Header("+Detection+")]
        [Space(4)]
        [InspectorName("Player Tag")]
        [SerializeField] private string playerTag = "Player";

        [Space(10)]
        [Header("--------- Pickup ---------")]
        [Header("+Visual+")]
        [Space(4)]
        [InspectorName("Fade Out Time")]
        [SerializeField] private float fadeOutDuration = 0.25f;

        [Space(10)]
        [Header("--------- Hand Collapse ---------")]
        [Header("+References+")]
        [Space(4)]
        [InspectorName("Bridge Collapse")]
        [SerializeField] private CrumblingHandBridgeCollapse bridgeCollapse;
        [Space(4)]
        [InspectorName("Pieces To Drop")]
        [SerializeField] private string[] piecesToDrop;
        [Space(4)]
        [InspectorName("Disable Colliders")]
        [SerializeField] private Collider2D[] collidersToDisableOnPickup;

        [Header("+Timing+")]
        [Space(4)]
        [InspectorName("Collapse Delay")]
        [SerializeField] private float collapseStartDelay = 0.16f;
        [Space(4)]
        [InspectorName("Piece Stagger")]
        [SerializeField] private float collapsePieceStagger = 0.08f;
        [Space(4)]
        [InspectorName("Collider Delay")]
        [SerializeField] private float colliderDisableDelay = 4.45f;

        [Space(10)]
        [Header("--------- Ground Shake ---------")]
        [Header("+Feel+")]
        [Space(4)]
        [InspectorName("Screen Shake")]
        [SerializeField] private StageScreenShake screenShake;
        [Space(4)]
        [InspectorName("Shake Time")]
        [SerializeField] private float shakeDuration = 1.15f;
        [Space(4)]
        [InspectorName("Shake Intensity")]
        [SerializeField] private float shakeIntensityMultiplier = 6f;
        [Space(4)]
        [InspectorName("Shake Frequency")]
        [SerializeField] private float shakeFrequencyMultiplier = 3.6f;

        private bool _collected;
        private Collider2D _triggerCollider;

        private void Reset()
        {
            _triggerCollider = GetComponent<Collider2D>();
            _triggerCollider.isTrigger = true;
        }

        private void Awake()
        {
            FindReferences();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_collected || !other.CompareTag(playerTag))
            {
                return;
            }

            StartCoroutine(CollectRoutine());
        }

        private IEnumerator CollectRoutine()
        {
            _collected = true;

            if (_triggerCollider != null)
            {
                _triggerCollider.enabled = false;
            }

            yield return FadeOut();

            var disableColliderRoutine = collidersToDisableOnPickup != null && collidersToDisableOnPickup.Length > 0
                ? StartCoroutine(DisablePickupCollidersAfterDelay())
                : null;

            if (screenShake != null)
            {
                screenShake.PlayBurst(shakeDuration, shakeIntensityMultiplier, shakeFrequencyMultiplier);
            }

            if (bridgeCollapse != null)
            {
                yield return bridgeCollapse.CollapsePiecesSequentially(
                    piecesToDrop,
                    collapseStartDelay,
                    collapsePieceStagger,
                    false);
            }

            if (disableColliderRoutine != null)
            {
                yield return disableColliderRoutine;
            }

            gameObject.SetActive(false);
        }

        private IEnumerator DisablePickupCollidersAfterDelay()
        {
            if (colliderDisableDelay > 0f)
            {
                yield return new WaitForSeconds(colliderDisableDelay);
            }

            DisablePickupColliders();
        }

        private void DisablePickupColliders()
        {
            if (collidersToDisableOnPickup == null)
            {
                return;
            }

            for (var i = 0; i < collidersToDisableOnPickup.Length; i++)
            {
                if (collidersToDisableOnPickup[i] != null)
                {
                    collidersToDisableOnPickup[i].enabled = false;
                }
            }
        }

        private IEnumerator FadeOut()
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (fadeOutDuration <= 0f || renderers.Length == 0)
            {
                yield break;
            }

            var colors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                colors[i] = renderers[i].color;
            }

            var elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / fadeOutDuration);
                t = t * t * (3f - 2f * t);

                for (var i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null)
                    {
                        continue;
                    }

                    var color = colors[i];
                    color.a = Mathf.Lerp(colors[i].a, 0f, t);
                    renderers[i].color = color;
                }

                yield return null;
            }
        }

        private void FindReferences()
        {
            if (_triggerCollider == null)
            {
                _triggerCollider = GetComponent<Collider2D>();
                _triggerCollider.isTrigger = true;
            }

            if (bridgeCollapse == null)
            {
#if UNITY_2023_1_OR_NEWER
                bridgeCollapse = FindFirstObjectByType<CrumblingHandBridgeCollapse>();
#else
                bridgeCollapse = FindObjectOfType<CrumblingHandBridgeCollapse>();
#endif
            }

            if (screenShake == null)
            {
#if UNITY_2023_1_OR_NEWER
                screenShake = FindFirstObjectByType<StageScreenShake>();
#else
                screenShake = FindObjectOfType<StageScreenShake>();
#endif
            }
        }
    }
}
