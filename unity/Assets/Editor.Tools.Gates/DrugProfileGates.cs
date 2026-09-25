// 权威来源:Story 004(production/epics/item-database/story-004-quality-timeline-and-stacking.md)
//   · AC-21a-50  —— drug_profile.axis_offset_by_quality[] 长度 ≠ MAX_QUALITY ⇒ 拒(非空时)
//   · AC-21a-50b —— gather_profile.quality_character[] 长度 ≠ MAX_QUALITY ⇒ 拒;
//                   **2026-09-25 R13 = 甲 扩充**:MAX_QUALITY > 1 时 null / 空列 ⇒ 拒(最小非空),
//                   且逐档非空(原「非空时才查长度」豁免废止)
//   · AC-21a-60  —— drug_profile.quality_axis 在 P0 期 ∉ {half_life} ⇒ 拒(D-21-23 收窄落盘)
//   · AC-21a-61  —— axis_offset_by_quality[] 任一非零档 |offset| < 可感知地板 ⇒ 拒(D-21-24 第二半)
//   · AC-21a-62  —— drug_quality_character[] 长度 ≠ MAX_QUALITY ⇒ 拒(成药侧);
//                   **2026-09-25 R13 = 甲 同批**:同 50b 扩充(空 = 拒 + 逐档非空)
//   GDD:design/gdd/item-database.md §Formulas F5(:688-748)· §Schema B/C · §Edge Cases 写入期校验族
//   ADR-006 §Decision 一(档位表与 base 同量纲 Q16.16,逐元素经 FixParse)
//   ADR-014 §三(逐 schema 已知键白名单 / 硬失败归烘焙管线 Story 008 —— 本文件只提供可复用纯函数)
//   ADR-025 §①(Editor.Tools 族 = L6,UnityEditor 自由,不进构建)
//
// ⚠️ 落点(unity-specialist 约束①,2026-09-23;本文件承该先例):校验纯函数住 Editor.Tools.Gates
//    **不**住 Sim —— 编辑期职责(008 烘焙期校验),不进玩家构建。schema **类型**仍住 Sim.Contracts。
// ⚠️ 执法体统一形态 = **错误列表(空 = 通过)**(与同目录 ItemDbValidation.cs 同形);「硬失败 throw」
//    由调用方(008 烘焙管线)聚合非空列表后执行 —— 本文件是纯函数,无 I/O、无静态可变态。
// ⚠️ 零数值:MAX_QUALITY / PERCEPTIBLE_FLOOR 一律**入参**,本文件不含任何旋钮字面量(AC-21a-48)。
// ⚠️ 单一实现(AC-21a-61):地板判据消费 <see cref="QualityTimelineSolver.AllOffsetsSatisfyPerceptibleFloor"/>
//    —— 不在本文件复制公式体;本文件只在判据为 false 时枚举违规档以**具名诊断**。
// ⚠️ 范围锁:本文件**不**做容器闭包(Story 010)、**不**做守恒律门(Story 005)、
//    **不**做烘焙管线接线(Story 008)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>F5 品级档位表 / 品级修饰表的构建期校验纯函数(Story 004 五条 AC 的执行体)。
    /// <para>全部方法:纯函数 · 无 I/O · 无静态可变态 · 返回错误列表(空 = 通过)——
    /// 供 Story 008 两阶段烘焙管线复用(聚合非空列表 ⇒ 构建失败,ADR-014 §三「全量校验失败 = 构建失败」)。</para>
    /// <para>「P0 可空」口径(D-21-6 / D-21-16)贯穿本文件:**空表放行**,长度校验仅在表非空时生效
    /// —— 空表 = 「字段在、值为空」,不是「写错了长度」。</para>
    /// </summary>
    public static class DrugProfileGates
    {
        // ══════════ AC-21a-50:axis_offset_by_quality[] 长度 = MAX_QUALITY ══════════

        /// <summary>校验 <c>drug_profile.axis_offset_by_quality[]</c> 的长度恰 = <c>MAX_QUALITY</c>(AC-21a-50)。
        /// <para>长度 ≠ 档数 ⇒ F5 的 <c>quality − 1</c> 越界读,会污染 9 的病史事件流(GDD §Edge Cases 明文)。</para>
        /// <para><b>空表放行</b>(null / 长度 0)= 「字段在、值为空」,P0 合法(D-21-6)—— 此时无 F5 作用,
        /// 无越界读风险,长度校验不适用(与 AC-21a-62 同口径)。</para></summary>
        /// <param name="offsets">档位偏移表(Q16.16;null / 空 = 放行)。</param>
        /// <param name="maxQuality">档数上限 <c>MAX_QUALITY</c>(≥ 1;数值旋钮,由调用方注入)。</param>
        /// <param name="recordLabel">诊断标签(如 base_id;可省)。</param>
        /// <returns>错误列表;空 = 长度合法或表为空。</returns>
        /// <example><c>ValidateAxisOffsetLength(new Fix[5], 5)</c> ⇒ 空;
        /// <c>ValidateAxisOffsetLength(new Fix[4], 5)</c> ⇒ 1 条错误。</example>
        public static IReadOnlyList<string> ValidateAxisOffsetLength(
            Fix[] offsets, int maxQuality, string recordLabel = "")
        {
            if (offsets == null || offsets.Length == 0)
                return Array.Empty<string>();

            if (offsets.Length == maxQuality)
                return Array.Empty<string>();

            return new[]
            {
                $"{RecordPrefix(recordLabel)} axis_offset_by_quality[] 长度 {offsets.Length} ≠ MAX_QUALITY " +
                $"{maxQuality} —— F5 的 quality−1 越界读会污染 9 的病史事件流(AC-21a-50)",
            };
        }

        // ══════════ AC-21a-50b / AC-21a-62:品级定性修饰表长度 ══════════

        /// <summary>校验 <c>gather_profile.quality_character[]</c> 的长度恰 = <c>MAX_QUALITY</c>(AC-21a-50b)。
        /// <para>标签与品级档必须一一对应(D-21-16);长度偏离 ⇒ 某一档无标签或有孤儿标签。
        /// <b>2026-09-25 R13 = 甲 改判</b>:<c>MAX_QUALITY &gt; 1</c> 时**最小非空**
        /// (null / 整列空 ⇒ 硬失败;原「P0 可空 / 空表放行」豁免废止)且逐档非空。</para></summary>
        /// <param name="qualityCharacter">原料侧品级修饰表(null / 空在 <c>maxQuality &gt; 1</c> 下 = 拒)。</param>
        /// <param name="maxQuality">档数上限 <c>MAX_QUALITY</c>(数值旋钮,入参)。</param>
        /// <param name="recordLabel">诊断标签(可省)。</param>
        /// <returns>错误列表;空 = 合法(长度恰等、逐档非空)。</returns>
        /// <example><c>ValidateGatherQualityCharacterLength(new string[0], 5)</c> ⇒ 1 条错误(R13=甲);
        /// <c>ValidateGatherQualityCharacterLength(new string[5], 5)</c> ⇒ 空;
        /// <c>ValidateGatherQualityCharacterLength(new string[1], 5)</c> ⇒ 1 条错误。</example>
        public static IReadOnlyList<string> ValidateGatherQualityCharacterLength(
            string[] qualityCharacter, int maxQuality, string recordLabel = "")
            => ValidateCharacterTableLength(
                qualityCharacter, maxQuality, recordLabel,
                "gather_profile.quality_character[]", "AC-21a-50b", "D-21-16");

        /// <summary>校验 <c>drug_profile.drug_quality_character[]</c> 的长度恰 = <c>MAX_QUALITY</c>(AC-21a-62)。
        /// <para>与 AC-21a-50b **同型**(成药侧,勿与原料侧路径混)。
        /// <b>2026-09-25 R13 = 甲 同批</b>:<c>MAX_QUALITY &gt; 1</c> 时**最小非空**
        /// (null / 空 = 拒;原「P0 可空放行」豁免废止)且逐档非空。</para></summary>
        /// <param name="drugQualityCharacter">成药侧品级修饰表(null / 空在 <c>maxQuality &gt; 1</c> 下 = 拒)。</param>
        /// <param name="maxQuality">档数上限 <c>MAX_QUALITY</c>(数值旋钮,入参)。</param>
        /// <param name="recordLabel">诊断标签(可省)。</param>
        /// <returns>错误列表;空 = 合法(长度恰等、逐档非空)。</returns>
        /// <example><c>ValidateDrugQualityCharacterLength(new string[0], 5)</c> ⇒ 1 条错误(R13=甲);
        /// <c>ValidateDrugQualityCharacterLength(new string[5], 5)</c> ⇒ 空;
        /// <c>ValidateDrugQualityCharacterLength(new string[6], 5)</c> ⇒ 1 条错误。</example>
        public static IReadOnlyList<string> ValidateDrugQualityCharacterLength(
            string[] drugQualityCharacter, int maxQuality, string recordLabel = "")
            => ValidateCharacterTableLength(
                drugQualityCharacter, maxQuality, recordLabel,
                "drug_profile.drug_quality_character[]", "AC-21a-62", "D-21-24");

        /// <summary>两表共用的长度判据(AC-21a-50b / AC-21a-62 同型,仅字段名与出处不同)。
        /// <para><b>2026-09-25 R13 = 甲</b>:<c>maxQuality &gt; 1</c> 时 null / 空 = 一条错误
        /// (最小非空);非空且长度 ≠ maxQuality ⇒ 一条错误;长度恰等但存在 null / 空白档
        /// ⇒ 一条错误(逐档非空)。<c>maxQuality ≤ 1</c> 时保留旧口径(空放行)。</para></summary>
        private static IReadOnlyList<string> ValidateCharacterTableLength(
            string[] values, int maxQuality, string recordLabel,
            string fieldName, string ac, string decision)
        {
            if (values == null || values.Length == 0)
            {
                if (maxQuality > 1)
                    return new[]
                    {
                        $"{RecordPrefix(recordLabel)} {fieldName} 为 null / 空列而 MAX_QUALITY {maxQuality} > 1 —— " +
                        $"R13 = 甲:最小非空,原「P0 可空」豁免已废止({decision};{ac})",
                    };
                return Array.Empty<string>();
            }

            if (values.Length != maxQuality)
                return new[]
                {
                    $"{RecordPrefix(recordLabel)} {fieldName} 长度 {values.Length} ≠ MAX_QUALITY {maxQuality} —— " +
                    $"标签与品级档须一一对应({decision};{ac})",
                };

            for (int i = 0; i < values.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(values[i]))
                    return new[]
                    {
                        $"{RecordPrefix(recordLabel)} {fieldName}[{i}] 为空白档 —— " +
                        $"R13 = 甲:逐档非空({decision};{ac})",
                    };
            }

            return Array.Empty<string>();
        }

        // ══════════ AC-21a-60:P0 quality_axis 收窄 ══════════

        /// <summary>校验 P0 期 <c>drug_profile.quality_axis</c> 取值 ∈ {<c>half_life</c>}(AC-21a-60 · D-21-23)。
        /// <para>P0 下 9 的处置载荷只有 <c>polarity / drug_potency / half_life</c> 三项 —— 其余三轴
        /// **产出即被丢弃** ⇒ 构建期拒(「枚举写了四值、实际只一值」是最难查的错,GDD §F5 明文)。</para>
        /// <para><b>null 放行</b>:字段为 null = P0 合法空值(D-21-6),无 F5 作用 ⇒ 本条不触发。
        /// 这与「字段必须在」是两回事(后者归 Story 008 绑定期原始键白名单)。</para>
        /// <para>枚举外字面量(非四轴任一的字符串)不归本条 —— 那是 AC-21a-22 枚举闭合(Story 002/006)。</para></summary>
        /// <param name="axis">解析后的作用轴;null = 字段缺席 / JSON null(放行)。</param>
        /// <param name="recordLabel">诊断标签(可省)。</param>
        /// <returns>错误列表;空 = null 或 = half_life。</returns>
        /// <example><c>ValidateP0QualityAxis(QualityAxis.HalfLife)</c> ⇒ 空;
        /// <c>ValidateP0QualityAxis(QualityAxis.Onset)</c> ⇒ 1 条错误。</example>
        public static IReadOnlyList<string> ValidateP0QualityAxis(
            QualityAxis? axis, string recordLabel = "")
        {
            if (axis == null || axis.Value == QualityAxis.HalfLife)
                return Array.Empty<string>();

            return new[]
            {
                $"{RecordPrefix(recordLabel)} quality_axis = {axis.Value} 在 P0 期非法 —— " +
                $"P0 只许 half_life(9 的处置载荷无 {axis.Value} 落点,产出即被丢弃;AC-21a-60 / D-21-23)",
            };
        }

        // ══════════ AC-21a-61:非零档 |offset| ≥ 可感知地板 ══════════

        /// <summary>校验 <c>axis_offset_by_quality[]</c> 每个**非零档**的模 ≥ 可感知地板(AC-21a-61 · D-21-24)。
        /// <para>偏移小于 9 的病史噪声带 ⇒ 统计上不可区分于噪声,玩家永远感觉不到;而 U-1/U-2 又禁数字刻度
        /// ⇒ 等于没有出口(GDD §F5 明文)。<b>零档豁免</b>地板(但仍受 AC-21a-37「至少一档非零」约束)。</para>
        /// <para>⚠️ <b>判据单一实现</b>:通过/失败由
        /// <see cref="QualityTimelineSolver.AllOffsetsSatisfyPerceptibleFloor"/> 裁定 —— 本方法**不复制**
        /// 该公式体,只在判据为 false 时枚举违规档以具名诊断。</para>
        /// <para>地板 ≤ 0 ⇒ 底线失效,恒放行(与谓词同口径)。空表放行。</para></summary>
        /// <param name="offsets">档位偏移表(Q16.16;null / 空 = 放行)。</param>
        /// <param name="floor">可感知地板(<c>Fix</c>;≤ 0 ⇒ 无约束)。数值旋钮,入参。</param>
        /// <param name="recordLabel">诊断标签(可省)。</param>
        /// <returns>错误列表;空 = 全部非零档 ≥ 地板(或表为空 / 地板失效)。</returns>
        /// <example>地板 1/8:<c>{ 0, 3/8, −1/4 }</c> ⇒ 空;〈 1/16 〉 ⇒ 1 条错误。</example>
        public static IReadOnlyList<string> ValidatePerceptibleFloor(
            Fix[] offsets, Fix floor, string recordLabel = "")
        {
            if (offsets == null || offsets.Length == 0)
                return Array.Empty<string>();

            // 判据唯一出处 = 求解器谓词(与运行期/测试消费同一函数,防扫描面分叉)。
            if (QualityTimelineSolver.AllOffsetsSatisfyPerceptibleFloor(offsets, floor))
                return Array.Empty<string>();

            // 到达此处谓词必为 false ⇒ 由谓词定义 floor.Raw > 0(floor ≤ 0 时谓词恒真)
            // ⇒ 下述比较无 long.MinValue 回绕之虞,不需要 ulong 域绝对值。
            var errors = new List<string>();
            for (int k = 0; k < offsets.Length; k++)
            {
                long raw = offsets[k].Raw;
                if (raw == 0L)
                    continue;                                  // 零档豁免地板
                bool belowFloor = raw > 0L ? raw < floor.Raw : raw > -floor.Raw;
                if (!belowFloor)
                    continue;

                errors.Add(
                    $"{RecordPrefix(recordLabel)} axis_offset_by_quality[{k}] raw = {raw} 的模 < " +
                    $"可感知地板 raw = {floor.Raw} —— 偏移不可区分于 9 的噪声带,等于没有出口(AC-21a-61 / D-21-24)");
            }

            return errors;
        }

        // ══════════ 共用小件 ══════════

        /// <summary>诊断前缀:无标签时用「记录」,有标签时用「记录(base_id)」(与 ItemDbValidation 同形)。</summary>
        private static string RecordPrefix(string recordLabel)
            => string.IsNullOrEmpty(recordLabel) ? "记录" : $"记录({recordLabel})";
    }
}
