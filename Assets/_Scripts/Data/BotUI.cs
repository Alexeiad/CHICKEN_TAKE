using System.Collections;
using TMPro;
using UnityEngine;

public class BotUI : MonoBehaviour
{
    [Header("Bot")]
    [SerializeField] private BotChickenCollector _collector;

    [Header("Chicken")]
    [SerializeField] private TMP_Text _chickenText;

    [Header("Cash")]
    [SerializeField] private bool _showCash;
    [SerializeField] private TMP_Text _cashText;

    [Header("Sell Notification")]
    [SerializeField] private TMP_Text _coinsNotificationText;
    [SerializeField] private TMP_Text _chickensNotificationText;

    private BotDataSO _data;

    private Coroutine _coinsCoroutine;
    private Coroutine _chickensCoroutine;

    private void Start()
    {
        if (_collector == null)
        {
            Debug.LogError(
                $"[{name}] BotChickenCollector не назначен!",
                this
            );

            return;
        }

        // BotChickenCollector.Awake() уже гарантированно отработал.
        _data = _collector.Data;

        if (_data == null)
        {
            Debug.LogError(
                $"[{name}] BotDataSO не создан у {_collector.name}!",
                this
            );

            return;
        }

        _data.OnDataChanged += UpdateUI;
        _collector.OnSell += OnSell;

        UpdateUI();
        HideAllNotifications();
    }

    private void OnDestroy()
    {
        if (_data != null)
            _data.OnDataChanged -= UpdateUI;

        if (_collector != null)
            _collector.OnSell -= OnSell;
    }

    private void UpdateUI()
    {
        if (_data == null)
            return;

        if (_chickenText != null)
            _chickenText.text = _data.Chicken.ToString();

        if (_showCash && _cashText != null)
            _cashText.text = _data.Cash.ToString();
    }

    private void OnSell(int coins, int chickens)
    {
        SetPersistentTextsActive(false);

        ShowSellNotification(coins, chickens);

        CancelInvoke(nameof(ShowPersistentTexts));
        Invoke(nameof(ShowPersistentTexts), 3f);
    }

    private void ShowPersistentTexts()
    {
        UpdateUI();
        SetPersistentTextsActive(true);
    }

    private void SetPersistentTextsActive(bool active)
    {
        if (_chickenText != null)
            _chickenText.gameObject.SetActive(active);

        if (_showCash && _cashText != null)
            _cashText.gameObject.SetActive(active);
    }

    private void ShowSellNotification(int coins, int chickens)
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

        if (text != null)
        {
            text.text = string.Empty;
            text.gameObject.SetActive(false);
        }
    }

    private void HideAllNotifications()
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