// 权威来源:production/epics/audio-system/story-003-mixer-topology-snapshots.md(两级组结构纪律)
//   · 2026-09-26 unity-specialist 6.3 裁定:AudioMixer.SetFloat 原文 ——
//     「Once you call this function, mixer snapshots will no longer control the exposed
//     parameter」⇒ 快照按定义控制**全部** mixer 参数;SetFloat 一次性把玩家参数**从快照
//     控制中摘除**。MUST:启动初始化对 7 个 bus_volume_* 各 SetFloat 一次(否则首帧前仍受
//     快照控制);玩家参数**禁止** ClearFloat(交还快照控制 = 出入快照打回玩家值)。
// GDD:design/gdd/audio-system.md §States 两级组纪律(滑块写玩家组,快照过渡不触碰)
// ADR-018 §三 · 注册表 AC②(交集 = ∅ 的运行期半边)
//
// ⚠️ 接线(八轮已接):`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 入口 ——
//    **不改 Boot 场景**(共享资产能不碰就不碰),且早于任何场景加载 ⇒ 在首次快照过渡之前。
//    mixer 获取走 **③ 路(用户裁定)**:`MixerResolver` 缝由 Story 010 Addressables 装载面注入;
//    未注入 = 静默让位(不硬找 Resources —— story 禁拷资产进 Resources;不挂场景 MonoBehaviour)。
//    「存在调用点」由 mixer_asset_generator_test 反射断言。
// ⚠️ 默认值(dB)**由调用方供给**(游戏数值不硬编码);P0 出厂 = `FactoryDefaults`(0 dB 种子,
//    Story 011 设置 store 接管前的回退)。

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>玩家总线音量初始化器:对注册表 7 个 <c>bus_volume_*</c> 各
    /// <c>SetFloat</c> 一次,把玩家参数从快照控制中摘除(两级组纪律的运行期半边)。</summary>
    public static class PlayerBusVolumeInitializer
    {
        /// <summary>P0 出厂默认集(**种子值 0 dB = 中性,用户自己调**;Story 011 设置 store
        /// 接管后,本集仅作「store 不可用」的回退)。键 = 注册表 7 员,值由本常量集中供给 ——
        /// 初始化器逻辑内**零硬编码数值**。</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, float>> FactoryDefaults =
            BuildFactoryDefaults();

        private static IReadOnlyList<KeyValuePair<string, float>> BuildFactoryDefaults()
        {
            var defaults = new List<KeyValuePair<string, float>>(
                MixerRegistry.BusVolumeParameters.Count);
            foreach (string parameter in MixerRegistry.BusVolumeParameters)
                defaults.Add(new KeyValuePair<string, float>(parameter, 0f));
            return defaults;
        }

        /// <summary>mixer 解析缝(**③ 路,用户裁定**):Story 010 Addressables 装载面完成后注入
        /// (如 <c>() =&gt; addressables.LoadAsset&lt;AudioMixer&gt;(…).Result</c>);
        /// 未注入 = 入口静默让位。**不**拷资产进 Resources(story 禁)、**不**改 Boot 场景。</summary>
        public static Func<AudioMixer> MixerResolver;

        /// <summary>启动入口:任何场景加载 / 首次快照过渡**之前**执行(八轮接线)。
        /// 无 resolver / 无 mixer ⇒ 静默让位(010 管线未接);有 ⇒ 推 7 参数 + 错误红日志。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Func<AudioMixer> resolver = MixerResolver;
            if (resolver == null) return;            // 装载缝未接(010)—— 不硬找资源
            AudioMixer mixer = resolver();
            if (mixer == null) return;

            IReadOnlyList<string> errors = ApplyDefaults(mixer, FactoryDefaults);
            if (errors.Count > 0)
            {
                foreach (string error in errors)
                    Debug.LogError("[PlayerBusVolume] " + error);
            }
        }

        /// <summary>校验默认集键覆盖注册表 7 员且无未知键(拼写错 = 静默漏 SetFloat)。
        /// <para>纯函数 · 零 throw · 错误列表(空 = 通过)。</para></summary>
        /// <param name="providedKeys">调用方提供的默认集键。</param>
        /// <example>
        /// var errs = PlayerBusVolumeInitializer.ValidateDefaults(mixerDefaults.Keys);
        /// </example>
        public static IReadOnlyList<string> ValidateDefaults(IEnumerable<string> providedKeys)
        {
            // ── 判据唯一出处 = 本方法(运行期程序集不能反向依赖编辑期门);
            //    MixerTopologyGates.ValidatePlayerVolumeDefaults **委托**到此,不复写第二份。
            var errors = new List<string>();
            if (providedKeys == null)
                return new[] { "[NOT-RUN 守卫] 默认集缺失(null)—— 键覆盖校验不可执行" };

            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in providedKeys)
                if (!string.IsNullOrEmpty(key)) set.Add(key);

            foreach (string param in MixerRegistry.BusVolumeParameters)
                if (!set.Contains(param))
                    errors.Add($"[两级组纪律] 玩家音量默认集缺「{param}」—— 7 个参数须各 SetFloat 一次");

            foreach (string key in set)
            {
                bool known = false;
                foreach (string param in MixerRegistry.BusVolumeParameters)
                    if (string.Equals(param, key, StringComparison.Ordinal)) known = true;
                if (!known)
                    errors.Add($"[两级组纪律] 默认集含未知键「{key}」(非注册 bus_volume_*)");
            }

            return errors;
        }

        /// <summary>对注册表 7 个玩家参数各 <c>SetFloat</c> 一次(摘除快照控制)。
        /// <para>键校验失败 ⇒ **不做任何 SetFloat**(半初始化比不初始化更难排查),返回错误列表。</para></summary>
        /// <param name="mixer">目标混音器。</param>
        /// <param name="defaults">玩家参数默认值(键须覆盖注册表 7 员;数值由调用方供给)。</param>
        /// <returns>错误列表(空 = 全部 7 参数已推送)。</returns>
        /// <example>
        /// IReadOnlyList&lt;string&gt; errs = PlayerBusVolumeInitializer.ApplyDefaults(
        ///     mixer, store.Get出厂默认集());
        /// </example>
        public static IReadOnlyList<string> ApplyDefaults(
            AudioMixer mixer, IReadOnlyList<KeyValuePair<string, float>> defaults)
        {
            if (mixer == null)
                return new[] { "[NOT-RUN 守卫] mixer 缺失(null)—— SetFloat 初始化不可执行" };
            if (defaults == null)
                return new[] { "[NOT-RUN 守卫] 默认集缺失(null)—— SetFloat 初始化不可执行" };

            var keys = new List<string>(defaults.Count);
            foreach (KeyValuePair<string, float> pair in defaults)
                keys.Add(pair.Key);

            IReadOnlyList<string> errors = ValidateDefaults(keys);
            if (errors.Count > 0)
                return errors;

            foreach (KeyValuePair<string, float> pair in defaults)
                mixer.SetFloat(pair.Key, pair.Value);   // 摘除快照控制(7 次,一次性)

            return Array.Empty<string>();
        }
    }
}
