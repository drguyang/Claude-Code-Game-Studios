// 权威来源:ADR-015 §三(单一整数格)· ADR-009 §Key Interfaces(:473)
//
// ⚠️ 禁用第二套坐标;禁 chunk 局部坐标系(ADR-015 §四:chunk 是流式/脏块优化,非坐标原点)。
// ⚠️ 与 Int3 的区别是本类型承载「逻辑格身份」(进事件流、参与判定),Int3 只承载「呈现锚点」。

using System;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>整数格坐标(i32 × 3)—— 地形格 / 建造槽 / 掉落锚点 / 资源点 / 导航格共用(ADR-015 §三)。</summary>
    public readonly struct WorldPos : IEquatable<WorldPos>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public WorldPos(int x, int y, int z) { X = x; Y = y; Z = z; }

        public bool Equals(WorldPos other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is WorldPos p && Equals(p);

        // 仅作内存字典键,不落盘(承 ADR-007 §四 对 PatientId.GetHashCode 的同一纪律)。
        public override int GetHashCode() => unchecked((X * 73856093) ^ (Y * 19349663) ^ (Z * 83492791));

        public static bool operator ==(WorldPos a, WorldPos b) => a.Equals(b);
        public static bool operator !=(WorldPos a, WorldPos b) => !a.Equals(b);
    }
}
