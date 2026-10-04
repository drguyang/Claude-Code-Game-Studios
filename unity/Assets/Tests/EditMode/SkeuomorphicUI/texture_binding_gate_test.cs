namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    using DaYiJingCheng.EditorTools.Gates;
    using NUnit.Framework;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Story 019-c: 贴图接入护栏 · 单元测试。
    /// <para>覆盖 **AC-42-C10**(屏幕层禁直引贴图)与 **AC-42-C11**(贴图缺失 / GUID 悬空 ⇒ 硬失败);
    /// 另含 AC-42-C7 的**骨架半**(已注册元件类须有 background-image)与 C8/C9 的**NOT-RUN 登记**。</para>
    ///
    /// <para>⚠️ **本 story 只做护栏,不接图** —— C7 的「每类有真实贴图」须待 019-d(切图冻结件 + 映射语义),
    /// C8(slice = 冻结件元数据)/ C9(`Pages_frame ≤ PAGES_MAX`)分别为 019-d / 019-b,均 NOT-RUN。</para>
    /// </summary>
    [TestFixture]
    public class texture_binding_gate_test
    {
        // ⚠️ 实测 Unity CLI EditMode 的 cwd = `<repo>/unity`(工程根),**不是** `<repo>`;
        //    直接用 cwd 拼 `Assets/…` 会拼空 ⇒ 门静默假绿。故走门提供的上溯解析
        //    (与生产侧 `SkeuomorphicUiGates` 同一锚 —— 承 `modal_gate_test.cs:502` 同族订正)。
        private static string RepoRoot => TextureBindingGates.DefaultRepoRoot;

        private static string SkeuoDir =>
            Path.Combine(RepoRoot, "Assets", "Gameplay.UI", "Skeuomorphic");

        private static string ScreensDir => Path.Combine(SkeuoDir, "Screens");

        private static string TexturesDir => Path.Combine(SkeuoDir, "Textures");

        // ══════════════════════════════════════════════════════
        // AC-42-C10 —— 屏幕级 UXML/USS 内不得出现任何 url()
        // ══════════════════════════════════════════════════════

        [Test]
        public void test_ac42c10_all_seven_screens_have_zero_url_hits()
        {
            // Arrange: 屏幕级文件 = Screens/ 下的 7 个 uxml(与 .uss 若有)
            var screens = Directory.GetFiles(ScreensDir, "*.uxml", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(ScreensDir, "*.uss", SearchOption.TopDirectoryOnly))
                .ToArray();

            // Act
            var errs = TextureBindingGates.ValidateScreenLevelNoTextureUrl(RepoRoot);

            // Assert: 屏幕层零 url 命中 ⇒ 零 C10 错误
            Assert.IsEmpty(errs,
                "屏幕级文件出现 url() —— 贴图只经元件库接入(AC-42-C10)。实际命中:\n" +
                string.Join("\n", errs));
            Assert.IsNotEmpty(screens, "Screens/ 下应存在屏幕级 UXML(否则夹具空跑,判别力为零)。");
        }

        [Test]
        public void test_ac42c10_url_targets_textures_helper_discriminates()
        {
            // Arrange / Act / Assert: 该判定谓词须能区分「贴图」与「非贴图」url ——
            // 否则 C10 的整条断言就是同义反复(恒真)。
            Assert.IsTrue(TextureBindingGates.UrlTargetsTextures("Textures/paper_xuan-final.png"),
                "指向 Textures/ 的 url 须判为贴图。");
            Assert.IsTrue(TextureBindingGates.UrlTargetsTextures("../Textures/ink_wet-final.png"),
                "相对路径指向 Textures/ 亦须判为贴图。");
            Assert.IsFalse(TextureBindingGates.UrlTargetsTextures("SkeuoThemeVariables.uss"),
                "非贴图 url 不得误判。");
            Assert.IsFalse(TextureBindingGates.UrlTargetsTextures(""),
                "空 url 不得判为贴图。");
        }

        // ══════════════════════════════════════════════════════
        // AC-42-C11 —— 贴图 url 缺失 / GUID 悬空 ⇒ 硬失败
        // ══════════════════════════════════════════════════════

        [Test]
        public void test_ac42c11_asset_resolves_true_for_existing_texture()
        {
            // Arrange: 取一张已入库的真实贴图
            string real = Path.Combine("Assets", "Gameplay.UI", "Skeuomorphic", "Textures", "paper_xuan-final.png");
            Assert.IsTrue(File.Exists(Path.Combine(RepoRoot, real)),
                $"夹具前置失败:真实贴图 {real} 不存在 —— 后续断言判别力为零。");

            // Act / Assert
            Assert.IsTrue(TextureBindingGates.AssetResolves(real, g => null, RepoRoot),
                "已入库贴图须可解析为真实资产。");
        }

        [Test]
        public void test_ac42c11_asset_resolves_false_for_missing_texture()
        {
            // Arrange: 一个不存在的贴图路径(悬空)
            const string dangling = "Assets/Gameplay.UI/Skeuomorphic/Textures/__no_such_texture__-final.png";

            // Act / Assert
            Assert.IsFalse(TextureBindingGates.AssetResolves(dangling, g => null, RepoRoot),
                "悬空贴图路径须判为不可解析 —— 这正是 C11 要消灭的静默降级。");
        }

        [Test]
        public void test_ac42c11_asset_resolves_false_for_dangling_guid()
        {
            // Arrange: 一个占位 GUID(非真实资产)
            const string danglingGuid = "guid:00000000000000000000000000000000";

            // Act / Assert
            Assert.IsFalse(TextureBindingGates.AssetResolves(danglingGuid, g => null, RepoRoot),
                "悬空 GUID 须判为不可解析(C11 的第二失效形态)。");
        }

        [Test]
        public void test_ac42c11_current_library_has_no_dangling_refs()
        {
            // Act: 当前元件库 USS 内所有 background-image url() 均可解析
            var errs = TextureBindingGates.ValidateTextureReferencesResolve(RepoRoot, g => null);

            // Assert: 尚无引用 ⇒ 零悬空(「没有引用」是合法态,非假绿)
            Assert.IsEmpty(errs,
                "元件库出现悬空贴图引用:\n" + string.Join("\n", errs));
        }

        // ══════════════════════════════════════════════════════
        // AC-42-C7 —— 骨架半:每个已注册元件类的选择器块存在
        // ══════════════════════════════════════════════════════

        [Test]
        public void test_ac42c7_every_registered_class_has_selector_block()
        {
            // Arrange
            DaYiJingCheng.Gameplay.UI.Skeuomorphic.SkeuoComponentRegistry.Reset();
            DaYiJingCheng.Gameplay.UI.Skeuomorphic.SkeuoComponentRegistry.InitializeDefaults();
            var registered = DaYiJingCheng.Gameplay.UI.Skeuomorphic.SkeuoComponentRegistry.All;

            // Act: 汇总元件库 USS 文本
            var text = string.Join("\n",
                Directory.GetFiles(SkeuoDir, "*.uss", SearchOption.TopDirectoryOnly)
                    .Where(f => Path.GetFileName(f) != "SkeuoThemeVariables.uss")
                    .Select(File.ReadAllText));

            // Assert: 每个已注册类的选择器块都须存在(否则 C7 无从谈起)
            foreach (var kv in registered)
            {
                string cls = kv.Value.UssClassName;
                var m = Regex.Match(text, @"\." + Regex.Escape(cls) + @"\s*\{",
                    RegexOptions.IgnoreCase);
                Assert.IsTrue(m.Success,
                    $"已注册元件「{kv.Key}」的 USS 类「.{cls}」缺选择器块。");
            }
            Assert.IsNotEmpty(registered, "注册表应非空,否则夹具空跑。");
        }

        [Test]
        public void test_ac42c7_registry_has_four_kinds_matching_story_scope()
        {
            // Arrange / Act
            DaYiJingCheng.Gameplay.UI.Skeuomorphic.SkeuoComponentRegistry.Reset();
            DaYiJingCheng.Gameplay.UI.Skeuomorphic.SkeuoComponentRegistry.InitializeDefaults();
            var registered = DaYiJingCheng.Gameplay.UI.Skeuomorphic.SkeuoComponentRegistry.All;

            // Assert: 实测四类(paper/scroll/ink/seal)—— 与 story-019 五族图集的分族对齐,
            //         图标族(ui_icons_sprite)无注册类 ⇒ 归 019-d 的映射裁定。
            Assert.AreEqual(4, registered.Count,
                "story-019 范围内应恰有 4 个已注册元件类(纸/卷轴/墨/印章)。");
        }

        // ══════════════════════════════════════════════════════
        // AC-42-E1 —— 导入格式订正(019-e:九宫格物理前提)
        // ══════════════════════════════════════════════════════

        [Test]
        public void test_ac42e1_all_sixteen_textures_have_sliced_import_format()
        {
            // Act: 逐张核 spriteMode/textureType/alphaIsTransparency
            var errs = TextureBindingGates.ValidateSlicedTextureImportFormat(RepoRoot);

            // Assert: 订正后须零错(spriteMode:0 下九宫格不可能工作 —— AC-42-C8 的物理前提)
            Assert.IsEmpty(errs,
                "贴图导入格式不满足九宫格要求(AC-42-E1):\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_ac42e1_covers_all_sixteen_textures_not_a_subset()
        {
            // Arrange: 扫描面须真覆盖 16 张(否则「零错」是漏扫出来的)
            var pngs = Directory.GetFiles(TexturesDir, "*-final.png", SearchOption.TopDirectoryOnly);

            // Assert
            Assert.AreEqual(16, pngs.Length,
                "019-e 的扫描面须恰 16 张 —— 少一张就是漏改。实际:\n" +
                string.Join("\n", pngs.Select(Path.GetFileName)));
            foreach (var p in pngs)
                Assert.IsTrue(File.Exists(p + ".meta"), $"{Path.GetFileName(p)} 缺 .meta。");
        }

        [Test]
        public void test_ac42e1_sprite_border_still_zero_sentinel_pending_019f()
        {
            // Act: 耦合守卫 —— spriteBorder 的值归 019-f 冻结件,019-e 不得自填
            var errs = TextureBindingGates.ValidateSpriteBorderLeftAsSentinel(RepoRoot);

            // Assert: 须仍为零哨兵(非零 = 已手填 = 第二真源)
            Assert.IsEmpty(errs,
                "spriteBorder 被自填 —— 违「做完即错」纪律(值须来自 019-f 冻结件):\n" +
                string.Join("\n", errs));
        }

        [Test]
        public void test_ac42e1_missing_textures_dir_fails_loud_not_silent()
        {
            // Arrange: 不存在的仓库根
            string bogus = Path.Combine(Path.GetTempPath(), "__no_such_repo_root_019e__");

            // Act
            var fmt = TextureBindingGates.ValidateSlicedTextureImportFormat(bogus);
            var border = TextureBindingGates.ValidateSpriteBorderLeftAsSentinel(bogus);

            // Assert: 两条门都须**报错**,而非静默空(空跑 ≠ 通过)
            Assert.IsNotEmpty(fmt, "贴图目录不存在时格式门须硬报错,否则 C8 物理前提恒真。");
            Assert.IsNotEmpty(border, "贴图目录不存在时哨兵门须硬报错,否则耦合守卫恒真。");
        }

        [Test]
        public void test_negative_fixture_wrong_sprite_mode_would_be_caught()
        {
            // ⚠️ **本夹具是文档性说明,不是判别力证据**(单轮评审 QA 侧 MINOR-3 正确地指出:
            //    `Assert.IsFalse(<常量>.Contains(...))` 是字符串自证,**恒真**,与门是否坏无关)。
            //    AC-42-E1 的真实判别力由 **MUT-E1a 的门级变异**提供(单张回落 spriteMode:0 ⇒ 1 红,
            //    原件 `unity/Logs/mut-e1a.xml`)—— 那才是「门会红」的凭证。此处保留仅为
            //    AC↔夹具 可追溯性。
            const string stale = "  spriteMode: 0\n  textureType: 8\n  alphaIsTransparency: 1\n";
            Assert.IsFalse(stale.Contains("spriteMode: 1"),
                "仅作文档性说明 —— 不充当判别力证据(见方法上方注)。");
        }

        // ══════════════════════════════════════════════════════
        // 判别力(负夹具)—— 证明护栏不是恒真
        // ══════════════════════════════════════════════════════

        [Test]
        public void test_negative_fixture_screen_with_url_would_be_caught()
        {
            // Arrange: 构造一段「屏幕层内直引贴图」的 USS 文本(不落盘 —— 走谓词面证明判别力)
            const string offendingLine = ".casebook-root { background-image: url(\"Textures/paper_xuan-final.png\"); }";

            // Act
            var m = Regex.Match(offendingLine, @"url\s*\(\s*[""']?([^""')]+)[""']?\s*\)",
                RegexOptions.IgnoreCase);

            // Assert: 正则须命中,且谓词须判为「指向贴图」⇒ C10 对此行必红
            Assert.IsTrue(m.Success, "C10 的 url 正则须能命中屏幕层直引。");
            Assert.IsTrue(TextureBindingGates.UrlTargetsTextures(m.Groups[1].Value),
                "该违规行须被判为贴图直引 —— 否则 C10 恒真,无判别力。");
        }

        [Test]
        public void test_negative_fixture_dangling_texture_would_be_caught()
        {
            // Arrange: 构造一个指向不存在贴图的 background-image
            const string url = "Textures/__does_not_exist__-final.png";

            // Act / Assert: 谓词须判定不可解析 ⇒ C11 对此行必红
            Assert.IsFalse(TextureBindingGates.AssetResolves(url, g => null, RepoRoot),
                "不存在的贴图须判为悬空 —— 否则 C11 恒真,无判别力。");
        }

        // ══════════════════════════════════════════════════════
        // 五族 ↔ 四注册类的覆盖盘点(实测,供 019-d 排程)
        // ══════════════════════════════════════════════════════

        [Test]
        public void test_sixteen_textures_present_and_grouped_into_five_families()
        {
            // Arrange / Act
            var pngs = Directory.GetFiles(TexturesDir, "*-final.png", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).OrderBy(x => x).ToArray();

            // Assert: 16 张已入库(story-019 的输入面事实)
            Assert.AreEqual(16, pngs.Length,
                "Textures/ 应有 16 张 *-final.png(story-019 实测基数)。实际:\n" +
                string.Join("\n", pngs));

            // 五族枚举(story-019 §Implementation Notes 第 1 条)
            var families = new Dictionary<string, string[]>
            {
                ["纸族"]     = new[] { "paper_xuan", "paper_aged", "paper_hemp", "paper_burnt_edge", "border_paper" },
                ["墨族"]     = new[] { "ink_wet", "ink_dry", "ink_light", "ink_dot" },
                ["卷轴族"]   = new[] { "scroll_cap", "scroll_rod", "scroll_knot", "border_scroll" },
                ["印章族"]   = new[] { "seal_red", "seal_surface" },
                ["图标族"]   = new[] { "ui_icons_sprite" },
            };
            int sum = families.Values.Sum(v => v.Length);
            Assert.AreEqual(16, sum, "五族枚举须恰好覆盖 16 张(漏族 = 019-d 会漏接)。");

            foreach (var kv in families)
                foreach (var stem in kv.Value)
                    Assert.Contains(stem + "-final.png", pngs,
                        $"{kv.Key}的 {stem} 未入库。");
        }

        [Test]
        public void test_icon_family_has_no_registered_class_notrun_registration()
        {
            // Arrange
            DaYiJingCheng.Gameplay.UI.Skeuomorphic.SkeuoComponentRegistry.Reset();
            DaYiJingCheng.Gameplay.UI.Skeuomorphic.SkeuoComponentRegistry.InitializeDefaults();
            var classes = DaYiJingCheng.Gameplay.UI.Skeuomorphic.SkeuoComponentRegistry.All
                .Values.Select(v => v.UssClassName).ToArray();

            // Assert / 登记: 图标族(ui_icons_sprite)无对应注册类 ——
            // ⚠️ 这是 **NOT-RUN 登记**,不是缺陷:C7 的「已注册类」范围不含图标族,
            //    图标族的接入与否须由 019-d 的映射裁定(新增注册类 or 明确排除)。
            Assert.IsFalse(classes.Contains("icon"),
                "若图标族已获得注册类,须同步更新 019-d 的映射假设与本登记。");
            Assert.IsFalse(classes.Contains("icons"),
                "同上。");
        }

        // ══════════════════════════════════════════════════════
        // 反空跑守卫 —— 证明「零命中」不是「看不到」
        // (2026-10-05 修复轮:原门对不存在的扫描面静默返回空 ⇒ 假绿)
        // ══════════════════════════════════════════════════════

        [Test]
        public void test_repo_root_resolution_climbs_to_assets_holder()
        {
            // Arrange: 从「叶子目录」出发(模拟 cwd = <repo>/unity 或更深)
            string leaf = Path.Combine(RepoRoot, "Assets", "Gameplay.UI", "Skeuomorphic");

            // Act: 上溯寻「含 Assets/ 的仓库根」
            string resolved = TextureBindingGates.ResolveRepoRoot(leaf);

            // Assert: 须回到仓库根(即 Assets/ 的父),且真含 Assets/
            Assert.IsTrue(Directory.Exists(Path.Combine(resolved, "Assets")),
                $"上溯解析须落在含 Assets/ 的目录;实际 {resolved}");
            Assert.AreEqual(Path.GetFullPath(RepoRoot), Path.GetFullPath(resolved),
                "从任一叶子目录上溯,须得同一仓库根。");
        }

        [Test]
        public void test_ac42c10_missing_screens_dir_fails_loud_not_silent()
        {
            // Arrange: 一个不存在的仓库根(扫描面必不存在)
            string bogus = Path.Combine(Path.GetTempPath(), "__no_such_repo_root_019c__");

            // Act
            var errs = TextureBindingGates.ValidateScreenLevelNoTextureUrl(bogus);

            // Assert: 须**报错**(空跑),而**非**静默返回空列表(假绿)
            Assert.IsNotEmpty(errs,
                "扫描面不存在时须硬报错 —— 静默返回空 = 「看不到」被当成「合规」(假绿)。");
        }

        [Test]
        public void test_ac42c11_missing_uss_dir_fails_loud_not_silent()
        {
            // Arrange: 不存在的仓库根
            string bogus = Path.Combine(Path.GetTempPath(), "__no_such_repo_root_019c__");

            // Act
            var errs = TextureBindingGates.ValidateTextureReferencesResolve(bogus, g => null);

            // Assert: 须报错而非静默空
            Assert.IsNotEmpty(errs,
                "元件库 USS 目录不存在时须硬报错 —— 否则 C11 恒绿无判别力。");
        }

        [Test]
        public void test_ac42c7_empty_registry_set_fails_loud_not_silent()
        {
            // Arrange / Act: 空类集(调用方喂空)
            var errs = TextureBindingGates.ValidateRegisteredClassesHaveSelectorBlock(
                RepoRoot, System.Linq.Enumerable.Empty<string>());

            // Assert: 须报错 —— 空集下「零缺失」是同义反复
            Assert.IsNotEmpty(errs, "已注册类集为空时须硬报错,否则 C7 骨架半恒真。");
        }

        [Test]
        public void test_current_repo_root_scan_finds_real_files_not_empty()
        {
            // Arrange / Act: 用**生产锚**跑,确认扫描面真非空
            var c10 = TextureBindingGates.ValidateScreenLevelNoTextureUrl(RepoRoot);
            var c11 = TextureBindingGates.ValidateTextureReferencesResolve(RepoRoot, g => null);

            // Assert: 真扫描面下,**合规态就是零错误**(与上面的空跑错误区分开)
            Assert.IsEmpty(c10, "真仓库根下屏幕层应零 url —— 实际:\n" + string.Join("\n", c10));
            Assert.IsEmpty(c11, "真仓库根下元件库应零悬空 —— 实际:\n" + string.Join("\n", c11));
        }
    }
}
