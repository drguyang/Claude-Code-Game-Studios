// 权威来源:
//   ADR-014 §五 —— 「`ConfigVersion` 由源数据集内容哈希派生(对 `assets/data/` 源文本集求哈希
//     后截断 u32)」⇒ **全局单一值**
//   ADR-010 §七 —— 存档头 `ConfigVersion` 与当前产物比对;**不匹配非致命**
//   `AC-51-D7`(`design/gdd/telemetry-analytics.md:621`,BLOCKING)
//   `TD-epic-01-f`(本件验其解除前置 ①「源头真值来源落地」的实现)
//
// ═══════════════════════════════════════════════════════════════════════════════
// 本文件验什么 / 不验什么
// ═══════════════════════════════════════════════════════════════════════════════
// **验**:`DataCoreConfigVersion.Combine` 的**组合口径**(排序无关 / 同输入同输出 /
//   空集与坏头抛 / 与 `ConfigVersionUtility` 同算法族)。
// **不验**:接线(那是 `data_core_config_version_wiring_test.cs` 的事)——
//   本文件**直接调静态方法**,对「装配根有没有把它接上」零判别力。
//   ⇒ 两件必须都跑;只有本件绿**不能**作为 `AC-51-D7` 已落地的证据(承 `TD-epic-01-f`
//     §五 第 3 条对「直接 new ConfigVersionCheck」夹具的批评,同一条纪律)。
//
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖 · 零文件系统(内存造头)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.DataPipeline
{
    [TestFixture]
    internal sealed class DataCoreConfigVersionTest
    {
        // ══════════════ 组合口径:排序无关 ══════════════

        /// <summary>**排序是判据的一部分** —— 同一组产物,字典枚举序不同 ⇒ 组合值**逐位相同**。
        /// <para><b>生产主语</b>:<see cref="DataCoreConfigVersion.Combine"/> 里的
        /// <c>addresses.Sort(StringComparer.Ordinal)</c>。</para>
        /// <para><b>判别力</b>:删掉那行 <c>Sort</c>(退回字典枚举序)⇒ 本测红。</para>
        /// <para><b>为什么这条重要</b>:`DataCorePreloader.DataCoreAddresses` 是**装载序**数组,
        /// 历史上追加过 10 次(7→8→…→22)。若按数组/枚举序组合,「接线顺序调整」会改全局值
        /// 而**数据一字未动** —— 那是假变更,会让每份旧存档突然「版本不匹配」。</para></summary>
        [Test]
        public void test_combine_insertionOrderIrrelevant_sameValue()
        {
            var forward = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["a.cooked.bytes"] = Header(1),
                ["b.cooked.bytes"] = Header(2),
                ["c.cooked.bytes"] = Header(3),
            };
            var backward = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["c.cooked.bytes"] = Header(3),
                ["b.cooked.bytes"] = Header(2),
                ["a.cooked.bytes"] = Header(1),
            };

            uint v1 = DataCoreConfigVersion.Combine(forward);
            uint v2 = DataCoreConfigVersion.Combine(backward);

            Assert.That(v2, Is.EqualTo(v1),
                "插入序不得影响组合值(排序是判据的一部分)");
        }

        /// <summary>**纯函数**:同输入同输出(连跑三次逐位相同)。
        /// <para><b>判别力</b>:把 FNV 初值改成读某个静态可变量 ⇒ 本测红。</para></summary>
        [Test]
        public void test_combine_sameInput_sameValue()
        {
            var headers = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["x.cooked.bytes"] = Header(0xDEADBEEF),
                ["y.cooked.bytes"] = Header(0x12345678),
            };

            uint a = DataCoreConfigVersion.Combine(headers);
            uint b = DataCoreConfigVersion.Combine(headers);
            uint c = DataCoreConfigVersion.Combine(headers);

            Assert.That(b, Is.EqualTo(a));
            Assert.That(c, Is.EqualTo(a));
        }

        // ══════════════ 定义域收窄:坏输入必须抛 ══════════════

        /// <summary>**空集 ⇒ 抛,不是返回 0**。
        /// <para><b>为何是判据而不是洁癖</b>:返回 0 会让「一个产物都没装载」与
        /// 「装载了但版本恰为 0」**不可分** —— 前者是装配缺陷(ADR-014 §五 E-13 面),
        /// 后者是合法值。混同 = 把装配缺陷伪装成正常。</para>
        /// <para><b>判别力</b>:把 <c>Count == 0</c> 的抛改成 <c>return 0u</c> ⇒ 本测红。</para></summary>
        [Test]
        public void test_combine_emptySet_throws()
        {
            var empty = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            var ex = Assert.Throws<ArgumentException>(
                () => DataCoreConfigVersion.Combine(empty),
                "空集必须抛(返回 0 会让「没装载」与「版本 0」不可分)");
            StringAssert.Contains("空", ex.Message);
        }

        /// <summary>null 字典 ⇒ <see cref="ArgumentNullException"/>。</summary>
        [Test]
        public void test_combine_nullDictionary_throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => DataCoreConfigVersion.Combine(null));
        }

        /// <summary>**短于 20 字节的头 ⇒ 抛**(不得静默按 0 补齐)。
        /// <para><b>判别力</b>:删掉 <c>header.Length &lt; HeaderSize</c> 检查 ⇒
        /// 后面的 <c>header[12..15]</c> 越界抛 <c>IndexOutOfRangeException</c>(类型不对)⇒ 本测红。</para></summary>
        [Test]
        public void test_combine_shortHeader_throws()
        {
            var headers = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["ok.cooked.bytes"] = Header(1),
                ["bad.cooked.bytes"] = new byte[DataCoreConfigVersion.HeaderSize - 1],
            };

            var ex = Assert.Throws<ArgumentException>(
                () => DataCoreConfigVersion.Combine(headers));
            StringAssert.Contains("bad.cooked.bytes", ex.Message,
                "错误信息须点名是哪一份产物 —— 22 份里挑一份,不点名等于没报");
        }

        /// <summary>**魔数不符 ⇒ 抛**(头部不是本格式 ⇒ 后面的 offset 12 读到的不是 configVersion)。
        /// <para><b>判别力</b>:删掉 <c>VerifyMagic</c> 调用 ⇒ 本测红。</para></summary>
        [Test]
        public void test_combine_wrongMagic_throws()
        {
            byte[] header = Header(1);
            header[0] = (byte)'X';   // "DYJC" → "XYJC"

            var headers = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["fake.cooked.bytes"] = header,
            };

            var ex = Assert.Throws<ArgumentException>(
                () => DataCoreConfigVersion.Combine(headers));
            StringAssert.Contains("魔数", ex.Message);
        }

        // ══════════════ 灵敏度:改一份产物的版本 ⇒ 全局值必变 ══════════════

        /// <summary>**任一产物的 ConfigVersion 变了 ⇒ 全局值必变**。
        /// <para>这是「组合值真的覆盖了全部 22 份」的最低可观测判据 —— 若只取某一份,
        /// 改其余份的版本号就**测不出来**,而 AC-51-D7 只盖住 1/22(仍是大半假绿)。</para>
        /// <para><b>判别力</b>:把 <c>Combine</c> 改成只喂第一份 ⇒ 改第二份版本的断言红。</para></summary>
        [Test]
        public void test_combine_anyProductVersionChanged_valueChanges()
        {
            var before = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["a.cooked.bytes"] = Header(1),
                ["b.cooked.bytes"] = Header(2),
                ["c.cooked.bytes"] = Header(3),
            };
            var after = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["a.cooked.bytes"] = Header(1),
                ["b.cooked.bytes"] = Header(2),
                ["c.cooked.bytes"] = Header(4),   // ← 只改 c
            };

            Assert.That(DataCoreConfigVersion.Combine(after),
                Is.Not.EqualTo(DataCoreConfigVersion.Combine(before)),
                "改动任一份产物的 ConfigVersion ⇒ 全局组合值必须随之变化");
        }

        /// <summary>**address 参与哈希**:同版本号但 address 换了 ⇒ 值必变。
        /// <para>若只喂版本号不喂 address,「把 A 产物的版本挪给 B 产物」会得同值 ——
        /// 那是两份不同数据的碰撞。</para>
        /// <para><b>判别力</b>:删掉 <c>hash = Fnv1a(hash, address)</c> 与 0x00 分隔 ⇒ 本测红。</para></summary>
        [Test]
        public void test_combine_addressParticipates_valueDiffers()
        {
            var one = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["a.cooked.bytes"] = Header(7),
            };
            var other = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["z.cooked.bytes"] = Header(7),
            };

            Assert.That(DataCoreConfigVersion.Combine(other),
                Is.Not.EqualTo(DataCoreConfigVersion.Combine(one)),
                "同版本号不同 address ⇒ 值须不同(address 是哈希输入的一部分)");
        }

        // ══════════════ 常量对账:与 CookedFormat 逐值相等 ══════════════

        /// <summary>本类复刻的 <c>HeaderSize</c> / <c>ConfigVersionOffset</c> 与
        /// <see cref="CookedFormat"/> **逐值相等**。
        /// <para><b>为何要有这条</b>:<see cref="DataCoreConfigVersion"/> 刻意**不引用**
        /// <c>CookedFormat</c> 的常量(见其 doc:复刻只为让文件自足可读)。复刻 = 第二份定义
        /// ⇒ 必须有机械对账,否则改一处不改另一处是**静默分叉**,而分叉后读到的
        /// configVersion 会是**别的字段**(schemaVersion / payloadLength)—— 症状是版本号
        /// 看起来「总在变」,极难归因。</para>
        /// <para><b>判别力</b>:把 <c>ConfigVersionOffset</c> 改成 8 ⇒ 本测红。</para></summary>
        [Test]
        public void test_constants_matchCookedFormat()
        {
            Assert.That(DataCoreConfigVersion.HeaderSize, Is.EqualTo(CookedFormat.HeaderSize),
                "HeaderSize 复刻须与 CookedFormat 同值");
            Assert.That(DataCoreConfigVersion.ConfigVersionOffset, Is.EqualTo(12),
                "configVersion 在头部的 offset = 12(CookedFormat 头部布局逐字)");
        }

        /// <summary><see cref="DataCoreConfigVersion.ReadProductConfigVersion"/> 读出的
        /// 就是 offset 12 的 u32 LE,且与 <c>Combine</c> 用的是同一字段。
        /// <para><b>判别力</b>:把 <c>ReadProductConfigVersion</c> 的字节序改成 BE ⇒ 本测红。</para></summary>
        [Test]
        public void test_readProductConfigVersion_littleEndianOffset12()
        {
            byte[] header = Header(0x0A0B0C0D);

            Assert.That(DataCoreConfigVersion.ReadProductConfigVersion(header),
                Is.EqualTo(0x0A0B0C0Du),
                "须按 little-endian 读 offset 12 的 u32");
        }

        // ══════════════ 工具 ══════════════

        /// <summary>造一份合法的 20 字节产物头(魔数 + 四个 u32,只有 configVersion 是变参)。</summary>
        private static byte[] Header(uint configVersion)
        {
            var header = new byte[CookedFormat.HeaderSize];
            header[0] = (byte)'D';
            header[1] = (byte)'Y';
            header[2] = (byte)'J';
            header[3] = (byte)'C';
            WriteU32Le(header, 4, CookedFormat.FormatVersion);
            WriteU32Le(header, 8, CookedFormat.SchemaVersion);
            WriteU32Le(header, DataCoreConfigVersion.ConfigVersionOffset, configVersion);
            WriteU32Le(header, 16, 0u);   // payloadLength —— 本测不关心
            return header;
        }

        private static void WriteU32Le(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
