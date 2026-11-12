// 序章三段关卡源(P1-a)的夹具 —— 尺寸 / 分区可走性 / C1–C6 检查链。
//
// 权威来源:
//   `design/levels/chapter-1-tent-clinic.md`(2026-11-12 Approved)§2 空间总图 · §3 分区
//   ADR-022 §四(C1–C6)· ADR-015 §三(单一整数格)
//
// ⚠️ **本件钉的是「P1-a 的声明能过门」**,不是「关卡好玩」—— 后者的判据在规格 §8 的
//    16 步走查(归 P1-e),本件零覆盖。
// ⚠️ 本件零写盘(只调 `Define()` 与 `LevelCiRunner.Run`,不落文件)。

using NUnit.Framework;
using DaYiJingCheng.EditorTools.Level;

namespace DaYiJingCheng.Tests.LevelTool
{
    /// <summary>序章三段关卡源:尺寸 / 可走分区 / 检查链。</summary>
    [TestFixture]
    public class prologue_level_test
    {
        private const int SizeX = 128;
        private const int SizeZ = 40;

        /// <summary>索引公式 = `(x*H + y)*D + z`(H = 1)。</summary>
        private static int Index(int x, int z) => x * SizeZ + z;

        /// <summary>尺寸与 chunk 划分:128×1×40 = 5,120 格;chunk 8×1×8 ⇒ 16×5 = 80 片。</summary>
        [Test]
        public void test_prologue_dimensions_are128x40_withEightyChunks()
        {
            LevelSource src = PrologueLevel.Define();

            Assert.AreEqual(128, src.Width, "X");
            Assert.AreEqual(1, src.Height, "Y");
            Assert.AreEqual(40, src.Depth, "Z");
            Assert.AreEqual(5120, src.TotalCells, "格总数");
            Assert.AreEqual(5120, src.Walkable.Count, "可走性数组须与格数等长");
            Assert.AreEqual(8, src.ChunkSizeX);
            Assert.AreEqual(1, src.ChunkSizeY);
            Assert.AreEqual(8, src.ChunkSizeZ);
            Assert.AreEqual(16 * 5, (src.Width / src.ChunkSizeX) * (src.Depth / src.ChunkSizeZ),
                "chunk 片数 = 16×5 = 80(规格 §2)");
        }

        /// <summary>
        /// 可走分区:A/B/C 三区通、区外不通、教堂墙体挡而塌口通、药铺瓦砾挡。
        /// <para>⚠️ 判别力:任一条反过来(如把边界格改成可走、把塌口堵上)⇒ 本测当场红。</para>
        /// </summary>
        [Test]
        public void test_prologue_walkable_regions_arePartitionedAsSpecified()
        {
            LevelSource src = PrologueLevel.Define();
            var w = src.Walkable;

            Assert.AreEqual(1, w[Index(18, 21)], "A2 教堂前庭(出生点)须可走");
            Assert.AreEqual(1, w[Index(60, 20)], "B 七里路中段须可走");
            Assert.AreEqual(1, w[Index(108, 16)], "C2 帐篷位须可走");

            Assert.AreEqual(0, w[Index(0, 10)], "世界西边界不可走");
            Assert.AreEqual(0, w[Index(127, 10)], "世界东边界不可走");
            Assert.AreEqual(0, w[Index(10, 0)], "世界南边界不可走");
            Assert.AreEqual(0, w[Index(10, 39)], "世界北边界不可走");
            Assert.AreEqual(0, w[Index(40, 5)], "三区之外(z=5 在 A 之外、B 之外)须不可走");

            Assert.AreEqual(1, w[Index(8, 21)], "教堂内部可走");
            Assert.AreEqual(0, w[Index(4, 21)], "教堂西墙不可走");
            Assert.AreEqual(1, w[Index(15, 21)], "东墙塌口(z=21)可走 —— 离开教堂的唯一开口");
            Assert.AreEqual(0, w[Index(15, 18)], "东墙非塌口段不可走");

            Assert.AreEqual(0, w[Index(28, 6)], "药铺瓦砾块不可走");
            Assert.AreEqual(1, w[Index(24, 8)], "药铺可走区(POI 所在)可走");
        }

        /// <summary>
        /// C1–C6 检查链对新关卡退出码 = 0(无硬失败;`NotRun` / 告警允许存在,但会打印)。
        /// </summary>
        [Test]
        public void test_prologue_ciChain_exitsGreen()
        {
            LevelSource src = PrologueLevel.Define();

            LevelCiResult r = LevelCiRunner.Run(
                src, controller: null, catalog: null, navSampler: null,
                engineParams: CharacterControllerParamProbe.ProbeCurrent());

            Assert.IsNull(r.Fatal, "检查链不得以「跑不起来」收场(退出码 2)");
            Assert.AreEqual(0, r.ExitCode, "序章关卡源的 C1–C6 须无硬失败。报告:\n" + r.Render());
        }
    }
}
