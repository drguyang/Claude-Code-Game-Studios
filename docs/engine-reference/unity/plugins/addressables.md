# Unity 6.3 — Addressables

**Last verified:** 2026-02-13
**API re-verified against source:** 2026-09-23 (`com.unity.addressables` **2.10.3**, mirror tag `2.10.3` @ `6fef233`)
**Status:** Production-Ready
**Package:** `com.unity.addressables` (Package Manager)

---

## Overview

**Addressables** is Unity's advanced asset management system that replaces `Resources.Load()`
with async loading, remote content delivery, and better memory control.

**Use Addressables for:**
- Async asset loading (non-blocking)
- DLC and remote content
- Memory optimization (load/unload on demand)
- Asset dependency management
- Large projects with many assets

**DON'T use Addressables for:**
- Tiny projects (overhead not worth it)
- Assets needed immediately at startup (use direct references)

---

## Installation

### Install via Package Manager

1. `Window > Package Manager`
2. Unity Registry > Search "Addressables"
3. Install `Addressables`

---

## Core Concepts

### 1. **Addressable Assets**
- Assets marked as "Addressable" (assigned unique keys)
- Can be loaded by key at runtime

### 2. **Asset Groups**
- Organize assets (e.g., "UI", "Weapons", "Level1")
- Groups determine build settings (local vs remote)

### 3. **Async Loading**
- All loading is async (non-blocking)
- Returns `AsyncOperationHandle`

### 4. **Reference Counting**
- Addressables tracks asset usage
- Must manually release assets when done

---

## Setup

### 1. Mark Assets as Addressable

1. Select asset in Project window
2. Inspector > Check "Addressable"
3. Assign key (e.g., "Enemies/Goblin")

**OR via script:**
```csharp
#if UNITY_EDITOR
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

var settings = AddressableAssetSettingsDefaultObject.GetSettings(true); // create=true 首次初始化
var group    = settings.DefaultGroup;                                   // AddressableAssetGroup
var entry    = settings.CreateOrMoveEntry(guid, group);                 // 已存在则返回既有条目并归位
entry.address = "MyAssetKey";                                           // setter → SetAddress(key)
#endif
```

> ⚠️ **2.10.3 勘误(2026-09-23)**:旧线的三参形态
> `AddressableAssetSettings.AddAssetEntry(guid, address, groupName)` **已不存在**(2.10.3 源内无此重载)。
> 现行入口 = `CreateOrMoveEntry(string guid, AddressableAssetGroup targetParent, bool readOnly = false, bool postEvent = true)`
> 配 `entry.address` setter。组取 `settings.DefaultGroup`(`AddressableAssetGroup`,是 `ScriptableObject` ⇒ 具名用 `.name`,**无** `.Name`)。
>
> 相关签名(`AddressableAssetSettings`,2.10.3 实读):
> - `public static void BuildPlayerContent(out AddressablesPlayerBuildResult result)` —— **返回值是 `void`**,成败只能读 `result.Error`;
>   运行期静态类 `Addressables` 上**没有** `BuildPlayerContent`(它只暴露运行期 Load/Instantiate)。
> - `public List<ScriptableObject> DataBuilders { get; }` —— 元素声明类型是 `ScriptableObject`,
>   `Name` 是 `IDataBuilder` 成员 ⇒ 须经 `GetDataBuilder(int)`(返回 `IDataBuilder`)取,不能直接点 `.Name`。
> - `public int ActivePlayModeDataBuilderIndex { get; set; }` · `IDataBuilder ActivePlayModeDataBuilder { get; }`。
>
> ⚠️ **已知黄字(2.10.3 实读,非本项目缺陷)**:构建时 Console 可能出现一条
> `ProfileValueReference: GetValue called with empty id.` —— 源 =
> `Editor/Build/BuildPipelineTasks/BuildLayoutGenerationTask.cs:775` 对
> `Settings.RemoteCatalogBuildPath.GetValue(...)` 的**无守卫**读取(构建布局报告的元数据字段,
> 每次构建恒定执行);对偶的 `RemoteCatalogLoadPath` 那条在 `:1215`,有 `if (aaSettings.BuildRemoteCatalog)` 守卫,
> 所以只会吐一条。`ProfileValueReference.GetValue`(`:96`)在引用 `Id` 为空时打黄字并返回 `null`;
> 惰性重绑守卫是 `Id == null`,**漏掉空串 Id** ⇒ 不会自愈。
> `BuildRemoteCatalog` 默认 `false` ⇒ 该值不进出货,**黄字为噪声**;要消则构建前
> `profileSettings.CreateValue(<标准路径名>, <默认值>)` 补齐变量,再
> `settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath)`
> (LoadPath / DefaultGroup 的 `BundledAssetGroupSchema.BuildPath|LoadPath` 同理)。标准路径变量名 =
> `Local.BuildPath` / `Local.LoadPath` / `Remote.BuildPath` / `Remote.LoadPath`(公开常量 `kLocalBuildPath` 等)。
> **该黄字不影响 DefaultGroup 的群体构建路径**(后者走 `:1148/:1152`,Id 空会先黄字再 NRE)——
> 若同时见到 NRE 或多条黄字,才说明 profile 变量真缺,须查 `Create()` 初始化顺序。

