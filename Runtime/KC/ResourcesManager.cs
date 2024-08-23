using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace KC
{
    /// <summary>
    /// 远端资源地址查询服务类
    /// </summary>
    public class RemoteServices : IRemoteServices
    {
        private readonly string _defaultHostServer;
        private readonly string _fallbackHostServer;

        public RemoteServices(string defaultHostServer, string fallbackHostServer)
        {
            _defaultHostServer = defaultHostServer;
            _fallbackHostServer = fallbackHostServer;
        }

        string IRemoteServices.GetRemoteMainURL(string fileName)
        {
            return $"{_defaultHostServer}/{fileName}";
        }

        string IRemoteServices.GetRemoteFallbackURL(string fileName)
        {
            return $"{_fallbackHostServer}/{fileName}";
        }
    }
    
    public class ResourcesManager 
    {
        private static YooConfig _yooConfig;
        private static ResourcePackage _package;

        public ResourcePackage Package => _package;

        public ResourcesManager()
        {
            YooAssets.Initialize();
        }

        public async UniTask CreatePackageAsync(string packageName, bool isDefault = true)
        {
            _package = YooAssets.CreatePackage(packageName);
            YooAssets.SetDefaultPackage(_package);

            _yooConfig = Resources.Load<YooConfig>("YooConfig");
            EPlayMode ePlayMode = _yooConfig.playMode;
            Debug.Log(ePlayMode);
            // 编辑器下的模拟模式
            switch (ePlayMode)
            {
                case EPlayMode.EditorSimulateMode:
                {
                    var simulateBuildResult =
                        EditorSimulateModeHelper.SimulateBuild(EDefaultBuildPipeline.ScriptableBuildPipeline,
                            packageName);
                    var createParameters = new EditorSimulateModeParameters();
                    createParameters.EditorFileSystemParameters =
                        FileSystemParameters.CreateDefaultEditorFileSystemParameters(simulateBuildResult);
                    await _package.InitializeAsync(createParameters);
                    break;
                }
                case EPlayMode.OfflinePlayMode:
                {
                    var createParameters = new OfflinePlayModeParameters();
                    createParameters.BuildinFileSystemParameters =
                        FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
                    await _package.InitializeAsync(createParameters);
                    break;
                }
                case EPlayMode.HostPlayMode:
                {
                    string defaultHostServer = GetHostServerURL();
                    string fallbackHostServer = GetHostServerURL();
                    IRemoteServices remoteServices = new RemoteServices(defaultHostServer, fallbackHostServer);
                    var createParameters = new HostPlayModeParameters();
                    createParameters.BuildinFileSystemParameters =
                        FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
                    createParameters.CacheFileSystemParameters =
                        FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices);
               
                    await _package.InitializeAsync(createParameters).ToUniTask();
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private string GetHostServerURL()
        {
            string hostServerIP = _yooConfig.CDN;
            string appVersion = Application.version;
            string AppName = Application.productName;

#if UNITY_EDITOR
            if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android)
            {
                return $"{hostServerIP}/{AppName}/Android/{appVersion}";
            }
            else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.iOS)
            {
                return $"{hostServerIP}/{AppName}/IPhone/{appVersion}";
            }
            else if (UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.WebGL)
            {
                return $"{hostServerIP}/{AppName}/WebGL/{appVersion}";
            }

            return $"{hostServerIP}/{AppName}/StandaloneWindows64/{appVersion}";
#else
            if (Application.platform == RuntimePlatform.Android)
            {
                return $"{hostServerIP}/{AppName}/Android/{appVersion}";
            }
            else if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                return $"{hostServerIP}/{AppName}/IPhone/{appVersion}";
            }
            else if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                return $"{hostServerIP}/{AppName}/WebGL/{appVersion}";
            }

            return $"{hostServerIP}/{AppName}/StandaloneWindows64/{appVersion}";
#endif
        }
        
        public async UniTask<string> UpdatePackageVersion()
        {
            var operation = _package.RequestPackageVersionAsync();
            await operation.ToUniTask();

            if (operation.Status != EOperationStatus.Succeed)
            {
                Debug.LogError($"获取远端资源版本失败,原因:{operation.Error}");
                return null;
            }

            Debug.Log($"远端最新资源版本为:{operation.PackageVersion}");
            return operation.PackageVersion;
        }
        
        public async UniTask UpdateManifest(string packageVersion)
        {
           
            var operation = _package.UpdatePackageManifestAsync(packageVersion);

            await operation.ToUniTask();
            if (operation.Status != EOperationStatus.Succeed)
            {
                Debug.LogError($"更新资源清单失败,原因:{operation.Error}");
            }
        }
        
        public ResourceDownloaderOperation GetDownloader()
        {
            int downloadingMaxNum = 10;
            int failedTryAgain = 3;
            var downloader = _package.CreateResourceDownloader(downloadingMaxNum, failedTryAgain);
            return downloader;
        }
        
        public async UniTask Download(ResourceDownloaderOperation downloader)
        {
            int totalDownloadCount = downloader.TotalDownloadCount;
            long totalDownloadBytes = downloader.TotalDownloadBytes;

            downloader.OnDownloadErrorCallback += (name, error) => Debug.LogError($"下载资源:{name}错误,原因:{error}");
            downloader.OnDownloadProgressCallback += (count, downloadCount, bytes, downloadBytes) =>
            {
                Debug.Log(
                    $"下载资源中,总下载数量:{count} 当前下载数量:{downloadCount} 总资源大小:{bytes / 1024}kb 当前下载资源大小:{downloadBytes / 1024}kb");
            };
            
            downloader.OnDownloadOverCallback += succeed =>
            {
                Debug.Log("资源下载完成");
            };

            downloader.OnStartDownloadFileCallback += (name, bytes) => Debug.Log($"开始下载资源:{name} 资源大小:{bytes / 1024}kb");

            downloader.BeginDownload();

            await downloader.ToUniTask();
            if (downloader.Status != EOperationStatus.Succeed)
            {
                Debug.LogError($"资源下载失败,原因:{downloader.Error}");
            }
            else
            {
                Debug.Log("资源下载成功");
            }

            var clearOperation = _package.ClearUnusedBundleFilesAsync();
            await clearOperation.ToUniTask();
        }
        
  
        public async UniTask<Dictionary<string, T>> LoadAllAssetsAsync<T>(string location) where T : UnityEngine.Object
        {
            AllAssetsHandle allAssetsOperationHandle = YooAssets.LoadAllAssetsAsync<T>(location);
            await allAssetsOperationHandle.Task;
            Dictionary<string, T> dictionary = new Dictionary<string, T>();
            foreach (UnityEngine.Object assetObj in allAssetsOperationHandle.AllAssetObjects)
            {
                T t = assetObj as T;
                dictionary.Add(t!.name, t);
            }

            allAssetsOperationHandle.Release();
            return dictionary;
        }
    }
}

