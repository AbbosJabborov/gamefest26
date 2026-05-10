using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ping-pong sprite animation directly on the panel's Image component.
/// No child layers needed.
/// 
/// Assign sprites in order: [0]=0% opacity … [4]=100% opacity text.
/// </summary>
[RequireComponent(typeof(Image))]
public class UI_PanelRevealAnim : MonoBehaviour
{
    [Header("Frames  (hidden → visible)")]
    [Tooltip("Drag your Canva exports in order: 0%, 25%, 50%, 75%, 100%.")]
    public Sprite[] frames;

    [Header("Timing")]
    [Tooltip("How long each frame is shown before switching to the next.")]
    public float holdDuration = 0.5f;

    [Tooltip("Delay before the animation starts.")]
    public float startDelay = 0.25f;

    // ── Internal ──────────────────────────────────────────────────────────────
    Image _image;
    int   _currentIndex;
    int   _direction = 1;

    void Awake()
    {
        _image = GetComponent<Image>();
    }

    void Start()
    {
        if (frames == null || frames.Length < 2)
        {
            Debug.LogWarning("UI_PanelRevealAnim: Need at least 2 frames.");
            return;
        }

        _image.sprite = frames[0];
        StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            yield return new WaitForSeconds(holdDuration);

            _currentIndex += _direction;
            _image.sprite  = frames[_currentIndex];

            if (_currentIndex >= frames.Length - 1) _direction = -1;
            if (_currentIndex <= 0)                 _direction =  1;
        }
    }

    /// <summary>Call this when the dissolve triggers to stop the animation.</summary>
    public void StopAnim()
    {
        StopAllCoroutines();
    }
}
