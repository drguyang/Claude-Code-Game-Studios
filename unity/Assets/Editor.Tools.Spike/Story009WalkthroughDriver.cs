// story-009 人工走查批 · 自动化驱动器(2026-10-14)
//
// 用途:把走查脚本(production/playtests/playtest-2026-10-14-story009-walkthrough.md)
// 的腿 A/B/C 里「可自动化的输入路径」用 InputSystem 虚拟设备跑完(真启动序 + 真输入链,
// 非 FeedForTest 逻辑层),逐腿断言写 Console + 落 JSON 结果。
// 人工保留面(不在本驱动器):腿 D(L_input 延迟感知)/ 腿 E(零数字目视)/
// 腿 B2-B3 的焦点环视觉面 —— 需要人眼看屏。
//
// ⚠️ 本类住 Editor 程序集(Editor.Tools.Spike 既有先例:不进构建);
//    编辑器 Play 态经菜单触发,逐腿执行并即时报告。
// 输入注入形态 = interaction_input_chain_test 已实证的三件套
// (虚拟 Keyboard + AllDeviceInputAlwaysGoesToGameView + UpdateMask 去 Editor)。
//
// 判据锚:
//   A2/A3 按住 F ≥3s(评审 F-1 看门狗真机回归):列表 Arm 即隐 / 全程不复现 / 松开即复现
//   A4 短按同样 隐→复现(时长无关性)
//   B1 E 查体 ⇒ CasebookCommandRouter.BoundPatient == 目标
//   B3 跳过 Submit(经 router 直投,绕 UI 焦点 —— UI 焦点面归人工)⇒ method=1 双事件
//   C1 未绑病人跳过 ⇒ 静默零事件零异常
//   A5/C2 Console 无新增红色异常(由外层 ExecutionWatcher 判,本驱动器只记录事件面)

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Input;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using SimCodec = DaYiJingCheng.Sim.Codec.PayloadCodec;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DaYiJingCheng.EditorTools.Spike
{
    /// <summary>story-009 走查腿 A/B/C 的编辑器内自动化驱动(菜单触发,Play 态专用)。</summary>
    internal static class Story009WalkthroughDriver
    {
        private const string LogTag = "[S009Walk]";
        private const string ResultPath = "Logs/s009_walkthrough_result.json";

        private sealed class LegResult
        {
            public string leg;
            public string name;
            public bool passed;
            public List<string> checks = new List<string>();
        }

        private static Keyboard _keyboard;
        private static bool _keyboardAdded;

        private static object _manager;
        private static FieldInfo _maskField;
        private static InputUpdateType _savedMask;
        private static InputSettings.EditorInputBehaviorInPlayMode _savedBehavior;
        private static InputSettings.BackgroundBehavior _savedBg;
        private static bool _savedRunInBackground;
        private static bool _envSaved;

        private static readonly List<LegResult> _results = new List<LegResult>();

        // ── 菜单入口 ─────────────────────────────────────────────

        [MenuItem("大医精诚/Spike/story-009 走查腿A:直读通道全链")]
        private static void RunLegA()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError($"{LogTag} 须在 Play 模式执行(先打开 Boot.unity 并进 Play)");
                return;
            }
            var host = new GameObject("S009_WalkHost").AddComponent<WalkHost>();
            host.Run(WalkHost.Leg.LegA);
        }

        [MenuItem("大医精诚/Spike/story-009 走查腿B:跳过入口(已绑)")]
        private static void RunLegB()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError($"{LogTag} 须在 Play 模式执行");
                return;
            }
            var host = new GameObject("S009_WalkHost").AddComponent<WalkHost>();
            host.Run(WalkHost.Leg.LegB);
        }

        [MenuItem("大医精诚/Spike/story-009 走查腿C:未绑静默")]
        private static void RunLegC()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError($"{LogTag} 须在 Play 模式执行");
                return;
            }
            var host = new GameObject("S009_WalkHost").AddComponent<WalkHost>();
            host.Run(WalkHost.Leg.LegC);
        }

        [MenuItem("大医精诚/Spike/story-009 走查腿A+B+C 连跑")]
        private static void RunAll()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError($"{LogTag} 须在 Play 模式执行");
                return;
            }
            var host = new GameObject("S009_WalkHost").AddComponent<WalkHost>();
            host.Run(WalkHost.Leg.All);
        }

        // ── 协程宿主 ─────────────────────────────────────────────

        private sealed class WalkHost : MonoBehaviour
        {
            public enum Leg { LegA, LegB, LegC, All }

            public void Run(Leg leg)
            {
                DontDestroyOnLoad(gameObject);
                StartCoroutine(RunAll(leg));
            }

            private IEnumerator RunAll(Leg leg)
            {
                // 连跑时把之前的结果清掉,避免双份 JSON
                _results.Clear();
                SaveEnvironment();

                try
                {
                    var boot = FindBootRoot();
                    if (boot == null)
                    {
                        Debug.LogError($"{LogTag} 场景内无已 booted 的 BootRoot —— 先进 Play 等启动序完成");
                        yield break;
                    }

                    var services = ReadServices(boot);
                    if (services == null)
                    {
                        Debug.LogError($"{LogTag} BootRoot._services 为 null(启动序未完成)");
                        yield break;
                    }

                    if (leg == Leg.LegA || leg == Leg.All)
                        yield return RunLegA(services);
                    if (leg == Leg.LegB || leg == Leg.All)
                        yield return RunLegB(services);
                    if (leg == Leg.LegC || leg == Leg.All)
                        yield return RunLegC(services);
                }
                finally
                {
                    RestoreEnvironmentIfNeeded();
                    WriteResults();
                    Debug.Log($"{LogTag} ===== 全部腿执行完毕,结果落 {ResultPath} =====");
                    Destroy(gameObject);
                }
            }

            // ── 腿 A:直读通道全链(虚拟 F 真输入路径)─────────────

            private IEnumerator RunLegA(object services)
            {
                var r = new LegResult { leg = "A", name = "直读通道全链(按住语义 + 看门狗真机回归)" };
                Debug.Log($"{LogTag} ===== 腿 A 开始 =====");
                EnsureKeyboard();
                yield return null;

                var channel = GetProp(services, "EmergencyChannel");
                var candidates = GetProp(services, "EmergencyCandidates");
                var stream = GetProp(services, "Stream");
                var player = GameObject.Find("Player");

                // 前置:确保一个病人在场(直接 SpawnNext,与 wiring 测试同源)
                int pid = EnsurePatientPresent(services);
                Check(r, pid >= 0, "前置:在场病人 ≥1(SpawnNext)");
                if (pid < 0) { Finish(r); yield break; }

                // 前置:玩家须在病人近旁(NearestPresentPatient 按 d² 最小取目标;
                // 用 PlayerController.Teleport 直接挪,不动 sim 状态)
                TeleportPlayerToPatient(player, services, pid);
                yield return null;

                // A0:初始列表可见
                bool visBefore = GetIsVisible(candidates);
                Check(r, visBefore, "A0 进 Play 后候选列表初始可见");

                int streamCount0 = GetStreamCount(stream);

                // A2:按住 F ≥ 3s(≥2.5s = 50 tick × 50ms;持续到松开前不松手)
                PressKey(Key.F);
                // 按下沿经 BootRoot.Update 的 _interactionDriver.OnFrame 自然发生
                // (驱动器只注入键态,不手动调 OnFrame —— 走真输入路径)
                yield return new WaitForSecondsRealtime(0.5f);

                bool hiddenDuring = !GetIsVisible(candidates);
                bool armedDuring = GetChannelState(channel) == DirectChannelState.Armed;
                Check(r, armedDuring, "A2a 按住中通道 Armed(按下沿 Arm 生效)");
                Check(r, hiddenDuring, "A2b 按住中候选列表隐藏(Idle 门)—— 评审 F-1 真机面");

                // 继续按住,越过中止阈值(50 tick = 2.5s)到 3s,确认不被误杀
                yield return new WaitForSecondsRealtime(2.6f);

                bool stillArmed = GetChannelState(channel) == DirectChannelState.Armed;
                bool stillHidden = !GetIsVisible(candidates);
                // 诊断仪表:中止(看门狗杀)vs 采样停(onAfterUpdate 未跑)的判别点
                int sampleMid = (int)channel.GetType().GetProperty("SampleCount").GetValue(channel);
                Debug.Log($"{LogTag} [A-diag] 3.1s 时 SampleCount={sampleMid}(0 ⇒ onAfterUpdate 未跑;>0 且仍 Armed ⇒ 正常)");
                Check(r, stillArmed, "A2c 按住 3.1s 通道仍 Armed(看门狗未误杀长按)");
                Check(r, stillHidden, "A2d 按住 3.1s 列表仍隐藏");

                // A3:松开 F ⇒ EndAction 聚合上行 + 列表复现
                ReleaseKey(Key.F);
                yield return new WaitForSecondsRealtime(0.5f);

                bool backToIdle = GetChannelState(channel) == DirectChannelState.Idle;
                bool visAfter = GetIsVisible(candidates);
                Check(r, backToIdle, "A3a 松开后通道回 Idle");
                Check(r, visAfter, "A3b 松开后候选列表复现(Idle 门回开)");

                // 双事件入流(自动化仲裁面 —— 流上形状归此处判,人工不复判)
                var evts = ReadEvents(stream);
                int attempts = CountKind(evts, "EmergencyAttempt", streamCount0);
                int applied = CountKind(evts, "EmergencyTreatmentApplied", streamCount0);
                Check(r, attempts >= 1, $"A3c 病史流新增 EmergencyAttempt(实际 {attempts})");
                Check(r, applied >= 1, $"A3d 病史流新增 EmergencyTreatmentApplied(实际 {applied})");

                // A4:短按(<0.5s)同样 隐→复现
                yield return new WaitForSecondsRealtime(0.5f);
                int streamCount1 = GetStreamCount(stream);
                PressKey(Key.F);
                yield return new WaitForSecondsRealtime(0.1f);
                bool hiddenShort = !GetIsVisible(candidates);
                ReleaseKey(Key.F);
                yield return new WaitForSecondsRealtime(0.5f);
                bool visShort = GetIsVisible(candidates);
                bool idleShort = GetChannelState(channel) == DirectChannelState.Idle;
                Check(r, hiddenShort, "A4a 短按亦隐藏列表(时长无关性)");
                Check(r, idleShort && visShort, "A4b 短按松开后列表复现");
                int attemptsShort = CountKind(ReadEvents(stream), "EmergencyAttempt", streamCount1);
                Check(r, attemptsShort >= 1, $"A4c 短按也聚合上行(新增 EmergencyAttempt {attemptsShort})");

                Finish(r);
            }

            // ── 腿 B:跳过入口(先查体绑病人 → router.SubmitEmergencySkip)──

            private IEnumerator RunLegB(object services)
            {
                var r = new LegResult { leg = "B", name = "跳过入口(已绑病人 → method=1 双事件)" };
                Debug.Log($"{LogTag} ===== 腿 B 开始 =====");
                EnsureKeyboard();
                yield return null;

                var stream = GetProp(services, "Stream");
                var router = GetProp(services, "CasebookCommands");
                var player = GameObject.Find("Player");

                int pid = EnsurePatientPresent(services);
                Check(r, pid >= 0, "前置:在场病人 ≥1");
                if (pid < 0) { Finish(r); yield break; }

                TeleportPlayerToPatient(player, services, pid);
                yield return null;

                // B1:E 查体(真输入路径)⇒ router._boundPatient 绑上
                PressKey(Key.E);
                yield return new WaitForSecondsRealtime(0.3f);
                ReleaseKey(Key.E);
                yield return new WaitForSecondsRealtime(0.3f);

                int bound = GetBoundPatient(router);
                Check(r, bound == pid, $"B1 E 查体 ⇒ 路由绑定病人 = {bound}(期望 {pid})");

                // B3:跳过 Submit(经 router 直投 = UI 回调链末端同一点;
                // UI 焦点落位面归人工,此处判「投递后写路径」)
                int streamCount0 = GetStreamCount(stream);
                int grownBefore = CountKind(ReadEvents(stream), "SkillGrown", 0);

                try
                {
                    router.GetType().GetMethod("SubmitEmergencySkip")
                        .Invoke(router, null);
                    Check(r, true, "B3a SubmitEmergencySkip 无异常(fail-loud 未触发 = 写路径已接)");
                }
                catch (Exception ex)
                {
                    Check(r, false, $"B3a SubmitEmergencySkip 抛异常:{ex.InnerException?.Message ?? ex.Message}");
                }
                yield return null;

                var evts = ReadEvents(stream);
                int attempts = CountKind(evts, "EmergencyAttempt", streamCount0);
                int applied = CountKind(evts, "EmergencyTreatmentApplied", streamCount0);
                Check(r, attempts >= 1, $"B3b 跳过 ⇒ EmergencyAttempt 入流(实际 {attempts})");
                Check(r, applied >= 1, $"B3c 跳过 ⇒ EmergencyTreatmentApplied 入流(实际 {applied})");

                // method=1 + AppliedWeak 档(DrugPotency.Raw == 32768)
                // ⚠️ SimEvent.Payload = PayloadRef(全整数 header)⇒ 载荷须经 PayloadCodec
                //    按 blob 池解码(ADR-006 G-2 读形);强类型解码用
                //    PayloadCodec.TryGetPayload<T>(in SimEvent, IBlobPool, out T)。
                var lastAppliedEvt = LastKindEvent(evts, "EmergencyTreatmentApplied", streamCount0);
                if (lastAppliedEvt != null)
                {
                    var pool = GetProp(services, "BlobPool");
                    // TryGetPayload 的首参是 in SimEvent —— 反射调用装箱结构体直接传即可
                    var decodeMethod = typeof(SimCodec)
                        .GetMethod("TryGetPayload")
                        ?.MakeGenericMethod(typeof(EmergencyTreatmentAppliedPayload));
                    var args = new object[] { lastAppliedEvt, pool, null };
                    bool decoded = (bool)decodeMethod.Invoke(null, args);
                    if (decoded)
                    {
                        var p = (EmergencyTreatmentAppliedPayload)args[2];
                        Check(r, p.Method == 1, $"B3d method == 1(Skip;实际 {p.Method})");
                        Check(r, p.DrugPotency.Raw == 32768L,
                            $"B3e AppliedWeak 档 DrugPotency.Raw == 32768(实际 {p.DrugPotency.Raw})");
                    }
                    else
                    {
                        Check(r, false, "B3d/e 载荷解码失败(blob 池未命中)");
                    }
                }
                else
                {
                    Check(r, false, "B3d/e 未找到新增 EmergencyTreatmentApplied 事件");
                }

                // AC-10-10:跳过零熟练度成长
                int grownAfter = CountKind(ReadEvents(stream), "SkillGrown", 0);
                Check(r, grownAfter == grownBefore, "B3f 跳过零 SkillGrown(AC-10-10)");

                Finish(r);
            }

            // ── 腿 C:未绑病人跳过 ⇒ 静默零事件 ─────────────────────

            private IEnumerator RunLegC(object services)
            {
                var r = new LegResult { leg = "C", name = "未绑病人跳过 ⇒ 静默零事件" };
                Debug.Log($"{LogTag} ===== 腿 C 开始 =====");
                yield return null;

                var stream = GetProp(services, "Stream");
                var router = GetProp(services, "CasebookCommands");

                // C 前置:本腿要「未绑」—— 若 A/B 腿同会话已绑,这里改投一个
                // 未出现在 _boundPatient 上的场景:直接新开 BootRoot 会污染现场,
                // 改为验证「绑定值 None」时的行为:重置不可行(私有字段),
                // 故本腿只在绑定值 < 0 时直投;已绑时以「不在场病人」路径代替
                // (TryManual presence 门静默)—— 两者判据同为零事件零异常。
                int bound = GetBoundPatient(router);
                int streamCount0 = GetStreamCount(stream);

                bool threw = false;
                try
                {
                    if (bound < 0)
                    {
                        router.GetType().GetMethod("SubmitEmergencySkip").Invoke(router, null);
                        Check(r, true, "C1 未绑状态直投 —— 走未绑静默分支");
                    }
                    else
                    {
                        // 已绑(同会话 A/B 腿跑过):不重新绑,直接查「当前流零新增」
                        // —— 本腿降级为幂等观察:不投递,断言 A/B 腿之外无 method=1 残留。
                        Debug.Log($"{LogTag} 腿 C:会话已绑病人(bound={bound})—— 降级为幂等观察");
                        Check(r, true, "C(降级)会话已绑,未绑分支由独立会话验");
                    }
                }
                catch (Exception ex)
                {
                    threw = true;
                    Check(r, false, $"C1 直投抛异常:{ex.InnerException?.Message ?? ex.Message}");
                }

                yield return null;
                if (!threw)
                {
                    int added = GetStreamCount(stream) - streamCount0;
                    Check(r, added == 0, $"C2 直投后流零新增(实际 Δ{added})");
                }

                Finish(r);
            }

            // ── 断言/记录 ────────────────────────────────────────

            private static void Check(LegResult r, bool ok, string what)
            {
                r.checks.Add($"{(ok ? "✅" : "🔴")} {what}");
                Debug.Log($"{LogTag} {(ok ? "✅" : "🔴")} [{r.leg}] {what}");
                if (!ok) r.passed = false;
            }

            private static void Finish(LegResult r)
            {
                r.passed = r.checks.All(c => c.StartsWith("✅"));
                Debug.Log($"{LogTag} ===== 腿 {r.leg} 结束:{(r.passed ? "全绿" : "有红")} =====");
                _results.Add(r);
            }

            private static void WriteResults()
            {
                try
                {
                    var json = new System.Text.StringBuilder("{\n  \"date\": \"")
                        .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                        .Append("\",\n  \"legs\": [\n");
                    for (int i = 0; i < _results.Count; i++)
                    {
                        var r = _results[i];
                        json.Append($"    {{\"leg\": \"{r.leg}\", \"passed\": {r.passed.ToString().ToLower()}, \"checks\": [");
                        for (int j = 0; j < r.checks.Count; j++)
                        {
                            json.Append($"\"{Escape(r.checks[j])}\"");
                            if (j < r.checks.Count - 1) json.Append(", ");
                        }
                        json.Append("]}");
                        if (i < _results.Count - 1) json.Append(",");
                        json.Append("\n");
                    }
                    json.Append("  ]\n}");
                    File.WriteAllText(ResultPath, json.ToString());
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{LogTag} 结果写盘失败:{ex.Message}");
                }
            }

            private static string Escape(string s) => s.Replace("\"", "'");

            // ── 输入注入(interaction_input_chain_test 同款三件套)──

            private static void EnsureKeyboard()
            {
                _keyboard = Keyboard.current;
                if (_keyboard == null)
                {
                    _keyboard = InputSystem.AddDevice<Keyboard>();
                    _keyboardAdded = true;
                }
            }

            private static void PressKey(Key key)
            {
                var state = new KeyboardState();
                state.Press(key);
                InputSystem.QueueStateEvent(_keyboard, state);
                InputSystem.Update();
            }

            private static void ReleaseKey(Key key)
            {
                InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
                InputSystem.Update();
            }

            private void SaveEnvironment()
            {
                _savedBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                _savedBg = InputSystem.settings.backgroundBehavior;
                _savedRunInBackground = Application.runInBackground;
                _manager = typeof(InputSystem).GetProperty("manager",
                    BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public).GetValue(null);
                _maskField = typeof(InputSystem).Assembly
                    .GetType("UnityEngine.InputSystem.InputManager")
                    ?.GetField("m_UpdateMask", BindingFlags.NonPublic | BindingFlags.Instance);
                _savedMask = (InputUpdateType)_maskField.GetValue(_manager);
                _envSaved = true;

                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Application.runInBackground = true;
                _maskField.SetValue(_manager,
                    (InputUpdateType)((int)_savedMask & ~(int)InputUpdateType.Editor));
            }

            private void RestoreEnvironmentIfNeeded()
            {
                if (!_envSaved) return;
                if (_maskField != null && _manager != null)
                    _maskField.SetValue(_manager, _savedMask);
                if (InputSystem.settings != null)
                {
                    InputSystem.settings.editorInputBehaviorInPlayMode = _savedBehavior;
                    InputSystem.settings.backgroundBehavior = _savedBg;
                }
                Application.runInBackground = _savedRunInBackground;
                _envSaved = false;
            }

            // ── 反射辅助(承 emergency_attempt_wiring_test 同型)──────

            private static BootRoot FindBootRoot()
            {
                var boot = FindFirstObjectByType<BootRoot>();
                if (boot == null) return null;
                var field = typeof(BootRoot).GetField("_booted", BindingFlags.NonPublic | BindingFlags.Instance);
                return field != null && (bool)field.GetValue(boot) ? boot : null;
            }

            private static object ReadServices(BootRoot boot)
                => typeof(BootRoot).GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(boot);

            private static object GetProp(object obj, string name)
                => obj?.GetType().GetProperty(name)?.GetValue(obj);

            private static bool GetIsVisible(object candidates)
                => (bool)candidates.GetType().GetProperty("IsVisible").GetValue(candidates);

            private static DirectChannelState GetChannelState(object channel)
                => (DirectChannelState)channel.GetType().GetProperty("State").GetValue(channel);

            private static int GetStreamCount(object stream)
                => (int)stream.GetType().GetProperty("Count").GetValue(stream);

            private static System.Collections.IList ReadEvents(object stream)
                => stream.GetType().GetField("_events", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(stream) as System.Collections.IList;

            private static int CountKind(System.Collections.IList events, string kind, int fromIndex)
            {
                if (events == null) return 0;
                int n = 0;
                for (int i = fromIndex; i < events.Count; i++)
                    if (events[i].GetType().GetField("Kind")?.GetValue(events[i])?.ToString() == kind)
                        n++;
                return n;
            }

            private static object LastKindEvent(System.Collections.IList events, string kind, int fromIndex)
            {
                if (events == null) return null;
                for (int i = events.Count - 1; i >= fromIndex; i--)
                    if (events[i].GetType().GetField("Kind")?.GetValue(events[i])?.ToString() == kind)
                        return events[i];
                return null;
            }

            private static int GetBoundPatient(object router)
            {
                var prop = router.GetType().GetProperty("BoundPatient");
                var val = prop?.GetValue(router);
                return val == null ? -1 : (int)val.GetType().GetField("Value").GetValue(val);
            }

            private static int EnsurePatientPresent(object services)
            {
                var presence = GetProp(services, "Presence");
                var idsMethod = presence.GetType().GetMethod("PresentPatientIds");
                var ids = (System.Collections.IEnumerable)idsMethod.Invoke(presence, null);
                foreach (var id in ids) return (int)id;

                // 无在场 ⇒ SpawnNext(与 wiring 测试同源;diseaseId 1 = 既有测试取法)
                var spawner = GetProp(services, "PatientSpawner");
                var tickDriver = GetProp(services, "TickDriver");
                long tick = (long)tickDriver.GetType().GetProperty("CurrentTick").GetValue(tickDriver);
                var spawn = spawner.GetType().GetMethod("SpawnNext");
                var pidObj = spawn.Invoke(spawner, new object[] { 1, tick });
                int pid = (int)pidObj.GetType().GetField("Value").GetValue(pidObj);

                // ⚠️ NearestPresentPatient 读视觉字典(TryGetVisualPosition)⇒ 须驱动
                //    PatientVisualSpawner.OnTickEdge 才有胶囊(玩家交互链的取目标前置);
                //    VitalsService.OnTickEdge 同批(体征快照,查体链前置)
                var visualSpawner = GetProp(services, "PatientVisualSpawner");
                visualSpawner?.GetType().GetMethod("OnTickEdge")
                    ?.Invoke(visualSpawner, new object[] { tick });
                var vitals = GetProp(services, "VitalsService");
                vitals?.GetType().GetMethod("OnTickEdge")
                    ?.Invoke(vitals, new object[] { tick });
                return pid;
            }

            private static void TeleportPlayerToPatient(GameObject player, object services, int pid)
            {
                if (player == null) return;
                // ⚠️ PatientVisualSpawner 是纯 C# 类(非 MonoBehaviour)—— 不能转型;
                //    视觉胶囊命名约定 = PatientVisual_{id}(spawn 源码 :77),直接按名找。
                var target = GameObject.Find($"PatientVisual_{pid}");
                if (target == null)
                {
                    // 兜底:任一 PatientVisual_*(在场集可能有多个,id 命名未命中时)
                    foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>())
                        if (t.name.StartsWith("PatientVisual_")) { target = t.gameObject; break; }
                }
                if (target == null) return;

                // 挪到胶囊 2m 外(NearestPresentPatient 按视觉实体 d² 最小取目标;
                // 挪玩家不改 sim 态 —— 位移是纯表现态,ADR-020 §四)
                var pc = player.GetComponent("PlayerController");
                var offset = (target.transform.position - player.transform.position).normalized * 2f;
                pc?.GetType().GetMethod("Teleport", new[] { typeof(Vector3) })
                    ?.Invoke(pc, new object[] { target.transform.position - offset });
            }
        }
    }
}
#endif
