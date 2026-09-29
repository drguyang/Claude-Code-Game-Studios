// 权威来源:design/gdd/skeuomorphic-ui.md §三 无替代反馈(数量 = 器物本身)
//   · production/epics/skeuomorphic-ui/story-013-inventory-container.md
//
// 设计说明:
//   · 器物重量影响翻页节奏(重物翻页慢);重量归 20/21,42 只读呈现。
//   · 读取即弃,不落字段(AC-42-D3 不持游戏量)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>器物重量视觉反馈(翻页节奏倾斜)—— 只读呈现,不持值。</summary>
    public sealed class ItemWeightTilt
    {
        /// <summary>
        /// 由重量计算翻页节奏系数(读取即用,不缓存)。
        /// </summary>
        /// <param name="load">器物重量(0..1 归一化;数据归 20/21)。</param>
        /// <returns>节奏系数(&lt;1 慢,=1 基准)。</returns>
        public float EvaluatePageTempo(float load)
        {
            float w = load < 0 ? 0 : (load > 1 ? 1 : load);
            return 1f - 0.5f * w; // 重物最快半速,轻物基准速
        }
    }
}
