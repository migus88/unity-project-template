using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Player
{
    internal sealed class PlayerView : MonoBehaviour
    {
        public Transform CameraTarget => _cameraTarget;

        [SerializeField, Required] private Rigidbody _rigidbody = null!;
        [SerializeField, Required] private Transform _cameraTarget = null!;

        public void Teleport(Vector3 position)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.position = position;
            transform.position = position;
        }

        public void SetHorizontalVelocity(Vector3 velocity)
        {
            _rigidbody.linearVelocity = new Vector3(velocity.x, _rigidbody.linearVelocity.y, velocity.z);
        }
    }
}
