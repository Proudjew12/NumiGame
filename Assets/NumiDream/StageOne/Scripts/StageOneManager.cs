using UnityEngine;

namespace NumiDream.StageOne
{
    [DisallowMultipleComponent]
    public sealed class StageOneManager : MonoBehaviour
    {
        [Header("--------- References ---------")]
        [Header("+Stage+")]
        [Space(4)]
        [InspectorName("Screen Shake")]
        [SerializeField] private StageScreenShake screenShake;

        public bool FinalBridgeSolved { get; private set; }

        private void Awake()
        {
            if (screenShake == null)
            {
                screenShake = FindFirstObjectByType<StageScreenShake>();
            }
        }

        public void MarkFinalBridgeSolved()
        {
            FinalBridgeSolved = true;

            if (screenShake != null)
            {
                screenShake.StopShake();
            }
        }
    }
}
