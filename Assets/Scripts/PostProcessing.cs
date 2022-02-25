using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

public class PostProcessing : MonoBehaviour
{
    public static PostProcessing instance;

    [Header("Vignette")]
    [Tooltip("Duration of the vignette transition in seconds")]
    public float vignetteTransitionDuration = 0.5f;

    private Volume volume;
    private Vignette vignette;
    private Coroutine vignetteCoroutine = null;
    private bool vignetteCoroutineRunning = false;
    private float vignetteCurrentIntensity;

    private void Awake()
    {
        instance = this;

        volume = GetComponent<Volume>();
        volume.profile.TryGet(out vignette);
        vignetteCurrentIntensity = vignette.intensity.value;
    }

    public void AddVignette(float intensity)
    {
        if (vignetteCoroutineRunning && vignetteCoroutine != null)
            StopCoroutine(vignetteCoroutine);
        vignetteCoroutine = StartCoroutine(Vignette(vignetteCurrentIntensity, intensity));
    }

    public void RemoveVignette()
    {
        if (vignetteCoroutineRunning && vignetteCoroutine != null)
            StopCoroutine(vignetteCoroutine);
        vignetteCoroutine = StartCoroutine(Vignette(vignetteCurrentIntensity, 0));
    }

    private IEnumerator Vignette(float startValue, float endValue)
    {
        float elapsedTime = 0.0f;
        vignetteCoroutineRunning = true;

        while (elapsedTime <= vignetteTransitionDuration)
        {
            float interpolation = elapsedTime / vignetteTransitionDuration;
            elapsedTime += Time.deltaTime;

            vignetteCurrentIntensity = Mathf.Lerp(startValue, endValue, interpolation);
            vignette.intensity.Override(vignetteCurrentIntensity);

            yield return null;
        }

        vignetteCurrentIntensity = endValue;
        vignette.intensity.Override(endValue);
        vignetteCoroutineRunning = false;
    }
}
