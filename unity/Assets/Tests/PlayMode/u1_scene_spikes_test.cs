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
//    - Addressables 在 unload 中途抛的 Error 日志会按 UTF 默认把测试打红 —— 那本身就是 S1 发现;
//    - 前置:菜单 DaYi/Spike/Setup U1 Spikes 已跑(缺 key → 三条全 Ignore,不会假绿)。
//    - bundle 计数依赖 Play Mode = Existing Build(Setup 已尝试自动切);Use Asset Database
//      模式下 bundle 数恒 0,结果行会标 mode=AssetDatabase(spike 判据 2/3 记 N/A)。
// ⚠️ 结果写 Logs/u1_spike_results.txt(Unity .gitignore 已含 [Ll]ods —— 不入库),
//    每行同时打进 Console([U1-S1]/[U1-S3]/[U1-S4] 前缀)。
// ⚠️ 编译判定唯一归【桌面】(集群无 Unity);黄金纪律不适用此处(无黄金期望值)。

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

        // UTF 可能在每个测试前重置 LogAssert 的静态态,故逐测再钉一次
        // (2026-09-23:首跑 S1/S3 红 —— Addressables 中途 Error 日志在 UTF 默认下会立即红掉并
        //  中止协程,数字全丢。改为捕获进报告;发现的【内容】不丢,只是不再以红的形式吞掉后续测量)。
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

        static IEnumerator WaitDone(AsyncOperationHandle handle, string label)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!handle.IsDone)
            {
                if (Time.realtimeSinceStartup - t0 > TimeoutSec)
                    Assert.Fail($"[U1] {label} 超时 {TimeoutSec}s 未完成,status={handle.Status}");
                yield return null;
            }
        }

        static bool IsMissingKey(AsyncOperationHandle handle)
        {
            var ex = handle.OperationException;
            return ex is InvalidKeyException
                   || (ex != null && ex.Message != null && ex.Message.Contains("InvalidKey"));
        }

        /// <summary>先探 key 是否存在:Setup 没跑 → Ignore(不假绿);真失败 → Fail。</summary>
        static void GuardSetup(AsyncOperationHandle handle, string label)
        {
            if (handle.Status != AsyncOperationStatus.Failed) return;
            if (IsMissingKey(handle))
                Assert.Ignore($"[U1] {label}:Addressable key 不存在 —— 先跑菜单 DaYi/Spike/Setup U1 Spikes 再 Run");
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

        static string Ex<T>(AsyncOperationHandle<T> h) => h.OperationException?.Message ?? "none";

        /// <summary>成功 → 返回 Result(已销毁时 Unity 重载 == 视作 null);未成功 → 不触碰 Result。</summary>
        static GameObject Go(AsyncOperationHandle<GameObject> h)
            => h.Status == AsyncOperationStatus.Succeeded ? h.Result : null;

        static Scene SceneOf(AsyncOperationHandle<SceneInstance> h)
            => h.Status == AsyncOperationStatus.Succeeded ? h.Result.Scene : default;

        // ────────────────────────── S1 ──────────────────────────

        [UnityTest]
        public IEnumerator test_s1_additive_load_unload_logs_ms()
        {
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
            var loadEx = load.OperationException;   // ⚠️ 判据用的值一律**在 release 之前**取出 ——
                                                    //    unload 会释放 handle,之后读 .Status/.OperationException 不保证安全
            Report($"[U1-S1] cold_load_ms={coldLoadMs:F1} load_status={load.Status} " +
                   $"scene_isLoaded={loadSceneLoaded} op_ex={loadEx?.Message ?? "none"}");
            GuardSetup(load, "S1 load");
            var scene = SceneOf(load);

            sw.Restart();
            var unload = Addressables.UnloadSceneAsync(load);
            yield return WaitDone(unload, "S1 unload");
            sw.Stop();
            double unloadMs = sw.Elapsed.TotalMilliseconds;
            bool sceneStillLoaded = scene.isLoaded;
            var unloadEx = unload.OperationException;
            Report($"[U1-S1] unload_ms={unloadMs:F1} unload_status={unload.Status} " +
                   $"scene_still_loaded={sceneStillLoaded} op_ex={unloadEx?.Message ?? "none"}");

            // 暖轮
            sw.Restart();
            var load2 = Addressables.LoadSceneAsync(KeyS1, LoadSceneMode.Additive);
            yield return WaitDone(load2, "S1 load(warm)");
            sw.Stop();
            double warmLoadMs = sw.Elapsed.TotalMilliseconds;
            bool warmLoaded = SceneOf(load2).isLoaded;
            Report($"[U1-S1] warm_load_ms={warmLoadMs:F1} warm_scene_isLoaded={warmLoaded} op_ex={Ex(load2)}");

            var unload2 = Addressables.UnloadSceneAsync(load2);
            yield return WaitDone(unload2, "S1 unload(2)");
            Report($"[U1-S1] unload2_status={unload2.Status} op_ex={Ex(unload2)}");
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
            int b0 = BundleCount();
            Report($"[U1-S3] bundle baseline={b0}");

            // ── 阶段 1:双亲代变体 ──
            //   ext  = 外部亲代(测试自己创建的 Holder)—— ADR-023 ⑤「不销毁」的直接检验;
            //   场景内 = 场景 Marker 亲代 —— 若随场景层级一起亡,是 Unity 层级语义,不是 Addressables 追踪的反例,
            //            两个方向都记,供 §6 判读。
            // ⚠️ 同 S1:数字先落盘、判据放最后(2026-09-23 首跑教训)。
            var load = Addressables.LoadSceneAsync(KeyS1, LoadSceneMode.Additive);
            yield return WaitDone(load, "S3 load");
            bool sceneLoaded = SceneOf(load).isLoaded;
            Report($"[U1-S3] load_status={load.Status} scene_isLoaded={sceneLoaded} op_ex={Ex(load)}");
            GuardSetup(load, "S3 load");
            var scene = SceneOf(load);

            var holder = new GameObject("U1_S3_Holder");
            var ext = Addressables.InstantiateAsync(KeyCube, holder.transform);
            yield return WaitDone(ext, "S3 Instantiate(ext)");
            var extStatus = ext.Status;   // ⚠️ release 前取出(判据值不在 release 之后读 handle)
            Report($"[U1-S3] instantiate_ext_status={extStatus} op_ex={Ex(ext)}");

            var marker = new GameObject("U1_S3_Marker");
            SceneManager.MoveGameObjectToScene(marker, scene);
            var inScene = Addressables.InstantiateAsync(KeyCube, marker.transform);
            yield return WaitDone(inScene, "S3 Instantiate(in-scene)");
            var inSceneStatus = inScene.Status;
            Report($"[U1-S3] instantiate_inscene_status={inSceneStatus} op_ex={Ex(inScene)}");

            bool extBefore = Go(ext) != null;
            bool inSceneBefore = Go(inScene) != null;
            int b1 = BundleCount();

            var unload = Addressables.UnloadSceneAsync(load);
            yield return WaitDone(unload, "S3 unload");

            bool extAlive = Go(ext) != null;                  // Unity 重载 ==:销毁即 fake-null
            bool inSceneAlive = Go(inScene) != null;
            int b2 = BundleCount();
            Report($"[U1-S3] unload_status={unload.Status} op_ex={Ex(unload)}");

            // 清阶段 1:先 ReleaseInstance(六步次序的「正确路径」),再等 bundle 落底。
            Addressables.ReleaseInstance(ext);
            Addressables.ReleaseInstance(inScene);
            yield return SettleBundles(b0);
            int b3 = BundleCount();

            // ── 阶段 2:故意漏 Release(判据 3 可观测性)──
            var load2 = Addressables.LoadSceneAsync(KeyS1, LoadSceneMode.Additive);
            yield return WaitDone(load2, "S3 load(leak)");
            var holder2 = new GameObject("U1_S3_Holder2");
            var ext2 = Addressables.InstantiateAsync(KeyCube, holder2.transform);
            yield return WaitDone(ext2, "S3 Instantiate(leak)");
            var unload2 = Addressables.UnloadSceneAsync(load2);
            yield return WaitDone(unload2, "S3 unload(leak)");
            yield return SettleBundles(b0, 0.5f); // 只等已知会到的;leak 态预期 >b0

            bool leakAlive = Go(ext2) != null;
            int bLeak = BundleCount();

            // 清阶段 2(观测完仍要还干净 —— 测试隔离)。
            Addressables.ReleaseInstance(ext2);
            yield return SettleBundles(b0);
            int bFinal = BundleCount();

            UnityEngine.Object.Destroy(holder);
            UnityEngine.Object.Destroy(holder2);

            Report($"[U1-S3] judge1_ext_parent_alive_after_unload={extAlive} " +
                   $"(created={extBefore}, ADR-023 ⑤ 预期 True = 泄漏形态成立) " +
                   $"judge1_scene_child_alive_after_unload={inSceneAlive} (created={inSceneBefore};False=随层级亡,非 Addressables 反例)");
            Report($"[U1-S3] bundles baseline={b0} after_load_inst={b1} after_unload={b2} " +
                   $"after_release={b3} (判据2:回到 baseline=refcount 归零;>baseline=S-4 已知形态)");
            Report($"[U1-S3] judge3_leak_no_release_alive={leakAlive} leak_bundles={bLeak} " +
                   $"final_after_release={bFinal} (判据3:漏 Release 可观测 = alive 或 leak_bundles>baseline)");
            Report("[U1-S3] → 回填 ADR-023 S3 勾选(含 S-4 补强)");

            // ── 判据(数字已落盘,内容与原稿一字未改)──
            Assert.IsTrue(sceneLoaded, "[U1-S3] 场景未就位");
            Assert.AreEqual(AsyncOperationStatus.Succeeded, extStatus, "S3 外部亲代实例化失败");
            Assert.AreEqual(AsyncOperationStatus.Succeeded, inSceneStatus, "S3 场景内实例化失败");
        }

        // ────────────────────────── S4 ──────────────────────────

        [UnityTest]
        public IEnumerator test_s4_scene_switch_vs_setactive_logs_ms()
        {
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
                var u = Addressables.UnloadSceneAsync(cur);
                yield return WaitDone(u, $"S4 A路 unload#{i}");
                cur = Addressables.LoadSceneAsync(keys[(i + 1) % 2], LoadSceneMode.Additive);
                yield return WaitDone(cur, $"S4 A路 load#{i}");
                sw.Stop();
                double ms = sw.Elapsed.TotalMilliseconds;
                aTotalMs += ms;
                if (ms > aMaxMs) aMaxMs = ms;
                if (i % 5 == 4) Report($"[U1-S4] A路 iter={i + 1} switch_ms={ms:F2}");
            }
            var uFinal = Addressables.UnloadSceneAsync(cur);
            yield return WaitDone(uFinal, "S4 A路 收尾 unload");
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

            var uB = Addressables.UnloadSceneAsync(loadB);
            yield return WaitDone(uB, "S4 B路 收尾 unload");

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
