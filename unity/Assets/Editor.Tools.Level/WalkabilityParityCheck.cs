// 关卡工具 —— **C2 可走性同源**(硬失败)。ADR-022 §四。
//
// 权威来源:
//   ADR-022 §四 C1–C6 表:**C2 两层可走性同源 = 硬失败**
//   ADR-015 §一之补(`slopeLimit` / `stepOffset` 真源 = 关卡工具;系统 1 只执行)
//   `Sim.Contracts/LatticeParamQuantization.IsWithinOneQuantum`(**唯一的偏差公式**)
//
// ═══════════════════════════════════════════════════════════════════════════
// 判据:`|engineUnits − bakedUnits| ≤ 1 量子`,slopeLimit 与 stepOffset **各一条**
// ═══════════════════════════════════════════════════════════════════════════
// 为什么是**两条独立判据**而不是一条:两者是不同的量、不同的量子(0.01 度 vs 1 mm)、
// 不同的运行时消费者(slopeLimit 决定能不能上坡;stepOffset 决定能不能迈台阶)。
// 合成一条会把「slopeLimit 差 5 度」与「stepOffset 差 5 毫米」压成同一个布尔 ⇒
// 夹具停用其中一条时另一条会把它盖住,**判别力归零**(本仓「一属性一条变异」纪律的同一理由)。
//
// 为什么容差是 1 量子而不是 0:引擎侧是 `float`,量化必然有取整误差;而「1 量子」
// 恰好是「取整可能引入的最大误差」的量级 ⇒ 容差若为 0,任何一次取整都会红。
// 容差若为 2,「有人把 45° 改成 45.02°」这种真实漂移就会被吞掉。
// ⇒ 1 是唯一自洽的取值,**且它必须由 `IsWithinOneQuantum` 定义,不能在这里写 `<= 1`**。

using System;                        // ArgumentNullException(terrain 为 null 时 fail-closed)
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // LatticeParamQuantization(唯一出处)

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>C2:烘焙产物里的格点参数 ↔ 引擎侧控制器参数**是否同源**。</summary>
    public static class WalkabilityParityCheck
    {
        /// <summary>判据关键词:`slopeLimit` 那一半的报文前缀(夹具按它点名)。</summary>
        public const string SlopeLimitKey = "C2 slopeLimit";

        /// <summary>判据关键词:`stepOffset` 那一半的报文前缀。</summary>
        public const string StepOffsetKey = "C2 stepOffset";

        /// <summary>
        /// 跑两条同源判据(不抛;失败项进 `violations`)。
        /// </summary>
        /// <param name="terrain">
        /// 关卡的地形声明(烘焙侧整数真源)。**不得为 `null`** —— 没有烘焙侧就**没有比对对象**;
        /// 调用方须先判「本关卡是否声明了地形」并自行登记 `NotRun`。
        /// </param>
        /// <param name="engine">引擎侧已量化的参数。</param>
        /// <param name="violations">输出:失败项(两条判据各自独立追加)。</param>
        /// <returns>两条都通过 ⇒ `true`。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="terrain"/> 为 null。</exception>
        /// <remarks>
        /// 🔁 <b>2026-10-29 复评修复(Agent A 残留)</b>:此前 `terrain == null` 直接
        /// `return true` —— 「**没有比对对象**」被表达成「**比对通过**」。
        /// 调用方当时靠外层守卫绕开了它,但那只是**调用方**记得;任何新调用方都会再踩一次。
        /// ⇒ 改为**抛**:唯一合法做法是由调用方在缺输入时登记 `NotRun`,而不是让本方法假装通过。
        /// <para>这是 fail-closed:宁可让新调用方立刻炸,也不要让它静默绿。</para>
        /// </remarks>
        public static bool TryCheck(TerrainSource terrain, LatticeEngineParams engine,
            out List<string> violations)
        {
            violations = new List<string>();
            if (terrain == null)
                throw new ArgumentNullException(
                    nameof(terrain),
                    "C2 的烘焙侧输入为 null —— 没有比对对象时**不存在**「通过」这个结论;" +
                    "调用方须先判「本关卡是否声明了地形」并登记 NotRun");

            if (!LatticeParamQuantization.IsWithinOneQuantum(
                    engine.SlopeLimitDegX100, terrain.SlopeLimitDegX100))
            {
                violations.Add(
                    $"{SlopeLimitKey} 不同源:引擎侧 {engine.SlopeLimitDegX100}(0.01 度)≠ " +
                    $"烘焙侧 {terrain.SlopeLimitDegX100},偏差 " +
                    $"{Delta(engine.SlopeLimitDegX100, terrain.SlopeLimitDegX100)} 量子 > 1 —— " +
                    "玩家读的是控制器,不是整数格(ADR-022 §四 C2' 的同一理由):" +
                    "两侧不一致 ⇒ 同一段坡,判定说能走、脚感说不能");
            }

            if (!LatticeParamQuantization.IsWithinOneQuantum(
                    engine.StepOffsetMm, terrain.StepOffsetMm))
            {
                violations.Add(
                    $"{StepOffsetKey} 不同源:引擎侧 {engine.StepOffsetMm} mm ≠ " +
                    $"烘焙侧 {terrain.StepOffsetMm} mm,偏差 " +
                    $"{Delta(engine.StepOffsetMm, terrain.StepOffsetMm)} 量子 > 1 —— " +
                    "台阶高度两侧不一致 ⇒ 建造模块的 collider 在一边是台阶、另一边是墙");
            }

            return violations.Count == 0;
        }

        private static long Delta(int engineUnits, int bakedUnits)
            => System.Math.Abs((long)engineUnits - bakedUnits);
    }
}
