using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Core;
using Core.Domains;
using Core.Save;
using Core.Settings;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Unity.Loading;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Bootstrap.PlayModeTests
{
    [PrebuildSetup(typeof(TestBootSetup))]
    [PostBuildCleanup(typeof(TestBootSetup))]
    public sealed class DebugRunnableDomainTests
    {
        private const string EngineCacheFolderName = "Unity";

        private static readonly TimeSpan ScopeBuildTimeout = TimeSpan.FromSeconds(30);

        [Test]
        public async Task RunDebugAsync_EachRootDomainCancelledAfterScopeBuilt_UnloadsSceneDisposesScopeAndKeepsRealStorage()
        {
            // Arrange
            BootMode.Current.Should().Be(BootMode.Kind.Test);
            var realStorageBefore = SnapshotFiles(Application.persistentDataPath);
            var root = VContainerSettings.Instance.GetOrCreateRootLifetimeScopeInstance();
            await root.Container.Resolve<CoreStartup>().RunAsync(root.destroyCancellationToken);
            root.Container.Resolve<ISaveStore>().ActiveSlot.Should().Be(0);
            File.Exists(Path.Combine(BootMode.TestStorageRoot, SettingsService.FilePath)).Should().BeTrue();
            var domains = root.Container.Resolve<IReadOnlyList<IDebugRunnableDomain>>();
            var runs = new List<DomainRun>();

            // Act
            foreach (var domain in domains)
            {
                runs.Add(await RunUntilScopeBuiltThenCancelAsync(domain, root));
            }

            // Assert
            runs.Should().NotBeEmpty();

            foreach (var run in runs)
            {
                run.WasScopeSceneLoaded.Should().BeTrue(run.DomainName);
                run.ScopeDepth.Should().Be(1, run.DomainName);
                run.ScopeParent.Should().BeSameAs(root, run.DomainName);
                run.WasCancelled.Should().BeTrue(run.DomainName);
                run.IsScopeSceneLoadedAfterCancel.Should().BeFalse(run.DomainName);
                run.SceneCountAfterCancel.Should().Be(run.SceneCountBefore, run.DomainName);
                run.IsScopeDisposed.Should().BeTrue(run.DomainName);
            }

            SnapshotFiles(Application.persistentDataPath).Should().BeEquivalentTo(realStorageBefore);
        }

        private static Dictionary<string, FileStamp> SnapshotFiles(string directory)
        {
            var files = new Dictionary<string, FileStamp>();
            var engineCachePrefix = Path.Combine(directory, EngineCacheFolderName) + Path.DirectorySeparatorChar;

            if (!Directory.Exists(directory))
            {
                return files;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (path.StartsWith(engineCachePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var info = new FileInfo(path);
                files[path] = new FileStamp(info.LastWriteTimeUtc, info.Length);
            }

            return files;
        }

        private static async UniTask<DomainRun> RunUntilScopeBuiltThenCancelAsync(IDebugRunnableDomain domain, LifetimeScope root)
        {
            var domainName = domain.GetType().Name;
            var scopeSceneId = domain.Descriptor.EditorContent.ScopeScene;
            var sceneCountBefore = SceneManager.sceneCount;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(root.destroyCancellationToken);

            var running = domain.RunDebugAsync(cts.Token);
            DomainLifetimeScope scope;

            try
            {
                scope = await WaitForBuiltScopeAsync(scopeSceneId, running);
            }
            catch
            {
                cts.Cancel();
                throw;
            }

            var scopeScene = scope.gameObject.scene;
            var wasScopeSceneLoaded = scopeScene.isLoaded;
            var scopeRef = scope.Container.Resolve<ScopeRef>();

            cts.Cancel();
            var (wasCancelled, _) = await running.SuppressCancellationThrow();

            return new DomainRun(
                domainName,
                wasScopeSceneLoaded,
                scopeRef.Depth,
                scope.Parent,
                wasCancelled,
                SceneManager.GetSceneByLoadableSceneId(scopeSceneId).isLoaded,
                sceneCountBefore,
                SceneManager.sceneCount,
                scope.Container == null);
        }

        private static async UniTask<DomainLifetimeScope> WaitForBuiltScopeAsync(LoadableSceneId scopeSceneId, UniTask<object> running)
        {
            DomainLifetimeScope? scope = null;

            await UniTask.WaitUntil(() =>
            {
                scope = FindBuiltScope(scopeSceneId);
                return scope != null || running.Status != UniTaskStatus.Pending;
            }).Timeout(ScopeBuildTimeout);

            if (scope == null)
            {
                await running;
                throw new InvalidOperationException("The domain finished before its scope was built.");
            }

            return scope;
        }

        private static DomainLifetimeScope? FindBuiltScope(LoadableSceneId scopeSceneId)
        {
            var scene = SceneManager.GetSceneByLoadableSceneId(scopeSceneId);

            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            var scope = LifetimeScope.Find<DomainLifetimeScope>(scene) as DomainLifetimeScope;
            return scope != null && scope.Container != null ? scope : null;
        }

        private sealed record DomainRun(
            string DomainName,
            bool WasScopeSceneLoaded,
            int ScopeDepth,
            LifetimeScope ScopeParent,
            bool WasCancelled,
            bool IsScopeSceneLoadedAfterCancel,
            int SceneCountBefore,
            int SceneCountAfterCancel,
            bool IsScopeDisposed);

        private readonly record struct FileStamp(DateTime LastWriteTimeUtc, long Length);
    }
}
