using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using System.Collections;

public class InteractableManipulator : MonoBehaviour
{
    [Header("Camera Reference")]
    [SerializeField] private CinemachineCamera playerCamera;

    [Header("Move")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private InputActionReference moveAction;

    [Header("Scale")]
    [SerializeField] private float scaleSpeed = 1f;
    [SerializeField] private float minScale = 0.2f;
    [SerializeField] private float maxScale = 5f;
    [SerializeField] private InputActionReference scaleUpAction;
    [SerializeField] private InputActionReference scaleDownAction;

    [Header("Snap Back")]
    [SerializeField] private float maxDistance = 8f;
    [SerializeField] private float snapSpeed = 8f;
    [SerializeField] private bool instantSnap = false;

    public Transform originPoint;

    private bool _isSnappingBack = false;
    private Rigidbody2D _rb;

    private bool IsControlled => playerCamera != null && playerCamera.Follow == transform;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    public void OnFocused()
    {
        if (_rb == null) return;
        _rb.gravityScale = 0f;
        _rb.linearVelocity = Vector2.zero;
    }

    public void OnFocusReleased()
    {
        if (_rb == null) return;
        _rb.gravityScale = 1f;
        _rb.linearVelocity = Vector2.zero;
    }

    private void Update()
    {
        if (originPoint != null && !_isSnappingBack)
        {
            float distance = Vector3.Distance(transform.position, originPoint.position);

            if (distance > maxDistance)
            {
                _isSnappingBack = true;
                if (_rb != null)
                {
                    _rb.gravityScale = 0f;
                    _rb.linearVelocity = Vector2.zero;
                }
            }
        }

        if (_isSnappingBack)
        {
            SnapBack();
            return;
        }

        if (!IsControlled) return;

        // Movement
        Vector2 input = moveAction != null
            ? moveAction.action.ReadValue<Vector2>()
            : Vector2.zero;

        transform.position += (Vector3)(input * moveSpeed * Time.deltaTime);

        // Scale
        float scaleDelta = 0f;

        if (scaleUpAction != null && scaleUpAction.action.IsPressed())
            scaleDelta += scaleSpeed * Time.deltaTime;

        if (scaleDownAction != null && scaleDownAction.action.IsPressed())
            scaleDelta -= scaleSpeed * Time.deltaTime;

        if (scaleDelta != 0f)
        {
            float next = Mathf.Clamp(transform.localScale.x + scaleDelta, minScale, maxScale);
            transform.localScale = new Vector3(next, next, next);
        }
    }

    private void SnapBack()
    {
        if (originPoint == null)
        {
            _isSnappingBack = false;
            return;
        }

        if (instantSnap)
        {
            transform.position = originPoint.position;
            transform.localScale = Vector3.one;
            _isSnappingBack = false;
            if (_rb != null && !IsControlled)
            {
                _rb.gravityScale = 1f;
                _rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        transform.position = Vector3.Lerp(
            transform.position,
            originPoint.position,
            snapSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, originPoint.position) < 0.01f)
        {
            transform.position = originPoint.position;
            transform.localScale = Vector3.one;
            _isSnappingBack = false;
            StartCoroutine(FlashInstantSnap());
        }
    }

    private IEnumerator FlashInstantSnap()
    {
        instantSnap = true;
        if (_rb != null && !IsControlled)
        {
            _rb.gravityScale = 1f;
            _rb.linearVelocity = Vector2.zero;
        }
        yield return new WaitForEndOfFrame();
        instantSnap = false;
    }

private void OnCollisionEnter2D(Collision2D col)
{
    if (col.gameObject.CompareTag("Player"))
    {
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePosition;
    }
}

private void OnCollisionExit2D(Collision2D col)
{
    if (col.gameObject.CompareTag("Player"))
    {
        _rb.constraints = RigidbodyConstraints2D.None;
    }
}
}