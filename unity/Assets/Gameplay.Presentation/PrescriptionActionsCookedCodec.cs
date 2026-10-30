// 权威来源:ADR-014 §二/§五(运行期读烘焙产物,零 JSON 解析)· CookedFormat.cs(布局契约)
//          · Sim.Contracts.CookedFormat(头 20 B + 「字符串 = i32 字节长 + UTF-8」)
//          · 写方严格镜像:`Editor.Tools.Bake/PrescriptionActionsCookedWriter.cs`
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 版本不匹配 ⇒ 显式 throw(启动期硬失败,E-13 口径),
//    绝不返回默认值,绝不静默降级。
//
// ⚠️ 住 Gameplay.Presentation(边界程序集)—— 读产物、喂契约行给 sim 侧是本装的合法用途
//    (与 CookedCodec / InteractionKindCookedCodec 同族)。
//
// ⚠️ 行类型**不跨程序集复用**:`Editor.Tools.Bake.PrescriptionActionRow` 住编辑期装配
//    (不进玩家构建),故本文件自带同形只读行 `PrescriptionActionCookedRow`。
//    字段序与写方逐字对应,由 `test_wiring_codecRoundTrip_*` 对拍钉住。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>处方表契约行的**运行期读形**(与 <c>PrescriptionActionsCookedWriter</c> 镜像)。
    /// <para>只用 BCL(<see cref="BinaryReader"/>,little-endian);全部字段 = 整数 / 字符串,零解析器。</para>
    /// <example><code>var ds = PrescriptionActionsCookedCodec.Read(bytes); // 失败即 throw</code></example>
    /// </summary>
    public static class PrescriptionActionsCookedCodec
    {
        /// <summary>读处方表行集(常量回声 + 行集 + <c>ConfigVersion</c>)。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / 载荷长度不符 / 行数为 null 标记 —— 启动期硬失败。</exception>
        public static PrescriptionActionsDataSet Read(byte[] bytes)
        {
            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, "prescription_actions", out uint configVersion))
            {
                int doseBase = r.ReadInt32();
                int maxDoseDetents = r.ReadInt32();
                long singleDoseMaxRaw = r.ReadInt64();

                int count = r.ReadInt32();
                if (count < 0)
                    throw new InvalidDataException(
                        "[cooked] prescription_actions 行数为 null 标记 —— 产物损坏,启动期硬失败(E-13)");

                var raw = new List<(string ItemKey, int ActionId, string Polarity)>(count);
                for (int i = 0; i < count; i++)
                {
                    string itemKey = CookedReadUtil.ReadString32(r, "prescription_actions.item_key");
                    int actionId = r.ReadInt32();
                    string polarity = CookedReadUtil.ReadString32(r, "prescription_actions.polarity");
                    raw.Add((itemKey, actionId, polarity));
                }

                // ── 尾段:禁忌规则(2026-10-30 · ADR-035 §①B)──
                // ⚠️ 旧产物(无尾段)会在此读到 EOF ⇒ `EndOfStreamException`(经 OpenChecked 的
                //    载荷长度断言更早失败)⇒ **抛**,不静默给「零规则」。
                int sectionCount = r.ReadInt32();
                if (sectionCount < 0 || sectionCount > raw.Count)
                    throw new InvalidDataException(
                        $"[cooked] prescription_actions 规则段数 = {sectionCount} 越界(行数 {raw.Count})—— 产物损坏(E-13)");

                var rulesByKey = new Dictionary<string, ContraindicationRule[]>(StringComparer.Ordinal);
                for (int s = 0; s < sectionCount; s++)
                {
                    string itemKey = CookedReadUtil.ReadString32(r, "prescription_actions.rule.item_key");
                    int ruleCount = r.ReadInt32();
                    if (ruleCount <= 0)
                        throw new InvalidDataException(
                            $"[cooked] prescription_actions 规则段「{itemKey}」的 rule_count = {ruleCount} —— " +
                            "空段不得落盘(无规则的行不该出现在尾段里;E-13)");
                    var rules = new ContraindicationRule[ruleCount];
                    for (int k = 0; k < ruleCount; k++)
                        rules[k] = new ContraindicationRule(r.ReadInt32(), r.ReadInt32());
                    rulesByKey[itemKey] = rules;
                }

                var rows = new List<PrescriptionActionCookedRow>(raw.Count);
                for (int i = 0; i < raw.Count; i++)
                {
                    rulesByKey.TryGetValue(raw[i].ItemKey, out ContraindicationRule[] rules);
                    rows.Add(new PrescriptionActionCookedRow(raw[i].ItemKey, raw[i].ActionId, raw[i].Polarity, rules));
                }

                CookedReadUtil.AssertFullyConsumed(r, "prescription_actions");
                return new PrescriptionActionsDataSet(
                    rows, doseBase, maxDoseDetents, singleDoseMaxRaw, configVersion);
            }
        }
    }

    /// <summary>处方表的一行(与写方 <c>PrescriptionActionRow</c> 同形)。
    /// <para>⚠️ 刻意**不是**同一个类型 —— 行类型本属编辑期装配,不跨程序集复用。</para></summary>
    public readonly struct PrescriptionActionCookedRow
    {
        /// <summary>物品 key(烘焙期已校验 ∈ ItemDef 闭集)。</summary>
        public readonly string ItemKey;

        /// <summary>处置 id(枚举序数)。</summary>
        public readonly int ActionId;

        /// <summary>极性(causal / symptomatic)。</summary>
        public readonly string Polarity;

        /// <summary>禁忌规则集(2026-10-30 · ADR-035 §①B;`null` = 本药无禁忌记载)。</summary>
        public readonly ContraindicationRule[] ContraindicationRules;

        /// <summary>构造一行。</summary>
        public PrescriptionActionCookedRow(string itemKey, int actionId, string polarity)
            : this(itemKey, actionId, polarity, null)
        {
        }

        /// <summary>构造一行(带禁忌规则)。</summary>
        public PrescriptionActionCookedRow(string itemKey, int actionId, string polarity,
                                           ContraindicationRule[] contraindicationRules)
        {
            ItemKey = itemKey; ActionId = actionId; Polarity = polarity;
            ContraindicationRules = contraindicationRules;
        }
    }

    /// <summary>处方表烘焙产物的解码形(<c>prescription_actions.cooked.bytes</c>)。
    /// <para><see cref="ConfigVersion"/> = 源数据集内容哈希派生(u32,ADR-014 §五)。</para></summary>
    public struct PrescriptionActionsDataSet
    {
        /// <summary>全量契约行(序 = 源 JSON 序)。</summary>
        public List<PrescriptionActionCookedRow> Rows;

        /// <summary>剂量基点(raw Q16.16 = <c>DOSE_BASE</c>)。</summary>
        public int DoseBase;

        /// <summary>单次剂量最大档数(<c>MAX_DOSE_DETENTS</c>)。</summary>
        public int MaxDoseDetents;

        /// <summary>烘焙期派生常量 <c>single_dose_max</c>(raw Q16.16;AC-11-09 —— 零手填)。</summary>
        public long SingleDoseMaxRaw;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32)。</summary>
        public uint ConfigVersion;

        /// <summary>构造解码形。</summary>
        public PrescriptionActionsDataSet(List<PrescriptionActionCookedRow> rows, int doseBase,
            int maxDoseDetents, long singleDoseMaxRaw, uint configVersion)
        {
            Rows = rows; DoseBase = doseBase; MaxDoseDetents = maxDoseDetents;
            SingleDoseMaxRaw = singleDoseMaxRaw; ConfigVersion = configVersion;
        }
    }
}
