// 权威来源:production/epics/audio-system/story-003-mixer-topology-snapshots.md(AC-44-C1 载体)
//   · 快照切换一律由**玩家行为**驱动(听诊进出 / 对话起止 / 暂停)—— 无任何非拟物状态播报
//   · DialogueFocus 仅限玩家主动发起对话(病人自发呻吟 / 咳嗽不触发 —— 规则三 2026-09-18 修订)
//   · StethoscopeFocus 只压 Ambience / Music 电平(F7=甲;贴耳抬升 / bus 路由 = Story 009,勿加)
// GDD:design/gdd/audio-system.md §States 快照五员表 · AC-44-C1(:977)
// ADR-018 §一(44 只触发 / 只渲染,永不持有游戏状态)· §三(快照禁播报铁律)· §六
//
// ⚠️ 调用点纪律:每个 public 方法体**直接**调 TransitionTo(不抽公共漏斗)—— 调用点白名单
//    按 `Type::Method` 登记(见 MixerTopologyGates.TransitionCallsiteRules),漏斗会让白名单
//    退化成单点、判据失焦。**新增切换入口先登记白名单**,否则 IL 扫描必红。
// ⚠️ 字段名是判据的一部分:白名单规则按 `AudioMixerSnapshot` 字段读取配对
//    (_stethoscopeFocus / _dialogueFocus / _pausedSnapshot / _defaultSnapshot)—— 改名须同步
//    MixerTopologyGates.TransitionCallsiteRules。
// ⚠️ 本类持有的只是「声音正在发生什么」的切换句柄(呈现层),无游戏状态(ADR-018 §一)。
// ⚠️ transitionSeconds 是**调用方供给的旋钮**(GDD 快照族 Tuning)—— 逻辑内不硬编码数值。

using System;
using UnityEngine.Audio;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>快照切换的唯一收口(44 实现面)。全部 <c>TransitionTo</c> 调用点都是
    /// 玩家行为入口,受 AC-44-C1 调用点白名单 IL 扫描约束。</summary>
    /// <example>
    /// var director = new SnapshotDirector(mixer, transitionSeconds: 0.35f);
    /// director.BeginDialogueFocus();   // 玩家点选病人交谈
    /// director.EndDialogueFocus();     // 对话结束 → 回 Default
    /// </example>
    public sealed class SnapshotDirector
    {
        private readonly AudioMixerSnapshot _defaultSnapshot;
        private readonly AudioMixerSnapshot _stethoscopeFocus;
        private readonly AudioMixerSnapshot _dialogueFocus;
        private readonly AudioMixerSnapshot _pausedSnapshot;
        private readonly float _transitionSeconds;

        /// <summary>从混音器解析快照句柄;缺任一五员快照 = 硬失败(资产与注册表失配,
        /// 与「静默降级」相反 —— 缺快照时切换会无声失效,难以排查)。</summary>
        /// <param name="mixer">目标混音器(资产由 MixerAssetGenerator 生成)。</param>
        /// <param name="transitionSeconds">过渡时长(调用方供给的旋钮;GDD 快照族)。</param>
        /// <exception cref="ArgumentNullException">mixer 为 null。</exception>
        /// <exception cref="InvalidOperationException">注册表五员快照在 mixer 中缺失。</exception>
        public SnapshotDirector(AudioMixer mixer, float transitionSeconds)
        {
            if (mixer == null) throw new ArgumentNullException(nameof(mixer));
            _defaultSnapshot = RequireSnapshot(mixer, MixerRegistry.SnapshotDefault);
            _stethoscopeFocus = RequireSnapshot(mixer, MixerRegistry.SnapshotStethoscopeFocus);
            _dialogueFocus = RequireSnapshot(mixer, MixerRegistry.SnapshotDialogueFocus);
            _pausedSnapshot = RequireSnapshot(mixer, MixerRegistry.SnapshotPaused);
            _transitionSeconds = transitionSeconds;
        }

        /// <summary>玩家主动进入听诊动作(玩家行为)。</summary>
        public void EnterStethoscopeFocus()
        {
            _stethoscopeFocus.TransitionTo(_transitionSeconds);
        }

        /// <summary>玩家离开听诊动作 → 回 Default(玩家行为)。</summary>
        public void ExitStethoscopeFocus()
        {
            _defaultSnapshot.TransitionTo(_transitionSeconds);
        }

        /// <summary>玩家**主动发起**对话(点选病人交谈)→ 语声优先(玩家行为;
        /// 病人自发呻吟 / 咳嗽**不**走本方法 —— 规则三)。</summary>
        public void BeginDialogueFocus()
        {
            _dialogueFocus.TransitionTo(_transitionSeconds);
        }

        /// <summary>对话结束 → 回 Default(玩家行为)。</summary>
        public void EndDialogueFocus()
        {
            _defaultSnapshot.TransitionTo(_transitionSeconds);
        }

        /// <summary>暂停 / 恢复(玩家行为;Paused = 全总线衰减,回退 = Default)。</summary>
        /// <param name="paused">true = 进入 Paused;false = 回 Default。</param>
        public void SetPaused(bool paused)
        {
            if (paused)
            {
                _pausedSnapshot.TransitionTo(_transitionSeconds);
            }
            else
            {
                _defaultSnapshot.TransitionTo(_transitionSeconds);
            }
        }

        private static AudioMixerSnapshot RequireSnapshot(AudioMixer mixer, string snapshotName)
        {
            AudioMixerSnapshot snapshot = mixer.FindSnapshot(snapshotName);
            if (snapshot == null)
            {
                throw new InvalidOperationException(
                    $"混音器缺快照「{snapshotName}」—— 注册表五员(MixerRegistry.SnapshotNames)" +
                    "与资产失配;跑 MixerAssetGenerator 重新生成资产");
            }
            return snapshot;
        }
    }
}
