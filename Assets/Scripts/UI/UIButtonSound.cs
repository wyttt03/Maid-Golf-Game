using UnityEngine;
using UnityEngine.EventSystems;

// Plays a hover sound and a click sound on any UI element (buttons, class cards).
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public AudioSource source;
    public AudioClip hoverClip;
    public AudioClip clickClip;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (source && hoverClip) source.PlayOneShot(hoverClip);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (source && clickClip) source.PlayOneShot(clickClip);
    }
}