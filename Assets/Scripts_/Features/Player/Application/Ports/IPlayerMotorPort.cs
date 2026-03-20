using Shared.Math;

namespace Features.Player.Application.Ports
{
    public interface IPlayerMotorPort
    {
        MotorResult Move(Float3 delta);
    }
}
