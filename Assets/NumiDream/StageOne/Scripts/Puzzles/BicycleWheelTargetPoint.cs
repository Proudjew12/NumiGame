using UnityEngine;

namespace NumiDream.StageOne.Puzzles
{
    [DisallowMultipleComponent]
    [AddComponentMenu("NumiDream/Stage One/Bicycle Wheel Target Point")]
    public sealed class BicycleWheelTargetPoint : MonoBehaviour
    {
        [Header("--------- Gizmo ---------")]
        [Header("+Display+")]
        [Space(4)]
        [InspectorName("Radius")]
        [SerializeField] private float gizmoRadius = 0.35f;
        [Space(4)]
        [InspectorName("Color")]
        [SerializeField] private Color gizmoColor = new Color(0.25f, 1f, 0.45f, 0.9f);

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);
            Gizmos.DrawLine(transform.position + Vector3.left * gizmoRadius, transform.position + Vector3.right * gizmoRadius);
            Gizmos.DrawLine(transform.position + Vector3.down * gizmoRadius, transform.position + Vector3.up * gizmoRadius);
        }
    }
}
