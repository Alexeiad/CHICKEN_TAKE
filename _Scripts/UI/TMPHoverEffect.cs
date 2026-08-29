
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class TMPHoverEffect : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private List<TextMeshProUGUI> _texts = new();

    [Header("Hover")]
    [SerializeField] private Color _hoverColor = Color.white;
    [SerializeField] private float _hoverScaleX = 0.9f;
    [SerializeField] private float _duration = 0.2f;
    [SerializeField] private Ease _ease = Ease.OutQuad;

    private readonly Dictionary<TextMeshProUGUI, Color> _defaultColors = new();
    private readonly Dictionary<TextMeshProUGUI, Vector3> _defaultScales = new();

    private void Awake()
    {
        foreach (TextMeshProUGUI text in _texts)
        {
            if (text == null)
                continue;

            _defaultColors[text] = text.color;
            _defaultScales[text] = text.transform.localScale;

            EventTrigger trigger = text.GetComponent<EventTrigger>();

            if (trigger == null)
                trigger = text.gameObject.AddComponent<EventTrigger>();

            AddEvent(trigger, EventTriggerType.PointerEnter, _ => OnPointerEnter(text));
            AddEvent(trigger, EventTriggerType.PointerExit, _ => OnPointerExit(text));
            AddEvent(trigger, EventTriggerType.PointerDown, _ => OnPointerDown(text));
        }
    }

    private void AddEvent(
        EventTrigger trigger,
        EventTriggerType eventType,
        UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };

        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    private void OnPointerEnter(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        Vector3 targetScale = _defaultScales[text];
        targetScale.x *= _hoverScaleX;

        text.transform.DOKill();
        text.DOKill();

        text.transform
            .DOScale(targetScale, _duration)
            .SetEase(_ease);

        text.DOColor(_hoverColor, _duration)
            .SetEase(_ease);
    }

    private void OnPointerExit(TextMeshProUGUI text)
    {
        ResetText(text);
    }

    private void OnPointerDown(TextMeshProUGUI text)
    {
        ResetText(text);
    }

    private void ResetText(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        text.transform.DOKill();
        text.DOKill();

        text.transform.localScale = _defaultScales[text];
        text.color = _defaultColors[text];
    }

    private void OnDestroy()
    {
        foreach (TextMeshProUGUI text in _texts)
        {
            if (text == null)
                continue;

            text.transform.DOKill();
            text.DOKill();
        }
    }
}

