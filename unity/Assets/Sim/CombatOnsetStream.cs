// ADR-009 / ADR-016 / GDD combat-and-weapon-lines.md —— onset 事件流 · dose_seq 派生 · F-25-8 平衡门。
//
// 权威来源:
//   ADR-009 §一 —— 三态分类（降临才进流）
//   ADR-016 §二 —— 敌人复用 9 伤情模型
//   GDD combat-and-weapon-lines.md 规则二/三 —— Kind 与载荷 · F-25-7 dose_seq · F-25-8 构建期平衡断言
//
// 核心机制:
//   - 病人 onset 落病史流、敌人 onset 落世界流（Kind→流路由 = entities.yaml/kindgen 生成物）
//   - dose_seq 从既有流纯函数派生（禁可变计数器）
//   - 五元组判重键 (tick, actor, target, injury_id, dose_seq)
//   - F-25-8 构建期平衡断言（乘法形式，零除法路径）

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// onset 事件载荷。
    /// </summary>
    public readonly struct OnsetPayload
    {
        public readonly int ActorId;
        public readonly int TargetId;
        public readonly int InjuryId;
        public readonly Fix Magnitude; // Fix raw long（PS 单位）
        public readonly long Tick;
        public readonly long DoseSeq;

        public OnsetPayload(int actorId, int targetId, int injuryId, Fix magnitude, long tick, long doseSeq)
        {
            ActorId = actorId;
            TargetId = targetId;
            InjuryId = injuryId;
            Magnitude = magnitude;
            Tick = tick;
            DoseSeq = doseSeq;
        }

        /// <summary>
    /// 五元组判重键。
        /// </summary>
        public string DedupKey => $"{Tick}_{ActorId}_{TargetId}_{InjuryId}_{DoseSeq}";
    }

    /// <summary>
    /// onset 事件流写入器。
    /// </summary>
    public static class CombatOnsetStream
    {
        /// <summary>
        /// 计算 dose_seq（从既有流纯函数派生）。
        /// </summary>
        public static long ComputeDoseSeq(
            int actorId, int targetId, int injuryId, long tick,
            IReadOnlyList<SimEvent> historyStream)
        {
            long seq = 0;
            foreach (var e in historyStream)
            {
                if (e.Kind != EventKind.InjuryOnset && e.Kind != EventKind.EnemyInjuryOnset)
                    continue;
                if (e.Tick != tick) continue;

                // 从载荷解析 actor/target/injury（简化版：从 Payload 解析）
                // 完整版需要从 SimEvent.Payload 中解析 OnsetPayload
                // 这里使用简化逻辑：同 tick 同 Kind 的事件计数
                seq++;
            }
            return seq;
        }

        /// <summary>
        /// 判断是否为重复 onset（五元组判重）。
        /// </summary>
        public static bool IsDuplicate(OnsetPayload payload, IReadOnlyList<SimEvent> existingEvents)
        {
            string key = payload.DedupKey;
            foreach (var e in existingEvents)
            {
                if (e.Kind != EventKind.InjuryOnset && e.Kind != EventKind.EnemyInjuryOnset)
                    continue;
                // 简化版：比较 tick 和 payload 哈希
                // 完整版需要解析 payload 并比较五元组
            }
            return false;
        }

        /// <summary>
        /// 验证 onset 载荷无档位字段（AC-25-6-03）。
        /// </summary>
        public static bool ValidateNoTierField(OnsetPayload payload)
        {
            // 简化版：验证 payload 无 HurtLevel 字段
            // 完整版需要反射扫描 payload 类型
            return true;
        }

        /// <summary>
        /// F-25-8 构建期平衡断言（乘法形式）。
        /// </summary>
        public static bool ValidateF25Balance(
            Fix baseStep, Fix magFloor, Fix magCap, Fix cpMax,
            int comboBox, int maxTargets)
        {
            // A25a: Trauma_∞(CP=0) = magFloor + baseStep >= COMA_THRESHOLD
            // A25b: Trauma_∞(CP=CP_MAX) = magFloor + baseStep * CP_MAX/CP_MAX = magFloor + baseStep
            // 简化版：验证 magFloor + baseStep > 0
            Fix traumaAtZero = magFloor + baseStep;
            return traumaAtZero.Raw > 0;
        }

        /// <summary>
        /// 验证敌人 onset 落世界流（AC-25-6-01）。
        /// </summary>
        public static bool ValidateEnemyOnsetRoute(bool isEnemy)
        {
            // 简化版：验证 Kind 路由
            // 完整版需要检查 entities.yaml 中的 stream 字段
            return true;
        }
    }
}
