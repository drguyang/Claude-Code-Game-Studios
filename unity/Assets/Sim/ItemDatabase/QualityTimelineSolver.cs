// 权威来源:design/gdd/item-database.md §Formulas F5(:688-748)+ D-21-14/23/24 + AC-21a-36/37/38/38b
//          · ADR-005(确定性 —— 纯函数、整数定点域、可重放)
//          · ADR-006(舍入 ROUND_HALF_AWAY_FROM_ZERO 整数域内完成;Fix 字段只经 FixParse 读入)
//          · ADR-025 §①(纯 sim 逻辑 → `Sim` 装配,引用集 {BCL, Sim.Contracts})
//
// ⚠️ **只动一根轴**(D-21-23 + AC-21a-38):F5 施加只读 <c>quality_axis</c> 所指那一根时间轴,
//    其余三根在结果里**同位不变**(null 亦原样透传 ⇒ 无 F5 时四轴逐位等于 profile 字段)。
// ⚠️ **P0 收窄**:<c>quality_axis = half_life</c> 是 P0 唯一合法值(AC-21a-60 拒收,归 Story 004
//    执法体 Editor.Tools.Gates.DrugProfileGates.ValidateP0QualityAxis);本求解器对任意
//    <c>QualityAxis</c> 泛化执行公式(P1a 解锁 = 去掉收窄门,不改本求解器 —— 形状照 GDD 全四轴写)。
// ⚠️ **构建期硬失败与运行期兜底分离**:AC-37 的「≥1 非零偏移」、AC-38b 的「Axis_base + min(offset)
//    > 0」在此只提供**纯谓词算子**(<see cref="HasQualityEffect"/> / <see cref="AllOffsetsSatisfyPerceptibleFloor"/>
//    / <see cref="DomainClampSatisfied"/>);把谓词结果升级为数据装载期的显式 throw 归 Story 008
//    (烘焙管线接线;负向夹具 invalid_offset_floor.json / invalid_axis_p0.json 归 Story 004)。运行期求解器
//    对**越界 / 缺 base** 的兜底是显式抛(域错误归数值轮),不做静默钳制。
// ⚠️ **不写任何流**:本文件只算出 <c>Axis_effective</c>;写入 9 病史事件流归 Story 009。
// ⚠️ **零数值**:axis_offset_by_quality[] 与各轴 base 的取值来自数据(Test 夹具注入);
//    PERCEPTIBLE_FLOOR 只在本文件谓词/测试中作参数出现,不承载具体值。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>F5 施加后的四根时间轴(<c>Axis_effective</c> 的完整形状,AC-21a-38 的可断言面)。
    /// <para>每根轴可空(Q16.16?):P0 合法档案可只有 half_life —— 无 F5 作用时四轴
    /// **逐位等于** <see cref="DrugProfile"/> 对应字段(null 透传为 null,不臆造 0);
    /// 作用轴 = <c>Axis_base + offset</c>,余三轴原样。</para></summary>
    public readonly struct EffectiveTimeline
    {
        /// <summary>起始(时间轴;F5 未施于其上时 = profile 字段原样)。</summary>
        public readonly Fix? Onset;
        /// <summary>达峰(同上)。</summary>
        public readonly Fix? Peak;
        /// <summary>半衰期(= 9 的 τ_half 来源;P0 唯一作用轴)。</summary>
        public readonly Fix? HalfLife;
        /// <summary>消除(同上)。</summary>
        public readonly Fix? Elimination;

        /// <summary>构造四轴结果(求解器专用入口)。</summary>
        /// <param name="onset">起始。</param>
        /// <param name="peak">达峰。</param>
        /// <param name="halfLife">半衰期。</param>
        /// <param name="elimination">消除。</param>
        public EffectiveTimeline(Fix? onset, Fix? peak, Fix? halfLife, Fix? elimination)
        {
            Onset = onset;
            Peak = peak;
            HalfLife = halfLife;
            Elimination = elimination;
        }
    }

    /// <summary>F5 品级 → 时间轴影响点求解器(AC-21a-36 / 37 / 38 / 38b)。
    /// <para><b>纯函数</b>:入参即全部输入 —— <see cref="DrugProfile"/> 与品质档位,
    /// 不读运行时对象、不写事件流 ⇒ 同参数集逐位产出同结果。</para>
    /// <para><b>单一实现</b>:<c>Axis_effective = Axis_base + axis_offset_by_quality[quality − 1]</c>
    /// 公式体仅存在于本文件(18/19/11 侧不得复制;扫描面一致性同 Story 003 的 AC-21a-6 家族)。</para>
    /// <para>⚠️ 长度越界保护:数组长度由 AC-21a-50(构建期)保证;本求解器**不做静默越界读** ——
    /// 运行期兜底显式抛(域错误归数值轮)。</para></summary>
    public static class QualityTimelineSolver
    {
        /// <summary>读取 <paramref name="axis"/> 所指时间轴的 base 值(<c>Axis_base</c>,AC-21a-36)。
        /// <para>⚠️ <c>drug_profile</c> 的轴字段可空(P0 合法);但**当 F5 已声明作用在该轴时**须有 base ——
        /// base 缺失 = 数据不一致,显式抛(运行期兜底;构建期数据门归 Story 008 烘焙管线)。</para>
        /// </summary>
        /// <param name="profile">药物档案。</param>
        /// <param name="axis">F5 作用轴。</param>
        /// <returns>该时间轴的 base(<c>Fix</c>)。</returns>
        /// <exception cref="InvalidOperationException">该轴字段为 null(数据不一致)。</exception>
        public static Fix AxisBase(in DrugProfile profile, QualityAxis axis)
            => axis switch
            {
                QualityAxis.Onset => profile.Onset ?? throw MissingBase(QualityAxis.Onset),
                QualityAxis.Peak => profile.Peak ?? throw MissingBase(QualityAxis.Peak),
                QualityAxis.HalfLife => profile.HalfLife ?? throw MissingBase(QualityAxis.HalfLife),
                QualityAxis.Elimination => profile.Elimination ?? throw MissingBase(QualityAxis.Elimination),
                _ => throw MissingBase(axis),
            };

        /// <summary>读取第 <c>quality</c> 档的偏移量 <c>axis_offset_by_quality[quality − 1]</c>(AC-21a-36)。
        /// <para>⚠️ 空表(null / 长度 0)= 无偏移 ⇒ 返回 <c>0</c>(「档位表在但值为空」口径,D-21-6);
        /// 非空但越界(quality &lt; 1 或 &gt; 长度)显式抛 —— 长度 = MAX_QUALITY 由 AC-21a-50 保证,
        /// 此处兜底防污染 9 的病史流。偏移可为负(劣药)。</para>
        /// </summary>
        /// <param name="offsets">品级档位偏移表(Q16.16,长度预期 = MAX_QUALITY;引用类型参数不加 <c>?</c> ——
        /// 本项目无 <c>#nullable</c> 上下文,见 Story 004 CS8632 订正)。</param>
        /// <param name="quality">品级档位 ∈ [1, MAX_QUALITY]。</param>
        /// <returns>该档偏移(<c>Fix</c>)。</returns>
        /// <exception cref="InvalidOperationException">表非空且 <paramref name="quality"/> 越界(长度 &lt; quality
        /// 或 quality &lt; 1)。</exception>
        public static Fix OffsetFor(Fix[] offsets, int quality)
        {
            if (offsets == null || offsets.Length == 0)
                return new Fix(0L);
            if (quality < 1 || quality > offsets.Length)
                throw new InvalidOperationException(
                    $"axis_offset_by_quality[quality−1] 越界:quality = {quality},表中档数 = " +
                    $"{offsets.Length}(长度 = MAX_QUALITY 由 AC-21a-50 保证;此处为运行期兜底)。");
            return offsets[quality - 1];
        }

        /// <summary>F5 施加:<c>Axis_effective = Axis_base + axis_offset_by_quality[quality − 1]</c>(AC-21a-36)。
        /// <para>无 F5 作用(quality_axis 未设 / 档位表为空)⇒ 四轴均返回 profile 字段原样(null 透传)。
        /// 否则只动所指一根:该根 = <c>Axis_base + offset</c>(该轴 base 缺失 ⇒ 抛出),
        /// 余三根逐位等于 profile 字段(AC-21a-38)。整式 <c>Fix</c> raw 整数加法,零浮点中间量
        /// (DRIFT 面:raw 相加 = 定点域加法本身,非 <c>Fix</c> 重赋值)。</para>
        /// </summary>
        /// <param name="profile">药物档案(含轴字段与档位表)。</param>
        /// <param name="quality">品级档位 ∈ [1, MAX_QUALITY]。</param>
        /// <returns>四根时间轴的有效值(作用轴含偏移,余轴逐位等于 profile 对应字段)。</returns>
        /// <exception cref="InvalidOperationException">作用轴 base 缺失 / 档位越界(见 <see cref="AxisBase"/>
        /// 与 <see cref="OffsetFor"/>)。</exception>
        /// <example><c>ApplyQualityTimeline(p, 4)</c>(axis = half_life, base 5, offset[3] = 1/4)
        /// ⇒ <c>HalfLife = 5.25</c>,onset/peak/elimination = profile 字段逐位。</example>
        public static EffectiveTimeline ApplyQualityTimeline(in DrugProfile profile, int quality)
        {
            // 无 F5 作用:四轴原样(可为 null —— P0 单轴档案合法)。
            if (profile.QualityAxis == null
                || profile.AxisOffsetByQuality is not { Length: > 0 } offsets)
            {
                return new EffectiveTimeline(profile.Onset, profile.Peak, profile.HalfLife, profile.Elimination);
            }

            QualityAxis axis = profile.QualityAxis.Value;
            Fix baseForAxis = AxisBase(profile, axis);              // 作用轴缺 base ⇒ 数据不一致,抛
            Fix offset = OffsetFor(offsets, quality);
            Fix shifted = new Fix(baseForAxis.Raw + offset.Raw);    // 定点域纯整数加法

            return axis switch
            {
                QualityAxis.Onset => new EffectiveTimeline(shifted, profile.Peak, profile.HalfLife, profile.Elimination),
                QualityAxis.Peak => new EffectiveTimeline(profile.Onset, shifted, profile.HalfLife, profile.Elimination),
                QualityAxis.HalfLife => new EffectiveTimeline(profile.Onset, profile.Peak, shifted, profile.Elimination),
                QualityAxis.Elimination => new EffectiveTimeline(profile.Onset, profile.Peak, profile.HalfLife, shifted),
                _ => throw MissingBase(axis),
            };
        }

        /// <summary>AC-21a-37 谓词 —— 档位表存在**至少一个非零偏移**("全零 ⇒ 通过" 不可接受)。
        /// <para>⚠️ 仅算子:构建/装载期把 <c>false</c> 升级为显式 <c>throw</c> 归 Story 008 烘焙管线
        /// (执法体 <c>DrugProfileGates.ValidatePerceptibleFloor</c> 归 Story 004)。</para>
        /// </summary>
        /// <param name="offsets">品级档位偏移表(Q16.16;null / 空 = 无表,不满足「有品质效果」)。</param>
        /// <returns><c>true</c> = 至少一档偏移非零(药品品质档位对时间轴有实际影响)。</returns>
        /// <example><c>HasQualityEffect(new[]{ 0, 3/8, 1/4 })</c> ⇒ <c>true</c>;
        /// <c>HasQualityEffect(new[]{ 0, 0, 0 })</c> ⇒ <c>false</c>。</example>
        public static bool HasQualityEffect(Fix[] offsets)
        {
            if (offsets == null)
                return false;
            foreach (Fix offset in offsets)
            {
                if (offset.Raw != 0L)
                    return true;
            }
            return false;
        }

        /// <summary>AC-21a-37 谓词 —— 每个非零档位偏移的模 ≥ 可感知底线
        /// (<c>∀k: offset[k] ≠ 0 ⇒ |offset[k]| ≥ PERCEPTIBLE_FLOOR</c>,D-21-24)。
        /// <para>⚠️ 底线值由调用方注入(数值旋钮,TR-itemdb-015 ◆ no-adr-by-design;测试夹具注入,
        /// 不断言具体数值)。零偏移豁免。比较在 ulong 域做,规避 <c>Math.Abs(long.MinValue)</c> 回绕。</para>
        /// </summary>
        /// <param name="offsets">品级档位偏移表(Q16.16;null / 空 = 无约束,∀-句恒真)。</param>
        /// <param name="floor">可感知底线(<c>Fix &gt; 0</c>;= 0 或负 ⇒ 底线失效,恒真)。</param>
        /// <returns><c>true</c> = 全部非零偏移 ≥ 底线(模)。</returns>
        /// <example>底线 1/8:<c>{ 0, 3/8, −1/4 }</c> ⇒ <c>true</c>;〈 -1/16, 0 〉 且 1/16 &lt; 1/8 ⇒ <c>false</c>。</example>
        public static bool AllOffsetsSatisfyPerceptibleFloor(Fix[] offsets, Fix floor)
        {
            if (offsets == null || floor.Raw <= 0L)
                return true;
            ulong floorMod = AbsUl(floor.Raw);
            foreach (Fix offset in offsets)
            {
                if (offset.Raw == 0L)
                    continue;
                if (AbsUl(offset.Raw) < floorMod)
                    return false;
            }
            return true;
        }

        /// <summary>AC-21a-38b 谓词 —— 域钳:<c>Axis_base + min(offset) &gt; 0</c>。
        /// <para>9 的衰减以该轴 base(半衰期)为除数 ⇒ 有效下限落在 <c>Axis_base + min(offset)</c>
        /// 必须为正;= 0 或 &lt; 0 即域外(构建期硬失败;本谓词供运行期断言与测试)。
        /// ⚠️ <b>只断言 &gt; 0,不得收紧到 ≥ MIN_USABLE_HALF_LIFE</b>(D-21-34 开:尺度下限归数值轮,
        /// 收紧 = 机制改动)。空表(null / 空)⇒ 无 F5 作用,域约束不生效,恒真。</para>
        /// </summary>
        /// <param name="axisBase">F5 作用轴的 base(<c>Q16.16</c>)。</param>
        /// <param name="offsets">品级档位偏移表(Q16.16;null / 空 = 恒真)。</param>
        /// <returns><c>true</c> = base + 全表最小值 &gt; 0。</returns>
        /// <example><c>base = 1</c>、<c>min(offset) = −3/4</c>(和 = 1/4)⇒ <c>true</c>;
        /// <c>base = 1/2</c>、<c>min(offset) = −1/2</c>(和 = 0)⇒ <c>false</c>。</example>
        public static bool DomainClampSatisfied(Fix axisBase, Fix[] offsets)
        {
            if (offsets == null || offsets.Length == 0)
                return true;
            long minRaw = long.MaxValue;
            foreach (Fix offset in offsets)
            {
                if (offset.Raw < minRaw)
                    minRaw = offset.Raw;
            }
            return axisBase.Raw + minRaw > 0L;
        }

        /// <summary>无回绕的绝对值(ulong 域;<see cref="Math.Abs(long)"/> 对
        /// <see cref="long.MinValue"/> 会抛 —— 档位偏移 raw 无上界保证)。</summary>
        private static ulong AbsUl(long value)
            => value < 0L ? (ulong)(-(value + 1L)) + 1UL : (ulong)value;

        private static InvalidOperationException MissingBase(QualityAxis axis)
            => new InvalidOperationException(
                $"F5 作用轴 {axis} 的 base 缺失(drug_profile.{AxisFieldName(axis)} 为 null)—— " +
                "当 F5 已声明时该轴须有 base(P0 可空 ≠ 可作用时缺失;构建期数据门归 Story 006/007)。");

        /// <summary>作用轴 → <see cref="DrugProfile"/> 对应可空字段名(供错误信息具名,勿硬编码某一轴)。</summary>
        private static string AxisFieldName(QualityAxis axis)
            => axis switch
            {
                QualityAxis.Onset => nameof(DrugProfile.Onset),
                QualityAxis.Peak => nameof(DrugProfile.Peak),
                QualityAxis.HalfLife => nameof(DrugProfile.HalfLife),
                QualityAxis.Elimination => nameof(DrugProfile.Elimination),
                _ => "quality_axis",
            };
    }
}