public interface IPlayerMovement : IMovement
{
    void Jump();
    bool IsGrounded { get; }
}