// 权威来源:GDD design/gdd/clinic-machine.md
//            公共定义 `:155-172`(S = 连通家具格集 · 4 邻接只查 +x/+y · w 无序可负缺失=0)
//            F-24-1 `:174-206`(env_score 线性,**不钳制**)· F-24-2 `:208-231`(equip_score 档位求和 + 单钳制)
//            F-24-3 `:233-241`(K_CONTEXT_MAX —— **不属本件**:表派生值已在 `ClinicContextTable.KContextMax`)
//          · ADR-005(确定性模拟)· ADR-006(定点域 · 单一舍入 · 边界契约)
//          · ADR-012 F7(int64 溢出:显式范围守卫,不靠回绕)
//          · story-002:`ClinicRoomExtractionResult`(输入 = 房间析出结果)
//          · story-001:`ClinicContextTable` / `ClinicAdjTable`(输入 = cooked 表)
//
// ⚠️ **命名撞车(卡面 QA plan 回填 §5 点名)**:`Sim/EnvMod.cs` 是**系统 5** 的 `EnvMod_raw`。
//   本文件是**系统 24** 的 F-24-1 EnvMod ⇒ 全部名字带 `Clinic` 前缀(`ClinicEnvModOf` 等),
//   测试同样限定命名。**不得**把本文件的方法并进 `EnvMod.cs`。
//
// ⚠️ **无 clamp 铁律(AC-24-01b / D-21-31)**:`env_score` 的**唯一钳制点在 21a F1**
//   (`item-database.md:478-483`)。本件**原样透传**,越界是**合法输入**。
//   代码里出现任何对 env 侧的 clamp / 饱和 = 设计违例;`ClinicEnvModOf` 的注释与测试互相钉住这一条。
//
// ⚠️ **本件零除法**:求值链 = 加 / 档位整数和 / `<< 16` 标度。实现期若出现除法 = 偏离卡面
//   §Implementation Notes 4(「先停再问」)。舍入因此**不进本链**(ADR-006 的单一模式无落点)。
//
// ⚠️ **ROOM_NONE ⇒ 中性 (0, 0)**(EC-24-01 / EC-24-02 / EC-24-07):三个函数都以
//   `ClinicRoomExtractionResult.HasRoom == false` 短路为 0 —— **含 env**(不返回 base_env)。
//
// ⚠️ **本件不做的三件事**:
//   ① 「布局 → 房间类型」匹配(ROOM_TABLE 求值)—— 未实现;故 `base_env` 由**调用方**解析后传入
//      (`ClinicContextTable.TryGetBaseEnv(roomType, out Fix)`),本件不假装房间匹配已存在。
//   ② FURN_TABLE 的生产实现(`O-24-1` 未兑现)⇒ 属性来源靠注入,生产侧缺席由哨兵登记。
//      🔁 **2026-10-21 订正**:`O-24-1` 已兑现,生产实现 = `ClinicFurnitureTraitsSource`
//      ⇒ 本件仍**只依赖抽象**(不改签名),但「生产侧缺席」不再成立。
//   ③ `K_CONTEXT_MAX` 派生 —— 已在 `ClinicContextTable.KContextMax`(F-24-3,表构造期重算并校验)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Clinic
{
    /// <summary>
    /// 系统 24 的乘子求值(F-24-1 EnvMod · F-24-2 EquipMod · adj_sum)—— 三个**纯函数**。
    /// <para>无状态:同一输入两次求值 ⇒ 逐位相同的 raw;不缓存、不持有任何引用(ADR-005)。</para>
    /// </summary>
    public static class ClinicModifierEvaluator
    {
        /// <summary>`EQUIP_MOD_CAP` 的 raw 值 = `1/4` 的 Q16.16 表示(F-24-2;21a 常量)。
        /// <para>`65536/4 = 16384`,**不引入浮点**。⚠️ CAP &lt; 1 ⇒ 任何 `tier &gt; 0` 的布局即钉顶
        /// (`EQUIP_MOD_CAP &lt; 1` 的二值语义,2026-09-25 随拍记账);本件**不**为此改式。</para></summary>
        public const long EquipModCapRaw = Fix.OneRaw / 4;

        /// <summary>`equip_score → Fix` 的标度 = Q16.16 的左移位数(F-24-2 的 `× 65536`)。</summary>
        private const int EquipScoreScaleBits = Fix.FractionalBits;

        /// <summary>
        /// 缺口哨兵:`FURN_TABLE`(21a 家具目录的 `type` / `tier` / `e_env`)是否已在生产路径兑现。
        /// <para>**✅ 2026-10-21 转 true(O-24-1 兑现)** —— 生产实现 =
        /// <see cref="ClinicFurnitureTraitsSource"/>(输入 = 23 的只读视图 + 21a 家具目录的运行期表
        /// <see cref="ClinicFurnitureCatalogue"/>,数据来自 `item_database_furniture.cooked.bytes`)。
        /// 原 false 语义(「生产侧没有实现者,求值器只能吃夹具源」)已不成立。</para>
        /// <para>⚠️ **本哨兵不是「AC-24-08/12 已通过」的同义词** —— 那两条评的是**烘焙期**字段与闭集校验,
        /// 由 `ItemDatabaseFurnitureBinder` / `ClinicMachineRoomBinder` 承担;本哨兵只回答
        /// 「运行期格属性源有没有生产实现者」。</para>
        /// </summary>
        public static bool FurnTraitsProductionReady => true;

        // ══════════════════ F-24-1 的 adj_sum(公共定义的邻接和)══════════════════

        /// <summary>
        /// `adj_sum = Σ_{c∈S} w(type(c), type(c+ex)) + Σ_{c∈S} w(type(c), type(c+ey))`
        /// —— **只查 +x / +y 两向**,每条相邻边恰计一次(F-24-1 公共定义)。
        /// </summary>
        /// <param name="room">房间析出结果(`RoomCells` = S;`HasRoom == false` ⇒ 返回 0)。</param>
        /// <param name="traits">格属性来源。</param>
        /// <param name="adjTable">ADJ_TABLE(无序对;缺失 = 0)。</param>
        /// <exception cref="ArgumentNullException">注入缺失(接线错误,非设计态)。</exception>
        /// <exception cref="InvalidOperationException">某格属性为 <see cref="ClinicCellLookup.Unknown"/>
        /// (源不完整 —— 静默跳过一次就会少算一个 `e_env` / 一条边)。</exception>
        /// <exception cref="ArgumentException">`|S| &gt; n_max` —— 越出 F-24-1 的 int64 域前置(OQ-24-5)。</exception>
        public static Fix ClinicAdjSum(
            in ClinicRoomExtractionResult room,
            IClinicFurnitureTraitsSource traits,
            ClinicAdjTable adjTable)
        {
            if (adjTable == null) throw new ArgumentNullException(nameof(adjTable));
            if (!room.HasRoom) return Fix.Zero;

            Dictionary<WorldPos, string> typeByCell = ResolveTypes(room, traits, adjTable);
            return SumAdjacency(room.RoomCells, typeByCell, adjTable);
        }

        // ══════════════════ F-24-1 EnvMod(线性,**不钳制**)══════════════════

        /// <summary>
        /// `env_score = base_env + Σ_{c∈S} e_env(type(c)) + adj_sum` —— **线性,不钳制**。
        /// <para>⚠️ **唯一钳制点在 21a F1**(D-21-31)。本方法**必须**原样返回越界值:
        /// 返回值落在 [ENV_MOD_MIN, ENV_MOD_MAX] 之外是**合法输出**,不是错误。</para>
        /// </summary>
        /// <param name="room">房间析出结果(RoomCells = S;`HasRoom == false` ⇒ 返回 0,EC-24-01/02)。</param>
        /// <param name="traits">格属性来源。</param>
        /// <param name="adjTable">ADJ_TABLE。</param>
        /// <param name="baseEnv">`base_env(room_type)` —— 由调用方经
        /// <see cref="ClinicContextTable.TryGetBaseEnv"/> 解析(房间匹配不在本件,见文件头 ①)。</param>
        /// <exception cref="OverflowException">线性求和在 int64 内越界(ADR-012 F7:显式守卫,不靠回绕)。</exception>
        public static Fix ClinicEnvModOf(
            in ClinicRoomExtractionResult room,
            IClinicFurnitureTraitsSource traits,
            ClinicAdjTable adjTable,
            Fix baseEnv)
        {
            if (adjTable == null) throw new ArgumentNullException(nameof(adjTable));
            if (!room.HasRoom) return Fix.Zero;

            Dictionary<WorldPos, ClinicFurnitureTraits> byCell = ResolveTraits(room, traits, adjTable);

            long sum = baseEnv.Raw;
            foreach (KeyValuePair<WorldPos, ClinicFurnitureTraits> kv in byCell)
                sum = CheckedAdd(sum, kv.Value.EEnv.Raw, "env_score 的 Σ e_env");

            Dictionary<WorldPos, string> typeByCell = TypesOf(byCell);
            long adj = SumAdjacencyRaws(room.RoomCells, typeByCell, adjTable);
            sum = CheckedAdd(sum, adj, "env_score 的 adj_sum");

            return new Fix(sum);
        }

        // ══════════════════ F-24-2 EquipMod(档位和 + 单钳制)══════════════════

        /// <summary>
        /// `EquipMod = clamp(Σ_{tier(c)&gt;0} tier(c) × 65536, 0, EQUIP_MOD_CAP)`。
        /// <para>⚠️ 乘积在 **int64** 域求完再 clamp(F-24-2 运算域注:`int32` 在
        /// `equip_score &gt; 32767` 时溢出)。`tier(c) == 0` 的家具**不进和**。</para>
        /// <para>⚠️ **本方法的 clamp 是全案第二处、也是 24 侧唯一一处钳制**(F-24-2 自带);
        /// 与 env 侧"零钳制"是**同一条纪律的两面**,不得互相搬用。</para>
        /// </summary>
        /// <param name="room">房间析出结果(`HasRoom == false` ⇒ 0,EC-24-01/02)。</param>
        /// <param name="traits">格属性来源。</param>
        /// <exception cref="OverflowException">档位和 `× 65536` 在 int64 内越界(ADR-012 F7)。</exception>
        public static Fix ClinicEquipModOf(
            in ClinicRoomExtractionResult room,
            IClinicFurnitureTraitsSource traits)
        {
            if (!room.HasRoom) return Fix.Zero;

            // 档位和不需要 ADJ_TABLE;但 ResolveTraits 承担「Unknown ⇒ 硬失败」与格子解析,
            // 故此处走同一个遍历(adjTable 传 null 表示「本函数不查邻接」)。
            Dictionary<WorldPos, ClinicFurnitureTraits> byCell = ResolveTraits(room, traits, null);

            long score = 0;
            foreach (KeyValuePair<WorldPos, ClinicFurnitureTraits> kv in byCell)
            {
                int tier = kv.Value.Tier;
                if (tier <= 0) continue;                       // tier = 0 无档,不计入(ADR-006 D-21-17)
                score = CheckedAdd(score, tier, "equip_score 档位和");
            }

            // × 65536 在 int64 内:先判上界再移位(禁回绕,ADR-012 F7)。
            if (score > (long.MaxValue >> EquipScoreScaleBits))
                throw new OverflowException(
                    $"F-24-2 equip_score={score} × 2^{EquipScoreScaleBits} 越出 int64 —— 拒收(ADR-012 F7)");

            long raw = score << EquipScoreScaleBits;

            // 单钳制点:score ≥ 0 ⇒ 下界冗余存在但保留 F-24-2 的原式形。
            if (raw < 0) raw = 0;
            if (raw > EquipModCapRaw) raw = EquipModCapRaw;
            return new Fix(raw);
        }

        // ══════════════════ 内部(全部 private static)══════════════════

        /// <summary>解析 S 每格的属性;`Unknown` ⇒ 硬失败;`NonFurniture` ⇒ 跳过(非格,不计分不连通)。</summary>
        /// <param name="adjTable">非 null ⇒ 顺带执行 `|S| ≤ n_max` 域检查(仅邻接求值需要)。</param>
        private static Dictionary<WorldPos, ClinicFurnitureTraits> ResolveTraits(
            in ClinicRoomExtractionResult room,
            IClinicFurnitureTraitsSource traits,
            ClinicAdjTable adjTable)
        {
            if (traits == null) throw new ArgumentNullException(nameof(traits));

            IReadOnlyList<WorldPos> cells = room.RoomCells;
            if (adjTable != null && cells.Count > adjTable.NMax)
                throw new ArgumentException(
                    $"房间格数 |S| = {cells.Count} > n_max = {adjTable.NMax} —— " +
                    "越出 F-24-1 的 int64 算术域前置(OQ-24-5 已裁 64;超界布局不属本表域)");

            var byCell = new Dictionary<WorldPos, ClinicFurnitureTraits>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                WorldPos cell = cells[i];
                ClinicCellLookup lookup = traits.Lookup(cell, out ClinicFurnitureTraits t);
                switch (lookup)
                {
                    case ClinicCellLookup.Furniture:
                        byCell[cell] = t;
                        break;
                    case ClinicCellLookup.NonFurniture:
                        break;                                  // 非格:不计分
                    default:
                        throw new InvalidOperationException(
                            $"格 ({cell.X},{cell.Y},{cell.Z}) 的家具属性查询返回 Unknown —— " +
                            "属性源不完整(O-24-1 未兑现 / 目录缺该格)。静默跳过会让 e_env 与档位静默少算");
                }
            }
            return byCell;
        }

        /// <summary>`ClinicAdjSum` 用:只要类型表,不要 e_env 细节(仍走同一 Unknown 判据)。</summary>
        private static Dictionary<WorldPos, string> ResolveTypes(
            in ClinicRoomExtractionResult room,
            IClinicFurnitureTraitsSource traits,
            ClinicAdjTable adjTable)
            => TypesOf(ResolveTraits(room, traits, adjTable));

        private static Dictionary<WorldPos, string> TypesOf(
            Dictionary<WorldPos, ClinicFurnitureTraits> byCell)
        {
            var types = new Dictionary<WorldPos, string>(byCell.Count);
            foreach (KeyValuePair<WorldPos, ClinicFurnitureTraits> kv in byCell)
            {
                if (string.IsNullOrEmpty(kv.Value.Type))
                    throw new InvalidOperationException(
                        $"格 ({kv.Key.X},{kv.Key.Y},{kv.Key.Z}) 标记为家具但 type 为空 —— 属性源违约");
                types[kv.Key] = kv.Value.Type;
            }
            return types;
        }

        private static Fix SumAdjacency(
            IReadOnlyList<WorldPos> cells,
            Dictionary<WorldPos, string> typeByCell,
            ClinicAdjTable adjTable)
            => new Fix(SumAdjacencyRaws(cells, typeByCell, adjTable));

        /// <summary>
        /// 邻接和(整数 raw):遍历 S 的每格,只查 **+x** 与 **+y** 两个邻居 ——
        /// 每条无向相邻边因此**恰计一次**(承 F-24-1「每条边恰好计数一次」)。
        /// </summary>
        private static long SumAdjacencyRaws(
            IReadOnlyList<WorldPos> cells,
            Dictionary<WorldPos, string> typeByCell,
            ClinicAdjTable adjTable)
        {
            long sum = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                WorldPos cell = cells[i];
                if (!typeByCell.TryGetValue(cell, out string type)) continue;   // 非格:不参与邻接

                sum = AddNeighbor(sum, type, typeByCell, cell.X + 1, cell.Y, cell.Z, adjTable);
                sum = AddNeighbor(sum, type, typeByCell, cell.X, cell.Y + 1, cell.Z, adjTable);
            }
            return sum;
        }

        private static long AddNeighbor(
            long sum, string type,
            Dictionary<WorldPos, string> typeByCell,
            int nx, int ny, int nz,
            ClinicAdjTable adjTable)
        {
            var neighbor = new WorldPos(nx, ny, nz);
            if (!typeByCell.TryGetValue(neighbor, out string neighborType)) return sum;

            // 缺失 = 0(F-24-1「无序对、可负、缺失 = 0」)。
            if (!adjTable.TryGetWeight(type, neighborType, out Fix w)) return sum;
            return CheckedAdd(sum, w.Raw, "adj_sum");
        }

        /// <summary>显式 int64 加法守卫(ADR-012 F7:IL2CPP 侧有符号溢出是 UB,不靠回绕判定)。</summary>
        private static long CheckedAdd(long a, long b, string what)
        {
            if (b > 0 && a > long.MaxValue - b)
                throw new OverflowException($"{what}:{a} + {b} 越出 int64 上界 —— 拒收(ADR-012 F7)");
            if (b < 0 && a < long.MinValue - b)
                throw new OverflowException($"{what}:{a} + {b} 越出 int64 下界 —— 拒收(ADR-012 F7)");
            return a + b;
        }
    }
}
