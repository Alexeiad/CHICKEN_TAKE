using System.Collections;
using TMPro;
using UnityEngine;

public abstract class ChickenCollectorUIBase : MonoBehaviour
{
    [Header("Chicken")]
    [SerializeField] protected TMP_Text _chickenText;

    [Header("Sell Notification")]
    [SerializeField] protected TMP_Text _coinsNotificationText;
    [SerializeField] protected TMP_Text _chickensNotificationText;

    protected ChickenCollectorBase _collector;

    private Coroutine _coinsCoroutine;
    private Coroutine _chickensCoroutine;

    protected virtual void Start()
    {
        InitializeCollector();

        if (_collector == null)
            return;

        SubscribeToCollector();

        UpdateChickenUI();
        HideAllNotifications();
    }

    protected virtual void OnDestroy()
    {
        UnsubscribeFromCollector();
    }

    protected abstract void InitializeCollector();

    protected virtual void SubscribeToCollector()
    {
        _collector.OnChickenCountChanged += UpdateChickenUI;
        _collector.OnSell += ShowSellNotification;
    }

    protected virtual void UnsubscribeFromCollector()
    {
        if (_collector == null)
            return;

        _collector.OnChickenCountChanged -= UpdateChickenUI;
        _collector.OnSell -= ShowSellNotification;
    }

    protected virtual void UpdateChickenUI()
    {
        if (_collector == null || _chickenText == null)
            return;

        _chickenText.text = _collector.CollectedCountText;
    }

    protected virtual void ShowSellNotification(
        int coins,
        int chickens)
    {
        ShowCoinsNotification($"+{coins}");
        ShowChickensNotification($"-{chickens}");
    }

    private void ShowCoinsNotification(string message)
    {
        if (_coinsNotificationText == null)
            return;

        _coinsNotificationText.text = message;
        _coinsNotificationText.gameObject.SetActive(true);

        if (_coinsCoroutine != null)
            StopCoroutine(_coinsCoroutine);

        _coinsCoroutine = StartCoroutine(
            HideAfterDelay(_coinsNotificationText)
        );
    }

    private void ShowChickensNotification(string message)
    {
        if (_chickensNotificationText == null)
            return;

        _chickensNotificationText.text = message;
        _chickensNotificationText.gameObject.SetActive(true);

        if (_chickensCoroutine != null)
            StopCoroutine(_chickensCoroutine);

        _chickensCoroutine = StartCoroutine(
            HideAfterDelay(_chickensNotificationText)
        );
    }

    private IEnumerator HideAfterDelay(TMP_Text text)
    {
        yield return new WaitForSeconds(3f);

        if (text == null)
            yield break;

        text.text = string.Empty;
        text.gameObject.SetActive(false);
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
}