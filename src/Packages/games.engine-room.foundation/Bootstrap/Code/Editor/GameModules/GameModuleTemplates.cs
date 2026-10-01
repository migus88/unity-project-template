namespace Bootstrap.Editor.GameModules
{
    internal static class GameModuleTemplates
    {
        public const string CscRsp = "-langversion:12\n-nullable:enable\n";

        public static string BuildAsmdef(string name)
        {
            return "{\n"
                + $"    \"name\": \"{name}\",\n"
                + $"    \"rootNamespace\": \"{name}\",\n"
                + "    \"references\": [\n"
                + "        \"Bootstrap\",\n"
                + "        \"Core\",\n"
                + "        \"UniTask\",\n"
                + "        \"VContainer\"\n"
                + "    ],\n"
                + "    \"includePlatforms\": [],\n"
                + "    \"excludePlatforms\": [],\n"
                + "    \"allowUnsafeCode\": false,\n"
                + "    \"overrideReferences\": false,\n"
                + "    \"precompiledReferences\": [],\n"
                + "    \"autoReferenced\": false,\n"
                + "    \"defineConstraints\": [],\n"
                + "    \"versionDefines\": [],\n"
                + "    \"noEngineReferences\": false\n"
                + "}\n";
        }

        public static string BuildGameModule(string name)
        {
            return "using Bootstrap;\n"
                + "using UnityEngine;\n"
                + "using VContainer;\n"
                + "\n"
                + $"namespace {name}\n"
                + "{\n"
                + $"    [CreateAssetMenu(menuName = \"Game Modules/{name}\")]\n"
                + $"    public sealed class {name}GameModule : GameModule\n"
                + "    {\n"
                + "        public override void Install(IContainerBuilder builder)\n"
                + "        {\n"
                + $"            builder.Register<IMainFlow, {name}MainFlow>(Lifetime.Singleton);\n"
                + "        }\n"
                + "    }\n"
                + "}\n";
        }

        public static string BuildMainFlow(string name)
        {
            return "using System.Threading;\n"
                + "using Bootstrap;\n"
                + "using Core.Transitions;\n"
                + "using Cysharp.Threading.Tasks;\n"
                + "\n"
                + $"namespace {name}\n"
                + "{\n"
                + $"    internal sealed class {name}MainFlow : IMainFlow\n"
                + "    {\n"
                + "        private readonly ILoadingScreen _loadingScreen;\n"
                + "\n"
                + $"        public {name}MainFlow(ILoadingScreen loadingScreen)\n"
                + "        {\n"
                + "            _loadingScreen = loadingScreen;\n"
                + "        }\n"
                + "\n"
                + "        public async UniTask RunAsync(CancellationToken ct)\n"
                + "        {\n"
                + "            await _loadingScreen.HideAsync(ct);\n"
                + "            await UniTask.Never(ct);\n"
                + "        }\n"
                + "    }\n"
                + "}\n";
        }
    }
}
