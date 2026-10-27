using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Zenject;

/// <summary>
/// Главный скрипт обучалок. Единственный, кто проверяет дистанцию и показывает текст.
/// Повесить один раз на сцену (например, на Canvas с подсказкой).
/// </summary>
public class TutorialHintManager : MonoBehaviour
{
    [Serializable]
    private class HintEntry
    {
        public string type;
        public string text;
    }

    [Serializable]
    private class HintsData
    {
        public List<HintEntry> hints;
    }

    [Header("UI")]
    [SerializeField] private TMP_Text _hintText;
    [Tooltip("Необязательно: панель-фон, включается/выключается вместе с текстом.")]
    [SerializeField] private GameObject _hintRoot;

    [Header("Настройки")]
    [SerializeField] private float _showDistance = 10f;
    [Tooltip("Сколько секунд игрок должен идти в сторону ящика, чтобы подсказка появилась.")]
    [SerializeField] private float _approachTime = 1f;
    [Tooltip("Минимальная скорость сближения (м/с), чтобы считалось, что игрок идёт к ящику.")]
    [SerializeField] private float _minApproachSpeed = 0.3f;

    [Header("Тексты")]
    [Tooltip("Путь к JSON в папке Resources без расширения.")]
    [SerializeField] private string _hintsJsonPath = "TutorialHints";

    private Player _player;

    private static readonly List<TutorialHintBox> Boxes = new List<TutorialHintBox>();

    private class BoxState { public float prevDist; public float timer; public bool init; }
    private readonly Dictionary<TutorialHintBox, BoxState> _states = new Dictionary<TutorialHintBox, BoxState>();
    private readonly Dictionary<TutorialHintType, string> _textByType = new Dictionary<TutorialHintType, string>();

    private TutorialHintBox _current;

    public static void Register(TutorialHintBox box) { if (!Boxes.Contains(box)) Boxes.Add(box); }
    public static void Unregister(TutorialHintBox box) { Boxes.Remove(box); }

    private void Awake()
    {
        RebuildLookup();
        SetVisible(false);
    }

    private void RebuildLookup()
    {
        _textByType.Clear();
        var asset = Resources.Load<TextAsset>(_hintsJsonPath);
        if (asset == null)
        {
            Debug.LogError($"Tutorial hints JSON not found: Resources/{_hintsJsonPath}", this);
            return;
        }

        var data = JsonUtility.FromJson<HintsData>(asset.text);
        foreach (var entry in data.hints)
        {
            if (Enum.TryParse(entry.type, out TutorialHintType type)) _textByType[type] = entry.text;
            else Debug.LogWarning($"Unknown hint type in JSON: {entry.type}", this);
        }
    }

    private void Update()
    {
        if (_hintText == null) return;

        if (_player == null)
        {
            _player = FindFirstObjectByType<Player>();
            if (_player == null) return;
        }

        // Если игрок выключен (сидит в транспорте) — прячем подсказку и выходим
        if (!_player.gameObject.activeInHierarchy)
        {
            if (_current != null)
            {
                SetVisible(false);
                _current = null;
            }
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        TutorialHintBox best = null;
        float bestDist = float.MaxValue;

        for (int i = Boxes.Count - 1; i >= 0; i--)
        {
            var box = Boxes[i];
            if (box == null)
            {
                Boxes.RemoveAt(i);
                continue;
            }

            if (!_states.TryGetValue(box, out var st))
            {
                st = new BoxState();
                _states[box] = st;
            }

            float dist = Vector3.Distance(_player.transform.position, box.transform.position);

            if (dist > _showDistance)
            {
                st.timer = 0f;
                st.init = false;
                continue;
            }

            float closing = st.init ? (st.prevDist - dist) / dt : 0f;
            st.prevDist = dist;
            st.init = true;

            if (closing >= _minApproachSpeed)
                st.timer += dt;
            else
                st.timer = 0f;

            bool eligible = box == _current || st.timer >= _approachTime;

            if (eligible && dist < bestDist)
            {
                best = box;
                bestDist = dist;
            }
        }

        if (best != _current)
        {
            _current = best;

            if (_current != null && _textByType.TryGetValue(_current.HintType, out var txt))
            {
                _hintText.text = txt;
                SetVisible(true);
            }
            else
            {
                SetVisible(false);
                _current = null;
            }
        }
    }

    private void SetVisible(bool v)
    {
        if (_hintRoot != null) _hintRoot.SetActive(v);
        else if (_hintText != null) _hintText.gameObject.SetActive(v);
    }

}
