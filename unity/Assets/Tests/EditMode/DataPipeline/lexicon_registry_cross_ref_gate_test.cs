// 病名册词表 ↔ 病种注册表 跨表外键门 —— 判据本体(2026-11-07 立)
//
// 立门缘由见 `Editor.Tools.Gates/LexiconRegistryCrossRefGate.cs` 头注:
// `case_judgment_lexicon.json` 里「伤寒」的 `disease_ids` 写成 `[1]`,而真注册表
// id 段是 2..17 ⇒ 病名册在生产里**恒空**、教学链 `judge` 步**永不发火**,
// 而构建 / 烘焙 / 全部夹具当时**都是绿的**(2026-11-07 真包走查才抓到)。
//
// 本文件补的是「门真的会红」这一面 —— 门存在 ≠ 门有效(F6 形态):
//   ① 正例:盘上真产物全过(否则门恒红,判据对合法输入误判);
//   ② 反例:悬空外键 ⇒ **恰一条**红,且报错串里带那个 id(不是「报了个红」就算);
//   ③ 空集守卫:任一侧 0 行 ⇒ 红(空集上判据恒真 = 空洞正控);
//   ④ 方向:注册表里没有对应词条的病种**合法**(AC-37-35 的双射候选拒依赖孤儿条目)。

using System.Collections.Generic;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Casebook;
using DaYiJingCheng.Sim;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.DataPipeline
{
    /// <summary>`LexiconRegistryCrossRefGate` 的判据本体(纯函数面,不碰文件系统)。</summary>
    internal sealed class lexicon_registry_cross_ref_gate_test
    {
        /// <summary>注册表两行:id 10 / 11(形状最小,够判外键归属)。</summary>
        private static IReadOnlyList<DiseaseRegistryEntry> Registry() => new[]
        {
            new DiseaseRegistryEntry { DiseaseId = 10, DiseaseKey = "DIS_TYPHOID" },
            new DiseaseRegistryEntry { DiseaseId = 11, DiseaseKey = "DIS_MEASLES" },
        };

        // ══════════════════════════════════════════════════════════════════════
        // ① 正例:外键落在注册表内 ⇒ 零红
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_validForeignKey_zeroErrors()
        {
            var lexicon = new[]
            {
                new JudgmentLexemeRow(1, "伤寒", new[] { 10 }),
                new JudgmentLexemeRow(2, "麻疹", new[] { 11, 10 }),   // 多对一亦合法
            };

            List<string> errs = LexiconRegistryCrossRefGate.RunAll(lexicon, Registry());
            CollectionAssert.IsEmpty(errs, "外键全在注册表内 ⇒ 零红");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 反例:悬空外键 ⇒ 恰一条红,且带出那个 id(2026-11-07 的真实形态:[1])
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_danglingDiseaseId_exactlyOneError_namingTheId()
        {
            var lexicon = new[]
            {
                new JudgmentLexemeRow(1, "伤寒", new[] { 1 }),    // ← 真包抓到的原值
                new JudgmentLexemeRow(2, "麻疹", new[] { 11 }),
            };

            List<string> errs = LexiconRegistryCrossRefGate.RunAll(lexicon, Registry());

            Assert.AreEqual(1, errs.Count, "恰一条 —— 合法行不得陪绑报红");
            StringAssert.Contains("disease_ids[0]=1", errs[0],
                "报错串须点名那个悬空 id(改坏之后读的人要能直接定位)");
            StringAssert.Contains("伤寒", errs[0], "须点名是哪一条词条");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 空集守卫:任一侧 0 行 ⇒ 红(空集上判据恒真)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_emptyLexicon_red()
        {
            List<string> errs = LexiconRegistryCrossRefGate.RunAll(
                System.Array.Empty<JudgmentLexemeRow>(), Registry());
            Assert.AreEqual(1, errs.Count, "词表 0 行 ⇒ 判据不可执行,必须红(不得静默绿)");
        }

        [Test]
        public void test_emptyRegistry_red()
        {
            var lexicon = new[] { new JudgmentLexemeRow(1, "伤寒", new[] { 10 }) };
            List<string> errs = LexiconRegistryCrossRefGate.RunAll(
                lexicon, System.Array.Empty<DiseaseRegistryEntry>());
            Assert.AreEqual(1, errs.Count, "注册表 0 行 ⇒ 无被引方,悬空无从判定 ⇒ 必须红");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④ 方向:孤儿条目 / 无词条的病种 ⇒ 均合法(不得反向报红)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_orphanEntry_andUnmappedDisease_areLegal()
        {
            var lexicon = new[]
            {
                new JudgmentLexemeRow(1, "湿温", System.Array.Empty<int>()),   // 孤儿(合法)
                new JudgmentLexemeRow(2, "温毒", null),                        // null ⇒ 视作孤儿
            };

            List<string> errs = LexiconRegistryCrossRefGate.RunAll(lexicon, Registry());
            CollectionAssert.IsEmpty(errs,
                "孤儿条目合法(AC-37-35 的双射候选拒**依赖**它);" +
                "注册表里有病种没有词条同样合法 —— 本门只查「词表 → 注册表」一向");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑤ 盘上真产物:全过(门不得恒红 —— 否则判据对合法输入误判)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_realArtifacts_zeroErrors()
        {
            List<string> errs = LexiconRegistryCrossRefGate.RunAll();
            CollectionAssert.IsEmpty(errs,
                "盘上 `case_judgment_lexicon.cooked.bytes` 的 disease_ids 须全在 " +
                "`disease_registry.cooked.bytes` 的 id 集内(2026-11-07 已修为 10 = DIS_TYPHOID)");
        }
    }
}
