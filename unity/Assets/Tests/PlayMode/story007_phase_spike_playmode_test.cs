// Story 007 · S1 / S3 spike 探针 · ADR-011 §Risks-A 的引擎腿(要求先于实现实测)。
//
// 权威来源:
//   ADR-011 §Risks-A S1 —— `InputSystem.updateMode` 必须钉死(旋钮表明列,失败路径 = 降级
//     「相位确定性并入 10 误差带」并登记修订;【本故事性质判据不因 spike 改写】);
//   ADR-011 §Risks-A S3 —— `onAfterUpdate` 相位实测:每帧恰一次?Manual 干跑是否双计?
//   卡 production/epics/input-system/story-007-direct-read-channel.md
//     · AC-3-B2③(B2③ 判据 = 渲染帧 +1 ⇒ 采样恰 +1;不得以枚举成员名为主语);
//     · QA Test Cases「手动 InputSystem.Update() 干跑一次 ⇒ 采样计数不得双计」。
//
// ⚠️ 本文件是**执行装置,不是黄金断言**(同 u1_scene_spikes_test 前例):
//    - 帧增量 / 触发次数 / 模式值一律 Report 进 Logs/story007_spike_results.txt(不入库),
//      同帧行进 Console([007-S1] / [007-S3] 前缀)—— 任一方向都是 spike 发现;
//    - 判据(若有)排在 Report 之后(数字先落盘,assert 不吞测量);
//    - spike 允许记录枚举名作诊断,但 **B2③ 正式判据不得以枚举成员名为主语**
//      (story 原文;此处仅诊断,正式对偶断言在 direct_read_channel_test.cs)。
//    - 前置:无(本探针只用 InputSystem settings + onAfterUpdate,零 Addressables、
//      零场景、零设备 —— 不依赖 菜单 Setup,也不受 missing-key Ignore 影响)。
//
// ⚠️ 解析纪律:本文件在 **PlayMode 装配**(Gameplay.Tests) 下编译 —— 已引用
//    "Unity.InputSystem"; `InputSystem.onAfterUpdate` / `InputSystem.Update()` /
//    `InputSystem.settings.updateMode` 均为公共 API(InputSystem.cs 源实读 1.20.0,
//    post-cutoff —— 这正是本 spike 的意义)。

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace DaYiJingCheng.Tests.PlayMode
{
    internal sealed class Story007PhaseSpikeTest
    {
        // ────────────────────────── 报告装置(u1 同款,缩到本文件需要的最小面)──────────────────────────

        static readonly List<string> Candidates = new List<string>();
        static string _activePath;
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
            Candidates.Add(Path.Combine(root, "Logs", "story007_spike_results.txt"));             // 主:项目根 Logs/
            Candidates.Add(Path.Combine(Application.dataPath, "story007_spike_results.txt"));     // 兜底:Assets/
            Candidates.Add(Path.Combine(Application.persistentDataPath, "story007_spike_results.txt")); // 兜底:玩家数据目录
        }

        static void Report(string line)
        {
            Debug.Log($"[[007]] {line}");
            InitPaths();
            if (_activePath != null)
            {
                try { File.AppendAllText(_activePath, line + "\n", Encoding.UTF8); }
                catch (Exception e) { Debug.LogWarning($"[007] 追加写盘失败 {_activePath}:{e.Message}"); }
                return;
            }
            foreach (var p in Candidates)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(p));
                    File.AppendAllText(p, line + "\n", Encoding.UTF8);
                    _activePath = p;
                    Debug.Log($"[007] 结果文件 = {_activePath}");
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[007] 写盘失败 {p}:{e.Message}");
                }
            }
            Debug.LogWarning("[007] 三个候选路径全部写盘失败 —— 数字仍在上方 [[007]] Console 行里");
        }

        [OneTimeSetUp]
        public void ReportHeader()
        {
            InitPaths();
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnUnityLog;
            Debug.Log("[007] dataPath=" + Application.dataPath +
                      " · persistentDataPath=" + Application.persistentDataPath +
                      " · cwd=" + Directory.GetCurrentDirectory());
            Report("=== Story007 spike run " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                   " · Unity " + Application.unityVersion + " · batch/editor ===");
            Report("marker:story007 spike 结果不存在时,搜 Console 前缀 [007-S 亦可拿到全部数字");
        }

        [SetUp]
        public void BeforeEach() => LogAssert.ignoreFailingMessages = true;

        // qa-F9:SetUp/OneTimeSetUp 抬起的 ignore 必须有对应回落 —— 否则泄漏到本装配其余测试
        // (失败日志被吞 = 假绿面)。每测回落;末测后终值 = false。
        [TearDown]
        public void AfterEach() => LogAssert.ignoreFailingMessages = false;

        [OneTimeTearDown]
        public void ReportFooter()
        {
            Application.logMessageReceived -= OnUnityLog;
            Report("--- 捕获到的 Error/Exception/Assert 日志:" + CapturedLogs.Count + " 条 ---");
            foreach (var l in CapturedLogs) Report("[007-LOG] " + l);
            Report("=== run end ===");
            Debug.Log(_activePath != null
                ? "[007] 本次结果文件 = " + _activePath
                : "[007] 本次结果文件未写成 —— 数字以上方 [[007]] Console 行为准");
        }

        // ────────────────────────── S1:updateMode 实际值 ──────────────────────────

        /// <summary>S1:从真实 settings 对象读 updateMode(非投影/非假设),记录名称+int。
        /// 期望观察:ProcessEventsInDynamicUpdate(默认 m_UpdateMode,InputSettings.cs 源读)。
        /// 仅诊断记录 + 轻 sanity —— 正式钉死判据落在 B2③ 的对偶断言(不含枚举字面)。</summary>
        [Test]
        public void test_007_s1_update_mode_reads_actual_settings()
        {
            // 坑 C:体内第一行钉(SetUp 里的落瞬态 scope)。
            LogAssert.ignoreFailingMessages = true;

            var mode = InputSystem.settings.updateMode;
            int modeInt = (int)mode;
            Report($"[007-S1] updateMode_name={mode} updateMode_int={modeInt} " +
                   $"(ProcessEventsInDynamicUpdate 实值在 InputSettings.cs:835;此处只诊断,正式判据走对偶)");

            bool isManual = mode == InputSettings.UpdateMode.ProcessEventsManually;
            bool isFixedOnly = mode == InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
            Report($"[007-S1] is_manual={isManual} is_fixed_only={isFixedOnly} " +
                   "→ B2③ 帧-采样对偶断言期望 dynamic/渲染帧相位;非此二值即视为可用的动态相位候选");

            // 数字已落盘;sanity 判据(轻):不可能是 Manual(否则 B2③ 测试永无自动采样)。
            Assert.IsFalse(isManual, "[007-S1] updateMode == Manual —— B2③ 帧-采样对偶将无自动相位可测");
        }

        // ────────────────────────── S3:onAfterUpdate 相位 ──────────────────────────

        /// <summary>S3 主测量:渲染帧步进 × onAfterUpdate 触发数的对偶,外加手动干跑竞争面。
        /// 回答四个问题:① yield 一帧 ⇒ frameCount 增量?(集群 -nographics 下也是?本批实测)
        /// ② 帧 +1 ⇒ onAfterUpdate 恰 +1?③ 手动 InputSystem.Update() 干跑 ⇒ 触发几次?
        /// ④ 手动干跑与自动帧更新是否**同一帧内叠合**(双计面,正式测试要抓的 Manual 竞争)。
        /// 本测只报告不红断言 —— 任一方向都是 spike 发现,写进结果 + Completion Notes。</summary>
        [UnityTest]
        public IEnumerator test_007_s3_on_after_update_phase_dynamics_logs()
        {
            LogAssert.ignoreFailingMessages = true;

            int fires = 0;
            void Cb() => fires++;
            InputSystem.onAfterUpdate += Cb;
            try
            {
                // ── ① 帧步进段:8 次 yield,null 一帧采样一次 ──
                for (int i = 0; i < 8; i++)
                {
                    int firesBefore = fires;
                    int frameBefore = Time.frameCount;
                    yield return null;
                    int frameAdvance = Time.frameCount - frameBefore;
                    int firesDelta = fires - firesBefore;
                    Report($"[007-S3] step{i}: frame_advance={frameAdvance} " +
                           $"on_after_update_delta={firesDelta} " +
                           $"cumulative_frames={Time.frameCount} cumulative_fires={fires}");
                }

                // ── ② 手动干跑段:一次 InputSystem.Update() 的触发数与帧增量 ──
                int firesBeforeManual = fires;
                int frameBeforeManual = Time.frameCount;
                InputSystem.Update();
                int firesManual = fires - firesBeforeManual;
                int frameAdvanceManual = Time.frameCount - frameBeforeManual;
                Report($"[007-S3] manual_dryrun_1: frame_advance={frameAdvanceManual} " +
                       $"on_after_update_delta={firesManual} (期望 1:一次 update 恰一次 onAfterUpdate;" +
                       "若 >1 ⇒ 自动更新与手动更新同帧叠合 = B2③「不得双计」要抓的面)");

                // ── ③ 手动干跑×3 序列:逐调用可数性(每调用恰 +1) ──
                int firesBeforeSeq = fires;
                InputSystem.Update();
                InputSystem.Update();
                InputSystem.Update();
                int firesSeq = fires - firesBeforeSeq;
                Report($"[007-S3] manual_dryrun_x3: " +
                       $"on_after_update_delta={firesSeq} (期望 3:手动驱动的调用级可数性)");

                // ── ④ 第二次自动帧段:确认前段测量未被手动干跑污染(回到每帧恰一次) ──
                int firesBefore2 = fires;
                yield return null;
                int firesAfter2 = fires - firesBefore2;
                Report($"[007-S3] post_manual_frame: on_after_update_delta={firesAfter2} " +
                       $"(段内帧增量=1,手动段不应改变帧-采样对偶)");
            }
            finally
            {
                InputSystem.onAfterUpdate -= Cb;
            }

            // 数字已全部落盘;sanity 判据:至少发生了一次帧步进(证明本装置真在跑 player loop)。
            Assert.IsTrue(fires > 0, "[007-S3] 整个测量段 onAfterUpdate 从未触发 —— 装置未接入 player update 相位");
        }
    }
}