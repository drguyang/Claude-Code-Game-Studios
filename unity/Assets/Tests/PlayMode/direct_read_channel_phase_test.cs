// Story 007 · 急救直读通道与输入更新相位 —— PlayMode 真身(B2③ 相位对偶 + B1a 真实注入接线)
//
// 权威来源:production/epics/input-system/story-007-direct-read-channel.md(判据权威,AC 原文)
//   · AC-3-B2③(BLOCKING)性质断言「输入更新相位 = 渲染帧相位、每帧恰一次」——
//     Time.frameCount +1 ⇒ 采样计数恰 +1(排除 Fixed 0/2 次与 Manual 竞争);
//     **断言内不得出现 updateMode == Dynamic 枚举字面比较**(比对的是性质,不是模式名)。
//     Edge: 一次推进跨多帧累积;手动 InputSystem.Update() 干跑 ⇒ 不得双计。
//     Negative: updateMode 改 Fixed ⇒ 必须抓红(性质失配 ⇒ 有一步 sampleDelta != frameDelta)。
//   · AC-3-B1a(BLOCKING · P0)零硬件合成注入 → 读数可见性 ≤1 帧且同帧可见 ——
//     本文件跑**真实接线面**:Arm+Attach 挂上 onAfterUpdate 回调后,QueueStateEvent 注入
//     真实按键,下一帧回调采样(逻辑面的 FeedForTest 直读等价形态见 EditMode 真身)。
//
// 为什么 B2③ 只在 PlayMode:EditMode 不跑渲染帧步进,拿不到真实的「帧 +1」player loop;
// 必须在 PlayMode 协程里 yield return null 才有帧相位。S1/S3 spike 已实测(2026-09-26,
// unity/Logs/story007_spike_results.txt):
//   · updateMode 默认 = ProcessEventsInDynamicUpdate(int 1),非 Fixed 非 Manual
//   · 8/8 步 frame_advance=1 且 on_after_update_delta=1(动态模式下回调跟帧)
//   · 手动 InputSystem.Update() 干跑:on_after_update_delta=0(**不**触发 onAfterUpdate
//     ⇒ 通道内 SampleCount 不变;若双计则 delta>=1 ⇒ 本测试抓红)
//   · 0 条 Error 日志(本文件不设 LogAssert.ignoreFailingMessages,严格收集)
//
// GDD input-system.md 规则七(直读在 onAfterUpdate 回调,非轮询)· ADR-011 §二(直读独立于 42)。
//
// 落点注记:故事 Test Evidence 登记口径 = tests/integration/input_system/…;
//   Unity 只编译 unity/Assets/ 树 ⇒ 真身落本路径(承 Story 001/005/006 同一先例)。
// 纪律:确定性(无随机 / 无墙钟依赖)· 手柄/键鼠夹具自管生命周期(Device 与 Asset 均
//   本夹具 Add/Remove,不依赖场景预置)· 退出态全部 finally 恢复。

