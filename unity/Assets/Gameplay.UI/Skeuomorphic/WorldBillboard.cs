// 权威来源:design/gdd/skeuomorphic-ui.md F6(世界锚点面片尺寸 · 恒定屏幕尺寸)
//   · production/epics/skeuomorphic-ui/story-005-world-billboard.md
//
// 设计说明:
//   · scale_world = k_screen × 2·d × tan(FOV_v / 2)(OQ-42-1 裁定甲,唯一模式)。
//   · 面片始终面向相机;世界空间 = 独占焦点门(不参与平面焦点门)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>世界空间 billboard 面片(F6)—— 恒定屏幕尺寸的锚点渲染。</summary>
    public sealed class WorldBillboard
    {
        /// <summary>屏幕占比系数 k_screen(ratio,值域 (0,1);数值归数值轮)。</summary>
        public double KScreen { get; set; } = 0.08;

        /// <summary>
        /// 计算世界缩放(恒定屏幕尺寸模式)。
        /// </summary>
        /// <param name="distance">锚点–相机距离 d(&gt;0)。</param>
        /// <param name="fovDegrees">垂直视场角(0,180)开区间。</param>
        /// <returns>世界缩放 &gt; 0;参数非法返回 0。</returns>
        public double ComputeWorldScale(double distance, double fovDegrees)
        {
            if (distance <= 0 || fovDegrees <= 0 || fovDegrees >= 180 || KScreen <= 0)
                return 0;
            double halfFovRad = fovDegrees * System.Math.PI / 180.0 / 2.0;
            return KScreen * 2.0 * distance * System.Math.Tan(halfFovRad);
        }
    }
}
