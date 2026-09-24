// 权威来源:Story 010 AC-21a-63(铸造点唯一来源 = IIdAuthority;静态检索 + 单测调用权威)
//          · D-21-26/27(仅主机铸币;客户端本地铸造 = 迁移后重号 ⇒ 物品悄悄合并/丢失)
//          · ADR-006 Amendment B 机制 A(计数器永不复位 0;next = max(id)+1 高水位重构)
//          · ADR-007 §四(PatientId.None = -1 哨兵不进 id 空间、不污染高水位)
//          · ADR-010 §五(ItemInstanceId.Next() 机制落点)
//
// ⚠️ 落点:账本路径 = tests/unit/item_database/id_authority.cs(GDD 指名,照录);
//    Unity 只编译 unity/Assets/ 树 ⇒ 真身 = 本文件(Story 001–009 同一先例)。
//
// 换名铸造扫描词表(AC-63 边例「扫描词表须登记防绕过」—— 本表即登记处,加词 = 改本文件):
//   ① "new ItemInstanceId("  —— 手工铸造(白名单 = ItemInstanceId.cs 构造器定义自身 +
//      IdAuthority.cs 权威实现,二者是「唯一铸造来源」的本体与定义);
//   ② "Guid.NewGuid"         —— 全扫描面零容忍(GUID 型 id 绕过计数器 ⇒ 无法高水位重构);
//   ③ "new Random("          —— 全扫描面零容忍;
//   ④ "RandomNumberGenerator" —— 全扫描面零容忍(System.Security.Cryptography 型换名)。
// 扫描面 = unity/Assets 全部 *.cs,排除 /Tests/(测试自身含字面量,不排除即自我扫红);
// 注释剥离后匹配(文档性提及不算铸造)。**字符串字面量不剥**(与 Story 002/008 同口径)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.ItemDatabase
{
    [TestFixture]
    internal sealed class IdAuthorityTest
    {
        // ══════════════ AC-63 静态检索:铸造点 ⊆ IdAuthority ══════════════

        [Test]
        public void test_ac21a63_mintPoints_onlyAuthorityHasMinting()
        {
            string assetsRoot = Application.dataPath;   // …/unity/Assets
            string[] whitelist =
            {
                Path.Combine(assetsRoot, "Sim.Contracts", "ItemInstanceId.cs"),   // 构造器定义
                Path.Combine(assetsRoot, "Sim", "ItemDatabase", "IdAuthority.cs"),// 唯一铸造实现
            };

            var violations = new List<string>();
            foreach (string file in Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains("/Tests/")) continue;

                string text = StripComments(File.ReadAllText(file));
                bool whitelisted = Array.IndexOf(whitelist, file) >= 0;

                if (!whitelisted && text.Contains("new ItemInstanceId("))
                    violations.Add($"{file}: 手工铸造 new ItemInstanceId( —— 铸造点必须经 IIdAuthority(D-21-26/27)");
                if (text.Contains("Guid.NewGuid"))
                    violations.Add($"{file}: Guid.NewGuid —— GUID 型 id 绕过计数器,无法高水位重构(AC-21a-63 换名词表 ②)");
                if (text.Contains("new Random("))
                    violations.Add($"{file}: new Random( —— 随机型 id 同上(词表 ③)");
                if (text.Contains("RandomNumberGenerator"))
                    violations.Add($"{file}: RandomNumberGenerator —— 换名铸造(词表 ④)");
            }

            Assert.That(violations, Is.Empty,
                "铸造点越出 IIdAuthority 白名单:\n" + string.Join("\n", violations) +
                "\n迁移后重号 = 物品悄悄合并/丢失的静默失败(D-21-27);词表见本文件头注。");
        }

        /// <summary>剥离 // 与 /* */ 注释(非字符串感知 —— 扫描词表不含合法出现在字符串里的 token)。</summary>
        private static string StripComments(string source)
        {
            string noBlock = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", string.Empty);
        }

        // ══════════════ 单测调用权威:单调 / 不复位 / 高水位 / 哨兵 ══════════════

        [Test]
        public void test_ac21a63_itemMinting_isMonotonicFromZero()
        {
            var authority = new IdAuthority();
            Assert.That(authority.NextItemInstanceId().Value, Is.EqualTo(0L), "首个物品 id = 0");
            Assert.That(authority.NextItemInstanceId().Value, Is.EqualTo(1L), "单调递增 ①");
            Assert.That(authority.NextItemInstanceId().Value, Is.EqualTo(2L), "单调递增 ②");
        }

        [Test]
        public void test_ac21a63_twoCounterSpaces_areIndependent()
        {
            var authority = new IdAuthority();
            authority.NextItemInstanceId();
            authority.NextItemInstanceId();
            authority.NextItemInstanceId();

            Assert.That(authority.NextPatientId().Value, Is.EqualTo(0),
                "物品计数器推进不得带动病人/敌人/玩家计数器(两支共用机制 A、独立空间)");
        }

        [Test]
        public void test_ac21a63_reconstruct_fromHighWater_mintsAboveSeen()
        {
            var authority = new IdAuthority();
            authority.ReconstructItemHighWater(new long[] { 0L, 4L, -1L, -5L });

            Assert.That(authority.NextItemInstanceId().Value, Is.EqualTo(5L),
                "next = max(id)+1 = 5;负哨兵(-1/-5)被忽略、不参与高水位(ADR-007 哨兵口径)");
        }

        [Test]
        public void test_ac21a63_reconstruct_negativeOnly_keepsNextAtZero()
        {
            var authority = new IdAuthority();
            authority.ReconstructItemHighWater(new long[] { -1L, -999L });

            Assert.That(authority.NextItemInstanceId().Value, Is.EqualTo(0L),
                "存档只含负哨兵 ⇒ 高水位不被拉偏;首个铸造仍是 0(负值不进物品 id 空间)");
        }

        [Test]
        public void test_ac21a63_reconstruct_neverResetsCounters()
        {
            var authority = new IdAuthority();
            for (int i = 0; i < 10; i++) authority.NextItemInstanceId();   // next = 10

            authority.ReconstructItemHighWater(new long[] { 2L });

            Assert.That(authority.NextItemInstanceId().Value, Is.EqualTo(10L),
                "机制 A 不变量①:计数器只升不降 —— 低水位存档不得把 next 拉回 3(复位即重号)");
        }

        [Test]
        public void test_ac21a63_reconstruct_thenMint_noReuse()
        {
            var authority = new IdAuthority();
            authority.ReconstructItemHighWater(new long[] { 0L, 1L, 2L });

            long a = authority.NextItemInstanceId().Value;
            long b = authority.NextItemInstanceId().Value;

            Assert.That(new[] { a, b }, Is.EqualTo(new[] { 3L, 4L }),
                "迁移重构后连续铸造无重号(AC-63 Then)");
        }

        [Test]
        public void test_ac21a63_reconstruct_idZero_setNextAboveZero()
        {
            var authority = new IdAuthority();
            authority.ReconstructItemHighWater(new long[] { 0L });

            Assert.That(authority.NextItemInstanceId().Value, Is.EqualTo(1L),
                "存档含 id=0 ⇒ next = max+1 = 1(AC-63 边例:id=0 不是哨兵、正常推进)");
        }

        [Test]
        public void test_ac21a63_reconstruct_highWaterMaxValue_overflowsExplicitly()
        {
            var authority = new IdAuthority();
            Assert.Throws<OverflowException>(
                () => authority.ReconstructItemHighWater(new long[] { long.MaxValue }),
                "高水位耗尽必须显式硬失败,不得静默回绕成 0(回绕 = 大规模重号)");
        }

        [Test]
        public void test_ac21a63_patientReconstruct_ignoresNoneSentinel()
        {
            var authority = new IdAuthority();
            authority.NextPatientId();   // 0
            authority.NextPatientId();   // 1
            authority.ReconstructPatientHighWater(new[] { -1, 0 });

            Assert.That(authority.NextPatientId().Value, Is.EqualTo(2),
                "病人高水位忽略 None(-1);max(已见 0)+1 = 1 < 已发 next=2 ⇒ 只升不降保持 2");
        }

        [Test]
        public void test_ac21a63_ctor_negativeSeed_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new IdAuthority(nextItem: -1L),
                "负初值不是合法待发号(负域属哨兵,不属号空间)");
            Assert.Throws<ArgumentOutOfRangeException>(() => new IdAuthority(nextPatient: -1),
                "同上(病人侧)");
        }
    }
}
