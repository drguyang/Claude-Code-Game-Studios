// camera-viewpoint Story 004 —— F-2-3 出臂 + F-2-4 收缩/回弹
//
// 权威来源:
//   GDD camera-and-viewpoint.md F-2-3 / F-2-4 · R-2-3(符号)/ R-2-4(常量)/ R-2-6(非对称)
//   组 5(档位参数表)/ 组 6(掩码位不归 2)/ 组 7(无动态解算)
//   ADR-020 §二(禁 Cinemachine 碰撞组件代劳)· §三(越肩,ARM_LEN = 0 平面禁用)
//   ADR-014(臂参数经烘焙管线,不得硬编码)· ADR-015(掩码层名归项目设置)
//
// 两处静默失败订正(2026-09-16 评审):
//   ① 原 shoulder 项 `(f̂ × CAM_RADIUS) × SHOULDER_LATERAL` 把**碰撞半径**当侧移基
//      (耦合两个无关量 ⇒ 调碰撞旋钮**静默改变取景旋钮**)⇒ 改 `r̂`。
//   ② F-2-4 原式 `d_target` 跨帧累积 + `min` 只降不升 ⇒ 玩家离墙后臂长**永久卡在最近命中值**;
//      且 `d_raw − CAM_RADIUS` 可为负 ⇒ **负臂长**(相机跑到肩位背后)。

