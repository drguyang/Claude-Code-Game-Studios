// 权威来源:docs/tech-debt-register.md `TD-audio-035` ①(格边长 = 44 自造代偿常量)
//   · ADR-015 §三(`WorldPos` 单一整数格;`LATTICE_SIZE` 取值归系统 6)
//   · GDD `world-and-ecozones.md` F-6-1 / F-6-2(`LATTICE_SIZE` 的契约与下界)
//   · ADR-022 §三(关卡工具是逻辑层几何的**唯一**作者,`lattice_size_mm` 随导出)
//   · ADR-014(两阶段烘焙:`world_geometry.json` → `world_geometry.cooked`)
//   · `Audio/LatticeToWorld.cs` 文件头(「44 不得自造比例」)
//   · 裁定:2026-10-29(本债 ① 换源轮)
//
// ⚠️ **本夹具钉四件事**:
//   ① 毫米 → 米**只在装配层换算一次**(`MetersFromMillimetres`)—— 2.5 ≠ 1 才算判别力;
//   ② 缝的默认值是 **0 = 未供给**(不是 1 米)—— 旧默认 1f 是一份悄悄生效的 44 自造比例;
//   ③ `ApplyLatticeFromWorldGeometry` 把 6 的值写进缝,缝为 null 时空操作(不抛);
//   ④ 生产 `BootRoot` **在 `Assemble` 之前**调该写入 —— 定序判据(停用/挪位 ⇒ 本测红)。
//
// ⚠️ **本夹具不证**:
//   · 数值 1000 mm 是否正确 —— 归**用户数值轮**(`P0ClinicLevel.LatticeSizeMm` 自陈占位值);
//   · 产物 → 出口那一段 —— 由 `world_geometry_load_test::test_ac4_latticeSizeOutParam_comesFromArtifact`
//     用 2500 mm 的合成产物钉(本文件不重复造世界夹具);
//   · 缝 → 渲染目标那一段 —— 由 `spatialization_wiring_test::test_td028_productionAssembly_withSeams_*` 钉。
//
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖 · 零写盘(只读一份源文件)。

