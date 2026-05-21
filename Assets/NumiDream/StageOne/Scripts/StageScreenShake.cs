using UnityEngine;

namespace NumiDream.StageOne
{
    [DisallowMultipleComponent]
    public sealed class StageScreenShake : MonoBehaviour
    {
        [Header("--------- Screen Shake ---------")]
        [Header("+Runtime+")]
        [Space(4)]
        [InspectorName("Enabled")]
        [SerializeField] private bool enableRuntimeShake;
        [Space(4)]
        [InspectorName("Active")]
        [SerializeField] private bool shakeActive = true;
        [Header("+Feel+")]
        [Space(4)]
        [SerializeField] private float intensity = 0.012f;
        [Space(4)]
        [SerializeField] private float frequency = 1.7f;
        [Space(4)]
        [InspectorName("Vertical Weight")]
        [SerializeField] private float verticalWeight = 0.65f;
        [Space(4)]
        [InspectorName("Fade Out Speed")]
        [SerializeField] private float fadeOutSpeed = 2f;

        private float _currentIntensity;
        private float _seed;
        private bool _burstRunning;
        private bool _burstPreviousEnabled;
        private bool _burstPreviousActive;
        private float _burstEndTime;
        private float _burstIntensityMultiplier = 1f;
        private float _burstFrequencyMultiplier = 1f;

        public Vector2 CurrentOffset { get; private set; }

        private void Awake()
        {
            _currentIntensity = IsShaking ? intensity : 0f;
            _seed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            if (_burstRunning && Time.time >= _burstEndTime)
            {
                enableRuntimeShake = _burstPreviousEnabled;
                shakeActive = _burstPreviousActive;
                _burstRunning = false;
            }

            var burstActive = _burstRunning && Time.time < _burstEndTime;
            var targetIntensity = IsShaking ? intensity : 0f;
            if (burstActive)
            {
                targetIntensity = Mathf.Max(targetIntensity, intensity * _burstIntensityMultiplier);
            }

            _currentIntensity = Mathf.MoveTowards(_currentIntensity, targetIntensity, fadeOutSpeed * Time.deltaTime);

            if (_currentIntensity <= 0.0001f)
            {
                CurrentOffset = Vector2.zero;
                return;
            }

            var currentFrequency = burstActive ? frequency * _burstFrequencyMultiplier : frequency;
            var time = Time.time * currentFrequency;
            var x = Mathf.PerlinNoise(_seed, time) - 0.5f;
            var y = Mathf.PerlinNoise(_seed + 31.7f, time) - 0.5f;
            CurrentOffset = new Vector2(x, y * verticalWeight) * (_currentIntensity * 2f);
        }

        public void SetActive(bool active)
        {
            enableRuntimeShake = active;
            shakeActive = active;
        }

        public void StartShake()
        {
            enableRuntimeShake = true;
            shakeActive = true;
        }

        public void StopShake()
        {
            shakeActive = false;
        }

        public void PlayBurst(float duration, float intensityMultiplier, float frequencyMultiplier)
        {
            if (duration <= 0f)
            {
                return;
            }

            if (!_burstRunning)
            {
                _burstPreviousEnabled = enableRuntimeShake;
                _burstPreviousActive = shakeActive;
            }

            _burstRunning = true;
            _burstEndTime = Time.time + duration;
            _burstIntensityMultiplier = Mathf.Max(1f, intensityMultiplier);
            _burstFrequencyMultiplier = Mathf.Max(1f, frequencyMultiplier);
            enableRuntimeShake = true;
            shakeActive = true;
        }

        private bool IsShaking => enableRuntimeShake && shakeActive;
    }
}
