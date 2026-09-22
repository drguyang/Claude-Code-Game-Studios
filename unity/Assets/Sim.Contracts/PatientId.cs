// 权威来源:ADR-007 §四(:251-267,逐字转录)
//
// 必须是值类型(可作字段 / 参数 / 返回类型),不能是 static class ——
// 2026-09-15 架构复核 C-1 就地修正了原稿的 `public static class PatientId`(编译级冲突)。

using System;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>病人 / 受伤实体 id(值类型)。与敌人、玩家共用同一发号空间(ADR-016 §二 · ADR-006 Amendment B)。</summary>
    public readonly struct PatientId : IEquatable<PatientId>
    {
        public readonly int Value;

        public PatientId(int value) { Value = value; }

        /// <summary>显式负数哨兵,与任何合法 id(≥ 0)可区分。</summary>
        public static readonly PatientId None = new PatientId(-1);

        public bool IsNone => Value < 0;

        public bool Equals(PatientId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PatientId p && Equals(p);
        public override int GetHashCode() => Value;      // 仅用于字典键,不落盘(ADR-007 §四 · 报告 E-6)
        public static bool operator ==(PatientId a, PatientId b) => a.Value == b.Value;
        public static bool operator !=(PatientId a, PatientId b) => a.Value != b.Value;
    }
}
