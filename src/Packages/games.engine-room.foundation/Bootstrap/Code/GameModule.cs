using UnityEngine;
using VContainer;

namespace Bootstrap
{
    public abstract class GameModule : ScriptableObject
    {
        [field: SerializeField] public bool IsUiNavigationEnabled { get; private set; }

        public abstract void Install(IContainerBuilder builder);
    }
}
