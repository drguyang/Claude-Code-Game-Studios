// 权威来源:Story 002 · GDD input-system.md F-3.1 全文(radial 死区 → C1 幂缓动 → 归一)
//   · ADR-025 §② 甲案(b4 ToFloat 白名单含 Gameplay.* ⇒ 常量在本装配内转 float 合法)
//   · ADR-006(运行期不存在 float → Fix 路径 —— 本函数 float 进 float 出,常量单向 ToFloat)

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>F-3.1 轴处理:死区 → 幂缓动 → 归一(手感层;输出不进流、不进 sim —— GDD 规则八)。
    /// <para>常量以 <see cref="Fix"/> 承载(装载期断言把关),每次求值经 <c>ToFloat()</c>
    /// 单向转入手感 float 域;逐帧成本 = 3 次标量转换,热路径优化归 Story 010。</para></summary>
    public static class AxisProcessor
    {
        /// <summary>只取归一化输出(常规消费入口)。</summary>
        public static Vector2 Process(Vector2 raw, in AxisTuning tuning)
            => Evaluate(raw, tuning).Out;

        /// <summary>求值并返回全部中间量(AC-3-A9②③ 的 t / g 断言经此读取)。
        /// <para><b>T / G 是 m 的纯公式函数,无条件求值</b>(含 m = 0 点)—— ① 的守卫只作用于
        /// <see cref="AxisEvaluation.Out"/>;若在入口把 T 清零,③ 的负例(直改 DZ_INNER &lt; 0
        /// ⇒ 静止点 t &gt; 0 ⇒ 测红)就成了空断言。</para>
        /// <para>非有限输入:T / G 为 NaN 但不外泄(Out 恒零);M 原样保留供诊断。</para></summary>
        public static AxisEvaluation Evaluate(Vector2 raw, in AxisTuning tuning)
        {
            float inner = tuning.DzInner.ToFloat();
            float outer = tuning.DzOuter.ToFloat();
            float pow = tuning.CurvePow.ToFloat();

            float m = raw.magnitude;

            // t:手写两分支 clamp —— 与 GDD 原式逐字对应;NaN 两比较皆 false ⇒ 原样穿过,
            // 不依赖引擎 clamp 对 NaN 的行为。守卫见下,不在此处。
            float t = (m - inner) / (outer - inner);
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;

            float s = 3f * t * t - 2f * t * t * t;
            float g = (float)System.Math.Pow(s, pow);

            // AC-3-A9①:NaN / ∞ / m = 0 ⇒ 输出恰 (0,0)(非 NaN、非异常)。
            // !(m > 0f) 同时捕获 m = 0 与 m = NaN;raw 分量的 NaN / ∞ 单独拦(raw.x/m 对
            // ∞/∞ = NaN,m 对单分量 ∞ = ∞ 不为 NaN —— 两个通道都要封)。
            Vector2 outv;
            if (float.IsNaN(raw.x) || float.IsInfinity(raw.x)
                || float.IsNaN(raw.y) || float.IsInfinity(raw.y)
                || !(m > 0f))
            {
                outv = Vector2.zero;
            }
            else
            {
                outv = new Vector2(raw.x / m * g, raw.y / m * g);
            }

            return new AxisEvaluation(m, t, g, outv);
        }
    }
}
