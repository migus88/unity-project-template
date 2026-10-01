using UnityEngine;
using VContainer;

namespace Bootstrap
{
    public abstract class GameModule : ScriptableObject
    {
        public abstract void Install(IContainerBuilder builder);
    }
}
