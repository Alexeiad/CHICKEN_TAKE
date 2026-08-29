using TMPro;
using UnityEngine;
using System.Collections;

public abstract class UIBase : MonoBehaviour
{
    [Header("Sell Notification")]
    [SerializeField] protected TMP_Text _coinsNotificationText;
    [SerializeField] protected TMP_Text _chickensNotificationText;

    protected Coroutine _coinsCoroutine;
    protected Coroutine _chickensCoroutine;

    // Показывает уведомление о монетах и курах
    protected void ShowSellNotification(int coins, int chickens)
    {
        ShowCoinsNotification($"+{coins}");
        ShowChickensNotification($"-{chickens}");
    }

    protected void ShowCoinsNotification(string message)
    {
        if (_coinsNotificationText == null) return;
        _coinsNotificationText.text = message;
        _coinsNotificationText.gameObject.SetActive(true);

        if (_coinsCoroutine != null) StopCoroutine(_coinsCoroutine);
        _coinsCoroutine = StartCoroutine(HideAfterDelay(_coinsNotificationText, 3f));
    }

    protected void ShowChickensNotification(string message)
    {
        if (_chickensNotificationText == null) return;
        _chickensNotificationText.text = message;
        _chickensNotificationText.gameObject.SetActive(true);

        if (_chickensCoroutine != null) StopCoroutine(_chickensCoroutine);
        _chickensCoroutine = StartCoroutine(HideAfterDelay(_chickensNotificationText, 3f));
    }

    protected void HideAllNotifications()
    {
        if (_coinsNotificationText != null)
        {
            _coinsNotificationText.text = string.Empty;
            _coinsNotificationText.gameObject.SetActive(false);
        }
        if (_chickensNotificationText != null)
        {
            _chickensNotificationText.text = string.Empty;
            _chickensNotificationText.gameObject.SetActive(false);
        }
    }

    private IEnumerator HideAfterDelay(TMP_Text text, float delay)
    {
        yield return new WaitForSeconds(delay);
        text.text = string.Empty;
        text.gameObject.SetActive(false);
    }

    // Абстрактные методы, которые должны реализовать наследники
    protected abstract void SubscribeToEvents();
    protected abstract void UnsubscribeFromEvents();
    protected abstract void UpdatePersistentUI();

    protected virtual void OnEnable()
    {
        SubscribeToEvents();
        UpdatePersistentUI();
        HideAllNotifications();
    }

    protected virtual void OnDisable()
    {
        UnsubscribeFromEvents();
    }
}