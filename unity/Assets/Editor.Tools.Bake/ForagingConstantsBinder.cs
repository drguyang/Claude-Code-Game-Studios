// S20-3 · story-001 —— `foraging_constants.json` 的**阶段 2 绑定器**。
//
// 权威来源:
//   ADR-014 §三(阶段 2:已知键白名单 · FixParse · schema_version · 聚合 throw)· §四(Fix 字段必须字符串)
//   GDD design/gdd/foraging.md
//     · F-17-1 `:265-310` + 装载期校验 `:305-311`(CapTable 四性质 · Σw ≥ 1)
//     · F-17-2 `:318-336`(GatherMul 值域 [1, GATHER_MUL_CAP])
//     · F-17-3 `:338-360`(RegrowWindow > 0 · SEASON_MULT 表长 = SEASONS_PER_YEAR)
//     · §Tuning Knobs `:541-548`(全部数值 2026-10-06 已裁)
//   story production/epics/foraging/story-001-variety-closure-and-data-load-validation.md
//     AC-17-08b / AC-17-14 / AC-17-16
//
// ⚠️ 本件是 `foraging_constants.json` 的**唯一构建期校验点**(与 `ClinicMachineContextBinder` 同口径:
//   Sim 侧 `ForagingConstants` 的构造期拒收是**纵深防御**,不是第二真源 —— 两侧判据逐条同源)。
//
// ⚠️ **本类不抛裸 Exception**:错误进列表,聚合后一次 `BakeValidationException`
//   (既有门零散 throw 纪律;见 `ClinicMachineContextBinder` 同款)。
//
// ⚠️ **`quality_distribution` 不在此表** —— 它住 21a 的 `gather_profile`(GDD 六处一致;
//   2026-10-26 用户裁定)。本表若出现该键 ⇒ 白名单外拒收(见 `ForagingConstantsSchema.RootKeys`)。
//
// ⚠️ **`seasons_per_year` 存在本表,但它归 5**(`OQ-5-1` 已裁)。这是**声明式的拷贝**:
//   17 需要在装载期断 `SEASON_MULT` 表长,而 17 不得硬编码 5。⇒ 本表登记 5 的现行值,
//   由装配轮与 5 的产物比对(跨表断言;比对点不在本件 —— 5 的 cooked 装载归装配轮)。
//   ⚠️ 登记为**已知 seam**:两侧不同步时,17 装载会按本表的值断表长 ⇒ 早失败而非静默错算。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`foraging_constants.json` 的阶段 2 绑定器(AC-17-08b / 14 / 16 的构建期校验点)。</summary>
    public static class ForagingConstantsBinder
    {
        /// <summary>源文件名(单一出处)。</summary>
        public const string FileName = ForagingConstantsSchema.FileName;

        /// <summary>绑定结果(全部为已校验的原始域值;`Fix` 已是 raw)。</summary>
        public readonly struct BindResult
        {
            /// <summary>`CapTable[]`(长度恒 61;已断四性质)。</summary>
            public readonly int[] CapTable;

            /// <summary>`SEASON_MULT[]`(长度恒 = <see cref="SeasonsPerYear"/>;各值 &gt; 0)。</summary>
            public readonly Fix[] SeasonMult;

            /// <summary>`SEASONS_PER_YEAR`(5 的交付物;本表登记其现行值)。</summary>
            public readonly int SeasonsPerYear;

            /// <summary>`RegrowWindow`(tick;已断 &gt; 0)。</summary>
            public readonly long RegrowWindowTicks;

            /// <summary>`BaseCapacity`(已断 &gt; 0)。</summary>
            public readonly int BaseCapacity;

            /// <summary>`DECAY_RATE`(已断 &gt; 0)。</summary>
            public readonly Fix DecayRate;

            /// <summary>`DECAY_FLOOR`(已断 ∈ (0, 1])。</summary>
            public readonly Fix DecayFloor;

            /// <summary>`GATHER_MUL_CAP`(已断 ≥ 1)。</summary>
            public readonly Fix GatherMulCap;

            /// <summary>源 schema_version(写方头槽 offset 8 的唯一真值来源)。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果。</summary>
            public BindResult(
                int[] capTable, Fix[] seasonMult, int seasonsPerYear,
                long regrowWindowTicks, int baseCapacity,
                Fix decayRate, Fix decayFloor, Fix gatherMulCap, uint schemaVersion)
            {
                CapTable = capTable; SeasonMult = seasonMult; SeasonsPerYear = seasonsPerYear;
                RegrowWindowTicks = regrowWindowTicks; BaseCapacity = baseCapacity;
                DecayRate = decayRate; DecayFloor = decayFloor; GatherMulCap = gatherMulCap;
                SchemaVersion = schemaVersion;
            }
        }

        /// <summary>
        /// 绑定 + 全部构建期拒收(任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>)。
        /// </summary>
        /// <param name="json">源文件全文。</param>
        /// <param name="maxQuality">21a 的 `MAX_QUALITY`(D-21-7 双向耦合;`CapTable` 各值须 ≤ 它)。
        /// **由调用方从 21a 常量表读入,禁在本件写死 5**。</param>
        /// <exception cref="BakeValidationException">词法失败 / 任一校验失败(聚合后一次 throw)。</exception>
        public static BindResult Bind(string json, int maxQuality)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(
                    new List<string> { $"{FileName} 阶段1 词法失败:{lexErr}" });

            if (root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(
                    new List<string> { $"{FileName} 根须为对象(实得 {root.Kind})" });

            ForagingConstantsSchema.RejectUnknownKeys(
                root, ForagingConstantsSchema.RootKeys, "根", FileName, errors);

            // schema_version(与运行时头契约比对;只做域检查形同虚设)
            uint schemaVersion = 0;
            if (!root.TryGet("schema_version", out JsonNode svNode))
                errors.Add($"{FileName}:缺 schema_version");
            else if (svNode.Kind != JsonNodeKind.Integer || svNode.Int < 0 || svNode.Int > uint.MaxValue)
                errors.Add($"{FileName}:schema_version 须为 u32 域整数(实得 {svNode.Kind}={svNode.RawText})");
            else if ((uint)svNode.Int != CookedFormat.SchemaVersion)
                errors.Add(
                    $"{FileName}:schema_version={(uint)svNode.Int} ≠ 期望 {CookedFormat.SchemaVersion} " +
                    "(源表 schema 与运行时头契约不一致 ⇒ 构建期硬失败)");
            else
                schemaVersion = (uint)svNode.Int;

            // 必填键存在性(一次性报全,便于一轮修完)
            foreach (string key in ForagingConstantsSchema.RequiredKeys)
                if (!root.TryGet(key, out _))
                    errors.Add($"{FileName}:缺必填键 \"{key}\"");

            // ── ① seasons_per_year ──
            int seasonsPerYear = 0;
            if (ForagingConstantsSchema.ReadRequiredInt(root, "seasons_per_year", FileName, errors, out int spy))
            {
                seasonsPerYear = spy;
                if (seasonsPerYear < 1)
                    errors.Add($"{FileName}:seasons_per_year = {seasonsPerYear} < 1 —— 5 的 SEASONS_PER_YEAR 非法");
            }

            // ── ② cap_table(AC-17-14 四性质)──
            int[] capTable = null;
            if (ForagingConstantsSchema.ReadRequiredIntArray(root, "cap_table", FileName, errors, out int[] ct))
            {
                capTable = ct;
                if (ct.Length != ForagingConstantsSchema.CapTableLength)
                {
                    errors.Add(
                        $"{FileName}:cap_table 长度 {ct.Length} ≠ {ForagingConstantsSchema.CapTableLength} " +
                        "(= SKILL_CAP+1)—— AC-17-14 / GDD `:300`");
                }
                else
                {
                    for (int level = 0; level < ct.Length; level++)
                    {
                        if (ct[level] < 1)
                            errors.Add(
                                $"{FileName}:cap_table[{level}] = {ct[level]} < 1 —— AC-17-14;" +
                                "CapTable[0] = 0 会让 clamp(x, 1, 0) 抛异常");
                        if (ct[level] > maxQuality)
                            errors.Add(
                                $"{FileName}:cap_table[{level}] = {ct[level]} > MAX_QUALITY = {maxQuality} " +
                                "—— AC-17-14(D-21-7 双向耦合)");
                        if (level > 0 && ct[level] < ct[level - 1])
                            errors.Add(
                                $"{FileName}:cap_table 非单调不减:cap_table[{level}] = {ct[level]} < " +
                                $"cap_table[{level - 1}] = {ct[level - 1]} —— AC-17-14(非单调 = 升级反而采到更差)");
                    }
                }
            }

            // ── ③ season_mult(AC-17-16 表长)──
            Fix[] seasonMult = null;
            if (ForagingConstantsSchema.ReadRequiredFixArray(root, "season_mult", FileName, errors, out Fix[] sm))
            {
                seasonMult = sm;
                if (seasonsPerYear >= 1 && sm.Length != seasonsPerYear)
                {
                    errors.Add(
                        $"{FileName}:season_mult 长度 {sm.Length} ≠ seasons_per_year = {seasonsPerYear} " +
                        "—— AC-17-16 / GDD `:355`(不等会让 season_index 越界读)");
                }
                for (int i = 0; i < sm.Length; i++)
                    if (sm[i].Raw <= 0)
                        errors.Add(
                            $"{FileName}:season_mult[{i}] raw = {sm[i].Raw} ≤ 0 —— " +
                            "季节系数非正会让该季节点容量归零或为负(F-17-3 量纲齐一)");
            }

            // ── ④ regrow_window_ticks > 0(AC-17-08b)──
            long regrowWindowTicks = 0;
            if (ForagingConstantsSchema.ReadRequiredLong(
                    root, "regrow_window_ticks", FileName, errors, out long rw))
            {
                regrowWindowTicks = rw;
                if (rw <= 0)
                    errors.Add(
                        $"{FileName}:regrow_window_ticks = {rw} ≤ 0 —— AC-17-08b / GDD `:400`;" +
                        "≤ 0 会让「无再生」退化为「无限采集」");
            }

            // ── ⑤ base_capacity > 0 ──
            int baseCapacity = 0;
            if (ForagingConstantsSchema.ReadRequiredInt(root, "base_capacity", FileName, errors, out int bc))
            {
                baseCapacity = bc;
                if (bc <= 0)
                    errors.Add($"{FileName}:base_capacity = {bc} ≤ 0 —— F-17-3 的容量基数须 > 0");
            }

            // ── ⑥ decay_rate > 0 ──
            Fix decayRate = Fix.Zero;
            if (ForagingConstantsSchema.ReadRequiredFix(root, "decay_rate", FileName, errors, out Fix dr))
            {
                decayRate = dr;
                if (dr.Raw <= 0)
                    errors.Add(
                        $"{FileName}:decay_rate raw = {dr.Raw} ≤ 0 —— F-17-3;" +
                        "0 会让 yieldDecay 恒为 1(深水线消失)");
            }

            // ── ⑦ decay_floor ∈ (0, 1] ──
            Fix decayFloor = Fix.Zero;
            if (ForagingConstantsSchema.ReadRequiredFix(root, "decay_floor", FileName, errors, out Fix df))
            {
                decayFloor = df;
                if (df.Raw <= 0 || df.Raw > Fix.OneRaw)
                    errors.Add(
                        $"{FileName}:decay_floor raw = {df.Raw} ∉ (0, 1.0] —— F-17-3;" +
                        "≤ 0 会让节点触底后归零;> 1 会让衰减项反而放大");
            }

            // ── ⑧ gather_mul_cap ≥ 1 ──
            Fix gatherMulCap = Fix.Zero;
            if (ForagingConstantsSchema.ReadRequiredFix(root, "gather_mul_cap", FileName, errors, out Fix gmc))
            {
                gatherMulCap = gmc;
                if (gmc.Raw < Fix.OneRaw)
                    errors.Add(
                        $"{FileName}:gather_mul_cap raw = {gmc.Raw} < 1.0 —— F-17-2;" +
                        "上界 < 1 会让高技能反而采得更少(双重惩罚)");
            }

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(
                capTable, seasonMult, seasonsPerYear, regrowWindowTicks, baseCapacity,
                decayRate, decayFloor, gatherMulCap, schemaVersion);
        }
    }
}