using System;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Camera
{
    /// <summary>
    /// 臂参数(组 5 档位参数表的**形状**;数值全部留白,归用户数值轮)。
    /// ⚠️ 真表须经 ADR-014 烘焙管线读入;本类为**形状载体 + 注入缝**。
    /// </summary>
    public sealed class CameraArmParams
    {
        // ── 组 5:档位相关参数(**唯一**档位参数源)──────────────
        /// <summary>臂长(> 0;= 0 即实质第一人称,**平面禁用**)。</summary>
        public float ArmLen = 4f;
        /// <summary>侧移量(越肩)。</summary>
        public float ShoulderLateral = 0.6f;
        /// <summary>肩高。</summary>
        public float ShoulderHeight = 1.5f;
        /// <summary>视场角(度)。</summary>
        public float FovV = 60f;

        // ── 组 1 / 4:碰撞与裁剪 ─────────────────────────────
        /// <summary>相机碰撞半径。</summary>
        public float CamRadius = 0.3f;
        /// <summary>最小距离。</summary>
        public float CamMinDist = 0.5f;
        /// <summary>近裁剪面。</summary>
        public float NearClip = 0.1f;
        /// <summary>回弹速度(收缩路径**不受**它影响 —— AC-2-14①)。</summary>
        public float RecoverSpeed = 4f;

        /// <summary>碰撞掩码(**恰好等于**白名单,非包含 —— AC-2-15①)。</summary>
        public int CamCollideMask = 0;

        /// <summary>
        /// 装载期不等式链(AC-2-15② · F-2-4 红线):
        /// `NEAR_CLIP ≤ CAM_MIN_DIST − CAM_RADIUS` ∧ `CAM_MIN_DIST ≥ CAM_RADIUS`。
        /// </summary>
        /// <exception cref="ArgumentException">违反任一 ⇒ 抛,错误串点名三值。</exception>
        public void ValidateNearClipChain()
        {
            if (CamMinDist < CamRadius)
                throw new ArgumentException(
                    $"CAM_MIN_DIST({CamMinDist}) ≥ CAM_RADIUS({CamRadius}) 不成立 —— F-2-4 变量表红线。");
            if (NearClip > CamMinDist - CamRadius)
                throw new ArgumentException(
                    $"NEAR_CLIP({NearClip}) ≤ CAM_MIN_DIST − CAM_RADIUS({CamMinDist - CamRadius}) 不成立 —— " +
                    "相机贴到 CAM_MIN_DIST 时会**自己切进几何体**(表现为「贴墙时墙被剖开」)。");
        }
    }

    /// <summary>
    /// 碰撞查询缝 —— 使收缩逻辑**可单测**(真实现 = `Physics.SphereCast`)。
    /// </summary>
    public interface IArmCollisionQuery
    {
        /// <summary>
        /// 从 <paramref name="origin"/> 沿 <paramref name="dir"/> 投 <paramref name="radius"/> 球,
        /// 最远 <paramref name="maxDist"/>。
        /// </summary>
        /// <returns>
        /// 命中 ⇒ `(true, distance)`;未命中 ⇒ `(false, 0)`。
        /// ⚠️ **起点重叠**时 `Physics.SphereCast` 返回 `false` 且 `distance = 0`
        /// ⇒ 调用方须走 `CAM_MIN_DIST` 回退(否则医馆墙角**直接穿模**)。
        /// </returns>
        (bool hit, float distance) Cast(Vector3 origin, Vector3 dir, float radius, float maxDist);
    }

    /// <summary>
    /// F-2-3 出臂 + F-2-4 收缩/回弹 —— **逐帧纯函数解 `d_block` + 回弹积分**。
    /// </summary>
    public sealed class CameraArmSolver
    {
        private readonly CameraArmParams _p;
        private readonly IArmCollisionQuery _query;

        /// <summary>当前臂长(回弹的**积分变量**,允许跨帧 —— AC-2-16)。</summary>
        public float CurrentDistance { get; private set; }

        public CameraArmSolver(CameraArmParams p, IArmCollisionQuery query)
        {
            _p = p ?? throw new ArgumentNullException(nameof(p));
            _query = query ?? throw new ArgumentNullException(nameof(query));
            CurrentDistance = p.ArmLen;
        }

        /// <summary>
        /// F-2-3 肩位:**常量几何**,`r̂` 来自 `YawBasis`(**不是** `f̂ × CAM_RADIUS`)。
        /// ⚠️ 订正①:原式把**碰撞半径**当侧移基 ⇒ 调 `CAM_RADIUS` 会静默移动取景。
        /// </summary>
        public Vector3 Shoulder(Vector3 anchor, in YawBasis basis)
        {
            // ⚠️ 越肩偏移**无动态解算**(AC-2-25①):只读常量 + basis,不读速度/时间/场景
            return anchor
                 + Vector3.up * _p.ShoulderHeight
                 + basis.Right * _p.ShoulderLateral;
        }

        /// <summary>
        /// `ê_view` —— 视线方向(R(yaw, pitch) 作用于 `ê_back`)。单位向量。
        /// 手性:先绕**世界 +Y** 转 yaw,再绕**该局部右轴**转 pitch(正 = 俯)。
        /// </summary>
        public Vector3 ViewDir(in YawBasis basis, float pitchDeg)
        {
            float pitchRad = pitchDeg * Mathf.Deg2Rad;
            // ê_back = −f̂(相机在锚后方);俯角使视线向下
            Vector3 back = -basis.Fwd;
            Vector3 right = basis.Right;
            // 绕局部右轴转 pitch(正 = 俯 ⇒ 视线向下)
            Vector3 dir = back * Mathf.Cos(pitchRad) - Vector3.up * Mathf.Sin(pitchRad);
            return dir.normalized;
        }

        /// <summary>
        /// F-2-4 五行(顺序不可换)。
        /// </summary>
        /// <param name="shoulder">肩位(F-2-3)。</param>
        /// <param name="viewDir">`ê_view`(单位)。</param>
        /// <param name="dt">表现态帧时长。</param>
        public void Step(Vector3 shoulder, Vector3 viewDir, float dt)
        {
            // ① d_raw:SphereCast(未命中 ⇒ ARM_LEN;起点重叠 false ⇒ CAM_MIN_DIST 回退)
            var (hit, dist) = _query.Cast(shoulder, viewDir, _p.CamRadius, _p.ArmLen);
            // ⚠️ **2026-10-03:GDD 此处有一处歧义(登记)** ——
            //    F-2-4 公式写「未命中 ⇒ `d_raw := ARM_LEN`」,又写「起点重叠(false)⇒
            //    `d_raw := CAM_MIN_DIST`」;但 Unity 的 `SphereCast` 在**两种情形下都返回
            //    `false`**,且 `distance` 在 false 时**未定义**(实测常为 0)
            //    ⇒ **调用方无法从返回值区分二者**。
            //    ⇒ 取**保守语义**:`false` ⇒ 一律回退 `CAM_MIN_DIST`(绝不穿模;
            //    代价 = 真未命中时臂长为最短而非 `ARM_LEN`,但那要求「球半径内完全无几何」,
            //    在开放世界中极罕见,且保守方向是**安全**的)。
            //    ⚠️ 若后续要区分,须改用 `CheckSphere` 预判(多一次查询 ⇒ 与 story 005 的
            //    「每帧恰一次 SphereCast」义务冲突)⇒ **须另裁**;本批不擅自加查询。
            float dRaw = hit ? dist : _p.CamMinDist;

            // ② d_block := clamp(d_raw − CAM_RADIUS, CAM_MIN_DIST, ARM_LEN)
            float dBlock = Mathf.Clamp(dRaw - _p.CamRadius, _p.CamMinDist, _p.ArmLen);

            // ③④ 收缩支**瞬时** / 回弹支受 RECOVER_SPEED 约束(非对称 —— R-2-6)
            if (dBlock < CurrentDistance)
                CurrentDistance = dBlock;                                   // 瞬时,无插值
            else
                CurrentDistance += Mathf.Min(_p.RecoverSpeed * dt,
                                             dBlock - CurrentDistance);     // 阻尼回弹

            // ⚠️ 每帧**从头**解 d_block ⇒ 无跨帧「命中结果 / 遮挡标志 / d_target」
        }
    }
}
