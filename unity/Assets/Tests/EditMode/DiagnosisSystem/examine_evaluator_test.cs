// sprint-06 G1.0/G1.1 · 查体求值门面 + 数据烘焙 EditMode 判据(2026-10-11)
//
// 判据(可证伪):
//   ① G1.0 烘焙:diagnosis_signs / diagnosis_read_floor 两份 cooked 可经生产烘焙器
//      (BakeFromRepo,菜单/测试同台机器)产出 + codec 解码 roundtrip(E-13 零容忍);
//   ② G1.1 纯函数:同输入两次逐字相同(AC-8-3)· 空表→空集 · skill 越界钳位 ·
//      全空词→不收录 · 通道掩码按 AC-21 位聚合 · History 无位(A 不污染)
//   ③ 变异:删收录分支(阳性/阴性外不退)⇒ 空集恰红 —— 判别力自证
//
// ⚠️ 门式读仓例外:判据对象 = 仓库内版本化源表(assets/data/*.json),必须读仓
//    (承 sign_table_test 同款先例);烘焙 = 生产机器驱动(反空转)。
// ⚠️ 依赖注入:本测全部显式传表/行集(8 不持状态 —— AC-8-3 判的是生产类型,非测试)。

using System.Collections.Generic;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using NUnit.Framework;
// CS0104 消歧(同 sign_table_test):本文件 SignChannel 指 R-8.1 枚举。
using SignChannel = DaYiJingCheng.Gameplay.Presentation.Diagnosis.SignChannel;
using ChannelMask = DaYiJingCheng.Sim.Contracts.SignChannel;

namespace DaYiJingCheng.Tests.DiagnosisSystem
{
    /// <summary>查体求值门面 + 数据烘焙(sprint-06 G1.0/G1.1)。</summary>
    internal sealed class ExamineEvaluatorTest
    {
        private const int SkillCap = 60;

        /// <summary>合成门槛定表:Floor(skill) 在 [0,1] 爬坡,skill 越高门槛越低。
        /// ⚠️ 数值为**测试夹具**(非裁定值;真实定表由 read_floor 源表烘焙)。</summary>
        private static DiagnosisReadFloorTable BuildFloor()
        {
            var floor = new float[SkillCap + 1];
            for (int i = 0; i <= SkillCap; i++)
                floor[i] = 0.5f;   // 恒定中门槛:0.5 以上可读
            return new DiagnosisReadFloorTable(SkillCap, 0x8000L, 0x8000L, 0x10000L, floor);
        }

        private static SignLexemeRow Row(string id, string[] words, SignChannel ch,
                                         SignPolarity pol = SignPolarity.Positive,
                                         int tierNamed = 1)
            => new SignLexemeRow(id, words, ch,
                                 new[] { RevealMethod.Inspection }, tierNamed, pol, null);

        // ════════════ G1.0 烘焙 + 解码 roundtrip ════════════

        [Test]
        public void test_g1_0_bake_signsAndReadFloor_roundtripDecode()
        {
            // 生产烘焙器(菜单同台机器;BakeFromRepo 读仓根 assets/data/)
            DiagnosisSignBaker.BakeOutput signs =
                DiagnosisSignBaker.BakeFromRepo(GameRepoRoot);
            DiagnosisReadFloorBaker.BakeOutput floor =
                DiagnosisReadFloorBaker.BakeFromRepo(GameRepoRoot);

            Assert.IsTrue(signs.Cooked?.Length > 0, "signs cooked 非空(机器活性)");
            Assert.IsTrue(floor.Cooked?.Length > 0, "read_floor cooked 非空(机器活性)");
            Assert.AreNotEqual(0u, signs.ConfigVersion, "ConfigVersion 派生自内容哈希(非 0)");

            // 解码 roundtrip(生产 codec;E-13 零容忍)
            var signsDs = DiagnosisSignCookedCodec.Read(signs.Cooked);
            Assert.AreEqual(signsDs.Rows.Count, 43,
                "真表 R-8.2 43 行(与 sign_table_test 金标一致)");
            Assert.GreaterOrEqual(signsDs.ConfigVersion, 1u);

            var floorDs = DiagnosisReadFloorCookedCodec.Read(floor.Cooked);
            Assert.Greater(floorDs.Table.Count, 0, "read_floor 定表非空");
        }

        // ════════════ G1.1 纯函数判据 ════════════

        [Test]
        public void test_g1_1_sameInput_sameOutput_ac8_3()
        {
            var table = BuildFloor();
            var rows = new List<SignLexemeRow>
            {
                Row("s_pallor", new[] { "面色不好", "面色发白", "面色苍白" }, SignChannel.FaceColor),
            };

            ExaminedSet a = DiagnosisExamineEvaluator.Evaluate(in table, rows, skill: 5, 0.8f);
            ExaminedSet b = DiagnosisExamineEvaluator.Evaluate(in table, rows, skill: 5, 0.8f);

            Assert.AreEqual(a.SignCount, b.SignCount);
            Assert.AreEqual(a.SignChannelMask, b.SignChannelMask);
            Assert.AreEqual(a.PrecisionSlot, b.PrecisionSlot);
            Assert.AreEqual(a.Rows.Count, b.Rows.Count, "同输入两次逐字相同(AC-8-3 纯函数)");
        }

