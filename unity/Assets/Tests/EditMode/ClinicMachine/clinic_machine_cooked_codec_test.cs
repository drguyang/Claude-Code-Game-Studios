// 权威来源:ADR-014 §二(运行期读烘焙产物,零 JSON 解析)· E-13(解码失败 = 启动期硬失败)
//          写方:`Editor.Tools.Bake/ClinicMachineCookedWriter.cs`
//          读方:`Gameplay.Presentation/ClinicMachineCookedCodec.cs`
//          表构造:`Sim/Clinic/ClinicMachineTables.cs`
//
// ⚠️ **本件补的是「读方 ↔ 写方镜像」门**(承 `DataPipeline/cooked_codec_roundtrip_test.cs` 的设计):
//    `cooked_drift_all_artifacts_test` 只证明「盘上字节 == 现烘字节」,**不证明有人读得懂**它。
//    24 三表在 2026-10-21 之前**零读方**(只有 EditMode 直读源 JSON)⇒ 布局错位完全不可见。
//
// ⚠️ 与 `clinicmachine_tables_schema_test`(烘焙侧)分工:
//    那边测「作者态 JSON → 拒收」;这边测「产物字节 → 运行期表」。
//
// ⚠️ 测试不写盘(承 `.claude/rules/test-standards.md`:Unit tests must not depend on
//    external state)。本件只**读**已入库产物 + 内存内现烘。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Clinic;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.ClinicMachine
{
    /// <summary>
    /// <b>24 医馆三表 cooked 读方门</b> —— 盘上字节 / 现烘字节 / 运行期表 / provider 装载面,
    /// 四者必须说同一种布局。
    /// </summary>
    [TestFixture]
    public class ClinicMachineCookedCodecTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir
            => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        private static byte[] ReadCommitted(string fileName)
        {
            string path = Path.Combine(CookedDir, fileName);
            Assert.That(File.Exists(path), Is.True,
                $"盘上产物缺失:{path} —— 重烘:菜单「大医精诚/数据管线/烘焙 clinic-machine 三表(24 医馆)」");
            return File.ReadAllBytes(path);
        }

        private static ClinicMachineTablesBake.BakeOutput FreshBake()
            => ClinicMachineTablesBake.BakeFromRepo(RepoRoot);

        // ══════════════════════ ① 盘上 ↔ 现烘(CONTEXT)══════════════════════

        /// <summary>CONTEXT_TABLE:盘上字节与现烘字节解码后逐行相等。</summary>
        /// <remarks><b>判别力</b>:读方字段序错位(如把 context_id 与 k_speed 次序弄反)⇒
        /// 字符串长度校验先炸,或行字段不等。头导出值 K_CONTEXT_MAX 单独对齐。</remarks>
        [Test]
        public void test_clinic_cookedContext_diskMatchesFreshBake()
        {
            ClinicContextDataSet disk = ClinicMachineCookedCodec.ReadContext(
                ReadCommitted(DataBakeMenu.ClinicContextCookedAssetName));
            ClinicContextDataSet fresh = ClinicMachineCookedCodec.ReadContext(FreshBake().ContextCooked);

            Assert.That(disk.ConfigVersion, Is.EqualTo(fresh.ConfigVersion), "ConfigVersion 不一致");
            Assert.That(disk.Table.Count, Is.EqualTo(fresh.Table.Count), "行数不一致");
            Assert.That(disk.Table.KContextMax.Raw, Is.EqualTo(fresh.Table.KContextMax.Raw),
                "K_CONTEXT_MAX(F-24-3 导出值)不一致");

            for (int i = 0; i < fresh.Table.Count; i++)
            {
                ClinicContextRow expect = fresh.Table.Rows[i];
                Assert.That(disk.Table.TryGetRow(expect.RoomType, out ClinicContextRow actual), Is.True,
                    $"盘上表缺 room_type「{expect.RoomType}」");
                Assert.That(actual.ContextId, Is.EqualTo(expect.ContextId));
                Assert.That(actual.KSpeed.Raw, Is.EqualTo(expect.KSpeed.Raw));
                Assert.That(actual.BaseEnv.Raw, Is.EqualTo(expect.BaseEnv.Raw));
            }
        }

        // ══════════════════════ ② 盘上 ↔ 现烘(ADJ)══════════════════════

        /// <summary>ADJ_TABLE:盘上字节与现烘字节解码后域端点 / N_max / 每条权重相等。</summary>
        /// <remarks><b>判别力</b>:读方把 `w_min` / `w_max` / `n_max` 三个 i64 的次序读错 ⇒ 端点不等。</remarks>
        [Test]
        public void test_clinic_cookedAdj_diskMatchesFreshBake()
        {
            ClinicAdjDataSet disk = ClinicMachineCookedCodec.ReadAdj(
                ReadCommitted(DataBakeMenu.ClinicAdjCookedAssetName));
            ClinicAdjDataSet fresh = ClinicMachineCookedCodec.ReadAdj(FreshBake().AdjCooked);

            Assert.That(disk.ConfigVersion, Is.EqualTo(fresh.ConfigVersion));
            Assert.That(disk.Table.WMin.Raw, Is.EqualTo(fresh.Table.WMin.Raw));
            Assert.That(disk.Table.WMax.Raw, Is.EqualTo(fresh.Table.WMax.Raw));
            Assert.That(disk.Table.NMax, Is.EqualTo(fresh.Table.NMax));
            Assert.That(disk.Table.DirectedCount, Is.EqualTo(fresh.Table.DirectedCount));
            Assert.That(disk.Table.UnorderedPairCount, Is.EqualTo(fresh.Table.UnorderedPairCount));

            // 对称性(AC-24-06)在解码期已校验;此处再验「对调参序同值」这一可观测后果。
            // 取种子里的「床 ↔ 药炉」对(w = −1/4,`assets/data/clinicmachine_adj.json`)。
            Assert.That(disk.Table.TryGetWeight("bed", "chamber_pot", out Fix ab), Is.True, "床-药炉对缺失");
            Assert.That(disk.Table.TryGetWeight("chamber_pot", "bed", out Fix ba), Is.True,
                "药炉-床对缺失(无序查询失败)");
            Assert.That(ab.Raw, Is.EqualTo(ba.Raw), "无序查询两侧不等");
            Assert.That(ab.Raw, Is.EqualTo(-Fix.OneRaw / 4), "床-药炉权重应为 −1/4");
        }

        // ══════════════════════ ③ 盘上 ↔ 现烘(ROOM)══════════════════════

        /// <summary>ROOM_TABLE:盘上字节与现烘字节解码后房间类型 / 子句逐字段相等。</summary>
        /// <remarks><b>判别力</b>:读方把子句的 `min` 与 `neighbor_type` 次序读错 ⇒
        /// min 变成字符串长度前缀的字节数,解析即炸。</remarks>
        [Test]
        public void test_clinic_cookedRoom_diskMatchesFreshBake()
        {
            ClinicRoomDataSet disk = ClinicMachineCookedCodec.ReadRoom(
                ReadCommitted(DataBakeMenu.ClinicRoomCookedAssetName));
            ClinicRoomDataSet fresh = ClinicMachineCookedCodec.ReadRoom(FreshBake().RoomCooked);

            Assert.That(disk.ConfigVersion, Is.EqualTo(fresh.ConfigVersion));
            Assert.That(disk.Table.Count, Is.EqualTo(fresh.Table.Count));

            for (int i = 0; i < fresh.Table.Count; i++)
            {
                ClinicRoomDef expect = fresh.Table.Rooms[i];
                Assert.That(disk.Table.TryGetRoom(expect.RoomType, out ClinicRoomDef actual), Is.True,
                    $"盘上表缺 room_type「{expect.RoomType}」");
                Assert.That(actual.Clauses.Count, Is.EqualTo(expect.Clauses.Count));

                for (int c = 0; c < expect.Clauses.Count; c++)
                {
                    Assert.That(actual.Clauses[c].Kind, Is.EqualTo(expect.Clauses[c].Kind),
                        $"「{expect.RoomType}」clauses[{c}].kind 不等");
                    Assert.That(actual.Clauses[c].Type, Is.EqualTo(expect.Clauses[c].Type));
                    Assert.That(actual.Clauses[c].NeighborType, Is.EqualTo(expect.Clauses[c].NeighborType));
                    Assert.That(actual.Clauses[c].Direction, Is.EqualTo(expect.Clauses[c].Direction));
                    Assert.That(actual.Clauses[c].Min, Is.EqualTo(expect.Clauses[c].Min));
                }
            }
        }

        // ══════════════════════ ④ provider 三支路装载 ══════════════════════

        /// <summary>provider 的三个 clinic 分支真能装载真产物(经注入字节源,不碰 Addressables)。</summary>
        /// <remarks><b>判别力</b>:删掉任一 `typeof(Clinic*DataSet)` 分支 ⇒ 落「未知数据集类型」兜底 ⇒ 红。
        /// 同时钉住「地址 → 产物名」映射:字节源按 address 取名,取不到即抛。</remarks>
        [Test]
        public void test_clinic_provider_threeBranchesLoadRealArtifacts()
        {
            var bytesByAddress = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [AddressablesDataProvider.ClinicContextAddress] =
                    ReadCommitted(DataBakeMenu.ClinicContextCookedAssetName),
                [AddressablesDataProvider.ClinicAdjAddress] =
                    ReadCommitted(DataBakeMenu.ClinicAdjCookedAssetName),
                [AddressablesDataProvider.ClinicRoomAddress] =
                    ReadCommitted(DataBakeMenu.ClinicRoomCookedAssetName),
            };

            AddressablesDataProvider provider = MakeProvider(address =>
                bytesByAddress.TryGetValue(address, out byte[] b)
                    ? b
                    : throw new InvalidOperationException($"未提供的 address:{address}"));

            ClinicContextDataSet ctx = provider.Load<ClinicContextDataSet>();
            ClinicAdjDataSet adj = provider.Load<ClinicAdjDataSet>();
            ClinicRoomDataSet room = provider.Load<ClinicRoomDataSet>();

            Assert.That(ctx.Table, Is.Not.Null);
            Assert.That(ctx.Table.Count, Is.GreaterThan(0));
            Assert.That(adj.Table, Is.Not.Null);
            Assert.That(adj.Table.NMax, Is.EqualTo(64), "OQ-24-5 已裁 n_max = 64");
            Assert.That(room.Table, Is.Not.Null);
            Assert.That(room.Table.Count, Is.GreaterThan(0));
        }

        // ══════════════════════ ⑤ 解码失败 = 启动期硬失败 ══════════════════════

        /// <summary>字节短于头部 ⇒ 抛 <see cref="InvalidDataException"/>,报文点名产物。</summary>
        [Test]
        public void test_clinic_codec_truncatedBytes_throwsWithProductName()
        {
            byte[] good = ReadCommitted(DataBakeMenu.ClinicContextCookedAssetName);
            byte[] truncated = new byte[10];
            Array.Copy(good, truncated, truncated.Length);

            var ex = Assert.Throws<InvalidDataException>(
                () => ClinicMachineCookedCodec.ReadContext(truncated));
            Assert.That(ex.Message, Does.Contain("clinicmachine_context"),
                "报文须点名产物,否则启动期日志无法定位是哪一份坏了");
        }

        /// <summary>改一个载荷字节 ⇒ 或长度不符或表构造拒收,**绝不静默返回默认值**。</summary>
        /// <remarks><b>判别力</b>:若 codec 吞异常返回空表,本测红(空表在构造期已被拒)。
        /// 选最容易观察的改法:把 `k_context_max_raw` 的末字节 +1 ⇒ K_CONTEXT_MAX 与重算值不符。</remarks>
        [Test]
        public void test_clinic_codec_tamperedKContextMax_throws()
        {
            byte[] tampered = (byte[])ReadCommitted(DataBakeMenu.ClinicContextCookedAssetName).Clone();
            // 头 20 B 之后第一个字段 = k_context_max_raw(8 B,小端)⇒ offset 20 起。
            tampered[20] ^= 0x01;

            Assert.Throws<InvalidDataException>(() => ClinicMachineCookedCodec.ReadContext(tampered));
        }

        /// <summary>魔数被改 ⇒ 拒收(与产物名一起报出)。</summary>
        [Test]
        public void test_clinic_codec_badMagic_throws()
        {
            byte[] tampered = (byte[])ReadCommitted(DataBakeMenu.ClinicAdjCookedAssetName).Clone();
            tampered[0] = (byte)'X';

            var ex = Assert.Throws<InvalidDataException>(() => ClinicMachineCookedCodec.ReadAdj(tampered));
            Assert.That(ex.Message, Does.Contain("clinicmachine_adj"));
        }

        // ══════════════════════ ⑥ Sim 侧构造拒收(纵深防御)══════════════════════

        /// <summary>CONTEXT:重复 room_type ⇒ 拒收(重复会让「取哪一行」成为隐式规则)。</summary>
        [Test]
        public void test_clinic_contextTable_duplicateRoomType_throws()
        {
            var rows = new[]
            {
                new ClinicContextRow("ward", "普通病房", new Fix(Fix.OneRaw), new Fix(0)),
                new ClinicContextRow("ward", "重复", new Fix(Fix.OneRaw), new Fix(0)),
            };
            Assert.Throws<ArgumentException>(() => new ClinicContextTable(rows, new Fix(Fix.OneRaw)));
        }

        /// <summary>CONTEXT:头导出 K_CONTEXT_MAX ≠ 重算 max ⇒ 拒收(写方 / 读方漂移的探测器)。</summary>
        [Test]
        public void test_clinic_contextTable_kContextMaxMismatch_throws()
        {
            var rows = new[]
            {
                new ClinicContextRow("surgery", "无菌", new Fix(Fix.OneRaw), new Fix(0)),
                new ClinicContextRow("herb", "药气重", new Fix(Fix.OneRaw * 3 / 4), new Fix(0)),
            };
            // 声明 3/4 —— 小于实际 max(1.0)
            Assert.Throws<ArgumentException>(
                () => new ClinicContextTable(rows, new Fix(Fix.OneRaw * 3 / 4)));
        }

        /// <summary>ADJ:单向缺项(只有 a→b,没有 b→a)⇒ 拒收(AC-24-06 的对称完整性)。</summary>
        [Test]
        public void test_clinic_adjTable_missingMirror_throws()
        {
            var pairs = new[] { new ClinicAdjPair("bed", "shelf", new Fix(4000)) };
            Assert.Throws<ArgumentException>(
                () => new ClinicAdjTable(pairs, 64, new Fix(-16384), new Fix(16384)));
        }

        /// <summary>ADJ:镜像值不等 ⇒ 拒收(镜像 / 旋转布局必须同分)。</summary>
        [Test]
        public void test_clinic_adjTable_asymmetricWeight_throws()
        {
            var pairs = new[]
            {
                new ClinicAdjPair("bed", "shelf", new Fix(4000)),
                new ClinicAdjPair("shelf", "bed", new Fix(6000)),
            };
            var ex = Assert.Throws<ArgumentException>(
                () => new ClinicAdjTable(pairs, 64, new Fix(-16384), new Fix(16384)));
            Assert.That(ex.Message, Does.Contain("不对称"));
        }

        /// <summary>ADJ:权重越出 [w_min, w_max] ⇒ 拒收。</summary>
        [Test]
        public void test_clinic_adjTable_weightOutOfDomain_throws()
        {
            var pairs = new[]
            {
                new ClinicAdjPair("bed", "shelf", new Fix(20000)),
                new ClinicAdjPair("shelf", "bed", new Fix(20000)),
            };
            Assert.Throws<ArgumentException>(
                () => new ClinicAdjTable(pairs, 64, new Fix(-16384), new Fix(16384)));
        }

        /// <summary>ROOM:空子句集 ⇒ 拒收(空合取 = 恒匹配 ⇒ 整馆恒判为该类型)。</summary>
        [Test]
        public void test_clinic_roomTable_emptyClauses_throws()
        {
            var rooms = new[] { new ClinicRoomDef("ward", Array.Empty<ClinicRoomClause>()) };
            Assert.Throws<ArgumentException>(() => new ClinicRoomTable(rooms));
        }

        /// <summary>ROOM:邻接子句缺 direction ⇒ 拒收(方向不确定 = 判据不确定)。</summary>
        [Test]
        public void test_clinic_roomTable_adjacencyWithoutDirection_throws()
        {
            var rooms = new[]
            {
                new ClinicRoomDef("surgery", new[]
                {
                    new ClinicRoomClause(ClinicRoomClauseKind.AdjacencyMinCount,
                        "operating_table", "sterile_cabinet", ClinicRoomClauseDirection.None, 1),
                }),
            };
            Assert.Throws<ArgumentException>(() => new ClinicRoomTable(rooms));
        }

        /// <summary>ROOM:非邻接子句带 neighbor_type ⇒ 拒收(字段语义互斥)。</summary>
        [Test]
        public void test_clinic_roomTable_nonAdjacencyWithNeighbor_throws()
        {
            var rooms = new[]
            {
                new ClinicRoomDef("ward", new[]
                {
                    new ClinicRoomClause(ClinicRoomClauseKind.HasType,
                        "bed", "shelf", ClinicRoomClauseDirection.None, 0),
                }),
            };
            Assert.Throws<ArgumentException>(() => new ClinicRoomTable(rooms));
        }

        // ══════════════════════ ⑦ 跨侧枚举序号 ══════════════════════

        /// <summary>Sim 侧镜像枚举 ↔ 编辑期 Binder 枚举:名称与序号逐项相等。</summary>
        /// <remarks><b>为什么必须有</b>:产物把子句类型写成 `u8`(**序号**,不是名字)。
        /// 两侧枚举任一处增员 / 换序 ⇒ 已烘产物被**静默错读**成另一种子句 ——
        /// 没有任何运行时判据能发现「MinCount 被读成 HasType」。</remarks>
        [Test]
        public void test_clinic_roomClauseEnums_simMirrorMatchesBinderOrdinals()
        {
            AssertEnumMirror(typeof(ClinicRoomClauseKind),
                typeof(DaYiJingCheng.EditorTools.Bake.ClinicClauseKind));
            AssertEnumMirror(typeof(ClinicRoomClauseDirection),
                typeof(DaYiJingCheng.EditorTools.Bake.ClinicClauseDirection));
        }

        private static void AssertEnumMirror(Type simEnum, Type binderEnum)
        {
            string[] simNames = Enum.GetNames(simEnum);
            string[] binderNames = Enum.GetNames(binderEnum);

            Assert.That(simNames.Length, Is.EqualTo(binderNames.Length),
                $"{simEnum.Name} 与 {binderEnum.Name} 成员数不等(一侧增员 ⇒ 产物静默错读)");
            Assert.That(simNames, Is.EqualTo(binderNames),
                $"{simEnum.Name} 与 {binderEnum.Name} 的**声明序**不等");

            foreach (string name in simNames)
            {
                long simValue = Convert.ToInt64(Enum.Parse(simEnum, name));
                long binderValue = Convert.ToInt64(Enum.Parse(binderEnum, name));
                Assert.That(simValue, Is.EqualTo(binderValue),
                    $"{name}:Sim={(byte)simValue} / Binder={binderValue} —— 产物 u8 序号错位");
            }
        }

        // ══════════════════════ 只读视图不可绕过 ══════════════════════

        /// <summary>`ClinicContextTable.Rows` 返回**包装过的**只读视图,不能强转回数组就地改写。</summary>
        /// <remarks><b>判别力</b>:若 `Rows` 直接返回 `_rows`(裸数组),`Rows is ClinicContextRow[]`
        /// 为 true ⇒ 调用方可 `((ClinicContextRow[])Rows)[0] = default` 改掉构造期校验过的行,
        /// 「表不可变」契约静默失效。**这是「可被强转」的形态断言**,不是「写入抛异常」的形态断言 ——
        /// 包装后强转直接成功不了,根本走不到写入那一步。</remarks>
        [Test]
        public void test_clinic_contextTable_rows_notCastableToArray()
        {
            ClinicContextTable table = ClinicMachineCookedCodec
                .ReadContext(ReadCommitted(DataBakeMenu.ClinicContextCookedAssetName)).Table;

            Assert.That(table.Rows, Is.Not.InstanceOf<ClinicContextRow[]>(),
                "Rows 暴露的是裸数组 ⇒ 调用方可强转后就地改写");
        }

        /// <summary>`ClinicRoomTable.Rooms` 同上。</summary>
        /// <remarks><b>判别力</b>:同 context 侧。</remarks>
        [Test]
        public void test_clinic_roomTable_rooms_notCastableToArray()
        {
            ClinicRoomTable table = ClinicMachineCookedCodec
                .ReadRoom(ReadCommitted(DataBakeMenu.ClinicRoomCookedAssetName)).Table;

            Assert.That(table.Rooms, Is.Not.InstanceOf<ClinicRoomDef[]>(),
                "Rooms 暴露的是裸数组 ⇒ 调用方可强转后就地改写");
        }

        // ══════════════════════ 工具 ══════════════════════

        private static AddressablesDataProvider MakeProvider(Func<string, byte[]> byteSource)
        {
            ConstructorInfo ctor = typeof(AddressablesDataProvider).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Func<string, byte[]>) },
                modifiers: null);

            Assert.That(ctor, Is.Not.Null,
                "`AddressablesDataProvider(Func<string, byte[]>)` 内部构造不见了 —— 装载面夹具的注入缝被移除");

            return (AddressablesDataProvider)ctor.Invoke(new object[] { byteSource });
        }
    }
}
