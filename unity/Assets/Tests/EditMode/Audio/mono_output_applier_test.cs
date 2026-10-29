// `mono_enabled` 的实际施加面(`AC-44-13` / `AC-44-12` ③-a · 2026-10-29)。
//
// ── 病因 ────────────────────────────────────────────────────────────────────
// `mono_enabled` 在注册表 / `MixerRegistry` / 42 壳 UI 三处都有,**生产侧零消费方**
// ⇒ 玩家改 mono 不产生任何声学变化。
//
// ── 关键实测(推翻债册假设)──────────────────────────────────────────────────
// 债册写「`.mixer` 的 `m_ExposedParameters` 不含 `mono_enabled`」⇒ 暗示修法 = 加参数。
// 实测:该 mixer 的 12 个暴露参数**全是音量/滤波类**(guid 均指向 `AudioMixerGroupController.m_Volume`);
// `monodis` 反汇编 Unity 6.3.24f1 的 `UnityEngine.AudioModule.dll`:
//   · `AudioListener` **无** forceMono 成员;
//   · `AudioSettings.speakerMode` **已 deprecated**,官方修法 = `GetConfiguration` → `Reset`;
//   · `AudioSpeakerMode.Mono` 存在。
// ⇒ **Unity 6.3 的 mono 是设备级 speakerMode,不是混音器参数。**
//
// ── 判据面 ──────────────────────────────────────────────────────────────────
// 用注入缝(`GetSpeakerMode` / `SetSpeakerMode`)观测「设备模式真的变了」,
// 而不是「一个 bool 被存起来了」—— 后者正是本债原来的假绿形态。
//
// 权威来源:`design/gdd/audio-system.md:782` / `:933` · `AC-44-13` · `AC-44-12` ③-a。

using System;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Audio
{
    /// <summary>`MonoOutputApplier` 的设备级施加判据。</summary>
    public sealed class MonoOutputApplierTest
    {
        private AudioSpeakerMode _deviceMode;
        private int _setCount;

        [SetUp]
        public void SetUp()
        {
            MonoOutputApplier.ResetForTest();
            _deviceMode = AudioSpeakerMode.Stereo;
            _setCount = 0;

            MonoOutputApplier.GetSpeakerMode = () => _deviceMode;
            MonoOutputApplier.SetSpeakerMode = mode => { _deviceMode = mode; _setCount++; return null; };
        }

        [TearDown]
        public void TearDown() => MonoOutputApplier.ResetForTest();

        /// <summary>**开 mono ⇒ 设备真的切到 Mono**。这是本债的核心判据。</summary>
        [Test]
        public void test_applyOn_switchesDeviceToMono()
        {
            Assert.That(_deviceMode, Is.EqualTo(AudioSpeakerMode.Stereo), "前置:设备初始为 Stereo");

            string err = MonoOutputApplier.Apply(true);

            Assert.That(err, Is.Empty, "施加不应报错");
            Assert.That(_deviceMode, Is.EqualTo(AudioSpeakerMode.Mono),
                "开 mono ⇒ speakerMode 必须真的变成 Mono。" +
                "仍为 Stereo = mono_enabled 只是被存起来,从未施加到设备(本债原形态)");
            Assert.That(_setCount, Is.EqualTo(1), "应恰写一次");
        }

        /// <summary>**关 mono ⇒ 还原到开之前的模式**,不是硬编码 Stereo。
        /// <para>⚠️ 硬编码 Stereo 会把玩家的 5.1 / 7.1 设备**静默降级** —— 那是另一种缺陷。</para></summary>
        [Test]
        public void test_applyOff_restoresOriginalMode_notHardcodedStereo()
        {
            _deviceMode = AudioSpeakerMode.Mode5point1;   // 玩家本来的设备

            MonoOutputApplier.Apply(true);
            Assert.That(_deviceMode, Is.EqualTo(AudioSpeakerMode.Mono));

            MonoOutputApplier.Apply(false);
            Assert.That(_deviceMode, Is.EqualTo(AudioSpeakerMode.Mode5point1),
                "关 mono ⇒ 必须还原成**开之前的** 5.1,不得硬编码 Stereo(否则玩家设备被静默降级)");
        }

        /// <summary>**重复开 mono 是幂等的**,且不得把还原目标记成 Mono 自身。
        /// <para>⚠️ 若第二次开 mono 覆盖了 `_preMonoMode`,关 mono 会「还原」到 Mono = 关不掉。</para></summary>
        [Test]
        public void test_applyOnTwice_isIdempotent_andKeepsRestoreTarget()
        {
            _deviceMode = AudioSpeakerMode.Mode7point1;

            MonoOutputApplier.Apply(true);
            MonoOutputApplier.Apply(true);      // 第二次:已在 Mono ⇒ 无变化

            Assert.That(_deviceMode, Is.EqualTo(AudioSpeakerMode.Mono));
            Assert.That(_setCount, Is.EqualTo(1), "第二次开 mono 不应再写设备(幂等)");

            MonoOutputApplier.Apply(false);
            Assert.That(_deviceMode, Is.EqualTo(AudioSpeakerMode.Mode7point1),
                "关 mono 须还原到**第一次开之前**的 7.1 —— 若还原到 Mono,则 mono 永远关不掉");
        }

        /// <summary>**从未开过 mono 时关它 ⇒ 无变化**(不得凭 `_preMonoMode` 缺失去猜)。
        /// </summary>
        [Test]
        public void test_applyOff_whenNeverOn_isNoOp()
        {
            _deviceMode = AudioSpeakerMode.Stereo;
            string err = MonoOutputApplier.Apply(false);

            Assert.That(err, Is.Empty);
            Assert.That(_deviceMode, Is.EqualTo(AudioSpeakerMode.Stereo), "从未开过 ⇒ 设备不动");
            Assert.That(_setCount, Is.EqualTo(0), "从未开过 ⇒ 不应写设备");
        }

        /// <summary>**设置失败不抛** —— 设备不支持该模式属可降级面,但错误串须回传(不静默)。</summary>
        [Test]
        public void test_setFailure_returnsError_notThrow()
        {
            MonoOutputApplier.SetSpeakerMode = mode => "设备不支持";

            string err = MonoOutputApplier.Apply(true);

            Assert.That(err, Is.EqualTo("设备不支持"), "失败须回传错误串(不抛、不静默)");
        }

        /// <summary>**清空注入缝后回落生产实现** —— 证明 `ResetForTest` 真的复位了静态态。
        /// <para>⚠️ 这条钉的是跨夹具隔离:静态注入缝不复位会污染后续夹具(本仓已多次踩)。</para></summary>
        [Test]
        public void test_resetForTest_clearsInjectionSeamsAndRestoreMemory()
        {
            _deviceMode = AudioSpeakerMode.Mode5point1;
            MonoOutputApplier.Apply(true);      // 记下 _preMonoMode = 5.1

            MonoOutputApplier.ResetForTest();

            Assert.That(MonoOutputApplier.GetSpeakerMode, Is.Null, "GetSpeakerMode 须被清空");
            Assert.That(MonoOutputApplier.SetSpeakerMode, Is.Null, "SetSpeakerMode 须被清空");

            // 还原记忆也清了 ⇒ 再关 mono 是 no-op(不会凭陈旧记忆去改设备)
            string err = MonoOutputApplier.Apply(false);
            Assert.That(err, Is.Empty, "还原记忆已清 ⇒ 关 mono 应为 no-op");
        }
    }
}
