using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Logging;
using Core.Results;
using OneOf;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Success = OneOf.Types.Success;

namespace Bootstrap.Editor.GameModules
{
    public static class GameModuleScaffold
    {
        public const string ProjectFolder = "Assets/_Project";

        private const string PendingNameKey = "Bootstrap.Editor.GameModuleScaffold.PendingName";

        private static readonly Regex NamePattern = new("^[A-Z][A-Za-z0-9]*$");

        public static OneOf<Success, Error> Create(string name)
        {
            if (ValidateName(name).TryPickT1(out var error, out _))
            {
                return error;
            }

            var folder = GetFolder(name);
            var codeFolder = $"{folder}/Code";
            Directory.CreateDirectory(codeFolder);
            File.WriteAllText($"{codeFolder}/{name}.asmdef", GameModuleTemplates.BuildAsmdef(name));
            File.WriteAllText($"{codeFolder}/csc.rsp", GameModuleTemplates.CscRsp);
            File.WriteAllText($"{codeFolder}/{name}GameModule.cs", GameModuleTemplates.BuildGameModule(name));
            File.WriteAllText($"{codeFolder}/{name}MainFlow.cs", GameModuleTemplates.BuildMainFlow(name));

            SessionState.SetString(PendingNameKey, name);
            Log.Info(LogTags.GameModule, $"Wrote the code of game module '{name}' into '{codeFolder}'. Its assets are created after the next compile.");
            AssetDatabase.Refresh();
            return new Success();
        }

        private static OneOf<Success, Error> ValidateName(string name)
        {
            if (!NamePattern.IsMatch(name))
            {
                return new Error($"Game module name '{name}' must be PascalCase letters and digits.");
            }

            if (AssetDatabase.IsValidFolder(GetFolder(name)) || Directory.Exists(GetFolder(name)))
            {
                return new Error($"'{GetFolder(name)}' already exists.");
            }

            if (IsAssemblyName(name))
            {
                return new Error($"An assembly named '{name}' already exists.");
            }

            return new Success();
        }

        private static string GetFolder(string name)
        {
            return $"{ProjectFolder}/{name}";
        }

        [InitializeOnLoadMethod]
        private static void ResumePending()
        {
            var name = SessionState.GetString(PendingNameKey, string.Empty);

            if (!string.IsNullOrEmpty(name))
            {
                EditorApplication.delayCall += () => CreateAssets(name);
            }
        }

        private static void CreateAssets(string name)
        {
            SessionState.EraseString(PendingNameKey);
            var moduleType = TypeCache.GetTypesDerivedFrom<GameModule>().FirstOrDefault(type => type.FullName == $"{name}.{name}GameModule");

            if (moduleType == null)
            {
                Log.Error(LogTags.GameModule, $"Type '{name}.{name}GameModule' was not compiled. Fix the compile errors, delete '{GetFolder(name)}' and create the game module again.");
                return;
            }

            var folder = GetFolder(name);
            var module = (GameModule)ScriptableObject.CreateInstance(moduleType);
            AssetDatabase.CreateAsset(module, $"{folder}/{name}GameModule.asset");
            var rootScope = RootScopeAssets.CreateVariant(module, $"{folder}/{name}RootLifetimeScope.prefab");
            var settings = RootScopeAssets.CreateSettings(rootScope, $"{folder}/{name}VContainerSettings.asset");
            RootScopeAssets.Use(settings);
            Log.Info(LogTags.GameModule, $"Game module '{name}' is ready in '{folder}' and boots from Play.");
        }

        private static bool IsAssemblyName(string name)
        {
            return CompilationPipeline.GetAssemblies().Any(assembly => string.Equals(assembly.name, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