using System.Collections;
using System.Reflection;
using DaYiJingCheng.Gameplay.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>Story 007 直读通道的 PlayMode 测试:输入更新相位性质(B2③)+ 真实注入同帧可见(B1a)。
    /// B1a 逻辑面 / B2① 结构门 / B2② Roslyn 门的真身在 EditMode(direct_read_channel_test.cs)。</summary>
    [TestFixture]
    internal sealed class DirectReadChannelPhaseTest
    {
        // ── 测试乘子(结构性合法常量:F-10.1 形状 AXIAL_SCALE ≥ MAG_MAX · DZ_MAG ≥ 0)──
        // ⚠️ 数值归 OQ-10-7 数值轮 —— 本处只取「1.0 轴 → 65536 满幅」的可读值证形状,不代表裁定值。
        private const int TestAxialScale = 65536;
        private const int TestDzMag = 0;
        private const int TestMagMax = 65536;

        private const string ActionMapName = "Player";
        private const string ActionName = "Emergency";
        private const string KeyboardBinding = "<Keyboard>/f";
        private const int ArmedOrdinal = 11;

        /// <summary>测试夹具:内联创建动作资产(不依赖场景预置,规避 UnityEditor 资产加载依赖)、
        /// 注入一个真实 Keyboard 设备、Arm+Attach 通道并 enable 动作。调用方负责在 finally 中 Cleanup。</summary>
        private sealed class ChannelFixture
        {
            public InputActionAsset Asset;
            public InputAction Action;
            public Keyboard Keyboard;
            public EmergencyDirectReadChannel Channel;

            private object _manager;
            private FieldInfo _maskField;
            private InputUpdateType _savedMask;
            private InputSettings.EditorInputBehaviorInPlayMode _savedBehavior;
            private InputSettings.BackgroundBehavior _savedBg;
            private bool _savedRunInBackground;

            public void Setup()
            {
                // 输入环境必须在 AddDevice 之前设好 —— 无焦点 batch 下若以默认
                // backgroundBehavior 加入键盘,AddDevice 会立刻给它挂上
                // DisabledWhileInBackground(InputManager 后台保活判定),此后 player
                // update 里的事件全被 enabled 检查丢弃(诊断实测 evCount=0)。
                // 三件套 + 关 Editor update = 官方 InputTestFixture 配方的 batch 等价物
                // (公开 updateMask setter 在编辑器会强制 |= Editor,只能反射直写);
                // Cleanup 恢复全部四项。
                _savedBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                _savedBg = InputSystem.settings.backgroundBehavior;
                _savedRunInBackground = Application.runInBackground;
                _manager = typeof(InputSystem).GetProperty("manager",
                    BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public).GetValue(null);
                _maskField = typeof(InputSystem).Assembly
                    .GetType("UnityEngine.InputSystem.InputManager")
                    .GetField("m_UpdateMask", BindingFlags.NonPublic | BindingFlags.Instance);
                _savedMask = (InputUpdateType)_maskField.GetValue(_manager);

                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Application.runInBackground = true;
                _maskField.SetValue(_manager,
                    (InputUpdateType)((int)_savedMask & ~(int)InputUpdateType.Editor));

                Asset = ScriptableObject.CreateInstance<InputActionAsset>();
                Asset.AddActionMap(ActionMapName);
                Action = Asset.FindActionMap(ActionMapName, throwIfNotFound: true)
                    .AddAction(ActionName, InputActionType.Button, binding: KeyboardBinding);

                Keyboard = InputSystem.AddDevice<Keyboard>();

                Channel = new EmergencyDirectReadChannel(Asset, TestAxialScale, TestDzMag, TestMagMax);
                Channel.Arm(ArmedOrdinal);
                Channel.Attach();
                Action.Enable();
            }

            public void Cleanup()
            {
                if (Channel != null)
                {
                    Channel.AbortAction();
                    Channel.Detach();
                    Channel = null;
                }
                if (Action != null)
                {
                    Action.Disable();
                    Action = null;
                }
                if (Keyboard != null && Keyboard.added)
                {
                    InputSystem.RemoveDevice(Keyboard);
                    Keyboard = null;
                }
                if (Asset != null)
                {
                    Object.DestroyImmediate(Asset);
                    Asset = null;
                }
                _maskField.SetValue(_manager, _savedMask);
                InputSystem.settings.editorInputBehaviorInPlayMode = _savedBehavior;
                InputSystem.settings.backgroundBehavior = _savedBg;
                Application.runInBackground = _savedRunInBackground;
            }

            /// <summary>按下 F 键(经 InputSystem 队列,下一帧 InputSystem.Update 时生效)。</summary>
            public void PressF() => InputSystem.QueueStateEvent(Keyboard, new KeyboardState(Key.F));

            /// <summary>释放 F 键(清空键盘状态)。</summary>
            public void ReleaseF() => InputSystem.QueueStateEvent(Keyboard, new KeyboardState());
        }

        // ══════════ AC-3-B2③ · 输入更新相位 = 渲染帧相位、每帧恰一次 ══════════

        /// <summary>AC-3-B2③ 正例:8 步连续推进,每步 frameCount+1 ⇒ SampleCount 恰 +1
        /// (纯性质断言,不比对 updateMode 枚举字面)。偏离此性质(0 次或 2 次)即红。</summary>
        [UnityTest]
        public IEnumerator test_direct_read_phase_frame_plus_one_yields_sample_plus_one()
        {
            var fx = new ChannelFixture();
            fx.Setup();
            try
            {
                // 先落一帧让 Attach 后的首次更新稳定(避免把 SetUp 帧的采样算进首步)
                yield return null;
                int expectedSamples = fx.Channel.SampleCount;

                for (int i = 0; i < 8; i++)
                {
                    int frameBefore = Time.frameCount;
                    yield return null;
                    int frameDelta = Time.frameCount - frameBefore;
                    int sampleDelta = fx.Channel.SampleCount - expectedSamples;

                    Assert.That(frameDelta, Is.EqualTo(1), $"第 {i} 步渲染帧推进必须恰为 1");
                    Assert.That(sampleDelta, Is.EqualTo(frameDelta),
                        $"第 {i} 步采样增量({sampleDelta})必须 = 帧推进({frameDelta})" +
                        " —— 输入更新相位必须与渲染帧相位 1:1(AC-3-B2③)");
                    expectedSamples = fx.Channel.SampleCount;
                }
            }
            finally
            {
                fx.Cleanup();
            }
        }

        /// <summary>AC-3-B2③ Edge「手动 InputSystem.Update() 干跑 ⇒ 不得双计」:聚焦环境 /
        /// 玩家构建下手动 Update 会在同帧再触发 onAfterUpdate —— 通道同帧去重闸拦下 ⇒
        /// 采样计数不变;下一帧正常推进恰 +1。若双计(手动调用被算成第二个样本)⇒ 此处
        /// delta>=1 ⇒ 红。</summary>
        [UnityTest]
        public IEnumerator test_direct_read_phase_manual_update_dry_run_does_not_double_count()
        {
            var fx = new ChannelFixture();
            fx.Setup();
            try
            {
                yield return null; // 稳定一帧
                int beforeDryRun = fx.Channel.SampleCount;

                InputSystem.Update(); // 手动干跑 —— 同帧不得多出样本
                Assert.That(fx.Channel.SampleCount, Is.EqualTo(beforeDryRun),
                    "手动 InputSystem.Update() 同帧干跑不得多出采样(双计 ⇒ 此处红,AC-3-B2③ Edge)");

                yield return null; // 下一帧正常推进
                int afterNormalFrame = fx.Channel.SampleCount;
                Assert.That(afterNormalFrame - beforeDryRun, Is.EqualTo(1),
                    "干跑后下一帧必须恰采样 1 次(证明采样只跟渲染帧,不跟手动调用计数)");
            }
            finally
            {
                fx.Cleanup();
            }
        }

        /// <summary>AC-3-B2③ Negative「updateMode 改 Fixed ⇒ 必须抓红」:切到 Fixed 模式后,
        /// 固定步频与渲染帧频不对齐(本夹具固定 fixedDeltaTime=0.5s ⇒ 1 个渲染帧内
        /// Fixed 更新可能 0 次或多次)⇒ ≤12 步内必能检出至少一步 sampleDelta != frameDelta。
        /// finally 恢复原 updateMode / fixedDeltaTime,并对保存的原值断言恢复成功
        /// (不比对任何 Dynamic 枚举字面 —— 只比对「值 = 保存的原值」)。</summary>
        [UnityTest]
        public IEnumerator test_direct_read_phase_fixed_mode_negative_fixture_caught()
        {
            var settings = InputSystem.settings;
            var savedMode = settings.updateMode;
            float savedFixedDelta = Time.fixedDeltaTime;
            bool caught = false;

            var fx = new ChannelFixture();
            fx.Setup();
            try
            {
                yield return null; // 稳定一帧

                settings.updateMode = InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
                Time.fixedDeltaTime = 0.5f; // 抬固定步长 ⇒ Fixed 更新频率与渲染帧明显错开

                int expectedSamples = fx.Channel.SampleCount;
                for (int i = 0; i < 12; i++)
                {
                    int frameBefore = Time.frameCount;
                    yield return null;
                    int frameDelta = Time.frameCount - frameBefore;
                    int sampleDelta = fx.Channel.SampleCount - expectedSamples;
                    expectedSamples = fx.Channel.SampleCount;

                    if (sampleDelta != frameDelta)
                    {
                        caught = true; // Fixed 模式下相位与帧不再 1:1 —— 正是本负例要抓的失配
                        break;
                    }
                }

                Assert.That(caught, Is.True,
                    "Fixed 模式下 ≤12 步内必须检出至少一步「采样增量 != 帧推进」" +
                    " —— 负例夹具未抓红,说明相位性质断言对该模式不敏感(AC-3-B2③ Negative)");
            }
            finally
            {
                settings.updateMode = savedMode;
                Time.fixedDeltaTime = savedFixedDelta;

                Assert.That(settings.updateMode, Is.EqualTo(savedMode),
                    "updateMode 未恢复到保存的原值(测试夹具不得污染工程状态)");
                // float 往返有损:Time.fixedDeltaTime 经引擎内部存储 roundtrip 会丢 ~1e-8 级精度
                // (实测 0.0199999921 → 0.0199999865),NUnit 默认精确比较必误报。
                // 容差 1e-6 远小于任何真实污染(测试写入的是 0.5f),只吸收 roundtrip 误差。
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(savedFixedDelta).Within(1e-6f),
                    "fixedDeltaTime 未恢复到保存的原值(测试夹具不得污染工程状态)");

                fx.Cleanup();
            }
        }

        // ══════════ AC-3-B1a · 零硬件合成注入 → 读数同帧可见(真实接线面)══════════

        /// <summary>AC-3-B1a 真实接线:Arm+Attach 后同一帧注入按键(QueueStateEvent),
        /// 下一帧 onAfterUpdate 回调采样 ⇒ SampleCount 恰 ==1(轮询晚一帧 ⇒ 0 ⇒ 红)、
        /// IsReading 同帧可观察、可见性延迟恰 1 帧;EndAction 聚合出非默认读数
        /// (Edges==1 · EdgeTicks=[0] · MagPeak=满幅)。持续按住后再释放+再按 ⇒ Edges==2。</summary>
        [UnityTest]
        public IEnumerator test_direct_read_channel_b1a_injected_reading_visible_same_frame_wired()
        {
            var fx = new ChannelFixture();
            fx.Setup();
            try
            {
                // 帧 N:Arm+Attach 已完成 ⇒ 同一帧注入按键(排队,下一帧 InputSystem.Update 生效)
                fx.PressF();
                int frameAtInject = Time.frameCount;

                // 帧 N+1:InputSystem.Update 处理队列按键 → 动作触发 → onAfterUpdate 回调采样
                yield return null;

                int frameDelta = Time.frameCount - frameAtInject;
                Assert.That(frameDelta, Is.EqualTo(1), "注入到可见恰好跨 1 个渲染帧(AC-3-B1a ≤1 帧)");
                Assert.That(fx.Channel.SampleCount, Is.EqualTo(1),
                    "接线后首样必须在注入的下一帧恰到(轮询且晚一帧 ⇒ 此处 0 ⇒ 红)");
                Assert.That(fx.Channel.IsReading, Is.True,
                    "采样帧内 IsReading 必须同帧可见(AC-3-B1a)");
                Assert.That(fx.Channel.State, Is.EqualTo(DirectChannelState.Armed),
                    "采样中状态必须是 Armed(读数可观察,尚未 End/Abort)");

                // 持续按住一帧(无新沿,持有态采样)
                yield return null;
                Assert.That(fx.Channel.SampleCount, Is.EqualTo(2), "持有帧每帧恰 1 样本(B2③ 性质兼验)");

                // 释放 → 下一帧处理松键 → 无新沿(松键不是沿) → 但持有时长继续累计
                fx.ReleaseF();
                yield return null;

                // 再按 → 新沿
                fx.PressF();
                yield return null;
                Assert.That(fx.Channel.SampleCount, Is.EqualTo(4), "四帧各恰一样本(注入/持有/释放/再按)");

                // 收束:EndAction 聚合
                var result = fx.Channel.EndAction();
                Assert.That(result, Is.Not.Null, "Armed 期有过样本 ⇒ EndAction 必须聚合出一条");
                var agg = result.Value;

                Assert.That(agg.Action, Is.EqualTo(ArmedOrdinal), "读数动作身份 = Arm 时预约的 ordinal");
                Assert.That(agg.Edges, Is.EqualTo(2), "首按 + 再按 ⇒ 两条沿(默认 Edges=0 ⇒ 非默认读数)");
                Assert.That(agg.EdgeTicks.Length, Is.EqualTo(2), "两条沿的 tick 数组长度");
                Assert.That(agg.EdgeTicks[0], Is.EqualTo(0), "首沿 tick = 动作内第 1 个样本");
                Assert.That(agg.MagPeak, Is.EqualTo(TestMagMax),
                    "Button 动作按下 ReadValue<float>()=1 ⇒ 定点满幅 65536(非默认幅度)");
                Assert.That(fx.Channel.State, Is.EqualTo(DirectChannelState.Idle), "EndAction 后回 Idle");
            }
            finally
            {
                fx.Cleanup();
            }
        }
    }
}
