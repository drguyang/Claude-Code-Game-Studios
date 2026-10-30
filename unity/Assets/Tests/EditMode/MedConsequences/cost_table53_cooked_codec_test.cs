// ADR-035 T2 前置 —— `CostTable53CookedCodec`(53 用药代价表的运行期读方)夹具。
//
// 权威:ADR-014 §二/§五(运行期读烘焙产物)· CookedFormat.cs(布局契约)
//       · CostTable53.CreateTable(纵深防御的**单一出处** —— 本件只证「解码把那套判据接上了」)
//
// ⚠️ 本件证的是**读方**,不是「机制真跑」:
//   表今日是空骨架(`rows: []`)⇒ 往返比对的是「空表的解码形态」,不是「有行的表能查」。
//   真实行的缺口归 `TD-sim-03` / 数值轮,不在本件面内。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.MedConsequences
{
    /// <summary>53 代价表 cooked 解码器 —— 正向往返 + 六类损坏拒收。</summary>
    [TestFixture]
    public sealed class CostTable53CookedCodecTest
    {
        private static string RepoRoot
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static byte[] ReadCommitted(string fileName)
            => File.ReadAllBytes(Path.Combine(Application.dataPath, "DataCooked", fileName));

        /// <summary>9 的注册表行(与生产读方同一来源:注册表绑定器)。</summary>
        private static IReadOnlyList<DiseaseRegistryEntry> RegistryRows()
            => DiseaseRegistryBinder.Bind(
                File.ReadAllText(Path.Combine(RepoRoot, "assets", "data", "disease_registry.json"))).Rows;

        /// <summary>拼一份 cooked(20 字节头 + 载荷)。</summary>
        private static byte[] BuildCooked(byte[] payload, uint schemaVersion = 1, uint configVersion = 0xAABBCCDD)
        {
            var output = new byte[20 + payload.Length];
            Encoding.ASCII.GetBytes("DYJC").CopyTo(output, 0);
            WriteU32(output, 4, 1);                 // formatVersion
            WriteU32(output, 8, schemaVersion);
            WriteU32(output, 12, configVersion);
            WriteU32(output, 16, (uint)payload.Length);
            Buffer.BlockCopy(payload, 0, output, 20, payload.Length);
            return output;
        }

        private static void WriteU32(byte[] b, int off, uint v)
        {
            b[off] = (byte)(v & 0xFF);
            b[off + 1] = (byte)((v >> 8) & 0xFF);
            b[off + 2] = (byte)((v >> 16) & 0xFF);
            b[off + 3] = (byte)((v >> 24) & 0xFF);
        }

        /// <summary>常规形态:行集 + **空的**过量档位线(过量支未启用)。</summary>
        private static byte[] PayloadWithRows(params (int HarmKind, int Band, int InjuryId, int Sev)[] rows)
            => PayloadWithRowsAnd(rows, System.Array.Empty<long>());

        /// <param name="odThresholds">尾段的过量档位线(`Fix` raw,升序)。空 = 过量支未启用。
        /// 2026-10-30 起布局多了这一段 —— **旧布局(无尾段)会被读越界拒收**,这是预期行为。</param>
        private static byte[] PayloadWithRowsAnd(
            (int HarmKind, int Band, int InjuryId, int Sev)[] rows, long[] odThresholds)
        {
            var bytes = new List<byte>(4 + rows.Length * 16 + 4 + odThresholds.Length * 8);
            void W(int v)
            {
                bytes.Add((byte)(v & 0xFF)); bytes.Add((byte)((v >> 8) & 0xFF));
                bytes.Add((byte)((v >> 16) & 0xFF)); bytes.Add((byte)((v >> 24) & 0xFF));
            }
            void W64(long v)
            {
                W((int)(v & 0xFFFFFFFF)); W((int)((v >> 32) & 0xFFFFFFFF));
            }
            W(rows.Length);
            foreach (var r in rows) { W(r.HarmKind); W(r.Band); W(r.InjuryId); W(r.Sev); }

            // 尾段:过量档位线
            W(odThresholds.Length);
            foreach (long t in odThresholds) W64(t);
            return bytes.ToArray();
        }

        /// <summary>无行、无过量档位线的空表载荷(最常见的惰性形态)。</summary>
        private static byte[] EmptyPayload() => PayloadWithRows(System.Array.Empty<(int, int, int, int)>());

        private static InvalidDataException AssertRejected(
            byte[] cooked, string because, IReadOnlyList<DiseaseRegistryEntry> registry = null)
            => Assert.Throws<InvalidDataException>(
                () => CostTable53CookedCodec.Read(cooked, registry ?? RegistryRows()), because);

        /// <summary>
        /// 造一件**非致死自限型**伤情(唯一被 R1-22 允许的非致死形态),用于「合法行」类夹具。
        /// <para>⚠️ 手法与 `cost_table_binder_test` 同源:对真实注册表做**单点改写**
        /// (`lethal: true → false` ∧ `self_limit: false → true`),而不是手搓假注册表 ——
        /// 手搓会绕过 `DiseaseRegistryBinder` 的全部既有校验。</para>
        /// <para>⚠️ 这条夹具存在本身就在说明缺口:真实注册表**没有**任何合法非致死伤情
        /// (`TD-sim-03`)。</para>
        /// </summary>
        private static IReadOnlyList<DiseaseRegistryEntry> RegistryWithOneNonLethalInjury(
            out int nonLethalId)
        {
            string json = File.ReadAllText(Path.Combine(RepoRoot, "assets", "data", "disease_registry.json"));
            int atKey = json.IndexOf("\"key\": \"INJ_HEMORRHAGE\"", StringComparison.Ordinal);
            Assert.That(atKey, Is.GreaterThan(0), "夹具前提不成立:注册表里没有 INJ_HEMORRHAGE");

            ReplaceFirstAfter(ref json, atKey, "\"lethal\": true", "\"lethal\": false");
            ReplaceFirstAfter(ref json, atKey, "\"self_limit\": false", "\"self_limit\": true");

            IReadOnlyList<DiseaseRegistryEntry> rows = DiseaseRegistryBinder.Bind(json).Rows;
            nonLethalId = -1;
            foreach (DiseaseRegistryEntry e in rows)
                if (e != null && e.DiseaseKey == "INJ_HEMORRHAGE") { nonLethalId = e.DiseaseId; break; }
            Assert.That(nonLethalId, Is.GreaterThan(0), "夹具前提不成立:改写后找不到该行");
            return rows;
        }

        private static void ReplaceFirstAfter(ref string json, int after, string needle, string replacement)
        {
            int at = json.IndexOf(needle, after, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), $"夹具前提不成立:该行没有 `{needle}`");
            json = json.Substring(0, at) + replacement + json.Substring(at + needle.Length);
        }

        // ══════════════════════ 一、正向 ══════════════════════

        /// <summary>盘上产物与现烘产物解出同一形态(空表 + 同 ConfigVersion)。</summary>
        [Test]
        public void test_diskArtifact_matchesFreshBake()
        {
            IReadOnlyList<DiseaseRegistryEntry> registry = RegistryRows();
            CostTable53DataSet disk = CostTable53CookedCodec.Read(
                ReadCommitted(DataBakeMenu.CostTableCookedAssetName), registry);
            CostTable53DataSet fresh = CostTable53CookedCodec.Read(
                CostTable53Bake.BakeFromRepo(RepoRoot).Cooked, registry);

            Assert.That(disk.ConfigVersion, Is.EqualTo(fresh.ConfigVersion), "ConfigVersion 解码不等");
            Assert.That(disk.Table.RowCount, Is.EqualTo(fresh.Table.RowCount), "行数解码不等");
        }

        /// <summary>**空表合法** ⇒ `IsEmpty` 为真、查询 miss 不抛(惰性形态,S2)。</summary>
        [Test]
        public void test_emptyTable_decodesToLazyEmpty_notAnError()
        {
            CostTable53DataSet ds = CostTable53CookedCodec.Read(
                BuildCooked(PayloadWithRows()), RegistryRows());

            Assert.That(ds.Table.IsEmpty, Is.True);
            Assert.That(ds.Table.RowCount, Is.Zero);
            Assert.That(ds.Table.TryLookup(0, 0, out _, out _), Is.False, "空表查询须 miss,不得抛");
            Assert.That(ds.ConfigVersion, Is.EqualTo(0xAABBCCDDu), "ConfigVersion 须透传头槽值");
        }

        /// <summary>地址常量与菜单写盘名**同值**(单一出处的机械面)。</summary>
        [Test]
        public void test_addressConstant_matchesMenuAssetName()
        {
            Assert.That(CostTable53CookedCodec.CookedFileName,
                Is.EqualTo(DataBakeMenu.CostTableCookedAssetName));
            Assert.That(AddressablesDataProvider.CostTableAddress,
                Is.EqualTo(DataBakeMenu.CostTableCookedAssetName));
        }

        // ══════════════════════ 二、损坏拒收(六类)══════════════════════

        [Test]
        public void test_badMagic_isRejected()
        {
            byte[] cooked = BuildCooked(PayloadWithRows());
            cooked[0] = (byte)'X';
            AssertRejected(cooked, "魔数不符须拒收");
        }

        [Test]
        public void test_truncatedPayload_isRejected()
        {
            byte[] cooked = BuildCooked(PayloadWithRows());
            Array.Resize(ref cooked, cooked.Length - 4);
            AssertRejected(cooked, "载荷长度与头不符须拒收");
        }

        [Test]
        public void test_negativeRowCount_isRejected()
        {
            byte[] payload = PayloadWithRows();
            payload[0] = 0xFF; payload[1] = 0xFF; payload[2] = 0xFF; payload[3] = 0xFF; // -1
            AssertRejected(BuildCooked(payload), "负行数须拒收(与 DelayTable 的「0 即拒」不同 —— 本表 0 合法)");
        }

        /// <summary>L3:外键指向注册表里不存在的 id ⇒ 解码期就拒(不是运行时静默)。</summary>
        [Test]
        public void test_unknownInjuryId_isRejectedByL3()
        {
            AssertRejected(BuildCooked(PayloadWithRows((0, 0, 9999, 0))),
                "未知 injury_id 须由 CreateTable 的外键判据拒收,并被包成启动期硬失败");
        }

        /// <summary>L3:N-1 —— 指向 `lethal` 伤情 ⇒ 解码期就拒。</summary>
        [Test]
        public void test_lethalInjury_isRejectedByL3()
        {
            // INJ_STAB 在真实注册表里 lethal=true(不抄 id,从注册表取)
            int stabId = -1;
            foreach (DiseaseRegistryEntry e in RegistryRows())
                if (e != null && e.DiseaseKey == "INJ_STAB") { stabId = e.DiseaseId; break; }
            Assert.That(stabId, Is.GreaterThan(0), "夹具前提不成立:注册表里没有 INJ_STAB");

            AssertRejected(BuildCooked(PayloadWithRows((0, 0, stabId, 0))),
                "致死伤情须被 N-1 拒收(ADR-035 §Decision ③ 的 L3)");
        }

        /// <summary>L3:单射 —— 同一 `(harm_kind, band)` 两行 ⇒ 拒(**且报文点名「单射」**)。</summary>
        /// <remarks>
        /// ⚠️ **必须用合法行来测**:拿一件致死伤情喂两行,会先被 N-1 拒 ⇒ 夹具**绿得没有判别力**。
        /// 故本测注入一件非致死自限型伤情(`RegistryWithOneNonLethalInjury`),
        /// 并**断言报文含「单射」** —— 只断「会抛」会被 N-1 / `Dictionary.Add` 顶替。
        /// </remarks>
        [Test]
        public void test_duplicateKindBand_isRejectedByL3()
        {
            IReadOnlyList<DiseaseRegistryEntry> registry =
                RegistryWithOneNonLethalInjury(out int injuryId);

            InvalidDataException ex = AssertRejected(
                BuildCooked(PayloadWithRows((0, 0, injuryId, 0), (0, 0, injuryId, 0))),
                "重复 (harm_kind, band) 须拒收", registry);
            Assert.That(ex.Message, Does.Contain("单射"), "报文须点名单射;实得:" + ex.Message);
        }

        /// <summary>反例:同一伤情出现在**两个不同档** ⇒ 合法(单射判据不得误伤)。</summary>
        [Test]
        public void test_sameInjuryOnDifferentBands_isLegal()
        {
            IReadOnlyList<DiseaseRegistryEntry> registry =
                RegistryWithOneNonLethalInjury(out int injuryId);

            CostTable53DataSet ds = CostTable53CookedCodec.Read(
                BuildCooked(PayloadWithRows((0, 0, injuryId, 0), (0, 1, injuryId, 2))), registry);

            Assert.That(ds.Table.RowCount, Is.EqualTo(2));
            Assert.That(ds.Table.TryLookup(0, 0, out int id0, out int sev0), Is.True);
            Assert.That(ds.Table.TryLookup(0, 1, out int id1, out int sev1), Is.True);
            Assert.That(new[] { id0, id1 }, Is.EqualTo(new[] { injuryId, injuryId }));
            Assert.That(new[] { sev0, sev1 }, Is.EqualTo(new[] { 0, 2 }), "severity_band 须逐行保真");
        }

        /// <summary>构造被拒时,**报文须保留 Sim 侧的判据文字**(便于定位是 N-1 还是单射)。</summary>
        [Test]
        public void test_rejectionMessage_preservesSimSideText()
        {
            InvalidDataException ex = AssertRejected(
                BuildCooked(PayloadWithRows((0, 0, 9999, 0))), "未知 id 须拒");
            Assert.That(ex.Message, Does.Contain("53_cost_table"), "报文须点名产物");
            Assert.That(ex.Message, Does.Contain("外键失效").Or.Contain("不在疾病注册表内"),
                "报文须保留 Sim 侧判据文字;实得:" + ex.Message);
        }

        /// <summary>`registry` 为 null ⇒ `ArgumentNullException`(外键判据无处可依,不得静默放行)。</summary>
        [Test]
        public void test_nullRegistry_isRejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => CostTable53CookedCodec.Read(BuildCooked(PayloadWithRows()), null));
        }
    }
}
