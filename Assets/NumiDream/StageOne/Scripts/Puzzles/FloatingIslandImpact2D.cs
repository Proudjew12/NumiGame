using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NumiDream.StageOne.Puzzles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class FloatingIslandImpact2D : MonoBehaviour
    {
        [Header("--------- Player ---------")]
        [Header("+Detection+")]
        [Space(4)]
        [InspectorName("Player Tag")]
        [SerializeField] private string playerTag = "Player";
        [Space(4)]
        [InspectorName("Top Contact")]
        [SerializeField] private bool requireTopContact = true;
        [Space(4)]
        [InspectorName("Min Landing Speed")]
        [SerializeField] private float minLandingSpeed = 1.2f;
        [Space(4)]
        [InspectorName("Top Tolerance")]
        [SerializeField] private float topContactTolerance = 0.18f;
        [Space(4)]
        [InspectorName("Reset Up Speed")]
        [SerializeField] private float resetUpwardSpeed = 0.2f;

        [Space(10)]
        [Header("--------- Impact ---------")]
        [Header("+Motion+")]
        [Space(4)]
        [InspectorName("Down Distance")]
        [SerializeField] private float pressDistance = 0.35f;
        [Space(4)]
        [InspectorName("Down Time")]
        [SerializeField] private float pressTime = 0.32f;
        [Space(4)]
        [InspectorName("Bounce Time")]
        [SerializeField] private float settleTime = 0.55f;
        [Space(4)]
        [InspectorName("Bounce Height")]
        [SerializeField] private float settleLift = 0.1f;
        [Space(4)]
        [InspectorName("Return Time")]
        [SerializeField] private float returnTime = 0.48f;
        [Space(4)]
        [InspectorName("Cooldown")]
        [SerializeField] private float retriggerCooldown = 0.12f;

        [Space(10)]
        [Header("--------- References ---------")]
        [Header("+Components+")]
        [Space(4)]
        [SerializeField] private Rigidbody2D body;

        private Collider2D _collider;
        private Vector3 _startLocalPosition;
        private Coroutine _impactRoutine;
        private float _nextImpactTime;
        private bool _readyForLandingImpact = true;
        private readonly HashSet<Collider2D> _playerContacts = new HashSet<Collider2D>();

        private void Reset()
        {
            FindReferences();
            ConfigureBody();
        }

        private void Awake()
        {
            FindReferences();
            ConfigureBody();
            _startLocalPosition = transform.localPosition;
        }

        private void OnDisable()
        {
            if (_impactRoutine != null)
            {
                StopCoroutine(_impactRoutine);
                _impactRoutine = null;
            }

            _playerContacts.Clear();
            MoveToLocalPosition(_startLocalPosition);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            RegisterPlayerContact(collision);
            TryTriggerImpact(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (_readyForLandingImpact)
            {
                TryTriggerImpact(collision);
            }
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            UnregisterPlayerContact(collision);

            if (_playerContacts.Count == 0 && _impactRoutine == null)
            {
                _readyForLandingImpact = true;
            }
        }

        private void TryTriggerImpact(Collision2D collision)
        {
            if (!_readyForLandingImpact || Time.time < _nextImpactTime || !IsValidLanding(collision))
            {
                return;
            }

            _readyForLandingImpact = false;
            _nextImpactTime = Time.time + retriggerCooldown;

            if (_impactRoutine != null)
            {
                StopCoroutine(_impactRoutine);
            }

            _impactRoutine = StartCoroutine(ImpactRoutine());
        }

        private bool IsValidLanding(Collision2D collision)
        {
            if (!TryGetPlayerBody(collision, out var playerBody))
            {
                return false;
            }

            if (!HasLandingVelocity(collision, playerBody))
            {
                return false;
            }

            return !requireTopContact || HasTopContact(collision, playerBody);
        }

        private bool HasLandingVelocity(Collision2D collision, Rigidbody2D playerBody)
        {
            if (minLandingSpeed <= 0f)
            {
                return true;
            }

            if (playerBody.linearVelocity.y > resetUpwardSpeed)
            {
                return false;
            }

            var fallingFastEnough = playerBody.linearVelocity.y <= -minLandingSpeed;
            var verticalImpactFastEnough = Mathf.Abs(collision.relativeVelocity.y) >= minLandingSpeed;

            return fallingFastEnough || verticalImpactFastEnough;
        }

        private bool TryGetPlayerBody(Collision2D collision, out Rigidbody2D playerBody)
        {
            playerBody = null;

            if (collision.collider != null && IsPlayerObject(collision.collider.gameObject))
            {
                playerBody = collision.collider.attachedRigidbody;
            }

            if (playerBody == null && collision.otherCollider != null && IsPlayerObject(collision.otherCollider.gameObject))
            {
                playerBody = collision.otherCollider.attachedRigidbody;
            }

            if (playerBody == null && collision.rigidbody != null && IsPlayerObject(collision.rigidbody.gameObject))
            {
                playerBody = collision.rigidbody;
            }

            return playerBody != null;
        }

        private bool IsPlayerObject(GameObject candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            return HasPlayerTag(candidate) || HasPlayerTag(candidate.transform.root.gameObject);
        }

        private bool HasPlayerTag(GameObject candidate)
        {
            return candidate != null && !string.IsNullOrWhiteSpace(playerTag) && candidate.CompareTag(playerTag);
        }

        private bool HasTopContact(Collision2D collision, Rigidbody2D playerBody)
        {
            if (_collider == null)
            {
                return playerBody.position.y >= transform.position.y;
            }

            var ownBounds = _collider.bounds;
            var topContactY = ownBounds.center.y - Mathf.Max(0f, topContactTolerance);

            for (var i = 0; i < collision.contactCount; i++)
            {
                var contact = collision.GetContact(i);

                if (contact.point.y >= topContactY && playerBody.worldCenterOfMass.y >= ownBounds.center.y)
                {
                    return true;
                }
            }

            return playerBody.worldCenterOfMass.y >= ownBounds.center.y;
        }

        private IEnumerator ImpactRoutine()
        {
            var fromPosition = transform.localPosition;

            yield return DownBounceRoutine(fromPosition);

            MoveToLocalPosition(_startLocalPosition);
            _impactRoutine = null;
            _readyForLandingImpact = _playerContacts.Count == 0;
        }

        private void RegisterPlayerContact(Collision2D collision)
        {
            if (TryGetPlayerCollider(collision, out var playerCollider))
            {
                _playerContacts.Add(playerCollider);
            }
        }

        private void UnregisterPlayerContact(Collision2D collision)
        {
            if (TryGetPlayerCollider(collision, out var playerCollider))
            {
                _playerContacts.Remove(playerCollider);
            }
        }

        private bool TryGetPlayerCollider(Collision2D collision, out Collider2D playerCollider)
        {
            playerCollider = null;

            if (collision.collider != null && IsPlayerObject(collision.collider.gameObject))
            {
                playerCollider = collision.collider;
                return true;
            }

            if (collision.otherCollider != null && IsPlayerObject(collision.otherCollider.gameObject))
            {
                playerCollider = collision.otherCollider;
                return true;
            }

            return false;
        }

        private IEnumerator DownBounceRoutine(Vector3 startPosition)
        {
            var distance = Mathf.Max(0f, pressDistance);
            if (distance <= 0.001f)
            {
                MoveToLocalPosition(_startLocalPosition);
                yield break;
            }

            var currentDepth = Mathf.Max(0f, _startLocalPosition.y - startPosition.y);
            var lift = Mathf.Min(Mathf.Max(0f, settleLift), distance * 0.75f);
            var reboundDepth = Mathf.Max(0f, distance - lift);
            var settleDepth = Mathf.Lerp(reboundDepth, distance, 0.45f);

            yield return MoveDepthRoutine(currentDepth, distance, pressTime);
            yield return MoveDepthRoutine(distance, reboundDepth, settleTime * 0.55f);
            yield return MoveDepthRoutine(reboundDepth, settleDepth, settleTime * 0.45f);
            yield return MoveDepthRoutine(settleDepth, 0f, returnTime);

            MoveToLocalPosition(_startLocalPosition);
        }

        private IEnumerator MoveDepthRoutine(float fromDepth, float toDepth, float duration)
        {
            var safeDuration = Mathf.Max(0.01f, duration);
            var elapsed = 0f;

            while (elapsed < safeDuration)
            {
                elapsed += Time.fixedDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var depth = Mathf.Max(0f, Mathf.LerpUnclamped(fromDepth, toDepth, SmootherStep(t)));
                var position = _startLocalPosition + Vector3.down * depth;

                MoveToLocalPosition(position);
                yield return new WaitForFixedUpdate();
            }

            MoveToLocalPosition(_startLocalPosition + Vector3.down * Mathf.Max(0f, toDepth));
        }

        private void MoveToLocalPosition(Vector3 localPosition)
        {
            if (body != null && body.bodyType == RigidbodyType2D.Kinematic && body.simulated)
            {
                var worldPosition = transform.parent != null ? transform.parent.TransformPoint(localPosition) : localPosition;
                body.MovePosition(worldPosition);
                return;
            }

            transform.localPosition = localPosition;
        }

        private void FindReferences()
        {
            if (_collider == null)
            {
                _collider = GetComponent<Collider2D>();
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }
        }

        private void ConfigureBody()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody2D>();
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void OnValidate()
        {
            minLandingSpeed = Mathf.Max(0f, minLandingSpeed);
            topContactTolerance = Mathf.Max(0f, topContactTolerance);
            resetUpwardSpeed = Mathf.Max(0f, resetUpwardSpeed);
            pressDistance = Mathf.Max(0f, pressDistance);
            pressTime = Mathf.Max(0.01f, pressTime);
            settleTime = Mathf.Max(0f, settleTime);
            settleLift = Mathf.Max(0f, settleLift);
            returnTime = Mathf.Max(0.01f, returnTime);
            retriggerCooldown = Mathf.Max(0f, retriggerCooldown);
        }

        private static float SmoothStep(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static float SmootherStep(float value)
        {
            return value * value * value * (value * (value * 6f - 15f) + 10f);
        }
    }
}
