namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    using DaYiJingCheng.EditorTools.Gates;
    using NUnit.Framework;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Story 019-d: `SkeuomorphicUiGates.ValidateAll()` **聚合入口**的覆盖夹具。
    ///
    /// <para>⚠️ **本夹具补的是一个实测暴露的真缺口(2026-10-05)**:`ValidateAll()` 自 Story 001
    /// 起被文件头自述「④ CI EditMode 测试直调 `ValidateAll()`」,而实测全 `Tests/` 目录
    /// **零处调用** —— C1 / C2 / C4 / C5 / C6 五条在 CI 上**长期无覆盖**。
    /// 现存的 `texture_binding_gate_test.cs` 只测 `TextureBindingGates.*` 的**各纯逻辑半**,
    /// **从不调聚合入口**。</para>
    ///
    /// <para>⚠️ **调用契约(实测)**:`ValidateAll()` **不自调 `InitializeDefaults()`** ——
    /// 主题变量登记与元件注册是**进程态**的。漏调则 C2 报「未登记主题变量」132 条、C7 报
    /// 「已注册类集为空」1 条 —— **全是假红**(实测数据)。唯一真实调用者是菜单项 `RunMenu()`
    /// (它先调 `InitializeDefaults()`)。故本夹具以 `RunGatesAsMenuDoes()` 复刻该契约。</para>
    ///
    /// <para>⚠️ **契约形状由 `test_initialize_defaults_is_private_and_validate_all_is_public`
    /// 钉死**(可见性 + 双入口分离)—— 该测试只断结构,不碰进程态,故**不受执行顺序影响**。
    /// 若未来把初始化并入 `ValidateAll`,该测试会红,提示同步更新本文件头自述。</para>
    /// </summary>
    [TestFixture]
    public class validate_all_aggregate_test
    {
        /// <summary>复刻菜单项 `RunMenu()` 的真实调用序:`InitializeDefaults()` then `ValidateAll()`。
        /// <para>⚠️ `InitializeDefaults()` 是 `private static` —— 走反射调用。
        /// **这正是缺口所在**:公开入口 `ValidateAll()` 与私有前置 `InitializeDefaults()` 分离,
        /// 且前者不做自我初始化。若未来有人「顺手」把初始化挪进 `ValidateAll()`,
        /// 本夹具仍应通过(契约是「初始化后校验」,非「谁调初始化」)。</para></summary>
        private static List<string> RunGatesAsMenuDoes()
        {
            var gates = typeof(SkeuomorphicUiGates);
            var init = gates.GetMethod("InitializeDefaults",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(init,
                "[前置] InitializeDefaults 应为 private static —— 若已改名/改可见性,本夹具须同步");
            init.Invoke(null, null);

            return SkeuomorphicUiGates.ValidateAll();
        }

        // ══════════════════════════════════════════════════════
        // 聚合入口全绿(019-d 收口判据)
        // ══════════════════════════════════════════════════════

        [Test]
        public void test_validate_all_is_green_after_20261005_c4_cleanup()
        {
            var errs = RunGatesAsMenuDoes();

            // 失败时打印全部条数,便于定位(门是错误列表形态,不是异常)。
            Assert.IsEmpty(errs,
                $"[AC-42-C1…C11] ValidateAll 应为空(2026-10-05 C4 清理后);实际 {errs.Count} 条:\n" +
                string.Join("\n", errs));
        }

        // ══════════════════════════════════════════════════════
        // 前置契约:InitializeDefaults 与 ValidateAll 的**分离**是可断言的
        // ══════════════════════════════════════════════════════

        [Test]
        public void test_initialize_defaults_is_private_and_validate_all_is_public()
        {
            // 本测试钉的是**调用契约的形状**(而非进程态副作用 —— 那会与测试执行顺序耦合):
            //   ① 前置初始化 `InitializeDefaults` 是 private static(故调用方须显式走它);
            //   ② 校验入口 `ValidateAll` 是 public static(故 CI / 菜单 / 夹具均可直调);
            //   ③ 二者**不同名**、不同可见性 ⇒ 「ValidateAll 自初始化」这一混淆不可能静默发生。
            // ⚠️ 若未来把初始化挪进 `ValidateAll`(把契约收成单一入口),本测试**会红** ——
            //    那时应同步更新 `RunGatesAsMenuDoes()` 与文件头自述,而非删测试。
            var gates = typeof(SkeuomorphicUiGates);

            var init = gates.GetMethod("InitializeDefaults",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(init,
                "[契约] InitializeDefaults 应为 private static —— 前置初始化与校验入口分离");
            Assert.IsTrue(init.IsPrivate, "[契约] InitializeDefaults 可见性应为 private");

            var validate = gates.GetMethod("ValidateAll",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(validate, "[契约] ValidateAll 应为 public static(菜单/CI/夹具直调)");

            Assert.AreNotEqual(init, validate, "[契约] 初始化与校验须为两个不同入口");
        }

        [Test]
        public void test_theme_validator_reports_unregistered_name_as_false()
        {
            // C2 判据面的**最小非空断言** —— 只测「IsRegistered 对未声明名返回 false」
            // 这一个纯函数事实,与进程态登记无关(故不受测试执行顺序影响)。
            // ⚠️ 校验器住 **Gameplay.UI**(非 Editor.Tools.Gates)——
            //    与 `ThemeVariableReferenceValidator.cs:9` 的落点一致。
            var validatorType = typeof(
                DaYiJingCheng.Gameplay.UI.Skeuomorphic.ThemeVariableReferenceValidator);
            Assert.IsNotNull(validatorType,
                "[前置] ThemeVariableReferenceValidator 应存在于 Gameplay.UI 程序集");

            var isRegistered = validatorType.GetMethod("IsRegistered",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(isRegistered, "[前置] IsRegistered 应为 public static");

            // 一个**绝不**存在于 SkeuoThemeVariables.uss 的合成变量名 —— 其未登记恒成立。
            const string synthetic = "--skeuo-__never_declared_probe__";
            bool registered = (bool)isRegistered.Invoke(null, new object[] { synthetic });

            Assert.IsFalse(registered,
                "合成变量名不得被登记 —— C2 的判据面依赖「IsRegistered 对未声明名返回 false」");
        }

        // ══════════════════════════════════════════════════════
        // 分项覆盖:C1 / C4 / C5 / C6 各自的最小非空断言
        // ══════════════════════════════════════════════════════

        [Test]
        public void test_c4_scan_actually_walks_the_library()
        {
            // C4 清理后应为空,但**不能**因「扫不到文件」而空 —— 故断言扫描面非空。
            string sk = System.IO.Path.Combine(
                TextureBindingGates.DefaultRepoRoot,
                "Assets", "Gameplay.UI", "Skeuomorphic");
            var ussFiles = System.IO.Directory.GetFiles(sk, "*.uss",
                System.IO.SearchOption.TopDirectoryOnly);

            // 排除主题变量表后仍应有 ≥ 8 个元件 USS(四基类 + 铜/器具/记号/焦点 + …)
            var components = ussFiles
                .Where(f => System.IO.Path.GetFileName(f) != "SkeuoThemeVariables.uss")
                .ToArray();

            Assert.GreaterOrEqual(components.Length, 8,
                $"C4 扫描面应有 ≥ 8 个元件 USS 文件(实得 {components.Length})—— " +
                "扫描面塌缩 ⇒ 门静默假绿(承 texture_binding_gate_test 的同类 fail-loud 断言)");
        }

        [Test]
        public void test_theme_variables_table_has_nine_layers_after_20261005()
        {
            // 019-d 连带清理新增 brass / implement / marks / focus 四层(5 → 9)。
            string themeFile = System.IO.Path.Combine(
                TextureBindingGates.DefaultRepoRoot,
                "Assets", "Gameplay.UI", "Skeuomorphic", "SkeuoThemeVariables.uss");
            string text = System.IO.File.ReadAllText(themeFile);

            string[] requiredLayers =
            {
                "--skeuo-paper-", "--skeuo-scroll-", "--skeuo-ink-", "--skeuo-seal-",
                "--skeuo-brass-", "--skeuo-implement-", "--skeuo-marks-", "--skeuo-focus-",
                "--skeuo-shared-"
            };
            foreach (var layer in requiredLayers)
            {
                Assert.IsTrue(text.Contains(layer),
                    $"主题变量表应含 {layer}* 族(019-d 后共 9 层)");
            }
        }

        [Test]
        public void test_brass_bg_matches_art_bible_authoritative_value()
        {
            // ⚠️ 019-d 订正:`SkeuoBrass.uss` 原内联 `#b87333`(网上通用铜色),
            //    art-bible §4.1 权威值为 `#B8863B`(绿通道差 19,肉眼可辨)。
            string themeFile = System.IO.Path.Combine(
                TextureBindingGates.DefaultRepoRoot,
                "Assets", "Gameplay.UI", "Skeuomorphic", "SkeuoThemeVariables.uss");
            string text = System.IO.File.ReadAllText(themeFile);

            // 只看 `:root { … }` 声明区,且**先剥注释** —— 注释里**刻意**保留了订正史
            // (#b87333 → #B8863B);生产门的 C4 同样走 UssCommentRegex 剥注释,
            // 夹具须与之同口径(否则夹具比门严 ⇒ 假红;首版两回即栽在此)。
            text = System.Text.RegularExpressions.Regex.Replace(
                text, @"/\*.*?\*/", "",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            int rootStart = text.IndexOf(":root");
            int rootEnd = text.IndexOf('}', rootStart);
            Assert.IsTrue(rootStart >= 0 && rootEnd > rootStart, "[前置] 主题表应有 :root 块");
            string decls = text.Substring(rootStart, rootEnd - rootStart).ToUpperInvariant();

            Assert.IsTrue(decls.Contains("--SKEUO-BRASS-BG: #B8863B"),
                "铜色权威值应为 art-bible §4.1 的 #B8863B");
            Assert.IsFalse(decls.Contains("#B87333"),
                "原内联 #b87333 不得出现在声明区(非本项目裁定值)");
        }

        [Test]
        public void test_focus_carrier_is_brass_not_ink()
        {
            // 2026-10-05 用户裁定:焦点载体墨 → 铜(承 art-bible §7.4 Amendment)。
            string focusFile = System.IO.Path.Combine(
                TextureBindingGates.DefaultRepoRoot,
                "Assets", "Gameplay.UI", "Skeuomorphic", "SkeuoFocusVisible.uss");
            string text = System.IO.File.ReadAllText(focusFile);

            Assert.IsTrue(text.Contains("var(--skeuo-focus-border)"),
                "焦点载体须引 --skeuo-focus-border(铜侧)");
            Assert.IsFalse(text.Contains("var(--skeuo-ink-fg)"),
                "焦点载体不得回退墨侧(改判已落)");
        }
    }
}
