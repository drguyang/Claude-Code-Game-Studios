// Story 005 · schema hash 失配优雅清空与跨版本备份(AC-3-A3 / A8 / E3③ 三条 BLOCKING 的全部测试真身)
//
// 权威来源:production/epics/input-system/story-005-hash-mismatch-recovery.md(判据权威,AC/QA 原文)
//   · AC-3-A3 hash 失配 ⇒ 改名备份 + 载入默认 + 日志;不静默、不崩溃、不弹窗
//   · AC-3-A8 跨版本升级(bindingId 全变)⇒ hash 变 ⇒ 陈旧文件改名备份 + 日志可诊断(三要素)+ 载入默认
//   · AC-3-E3③ bindingId 变化 ⇒ hash 必变(失配检测的使能性质)
//   · GDD input-system.md 规则五 step 4(保留旧文件改名备份 + 载入默认 + 记日志)·
//     Edge Cases 二(连续多次失配不互相覆盖 / 只读目录跳过备份仍默认 / 损坏半截视同失配 / 首次启动不触发备份)
//   · ADR-011 §一 F3 + Amendment A ③(hash 含 bindingId)·
//     ADR-010(overrides 不进存档、不进三流)
//
// 落点注记:故事 Test Evidence 登记口径 tests/integration/input_system/hash_mismatch_recovery_test.cs;
//   Unity 只编译 unity/Assets/ 树 ⇒ 真身落本路径(README 落点说明同批)。
//
// 纪律:QA Test Cases 逐字映射,不发明原文之外的场景;实现期登记块(故事文件)记录
//   QA 未逐字点名但属 Then/读序直接蕴含的忠实扩展。确定性 · 无随机 · 无时间依赖 ·
//   每测试自建临时目录。文件系统访问是本故事 AC 的直接对象(备份/恢复/落盘),
//   与「单测不依赖文件系统」通例的出入已在 Test Evidence 落点注记中声明。
//
// 夹具约定:
//   · 共享资产(clone)与「内存重建资产」两条真源线:前者 = QA A3「前缀替换合成 id」路线,
//     后者 = QA A8「bindingId 全变」真实重建路线(引擎 run-time AddBinding/ChangeBinding 全 API)。
//   · 内存重建资产通过 ScriptableObject.CreateInstance<InputActionAsset>() 构造 ——
//     run-time 构造(非资产序列化),与共享资产并存测试 = 零 SaveAssets / SetDirty
//     (suite 级约束,Story 004 Completion Notes 登记:测试不得对资产 SaveAssets)。

