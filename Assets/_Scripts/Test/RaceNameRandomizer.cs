using System.Collections.Generic;
using UnityEngine;
using Zenject;

public sealed class RaceNameRandomizer : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Перезаписывать имена, которые уже заданы в инспекторе")]
    [SerializeField] private bool _overwriteExisting = true;

    [Tooltip("0 = случайный сид каждый запуск; иначе фиксированный (для дебага)")]
    [SerializeField] private int _seed = 0;

    [Tooltip("Имя игрока (к нему добавится \" (Вы)\")")]
    [SerializeField] private string _playerName = "Игрок";

    private static readonly string[] Names =
{
    "Зараза",     "Жадина",      "Олух",       "Енот",       "Лиса",
    "Капибара",   "Хитрюга",     "Прохвост",   "Балбес",     "Хомяк",
    "Барсук",     "Жмот",        "Плут",       "Дурень",     "Крот",
    "Выдра",      "Наглец",      "Пройдоха",   "Лопух",      "Бобр",
    "Куница",     "Скупердяй",   "Врун",       "Растяпа",    "Суслик",
    "Горностай",  "Хам",         "Проказник",  "Тюфяк",      "Сурок",
    "Ласка",      "Хлюст",       "Шельма",     "Бестолочь",  "Норка",
    "Росомаха",   "Жулик",       "Болван",     "Хитрец",     "Хорёк",
    "Соболь",     "Поганец",     "Недоумок",   "Песец",      "Колонок",
    "Плутишка",   "Оболтус",     "Хитрован",   "Барсучок",   "Капибарик",
    "Нерпа",      "Морж",        "Белёк",      "Тюлень",     "Ондатра",
    "Нутрия",     "Шиншилла",    "Дикобраз",   "Ёжик",       "Ехидна",
    "Утконос",    "Вомбат",      "Коала",      "Ленивец",    "Тапир",
    "Муравьед",   "Броненосец",  "Скунс",      "Мангуст",    "Фенёк",
};

    private IEntityRegistry<IEntity> _registry;
    private readonly HashSet<IEntity> _named = new HashSet<IEntity>();
    private readonly List<string> _pool = new List<string>();
    private int _fallback;

    [Inject]
    public void Construct(IEntityRegistry<IEntity> registry)
    {
        _registry = registry;
        _fallback = 1;

        if (_seed != 0)
            Random.InitState(_seed);

        _pool.AddRange(Names);
        Shuffle(_pool);
    }

    private void Update()
    {
        if (_registry == null) return;

        foreach (IEntity entity in _registry.AllEntities)
        {
            if (entity == null) continue;
            if (_named.Contains(entity)) continue;

            if (entity is Player)
            {
                entity.Name = $"{_playerName} (Вы)";
                _named.Add(entity);
                continue;
            }

            if (!_overwriteExisting && !string.IsNullOrWhiteSpace(entity.Name))
            {
                _named.Add(entity);
                continue;
            }

            entity.Name = _pool.Count > 0 ? TakeRandom() : $"Racer {_fallback++}";
            _named.Add(entity);
        }
    }

    private string TakeRandom()
    {
        int index = Random.Range(0, _pool.Count);
        string name = _pool[index];
        _pool.RemoveAt(index);
        return name;
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}