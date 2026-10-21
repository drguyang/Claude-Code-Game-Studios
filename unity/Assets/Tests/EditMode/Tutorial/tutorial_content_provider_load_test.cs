// STORY-001 (tutorial-onboarding) · AC-5 装载半边 —— `AddressablesDataProvider` 的 48 教学内容分支
//
// ⚠️ 立此件的缘由(2026-10-21 · 评审 BLOCKING-class B-2):
//    `provider_wiring_gate_test` 只证「三个 address 常量互等」,即**接线在源码里**;
//    `EnsureTutorialContent` 的**解码分支**在 EditMode 里原本只可观测到失败
//    (无 catalog ⇒ Addressables 抛),正向半边不可达 ⇒ AC-5 只能记 PARTIAL。
//    本件经 `AddressablesDataProvider` 的内部可注入构造(2026-10-21 新增,承
//    `DataCorePreloader.TryPreloadGated` 的「可注入核心」先例)**真跑**该分支:
//    真 cooked 字节 → `TutorialContentTable.TryDecode` → 表。
//
// ⚠️ 可见性:内部构造经**反射**取得(先例 = `pipeline_gate_test.cs` 取
//    `DataCorePreloader.TryPreloadGated` 的写法)。**刻意不加 `InternalsVisibleTo`** ——
//    `Gameplay.Presentation` 是出货装配,不为一条测试把测试装配名烘进去。
//
// ⚠️ 判据强度:本件钉的是**装载路径 + 解码产物**,不钉「Addressables 真 catalog 命中」
//    (那需要真 Group 构建 + 真 catalog,EditMode 不可得;已由 `provider_wiring_gate_test`
//    的地址表闭合门 + PlayMode 启动序覆盖)。两侧合起来才等于 AC-5 全绿,**单看任何一侧都不够**。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Tutorial
{
    /// <summary>AC-5 装载半边:`Load&lt;TutorialContentTable&gt;()` 走真 cooked 字节的端到端判据。</summary>
    [TestFixture]
    public class tutorial_content_provider_load_test
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        /// <summary>盘上产物路径(<c>unity/Assets/DataCooked/48_tutorial_content.cooked.bytes</c>)。</summary>
        private static string CookedPath
            => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked",
                TutorialContentTable.CookedFileName);

        /// <summary>
        /// 经**内部可注入构造**造 provider(反射)。
        /// <para>构造不存在 ⇒ 抛 <see cref="AssertionException"/> 并点名:说明注入缝被移除,
        /// 而移除它就得另找搬运 AC-5 正向半边的办法 —— 不允许静默跳过。</para>
        /// </summary>
        private static AddressablesDataProvider MakeProvider(Func<string, byte[]> byteSource)
        {
            ConstructorInfo ctor = typeof(AddressablesDataProvider).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Func<string, byte[]>) },
                modifiers: null);

            Assert.That(ctor, Is.Not.Null,
                "`AddressablesDataProvider(Func<string, byte[]>)` 内部构造不见了 —— AC-5 装载测的注入缝被移除");

            return (AddressablesDataProvider)ctor.Invoke(new object[] { byteSource });
        }

        // ══════════════════════ 正向:真产物 → 真解码 ══════════════════════

        /// <summary>真 cooked 字节经 provider 的 48 分支装载 ⇒ 解出非空表。
        /// <para><b>判别力</b>:把 `Load&lt;T&gt;` 的 `typeof(TutorialContentTable)` 分支删掉 ⇒
        /// 落「未知数据集类型」兜底,本测红。</para></summary>
        [Test]
        public void test_provider_load_realArtifact_decodesNonEmptyTable()
        {
            Assert.That(File.Exists(CookedPath), Is.True,
                $"产物不在盘上:{CookedPath} —— 重烘:菜单「大医精诚/数据管线/烘焙 48_tutorial_content」");

            byte[] onDisk = File.ReadAllBytes(CookedPath);
            AddressablesDataProvider provider = MakeProvider(_ => onDisk);

            TutorialContentTable table = provider.Load<TutorialContentTable>();

            Assert.That(table.Steps.Count, Is.GreaterThan(0), "步骤段为空 ⇒ 解码未真的走通");
            Assert.That(table.Narration.Count, Is.GreaterThan(0), "口述段为空");
            Assert.That(table.Demonstrations.Count, Is.GreaterThan(0), "示范段为空");
            Assert.That(table.Papers.Count, Is.GreaterThan(0), "纸段为空");
            Assert.That(table.ConfigVersion, Is.Not.EqualTo(0u),
                "ConfigVersion 为 0 ⇒ 头部槽未被写入(承 DelayTableCookedWriter 的 R-1 教训)");
        }

        /// <summary>产物内容的**钉子**:6 步 / 4 口述 / 6 示范 / 1 纸。
        /// <para>⚠️ 内容批改 `assets/data/48_tutorial_content.json` 后此处须**同批**改 ——
        /// 与 `cooked_drift_all_artifacts_test` 的 `pinnedDeclared` 同款纪律:
        /// 钉子转红 = 有人在改产物,不是回归。</para></summary>
        [Test]
        public void test_provider_load_realArtifact_sectionCountsArePinned()
        {
            TutorialContentTable table =
                MakeProvider(_ => File.ReadAllBytes(CookedPath)).Load<TutorialContentTable>();

            Assert.That(table.Steps.Count, Is.EqualTo(6), "步骤数");
            Assert.That(table.Narration.Count, Is.EqualTo(4), "口述数");
            Assert.That(table.Demonstrations.Count, Is.EqualTo(6), "示范数");
            Assert.That(table.Papers.Count, Is.EqualTo(1), "纸数");
        }

        /// <summary>分支请求的地址**必须**是 <see cref="AddressablesDataProvider.TutorialContentAddress"/>。
        /// <para><b>判别力</b>:把 `EnsureTutorialContent` 里的 `FetchBytes(TutorialContentAddress)`
        /// 换成别的地址 ⇒ 本测红。</para></summary>
        [Test]
        public void test_provider_load_requestsTutorialContentAddress()
        {
            var requested = new List<string>();
            byte[] onDisk = File.ReadAllBytes(CookedPath);

            MakeProvider(address =>
            {
                requested.Add(address);
                return onDisk;
            }).Load<TutorialContentTable>();

            Assert.That(requested, Is.EqualTo(new[] { AddressablesDataProvider.TutorialContentAddress }),
                "装载分支请求的地址不对(或请求了多次)");
        }

        /// <summary>幂等:同一 provider 连装两次 ⇒ 字节只取一次(走缓存)。
        /// <para><b>判别力</b>:删掉 `if (_tutorialContent.HasValue) return;` ⇒ 本测红。</para></summary>
        [Test]
        public void test_provider_load_twice_fetchesBytesOnce()
        {
            int fetches = 0;
            byte[] onDisk = File.ReadAllBytes(CookedPath);
            AddressablesDataProvider provider = MakeProvider(_ =>
            {
                fetches++;
                return onDisk;
            });

            TutorialContentTable first = provider.Load<TutorialContentTable>();
            TutorialContentTable second = provider.Load<TutorialContentTable>();

            Assert.That(fetches, Is.EqualTo(1), "二次装载重复取字节 ⇒ 缓存分支失效");
            Assert.That(second.Steps.Count, Is.EqualTo(first.Steps.Count));
        }

        // ══════════════════════ 负向:失败一律 E-13,绝不降级 ══════════════════════

        /// <summary>损坏字节(坏魔数)⇒ E-13 抛出,**不**返回空表。
        /// <para><b>判别力</b>:把 E-13 抛出改成「返回默认表」⇒ 本测红(这正是 ADR-014 §五 /
        /// E-13 的禁项:不得降级)。</para></summary>
        [Test]
        public void test_provider_load_corruptBytes_throwsE13()
        {
            byte[] corrupt = File.ReadAllBytes(CookedPath);
            corrupt[0] ^= 0xFF; // 破坏魔数首字节

            var ex = Assert.Throws<InvalidOperationException>(
                () => MakeProvider(_ => corrupt).Load<TutorialContentTable>());

            Assert.That(ex.Message, Does.Contain("[E-13]"), "失败报文缺 [E-13] 前缀");
            Assert.That(ex.Message, Does.Contain("48_tutorial_content"), "失败报文未点名本产物");
            Assert.That(ex.Message, Does.Contain("绝不降级"), "失败报文未声明不降级");
        }

        /// <summary>字节短于 cooked 头部 ⇒ 在 `FetchFromByteSource` 层即 E-13(不进解码器)。
        /// <para><b>判别力</b>:去掉 `bytes.Length &lt; CookedFormat.HeaderSize` 守卫 ⇒
        /// 会落到解码器的报错(报文不含「短于头部」)⇒ 本测红。</para></summary>
        [Test]
        public void test_provider_load_bytesShorterThanHeader_throwsE13AtFetchLayer()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => MakeProvider(_ => new byte[] { 0x01, 0x02 }).Load<TutorialContentTable>());

            Assert.That(ex.Message, Does.Contain("[E-13]"));
            Assert.That(ex.Message, Does.Contain("短于头部"), "未在取字节层拦下 ⇒ 头部守卫失效");
        }

        /// <summary>字节源返回 <c>null</c> ⇒ 同款 E-13(不是 NullReferenceException)。
        /// <para><b>判别力</b>:去掉 null 守卫 ⇒ 解码器收到 null 抛 NRE,本测的类型断言红。</para></summary>
        [Test]
        public void test_provider_load_nullBytes_throwsE13NotNre()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => MakeProvider(_ => null).Load<TutorialContentTable>());

            Assert.That(ex.Message, Does.Contain("[E-13]"));
            Assert.That(ex.Message, Does.Contain("短于头部") .Or.Contain("为空"));
        }

        /// <summary>字节源自身抛出 ⇒ 包成 E-13 并保留内因(不吞)。
        /// <para><b>判别力</b>:去掉注入路径的 try/catch ⇒ 抛的是原始异常类型,本测红。</para></summary>
        [Test]
        public void test_provider_load_byteSourceThrows_isWrappedAsE13()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => MakeProvider(_ => throw new IOException("磁盘掉了")).Load<TutorialContentTable>());

            Assert.That(ex.Message, Does.Contain("[E-13]"));
            Assert.That(ex.Message, Does.Contain("磁盘掉了"), "内因被吞 ⇒ 启动失败无从定位");
            Assert.That(ex.InnerException, Is.TypeOf<IOException>(), "InnerException 未保留");
        }

        /// <summary>注入缝不影响未知类型兜底(注入只管「字节从哪来」)。
        /// <para><b>判别力</b>:把注入路径写成「任何 T 都返回表」⇒ 本测红。</para></summary>
        [Test]
        public void test_provider_load_unknownType_stillThrowsUnknownType()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => MakeProvider(_ => File.ReadAllBytes(CookedPath)).Load<int>());

            Assert.That(ex.Message, Does.Contain("未知数据集类型"));
        }
    }
}
