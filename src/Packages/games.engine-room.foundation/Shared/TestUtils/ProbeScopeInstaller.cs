using Core.Domains;
using VContainer;

namespace TestUtils
{
    public sealed class ProbeScopeInstaller : ScopeInstaller
    {
        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(new InstallerProbe(name));
        }
    }
}
