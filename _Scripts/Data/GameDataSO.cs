using UnityEngine;
using UnityEngine.Events;

public abstract class GameDataSO : ScriptableObject
{
    [SerializeField] protected int _cash = 0;
    [SerializeField] protected int _chicken = 0;

    public UnityAction OnDataChanged;

    public int Cash
    {
        get => _cash;
        set
        {
            if (_cash != value)
            {
                _cash = value;
                Save();
                OnDataChanged?.Invoke();
            }
        }
    }

    public int Chicken
    {
        get => _chicken;
        set
        {
            if (_chicken != value)
            {
                _chicken = value;
                Save();
                OnDataChanged?.Invoke();
            }
        }
    }

    protected abstract string CashKey { get; }
    protected abstract string ChickenKey { get; }

    protected virtual void OnEnable()
    {
        Load();
    }

    public abstract void Load();
    public abstract void Save();

    public void InvokeDataChanged()
    {
        OnDataChanged?.Invoke();
    }
}