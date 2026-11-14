// 区域触发器(N1 · 2026-11-14)。
//
// 权威:
//   `design/levels/prologue-beat-script.md` §1(**N1** —— 「一张表 {cellRect, onEnter, once}
//   + 每 tick 比对玩家格;触发即回调」)
//   `design/gdd/content/chapter-plan.md` §四(施工顺序第 3 位)
//
// ═══ 它补的是什么 ══════════════════════════════════════════════════════════
//   全仓此前**没有可复用的空间触发**:
//     · 48 教学链只吃**事件流事实**(`predicateKind` = `EventKind` 成员名),空间不在谓词闭集里;
//     · 52 是**掷骰**驱动 ⇒ 不可复现,不能承载脚本节拍;
//     · `PrologueBeatDriver` 的「走到某处」是把 `near(anchor)` 闭包**临时塞进每一拍**的,
//       它没有**状态**(不知道「进去过没有」),也没有**出口**(离开时没有回调)。
//   ⇒ 凡是要「进入某片格 ⇒ 做一次事」的内容(十七个难民的出现、义地的可玩区域、
//     将来的任何区域内容)都得各写一遍格判定。本类就是那一份。
//
// ═══ 边界(三条硬线,承 prologue-beat-script §1 的要求)═══════════════════
//   ① **不写三流**:只读玩家格、只回调 —— 承 ADR-019 §三(只读消费者)。
//   ② **不进 sim 程序集**:`Sim` / `Sim.Contracts` 引用集一字不改(门 A 不受影响)。
//   ③ **只回答「在不在 / 进没进过」**:不冻结输入、不切相机、不推进时间 —— 那些归 N3。
//
// ⚠️ **矩形是闭区间**(端点含)。与 `chapter-1-tent-clinic.md` §3 的分区口径一致。

using System;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>一片**闭区间**格矩形上的区域触发器。</summary>
    public readonly struct RegionTrigger
    {
        /// <summary>触发器 id(非空、表内唯一;回调按它报信)。</summary>
        public readonly string Id;

        /// <summary>X 下界(含)。</summary>
        public readonly int X0;
        /// <summary>X 上界(含)。</summary>
        public readonly int X1;
        /// <summary>Z 下界(含)。</summary>
        public readonly int Z0;
        /// <summary>Z 上界(含)。</summary>
        public readonly int Z1;

        /// <summary>`true` = **一次**:首次进入后不再报 `onEnter`(仍报 `onExit`)。</summary>
        public readonly bool Once;

        /// <summary>构造。</summary>
        /// <exception cref="ArgumentException">id 为空,或区间反向(`X0 > X1` / `Z0 > Z1`)。</exception>
        public RegionTrigger(string id, int x0, int x1, int z0, int z1, bool once = true)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("区域触发器 id 不得为空", nameof(id));
            if (x0 > x1)
                throw new ArgumentException($"X 区间反向:X0={x0} > X1={x1}(闭区间要求 X0 ≤ X1)", nameof(x0));
            if (z0 > z1)
                throw new ArgumentException($"Z 区间反向:Z0={z0} > Z1={z1}(闭区间要求 Z0 ≤ Z1)", nameof(z0));

            Id = id;
            X0 = x0; X1 = x1; Z0 = z0; Z1 = z1;
            Once = once;
        }

        /// <summary>格是否落在本矩形内(**闭区间**)。</summary>
        public bool Contains(int x, int z) => x >= X0 && x <= X1 && z >= Z0 && z <= Z1;

        /// <summary>以一个格为中心的**切比雪夫**方框(半径 1 = 3×3,含本格与八邻格)。</summary>
        /// <param name="id">触发器 id。</param>
        /// <param name="cellX">中心格 X。</param>
        /// <param name="cellZ">中心格 Z。</param>
        /// <param name="radius">半径(≥ 0;0 = 只有本格)。</param>
        /// <param name="once">是否只报一次。</param>
        public static RegionTrigger Around(string id, int cellX, int cellZ, int radius = 1, bool once = true)
        {
            if (radius < 0)
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "半径不得为负");
            return new RegionTrigger(id, cellX - radius, cellX + radius,
                                     cellZ - radius, cellZ + radius, once);
        }

        /// <summary>诊断用:`id[x0..x1, z0..z1]`。</summary>
        public override string ToString() =>
            Id + "[" + X0 + ".." + X1 + ", " + Z0 + ".." + Z1 + "]" + (Once ? "" : "*");
    }
}
