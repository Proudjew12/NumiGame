using UnityEngine;

namespace NumiDream.Nomi
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class NomiChildTransformLock : MonoBehaviour
    {
        [Header("--------- Children ---------")]
        [Header("+Targets+")]
        [Space(4)]
        [InspectorName("Locked Children")]
        [SerializeField] private string[] lockedChildNames = { "Animation" };

        [Space(10)]
        [Header("--------- Modes ---------")]
        [Header("+Editor And Play+")]
        [Space(4)]
        [InspectorName("Lock In Edit")]
        [SerializeField] private bool lockInEditMode = true;
        [Space(4)]
        [InspectorName("Lock In Play")]
        [SerializeField] private bool lockInPlayMode = true;

        [Space(10)]
        [Header("--------- Transform Locks ---------")]
        [Header("+Channels+")]
        [Space(4)]
        [InspectorName("Position")]
        [SerializeField] private bool lockLocalPosition = true;
        [Space(4)]
        [InspectorName("Rotation")]
        [SerializeField] private bool lockLocalRotation = true;
        [Space(4)]
        [InspectorName("Scale")]
        [SerializeField] private bool lockLocalScale;

        [Header("+Values+")]
        [Space(4)]
        [InspectorName("Local Position")]
        [SerializeField] private Vector3 lockedLocalPosition = Vector3.zero;
        [Space(4)]
        [InspectorName("Local Euler")]
        [SerializeField] private Vector3 lockedLocalEulerAngles = Vector3.zero;
        [Space(4)]
        [InspectorName("Local Scale")]
        [SerializeField] private Vector3 lockedLocalScale = Vector3.one;

        private void Reset()
        {
            SnapLockedChildren();
        }

        private void OnValidate()
        {
            SnapLockedChildren();
        }

        private void LateUpdate()
        {
            if (Application.isPlaying)
            {
                if (lockInPlayMode)
                {
                    SnapLockedChildren();
                }

                return;
            }

            if (lockInEditMode)
            {
                SnapLockedChildren();
            }
        }

        [ContextMenu("Snap Locked Children Now")]
        public void SnapLockedChildren()
        {
            if (lockedChildNames == null)
            {
                return;
            }

            foreach (var childName in lockedChildNames)
            {
                if (string.IsNullOrWhiteSpace(childName))
                {
                    continue;
                }

                var child = transform.Find(childName);
                if (child == null)
                {
                    continue;
                }

                if (lockLocalPosition)
                {
                    child.localPosition = lockedLocalPosition;
                }

                if (lockLocalRotation)
                {
                    child.localRotation = Quaternion.Euler(lockedLocalEulerAngles);
                }

                if (lockLocalScale)
                {
                    child.localScale = lockedLocalScale;
                }
            }
        }
    }
}
