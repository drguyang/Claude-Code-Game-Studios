// 权威来源:
//   GDD design/gdd/clinic-machine.md `:161-162`(`S` 成员判据 = `type(c) ∈ FURN_TYPES`;
//     `tier(c) ∈ [0, TIER_MAX]`,0 = 无档)· `:188`(`e_env(type)` ∈ [−C_max, C_max],可负)
//   GDD `:297` 家具目录 `FURN_TABLE` 归 **21a**(作者态 `assets/data/item_database_furniture.json`)
//   OQ-24-2 `TIER_MAX = 3` · OQ-24-5 `n_max = 64`
//   ⚠️ **2026-10-21 用户裁定:`C_max` 1/64 → 1/8**(原派生式 `C_max = 1/n_max` 作废)
//   ADR-014 §五(运行期只读烘焙产物 —— 本类型是 cooked 的**解码后形态**,不是作者态)
//
// ⚠️ 本件住 Sim(`DaYiJingCheng.Sim.Clinic`,门 A:引用集 = BCL + Sim.Contracts,零引擎)。
//   它是 21a 家具目录在**模拟侧的只读视图**,不是第二真源 —— 真源永远是
//   `assets/data/item_database_furniture.json` → `item_database_furniture.cooked.bytes`。
//
// ⚠️ 构造器拒收 = Sim 侧纵深防御(与 `ClinicMachineTables` / `DelayTable53.Table` 同口径):
//   防「绕过解码直构表」与「产物被改」。烘焙期的主校验点在 `ItemDatabaseFurnitureBinder`。

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Clinic
{
    /// <summary>家具目录的一行(<c>FURN_TABLE</c> 的 `type` / `tier` / `e_env`)。</summary>
    public readonly struct ClinicFurnitureEntry
    {
        /// <summary>家具类型 id(`FURN_TYPES` 的成员)。</summary>
        public readonly string Type;

        /// <summary>档位(0 = 无档,不进 `equip_score`)。</summary>
        public readonly int Tier;

        /// <summary>单件环境贡献(`e_env`),**可负**。</summary>
        public readonly Fix EEnv;

        /// <summary>构造一行。</summary>
        public ClinicFurnitureEntry(string type, int tier, Fix eEnv)
        {
            Type = type; Tier = tier; EEnv = eEnv;
        }
    }

    /// <summary>家具目录(<c>FURN_TABLE</c>)的运行期只读视图 —— `FURN_TYPES` 闭集的运行期载体。</summary>
    public sealed class ClinicFurnitureCatalogue
    {
        /// <summary>`e_env` 的绝对值上界 `C_max = 1/8`。</summary>
        /// <remarks>
        /// <para>🔁 **2026-10-21 用户裁定:1/64 → 1/8**(原派生式 `C_max = 1/n_max` 作废)
        /// —— 理由与代价见 `ItemDatabaseFurnitureBinder.EnvContributionMax` 的 `<remarks>`。</para>
        /// <para>⚠️ 与 `ItemDatabaseFurnitureBinder.EnvContributionMax` **必须同值** ——
        /// 二者分居两层(烘焙期 / 运行期),跨侧等值由
        /// <c>test_furniture_catalogue_cMax_matchesBinder</c> 钉住(单一数值,两处表达)。</para>
        /// </remarks>
        public const long EnvContributionMaxRaw = Fix.OneRaw / 8;

        private readonly Dictionary<string, ClinicFurnitureEntry> _byType;
        private readonly ClinicFurnitureEntry[] _rows;
        private readonly ReadOnlyCollection<ClinicFurnitureEntry> _rowView;
        private readonly ReadOnlyCollection<string> _furnTypes;

        /// <summary>`TIER_MAX` 的声明值(源自产物头的 `tier_max`;OQ-24-2 = 3)。</summary>
        public long TierMax { get; }

        /// <summary>家具类型数(= `FURN_TYPES` 闭集大小)。</summary>
        public int Count => _rows.Length;

        /// <summary>全部行(只读视图;⚠️ 经 <see cref="ReadOnlyCollection{T}"/> 包装,
        /// 不暴露裸数组 —— 否者可被强转回数组就地改写)。</summary>
        public IReadOnlyList<ClinicFurnitureEntry> Rows => _rowView;

        /// <summary>
        /// `FURN_TYPES` 闭集(行序的 `type` 列;只读)。
        /// <para>⚠️ **命名刻意含 `FurnType`** —— `ClinicRoomExtraction` 的缺口哨兵检测
        /// (`test_furn_type_closure_absent_isDetectable_notJustObservable`)靠**命名**判定
        /// 「闭集是否已落地」;改名会让那条检测静默失效(该约定的已知边界,已在哨兵注释登记)。</para>
        /// <para>直接喂给 <see cref="ClinicRoomExtraction.Extract(IPlacedStructureReadView, IReadOnlyCollection{string})"/>
        /// 的真判据入口,或 <see cref="ClinicFurnitureTraitsSource"/> 的成员判定。</para>
        /// </summary>
        public IReadOnlyCollection<string> FurnTypes => _furnTypes;

        /// <summary>构造并校验。</summary>
        /// <param name="rows">行集(构造期拷贝)。</param>
        /// <param name="tierMax">产物头声明的 `tier_max`。</param>
        /// <exception cref="ArgumentException">① 空表 ② `type` 空 / 重复 ③ `tier ∉ [0, tierMax]`
        /// ④ `|e_env| &gt; C_max` ⑤ `tierMax &lt; 1`。</exception>
        public ClinicFurnitureCatalogue(IReadOnlyList<ClinicFurnitureEntry> rows, long tierMax)
        {
            if (rows == null || rows.Count == 0)
                throw new ArgumentException(
                    "FURN_TABLE 行数为 0 —— 空 FURN_TYPES 让全部房间谓词恒不匹配(整馆恒 ROOM_NONE)");
            if (tierMax < 1)
                throw new ArgumentException($"FURN_TABLE tier_max = {tierMax} < 1 —— 至少 1 档可计");

            TierMax = tierMax;
            _rows = new ClinicFurnitureEntry[rows.Count];
            _byType = new Dictionary<string, ClinicFurnitureEntry>(rows.Count, StringComparer.Ordinal);

            for (int i = 0; i < rows.Count; i++)
            {
                ClinicFurnitureEntry row = rows[i];
                if (string.IsNullOrEmpty(row.Type))
                    throw new ArgumentException($"FURN_TABLE 第 {i} 行 type 为空 —— 拒收");
                if (row.Tier < 0 || row.Tier > tierMax)
                    throw new ArgumentException(
                        $"FURN_TABLE「{row.Type}」tier={row.Tier} ∉ [0, {tierMax}] —— 档位域越界(F-24-2)");
                if (row.EEnv.Raw > EnvContributionMaxRaw || row.EEnv.Raw < -EnvContributionMaxRaw)
                    throw new ArgumentException(
                        $"FURN_TABLE「{row.Type}」e_env raw={row.EEnv.Raw} 越出 ±{EnvContributionMaxRaw}" +
                        "(C_max = 1/8;2026-10-21 用户裁定,原 C_max = 1/n_max 派生式已作废)");
                if (_byType.ContainsKey(row.Type))
                    throw new ArgumentException(
                        $"FURN_TABLE type「{row.Type}」重复 —— 每类型恰一行(重复使闭集大小失真)");

                _byType.Add(row.Type, row);
                _rows[i] = row;
            }

            _rowView = Array.AsReadOnly(_rows);

            var types = new string[_rows.Length];
            for (int i = 0; i < _rows.Length; i++) types[i] = _rows[i].Type;
            _furnTypes = Array.AsReadOnly(types);
        }

        /// <summary>查某类型的家具属性。</summary>
        /// <returns>存在 ⇒ true;否则 false(未知类型是调用方的正常分支 —— 「不是家具」)。</returns>
        public bool TryGet(string type, out ClinicFurnitureEntry entry)
        {
            if (type != null && _byType.TryGetValue(type, out entry)) return true;
            entry = default;
            return false;
        }

        /// <summary>该类型是否属于 `FURN_TYPES` 闭集(= S 成员判据)。</summary>
        public bool Contains(string type) => type != null && _byType.ContainsKey(type);
    }
}
