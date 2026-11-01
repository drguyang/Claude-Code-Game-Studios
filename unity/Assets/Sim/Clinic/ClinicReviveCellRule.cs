// 24 GDD 规则六 —— 床类家具格的**确定性选定**(复活点);2026-11-01 实现(结清 `TD-clinic-18` 的实现半)
//
// ─────────────────────────────────────────────────────────────────────────────
// 权威来源(逐字)
// ─────────────────────────────────────────────────────────────────────────────
//   GDD `design/gdd/clinic-machine.md` **规则六**(2026-10-30 补节):
//     ① 候选集 = { c ∈ clinic_cells : module_at(c) ∈ bed_modules }
//        其中 `bed_modules = { m ∈ MODULE_CATALOG : m.has_tag("bed") }`
//     ② 多格时取 **min(candidate_cells) 按 (y, x, z) 字典序**(先层、再 x、再 z,升序取首个)
//     ③ |candidate_cells| == 0 ⇒ **返回「无复活点」**,不填兜底(兜底 = 29 的规则)
//   `death-and-respawn.md:210` —— 「若医馆未建 / 无床 ⇒ 退回世界出生点」(**29 的裁决面**)
//   ADR-005 —— 纯函数,同布局逐位重放相同;遍历序不是确定性源
//   ADR-025 §① —— 本件住 `Sim`(门 A:引用集恰 = {BCL, Sim.Contracts},零引擎)
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 三条卡面禁令(逐条落实在本件的形状里,不是靠调用纪律)
// ─────────────────────────────────────────────────────────────────────────────
//   ① **禁「取第一个床格」**:那个说法依赖遍历序(字典迭代序 / 插入序 / 事件先后),
//      而遍历序不是确定性源(ADR-005)。⇒ 本件**只做坐标比较**,从不信任输入序。
//   ② **禁 29 自拍规则**:规则**只住本件**(24 侧),29 只消费 —— `RespawnCommandIssuer`
//      对它零假设(它只问「有/无 + 格」)。
//   ③ **禁零值哨兵**:GDD 规则六 ③ 明写「返回值须可与坐标为 (0,0,0) 的合法床格区分,
//      不得拿 `WorldPos` 的零值当哨兵」⇒ 本件用 `WorldPos?`(null = 无),**不用** `WorldPos` 零值。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 「床类」判据的**实测口径订正**(2026-11-01)
// ─────────────────────────────────────────────────────────────────────────────
//   GDD 规则六 ① 写的是 `m.has_tag("bed")` —— 一个**标签**判据。
//   实测两张表(`assets/data/build_modules.json` / `assets/data/item_database_furniture.json`,
//   2026-11-01)发现**全仓没有 tag 字段**:
//     · `build_modules.json` 的模块行键 = `module_id / module_type / furn_type /
//       build_item_key / occupancy_local / cost / refundable`(10 行,床 = `furn_type: "bed"`)
//     · `item_database_furniture.json` 的 `furn` 行键 = `type / tier / e_env`(7 行,床 = `type: "bed"`)
//   ⇒ 本件按 **`furn_type == "bed"`** 实现,并把判据收敛为**唯一常量** `BedFurnType`
//   (GDD 的意图是「按类别归类,加新床型不改本节」—— 这由**单一常量**兑现,不由字面匹配
//   每个模块 id 兑现;`bed_01` / `cot` / `bunk` 这类 id 名字面匹配正是 GDD 禁止的)。
//   ⚠️ **这是口径订正,不是裁量**:若将来给表加了真正的 tag 字段,改 `IsBedType` 一处即可。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Clinic
{
    /// <summary>
    /// 规则六:从「房间成员格 + 逐格家具类型」选出唯一的复活床格(纯函数)。
    /// <para><b>纯整数域</b>:只用坐标比较与字符串相等,零浮点、零遍历序依赖、零 I/O。</para>
    /// </summary>
    public static class ClinicReviveCellRule
    {
        /// <summary>
        /// 「床类」的家具类型名(GDD 规则六 ① 的 `bed` 类别;见文件头的实测口径订正)。
        /// </summary>
        public const string BedFurnType = "bed";

        /// <summary>该家具类型是否属于「床类」(规则六 ① 的唯一判据点)。</summary>
        public static bool IsBedType(string furnType) => BedFurnType.Equals(furnType, StringComparison.Ordinal);

        /// <summary>
        /// 选出复活床格(规则六 ②③)。
        /// </summary>
        /// <param name="clinicCells">医馆**房间成员格**(规则一析出产物;<see cref="ClinicRoomExtractionResult.RoomCells"/>)。
        /// 床落在房间外(如露天)不进候选集 —— 由本参数即已排除(调用方传房间格,不是全图格)。</param>
        /// <param name="furnTypeAt">逐格家具类型查询(该格无家具 / 无模块标识 ⇒ 返回 <c>null</c>)。</param>
        /// <returns>
        /// 候选集非空 ⇒ 按 `(y, x, z)` 字典序的**最小格**;<paramref name="clinicCells"/> 为空或
        /// 无任何床格 ⇒ <c>null</c>(= 「本布局下 24 给不出复活点」,**不是**兜底出生点 ——
        /// 兜底归 29,见 GDD 规则六 ③)。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="clinicCells"/> 或 <paramref name="furnTypeAt"/> 为 null(接线错误,非设计态)。
        /// </exception>
        /// <remarks>
        /// <para>⚠️ <b>为什么用「取 min」而不是「取首个命中」</b>:后者依赖 <paramref name="clinicCells"/>
        /// 的枚举序,而枚举序不是确定性源。本件对输入序**零假设** —— 同一格集合、任意顺序,
        /// 结果逐位相同(证伪轮 M1 正是打这一点)。</para>
        /// <para>⚠️ <b>并列不可能</b>:同一格只能有一个模块(规则一的布局前提)⇒ `min` 唯一,
        /// 无 tie-break 分支。若输入里出现重复格(调用方违约),`min` 仍唯一(同格取同值)。</para>
        /// </remarks>
        public static WorldPos? Select(IReadOnlyList<WorldPos> clinicCells, Func<WorldPos, string> furnTypeAt)
        {
            if (clinicCells == null) throw new ArgumentNullException(nameof(clinicCells));
            if (furnTypeAt == null) throw new ArgumentNullException(nameof(furnTypeAt));

            WorldPos? best = null;
            for (int i = 0; i < clinicCells.Count; i++)
            {
                WorldPos cell = clinicCells[i];
                if (!IsBedType(furnTypeAt(cell))) continue;   // 非床类 ⇒ 不进候选集
                if (best == null || CompareCellOrder(cell, best.Value) < 0) best = cell;
            }
            return best;
        }

        /// <summary>
        /// 规则六 ② 的排序键:**`(y, x, z)` 字典序**(先层、再 x、再 z)。
        /// </summary>
        /// <returns>负 = <paramref name="a"/> 在前;0 = 同格;正 = <paramref name="b"/> 在前。</returns>
        /// <remarks>
        /// ⚠️ <b>键序是规则的一部分,实现不得自行换序</b>(GDD 规则六 ② 逐字:「该选择是规则的
        /// 一部分,实现不得自行换序」)。为什么以 `y` 打头:同 `(x, z)` 的上下两层床(阁楼 / 地面)
        /// 须有稳定优先序 ⇒ 低层优先(「被抬回最近的床榻」,高层床是备选)。
        /// </remarks>
        public static int CompareCellOrder(in WorldPos a, in WorldPos b)
        {
            if (a.Y != b.Y) return a.Y < b.Y ? -1 : 1;   // ① 先层
            if (a.X != b.X) return a.X < b.X ? -1 : 1;   // ② 再 x
            if (a.Z != b.Z) return a.Z < b.Z ? -1 : 1;   // ③ 再 z
            return 0;
        }
    }
}
