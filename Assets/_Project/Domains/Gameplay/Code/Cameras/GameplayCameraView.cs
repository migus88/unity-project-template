using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Cameras
{
    internal sealed class GameplayCameraView : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _camera = null!;
        [SerializeField] private CinemachineFollow _follow = null!;
        [SerializeField] private Vector3 _baseFollowOffset = new(0f, 13f, -8.5f);

        public void SetFollowTarget(Transform target)
        {
            _camera.Follow = target;
            _camera.PreviousStateIsValid = false;
        }

        public void SetDistanceScale(float scale)
        {
            _follow.FollowOffset = _baseFollowOffset * scale;
            _camera.PreviousStateIsValid = false;
        }
    }
}
