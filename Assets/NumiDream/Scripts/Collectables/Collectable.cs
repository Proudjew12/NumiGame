using UnityEngine;
using UnityEngine.Audio;

namespace NumiDream.Collectables
{
    [DisallowMultipleComponent]
    public sealed class Collectable : MonoBehaviour
    {
        [Header("--------- Collectable ---------")]
        [Header("+Trigger+")]
        [Space(4)]
        [InspectorName("Player Tag")]
        [SerializeField] private string playerTag = "Player";
        [Space(4)]
        [InspectorName("Destroy On Collect")]
        [SerializeField] private bool destroyOnCollect = true;
        [Space(10)]
        [Header("+Audio+")]
        [Space(4)]
        [InspectorName("Collect Sound")]
        [SerializeField] private AudioClip collectSound;
        [Space(4)]
        [InspectorName("Volume")]
        [SerializeField] private float collectSoundVolume = 0.8f;
        [Space(4)]
        [InspectorName("Output")]
        [SerializeField] private AudioMixerGroup output;
        [Space(4)]
        [InspectorName("Spatial Blend")]
        [SerializeField] private float collectSoundSpatialBlend = 0.15f;

        private bool _collected;

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryCollect(other.gameObject);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryCollect(collision.gameObject);
        }

        private void TryCollect(GameObject other)
        {
            if (_collected || !other.CompareTag(playerTag))
            {
                return;
            }

            _collected = true;
            PlayCollectSound();

            if (destroyOnCollect)
            {
                Destroy(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }

        private void PlayCollectSound()
        {
            if (collectSound == null || collectSoundVolume <= 0f)
            {
                return;
            }

            var soundObject = new GameObject(name + "-CollectSound");
            soundObject.transform.position = transform.position;

            var source = soundObject.AddComponent<AudioSource>();
            source.clip = collectSound;
            source.volume = collectSoundVolume;
            source.outputAudioMixerGroup = output;
            source.spatialBlend = Mathf.Clamp01(collectSoundSpatialBlend);
            source.Play();

            Destroy(soundObject, collectSound.length);
        }

        private void OnValidate()
        {
            collectSoundVolume = Mathf.Clamp01(collectSoundVolume);
            collectSoundSpatialBlend = Mathf.Clamp01(collectSoundSpatialBlend);
        }
    }
}
