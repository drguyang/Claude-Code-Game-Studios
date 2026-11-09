// 病人抽取的**投放资格**判据(T4 · 2026-11-09)—— 技法门(硬)+ 等级门(软)
//
// 权威来源:
//   design/gdd/reviews/technique-unlock-map-2026-11-09.md §5.1 / §5.2(用户 2026-11-09 裁定)
//     §5.1  可投放 ⇔ (∃ t : t 已解锁 ∧ t ∈ treatable_by(d)) ∧ (d.min_skill_level ≤ 等级)
//     §5.2  钩子病例 = 技法未解锁时允许出现,但须标记(收场 / 计数归 T5)
//   ADR-043 §D5 / §D7 —— 技法 id = 处置轴 action id;解锁查询 = 病史流折叠
//   ADR-016 §一 —— 判据的全部输入 ∈ {事件流, 版本化烘焙数据, 二者的纯函数}:
//     本类是**纯函数**(解锁谓词与等级是形参,不读时钟、不读表现态)
//
// ── 为什么是「具名纯函数」而不是内联 lambda ────────────────────────────────
//   与 `EventPoolPatientSpawner` 同一个理由(见该类文件头):内联 lambda 无法被夹具
//   直接执行 ⇒ 夹具只能照抄一遍判据 ⇒ 生产改坏了夹具照样绿(自造断言顶替 AC 语义层)。
//
// ── 三态而不是两态(**本件最关键的一处判断**)──────────────────────────────
//   只看「有没有已解锁技法」会把**三种完全不同的处境**压成一个 bool:
//     ① 数据声明了,且至少一门已解锁        ⇒ 投放
//     ② 数据声明了,一门都没解锁            ⇒ **拒收**(这就是用户裁的「未开启前不出现」)
//     ③ 数据根本没覆盖到这个病种            ⇒ ?
//   ③ 若也拒收,今日的注册表会**一个病人都投放不出来**:
//     `disease_action_axis.json` 的 `treatable_by` 只覆盖 9 个 `DIS_*`,伤情 7 行
//     (含第一个病人「刺伤」)**零命中**;而 9 个 `DIS_*` 各自的技法 10/11/12 又都不在
//     起步集 `{0, 1}` 里。⇒ 严格两态会让「世界没有病人」变成**静默的设计结论**。
//   ⇒ 本件把 ③(以及「装配层拿不到轴产物」)判为**放行但计数**,理由与
//     `ITreatableByLookup` 的 null 语义同源:「未接线 / 未覆盖 ≠ 没有可用处置」。
//     ⚠️ 这不是给门开后门:② 一旦成立就**照拒**(今日 7 个病种真的被拒,见证据件)。
//     等 T7 把伤情也写进 `treatable_by`,③ 的覆盖面自然收缩为零 —— 判据不用改一行。
//
// ── 钩子病例 ──────────────────────────────────────────────────────────────
//   `asHookCase = true` ⇒ 跳过技法门与等级门,判定记 `HookCase`。**为什么连等级门也跳**:
//   钩子病例的功能是「看得懂、治不了、知道缺哪门」,等级是同一个技法内部的难易档,
//   不是另一道投放闸;若钩子还被等级卡住,它连出现都做不到,T5 的计数与收场永远跑不到。
//   ⚠️ 谁来决定「这一例是不是钩子」不归本类 —— 那是 T5(每章 1–3 例 + 三条反馈)。

