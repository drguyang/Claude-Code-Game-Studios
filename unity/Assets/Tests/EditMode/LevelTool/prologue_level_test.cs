// 序章三段关卡源(P1-a)的夹具 —— 尺寸 / 分区可走性 / C1–C6 检查链。
//
// 权威来源:
//   `design/levels/chapter-1-tent-clinic.md`(2026-11-12 Approved)§2 空间总图 · §3 分区
//   ADR-022 §四(C1–C6)· ADR-015 §三(单一整数格)
//
// ⚠️ **本件钉的是「P1-a 的声明能过门」**,不是「关卡好玩」—— 后者的判据在规格 §8 的
//    16 步走查(归 P1-e),本件零覆盖。
// ⚠️ 本件零写盘(只调 `Define()` 与 `LevelCiRunner.Run`,不落文件)。

using System.Collections.Generic;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Level;
using DaYiJingCheng.Sim.World;

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

        // ─────────────────────────────────────────────────────────────────────
        // 医馆骨架平移(用户 2026-11-12 裁定「甲案」)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>平移量 —— **字面量**(不从被测类读,否则变异会两边一起动)。</summary>
        private const int ShiftX = 96;
        private const int ShiftZ = 4;

        /// <summary>
        /// P0 医馆的 18 个槽**纯平移**进营地:每个槽反推 -(96,4) 后,
        /// 与 `P0ClinicLevel` 的同位槽在**类型 / 开档标记 / 模块 id** 上逐字段相同,且一一对应。
        /// <para>⚠️ 判别力:改任何一格(哪怕只改 1 格 x)=&gt; 反推后找不到同位槽 ⇒ 红。
        /// 这条钉的是「纯平移」这条**性质**,不是 18 个坐标的字面量 ——
        /// 有人为了「微调一格」手改坐标时,它会当场拦住并要求走裁定。</para>
        /// <para>为什么承重:24 的房间析出靠家具**连通簇**、4 的交互三件靠面板 / 开关 / 器具的
        /// **邻接** —— 差一格这些相对关系就不再等价于 P0 已验证的形态。</para>
        /// </summary>
        [Test]
        public void test_prologue_clinicSlots_arePureShiftOfP0Clinic()
        {
            LevelSource pro = PrologueLevel.Define();
            LevelSource p0 = P0ClinicLevel.Define();

            Assert.AreEqual(p0.Slots.Count, pro.Slots.Count,
                "槽数须与 P0 医馆相等(18)—— 少一条 = 丢了骨架件,多一条 = 新件没走裁定");

            // ① 正向:每个序章槽反推后必须命中一条 P0 槽
            var matched = new HashSet<int>();
            foreach (SlotSpec s in pro.Slots)
            {
                int px = s.X - ShiftX, pz = s.Z - ShiftZ;
                bool hit = false;

                for (int i = 0; i < p0.Slots.Count; i++)
                {
                    SlotSpec c = p0.Slots[i];
                    if (c.X != px || c.Y != s.Y || c.Z != pz) continue;

                    Assert.AreEqual(c.Type, s.Type,
                        $"槽 ({s.X},{s.Z}) 反推得 P0 ({px},{pz}),但类型不同");
                    Assert.AreEqual(c.BakedInitial, s.BakedInitial,
                        $"槽 ({s.X},{s.Z}) 反推得 P0 ({px},{pz}),但开档占用标记不同 —— " +
                        "甲案的承诺是逐槽保持原值");
                    Assert.AreEqual(c.ModuleId, s.ModuleId,
                        $"槽 ({s.X},{s.Z}) 反推得 P0 ({px},{pz}),但模块 id 不同");

                    matched.Add(i);
                    hit = true;
                    break;
                }

                Assert.IsTrue(hit,
                    $"槽 ({s.X},{s.Z}, {s.Type}) 反推 -(96,4) 得 ({px},{pz}) 在 P0 医馆里不存在 " +
                    "—— 医馆骨架不是纯平移");
            }

            // ② 反向:每条 P0 槽都被命中过(排除「序章多出一格落在 P0 空位」这种假通过)
            Assert.AreEqual(p0.Slots.Count, matched.Count,
                "存在 P0 槽未被对应 —— 序章的骨架比 P0 少件");
        }

        /// <summary>
        /// 帐篷 = **唯一新建**:`(108,16)` 字面量哨兵、`bakedInitial = false`、`NoModule`。
        /// <para>⚠️ 这是用户裁定的落点坐标(规格 §7 的出生/相机锚点以它定位);
        /// 用字面量而非从常量推导,是为了让「常量被顺手改了」也能当场红。</para>
        /// </summary>
        [Test]
        public void test_prologue_tent_isTheOnlyNewPieceAt108_16()
        {
            LevelSource pro = PrologueLevel.Define();

            int tents = 0;
            foreach (SlotSpec s in pro.Slots)
            {
                if (s.Type != SlotType.Tent) continue;
                tents++;
                Assert.AreEqual(108, s.X, "帐篷 X(裁定值)");
                Assert.AreEqual(16, s.Z, "帐篷 Z(裁定值)");
                Assert.IsFalse(s.BakedInitial, "帐篷是序章唯一新建件 ⇒ 不得是开档既有");
                Assert.AreEqual(SlotSpec.NoModule, s.ModuleId, "新建槽不得预置模块");
            }

            Assert.AreEqual(1, tents, "帐篷槽恰好一个");
        }

        /// <summary>
        /// 医馆柜随骨架平移:`instance_id` 沿用 P0 的 1L,格 = P0 柜格 +(96,4)。
        /// <para>⚠️ `instance_id` 沿用是刻意的 —— 装配根的 `ReconstructItemHighWater`
        /// 按高水位抬号,换关卡不改这条契约;改号会让开档物品实例与存档里的引用错位。</para>
        /// </summary>
        [Test]
        public void test_prologue_cabinet_followsTheShift()
        {
            LevelSource pro = PrologueLevel.Define();

            ContainerSpec cabinet = default;
            bool found = false;
            foreach (ContainerSpec c in pro.Containers.Containers)
            {
                if (c.InstanceId != P0ClinicLevel.CabinetInstanceId) continue;
                cabinet = c;
                found = true;
                break;
            }

            Assert.IsTrue(found,
                $"医馆柜(instance_id = {P0ClinicLevel.CabinetInstanceId})须在容器集里");
            Assert.AreEqual(P0ClinicLevel.CabinetCellX + ShiftX, cabinet.Cell.X, "柜 X");
            Assert.AreEqual(P0ClinicLevel.CabinetCellZ + ShiftZ, cabinet.Cell.Z, "柜 Z");

            // 字面量哨兵:上两行若被改常量会两边一起动,这一行钉绝对落点
            Assert.AreEqual(101, cabinet.Cell.X, "柜的绝对 X(P0 (5,5) +(96,4))");
            Assert.AreEqual(9, cabinet.Cell.Z, "柜的绝对 Z(P0 (5,5) +(96,4))");

            Assert.AreEqual(1, pro.Walkable[Index(cabinet.Cell.X, cabinet.Cell.Z)],
                "柜须落在可走格上(否则开档点不到它)");
        }
    }
}
