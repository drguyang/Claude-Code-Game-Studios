// 权威来源:
//   `production/epics/telemetry-analytics/story-001-readonly-boundary-zero-egress.md`
//     —— AC-19-05(ADVISORY):「发布清单 Privacy/Analytics 项固定为『无出厂 / 无第三方 /
//        无内建崩溃上报』」
//   ADR-019 §四(隐私面因「无出厂」而结构性满足;**硬边界** = 一旦引入出厂数据,
//     本节即失效,须另开 ADR,不得打补丁)
//
// 🔁 **2026-10-30 新建(`TD-epic-01-e` 三条 NOT-RUN 结清轮)**:
//   AC-19-05 此前登记为 **NOT-RUN**,理由是「纯文档项,全仓无发布清单文件」。
//   ⇒ 本轮建 `production/privacy-analytics-declaration.md` 作为**载体**,
//     并建本夹具保证:① 载体存在 ② 三项固定承诺逐字在 ③ 硬边界声明在。
//
//   ⚠️ **本判据的性质**(如实登记):它验的是**文档的存在与措辞**,
//     **不是**「运行时真的没有网络上报」(那由 `AC-19-02` / `AC-51-A4` 的反射夹具验)。
//     两者互补 —— 文档承诺 + 代码判据,缺一不可。

using System;
using System.IO;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class PrivacyDeclarationTest
    {
        private const string DeclRelPath = "production/privacy-analytics-declaration.md";

        /// <summary>AC-19-05:声明文件存在,且三项固定承诺**逐字**在内。
        /// <para><b>为什么逐字**:这三项是给发版 checklist 照抄的**固定文本** ——
        /// 措辞一变,checklist 抄出来的就是另一份承诺。⇒ 判据锚在字面量上,
        /// 改措辞即红(强制改的人先想清楚)。</para>
        /// </summary>
        [Test]
        public void test_ac1905_declarationExists_withThreeFixedPromises()
        {
            string path = RepoRoot() + "/" + DeclRelPath;
            Assert.That(File.Exists(path), Is.True,
                $"隐私 / 分析声明文件不存在 —— {path}。" +
                "AC-19-05 需要它作为发布清单 Privacy/Analytics 两项的固定文本载体。");

            string text = File.ReadAllText(path);

            // ⚠️ **判据必须锚在「固定文本」段内,不能全文扫**
            //    (2026-10-30 实测踩过:全文 `Does.Contain` 会被 §三 的**依据表格**借绿 ——
            //     M22b 删掉固定文本段里的承诺词,因表格里同串仍在 ⇒ 判据照绿,零判别力)。
            string fixedSection = Section(text, "## 二、固定文本", "## 三、");
            Assert.That(fixedSection, Is.Not.Empty,
                "缺「固定文本」节 —— 声明必须有可被 checklist **逐字照抄**的那一段");

            // 三项固定承诺(ADR-019 §二 / §四 的逐字口径)—— 只在固定文本段内找
            Assert.That(fixedSection, Does.Contain("无账号系统"), "承诺 ①「无出厂」缺:无账号系统");
            Assert.That(fixedSection, Does.Contain("无第三方分析 SDK"), "承诺 ②「无第三方」缺:无第三方分析 SDK");
            Assert.That(fixedSection, Does.Contain("无崩溃上报"), "承诺 ③「无内建崩溃上报」缺:无崩溃上报");
        }

        /// <summary>ADR-019 §四 的**硬边界**必须写在声明里 ——
        /// 「一旦引入出厂数据,本节即失效,须另开 ADR,不得打补丁」。
        /// <para><b>为什么这条是必须的</b>:它是本声明的**失效条件**。缺了它,
        /// 下一个人会以为「加一条匿名计数」只是编辑文档,而不是 supersede 一个 ADR。</para>
        /// </summary>
        [Test]
        public void test_adr019s4_hardBoundary_isDeclared()
        {
            string text = File.ReadAllText(RepoRoot() + "/" + DeclRelPath);
            // ⚠️ 同样锚在「硬边界」节内(全文扫会被 §一 / §三 的同串借绿)
            string boundary = Section(text, "## 四、硬边界", "## 五、");
            Assert.That(boundary, Is.Not.Empty, "缺「硬边界」节 —— ADR-019 §四 的失效条件必须单列");

            Assert.That(boundary, Does.Contain("另开 ADR"),
                "缺「须另开 ADR」—— ADR-019 §四 的硬边界必须写在声明里(不得打补丁)");
            Assert.That(boundary, Does.Contain("不得打补丁"),
                "缺「不得打补丁」逐字措辞 —— 这是 ADR-019 §四 的原文口径");
        }

        /// <summary>声明里的**可复算判据**必须指向真实存在的夹具/命令。
        /// <para>⚠️ 本条只验「声明里点了名的那几个 AC 编号在本 epic 的 story 里确实存在」——
        /// 不验「它们真的绿」(那由各自的夹具验)。防的是「声明引用了一个不存在的 AC」。</summary>
        /// </summary>
        [Test]
        public void test_declarationCitesOnlyExistingAcs()
        {
            string decl = File.ReadAllText(RepoRoot() + "/" + DeclRelPath);

            string[] cited = { "AC-19-01", "AC-19-02", "AC-19-03", "AC-51-A4", "AC-51-A7" };
            string story1 = File.ReadAllText(
                RepoRoot() + "/production/epics/telemetry-analytics/story-001-readonly-boundary-zero-egress.md");

            foreach (string ac in cited)
            {
                if (!decl.Contains(ac, StringComparison.Ordinal)) continue;   // 声明没引用它,跳过
                Assert.That(story1, Does.Contain(ac),
                    $"声明引用了 `{ac}`,但 story-001 里没有这个 AC —— 引据悬空(须同批订正)");
            }
        }

        /// <summary>取两个标记之间的文本(`from` 含、`to` 不含);找不到 ⇒ 空串。
        /// <para>⚠️ 这是本测**判别力**的关键:判据必须锚在**具体小节**上,
        /// 全文 `Does.Contain` 会被别处的同串借绿。</para>
        /// </summary>
        private static string Section(string text, string from, string to)
        {
            int i = text.IndexOf(from, StringComparison.Ordinal);
            if (i < 0) return string.Empty;
            int j = text.IndexOf(to, i + from.Length, StringComparison.Ordinal);
            if (j < 0) return text.Substring(i);
            return text.Substring(i, j - i);
        }

        /// <summary>仓库根(`CallerFilePath` 定位,不依赖 cwd)。
        /// <para>本文件在 `unity/Assets/Tests/EditMode/Telemetry/`
        /// ⇒ 上溯 5 层 = 仓库根。</para>
        /// </summary>
        private static string RepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
        {
            string dir = Path.GetDirectoryName(thisFile) ?? ".";
            // Telemetry → EditMode → Tests → Assets → unity → 仓库根
            return Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", ".."));
        }
    }
}