using System;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Prescription;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>投放资格的判定结果(每个病种每次抽取恰一个)。</summary>
    public enum PatientDrawVerdict
    {
        /// <summary>技法门与等级门都过 ⇒ 投放。</summary>
        Admissible = 0,

        /// <summary>钩子病例(§5.2)· 技法未解锁也放行,调用方须据此打标。</summary>
        HookCase = 1,

        /// <summary>轴里没声明该病种的处置 ⇒ **数据未覆盖**,放行并计数(T7 收口)。</summary>
        Undeclared = 2,

        /// <summary>声明了处置,但**一门都没解锁** ⇒ 硬门拒收(用户裁定的核心那条)。</summary>
        NoUnlockedTechnique = 3,

        /// <summary>技法够,但 `min_skill_level &gt; 玩家等级` ⇒ 软门拒收(节奏用)。</summary>
        LevelTooLow = 4,

        /// <summary>玩家 id 尚未绑定(启动期)⇒ 无可判定对象,放行。</summary>
        PlayerNotReady = 5,

        /// <summary>装配层拿不到轴产物(`dataProvider == null`)⇒ 未接线,放行。</summary>
        NotWired = 6,
    }

    /// <summary>一次投放判定的结果 + 它为什么。</summary>
    public readonly struct PatientDrawDecision
    {
        /// <summary>判定类别。</summary>
        public readonly PatientDrawVerdict Verdict;

        /// <summary>被判定的病种 id(回带,便于日志与夹具对应)。</summary>
        public readonly int DiseaseId;

        /// <summary>构造一次判定。</summary>
        public PatientDrawDecision(PatientDrawVerdict verdict, int diseaseId)
        {
            Verdict = verdict;
            DiseaseId = diseaseId;
        }

        /// <summary>是否允许该病种进入本轮候选。</summary>
        public bool Admitted => Verdict == PatientDrawVerdict.Admissible
                             || Verdict == PatientDrawVerdict.HookCase
                             || Verdict == PatientDrawVerdict.Undeclared
                             || Verdict == PatientDrawVerdict.PlayerNotReady
                             || Verdict == PatientDrawVerdict.NotWired;

        /// <summary>是否按**钩子病例**投放(调用方据此打标;T5 消费)。</summary>
        public bool IsHookCase => Verdict == PatientDrawVerdict.HookCase;

        /// <summary>是否因「数据未覆盖」而放行(观测面;T7 收口时应归零)。</summary>
        public bool IsUndeclared => Verdict == PatientDrawVerdict.Undeclared;
    }

    /// <summary>
    /// 病种投放资格 —— **装配与夹具的单一出处**(纯函数,零状态)。
    /// </summary>
    public static class PatientDrawEligibility
    {
        /// <summary>等级门读取的技能 id = 30 的「处方用药」(GDD 见 map §5.1)。</summary>
        /// <remarks>取 <c>PrescribeFlow.PrescriptionSkillId</c> 而非另写一个字面量 ——
        /// 等级只有一个读面(承 `ISkillLevelQuery` 头注「唯一声明处」的同一纪律)。</remarks>
        public const int PrescriptionSkillId = PrescribeFlow.PrescriptionSkillId;

        /// <summary>
        /// 判定一个病种本轮能否投放。
        /// </summary>
        /// <param name="row">注册表行(非 null)。</param>
        /// <param name="index">`treatable_by` 反向索引;`null` = 未接线 ⇒ 判 <see cref="PatientDrawVerdict.NotWired"/>。</param>
        /// <param name="playerPrescriptionLevel">玩家「处方用药」等级(P0 是装配常量;真等级归 30 实装)。</param>
        /// <param name="isTechniqueUnlocked">技法解锁谓词(已绑定玩家的调用方负责闭包)。</param>
        /// <param name="playerBound">玩家 id 是否已绑定;`false` ⇒ 判 <see cref="PatientDrawVerdict.PlayerNotReady"/>。</param>
        /// <param name="asHookCase">是否按钩子病例放行(§5.2);跳过两道门并打标。</param>
        /// <exception cref="ArgumentNullException"><paramref name="row"/> 为 null。</exception>
        /// <exception cref="ArgumentNullException"><paramref name="isTechniqueUnlocked"/> 为 null。
        /// <para>⚠️ 刻意**不**在 `index == null` 时容忍 null 谓词:两个 null 都表示「未接线」,
        /// 但容忍一个、不容忍另一个会让「装配漏了谓词」表现为静默放行。让它在构造期就炸。</para></exception>
        public static PatientDrawDecision Decide(
            DiseaseRegistryEntry row,
            TechniqueDrawIndex index,
            int playerPrescriptionLevel,
            Func<int, bool> isTechniqueUnlocked,
            bool playerBound = true,
            bool asHookCase = false)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (isTechniqueUnlocked == null) throw new ArgumentNullException(nameof(isTechniqueUnlocked));

            int id = row.DiseaseId;

            // ① 钩子病例:两条门都跳(§5.2),但必须能被调用方看见「这是特例」。
            if (asHookCase) return new PatientDrawDecision(PatientDrawVerdict.HookCase, id);

            // ② 未接线 / 玩家未就绪:没有可判定的对象,不拦(与 `ITreatableByLookup` 的 null 语义同源)。
            if (index == null) return new PatientDrawDecision(PatientDrawVerdict.NotWired, id);
            if (!playerBound) return new PatientDrawDecision(PatientDrawVerdict.PlayerNotReady, id);

            // ③ 数据未覆盖 ⇒ 放行但计数。**不是**「无处置可用」——那个结论需要数据先声明。
            //    ⚠️ 作者态 `"X": []`(声明为空)在产物里**不留痕迹** ⇒ 也落进本分支
            //    (见 `TechniqueDrawIndex` 头注的数据形状缺口)。二者今日实际影响相同。
            if (!index.IsDeclared(row.DiseaseKey))
                return new PatientDrawDecision(PatientDrawVerdict.Undeclared, id);

            // ④ 技法门(硬):∃ t ∈ treatable_by(d) : t 已解锁。
            //    声明为空数组 ⇒ 空集 ⇒ 恒假 ⇒ 拒收(与「有技法但都没解锁」同判:
            //    对投放门而言两者都是「此刻一门都使不出来」)。
            System.Collections.Generic.IReadOnlyList<int> techniques = index.TechniquesOf(row.DiseaseKey);
            bool anyUnlocked = false;
            for (int i = 0; i < techniques.Count; i++)
            {
                if (isTechniqueUnlocked(techniques[i])) { anyUnlocked = true; break; }
            }
            if (!anyUnlocked) return new PatientDrawDecision(PatientDrawVerdict.NoUnlockedTechnique, id);

            // ⑤ 等级门(软):与技法门**不乘**,过了才看。
            if (row.MinSkillLevel > playerPrescriptionLevel)
                return new PatientDrawDecision(PatientDrawVerdict.LevelTooLow, id);

            return new PatientDrawDecision(PatientDrawVerdict.Admissible, id);
        }
    }
}
