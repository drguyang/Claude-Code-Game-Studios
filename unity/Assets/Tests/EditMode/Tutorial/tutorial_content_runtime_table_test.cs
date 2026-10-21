// S17-3 · story-001 —— AC-5「48_tutorial_content.cooked 运行期只读」的跨侧一致性测试。
//
// 覆盖:
//   · 往返 —— BakeFromRepo → Write → TryDecode,关键字段逐个相等(步序 / 行数 / id 序 / 文本)
//   · 确定性 —— 同源两次烘焙字节逐位相同(ADR-014 §二)
//   · **跨侧字段序** —— 解码行数 == 烘焙行数;两侧布局分叉时本文件必红的判据写在注释里
//   · 损坏输入 —— 截断 / 坏魔数 ⇒ TryDecode 返回 false + 非空 error,不抛未捕获异常
//   · 只读 —— 反射断言表类型零可写属性 / 零可变集合属性 / 零非 readonly 字段
//   · 接线 —— Addressables address 常量 == 解码器文件名常量 == 菜单产物名常量
//
// ⚠️ 本文件**只读**仓库文件(`assets/data/48_tutorial_content.json` 是正当输入),
//   零写盘(承 FsWriteBoundaryGate R1/R2)、零随机、零时间依赖。
//
// ═══ 「跨侧字段序」的判据说明(为什么本文件能抓「只改了一侧」)═══════════
//   `TutorialContentCookedWriter.Write` 与 `TutorialContentTable.TryDecode` 是**手工镜像**的
//   两个实现(两侧分属编辑期装配 / 边界装配,无法共用代码)。若只改一侧:
//     ① 字段少/多的情形 —— 解码器末尾的「载荷必须恰好读完」断言会先命中
//        (`payload.Position != payload.Length`)⇒ 返回 false ⇒ 本文件的往返测红;
//     ② 字段类型/顺序**同宽**互换的情形(如 id 与 title 对调)—— 字节能读完、不报错,
//        但**内容互换** ⇒ 本文件对 id 序 / 文本的逐个相等断言红。
//   故「同源烘焙 → 解码 → 逐字段比对」是本布局漂移的唯一机械兜现;不要改测试迁就实现。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Tutorial
{
    /// <summary>48 教学内容 cooked 产物的运行期只读表:往返 / 确定性 / 损坏 / 只读 / 接线。</summary>
    [TestFixture]
    public sealed class tutorial_content_runtime_table_test
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        /// <summary>现烘一次(生产同一路径;AC-5 的输入面)。</summary>
        private static TutorialContentBake.BakeOutput BakeFresh()
            => TutorialContentBake.BakeFromRepo(RepoRoot);

        private static TutorialContentTable DecodeOrFail(byte[] bytes)
        {
            bool ok = TutorialContentTable.TryDecode(bytes, out TutorialContentTable table, out string error);
            Assert.That(ok, Is.True, "TryDecode 应成功:" + error);
            Assert.That(error, Is.Null, "成功时 error 必须为 null(否则失败通道语义被污染)");
            return table;
        }

        // ══════════════════ 往返 ══════════════════

        /// <summary>步行:解码序 == 六步闭集序(写方按闭集序重排),各行 id / title / 引用 id 相等。</summary>
        [Test]
        public void test_roundtrip_steps_matchBakedContent()
        {
            // Arrange
            TutorialContentBake.BakeOutput baked = BakeFresh();
            Assert.That(baked.Cooked, Is.Not.Null.And.Not.Empty, "烘焙输出为空 —— 编码器或源文件异常");

            // Act
            TutorialContentTable table = DecodeOrFail(baked.Cooked);

            // Assert
            Assert.That(table.Steps.Count, Is.EqualTo(baked.Bound.Steps.Count),
                "步骤行数:解码 != 烘焙");
            for (int i = 0; i < table.Steps.Count; i++)
                Assert.That(table.Steps[i].Id, Is.EqualTo(TutorialContentSchema.StepIds[i]),
                    $"第 {i} 行步 id 应为闭集序第 {i} 员(写方重排契约)");

            foreach (TutorialStep bound in baked.Bound.Steps)
            {
                TutorialStepRow row = table.Steps.Single(r => r.Id == bound.Id);
                Assert.That(row.Title, Is.EqualTo(bound.Title), $"{bound.Id}.title");
                Assert.That(row.PredicateKind, Is.EqualTo(bound.PredicateKind), $"{bound.Id}.predicateKind");
                Assert.That(row.Note, Is.EqualTo(bound.Note), $"{bound.Id}.note");
                CollectionAssert.AreEqual(bound.NarrationIds, row.NarrationIds, $"{bound.Id}.narration[]");
                CollectionAssert.AreEqual(bound.DemonstrationIds, row.DemonstrationIds, $"{bound.Id}.demonstrations[]");
                CollectionAssert.AreEqual(bound.PaperIds, row.PaperIds, $"{bound.Id}.papers[]");
            }
        }

        /// <summary>口述 / 示范 / 纸:行数相等,且按 id 取到的行逐字段相等(含 order 与文本)。</summary>
        [Test]
        public void test_roundtrip_narrationDemonstrationsPapers_matchBakedContent()
        {
            // Arrange
            TutorialContentBake.BakeOutput baked = BakeFresh();

            // Act
            TutorialContentTable table = DecodeOrFail(baked.Cooked);

            // Assert —— 行数
            Assert.That(table.Narration.Count, Is.EqualTo(baked.Bound.Narration.Count), "口述行数");
            Assert.That(table.Demonstrations.Count, Is.EqualTo(baked.Bound.Demonstrations.Count), "示范行数");
            Assert.That(table.Papers.Count, Is.EqualTo(baked.Bound.Papers.Count), "纸行数");

            // Assert —— 逐字段
            foreach (TutorialNarration bound in baked.Bound.Narration)
            {
                TutorialNarrationRow row = table.Narration.Single(r => r.Id == bound.Id);
                Assert.That(row.CueId, Is.EqualTo(bound.CueId), $"{bound.Id}.cueId");
                Assert.That(row.Order, Is.EqualTo(bound.Order), $"{bound.Id}.order");
                Assert.That(row.Text, Is.EqualTo(bound.Text), $"{bound.Id}.text");
            }
            foreach (TutorialDemonstration bound in baked.Bound.Demonstrations)
            {
                TutorialDemonstrationRow row = table.Demonstrations.Single(r => r.Id == bound.Id);
                Assert.That(row.StepId, Is.EqualTo(bound.StepId), $"{bound.Id}.step");
                Assert.That(row.Actor, Is.EqualTo(bound.Actor), $"{bound.Id}.actor");
                Assert.That(row.ClipKey, Is.EqualTo(bound.ClipKey), $"{bound.Id}.clipKey");
            }
            foreach (TutorialPaper bound in baked.Bound.Papers)
            {
                TutorialPaperRow row = table.Papers.Single(r => r.Id == bound.Id);
                Assert.That(row.StepId, Is.EqualTo(bound.StepId), $"{bound.Id}.step");
                Assert.That(row.Title, Is.EqualTo(bound.Title), $"{bound.Id}.title");
                Assert.That(row.Body, Is.EqualTo(bound.Body), $"{bound.Id}.body");
                Assert.That(row.AspectRatio, Is.EqualTo(bound.AspectRatio), $"{bound.Id}.aspectRatio");
                Assert.That(row.IllustrationKey, Is.EqualTo(bound.IllustrationKey), $"{bound.Id}.illustrationKey");
            }
        }

        /// <summary>头部回声:解码出的 schemaVersion / configVersion == 烘焙时的值(ADR-014 §二/§五)。</summary>
        [Test]
        public void test_roundtrip_headerVersions_matchBake()
        {
            // Arrange
            TutorialContentBake.BakeOutput baked = BakeFresh();

            // Act
            TutorialContentTable table = DecodeOrFail(baked.Cooked);

            // Assert
            Assert.That(table.SchemaVersion, Is.EqualTo(baked.Bound.SchemaVersion), "schemaVersion");
            Assert.That(table.ConfigVersion, Is.EqualTo(baked.ConfigVersion), "ConfigVersion");
        }

        // ══════════════════ 确定性 ══════════════════

        /// <summary>同源两次烘焙 ⇒ 字节逐位相同(ADR-014 §二;零字典迭代 / 零本地时间)。</summary>
        [Test]
        public void test_bake_twoRunsSameSource_bytesIdentical()
        {
            // Arrange / Act
            byte[] first = BakeFresh().Cooked;
            byte[] second = BakeFresh().Cooked;

            // Assert
            CollectionAssert.AreEqual(first, second,
                "同源两次烘焙字节不一致 —— 违反 ADR-014 §二(确定性烘焙);" +
                "查写方是否引入了字典迭代序 / 本地时间 / 不稳定排序");
        }

        // ══════════════════ 跨侧字段序 ══════════════════

        /// <summary>
        /// 跨侧行数守恒:解码出的四段行数 == binder 绑定结果的行数。
        /// <para>⚠️ 本测是文件头「判据说明」的落地:若只改一侧的字段序,① 载荷长度断言先红,
        /// 或 ② 本测的行数/内容比对红 —— 两者都会在 <c>TryDecode</c> 或此处暴露。</para>
        /// </summary>
        [Test]
        public void test_crossSide_rowCounts_equalBinderResult()
        {
            // Arrange
            TutorialContentBake.BakeOutput baked = BakeFresh();
            TutorialContentTable table = DecodeOrFail(baked.Cooked);

            // Act
            int[] decoded = { table.Steps.Count, table.Narration.Count,
                              table.Demonstrations.Count, table.Papers.Count };
            int[] bound = { baked.Bound.Steps.Count, baked.Bound.Narration.Count,
                            baked.Bound.Demonstrations.Count, baked.Bound.Papers.Count };

            // Assert
            CollectionAssert.AreEqual(bound, decoded,
                "跨侧行数分叉:解码 [steps, narration, demos, papers] != 绑定结果 —— " +
                "TutorialContentCookedWriter 与 TutorialContentTable.TryDecode 的字段序未同步");
            Assert.That(decoded[0], Is.EqualTo(TutorialContentSchema.StepCount),
                "步数须恰为闭集基数(写方重排的前提)");
        }

        // ══════════════════ 损坏输入(不抛未捕获异常)══════════════════

        /// <summary>截断 1 字节 ⇒ false + 非空 error,不抛。</summary>
        [Test]
        public void test_corrupt_truncatedBytes_returnsFalseWithError()
        {
            // Arrange
            byte[] bytes = BakeFresh().Cooked;
            var truncated = new byte[bytes.Length - 1];
            Array.Copy(bytes, truncated, truncated.Length);

            // Act
            bool ok = false;
            string error = null;
            TutorialContentTable table = default;
            Assert.DoesNotThrow(() => ok = TutorialContentTable.TryDecode(
                truncated, out table, out error), "TryDecode 不得抛未捕获异常(调用方要包 E-13)");

            // Assert
            Assert.That(ok, Is.False, "截断输入必须返回 false");
            Assert.That(error, Is.Not.Null.And.Not.Empty, "失败必须带非空 error");
            Assert.That(table.Steps, Is.Empty, "失败时 out 表须为 default(不得留下半解码状态)");
        }

        /// <summary>坏魔数 ⇒ false + 非空 error,不抛。</summary>
        [Test]
        public void test_corrupt_badMagic_returnsFalseWithError()
        {
            // Arrange
            byte[] bytes = BakeFresh().Cooked;
            bytes[0] = (byte)'X';   // 破坏 "DYJC"

            // Act
            bool ok = false;
            string error = null;
            TutorialContentTable table = default;
            Assert.DoesNotThrow(() => ok = TutorialContentTable.TryDecode(
                bytes, out table, out error), "TryDecode 不得抛未捕获异常");

            // Assert
            Assert.That(ok, Is.False, "坏魔数必须返回 false");
            Assert.That(error, Is.Not.Null.And.Not.Empty, "失败必须带非空 error");
        }

        /// <summary>null / 短于头部 ⇒ false + 非空 error(不与「损坏载荷」混为一谈)。</summary>
        [Test]
        public void test_corrupt_nullOrTooShort_returnsFalseWithError()
        {
            // Act / Assert —— null
            Assert.That(TutorialContentTable.TryDecode(null, out _, out string nullError), Is.False);
            Assert.That(nullError, Is.Not.Null.And.Not.Empty);

            // Act / Assert —— 短于头部
            Assert.That(TutorialContentTable.TryDecode(new byte[4], out _, out string shortError), Is.False);
            Assert.That(shortError, Is.Not.Null.And.Not.Empty);
        }

        // ══════════════════ 只读(反射断言)══════════════════

        /// <summary>
        /// 表类型**无可变状态**:零可写属性 · 零可变集合属性 · 全部实例字段 <c>readonly</c> ·
        /// 零公开可变方法(<c>Add</c> / <c>Remove</c> / <c>Clear</c> / <c>Sort</c>)。
        /// <para>承 casebook-002 的反射断言先例 —— 契约用反射钉,不靠注释。</para>
        /// </summary>
        [Test]
        public void test_readonly_tableType_hasNoMutableState()
        {
            Type type = typeof(TutorialContentTable);
            Assert.That(type.IsValueType, Is.True, "表须为值类型(Load<T> 约束 = where T : struct)");

            PropertyInfo[] props = type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.That(props, Is.Not.Empty, "表应有公开只读属性(空集说明反射扫错面)");

            foreach (PropertyInfo p in props)
            {
                Assert.That(p.CanWrite, Is.False, $"{p.Name} 有 setter —— 运行期表必须只读");
                Assert.That(typeof(System.Collections.IList).IsAssignableFrom(p.PropertyType), Is.False,
                    $"{p.Name} 的声明类型 {p.PropertyType.Name} 是可变列表 —— 须用 IReadOnlyList<>");
                Assert.That(typeof(System.Collections.IDictionary).IsAssignableFrom(p.PropertyType), Is.False,
                    $"{p.Name} 的声明类型 {p.PropertyType.Name} 是可变字典");
            }

            foreach (FieldInfo f in type.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                Assert.That(f.IsInitOnly, Is.True, $"字段 {f.Name} 非 readonly —— 表可被就地改写");

            foreach (string method in new[] { "Add", "Remove", "Clear", "Sort", "Insert" })
                Assert.That(type.GetMethod(method, BindingFlags.Public | BindingFlags.Instance), Is.Null,
                    $"表类型暴露了公开方法 {method} —— 破坏只读契约");
        }

        // ══════════════════ 接线 ══════════════════

        /// <summary>三处名字一致:Addressables address == 解码器文件名常量 == 菜单产物名常量。</summary>
        [Test]
        public void test_wiring_addressConstants_agree()
        {
            Assert.That(AddressablesDataProvider.TutorialContentAddress,
                Is.EqualTo(TutorialContentTable.CookedFileName), "address != 解码器文件名常量");
            Assert.That(DataBakeMenu.TutorialContentCookedAssetName,
                Is.EqualTo(TutorialContentTable.CookedFileName), "菜单产物名 != 解码器文件名常量");
        }
    }
}
