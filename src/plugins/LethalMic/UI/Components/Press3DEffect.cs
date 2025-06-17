using UnityEngine;
using UnityEngine.EventSystems;

namespace LethalMic.UI.Components
{
    /// <summary>
    /// Adds a 3D press effect to a button by offsetting its RectTransform when pressed.
    /// </summary>
    public class Press3DEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private RectTransform _rectTransform;
        private Vector2 _originalAnchoredPosition;
        private bool _initialized = false;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform != null)
            {
                _originalAnchoredPosition = _rectTransform.anchoredPosition;
                _initialized = true;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_initialized)
                _rectTransform.anchoredPosition = _originalAnchoredPosition + new Vector2(0, -2);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_initialized)
                _rectTransform.anchoredPosition = _originalAnchoredPosition;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_initialized)
                _rectTransform.anchoredPosition = _originalAnchoredPosition;
        }
    }
} 