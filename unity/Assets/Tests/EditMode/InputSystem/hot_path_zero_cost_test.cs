// Story 010 · 热路径零成本(预缓存 · Idle 零调用 · Armed 零分配)
//
// 权威来源:
//   Story: production/epics/input-system/story-010-hot-path-zero-cost.md
//     · AC-3-E1(BLOCKING)热路径零每帧字符串查找(Roslyn 守门:FindAction / FindActionMap / 索引器)
//     · AC-3-E4(BLOCKING)Idle 零调用 = Emergency action enabled == false ∧ 回调计数 == 0
//     · AC-3-E5(BLOCKING)Armed 读路径 GC.Alloc == 0;方法学四要素(统计量/窗口/剔除/工具)
//   TR: docs/architecture/tr-registry.yaml TR-input-012
//   ADR: ADR-011 §二 / Amendment A / Amendment B(输入架构)
//   GDD: input-system.md §Edge Cases 三 / §Tuning Knobs 五
//
// E5 零分配须 Development Build + Mono/IL2CPP 双后端 ProfilerRecorder 探针,
// 在 EditMode 不可执行 —— 方法学记录贴在 production/qa/evidence/story-010-evidence.md,
// 本文件只落 E1/E4 的可执行断言(E5 的结构可达性由实现代码保证)。

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Input;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.InputSystem
{
    /// <summary>Story 010 热路径零成本的 EditMode 测试(E1 Roslyn 禁令 / E4 Idle 零调用)。</summary>
    [TestFixture]
    internal sealed class HotPathZeroCostTest
    {
        private const string ActionAssetPath = "Assets/InputSystem_Actions.inputactions";
        private const int TestAxialScale = 65536;
        private const int TestDzMag = 0;
        private const int TestMagMax = 65536;

        // ══════════ AC-3-E1 · 热路径零字符串查找(Roslyn 守门) ═══════════

        /// <summary>E1 端到端:InputBoundaryGates.RunAll 内的 CheckHotPathStringLookups 当前树零红错。</summary>
        [Test]
        public void test_e1_hot_path_string_lookup_real_tree_zero_errors()
        {
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "E1 端到端前提:编译成功(读源码文本面不受编译状态影响,但 RunAll 内其他门需要)");

            var allErrs = InputBoundaryGates.RunAll(out _);

            var e1Errs = allErrs.Where(e => e.StartsWith("[E1]")).ToList();
            Assert.That(e1Errs, Is.Empty,
                $"[E1] 热路径零字符串查找禁令违例({e1Errs.Count} 条):\n  " +
                string.Join("\n  ", e1Errs.Take(10)));
        }

        /// <summary>E1 负例:往热路径方法体内注入 FindAction 字符串 → 禁令应捕获(合成文本纯函数)。</summary>
        [Test]
        public void test_e1_negative_fixture_hot_path_with_find_action_returns_red()
        {
            const string fixture = @"
using UnityEngine.InputSystem;
class Fixture {
    InputActionAsset _actions;
    void OnAfterUpdate() {
        var a = _actions.FindAction(""Emergency"");
    }
}";
            var errs = InputBoundaryGates.CheckHotPathStringLookups(fixture, "Fixture.cs");
            Assert.That(errs, Is.Not.Empty,
                "负例:OnAfterUpdate 内含 FindAction 应被 [E1] 捕获");
            Assert.That(errs[0], Does.Contain("[E1]"),
                $"负例首条应为 [E1] 前缀,实际: {errs[0]}");
        }

        /// <summary>E1 热路径方法名集合完整覆盖全部 14 个帧内方法。</summary>
        [Test]
        public void test_e1_hot_path_methods_no_prohibited_tokens()
        {
            var names = InputBoundaryGates.HotPathMethodNames;
            Assert.That(names.Count, Is.EqualTo(15), "热路径集合应恰好 15 个方法名");
            Assert.That(names, Does.Contain("OnAfterUpdate"));
            Assert.That(names, Does.Contain("Arm"));
            Assert.That(names, Does.Contain("DisableEmergencyAction"));
            Assert.That(names, Does.Contain("ReadEmergency"));
            Assert.That(names, Does.Contain("EnableEmergencyAction"));
            Assert.That(names, Does.Contain("Feed"));
            Assert.That(names, Does.Contain("FeedForTest"));
            Assert.That(names, Does.Contain("Sample"));
            Assert.That(names, Does.Contain("EndAttempt"));
            Assert.That(names, Does.Contain("EndAction"));
            Assert.That(names, Does.Contain("AbortAttempt"));
            Assert.That(names, Does.Contain("AbortAction"));
            Assert.That(names, Does.Contain("ResetToIdle"));
            Assert.That(names, Does.Contain("MagnitudeFromAxis"));
            Assert.That(names, Does.Contain("RoundHalfAwayFromZero"));
        }

        /// <summary>E1 初始化路径不在热路径集合(构造/Attach/Init 均未入列)。</summary>
        [Test]
        public void test_e1_initialization_path_exempt_from_ban()
        {
            var names = InputBoundaryGates.HotPathMethodNames;
            Assert.That(names, Is.All.Match(@"^(Arm|DisableEmergencyAction|EnableEmergencyAction|OnAfterUpdate|ReadEmergency|Feed|FeedForTest|Sample|EndAttempt|EndAction|AbortAttempt|AbortAction|ResetToIdle|MagnitudeFromAxis|RoundHalfAwayFromZero)$"),
                "热路径集合只应包含帧内方法,不含初始化方法");
        }

        // ══════════ AC-3-E4 · Idle 零调用(动作 disabled + 回调计数归零) ═══════════

        /// <summary>E4 默认态:EmergencyActionEnabled == false, EmergencyCallbackCount == 0。</summary>
        [Test]
        public void test_e4_idle_emergency_action_disabled_by_default()
        {
            var channel = new EmergencyDirectReadChannel(null, TestAxialScale, TestDzMag, TestMagMax);
            Assert.That(channel.EmergencyActionEnabled, Is.False,
                "E4 ①:Idle 态 Emergency 动作必须 disabled");
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0),
                "E4 ②:Idle 态回调计数必须为 0");
        }

        /// <summary>E4 Arm → Idle 回:EndAction 后 enabled 归 false 且回调计数归零。</summary>
        [Test]
        public void test_e4_armed_to_idle_callback_count_resets_to_zero()
        {
            var channel = new EmergencyDirectReadChannel(null, TestAxialScale, TestDzMag, TestMagMax);
            channel.Arm(0);
            Assert.That(channel.EmergencyActionEnabled, Is.True,
                "Armed 态 Emergency 动作必须 enabled");

            int frame = UnityEngine.Time.frameCount;
            channel.NotifyAfterUpdateForTest(frame);
            channel.NotifyAfterUpdateForTest(frame + 1);
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(2),
                "Armed 期两次回调应累计到 2");

            channel.EndAction();
            Assert.That(channel.EmergencyActionEnabled, Is.False,
                "EndAction 后必须回 Idle(enabled = false)");
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0),
                "EndAction 后回调计数必须归零");
        }

        /// <summary>E4 Arm → Idle 回:AbortAction 后 enabled 归 false 且回调计数归零。</summary>
        [Test]
        public void test_e4_abort_to_idle_callback_count_resets_to_zero()
        {
            var channel = new EmergencyDirectReadChannel(null, TestAxialScale, TestDzMag, TestMagMax);
            channel.Arm(0);
            int frame = UnityEngine.Time.frameCount;
            channel.NotifyAfterUpdateForTest(frame);
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(1));

            channel.AbortAction();
            Assert.That(channel.EmergencyActionEnabled, Is.False,
                "AbortAction 后必须回 Idle(enabled = false)");
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0),
                "AbortAction 后回调计数必须归零");
        }

        /// <summary>E4 DisableEmergencyAction 直接归零(不依赖 End/Abort 路径)。</summary>
        [Test]
        public void test_e4_disable_emergency_action_resets_callback_count()
        {
            var channel = new EmergencyDirectReadChannel(null, TestAxialScale, TestDzMag, TestMagMax);
            channel.Arm(0);
            int frame = UnityEngine.Time.frameCount;
            channel.NotifyAfterUpdateForTest(frame);
            channel.NotifyAfterUpdateForTest(frame + 1);
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(2));

            channel.DisableEmergencyAction();
            Assert.That(channel.EmergencyActionEnabled, Is.False);
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0),
                "DisableEmergencyAction 必须同时归零回调计数");
        }

        /// <summary>E4 回调计数只数 wired onAfterUpdate(EnableEmergencyAction 不影响计数,只数回调触发)。</summary>
        [Test]
        public void test_e4_callback_count_only_counts_wired_onAfterUpdate()
        {
            var channel = new EmergencyDirectReadChannel(null, TestAxialScale, TestDzMag, TestMagMax);
            // 不 Attach(无 wired 回调),直接 Enable
            channel.EnableEmergencyAction();
            Assert.That(channel.EmergencyActionEnabled, Is.True);
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0),
                "Enable 不触发回调,计数保持 0");

            channel.DisableEmergencyAction();
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0),
                "Disable 不触发回调,计数保持 0");
        }

        /// <summary>E4 多次回调累计后 Arm → EndAction 循环可复现(状态机可逆)。</summary>
        [Test]
        public void test_e4_multiple_callbacks_accumulate_then_reset()
        {
            var channel = new EmergencyDirectReadChannel(null, TestAxialScale, TestDzMag, TestMagMax);
            int frame = UnityEngine.Time.frameCount;

            // 第一次周期
            channel.Arm(0);
            for (int i = 0; i < 5; i++) channel.NotifyAfterUpdateForTest(frame + i);
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(5));
            channel.EndAction();
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0));

            // 第二次周期
            channel.Arm(1);
            for (int i = 0; i < 3; i++) channel.NotifyAfterUpdateForTest(frame + i);
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(3));
            channel.AbortAction();
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0));
        }

        /// <summary>E4 接线模式 Attach 后 Idle 态仍不触发回调(动作 disabled + 状态非 Armed)。</summary>
        [Test]
        public void test_e4_idle_after_attach_no_callbacks()
        {
            // 逻辑层模式不能 Attach —— 用接线模式(真实动作资产)
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionAssetPath);
            Assert.That(asset, Is.Not.Null, "动作资产未找到:" + ActionAssetPath);
            var channel = new EmergencyDirectReadChannel(asset, TestAxialScale, TestDzMag, TestMagMax);

            channel.Attach();
            try
            {
                // Idle 态(未 Arm):enabled = false, 即使挂接 onAfterUpdate 也不应计数
                Assert.That(channel.EmergencyActionEnabled, Is.False);
                Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0));

                int frame = UnityEngine.Time.frameCount;
                channel.NotifyAfterUpdateForTest(frame);
                Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0),
                    "Idle 态 wired 回调不应增加计数(enabled == false 提前 return)");
            }
            finally
            {
                channel.Detach();
            }
        }

        /// <summary>E4 反复 Enable/Disable 抖动后计数仍归零(不经过 Armed 状态机)。</summary>
        [Test]
        public void test_e4_rapid_enable_disable_jitter_count_stays_zero()
        {
            var channel = new EmergencyDirectReadChannel(null, TestAxialScale, TestDzMag, TestMagMax);
            for (int i = 0; i < 10; i++)
            {
                channel.EnableEmergencyAction();
                Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0),
                    $"第 {i + 1} 轮 Enable 后计数应为 0");
                channel.DisableEmergencyAction();
                Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0),
                    $"第 {i + 1} 轮 Disable 后计数应为 0");
            }
        }

        /// <summary>E4 AC edge case 「Suspended 后回 Idle」:直接设状态为 Suspended 后
        /// 调用 <see cref="EmergencyDirectReadChannel.ResetToIdle"/> —— 回 Idle 时
        /// <see cref="DisableEmergencyAction"/> 必须同时归零回调计数(Story 008 设备态
        /// 事件驱动接入前用反射设态;ResetToIdle 已调用 DisableEmergencyAction = 本测试的
        /// 实际断言面)。</summary>
        [Test]
        public void test_e4_suspended_to_idle_callback_count_resets()
        {
            var channel = new EmergencyDirectReadChannel(null, TestAxialScale, TestDzMag, TestMagMax);
            // 经反射设态为 Suspended(Story 008 才有公开入口;当前用测试缝直设)
            var stateField = typeof(EmergencyDirectReadChannel).GetField(
                "_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            stateField.SetValue(channel, DirectChannelState.Suspended);

            // 逻辑层模式须显式 EnableEmergencyAction，否则回调体内部
            // `_emergencyActionEnabled` 为 false 而提前 return，不进入计数
            channel.EnableEmergencyAction();

            // 模拟 Suspended 期若干 wired 回调
            int frame = UnityEngine.Time.frameCount;
            channel.NotifyAfterUpdateForTest(frame);
            channel.NotifyAfterUpdateForTest(frame + 1);
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(2),
                "Suspended 期 wired 回调仍应计数(enabled=true)");

            // ResetToIdle 回 Idle —— DisableEmergencyAction 清零回调计数
            var resetMethod = typeof(EmergencyDirectReadChannel).GetMethod(
                "ResetToIdle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            resetMethod.Invoke(channel, null);

            Assert.That(channel.State, Is.EqualTo(DirectChannelState.Idle),
                "ResetToIdle 后状态必须回 Idle");
            Assert.That(channel.EmergencyActionEnabled, Is.False,
                "ResetToIdle 后 Emergency 动作必须 disabled");
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0),
                "ResetToIdle 后回调计数必须归零(Suspended→Idle edge case)");
        }

        // ══════════ AC-3-E5 · Armed 零分配(结构可达性 — EditMode 只断可达,实测归 Development Build) ═══════════

        /// <summary>E5 结构可达性 ①:EnableEmergencyAction / DisableEmergencyAction 存在且接线侧
        /// OnAfterUpdate 在 enabled=false 时提前 return —— 结构上保证零回调(分配测量本身
        /// 须 Development Build + ProfilerRecorder,见 production/qa/evidence/story-010-evidence.md)。</summary>
        [Test]
        public void test_e5_enable_disable_structural_reachable()
        {
            var channel = new EmergencyDirectReadChannel(null, TestAxialScale, TestDzMag, TestMagMax);

            // EnableEmergencyAction 存在且可调用
            channel.EnableEmergencyAction();
            Assert.That(channel.EmergencyActionEnabled, Is.True);

            // DisableEmergencyAction 存在且可调用
            channel.DisableEmergencyAction();
            Assert.That(channel.EmergencyActionEnabled, Is.False);
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0));
        }

        /// <summary>E5 结构可达性 ②:Arm(真资产) → 触发 OnAfterUpdate → ReadEmergency → Feed
        /// 整条读路径,确认 `SampleCount` 递增 —— 结构上 ReadEmergency 可达且产样本(分配测量本身
        /// 须 Development Build + ProfilerRecorder,见 production/qa/evidence/story-010-evidence.md)。</summary>
        [Test]
        public void test_e5_armed_read_path_traversed_sample_increments()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionAssetPath);
            Assert.That(asset, Is.Not.Null, "动作资产未找到:" + ActionAssetPath);
            var channel = new EmergencyDirectReadChannel(asset, TestAxialScale, TestDzMag, TestMagMax);
            channel.Arm(0);

            int frame = UnityEngine.Time.frameCount;
            channel.NotifyAfterUpdateForTest(frame);
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(1),
                "Armed 期 wired 回调应触发一次");
            Assert.That(channel.SampleCount, Is.EqualTo(1),
                "ReadEmergency → Feed 整条读路径应产一个样本");

            channel.NotifyAfterUpdateForTest(frame + 1);
            Assert.That(channel.SampleCount, Is.EqualTo(2),
                "第二帧回调应再产一个样本");

            channel.EndAction();
            Assert.That(channel.EmergencyCallbackCount, Is.EqualTo(0),
                "EndAction 回 Idle 后回调计数归零");
        }

        /// <summary>E5 结构可达性 ③:初始化路径不在热路径集合 —— 构造 / Attach / Detach
        /// 均未入列 <see cref="InputBoundaryGates.HotPathMethodNames"/>,故初始化方法内含
        /// <c>FindAction("Emergency")</c> 不应被 E1 捕获。</summary>
        [Test]
        public void test_e1_initialization_methods_excluded_from_ban()
        {
            const string initSrc = @"
using UnityEngine.InputSystem;
class Fixture {
    InputActionAsset _actions;
    Fixture() {
        _actions.FindAction(""Emergency"");
    }
    void Attach() {
        _actions.FindAction(""Emergency"");
    }
    void Detach() {
        _actions.FindAction(""Emergency"");
    }
}";
            var errs = InputBoundaryGates.CheckHotPathStringLookups(initSrc, "Fixture.cs");
            Assert.That(errs, Is.Empty,
                "初始化方法(构造/Attach/Detach)含 FindAction 不应被 E1 捕获");
        }
    }
}