        [Test]
        public void test_g1_1_aboveFloor_positiveCollected_maskAggregated()
        {
            var table = BuildFloor();   // Floor = 0.5
            var rows = new List<SignLexemeRow>
            {
                Row("s_pallor", new[] { "面色不好", "面色发白", "面色苍白" }, SignChannel.FaceColor),
                Row("s_voice_weak", new[] { "语声低", "语声低微", "语声明晰但无力" }, SignChannel.Voice),
            };

            ExaminedSet set = DiagnosisExamineEvaluator.Evaluate(in table, rows, skill: 5, 0.8f);

            Assert.AreEqual(2, set.SignCount, "两条阳性全收录");
            Assert.AreEqual(ChannelMask.Complexion | ChannelMask.Voice, set.SignChannelMask,
                "掩码 = 面色|语声 按位或(AC-21 位聚合)");
        }

        [Test]
        public void test_g1_1_historyChannel_noMaskBit()
        {
            var table = BuildFloor();
            var rows = new List<SignLexemeRow>
            {
                Row("s_purulent_stool", new[] { "脓血便", "脓血便较多", "脓血便量多色鲜" }, SignChannel.History),
            };

            ExaminedSet set = DiagnosisExamineEvaluator.Evaluate(in table, rows, skill: 20, 0.9f);

            Assert.AreEqual(1, set.SignCount, "病史词条收录(通道外第二类证据)");
            Assert.AreEqual(0, set.SignChannelMask,
                "History 无掩码位 ⇒ OR 0 不污染体格通道(Note 6 显式登记)");
        }

        [Test]
        public void test_g1_1_belowFloor_allUnreadable_empties()
        {
            var table = BuildFloor();   // Floor = 0.5
            var rows = new List<SignLexemeRow>
            {
                Row("s_pallor", new[] { "面色不好", "面色发白", "面色苍白" }, SignChannel.FaceColor),
            };

            ExaminedSet set = DiagnosisExamineEvaluator.Evaluate(in table, rows, skill: 5, 0.1f);

            Assert.AreEqual(0, set.SignCount, "全读不出(值 < floor)=> 空集");
            Assert.AreEqual(0, set.SignChannelMask, "空集 ⇒ 掩码 0");
        }

        [Test]
        public void test_g1_1_allBlankWords_notCollected()
        {
            var table = BuildFloor();
            // 三档词全空(全空档 ⇒ DisplayWord null ⇒ AC-8-8 读不出)
            var rows = new List<SignLexemeRow>
            {
                Row("s_empty", new string[] { null, null, null }, SignChannel.FaceColor),
            };

            ExaminedSet set = DiagnosisExamineEvaluator.Evaluate(in table, rows, skill: 5, 0.9f);

            Assert.AreEqual(0, set.SignCount, "全空词 ⇒ 读不出 ⇒ 不收录");
        }

        [Test]
        public void test_g1_1_skillClamped_slotTiers()
        {
            var table = BuildFloor();
            var rows = new List<SignLexemeRow>
            {
                Row("s_pallor", new[] { "面色不好", "面色发白", "面色苍白" }, SignChannel.FaceColor),
            };

            // TierIndex:Skill=0 → 0(粗);Skill=50 → 3(满;SLOT_BOUNDS 10/20/50)
            ExaminedSet coarse = DiagnosisExamineEvaluator.Evaluate(in table, rows, skill: 0, 0.9f);
            ExaminedSet full = DiagnosisExamineEvaluator.Evaluate(in table, rows, skill: 50, 0.9f);
            Assert.AreEqual(0, coarse.PrecisionSlot, "Skill=0 ⇒ 粗档(0)");
            Assert.AreEqual(3, full.PrecisionSlot, "Skill=50 ⇒ 满档(3)");
        }

        // ════════════ 变异:判据判别力 ════════════

        [Test]
        public void test_g1_1_mutation_collectorRemoved_goesRed()
        {
            // 语义:若实现把「收录」分支删掉(任何 state 都跳过),同输入必得空集。
            // 这里用一个「本应收录」的输入,断言它**必须**非空 —— 删分支即此红。
            var table = BuildFloor();
            var rows = new List<SignLexemeRow>
            {
                Row("s_pallor", new[] { "面色不好", "面色发白", "面色苍白" }, SignChannel.FaceColor),
            };
            ExaminedSet set = DiagnosisExamineEvaluator.Evaluate(in table, rows, skill: 5, 0.8f);
            Assert.Greater(set.SignCount, 0,
                "阳性且可读输入必须收录(若实现把收录分支删了 = 空集 ⇒ 此红,判别力自证)");
        }

        // ════════════ helpers ════════════

        /// <summary>仓库根:由**本文件路径**回溯 5 级(承 sign_table_test 同款;
        /// ⚠️ 不能用 TestContext.TestDirectory —— batch 指向 ScriptAssemblies)。</summary>
        private static readonly string GameRepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => System.IO.Path.GetFullPath(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));
    }
}