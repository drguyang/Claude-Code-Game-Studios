// 权威来源:ADR-014 §二/§五(运行期读烘焙产物,零 JSON 解析)· CookedFormat.cs(布局契约)
//          · 写方严格镜像:`Editor.Tools.Bake/DiseaseActionAxisCookedWriter.cs`
//
// ⚠️ 任何字段缺失 / 魔数不符 / 长度不符 / 版本不匹配 ⇒ 显式 throw(启动期硬失败,E-13 口径),
//    绝不返回默认值,绝不静默降级。
//
// ⚠️ 住 Gameplay.Presentation(边界程序集)—— 读产物、喂契约行给 sim 侧是本装的合法用途。
//
// ⚠️ 行类型**不跨程序集复用**:`Editor.Tools.Bake.ActionAxisRow` / `TreatableByRow` 住编辑期装配
//    (不进玩家构建),故本文件自带同形只读行,字段序与写方逐字对应。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>处置轴 + treatable_by 关系集烘焙产物(<c>disease_action_axis.cooked.bytes</c>)的运行期解码器。
    /// <para>载荷 = 处置轴行数 + 逐行(<c>action_id</c>, <c>name</c>, <c>owner</c>)
    /// + treatable_by 行数 + 逐行(<c>disease_key</c>, <c>action_id</c>, <c>polarity</c>)。</para>
    /// </summary>
    public static class DiseaseActionAxisCookedCodec
    {
        /// <summary>读处置轴行集 + treatable_by 关系集。</summary>
        /// <exception cref="InvalidDataException">头部损坏 / 载荷长度不符 / 行数为 null 标记 —— 启动期硬失败。</exception>
        public static DiseaseActionAxisDataSet Read(byte[] bytes)
        {
            using (BinaryReader r = CookedReadUtil.OpenChecked(bytes, "disease_action_axis", out uint configVersion))
            {
                int actionCount = r.ReadInt32();
                if (actionCount < 0)
                    throw new InvalidDataException(
                        "[cooked] disease_action_axis.actions 行数为 null 标记 —— 产物损坏,启动期硬失败(E-13)");

                var actions = new List<ActionAxisCookedRow>(actionCount);
                for (int i = 0; i < actionCount; i++)
                {
                    int actionId = r.ReadInt32();
                    string name = CookedReadUtil.ReadString32(r, "disease_action_axis.action.name");
                    string owner = CookedReadUtil.ReadString32(r, "disease_action_axis.action.owner");
                    actions.Add(new ActionAxisCookedRow(actionId, name, owner));
                }

                int treatableCount = r.ReadInt32();
                if (treatableCount < 0)
                    throw new InvalidDataException(
                        "[cooked] disease_action_axis.treatable_by 行数为 null 标记 —— 产物损坏,启动期硬失败(E-13)");

                var treatableBy = new List<TreatableByCookedRow>(treatableCount);
                for (int i = 0; i < treatableCount; i++)
                {
                    string diseaseKey = CookedReadUtil.ReadString32(r, "disease_action_axis.treatable_by.disease");
                    int actionId = r.ReadInt32();
                    string polarity = CookedReadUtil.ReadString32(r, "disease_action_axis.treatable_by.polarity");
                    treatableBy.Add(new TreatableByCookedRow(diseaseKey, actionId, polarity));
                }

                CookedReadUtil.AssertFullyConsumed(r, "disease_action_axis");
                return new DiseaseActionAxisDataSet(actions, treatableBy, configVersion);
            }
        }
    }

    /// <summary>处置轴的一行(<c>action_id</c> / <c>name</c> / <c>owner</c>)。
    /// <para>⚠️ 刻意**不是** <c>Editor.Tools.Bake.ActionAxisRow</c> —— 行类型不跨程序集复用。</para></summary>
    public readonly struct ActionAxisCookedRow
    {
        /// <summary>处置 id(闭集成员)。</summary>
        public readonly int ActionId;

        /// <summary>符号名(诊断输出)。</summary>
        public readonly string Name;

        /// <summary>该处置的归属系统("10" / "11")。</summary>
        public readonly string Owner;

        /// <summary>构造一行。</summary>
        public ActionAxisCookedRow(int actionId, string name, string owner)
        {
            ActionId = actionId; Name = name; Owner = owner;
        }
    }

    /// <summary>treatable_by 关系的一行(<c>disease_key</c> / <c>action_id</c> / <c>polarity</c>)。
    /// <para>⚠️ 刻意**不是** <c>Editor.Tools.Bake.TreatableByRow</c> —— 同上。</para></summary>
    public readonly struct TreatableByCookedRow
    {
        /// <summary>病种 key(如 "DIS_MALARIA")。</summary>
        public readonly string DiseaseKey;

        /// <summary>处置 id(烘焙期已校验 ∈ 处置轴闭集)。</summary>
        public readonly int ActionId;

        /// <summary>极性(causal / symptomatic)。</summary>
        public readonly string Polarity;

        /// <summary>构造一行。</summary>
        public TreatableByCookedRow(string diseaseKey, int actionId, string polarity)
        {
            DiseaseKey = diseaseKey; ActionId = actionId; Polarity = polarity;
        }
    }

    /// <summary>处置轴烘焙产物的解码形。
    /// <para><see cref="ConfigVersion"/> = 源数据集内容哈希派生(u32,ADR-014 §五)。</para></summary>
    public struct DiseaseActionAxisDataSet
    {
        /// <summary>处置轴行集(序 = 源 JSON 序)。</summary>
        public List<ActionAxisCookedRow> Actions;

        /// <summary>treatable_by 关系行集(序 = 源 JSON 序)。</summary>
        public List<TreatableByCookedRow> TreatableBy;

        /// <summary>烘焙时从源 JSON 文本派生的内容版本号(u32)。</summary>
        public uint ConfigVersion;

        /// <summary>构造解码形。</summary>
        public DiseaseActionAxisDataSet(List<ActionAxisCookedRow> actions,
            List<TreatableByCookedRow> treatableBy, uint configVersion)
        {
            Actions = actions; TreatableBy = treatableBy; ConfigVersion = configVersion;
        }
    }
}
