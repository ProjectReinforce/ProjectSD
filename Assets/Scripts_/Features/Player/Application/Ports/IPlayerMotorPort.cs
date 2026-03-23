using Shared.Math;

namespace Features.Player.Application.Ports
{
    public interface IPlayerMotorPort
    {
        MotorResult Move(Float3 delta);
    }

    /// <summary>캐릭터 이동 후 위치와 접지 상태를 반환하는 결과 데이터.</summary>
    public readonly struct MotorResult
    {
        public Float3 Position { get; }
        public bool IsGrounded { get; }

        public MotorResult(Float3 position, bool isGrounded)
        {
            Position = position;
            IsGrounded = isGrounded;
        }
    }
}