using System;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>Story 005 · 失配优雅清空与跨版本备份 三条 BLOCKING AC 的 EditMode 测试。</summary>
    [TestFixture]
    internal sealed class HashMismatchRecoveryTest
    {
        private const string HashA = "schema-hash-fixture-a";
        private const string HashB = "schema-hash-fixture-b";

        // Story 004 Completion Notes Q4(Q4 钉值夹具归 Story 005):由 Python 独立复算的
        // FNV-1a-64 钉死值 —— canon 编码若变,此断言即红 ⇒ 既有 bindings.schema.txt 头部
        // 会集体失配被误触发(本测是第一道哨兵)。
        private const string GoldenEmptySetHash = "cbf29ce484222325";   // 空记录流(基准值)
        private const string GoldenSingleRecordHash = "930d0dc7dd56f620";   // MakeRecord 单记录
        private const string GoldenRecordWithProcessorHash = "7c9f7f3022a44303";   // processors=["clamp"]

        private static readonly string RepoRoot = ComputeRepoRoot();

        private string _tempDir;
        private InputActionAsset _shared;
        private InputActionAsset _clone;

        private static string ComputeRepoRoot([CallerFilePath] string callerPath = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath), "..", "..", "..", "..", ".."));

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "dayj_hash_mismatch_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _shared = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Assert.That(_shared, Is.Not.Null, "Given:动作资产须可加载(Story 001 交付的单实例源)");
            _shared.RemoveAllBindingOverrides();
        }

        [TearDown]
        public void TearDown()
        {
            if (_clone != null)
            {
                UnityEngine.Object.DestroyImmediate(_clone);
                _clone = null;
            }
            if (_shared != null)
            {
                _shared.Disable();
                _shared.RemoveAllBindingOverrides();
            }
            Directory.Delete(_tempDir, true);
        }

        // ══════════ helpers ══════════

        /// <summary>干净克隆(施加 override 之前调用):深拷贝、binding Guid 与共享资产一致。</summary>
        private InputActionAsset CreateCleanClone()
        {
            _clone = UnityEngine.Object.Instantiate(_shared);
            _clone.name = _shared.name + "_hashMismatchClone";
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
            Assert.Fail($"Given:动作 {actionName} 无 path = {bindingPath} 的绑定");
        }

        private static Dictionary<Guid, (string path, string processors, string interactions)> Snapshot(InputActionAsset asset)
        {
            var map = new Dictionary<Guid, (string, string, string)>();
            foreach (InputBinding b in asset.bindings)
                map[b.id] = (b.overridePath, b.overrideProcessors, b.overrideInteractions);
            return map;
        }

        private static void AssertNoOverrides(
            Dictionary<Guid, (string path, string processors, string interactions)> snapshot, string because)
        {
            foreach (var kv in snapshot)
            {
                Assert.That(kv.Value.path, Is.Null, $"{because} —— binding {kv.Key} 不得带 override");
                Assert.That(kv.Value.processors, Is.Null, $"{because} —— binding {kv.Key} 不得带 overrideProcessors");
                Assert.That(kv.Value.interactions, Is.Null, $"{because} —— binding {kv.Key} 不得带 overrideInteractions");
            }
        }

        // ─── 内存重建资产(QA A8「bindingId 全变」的真实重建路线) ───

        /// <summary>run-time 构造动作资产(非资产序列化):map + action + 两条绑定(含一条复合容器)。</summary>
        private static InputActionAsset CreateInMemoryAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = asset.AddActionMap("Gameplay");
            InputAction move = map.AddAction("Move", InputActionType.Value);
            move.AddBinding("<Keyboard>/w");
            move.AddBinding("<Keyboard>/s");
            // 复合容器绑定(isComposite = true)覆盖 E3③ 的复合成员面;复合 part 不属于本故事 AC,不构造。
            InputAction interact = map.AddAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e").WithName("compositeContainer").WithGroups("Keyboard");
            return asset;
        }

        // ══════════ AC-3-A3:合成失配 ⇒ 备份 + 默认 + 日志 ══════════

        [Test]
        public void test_hashMismatch_syntheticMismatch_backupCreatedDefaultsLoaded_logged()
        {
            // QA A3 主用例:合法 sidecar + 内存构造/前缀替换合成失配 ⇒ ①备份已生成(非删除)
            // ②生效绑定 = 默认 ③未抛异常 ④有日志行。
            _shared.RemoveAllBindingOverrides();
            ApplyPathOverride(_shared, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            InputActionAsset clone = CreateCleanClone();
            var store = new BindingsStore(_tempDir);
            store.Save(_shared, HashA);   // 头部 = HashA,载荷含 override

            // 牙齿:改头部 hash 值(合成失配)⇒ 与 SchemaHash 语义无关,纯失配路径断言。
            byte[] payloadBefore = File.ReadAllBytes(store.OverridesPath);
            byte[] headerBefore = File.ReadAllBytes(store.SchemaPath);
            LogAssert.Expect(LogType.Warning, new Regex("失配"));
            LogAssert.Expect(LogType.Warning, new Regex("备份路径"));
            LogAssert.Expect(LogType.Warning, new Regex("恢复完成"));
            OverridesLoadResult result = store.Load(clone, HashB);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Mismatch), "失配 ⇒ 不喂");
            AssertNoOverrides(Snapshot(clone), "失配出口生效绑定 = 默认(两步已清,载荷未生效)");
            Assert.That(File.ReadAllBytes(store.OverridesPath + ".bak-001"), Is.EqualTo(payloadBefore),
                "① 陈旧载荷已改名备份、逐字节保留(非删除)");
            Assert.That(File.ReadAllBytes(store.SchemaPath + ".bak-001"), Is.EqualTo(headerBefore),
                "① 陈旧头部已改名备份、逐字节保留");
            Assert.That(File.Exists(store.OverridesPath), Is.False, "原路径已移除(后续 Save 可重建)");
            Assert.That(File.Exists(store.SchemaPath), Is.False, "原路径已移除(后续 Save 可重建)");
        }

        [Test]
        public void test_hashMismatch_doubleMismatch_twoBackupsCoexist_noOverwrite()
        {
            // QA A3 Edge:连续两次失配 ⇒ 两份备份并存(不覆盖)。
            // 失配恢复会移走原文件 ⇒ 第二次失配前须再 Save 重建原路径(现实语义:两次启动各
            // 先落盘再失配);.bak-001 逐字节保留、第二次落 .bak-002。
            _shared.RemoveAllBindingOverrides();
            var store = new BindingsStore(_tempDir);
            store.Save(_shared, HashA);

            LogAssert.Expect(LogType.Warning, new Regex("失配"));
            LogAssert.Expect(LogType.Warning, new Regex("备份路径"));
            LogAssert.Expect(LogType.Warning, new Regex("恢复完成"));
            store.Load(_shared, HashB);   // 第一次失配 → .bak-001

            byte[] payloadAfterFirst = File.ReadAllBytes(store.OverridesPath + ".bak-001");
            store.Save(_shared, HashA);   // 重建原路径(下次启动前的正常落盘)
            LogAssert.Expect(LogType.Warning, new Regex("失配"));
            LogAssert.Expect(LogType.Warning, new Regex("备份路径"));
            LogAssert.Expect(LogType.Warning, new Regex("恢复完成"));
            OverridesLoadResult result = store.Load(_shared, HashB);   // 第二次失配 → .bak-002

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Mismatch), "第二次失配仍走失配路径");
            Assert.That(File.ReadAllBytes(store.OverridesPath + ".bak-001"), Is.EqualTo(payloadAfterFirst),
                "第一份备份未被第二次失配覆盖(逐字节保留)");
            Assert.That(File.Exists(store.OverridesPath + ".bak-002"), Is.True,
                "第二次失配落 .bak-002(两份备份并存,不互相覆盖)");
        }

        [Test]
        public void test_hashMismatch_backupDirUnwritable_skipsBackupStillDefaults_noCrash()
        {
            // QA A3 Edge:备份目录不可写 ⇒ 记日志跳过备份但仍载入默认、仍不崩。
            // 手法:.bak-001 路径被目录占位(File.Exists 对目录返回 false ⇒ 序号探测放行,
            // File.Move 到已存在目录抛 IOException)⇒ 载荷/头部各自备份失败、只记日志。
            _shared.RemoveAllBindingOverrides();
            ApplyPathOverride(_shared, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            InputActionAsset clone = CreateCleanClone();
            var store = new BindingsStore(_tempDir);
            store.Save(_shared, HashA);
            Directory.CreateDirectory(store.OverridesPath + ".bak-001");
            Directory.CreateDirectory(store.SchemaPath + ".bak-001");

            // LogScope 逐事件匹配队头 Expect:失配事件 ×1 + 两条备份失败事件(载荷/头部各一)。
            // 「仍载入默认」由测试体 AssertNoOverrides 断言(日志侧「跳过备份」= 备份失败事件已消费)。
            LogAssert.Expect(LogType.Warning, new Regex("失配"));
            LogAssert.Expect(LogType.Warning, new Regex("备份失败"));
            LogAssert.Expect(LogType.Warning, new Regex("备份失败"));
            OverridesLoadResult result = store.Load(clone, HashB);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Mismatch), "仍走失配路径,不崩");
            AssertNoOverrides(Snapshot(clone), "仍载入默认(跳过备份 ≠ 不清空)");
            Assert.That(File.Exists(store.OverridesPath), Is.True,
                "备份失败后原文件未被删除(仅跳过改名,不得静默删)");
            Assert.That(Directory.Exists(store.OverridesPath + ".bak-001"), Is.True, "占位目录仍在(未被覆盖)");
        }

        [Test]
        public void test_hashMismatch_partialBackup_scopeLogNamesOnlyMovedSide()
        {
            // qa-tester 评审发现(real bug 修复的回归钉):「恢复完成」日志的范围必须由**实际移动结果**
            // 派生(载荷成功 + 头部失败 ⇒ 只点「overrides」,不得称「overrides 与头部」均已备份)。
            // 手法:头部 .bak-001 被目录占位 ⇒ 头部移动失败;载荷移动成功 ⇒ 断言范围点名只含 overrides。
            _shared.RemoveAllBindingOverrides();
            ApplyPathOverride(_shared, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            InputActionAsset clone = CreateCleanClone();
            var store = new BindingsStore(_tempDir);
            store.Save(_shared, HashA);
            Directory.CreateDirectory(store.SchemaPath + ".bak-001");   // 只占头部备份位

            // 事件流:失配 ×1 → 载荷备份成功(备份路径) → 头部备份失败 ×1 → 恢复完成。
            // LogScope 队头匹配:「备份失败」期望落在头部失败行上(载荷成功行含「备份路径」不匹配)。
            LogAssert.Expect(LogType.Warning, new Regex("失配"));
            LogAssert.Expect(LogType.Warning, new Regex("备份路径"));
            LogAssert.Expect(LogType.Warning, new Regex("备份失败"));
            // 恢复完成行范围只点「overrides」:若实现按存在性派生(hasPayload && hasHeader)⇒ 此期望红。
            LogAssert.Expect(LogType.Warning, new Regex("陈旧overrides已改名备份"));
            OverridesLoadResult result = store.Load(clone, HashB);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Mismatch), "部分备份仍走失配路径,不崩");
            Assert.That(File.Exists(store.OverridesPath + ".bak-001"), Is.True, "载荷侧移动成功、已改名备份");
            Assert.That(File.Exists(store.OverridesPath), Is.False, "载荷原路径已移除");
            Assert.That(Directory.Exists(store.SchemaPath + ".bak-001"), Is.True, "头部备份位仍为占位目录");
            Assert.That(File.Exists(store.SchemaPath), Is.True, "头部移动失败 ⇒ 原文件仍在(未删除)");
            AssertNoOverrides(Snapshot(clone), "部分备份出口资产维持默认(载荷未生效)");
        }

        [Test]
        public void test_hashMismatch_backupSerialExhaust_skipsWithLogDefaultsNoCrash()
        {
            // qa-tester 评审发现:999 个备份序号全被占用 ⇒ 跳过备份时原实现不记日志,与「备份失败」
            // 的日志契约不一致(「失效是响的」的备份侧缺口)。占满 .bak-001..999 两族(载荷/头部),
            // 断言两侧都发出「备份跳过」日志、原文件保留、仍载入默认、不崩。
            _shared.RemoveAllBindingOverrides();
            ApplyPathOverride(_shared, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            InputActionAsset clone = CreateCleanClone();
            var store = new BindingsStore(_tempDir);
            store.Save(_shared, HashA);
            for (int serial = 1; serial <= 999; serial++)
            {
                string suffix = string.Format(BindingsStore.BackupSuffixFormat, serial);
                File.WriteAllBytes(store.OverridesPath + suffix, Array.Empty<byte>());
                File.WriteAllBytes(store.SchemaPath + suffix, Array.Empty<byte>());
            }

            // 事件序:失配 ×1 → 载荷备份跳过 ×1 → 头部备份跳过 ×1;两侧均失败 ⇒ 无「恢复完成」行。
            LogAssert.Expect(LogType.Warning, new Regex("失配"));
            LogAssert.Expect(LogType.Warning, new Regex("载荷备份跳过"));
            LogAssert.Expect(LogType.Warning, new Regex("头部备份跳过"));
            OverridesLoadResult result = store.Load(clone, HashB);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Mismatch), "序号耗尽仍走失配路径,不崩");
            AssertNoOverrides(Snapshot(clone), "序号耗尽出口仍载入默认(跳过备份 ≠ 不清空)");
            Assert.That(File.Exists(store.OverridesPath), Is.True,
                "序号耗尽 ⇒ 不覆盖任何前次备份,原文件保留(未移动)");
            Assert.That(File.Exists(store.SchemaPath), Is.True, "头部原文件保留(未移动)");
            Assert.That(Directory.GetFiles(_tempDir, "*.bak-*").Length, Is.EqualTo(1998),
                "999 × 2 个备份占位全部保留(不覆盖、不清理既有备份)");
        }

        [Test]
        public void test_hashMismatch_headerWriteFailure_payloadAlone_mismatchNextLoad_backedUp()
        {
            // qa-tester GAPS #4(孤侧备份)· 直击手法:Save 的载荷成功 + 头部写失败留下的**单半截**
            // (载荷在场、头部路径被目录占位)—— 下次 Load 的 File.Exists(载荷)=真、File.Exists(头部)=假
            // ⇒ 走半截出口(不经过读取 catch)⇒ BackupSidecar(null, …) 把孤侧载荷改名备份。
            _shared.RemoveAllBindingOverrides();
            ApplyPathOverride(_shared, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            var store = new BindingsStore(_tempDir);
            Directory.CreateDirectory(store.SchemaPath);   // 头部写点被占位 ⇒ Save 载荷成功、头部失败

            LogAssert.Expect(LogType.Error, new Regex("写入失败"));
            string result = store.Save(_shared, HashA);
            Assert.That(result, Is.Null, "Given:头部写失败 ⇒ Save 返回 null");
            Assert.That(File.Exists(store.OverridesPath), Is.True, "Given:载荷已按「先载荷后头部」写成");
            Assert.That(Directory.Exists(store.SchemaPath), Is.True, "Given:头部未写成(仍为占位目录)");

            LogAssert.Expect(LogType.Warning, new Regex("半截"));
            LogAssert.Expect(LogType.Warning, new Regex("备份路径"));
            LogAssert.Expect(LogType.Warning, new Regex("恢复完成"));
            OverridesLoadResult loadResult = store.Load(_shared, HashA);

            Assert.That(loadResult, Is.EqualTo(OverridesLoadResult.Mismatch), "半截 ⇒ 视同失配,不尝试部分恢复");
            AssertNoOverrides(Snapshot(_shared), "半截出口资产维持默认");
            Assert.That(File.Exists(store.OverridesPath + ".bak-001"), Is.True,
                "孤侧载荷已改名备份(非删除)");
            Assert.That(File.Exists(store.OverridesPath), Is.False, "载荷原路径已移除(后续 Save 可重建)");
        }

        // 注:「头部读取失败 / 载荷读取失败」两个 catch 出口不单设直击测试 —— 理由:
        // ① 出口调用的 BackupSidecar 与已测出口(headerMissingSchemaHash = storedHash=null 形状 ·
        // 主失配 = 两文件在场合 hash 失配形状)是**同一代码**,备份行为已被双层覆盖;
        // ② 触发差异在 BCL 的 ReadAllBytes 抛 IO/Unauthorized,需平台私有故障注入
        //    (权限位 / 文件锁;`SetUnixFileMode` 是 .NET 7+ API,不在 Unity 6.3 netstandard2.1
        //    面内;目录占位会先被 File.Exists 判假、走半截分支,到不了 catch);
        // ③ 该 catch 的行为契约(视同失配 + 不抛给调用方)属 Story 003 既有 fail-safe,
        //    非 Story 005 新增代码。故登记为覆盖注记,不为此引入脆弱/不可移植夹具。
        // 半截出口的孤侧备份断言在 overrides_sidecar_test.cs 三条半截测试(同批扩展)。

        [Test]
        public void test_hashMismatch_neg_clearWithoutRename_stillBackedUp()
        {
            // QA A3 Negative:「只清空不改名备份」⇒ ①红 —— 反证:本实现改名备份后原路径
            // 已移除,若实现退化为「只清空」则 .bak-001 不存在、本测红。
            _shared.RemoveAllBindingOverrides();
            ApplyPathOverride(_shared, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            var store = new BindingsStore(_tempDir);
            store.Save(_shared, HashA);

            LogAssert.Expect(LogType.Warning, new Regex("失配"));
            LogAssert.Expect(LogType.Warning, new Regex("备份路径"));
            LogAssert.Expect(LogType.Warning, new Regex("恢复完成"));
            store.Load(_shared, HashB);

            Assert.That(File.Exists(store.OverridesPath + ".bak-001"), Is.True,
                "反证:存在改名备份(若实现只清空 → 红)");
        }

        // ══════════ AC-3-A8:跨版本升级(bindingId 全变)⇒ 备份 + 可诊断日志 + 载入默认 ══════════

        [Test]
        public void test_hashMismatch_crossVersionRebuild_allBindingIdsChanged_backupDiagnosticLogDefault()
        {
            // QA A8 主用例:旧版资产 + 其 overrides 文件(有效);新版资产重建后 bindingId 全不同。
            // When 新版启动装载 ⇒ 头部 hash 失配 ⇒ 备份生成;日志含旧 hash / 新 hash / 备份路径;
            // 生效绑定 = 新版默认;失效是响的(三要素齐,非无痕清空)。
            _shared.RemoveAllBindingOverrides();
            ApplyPathOverride(_shared, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            var store = new BindingsStore(_tempDir);
            // 旧版 sidecar 落**旧资产的真实 hash**(非合成夹具值)—— 这才是跨版本升级的真源语义:
            // 头部存的是旧版结构的真实指纹。
            string oldHash = SchemaHash.ComputeSchemaHash(_shared);
            store.Save(_shared, oldHash);

            // 「新版资产」= 内存重建(引擎 AddBinding 生成全新 bindingId)—— 真实重建,非合成前缀。
            InputActionAsset rebuilt = CreateInMemoryAsset();
            // 牙齿:新版资产的真实 hash 由 BuildRecords 独立重算,且必须与旧版真实 hash 失配 ——
            // 这证明「重建 ⇒ bindingId 全变 ⇒ hash 必变 ⇒ 失配被检测」的全链路端到端(A8 使能性质)。
            string rebuiltHash = SchemaHash.ComputeSchemaHash(rebuilt);
            Assert.That(rebuiltHash, Is.Not.EqualTo(oldHash), "Given:重建资产的真实 hash 必须 ≠ 旧版真实 hash");
            LogAssert.Expect(LogType.Warning, new Regex("失配"));
            LogAssert.Expect(LogType.Warning, new Regex("备份路径"));
            LogAssert.Expect(LogType.Warning, new Regex("恢复完成"));
            OverridesLoadResult result = store.Load(rebuilt, rebuiltHash);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Mismatch), "跨版本失配 ⇒ 不喂");
            AssertNoOverrides(Snapshot(rebuilt), "生效绑定 = 新版默认(旧 override 未错位生效)");
            Assert.That(File.Exists(store.OverridesPath + ".bak-001"), Is.True,
                "陈旧 overrides 已改名备份(非删除)");
            Assert.That(File.Exists(store.SchemaPath + ".bak-001"), Is.True, "陈旧头部已改名备份");
            UnityEngine.Object.DestroyImmediate(rebuilt);
        }

        [Test]
        public void test_hashMismatch_firstLaunch_noFiles_noBackupCreated()
        {
            // QA A8 Edge:旧文件根本不存在(首次启动)⇒ 走正常首载,不触发备份分支。
            _shared.RemoveAllBindingOverrides();
            var store = new BindingsStore(_tempDir);   // SetUp 建的空目录,无任何文件

            OverridesLoadResult result = store.Load(_shared, HashA);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.NoSidecar), "首次启动走正常路径");
            Assert.That(Directory.GetFileSystemEntries(_tempDir), Is.Empty,
                "首载不得触发任何备份/写盘(目录保持空 —— 备份分支不触发)");
        }

        [Test]
        public void test_hashMismatch_corruptHeaderReadsAsMismatch_backupCreated()
        {
            // QA A8 Edge:文件损坏(hash 读不出)⇒ 同样走失配分支(视为失配)。
            // 头部内容被改成非键值行(读不出 schema_hash)⇒ 视同失配,备份 + 默认。
            _shared.RemoveAllBindingOverrides();
            ApplyPathOverride(_shared, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            var store = new BindingsStore(_tempDir);
            store.Save(_shared, HashA);
            File.WriteAllBytes(store.SchemaPath, Encoding.UTF8.GetBytes("\u0000\u0001\u0002\u0003"));

            LogAssert.Expect(LogType.Warning, new Regex("schema_hash"));
            LogAssert.Expect(LogType.Warning, new Regex("备份路径"));
            LogAssert.Expect(LogType.Warning, new Regex("恢复完成"));
            OverridesLoadResult result = store.Load(_shared, HashA);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Mismatch), "损坏头部 ⇒ 视同失配");
            AssertNoOverrides(Snapshot(_shared), "损坏出口资产维持默认");
            Assert.That(File.Exists(store.OverridesPath + ".bak-001"), Is.True,
                "损坏的 sidecar 已整体改名备份(非删除)");
            Assert.That(File.Exists(store.SchemaPath + ".bak-001"), Is.True, "头部备份");
        }

        [Test]
        public void test_hashMismatch_neg_logContainsAllThreeDiagnosticElements()
        {
            // QA A8 Negative:日志不含 hash ⇒ ④「可诊断」红 —— 反证:断言备份日志行的
            // 三要素(旧 hash / 新 hash / 备份路径)同现一行,实现若只记「失配」不记要素 ⇒ 红。
            // 不用 Assert.Pass 收尾(SuccessException 短路 LogScope 求值,期望永不核验);
            // 用普通断言保持测试体贯通,让 LogAssert 期望在测试末尾被 EvaluateLogScope 消费。
            _shared.RemoveAllBindingOverrides();
            var store = new BindingsStore(_tempDir);
            store.Save(_shared, HashA);

            LogAssert.Expect(LogType.Warning, new Regex("失配"));
            // 三要素同现一行:载荷/头部备份行的原文为 `...fixture-b);备份路径 /tmp/...` ——
            // 分隔符 `;` 与「备份路径」之间无空格,正则不得在「备份路径」前写空格(否则永不匹配)。
            LogAssert.Expect(LogType.Warning, new Regex("旧 hash .* 新 hash .*备份路径"));
            LogAssert.Expect(LogType.Warning, new Regex("恢复完成"));
            OverridesLoadResult result = store.Load(_shared, HashB);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.Mismatch),
                "失配出口可诊断(三要素日志被 LogAssert 逐项消费;任一缺失 ⇒ 未决断言红)");
        }

        // ══════════ AC-3-E3③:bindingId 变化 ⇒ hash 必变 ══════════

        [Test]
        public void test_schemaHash_bindingIdDiffers_hashDiffers()
        {
            // QA E3③ 主用例:两条 R,唯一差异 = bindingId ⇒ hash 必不同(失配检测的使能性质)。
            BindingSchemaRecord other = new BindingSchemaRecord(
                "MapA", "ActionA", 0, "aaaaaaaa-bbbb-cccc-dddd-eeeeeee0",
                "binding-name", "<Keyboard>/e", false, false,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

            Assert.That(HashOf(MakeRecord()), Is.Not.EqualTo(HashOf(other)),
                "仅 bindingId 不同的两条记录 hash 必不同(重建 ⇒ GUID 重生成 ⇒ hash 必变)");
        }

        [Test]
        public void test_schemaHash_singleBindingIdChange_hashDiffers()
        {
            // QA E3③ Edge:只改一个 binding 的 id(非全变)⇒ 亦不同。
            // 实现:两条多记录流,唯一差异 = 其中一条记录的 bindingId。
            BindingSchemaRecord a = MakeRecord("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            BindingSchemaRecord b = MakeRecord("bbbbbbbb-bbbb-cccc-dddd-eeeeeeeeeeee");
            BindingSchemaRecord c = MakeRecord();   // 与 a 相同(做「只改一条」的对照)
            var left = new[] { a, c };
            var right = new[] { b, c };

            Assert.That(HashOf(left), Is.Not.EqualTo(HashOf(right)),
                "多记录流中只改一条 bindingId ⇒ hash 必不同");
        }

        [Test]
        public void test_schemaHash_sameBindingIds_orderChanged_hashSame()
        {
            // QA E3③ Edge:bindingId 相同但顺序变 ⇒ hash 相同(排序吸收)。
            // 实现:两条记录对调顺序,id 集合一致 ⇒ hash 必同。
            BindingSchemaRecord a = MakeRecord("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            BindingSchemaRecord b = MakeRecord("bbbbbbbb-bbbb-cccc-dddd-eeeeeeeeeeee");

            Assert.That(HashOf(a, b), Is.EqualTo(HashOf(b, a)),
                "记录顺序被 sort(R) 吸收 ⇒ hash 相同(集合序同理)");
        }

        // ══════════ Story 004 登记 Q4:钉值夹具(Q4 归 Story 005) ══════════

        [Test]
        public void test_schemaHash_pinnedGoldenFixtures_guardCanonStability()
        {
            // Story 004 Completion Notes Q4:无「固定输入 ⇒ 钉死 hash 字符串」golden 夹具;
            // canon 编码若变,既有 bindings.schema.txt 头部 hash 集体失配 ⇒ Story 005 清空被误触发。
            // 本测 = 第一道哨兵(独立复算值,见上方常量说明)。
            Assert.That(SchemaHash.ComputeSchemaHash(Array.Empty<BindingSchemaRecord>()),
                Is.EqualTo(GoldenEmptySetHash), "空记录流 hash == FNV-1a-64 基准值");
            Assert.That(HashOf(MakeRecord()), Is.EqualTo(GoldenSingleRecordHash),
                "MakeRecord 单记录 hash == 独立复算钉值");
            Assert.That(HashOf(MakeRecord(processors: new[] { "clamp" })),
                Is.EqualTo(GoldenRecordWithProcessorHash), "含 processors 记录 hash == 独立复算钉值");
        }

        // ══════════ Story 003 CorruptPayload 出口的备份扩展(QA「损坏视同失配」的载荷侧) ══════════

        [Test]
        public void test_hashMismatch_corruptPayload_routeToBackupAndDefault()
        {
            // QA A8 Edge「文件损坏(hash 读不出)」的载荷侧 + QA「损坏 ⇒ 视同失配」:
            // 头部完好(匹配)但载荷被出厂 API 拒收(损坏 JSON)⇒ CorruptPayload 出口也走备份 + 默认。
            _shared.RemoveAllBindingOverrides();
            ApplyPathOverride(_shared, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            InputActionAsset clone = CreateCleanClone();
            var store = new BindingsStore(_tempDir);
            store.Save(_shared, HashA);

            // 篡改:头部完好(匹配),载荷截断(损坏)
            File.WriteAllBytes(store.OverridesPath,
                Encoding.UTF8.GetBytes("{\"bindings\":[{\"id\":\"1c04ea5f-0000-0000-0000-000000000000\""));
            LogAssert.Expect(LogType.Error, new Regex("拒收"));
            LogAssert.Expect(LogType.Warning, new Regex("备份路径"));
            LogAssert.Expect(LogType.Warning, new Regex("恢复完成"));
            OverridesLoadResult result = store.Load(clone, HashA);

            Assert.That(result, Is.EqualTo(OverridesLoadResult.CorruptPayload), "损坏 ⇒ 不静默、不部分恢复");
            AssertNoOverrides(Snapshot(clone), "损坏出口资产维持默认");
            Assert.That(File.Exists(store.OverridesPath + ".bak-001"), Is.True,
                "损坏载荷已改名备份(非删除)");
            Assert.That(File.Exists(store.SchemaPath + ".bak-001"), Is.True,
                "完好头部同批改名备份(损坏载荷已不可喂,头部同批出局)");
        }

        // ══════════ AC-3-A3/A8 的反证扫描:备份语义 = 改名(Move),非复制/删除 ══════════

        [Test]
        public void test_hashMismatch_backupIsRenameNotDelete_notCopy()
        {
            // QA A3 ①「旧文件已改名备份(存在,非删除)」的语义扫面:失配恢复后原路径必须移除
            // (改名 = Move,不是 Copy) —— 若实现复制后保留原文件,后续 Save 会重建出与陈旧文件
            // 并存的状态;断言原路径移除 = 该语义被绑定。
            _shared.RemoveAllBindingOverrides();
            ApplyPathOverride(_shared, "Interact", "<Keyboard>/e", "<Keyboard>/q");
            var store = new BindingsStore(_tempDir);
            store.Save(_shared, HashA);

            LogAssert.Expect(LogType.Warning, new Regex("失配"));
            LogAssert.Expect(LogType.Warning, new Regex("备份路径"));
            LogAssert.Expect(LogType.Warning, new Regex("恢复完成"));
            store.Load(_shared, HashB);

            Assert.That(File.Exists(store.OverridesPath), Is.False, "改名 = 原路径移除(非复制保留)");
            Assert.That(File.Exists(store.OverridesPath + ".bak-001"), Is.True, "备份存在(非删除)");
        }

        /// <summary>合成记录:全字段固定,仅 bindingId / 三集合可变 —— E3③ 探针的夹具工厂。</summary>
        private static BindingSchemaRecord MakeRecord(
            string bindingId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            string[] groups = null, string[] processors = null, string[] interactions = null)
            => new BindingSchemaRecord(
                "MapA", "ActionA", 0, bindingId,
                "binding-name", "<Keyboard>/e", false, false,
                processors ?? Array.Empty<string>(),
                interactions ?? Array.Empty<string>(),
                groups ?? Array.Empty<string>());

        private static string HashOf(params BindingSchemaRecord[] records)
            => SchemaHash.ComputeSchemaHash(records);
    }
}
