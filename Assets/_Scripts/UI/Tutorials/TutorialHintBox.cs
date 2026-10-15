using UnityEngine;

/// <summary>
/// Вешается на каждый ящик с подсказкой. Единственное публичное поле — enum.
/// Дистанцию и показ текста обрабатывает TutorialHintManager.
/// </summary>
public class TutorialHintBox : MonoBehaviour
{
    [SerializeField] private TutorialHintType _hintType;

    public TutorialHintType HintType => _hintType;

    private void OnEnable() => TutorialHintManager.Register(this);
    private void OnDisable() => TutorialHintManager.Unregister(this);
}
