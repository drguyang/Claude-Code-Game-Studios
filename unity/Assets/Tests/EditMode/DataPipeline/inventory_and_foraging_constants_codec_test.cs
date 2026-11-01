// S22-9(2026-11-01)· 两份**新增运行期解码器**的直接夹具。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件、为什么必须读真产物而不内联
// ─────────────────────────────────────────────────────────────────────────────
// 读:
//   · `unity/Assets/DataCooked/inventory_constants.cooked.bytes`
//   · `unity/Assets/DataCooked/foraging_constants.cooked.bytes`
// 理由:这两个 codec 的**全部价值**就是「与写方逐字节镜像」。若把载荷内联成字面量,
//   夹具测的是我抄的那份字节,**写方改了布局它仍绿** —— 恰好放过它唯一要防的缺陷
//   (`CookedReadUtil.AssertFullyConsumed` 的自陈:「读方与写方布局不再镜像」)。
//   ⇒ 正例一律读**盘上真产物**;负例在真产物上**定点变异**(见下),不另造一份。
//
// 权威来源:ADR-014 §二/§五 · `CookedFormat`(20 B 头)· E-13(失败 = 启动期硬失败)
//
// ⚠️ 本文件**不**主张「采前闸已接通」—— 那要等驱动(④)落地。这里只覆盖
//   「产物 → 领域类型」这一跳的两条新读路及其四条硬拒收。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Foraging;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.DataPipeline
{
    /// <summary>
    /// <b>S22-9 两条新读路的夹具</b>：`inventory_constants`(CARRY_CAP)与 `foraging_constants`(17 调参表)。
    /// <para>每条负例都是<b>在真产物字节上定点变异</b>,而不是另造一份合成产物 ——
    /// 这样「写方真布局」始终是被测输入的一部分。</para>
    /// </summary>
    [TestFixture]
    public class InventoryAndForagingConstantsCodecTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        private static byte[] RealBytes(string fileName)
        {
            string path = Path.Combine(CookedDir, fileName);
            Assert.That(File.Exists(path), Is.True,
                $"盘上缺 {fileName} —— 重烘:大医精诚/数据管线/烘焙 {fileName.Replace(".cooked.bytes", "")}");
            return File.ReadAllBytes(path);
        }

        private static byte[] InventoryBytes() => RealBytes("inventory_constants.cooked.bytes");

        private static byte[] ForagingBytes() => RealBytes("foraging_constants.cooked.bytes");

        /// <summary>把 i32 写进载荷的指定偏移(**载荷相对**,不含 20 B 头)。</summary>
        private static void PokeI32(byte[] b, int payloadOffset, int value)
        {
            int off = CookedFormat.HeaderSize + payloadOffset;
            b[off] = (byte)value;
            b[off + 1] = (byte)(value >> 8);
            b[off + 2] = (byte)(value >> 16);
            b[off + 3] = (byte)(value >> 24);
        }

        /// <summary>改头部声明的载荷长度(offset 16,u32 LE)。</summary>
        private static void PokePayloadLength(byte[] b, uint length)
        {
            b[16] = (byte)length;
            b[17] = (byte)(length >> 8);
            b[18] = (byte)(length >> 16);
            b[19] = (byte)(length >> 24);
        }

        // ══════════════════ 20 · CARRY_CAP ══════════════════

        /// <summary>
        /// 真产物读出的 `CARRY_CAP` = **60**(`assets/data/inventory_constants.json` 的当前值)。
        /// <para><b>为什么写死 60 而不从 JSON 反读</b>:这是一条**哨兵** —— 数值轮改容量时
        /// 应当有人**看见**「运行期读到的容量变了」。从 JSON 反读会让两边一起动 ⇒ 零判别力
        /// (自指判据,假绿 F3)。改容量 ⇒ 本测红 ⇒ 同步改这一行的字面量,是一次**有意识的裁定**。</para>
        /// </summary>
        [Test]
        public void test_carryCap_realArtifact_isSixty()
        {
            Assert.That(InventoryConstantsCookedCodec.ReadCarryCap(InventoryBytes()), Is.EqualTo(60),
                "CARRY_CAP 变了 —— 若是有意改容量,同步更新本字面量;否则是产物被改坏了");
        }

        /// <summary>非正容量在**装载期**硬拒收(AC-20-13:禁 null 兜底 / 禁静默默认值)。</summary>
        /// <remarks><b>判别力</b>:删掉 `ReadCarryCap` 里的 `carryCap &lt;= 0` 分支 ⇒ 本测红。</remarks>
        [Test]
        public void test_carryCap_zero_isRejectedAtLoad()
        {
            byte[] bytes = InventoryBytes();
            PokeI32(bytes, 0, 0);   // 载荷首字段 = carry_cap

            var ex = Assert.Throws<InvalidDataException>(
                () => InventoryConstantsCookedCodec.ReadCarryCap(bytes));

            Assert.That(ex.Message, Does.Contain("carry_cap"),
                "报文须点名是哪个字段 —— 否则启动期日志只知道「某处坏了」");
        }

        /// <summary>
        /// 载荷尾多出 4 B(且头部同步声明)⇒ 读方与写方布局**不再镜像** ⇒ 硬拒收。
        /// <para><b>为什么必须同步改头部</b>:只追加字节而头部不动,会先在
        /// 「载荷长度不符」那一条就抛,`AssertFullyConsumed` **永远走不到** ——
        /// 那样本测是绿在**另一条**判据上,对目标守卫零判别力(靶测绕过被变异函数)。</para>
        /// </summary>
        /// <remarks><b>判别力</b>:删掉 `AssertFullyConsumed` 调用 ⇒ 本测红。</remarks>
        [Test]
        public void test_carryCap_trailingBytes_isRejected()
        {
            byte[] real = InventoryBytes();
            int realPayload = real.Length - CookedFormat.HeaderSize;

            var grown = new List<byte>(real);
            grown.AddRange(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });
            byte[] bytes = grown.ToArray();
            PokePayloadLength(bytes, (uint)(realPayload + 4));

            var ex = Assert.Throws<InvalidDataException>(
                () => InventoryConstantsCookedCodec.ReadCarryCap(bytes));

            Assert.That(ex.Message, Does.Contain("仍剩"),
                "期望命中 `AssertFullyConsumed`(报文含「仍剩」),而不是头部长度校验 —— " +
                "若这里变了,说明靶测绕到了别的判据上");
        }

        // ══════════════════ 17 · ForagingConstants ══════════════════

        /// <summary>真产物能构造出领域类型,且 `maxQuality` 由**调用方注入**(产物里没有它)。</summary>
        [Test]
        public void test_foragingConstants_realArtifact_constructsWithInjectedMaxQuality()
        {
            ForagingConstants c = ForagingConstantsCookedCodec.Read(ForagingBytes(), 5);

            Assert.That(c.CapTable.Count, Is.EqualTo(ForagingConstants.CapTableLength),
                "CapTable 长度须 = SKILL_CAP+1(读方已按此校验,这里再钉一次是防校验被删)");
            Assert.That(c.SeasonMult.Count, Is.GreaterThanOrEqualTo(1), "SEASON_MULT 至少一季");
            Assert.That(c.MaxQuality, Is.EqualTo(5),
                "MAX_QUALITY 是调用方注入的,产物里不该有它 —— 若这里不是 5,说明注入被绕过");
        }

        /// <summary>
        /// `CapTable` 长度与 `SKILL_CAP+1` 不符 ⇒ 写读契约漂移 ⇒ 硬拒收。
        /// </summary>
        /// <remarks><b>判别力</b>:删掉 `capCount != ForagingConstants.CapTableLength` 分支 ⇒ 本测红。</remarks>
        [Test]
        public void test_foragingConstants_capTableLengthDrift_isRejected()
        {
            byte[] bytes = ForagingBytes();
            // 载荷布局:seasonsPerYear(i32) → capCount(i32) @ payload offset 4
            PokeI32(bytes, 4, ForagingConstants.CapTableLength - 1);

            var ex = Assert.Throws<InvalidDataException>(
                () => ForagingConstantsCookedCodec.Read(bytes, 5));

            Assert.That(ex.Message, Does.Contain("CapTable"),
                "报文须点名 CapTable —— 否则启动期日志指不出是哪一段漂移");
        }

        /// <summary>
        /// `maxQuality &lt; 1` ⇒ 原样透出 `ForagingConstants` 构造期的拒收(**本类不重写判据**)。
        /// <para><b>为什么这条重要</b>:`maxQuality` 是注入量,注入错了必须**立刻炸**,
        /// 而不是让 `OutQuality` 拿到一个没有分布的品级域。</para>
        /// </summary>
        [Test]
        public void test_foragingConstants_maxQualityBelowOne_isRejectedByConstructor()
        {
            Assert.Throws(Is.InstanceOf<ArgumentException>(),
                () => ForagingConstantsCookedCodec.Read(ForagingBytes(), 0));
        }
    }
}
