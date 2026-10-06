// 权威来源:ADR-014 §三(阶段 2 绑定的可测接缝)· Story 001
//
// ⚠️ 本件是**测试可见的薄探针** —— 把 `PrescriptionActionsBinder` / `PrescriptionActionsBaker` 的
//    internal 面暴露成 public,唯一目的是让 EditMode 测试能**驱动生产同一台机器**
//    (反空转:测试断言的必须是生产接缝,不是测试侧重实现)。
//
// ⚠️ 零逻辑:本件不含任何判据、不做任何转换,只转发。判据全部在生产件里。

using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>生产烘焙接缝的 public 转发(仅供测试驱动;零逻辑)。</summary>
    public static class PrescriptionActionsBinderProbe
    {
        /// <summary>用给定源文本跑完整阶段 2(绑定 + DC 校验 + 编码),返回 cooked 字节。</summary>
        /// <exception cref="BakeValidationException">任一违例(聚合硬失败)。</exception>
        public static byte[] Bake(string actionsJson, string lexiconJson, string itemsJson)
        {
            PrescriptionActionsBinder.BindResult bound = PrescriptionActionsBinder.Bind(actionsJson, lexiconJson, itemsJson);
            uint configVersion = ConfigVersionOf(actionsJson, lexiconJson);
            return PrescriptionActionsCookedWriter.Write(bound.Rows, bound.SchemaVersion, configVersion,
                bound.DoseBase, bound.MaxDoseDetents, bound.SingleDoseMaxRaw);
        }

        /// <summary>用给定源文本跑阶段 2 绑定 + 校验(不编码;供负夹具断言拒绝)。</summary>
        /// <exception cref="BakeValidationException">任一违例(聚合硬失败)。</exception>
        public static PrescriptionActionsBinder.BindResult Bind(string actionsJson, string lexiconJson, string itemsJson)
            => PrescriptionActionsBinder.Bind(actionsJson, lexiconJson, itemsJson);

        /// <summary>派生源文本集的 ConfigVersion(与生产同源,供确定性断言)。
        /// <para>⚠️ 哈希**不含** `item_database_items.json` —— 与生产 `PrescriptionActionsBaker.BakeFromRepo`
        /// 逐字同源(仅 actions + lexicon 两键)。AC-11-09 的「源字段变 ⇒ ConfigVersion 变」
        /// 由本测试面按**同一命名键**复算证明;生产侧是否把 items 纳入哈希归 story-001 的域。</para></summary>
        public static uint ConfigVersionOf(string actionsJson, string lexiconJson)
            => ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(PrescriptionActionsBaker.ActionsFileName, actionsJson),
                new KeyValuePair<string, string>(PrescriptionActionsBaker.LexiconFileName, lexiconJson),
            });

        /// <summary>读绑定结果的派生常量 `single_dose_max`(raw Q16.16;AC-11-09)。
        /// <para>零逻辑转发 —— 判据全部在生产件 <see cref="PrescriptionDerivedBaker"/> 里。</para></summary>
        /// <exception cref="BakeValidationException">绑定 / 派生任一失败(聚合硬失败)。</exception>
        public static long SingleDoseMaxRawOf(string actionsJson, string lexiconJson, string itemsJson)
            => Bind(actionsJson, lexiconJson, itemsJson).SingleDoseMaxRaw;
    }
}
