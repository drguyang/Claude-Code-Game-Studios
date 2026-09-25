// Story 004 · 绑重 schema hash 稳定性(AC-3-E3 ①②④⑤⑦ 五条的全部测试真身)
//
// 权威来源:production/epics/input-system/story-004-binding-schema-hash.md(判据权威,AC/QA 原文)
//   · AC-3-E3① 同一资产两次导出 hash 相同(确定性;记录枚举序漂移被排序吸收)
//   · AC-3-E3② 写一次 override ⇒ 重算 ⇒ hash 不变(改键不自我毁灭;含复合 part 与删除回程)
//   · AC-3-E3④ 不使用 string.GetHashCode() / 任何 BCL 默认哈希(输入程序集全目录静态扫描)
//   · AC-3-E3⑤ R 的读点零 effective* / 零 SaveBindingOverridesAsJson 载荷 ——
//       有 override 夹具下重算不变(静态扫描 SchemaHash.cs + effectivePath 判别探针)
//   · AC-3-E3⑦ 集合元素计数前缀生效([] vs [""] / ["a","b"] vs ["ab"] hash 不同)
//   · GDD input-system.md F-3.5 全文(元组 / 规范序列化 / 两不变量 / 硬纪律)·
//     ADR-011 Amendment A ③(bindingId + 三组集合入 R;UI map 包含在 R 内)
//
// 落点注记:故事 Test Evidence 登记口径 tests/unit/input_system/binding_schema_hash_test.cs;
//   Unity 只编译 unity/Assets/ 树 ⇒ 真身落本路径(README 落点说明同批)。
//
// 纪律:QA Test Cases 逐字映射,不发明原文之外的场景;实现期登记块(故事文件)记录
//   QA 与 GDD 原文的口径出入。确定性 · 无随机 · 无时间依赖。除静态扫描读源码文件外
//   零文件系统写入(文件系统读是 E3④/E3⑤ 静态扫描 AC 的直接对象)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DaYiJingCheng.Gameplay.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DaYiJingCheng.Tests.Unit.InputSystem
{
    /// <summary>Story 004 · 绑重 schema hash 五条 AC 的 EditMode 测试。</summary>
    [TestFixture]
    internal sealed class BindingSchemaHashTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();
        private static readonly string GameplayInputDir =
            Path.Combine(RepoRoot, "unity", "Assets", "Gameplay.Input");

        private InputActionAsset _asset;

        private static string ComputeRepoRoot([CallerFilePath] string callerPath = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath), "..", "..", "..", "..", ".."));

        [SetUp]
        public void SetUp()
        {
            _asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Assert.That(_asset, Is.Not.Null, "Given:动作资产须可加载(Story 001 交付的单实例源)");
            _asset.RemoveAllBindingOverrides();
        }

        [TearDown]
        public void TearDown()
        {
            if (_asset != null)   // SetUp 断言失败时不以 TearDown 的 NRE 掩盖真实失败
            {
                _asset.Disable();
                _asset.RemoveAllBindingOverrides();
            }
        }

        // ══════════ helpers ══════════

        private static void ApplyPathOverride(InputActionAsset asset, string actionName, string bindingPath, string newPath)
        {
            InputAction action = asset.FindAction(actionName);
            Assert.That(action, Is.Not.Null, $"Given:动作 {actionName} 须存在");
            int index = FindBindingIndex(action, bindingPath);
            action.ApplyBindingOverride(index, newPath);
        }

        private static void RemovePathOverride(InputActionAsset asset, string actionName, string bindingPath)
        {
            InputAction action = asset.FindAction(actionName);
            Assert.That(action, Is.Not.Null, $"动作 {actionName} 须存在");
            action.RemoveBindingOverride(FindBindingIndex(action, bindingPath));
        }

        private static int FindBindingIndex(InputAction action, string bindingPath)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].path == bindingPath)
                    return i;
            }
            Assert.Fail($"Given:动作 {action.name} 无 path = {bindingPath} 的绑定(资产结构与 Story 001 QA 文本不一致?)");
            return -1;
        }

        /// <summary>判别探针专用:与生产 BuildRecords 同构,但 path 读 effectivePath
        /// (引擎已合并 override 的视图)—— 仅用于证明 E3⑤ 主断言有牙,不进生产路径。</summary>
        private static List<BindingSchemaRecord> BuildEffectiveRecords(InputActionAsset asset)
        {
            var records = new List<BindingSchemaRecord>();
            foreach (InputActionMap map in asset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        InputBinding b = action.bindings[i];
                        records.Add(new BindingSchemaRecord(
                            map.name, action.name, i, b.id.ToString(), b.name,
                            b.effectivePath,
                            b.isComposite, b.isPartOfComposite,
                            SplitList(b.processors), SplitList(b.interactions), SplitList(b.groups)));
                    }
                }
            }
            return records;
        }

        private static string[] SplitList(string raw)
            => string.IsNullOrEmpty(raw) ? Array.Empty<string>() : raw.Split(';');

        /// <summary>合成记录:全字段固定,仅三集合可变 —— E3⑦ 探针的夹具工厂。</summary>
        private static BindingSchemaRecord MakeRecord(
            string[] groups = null, string[] processors = null, string[] interactions = null)
            => new BindingSchemaRecord(
                "MapA", "ActionA", 0, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                "binding-name", "<Keyboard>/e", false, false,
                processors ?? Array.Empty<string>(),
                interactions ?? Array.Empty<string>(),
                groups ?? Array.Empty<string>());

        private static string HashOf(params BindingSchemaRecord[] records)
            => SchemaHash.ComputeSchemaHash(records);

        // ══════════ AC-3-E3①:两次导出 hash 相同 ══════════

        [Test]
        public void test_schemaHash_sameAsset_twiceCompute_identical()
        {
            // QA E3① 主用例:同一资产两次构造 R 并计算 ⇒ 逐位相等。
            string first = SchemaHash.ComputeSchemaHash(_asset);
            string second = SchemaHash.ComputeSchemaHash(_asset);

            Assert.That(first, Is.EqualTo(second), "同一资产两次计算 hash 必须相同(确定性)");
            Assert.That(first, Does.Match("^[0-9a-f]{16}$"),
                "输出 = FNV-1a-64 小写 hex 16 位(BindingsStore 头部按 Ordinal 字符串比较)");

            // Edge:记录枚举顺序被打乱 ⇒ 排序吸收,仍相等。
            var records = new List<BindingSchemaRecord>(SchemaHash.BuildRecords(_asset));
            records.Reverse();
            Assert.That(SchemaHash.ComputeSchemaHash(records), Is.EqualTo(first),
                "记录枚举序漂移被 sort(R) 吸收(集合序同理由 Ordinal 排序保证)");
        }

        [Test]
        public void test_schemaHash_buildRecords_coverAllBindingsWithIds()
        {
            // Manifest Required 实证:R 覆盖全部绑定(含 UI map)且每条 bindingId 非空
            // (ADR-011 Amendment A ③ —— 重建检测的使能性质靠 bindingId 在 R 内)。
            IReadOnlyList<BindingSchemaRecord> records = SchemaHash.BuildRecords(_asset);

            Assert.That(records.Count, Is.EqualTo(_asset.bindings.Count()),
                "R 覆盖资产全部绑定(孤儿绑定除外 —— 当前资产零孤儿)");
            Assert.That(records.Any(r => r.MapName == "UI"), "UI map 包含在 R 内(GDD F-3.5 明文)");
            Assert.That(records.All(r => !string.IsNullOrEmpty(r.BindingId)), "每条记录 bindingId 非空");
            Assert.That(records.Select(r => r.BindingId).Distinct().Count(), Is.EqualTo(records.Count),
                "bindingId 全局唯一(overrides 持久化键)");
            Assert.That(records.Any(r => r.Groups.Length > 0), "真实资产含非空 groups(三组集合参与 R)");
            Assert.That(records.Any(r => r.MapName == "UI" && r.Groups.Length > 0),
                "UI map 的记录同样携带真实字段(排除 UI 的实现在此红)");
        }

        // ══════════ AC-3-E3②:写一次 override ⇒ hash 不变 ══════════

        [Test]
        public void test_schemaHash_overridesWritten_recomputeUnchanged()
        {
            // QA E3② 主用例 + 两组 Edge(复合 parts 被 override / override 后再删回默认)。
            string baseline = SchemaHash.ComputeSchemaHash(_asset);

            ApplyPathOverride(_asset, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            Assert.That(SchemaHash.ComputeSchemaHash(_asset), Is.EqualTo(baseline),
                "写一次 path override ⇒ 重算 hash 不变(改键不自我毁灭 —— 不变量①)");

            ApplyPathOverride(_asset, "Move", "<Keyboard>/w", "<Keyboard>/z");   // 复合绑重的单个 part
            Assert.That(SchemaHash.ComputeSchemaHash(_asset), Is.EqualTo(baseline),
                "复合绑重 part 被 override ⇒ hash 仍不变");

            RemovePathOverride(_asset, "Interact", "<Keyboard>/e");
            RemovePathOverride(_asset, "Move", "<Keyboard>/w");
            Assert.That(SchemaHash.ComputeSchemaHash(_asset), Is.EqualTo(baseline),
                "override 后再删(回默认)⇒ hash 仍等于基线");
        }

        // ══════════ AC-3-E3④:不用 BCL 默认哈希 ══════════

        [Test]
        public void test_schemaHash_sourceScan_noBclDefaultHash()
        {
            // QA E3④ 主用例:扫描输入程序集(Gameplay.Input 全目录)源码 ——
            // 去注释 + 去字面量后,默认哈希入口零出现;hash 实现内仅自实现 FNV-1a-64。
            // token 覆盖「任何 BCL 默认哈希」两族入口:GetHashCode / EqualityComparer(初版)+
            // HashCode(System.HashCode 与 HashCode.Combine/ToHashCode —— 不含 GetHashCode 子串,评审 Q2 补)。
            string[] tokens = { "GetHashCode", "EqualityComparer<", "HashCode" };
            var violations = new List<string>();
            foreach (string file in Directory.GetFiles(GameplayInputDir, "*.cs", SearchOption.AllDirectories))
            {
                string code = StripLiterals(StripComments(File.ReadAllText(file)));
                foreach (string token in tokens)
                {
                    if (code.Contains(token))
                        violations.Add($"{Path.GetFileName(file)}:{token}");
                }
            }
            Assert.That(violations, Is.Empty,
                $"输入程序集零默认哈希入口(违例:{string.Join(", ", violations)})");

            // Negative fixture:夹具源码 return s.GetHashCode() ⇒ 扫描必须红。
            const string negative = "class F { string T(string s) { return s.GetHashCode(); } }";
            Assert.That(StripLiterals(StripComments(negative)), Does.Contain("GetHashCode"),
                "负例夹具必须被命中(证明上循环的断言非空转)");

            // Negative fixture(评审 Q2):System.HashCode / HashCode.Combine 族 —— 不含 "GetHashCode"
            // 子串,靠独立 token 兜住;漏 token 则此夹具文字面检不出红。
            const string hashCodeNegative = "class F { int C(int a, int b) { return System.HashCode.Combine(a, b); } }";
            string strippedHashCode = StripLiterals(StripComments(hashCodeNegative));
            Assert.That(strippedHashCode, Does.Contain("HashCode"),
                "HashCode.Combine 族负例必须被命中(BCL 默认哈希的另一半入口)");

            // 插值字符串洞内代码不得被当字面量剥掉(评审 F4 —— 剥离器 $ 分支的牙齿)。
            const string interpNegative = "class F { string T(string s) { return $\"{s.GetHashCode()}\"; } }";
            Assert.That(StripLiterals(StripComments(interpNegative)), Does.Contain("GetHashCode"),
                $"插值洞代码保留负例必须被命中(剥离后文本:{StripLiterals(StripComments(interpNegative))})");
            // 正对照:插值的纯字面量段(无洞)仍被剥掉。
            const string interpLiteralOnly = "class F { string T() { return $\"plain GetHashCode here\"; } }";
            Assert.That(StripLiterals(StripComments(interpLiteralOnly)), Does.Not.Contain("GetHashCode"),
                "插值的字面量段照常剥离(正对照)");

            // 正对照:同词出现在注释里 ⇒ 剥离后不命中(剥离器有牙,真源注释不误报)。
            const string commentOnly = "// never: s.GetHashCode() and EqualityComparer<string>.Default\nreturn 0;";
            string stripped = StripLiterals(StripComments(commentOnly));
            Assert.That(stripped, Does.Not.Contain("GetHashCode"));
            Assert.That(stripped, Does.Not.Contain("EqualityComparer<"));
        }

        // ══════════ AC-3-E3⑤:R 读点零 effective* / 零 override 载荷 ══════════

        [Test]
        public void test_schemaHash_withOverride_recomputeUnchanged_effectiveProbeDiffers()
        {
            // QA E3⑤ 主用例(必须用有 override 的夹具 —— 无 override 时 effectivePath == path,
            // 正例无法区分):重算不变;若读点用了 effectivePath ⇒ 必变 ⇒ 判别探针证明主断言有牙。
            string baseline = SchemaHash.ComputeSchemaHash(_asset);
            Assert.That(SchemaHash.ComputeSchemaHash(BuildEffectiveRecords(_asset)), Is.EqualTo(baseline),
                "正对照:无 override 时 effectivePath == path ⇒ 两种读法同 hash(故夹具必须带 override)");

            ApplyPathOverride(_asset, "Interact", "<Keyboard>/e", "<Keyboard>/q");

            Assert.That(SchemaHash.ComputeSchemaHash(_asset), Is.EqualTo(baseline),
                "有 override 夹具下重算 hash 不变(AC 主断言 —— 读点用 effectivePath 或载荷则此断言红)");
            Assert.That(SchemaHash.ComputeSchemaHash(BuildEffectiveRecords(_asset)), Is.Not.EqualTo(baseline),
                "判别探针:改读 effectivePath 的变体在同夹具下 hash 必变 ⇒ 主断言非空转");
        }

        [Test]
        public void test_schemaHash_schemaHashSource_hasNoEffectiveOrPayloadReads()
        {
            // QA E3⑤ 静态半边:R 的读点零 effective* / 零 SaveBindingOverridesAsJson ——
            // 走去注释 + 去字面量文本(doc comment 里以「禁读」字样提及这些名字不构成违规)。
            // 扫描面 = Gameplay.Input 全目录(评审 Q5:单锚 SchemaHash.cs 时 BuildRecords 搬家即恒绿);
            // 唯一豁免 = BindingsStore.cs × SaveBindingOverridesAsJson —— Story 003 的合法写点
            // (载荷只落盘、不回读进资产,「3 不解析」由 story-003 的解析扫描守)。
            string[] tokens =
            {
                "effectivePath", "effectiveProcessors", "effectiveInteractions", "effectiveGroups",
                "overridePath", "overrideProcessors", "overrideInteractions", "overrideGroups",
                "SaveBindingOverridesAsJson",
            };
            const string exemptFile = "BindingsStore.cs";
            var violations = new List<string>();
            foreach (string file in Directory.GetFiles(GameplayInputDir, "*.cs", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(file);
                string code = StripLiterals(StripComments(File.ReadAllText(file)));
                foreach (string token in tokens)
                {
                    if (!code.Contains(token))
                        continue;
                    if (token == "SaveBindingOverridesAsJson" && fileName == exemptFile)
                        continue;
                    violations.Add($"{fileName}:{token}");
                }
            }
            Assert.That(violations, Is.Empty,
                $"R 读点零 effective*/override*/载荷调用(违例:{string.Join(", ", violations)})");

            // Negative fixture:读点改用 effectivePath 的变体 ⇒ 红。
            const string negative =
                "class F { string P(UnityEngine.InputSystem.InputBinding b) { return b.effectivePath; } }";
            Assert.That(StripLiterals(StripComments(negative)), Does.Contain("effectivePath"),
                "负例夹具必须被命中(证明上循环的断言非空转)");

            // 正对照:同词只出现在注释里 ⇒ 剥离后不命中(剥离器有牙,「禁读」注释不误报)。
            const string commentOnly = "/// 禁读 binding.effectivePath / SaveBindingOverridesAsJson 载荷\nreturn b.path;";
            string stripped = StripLiterals(StripComments(commentOnly));
            Assert.That(stripped, Does.Not.Contain("effectivePath"));
            Assert.That(stripped, Does.Not.Contain("SaveBindingOverridesAsJson"));
        }

        // ══════════ AC-3-E3⑦:集合元素计数前缀 ══════════

        [Test]
        public void test_schemaHash_countPrefix_distinguishesEmptyAdjacentShift()
        {
            // QA E3⑦ 夹具一:groups = [] 与 groups = [""] ⇒ hash 不同(计数 0 vs 1 参与)。
            Assert.That(HashOf(MakeRecord(groups: Array.Empty<string>())),
                Is.Not.EqualTo(HashOf(MakeRecord(groups: new[] { "" }))),
                "空集与单空串元素必须可区分(AC 夹具原文)");

            // QA E3⑦ 夹具二:["a","b"] 与 ["ab"] ⇒ hash 不同(逐元素长度前缀 + 计数)。
            Assert.That(HashOf(MakeRecord(groups: new[] { "a", "b" })),
                Is.Not.EqualTo(HashOf(MakeRecord(groups: new[] { "ab" }))),
                "元素切分不同的集合必须可区分(AC 夹具原文)");

            // 计数牙齿(QA Negative「去掉计数前缀 ⇒ 本测红」的可构造形态 —— 见故事
            // 实现期登记:AC 两夹具在「逐元素长度前缀」编码下即使去掉计数也不撞,
            // 相邻集合平移才会撞:proc=[clamp]/int=[] 与 proc=[]/int=[clamp] 在无计数
            // 时字节流相同 ⇒ 计数参与则必须不同,去掉计数此断言红)。
            Assert.That(HashOf(MakeRecord(processors: new[] { "clamp" })),
                Is.Not.EqualTo(HashOf(MakeRecord(interactions: new[] { "clamp" }))),
                "相邻集合平移可区分 —— 计数前缀的直接牙齿");
        }

        [Test]
        public void test_schemaHash_elementLengthPrefix_sameCountSplit_differs()
        {
            // GDD F-3.5 长度前缀行的牙齿:("ab","c") 与 ("a","bc") —— 计数同为 2,
            // 只有逐元素长度前缀区分(去掉元素长度前缀则两侧都是 "abc" ⇒ 撞)。
            Assert.That(HashOf(MakeRecord(groups: new[] { "ab", "c" })),
                Is.Not.EqualTo(HashOf(MakeRecord(groups: new[] { "a", "bc" }))),
                "同计数不同切分必须可区分(逐元素长度前缀)");
        }

        // ══════════ 不变量② / manifest Required:hash 输入含 bindingId(评审批追加) ══════════

        [Test]
        public void test_schemaHash_bindingIdDiffers_hashDiffers()
        {
            // ADR-011 Amendment A ③ + control manifest Required「hash 输入须含 bindingId」
            // (重建检测的使能性质;失配后果归 Story 005)。牙齿:canon 删掉 WriteString(BindingId)
            // 这一行 ⇒ 本测红 —— 评审 F1 指出原 8 测对不变量②全盲,此处补牙。
            BindingSchemaRecord other = new BindingSchemaRecord(
                "MapA", "ActionA", 0, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeee0",
                "binding-name", "<Keyboard>/e", false, false,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

            Assert.That(HashOf(MakeRecord()), Is.Not.EqualTo(HashOf(other)),
                "仅 bindingId 不同的两条记录 hash 必不同(重建 ⇒ GUID 重生成 ⇒ hash 必变)");
        }

        // ══════════ 源码剥离器(自包含 —— 与 Story 003 的同名实现刻意不共享) ══════════

        /// <summary>剥离 // 与 /* */ 注释、保留字符串/字符字面量内容(状态机)。
        /// 逐字串 @"..." 按 "" 转义处理。</summary>
        private static string StripComments(string source)
        {
            var sb = new StringBuilder(source.Length);
            int i = 0;
            while (i < source.Length)
            {
                char c = source[i];
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n')
                        i++;
                    continue;
                }
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                        i++;
                    i = Math.Min(i + 2, source.Length);
                    continue;
                }
                if (c == '@' && i + 1 < source.Length && source[i + 1] == '"')
                {
                    sb.Append("@\"");
                    i += 2;
                    while (i < source.Length)
                    {
                        if (source[i] == '"')
                        {
                            if (i + 1 < source.Length && source[i + 1] == '"')
                            {
                                sb.Append("\"\"");
                                i += 2;
                                continue;
                            }
                            sb.Append('"');
                            i++;
                            break;
                        }
                        sb.Append(source[i]);
                        i++;
                    }
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    sb.Append(c);
                    i++;
                    while (i < source.Length)
                    {
                        char d = source[i];
                        sb.Append(d);
                        i++;
                        if (d == '\\' && i < source.Length)
                        {
                            sb.Append(source[i]);
                            i++;
                            continue;
                        }
                        if (d == quote)
                            break;
                    }
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>剥离字符串/字符/逐字串字面量内容(保留代码骨架)—— 符号断言走此文本。
        /// 插值字符串 $"…{hole}…" / $@"…" / @$"…":剥字面量段、<b>保留插值洞内代码</b>
        /// (否则 $"{s.GetHashCode()}" 的洞被当字面量剥掉 ⇒ 扫描假阴性 —— 评审 F4);
        /// 洞内嵌套字面量照常剥(占位防粘连)。</summary>
        private static string StripLiterals(string source)
        {
            var sb = new StringBuilder(source.Length);
            int i = 0;
            while (i < source.Length)
            {
                char c = source[i];

                // 插值字符串(必须先于 @ 与普通引号分支:$@ / @$ 两种前缀)
                bool interpVerbatim = false;
                bool isInterp = false;
                if (c == '$' && i + 1 < source.Length && source[i + 1] == '"')
                {
                    isInterp = true;
                    i += 2;
                }
                else if (c == '$' && i + 2 < source.Length && source[i + 1] == '@' && source[i + 2] == '"')
                {
                    isInterp = true;
                    interpVerbatim = true;
                    i += 3;
                }
                else if (c == '@' && i + 2 < source.Length && source[i + 1] == '$' && source[i + 2] == '"')
                {
                    isInterp = true;
                    interpVerbatim = true;
                    i += 3;
                }
                if (isInterp)
                {
                    while (i < source.Length)
                    {
                        char d = source[i];
                        if (!interpVerbatim && d == '\\' && i + 1 < source.Length)
                        {
                            i += 2;
                            continue;
                        }
                        if (d == '"')
                        {
                            if (interpVerbatim && i + 1 < source.Length && source[i + 1] == '"')
                            {
                                i += 2;
                                continue;
                            }
                            i++;
                            break;
                        }
                        if (d == '{')
                        {
                            if (i + 1 < source.Length && source[i + 1] == '{')   // {{ 转义大括号,非洞
                            {
                                i += 2;
                                continue;
                            }
                            i++;
                            sb.Append('{');
                            int depth = 1;
                            while (i < source.Length && depth > 0)
                            {
                                char h = source[i];
                                if (h == '{')
                                {
                                    depth++;
                                    sb.Append(h);
                                    i++;
                                }
                                else if (h == '}')
                                {
                                    depth--;
                                    sb.Append(h);
                                    i++;
                                }
                                else if (h == '"' || h == '\'')
                                {
                                    char q = h;
                                    i++;
                                    while (i < source.Length)
                                    {
                                        char n = source[i];
                                        if (!interpVerbatim && n == '\\' && i + 1 < source.Length)
                                        {
                                            i += 2;
                                            continue;
                                        }
                                        if (n == q)
                                        {
                                            if (interpVerbatim && q == '"' && i + 1 < source.Length && source[i + 1] == '"')
                                            {
                                                i += 2;
                                                continue;
                                            }
                                            i++;
                                            break;
                                        }
                                        i++;
                                    }
                                    sb.Append(q == '"' ? "\"\"" : "''");   // 占位防粘连(内容按字面量剥)
                                }
                                else
                                {
                                    sb.Append(h);
                                    i++;
                                }
                            }
                            continue;
                        }
                        i++;
                    }
                    sb.Append("\"\"");
                    continue;
                }

                if (c == '@' && i + 1 < source.Length && source[i + 1] == '"')
                {
                    i += 2;
                    while (i < source.Length)
                    {
                        if (source[i] == '"')
                        {
                            if (i + 1 < source.Length && source[i + 1] == '"')
                            {
                                i += 2;
                                continue;
                            }
                            i++;
                            break;
                        }
                        i++;
                    }
                    sb.Append("\"\"");
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    i++;
                    while (i < source.Length)
                    {
                        char d = source[i];
                        i++;
                        if (d == '\\' && i < source.Length)
                        {
                            i++;
                            continue;
                        }
                        if (d == quote)
                            break;
                    }
                    sb.Append(quote == '"' ? "\"\"" : "''");
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }
    }
}
