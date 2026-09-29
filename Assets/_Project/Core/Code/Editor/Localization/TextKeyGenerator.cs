using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Core.Localization;
using Core.Logging;
using Core.Results;
using OneOf;
using UnityEditor;
using UnityEditor.Compilation;
using Success = OneOf.Types.Success;

namespace Core.Editor.Localization
{
    public static class TextKeyGenerator
    {
        private const string DomainsFolder = "Assets/_Project/Domains/";
        private const string CodeFolderName = "Code";
        private const string KeysNamespace = "Core.Localization";

        private static readonly Regex KeyPattern = new("^[a-z][a-z0-9]*(_[a-z0-9]+)*$");
        private static readonly Regex TableNamePattern = new("^[A-Z][A-Za-z0-9]*$");

        public static OneOf<Success, Error> GenerateAll()
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(LocalizationTable)}"))
            {
                var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(AssetDatabase.GUIDToAssetPath(guid));
                var result = Generate(table);

                if (result.IsT1)
                {
                    return result;
                }
            }

            return new Success();
        }

        public static OneOf<Success, Error> Generate(LocalizationTable table)
        {
            var tablePath = AssetDatabase.GetAssetPath(table);

            if (string.IsNullOrEmpty(tablePath))
            {
                return new Error($"Localization table '{table.name}' is not an asset.");
            }

            if (!TableNamePattern.IsMatch(table.TableName))
            {
                return new Error($"Localization table '{tablePath}' needs a PascalCase table name, got '{table.TableName}'.");
            }

            var folder = Path.GetDirectoryName(tablePath)!.Replace('\\', '/');
            var className = table.TableName + "Text";

            if (!FindOutputFolder(folder).TryPickT0(out var outputFolder, out var outputFolderError))
            {
                return outputFolderError;
            }

            var outputPath = $"{outputFolder}/{className}.g.cs";

            if (!FindNamespace(outputFolder, outputPath).TryPickT0(out var @namespace, out var namespaceError))
            {
                return namespaceError;
            }

            if (!BuildSource(table, tablePath, @namespace, className, IsPublic(folder)).TryPickT0(out var source, out var sourceError))
            {
                return sourceError;
            }

            if (File.Exists(outputPath) && File.ReadAllText(outputPath) == source)
            {
                return new Success();
            }

            Directory.CreateDirectory(outputFolder);
            File.WriteAllText(outputPath, source);
            AssetDatabase.ImportAsset(outputPath);
            Log.Info(LogTags.Localization, $"Generated '{outputPath}'.");
            return new Success();
        }

        [MenuItem("Tools/Localization/Generate Text Keys")]
        private static void GenerateFromMenu()
        {
            GenerateAll().Switch(
                _ => Log.Info(LogTags.Localization, "Text keys generated for every localization table."),
                error => Log.Error(LogTags.Localization, error.Message));
        }

        private static OneOf<string, Error> FindOutputFolder(string folder)
        {
            var moduleFolder = folder;

            while (moduleFolder.Length > 0)
            {
                var codeFolder = $"{moduleFolder}/{CodeFolderName}";

                if (Directory.Exists(codeFolder))
                {
                    return codeFolder + folder.Substring(moduleFolder.Length);
                }

                moduleFolder = Path.GetDirectoryName(moduleFolder)?.Replace('\\', '/') ?? string.Empty;
            }

            return new Error($"Localization table folder '{folder}' has no '{CodeFolderName}' folder in it or above it.");
        }

        private static OneOf<string, Error> FindNamespace(string folder, string outputPath)
        {
            var asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromScriptPath(outputPath);
            var rootNamespace = CompilationPipeline.GetAssemblyRootNamespaceFromScriptPath(outputPath);

            if (string.IsNullOrEmpty(asmdefPath) || string.IsNullOrEmpty(rootNamespace))
            {
                return new Error($"Localization table folder '{folder}' must belong to an assembly definition with a root namespace.");
            }

            var asmdefFolder = Path.GetDirectoryName(asmdefPath)!.Replace('\\', '/');
            var relativeFolder = folder.Length > asmdefFolder.Length ? folder.Substring(asmdefFolder.Length + 1) : string.Empty;
            var segments = relativeFolder.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(".", segments.Prepend(rootNamespace));
        }

        private static bool IsPublic(string folder)
        {
            return !(folder + "/").StartsWith(DomainsFolder, StringComparison.Ordinal);
        }

        private static OneOf<string, Error> BuildSource(LocalizationTable table, string tablePath, string @namespace, string className, bool isPublic)
        {
            var identifiers = new HashSet<string>(StringComparer.Ordinal) { className };
            var fields = new StringBuilder();

            foreach (var entry in table.Entries)
            {
                if (!KeyPattern.IsMatch(entry.Key))
                {
                    return new Error($"Localization table '{tablePath}' has the key '{entry.Key}', which is not lower snake_case.");
                }

                var identifier = ToIdentifier(entry.Key);

                if (!identifiers.Add(identifier))
                {
                    return new Error($"Localization table '{tablePath}' has the key '{entry.Key}', whose constant name '{identifier}' is already taken.");
                }

                fields.Append($"        public static readonly TextKey {identifier} = new(\"{table.TableName}\", \"{entry.Key}\");\n");
            }

            var source = new StringBuilder();

            if (@namespace != KeysNamespace)
            {
                source.Append($"using {KeysNamespace};\n\n");
            }

            source.Append($"namespace {@namespace}\n{{\n");
            source.Append($"    {(isPublic ? "public" : "internal")} static class {className}\n    {{\n");
            source.Append(fields);
            source.Append("    }\n}\n");
            return source.ToString();
        }

        private static string ToIdentifier(string key)
        {
            var identifier = new StringBuilder(key.Length);

            foreach (var word in key.Split('_'))
            {
                identifier.Append(char.ToUpperInvariant(word[0]));
                identifier.Append(word, 1, word.Length - 1);
            }

            return identifier.ToString();
        }
    }
}
