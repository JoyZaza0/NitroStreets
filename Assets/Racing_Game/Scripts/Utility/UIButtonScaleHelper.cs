//______________________________________________
// ALIyerEdon
// https://assetstore.unity.com/publishers/23606
//______________________________________________

using UnityEngine;
using UnityEngine.EventSystems;

namespace ALIyerEdon
{
    public class UIButtonScaleHelper : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Scale Settings")]
        [SerializeField] private Vector3 hoveredScale = new Vector3(1.1f, 1.1f, 1.1f);
        [SerializeField] private float transitionSpeed = 10f;

        private Vector3 originalScale;
        private Vector3 targetScale;

        void Start()
        {
            originalScale = transform.localScale;
            targetScale = originalScale;
        }

        void Update()
        {
            // Smoothly transition scale
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            targetScale = Vector3.Scale(originalScale, hoveredScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = originalScale;
        }

        void OnDisable()
        {
            // Reset to original scale when disabled to prevent sticking
            transform.localScale = originalScale;
            targetScale = originalScale;
        }

        public void SetTargetScale(Vector3 newScale)
        {
            targetScale = Vector3.Scale(originalScale, newScale);
        }

        public void SetTargetScale()
        {
            targetScale = Vector3.Scale(originalScale, hoveredScale);
        }
    }
}
