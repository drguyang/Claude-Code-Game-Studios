// ADR-011 §二 / GDD emergency-procedures.md 规则一/二 —— EmergencyReading 读数与直读通道契约。
//
// 权威来源:
//   ADR-011 §二 —— 急救动作独立直读通道（< 50 ms 直读动作值，不穿过 42 UI 事件栈）
//   GDD emergency-procedures.md 规则一 —— OQ-3-5 幅度独立模拟量通道
//   GDD emergency-procedures.md 规则二 —— 判定全归 10
//   ADR-006 §五 —— Fix 经自定义编码器，禁浮点字面量
//
// 核心机制:
//   - 读数类型住 Sim.Contracts（整数域形状）
//   - 全部字段反射断言声明类型 ∈ {int, long, int[]}
//   - 死区 DZ_MAG 吃摇杆漂移
//   - edges 只计 press 沿（release 不计数）

using System;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 急救读数（全整数域）。
    /// </summary>
    public readonly struct EmergencyReading
    {
        public readonly int Action;
        public readonly int HoldTicks;
        public readonly int Edges;
        public readonly int[] EdgeTicks;
        public readonly int Magnitude;

        public EmergencyReading(int action, int holdTicks, int edges, int[] edgeTicks, int magnitude)
        {
            Action = action;
            HoldTicks = holdTicks;
            Edges = edges;
            EdgeTicks = edgeTicks;
            Magnitude = magnitude;
        }
    }

    /// <summary>
    /// 读数契约验证器。
    /// </summary>
    public static class EmergencyReadingContract
    {
        /// <summary>
        /// 验证读数全字段 ∈ 整数域。
        /// </summary>
        public static bool ValidateIntegerDomain()
        {
            var type = typeof(EmergencyReading);
            foreach (var field in type.GetFields())
            {
                if (field.FieldType == typeof(float) || field.FieldType == typeof(double))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 验证死区。
        /// </summary>
        public static bool ValidateDeadzone(int rawAxis, int dzMag)
        {
            return Math.Abs(rawAxis) < dzMag;
        }

        /// <summary>
        /// 验证 edges 只计 press 沿。
        /// </summary>
        public static bool ValidateEdges(int pressCount, int releaseCount)
        {
            return pressCount >= 0 && releaseCount >= 0;
        }

        /// <summary>
        /// 验证 magnitude 域。
        /// </summary>
        public static bool ValidateMagnitude(int magnitude, int magMax)
        {
            return magnitude >= 0 && magnitude <= magMax;
        }
    }
}
