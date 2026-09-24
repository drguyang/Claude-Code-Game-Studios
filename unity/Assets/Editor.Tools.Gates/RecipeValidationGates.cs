// 权威来源:production/epics/item-database/story-006-recipe-validation-fixtures.md(11 条 AC 执行体)
//   · AC-21a-7   —— 任一 outputs[].qty ≤ 0 拒(逐条 outputs_i.qty,非配方级标量)
//   · AC-21a-9   —— Σ(正的 cap) + max(0, ENV_MOD_MAX) > QTY_MULT_MAX − 1 拒(算术**不在此重抄**,
//                    委托 RecipeSettlementConstantTableValidator.IsSelfConsistent —— Story 005 产物)
//   · AC-21a-10  —— QTY_MULT_MIN ≥ QTY_MULT_MAX 拒(含等号,区间为空)
//   · AC-21a-11  —— RETAIN_MIN > RETAIN_MAX / RETAIN_MAX > 1 / RETAIN_MIN ≤ 0 三条件之一拒
//   · AC-21a-12  —— ENV_MOD_MIN > ENV_MOD_MAX 拒(相等 / 全负 / 跨零按现文过)
//   · AC-21a-16  —— 配方项(inputs **与** outputs)qty ≤ 0 拒 + 空 inputs/outputs 拒
//                    (GDD §Edge Cases :759/:760:凭空造物 / 销毁不归配方表)
//   · AC-21a-17  —— 配方项 item_key 外键悬空拒(已知键集合由**调用方注入**,纯函数不自读表)
//   · AC-21a-18  —— duration_ticks ≤ 0 拒(=1 过;只判 >0,秒值语义归审查)
//   · AC-21a-19  —— skill_gate ∉ [0, SKILL_CAP] 拒(SkillCap 经 RecipeSettlementConstants 读)
//   · AC-21a-20  —— min_quality < 1 或 > MAX_QUALITY 拒(MaxQuality 经 RecipeSettlementConstants 读)
//   · AC-21a-66  —— owner ∈ {process, craft, build} 三子集划分:缺失/null 拒、枚举外拒、
//                    划分互斥且并 = 全表(raw 字段级两层缝,照 FindStoredStackableKeys 先例 ——
//                    Recipe.Owner 是强类型 enum,「缺失」在强类型层不可表达,故入参 = 字面量序列)
//
// GDD:design/gdd/item-database.md §Edge Cases 写入期校验族(:755-830)· §Acceptance 组三(:1049-1075)
//      · §Schema F 配方八行字段表(:400-412)
// ADR-014 §Decision(全量校验失败 = 构建失败;本文件只供可复用纯函数,烘焙管线接线归 Story 008)
// ADR-025 §①(Editor.Tools 族 = 编辑期,不进玩家构建)
//
// ⚠️ 形态 = **错误列表(空 = 通过)**;本文件**零 throw** —— 显式 throw 聚合归 Story 008。
// ⚠️ **单一实现(承重纪律)**:AC-9 算术委托 IsSelfConsistent(禁在门里重抄不等式);
//    AC-66 枚举解析委托 ItemDbValidation.TryParseRecipeOwner(禁另写 switch/Enum.TryParse)。
// ⚠️ **零调参字面量(AC-21a-48)**:SkillCap / MaxQuality 一律经 RecipeSettlementConstants
//    的 PascalCase 属性读取;本文件不声明任何 const / static readonly 数值。
// ⚠️ 范围锁:不做守恒律与 EFF 区间(Story 005)、不做 recipe_id 唯一性(Story 002 AC-21a-21)、
//    不做 axis 长度(Story 004)、不做 state 通路符合性与物品表侧夹具(Story 007,含 AC-21a-24)、
//    不做烘焙管线接线(Story 008)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>配方表写入期校验套件的构建期纯函数(Story 006 十一条 AC 的执行体)。
    /// <para>全部方法:纯函数 · 无 I/O · 无静态可变态 · **零 throw** · 返回错误列表(空 = 通过)——
    /// 供 Story 008 两阶段烘焙管线复用(聚合非空列表 ⇒ 构建失败,ADR-014 §三
    /// 「全量校验失败 = 构建失败」)。</para>
    /// <example>
    /// var errors = RecipeValidationGates.ValidateDurationTicks(recipe.DurationTicks, recipe.RecipeId);
    /// if (errors.Count &gt; 0) throw new InvalidOperationException(string.Join("\n", errors));
    /// </example></summary>
    public static class RecipeValidationGates
    {
        // ══════════ AC-21a-7:outputs 逐条 qty ══════════

        /// <summary>校验产出侧逐条 <c>outputs_i.qty ≥ 1</c>(AC-21a-7 · GDD §Edge Cases :785)。
        /// <para><b>只查 Outputs</b> —— inputs 侧同判据归 AC-21a-16(同管道不同表,QA 明文)。
        /// <c>qty = 0</c> 使该条产量恒为 <c>max(1,0) = 1</c>(条目无意义),负值更错;
        /// 「配方级标量 BaseQty」在三轮 blocking #2 后已不存在,判据是**逐条**。</para>
        /// <para>浮点 qty(如 0.5)在 schema/类型层已被拒(<see cref="RecipeEntry.Qty"/> 是 <c>int</c>),
        /// 不在本门重复判定。</para></summary>
        /// <param name="outputs">产出项数组(null = 字段缺失,具名一条)。</param>
        /// <param name="recordLabel">诊断标签(如 recipe_id;可省)。</param>
        /// <returns>错误列表;空 = 全部产出 qty ≥ 1。</returns>
        /// <example><c>ValidateOutputQty(new[] { e0, e1 })</c>(qty 0 与 −1)⇒ 2 条错误。</example>
        public static IReadOnlyList<string> ValidateOutputQty(
            RecipeEntry[] outputs, string recordLabel = "")
        {
            if (outputs == null)
            {
                return new[]
                {
                    $"{RecordPrefix(recordLabel)} outputs 字段缺失(null)—— 逐条产出基数不可校验" +
                    "(AC-21a-7)",
                };
            }

            var errors = new List<string>();
            for (int i = 0; i < outputs.Length; i++)
            {
                if (outputs[i].Qty <= 0)
                {
                    errors.Add(
                        $"{RecordPrefix(recordLabel)} outputs[{i}].qty = {outputs[i].Qty} ≤ 0 —— " +
                        "逐条产出基数须 ≥ 1:qty = 0 使该条产量恒为 max(1, 0) = 1(条目无意义)," +
                        "负值更错;构建期硬失败(AC-21a-7 / GDD §Edge Cases「任一 outputs[].qty ≤ 0」)");
                }
            }

            return errors;
        }

        // ══════════ AC-21a-9:cap 和(委托 IsSelfConsistent —— 禁重抄算术)══════════

        /// <summary>校验常量表 <c>Σ(正的 cap) + max(0, EnvModMax) ≤ QtyMultMax − 1</c>(AC-21a-9)。
        /// <para><b>单一实现</b>:判据本体由 <see cref="RecipeSettlementConstantTableValidator.IsSelfConsistent"/>
        /// 裁定(raw 整数域,内建 <c>Positive()</c> —— EnvModMax ≤ 0 时该项按 0 计);
        /// 本门**只包装**:谓词为 false 时产出具名诊断,**不复制任何算术**。</para>
        /// <para>与 AC-21a-3 正反同判据:谓词 true ⇔ 本门空列表(同一谓词的两面)。</para></summary>
        /// <param name="constants">待校验常量表(值经 PascalCase 属性读;本文件不承载数值)。</param>
        /// <param name="recordLabel">诊断标签(可省)。</param>
        /// <returns>错误列表;空 = 自洽。</returns>
        /// <example>仅 EnvModMax 为正把式子推过界(cap 全小)⇒ 1 条点名两边 raw 分量的错误。</example>
        public static IReadOnlyList<string> ValidateConstantCapSum(
            in RecipeSettlementConstants constants, string recordLabel = "")
        {
            if (RecipeSettlementConstantTableValidator.IsSelfConsistent(constants))
                return Array.Empty<string>();

            // 错误文本点名不等式两边的 raw 分量(不重抄求和算术 —— 求和归 IsSelfConsistent)。
            return new[]
            {
                $"{RecordPrefix(recordLabel)} 常量表自洽性失败 —— " +
                $"Σ(正的 cap) + max(0, EnvModMax) > QtyMultMax − 1;" +
                $"左端分量 raw:SkillModCap = {constants.SkillModCap.Raw}, " +
                $"QualModCap = {constants.QualModCap.Raw}, EquipModCap = {constants.EquipModCap.Raw}, " +
                $"EnvModMax = {constants.EnvModMax.Raw};" +
                $"右端 raw:QtyMultMax = {constants.QtyMultMax.Raw}(减 1.0 raw = {Fix.OneRaw})。" +
                "后果 = 高投入段被 clamp 静默截断(投入无回报);构建期硬失败(AC-21a-9)",
            };
        }

        // ══════════ AC-21a-10:QTY_MULT 区间 ══════════

        /// <summary>校验 <c>QtyMultMin &lt; QtyMultMax</c>(AC-21a-10 · GDD §Edge Cases「区间为空」)。
        /// <para><b>拒含等号</b>:<c>QtyMultMin ≥ QtyMultMax</c> 均拒(相等 = 空区间);
        /// 差一个最小单位(raw 差 1)过。</para></summary>
        /// <param name="constants">待校验常量表。</param>
        /// <param name="recordLabel">诊断标签(可省)。</param>
        /// <returns>错误列表;空 = 区间非空。</returns>
        /// <example>min raw = max raw ⇒ 1 条;min raw = max raw − 1 ⇒ 空。</example>
        public static IReadOnlyList<string> ValidateQtyMultRange(
            in RecipeSettlementConstants constants, string recordLabel = "")
        {
            if (constants.QtyMultMin.Raw < constants.QtyMultMax.Raw)
                return Array.Empty<string>();

            return new[]
            {
                $"{RecordPrefix(recordLabel)} 产出乘子区间为空 —— " +
                $"QtyMultMin raw = {constants.QtyMultMin.Raw} ≥ QtyMultMax raw = " +
                $"{constants.QtyMultMax.Raw}(含等号:相等即空区间);构建期硬失败(AC-21a-10)",
            };
        }

        // ══════════ AC-21a-11:RETAIN 三条件 ══════════

        /// <summary>校验品级保留率区间(<c>0 &lt; RetainMin ≤ RetainMax ≤ 1</c>,AC-21a-11)。
        /// <para>三种违反,各具名一条(<b>可返回 3 条</b>):</para>
        /// <list type="number">
        /// <item><c>RetainMin &gt; RetainMax</c> —— 区间反向;</item>
        /// <item><c>RetainMax &gt; 1</c> —— 会被运行期 clamp 掩盖,<b>必须显式拒</b>;</item>
        /// <item><c>RetainMin ≤ 0</c> —— 零技能品级归零(GDD 🔑:必须 &gt; 0)。</item>
        /// </list>
        /// <para>边界:<c>RetainMax</c> 恰 = 1 过(满技能全保);<c>RetainMin = RetainMax</c> 过
        /// (退化点,GDD 只拒 <c>&gt;</c>,不擅自收紧)。</para></summary>
        /// <param name="constants">待校验常量表。</param>
        /// <param name="recordLabel">诊断标签(可省)。</param>
        /// <returns>错误列表;空 = 三条件全过。</returns>
        /// <example><c>RetainMin = 3/4, RetainMax = 1/2</c> ⇒ 1 条(条件一)。</example>
        public static IReadOnlyList<string> ValidateRetainRange(
            in RecipeSettlementConstants constants, string recordLabel = "")
        {
            var errors = new List<string>();

            if (constants.RetainMin.Raw > constants.RetainMax.Raw)
            {
                errors.Add(
                    $"{RecordPrefix(recordLabel)} 品级保留率区间反向 —— " +
                    $"RetainMin raw = {constants.RetainMin.Raw} > RetainMax raw = " +
                    $"{constants.RetainMax.Raw};构建期硬失败(AC-21a-11 条件一)");
            }

            if (constants.RetainMax.Raw > Fix.OneRaw)
            {
                errors.Add(
                    $"{RecordPrefix(recordLabel)} 满技能保留率上界 raw = {constants.RetainMax.Raw} > " +
                    $"1.0 raw = {Fix.OneRaw} —— 会被运行期 clamp 掩盖,必须显式拒;" +
                    "构建期硬失败(AC-21a-11 条件二)");
            }

            if (constants.RetainMin.Raw <= 0L)
            {
                errors.Add(
                    $"{RecordPrefix(recordLabel)} 技能 0 保留率下界 raw = {constants.RetainMin.Raw} ≤ 0 —— " +
                    "零技能品级会归零;构建期硬失败(AC-21a-11 条件三)");
            }

            return errors;
        }

        // ══════════ AC-21a-12:ENV_MOD 区间 ══════════

        /// <summary>校验 <c>EnvModMin ≤ EnvModMax</c>(AC-21a-12)。
        /// <para>GDD 现文只拒 <c>&gt;</c>:相等**过**;全负区间**过**;跨零**过**(负环境修正合法,
        /// 由 QtyMultMin 兜底 —— §Edge Cases「环境修正为负」)。</para></summary>
        /// <param name="constants">待校验常量表。</param>
        /// <param name="recordLabel">诊断标签(可省)。</param>
        /// <returns>错误列表;空 = 区间非空。</returns>
        /// <example><c>EnvModMin = 1/2, EnvModMax = −1/2</c> ⇒ 1 条;相等 ⇒ 空。</example>
        public static IReadOnlyList<string> ValidateEnvModRange(
            in RecipeSettlementConstants constants, string recordLabel = "")
        {
            if (constants.EnvModMin.Raw <= constants.EnvModMax.Raw)
                return Array.Empty<string>();

            return new[]
            {
                $"{RecordPrefix(recordLabel)} 环境修正区间为空 —— " +
                $"EnvModMin raw = {constants.EnvModMin.Raw} > EnvModMax raw = " +
                $"{constants.EnvModMax.Raw};构建期硬失败(AC-21a-12)",
            };
        }

        // ══════════ AC-21a-16:配方项 qty 双侧 + 空侧 ══════════

        /// <summary>校验配方项 <c>qty ≥ 1</c>(inputs 与 outputs **双侧**逐条)且两侧数组非空
        /// (AC-21a-16 · GDD §Edge Cases :759/:760/:798)。
        /// <para>零量 = 凭空造物 / 静默销毁;空 inputs = 凭空造物,空 outputs = 「销毁不由配方表实现」
        /// —— 均**独立拒**(QA Edge 明文归本族同管道)。判据与 AC-21a-7 相同但覆盖双侧。</para></summary>
        /// <param name="inputs">投入项(null 与空数组均拒)。</param>
        /// <param name="outputs">产出项(null 与空数组均拒)。</param>
        /// <param name="recordLabel">诊断标签(可省)。</param>
        /// <returns>错误列表;空 = 双侧非空且全部 qty ≥ 1。</returns>
        /// <example>inputs[0].qty = 0 ⇒ 1 条;outputs[0].qty = −1 ⇒ 1 条;空数组 ⇒ 各 1 条。</example>
        public static IReadOnlyList<string> ValidateRecipeEntryQuantities(
            RecipeEntry[] inputs, RecipeEntry[] outputs, string recordLabel = "")
        {
            var errors = new List<string>();

            if (inputs == null || inputs.Length == 0)
            {
                errors.Add(
                    $"{RecordPrefix(recordLabel)} inputs 为空 —— 空输入 = 凭空造物,不是配方;" +
                    "构建期硬失败(AC-21a-16 / GDD §Edge Cases「配方 inputs 为空」)");
            }
            else
            {
                for (int i = 0; i < inputs.Length; i++)
                {
                    if (inputs[i].Qty <= 0)
                    {
                        errors.Add(
                            $"{RecordPrefix(recordLabel)} inputs[{i}].qty = {inputs[i].Qty} ≤ 0 —— " +
                            "投入基数须 ≥ 1(零量 = 凭空造物 / 静默销毁);构建期硬失败(AC-21a-16)");
                    }
                }
            }

            if (outputs == null || outputs.Length == 0)
            {
                errors.Add(
                    $"{RecordPrefix(recordLabel)} outputs 为空 —— 销毁不由配方表实现(丢弃归库存);" +
                    "构建期硬失败(AC-21a-16 / GDD §Edge Cases「配方 outputs 为空」)");
            }
            else
            {
                for (int i = 0; i < outputs.Length; i++)
                {
                    if (outputs[i].Qty <= 0)
                    {
                        errors.Add(
                            $"{RecordPrefix(recordLabel)} outputs[{i}].qty = {outputs[i].Qty} ≤ 0 —— " +
                            "产出基数须 ≥ 1(零量 = 凭空造物 / 静默销毁);构建期硬失败(AC-21a-16)");
                    }
                }
            }

            return errors;
        }

        // ══════════ AC-21a-17:item_key 外键悬空 ══════════

        /// <summary>校验配方 inputs/outputs 的 <c>item_key</c> 外键(AC-21a-17 · 双向都查)。
        /// <para>判据 = <c>ItemKey</c> **整体** ∈ 已知键集合(不是只查 BaseId)——
        /// base 存在但 state 不成条目(如 raw 已登记、extracted 未登记)也算悬空;
        /// 大小写敏感由 <see cref="ItemKey"/> 的序数相等性承担(base_id 不同字面量 = 不同键)。</para>
        /// <para>已知键集合由**调用方注入**(纯函数,不自读物品表 —— 表的真源在数据文件,
        /// 由 Story 008 绑定层交给本门)。</para></summary>
        /// <param name="inputs">投入项(null/空由 AC-21a-16 判,本门对 null 按空侧不产错)。</param>
        /// <param name="outputs">产出项(同上)。</param>
        /// <param name="knownItemKeys">已知物品键集合(null 按空集合:全部引用悬空)。</param>
        /// <param name="recordLabel">诊断标签(可省)。</param>
        /// <returns>错误列表;空 = 双向外键全部命中。</returns>
        /// <example>inputs 引 (willow_bark, extracted) 而集合只有 (willow_bark, raw) ⇒ 1 条。</example>
        public static IReadOnlyList<string> ValidateRecipeForeignKeys(
            RecipeEntry[] inputs,
            RecipeEntry[] outputs,
            IReadOnlyCollection<ItemKey> knownItemKeys,
            string recordLabel = "")
        {
            var errors = new List<string>();
            var known = knownItemKeys == null
                ? new HashSet<ItemKey>()
                : new HashSet<ItemKey>(knownItemKeys);

            appendForeignKeyErrors(errors, inputs, known, "inputs", recordLabel);
            appendForeignKeyErrors(errors, outputs, known, "outputs", recordLabel);
            return errors;
        }

        // ══════════ AC-21a-18:duration_ticks ══════════

        /// <summary>校验 <c>duration_ticks &gt; 0</c>(AC-21a-18 · 规则五;单位是 tick 不是秒)。
        /// <para><c>= 1</c> 过(单 tick 配方);只判 <c>&gt; 0</c> —— 「写成秒值」与本值无法区分,
        /// 归语义审查(QA 明文);极大 tick 的溢出归 AC-21a-64 族。</para></summary>
        /// <param name="durationTicks">工时(逻辑 tick 数)。</param>
        /// <param name="recordLabel">诊断标签(可省)。</param>
        /// <returns>错误列表;空 = durationTicks ≥ 1。</returns>
        /// <example><c>ValidateDurationTicks(0)</c> ⇒ 1 条;<c>ValidateDurationTicks(1)</c> ⇒ 空。</example>
        public static IReadOnlyList<string> ValidateDurationTicks(
            int durationTicks, string recordLabel = "")
        {
            if (durationTicks > 0)
                return Array.Empty<string>();

            return new[]
            {
                $"{RecordPrefix(recordLabel)} duration_ticks = {durationTicks} ≤ 0 —— " +
                "工时须为正逻辑 tick 数(单位是 tick 不是秒);构建期硬失败(AC-21a-18 / 规则五)",
            };
        }

        // ══════════ AC-21a-19:skill_gate 区间 ══════════

        /// <summary>校验 <c>skill_gate ∈ [0, SkillCap]</c>(AC-21a-19)。
        /// <para><c>SkillCap</c> 从注入的常量表属性读取(引用自 30 技能系统,本文件零字面量 ——
        /// AC-21a-48);<c>&gt; SkillCap</c> 的配方永不可制,是数据错误。0(无门槛)与恰 =
        /// <c>SkillCap</c> 均过。</para></summary>
        /// <param name="skillGate">配方技能门槛。</param>
        /// <param name="constants">常量表(读其 <c>SkillCap</c> 属性)。</param>
        /// <param name="recordLabel">诊断标签(可省)。</param>
        /// <returns>错误列表;空 = 在界内。</returns>
        /// <example><c>ValidateSkillGate(-1, c)</c> ⇒ 1 条;<c>ValidateSkillGate(c.SkillCap, c)</c> ⇒ 空。</example>
        public static IReadOnlyList<string> ValidateSkillGate(
            int skillGate, in RecipeSettlementConstants constants, string recordLabel = "")
        {
            if (skillGate >= 0 && skillGate <= constants.SkillCap)
                return Array.Empty<string>();

            return new[]
            {
                $"{RecordPrefix(recordLabel)} skill_gate = {skillGate} ∉ [0, SkillCap = " +
                $"{constants.SkillCap}] —— 超上界的配方永不可制,是数据错误;" +
                "构建期硬失败(AC-21a-19)",
            };
        }

        // ══════════ AC-21a-20:min_quality 准入闸 ══════════

        /// <summary>校验 <c>min_quality ∈ [1, MaxQuality]</c>(AC-21a-20 · 准入闸,非品级出口)。
        /// <para><c>MaxQuality</c> 从注入的常量表属性读取(AC-21a-48);
        /// <c>&lt; 1</c> 或 <c>&gt; MaxQuality</c> 均拒;恰 = 1 与恰 = <c>MaxQuality</c> 均过。
        /// 运行期「输入实例 quality ≥ min_quality」的比较归 18/19 调用方,不在本门。</para></summary>
        /// <param name="minQuality">配方准入品级下限。</param>
        /// <param name="constants">常量表(读其 <c>MaxQuality</c> 属性)。</param>
        /// <param name="recordLabel">诊断标签(可省)。</param>
        /// <returns>错误列表;空 = 在界内。</returns>
        /// <example><c>ValidateMinQuality(0, c)</c> ⇒ 1 条;<c>ValidateMinQuality(c.MaxQuality, c)</c> ⇒ 空。</example>
        public static IReadOnlyList<string> ValidateMinQuality(
            int minQuality, in RecipeSettlementConstants constants, string recordLabel = "")
        {
            if (minQuality >= 1 && minQuality <= constants.MaxQuality)
                return Array.Empty<string>();

            return new[]
            {
                $"{RecordPrefix(recordLabel)} min_quality = {minQuality} ∉ [1, MaxQuality = " +
                $"{constants.MaxQuality}] —— 准入闸越界(品级下限恒 ≥ 1,上限随品级档数);" +
                "构建期硬失败(AC-21a-20)",
            };
        }

        // ══════════ AC-21a-66:owner 三子集划分(raw 字段级)══════════

        /// <summary>全量配方表按 <c>owner</c> 字面量分三子集(AC-21a-66 · D-21-30)。
        /// <para><b>raw 字段级两层缝</b>(照 <see cref="ItemDbValidation.FindStoredStackableKeys"/> 先例):
        /// <c>Recipe.Owner</c> 是强类型 enum,「缺失 / JSON null」在强类型层不可表达 ——
        /// 故入参是**字面量序列**(null 元素 = 缺字段 / JSON null,同罚)。</para>
        /// <para>判据:① 缺失/null ⇒ 拒;② 枚举外字面量 ⇒ 拒
        /// (**复用** <see cref="ItemDbValidation.TryParseRecipeOwner"/>,不另写解析);
        /// ③ 全部合法时按下标划分三子集 —— 每条记录**恰入一个**子集
        /// ⇒ 并 = 全表、两两交 = ∅ 结构性成立(测试显式断言三性质)。
        /// 单子集空表**合法**(并仍等全表)。</para>
        /// <para>范围:不实现 recipe_id 唯一性(Story 002 AC-21a-21)—— 重复 id 跨子集时
        /// 唯一性另判,owner 划分仍互斥(按下标,与 id 无关)。</para></summary>
        /// <param name="ownerLiterals">按表序的 owner 字面量序列(null 元素 = 缺失)。</param>
        /// <param name="processIndices">process 子集的**记录下标**(错误时为已解析部分)。</param>
        /// <param name="craftIndices">craft 子集的记录下标。</param>
        /// <param name="buildIndices">build 子集的记录下标。</param>
        /// <returns>错误列表;空 = 全表可划分。</returns>
        /// <example><c>["process", null, "Process"]</c> ⇒ 2 条错误(null 缺失 + 枚举外);<c>["craft"]</c> ⇒ 空,craft = [0]。</example>
        public static IReadOnlyList<string> PartitionRecipeOwners(
            IReadOnlyList<string> ownerLiterals,
            out IReadOnlyList<int> processIndices,
            out IReadOnlyList<int> craftIndices,
            out IReadOnlyList<int> buildIndices)
        {
            var process = new List<int>();
            var craft = new List<int>();
            var build = new List<int>();
            var errors = new List<string>();

            if (ownerLiterals == null)
            {
                processIndices = process;
                craftIndices = craft;
                buildIndices = build;
                errors.Add("配方表 owner 序列缺失(null)—— 任一配方缺 owner 即装载硬失败(AC-21a-66)");
                return errors;
            }

            for (int i = 0; i < ownerLiterals.Count; i++)
            {
                string literal = ownerLiterals[i];

                if (literal == null)
                {
                    errors.Add(
                        $"记录[{i}] 缺 owner 字段(JSON null / 缺失)—— 缺字段与 null 同罚," +
                        "装载硬失败(AC-21a-66)");
                    continue;
                }

                if (!ItemDbValidation.TryParseRecipeOwner(literal, out RecipeOwner owner))
                {
                    errors.Add(
                        $"记录[{i}] owner 字面量 \"{literal}\" ∉ 闭集 process|craft|build" +
                        "(序数精确 —— 枚举闭合同 AC-21a-22 口径);装载硬失败(AC-21a-66)");
                    continue;
                }

                if (owner == RecipeOwner.Process) process.Add(i);
                else if (owner == RecipeOwner.Craft) craft.Add(i);
                else build.Add(i);
            }

            processIndices = process;
            craftIndices = craft;
            buildIndices = build;
            return errors;
        }

        // ══════════ 共用小件 ══════════

        /// <summary>外键侧扫描(inputs / outputs 同判据,双向共用)。</summary>
        private static void appendForeignKeyErrors(
            List<string> errors,
            RecipeEntry[] side,
            HashSet<ItemKey> known,
            string sideName,
            string recordLabel)
        {
            if (side == null)
                return; // 空/null 侧的非空性归 AC-21a-16,本门不重复判

            for (int i = 0; i < side.Length; i++)
            {
                ItemKey key = side[i].Key;
                if (!known.Contains(key))
                {
                    errors.Add(
                        $"{RecordPrefix(recordLabel)} {sideName}[{i}].item_key = {key} 外键悬空 —— " +
                        "已知物品键集合中无此 (base_id, state) 整体(base 存在但 state 不成条目" +
                        "亦算悬空;大小写敏感);构建期硬失败(AC-21a-17)");
                }
            }
        }

        /// <summary>诊断前缀:无标签时用「记录」,有标签时用「记录(标签)」(与 ConservationGates 同形)。</summary>
        private static string RecordPrefix(string recordLabel)
            => string.IsNullOrEmpty(recordLabel) ? "记录" : $"记录({recordLabel})";
    }
}
