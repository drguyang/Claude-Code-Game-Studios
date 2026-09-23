// 权威来源:
//   Story 002(production/epics/item-database/story-002-schema-types-and-primary-key.md)
//     · AC-21a-21 —— 同 (base_id, processing_state) 复合主键重复 ⇒ 拒(夹具 invalid_dup_key.json)
//     · AC-21a-22 —— category / processing_state 取枚举外字面量 ⇒ 拒(夹具 invalid_enum.json)
//     · AC-21a-27 —— ItemInstance 递归类型图零 UnityEngine(违例夹具 invalid_instance_unity_ref.cs)
//     · AC-21a-48 —— 调参旋钮零硬编码字面量(扫 unity/Assets/**,显式白名单结构常量)
//     · AC-21a-49 —— GDD §Debt Register 每行 owner + 状态;⏳ 行无真名 owner 即失败
//     · AC-21a-59 —— stackable 显式写入即拒(夹具 invalid_stored_stackable.json;派生 = StackMax > 1)
//   GDD:design/gdd/item-database.md §Schema A–F · §Edge Cases · §Debt Register · §Tuning Knobs
//   ADR-014(主):逐 schema 白名单 / 硬失败归烘焙管线 —— 本文件只测可复用纯函数
//   ADR-006(次):weight / stack_max = int 非 Fix;Fix 只经 FixParse
//
// ⚠️ 落点:故事头登记的账本路径 = tests/unit/item_database/schema_types_primary_key_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ 真身 = 本文件(与 Story 001 fix_parse_boundary_test 同一先例;
//    账本与夹具住 tests/unit/item_database/,README 已记落点表)。
//
// ⚠️ 范围(Out of Scope 硬边界):组三其余写入期校验(AC-7~20/23~26 等)归 Story 006/007;
//    烘焙管线接线归 008;Fix 编码器归 010;FixParse 本身归 001(本文件不重写,只间接调用)。
//
// ⚠️ unity-specialist 约束回执(2026-09-23 并入):① schema 类型住 Sim.Contracts /
//    校验·扫描纯函数住 Editor.Tools.Gates(落点修正,见 ItemDbValidation.cs 头注);
//    ② [SerializeReference] 按 FullName 判;③ 递归 visited 防环 / 数组解元素 / Nullable 展开 /
//    接口·抽象·object 直判不递归 / 查 base chain;④ 违例夹具只在测试程序集;
//    ⑤ AC-48 扫描面 = unity/Assets/** 排除 Tests、旋钮值一律入参禁 const、白名单显式;
//    ⑥ [CallerFilePath] 上跳 5 级到仓根 + 先 Assert File.Exists;
//    ⑦ ProcessingState 恰 P0 五值(TryParse 天然拒 P1a);
//    ⑧ Fix 字段只建不编码(归 010);⑨ 校验纯函数无 I/O —— AC-49/48 的文本扫描是测试辅助,
//    住本测试文件(编辑期 GDD/源码断言,不进生产程序集)。
//
// 测试纪律:test_[scenario]_[expected];无随机、无时间依赖;边界值字面量按 coding-standards 例外。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.EditorTools.Gates;

namespace DaYiJingCheng.Tests.Unit.ItemDatabase
{
    [TestFixture]
    internal sealed class SchemaTypesPrimaryKeyTest
    {
        // ══════════════ AC-21a-21:复合主键重复拒绝 ══════════════

        [Test]
        public void test_duplicateCompositeKey_fixture_rejected()
        {
            // Given:QA 指定负向夹具 —— willow_bark/raw 恰两重重复(另两条为合法对照)
            string json = readFixture("invalid_dup_key.json");
            List<ItemKey> keys = zipKeys(
                extractAllValues(json, "base_id"),
                extractAllValues(json, "processing_state"));
            Assert.That(keys.Count, Is.EqualTo(4), "夹具自证:恰 4 条目(否则负向夹具失去意义)");

            // When:构建期 schema 校验(纯函数,008 聚合后硬失败)
            IReadOnlyList<string> errors = ItemDbValidation.FindDuplicateCompositeKeys(keys);

            // Then:硬失败 —— 每个重复键一条错误(恰两重形)
            Assert.That(errors.Count, Is.EqualTo(1),
                "willow_bark/raw 两重重复 ⇒ 恰 1 条错误,逐键报告不逐次刷屏:" +
                string.Join(";", errors));
        }

        [Test]
        public void test_tripleDuplicateKey_rejected()
        {
            // Given:三重重复(QA Edge case「重复恰两条 / 三重重复」的上缘)
            ItemKey k = new ItemKey("willow_bark", ProcessingState.Raw);
            var keys = new[] { k, k, k };

            // When/Then:同一判据 —— 三重重复同样 1 条错误(逐键报告)
            IReadOnlyList<string> errors = ItemDbValidation.FindDuplicateCompositeKeys(keys);
            Assert.That(errors.Count, Is.EqualTo(1),
                "三重重复与两重复用同一唯一性判据:" + string.Join(";", errors));
        }

        [Test]
        public void test_sameBaseDifferentStates_fourEntryForm_passes()
        {
            // Given:同 base_id 四条目形态(§Edge Cases:合法四条目形态)—— 全部来自夹具解析对 + 构造补齐
            string json = readFixture("invalid_dup_key.json");
            List<ItemKey> fromFixture = zipKeys(
                extractAllValues(json, "base_id"),
                extractAllValues(json, "processing_state"));

            var fourEntries = new[]
            {
                new ItemKey("willow_bark", ProcessingState.Raw),
                new ItemKey("willow_bark", ProcessingState.Dried),
                new ItemKey("willow_bark", ProcessingState.Extracted),
                new ItemKey("willow_bark", ProcessingState.Tincture),
            };

            // When/Then:同 base 异 state —— 不是重复,通过
            Assert.That(ItemDbValidation.FindDuplicateCompositeKeys(fourEntries), Is.Empty,
                "同 base_id 四条 P0 state = 复合主键合法形态,不得误报");

            // 夹具内抽取的同 base 异 state 子集(条目 0 raw + 条目 2 dried)亦通过
            var sameBaseDiffState = new[] { fromFixture[0], fromFixture[2] };
            Assert.That(ItemDbValidation.FindDuplicateCompositeKeys(sameBaseDiffState), Is.Empty,
                "夹具对照子集:同 base_id 不同 state 通过");
        }

        [Test]
        public void test_differentBaseSameState_passes()
        {
            // Given:不同 base_id 同 processing_state(§Edge Cases 明文「完全正常」)
            string json = readFixture("invalid_dup_key.json");
            List<ItemKey> keys = zipKeys(
                extractAllValues(json, "base_id"),
                extractAllValues(json, "processing_state"));

            var diffBaseSameState = new[] { keys[0] /* willow_bark/raw */, keys[3] /* aspirin/raw */ };

            // When/Then:通过 —— 主键是 (base_id, state) 二元组,不是 state 单轴
            Assert.That(ItemDbValidation.FindDuplicateCompositeKeys(diffBaseSameState), Is.Empty,
                "不同 base_id 同 state = 完全正常,不得误报");
        }

