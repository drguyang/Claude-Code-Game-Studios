// 权威来源:production/epics/audio-system/story-018-runtime-audio-assembly.md(AC-018-2)
//   · A-6「37 个 wav 的 .meta 零 m_audioMixerGroup ⇒ 全走 Master 直通」
// engine-reference `docs/engine-reference/unity/modules/audio.md:96`
//   —— 总线路由的**运行期**属性 = `AudioSource.outputAudioMixerGroup`;
//   `.meta` 的 `m_audioMixerGroup` **不在**引擎参考覆盖面内(2026-10-17 实测 0/38 命中)。
// ADR-018 §三(七总线拓扑)· ADR-024 §⑥(白名单单一出处 —— bus 名**只**从 MixerRegistry 读)
//
// ⚠️ **为什么不在 38 个 `.meta` 上批改**:① 该字段不是引擎参考登记的机制(见上);
//   ② `.meta` 是**资产侧**的静态归属,而总路线归属是**事件表 `Bus` 列**的语义
//   (`assets/data/audio_events.json`)—— 两处各存一份 = 第二真源(ADR-024 §⑥ 同族)。
//   运行期按 cue 的 bus 挂组 ⇒ 真源恒为事件表,`.meta` 保持中性。
//
// ⚠️ **未知 bus 必须 fail-loud**:静默落 Master 会让「总线拓扑形同虚设」这件事
//   在运行期**不可见**(正是 A-6 的失效形态)。构造期一次性校验全部 7 条 bus 可解析,
//   缺失即抛 —— 把配置错误提前成具名异常。

using System;
using System.Collections.Generic;
using UnityEngine.Audio;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>bus 名 → <see cref="AudioMixerGroup"/> 的**唯一解析点**(A-6)。
    /// <para>构造期把 <see cref="MixerRegistry.BusNames"/> 逐条解析成组并缓存;
    /// 缺任一条即抛(不静默落 Master)。</para>
    /// <example><code>var resolver = new AudioBusResolver(mixer);
    /// AudioMixerGroup g = resolver.GroupFor("SFX");</code></example>
    /// </summary>
    public sealed class AudioBusResolver
    {
        private readonly Dictionary<string, AudioMixerGroup> _groups =
            new Dictionary<string, AudioMixerGroup>(StringComparer.Ordinal);

        /// <summary>解析并缓存全部 7 条 bus 的组。</summary>
        /// <param name="mixer">目标混音器(非 null)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="mixer"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">任一条注册 bus 在 mixer 内找不到组。</exception>
        public AudioBusResolver(AudioMixer mixer)
        {
            if (mixer == null) throw new ArgumentNullException(nameof(mixer));

            var missing = new List<string>();
            IReadOnlyList<string> buses = MixerRegistry.BusNames;
            for (int i = 0; i < buses.Count; i++)
            {
                string bus = buses[i];
                AudioMixerGroup group = FindGroup(mixer, bus);
                if (group == null) missing.Add(bus);
                else _groups[bus] = group;
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "[A-6] 混音器内找不到注册总线组:" + string.Join(", ", missing) +
                    " —— 七总线拓扑(ADR-018 §三)要求每条 bus 都有同名组;" +
                    "静默落 Master 会让总线归属在运行期不可见(本类拒绝该降级)。" +
                    $"注册集 = MixerRegistry.BusNames({buses.Count} 条)。");
            }
        }

        /// <summary>已解析的 bus 条数(应恒 = <see cref="MixerRegistry.BusNames"/>.Count)。</summary>
        public int Count => _groups.Count;

        /// <summary>取 bus 对应的混音组。</summary>
        /// <param name="bus">bus 名(须 ∈ <see cref="MixerRegistry.BusNames"/>)。</param>
        /// <returns>该 bus 的组(恒非 null —— 构造期已全量校验)。</returns>
        /// <exception cref="ArgumentException">bus 名不在注册集内。</exception>
        public AudioMixerGroup GroupFor(string bus)
        {
            if (bus != null && _groups.TryGetValue(bus, out AudioMixerGroup group))
                return group;
            throw new ArgumentException(
                $"未知总线「{bus}」—— 44 的总线闭集 = MixerRegistry.BusNames;" +
                "事件表 `bus` 列写错会在装配期暴露(禁静默落 Master)。", nameof(bus));
        }

        /// <summary>bus 是否在注册集内且已解析(纯查询,不抛)。</summary>
        /// <param name="bus">bus 名。</param>
        public bool TryGetGroup(string bus, out AudioMixerGroup group)
        {
            group = null;
            return bus != null && _groups.TryGetValue(bus, out group);
        }

        /// <summary>`AudioMixer.FindMatchingGroups` 的精确名匹配(该 API 返回**前缀匹配**的
        /// 多组,首项不保证是精确名 —— 故须逐项按 <see cref="StringComparer.Ordinal"/> 比对)。</summary>
        private static AudioMixerGroup FindGroup(AudioMixer mixer, string bus)
        {
            AudioMixerGroup[] matches = mixer.FindMatchingGroups(bus);
            if (matches == null) return null;
            for (int i = 0; i < matches.Length; i++)
            {
                AudioMixerGroup g = matches[i];
                if (g != null && string.Equals(g.name, bus, StringComparison.Ordinal)) return g;
            }
            return null;
        }
    }
}
