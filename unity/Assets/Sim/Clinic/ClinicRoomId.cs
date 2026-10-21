// Story 002 · clinic-machine —— 房间标识与 `ROOM_NONE` 哨兵(GDD 规则一 / EC-24-07)。
//
// 权威来源:
//   GDD design/gdd/clinic-machine.md 规则一 —— 「房间」**没有独立的全局 id**;它由布局 + 房间定义
//     **派生**(P1a 才需要跨房间命名)。
//   同 规则一 —— 布局产出 ≥ 2 个 4-不连通家具簇 ⇒ **整份布局 = `ROOM_NONE`(0,0)**(EC-24-07)。
//   ADR-005 —— 纯函数派生、无状态、可重放(同一布局 ⇒ 逐位一致的派生结果)。
//   story production/epics/clinic-machine/story-002-room-extraction-connectivity.md
//     §Implementation Notes 3 / 卡面 AC 第 5 条。
//
// 派生纪律(为什么是哈希而不是「最小角格」):
//   · 若 room id = 布局的最小格坐标,则一间位于世界原点的房间会得到 (0,0) —— **恰与
//     `ROOM_NONE` 的哨兵空间相交**,违反卡面实施约束「`ROOM_NONE = (0,0)`,与真实 room id
//     空间不交」。故 id 由**布局的规范排序格集**经确定性整数混合派生,`(0,0)` 保留给哨兵。
//   · 派生源**只有**格集(规范排序后的 x/y/z):无 `structure_id`(写入顺序产物)、
//     无 Unity instance id、无 `string.GetHashCode`(其 .NET Core 实现**每进程随机化**)、
//     无字典迭代序、无时间 / 随机源。
//   · 两个不同布局可能派生到同一 id —— 这不违反本卡面判据(房间无全局唯一 id;判据只要求
//     「同一布局两次求值 ⇒ 同一 room id」与「真实 id 永不等于 ROOM_NONE」)。

using System;

namespace DaYiJingCheng.Sim.Clinic
{
    /// <summary>
    /// 房间标识(由布局确定性派生);<see cref="None"/> = `ROOM_NONE` 哨兵 = `(0, 0)`。
    /// <para>⚠️ <see cref="High"/> / <see cref="Low"/> 是布局哈希的两个 32 位半,**不是世界坐标**,
    /// 不得拿来当格用(格一律走 `Sim.Contracts.WorldPos`)。</para>
    /// </summary>
    public readonly struct ClinicRoomId : IEquatable<ClinicRoomId>
    {
        /// <summary>哈希高 32 位(<see cref="None"/> 时为 0)。</summary>
        public readonly int High;

        /// <summary>哈希低 32 位(<see cref="None"/> 时为 0)。</summary>
        public readonly int Low;

        /// <summary>构造房间标识(唯一合法构造点 = <c>ClinicRoomExtraction</c>;测试不构造)。</summary>
        public ClinicRoomId(int high, int low)
        {
            High = high;
            Low = low;
        }

        /// <summary>
        /// `ROOM_NONE = (0,0)` —— 「无房间 / 不可评分」的哨兵(EC-24-01 / EC-24-02 / EC-24-07
        /// 的统一出口)。真实房间 id **永不**等于它(派生侧有确定性重映射,见
        /// <c>ClinicRoomExtraction.ComputeRoomId</c>)。
        /// </summary>
        public static readonly ClinicRoomId None = new ClinicRoomId(0, 0);

        /// <summary>是否等于 `ROOM_NONE` 哨兵。</summary>
        public bool IsNone => High == 0 && Low == 0;

        /// <summary>值相等(两个半都相等)。</summary>
        public bool Equals(ClinicRoomId other) => High == other.High && Low == other.Low;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is ClinicRoomId other && Equals(other);

        // 仅作内存字典键,不落盘(承 ADR-007 §四 对 PatientId.GetHashCode 的同一纪律)。
        /// <inheritdoc />
        public override int GetHashCode() => unchecked((High * 397) ^ Low);

        /// <summary>值相等。</summary>
        public static bool operator ==(ClinicRoomId a, ClinicRoomId b) => a.Equals(b);

        /// <summary>值不等。</summary>
        public static bool operator !=(ClinicRoomId a, ClinicRoomId b) => !a.Equals(b);

        /// <summary>诊断用文本(<c>ROOM_NONE (0,0)</c> 或 <c>room(high,low)</c>)。</summary>
        public override string ToString() =>
            IsNone ? "ROOM_NONE (0,0)" : "room(" + High + "," + Low + ")";
    }
}