        [Test]
        public void test_duplicateCompositeKey_itemDefOverload_rejected()
        {
            // Given:解析后记录列表形(008 烘焙管线的实际调用面)
            var defs = new[]
            {
                new ItemDef { BaseId = "aspirin", ProcessingState = ProcessingState.Raw },
                new ItemDef { BaseId = "aspirin", ProcessingState = ProcessingState.Raw },
            };

            // When/Then:记录版重载与键序列版同判据
            IReadOnlyList<string> errors = ItemDbValidation.FindDuplicateCompositeKeys(defs);
            Assert.That(errors.Count, Is.EqualTo(1),
                "ItemDef 重载投影到 ItemKey 后判据不变:" + string.Join(";", errors));
        }

        // ══════════════ AC-21a-22:枚举闭合拒绝 ══════════════

        [Test]
        public void test_enumClosure_fixtureOutsideLiterals_rejected()
        {
            // Given:QA 指定负向夹具 —— 记录0 state 拼写近似 "Raw";记录1 category 枚举外 "medicine"
            string json = readFixture("invalid_enum.json");
            List<string> states = extractAllValues(json, "processing_state");
            List<string> categories = extractAllValues(json, "category");
            Assert.That(states.Count, Is.EqualTo(3));
            Assert.That(categories.Count, Is.EqualTo(3));
            Assert.That(states[0], Is.EqualTo("Raw"), "夹具自证:记录0 state 为大小写近似值");
            Assert.That(categories[1], Is.EqualTo("medicine"), "夹具自证:记录1 category 出闭集");

            // When/Then:逐记录构建期校验
            IReadOnlyList<string> e0 = ItemDbValidation.ValidateEnumClosure(categories[0], states[0]);
            Assert.That(e0.Count, Is.EqualTo(1), "仅 state 非法 ⇒ 恰 1 条:" + string.Join(";", e0));
            Assert.That(e0[0], Does.Contain("processing_state"));

            IReadOnlyList<string> e1 = ItemDbValidation.ValidateEnumClosure(categories[1], states[1]);
            Assert.That(e1.Count, Is.EqualTo(1), "仅 category 非法 ⇒ 恰 1 条:" + string.Join(";", e1));
            Assert.That(e1[0], Does.Contain("category"));

            // 对照:记录2 两个字面量全在闭集内 ⇒ 通过(坏记录不牵连好记录)
            IReadOnlyList<string> e2 = ItemDbValidation.ValidateEnumClosure(categories[2], states[2]);
            Assert.That(e2, Is.Empty, "全枚举内字面量通过:" + string.Join(";", e2));
        }

        [Test]
        public void test_enumClosure_allP0Literals_pass()
        {
            // Given:两个枚举的全部 P0 成员字面量(五 state × 六 category 全组合)
            string[] allStates = { "raw", "dried", "extracted", "tincture", "pill" };
            string[] allCategories = { "material", "drug", "tool", "weapon", "build_part", "food" };
            Assert.That(allStates.Length, Is.EqualTo(5), "P0 闭集恰五值(约束⑦:P1a 不加)");
            Assert.That(allCategories.Length, Is.EqualTo(6));

            // When/Then:全组合通过
            foreach (string category in allCategories)
            {
                foreach (string state in allStates)
                {
                    Assert.That(ItemDbValidation.ValidateEnumClosure(category, state), Is.Empty,
                        $"({category}, {state}) 应在闭集内");
                    Assert.That(ItemDbValidation.TryParseItemCategory(category, out _), Is.True);
                    Assert.That(ItemDbValidation.TryParseProcessingState(state, out _), Is.True);
                }
            }
        }

        [Test]
        public void test_enumClosure_caseEmptyNullInt_rejected()
        {
            // Given/When/Then:QA Edge cases —— 拼写近似 / 空串 / null / int 字面量全拒
            Assert.That(ItemDbValidation.TryParseProcessingState("Raw", out _), Is.False,
                "拼写近似(大小写不符)= 序数精确匹配拒 —— AC-21a-22 边缘");
            Assert.That(ItemDbValidation.TryParseProcessingState("", out _), Is.False, "空串拒");
            Assert.That(ItemDbValidation.TryParseProcessingState(null, out _), Is.False,
                "null(缺失/JSON null)拒 —— 字段可空=否");
            Assert.That(ItemDbValidation.TryParseProcessingState("3", out _), Is.False,
                "int 字面量字符串拒(枚举白名单不含数字形 —— 数据产物层 int 编码扫描另归 AC-21a-26)");
            Assert.That(ItemDbValidation.TryParseProcessingState("honey_fried", out _), Is.False,
                "P1a 字面量天然拒(枚举恰 P0 五值 —— 约束⑦)");

            Assert.That(ItemDbValidation.TryParseItemCategory("MEDICINE", out _), Is.False);
            Assert.That(ItemDbValidation.TryParseItemCategory("", out _), Is.False);
            Assert.That(ItemDbValidation.TryParseItemCategory(null, out _), Is.False);
            Assert.That(ItemDbValidation.TryParseItemCategory("0", out _), Is.False,
                "int 字面量字符串拒");

            // null 双字段:闭合校验报两条(每字段各一)
            IReadOnlyList<string> bothNull = ItemDbValidation.ValidateEnumClosure(null, null);
            Assert.That(bothNull.Count, Is.EqualTo(2),
                "两字段均 null ⇒ 两条错误:" + string.Join(";", bothNull));
        }

        // ══════════════ AC-21a-27:ItemInstance 递归类型图断言 ══════════════

        [Test]
        public void test_itemInstance_typeGraph_cleanPass()
        {
            // Given/When:闭集 POD 正身(instance_id:long / item_key / quality:int / qty:int / children:long[])
            IReadOnlyList<string> violations =
                PodTypeScanner.FindForbiddenReferences(typeof(ItemInstance));

            // Then:零违例 —— children 是 long[](数组解元素后是 BCL 长整型)完全合法
            Assert.That(violations, Is.Empty,
                "ItemInstance 类型图不得含引擎引用/禁形字段:" + string.Join(";", violations));
        }

        [Test]
        public void test_schemaTypes_itemDefAndRecipe_typeGraph_cleanPass()
        {
            // Given/When/Then:本故事新增的其余契约类型同判据(可达类型图一并扫)
            foreach (Type type in new[]
                     {
                         typeof(ItemDef), typeof(Recipe), typeof(DrugProfile),
                         typeof(GatherProfile), typeof(ItemKey),
                     })
            {
                IReadOnlyList<string> violations = PodTypeScanner.FindForbiddenReferences(type);
                Assert.That(violations, Is.Empty,
                    $"{type.Name} 类型图应零违例:" + string.Join(";", violations));
            }
        }

        [Test]
        public void test_invalidUnityRefFixture_directAndNestedFields_flagged()
        {
            // Given:GDD 点名违例夹具 —— 一层直接 Sprite + 二层嵌套 GameObject
            IReadOnlyList<string> violations =
                PodTypeScanner.FindForbiddenReferences(typeof(InvalidInstanceUnityRef));

            // When/Then:两类漏检都要抓到(递归必须走进嵌套类)
            string joined = string.Join(";", violations);
            Assert.That(violations.Count, Is.GreaterThanOrEqualTo(2),
                "一层直接引用 + 二层嵌套引用 ⇒ 至少 2 条:" + joined);
            Assert.That(joined, Does.Contain("Icon"), "一层直接 UnityEngine.Sprite 字段须命中");
            Assert.That(joined, Does.Contain("Prefab"),
                "二层嵌套类内 GameObject 须命中 —— 递归类型图的漏检②(GDD §Schema E)");
        }

