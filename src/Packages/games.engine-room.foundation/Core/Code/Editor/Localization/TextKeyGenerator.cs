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
        private const string AssetsFolder = "Assets";
        private const string DomainsSegment = "/Domains/";
        private const string CodeFolderName = "Code";
        private const string GeneratedFileSuffix = ".g.cs";
        private const string KeysNamespace = "Core.Localization";
        private const string TableGuidConstant = "TableGuid";

        private static readonly Regex KeyPattern = new("^[a-z][a-z0-9]*(_[a-z0-9]+)*$");
        private static readonly Regex TableNamePattern = new("^[A-Z][A-Za-z0-9]*$");
        private static readonly Regex TableGuidPattern = new($"const string {TableGuidConstant} = \"([0-9a-f]{{32}})\";");

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

            SweepStaleOutputs();
            return new Success();
        }

        public static OneOf<Success, Error> Generate(LocalizationTable table)
        {
            var tablePath = AssetDatabase.GetAssetPath(table);

            if (string.IsNullOrEmpty(tablePath))
            {
                return new Error($"Localization table '{table.name}' is not an asset.");
            }

            if (!CorePackage.IsWritable(tablePath))
            {
                return new Success();
            }

            if (!TableNamePattern.IsMatch(table.TableName))
            {
                return new Error($"Localization table '{tablePath}' needs a PascalCase table name, got '{table.TableName}'.");
            }

            var folder = Path.GetDirectoryName(tablePath)!.Replace('\\', '/');
            var className = table.TableName + "Text";

            if (!FindOutputFolder(folder, Directory.Exists).TryPickT0(out var outputFolder, out var outputFolderError))
            {
                return outputFolderError;
            }

            var outputPath = $"{outputFolder}/{className}{GeneratedFileSuffix}";
            var tableGuid = AssetDatabase.AssetPathToGUID(tablePath);

            if (!FindNamespace(outputFolder, outputPath).TryPickT0(out var @namespace, out var namespaceError))
            {
                return namespaceError;
            }

            if (!BuildSource(table, tablePath, tableGuid, @namespace, className, IsPublic(folder)).TryPickT0(out var source, out var sourceError))
            {
                return sourceError;
            }

            if (!File.Exists(outputPath) || File.ReadAllText(outputPath) != source)
            {
                Directory.CreateDirectory(outputFolder);
                File.WriteAllText(outputPath, source);
                AssetDatabase.ImportAsset(outputPath);
                Log.Info(LogTags.Localization, $"Generated '{outputPath}'.");
            }

            SweepStaleOutputs(tableGuid, outputPath);
            return new Success();
        }

        public static void SweepStaleOutputs()
        {
            SweepStaleOutputs(null, null);
        }

        internal static bool IsStaleOutput(string outputPath, string source, string? keepGuid, string? keepPath, Func<string, bool> isTableGuid)
        {
            var match = TableGuidPattern.Match(source);

            if (!match.Success)
            {
                return false;
            }

            var tableGuid = match.Groups[1].Value;

            if (tableGuid == keepGuid)
            {
                return !string.Equals(outputPath, keepPath, StringComparison.Ordinal);
            }

            return !isTableGuid(tableGuid);
        }

        internal static OneOf<string, Error> BuildSource(LocalizationTable table, string tablePath, string tableGuid, string @namespace, string className, bool isPublic)
        {
            var identifiers = new HashSet<string>(StringComparer.Ordinal) { className, TableGuidConstant };
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
            source.Append($"        internal const string {TableGuidConstant} = \"{tableGuid}\";\n");

            if (fields.Length > 0)
            {
                source.Append('\n');
                source.Append(fields);
            }

            source.Append("    }\n}\n");
            return source.ToString();
        }

        internal static OneOf<string, Error> FindOutputFolder(string folder, Func<string, bool> directoryExists)
        {
            var moduleFolder = folder;

            while (moduleFolder.Length > 0)
            {
                var codeFolder = $"{moduleFolder}/{CodeFolderName}";

                if (directoryExists(codeFolder))
                {
                    return codeFolder + folder.Substring(moduleFolder.Length);
                }

                moduleFolder = Path.GetDirectoryName(moduleFolder)?.Replace('\\', '/') ?? string.Empty;
            }

            return new Error($"Localization table folder '{folder}' has no '{CodeFolderName}' folder in it or above it.");
        }

        internal static OneOf<string, Error> BuildNamespace(string folder, string asmdefPath, string rootNamespace)
        {
            var asmdefFolder = Path.GetDirectoryName(asmdefPath)!.Replace('\\', '/');
            var moduleFolder = Path.GetFileName(asmdefFolder) == CodeFolderName ? Path.GetDirectoryName(asmdefFolder)!.Replace('\\', '/') : asmdefFolder;

            if (folder != moduleFolder && !folder.StartsWith(moduleFolder + "/", StringComparison.Ordinal))
            {
                return new Error($"Folder '{folder}' compiles into '{asmdefPath}' but is not inside '{moduleFolder}'.");
            }

            var segments = folder.Substring(moduleFolder.Length)
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(segment => segment != CodeFolderName);

            return string.Join(".", segments.Prepend(rootNamespace));
        }

        internal static bool IsPublic(string folder)
        {
            return !(folder + "/").Contains(DomainsSegment, StringComparison.Ordinal);
        }

        internal static List<string> GetSweepFolders(string packageRoot, Func<string, bool> isWritable)
        {
            var folders = new List<string> { AssetsFolder };

            if (!packageRoot.StartsWith(AssetsFolder + "/", StringComparison.Ordinal) && isWritable(packageRoot))
            {
                folders.Add(packageRoot);
            }

            return folders;
        }

        [MenuItem("Tools/Localization/Generate Text Keys")]
        private static void GenerateFromMenu()
        {
            GenerateAll().Switch(
                _ => Log.Info(LogTags.Localization, "Text keys generated for every localization table."),
                error => Log.Error(LogTags.Localization, error.Message));
        }

        private static OneOf<string, Error> FindNamespace(string folder, string outputPath)
        {
            var asmdefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromScriptPath(outputPath);
            var rootNamespace = CompilationPipeline.GetAssemblyRootNamespaceFromScriptPath(outputPath);

            if (string.IsNullOrEmpty(asmdefPath) || string.IsNullOrEmpty(rootNamespace))
            {
                return new Error($"Localization table folder '{folder}' must belong to an assembly definition with a root namespace.");
            }

            return BuildNamespace(folder, asmdefPath, rootNamespace);
        }

        private static void SweepStaleOutputs(string? keepGuid, string? keepPath)
        {
            foreach (var folder in GetSweepFolders(CorePackage.Root, CorePackage.IsWritable))
            {
                foreach (var file in Directory.GetFiles(folder, $"*{GeneratedFileSuffix}", SearchOption.AllDirectories))
                {
                    var outputPath = file.Replace('\\', '/');

                    if (IsStaleOutput(outputPath, File.ReadAllText(outputPath), keepGuid, keepPath, IsTableGuid))
                    {
                        AssetDatabase.DeleteAsset(outputPath);
                        Log.Info(LogTags.Localization, $"Deleted '{outputPath}', its localization table was moved, renamed or deleted.");
                    }
                }
            }
        }

        private static bool IsTableGuid(string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return !string.IsNullOrEmpty(path) && AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(LocalizationTable);
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
