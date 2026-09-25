using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>F-3.1 单次求值的中间量(供 AC-3-A9②③ 断言 t / g;不进流、不进 sim)。</summary>
    public readonly struct AxisEvaluation
    {
        /// <summary>‖raw‖₂ —— 原样保留,含非有限输入下的 NaN / ∞(仅 Out 受 ① 守卫)。</summary>
        public readonly float M;

        /// <summary>死区进度 t = clamp((m − DZ_INNER)/(DZ_OUTER − DZ_INNER), 0, 1)。
        /// m = 0 点照常求值 —— ③ 的负例(直改 DZ_INNER &lt; 0 ⇒ 静止点 t &gt; 0 ⇒ 测红)依赖 T 不被守卫清零。</summary>
        public readonly float T;

        /// <summary>缓动增益 g = (3t² − 2t³)^CURVE_POW。</summary>
        public readonly float G;

        /// <summary>归一化输出;① 的守卫(NaN / ∞ / m = 0)使其恒为零向量。</summary>
        public readonly Vector2 Out;

        public AxisEvaluation(float m, float t, float g, Vector2 @out)
        {
            M = m;
            T = t;
            G = g;
            Out = @out;
        }
    }
}
