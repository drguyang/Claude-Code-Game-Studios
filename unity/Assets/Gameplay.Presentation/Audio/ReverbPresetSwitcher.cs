// 权威来源:production/epics/audio-system/story-003-mixer-topology-snapshots.md(AC-44-E3 ③)
//   · reverb preset 切换**所有者 = 44**;触发输入 = 玩家所在房间格(表现层派生,不进流)
//   · 切换走**组 effect + 快照**(2026-09-26 unity-specialist 裁定:禁 AudioReverbFilter 挂
//     AudioSource —— 它不被快照覆盖,违 ADR-018 §三「混响走 send,否则快照只能改增益」)
// GDD:AC-44-E3(ramp 同 HANDOVER_MS 级旋钮 —— 数值由调用方供给)· ADR-018 §三 / §一
// TR-audio-003(拓扑单一出处的 reverb 半边)
//
// ⚠️ 调用点:内部 Apply() 是快照切换调用点(白名单登记为**世界可感知声**类 ——
//    房间语境,非病人状态播报);IL 扫描见 MixerTopologyGates.TransitionCallsiteRules。
// ⚠️ 44 只渲染:房间格 → preset 的**数据映射**由调用方注入(烘焙逻辑层表现侧派生),
//    本类不读 sim、不读世界真源,只做快照切换(ADR-018 §一)。
// ⚠️ 快照缺失 = 红日志不崩(素材 / 快照资产随 Story 010 + MixerAssetGenerator)。

using System;
using UnityEngine;
using UnityEngine.Audio;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>reverb preset 切换器(44 持有;触发输入 = 房间格,表现层派生)。</summary>
    /// <example>
    /// var switcher = new ReverbPresetSwitcher(mixer, roomCell =&gt; lookup.PresetOf(roomCell), 0.5f);
    /// switcher.ApplyForRoomCell(playerRoomCell);   // 换房间 → 混响过渡
    /// </example>
    public sealed class ReverbPresetSwitcher
    {
        private readonly AudioMixer _mixer;
        private readonly Func<int, ReverbPreset> _presetForRoomCell;
        private readonly float _transitionSeconds;

        /// <summary>构造切换器。</summary>
        /// <param name="mixer">目标混音器。</param>
        /// <param name="presetForRoomCell">房间格 → preset 的**注入映射**(表现层派生数据源,
        /// 由调用方供给;44 不自持世界数据)。</param>
        /// <param name="transitionSeconds">ramp 时长(旋钮,GDD AC-44-E3「同 HANDOVER_MS 级」)。</param>
        /// <exception cref="ArgumentNullException">入参为 null。</exception>
        public ReverbPresetSwitcher(AudioMixer mixer, Func<int, ReverbPreset> presetForRoomCell,
                                    float transitionSeconds)
        {
            _mixer = mixer ?? throw new ArgumentNullException(nameof(mixer));
            _presetForRoomCell = presetForRoomCell ?? throw new ArgumentNullException(nameof(presetForRoomCell));
            _transitionSeconds = transitionSeconds;
        }

        /// <summary>按房间格应用 reverb preset(表现层派生入口;房间格来自烘焙逻辑层的表现侧)。</summary>
        /// <param name="roomCell">玩家所在房间格(整数格标识;经注入映射求 preset)。</param>
        public void ApplyForRoomCell(int roomCell)
        {
            Apply(_presetForRoomCell(roomCell));
        }

        /// <summary>切到指定 preset 的混音器快照(内部;调用点白名单 = 世界可感知声类)。</summary>
        /// <param name="preset">目标 reverb preset。</param>
        internal void Apply(ReverbPreset preset)
        {
            string snapshotName = MixerRegistry.ReverbPresetSnapshotPrefix + SuffixOf(preset);
            AudioMixerSnapshot snapshot = _mixer.FindSnapshot(snapshotName);
            if (snapshot == null)
            {
                Debug.LogError(
                    $"[44] reverb 快照「{snapshotName}」缺失 —— 资产未生成或 preset 枚举扩员未同步" +
                    "(MixerAssetGenerator / Story 010 素材管线);本次切换不执行");
                return;
            }
            snapshot.TransitionTo(_transitionSeconds);
        }

        private static string SuffixOf(ReverbPreset preset)
        {
            switch (preset)
            {
                case ReverbPreset.Outdoor: return "outdoor";
                case ReverbPreset.Indoor: return "indoor";
                case ReverbPreset.Cave: return "cave";
                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset,
                        "ReverbPreset 闭枚举外成员 —— 扩枚举须同批扩 SuffixOf 与快照名");
            }
        }
    }
}
