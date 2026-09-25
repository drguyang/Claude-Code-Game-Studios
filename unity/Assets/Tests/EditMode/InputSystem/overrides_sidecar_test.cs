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
        private static readonly string BindingsStoreSourcePath =
            Path.Combine(GameplayInputDir, "BindingsStore.cs");

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
            _asset.Disable();
            _asset.RemoveAllBindingOverrides();
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
                Assert.That(kv.Value.path, Is.Null, $"{because} —— binding {kv.Key} 不得带 override");
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
        public void test_overridesSidecar_hashMismatch_doesNotFeedPayload_fileUntouched()
        {
            // 读序负半边(实现期登记):hash 失配 ⇒ 不采用 + 记警告 + 载荷文件原样(恢复归 005)。
            _asset.RemoveAllBindingOverrides();
            ApplyPathOverride(_asset, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            var store = new BindingsStore(_tempDir);
            store.Save(_asset, HashA);
            byte[] payloadBefore = File.ReadAllBytes(store.OverridesPath);
            byte[] headerBefore = File.ReadAllBytes(store.SchemaPath);

            LogAssert.Expect(LogType.Warning, new Regex("失配"));
            OverridesLoadResult result = store.Load(_asset, HashB);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Mismatch), "失配 ⇒ 不喂");
            AssertNoOverrides(Snapshot(_asset), "失配出口资产维持默认");
            Assert.That(File.ReadAllBytes(store.OverridesPath), Is.EqualTo(payloadBefore),
                "失配不得触碰载荷文件(改名备份归 Story 005)");
            Assert.That(File.ReadAllBytes(store.SchemaPath), Is.EqualTo(headerBefore),
                "失配不得触碰头部文件");
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
            Assert.That(writeTargets.Count, Is.EqualTo(2),
                $"写盘点须恰为 2 处(实际 {writeTargets.Count}:{string.Join(", ", writeTargets)})");
            Assert.That(writeTargets, Is.EquivalentTo(new[]
            {
                BindingsStore.OverridesFileName, BindingsStore.SchemaFileName,
            }), "写盘点目标 ∈ {bindings.overrides.json, bindings.schema.txt}");
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
            store.Save(_asset, HashA);

            string fed = Encoding.UTF8.GetString(File.ReadAllBytes(store.OverridesPath));
            OverridesLoadResult result = store.Load(clone, HashA);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Applied));
            AssertSnapshotEqual(expected, Snapshot(clone), "喂入文件字节 = 保存字节");
        }

        [Test]
        public void test_sidecar_source_hasNoPayloadParsingCalls()
        {
            // QA E3⑥「扫描辅助:3 不解析」—— BindingsStore 源码(去注释)零 JSON 解析调用。
            string code = StripComments(File.ReadAllText(BindingsStoreSourcePath));
            string[] parseTokens =
            {
                "JsonUtility", "Newtonsoft", "JsonConvert", "JObject", "JToken",
                "JsonDocument", "JsonSerializer",
            };
            foreach (string token in parseTokens)
                Assert.That(code, Does.Not.Contain(token),
                    $"BindingsStore 不得出现载荷解析调用/库({token})—— 只逐字节喂出厂 API");
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
            // QA E3⑥ Negative:「插入美化/重排步骤 ⇒ 字节断言红」—— 本测证明该断言有牙:
            // 在 Save 返回值后追加一个换行即不再等于文件字节。
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

        private static readonly Regex WriteApiPattern = new Regex(
            @"\bFile\.(?:WriteAllText|WriteAllBytes|WriteAllLines|WriteText|WriteBytes|AppendAllText|AppendAllLines|AppendText|CreateText|Create|OpenWrite|Move|Delete|Copy)\s*\(" +
            @"|\bnew\s+StreamWriter\s*\(" +
            @"|\bFile\.Open\s*\([^;]*?FileMode\.(?:Write|Append|ReadWrite|OpenOrCreate)",
            RegexOptions.Compiled);

        /// <summary>扫描一段 C# 源码:拒绝清单符号 + 写盘点白名单。
        /// 返回(违例消息, 已解析的写点目标)。注释剥离后再扫 —— 注释里的 API 名不构成写盘。</summary>
        private static (List<string> violations, List<string> writeTargets) ScanWriteSurface(string source)
        {
            string code = StripComments(source);
            var violations = new List<string>();
            var writeTargets = new List<string>();

            foreach (string token in ForbiddenWriteTokens)
            {
                if (Regex.IsMatch(code, $@"\b{token}\b"))
                    violations.Add($"拒绝清单符号出现:{token}");
            }

            Dictionary<string, string> aliasToFile = BuildAliasMap(code);
            foreach (Match call in WriteApiPattern.Matches(code))
            {
                string arg = ExtractFirstArgument(code, call);
                string target = ResolveWriteTarget(arg, aliasToFile);
                if (target == null)
                    violations.Add($"写盘点目标不在白名单:{call.Value.Trim()} 首参「{arg.Trim()}」");
                else
                    writeTargets.Add(target);
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

        /// <summary>取写调用的第一个实参(括号深度感知 —— Path.Combine(a, b) 内的逗号不截断)。</summary>
        private static string ExtractFirstArgument(string source, Match call)
        {
            int open = call.Value.IndexOf('(');
            int start = call.Index + open + 1;
            int depth = 0;
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
                        return source.Substring(start, i - start);
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    return source.Substring(start, i - start);
                }
            }
            return source.Substring(start);
        }

        /// <summary>剥离 // 与 /* */ 注释、保留字符串/字符字面量内容(状态机)。</summary>
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
    }
}
