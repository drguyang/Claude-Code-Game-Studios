// 权威来源:production/epics/audio-system/story-003-mixer-topology-snapshots.md(注册表载体裁定)
//   · 注册表 AC③ —— bus_volume_* 常量集 = 七总线计数 7(含 Master;断言对象 = 本常量集)
//   · 两级组结构 —— 每总线拆「玩家音量组(bus_volume_*)+ 快照 duck 组(duck_*)」,交集 = ∅
// GDD:design/gdd/audio-system.md §UI Requirements 注册表(:896-909,语义源,对账断言守)
//      · §States(两级组纪律)· ADR-018 §三(七总线)
//
// ⚠️ **单一出处**:总线名 / 玩家参数 / 快照名 / reverb 快照前缀全部住本类(运行期常量,
//    SetFloat 初始化面与 FindSnapshot 都需要它);Editor.Tools.Gates.MixerTopologyGates
//    **引用**本类,禁在门内另抄一份(两处执行 = 两处分叉,承 ADR-024 §⑥ 同一纪律)。
// ⚠️ 游戏数值(具体 dB / 时长)**不在本类** —— 本类只承载**名字闭集**(story 硬纪律:
//    总线名 / 快照名 / 白名单 = 代码常量是刻意的;数值归用户轮)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>混音拓扑的**名字闭集**注册表(七总线 · 玩家 exposed 参数 · 快照五员 ·
    /// 两级组命名)。改名 / 扩总线 = 改本类,资产与门随之对账。</summary>
    public static class MixerRegistry
    {
        /// <summary>七总线组名(ADR-018 §三;顺序 = 树中 Master 之下的稳定序,Master 为根)。</summary>
        public static readonly IReadOnlyList<string> BusNames = new[]
        {
            "Master", "Music", "Ambience", "Voice", "SFX", "Stethoscope", "UICue",
        };

        /// <summary>玩家 exposed 总线音量参数(<c>bus_volume_*</c>;注册表 AC③ 的断言对象,
        /// 计数 = 7 含 master)。命名 = <c>bus_volume_ + 总线名小写</c>,与玩家音量组同名
        /// (两级组结构约定:exposed 参数由同名组承载)。
        /// <para>✅ **H-B 确认(2026-09-27)**:<c>AudioMixer.SetFloat</c> 在 EditMode 对**任何名字**
        /// 都返回 false(包括明显不存在的名字)⇒ 其「按名字查表」native 路径在此环境本身不通
        /// (EditMode / 无活跃 DSP 图 / native 状态),**与 exposed 配置完全无关**。
        /// <c>GetFloat</c> 走另一条路径(遍历组调 <c>GetGUIDForVolume()</c> 兜底),实测返回 true。
        /// ⇒ 本注册表 guid 与同名组 <c>m_Volume</c> 哈希逐条一致(配置守卫)+ <c>GetFloat</c>
        /// 返回 true(读路径守卫)可作为有效判据;<c>SetFloat</c> 在 EditMode 不可判,
        /// **运行期(播放态)仍有效**(EditMode 限制不适用于运行期)。</para></summary>
        public static readonly IReadOnlyList<string> BusVolumeParameters = new[]
        {
            "bus_volume_master", "bus_volume_music", "bus_volume_ambience",
            "bus_volume_voice", "bus_volume_sfx", "bus_volume_stethoscope",
            "bus_volume_uicue",
        };

        /// <summary>玩家音量组前缀(滑块直写;快照**永不捕获**,交集必须 = ∅)。</summary>
        public const string PlayerVolumeGroupPrefix = "bus_volume_";

        /// <summary>快照 duck 组前缀(快照只捕获这一级;玩家滑块不写它)。</summary>
        public const string DuckGroupPrefix = "duck_";

        /// <summary>快照五员(GDD §States 表;VRComfort = P1b,资产先占位)。</summary>
        public static readonly IReadOnlyList<string> SnapshotNames = new[]
        {
            "Default", "StethoscopeFocus", "DialogueFocus", "Paused", "VRComfort",
        };

        /// <summary>tier 滤波 / 噪声底的 **exposed 参数名闭集**(Story 004 · GDD F-44.1 六列中
        /// 可落浮点的五列;<c>band_detail_count</c> = 计数非浮点参数,不入)。
        /// <para>⚠️ **exposed + 脚本驱动,禁入任何快照**(GDD :194-201 —— 否则
        /// <c>DialogueFocus → Default</c> 回退会把档位拉回出厂)。**断言复用 Story 003 的
        /// <c>MixerTopologyGates.ValidateSnapshotExposedDisjoint</c>**(快照捕获集 ∩ exposed 集 = ∅),
        /// 不另写判据。参数**值** = 事件表 <c>tier_map</c> 行(数据归用户调,本类只载名字)。</para>
        /// <para>🚨 **阻塞登记(2026-09-26 用户裁定:只修 bus_volume 小口子,本缺口不做)** ——
        /// 本五名进 <c>.mixer</c> 的通路**被资产拓扑阻塞**,不是「生成器加一行」的事:
        /// ① <c>.mixer</c> 现有 **25 个 effect 全是 <c>Attenuation</c>、零 filter effect**
        /// ⇒ <c>passband_*</c> 等量**无真实参数可挂**(暴露 guid 须 = 真实参数哈希,
        /// 探针 <c>test_busVolumeExposedParam_actuallyAcceptsSetFloat</c> 已证通路语义);
        /// ② **谁承载 tier 滤波 = 全案未认领的资产拓扑裁定** —— GDD F-44.1(:278)说 tier 驱动
        /// 通带 / 噪声底、Story 004 Implementation Notes 说「消费列做滤波 / 噪声底驱动」,
        /// 但**无任何 story 认领「往 mixer 加 filter effect」**;③ 后果 = **阻塞 AC-44-02
        /// 将来的听测**(滤波不生效则三档听不出差异,听测必然失败)。
        /// **拓扑裁定落地前,禁给五名写「应 exposed」断言**(做不到,写了就是假红)。
        /// 本列表继续作为**名字闭集单一出处**待认领。</para></summary>
        public static readonly IReadOnlyList<string> TierFilterParameters = new[]
        {
            "tier_passband_center_hz", "tier_passband_width_hz",
            "tier_noise_floor_db", "tier_contact_noise_floor_db", "tier_signal_db",
        };

        /// <summary>Default 快照名。</summary>
        public const string SnapshotDefault = "Default";

        /// <summary>听诊聚焦快照名(只压 Ambience / Music 电平 —— F7=甲)。</summary>
        public const string SnapshotStethoscopeFocus = "StethoscopeFocus";

        /// <summary>对话聚焦快照名(仅限玩家主动发起对话 —— 规则三)。</summary>
        public const string SnapshotDialogueFocus = "DialogueFocus";

        /// <summary>暂停快照名(全总线衰减)。</summary>
        public const string SnapshotPaused = "Paused";

        /// <summary>reverb preset 快照名前缀(<c>reverb_preset_{indoor,outdoor,cave}</c>;
        /// 素材资产归 Story 010,快照本体由 MixerAssetGenerator 占位)。</summary>
        public const string ReverbPresetSnapshotPrefix = "reverb_preset_";

        /// <summary>玩家音量组名(= exposed 参数名;两级组命名约定)。</summary>
        /// <param name="bus">总线名(<see cref="BusNames"/> 成员)。</param>
        /// <example>MixerRegistry.VolumeGroupForBus("Music") == "bus_volume_music"</example>
        public static string VolumeGroupForBus(string bus)
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            return PlayerVolumeGroupPrefix + bus.ToLowerInvariant();
        }

        /// <summary>快照 duck 组名(每总线的第二级)。</summary>
        /// <param name="bus">总线名(<see cref="BusNames"/> 成员)。</param>
        /// <example>MixerRegistry.DuckGroupForBus("Music") == "duck_music"</example>
        public static string DuckGroupForBus(string bus)
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            return DuckGroupPrefix + bus.ToLowerInvariant();
        }
    }
}
