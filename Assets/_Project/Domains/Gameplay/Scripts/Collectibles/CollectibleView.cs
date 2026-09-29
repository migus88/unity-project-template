using R3;
using R3.Triggers;
using UnityEngine;

namespace Gameplay.Collectibles
{
    internal sealed class CollectibleView : MonoBehaviour
    {
        public Vector3 Position => transform.position;
        public Observable<Unit> Touched => this.OnTriggerEnterAsObservable().AsUnitObservable();

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