        [Test]
        public void test_invalidUnityRefFixture_interfaceAndSerializeReference_flagged()
        {
            // Given:接口字段形态(GDD 漏检③ —— 接口可装任意子类,直判违例不递归)
            IReadOnlyList<string> viaInterface =
                PodTypeScanner.FindForbiddenReferences(typeof(InvalidViaInterface));
            string joinedInterface = string.Join(";", viaInterface);
            Assert.That(viaInterface, Is.Not.Empty, "接口字段必须直判违例");
            Assert.That(joinedInterface, Does.Contain("Holder"), "违例须定位到接口字段本身");

            // Given:[SerializeReference] + object 双违例形态(按 FullName 判,零引擎引用约束②)
            IReadOnlyList<string> viaSerializeRef =
                PodTypeScanner.FindForbiddenReferences(typeof(InvalidSerializeReference));
            string joinedSerializeRef = string.Join(";", viaSerializeRef);
            Assert.That(viaSerializeRef, Is.Not.Empty, "[SerializeReference]/object 字段必须违例");
            Assert.That(joinedSerializeRef, Does.Contain("SerializeReference"),
                "违例信息须点名 [UnityEngine.SerializeReference] 判据:" + joinedSerializeRef);
        }

        [Test]
        public void test_invalidUnityRefHolderImpl_spriteField_flagged()
        {
            // Given:接口的具体实现类直接入扫 —— 子类内塞 Sprite(GDD 漏检③ 的子类携带半边)
            IReadOnlyList<string> violations =
                PodTypeScanner.FindForbiddenReferences(typeof(InvalidSpriteHolderImpl));

            // When/Then:递归进类字段命中 Sprite
            string joined = string.Join(";", violations);
            Assert.That(violations, Is.Not.Empty, "实现类内 Sprite 字段须命中");
            Assert.That(joined, Does.Contain("Sprite"), "违例须定位到 Sprite 字段:" + joined);
        }

        [Test]
        public void test_invalidUnityRefFixture_genericContainer_flagged()
        {
            // Given(F2 修回归):字段类型是 System.Collections.* 泛型容器,实参携带引擎引用 ——
            // 修前 Unwrap 只解数组/Nullable ⇒ 实参被 IsNonProjectSystemType 跳过、静默漏检
            IReadOnlyList<string> violations =
                PodTypeScanner.FindForbiddenReferences(typeof(InvalidViaGenericContainer));
            string joined = string.Join(";", violations);

            // When/Then:三个携带实参的字段都要抓到;纯 BCL(含嵌套)对照字段不得误报
            Assert.That(violations.Count, Is.GreaterThanOrEqualTo(3),
                "List<Sprite> + Dictionary<, GameObject> + 嵌套 Dictionary<, List<Sprite>> ⇒ 至少 3 条:" +
                joined);
            Assert.That(joined, Does.Contain("Attachments"),
                "List<Sprite> 实参须命中(F2 泛型盲区):" + joined);
            Assert.That(joined, Does.Contain("Sprite"),
                "违例须点名实参类型 Sprite:" + joined);
            Assert.That(joined, Does.Contain("PrefabMap"),
                "Dictionary 第二实参 GameObject 须命中(F2 泛型盲区):" + joined);
            Assert.That(joined, Does.Contain("GameObject"),
                "违例须点名实参类型 GameObject:" + joined);
            Assert.That(joined, Does.Contain("NestedList"),
                "嵌套容器内层 List<Sprite> 须命中(F7 回环缺口):" + joined);
            Assert.That(joined, Does.Not.Contain("Ids"),
                "对照字段 List<long> 全 BCL 实参必须干净 —— 解实参不得误伤:" + joined);
            Assert.That(joined, Does.Not.Contain("CleanNested"),
                "对照字段 Dictionary<, List<long>> 纯 BCL 嵌套必须干净(F7 不误报):" + joined);
            Assert.That(violations.Count, Is.EqualTo(3),
                "Ids + CleanNested 均被跳过 ⇒ 恰 3 条(逐携带字段一判):" + joined);
        }

        [Test]
        public void test_invalidUnityRefFixture_abstractField_flagged()
        {
            // Given(F3 修):字段类型是抽象类 —— QA Given 点名「接口/抽象」,接口侧已有,
            // 本函数补抽象侧单证(PodTypeScanner 直判分支:IsInterface || IsAbstract || object)
            IReadOnlyList<string> violations =
                PodTypeScanner.FindForbiddenReferences(typeof(InvalidViaAbstract));

            // When/Then:直判命中(不递归也必须报)
            string joined = string.Join(";", violations);
            Assert.That(violations, Is.Not.Empty, "抽象类字段必须直判违例");
            Assert.That(joined, Does.Contain("Holder"),
                "违例须定位到抽象字段本身:" + joined);
            Assert.That(joined, Does.Contain("抽象"),
                "违例信息须点名接口/抽象/object 判据:" + joined);
        }

        // ══════════════ AC-21a-48:无硬编码调参旋钮 ══════════════

