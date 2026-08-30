using TMPro;
using UnityEngine;

public class PlayerUI : UIBase
{
    [Header("Player Data")]
    [SerializeField] private PlayerDataSO _data;

    [Header("Persistent UI")]
    [SerializeField] private TMP_Text _cashText;
    [SerializeField] private TMP_Text _chickenText;

    [Header("Zone Event")]
    [SerializeField] private PlayerZoneEventSO _zoneEvent;

    [Header("Interaction")]
    [SerializeField] private PlayerInteractionZoneSO _interactionZone;
    [SerializeField] private GameObject _interactionObject;

    protected override void SubscribeToEvents()
    {
        if (_data != null)
            _data.OnDataChanged += UpdatePersistentUI;

        if (_zoneEvent != null)
            _zoneEvent.OnSell += ShowSellNotification;

        if (_interactionZone != null)
            _interactionZone.OnZoneStateChanged += SetInteractionObjectActive;
    }

    protected override void UnsubscribeFromEvents()
    {
        if (_data != null)
            _data.OnDataChanged -= UpdatePersistentUI;

        if (_zoneEvent != null)
            _zoneEvent.OnSell -= ShowSellNotification;

        if (_interactionZone != null)
            _interactionZone.OnZoneStateChanged -= SetInteractionObjectActive;
    }

    protected override void UpdatePersistentUI()
    {
        if (_data == null)
            return;

        if (_cashText != null)
            _cashText.text = _data.Cash.ToString();

        if (_chickenText != null)
            _chickenText.text = _data.Chicken.ToString();
    }

    private void SetInteractionObjectActive(bool isActive)
    {
        if (_interactionObject != null)
            _interactionObject.SetActive(isActive);
    }
}