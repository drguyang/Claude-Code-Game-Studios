// 权威来源:
//   GDD design/gdd/diagnosis-system.md —— §F-8.1(READ_FLOOR 曲线 · 三锚点 · C-1 硬约束)/
//     §F-8.2(SLOT_BOUNDS · TierIndex 计数式 · 空白档回退)/ 规则三(档位)/ 规则四(精度)/
//     §Edge Cases(体征客观存在但低于门槛 ⇒ 已查 · 阴性形态 · 不是未查)/
//     AC-8-5(端点与单调)· AC-8-7(G-3 整数档位)· AC-8-8(空白档回退)· AC-8-9(手段不上锁)/
//     AC-8-22(已查+读不出 ≠ 未查)· AC-8-46(掉级回升)
//   ADR-026(FixPow 唯一整数幂;8 侧等价物 = 预计算定表)· ADR-005(定点域)· ADR-006(舍入)
//   Story 003(定表读侧 + 求值器;Implementation Note 2/3/4)
//
// ⚠️ 本文件 = F-8.1 / F-8.2 的**运行期读侧**(story 003 的落点)。
//
// ⚠️ 住前缀命名空间 `…Presentation.Diagnosis`(DiagnosisBoundaryGates 扫描键)——
//   受 D-TREF / D-PERSIST / D-FIX / D-CLK / D-G1 / D-11 源层与 IL 谓词约束:
//   · **运行期零幂运算**(G-1):定表值由烘焙期算好,此处只有查表 + 整数比较;
//   · **不构造 `Fix`、不写 `FixParse`、不访问 `Fix` 成员**(D-FIX;系数以 raw `long` 回声承载,
//     运行期不参与任何定点算术);
//   · **无 `System.IO` / 持久化 / 时钟 / RNG**(D-PERSIST · D-CLK);
//   · **零 `double`**(G-4;标量一律 `System.Single`);
//   · 字段名零累积量语义 token(AC-8-3 反射扫描 —— 本类型为**只读值**,零跨调用累积量)。
//
// ⚠️ **纯函数形状**(AC-8-3):求值器静态、零字段;定表是**入参**(依赖注入),
//   8 不持任何跨调用状态。两独立进程喂同一定表 + 同一 (Skill, sign) ⇒ 逐位相同。

using System;

namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    /// <summary>精度档(§F-8.2 <c>SlotOf</c>;玩家看到的词走这条)。
    /// <para>序数 0=粗 1=中 2=细 3=满,与 `SLOT_BOUNDS` 的切点序一致
    /// (切点 10/20/50 把 <c>[0,60]</c> 分成四段;GDD §F-8.2 表)。</para></summary>
    public enum DiagnosisSlot : byte
    {
        /// <summary>粗档(<c>0 ≤ Skill &lt; 10</c>)。</summary>
        Coarse = 0,

        /// <summary>中档(<c>10 ≤ Skill &lt; 20</c>)。</summary>
        Medium = 1,

        /// <summary>细档(<c>20 ≤ Skill &lt; 50</c>)。</summary>
        Fine = 2,

        /// <summary>满档(<c>50 ≤ Skill ≤ SKILL_CAP</c>)。</summary>
        Full = 3,
    }

    /// <summary>读数状态机的输入字母表(§S-8.2;story 003 钉死字母表,转移归 story 005)。
    /// <para>GDD §S-8.2 的四态 = 待查 / 阳性 / 阴性 / 旧;本枚举另置
    /// <see cref="UnreadableNegative"/> —— 「已查 + 读不出」在**呈现上**是阴性形态
    /// (AC-8-22),但在**数据上**必须与真阴性、与待查三者两两可分(Note 4 的三值哨兵)。
    /// 故本枚举 = 「呈现在脉案上的形态」+ 「数据上的可分性」两个要求的最小并集。</para>
    /// <para>⚠️ <b>不存在「不可用 / 锁闭」态</b>(裁定⑨ · AC-8-9/AC-8-24)—— 手段永不上锁。</para></summary>
    public enum SignReadState : byte
    {
        /// <summary>待查(玩家未做该手段)—— <b>不由求值器产出</b>,由调用方给(AC-8-23 空行)。</summary>
        Blank = 0,

        /// <summary>阳性(可读 + 阳性词;浓墨有收锋,AC-8-21)。</summary>
        Positive = 1,

        /// <summary>阴性(可读 + 阴性词,如 <c>sign_lung_clear</c>;可参与排除)。</summary>
        Negative = 2,

        /// <summary>已查但读不出(值 &lt; <c>READ_FLOOR</c> 或本档无词)—— 呈**阴性形态**
        /// (AC-8-22),但 <c>可读_j = false</c>,**不得参与排除**(AC-8-17 真值半边)。</summary>
        UnreadableNegative = 3,

        /// <summary>旧(快照过期 / 9 的状态转移;转移规则归 story 005,本枚举只登记字母表)。</summary>
        Stale = 4,
    }

    /// <summary>F-8.1 定表(<c>diagnosis_read_floor.cooked.bytes</c> 的解码形)。
    /// <para>只读值:系数以 raw Q16.16 <c>long</c> 承载(运行期不做定点算术,仅供端点复核 /
    /// 跨端一致性断言),<c>Floor[0..SkillCap]</c> 为 binary32 定表值。</para>
    /// </summary>
    public readonly struct DiagnosisReadFloorTable
    {
        private readonly float[] _floor;

        /// <summary>等级上限(须 == 30 的 `SKILL_CAP`;构建期已断言,运行期只读)。</summary>
        public readonly int SkillCap;

        /// <summary>门槛上端锚 raw Q16.16(`READ_FLOOR(0)`,= <c>BASE_READ</c>)。</summary>
        public readonly long BaseReadRaw;

        /// <summary>门槛下端锚 raw Q16.16(`READ_FLOOR(SKILL_CAP)`,= <c>READ_FLOOR_MIN</c>)。</summary>
        public readonly long ReadFloorMinRaw;

        /// <summary>曲线陡度 raw Q16.16(∈ {整数, 整数+1/2})。</summary>
        public readonly long ReadGammaRaw;

        /// <summary>定表项数(= <see cref="SkillCap"/> + 1)。</summary>
        public int Count => _floor?.Length ?? 0;

        /// <summary>完整构造(codec / 测试用)。</summary>
        /// <exception cref="ArgumentException">表长 ≠ <paramref name="skillCap"/> + 1。</exception>
        public DiagnosisReadFloorTable(
            int skillCap, long baseReadRaw, long readFloorMinRaw, long readGammaRaw, float[] floor)
        {
            if (floor == null) throw new ArgumentNullException(nameof(floor));
            if (floor.Length != skillCap + 1)
                throw new ArgumentException(
                    $"定表长度 {floor.Length} ≠ skill_cap({skillCap}) + 1 —— 覆盖域须为 [0, SKILL_CAP]");
            SkillCap = skillCap;
            BaseReadRaw = baseReadRaw;
            ReadFloorMinRaw = readFloorMinRaw;
            ReadGammaRaw = readGammaRaw;
            _floor = floor;
        }

        /// <summary>查定表(`Skill` 越界即钳位 —— §F-8.1 <c>s = clamp(Skill,0,SKILL_CAP)/SKILL_CAP</c>)。</summary>
        /// <param name="skill">诊断熟练度(整数等级)。</param>
        /// <returns>该等级的 <c>READ_FLOOR</c>(binary32)。</returns>
        public float ReadFloor(int skill)
        {
            if (_floor == null || _floor.Length == 0) return 0f;
            if (skill < 0) skill = 0;
            else if (skill > SkillCap) skill = SkillCap;
            return _floor[skill];
        }

        /// <summary>定表原始值(供形状断言;越界即钳位)。</summary>
        public float FloorAt(int index) => ReadFloor(index);
    }

    /// <summary>F-8.1 / F-8.2 的运行期求值器(纯静态、零字段 —— AC-8-3)。
    /// <para>运行期零幂运算:门槛走定表查表,档位走整数计数比较(G-1 / G-3)。</para>
    /// </summary>
    public static class DiagnosisReadFloorEvaluator
    {
        /// <summary>单次读数的结果(形态 + 词 + 可读性 —— 三输出可分)。</summary>
        public readonly struct Outcome
        {
            /// <summary>呈现形态(§S-8.2 字母表)。</summary>
            public readonly SignReadState State;

            /// <summary>纸上的词(<c>display_词_j</c>);本档无可回退词时 = <c>null</c>。
            /// <para>⚠️ 「有词」≠「可读」:即使 <see cref="IsReadable"/> = false,词仍可非 null
            /// (AC-8-5 双条件门 / GDD §F-8.1 的两行分工)。</para></summary>
            public readonly string DisplayWord;

            /// <summary>是否构成阳性证据(双条件「与」门:`Skill ≥ tier_named ∧ Sign ≥ READ_FLOOR`)。</summary>
            public readonly bool IsReadable;

            public Outcome(SignReadState state, string displayWord, bool isReadable)
            {
                State = state;
                DisplayWord = displayWord;
                IsReadable = isReadable;
            }
        }

        /// <summary>
        /// <b>§F-8.2 <c>TierIndex</c>(G-3:整数等级,不经浮点)</b> ——
        /// <c>#{ b ∈ SLOT_BOUNDS : b ≤ Skill }</c> ∈ {0..3}。
        /// <para>计数天然钳位,对全体 <c>Skill ∈ [0, SKILL_CAP]</c> 有定义(无中间空洞 ——
        /// GDD §F-8.2 修正说明)。切点取自 <see cref="DiagnosisTuning.SlotBounds"/>(单一真源),
        /// 与 30 的 `DIAG_TIERS` 由构建期耦合断言守(TR-diag-014 / D-8-9)。</para>
        /// </summary>
        /// <param name="skill">诊断熟练度(整数等级)。</param>
        /// <returns>档序 0=粗 1=中 2=细 3=满。</returns>
        public static int TierIndex(int skill)
        {
            int index = 0;
            System.Collections.Generic.IReadOnlyList<int> bounds = DiagnosisTuning.SlotBounds;
            for (int i = 0; i < bounds.Count; i++)
                if (skill >= bounds[i]) index++;
            return index;
        }

        /// <summary>档序 → 档(§F-8.2 <c>SlotOf</c>;越界钳位)。</summary>
        public static DiagnosisSlot SlotOf(int tierIndex)
        {
            if (tierIndex < 0) tierIndex = 0;
            else if (tierIndex > 3) tierIndex = 3;
            return (DiagnosisSlot)tierIndex;
        }

        /// <summary>熟练度 → 档(<c>slot_j(Skill) = SlotOf(TierIndex(Skill))</c>)。</summary>
        public static DiagnosisSlot SlotOfSkill(int skill) => SlotOf(TierIndex(skill));

        /// <summary>
        /// <b>§F-8.2 空白档回退</b> —— 自 <c>TierIndex</c> **向下**(往更粗档)找首个非空档;
        /// 全空 ⇒ 返回 <c>null</c>(该体征在此精度下**读不出**,AC-8-8)。
        /// <para>⚠️ 只向下、不向上 —— 向上会泄漏「比你的精度更细」的词(规则四:精度有上限)。
        /// 与 GDD §F-8.2 的「向下回退到最近的非空档」**规则**字面一致(koplik 的粗 / 中档为空 ⇒
        /// 在粗 / 中档读不出,只在细档起出词 —— 这正是「要想到去查」的语义,GDD R-8.2 表)。</para>
        /// <para>⚠️ <b>GDD 散文勘误(2026-10-05 登记,待设计轮)</b>:GDD §F-8.2 该段紧跟的**示例**
        /// 「`sign_rales` 在粗档无专属词时回退到**中档**」**方向反了**(向上而非向下),
        /// 且 `sign_rales` 粗档实有词(「胸里有声,说不清」)故该示例永不触发 ——
        /// 属 GDD 散文缺陷,**本实现以规则(向下)为准**,示例待 GDD 轮订正。</para>
        /// </summary>
        /// <param name="row">词条行(三档词,空档 = <c>null</c>)。</param>
        /// <param name="skill">诊断熟练度。</param>
        /// <returns>可回退到的词;全空 = <c>null</c>(读不出)。</returns>
        public static string DisplayWord(SignLexemeRow row, int skill)
        {
            string[] words = row.DisplayWords;
            if (words == null) return null;

            int top = TierIndex(skill);
            if (top > words.Length - 1) top = words.Length - 1;
            for (int i = top; i >= 0; i--)
                if (!string.IsNullOrEmpty(words[i])) return words[i];
            return null;
        }

        /// <summary>
        /// <b>§F-8.1 可读性判定(双条件「与」门)</b> + 形态裁决。
        /// <para><c>可读_j ⟺ Skill ≥ tier_named_j ∧ Sign_j ≥ READ_FLOOR(Skill)</c>
        /// (两条件缺一不可读 —— 规则三 能不能指名 × 规则四 值不值一读)。</para>
        /// <para>形态裁决(§S-8.2 / AC-8-22):可读 + 阳性词 ⇒ 阳性;可读 + 阴性词 ⇒ 阴性;
        /// 否则 ⇒ <see cref="SignReadState.UnreadableNegative"/>(已查 · 读不出 · 呈阴性形态)。</para>
        /// </summary>
        /// <param name="table">F-8.1 定表(依赖注入 —— 8 不持状态)。</param>
        /// <param name="row">词条行。</param>
        /// <param name="skill">诊断熟练度(整数等级)。</param>
        /// <param name="signValue01">9 的 <c>Project(Sign_j) ∈ [0,1]</c>(D-8-4;8 不重算)。</param>
        /// <returns>形态 + 词 + 可读性。</returns>
        public static Outcome Evaluate(
            in DiagnosisReadFloorTable table, SignLexemeRow row, int skill, float signValue01)
        {
            string word = DisplayWord(row, skill);

            // 双条件「与」门(G-3:tier 比较走整数;门槛比较走定表 float —— F-8.1 本体是浮点曲线)
            // ⚠️ 第三合取:全空档(无词可回退)⇒ 读不出(AC-8-8「全空 ⇒ 读不出,阴性形态」)——
            //    「可读」是「读出一个**词**并算证据」,无词则连词都没有,不构成阳性。
            bool named = skill >= row.TierNamed;
            bool aboveFloor = signValue01 >= table.ReadFloor(skill);
            bool readable = named && aboveFloor && word != null;

            SignReadState state;
            if (!readable)
            {
                state = SignReadState.UnreadableNegative;
            }
            else
            {
                state = row.Polarity == SignPolarity.Negative
                    ? SignReadState.Negative
                    : SignReadState.Positive;
            }

            return new Outcome(state, word, readable);
        }
    }
}
