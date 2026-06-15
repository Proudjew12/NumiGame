using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class RisingFlashVFX : MonoBehaviour
{
    [Header("Timing")]
    public float delayAfterRising = 1.5f;

    [Header("Flash Appearance")]
    public Color flashColor = Color.white;

    [Range(0f, 1f)]
    public float maxOpacity = 1f;

    [Header("Fill Speed")]
    public float fillSpeed = 1f;

    [Header("Edge Softness")]
    [Range(0f, 0.3f)]
    public float edgeSoftness = 0.08f;

    private RawImage rawImage;
    private Material flashMat;
    private Coroutine flashRoutine;

    private static readonly int PropRadius   = Shader.PropertyToID("_Radius");
    private static readonly int PropCenter   = Shader.PropertyToID("_Center");
    private static readonly int PropColor    = Shader.PropertyToID("_FlashColor");
    private static readonly int PropSoftness = Shader.PropertyToID("_Softness");
    private static readonly int PropAspect   = Shader.PropertyToID("_Aspect");

    private void Awake()
    {
        rawImage = GetComponent<RawImage>();

        Shader shader = Shader.Find("Hidden/RadialFlashVFX");
        if (shader == null)
        {
            Debug.LogError("[RisingFlashVFX] Could not find 'Hidden/RadialFlashVFX' shader.");
            enabled = false;
            return;
        }

        flashMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        rawImage.material = flashMat;
        rawImage.color = Color.white;
        rawImage.texture = Texture2D.whiteTexture;
        rawImage.raycastTarget = false;

        // Disable the component — GameObject stays active so coroutines work fine
        rawImage.enabled = false;
    }

    private void OnDestroy()
    {
        if (flashMat != null)
            Destroy(flashMat);
    }

    public void StartRising()
    {
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashSequence());
    }

    public void ResetFlash()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }
        rawImage.enabled = false;
    }

    private IEnumerator FlashSequence()
    {
        SetRadius(0f);
        rawImage.enabled = false; // keep hidden during delay

        yield return new WaitForSeconds(delayAfterRising);

        rawImage.enabled = true; // show only when expanding starts
        
        float aspect    = (float)Screen.width / Screen.height;
        float maxRadius = Mathf.Sqrt(0.25f * aspect * aspect + 0.25f) + 0.02f;

        float radius = 0f;
        while (radius < maxRadius)
        {
            radius += fillSpeed * maxRadius * Time.deltaTime;
            SetRadius(Mathf.Min(radius, maxRadius));
            yield return null;
        }
    }

    private void SetRadius(float r)
    {
        if (flashMat == null) return;

        Color c = flashColor;
        c.a = maxOpacity;

        flashMat.SetColor (PropColor,    c);
        flashMat.SetFloat (PropRadius,   r);
        flashMat.SetVector(PropCenter,   new Vector4(0.5f, 0.5f, 0f, 0f));
        flashMat.SetFloat (PropSoftness, edgeSoftness);
        flashMat.SetFloat (PropAspect,   (float)Screen.width / Screen.height);
    }
}