using Sirenix.OdinInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace Gameplay.Cameras
{
    internal sealed class GameplayCameraView : MonoBehaviour
    {
        [SerializeField, Required] private CinemachineCamera _camera = null!;

        public void SetFollowTarget(Transform target)
        {
            _camera.Follow = target;
            _camera.PreviousStateIsValid = false;
        }
    }
}
