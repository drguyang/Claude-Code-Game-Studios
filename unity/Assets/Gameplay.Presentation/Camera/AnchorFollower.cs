// camera-viewpoint Story 003 —— F-2-1 锚跟随(半隐式 + 子步)
//
// 权威来源:
//   GDD camera-and-viewpoint.md F-2-1(公式)· 组 1(旋钮)/ 组 5b(EPS 归属)/ 组 5c(n/h 是派生量)/ 组 6(MAX_DT)
//   ADR-020 §一(数值留白 AC-20-11)· §四(相机只读 1 的 Position)
//   ADR-015 §三(MAX_DT 与 LATTICE_SIZE 同源,单一装载常量,禁二次定义)
//
// 积分器改判(2026-09-16 评审):
//   显式(前向)欧拉在二阶系统上**条件稳定**(ω·dt 近 1 环振、超 2 发散);
//   原稿把「钳到 MAX_DT」当稳定性手段 —— **恰恰掩盖了问题**(钳位防单帧尖峰,
//   防不了 ANCHOR_RESPONSE 被调大)。改**半隐式 + 子步**后稳定性内化
//   ⇒ **ω 无上界约束**;MAX_DT 回归它在系统 1 的本来身份:**位移预算**常量。
//
// 两行顺序是判据不是风格:①先更新速度 ②再用**新**速度更新位置。
// 写反 = 前向欧拉 ⇒ 大 ω + 大 dt 处**发散**而非仅过冲。
//
// ⚠️ **2026-10-03 评审回刷(诚实口径)**:「ω 超大档不发散」这条**只证有界**,
//   **不区分**两种积分器 —— 子步机制保证 ω·h ≤ 0.5,而该条件下**前向欧拉也稳定**
//   (实测:ω=1e4、dt=1/60 ⇒ n=334、ω·h≈0.499,两者 300 帧后 max|anchor| 均 ≈ 1.0)。
//   ⇒ 区分两行顺序的**唯一**判据是**首帧位移**(半隐式首帧即动,前向欧拉用旧速度 0
//   ⇒ 首帧零位移),且该判据**仅在 n == 1 时成立**(ω 小到不触发子步)。

using System;
using DaYiJingCheng.Sim.World;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Camera
{
    /// <summary>
    /// F-2-1 的旋钮(数值全部留白,归用户数值轮)。
    /// </summary>
    public sealed class AnchorFollowConfig
    {
        /// <summary>锚响应频率 ω(组 1;数值留白)。</summary>
        public float AnchorResponse = 8f;

        /// <summary>
        /// 位移预算钳位常量(**毫秒**,**唯一来源 = 系统 1 的 `WorldLatticeParams.MaxDtMs`)。
        /// 🔴 **2026-10-03 评审修复(B1)**:初版此处持**字面量默认值 `= 100`**
        /// ⇒ 2 侧**自造了第二份 `MAX_DT`**,而「同源」的机械含义是**读同一实体**
        /// (`WorldLatticeParams.MaxDtMs`),不是两份相等的字面量(相等字面量会各自漂移 ——
        /// GDD 组 6 的静默失配防线)。
        /// ⇒ 现**移除字面量默认**:本字段为**必注入**;唯一合法装载路径 =
        /// <see cref="AnchorFollowConfig.FromWorldLattice"/>。
        /// </summary>
        public int MaxDtMs;

        /// <summary>超过该距离 ⇒ 直接吸附(EC-2-4;数值留白)。</summary>
        public float TeleportSnapDist = 50f;

        /// <summary>过冲容差(组 5b;**归 2**,数值留白)。</summary>
        public float AnchorOvershootEps = 1e-3f;

        /// <summary>位移上界(组 1 的派生界/登记量;数值留白)。</summary>
        public float AnchorSpeedMax = 100f;

        /// <summary>
        /// **单一来源工厂**(AC-2-12② 的机制本体):从系统 1 的装载常量
        /// <see cref="WorldLatticeParams.MaxDtMs"/> 读取位移预算,在 2 侧**不定义第二份**。
        /// </summary>
        /// <remarks>
        /// 余下旋钮(ω / 吸附距 / 容差 / 位移上界)仍留白 ⇒ 由调用方在返回后注入;
        /// 本工厂只钉死「同源」的那一个量。GDD 组 6:「若 2 自行定义第二个 `MAX_DT`,
        /// 即构成静默失配」—— 本方法即该纪律的唯一合法入口。
        /// </remarks>
        public static AnchorFollowConfig FromWorldLattice(in WorldLatticeParams lattice)
        {
            if (lattice.MaxDtMs <= 0)
                throw new ArgumentException(
                    $"WorldLatticeParams.MaxDtMs 须 > 0,实际 = {lattice.MaxDtMs} ms —— " +
                    "相机位移预算不得在 2 侧兜底(读同一实体,不造第二份)。", nameof(lattice));
            return new AnchorFollowConfig { MaxDtMs = lattice.MaxDtMs };
        }
    }

    /// <summary>
    /// F-2-1 锚跟随器 —— 半隐式 + 子步。
    /// 纯 C#(不依赖 MonoBehaviour)⇒ 可单测。
    /// </summary>
    public sealed class AnchorFollower
    {
        private readonly AnchorFollowConfig _cfg;

        /// <summary>当前锚位。</summary>
        public Vector3 Anchor { get; private set; }

        /// <summary>当前锚速。</summary>
        public Vector3 Velocity { get; private set; }

        public AnchorFollower(AnchorFollowConfig cfg)
        {
            _cfg = cfg ?? throw new System.ArgumentNullException(nameof(cfg));
            Anchor = Vector3.zero;
            Velocity = Vector3.zero;
        }

        /// <summary>子步数 n := ceil(dt / (0.5/ω)) —— **派生量**,非旋钮(组 5c)。</summary>
        public int SubstepCount(float dt)
        {
            float maxH = 0.5f / _cfg.AnchorResponse;   // ω·h ≤ 0.5 的稳定性前提
            if (maxH <= 0f) return 1;
            int n = Mathf.CeilToInt(dt / maxH);
            return n < 1 ? 1 : n;                       // n ≥ 1;n=1 退化为半隐式欧拉(仍无条件稳定)
        }

        /// <summary>
        /// 推进一步。
        /// </summary>
        /// <param name="playerPos">1 的连续位置(下游值,只读)。</param>
        /// <param name="dtSeconds">表现态帧时长(**非 tick**;已由调用方取 unscaled)。</param>
        public void Step(Vector3 playerPos, float dtSeconds)
        {
            // ── EC-2-4:超距 ⇒ 直接吸附,v := 0,不做平滑追赶 ──────────────
            //    (否则传送后玩家看见相机横穿整张地图)
            if (Vector3.Distance(playerPos, Anchor) > _cfg.TeleportSnapDist)
            {
                Anchor = playerPos;
                Velocity = Vector3.zero;
                return;
            }

            // ── AC-2-12①:位移预算钳位(不是稳定性面)────────────────────
            float maxDt = _cfg.MaxDtMs / 1000f;
            float dt = Mathf.Min(dtSeconds, maxDt);

            // ── 半隐式 + 子步 ────────────────────────────────────────────
            int n = SubstepCount(dt);
            float h = dt / n;

            float omega = _cfg.AnchorResponse;
            for (int i = 0; i < n; i++)
            {
                // ① 先更新速度(用**旧**位置误差)
                Vector3 accel = (playerPos - Anchor) * (omega * omega) - Velocity * (2f * omega);
                Velocity += accel * h;
                // ② 再用**新**速度更新位置
                Anchor += Velocity * h;
            }
        }
    }
}
