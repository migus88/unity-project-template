using R3;
using R3.Triggers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Collectibles
{
    internal sealed class CollectibleView : MonoBehaviour
    {
        public Vector3 Position => transform.position;
        public PickupEffectView PickupEffect => _pickupEffect;
        public Observable<Unit> Touched => this.OnTriggerEnterAsObservable().AsUnitObservable();

        [SerializeField, Required] private GameObject _visual = null!;
        [SerializeField, Required] private Collider _trigger = null!;
        [SerializeField, Required] private PickupEffectView _pickupEffect = null!;

        public void Hide()
        {
            _trigger.enabled = false;
            _visual.SetActive(false);
        }
    }
}
