using UnityEngine;

public class ChickenAnimator : MonoBehaviour
{
    public enum AnimationType { Eating, Standing, Walking, Running }

    private static readonly int IsEating = Animator.StringToHash("IsEating");
    private static readonly int IsStanding = Animator.StringToHash("IsStanding");
    private static readonly int IsWalking = Animator.StringToHash("IsWalking");
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");

    [SerializeField] private Animator _animator;

    private void Awake() => _animator ??= GetComponentInChildren<Animator>();

    public void SetAnimation(AnimationType animation)
    {
        if (_animator == null) return;

        _animator.SetBool(IsEating, animation == AnimationType.Eating);
        _animator.SetBool(IsStanding, animation == AnimationType.Standing);
        _animator.SetBool(IsWalking, animation == AnimationType.Walking);
        _animator.SetBool(IsRunning, animation == AnimationType.Running);
    }
}