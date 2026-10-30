using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

// Custom toggle logic for settings panel with change of background colour and off on text displaying
public class CustomToggle : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Graphic backgroundGraphic;
    [SerializeField] private RectTransform toggleKnob;
    [SerializeField] private Color onColor = Color.green;
    [SerializeField] private Color offColor = Color.red;
    [SerializeField] private Text onText;
    [SerializeField] private Text offText;

    public UnityEvent<bool> onValueChanged = new UnityEvent<bool>();

    // Position of the toggle
    private Vector2 knobOnPosition = new Vector2(1, 0.5f);
    private Vector2 knobOffPosition = new Vector2(0, 0.5f);

    private bool _isOn = false;

    public bool isOn
    {
        get { return _isOn; }
        set
        {
            if (_isOn == value) return;
            _isOn = value;
            UpdateVisuals();
            onValueChanged.Invoke(_isOn);
        }
    }

    private void Awake()
    {
        UpdateVisuals();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        isOn = !isOn;
    }

    // Moving the knob on or off position and change background
    private void UpdateVisuals()
    {
        Vector2 targetAnchor = isOn ? knobOnPosition : knobOffPosition;
        toggleKnob.anchorMin = targetAnchor;
        toggleKnob.anchorMax = targetAnchor;
        toggleKnob.pivot = targetAnchor;

        backgroundGraphic.color = isOn ? onColor : offColor;

        if (onText != null && offText != null)
        {
            onText.gameObject.SetActive(isOn);
            offText.gameObject.SetActive(!isOn);
        }
    }
}