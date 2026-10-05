// ADR 权威来源:
//   GDD design/gdd/diagnosis-system.md —— R-8.1(词条七字段 schema)/ R-8.2(P0 冻结清单)/
//     规则二/三(读数入口与三级结构)/ C-6(neg_weight 仅阴性非空且 >0)/ C-7(阴性同锚)/
//     D-8-9(SLOT_BOUNDS ⊂ DIAG_TIERS 双向耦合)/ F-8.2(空白档回退 —— 数据可表达即可,读侧归 story 003)
//   ADR-014 §三(阶段 2 per-schema 绑定 + 白名单/闭集校验;`diagnosis_signs.json` → `*.cooked`)
//   ADR-026(DIAG_TIERS 数值归 30 层、义归 8 —— 本表消费 tier_named 语义)
//   ADR-009(通道位与体征 id 的真源在 9 的 R1 注册表,8 是外键方)
//
// ⚠️ 本文件 = R-8.1 / AC-8-32…35 的**校验真身**(story 002 的落点)。
//
// ⚠️ **文件族命名**(结构侧评审 S-7):`DiagnosisSignTable.cs` 是**文件名**,**无同名 C#
//   类型** —— 本文件承载 SignChannel / RevealMethod / SignPolarity / SignLexemeRow /
//   DiagnosisTuning / DiagnosisSignTableValidator 六个类型;读者按文件名找
//   `class DiagnosisSignTable` 会扑空,按类型名找文件亦然。
//
//   两条铁律:
//   ① **构建期硬失败**(`throw`),不是运行期告警 —— 违例进不了出货包(ADR-014 阶段 2 位点);
//   ② 每条校验有**独立判据**(各注入一个违例夹具 ⇒ 各红),禁折成一个「大 if」。
//
// ⚠️ 住前缀命名空间 `…Presentation.Diagnosis`(DiagnosisBoundaryGates 扫描键)——
//   本文件受 D-TREF / D-PERSIST / D-FIX / D-CLK / D-11 源层谓词与 IL 谓词约束:
//   · **不构造 `Fix`、不写 `FixParse`**(D-FIX;`neg_weight` 以 raw `long?` 承载,
//     Fix/float 换算出口延至 story 004 —— 见 story-002 Completion Notes);
//   · **无 `System.IO` / 持久化 API / 时钟 / RNG**(D-PERSIST · D-CLK);
//   · 字段名零累积量语义 token(AC-8-3 反射扫描)、零 double(G-4)。
//
// ⚠️ `DiagnosisTuning.SlotBounds` 在本故事**首次定义**(D-8-9 的 8 侧一半),
//   由 <see cref="DiagnosisSignTableValidator.AssertSlotBoundsCoupled"/> 与 30 的
//   `SkillTuningTable.GetDiagTiers()` 做构建期双向耦合断言(TR-diag-014)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts.SkillSystem;

namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    /// <summary>体征所属通道(R-8.1 <c>channel</c>)。
    /// <para>前五 = GDD 五通道(面色/语声/姿态/呼吸/触感,恰好五个值,无新造 —— AC-8-32);
    /// <see cref="History"/> = 病史组 <c>问诊(非体格通道)</c>(通道外第二类证据,
    /// GDD 2026-09-14 用户裁定;AC-8-34 明写「该值不计为新造通道」,且仅白名单给
    /// <c>sign_purulent_stool</c>)。</para>
    /// <para>P1a 通道(舌/脉/情志/体质/时序)刻意**不在枚举内** —— 绑定期即拒(AC-8-33)。</para>
    /// <para>⚠️⚠️ **同名不同物**(结构侧评审 S-MAJOR):<c>Sim.Contracts.SignChannel</c> 是
    /// AC-21 的体征通道**位掩码静态类**(<c>Complexion=1&lt;&lt;0</c> …),与本枚举的**序数标签**
    /// 同名但完全不同物 —— 禁把本枚举序数当掩码位用、禁相互 cast;序数 ↔ 掩码位的**映射表 +
    /// 构建期双向断言**归 story 003 登记(消费前必读)。</para></summary>
    public enum SignChannel : byte
    {
        /// <summary>面色(视诊所得)。</summary>
        FaceColor = 0,

        /// <summary>语声(一般检查,非「闻诊」—— GDD 语声组注)。</summary>
        Voice = 1,

        /// <summary>姿态(整体体态 + 颈项)。</summary>
        Posture = 2,

        /// <summary>呼吸(节奏 + 听叩所见)。</summary>
        Breathing = 3,

        /// <summary>触感(触诊 + 叩诊手感)。</summary>
        Touch = 4,

        /// <summary>病史(<c>问诊(非体格通道)</c> —— 仅 <c>sign_purulent_stool</c>)。</summary>
        History = 5,
    }

    /// <summary>揭示手段(R-8.1 <c>reveal_by</c>)—— P0 五法:视/触/叩/听/问。
    /// <para>P1a 手段(望/闻/切)刻意**不在枚举内** —— 绑定期即拒(AC-8-33)。</para></summary>
    public enum RevealMethod : byte
    {
        /// <summary>视诊。</summary>
        Inspection = 0,

        /// <summary>触诊。</summary>
        Palpation = 1,

        /// <summary>叩诊。</summary>
        Percussion = 2,

        /// <summary>听诊(听诊器)—— 与「听语声」不同(GDD 语声组注)。</summary>
        Auscultation = 3,

        /// <summary>问诊(P0 手段;语声组正式评估 / 病史直接取得)。</summary>
        Inquiry = 4,
    }

    /// <summary>词条极性(R-8.1 <c>polarity</c>):阳性体征 / 阴性体征。</summary>
    public enum SignPolarity : byte
    {
        /// <summary>阳性体征(「有」;无 <c>neg_weight</c>)。</summary>
        Positive = 0,

        /// <summary>阴性体征(「没有」也要本钱 —— 构成排除;须带 <c>neg_weight</c> > 0)。</summary>
        Negative = 1,
    }

    /// <summary>
    /// 单条体征词条行(R-8.1 七字段的烘焙形态;<c>diagnosis_signs.json</c> 的一行)。
    /// <para>字段与 GDD R-8.1 逐字对应;医学身份已过冻结轮 —— 本类型只签**形状 + 校验**。</para>
    /// <para>⚠️ <see cref="NegWeightRaw"/> 存 <b>raw Q16.16 long</b>(非 <c>Fix</c>):前缀内
    /// 禁止构造 <c>Fix</c>(D-FIX IL 谓词含 <c>.ctor</c>),Fix/float 换算出口延至 story 004。</para>
    /// </summary>
    public readonly struct SignLexemeRow
    {
        /// <summary>R-8.1 主键(对 9 的 R1 注册表 <c>signs[]</c> 做外键闭合 —— AC-8-34)。</summary>
        public readonly string SignId;

        /// <summary>三档呈现词(粗/中/细;数组序 = slot 序)。空档 = <c>null</c>
        /// (F-8.2 空白档回退的可表达性;**禁空串** —— 空串与阴性形态在呈现层同构)。</summary>
        public readonly string[] DisplayWords;

        /// <summary>所属通道(五通道 + 病史例外;AC-8-32/8-34)。</summary>
        public readonly SignChannel Channel;

        /// <summary>揭示手段(P0 五法闭集,可多法合证;AC-8-32/8-33)。</summary>
        public readonly RevealMethod[] RevealBy;

        /// <summary>命名门槛 <c>tier_named</c> ∈ {1} ∪ SLOT_BOUNDS(≠35,无中间值;AC-8-32)。</summary>
        public readonly int TierNamed;

        /// <summary>极性(必填;AC-8-32)。</summary>
        public readonly SignPolarity Polarity;

        /// <summary>阴性把握度权重 raw Q16.16(仅阴性非空且 &gt;0 —— C-6;阳性误填即拒)。
        /// <para>⚠️ 数值 = 占位(GDD 原值 <c>*待裁*</c>,归用户数值轮);本故事只签「&gt;0」结构约束。</para></summary>
        public readonly long? NegWeightRaw;

        /// <summary>完整构造(七字段)。</summary>
        public SignLexemeRow(
            string signId, string[] displayWords, SignChannel channel,
            RevealMethod[] revealBy, int tierNamed, SignPolarity polarity, long? negWeightRaw)
        {
            SignId = signId;
            DisplayWords = displayWords;
            Channel = channel;
            RevealBy = revealBy;
            TierNamed = tierNamed;
            Polarity = polarity;
            NegWeightRaw = negWeightRaw;
        }
    }

    /// <summary>
    /// 8 侧全局常量(结构前提的 8 侧一半)。
    /// <para><b>首次定义</b>(story 002 · D-8-9 的 SLOT_BOUNDS 侧):精度档槽边界
    /// (粗/中/细/满 的分界,<c>35</c> 检验线不参与槽划分故不在此列)。</para>
    /// <para>⚠️ 只存**结构值**;曲线系数等数值归数值轮(GDD Tuning)。</para>
    /// </summary>
    public static class DiagnosisTuning
    {
        /// <summary>SLOT_BOUNDS 存储(私有;对外只暴露 <see cref="SlotBounds"/> 只读视图)。</summary>
        private static readonly int[] SlotBoundsStorage = { 10, 20, 50 };

        /// <summary>SLOT_BOUNDS = (10, 20, 50) —— F-8.2 精度档槽边界(3 槽)。
        /// <para>与 30 的 <c>DIAG_TIERS (10/20/35/50)</c> 双向耦合(D-8-9 / TR-diag-014),
        /// 由 <see cref="DiagnosisSignTableValidator.Validate"/> 构建期断言。</para>
        /// <para>⚠️ 只读视图,底层数组不外泄(结构侧评审 S-5:public 可变数组 = 调用方可
        /// 运行期改槽边界,绕过一切耦合断言 —— 静默分叉的后门)。</para></summary>
        public static IReadOnlyList<int> SlotBounds => SlotBoundsStorage;
    }

    /// <summary>
    /// 词条表的**构建期校验器**(R-8.1 / AC-8-32…35 / C-6 / D-8-9;ADR-014 阶段 2 位点)。
    /// <para>每条规则独立判据,违则 <c>throw</c>(构建期硬失败 —— 违例进不了出货包)。</para>
    /// <para><b>唯一调用点</b> = <c>DiagnosisSignBinder.Bind</c>(Editor 侧;删它 ⇒ 违例夹具静默烘出)。</para>
    /// <para><b>值归数值轮</b>:neg_weight 具体数值 / tier 阈值语义不在本类签,只签结构约束。</para>
    /// </summary>
    public static class DiagnosisSignTableValidator
    {
        /// <summary>跑全部构建期校验(烘焙期一次调用)。任一违例即 `throw`。
        /// <b>先跑耦合断言</b>(SLOT_BOUNDS 漂移 ⇒ 后续 tier 域全部无意义)。</summary>
        /// <param name="rows">校验通过绑定期的行集。</param>
        public static void Validate(IReadOnlyList<SignLexemeRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            // TR-diag-014 / D-8-9:SLOT_BOUNDS ⊂ DIAG_TIERS 双向耦合(构建期)
            AssertSlotBoundsCoupled(SkillTuningTable.Default.GetDiagTiers(), DiagnosisTuning.SlotBounds);

            // story-003 Note 6 / 承 S-MAJOR:通道序数 ↔ 位掩码映射的构建期双向断言
            // (五通道恰满 · 零重复位 · 零悬空 · Wound/History 两处不全等显式登记)
            DiagnosisChannelMaskMap.AssertConsistent();

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var allowedTiers = BuildTierDomain();

            foreach (SignLexemeRow row in rows)
            {
                // ── AC-8-32 · tier_named ∈ {1} ∪ SLOT_BOUNDS(35 / 中间值即拒)──
                if (!allowedTiers.Contains(row.TierNamed))
                    throw new ArgumentException(
                        $"AC-8-32·tier_named={row.TierNamed} ∉ {string.Join(",", allowedTiers)}" +
                        $"(35 = 检验线不参与槽划分;中间值无槽边界语义)—— sign_id={row.SignId}");

                // ── AC-8-33 · lab_* 不在 P0 表(检验 = P1a)──
                if (row.SignId != null &&
                    row.SignId.StartsWith("lab_", StringComparison.Ordinal))
                    throw new ArgumentException(
                        $"AC-8-33·lab 「{row.SignId}」以 lab_ 开头 —— 检验是 P1a 手段,禁入 P0 词表");

                // ── AC-8-34 · 病史通道白名单(通道外第二类证据仅此一行)──
                if (row.Channel == SignChannel.History && row.SignId != "sign_purulent_stool")
                    throw new ArgumentException(
                        $"AC-8-34·channel-exception channel=问诊(非体格通道) 仅白名单给 " +
                        $"sign_purulent_stool —— 「{row.SignId}」不得借用该通道(否则新造通道)");

                // ── R-8.1 · sign_id 两两互异(主键)──
                if (row.SignId == null || !seen.Add(row.SignId))
                    throw new ArgumentException(
                        $"R-8.1·sign_id 「{row.SignId}」缺失或重复 —— 主键必须唯一");

                // ── C-6 / AC-8-32 · neg_weight 仅阴性非空且 >0(阳性误填即拒)──
                if (row.Polarity == SignPolarity.Positive && row.NegWeightRaw.HasValue)
                    throw new ArgumentException(
                        $"AC-8-32·neg_weight 阳性条目不得携带 neg_weight" +
                        $"(实得 raw={row.NegWeightRaw.Value})—— sign_id={row.SignId}");
                if (row.Polarity == SignPolarity.Negative &&
                    (!row.NegWeightRaw.HasValue || row.NegWeightRaw.Value <= 0))
                    throw new ArgumentException(
                        $"AC-8-32·neg_weight 阴性条目须非空且 > 0(C-6;把握度公式除零/反号即此" +
                        $"口)—— sign_id={row.SignId} raw={row.NegWeightRaw?.ToString() ?? "null"}");
            }
        }

        /// <summary>
        /// <b>TR-diag-014 / D-8-9</b> —— <c>SLOT_BOUNDS ⊂ DIAG_TIERS</c> 双向耦合(纯函数)。
        /// <para>① 每个槽边界 ∈ 档位表(子集);② 档位表去 35 后与槽边界集**相等**
        /// (双向 —— 单向子集在「档位表多出 21 而槽边界仍旧」时静默通过)。
        /// <para><c>35</c> = 检验线(P1a),8 侧刻意排除 —— GDD 明写「不进枚举」。</para>
        /// </summary>
        /// <param name="diagTiers">30 的 <c>DIAG_TIERS</c>(如 {10,20,35,50})。</param>
        /// <param name="slotBounds">本侧 <see cref="DiagnosisTuning.SlotBounds"/>({10,20,50})。</param>
        public static void AssertSlotBoundsCoupled(
            IReadOnlyList<int> diagTiers, IReadOnlyList<int> slotBounds)
        {
            if (diagTiers == null) throw new ArgumentNullException(nameof(diagTiers));
            if (slotBounds == null) throw new ArgumentNullException(nameof(slotBounds));

            var tiers = new HashSet<int>(diagTiers);

            // ① 每槽 ∈ 档位表
            foreach (int slot in slotBounds)
                if (!tiers.Contains(slot))
                    throw new ArgumentException(
                        $"TR-diag-014 SLOT_BOUNDS={slot} ∉ DIAG_TIERS {{{string.Join(",", diagTiers)}}}" +
                        "(D-8-9:槽边界须是档位表的子集 —— 漂移 ⇒ 档位判定与槽划分分叉)");

            // ② 档位表去 35 后 == 槽边界集(双向)
            var tiersNoLab = new HashSet<int>();
            foreach (int t in diagTiers)
                if (t != 35) tiersNoLab.Add(t);

            if (tiersNoLab.Count != slotBounds.Count)
                throw new ArgumentException(
                    $"TR-diag-014 DIAG_TIERS 去 35 后 {{{string.Join(",", tiersNoLab)}}} 与 " +
                    $"SLOT_BOUNDS {{{string.Join(",", slotBounds)}}} 不等 —— 双向耦合(35 不参与槽划分)");

            foreach (int t in tiersNoLab)
                if (!Contains(slotBounds, t))
                    throw new ArgumentException(
                        $"TR-diag-014 DIAG_TIERS 含 {t} 而 SLOT_BOUNDS 未登记 —— " +
                        "单向子集会在此类漂移下静默通过(D-8-9 双向的必要性)");
        }

        /// <summary>
        /// <b>AC-8-34 · 正向外键闭合</b> —— 9 的 R1 注册表 <c>signs[]</c> 每项须在本表找得到(纯函数)。
        /// <para>⚠️ <b>不在生产路径调用</b>(本故事无 9 侧数据 —— R1 注册表数据归 disease story 004)。
        /// 生产接线后由该侧在装载期调用;本方法先立判据,负夹具证其红路径真实。</para>
        /// <para>反向孤儿子句 = BLOCKED-BY-disease-story-006(TR-diag-013 no-adr-by-design),
        /// 见 <c>sign_table_test.cs</c> 头注。</para>
        /// </summary>
        public static void ValidateForwardClosure(
            IReadOnlyList<SignLexemeRow> rows, IEnumerable<string> referencedSignIds)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (referencedSignIds == null) throw new ArgumentNullException(nameof(referencedSignIds));

            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (SignLexemeRow row in rows)
                if (row.SignId != null) present.Add(row.SignId);

            foreach (string id in referencedSignIds)
                if (!present.Contains(id))
                    throw new ArgumentException(
                        $"AC-8-34·fk 9 的 signs[] 引用「{id}」在 R-8.2 词表中不存在 —— " +
                        "外键悬空(9 引用却无登记)");
        }

        // ── 内部:tier 域 = {1} ∪ SLOT_BOUNDS(数值取自两处真源,不写字面量)──

        private static HashSet<int> BuildTierDomain()
        {
            var domain = new HashSet<int> { 1 };
            foreach (int s in DiagnosisTuning.SlotBounds) domain.Add(s);
            return domain;
        }

        private static bool Contains(IReadOnlyList<int> list, int value)
        {
            foreach (int v in list)
                if (v == value) return true;
            return false;
        }
    }
}