        [Test]
        public void test_tuningKnobIdentifiers_hardcodedInSourceCode_none()
        {
            // Given:GDD §Tuning Knobs 旋钮全集(从权威表**解析**,不手抄 —— 表增行自动进扫描面)
            string gdd = readGdd();
            List<string> knobs = extractKnobIdentifiers(gdd);
            Assert.That(knobs.Count, Is.GreaterThanOrEqualTo(20),
                "§Tuning Knobs 至少应解析出 20 个标识符,实际 " + knobs.Count +
                " —— 解析器与 GDD 表结构失配?");

            // 显式白名单(结构常量;每条带理由 —— 不静默放行任何旋钮)
            string[] whitelist = structuralKnobWhitelist();

            // When:扫 unity/Assets/** 的 C# 源(排除 Tests 装配;注释剥离 —— 判据是「当字面量用」,
            // 文档性提及不算;先例 = AssemblyGates.CheckToFloatCallsites 的 File.ReadAllLines 文本扫)
            var violations = new List<string>();
            string scanRoot = Path.Combine(repoRoot(), "unity", "Assets");
            Assert.That(Directory.Exists(scanRoot), Is.True, $"扫描根缺失:{scanRoot}");

            foreach (string file in Directory.GetFiles(scanRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.IndexOf("/Assets/Tests/", StringComparison.Ordinal) >= 0)
                    continue; // 测试装配排除(边界值字面量按 coding-standards 例外 + 约束⑤)

                string code = stripComments(File.ReadAllText(file));
                string relative = normalized.Substring(normalized.IndexOf("/Assets/", StringComparison.Ordinal));

                foreach (string knob in knobs)
                {
                    if (Array.IndexOf(whitelist, knob) >= 0)
                        continue;
                    if (code.IndexOf(knob, StringComparison.Ordinal) >= 0)
                        violations.Add($"{relative}: 旋钮「{knob}」以字面量出现在代码(AC-21a-48)");
                }
            }

            // Then:零命中
            Assert.That(violations, Is.Empty,
                "调参旋钮不得以字面量出现在 src 侧代码(全部经 作者态 JSON → 烘焙 → IDataProvider 通路," +
                "008 前该通路未建,本门先守「不在代码里当字面量用」):\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void test_tuningKnobWhitelist_neverCoversGlobalKnobs()
        {
            // Given:结构常量白名单(显式)与**核心全局旋钮清单**
            string[] whitelist = structuralKnobWhitelist();
            string[] neverWhitelisted =
            {
                "EFF_MIN", "EFF_MAX",
                "QTY_MULT_MIN", "QTY_MULT_MAX",
                "SKILL_MOD_CAP", "QUAL_MOD_CAP", "EQUIP_MOD_CAP",
                "ENV_MOD_MIN", "ENV_MOD_MAX",
                "RETAIN_MIN", "RETAIN_MAX",
                "MAX_QUALITY", "ROUND_MODE",
                "quality_axis", "axis_offset_by_quality",
                "quality_character", "drug_quality_character",
                "drug_potency", "weight_unit",
                "stack_max", "weight",
                "F5 偏移可感知地板", "drug_profile.<时间轴四字段>",
                // F6 修(2026-09-23 双评审):GDD 表里的**括号/点形**行名 —— extractKnobIdentifiers
                // 会把它们作为独立 token 收进 knobs(反引号原形),只锁裸名 ⇒ 白名单塞这些形态锁不住。
                "axis_offset_by_quality[]",
                "quality_character[]", "drug_quality_character[]",
                "ItemDef.stack_max", "ItemDef.weight",
            };

            // When/Then:任一核心旋钮出现在白名单 = 「静默放行旋钮」—— 直接红(QA Edge 明文禁)
            foreach (string knob in neverWhitelisted)
            {
                Assert.That(Array.IndexOf(whitelist, knob), Is.LessThan(0),
                    $"白名单不得放行核心旋钮「{knob}」—— 结构常量与旋钮的边界必须显式(AC-21a-48 Edge)");
            }

            // 同时自证:白名单里确实写出了结构常量(不是空表蒙混)
            Assert.That(Array.IndexOf(whitelist, "ActualConsumed"), Is.GreaterThanOrEqualTo(0),
                "载荷字段名 ActualConsumed 必须在白名单(registry schema 字段,非旋钮值)");
            Assert.That(Array.IndexOf(whitelist, "half_life"), Is.GreaterThanOrEqualTo(0),
                "quality_axis 枚举字面量 half_life 必须在白名单(QA Edge 点名的结构常量)");
        }

        // ══════════════ AC-21a-49:Debt Register 每行 owner + 状态 ══════════════

        [Test]
        public void test_debtRegister_gddAllRows_ownerAndStatus_present()
        {
            // Given:真 GDD §Debt Register(D-21-*/D-9-* 全表)
            string gdd = readGdd();

            // When:解析台账行
            List<string> errors = debtRegisterErrors(gdd, out int rowCount);

            // Then:全表通过(先已抽查现状可绿;若上游加行变红 = AC 设计意图「新增行无 owner 立即红」)
            Assert.That(rowCount, Is.GreaterThanOrEqualTo(20),
                $"台账解析行数 {rowCount} 过少 —— 解析器与表结构失配?");
            Assert.That(errors, Is.Empty,
                "§Debt Register 每行须有 owner + 状态标记,⏳ 行 owner 须为真名:\n" +
                string.Join("\n", errors));
        }

        [Test]
        public void test_debtRegister_pendingRowWithoutOwner_fails()
        {
            // Given:代码构造夹具段(QA 原文「代码构造夹具段」)—— ⏳ 行归属列为空
            string segment =
                "### §Debt Register(唯一台账)\n" +
                "| # | 债务 | 归属 | 状态 |\n" +
                "| --- | --- | --- | --- |\n" +
                "| **D-21-X1** | 示例债甲 |  | ⏳ 待办 |\n";

            // When:解析台账行
            List<string> errors = debtRegisterErrors(segment, out _);

            // Then:失败 —— 欠账不许匿名
            Assert.That(errors, Is.Not.Empty, "⏳ 行无 owner 必须失败");
            Assert.That(string.Join(";", errors), Does.Contain("D-21-X1"),
                "错误须点名到行:" + string.Join(";", errors));
        }

        [Test]
        public void test_debtRegister_pendingRowDashOwner_fails()
        {
            // Given:⏳ 行 owner 写「—」(规避占位不算 owner —— QA Edge)
            string segment =
                "### §Debt Register(唯一台账)\n" +
                "| # | 债务 | 归属 | 状态 |\n" +
                "| --- | --- | --- | --- |\n" +
                "| **D-21-X2** | 示例债乙 | — | ⏳ 待办 |\n";

            // When/Then:失败 —— 「以 ⏳ 行必须有真名 owner 为准」
            List<string> errors = debtRegisterErrors(segment, out _);
            Assert.That(errors, Is.Not.Empty, "⏳ 行 owner = 「—」必须失败(规避号不算真名)");
            Assert.That(string.Join(";", errors), Does.Contain("D-21-X2"));
        }

        [Test]
        public void test_debtRegister_headerAndSeparatorRows_skipped()
        {
            // Given:仅表头 + 分隔行 + 两条合规行(表头/分隔不计入数据行)
            string segment =
                "### §Debt Register(唯一台账)\n" +
                "| # | 债务 | 归属 | 状态 |\n" +
                "| --- | --- | --- | --- |\n" +
                "| **D-21-X3** | 示例债丙 | 21a | ✅ 已办 |\n" +
                "| **D-9-X** | 示例债丁 | ADR-006 | 🟡 修法已落盘 |\n";

            // When/Then:数据行恰 2、零错误(表头分隔不误报「缺 owner」)
            List<string> errors = debtRegisterErrors(segment, out int rowCount);
            Assert.That(rowCount, Is.EqualTo(2), "表头与分隔行不计数据行");
            Assert.That(errors, Is.Empty, "合规段通过:" + string.Join(";", errors));
        }

        [Test]
        public void test_debtRegister_dashOwnerOnClosedRow_tolerated()
        {
            // Given:owner = 「—」但状态**非** ⏳(QA Edge:以「⏳ 行必须有真名 owner」为准 ——
            // 已结案行的规避号不触发匿名禁令;行仍须有非空 owner 单元格与状态标记)
            string segment =
                "### §Debt Register(唯一台账)\n" +
                "| # | 债务 | 归属 | 状态 |\n" +
                "| --- | --- | --- | --- |\n" +
                "| **D-21-X4** | 示例债戊 | — | ✅ 已办 |\n";

            // When/Then:通过(该边缘的口径 = 严格真名要求只锚在 ⏳ 行)
            List<string> errors = debtRegisterErrors(segment, out int rowCount);
            Assert.That(rowCount, Is.EqualTo(1));
            Assert.That(errors, Is.Empty, "非 ⏳ 行 owner=「—」按 QA 口径容忍:" + string.Join(";", errors));
        }

        [Test]
        public void test_debtRegister_closedRowMissingStatus_fails()
        {
            // Given:任何行(此处取已结案行)状态列为空 —— 「每行都有 owner 与状态标记」
            string segment =
                "### §Debt Register(唯一台账)\n" +
                "| # | 债务 | 归属 | 状态 |\n" +
                "| --- | --- | --- | --- |\n" +
                "| **D-21-X5** | 示例债己 | 21a |  |\n";

            // When/Then:失败 —— 状态标记是全行义务,不只 ⏳ 行
            List<string> errors = debtRegisterErrors(segment, out _);
            Assert.That(errors, Is.Not.Empty, "缺状态标记必须失败");
            Assert.That(string.Join(";", errors), Does.Contain("D-21-X5"));
        }

        // ══════════════ AC-21a-59:stackable 显式写入即拒 ══════════════

        [Test]
        public void test_storedStackable_fixtureRecords_rejected()
        {
            // Given:QA 指定负向夹具 —— 三条记录 stackable:true / false / null(每条一行)
            string json = readFixture("invalid_stored_stackable.json");
            List<string> recordLines = recordKeyLines(json);
            Assert.That(recordLines.Count, Is.EqualTo(4), "夹具自证:恰 4 条记录行");

            // When/Then:前三条显式含 stackable 键 ⇒ 逐条拒(值无关 —— true/false 两形都要抓到)
            for (int i = 0; i < 3; i++)
            {
                List<string> keys = extractKeysFromObjectLine(recordLines[i]);
                Assert.That(keys, Does.Contain("stackable"),
                    $"夹具自证:记录 {i} 应含 stackable 键(否则负向夹具失去意义)");
                IReadOnlyList<string> errors =
                    ItemDbValidation.FindStoredStackableKeys(keys, "fixture[" + i + "]");
                Assert.That(errors.Count, Is.EqualTo(1),
                    $"记录 {i} 显式 stackable ⇒ 拒(值无关):" + string.Join(";", errors));
            }

            // 对照:末条无该键 ⇒ 通过
            IReadOnlyList<string> clean = ItemDbValidation.FindStoredStackableKeys(
                extractKeysFromObjectLine(recordLines[3]), "fixture[3]");
            Assert.That(clean, Is.Empty, "无 stackable 键的记录通过:" + string.Join(";", clean));
        }

        [Test]
        public void test_storedStackable_nullValueKeyPresent_rejected()
        {
            // Given:字段为 null 而非缺失(QA Edge「显式写入即拒」的 null 半边)—— 夹具第 3 条
            string json = readFixture("invalid_stored_stackable.json");
            List<string> recordLines = recordKeyKeyLinesGuard(json, expectIndex: 2);

            // 键集校验只看**键存在**,值(null/true/false)在两级缝隙的原始键集侧已被抽掉
            List<string> keys = extractKeysFromObjectLine(recordLines[2]);
            Assert.That(keys, Does.Contain("stackable"), "null 值记录仍有键 —— 键在即拒");

            // When/Then:拒
            IReadOnlyList<string> errors =
                ItemDbValidation.FindStoredStackableKeys(keys, "nullValue");
            Assert.That(errors.Count, Is.EqualTo(1),
                "键存在(值 null)同样拒 —— 键存在 = 显式写入:" + string.Join(";", errors));
        }

        [Test]
        public void test_storedStackable_absentKey_passes()
        {
            // Given:无 stackable 键的键集(夹具末条 + 代码构造形)
            string json = readFixture("invalid_stored_stackable.json");
            List<string> recordLines = recordKeyLines(json);
            List<string> fixtureCleanKeys = extractKeysFromObjectLine(recordLines[3]);
            var codeConstructed = new List<string> { "base_id", "stack_max", "weight", "deprecated" };

            // When/Then:通过 —— 派生值从 ItemDef.Stackable 读
            Assert.That(ItemDbValidation.FindStoredStackableKeys(fixtureCleanKeys), Is.Empty);
            Assert.That(ItemDbValidation.FindStoredStackableKeys(codeConstructed), Is.Empty,
                "键集无 stackable ⇒ 通过(008 绑定层须把原始键集一并交来 —— 两级缝隙登记在校验器文档)");
        }

        [Test]
        public void test_stackable_derivedFromStackMax_notStored()
        {
            // Given:派生属性只依赖 stack_max(QA Edge 两个漂移最恶劣形)
            var nonStackable = new ItemDef { BaseId = "bandage", StackMax = 1 };
            var stackable = new ItemDef { BaseId = "willow_bark", StackMax = 99 };

            // When/Then:派生值
            Assert.That(nonStackable.Stackable, Is.False, "stack_max = 1 ⇒ 不可堆叠(合法)");
            Assert.That(stackable.Stackable, Is.True, "stack_max > 1 ⇒ 可堆叠");

            // 漂移形一:stackable:true + stack_max:1 —— 派生 false,但键在 ⇒ 校验仍拒
            IReadOnlyList<string> driftTrue =
                ItemDbValidation.FindStoredStackableKeys(
                    new[] { "base_id", "stackable", "stack_max" }, "driftTrue");
            Assert.That(driftTrue.Count, Is.EqualTo(1),
                "最恶劣漂移形:显式 true 与派生 false 并存 —— 键在即拒,不看值");
            Assert.That(nonStackable.Stackable, Is.False, "同时派生恒 = stack_max > 1");

            // 漂移形二:stackable:false + stack_max:99 —— 派生 true,键在仍拒
            IReadOnlyList<string> driftFalse =
                ItemDbValidation.FindStoredStackableKeys(
                    new[] { "base_id", "stackable", "stack_max" }, "driftFalse");
            Assert.That(driftFalse.Count, Is.EqualTo(1),
                "显式 false 与派生 true 并存 —— 键在即拒");
            Assert.That(stackable.Stackable, Is.True, "派生不受存储值影响");

            // 反证:同一 ItemDef 形状、键集去掉 stackable ⇒ 通过(拒绝的是「存储」不是「值」)
            IReadOnlyList<string> cleanShape =
                ItemDbValidation.FindStoredStackableKeys(
                    new[] { "base_id", "stack_max" }, "clean");
            Assert.That(cleanShape, Is.Empty, "去掉键即通过 —— 派生量走属性不走数据");
        }

        // ══════════════ 附加硬约束守卫(超出 6 条 AC,服务 Story 头 BLOCKING 纪律)══════════════

        [Test]
        public void test_schemaTypes_noFloatingPointFields()
        {
            // Given:全部本故事 schema 类型
            Type[] types =
            {
                typeof(ItemDef), typeof(DrugProfile), typeof(GatherProfile), typeof(TcmProfile),
                typeof(ItemInstance), typeof(Recipe), typeof(RecipeEntry), typeof(ItemKey),
                typeof(DoseRange), typeof(QualityDistribution), typeof(ProcessingTransition),
            };

            foreach (Type type in types)
            {
                // When:实例字段 + 属性逐个查(数组/Nullable 解包)
                foreach (FieldInfo field in type.GetFields(
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    Type effective = unwrapType(field.FieldType);
                    Assert.That(effective == typeof(float) || effective == typeof(double), Is.False,
                        $"{type.Name}.{field.Name} 是浮点 —— schema 硬约束:Fix 走定点、计数走 int(ADR-006)");
                    if (effective == typeof(Fix))
                        Assert.That(type, Is.EqualTo(typeof(DrugProfile)),
                            $"Fix 字段只允许住 drug_profile(§Schema);{type.Name} 出现 Fix");
                }

                foreach (PropertyInfo property in type.GetProperties(
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    Type effective = unwrapType(property.PropertyType);
                    Assert.That(effective == typeof(float) || effective == typeof(double), Is.False,
                        $"{type.Name}.{property.Name} 是浮点 —— schema 硬约束(ADR-006)");
                    if (effective == typeof(Fix))
                        Assert.That(type, Is.EqualTo(typeof(DrugProfile)),
                            $"Fix 字段只允许住 drug_profile;{type.Name} 出现 Fix");
                }
            }
        }

        [Test]
        public void test_itemInstance_closedPod_exactFiveFields()
        {
            // Given/When:§Schema E 闭集 —— 恰五字段,名字与类型逐一对上
            FieldInfo[] fields = typeof(ItemInstance).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var byName = new Dictionary<string, Type>();
            foreach (FieldInfo field in fields)
                byName[field.Name] = field.FieldType;

            // Then:恰五个实例字段(instance_id / item_key / quality / qty / children)
            Assert.That(byName.Count, Is.EqualTo(5),
                "闭集纯 POD 恰 5 字段,实得 " + byName.Count + ":" + string.Join(",", byName.Keys));
            Assert.That(byName["InstanceId"], Is.EqualTo(typeof(long)));
            Assert.That(byName["Key"], Is.EqualTo(typeof(ItemKey)));
            Assert.That(byName["Quality"], Is.EqualTo(typeof(int)));
            Assert.That(byName["Qty"], Is.EqualTo(typeof(int)));
            Assert.That(byName["Children"], Is.EqualTo(typeof(long[])), "children 承载 = long[]");

            // children 非空禁 null:ctor 对 null 抛(空数组语义在构造入口钉死)
            ItemKey key = new ItemKey("willow_bark", ProcessingState.Raw);
            Assert.Throws<ArgumentNullException>(
                () => new ItemInstance(1L, key, 1, 1, null),
                "children = null 必须拒 —— 非容器传 Array.Empty<long>()(§Schema E)");
            Assert.DoesNotThrow(
                () => new ItemInstance(1L, key, 1, 1, Array.Empty<long>()),
                "空数组 = 非容器合法形");
        }

        [Test]
        public void test_drugProfile_noUnitySerializationAttributes()
        {
            // Given:D-21-18 禁形 —— Fix 承载类型不得引导 Unity 序列化(编码器归 Story 010,
            // 本故事只守住「没被标上去」这个静态面)
            assertNoEngineOrSerializableAttribute(typeof(DrugProfile));
            assertNoEngineOrSerializableAttribute(typeof(ItemDef));

            // 属性与字段逐一查(Fix 字段所在类型的成员面)
            foreach (MemberInfo member in typeof(DrugProfile).GetMembers(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.DeclaredOnly))
            {
                assertMemberHasNoEngineAttribute(member);
            }
        }

        // ══════════════ helpers(测试辅助 —— 住测试程序集,不进生产:约束⑨)══════════════

        /// <summary>从测试文件上跳 5 级到仓根(unity/Assets/Tests/EditMode/ItemDatabase → 仓根;
        /// 承 Story 001 [CallerFilePath] 先例,层级已核)。</summary>
        private static string repoRoot([CallerFilePath] string thisFile = "")
        {
            string dir = Path.GetDirectoryName(thisFile) ?? ".";
            return Path.GetFullPath(Path.Combine(
                dir, "..", "..", "..", "..", ".."));
        }

        /// <summary>读 QA 指定负向夹具(缺文件 = 断言红,不 skip —— 约束⑥)。</summary>
        private static string readFixture(string fileName)
        {
            string path = Path.Combine(
                repoRoot(), "tests", "unit", "item_database", "fixtures", fileName);
            Assert.That(File.Exists(path), Is.True, $"负向夹具缺失(Story 002 QA 指定):{path}");
            return File.ReadAllText(path);
        }

        /// <summary>读 GDD 真源(AC-48/49 的解析对象;缺文件 = 断言红)。</summary>
        private static string readGdd()
        {
            string path = Path.Combine(repoRoot(), "design", "gdd", "item-database.md");
            Assert.That(File.Exists(path), Is.True, $"GDD 缺失:{path}");
            return File.ReadAllText(path);
        }

        /// <summary>按出现序抽全部 `"key": value` 的 value(带引号去引号)。
        /// 手写抽取 —— 工程未装 Newtonsoft(承 Story 001 同型;JSON 数字/字符串消歧归 008)。</summary>
        private static List<string> extractAllValues(string json, string key)
        {
            var results = new List<string>();
            var regex = new Regex(
                "\"" + Regex.Escape(key) + "\"\\s*:\\s*(\"(?:[^\"\\\\]|\\\\.)*\"|[^\\s,}\\]]+)",
                RegexOptions.Compiled);
            foreach (Match match in regex.Matches(json))
            {
                string raw = match.Groups[1].Value;
                if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                    raw = raw.Substring(1, raw.Length - 2);
                results.Add(raw);
            }

            return results;
        }

        /// <summary>把平行的 base_id / processing_state 值序列 zip 成复合主键(解析走闭集白名单)。</summary>
        private static List<ItemKey> zipKeys(List<string> baseIds, List<string> states)
        {
            Assert.That(baseIds.Count, Is.EqualTo(states.Count),
                "夹具两列长度失配 —— 逐条目必含两键");

            var keys = new List<ItemKey>();
            for (int i = 0; i < baseIds.Count; i++)
            {
                Assert.That(
                    ItemDbValidation.TryParseProcessingState(states[i], out ProcessingState state),
                    Is.True, $"夹具第 {i} 条 processing_state「{states[i]}」应可解析(负向夹具只在指定字段出错)");
                keys.Add(new ItemKey(baseIds[i], state));
            }

            return keys;
        }

        /// <summary>取夹具内逐条记录行(含 `"base_id"` 的行 —— 夹具一记录一行)。</summary>
        private static List<string> recordKeyLines(string json)
        {
            var lines = new List<string>();
            foreach (string line in json.Split('\n'))
            {
                if (line.IndexOf("\"base_id\"", StringComparison.Ordinal) >= 0)
                    lines.Add(line);
            }

            return lines;
        }

        /// <summary>recordKeyLines 的显式索引护栏(自证夹具形状;expectIndex 为待测记录下标)。</summary>
        private static List<string> recordKeyKeyLinesGuard(string json, int expectIndex)
        {
            List<string> lines = recordKeyLines(json);
            Assert.That(lines.Count, Is.GreaterThan(expectIndex),
                $"夹具记录行数不足 —— 期待至少 {expectIndex + 1} 行");
            return lines;
        }

        /// <summary>抽一行对象文本内的全部键名(`"key":` 形)。</summary>
        private static List<string> extractKeysFromObjectLine(string line)
        {
            var keys = new List<string>();
            foreach (Match match in Regex.Matches(line, "\"([A-Za-z_][A-Za-z0-9_]*)\"\\s*:"))
                keys.Add(match.Groups[1].Value);
            return keys;
        }

        // ── AC-21a-49:§Debt Register 解析器(文档断言;QA「代码构造夹具段」同函数复用)──

        /// <summary>解析 markdown 台账段,产出逐行错误(空 = 通过)并回传数据行数。
        /// 规则:表头/分隔行跳过;每行恰 4 列;归属非空;状态非空;**⏳ 行 owner 不得为「—」类规避占位**。
        /// <para>F1 修(2026-09-23 双评审):锚点必须是 **markdown 标题形态** <c>^#{1,6} ... Debt Register</c>
        /// —— 真 GDD 首个 "§Debt Register" 字面命中是 :22 的交叉引用指针行(非标题),
        /// 旧 IndexOf 锚把扫描区落在零表行处 ⇒ rowCount=0;代码段夹具同样以标题行起段,
        /// 与真表走**同一解析路径**(无特例分支)。</para></summary>
        private static List<string> debtRegisterErrors(string markdown, out int dataRowCount)
        {
            var errors = new List<string>();
            dataRowCount = 0;

            // 锚真正的节标题行(标题形态;真 GDD 唯一命中 = 「### §Debt Register(唯一台账)」)
            string[] lines = markdown.Split('\n');
            int sectionStart = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (Regex.IsMatch(lines[i].TrimEnd('\r'), @"^#{1,6}\s+\S.*Debt Register"))
                {
                    sectionStart = i;
                    break;
                }
            }

            if (sectionStart < 0)
            {
                errors.Add("缺 Debt Register 标题行(# 形态)—— 台账不可解析");
                return errors;
            }

            // 节 = 标题行之后直到下一标题行(任意 # 级)
            for (int lineIdx = sectionStart + 1; lineIdx < lines.Length; lineIdx++)
            {
                int lineNumber = lineIdx + 1; // 1-based 行号(诊断用)
                string line = lines[lineIdx].TrimEnd('\r');

                if (line.Length > 0 && line[0] == '#')
                    break; // 下一节开始(标题行本身已在 sectionStart 跳过,不会误断)

                if (!line.StartsWith("|"))
                    continue;

                string[] cells = line.Trim().Trim('|').Split('|');
                for (int cellIdx = 0; cellIdx < cells.Length; cellIdx++)
                    cells[cellIdx] = cells[cellIdx].Trim();

                // 表头行
                if (cells.Length > 0 && cells[0] == "#")
                    continue;
                // 分隔行(全为 - : 空格)
                if (cells.Length > 0 && cells[0].Length > 0 &&
                    Regex.IsMatch(cells[0], "^[-:\\s]+$"))
                    continue;

                if (cells.Length < 4)
                {
                    errors.Add($"L{lineNumber}:列数 {cells.Length} < 4(表结构失配):{line}");
                    continue;
                }
                if (cells.Length > 4)
                {
                    errors.Add($"L{lineNumber}:列数 {cells.Length} > 4(单元格含未转义 |?):{line}");
                    continue;
                }

                dataRowCount++;
                string id = cells[0];
                string owner = cells[2];
                string status = cells[3];
                string where = string.IsNullOrEmpty(id) ? $"L{lineNumber}" : id;

                if (id.Length == 0)
                    errors.Add($"L{lineNumber}:行缺 # 列(债务编号)");
                if (owner.Length == 0)
                {
                    errors.Add($"{where}:缺归属(owner)—— 欠账不许匿名(AC-21a-49)");
                    continue; // 无 owner 时 ⏳ 判据无从谈起
                }

                if (status.Length == 0)
                {
                    errors.Add($"{where}:缺状态标记(AC-21a-49)");
                    continue;
                }

                if (status.IndexOf("⏳", StringComparison.Ordinal) >= 0 && isPlaceholderOwner(owner))
                    errors.Add($"{where}:⏳ 行 owner「{owner}」是规避占位 —— 须真名(AC-21a-49)");
            }

            if (dataRowCount == 0 && errors.Count == 0)
                errors.Add("§Debt Register 解析出 0 个数据行 —— 表结构失配?");

            return errors;
        }

        private static bool isPlaceholderOwner(string owner)
        {
            switch (owner)
            {
                case "—":
                case "-":
                case "–":
                case "─":
                    return true;
                default:
                    return false;
            }
        }

        // ── AC-21a-48:旋钮标识符解析 + 注释剥离 + 结构常量白名单 ──

        /// <summary>从 GDD §Tuning Knobs 表第一列解析旋钮标识符(反引号 token 逐个取;
        /// `A / B` 同行拆分;`ItemDef.x` 追加裸后缀;`foo[]` 追加去括号形;非反引号中文名整取)。
        /// **从权威表动态解析,不手抄** —— 表增行自动进扫描面。</summary>
        private static List<string> extractKnobIdentifiers(string gdd)
        {
            int start = gdd.IndexOf("## Tuning Knobs", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "GDD 缺 ## Tuning Knobs 节");

            int end = gdd.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
            string section = end > start ? gdd.Substring(start, end - start) : gdd.Substring(start);

            var knobs = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void add(string token)
            {
                token = token.Trim();
                if (token.Length == 0)
                    return;
                if (seen.Add(token))
                    knobs.Add(token);
            }

            foreach (string rawLine in section.Split('\n'))
            {
                string line = rawLine.Trim();
                if (!line.StartsWith("|"))
                    continue;

                string[] cells = line.Trim().Trim('|').Split('|');
                if (cells.Length == 0)
                    continue;

                string nameCell = cells[0].Trim();
                if (nameCell.Length == 0 || nameCell == "旋钮")
                    continue;
                if (Regex.IsMatch(nameCell, "^[-:\\s]+$"))
                    continue;

                MatchCollection ticks = Regex.Matches(nameCell, "`([^`]+)`");
                if (ticks.Count == 0)
                {
                    add(nameCell); // 非反引号形(如「F5 偏移可感知地板」)
                    continue;
                }

                foreach (Match tick in ticks)
                {
                    foreach (string part in tick.Groups[1].Value.Split('/'))
                    {
                        string token = part.Trim();
                        if (token.Length == 0)
                            continue;

                        add(token);

                        // `foo[]` → foo
                        if (token.EndsWith("[]", StringComparison.Ordinal))
                            add(token.Substring(0, token.Length - 2));

                        // `ItemDef.stack_max` → stack_max(裸后缀须同样受扫)
                        int dot = token.LastIndexOf('.');
                        if (dot >= 0 && dot < token.Length - 1)
                        {
                            string suffix = token.Substring(dot + 1);
                            if (Regex.IsMatch(suffix, "^[A-Za-z_][A-Za-z0-9_]*$"))
                                add(suffix);
                        }
                    }
                }
            }

            return knobs;
        }

        /// <summary>结构常量白名单 —— **显式列出**,每条带理由(QA Edge:不得静默放行旋钮)。
        /// 与旋钮的边界由 test_tuningKnobWhitelist_neverCoversGlobalKnobs 反向锁死。</summary>
        private static string[] structuralKnobWhitelist()
        {
            return new[]
            {
                // ① 载荷字段名(registry schema):Craft 实耗数组字段 —— 表行同名,但代码中出现的
                //    是**事件载荷字段**不是调参常量;实耗值本身逐配方走数据文件(D-21-15)。
                "ActualConsumed",
                // ② Q16.16 scale 因子(结构常量,ADR-005 —— 非旋钮):Fix.OneRaw / FractionalBits。
                "Q16.16", "65536", "OneRaw", "FractionalBits",
                // ③ 舍入模式的**值**(ROUND_MODE 行的取值 = 全局常量,ADR-006 §Decision 三「不建议改」)。
                "ROUND_HALF_AWAY_FROM_ZERO", "HalfAwayFromZero",
                // ④ quality_axis 的四个枚举字面量(结构词汇;QA Edge 点名 half_life 一例)。
                "half_life", "onset", "peak", "elimination",
                // ⑤ tick 量纲(真源 = entities.yaml 的 TICK_SECONDS/TICK_PERIOD,非本 GDD 旋钮表)。
                "TICK_SECONDS", "TICK_PERIOD",
                // ⑥ SKILL_CAP:引用自 30 技能系统,非本系统所有(QA Edge 原文)。
                "SKILL_CAP",
                // ⑦ schema_version:ADR-014 的结构字段门,非旋钮。
                "schema_version",
            };
        }

        /// <summary>剥离 // 与 /* */ 注释 **并置空白字符串/字符字面量内容**(F5 修,2026-09-23 双评审)。
        /// <para>判据 = 「旋钮名不在代码里**当字面量用**」:
        /// ① 注释内提及不算(文档性);② **字符串字面量内容也不算** —— 008 接线将写
        /// <c>Get("EFF_MAX")</c> 一类 JSON 键读取,旋钮名作为**字符串**出现是数据通路而非硬编码,
        /// 不剥离则 008 接线必假红;③ 裸代码标识符仍命中(硬编码常量、成员访问)。
        /// 字面量内容以**单空格替换**(保留引号/长度结构,防跨串误拼接);转义对整体置空。
        /// 局限(已知):不识别 <c>@</c> verbatim 串(工程内无此形);插值串 <c>$"...{expr}..."</c>
        /// 的洞内容按字符串置空 —— 洞内裸旋钮名不报(欠检不误报,接受)。</para></summary>
        private static string stripComments(string source)
        {
            var sb = new StringBuilder(source.Length);
            bool inString = false;
            bool inChar = false;
            bool inLineComment = false;
            bool inBlockComment = false;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];

                if (inLineComment)
                {
                    if (c == '\n')
                    {
                        inLineComment = false;
                        sb.Append(c);
                    }

                    continue;
                }

                if (inBlockComment)
                {
                    if (c == '*' && i + 1 < source.Length && source[i + 1] == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }

                    continue;
                }

                if (inString)
                {
                    if (c == '\\' && i + 1 < source.Length)
                    {
                        // 转义对(如 \")整体置空:两字符各一空格,长度结构保持
                        sb.Append(' ');
                        sb.Append(' ');
                        i++;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                        sb.Append('"');
                        continue;
                    }

                    sb.Append(' '); // 字面量内容置空 —— 旋钮名进不了扫描面(F5)
                    continue;
                }

                if (inChar)
                {
                    if (c == '\\' && i + 1 < source.Length)
                    {
                        sb.Append(' ');
                        sb.Append(' ');
                        i++;
                        continue;
                    }

                    if (c == '\'')
                    {
                        inChar = false;
                        sb.Append('\'');
                        continue;
                    }

                    sb.Append(' ');
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    sb.Append('"');
                    continue;
                }

                if (c == '\'')
                {
                    inChar = true;
                    sb.Append('\'');
                    continue;
                }

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    inLineComment = true;
                    i++;
                    continue;
                }

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    inBlockComment = true;
                    i++;
                    continue;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        [Test]
        public void test_stripComments_knobInStringAndComment_skipped_bareFlagged()
        {
            // F5 修的回归单测(双评审点名):三种上下文同一判据 ——
            // 扫描器谓词 = IndexOf(knob) on stripComments(source)(白名单另判),此处直接证谓词三向。

            // Given ①:旋钮名在字符串字面量内(JSON 键读取 —— 008 接线形态)
            string knobInString = "var eff = cfg.Get(\"EFF_MAX\");";
            // Given ②:旋钮名在行注释内(文档性提及)
            string knobInLineComment = "// EFF_MAX 是技能 0 转化效率\nvar eff = 1;";
            // Given ③:旋钮名在块注释内
            string knobInBlockComment = "/* EFF_MAX */\nvar eff = 1;";
            // Given ④:旋钮名裸用在代码里(硬编码常量 —— 仍须报)
            string knobBare = "var eff = EFF_MAX;";

            // When
            string strippedString = stripComments(knobInString);
            string strippedLineComment = stripComments(knobInLineComment);
            string strippedBlockComment = stripComments(knobInBlockComment);
            string strippedBare = stripComments(knobBare);

            // Then
            Assert.That(strippedString.IndexOf("EFF_MAX", StringComparison.Ordinal), Is.LessThan(0),
                "字符串字面量内容须置空 —— 008 的 Get(\"EFF_MAX\") 键读不得假红(F5):" + strippedString);
            Assert.That(strippedLineComment.IndexOf("EFF_MAX", StringComparison.Ordinal), Is.LessThan(0),
                "行注释内旋钮名不报(既有行为保持):" + strippedLineComment);
            Assert.That(strippedBlockComment.IndexOf("EFF_MAX", StringComparison.Ordinal), Is.LessThan(0),
                "块注释内旋钮名不报(既有行为保持):" + strippedBlockComment);
            Assert.That(strippedBare.IndexOf("EFF_MAX", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0),
                "裸代码标识符仍命中 —— 置空只作用于字面量内容,不得误伤标识符:" + strippedBare);

            // 结构自证:引号保留(防跨串误拼接),裸代码其余部分原样
            Assert.That(strippedString, Does.Contain("\""),
                "字符串引号必须保留(只置空内容):" + strippedString);
            Assert.That(strippedBare, Is.EqualTo(knobBare),
                "无字面量/注释的源码应原样通过:" + strippedBare);
        }

        // ── schema 形状守卫的辅助 ──

        private static Type unwrapType(Type type)
        {
            if (type.IsArray)
            {
                Type element = type.GetElementType();
                if (element != null)
                    return unwrapType(element);
            }

            Type underlying = Nullable.GetUnderlyingType(type);
            return underlying != null ? unwrapType(underlying) : type;
        }

        /// <summary>类型级:不得有 UnityEngine.* / System.Serializable 引导(D-21-18 静态面)。</summary>
        private static void assertNoEngineOrSerializableAttribute(Type type)
        {
            foreach (CustomAttributeData attr in type.GetCustomAttributesData())
            {
                string fullName = attr.AttributeType.FullName ?? "";
                Assert.That(fullName.StartsWith("UnityEngine.", StringComparison.Ordinal), Is.False,
                    $"{type.Name} 带 Unity 特性 {fullName} —— Fix 承载类型禁引导 Unity 序列化(D-21-18)");
                Assert.That(fullName, Is.Not.EqualTo("System.SerializableAttribute"),
                    $"{type.Name} 带 [Serializable] —— 归 Story 010 自定义编码器,此处禁标");
            }

            foreach (Type iface in type.GetInterfaces())
            {
                string fullName = iface.FullName ?? "";
                Assert.That(fullName.StartsWith("UnityEngine.", StringComparison.Ordinal), Is.False,
                    $"{type.Name} 实现引擎接口 {fullName} —— 禁形(D-21-18)");
            }
        }

        /// <summary>成员级:不得带 UnityEngine.* 特性(D-21-18 静态面)。</summary>
        private static void assertMemberHasNoEngineAttribute(MemberInfo member)
        {
            foreach (CustomAttributeData attr in member.GetCustomAttributesData())
            {
                string fullName = attr.AttributeType.FullName ?? "";
                Assert.That(fullName.StartsWith("UnityEngine.", StringComparison.Ordinal), Is.False,
                    $"{member.DeclaringType?.Name}.{member.Name} 带 Unity 特性 {fullName}(D-21-18)");
            }
        }
    }
}
