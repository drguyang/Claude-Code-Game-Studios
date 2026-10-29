// 设置链的生产接线判据(2026-10-29 · `TD-audio-105` 缺口 A)。
//
// ── 病因(实测)──────────────────────────────────────────────────────────────
// 设置链三件各自都对,但**生产侧一个构造点都没有**:
//   · `AudioSettingsStore` —— `grep "new AudioSettingsStore"` 全库只命中测试;
//   · `MixerPushSink` —— 生产侧零接线;
//   · `mono_enabled` —— 生产侧零消费方。
// ⇒ 玩家在 42 设置壳拨任何东西,不产生任何声学变化。
//
// ── 判据面(为什么用观测缝而不是读混音器)───────────────────────────────────
// `AudioMixer.SetFloat` 在 **EditMode 下不生效**(既有实测:`runtime_assembly_test.cs`
// 记着「写 −80 后 GetFloat 仍读回 0」)⇒ 夹具无法经混音器回读验证「推了什么」。
// 故 `AudioSettingsRuntime` 有 `pushObserver` 缝(只观测、不改行为),
// 与 `AudioTierDriver` 的 `Action<string> logError` 同一模式。
//
// **不用「store 里有个值」当判据** —— 那正是本债原来的假绿形态。
//
// 权威来源:AC-44-19(归零三步)· AC-44-13(mono)· ADR-018 §一(44 只渲染不持值)
//   · `AudioSettingsRuntime` 文件头(为什么 mono 走单独一跳)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.Presentation.Settings;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>`AudioSettingsRuntime` 的接线判据。</summary>
    public sealed class AudioSettingsRuntimeTest
    {
        private string _tempDir;
        private AudioSpeakerMode _deviceMode;
        private readonly List<KeyValuePair<string, double>> _pushed =
            new List<KeyValuePair<string, double>>();

        [SetUp]
        public void SetUp()
        {
            MonoOutputApplier.ResetForTest();
            _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "dyjc-audiosettings-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_tempDir);
            _deviceMode = AudioSpeakerMode.Stereo;
            _pushed.Clear();

            MonoOutputApplier.GetSpeakerMode = () => _deviceMode;
            MonoOutputApplier.SetSpeakerMode = mode => { _deviceMode = mode; return null; };
        }

        [TearDown]
        public void TearDown()
        {
            MonoOutputApplier.ResetForTest();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, recursive: true);
        }

        /// <summary>造一个 runtime(混音器解析缝给 null —— 观测缝已能看推送面)。</summary>
        private AudioSettingsRuntime MakeRuntime()
            => new AudioSettingsRuntime(_tempDir, mixerResolver: null,
                pushObserver: values => { foreach (var v in values) _pushed.Add(v); });

        /// <summary>**启动序真的推了 7 路总线音量** —— 接线成立的核心判据。
        /// <para>未接线(`MixerPushSink == null`)⇒ 一条都不会推 ⇒ 本测红。</para></summary>
        [Test]
        public void test_startup_pushesAllSevenBusVolumes()
        {
            AudioSettingsRuntime runtime = MakeRuntime();

            runtime.Startup();

            Assert.That(_pushed.Count, Is.EqualTo(MixerRegistry.BusVolumeParameters.Count),
                $"应推 {MixerRegistry.BusVolumeParameters.Count} 路 —— 实推 {_pushed.Count} 条。" +
                "0 条 = `MixerPushSink` 没接线(本债原形态)");
        }

        /// <summary>**推的是 dB,不是线性值** —— 注册表域 0–1,混音器要 dB。
        /// <para>推线性 1.0 会被混音器当 1 dB;正确值是 0 dB。</para></summary>
        [Test]
        public void test_startup_pushesDbNotLinear()
        {
            AudioSettingsRuntime runtime = MakeRuntime();
            runtime.Startup();

            double master = FindPushed(MixerRegistry.BusVolumeParameters[0]);
            Assert.That(master, Is.EqualTo(0.0).Within(1e-4),
                $"线性 1.0 应换算成 0 dB —— 实推 {master}。推 1.0 = 没换算");
        }

        /// <summary>**mono 走设备级,不走 mixer** —— 钉住「两条施加面分开」的设计。</summary>
        [Test]
        public void test_startup_appliesMonoToDevice_notToMixer()
        {
            AudioSettingsRuntime runtime = MakeRuntime();
            runtime.Store.Set(MixerRegistry.MonoParameter, SettingsValue.OfBool(true));

            runtime.Startup();

            Assert.That(_deviceMode, Is.EqualTo(AudioSpeakerMode.Mono),
                "mono=true ⇒ 设备 speakerMode 须为 Mono");

            foreach (KeyValuePair<string, double> p in _pushed)
                Assert.That(p.Key, Is.Not.EqualTo(MixerRegistry.MonoParameter),
                    "mono_enabled 不是混音器参数 —— 推它等于往不存在的槽写(静默无效)");
        }

        /// <summary>**改一个设置后的统一施加入口按参数种类路由**:音量 ⇒ 推;mono ⇒ 改设备且**不**推。
        /// <para>⚠️ **本测不断言「无错误」** —— `MakeRuntime()` 的混音器解析缝是 null
        /// (EditMode 无法造 `AudioMixer` 替身,且 `SetFloat` 在该模式不生效),故改音量
        /// **必然**回一条「混音器不可解析」。那是**环境的已知降级面**,不是路由失败。
        /// 本测的判据面是**路由**:走没走对那条施加面。</para></summary>
        [Test]
        public void test_applyAfterChange_routesByParamKind()
        {
            AudioSettingsRuntime runtime = MakeRuntime();
            runtime.Startup();
            _pushed.Clear();

            // 改音量 ⇒ 走 mixer 推送面(错误串因 null mixer 必然出现,不在判据内)
            string master = MixerRegistry.BusVolumeParameters[0];
            runtime.Store.Set(master, SettingsValue.OfFloat(0.25));
            runtime.ApplyAfterChange(master);
            Assert.That(_pushed.Count, Is.GreaterThan(0), "改音量 ⇒ 须走 mixer 推送面");

            // 改 mono ⇒ 走设备面,**不**走 mixer 面
            _pushed.Clear();
            runtime.Store.Set(MixerRegistry.MonoParameter, SettingsValue.OfBool(true));
            string errMono = runtime.ApplyAfterChange(MixerRegistry.MonoParameter);
            Assert.That(errMono, Is.Empty, $"改 mono 不应报错:{errMono}");
            Assert.That(_deviceMode, Is.EqualTo(AudioSpeakerMode.Mono), "改 mono ⇒ 须改设备");
            Assert.That(_pushed, Is.Empty, "改 mono ⇒ **不得**推 mixer(它不是 mixer 参数)");
        }

        /// <summary>**混音器不可解析 ⇒ 报错但 mono 仍施加**(两个面互不牵连)。</summary>
        [Test]
        public void test_mixerUnavailable_monoStillApplied()
        {
            AudioSettingsRuntime runtime = MakeRuntime();
            runtime.Store.Set(MixerRegistry.MonoParameter, SettingsValue.OfBool(true));

            IReadOnlyList<string> errors = runtime.Startup();

            Assert.That(errors.Count, Is.GreaterThan(0), "混音器缺失须报错(不静默)");
            Assert.That(_deviceMode, Is.EqualTo(AudioSpeakerMode.Mono),
                "混音器缺失不得阻断 mono 施加 —— 两个面独立");
        }

        /// <summary>**mono 关 ⇒ 设备还原**,且不残留到后续 runtime。
        /// <para>⚠️ 这条钉的是「静态还原记忆不跨夹具」—— 承本仓多次踩的静态态泄漏。</para></summary>
        [Test]
        public void test_monoOff_restoresDevice()
        {
            _deviceMode = AudioSpeakerMode.Mode5point1;
            AudioSettingsRuntime runtime = MakeRuntime();

            runtime.Store.Set(MixerRegistry.MonoParameter, SettingsValue.OfBool(true));
            runtime.Startup();
            Assert.That(_deviceMode, Is.EqualTo(AudioSpeakerMode.Mono));

            runtime.Store.Set(MixerRegistry.MonoParameter, SettingsValue.OfBool(false));
            runtime.ApplyAfterChange(MixerRegistry.MonoParameter);

            Assert.That(_deviceMode, Is.EqualTo(AudioSpeakerMode.Mode5point1),
                "关 mono ⇒ 须还原到开之前的 5.1(不得硬编码 Stereo)");
        }

        /// <summary>**`MixerPushSink` 真的接上了** —— 这是证伪轮 M4 假存活后补的专属靶。
        /// <para>⚠️ 为什么原来抓不到:`Startup()` 曾**直调** `PushToMixer`(绕过 sink),
        /// 故删掉接线后测试仍绿 —— sink 成了死字段。本测直接读 sink 是否非 null,
        /// 并把「推送经 sink 走」这件事钉住。</para></summary>
        [Test]
        public void test_mixerPushSink_isWired()
        {
            AudioSettingsRuntime runtime = MakeRuntime();

            Assert.That(runtime.Store.MixerPushSink, Is.Not.Null,
                "`MixerPushSink` 必须是 `PushToMixer` —— null = 接线没做(本债原形态)");
        }

        /// <summary>**推送真的经 sink 走**(不是直调私有方法)。
        /// <para>判据:把 sink 换成一个只记数的替身 ⇒ `Startup` 的推送面必须走它。</para></summary>
        [Test]
        public void test_startup_pushGoesThroughSink_notDirectCall()
        {
            AudioSettingsRuntime runtime = MakeRuntime();
            int sinkCalls = 0;
            runtime.Store.MixerPushSink = _ => { sinkCalls++; return Array.Empty<string>(); };
            _pushed.Clear();

            runtime.Startup();

            Assert.That(sinkCalls, Is.EqualTo(1), "启动序的推送须经 sink 走恰一次");
            Assert.That(_pushed, Is.Empty,
                "换掉 sink 后观测缝不应再收到推送 —— 收到 = `Startup` 绕过了 sink 直调私有方法");
        }

        // ── 辅助 ────────────────────────────────────────────────────────

        private double FindPushed(string key)
        {
            foreach (KeyValuePair<string, double> p in _pushed)
                if (p.Key == key) return p.Value;
            return double.NaN;
        }
    }
}
