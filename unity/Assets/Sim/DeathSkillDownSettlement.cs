// S23-3 · story-003 —— 死亡掉级的**结算面**(29 侧编排;2026-11-01)
//
// ── 权威式与唯一出处(AC-29-14 / AC-29-05)──────────────────────────────────────
// 掉级公式 = F-29-1(`death-and-respawn.md:268-291`)**整数截断除法**
// `(Level × 19) / 20`,唯一出处 = 30 的 `DeathPenalty.Apply`(Sim.Contracts/SkillSystem)
// —— 本类**只编排时机**(读等级 → 逐技能求值 → 经 30 的既有成长面发 `SkillGrown`),
// **零公式重复实现**(检索断言钉住:本文件源码无 `* 19` / `/ 20` 字面算式)。
//
// ⚠️ **2026-11-01 订正(本类存在的直接原因)**:30 侧 `DeathPenalty.Apply` 原实现是
//   Fix 路径(`floor_Fix(Mul)`),与 F-29-1 表值差一(`20→18` 而非 `19`)—— 这是
//   death-and-respawn GDD 已登记的「同源同错」挂账义务。S23-3 兑付其**代码半边**
//   (Apply 本体改整数截断;`skill-system.md` §4.3 公式行文本订正仍挂 30 下一轮)。
//   ⇒ 本类照卡面原计划消费 30 的 Apply —— AC-29-14「零重复实现」在**订正后的**
//   权威面上成立,不是在旧 Fix 面上。
//
// ── 掉级的事件表达(F-29-4 · 不落新 Kind)────────────────────────────────────
// 掉级 = `SkillGrown` 事件携带**更低的绝对等级**(Level 语义:「升级时 = 新等级」,
// 掉级 = 同一信道的负向移动;`SkillStateRebuilder.RebuildLevels` 只认最新绝对等级
// ⇒ 重放/读档后等级逐位重建)。**零「掉级 Kind」Append 点** —— 本类不触
// `IEventSink`,发射经 30 的既有成长面 `IGrowthEmitter`(17 采集同款适配器),
// 编码走 ADR-029 唯一路径(适配器内)。
//
// ── 逐技能语义 ────────────────────────────────────────────────────────────────
// · 遍历域 = 等级表里**该玩家已有成长记录**的技能(查无 = 0 级;0 → 0 无变化);
// · `DeathPenalty.Apply(0) == 0` 与「无变化不发」双保险:**零变化零事件**
//   (与 S23-2 空背包零事件同纪律 —— 死亡不该给每个 0 级技能刷一条恒等事件);
// · 排序 = skillId 升序(显式排序,不用字典迭代序 —— 确定性纪律);
// · 载荷:xpGain = Fix.Zero(死亡不得经掉级净得经验;EmitGrowth 校验非负 ✓)、
//   objectId = 0(无对象语境;RebuildLevels 不读该字段)、novelty = Normal、
//   patientId = PatientId.None(玩家技能无病例语境,ADR-007 哨兵不污染高水位)。
//
// ── 判断类豁免(R-4-W-6 口径待裁)────────────────────────────────────────────
// F-29-1 的作用域是「∀ s ∈ 技能集 ∖ 判断类」,但**豁免口径未裁**(R-4-W-6,
// 2026-09-20 起)—— 30 §4.3 无此豁免。⇒ 本类收**可注入豁免谓词**,生产装配传
// **null = 全掉**(与 30 §4.3 现行为一致);「全掉 / 全不掉」两极端由双分支夹具
// 登记行为差异,**不实现选择**(story-003 AC 行)。
//
// ── 等级来源(派生态,每次重 fold)────────────────────────────────────────────
// 与 S23-2 投影同格:等级表是派生态,由 `SkillStateRebuilder.RebuildLevels` 从
// 病史流 SkillGrown 序列重建(不持快照 —— 持快照 = 载重停在装配那一刻)。
// 流枚举序 = 追加序 = Tick 升序(RebuildLevels 的输入约定,7a 读出同序)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.Foraging;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 死亡掉级结算:对死亡玩家的每个已有成长技能,按 F-29-1 权威式
    /// (<see cref="DeathPenalty.Apply"/>)求新等级,经 30 的既有成长面
    /// (<see cref="IGrowthEmitter"/>)发一条携带新绝对等级的 <c>SkillGrown</c>。
    /// </summary>
    public sealed class DeathSkillDownSettlement
    {
        private readonly IGrowthEmitter _growth;
        private readonly Func<IEnumerable<SimEvent>> _historyOf;
        private readonly IPayloadDecoder _decoder;

        /// <summary>判断类技能豁免谓词(**口径待裁 R-4-W-6**)。
        /// <para>null = 全掉(生产装配现值 —— 与 30 §4.3 无豁免的现行为一致);
        /// 非null = 按 skillId 判豁免。两极端行为由双分支夹具登记,选择不实现。</para></summary>
        private readonly Func<int, bool> _isExempt;

        /// <summary>构造掉级结算面。</summary>
        /// <param name="growth">30 的既有成长面(30 的 `EmitGrowth` 主机侧适配;
        /// 编码 + Append 在其内 —— 本类零 Append 点,AC-29-16「零掉级 Kind」)。</param>
        /// <param name="historyOf">病史流事件来源(派生态每次重取,不持快照)。</param>
        /// <param name="decoder">载荷解码器(ADR-031;重建等级须读 SkillGrown 载荷)。</param>
        /// <param name="isExempt">判断类豁免谓词(**R-4-W-6 待裁**;null = 全掉)。</param>
        /// <exception cref="ArgumentNullException">任一必需依赖为 null。</exception>
        public DeathSkillDownSettlement(IGrowthEmitter growth,
            Func<IEnumerable<SimEvent>> historyOf, IPayloadDecoder decoder,
            Func<int, bool> isExempt = null)
        {
            _growth = growth ?? throw new ArgumentNullException(nameof(growth));
            _historyOf = historyOf ?? throw new ArgumentNullException(nameof(historyOf));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _isExempt = isExempt;
        }

        /// <summary>
        /// 死亡掉级:逐技能按权威式求新等级并发 <c>SkillGrown</c>(同 tick)。
        /// </summary>
        /// <param name="tick">死亡判定的逻辑 tick(与 `PlayerDied` 同 tick;
        /// 结算段次位 = 掉落之后、复活传送(story-005)之前)。</param>
        /// <param name="actorId">死亡玩家 id(技能等级按施予者归组)。</param>
        /// <returns>发出的降级 `SkillGrown` 条数(零技能有变化 = 0 且零事件)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">tick / actorId 为负。</exception>
        public int Settle(long tick, int actorId)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "玩家 id 走机制 A 计数器,不得为负");

            Dictionary<int, Dictionary<int, int>> levels =
                SkillStateRebuilder.RebuildLevels(GrownPayloads());

            if (!levels.TryGetValue(actorId, out Dictionary<int, int> bySkill))
                return 0; // 该玩家无任何成长记录 ⇒ 全 0 级 ⇒ 全部无变化 ⇒ 零事件

            // skillId 升序(显式排序 —— 字典迭代序不构成确定性依据)
            int[] skillIds = new int[bySkill.Count];
            bySkill.Keys.CopyTo(skillIds, 0);
            Array.Sort(skillIds);

            int emitted = 0;
            for (int i = 0; i < skillIds.Length; i++)
            {
                int skillId = skillIds[i];

                // 判断类豁免 —— ⚠️ **口径待裁(R-4-W-6,2026-09-20 起)**:F-29-1 的
                // 「∖ 判断类」取哪一集合(诊断/辨证,还是含其它)未裁;30 §4.3 无此豁免。
                // 生产装配传 null = 全掉;两极端行为由双分支夹具登记,此处不实现选择。
                if (_isExempt != null && _isExempt(skillId))
                    continue;

                int current = bySkill[skillId];
                int downgraded = DeathPenalty.Apply(current);   // ← 唯一出处(AC-29-14)
                if (downgraded == current)
                    continue; // 零变化零事件(0 级恒等;与 S23-2 空背包同纪律)

                _growth.Emit(tick, actorId, skillId, ObjectIdNone,
                    NoveltyClass.Normal, downgraded, Fix.Zero);
                emitted++;
            }
            return emitted;
        }

        /// <summary>「无对象语境」的载荷占位(EmitGrowth 校验 ≥ 0;等级重建不读该字段)。</summary>
        private const int ObjectIdNone = 0;

        /// <summary>从病史流解码 SkillGrown 载荷(只滤 Kind,流枚举序 = Tick 升序)。</summary>
        private IEnumerable<SkillGrownPayload> GrownPayloads()
        {
            foreach (SimEvent e in _historyOf())
            {
                if (e.Kind != EventKind.SkillGrown)
                    continue;
                yield return _decoder.Decode<SkillGrownPayload>(e.Kind, e.Payload);
            }
        }
    }
}