---

### 2. Create Groups

`Window > Asset Management > Addressables > Groups`

- **Default Local Group**: Bundled with build
- **Remote Group**: Hosted on server (CDN)

---

## Basic Loading

### Load Asset Async

```csharp
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AssetLoader : MonoBehaviour {
    async void Start() {
        // ✅ Load asset asynchronously
        AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>("Enemies/Goblin");
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded) {
            GameObject prefab = handle.Result;
            Instantiate(prefab);
        } else {
            Debug.LogError("Failed to load asset");
        }

        // ⚠️ IMPORTANT: Release when done
        Addressables.Release(handle);
    }
}
```

---

### Load and Instantiate

```csharp
async void SpawnEnemy() {
    // ✅ Load and instantiate in one step
    AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync("Enemies/Goblin");
    await handle.Task;

    GameObject enemy = handle.Result;
    // Use enemy...

    // ✅ Release when destroying
    Addressables.ReleaseInstance(enemy);
}
```

---

### Load Multiple Assets

```csharp
async void LoadAllWeapons() {
    // Load all assets with label "Weapons"
    AsyncOperationHandle<IList<GameObject>> handle = Addressables.LoadAssetsAsync<GameObject>("Weapons", null);
    await handle.Task;

    foreach (var weapon in handle.Result) {
        Debug.Log($"Loaded: {weapon.name}");
    }

    Addressables.Release(handle);
}
```

---

## Asset Labels (Tags)

### Assign Labels

1. `Window > Asset Management > Addressables > Groups`
2. Select asset > Inspector > Labels > Add label (e.g., "Level1", "UI")

### Load by Label

```csharp
// Load all assets with label "Level1"
Addressables.LoadAssetsAsync<GameObject>("Level1", null);
```

---

## Remote Content (DLC)

### Setup Remote Groups

1. Create new group: `Window > Addressables > Groups > Create New Group > Packed Assets`
2. Group Settings:
   - **Build Path**: `ServerData/[BuildTarget]`
   - **Load Path**: `http://yourcdn.com/content/[BuildTarget]`

### Build Remote Content

1. `Window > Asset Management > Addressables > Build > New Build > Default Build Script`
2. Upload `ServerData/` folder to CDN
3. Game loads assets from remote server

---

## Preloading / Caching

### Download Dependencies

```csharp
async void PreloadLevel() {
    // Download all assets in group without loading into memory
    AsyncOperationHandle handle = Addressables.DownloadDependenciesAsync("Level1");
    await handle.Task;

    // Now "Level1" assets are cached, load instantly
    Addressables.Release(handle);
}
```

### Check Download Size

```csharp
async void CheckDownloadSize() {
    AsyncOperationHandle<long> handle = Addressables.GetDownloadSizeAsync("Level1");
    await handle.Task;

    long sizeInBytes = handle.Result;
    Debug.Log($"Download size: {sizeInBytes / (1024 * 1024)} MB");

    Addressables.Release(handle);
}
```

---

## Memory Management

### Release Assets

```csharp
// ✅ Always release when done
Addressables.Release(handle);

// ✅ For instantiated objects
Addressables.ReleaseInstance(gameObject);
```

### Check Reference Count

```csharp
// Addressables uses reference counting
// Asset is unloaded when refCount == 0
```

---

## Asset References (Inspector-Assigned)

### Use AssetReference

```csharp
using UnityEngine.AddressableAssets;

public class EnemySpawner : MonoBehaviour {
    // ✅ Assign in Inspector (drag & drop)
    public AssetReference enemyPrefab;

    async void SpawnEnemy() {
        AsyncOperationHandle<GameObject> handle = enemyPrefab.InstantiateAsync();
        await handle.Task;

        GameObject enemy = handle.Result;
        // Use enemy...

        enemyPrefab.ReleaseInstance(enemy);
    }
}
```

---

## Scenes

### Load Addressable Scene

```csharp
using UnityEngine.SceneManagement;

async void LoadScene() {
    AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync("MainMenu", LoadSceneMode.Additive);
    await handle.Task;

    SceneInstance sceneInstance = handle.Result;
    // Scene loaded

    // Unload scene
    await Addressables.UnloadSceneAsync(handle).Task;
}
```

