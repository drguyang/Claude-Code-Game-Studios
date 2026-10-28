// 关卡工具 —— **POI 定义声明**(ADR-022 §三 `world_poi.json`;ADR-021 §① 定义 = 派生态)。
//
// 权威来源:
//   ADR-022 §三(导出契约 `world_poi.json`)· §六(边界表:「POI **状态**:✖ 不碰」)
//   ADR-021 §①(POI 一分为二:**定义 = 派生态**(位置 / 守卫 / 类型,烘焙逻辑层,不进流)·
//     **状态 = 模拟态**(进世界流,唯一写者 = 系统 6))
//   ADR-015 §三(逻辑层只有整数格;禁浮点)
//   `production/epics/level-tool/story-004-world-poi-and-resources.md`(AC-1 / AC-6)
//
// ⚠️ **本文件里出现不了 `PoiState`** —— 那是 story-004 AC-6 的机械判据
//    (`grep -c "PoiState" unity/Assets/Editor.Tools.Level/*.cs` == 0)。
//    ADR-022 §六 的边界不是「我们小心不碰」,而是**扫得出来**。
//
// ⚠️ **`guard_ref` 取「乙案」= 整数档位,不是敌人 id**(story-004 §Implementation Notes)。
//    理由:27 的敌人定义集**今日不存在**;取甲案会引入一个**未定义的跨系统引用**。
//    具体敌人由 52 抽池时按档位决定 —— 那一步归 52,不在本工具的边界内。
//    ⚠️ 丙案(字符串引用资产名)**已否决**:ADR-015 §三 的逻辑层零浮点 / 零字符串引用。
//
// ⚠️ **`type` 是裸 `int`,本文件不建闭集** —— `PoiType` 的具体取值归 **6 的 GDD**
//    (承 ADR-021 §③ 的纪律:*骨架先行,载荷归系统 ADR*)。在工具里凭猜造一个枚举,
//    会在 6 的 GDD 落地时变成第二个真源。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // WorldPos

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>一条 POI **定义**(整数格 + 类型 + 守卫档位)。</summary>
    /// <remarks>
    /// <para>⚠️ 本结构体**只承载定义**,不承载状态。`PoiState`(未发现 / 已发现 / 已解决)
    /// 住世界流,唯一写者是系统 6(ADR-021)。</para>
    /// </remarks>
    public readonly struct PoiSpec
    {
        /// <summary>POI id(≥ 0;`-1` 是「本格无 POI」的哨兵,不得作为定义 id)。</summary>
        public readonly int PoiId;

        /// <summary>所在整数格。</summary>
        public readonly WorldPos Cell;

        /// <summary>POI 类型(裸 `int`;取值闭集归 6 的 GDD —— 见文件头)。</summary>
        public readonly int Type;

        /// <summary>守卫强度**档位**(整数;`0` = 无守卫。具体敌人由 52 抽池时决定)。</summary>
        public readonly int GuardRef;

        /// <summary>构造一条 POI 定义。</summary>
        public PoiSpec(int poiId, WorldPos cell, int type, int guardRef)
        {
            PoiId = poiId; Cell = cell; Type = type; GuardRef = guardRef;
        }
    }

    /// <summary>POI **定义**集合(声明式源;`null` = 本关卡未声明)。</summary>
    /// <remarks>
    /// <para>构造即校验(作者错误当场抛,不留到导出):`poi_id` 唯一且 ≥ 0 · 同格不得两条 ·
    /// 格坐标须落在关卡声明范围内(范围由 <see cref="ValidateAgainstLevel"/> 在导出期给出 ——
    /// 构造时本类不知道关卡尺寸,不臆造)。</para>
    /// </remarks>
    public sealed class PoiSource
    {
        /// <summary>POI 定义集(声明序;导出时按 `poi_id` 升序重排)。</summary>
        public readonly IReadOnlyList<PoiSpec> Pois;

        /// <summary>构造 POI 定义集。</summary>
        /// <exception cref="ArgumentException">`poi_id` 为负 / 重复 / 同格两条。</exception>
        public PoiSource(IReadOnlyList<PoiSpec> pois)
        {
            Pois = pois ?? throw new ArgumentNullException(nameof(pois));

            var seenIds = new HashSet<int>();
            var seenCells = new HashSet<(int, int, int)>();

            for (int i = 0; i < pois.Count; i++)
            {
                PoiSpec p = pois[i];
                if (p.PoiId < 0)
                    throw new ArgumentException(
                        $"pois[{i}] 的 poi_id = {p.PoiId} —— 须 ≥ 0。" +
                        "`-1` 是「本格无 POI」的哨兵(`GetPoiId` 的返回值);拿它当定义 id " +
                        "会让「无 POI」与「id 为 -1 的 POI」不可分", nameof(pois));

                if (!seenIds.Add(p.PoiId))
                    throw new ArgumentException(
                        $"poi_id {p.PoiId} 重复(pois[{i}])—— 定义集里 id 是主键;" +
                        "重复会让状态机的 id 集静默去重、而两条定义各自不同",
                        nameof(pois));

                // 同格两条 POI ⇒「这一格是哪一条」成为隐式规则(与 world_buildslots 的重复格检查同型)。
                if (!seenCells.Add((p.Cell.X, p.Cell.Y, p.Cell.Z)))
                    throw new ArgumentException(
                        $"格 ({p.Cell.X},{p.Cell.Y},{p.Cell.Z}) 声明了两条 POI(pois[{i}])—— " +
                        "一格一条是硬约束;否则「取哪一条」会成为没人写下来的隐式规则",
                        nameof(pois));
            }
        }

        /// <summary>按 `poi_id` 升序返回(导出 / 烘焙共用的**确定序**)。</summary>
        /// <remarks>行序由**数据**定,不由作者书写顺序定 —— cooked 的行序是产物的一部分。</remarks>
        public List<PoiSpec> SortedById()
        {
            var copy = new List<PoiSpec>(Pois);
            copy.Sort((a, b) => a.PoiId.CompareTo(b.PoiId));
            return copy;
        }

        /// <summary>全部 `poi_id`(升序;供装配期给 `PoiStateMachine` 的初始 id 集)。</summary>
        public List<int> AllIds()
        {
            List<PoiSpec> sorted = SortedById();
            var ids = new List<int>(sorted.Count);
            for (int i = 0; i < sorted.Count; i++) ids.Add(sorted[i].PoiId);
            return ids;
        }

        /// <summary>校验每条定义的格落在关卡声明范围内(导出期调用)。</summary>
        /// <exception cref="InvalidOperationException">任一格越界。</exception>
        public PoiSource ValidateAgainstLevel(int width, int height, int depth)
        {
            var bad = new List<string>();
            for (int i = 0; i < Pois.Count; i++)
            {
                PoiSpec p = Pois[i];
                if (p.Cell.X < 0 || p.Cell.X >= width ||
                    p.Cell.Y < 0 || p.Cell.Y >= height ||
                    p.Cell.Z < 0 || p.Cell.Z >= depth)
                {
                    bad.Add($"pois[{i}] poi_id={p.PoiId} 的格 ({p.Cell.X},{p.Cell.Y},{p.Cell.Z}) " +
                            $"超出 {width}×{height}×{depth}");
                }
            }
            if (bad.Count > 0)
                throw new InvalidOperationException(
                    "[关卡工具] POI 定义越界:" + string.Join(";", bad));
            return this;
        }
    }
}