using System;
using System.Text;
using DaYiJingCheng.Gameplay.Boot;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>`TD-audio-035` ①:44 的格边长来自系统 6 的产物,不再自造。</summary>
    [TestFixture]
    public sealed class AudioLatticeSourceTest
    {
        // ══════════════ ② 毫米 → 米:唯一换算处 ══════════════

        [Test]
        public void test_metersFromMillimetres_convertsAtOneThousandth()
        {
            // 判别力:把 `/ 1000f` 写成 `/ 1f`(或不换算)⇒ 2500 那条立刻红
            Assert.That(AudioAssemblySeams.MetersFromMillimetres(2500), Is.EqualTo(2.5f).Within(1e-6),
                "2500 mm 须 = 2.5 m —— 换算漏掉或比例错会全文静默(声音距离整体差 1000 倍)");
            Assert.That(AudioAssemblySeams.MetersFromMillimetres(1000), Is.EqualTo(1f).Within(1e-6),
                "1000 mm = 1 m(今日产物值 —— 它就是 44 兜底常量的米制形态)");
            Assert.That(AudioAssemblySeams.MetersFromMillimetres(1), Is.EqualTo(0.001f).Within(1e-9),
                "1 mm = 0.001 m(亚米格边长须不被截断成 0)");
        }

        [Test]
        public void test_metersFromMillimetres_rejectsNonPositive()
        {
            // 定义域收窄:<= 0 的格边长不是「小」,是**无意义**(0 ⇒ 所有格塌到原点;负 ⇒ 镜像世界)
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AudioAssemblySeams.MetersFromMillimetres(0),
                "0 mm 必须拒 —— 否则 44 静默把整个世界的声源压到原点");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AudioAssemblySeams.MetersFromMillimetres(-1),
                "负 mm 必须拒 —— 否则左右/前后整体镜像,而混响听起来「仍然正常」");
        }

        // ══════════════ ② 缝的默认值 = 未供给 ══════════════

        [Test]
        public void test_seam_default_isUnsupplied_notOneMetre()
        {
            // 🔁 旧默认 1f 是「44 自造的比例」:它恰好等于 6 的产物值,所以从可观测面分不出来。
            //    改成 0 = 未供给后,「组合根回落到兜底常量」是一个**显式**的分支。
            var seams = new AudioAssemblySeams();

            Assert.That(seams.LatticeSize, Is.EqualTo(0f),
                "缝的默认格边长须为 0(未供给)—— 1f 是悄悄生效的 44 自造比例(承 ADR-024 §⑥ 单一出处)");
        }

        // ══════════════ ③ 写入点 ══════════════

        [Test]
        public void test_applyLattice_writesSixSourceValue()
        {
            var seams = new AudioAssemblySeams();

            AudioAssemblySeams.ApplyLatticeFromWorldGeometry(seams, 2500);

            Assert.That(seams.LatticeSize, Is.EqualTo(2.5f).Within(1e-6),
                "写进缝的须是 6 的 2500 mm 的米制形态,不是 44 的任何常量");
        }

        [Test]
        public void test_applyLattice_nullSeam_isNoop()
        {
            // 缝不接(预载失败)的路径:该分支上 AudioRenderAssembled = false 本就可观测,
            // 故这里空操作是「同一处置」而不是吞错。
            Assert.DoesNotThrow(() => AudioAssemblySeams.ApplyLatticeFromWorldGeometry(null, 2500),
                "缝为 null 时 apply 不该抛 —— 预载失败路径上音频面本就整体不装配");
        }

        // ══════════════ ④ 生产装配序(定序)══════════════

        [Test]
        public void test_bootRoot_appliesLattice_beforeAssemble()
        {
            string code = StripComments(ReadProductionSource("Gameplay.Boot/BootRoot.cs"));

            int apply = code.IndexOf("ApplyLatticeFromWorldGeometry(", StringComparison.Ordinal);
            int assemble = code.IndexOf("CompositionRoot.Assemble(", StringComparison.Ordinal);

            Assert.That(apply, Is.GreaterThanOrEqualTo(0),
                "生产启动序里找不到格边长的写入点 —— TD-audio-035 ① 的换源被摘掉了" +
                "(组合根会静默回落 1 m,而 6 的值可能与它不同)");
            Assert.That(assemble, Is.GreaterThanOrEqualTo(0), "前置:启动序里应有 Assemble 调用");
            Assert.That(apply, Is.LessThan(assemble),
                "写入点排在 Assemble **之后** —— 组合根在装配期一次性把格边长交给导演/渲染目标," +
                "之后再写不生效(等于没换源)");
        }

        // ══════════════ 工具 ══════════════

        private static string ReadProductionSource(string relativePath)
        {
            string path = Application.dataPath.Replace('\\', '/') + "/" + relativePath;
            Assert.That(System.IO.File.Exists(path), Is.True,
                $"找不到生产源文件 {relativePath} —— 判据的对象被移动/改名,须同改本测");
            return System.IO.File.ReadAllText(path);
        }

        /// <summary>剥掉行注释与块注释(被测字面量 `ApplyLatticeFromWorldGeometry(` 也出现在注释里,
        /// 不剥会把「注释里写了」当成「代码里调了」—— 那正是本仓反复抓到的假绿形态)。</summary>
        private static string StripComments(string text)
        {
            var sb = new StringBuilder(text.Length);
            bool inBlock = false;
            int i = 0;
            while (i < text.Length)
            {
                if (inBlock)
                {
                    int close = text.IndexOf("*/", i, StringComparison.Ordinal);
                    if (close < 0) return sb.ToString();
                    inBlock = false;
                    i = close + 2;
                    continue;
                }

                int lineComment = text.IndexOf("//", i, StringComparison.Ordinal);
                int blockOpen = text.IndexOf("/*", i, StringComparison.Ordinal);

                if (blockOpen < 0 && lineComment < 0) { sb.Append(text, i, text.Length - i); break; }
                if (blockOpen < 0 || (lineComment >= 0 && lineComment < blockOpen))
                {
                    sb.Append(text, i, lineComment - i);
                    int newline = text.IndexOf('\n', lineComment);
                    if (newline < 0) break;
                    i = newline;                       // 保留换行,继续下一行
                    continue;
                }

                sb.Append(text, i, blockOpen - i);
                inBlock = true;
                i = blockOpen + 2;
            }
            return sb.ToString();
        }
    }
}
