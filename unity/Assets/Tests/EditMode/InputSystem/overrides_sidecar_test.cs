// Story 003 · 绑重 overrides sidecar 持久化(AC-3-A2 / A5 / E3⑥ 三条 BLOCKING 的全部测试真身)
//
// 权威来源:production/epics/input-system/story-003-overrides-sidecar.md(判据权威,AC/QA 原文)
//   · AC-3-A2 Save→Load 往返逐键一致(含复合绑重 part;同动作多物理输入不受污染)
//   · AC-3-A5 落盘面白名单 —— 写盘调用点 ∈ {bindings.overrides.json, bindings.schema.txt};
//       拒绝清单符号(PlayerPrefs / EditorPrefs / IEventSink / Checkpoint)一票否决;
//       负例 = 拒绝清单夹具扫描必须红
//   · AC-3-E3⑥ 喂入字节 == 保存字节 —— 文件字节 == Save 返回值的 UTF-8 编码,
//       3 不解析不重写(源码无 JSON 解析调用);负例 = 插入美化换行 ⇒ 字节断言红
//   · GDD input-system.md 规则四(两文件 + 读序)· 规则五(重载前两步)· Edge Cases 二
//   · ADR-011 Amendment A ② / ADR-010 边界(overrides 不进存档、不进三流)
//
// 落点注记:故事 Test Evidence 登记口径 tests/integration/input_system/overrides_sidecar_test.cs;
//   Unity 只编译 unity/Assets/ 树 ⇒ 真身落本路径(README 落点说明同批)。
//
// 纪律:QA Test Cases 逐字映射,不发明原文之外的场景;实现期登记块(故事文件)记录
//   QA 未逐字点名但属 Then/读序直接蕴含的忠实扩展。确定性 · 无随机 · 无时间依赖 ·
//   每测试自建临时目录。文件系统访问是本故事 AC 的直接对象(往返/落盘面),
//   与「单测不依赖文件系统」通例的出入已在 Test Evidence 落点注记中声明。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.Unit.InputSystem
{
    /// <summary>Story 003 · overrides sidecar 三条 BLOCKING AC 的 EditMode 测试。</summary>
    [TestFixture]
    internal sealed class OverridesSidecarTest
    {
        private const string HashA = "schema-hash-fixture-a";
        private const string HashB = "schema-hash-fixture-b";

        private static readonly string RepoRoot = ComputeRepoRoot();
        private static readonly string GameplayInputDir =
            Path.Combine(RepoRoot, "unity", "Assets", "Gameplay.Input");

        private string _tempDir;
        private InputActionAsset _asset;
        private InputActionAsset _clone;

        private static string ComputeRepoRoot([CallerFilePath] string callerPath = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath), "..", "..", "..", "..", ".."));

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "dayj_overrides_sidecar_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Assert.That(_asset, Is.Not.Null, "Given:动作资产须可加载(Story 001 交付的单实例源)");
            _asset.RemoveAllBindingOverrides();
        }

        [TearDown]
        public void TearDown()
        {
            if (_clone != null)
            {
                UnityEngine.Object.DestroyImmediate(_clone);
                _clone = null;
            }
            if (_asset != null)   // SetUp 断言失败时不以 TearDown 的 NRE 掩盖真实失败(review Q10)
            {
                _asset.Disable();
                _asset.RemoveAllBindingOverrides();
            }
            Directory.Delete(_tempDir, true);
        }

        // ══════════ helpers ══════════

        /// <summary>干净克隆(施加 override 之前调用):深拷贝、binding Guid 与共享资产一致。</summary>
        private InputActionAsset CreateCleanClone()
        {
            _clone = UnityEngine.Object.Instantiate(_asset);
            _clone.name = _asset.name + "_sidecarClone";
            return _clone;
        }

        private static void ApplyPathOverride(InputActionAsset asset, string actionName, string bindingPath, string newPath)
        {
            InputAction action = asset.FindAction(actionName);
            Assert.That(action, Is.Not.Null, $"Given:动作 {actionName} 须存在");
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].path == bindingPath)
                {
                    action.ApplyBindingOverride(i, newPath);
                    return;
                }
            }
            Assert.Fail($"Given:动作 {actionName} 无 path = {bindingPath} 的绑定(资产结构与 Story 001 QA 文本不一致?)");
        }

        private static InputBinding FindBinding(InputActionAsset asset, string actionName, string bindingPath)
        {
            InputAction action = asset.FindAction(actionName);
            Assert.That(action, Is.Not.Null, $"动作 {actionName} 须存在");
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].path == bindingPath)
                    return action.bindings[i];
            }
            Assert.Fail($"动作 {actionName} 无 path = {bindingPath} 的绑定");
            return default;
        }

        private static Dictionary<Guid, (string path, string processors, string interactions)> Snapshot(InputActionAsset asset)
        {
            var map = new Dictionary<Guid, (string, string, string)>();
            foreach (InputBinding b in asset.bindings)
                map[b.id] = (b.overridePath, b.overrideProcessors, b.overrideInteractions);
            return map;
        }

        private static void AssertSnapshotEqual(
            Dictionary<Guid, (string path, string processors, string interactions)> expected,
            Dictionary<Guid, (string path, string processors, string interactions)> actual,
            string because)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), $"{because} —— 绑定集合(按 id)必须一致");
            foreach (var kv in expected)
                Assert.That(actual[kv.Key], Is.EqualTo(kv.Value),
                    $"{because} —— binding {kv.Key} 的 override 三元组(path/processors/interactions)不一致");
        }

        private static void AssertNoOverrides(
            Dictionary<Guid, (string path, string processors, string interactions)> snapshot, string because)
        {
            foreach (var kv in snapshot)
            {
                Assert.That(kv.Value.path, Is.Null, $"{because} —— binding {kv.Key} 不得带 override");
                Assert.That(kv.Value.processors, Is.Null,
                    $"{because} —— binding {kv.Key} 不得带 overrideProcessors 污染(review Q8)");
                Assert.That(kv.Value.interactions, Is.Null,
                    $"{because} —— binding {kv.Key} 不得带 overrideInteractions 污染(review Q8)");
            }
        }

        private static int CountOverridden(
            Dictionary<Guid, (string path, string processors, string interactions)> snapshot)
            => snapshot.Values.Count(v => v.path != null || v.processors != null || v.interactions != null);

        // ══════════ AC-3-A2:Save→Load 往返逐键一致 ══════════

        [Test]
        public void test_overridesSidecar_saveLoad_roundTrip_perKeyIncludingComposite()
        {
            // QA A2 主用例 + 两组 Edge(仅复合一部分 / 同一动作两物理输入)合并:
            // Given 普通绑重 + 复合 part 绑重施加于共享资产,干净克隆作接收方。
            _asset.RemoveAllBindingOverrides();
            InputActionAsset clone = CreateCleanClone();
            string assetFile = Path.Combine(RepoRoot, "unity", "Assets", "InputSystem_Actions.inputactions");
            byte[] assetFileBefore = File.ReadAllBytes(assetFile);   // 全程不得写回 .inputactions 源文件(review Q10)

            ApplyPathOverride(_asset, "Interact", "<Keyboard>/e", "<Keyboard>/q");   // 普通绑重
            ApplyPathOverride(_asset, "Move", "<Keyboard>/w", "<Keyboard>/z");       // 复合绑重的单个 part
            // Interact 同一动作两物理输入:K&M 改,buttonNorth / XR 刻意不动

            var expected = Snapshot(_asset);
            Assert.That(CountOverridden(expected), Is.EqualTo(2),
                "Given:恰好两处 override 施加成功(复合 part + Interact K&M)");

            var store = new BindingsStore(_tempDir);

            // Act
            store.Save(_asset, HashA);
            OverridesLoadResult result = store.Load(clone, HashA);

            // Then:逐键一致,含复合 part;未动的物理输入不受污染
            Assert.That(result, Is.EqualTo(OverridesLoadResult.Applied), "匹配 ⇒ 载荷喂入");
            var actual = Snapshot(clone);
            AssertSnapshotEqual(expected, actual, "Save→Load 往返");

            Guid interactKm = FindBinding(_asset, "Interact", "<Keyboard>/e").id;
            Guid interactPad = FindBinding(_asset, "Interact", "<Gamepad>/buttonNorth").id;
            Guid interactXr = FindBinding(_asset, "Interact", "<XRController>/{PrimaryAction}").id;
            Guid moveW = FindBinding(_asset, "Move", "<Keyboard>/w").id;

            Assert.That(actual[interactKm].path, Is.EqualTo("<Keyboard>/q"), "普通绑重逐键一致");
            Assert.That(actual[moveW].path, Is.EqualTo("<Keyboard>/z"), "复合 part 绑重逐键一致(仅改 part)");
            Assert.That(actual[interactPad].path, Is.Null,
                "Edge 同一动作两物理输入:手柄侧不受污染");
            Assert.That(actual[interactXr].path, Is.Null,
                "Edge 同一动作两物理输入:XR 侧不受污染");
            Assert.That(File.ReadAllBytes(assetFile), Is.EqualTo(assetFileBefore),
                "override 只活在内存/sidecar —— .inputactions 源文件字节未变(review Q10)");
        }

        [Test]
        public void test_overridesSidecar_saveLoad_emptyOverrideSet_roundTripsClean()
        {
            // QA A2 Edge:「空 override 集(出厂 API 返回空串)」—— 载荷 0 字节,读回零污染。
            _asset.RemoveAllBindingOverrides();
            InputActionAsset clone = CreateCleanClone();
            var store = new BindingsStore(_tempDir);

            string payload = store.Save(_asset, HashA);
            Assert.That(payload, Is.EqualTo(string.Empty), "无 override 时出厂 API 返回空串");
            Assert.That(new FileInfo(store.OverridesPath).Length, Is.EqualTo(0), "载荷文件 0 字节");

            OverridesLoadResult result = store.Load(clone, HashA);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Applied), "空载荷是合法载荷(匹配 ⇒ Applied)");
            AssertNoOverrides(Snapshot(clone), "空 override 集往返后资产仍为出厂默认");
        }

        [Test]
        public void test_overridesSidecar_load_firstLaunchNoFiles_returnsNoSidecar()
        {
            // QA A2 Edge:「空 override 集(首次启动)」—— 两文件皆无 = 正常路径,非异常。
            _asset.RemoveAllBindingOverrides();
            ApplyPathOverride(_asset, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            var store = new BindingsStore(_tempDir);   // SetUp 建的空目录,无任何文件

            OverridesLoadResult result = store.Load(_asset, HashA);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.NoSidecar), "首次启动走正常路径");
            AssertNoOverrides(Snapshot(_asset), "无 sidecar ⇒ 载入出厂默认(规则五两步仍执行)");
        }

        [Test]
        public void test_overridesSidecar_neg_corruptPayload_returnsCorruptPayloadNotSilent()
        {
            // QA A2 Negative:「损坏 JSON(半截)⇒ 走失配路径不静默(交叉 Story 005)」。
            _asset.RemoveAllBindingOverrides();
            InputActionAsset clone = CreateCleanClone();
            ApplyPathOverride(_asset, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            var store = new BindingsStore(_tempDir);
            store.Save(_asset, HashA);

            // 篡改:头部完好(匹配),载荷截断(损坏)
            File.WriteAllBytes(store.OverridesPath,
                Encoding.UTF8.GetBytes("{\"bindings\":[{\"id\":\"1c04ea5f-0000-0000-0000-000000000000\""));

            LogAssert.Expect(LogType.Error, new Regex("拒收"));
            OverridesLoadResult result = store.Load(clone, HashA);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.CorruptPayload), "损坏 ⇒ 不静默、不部分恢复");
            AssertNoOverrides(Snapshot(clone), "损坏出口维持默认(两步已清,载荷未生效)");
        }

        [Test]
        public void test_overridesSidecar_hashMismatch_backupRecoveryAndDefault()
        {
            // QA A2 负半边 → Story 005 接管(实现期登记):hash 失配 ⇒ 陈旧 sidecar 改名备份 +
            // 载入默认 + 日志三要素(AC-3-A3 的 store 级半边;完整断言住 005 的测试文件)。
            _asset.RemoveAllBindingOverrides();
            ApplyPathOverride(_asset, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            var store = new BindingsStore(_tempDir);
            store.Save(_asset, HashA);
            byte[] payloadBefore = File.ReadAllBytes(store.OverridesPath);
            byte[] headerBefore = File.ReadAllBytes(store.SchemaPath);

            LogAssert.Expect(LogType.Warning, new Regex("失配"));
            LogAssert.Expect(LogType.Warning, new Regex("备份路径"));
            LogAssert.Expect(LogType.Warning, new Regex("恢复完成"));
            OverridesLoadResult result = store.Load(_asset, HashB);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Mismatch), "失配 ⇒ 不喂");
            AssertNoOverrides(Snapshot(_asset), "失配出口资产维持默认");
            Assert.That(File.ReadAllBytes(store.OverridesPath + ".bak-001"), Is.EqualTo(payloadBefore),
                "陈旧载荷已改名备份、逐字节保留(非删除)");
            Assert.That(File.ReadAllBytes(store.SchemaPath + ".bak-001"), Is.EqualTo(headerBefore),
                "陈旧头部已改名备份、逐字节保留");
            Assert.That(File.Exists(store.OverridesPath), Is.False, "原路径已移除(后续 Save 可重建)");
            Assert.That(File.Exists(store.SchemaPath), Is.False, "原路径已移除(后续 Save 可重建)");
        }

        [Test]
        public void test_overridesSidecar_halfSidecar_treatedAsMismatch()
        {
            // QA A2「损坏/半截」的文件级半截:只有一半 sidecar ⇒ 视同失配(实现期登记)。
            _asset.RemoveAllBindingOverrides();
            var store = new BindingsStore(_tempDir);
            store.Save(_asset, HashA);
            File.Delete(store.SchemaPath);   // 只剩载荷,头部缺失

            LogAssert.Expect(LogType.Warning, new Regex("半截"));
            OverridesLoadResult result = store.Load(_asset, HashA);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Mismatch), "半截 ⇒ 视同失配,不尝试部分恢复");
        }

        [Test]
        public void test_overridesSidecar_load_disablesAsset_ruleFiveStep1()
        {
            // 规则五 step1 / ADR-011 Amendment A ② 的实证(review Q2):既有用例只断 override
            // 三元组,漏删 Disable() 全部照绿 —— 本测直接断动作启用态。
            _asset.RemoveAllBindingOverrides();
            InputActionAsset clone = CreateCleanClone();
            clone.Enable();
            InputAction interact = clone.FindAction("Interact");
            Assert.That(interact, Is.Not.Null, "Given:Interact 须在");
            Assert.That(interact.enabled, Is.True, "Given:资产已启用");

            var store = new BindingsStore(_tempDir);

            OverridesLoadResult result = store.Load(clone, HashA);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.NoSidecar), "首次启动正常路径(本测焦点在 step1)");
            Assert.That(clone.FindAction("Interact").enabled, Is.False,
                "Load 返回后资产须处于 Disable 态 —— 重载前两步第一步实际执行");
        }

        [Test]
        public void test_overridesSidecar_headerMissingSchemaHash_mismatchNotSilent()
        {
            // 读序:头部存在但缺 schema_hash 字段 ⇒ 视同失配不静默(review Q5/F9)。
            _asset.RemoveAllBindingOverrides();
            ApplyPathOverride(_asset, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            var store = new BindingsStore(_tempDir);
            store.Save(_asset, HashA);
            File.WriteAllBytes(store.SchemaPath,
                Encoding.UTF8.GetBytes("format_version=1\nasset_id=AssetX"));

            LogAssert.Expect(LogType.Warning, new Regex("schema_hash"));
            LogAssert.Expect(LogType.Warning, new Regex("备份路径"));
            LogAssert.Expect(LogType.Warning, new Regex("恢复完成"));
            OverridesLoadResult result = store.Load(_asset, HashA);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Mismatch), "缺 hash 字段 ⇒ 视同失配");
            AssertNoOverrides(Snapshot(_asset), "缺 hash 字段出口资产维持默认");
            Assert.That(File.Exists(store.OverridesPath + ".bak-001"), Is.True,
                "缺 hash 字段出口陈旧载荷已改名备份(不触碰不代表保留 —— Story 005 语义为改名备份)");
            Assert.That(File.Exists(store.SchemaPath + ".bak-001"), Is.True,
                "缺 hash 字段出口陈旧头部已改名备份");
        }

        [Test]
        public void test_overridesSidecar_headerOnly_treatedAsMismatch()
        {
            // 文件级半截的另一方向(review Q5):头部在、载荷被删 ⇒ 同样视同失配。
            _asset.RemoveAllBindingOverrides();
            var store = new BindingsStore(_tempDir);
            store.Save(_asset, HashA);
            File.Delete(store.OverridesPath);

            LogAssert.Expect(LogType.Warning, new Regex("半截"));
            OverridesLoadResult result = store.Load(_asset, HashA);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Mismatch), "半截(头在载荷缺)⇒ 视同失配");
            AssertNoOverrides(Snapshot(_asset), "半截出口资产维持默认");
        }

        [Test]
        public void test_overridesSidecar_headerContent_writtenFields()
        {
            // 头部字段落盘面(review Q6):format_version / schema_hash / asset_id / asset_version
            // 逐字段断言 + 可选入参实证 —— 否则键名写错只靠 Applied 用例间接兜底。
            _asset.RemoveAllBindingOverrides();
            var store = new BindingsStore(_tempDir);

            store.Save(_asset, HashA, "AssetX", "v7");
            string header = File.ReadAllText(store.SchemaPath);

            Assert.That(header, Does.Contain($"format_version={BindingsStore.FormatVersion}"));
            Assert.That(header, Does.Contain($"schema_hash={HashA}"), "hash 键名与值都须落对");
            Assert.That(header, Does.Contain("asset_id=AssetX"), "显式 assetId 入参落盘");
            Assert.That(header, Does.Contain("asset_version=v7"), "显式 assetVersion 入参落盘");
            Assert.That(header, Does.Not.EndWith("\n"), "头部无尾换行(逐字节纪律)");
        }

        [Test]
        public void test_overridesSidecar_removedOverride_roundTripsDeleted()
        {
            // QA A2 Given「改 path / 增删 override」的删半边(review Q9):移除 override 后
            // Save → Load,删除不得经 sidecar 复活(出厂 API 吐陈旧条目 ⇒ 此测红)。
            _asset.RemoveAllBindingOverrides();
            InputActionAsset clone = CreateCleanClone();
            ApplyPathOverride(_asset, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            InputAction action = _asset.FindAction("Interact");
            Assert.That(action, Is.Not.Null, "Given:Interact 须在");
            int index = -1;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].path == "<Keyboard>/e")
                {
                    index = i;
                    break;
                }
            }
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Given:K&M 绑定须在");
            action.RemoveBindingOverride(index);
            Assert.That(action.bindings[index].overridePath, Is.Null, "Given:override 已移除");

            var store = new BindingsStore(_tempDir);
            store.Save(_asset, HashA);
            OverridesLoadResult result = store.Load(clone, HashA);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Applied), "空/残缺载荷仍是合法载荷");
            AssertNoOverrides(Snapshot(clone), "删除的 override 不得经 sidecar 复活");
        }

        // ══════════ AC-3-A5:落盘面白名单 ══════════

        [Test]
        public void test_writeSurface_scan_gameplayInput_onlyWhitelistedWriteSites()
        {
            // QA A5 主用例:扫描输入程序集全部 .cs —— 零拒绝清单符号 + 写盘点 ∈ 两白名单文件。
            var violations = new List<string>();
            var writeTargets = new List<string>();
            foreach (string file in Directory.GetFiles(GameplayInputDir, "*.cs", SearchOption.AllDirectories))
            {
                (List<string> v, List<string> t) = ScanWriteSurface(File.ReadAllText(file));
                violations.AddRange(v.Select(msg => $"{Path.GetFileName(file)}:{msg}"));
                writeTargets.AddRange(t);
            }

            Assert.That(violations, Is.Empty, "输入程序集零违例(拒绝清单 + 写点白名单)");
            Assert.That(writeTargets.Count, Is.EqualTo(4),
                $"写盘点须恰为 4 处(实际 {writeTargets.Count}:{string.Join(", ", writeTargets)})");
            // 基数断言保留 AC「仅…两处」原文(每文件两写点:Save 两处 + Story 005 恢复备份两处)。
            // 已知跨故事协调点(review F3 → Story 005 已同批兑现):失配恢复备份(File.Move)新增
            // 两写点(载荷 + 头部),白名单不新增文件、计数 2 → 4 —— 红 = 正确信号,不是假红。
            // 已知 fail-closed 约束(review Q4):① 同义词表按单文件建,他文件 const 别名解析不到
            // ⇒ 误报红;② 插值字符串里的白名单文件名字面量解析不到 ⇒ 同样误报红。
            // 两者方向均为红(非漏报绿);放宽须扩到程序集级常量扫描 —— 见集成 README 扫描器段。
            // Is.EquivalentTo 按多重性计集合(每文件两写点 ⇒ 原始列表 4 项)⇒ 先 Distinct 再断言目标集。
            Assert.That(writeTargets.Distinct(), Is.EquivalentTo(new[]
            {
                BindingsStore.OverridesFileName, BindingsStore.SchemaFileName,
            }), "写盘点目标(去重后)∈ {bindings.overrides.json, bindings.schema.txt}");
        }

        [Test]
        public void test_writeSurface_scan_playerPrefsFixture_flagsRed()
        {
            // QA A5 Negative:夹具源码含一行 PlayerPrefs 写入 ⇒ 扫描必须红。
            const string fixture = @"
class Fixture {
    void Write(string rebinds) {
        UnityEngine.PlayerPrefs.SetString(""InputBindings"", rebinds);
    }
}";
            (List<string> violations, List<string> _) = ScanWriteSurface(fixture);
            Assert.That(violations, Is.Not.Empty, "负例夹具必须红(AC-3-A5 拒绝清单)");
            Assert.That(string.Join(";", violations), Does.Contain("PlayerPrefs"),
                "违例须可定位到拒绝清单符号");
        }

        [Test]
        public void test_writeSurface_scan_nonWhitelistedWriteTarget_flagsRed()
        {
            // QA A5 Then 第一支的负例:写盘点指向白名单外文件 ⇒ 红。
            const string fixture = @"
class Fixture {
    void Write(string payload) {
        System.IO.File.WriteAllText(""/tmp/evil.json"", payload);
    }
}";
            (List<string> violations, List<string> _) = ScanWriteSurface(fixture);
            Assert.That(violations, Is.Not.Empty, "非白名单写点必须红");
            Assert.That(string.Join(";", violations), Does.Contain("白名单"),
                "违例须标明写点目标不在白名单");
        }

        [Test]
        public void test_writeSurface_scan_synonymAlias_passesAndRenameBypass_flagsRed()
        {
            // QA A5 Edge:「路径经常量/拼接间接引用(同义词表登记,防换名绕过)」。
            // 合法同义:常量别名指向白名单文件 ⇒ 解析通过、不误报。
            const string legit = @"
class Fixture {
    const string CustomName = ""bindings.overrides.json"";
    string PayloadPath => System.IO.Path.Combine(dir, CustomName);
    void Write(byte[] data) {
        System.IO.File.WriteAllBytes(PayloadPath, data);
    }
}";
            (List<string> legitViolations, List<string> legitTargets) = ScanWriteSurface(legit);
            Assert.That(legitViolations, Is.Empty, "同义词表须解析常量别名(不误报)");
            Assert.That(legitTargets, Is.EqualTo(new[] { BindingsStore.OverridesFileName }));

            // 换名绕过:常量指向非白名单文件 ⇒ 即使经别名间接引用仍红。
            const string bypass = @"
class Fixture {
    const string Sneaky = ""evil.json"";
    string SneakyPath => System.IO.Path.Combine(dir, Sneaky);
    void Write(byte[] data) {
        System.IO.File.WriteAllBytes(SneakyPath, data);
    }
}";
            (List<string> bypassViolations, List<string> _) = ScanWriteSurface(bypass);
            Assert.That(bypassViolations, Is.Not.Empty, "换名绕过必须红(别名解析不到白名单 ⇒ 拒)");
        }

        [Test]
        public void test_overridesSidecar_save_writeFailure_logsOnlyKeepsWritePoints()
        {
            // QA A5 Edge:「写失败只记日志不换写点」—— 目标路径被目录占位 ⇒ 写失败:
            // 返回 null + 错误日志;不产生任何替代文件(不换写点的可观测半边)。
            string blocker = Path.Combine(_tempDir, BindingsStore.OverridesFileName);
            Directory.CreateDirectory(blocker);
            var store = new BindingsStore(_tempDir);

            LogAssert.Expect(LogType.Error, new Regex("写入失败"));
            string result = store.Save(_asset, HashA);

            Assert.That(result, Is.Null, "写失败 ⇒ 返回 null(不抛给调用方)");
            Assert.That(Directory.GetFileSystemEntries(_tempDir), Is.EquivalentTo(new[] { blocker }),
                "目录内只有占位 —— 未创建任何替代写点文件;头部按「先载荷后头部」次序也未写");
        }

        [Test]
        public void test_overridesSidecar_headerWriteFailure_payloadAlone_mismatchNextLoad()
        {
            // 写失败的反向半边(review Q11):载荷写成、头部失败(头部路径被目录占位)——
            // Save 返回 null + 错误日志;留下的单半截在下次 Load 视同失配(「先载荷后头部」的安全方向)。
            var store = new BindingsStore(_tempDir);
            Directory.CreateDirectory(store.SchemaPath);   // 头部写点被占位

            LogAssert.Expect(LogType.Error, new Regex("写入失败"));
            string result = store.Save(_asset, HashA);

            Assert.That(result, Is.Null, "头部写失败 ⇒ Save 返回 null");
            Assert.That(File.Exists(store.OverridesPath), Is.True, "载荷已按「先载荷后头部」次序写成");
            Assert.That(Directory.Exists(store.SchemaPath), Is.True, "头部未写成(仍为占位目录)");

            LogAssert.Expect(LogType.Warning, new Regex("半截"));
            OverridesLoadResult loadResult = store.Load(_asset, HashA);

            Assert.That(loadResult, Is.EqualTo(OverridesLoadResult.Mismatch),
                "半截(载荷在头部缺)⇒ 下次 Load 视同失配,不尝试部分恢复");
        }

        [Test]
        public void test_writeSurface_scan_writeApiVariants_flagsRed()
        {
            // 扫描器模式盲区负例(review F2/Q3):Async 变体 / Move 第二实参 / new FileStream /
            // FileMode.Create / using 换名别名 —— 每一种都必须红(漏检即主扫描计数假绿)。
            const string asyncWrite =
                "class F { void W(string p) { System.IO.File.WriteAllTextAsync(\"/tmp/evil.json\", p); } }";
            const string moveSecondArg =
                "class F { void W() { System.IO.File.Move(\"bindings.overrides.json\", \"/tmp/evil.json\"); } }";
            const string fileStreamCreate =
                "class F { void W() { var s = new System.IO.FileStream(\"/tmp/evil.json\", System.IO.FileMode.Create); } }";
            const string openFileModeCreate =
                "class F { void W() { System.IO.File.Open(\"/tmp/evil.json\", System.IO.FileMode.Create); } }";
            const string usingAlias =
                "using IOFile = System.IO.File;\nclass F { void W(string p) { IOFile.WriteAllText(\"/tmp/evil.json\", p); } }";

            foreach (string fixture in new[] { asyncWrite, moveSecondArg, fileStreamCreate, openFileModeCreate, usingAlias })
            {
                (List<string> violations, List<string> _) = ScanWriteSurface(fixture);
                Assert.That(violations, Is.Not.Empty, $"写法盲区负例必须红:{fixture}");
                Assert.That(string.Join(";", violations), Does.Contain("白名单"),
                    $"违例须定位到白名单判据:{fixture}");
            }
        }

        // ══════════ AC-3-E3⑥:喂入字节 == 保存字节 ══════════

        [Test]
        public void test_sidecar_payloadFileBytes_equalSaveBytesVerbatim()
        {
            // QA E3⑥ 主用例:文件字节 == Save 返回值的 UTF-8 编码(逐字节、无附加换行)。
            _asset.RemoveAllBindingOverrides();
            ApplyPathOverride(_asset, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            var store = new BindingsStore(_tempDir);

            string json = store.Save(_asset, HashA);
            byte[] fileBytes = File.ReadAllBytes(store.OverridesPath);
            byte[] expectedBytes = Encoding.UTF8.GetBytes(json);

            Assert.That(json, Is.Not.Empty, "Given:本测需要非空载荷(有一处 override)");
            Assert.That(fileBytes, Is.EqualTo(expectedBytes),
                "文件字节 == Save 返回值 UTF-8 编码 —— 不附加换行、不 BOM、不重排");
        }

        [Test]
        public void test_sidecar_load_feedsSavedBytes_verbatimRoundTrip()
        {
            // QA E3⑥ 主用例运行期半边:读回文件字节解码后直接喂 Load ⇒ 生效与 Save 时一致。
            _asset.RemoveAllBindingOverrides();
            InputActionAsset clone = CreateCleanClone();
            ApplyPathOverride(_asset, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            ApplyPathOverride(_asset, "Move", "<Keyboard>/w", "<Keyboard>/z");
            var expected = Snapshot(_asset);
            var store = new BindingsStore(_tempDir);
            string saved = store.Save(_asset, HashA);

            string fed = Encoding.UTF8.GetString(File.ReadAllBytes(store.OverridesPath));
            OverridesLoadResult result = store.Load(clone, HashA);

            Assert.That(fed, Is.EqualTo(saved),
                "「喂入字节 == 保存字节」的字面断言 —— 读回的喂入内容不得偏离 Save 返回值(review F5/Q1)");
            Assert.That(result, Is.EqualTo(OverridesLoadResult.Applied));
            AssertSnapshotEqual(expected, Snapshot(clone), "喂入文件字节 = 保存字节");
        }

        [Test]
        public void test_sidecar_source_hasNoPayloadParsingCalls()
        {
            // QA E3⑥「扫描辅助:3 不解析」—— 扫 Gameplay.Input 全程序集而非单文件(review F1:
            // 载荷处理抽到别的文件必须同红),去注释 + 去字面量后零解析调用;
            // 另断 payload 标识符上的手工字符串操作(review Q12:「不解析」不限于「不用 JSON 库」)。
            string[] parseTokens =
            {
                "JsonUtility", "Newtonsoft", "JsonConvert", "JObject", "JToken",
                "JsonDocument", "JsonSerializer",
            };
            foreach (string file in Directory.GetFiles(GameplayInputDir, "*.cs", SearchOption.AllDirectories))
            {
                string code = StripLiterals(StripComments(File.ReadAllText(file)));
                foreach (string token in parseTokens)
                    Assert.That(code, Does.Not.Contain(token),
                        $"{Path.GetFileName(file)} 不得出现载荷解析调用/库({token})—— 只逐字节喂出厂 API");
                Assert.That(PayloadSurgeryPattern.IsMatch(code), Is.False,
                    $"{Path.GetFileName(file)} 对 payload 的手工字符串操作 = 违反「不解析不重写」");
            }

            // 负例夹具:谓词本身必须有牙(Q12)。
            const string fixture = "class F { void P(string payload) { string x = payload.Substring(1); } }";
            Assert.That(PayloadSurgeryPattern.IsMatch(StripLiterals(StripComments(fixture))), Is.True,
                "负例夹具(payload.Substring)必须被谓词命中(证明上循环的断言非空转)");
        }

        [Test]
        public void test_sidecar_nonAsciiOverridePath_byteRoundTrip()
        {
            // QA E3⑥ Edge:非 ASCII 绑定路径的字节保真(UTF-8 无损往返)。
            const string nonAsciiPath = "键位/é/路径绑定";
            _asset.RemoveAllBindingOverrides();
            InputActionAsset clone = CreateCleanClone();
            ApplyPathOverride(_asset, "Interact", "<Keyboard>/e", nonAsciiPath);
            var store = new BindingsStore(_tempDir);
            store.Save(_asset, HashA);

            OverridesLoadResult result = store.Load(clone, HashA);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Applied));
            Guid id = FindBinding(_asset, "Interact", "<Keyboard>/e").id;
            var actual = Snapshot(clone);
            Assert.That(actual[id].path, Is.EqualTo(nonAsciiPath),
                "非 ASCII 路径经 UTF-8 字节往返后逐字符一致");
        }

        [Test]
        public void test_sidecar_doubleSave_idempotentBytes()
        {
            // QA E3⑥ Edge:连续两次 Save 幂等(文件字节完全一致)。
            _asset.RemoveAllBindingOverrides();
            ApplyPathOverride(_asset, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            var store = new BindingsStore(_tempDir);

            string first = store.Save(_asset, HashA);
            byte[] payloadFirst = File.ReadAllBytes(store.OverridesPath);
            byte[] headerFirst = File.ReadAllBytes(store.SchemaPath);

            string second = store.Save(_asset, HashA);
            byte[] payloadSecond = File.ReadAllBytes(store.OverridesPath);
            byte[] headerSecond = File.ReadAllBytes(store.SchemaPath);

            Assert.That(second, Is.EqualTo(first), "两次 Save 返回值一致");
            Assert.That(payloadSecond, Is.EqualTo(payloadFirst), "载荷文件幂等");
            Assert.That(headerSecond, Is.EqualTo(headerFirst), "头部文件幂等");
        }

        [Test]
        public void test_sidecar_neg_beautifiedRewrite_differsFromSavedBytes()
        {
            // QA E3⑥ Negative:「插入美化/重排步骤 ⇒ 字节断言红」—— 本测是断言的**牙齿证明**
            // (正对照 + 失效签名:追加换行即不再等于文件字节),自证字节断言非空转;
            // 它不直接探测生产代码的美化改写(那一层由 test_sidecar_payloadFileBytes_equalSaveBytesVerbatim
            // 的主断言承担,与本测互补 —— review Q7 登记的适用面)。
            _asset.RemoveAllBindingOverrides();
            ApplyPathOverride(_asset, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            var store = new BindingsStore(_tempDir);

            string json = store.Save(_asset, HashA);
            byte[] fileBytes = File.ReadAllBytes(store.OverridesPath);
            byte[] beautified = Encoding.UTF8.GetBytes(json + "\n");

            Assert.That(fileBytes, Is.EqualTo(Encoding.UTF8.GetBytes(json)),
                "positive control:原字节 == Save 返回值编码");
            Assert.That(fileBytes, Is.Not.EqualTo(beautified),
                "失效签名:任何美化/追加换行都与文件字节不符 ⇒ 主断言(A2/E3⑥ 字节等)必红");
        }

        // ══════════ WriteSurface 扫描器(AC-3-A5 的执行体,住测试侧) ══════════

        private static readonly string[] AllowedWriteTargets =
        {
            BindingsStore.OverridesFileName, BindingsStore.SchemaFileName,
        };

        private static readonly string[] ForbiddenWriteTokens =
        {
            "PlayerPrefs", "EditorPrefs", "IEventSink", "Checkpoint",
        };

        // 写调用面(review F2/Q3):
        //   ① 写族方法名带可选 Async 后缀(WriteAllTextAsync 等);
        //   ② 不锚定 File. 前缀 —— `using IOFile = System.IO.File;` 之类的换名别名同红;
        //   ③ Create / Open / Move / Delete / Copy / Replace 与 new StreamWriter/FileStream
        //      一并入面;FileMode.Create 经实参解析覆盖,无需单列。
        // 误报方向说明(review Q4 登记的 fail-closed 取舍):Array.Copy / string.Replace 等
        // 通用同名调用会被要求首参解析到白名单 —— 解析不到即红(非漏报绿),误报可接受。
        private static readonly Regex WriteApiPattern = new Regex(
            @"\.\s*(?:WriteAllText|WriteAllBytes|WriteAllLines|WriteText|WriteBytes|AppendAllText|AppendAllLines|AppendText|CreateText|OpenWrite)(?:Async)?\s*\(" +
            @"|\.\s*(?:Create|Open|Move|Delete|Copy|Replace)\s*\(" +
            @"|\bnew\s+(?:[\w.]+\.)?(?:StreamWriter|FileStream)\s*\(",
            RegexOptions.Compiled);

        // 「解析载荷」的等价形态(review Q12):payload 标识符上的手工字符串手术。
        // payloadBytes 等前缀更长的标识符不会命中(其后不是 `.`)。
        private static readonly Regex PayloadSurgeryPattern = new Regex(
            @"payload\s*\.\s*(?:Substring|Replace|Split|Trim|Remove|Insert|Contains|StartsWith|EndsWith)",
            RegexOptions.Compiled);

        /// <summary>扫描一段 C# 源码:拒绝清单符号 + 写盘点白名单。
        /// 返回(违例消息, 已解析的写点目标)。注释剥离后再扫 —— 注释里的 API 名不构成写盘;
        /// 拒绝清单符号走**去字面量**文本 —— 日志串里出现 "PlayerPrefs" 一词不构成违规(review F8)。</summary>
        private static (List<string> violations, List<string> writeTargets) ScanWriteSurface(string source)
        {
            string code = StripComments(source);
            string symbolCode = StripLiterals(code);
            var violations = new List<string>();
            var writeTargets = new List<string>();

            foreach (string token in ForbiddenWriteTokens)
            {
                if (Regex.IsMatch(symbolCode, $@"\b{token}\b"))
                    violations.Add($"拒绝清单符号出现:{token}");
            }

            Dictionary<string, string> aliasToFile = BuildAliasMap(code);
            foreach (Match call in WriteApiPattern.Matches(code))
            {
                List<string> args = ExtractArguments(code, call);
                // Move / Copy 的目标是第二实参(review F2/Q3)—— 两实参都须在白名单,
                // File.Move(白名单, evil) 型绕过由第 2 实参判据拦下。
                bool checkSecond = call.Value.Contains("Move") || call.Value.Contains("Copy");
                int checkCount = checkSecond ? Math.Min(2, args.Count) : Math.Min(1, args.Count);
                for (int a = 0; a < checkCount; a++)
                {
                    string target = ResolveWriteTarget(args[a], aliasToFile);
                    if (target == null)
                        violations.Add($"写盘点目标不在白名单:{call.Value.Trim()} 第{a + 1}实参「{args[a].Trim()}」");
                    else if (a == 0)
                        writeTargets.Add(target);
                }
            }

            return (violations, writeTargets);
        }

        /// <summary>同义词表定点闭包:白名单字面量的常量 → 各常量/字段/表达式引用链上的名字
        /// 全部解析到对应白名单文件(防换名绕过:解析不到白名单的名字 = 违例)。</summary>
        private static Dictionary<string, string> BuildAliasMap(string code)
        {
            var defs = new List<(string name, string rhs)>();
            foreach (Match m in Regex.Matches(code, @"\bconst\s+string\s+(\w+)\s*=\s*([^;]+);"))
                defs.Add((m.Groups[1].Value, m.Groups[2].Value));
            foreach (Match m in Regex.Matches(code, @"\bstring\s+(\w+)\s*=\s*([^;]+);"))
                defs.Add((m.Groups[1].Value, m.Groups[2].Value));
            foreach (Match m in Regex.Matches(code, @"\bstring\s+(\w+)\s*=>\s*([^;]+)"))
                defs.Add((m.Groups[1].Value, m.Groups[2].Value));

            var map = new Dictionary<string, string>();
            foreach ((string name, string rhs) in defs)
            {
                foreach (string file in AllowedWriteTargets)
                {
                    if (rhs.Contains($"\"{file}\""))
                    {
                        map[name] = file;
                        break;
                    }
                }
            }

            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach ((string name, string rhs) in defs)
                {
                    if (map.ContainsKey(name))
                        continue;
                    foreach (KeyValuePair<string, string> kv in map)
                    {
                        if (Regex.IsMatch(rhs, $@"\b{Regex.Escape(kv.Key)}\b"))
                        {
                            map[name] = kv.Value;
                            grew = true;
                            break;
                        }
                    }
                }
            }
            return map;
        }

        private static string ResolveWriteTarget(string arg, Dictionary<string, string> aliasToFile)
        {
            foreach (string name in AllowedWriteTargets)
            {
                if (arg.Contains($"\"{name}\""))
                    return name;
            }
            foreach (Match id in Regex.Matches(arg, @"\b[A-Za-z_]\w*\b"))
            {
                if (aliasToFile.TryGetValue(id.Value, out string file))
                    return file;
            }
            return null;
        }

        /// <summary>取写调用的全部顶层实参(括号深度感知 —— Path.Combine(a, b) 内的逗号不截断)。
        /// 空实参列表时返回 [""](保证至少判一次,不给「零实参即免检」的漏检面)。</summary>
        private static List<string> ExtractArguments(string source, Match call)
        {
            var args = new List<string>();
            int open = call.Value.IndexOf('(');
            int start = call.Index + open + 1;
            int depth = 0;
            int argStart = start;
            for (int i = start; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    if (depth == 0)
                    {
                        args.Add(source.Substring(argStart, i - argStart));
                        return args;
                    }
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    args.Add(source.Substring(argStart, i - argStart));
                    argStart = i + 1;
                }
            }
            args.Add(source.Substring(argStart));
            return args;
        }

        /// <summary>剥离 // 与 /* */ 注释、保留字符串/字符字面量内容(状态机)。
        /// 逐字串 @"..." 按 "" 转义处理(review F8 —— 否则 @"C:\" 型内容会误吞后续代码)。</summary>
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

        /// <summary>剥离字符串/字符/逐字串字面量内容(保留代码骨架,字面量替换为空壳)——
        /// 符号断言(拒绝清单 / 解析 token / payload 手术)走此文本:日志串里提到
        /// "PlayerPrefs" 一词不构成违规(review F8),逐字串 "" 转义按规范处理。</summary>
        private static string StripLiterals(string source)
        {
            var sb = new StringBuilder(source.Length);
            int i = 0;
            while (i < source.Length)
            {
                char c = source[i];
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
