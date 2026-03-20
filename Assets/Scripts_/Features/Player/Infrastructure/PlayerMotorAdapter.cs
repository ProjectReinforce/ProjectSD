using Features.Player.Application.Ports;
using Shared.Math;
using UnityEngine;

namespace Features.Player.Infrastructure
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotorAdapter : MonoBehaviour, IPlayerMotorPort
    {
        private CharacterController _controller;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public MotorResult Move(Float3 delta)
        {
            _controller.Move(delta.ToVector3());
            return new MotorResult(
                transform.position.ToFloat3(),
                _controller.isGrounded
            );
        }
    }
}
