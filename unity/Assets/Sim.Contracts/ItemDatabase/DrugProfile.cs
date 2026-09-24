// 权威来源:design/gdd/item-database.md §Schema B(drug_profile 七字段表 + D-21-6/9/14/22/23/24)
//          · ADR-006 §Decision 一(weight/stack_max 是 int —— 本块无此二字段;Fix 字段只经 FixParse)
//          · ADR-014 §四(承载 Fix 的字段在 JSON 里必须是字符串 —— 绑定归 Story 008,本故事只落型)
//
// ⚠️ D-21-6:「字段必须在」是**绑定期**义务(Story 008 经原始键白名单强制),不是类型期 ——
//    类型用可空表达 P0 空值(JSON null / 缺席)。
// ⚠️ D-21-18(归 Story 010):本块含 Fix 字段,**禁**引导 Unity 内置序列化器;
//    本故事只建字段,不建编码器、不加 [Serializable]。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>剂量范围 <c>{min, max}</c>(§Schema B:dose_range 行)。整数计数,非 Fix(D-21-17 同族)。</summary>
    public readonly struct DoseRange
    {
        /// <summary>剂量下限(整数最小单位)。</summary>
        public readonly int Min;

        /// <summary>剂量上限(整数最小单位)。</summary>
        public readonly int Max;

        /// <summary>构造剂量范围。</summary>
        /// <param name="min">下限。</param>
        /// <param name="max">上限。</param>
        public DoseRange(int min, int max)
        {
            Min = min;
            Max = max;
        }
    }

    /// <summary><c>drug_profile</c> 块(扩 11 处方 / 12 药物槽)。仅 <c>category = drug</c> 非空;
    /// 反向(<c>category ≠ drug</c> 却带本块)校验归 Story 006(AC-21a 族)。
    /// <para>Fix 字段(drug_potency / 时间轴四字段 / axis_offset_by_quality[])的作者态形 = JSON 字符串,
    /// 解析唯一入口 = <c>FixParse</c>(ADR-006 §Decision 一 · ADR-014 §四);绑定归 Story 008。</para>
    /// <para>可空 = P0 空值合法(D-21-6:字段在,值可空)——「字段必须在」由绑定期原始键白名单强制。</para></summary>
    public struct DrugProfile
    {
        /// <summary>适应症(外键 → 9 的病种 id)。可空。</summary>
        public string[] Indications { get; set; }

        /// <summary>禁忌(外键 → 9 的病种 id)。可空。</summary>
        public string[] Contraindications { get; set; }

        /// <summary>剂量范围(整数对)。可空。</summary>
        public DoseRange? DoseRange { get; set; }

        /// <summary>药效幅值(D-21-22,原 Offset;9 处置事件 Offset 的来源)。静态基础值,不受品级调制。
        /// Q16.16 定点;JSON 形 = 字符串,经 <c>FixParse</c> 读入。可空。</summary>
        public Fix? DrugPotency { get; set; }

        /// <summary>起效(时间轴,Q16.16,字符串承载)。可空。</summary>
        public Fix? Onset { get; set; }

        /// <summary>达峰(时间轴,Q16.16,字符串承载)。可空。</summary>
        public Fix? Peak { get; set; }

        /// <summary>半衰期(= 9 的 τ_half 来源,Q16.16,字符串承载)。可空。</summary>
        public Fix? HalfLife { get; set; }

        /// <summary>消除(时间轴,Q16.16,字符串承载)。可空。</summary>
        public Fix? Elimination { get; set; }

        /// <summary>F5 作用轴(D-21-14/23)。P0 只许 <c>half_life</c> —— P0 收窄校验 = AC-21a-60
        /// (执法体 <c>Editor.Tools.Gates.DrugProfileGates.ValidateP0QualityAxis</c>,Story 004)。可空。</summary>
        public QualityAxis? QualityAxis { get; set; }

        /// <summary>F5:品级 → 时间轴档位偏移(Q16.16 逐元素,字符串承载)。
        /// 长度 = <c>MAX_QUALITY</c> 的校验 = AC-21a-50
        /// (执法体 <c>Editor.Tools.Gates.DrugProfileGates.ValidateAxisOffsetLength</c>,Story 004)。可空。</summary>
        public Fix[] AxisOffsetByQuality { get; set; }

        /// <summary>成药侧品级定性修饰(D-21-24,长度 = <c>MAX_QUALITY</c>,42 以药签措辞呈现)。
        /// 长度校验 = AC-21a-62
        /// (执法体 <c>Editor.Tools.Gates.DrugProfileGates.ValidateDrugQualityCharacterLength</c>,Story 004)。可空(P0)。</summary>
        public string[] DrugQualityCharacter { get; set; }
    }
}
