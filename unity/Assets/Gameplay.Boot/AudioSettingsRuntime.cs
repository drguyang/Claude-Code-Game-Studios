// 设置链的**生产装配点**(2026-10-29 · `TD-audio-105` 缺口 A)。
//
// ── 病因(实测)──────────────────────────────────────────────────────────────
// 设置链的三件各自都对,但**生产侧一个构造点都没有**:
//   · `AudioSettingsStore` —— `grep "new AudioSettingsStore"` 全库**只命中测试**;
//   · `AudioSettingsStore.MixerPushSink` —— 生产侧**零接线**(只有 store 自己读它);
//   · `mono_enabled` —— 生产侧**零消费方**(只有注册表 + 常量两处声明)。
// ⇒ 玩家在 42 设置壳里拨任何滑块 / 开关,**不产生任何声学变化**。
//
// ── 本件的职责 ──────────────────────────────────────────────────────────
// 把三件接成一条链,并且**只做接线**,不新增语义:
//   store(值真源)── push ──▶ mixer 7 路总线音量(`PlayerBusVolumeInitializer` 同款 SetFloat)
//                 └─ mono ──▶ `MonoOutputApplier`(设备级 speakerMode)
//
// ── 为什么 mono 走单独一跳,不混进 push ─────────────────────────────────────
// `MixerPush` 的签名是 `(IReadOnlyList<KeyValuePair<string,double>> dbValues) → errors`,
// 它的语义面**只有 dB 值**(`PlayerBusVolumeInitializer` 也只为 7 路音量存在)。
// 把 bool 的 mono 塞进去要做类型转换 + 在实现里再分派 —— 那是把两种**不同施加面**
// (混音器参数 vs 音频设备配置)压进一个委托,读者会误以为 mono 也是 mixer 参数
// (那正是本轮实测推翻的那个错误假设)。**分开 = 让「谁是设备级」在代码形状上可读**。
//
// 权威来源:AC-44-13(mono 选项)· AC-44-12 ③(44 的非视觉义务)· AC-44-19(归零三步)
//   · ADR-018 §一(44 只渲染不持值)· OQ-SS-5=甲(端本地 sidecar)

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.Presentation.Settings;
using UnityEngine;
using UnityEngine.Audio;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>设置链的生产装配件:store + 推 mixer + mono 设备施加。</summary>
    public sealed class AudioSettingsRuntime
    {
        private readonly AudioSettingsStore _store;
        private readonly Func<AudioMixer> _mixerResolver;
        private readonly Action<IReadOnlyList<KeyValuePair<string, double>>> _pushObserver;

        /// <summary>构造并接线(store 的 `MixerPushSink` 在此注入)。</summary>
        /// <param name="sidecarDirectory">端本地 sidecar 目录(null ⇒ 用 persistentDataPath 下的默认位置)。</param>
        /// <param name="mixerResolver">混音器解析缝(与 `PlayerBusVolumeInitializer.MixerResolver` 同源)。</param>
        /// <param name="pushObserver">**可观测缝**(收到即将推送的 dB 表;null ⇒ 不观测)。
        /// <para>⚠️ 存在的理由:`AudioMixer.SetFloat` 在 **EditMode 下不生效**(既有实测,
        /// 见 `runtime_assembly_test.cs` 的「写 −80 后 GetFloat 仍读回 0」)⇒ 夹具无法经混音器
        /// 回读来验证「推了什么」。本缝只做观测,**不改变推送行为**。
        /// 与 `AudioTierDriver` 的 `Action&lt;string&gt; logError` 同一模式(44 侧不新增项目接口面,AC-44-B3)。</para></param>
        public AudioSettingsRuntime(string sidecarDirectory, Func<AudioMixer> mixerResolver,
                                    Action<IReadOnlyList<KeyValuePair<string, double>>> pushObserver = null)
        {
            _mixerResolver = mixerResolver;
            _pushObserver = pushObserver;
            _store = new AudioSettingsStore(sidecarDirectory ?? DefaultSidecarDirectory());
            _store.MixerPushSink = PushToMixer;
        }

        /// <summary>值真源(42 壳读它;44 渲染它 —— 两侧都不持值)。</summary>
        public AudioSettingsStore Store => _store;

        /// <summary>端本地 sidecar 目录(与 7a 存档目录**刻意分开**:OQ-SS-5=甲)。</summary>
        public static string DefaultSidecarDirectory()
            => System.IO.Path.Combine(Application.persistentDataPath, "settings");

        /// <summary>启动序:载 sidecar → 推 mixer → 施加 mono。</summary>
        /// <remarks>⚠️ **次序不可换**:先载值(否则推的是出厂默认,玩家的偏好被覆盖),
        /// 再推(否则 mixer 停在未初始化态),最后 mono(设备级,代价最高)。</remarks>
        /// <returns>逐条错误(空 = 三步全成功)。</returns>
        public IReadOnlyList<string> Startup()
        {
            var errors = new List<string>();

            // ① 载 sidecar(NoSidecar / Mismatch 都是**正常路径**,不是错误 ——
            //    前者 = 首次启动,后者 = 陈旧件已改名备份 + 维持出厂默认)。
            _store.LoadFromSidecar();

            // ② 推 mixer —— 用**当前值**(不是出厂默认)。
            // ⚠️ **经 `_store.MixerPushSink` 调,不直调 `PushToMixer`** —— 直调会让
            //    「sink 有没有接线」变成不可观测(证伪轮 M4 实测:删掉接线仍全绿 = 假存活)。
            //    sink 未接(null)⇒ 记错误(与 `ResetToFactory` 的第二步同口径)。
            MixerPush push = _store.MixerPushSink;
            if (push == null)
            {
                errors.Add("[settings] MixerPushSink 未接线 —— 启动序第二步不可执行");
            }
            else
            {
                IReadOnlyList<string> pushErrors = push(BuildDbPushList());
                if (pushErrors != null && pushErrors.Count > 0) errors.AddRange(pushErrors);
            }

            // ③ 施加 mono(设备级)。
            string monoError = ApplyMonoFromStore();
            if (!string.IsNullOrEmpty(monoError)) errors.Add(monoError);

            return errors;
        }

        /// <summary>玩家改一个设置后的**统一施加入口**(42 壳写值后调)。
        /// <returns>错误串(空 = 已施加)。</returns>
        /// <remarks>⚠️ 总线音量与 mono 走**不同的施加面**(见文件头「为什么 mono 走单独一跳」)。</remarks>
        public string ApplyAfterChange(string param)
        {
            if (param == MixerRegistry.MonoParameter) return ApplyMonoFromStore();

            // 总线音量:推全部 7 路(幂等;只推变的那一路也行,但全推免去「哪一路变了」的状态)。
            // ⚠️ 同样**经 sink**,理由见 `Startup` 的注。
            MixerPush push = _store.MixerPushSink;
            if (push == null) return "[settings] MixerPushSink 未接线 —— 推送不可执行";

            IReadOnlyList<string> errors = push(BuildDbPushList());
            return errors == null || errors.Count == 0 ? string.Empty : string.Join("; ", errors);
        }

        /// <summary>从 store 读 mono 值并施加到音频设备。</summary>
        private string ApplyMonoFromStore()
        {
            if (!_store.TryGet(MixerRegistry.MonoParameter, out SettingsValue value))
                return $"[mono] store 无 {MixerRegistry.MonoParameter} —— 注册表与 store 不一致";

            return MonoOutputApplier.Apply(value.Bool);
        }

        /// <summary>`MixerPush` 的生产实现:把 dB 值集推给混音器。</summary>
        /// <remarks>⚠️ 输入已是 **dB**(`BuildDbPushList` 换算过)—— 本实现**不再换算**
        /// (与 `PlayerBusVolumeInitializer.ApplyDefaults` 的「调用方负责换算」纪律一致)。</remarks>
        private IReadOnlyList<string> PushToMixer(IReadOnlyList<KeyValuePair<string, double>> dbValues)
        {
            // 观测缝先于混音器解析 —— 混音器缺失时也要能看到「本该推什么」
            // (否则「推了 0 条」与「推了 7 条但 mixer 是 null」在夹具里不可区分)。
            _pushObserver?.Invoke(dbValues);

            AudioMixer mixer = _mixerResolver?.Invoke();
            if (mixer == null)
                return new[] { "[settings] 混音器不可解析 —— 总线音量推送跳过(归 zero-in 的 step-2 失败)" };

            foreach (KeyValuePair<string, double> pair in dbValues)
                mixer.SetFloat(pair.Key, (float)pair.Value);
            return Array.Empty<string>();
        }

        /// <summary>store 当前值 → dB 推送表(只取总线音量 7 路;mono 是 bool,不在此表)。</summary>
        private IReadOnlyList<KeyValuePair<string, double>> BuildDbPushList()
        {
            var list = new List<KeyValuePair<string, double>>(MixerRegistry.BusVolumeParameters.Count);
            foreach (string param in MixerRegistry.BusVolumeParameters)
            {
                if (!_store.TryGet(param, out SettingsValue value)) continue;
                // 注册表域 = 线性 0–1;推 mixer 前换算成 dB(单一换算处 = VolumeUnitMapping)。
                list.Add(new KeyValuePair<string, double>(
                    param, VolumeUnitMapping.LinearToDb(value.Float)));
            }
            return list;
        }
    }
}