> ⚠️ **Handle 生命周期(易踩)—— 2026-09-23 由包源码实读钉死**
> (verified against `com.unity.addressables` **2.10.3**, mirror tag `2.10.3` @ `6fef233`;
> 与 U1 spike 实测一致,见 ADR-023 §Validation S3):
>
> **1. `UnloadSceneAsync` 默认会自己释放「卸载」句柄。** 三个重载的 `autoReleaseHandle` 默认均为
> `true`;内部 `InternalUnloadScene` 在 `autoReleaseHandle` 时执行 `relOp.ReleaseHandleOnCompletion()`
> (`Runtime/AddressablesImpl.cs:1351-1357`)⇒ **卸载 operation 一完成,`UnloadSceneAsync` 返回的句柄
> 立即失效**。此后读它的 `.Status` / `.OperationException` / `.Result` 抛
> `System.Exception : Attempting to use an invalid operation handle`
> (`AsyncOperationHandle.cs:211`,判据 = `m_InternalOp.Version != m_Version`)。
> ⇒ 需要读卸载结果时传 `autoReleaseHandle: false`,读毕自行 `Addressables.Release(unloadHandle)`。
>
> **2. 场景卸载后,`load` 句柄本身也失效。** `SceneProvider.ReleaseScene` 走
> `resourceManager.StartOperation(unloadOp, sceneLoadHandle)` —— **StartOperation 持有并释放依赖**。
> ⇒ 任何要用 `load` 的判据值(如 `scene.isLoaded` / `OperationException`)必须**在调 unload 之前**
> 快照成局部变量;场景对象引用本身(`scene`.NET struct)仍可读。
>
> **3. `InstantiateAsync` 的实例若亲代在「被卸载的 Addressable 场景」内,其句柄会被自动释放。**
> `InstantiateAsync` 默认 `trackHandle: true`;场景卸载销毁实例后,
> `ResourceManager.CleanupSceneInstances`(`ResourceManager.cs:1036-1056`)把「`Result` 已为 null
> 且 `InstanceScene()==该场景`」的 tracked 实例 operation **减引用到 0 并自动释放**。
> ⇒ 这类实例**不得手动 `ReleaseInstance`**(会抛 invalid handle),也**不需要**手动释放;
> 而**亲代未入场景**(挂在普通根物体上)的实例**不会**被自动清理 —— 它**存活且句柄有效**,
> **必须**手动 `ReleaseInstance`,否则泄漏(这正是 ADR-023 ⑤ 拆序第 6 步要拦的形态)。
> 判断存活请用**卸载前抓下的 `GameObject` 引用**(Unity fake-null),不要用句柄。
>
> **4. 通用铁律:** 读任何 `AsyncOperationHandle` 前先 `handle.IsValid()`
> (`m_InternalOp != null && Version == m_Version`,**不抛**);`.Status` / `.Result` /
> `.OperationException` 在失效句柄上会抛。
>
> **5. 引用计数归零须 handle 全 `Release` 后才发生**(实测 S3:bundle 计数 `1→4→2→1`)。handle 尚未
> 全部 `Release` 时 bundle 仍被引用是**已知形态**,故「只查句柄登记簿」的运行期断言**查不到 bundle 层**。
>
> **附:类型命名空间 —— `UnloadSceneOptions` 不在本包源码内**(全仓 `.cs` grep 无其声明;由包外
> 程序集提供)。需要「不写该类型名」的调用形态时,用 `UnloadSceneAsync(handle, bool autoReleaseHandle)`
> 重载即可避开。`SceneInstance` / `SceneReleaseMode` 则确认住
> `UnityEngine.ResourceManagement.ResourceProviders`(`ISceneProvider.cs`)。

---

## Common Patterns

### Lazy Loading (Load on Demand)

```csharp
Dictionary<string, AsyncOperationHandle<GameObject>> loadedAssets = new();

async Task<GameObject> GetAsset(string key) {
    if (!loadedAssets.ContainsKey(key)) {
        var handle = Addressables.LoadAssetAsync<GameObject>(key);
        await handle.Task;
        loadedAssets[key] = handle;
    }
    return loadedAssets[key].Result;
}
```

---

### Cleanup on Scene Unload

```csharp
void OnDestroy() {
    // Release all handles
    foreach (var handle in loadedAssets.Values) {
        Addressables.Release(handle);
    }
    loadedAssets.Clear();
}
```

---

## Content Catalog Updates (Live Updates)

### Check for Catalog Updates

```csharp
async void CheckForUpdates() {
    AsyncOperationHandle<List<string>> handle = Addressables.CheckForCatalogUpdates();
    await handle.Task;

    if (handle.Result.Count > 0) {
        Debug.Log("Updates available");
        await Addressables.UpdateCatalogs(handle.Result).Task;
    }

    Addressables.Release(handle);
}
```

---

## Performance Tips

- **Preload** frequently used assets at startup
- **Release** assets immediately when not needed
- Use **labels** to batch-load related assets
- **Cache** remote content for offline use

---

## Debugging

### Addressables Event Viewer

`Window > Asset Management > Addressables > Event Viewer`

- Shows all load/release operations
- Memory usage per asset
- Reference counts

### Addressables Profiler

`Window > Asset Management > Addressables > Profiler`

- Real-time asset usage
- Bundle loading stats

---

## Migration from Resources

```csharp
// ❌ OLD: Resources.Load (synchronous, blocks frame)
GameObject prefab = Resources.Load<GameObject>("Enemies/Goblin");

// ✅ NEW: Addressables (async, non-blocking)
var handle = await Addressables.LoadAssetAsync<GameObject>("Enemies/Goblin").Task;
GameObject prefab = handle.Result;
```

---

## Sources
- https://docs.unity3d.com/Packages/com.unity.addressables@2.0/manual/index.html
- https://learn.unity.com/tutorial/addressables
