// U1 spike 批 · S1 / S3 / S4 三条引擎腿(ADR-023 §Validation,三点均 NOT-RUN → 本批实测)。
//
// 权威来源:
//   ADR-023 §Validation S1(Additive 加载/卸载行为 + E-13 异常观察)·
//             S3(UnloadSceneAsync 不销毁 InstantiateAsync 产物 + S-4 补强 bundle refcount)·
//             S4(A 路 场景切换 vs B 路 SetActive,量级对比 → 机制建议,不产新 ADR);
//   卡 production/u1-spike-checklist.md §4(判据正文)。
//
// ⚠️ 本文件是**执行装置,不是黄金断言**:
//    - 绿 = 装置跑通(加载/卸载/实例化按预期完成);存活方向、毫秒数、bundle 计数
//      **不作 Assert** —— 任一方向都是 spike 发现,写进 Logs/u1_spike_results.txt 供 §6 回填;
//    - 前置:菜单 大医精诚/Spike/Setup U1 Spikes 已跑(缺 key → 三条全 Ignore,不会假绿;
//      Ignore 生效依赖坑 C 的「测试体内钉 LogAssert」写法)。
//    - bundle 计数依赖 Play Mode = Existing Build(Setup 已尝试自动切);Use Asset Database
//      模式下 bundle 数恒 0,结果行会标 mode=AssetDatabase(spike 判据 2/3 记 N/A)。
// ⚠️ 结果写 Logs/u1_spike_results.txt(Unity .gitignore 已含 [Ll]ods —— 不入库),
//    每行同时打进 Console([U1-S1]/[U1-S3]/[U1-S4] 前缀)。
// ⚠️ 编译判定唯一归【桌面】(集群无 Unity);黄金纪律不适用此处(无黄金期望值)。
//
// ── 2026-09-23 第二轮:Addressables handle 生命周期的两个坑(源实读 2.10.3 @ 6fef233) ──
//   坑 A(踩中 S1):`Addressables.UnloadSceneAsync(h)` 默认 `autoReleaseHandle = true`,内部
//     执行 `relOp.ReleaseHandleOnCompletion()` ⇒ **卸载 operation 一完成,返回的 unload handle
//     自己就释放了**。此后读 `.Status` / `.OperationException` 抛
//     「Attempting to use an invalid operation handle」(AsyncOperationHandle.cs:211,Version 失配)。
//     修法:传 `autoReleaseHandle: false`,读完之后自己 `Addressables.Release(unload)`。
//   坑 B(踩中 S3):`InstantiateAsync(key, parent)` 默认 `trackHandle = true`;若实例亲代在
//     **被卸载的 Addressable 场景**里,场景卸载销毁实例后 `ResourceManager.CleanupSceneInstances`
//     会把「Result 已为 null 且 InstanceScene()==该场景」的 tracked 实例 operation 减引用到 0
//     ⇒ **该实例的 handle 被 Addressables 自动释放**(读 .Status 同样抛 invalid handle)。
//     外部亲代(未入场景的根物体)则存活、handle 保持有效 —— 这正是 ADR-023 ⑤ 要区分的两态。
//     修法:在卸载**之前**把 GameObject 引用取出来,卸载后用引用比对(Unity fake-null)判存活;
//     handle 读取一律先 `IsValid()`;`ReleaseInstance` 也先 `IsValid()`(已被自动清理的再释放会抛)。
//   ⇒ 三条读句柄的辅助(Ex / Go / SceneOf)全部先判 `IsValid()`,本文件不再有任何裸读。
//
// ── 2026-09-25 第三轮:坑 C —— LogAssert.ignoreFailingMessages 的**落点**(UTF 1.6.0 源实读) ──
//   在 [SetUp] / [OneTimeSetUp] 里设**无效**:命令链是 SetUpTearDownCommand **外包**
//   UnityLogCheckDelegatingCommand(TestCommandBuilder:42/75),SetUp 先于该测的 LogScope 创建执行;
//   且 BeforeAfterTestCommandBase(:149/:242)只给 SetUp 调用本身开一条**瞬态** scope ⇒ 设的值
//   落进瞬态 scope、随其销毁。逐帧 CheckFailingLogs 读的是 UnityLogCheck **自己**的 scope
//   (默认 false)⇒ 缺 key 的 InvalidKeyException Error 照样把测试红成
//   「Unhandled log message … Use LogAssert.Expect」,GuardSetup 的 Assert.Ignore 根本轮不到。
//   修法:**每个测试体第一行**再钉一次(此时 Current = 该测的 log-check scope,方法体不另开 scope,
//   EnumerableTestMethodCommand 逐帧 MoveNext 直通)。红变 Ignore 后,发现仍由 OnUnityLog 捕获进报告。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace DaYiJingCheng.Tests.PlayMode
{
    internal sealed class U1SceneSpikesTest
    {
        // key 与 Editor.Tools.Spike/U1SpikePaths 同值 —— 测试装配不引用 Editor 装配(编辑期 asmdef 单向),
        // 故常量在此镜像一份;改动须两处同步(卡 §4 注)。
        const string KeyS1 = "U1_SpikeS1";
        const string KeyA1 = "U1_SpikeS4A1";
        const string KeyA2 = "U1_SpikeS4A2";
        const string KeyB = "U1_SpikeS4B";
        const string KeyCube = "U1_SpikeCube";

        const float TimeoutSec = 30f;

        // ────────────────────────── 报告 ──────────────────────────
        // ⚠️ 2026-09-23:首跑【桌面】未产出结果文件。原因两类 —— ① 测试根本没跑(Run 的是 EditMode 标签页);
        //    ② 写盘落点与预期不符(路径推算依赖 Application.dataPath,Test Runner 若 "Run on Player" 会变)。
        //    故改为:先打印候选路径与三个基准目录,再逐个尝试,落成后**大声报出实际路径**;
        //    三个候选全失败也只是警告(数字仍在 Console 的 [U1]/[U1-S*] 行里,不至于丢失)。

        static readonly List<string> Candidates = new List<string>();
        static string _activePath;

        // 捕获 UTF 会「因日志失败」的错误(见文件头注:Addressables 中途抛的 Error 在 UTF 下会
        // 立刻红掉并中止协程 ⇒ 该测试的数字全丢)。改为捕获进报告 —— 发现仍然被记录,
        // 只是不再以「红」的形式吞掉后续测量。UT 的失败日志另有 LogAssert.ignoreFailingMessages 兜。
        static readonly List<string> CapturedLogs = new List<string>();

        static void OnUnityLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                CapturedLogs.Add($"[{type}] {message}");
        }

        static void InitPaths()
        {
            if (Candidates.Count > 0) return;
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Candidates.Add(Path.Combine(root, "Logs", "u1_spike_results.txt"));                    // 主:项目根 Logs/
            Candidates.Add(Path.Combine(Application.dataPath, "u1_spike_results.txt"));           // 兜底:Assets/
            Candidates.Add(Path.Combine(Application.persistentDataPath, "u1_spike_results.txt")); // 兜底:玩家数据目录
        }

        [OneTimeSetUp]
        public void ReportHeader()
        {
            InitPaths();
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnUnityLog;
            Debug.Log($"[U1] 结果文件候选: {string.Join(" | ", Candidates)}");
            Debug.Log($"[U1] dataPath={Application.dataPath} · persistentDataPath={Application.persistentDataPath} · cwd={Directory.GetCurrentDirectory()}");
            Report($"=== U1 spike run {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} · Unity {Application.unityVersion} ===");
            Report("marker:Logs 文件不存在时,搜 Console 前缀 [U1-S 亦可拿到全部数字");
        }

        // ⚠️ 这两处钉(OneTimeSetUp / SetUp)只落在**瞬态 scope** 上、随其销毁 —— 单靠它们
        //    兜不住逐帧 CheckFailingLogs(见文件头注坑 C)。**真正的钉点是各测试体第一行**;
        //    此处保留仅为双保险 + 让 Console 出现 IgnoreFailingMessages:true 的痕迹便于追查。
        [SetUp]
        public void BeforeEach() => LogAssert.ignoreFailingMessages = true;

        [OneTimeTearDown]
        public void ReportFooter()
        {
            Application.logMessageReceived -= OnUnityLog;
            Report($"--- 捕获到的 Error/Exception/Assert 日志:{CapturedLogs.Count} 条 ---");
            foreach (var l in CapturedLogs) Report("[U1-LOG] " + l);
            Report("=== run end ===");
            Debug.Log(_activePath != null
                ? $"[U1] 本次结果文件 = {_activePath}"
                : "[U1] 本次结果文件未写成 —— 数字以上方 [U1]/[U1-S*] Console 行为准");
        }

        static void Report(string line)
        {
            Debug.Log($"[U1] {line}");
            InitPaths();
            if (_activePath != null)
            {
                try { File.AppendAllText(_activePath, line + "\n", Encoding.UTF8); }
                catch (Exception e) { Debug.LogWarning($"[U1] 追加写盘失败 {_activePath}:{e.Message}"); }
                return;
            }
            foreach (var p in Candidates)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(p));
                    File.AppendAllText(p, line + "\n", Encoding.UTF8);
                    _activePath = p;
                    Debug.Log($"[U1] 结果文件 = {_activePath}");
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[U1] 写盘失败 {p}:{e.Message}");
                }
            }
            Debug.LogWarning("[U1] 三个候选路径全部写盘失败 —— 数字仍在上方 [U1]/[U1-S*] Console 行里");
        }

        // ────────────────────────── 通用 ──────────────────────────

        // ⚠️ 句柄可能已被 Addressables 自动释放(见文件头注坑 A / 坑 B):
        //    `IsValid()` 是安全读(`m_InternalOp != null && Version == m_Version`,不抛),
        //    而 `.Status` / `.OperationException` / `.Result` 在失效句柄上会抛。
        //    故 WaitDone 也先判 IsValid:失效即视为「无需再等」,不做断言。
        static IEnumerator WaitDone(AsyncOperationHandle handle, string label)
        {
            float t0 = Time.realtimeSinceStartup;
            while (handle.IsValid() && !handle.IsDone)
            {
                if (Time.realtimeSinceStartup - t0 > TimeoutSec)
                    Assert.Fail($"[U1] {label} 超时 {TimeoutSec}s 未完成,status={handle.Status}");
                yield return null;
            }
        }

        static bool IsMissingKey(AsyncOperationHandle handle)
        {
            if (!handle.IsValid()) return false;
            // ⚠️ 2026-09-25 修:S1 的 LoadSceneAsync 把 InvalidKeyException 包进
            //    ChainOperation 顶层(ex.Message = "ChainOperation failed ..."),
            //    真异常在 InnerException 链 —— 只查顶层 ⇒ S1 误落 Assert.Fail
            //    (S3/S4 的 LoadAssetAsync 顶层即 InvalidKey,故只有 S1 红)。
            //    沿链下钻 + ToString 兜底;沿链未中则仍视为真失败(不放水)。
            for (Exception ex = handle.OperationException; ex != null; ex = ex.InnerException)
            {
                if (ex is InvalidKeyException) return true;
                if (ex.Message != null && ex.Message.Contains("InvalidKey")) return true;
            }
            var top = handle.OperationException;
            return top != null && top.ToString() != null && top.ToString().Contains("InvalidKeyException");
        }

        /// <summary>先探 key 是否存在:Setup 没跑 → Ignore(不假绿);真失败 → Fail。</summary>
        static void GuardSetup(AsyncOperationHandle handle, string label)
        {
            if (!handle.IsValid()) return;
            if (handle.Status != AsyncOperationStatus.Failed) return;
            if (IsMissingKey(handle))
                Assert.Ignore($"[U1] {label}:Addressable key 不存在 —— 先跑菜单 大医精诚/Spike/Setup U1 Spikes 再 Run");
            Assert.Fail($"[U1] {label} 失败:{handle.OperationException}");
        }

        static int BundleCount() => Resources.FindObjectsOfTypeAll<AssetBundle>().Length;

        static IEnumerator SettleBundles(int baseline, float giveUpSec = 2f)
        {
            // refcount 归零后 bundle 卸载是异步的:先给 Addressables 几帧,再补一次 UnloadUnusedAssets。
            float t0 = Time.realtimeSinceStartup;
            while (BundleCount() > baseline && Time.realtimeSinceStartup - t0 < giveUpSec)
                yield return null;
            if (BundleCount() > baseline)
            {
                var op = Resources.UnloadUnusedAssets();
                while (!op.isDone) yield return null;
                t0 = Time.realtimeSinceStartup;
                while (BundleCount() > baseline && Time.realtimeSinceStartup - t0 < giveUpSec)
                    yield return null;
            }
        }

        // ── 三个读句柄的安全取值(全部先判 IsValid,失效句柄不抛)──

        static string Ex<T>(AsyncOperationHandle<T> h)
            => !h.IsValid() ? "<handle 已自动释放>"
                            : (h.OperationException?.Message ?? "none");

        /// <summary>成功 → Result;未成功 / 句柄已失效 → null(不触碰失效句柄)。</summary>
        static GameObject Go(AsyncOperationHandle<GameObject> h)
            => h.IsValid() && h.Status == AsyncOperationStatus.Succeeded ? h.Result : null;

        static Scene SceneOf(AsyncOperationHandle<SceneInstance> h)
            => h.IsValid() && h.Status == AsyncOperationStatus.Succeeded ? h.Result.Scene : default;

        /// <summary>卸载并**保留**可读句柄:autoReleaseHandle:false ⇒ 读得动 status/ex,读完自己 Release。
        /// 走 `(AsyncOperationHandle<SceneInstance>, bool)` 这个重载 ⇒ 代码里**不出现**
        /// `UnloadSceneOptions` 这个类型名(它不在包源码内,命名空间归属无法在集群侧实读钉死 —— 见卡 §8)。</summary>
        static AsyncOperationHandle<SceneInstance> UnloadKeepingHandle(AsyncOperationHandle<SceneInstance> sceneHandle)
            => Addressables.UnloadSceneAsync(sceneHandle, false);

        static void ReleaseIfValid<T>(AsyncOperationHandle<T> h)
        {
            if (h.IsValid()) Addressables.Release(h);
        }

        // ────────────────────────── S1 ──────────────────────────

        [UnityTest]
        public IEnumerator test_s1_additive_load_unload_logs_ms()
        {
            // 坑 C:必须在体内钉 —— SetUp 里那次落在瞬态 scope,拦不住本测逐帧的失败日志判定。
            LogAssert.ignoreFailingMessages = true;
            // 冷启:含 catalog / bundle 初始化;第二轮给暖值(卡 §4.1 要「加载毫秒、卸载毫秒」,
            // 暖值供 S4 量级对照参考,不改判据)。
            // ⚠️ 判据搬到**最后**(数字先落盘)—— 2026-09-23 首跑教训:Report 排在 assert 之后时,
            //    任一 assert 失败或 UTF 因 Error 日志中止协程 ⇒ 数字全丢。判据内容一字未改。
            var sw = Stopwatch.StartNew();
            var load = Addressables.LoadSceneAsync(KeyS1, LoadSceneMode.Additive);
            yield return WaitDone(load, "S1 load(cold)");
            sw.Stop();
            double coldLoadMs = sw.Elapsed.TotalMilliseconds;
            bool loadSceneLoaded = SceneOf(load).isLoaded;
            // ⚠️ 判据用的值一律**在 release 之前**取出 —— 卸载会连同 `load` 一起释放
            //    (SceneProvider.ReleaseScene → StartOperation(unloadOp, sceneLoadHandle) 持有并释放依赖)。
            var loadEx = load.IsValid() ? load.OperationException : null;
            var loadStatus = load.IsValid() ? load.Status.ToString() : "<invalid>";
            Report($"[U1-S1] cold_load_ms={coldLoadMs:F1} load_status={loadStatus} " +
                   $"scene_isLoaded={loadSceneLoaded} op_ex={loadEx?.Message ?? "none"}");
            GuardSetup(load, "S1 load");
            var scene = SceneOf(load);

            sw.Restart();
            var unload = UnloadKeepingHandle(load);
            yield return WaitDone(unload, "S1 unload");
            sw.Stop();
            double unloadMs = sw.Elapsed.TotalMilliseconds;
            bool sceneStillLoaded = scene.isLoaded;
            var unloadStatus = unload.IsValid() ? unload.Status.ToString() : "<invalid>";   // autoRelease:false ⇒ 读得动
            var unloadEx = unload.IsValid() ? unload.OperationException : null;
            Report($"[U1-S1] unload_ms={unloadMs:F1} unload_status={unloadStatus} " +
                   $"scene_still_loaded={sceneStillLoaded} op_ex={unloadEx?.Message ?? "none"}");
            ReleaseIfValid(unload);   // 自持句柄自还

            // 暖轮
            sw.Restart();
            var load2 = Addressables.LoadSceneAsync(KeyS1, LoadSceneMode.Additive);
            yield return WaitDone(load2, "S1 load(warm)");
            sw.Stop();
            double warmLoadMs = sw.Elapsed.TotalMilliseconds;
            bool warmLoaded = SceneOf(load2).isLoaded;
            Report($"[U1-S1] warm_load_ms={warmLoadMs:F1} warm_scene_isLoaded={warmLoaded} op_ex={Ex(load2)}");

            var unload2 = UnloadKeepingHandle(load2);
            yield return WaitDone(unload2, "S1 unload(2)");
            var unload2Status = unload2.IsValid() ? unload2.Status.ToString() : "<invalid>";
            Report($"[U1-S1] unload2_status={unload2Status} op_ex={Ex(unload2)}");
            ReleaseIfValid(unload2);
            Report("[U1-S1] → 回填 ADR-023 S1 勾选");

            // ── 判据(数字已落盘,内容与原稿一字不差)──
            Assert.IsTrue(loadSceneLoaded, "[U1-S1] handle 成功但 scene.isLoaded=false");
            Assert.IsNull(loadEx, $"[U1-S1] load OperationException:{loadEx}");
            Assert.IsFalse(sceneStillLoaded, "[U1-S1] unload 完成后 scene 仍在");
            Assert.IsNull(unloadEx, $"[U1-S1] unload OperationException:{unloadEx}");
            Assert.IsTrue(warmLoaded, "[U1-S1] 暖轮 load 未就位");
        }

        // ────────────────────────── S3 ──────────────────────────

        [UnityTest]
        public IEnumerator test_s3_instance_survival_and_refcount_logs()
        {
            // 坑 C:必须在体内钉 —— SetUp 里那次落在瞬态 scope,拦不住本测逐帧的失败日志判定。
            LogAssert.ignoreFailingMessages = true;
            int b0 = BundleCount();
            Report($"[U1-S3] bundle baseline={b0}");

            // ── 阶段 1:双亲代变体 ──
            //   ext  = 外部亲代(测试自己创建的 Holder,未入 Addressable 场景)—— ADR-023 ⑤「不销毁」的直接检验;
            //   场景内 = 场景 Marker 亲代 —— 随场景层级一起亡,且其 tracked 实例 handle 被
            //            ResourceManager.CleanupSceneInstances **自动释放**(见文件头注坑 B),
            //            两个方向都记,供 §6 判读。
            // ⚠️ 同 S1:数字先落盘、判据放最后(2026-09-23 首跑教训)。
            var load = Addressables.LoadSceneAsync(KeyS1, LoadSceneMode.Additive);
            yield return WaitDone(load, "S3 load");
            bool sceneLoaded = SceneOf(load).isLoaded;
            Report($"[U1-S3] load_status={(load.IsValid() ? load.Status.ToString() : "<invalid>")} " +
                   $"scene_isLoaded={sceneLoaded} op_ex={Ex(load)}");
            GuardSetup(load, "S3 load");
            var scene = SceneOf(load);

            var holder = new GameObject("U1_S3_Holder");   // 根物体、不入场景 ⇒ 场景卸载后仍存活
            var ext = Addressables.InstantiateAsync(KeyCube, holder.transform);
            yield return WaitDone(ext, "S3 Instantiate(ext)");
            // ⚠️ 判据值在卸载前取成**快照**(句柄之后可能被自动释放,那时 .Status 会抛)
            var extStatus = ext.IsValid() ? ext.Status : AsyncOperationStatus.Failed;
            GameObject extGo = Go(ext);                    // ⚠️ 卸载前抓引用:之后句柄可能失效,引用仍可比对
            Report($"[U1-S3] instantiate_ext_status={extStatus} op_ex={Ex(ext)}");

            var marker = new GameObject("U1_S3_Marker");
            SceneManager.MoveGameObjectToScene(marker, scene);
            var inScene = Addressables.InstantiateAsync(KeyCube, marker.transform);
            yield return WaitDone(inScene, "S3 Instantiate(in-scene)");
            var inSceneStatus = inScene.IsValid() ? inScene.Status : AsyncOperationStatus.Failed;
            GameObject inSceneGo = Go(inScene);            // ⚠️ 同上
            Report($"[U1-S3] instantiate_inscene_status={inSceneStatus} op_ex={Ex(inScene)}");

            bool extBefore = extGo != null;
            bool inSceneBefore = inSceneGo != null;
            int b1 = BundleCount();

            var unload = UnloadKeepingHandle(load);
            yield return WaitDone(unload, "S3 unload");
            var unloadStatus = unload.IsValid() ? unload.Status.ToString() : "<invalid>";
            var unloadEx = unload.IsValid() ? unload.OperationException : null;
            ReleaseIfValid(unload);

            // 存活判定走**卸载前抓的 GameObject 引用**(Unity 重载 ==:销毁即 fake-null)——
            // 不用句柄,因为场景内那支句柄可能已被 Addressables 自动释放(读它会抛)。
            bool extAlive = extGo != null;
            bool inSceneAlive = inSceneGo != null;
            bool extHandleValid = ext.IsValid();
            bool inSceneHandleValid = inScene.IsValid();
            int b2 = BundleCount();
            Report($"[U1-S3] unload_status={unloadStatus} op_ex={unloadEx?.Message ?? "none"}");

            // 清阶段 1:先 ReleaseInstance(六步次序的「正确路径」);已被自动清理的那支不能再释放。
            if (ext.IsValid()) Addressables.ReleaseInstance(ext);
            if (inScene.IsValid()) Addressables.ReleaseInstance(inScene);
            yield return SettleBundles(b0);
            int b3 = BundleCount();

            // ── 阶段 2:故意漏 Release(判据 3 可观测性)──
            var load2 = Addressables.LoadSceneAsync(KeyS1, LoadSceneMode.Additive);
            yield return WaitDone(load2, "S3 load(leak)");
            var holder2 = new GameObject("U1_S3_Holder2");
            var ext2 = Addressables.InstantiateAsync(KeyCube, holder2.transform);
            yield return WaitDone(ext2, "S3 Instantiate(leak)");
            var unload2 = UnloadKeepingHandle(load2);
            yield return WaitDone(unload2, "S3 unload(leak)");
            ReleaseIfValid(unload2);
            yield return SettleBundles(b0, 0.5f); // 只等已知会到的;leak 态预期 >b0

            GameObject ext2Go = Go(ext2);
            bool leakAlive = ext2Go != null;
            int bLeak = BundleCount();

            // 清阶段 2(观测完仍要还干净 —— 测试隔离)。
            if (ext2.IsValid()) Addressables.ReleaseInstance(ext2);
            yield return SettleBundles(b0);
            int bFinal = BundleCount();

            UnityEngine.Object.Destroy(holder);
            UnityEngine.Object.Destroy(holder2);

            Report($"[U1-S3] judge1_ext_parent_alive_after_unload={extAlive} " +
                   $"(created={extBefore}, ADR-023 ⑤ 预期 True = 泄漏形态成立) " +
                   $"judge1_scene_child_alive_after_unload={inSceneAlive} (created={inSceneBefore};False=随层级亡,非 Addressables 反例)");
            Report($"[U1-S3] handle_valid_after_scene_unload ext={extHandleValid} inScene={inSceneHandleValid} " +
                   $"(CleanupSceneInstances 会释放「随场景销毁」的 tracked 实例 handle ⇒ inScene 预期 False)");
            Report($"[U1-S3] bundles baseline={b0} after_load_inst={b1} after_unload={b2} " +
                   $"after_release={b3} (判据2:回到 baseline=refcount 归零;>baseline=S-4 已知形态)");
            Report($"[U1-S3] judge3_leak_no_release_alive={leakAlive} leak_bundles={bLeak} " +
                   $"final_after_release={bFinal} (判据3:漏 Release 可观测 = alive 或 leak_bundles>baseline)");
            Report("[U1-S3] → 回填 ADR-023 S3 勾选(含 S-4 补强)");

            // ── 判据(数字已落盘,内容与原稿一字未改;取值全走卸载前快照)──
            Assert.IsTrue(sceneLoaded, "[U1-S3] 场景未就位");
            Assert.AreEqual(AsyncOperationStatus.Succeeded, extStatus, "S3 外部亲代实例化失败");
            Assert.AreEqual(AsyncOperationStatus.Succeeded, inSceneStatus, "S3 场景内实例化失败");
            Assert.IsTrue(extAlive, "[U1-S3] 外部亲代实例在卸载后消失(ADR-023 ⑤ 前提被推翻,须登记)");
        }

        // ────────────────────────── S4 ──────────────────────────

        [UnityTest]
        public IEnumerator test_s4_scene_switch_vs_setactive_logs_ms()
        {
            // 坑 C:必须在体内钉 —— SetUp 里那次落在瞬态 scope,拦不住本测逐帧的失败日志判定。
            LogAssert.ignoreFailingMessages = true;
            const int Iters = 20;

            // ── A 路:两个 Addressable 场景 load/unload 交替 ×20 ──
            var keys = new[] { KeyA1, KeyA2 };
            var cur = Addressables.LoadSceneAsync(keys[0], LoadSceneMode.Additive);
            yield return WaitDone(cur, "S4 A路 首载");
            GuardSetup(cur, "S4 A路 首载");

            double aTotalMs = 0, aMaxMs = 0;
            for (int i = 0; i < Iters; i++)
            {
                var sw = Stopwatch.StartNew();
                var u = UnloadKeepingHandle(cur);
                yield return WaitDone(u, $"S4 A路 unload#{i}");
                ReleaseIfValid(u);
                cur = Addressables.LoadSceneAsync(keys[(i + 1) % 2], LoadSceneMode.Additive);
                yield return WaitDone(cur, $"S4 A路 load#{i}");
                sw.Stop();
                double ms = sw.Elapsed.TotalMilliseconds;
                aTotalMs += ms;
                if (ms > aMaxMs) aMaxMs = ms;
                if (i % 5 == 4) Report($"[U1-S4] A路 iter={i + 1} switch_ms={ms:F2}");
            }
            var uFinal = UnloadKeepingHandle(cur);
            yield return WaitDone(uFinal, "S4 A路 收尾 unload");
            ReleaseIfValid(uFinal);
            Report($"[U1-S4] A路(scene load/unload) avg_ms={aTotalMs / Iters:F2} max_ms={aMaxMs:F2} " +
                   $"total_ms={aTotalMs:F1} iters={Iters}");

            // ── B 路:单场景双根 SetActive ×20 ──
            var loadB = Addressables.LoadSceneAsync(KeyB, LoadSceneMode.Additive);
            yield return WaitDone(loadB, "S4 B路 载入");
            GuardSetup(loadB, "S4 B路 载入");
            var sceneB = SceneOf(loadB);
            GameObject rootA = null, rootB = null;
            foreach (var go in sceneB.GetRootGameObjects())
            {
                if (go.name == "RootA") rootA = go;
                else if (go.name == "RootB") rootB = go;
            }
            if (rootA == null || rootB == null)
            {
                Report($"[U1-S4] B路 场景缺根:rootA={(rootA != null)} rootB={(rootB != null)} —— B 路无法测");
                Assert.Fail("S4 B路场景缺 RootA/RootB(Setup 未按约定生成?)");
            }

            // ⚠️ 亚毫秒量级:必须用 Elapsed.TotalMilliseconds。旧版用 long ElapsedMilliseconds,
            //    SetActive 每次 <1ms 被截断成 0 ⇒ 20 次累计仍 0 ⇒ 上一轮实测 B 路 avg=0.000、比值 inf
            //    (2026-09-23)。判据不变,只修量具精度。
            double bTotalMs = 0, bMaxMs = 0;
            for (int i = 0; i < Iters; i++)
            {
                var sw = Stopwatch.StartNew();
                bool aOn = rootA.activeSelf;
                rootA.SetActive(!aOn);
                rootB.SetActive(aOn);
                sw.Stop();
                double ms = sw.Elapsed.TotalMilliseconds;
                bTotalMs += ms;
                if (ms > bMaxMs) bMaxMs = ms;
            }

            var uB = UnloadKeepingHandle(loadB);
            yield return WaitDone(uB, "S4 B路 收尾 unload");
            ReleaseIfValid(uB);

            double aAvg = aTotalMs / Iters;
            double bAvg = bTotalMs / Iters;
            string ratio = bAvg > 0 ? (aAvg / bAvg).ToString("F1") : (aAvg > 0 ? "inf" : "n/a");
            Report($"[U1-S4] B路(SetActive)          avg_ms={bAvg:F4} max_ms={bMaxMs:F4} " +
                   $"total_ms={bTotalMs:F3} iters={Iters}");
            Report($"[U1-S4] A/B 量级比={ratio}× → 机制建议填卡 §6(量级差即结论,不产新 ADR;回填 ADR-023 S4 勾选)");
            Report("[U1-S4] 注:B 路亚毫秒 ⇒ 量具用 Elapsed.TotalMilliseconds(旧版 long 截断致比值 inf)");
        }
    }
}
