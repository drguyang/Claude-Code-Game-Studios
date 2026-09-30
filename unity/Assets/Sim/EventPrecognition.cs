// ADR-009 / GDD random-events.md 规则五/六/八 —— 预告制、避险与因果可见。
//
// 权威来源:
//   ADR-009 §一 —— 三态分类（预告 = 导演本地态不进流，降临才进流）
//   GDD random-events.md 规则五 —— 预告制
//   GDD random-events.md 规则六 —— 因果可见
//   GDD random-events.md 规则八 —— 可拒绝/绕开
//
// 核心机制:
//   - 预告制无直降路径
//   - 预告内容 = 定性线索（零数值通道）
//   - 避险 = 纯不生成（sim 侧零伤害路径）
//   - 可脱离 = 降临遭遇存在撤退路径

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 预告状态（导演本地态，不进流）。
    /// </summary>
    public sealed class PrecognitionState
    {
        public int EventKey;
        public long PreviewTick;
        public long ArrivalTick;
        public bool IsDefered;
        public bool IsEvaded;
        public string CauseClueKey;

        public PrecognitionState(int eventKey, long previewTick, long arrivalTick, string causeClueKey)
        {
            EventKey = eventKey;
            PreviewTick = previewTick;
            ArrivalTick = arrivalTick;
            IsDefered = false;
            IsEvaded = false;
            CauseClueKey = causeClueKey;
        }
    }

    /// <summary>
    /// 线索 DTO（整数 key，呈现层自行映射定性文案）。
    /// </summary>
    public readonly struct EventCueDto
    {
        public readonly int EventKey;
        public readonly string CauseClueKey;
        public readonly int TierKind;

        public EventCueDto(int eventKey, string causeClueKey, int tierKind)
        {
            EventKey = eventKey;
            CauseClueKey = causeClueKey;
            TierKind = tierKind;
        }
    }

    /// <summary>
    /// 预告制、避险与因果可见。
    /// </summary>
    public static class EventPrecognition
    {
        /// <summary>
        /// 判断是否应避险（三条件门）。
        /// </summary>
        public static bool ShouldEvade(bool isInClinic, bool isOnExpedition, bool isInDanger)
        {
            // 避险 = 在医馆 ∧ 在出诊路径上 ∧ 处于危险
            return isInClinic && isOnExpedition && isInDanger;
        }

        /// <summary>
        /// 判断是否可脱离。
        /// </summary>
        public static bool CanDisengage(bool isSurrounded, bool hasRetreatPath)
        {
            // 可脱离 = 未被包围 ∨ 有撤退路径
            return !isSurrounded || hasRetreatPath;
        }

        /// <summary>
        /// 验证线索零数值。
        /// </summary>
        public static bool ValidateNoNumeric(string clueKey)
        {
            if (string.IsNullOrEmpty(clueKey)) return true;

            // 检查是否包含阿拉伯数字
            foreach (char c in clueKey)
            {
                if (char.IsDigit(c)) return false;
            }
            return true;
        }

        /// <summary>
        /// 创建线索 DTO。
        /// </summary>
        public static EventCueDto CreateCue(int eventKey, string causeClueKey, int tierKind)
        {
            return new EventCueDto(eventKey, causeClueKey, tierKind);
        }

        /// <summary>
        /// 验证预告制无直降。
        /// </summary>
        public static bool ValidateNoDirectDrop(List<PrecognitionState> states)
        {
            foreach (var state in states)
            {
                // 每个降临必须有先行的预告
                if (state.ArrivalTick <= state.PreviewTick)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
