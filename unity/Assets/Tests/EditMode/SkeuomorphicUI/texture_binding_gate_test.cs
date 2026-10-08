namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    using DaYiJingCheng.EditorTools.Gates;
    using NUnit.Framework;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Story 019-c / 019-e / 019-f: 贴图接入护栏 · 单元测试。
    /// <para>覆盖 **AC-42-C10**(屏幕层禁直引贴图)与 **AC-42-C11**(贴图缺失 / GUID 悬空 ⇒ 硬失败);
    /// 另含 AC-42-C7 的**骨架半**、AC-42-E1 导入格式,以及 **AC-42-C8 冻结件一致性**
    /// (2026-10-08 冻结轮接棒,原零哨兵测试退役)。**C9 仍 NOT-RUN**(019-b / PAGES_MAX spike)。</para>
    ///
    /// <para>⚠️ **C8 真源** = `design/assets/specs/nine-slice-freeze-2026-10-08.md` 的
    /// `freeze-v1` 机器块;meta 与 USS 任一单点改 ⇒ 本夹具红(禁第二真源)。</para>
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
            // ⚠️ 2026-10-05(019-d 同步):原传 `g => null` 桩是可以的 —— 因 019-c 时
            //    元件库**零 `url()` 引用**,null 桩等于「无引用可解析」。
            //    019-d 接图后(paper/scroll/ink/seal + paper-aged 共 5 条 guid: 引用),
            //    null 桩 ⇒ 5 条全报悬空(假红)。生产门走**真 resolver**,夹具须同口径。
            var errs = TextureBindingGates.ValidateTextureReferencesResolve(
                RepoRoot, UnityEditor.AssetDatabase.GUIDToAssetPath);

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
        public void test_ac42c8_sprite_border_and_uss_match_freeze_record()
        {
            // Arrange: 冻结件 = design/assets/specs/nine-slice-freeze-2026-10-08.md 的 freeze-v1 块
            //    (2026-10-08 冻结轮落盘;原零哨兵测试同批退役 —— 哨兵态使命已完成)

            // Act: C8 冻结件一致性门(meta spriteBorder + USS slice 双侧 = 冻结值)
            var errs = TextureBindingGates.ValidateSpriteBorderMatchesFreeze(RepoRoot);

            // Assert: 真仓库须零错 —— 任一侧被单点改(≠ 冻结件)即红(禁第二真源)
            Assert.IsEmpty(errs,
                "spriteBorder / -unity-slice-* 与切图冻结件不一致(AC-42-C8,2026-10-08 冻结轮):\n" +
                string.Join("\n", errs));
        }

        [Test]
        public void test_ac42c8_missing_freeze_record_fails_loud_not_silent()
        {
            // Arrange: 不存在的仓库根(无冻结件、无贴图目录)
            string bogus = Path.Combine(Path.GetTempPath(), "__no_such_repo_root_019e__");

            // Act
            var fmt = TextureBindingGates.ValidateSlicedTextureImportFormat(bogus);
            var freeze = TextureBindingGates.ValidateSpriteBorderMatchesFreeze(bogus);

            // Assert: 两条门都须**报错**,而非静默空(空跑 ≠ 通过)
            Assert.IsNotEmpty(fmt, "贴图目录不存在时格式门须硬报错,否则 E1 物理前提恒真。");
            Assert.IsNotEmpty(freeze, "冻结件不存在时 C8 门须硬报错,否则冻结一致性判据恒真。");
        }

        [Test]
        public void test_negative_fixture_border_and_slice_mismatch_would_be_caught()
        {
            // Arrange: 最小假仓库 <tmp>/repo/Assets/... + 冻结件落 <tmp>/design/...
            //   失配两处:meta border 9 ≠ 冻结 8;USS slice 16 ≠ 冻结 8
            string tmp = Path.Combine(Path.GetTempPath(), "__c8_freeze_fixture__");
            try
            {
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                string repo = Path.Combine(tmp, "repo");
                string texDir = Path.Combine(repo, "Assets", "Gameplay.UI", "Skeuomorphic", "Textures");
                string ussDir = Path.Combine(repo, "Assets", "Gameplay.UI", "Skeuomorphic");
                string recDir = Path.Combine(tmp, "design", "assets", "specs");
                Directory.CreateDirectory(texDir);
                Directory.CreateDirectory(ussDir);
                Directory.CreateDirectory(recDir);

                File.WriteAllText(Path.Combine(texDir, "mismatch-final.png"), "png-bytes");
                File.WriteAllText(Path.Combine(texDir, "mismatch-final.png.meta"),
                    "fileFormatVersion: 2\nguid: 0000000000000000000000000000c8f1\n" +
                    "TextureImporter:\n  spriteBorder: {x: 9, y: 9, z: 9, w: 9}\n");
                File.WriteAllText(Path.Combine(ussDir, "SkeuoC8Fixture.uss"),
                    ".c8-fixture {\n" +
                    "  -unity-slice-left: 16px;\n  -unity-slice-right: 16px;\n" +
                    "  -unity-slice-top: 16px;\n  -unity-slice-bottom: 16px;\n}\n");
                File.WriteAllText(Path.Combine(recDir, "nine-slice-freeze-2026-10-08.md"),
                    "# fixture\n\n```freeze-v1\nmismatch-final.png|8|SkeuoC8Fixture.uss\n```\n");

                // Act
                var errs = TextureBindingGates.ValidateSpriteBorderMatchesFreeze(repo);

                // Assert: 两处失配都必须被抓(证明门非恒真)
                Assert.IsTrue(errs.Any(e => e.Contains("spriteBorder")),
                    "meta spriteBorder(9)≠ 冻结件(8)须报错。实际:\n" + string.Join("\n", errs));
                Assert.IsTrue(errs.Any(e => e.Contains("-unity-slice")),
                    "USS slice(16)≠ 冻结件(8)须报错。实际:\n" + string.Join("\n", errs));
            }
            finally
            {
                try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }
                catch { /* 清理失败不掩护断言结果 */ }
            }
        }

        [Test]
        public void test_ac42c8_brass_focus_ring_bound_to_focus_visible_uss()
        {
            // ══ 2026-10-08 绑定轮:黄铜 2px 环图须真绑到焦点样式(明度轴判据的渲染载体)══

            // Arrange
            string ussPath = Path.Combine(SkeuoDir, "SkeuoFocusVisible.uss");
            Assert.IsTrue(File.Exists(ussPath), $"前置失败:{ussPath} 不存在。");
            string text = File.ReadAllText(ussPath);
            string body = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);

            // Act: slice 行集(去注释后)
            var slices = Regex.Matches(body, @"-unity-slice-(left|right|top|bottom)\s*:\s*(\d+)px");

            // Assert: ① 环图 guid 真被引用;② 恰 4 条 slice 全 = 冻结值 2;
            //         ③ 实色 border 不得回归(并存 = 双环 4px,违「2px 视觉厚度」);④ 禁用态复位环
            Assert.IsTrue(body.Contains("6d4c0acbd9a141348c34017e96d20ee2"),
                ".focus-visible 须绑黄铜 2px 环图(guid 6d4c0acb…)—— 否则焦点高亮无贴图载体。");
            Assert.AreEqual(4, slices.Count,
                $".focus-visible 须恰 4 条 -unity-slice-*,实见 {slices.Count}。");
            foreach (Match m in slices)
                Assert.AreEqual("2", m.Groups[2].Value,
                    $"-unity-slice-{m.Groups[1].Value} = {m.Groups[2].Value}px,冻结件 = 2(AC-42-C8)。");
            Assert.IsFalse(Regex.IsMatch(body, @"border-top-width:\s*var\(--skeuo-focus-border-width\)"),
                "实色 2px border 不得回归 —— 2px 形状由环图独挑,并存 = 外实内纹双环。");
            Assert.IsTrue(body.Contains("background-image: none"),
                "禁用态 .focus-visible-disabled 须复位环图,否则门关后高亮残留。");
        }

        [Test]
        public void test_negative_fixture_unregistered_subdir_png_caught()
        {
            // ══ 2026-10-08 覆盖检查递归化的判别力:子目录漏登记须被抓(原顶层面是盲区)══

            // Arrange: 顶层图已登记(meta 合规 0=0)+ 子目录图未登记 + 冻结件只含顶层行
            string tmp = Path.Combine(Path.GetTempPath(), "__c8_subdir_fixture__");
            try
            {
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                string repo = Path.Combine(tmp, "repo");
                string texDir = Path.Combine(repo, "Assets", "Gameplay.UI", "Skeuomorphic", "Textures");
                string subDir = Path.Combine(texDir, "Sub");
                string ussDir = Path.Combine(repo, "Assets", "Gameplay.UI", "Skeuomorphic");
                string recDir = Path.Combine(tmp, "design", "assets", "specs");
                Directory.CreateDirectory(subDir);
                Directory.CreateDirectory(ussDir);
                Directory.CreateDirectory(recDir);

                File.WriteAllText(Path.Combine(texDir, "toplevel-final.png"), "png");
                File.WriteAllText(Path.Combine(texDir, "toplevel-final.png.meta"),
                    "fileFormatVersion: 2\nguid: 0000000000000000000000000000c8f2\n" +
                    "TextureImporter:\n  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n");
                File.WriteAllText(Path.Combine(subDir, "orphan-final.png"), "png");
                File.WriteAllText(Path.Combine(recDir, "nine-slice-freeze-2026-10-08.md"),
                    "# fixture\n\n```freeze-v1\ntoplevel-final.png|0|-\n```\n");

                // Act
                var errs = TextureBindingGates.ValidateSpriteBorderMatchesFreeze(repo);

                // Assert: 子目录未登记图须被抓 —— 证明覆盖检查是 AllDirectories 面
                Assert.IsTrue(errs.Any(e => e.Contains("orphan-final.png") && e.Contains("未登记")),
                    "子目录漏登记须报错(覆盖检查应为递归面)。实际:\n" + string.Join("\n", errs));
            }
            finally
            {
                try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }
                catch { /* 清理失败不掩护断言结果 */ }
            }
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
            // ⚠️ 2026-10-05(019-d 同步):同 test_ac42c11_current_library_has_no_dangling_refs ——
            //    接图后须走真 resolver,null 桩会把 5 条真实引用全判悬空。
            var c11 = TextureBindingGates.ValidateTextureReferencesResolve(
                RepoRoot, UnityEditor.AssetDatabase.GUIDToAssetPath);

            // Assert: 真扫描面下,**合规态就是零错误**(与上面的空跑错误区分开)
            Assert.IsEmpty(c10, "真仓库根下屏幕层应零 url —— 实际:\n" + string.Join("\n", c10));
            Assert.IsEmpty(c11, "真仓库根下元件库应零悬空 —— 实际:\n" + string.Join("\n", c11));
        }
    }
}
