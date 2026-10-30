// 权威来源:
//   story production/epics/clinic-machine/story-004-delivery-boundary-clinicenvdto.md
//     (AC-24-07 白名单 · AC-24-01b 配对联测 · ClinicEnvDto 字段完备 ·
//      PresentationDtoGuard 递归扫描 · K_CONTEXT_MAX 经接口上交 · AC-24-09 移交 42)
//   GDD design/gdd/clinic-machine.md —— O-24-3(ClinicEnvDto)· 规则三/四/五(交付三向)·
//     F-24-3(K_CONTEXT_MAX)· EC-24-03(唯一钳制点 = 21a F1,D-21-31)·
//     AC-24-01 注(ENV_MOD_MIN=−1/2 / ENV_MOD_MAX=+1/2,2026-09-25 数值轮已裁)
//   ADR-025 §① —— ClinicEnvDto 住 Sim.Contracts(OQ-CP-4 甲);
//     `Sim` 期望引用集 = {BCL, Sim.Contracts}(b2 门的构建失败级执法体 =
//     `Editor.Tools.Gates/AssemblyGates.cs` CheckGateA;本件 §门A测 是同一性质的 EditMode 断言)
//   🔁 2026-10-30:`IClinicEnvQuery` **已从本件与契约面一并删除**(`TD-clinic-20` 丙案)——
//     它零实装零调用点,且 `GetEnv(int)` 装不下 64 位 `ClinicRoomId`。真实通道 =
//     `ClinicEnvironmentMemo.GetDto()`。本件保留一条「不得复活」的守卫。
//   ADR-013 §三 F7 / AC-37-15 —— PresentationDtoGuard 递归反射扫描(守卫本体不复写,直接复用)
//   21a 侧 clamp 契约 = `Sim/ItemDatabase/RecipeSettlementSolver.EnvModTotal`
//     (D-21-31:两分量各自无域,求和与唯一钳制在该函数执行)
//
// ⚠️ 本件是 story-004 的证据件:只测**契约面**(DTO / 接口 / 守卫 / 白名单 / 交付配对),
//    不复测 story-001/002/003 已交付的求值逻辑细节(表 schema / 析出 / 公式各归其卡)。
//
// ⚠️ 21a clamp 契约**已就绪**(EnvModTotal 公开纯函数,2026-10-25 实测)⇒ 配对联测走
//    **真件**,不需要 QA plan 预设的「接口桩 + Ignore(BLOCKED-BY)」形态 —— 真联测严格强于桩测。
//
// ⚠️ 夹具数值纪律:凡出现的 ±1/2(±32768)· 1/8(8192)等边界值 = GDD 已裁数值轮产物或
//    判别力需要的**域外探针**(承 clinic_modifier_eval_test ⑦ 段同款口径),不是调参发明。

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ClinicMachine
{
    /// <summary>
    /// story-004 交付边界判据:ClinicEnvDto 契约面 + PresentationDtoGuard + AC-24-07 白名单 +
    /// AC-24-01b 配对(24 出 / 21a 钳)+ K_CONTEXT_MAX 表派生上交。
    /// </summary>
    [TestFixture]
    public class ClinicEnvDeliveryTest
    {
        // ══════════════════ 常量(Q16.16 raw;来源见各用例注)══════════════════

        /// <summary>Q16.16 的 1.0(Fix.OneRaw;夹具自明,不引被测常量当判据)。</summary>
        private const long OneRaw = 65536L;
        /// <summary>ENV_MOD_MAX = +1/2(GDD clinic-machine AC-24-01 注,2026-09-25 数值轮已裁)。</summary>
        private const long EnvModMaxRaw = 32768L;
        /// <summary>ENV_MOD_MIN = −1/2(同上)。</summary>
        private const long EnvModMinRaw = -32768L;

        // ══════════════════ 夹具(房析出 / 求值输入,形制照 clinic_modifier_eval_test)══════════════════

        private sealed class FakeView : IPlacedStructureReadView
        {
            private readonly WorldPos[][] _instances;

            public FakeView(params WorldPos[] cells)
            {
                _instances = new WorldPos[cells.Length][];
                for (int i = 0; i < cells.Length; i++) _instances[i] = new[] { cells[i] };
            }

            public int StructureCount => _instances.Length;
            public IReadOnlyList<WorldPos> GetOccupiedCells(int structureIndex) => _instances[structureIndex];
            public string GetModuleId(int structureIndex) => null;
        }

        private sealed class FakeTraits : IClinicFurnitureTraitsSource
        {
            private readonly Dictionary<WorldPos, ClinicFurnitureTraits> _byCell;

            public FakeTraits(IEnumerable<(WorldPos Cell, string Type, int Tier, long EEnvRaw)> furniture)
            {
                _byCell = new Dictionary<WorldPos, ClinicFurnitureTraits>();
                foreach ((WorldPos cell, string type, int tier, long eEnvRaw) in furniture)
                    _byCell[cell] = new ClinicFurnitureTraits(type, tier, new Fix(eEnvRaw));
            }

            public ClinicCellLookup Lookup(WorldPos cell, out ClinicFurnitureTraits traits)
            {
                if (_byCell.TryGetValue(cell, out traits)) return ClinicCellLookup.Furniture;
                traits = default;
                return ClinicCellLookup.Unknown;
            }
        }

        private static readonly WorldPos C0 = new WorldPos(0, 0, 0);

        /// <summary>单格房间,env 分量由 baseEnv + eEnv 直接给足(判别力探针,域外值合法)。</summary>
        private static Fix ClinicEnvOf(long baseEnvRaw, long eEnvRaw)
        {
            ClinicRoomExtractionResult room = ClinicRoomExtraction.Extract(new FakeView(C0));
            var traits = new FakeTraits(new[] { (C0, "probe", 0, eEnvRaw) });
            ClinicAdjTable adj = new ClinicAdjTable(
                new List<ClinicAdjPair>
                {
                    new ClinicAdjPair("__unused_a", "__unused_b", new Fix(0)),
                    new ClinicAdjPair("__unused_b", "__unused_a", new Fix(0)),
                },
                64, new Fix(-1), new Fix(1));
            return ClinicModifierEvaluator.ClinicEnvModOf(room, traits, adj, new Fix(baseEnvRaw));
        }

        /// <summary>21a 常量表夹具:ENV 带取 GDD 已裁 [−1/2, +1/2];其余参数与本测无关,
        /// 取无害值(构造器不做范围校验 —— 校验是独立门,本测不触发)。</summary>
        private static RecipeSettlementConstants EnvConstants()
            => new RecipeSettlementConstants(
                qtyMultMin: new Fix(16384), qtyMultMax: new Fix(262144),
                skillModCap: new Fix(0), qualModCap: new Fix(0), equipModCap: new Fix(0),
                envModMin: new Fix(EnvModMinRaw), envModMax: new Fix(EnvModMaxRaw),
                retainMin: new Fix(OneRaw), retainMax: new Fix(OneRaw),
                effMin: new Fix(OneRaw), effMax: new Fix(OneRaw),
                maxQuality: 2, skillCap: 1);

        /// <summary>独立第二路径的 clamp 神谕(不调用被测函数 —— 差分判据)。</summary>
        private static long ClampOracle(long sum)
        {
            if (sum < EnvModMinRaw) return EnvModMinRaw;
            if (sum > EnvModMaxRaw) return EnvModMaxRaw;
            return sum;
        }

        // ══════════════════ ① ClinicEnvDto 字段完备(raw long · 零 float)══════════════════

        [Test]
        public void test_clinicenvdto_fieldSet_complete_rawLong_noFloat()
        {
            // Arrange / Act:反射枚举 DTO 的公开实例字段(字面量清单 = 闭合契约,新增字段即红)
            var fields = typeof(ClinicEnvDto)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(f => f.Name, f => f.FieldType);

            // Assert:恰四字段,名字与类型逐一对上(O-24-3)
            Assert.That(fields.Count, Is.EqualTo(4), "DTO 字段闭合集;多出/缺失任何一字段 = 红");
            Assert.That(fields.ContainsKey("RoomNameId") && fields["RoomNameId"] == typeof(int),
                "roomName = 词表索引 int");
            Assert.That(fields.ContainsKey("ContextIds") && fields["ContextIds"] == typeof(int[]),
                "contexts[] = 情境词表索引 int[]");
            Assert.That(fields.ContainsKey("EnvMod") && fields["EnvMod"] == typeof(Fix),
                "envMod = Fix(定点)");
            Assert.That(fields.ContainsKey("EquipMod") && fields["EquipMod"] == typeof(Fix),
                "equipMod = Fix(定点)");

            // 定点 raw 的量纲:Fix 的承载是 long(Q16.16),不是 float/double(TR-clinic-003)
            PropertyInfo raw = typeof(Fix).GetProperty("Raw");
            Assert.That(raw, Is.Not.Null, "Fix 须暴露 raw 读口");
            Assert.That(raw.PropertyType, Is.EqualTo(typeof(long)), "Fix.Raw 须为 long");

            // 零浮点:DTO 字段类型树的第一层禁 float/double/decimal
            foreach (KeyValuePair<string, Type> kv in fields)
                Assert.That(
                    kv.Value != typeof(float) && kv.Value != typeof(double) &&
                    kv.Value != typeof(decimal),
                    $"DTO 字段 {kv.Key} 出现浮点类型 {kv.Value} —— 禁 float 进 DTO");
        }

        // ══════════════════ ② PresentationDtoGuard:干净 + 递归活性 ═══════════════════

        /// <summary>AC「递归扫描 ClinicEnvDto:零 disease_id」。
        /// ⚠️ 本测断言**空集**,须与下方负例测配对读(负例测证明守卫活着)。
        /// <para>🔁 **2026-10-30 改(`TD-clinic-20` 结案 · 用户裁定丙案)**:原第二半扫的是
        /// `IClinicEnvQuery` —— 该接口已于本批**删除**(零实装零调用点,且 `GetEnv(int)` 装不下
        /// 64 位 `ClinicRoomId`)。⇒ 第二半改为「**该接口不得复活**」的守卫:
        /// 若有人把 `IClinicEnvQuery` 加回契约面,本测转红,强制他先裁程序集归属
        /// (见债册 `TD-clinic-20` 结案段的甲乙两案)。</para></summary>
        [Test]
        public void test_clinicenvdto_dtoGuard_recursiveScan_clean()
        {
            // Arrange / Act
            IReadOnlyList<string> dtoErrs = PresentationDtoGuard.Scan(typeof(ClinicEnvDto));

            // Assert ①:DTO 本身零疾病语义
            Assert.That(dtoErrs, Is.Empty, "ClinicEnvDto 零疾病语义(AC-37-15 同一守卫)");

            // Assert ②:被删的接口不得复活(复活 = 须先裁程序集归属,不得静默加回)
            var resurrected = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .Where(t => t.Name == "IClinicEnvQuery")
                .ToList();
            Assert.That(resurrected, Is.Empty,
                "`IClinicEnvQuery` 被加回契约面了 —— 该接口 2026-10-30 因「零实装零调用点 + " +
                "`GetEnv(int)` 装不下 64 位 `ClinicRoomId`」已删除(TD-clinic-20 丙案)。" +
                "复活它须先裁 `ClinicRoomId` 的程序集归属(甲/乙案),不得静默加回。");
        }

        /// <summary>守卫活性负例:疾病语义藏在**集合元素类型的嵌套字段**里 —— 只有递归进
        /// List&lt;T&gt; 的元素类型才可能抓到(QA plan AC-2 edge「新增字段即被扫描捕获,
        /// 反射非硬编码字段名清单」)。夹具类型只住本测试文件。</summary>
        [Test]
        public void test_clinicenvdto_dtoGuard_negativeFixture_nestedDiseaseField_caught()
        {
            // Arrange:root 本身零疾病词面;token 在二层(List 元素的字段)
            IReadOnlyList<string> errs = PresentationDtoGuard.Scan(typeof(GuardProbeRoot));

            // Assert
            Assert.That(errs, Is.Not.Empty, "递归扫描必须穿透集合元素类型,否则 = 假绿");
            Assert.That(errs.Any(e => e.Contains("DiseaseId")),
                "错误须点名嵌套字段本身;实际:" + string.Join(" | ", errs));
        }

        /// <summary>负例夹具:**root 无 token、嵌套字段有** —— 本测的判别力全靠递归。</summary>
        private sealed class GuardProbeRoot
        {
            internal List<GuardProbeInner> Frames;
        }

        private sealed class GuardProbeInner
        {
            internal long DiseaseId;
            internal float Height;
        }

        // ══════════════════ ③ AC-24-07:Sim 程序集引用白名单(枚举比对,非 grep)══════════════════

        /// <summary>24 的 sim 程序集(`Sim`)直接引用集 ⊆ {BCL, Sim.Contracts};零引擎程序集。
        /// <para>判据形态 = `Assembly.GetReferencedAssemblies()` **枚举比对**(= b2 门
        /// `AssemblyGates.CheckGateA` 同一性质的 EditMode 断言;构建失败级执法体在门侧,
        /// 本测是 its EditMode 面 ——「恰好界上」的传递引用不进本枚举,GetReferencedAssemblies
        /// 只返回直接引用)。</para></summary>
        [Test]
        public void test_ac2407_simAssembly_references_whitelist_enumeration()
        {
            // Arrange:24 的 sim 代码住 `Sim` 装配(取任一 Sim 类型定位装配)
            Assembly sim = typeof(ClinicModifierEvaluator).Assembly;
            Assert.That(sim.GetName().Name, Is.EqualTo("Sim"), "被测装配须为 Sim");

            // Act:直接引用集枚举(非 grep、非递归 —— 传递引用不计)
            List<string> refs = sim.GetReferencedAssemblies().Select(r => r.Name).ToList();

            // Assert 0:枚举非空(防空集冒充绿)
            Assert.That(refs, Is.Not.Empty, "引用枚举为空 = 扫描面丢失,拒判");

            // Assert 1:正控制 —— Sim.Contracts 必在集内(ADR-025 §① 期望集)
            Assert.That(refs, Does.Contain("Sim.Contracts"), "Sim 须引用 Sim.Contracts");

            // Assert 2:零引擎程序集(ADR-017 §二 门 A)
            foreach (string r in refs)
                Assert.That(
                    !r.StartsWith("UnityEngine") && !r.StartsWith("UnityEditor") &&
                    !r.StartsWith("Unity."),
                    $"门 A 违例:Sim 引用了引擎程序集「{r}」");

            // Assert 3:工程内侧 ⊆ {Sim.Contracts};其余必须是 BCL 基座
            // (BCL 判据与 b2 门 CheckGateA 同口径:System* / netstandard / mscorlib / Mono)
            foreach (string r in refs)
            {
                bool bcl = r.StartsWith("System") || r == "netstandard" ||
                           r == "mscorlib" || r == "Mono";
                Assert.That(bcl || r == "Sim.Contracts",
                    $"AC-24-07 白名单违例:Sim 直接引用「{r}」∉ {{BCL, Sim.Contracts}}");
            }
        }

        // ══════════════════ ④ AC-24-01b:配对联测(24 出 / 21a 钳)══════════════════

        /// <summary>配对前置(防恒真):**未配 clamp 时侧向超界可见** —— 24 侧对超界和原样透传,
        /// 上下两腿都打到 [−1/2, +1/2] 域外。若 24 侧私自 clamp,本测的红先于配对测。</summary>
        [Test]
        public void test_ac2401b_sideVisible_outOfRangePassesThroughUnclamped()
        {
            // Arrange:上腿 = +1400000(≫ +32768);下腿 = −1200000(≪ −32768)
            // Act
            long above = ClinicEnvOf(500000L, 900000L).Raw;
            long below = ClinicEnvOf(-300000L, -900000L).Raw;

            // Assert
            Assert.That(above, Is.EqualTo(1400000L), "超上界须原样出(24 侧无 clamp,D-21-31)");
            Assert.That(above, Is.GreaterThan(EnvModMaxRaw), "夹具坐实:确在域外");
            Assert.That(below, Is.EqualTo(-1200000L), "超下界对称原样出");
            Assert.That(below, Is.LessThan(EnvModMinRaw), "夹具坐实:确在域外");
        }

        /// <summary>AC-24-01b 正腿:24 的域外分量 + climate 分量进 21a `EnvModTotal` ⇒
        /// **在 21a 被钳到 ENV_MOD_MAX**,与手算神谕逐位一致(两侧皆 Q16.16 raw)。</summary>
        [Test]
        public void test_ac2401b_pairing_aboveMax_clampedAt21aSide_bitExact()
        {
            // Arrange:24 侧出域外值(上腿);climate = 1/8
            long clinicRaw = ClinicEnvOf(500000L, 900000L).Raw;
            Assert.That(clinicRaw, Is.EqualTo(1400000L));
            long climateRaw = 8192L;

            // Act:21a F1(真件 —— 求和与唯一钳制都在该函数)
            long clamped = RecipeSettlementSolver
                .EnvModTotal(EnvConstants(), new Fix(climateRaw), new Fix(clinicRaw)).Raw;

            // Assert:逐位 = 手算神谕;且确为饱和值(非透传)
            Assert.That(clamped, Is.EqualTo(ClampOracle(climateRaw + clinicRaw)),
                "21a 侧结果须 = 手算 clamp(climate + clinic)");
            Assert.That(clamped, Is.EqualTo(EnvModMaxRaw), "越上界 ⇒ 钳到 ENV_MOD_MAX = +1/2");
        }

        /// <summary>AC-24-01b 负腿(对称)+ 边界(恰等界上不截):
        /// 超下界钳到 ENV_MOD_MIN;恰好 +1/2 的分量逐位不变形。</summary>
        [Test]
        public void test_ac2401b_pairing_belowMin_symmetric_and_exactBound_notClipped()
        {
            // Arrange:下腿 = 24 出 −1200000;climate = −1/8
            long clinicRaw = ClinicEnvOf(-300000L, -900000L).Raw;
            Assert.That(clinicRaw, Is.EqualTo(-1200000L));

            // Act / Assert 1:负向钳到 ENV_MOD_MIN(对称)
            long clampedLow = RecipeSettlementSolver
                .EnvModTotal(EnvConstants(), new Fix(-8192L), new Fix(clinicRaw)).Raw;
            Assert.That(clampedLow, Is.EqualTo(ClampOracle(-8192L + clinicRaw)));
            Assert.That(clampedLow, Is.EqualTo(EnvModMinRaw), "越下界 ⇒ 钳到 ENV_MOD_MIN = −1/2");

            // Act / Assert 2:恰好界上(clinic = +1/2,climate = 0)⇒ 不截,逐位 == 输入
            long atBound = RecipeSettlementSolver
                .EnvModTotal(EnvConstants(), new Fix(0), new Fix(EnvModMaxRaw)).Raw;
            Assert.That(atBound, Is.EqualTo(EnvModMaxRaw), "恰等界上不截(逐位不变)");
        }

        // ══════════════════ ⑤ K_CONTEXT_MAX:表派生上交(非硬编码)══════════════════

        /// <summary>`K_CONTEXT_MAX = max(K_speed)` 经表的只读交付口上交
        /// (`ClinicContextTable.KContextMax`,F-24-3;构造期已验「头导出 == 按行重算」)。
        /// <para>**非硬编码的机械化** = 双表差分神谕:两张 max 不同的表各自等于独立扫表结果 ——
        /// 若交付口是写死的常量,第二张表必红。档集 {1, 7/8, 3/4} = OQ-24-1 已裁;
        /// 第二张表的 {1/2, 3/4} 是**判别力夹具**,不是调参取值。</para>
        /// <para>1 侧消费端口 = `WorldLatticeParams.kContextMax`(与 6 的 KTerrainMax 同形);
        /// 「1 不自持派生量」已由 locomotion_chain_test AC-1-06a 机械化,本件不重复。</para></summary>
        [Test]
        public void test_kcontextmax_deliveredFromTable_equalsOracleMax_notHardcoded()
        {
            // Arrange:表 A = 已裁档集 {1, 7/8, 3/4};表 B = 判别力夹具 {1/2, 3/4}
            ClinicContextTable tableA = ContextTable(
                (65536L, "ward", "ctx_ward"),
                (57344L, "theater", "ctx_theater"),
                (49152L, "pharmacy", "ctx_pharmacy"));
            ClinicContextTable tableB = ContextTable(
                (49152L, "ward", "ctx_ward"),
                (32768L, "store", "ctx_store"));

            // Act:独立第二路径扫行求 max(不复用被测派生代码)
            long oracleA = MaxKSpeedOracle(tableA);
            long oracleB = MaxKSpeedOracle(tableB);

            // Assert
            Assert.That(tableA.KContextMax.Raw, Is.EqualTo(oracleA),
                "表 A:交付值 == 独立扫表 max(K_speed)");
            Assert.That(tableA.KContextMax.Raw, Is.EqualTo(OneRaw),
                "表 A = 已裁档集 ⇒ K_CONTEXT_MAX = 1(OQ-24-1)");
            Assert.That(tableB.KContextMax.Raw, Is.EqualTo(oracleB),
                "表 B:换表则交付值随表变 —— 非硬编码常量的可观测判据");
            Assert.That(tableB.KContextMax.Raw, Is.EqualTo(49152L), "表 B max = 3/4");

            // 交付口只读(上交面无写通道)
            PropertyInfo p = typeof(ClinicContextTable).GetProperty("KContextMax");
            Assert.That(p, Is.Not.Null);
            Assert.That(p.CanWrite, Is.False, "K_CONTEXT_MAX 交付口须只读");
        }

        private static ClinicContextTable ContextTable(params (long KSpeedRaw, string RoomType, string Ctx)[] rows)
        {
            var list = new List<ClinicContextRow>();
            foreach ((long k, string roomType, string ctx) in rows)
                list.Add(new ClinicContextRow(roomType, ctx, new Fix(k), new Fix(0)));

            // 头导出值 = 独立扫表结果(夹具自己算,不经被测派生路径)
            long max = 0;
            foreach ((long k, _, _) in rows) if (k > max) max = k;
            return new ClinicContextTable(list, new Fix(max));
        }

        private static long MaxKSpeedOracle(ClinicContextTable table)
        {
            long max = 0;
            foreach (ClinicContextRow row in table.Rows)          // 独立第二路径
                if (row.KSpeed.Raw > max) max = row.KSpeed.Raw;
            return max;
        }

        // ══════════════════ ⑥ contexts[] 空 / 满两界 ═══════════════════

        /// <summary>DTO 的 contexts[] 两界:空数组(「印卸 = 不成立」语义,**空数组非 null**)
        /// 与满集(多元素)都按序原样承载(42 只读消费的形状前提)。</summary>
        [Test]
        public void test_clinicenvdto_contexts_emptyAndPopulated_carriedVerbatim()
        {
            // Arrange / Act
            var empty = new ClinicEnvDto(7, new int[0], new Fix(0), new Fix(0));
            var full = new ClinicEnvDto(7, new[] { 3, 1, 2 }, new Fix(8192), new Fix(16384));

            // Assert:空界
            Assert.That(empty.ContextIds, Is.Not.Null, "「可空集」= 空数组,不是 null");
            Assert.That(empty.ContextIds.Length, Is.EqualTo(0));

            // Assert:满界 —— 逐元素按序承载
            Assert.That(full.ContextIds, Is.EqualTo(new[] { 3, 1, 2 }));
            Assert.That(full.EnvMod.Raw, Is.EqualTo(8192L));
            Assert.That(full.EquipMod.Raw, Is.EqualTo(16384L));
            Assert.That(full.RoomNameId, Is.EqualTo(7));
        }
    }
}
