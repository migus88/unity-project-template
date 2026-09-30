using VContainer.Unity;

namespace Core.Domains
{
    public sealed record ScopeRef(LifetimeScope Scope, int Depth);
}
