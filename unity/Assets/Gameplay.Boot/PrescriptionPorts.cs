// sprint-06 G3.2 · 21a/20/30 最小生产端口实装(2026-10-11)
//
// 权威来源:
//   production/sprints/sprint-06.md §2 G3(2026-10-11 用户裁定 —— 「最小生产端口实装」
//   替换「结构性半边」降级预案)· 21a `item_database.md` §Schema B(drug_profile)
//   · 20 `inventory-and-items.md`(库存扣减面 IPortionsStore)· 30 `skill-system.md`
//   · ADR-029(IPayloadEncoder 唯一编码路径)· ADR-026(SkillGrown 落病史流)
//
// ⚠️ 本件 = 组合根装配层的**最小端口实装**(住 Gameplay.Boot,可同时见 Sim.Codec)。
//   · 21a:内联最小 drug 表(salicylic_acid —— 数值轮未开,取既有作者态字面:
//     potency 1/2 · half_life 2 · 档位 = 装配层注记)。**刻意不实现**烘焙装载面
//     (AddressablesDataProvider 新数据集 / DrugProfileCodec)—— 完整 21a 独立排期。
//   · 20:最小库存面(内存字典持量 + 扣减;`HasPortions`/`ConsumePortions` 真行为)。
//     **刻意不实现**库存账本 / 世界流 DropConsumed 消耗写者 —— 完整 20 独立排期。
//   · 30:装配层把 `SkillGrownEmitter`(Sim.Contracts)发出的载荷**编码 + Append 落病史流**
//     —— 生产写者(G3 起 SkillGrown 不再挂 b7 豁免)。
//
// ⚠️ 完整 21a/20 落地后**只换端口实现**,装配签名零改动(用户裁定注记)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.Prescription;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// G3.2 · 21a 最小 drug 表(装配层内联)。为 <see cref="IPortionsConversion"/> 提供
    /// 换算表(<c>portions_per_dose</c>),并为戥子 / 处方流程提供最小药物档案集。
    /// </summary>
    public sealed class MinimalDrugCatalog : IPortionsConversion
    {
        /// <summary>本表唯一药(21a 作者态已登记;数值轮未开的接线载体)。</summary>
        public static readonly ItemKey DrugKey =
            new ItemKey("salicylic_acid", ProcessingState.Extracted);

        /// <summary>每剂份数(21a 最小表:1 —— 全剂量恒消耗 1 份)。</summary>
        public int PortionsPerDose(ItemKey itemKey) => 1;

        /// <summary>最小药物档案(戥子档位集 = DoseRange(1..5),五档 —— 判据「≥2 档不同 dose」的可判面)。
        /// <para>档位值 = 装配层注记(作者态表 dose_range 为空 = 整剂,数值轮后由真表覆盖)。</para>
        /// <para>⚠️ **Potency 的量纲契约**(既有 test :947 同款):`DoseCalculator.Calculate` 的
        /// 公式 = <c>potency × dose / DOSE_BASE</c>,分母把 Q16.16 归一 ⇒ **载荷 DrugPotency
        /// = Q16.16 的 dose_potency**。故药效「1.0」的作者态字面 = <c>1 × DOSE_BASE × Q16.16</c>
        /// = <c>65536 × 65536</c>(既有测试写 <c>65536L * DoseBase</c>);本表 1/2 强度 =
        /// <c>1/2 × DOSE_BASE × Q16.16 = 2^31</c> ⇒ 档 2 ⇒ <c>2^31×2/65536 = 65536</c>(= 1.0 Q16.16)。</para></summary>
        public static DrugProfile DrugProfileFor(ItemKey itemKey)
        {
            if (itemKey != DrugKey)
                throw new KeyNotFoundException(
                    $"装配层最小 drug 表无此药:{itemKey}(本表只含 salicylic_acid;真表落地后换实现)。");
            return new DrugProfile
            {
                DoseRange = new DoseRange(1, 5),
                // ⚠️ 量纲:Q16.16 的 1.0 = 65536;作者态「1/2 强度」 = 2^31(Q16.16),
                //   F-11.1 分母 DOSE_BASE 归一 ⇒ 档 2 得 dose_potency = 1.0(Q16.16 = 65536)。
                DrugPotency = new Fix(1L << 31),            // 1/2 强度(Q16.16:2^31);见量纲契约注
                HalfLife = new Fix(2L * 65536L),            // 2(Q16.16;作者态字面 half_life 2)
                AxisOffsetByQuality = new[] { new Fix(0L), new Fix(0L), new Fix(0L), new Fix(0L), new Fix(0L) },
            };
        }
    }

    /// <summary>20 最小库存面(内存字典;`HasPortions` / `ConsumePortions` 真行为)。
    /// <para>⚠️ 只持**持有量映射**与扣减,不建实例账本 / 不产世界流消耗 —— 完整 20 独立排期。</para></summary>
    public sealed class MinimalPortionsStore : IPortionsStore
    {
        private readonly Dictionary<int, Dictionary<ItemKey, int>> _holdings =
            new Dictionary<int, Dictionary<ItemKey, int>>();

        /// <summary>给玩家注入持有量(测试 / 装配种子面)。</summary>
        public void Grant(int playerId, ItemKey itemKey, int portions)
        {
            if (portions < 0)
                throw new ArgumentOutOfRangeException(nameof(portions));
            if (!_holdings.TryGetValue(playerId, out var row))
                _holdings[playerId] = row = new Dictionary<ItemKey, int>();
            row.TryGetValue(itemKey, out int cur);
            row[itemKey] = cur + portions;
        }

        /// <inheritdoc />
        public bool HasPortions(int playerId, ItemKey itemKey, int portions)
        {
            if (portions < 0)
                throw new ArgumentOutOfRangeException(nameof(portions));
            return _holdings.TryGetValue(playerId, out var row)
                && row.TryGetValue(itemKey, out int cur)
                && cur >= portions;
        }

        /// <inheritdoc />
        public int PeekLowestQuality(int playerId, ItemKey itemKey, int portions)
        {
            // 最小表无实例级品级(单一药、无品级调制):portions>0 ⇒ 档 1;0 ⇒ 无品级哨兵。
            if (portions == 0) return PrescribeFlow.NoQuality;
            return 1;
        }

        /// <inheritdoc />
        public bool ConsumePortions(int playerId, ItemKey itemKey, int portions)
        {
            if (portions < 0)
                throw new ArgumentOutOfRangeException(nameof(portions));
            if (!HasPortions(playerId, itemKey, portions))
                return false;
            _holdings[playerId][itemKey] -= portions;
            return true;
        }
    }

    /// <summary>
    /// 30 的最小**装配端口**(等级查询 + 成长入口)。
    /// <para>成长入口 = 把 <see cref="SkillGrownEmitter.EmitGrowth"/> 产出的
    /// <see cref="SkillGrownPayload"/> 经 <see cref="IPayloadEncoder"/> 编码 + Append 落病史流
    /// —— 生产写者(ADR-026 / ADR-024 registry author=30)。</para>
    /// <para>⚠️ 等级查询 P0 只读:本件持一个固定等级(接线载体;真等级归 30 实装)。
    /// 等级校验(QueryLevel ≥ 处方 UnlockLevel)交给调用方 —— 本端口只回读。</para>
    /// </summary>
    public sealed class AssemblySkillGrowthPort : ISkillGrowthPort
    {
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;
        private readonly int _prescriptionLevel;

        /// <param name="sink">主机事件写入通道(唯一 Append 落点)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 唯一路径)。</param>
        /// <param name="prescriptionLevel">处方用药等级(P0 接线载体;数值轮后读 30 真等级)。</param>
        public AssemblySkillGrowthPort(IEventSink sink, IPayloadEncoder encoder, int prescriptionLevel)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _prescriptionLevel = prescriptionLevel;
        }

        /// <inheritdoc />
        public int QueryLevel(int actorId, int skillId)
            => skillId == PrescribeFlow.PrescriptionSkillId ? _prescriptionLevel : 0;

        /// <inheritdoc />
        /// <remarks>成长门(GateHit)由 PrescribeFlow 判定;本端口只在命中时被调用。
        /// 缺参 / 负 tick 等错误在 <see cref="SkillGrownEmitter"/> 内 fail-loud 抛出。</remarks>
        public void EmitGrowth(int actorId, int skillId, int objectId, int noveltyHint,
                               Fix kDifficulty, long tick, PatientId patientId)
        {
            // EmitGrowth 只构造载荷(30 明写不 Append)—— 编码 + 落流由调用方(本端口)完成。
            SkillGrownPayload payload = SkillGrownEmitter.EmitGrowth(
                actorId: actorId, skillId: skillId, objectId: objectId,
                novelty: NoveltyClass.Normal, level: null, currentTick: tick, patientId: patientId);

            var e = new SimEvent(tick, patientId, -1, EventKind.SkillGrown,
                                 _encoder.Encode(EventKind.SkillGrown, payload));
            _sink.Append(e);
        }
    }
}
