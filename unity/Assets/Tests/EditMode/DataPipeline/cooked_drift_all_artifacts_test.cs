// 权威来源:ADR-014 两阶段烘焙(`*.cooked` 是**入 git 的出货产物**)
//          · 先例 `Boot/examine_driver_test.cs:92-108`(diagnosis_* 两份)
//          · item-database 侧扩项 `ItemDatabase/data_pipeline_bake_test.cs`
//            (段名「产物漂移门(改源必重烘)」,2026-10-20)
//
// ⚠️ **本文件是「全目录通用漂移门」** —— 把上述两处**点状**漂移门推广成
//    **`unity/Assets/DataCooked/` 全目录**的单一门:
//    逐份产物比对「盘上已提交字节」vs「对应 baker 现烘输出」,不等 ⇒ 红。
//
// ⚠️ **为什么需要它(实证)**:
//    点状门原只覆盖 diagnosis_* 两份 + item_database_* 两份 = 4/9。
//    2026-10-20 勘查 `DataCooked/` 实测发现**盘上只有 9 份产物,而源码声明了
//    15 份产物名(14 个 baker,`ItemDatabaseBaker` 出两份)** ⇒ 6 份**从未落盘**
//    (git 历史 0 笔):
//      interaction_kinds · diagnosis_negative_confidence · prescription_actions
//      · disease_action_axis · 53_outcome_table · 53_delay_table
//    逐份查明:这 6 份**有 baker、有菜单项、有测试**,但
//    ① 无任何代码从 `DataCooked/` 读它们;② `AddressablesDataProvider.Preload()`
//    的装载清单恰为盘上那 9 份 ⇒ **不在运行期面内**,故从未被发现。
//    ⚠️ 且这 6 份所归属的 5 个 story **全部标 `Complete`**(含一件名为
//    `story-007-interaction-kinds-bake-wiring` 的「烘焙接线」件)。
//
//    ✅ **2026-10-20 用户裁定「那 6 份补烘掉」,已全部落盘** ⇒ 盘上 9 → **15**,
//    15 份全部进入 `BakedArtifacts` 受逐字节漂移门保护;`KnownUnbaked` 清空(6 → 0)。
//
//    ⚠️ **2026-10-21 增补(上文的 15 是当日实测值,原文保留)**:再加 4 份 ——
//    `48_tutorial_content`(S17-3 story-001 AC-5 接线)+ 24 医馆三表(见下「24 医馆」段)
//    ⇒ 盘上 **19** 份、`BakedArtifacts` 19 条、`pinnedDeclared` 19。
//    ⚠️ 这三份是本门**先前红着抓出来的**:S17-2 落盘后首跑全量即两条红
//    (`test_sweep_declaredArtifactNames_allTriaged` 16≠19 / `test_sweep_noGhostArtifact_onDiskButUnregistered`
//    点名三份幽灵产物)—— 门工作正常,是接线时漏登记。
//    ⚠️ **2026-10-21 二次增补**:再 +1 份 `item_database_furniture`(O-24-1 兑现)
//    ⇒ 盘上 **20** 份、`BakedArtifacts` 20 条、`pinnedDeclared` **20**。
//    ⚠️ **2026-10-21 三次增补**:再 +1 份 `build_modules`(`TD-itemdb-01` 兑现)
//    ⇒ 盘上 **21** 份、`BakedArtifacts` 21 条、`pinnedDeclared` **21**。
//    ⚠️ **2026-10-22 四次增补**:再 +1 份 `disease_registry`(B1 接真:读方 codec + 运行期
//    接线 + 产物落盘同批完成)⇒ 盘上 **22** 份、`BakedArtifacts` 22 条、`pinnedDeclared` **22**。
//    ⚠️ 本份**没有**走「先登记 KnownUnbaked 再迁入」的常规路径 —— 它当天先以欠账形态入表
//    (`KnownUnbaked` 1 条),同日读方补立后立即迁入 `BakedArtifacts` 并清空欠账表。
//    两次改动都在同一天,故 git 历史里会看到「0→1→0」的往返;这不是反复,是**同批两阶段**。
//    ⚠️ **2026-10-26 五次增补**:再 +2 份 `clinicmachine_room_names` / `clinicmachine_context_names`
//    (裁定 ①-C / ②-C 的两张词表)⇒ 盘上 **24** 份、`BakedArtifacts` 24 条、`pinnedDeclared` **24**。
//    ⚠️ **本门第四次抓到同类漏登记** —— 本轮加了两条 `BakedArtifacts` 却漏改哨兵,首跑全量即红
//    (`declared 24 ≠ pinned 22`)。门工作正常;补的是哨兵,不是放宽门。
//    ⚠️ 同样**是本门抓出来的**:落盘后首跑全量即两条红(21≠20 + 幽灵产物点名
//    `build_modules.cooked.bytes`)。**这是本门第三次抓到同类漏登记**(24 三表 / 21a 家具 / 本份)
//    —— 说明「新产物必登记」这条纪律靠人记是不成立的,门的存在是必要的。
//
// ⚠️ **本门查什么 / 不查什么**:
//    查 —— ① 已烘产物的**字节漂移**(改源不重烘);
//          ② 盘上出现的每一份都在登记表里(禁幽灵产物);
//          ③ **源码声明的每一份产物名都在登记表里**(禁「新增 baker 忘了登记」)。
//    不查 —— 该不该有某份产物(那是接线决策,归各 epic,**门无权替它决定**)。
//    故未落盘的 6 份走**具名登记 + 理由**,而非判红:
//    本门只断言「**登记表里声明为『已烘』的,字节必须与现烘一致**」。
//    ⚠️ **残余边界**:一份产物若**既不落盘、又不在任何源码字面量里**,本门看不见它
//    (无来源可比对)—— 但那种形态连「谁烘它」都没写下来,已不属本门的判据面。
//
// ⚠️ 零 UnityEngine / 零 Addressables 依赖:路径由 repoRoot 拼,产物名由
//    `DataBakeMenu` 的公共常量引用(**不另抄字面量** —— 承 ADR-024 §⑥ 单一定义处纪律)。
//
// ⚠️ **为什么住在 `Tests/EditMode/DataPipeline/` 而不是 `ItemDatabase/`**:
//    本门覆盖 **15 份产物**、跨 8 个系统(2 物品 / 3 诊断 / 1 病例 / 2 音频 / 1 输入 /
//    1 遥测 / 1 交互 / 1 处方 / 1 处置轴 / 2 医疗后果),
//    是**跨 Story 的数据管线基建**,不属任何单一系统。初始落在 `ItemDatabase/` 是从
//    先例(`data_pipeline_bake_test.cs`)续写的顺位,但与判据面不符 ⇒ 2026-10-20 迁至独立目录。
//    目录级装配仍共用 `EditMode.asmdef`(该树唯一共享装配),**零新增引用**。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Bake;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.DataPipeline
{
    /// <summary>
    /// <b>全目录产物漂移门</b> —— <c>unity/Assets/DataCooked/</c> 下每一份已烘产物
    /// 的字节,必须等于其 baker 的现烘输出。
    /// <para>⚠️ 本门<b>不</b>决定「该不该有某份产物」—— 那是接线决策。它只保证
    /// 「声明为已烘的,字节是对的」+「盘上出现的,都是登记过的」。</para>
    /// </summary>
    [TestFixture]
    internal sealed class cooked_drift_all_artifacts_test
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        /// <summary>盘上产物目录(<c>unity/Assets/DataCooked/</c>)。</summary>
        private static string CookedDir
            => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        // ══════════════════ 登记表(唯一真源)══════════════════

        /// <summary>
        /// 一份产物的登记:文件名 + 现烘取字节的委托。
        /// <para>委托返回<b>现烘</b>字节(不是读盘)—— 与盘上字节比对是门的事。</para>
        /// </summary>
        private readonly struct ArtifactBake
        {
            /// <summary>产物文件名(含 <c>.cooked.bytes</c>)。</summary>
            public readonly string FileName;

            /// <summary>仓根 → 现烘字节。</summary>
            public readonly Func<string, byte[]> Bake;

            public ArtifactBake(string fileName, Func<string, byte[]> bake)
            {
                FileName = fileName;
                Bake = bake;
            }
        }

        /// <summary>
        /// <b>已烘产物登记表</b> —— 盘上**应当存在**、且**字节须与现烘一致**的产物。
        /// <para>⚠️ 增删本表 = 改门的判据面。<b>删条目不会让门变松</b> ——
        /// <see cref="test_sweep_noGhostArtifact_onDiskButUnregistered"/> 会立刻红
        /// (盘上有该文件却没登记 = 幽灵产物)。</para>
        /// </summary>
        private static readonly ArtifactBake[] BakedArtifacts =
        {
            // ── item-database(2 份;同一次烘焙产出,共用 ConfigVersion)──
            new ArtifactBake(DataBakeMenu.ItemsCookedAssetName,
                root => ItemDatabaseBaker.BakeFromRepo(root).ItemsCooked),
            new ArtifactBake(DataBakeMenu.RecipesCookedAssetName,
                root => ItemDatabaseBaker.BakeFromRepo(root).RecipesCooked),

            // ── 8 诊断(3 份)──
            new ArtifactBake(DataBakeMenu.DiagnosisSignsCookedAssetName,
                root => DiagnosisSignBaker.BakeFromRepo(root).Cooked),
            new ArtifactBake(DataBakeMenu.DiagnosisReadFloorCookedAssetName,
                root => DiagnosisReadFloorBaker.BakeFromRepo(root).Cooked),
            new ArtifactBake(DataBakeMenu.DiagnosisNegativeConfidenceCookedAssetName,
                root => DiagnosisNegativeConfidenceBaker.BakeFromRepo(root).Cooked),

            // ── 37 病例(1 份)──
            new ArtifactBake(DataBakeMenu.JudgmentLexiconCookedAssetName,
                root => JudgmentLexiconBaker.BakeFromRepo(root).Cooked),

            // ── 44 音频(2 份)──
            new ArtifactBake(AudioEventTableBake.CookedFileName,
                root => AudioEventTableBake.BakeFromRepo(root).Cooked),
            new ArtifactBake(AudioSettingsRegistryBake.CookedFileName,
                root => AudioSettingsRegistryBake.BakeFromRepo(root).Cooked),

            // ── 3 输入(1 份)──
            new ArtifactBake(InputDeviceSwitchBake.CookedFileName,
                root => InputDeviceSwitchBake.BakeFromRepo(root).Cooked),

            // ── 51 遥测(1 份)──
            new ArtifactBake(TelemetryAnalyticsBake.CookedFileName,
                root => TelemetryAnalyticsBake.BakeFromRepo(root).Cooked),

            // ── 4 交互(1 份;2026-10-20 补烘)──
            new ArtifactBake(DataBakeMenu.InteractionKindsCookedAssetName,
                root => InteractionKindBaker.BakeFromRepo(root).Cooked),

            // ── 11 处方(1 份;2026-10-20 补烘)──
            new ArtifactBake(DataBakeMenu.PrescriptionActionsCookedAssetName,
                root => PrescriptionActionsBaker.BakeFromRepo(root).Cooked),

            // ── 9 处置轴(1 份;2026-10-20 补烘)──
            new ArtifactBake(DataBakeMenu.DiseaseActionAxisCookedAssetName,
                root => DiseaseActionAxisBaker.BakeFromRepo(root).Cooked),

            // ── 53 医疗后果(2 份;2026-10-20 补烘)──
            new ArtifactBake(DataBakeMenu.OutcomeTableCookedAssetName,
                root => OutcomeTable53.BakeFromRepo(root).Cooked),
            new ArtifactBake(DataBakeMenu.DelayTableCookedAssetName,
                root => DelayTable53Bake.BakeFromRepo(root).Cooked),

            // ── 48 教学内容(1 份;S17-3 story-001 AC-5 接线,2026-10-21)──
            new ArtifactBake(DataBakeMenu.TutorialContentCookedAssetName,
                root => TutorialContentBake.BakeFromRepo(root).Cooked),

            // ── 24 医馆(3 份;S17-2 前置「先补 cooked 落盘接线」,2026-10-21)──
            //    ⚠️ 三份共用同一次 BakeFromRepo 产物(同 ConfigVersion),故三次调用三次烘焙;
            //    与 item-database 两份同型(那边也是各条各调一次)。
            new ArtifactBake(DataBakeMenu.ClinicContextCookedAssetName,
                root => ClinicMachineTablesBake.BakeFromRepo(root).ContextCooked),
            new ArtifactBake(DataBakeMenu.ClinicAdjCookedAssetName,
                root => ClinicMachineTablesBake.BakeFromRepo(root).AdjCooked),
            new ArtifactBake(DataBakeMenu.ClinicRoomCookedAssetName,
                root => ClinicMachineTablesBake.BakeFromRepo(root).RoomCooked),

            // ── 24 两张词表(2 份;2026-10-26 裁定 ①-C / ②-C)──
            //    ⚠️ 它们是 `ClinicEnvDto.RoomNameId` / `ContextIds` 编号的**唯一稳定来源**;
            //    漂移 = 旧存档里的编号指向错误的房间 / 情境(静默错读,无异常)。
            new ArtifactBake(DataBakeMenu.ClinicRoomNamesCookedAssetName,
                root => ClinicMachineTablesBake.BakeFromRepo(root).RoomNamesCooked),
            new ArtifactBake(DataBakeMenu.ClinicContextNamesCookedAssetName,
                root => ClinicMachineTablesBake.BakeFromRepo(root).ContextNamesCooked),

            // ── 21a 家具目录(1 份;O-24-1 兑现,2026-10-21)──
            //    ⚠️ 生产菜单先烘本份、再把其闭集注入 24 的 ROOM_TABLE 成员资格门。
            //    本门只比字节;`FurnMembershipEnforced` **不进载荷**(它只影响校验,不改编码)
            //    ⇒ 有闭集 / 无闭集两条路径的 ROOM_TABLE 字节相同,故上面三条沿用单参重载。
            new ArtifactBake(DataBakeMenu.FurnitureCatalogueCookedAssetName,
                root => ItemDatabaseFurnitureBake.BakeFromRepo(root).Cooked),

            // ── 23 模块目录(1 份;`TD-itemdb-01` 兑现,2026-10-21)──
            //    ⚠️ 委托走 **`BuildModulesBake.BakeFromRepo`(裸烘焙)**,不是生产链路的
            //    `DataBakeMenu.BakeBuildModulesCore` —— 后者多一道跨表同源门(抛异常),
            //    而本门只比字节。同源门**不改编码**(只决定「要不要落盘」),
            //    故两条路径的 cooked 字节相同(与上面 21a 家具目录的 `FurnMembershipEnforced`
            //    同一口径:校验不进载荷)。
            new ArtifactBake(DataBakeMenu.BuildModulesCookedAssetName,
                root => BuildModulesBake.BakeFromRepo(root).Cooked),

            // ── 9 病种注册表(1 份;2026-10-22 B1 接真 —— 从 KnownUnbaked 迁入)──
            //    ⚠️ 与上面 21a / 23 同口径:委托走**裸烘焙** `DiseaseRegistryBaker.BakeFromRepo`,
            //    不是菜单里的 `BakeDiseaseRegistry`(后者多一道 `Materialize` 真往返,
            //    那是**校验**,不改编码 ⇒ 两条路径的 cooked 字节相同)。
            //    ⚠️ 本份与其余 21 份的**性质差异**:它的载荷里带两个**全局量**
            //    (`trend_window_ticks` / `trend_eps`),故本门同时钉住「baker 把这两个量
            //    真的写进了字节」—— 改 Binder 不透传 ⇒ 字节变 ⇒ 本门红。
            new ArtifactBake(DiseaseRegistryBaker.CookedFileName,
                root => DiseaseRegistryBaker.BakeFromRepo(root).Cooked),

            // ── 17 采集调参表(1 份;S20-3 评审 W-3 甲案补齐烘焙接线,2026-10-26)──
            //    ⚠️ 本份的**补烘理由**与前 24 份都不同:此前该 Binder 是 25 个 Binder 中
            //    **唯一零生产调用方**的 —— 44 条夹具直调 `Bind` 只证明「能拒收」,
            //    不证明「在生产烘焙路径上被执行」。落盘后本门逐字节比对,
            //    使 AC-17-08b 的「构建期硬失败」在生产路径上可证伪(承
            //    `coding-standards.md` §「某件有门」)。
            //    ⚠️ 委托走**裸烘焙** `ForagingConstantsBaker.BakeFromRepo`(与上面 21a / 23 / 9 同口径:
            //    菜单层只多做落盘,不改编码 ⇒ 两条路径的 cooked 字节相同)。
            new ArtifactBake(DataBakeMenu.ForagingConstantsCookedAssetName,
                root => ForagingConstantsBaker.BakeFromRepo(root).Cooked),

            // ── 建造槽位骨架(1 份;S20-20 落盘,2026-10-28)──
            //    ⚠️ **本门第五次抓到同类漏登记** —— S20-20 落了 `world_buildslots.cooked.bytes`
            //    却没登记,全量回归首跑即红两条(`noGhostArtifact` + `pinnedDeclared` 25→26)。
            //    ⚠️ 委托走**裸烘焙** `WorldBuildSlotsBake.BakeFromRepo`(与上面 21a / 23 / 9 / 17 同口径:
            //    菜单层的 `BakeWorldBuildSlotsCore` 只多做落盘与组注册,不改编码 ⇒ 字节相同)。
            //    ⚠️ 登记后本门顺带钉住一件此前**无门**的事(代码评审 A-26):
            //    「仓内 `assets/data/world_buildslots.json` 现烘 ⇒ 与盘上 cooked 逐字节相同」
            //    ⇒ 手改 JSON 不重烘 = 本门红,即 ADR-022 §② 「无第二作者」的可证伪落点。
            new ArtifactBake(WorldBuildSlotsBake.CookedFileName,
                root => WorldBuildSlotsBake.BakeFromRepo(root).Cooked),

            // ── 世界几何 / 地形表(2 份;关卡工具 story-002 落盘,2026-10-29)──
            //    ⚠️ **本门第六次抓到同类漏登记** —— story-002 落了两份新产物却没登记,
            //    全量回归首跑即红两条(`noGhostArtifact` + `pinnedDeclared` 26→28)。
            //    ⚠️ 委托走**裸烘焙** `BakeFromRepo`(同上方 21a / 23 / 9 / 17 / 槽位骨架口径:
            //    菜单层的 `BakeWorldGeometryCore` 只多做落盘与组注册,不改编码 ⇒ 字节相同)。
            //    ⚠️ 这两条同时把 ADR-022 §②「无第二作者」钉在几何 / 地形上:
            //    手改 `world_geometry.json` / `world_terrain.json` 而不重烘 ⇒ 本门红。
            new ArtifactBake(WorldGeometryBake.CookedFileName,
                root => WorldGeometryBake.BakeFromRepo(root).Cooked),
            new ArtifactBake(WorldTerrainBake.CookedFileName,
                root => WorldTerrainBake.BakeFromRepo(root).Cooked),

            // ── 生态区多边形(1 份;关卡工具 story-003 落盘,2026-10-29)──
            //    ⚠️ 本门**第七次**遇到「新增产物」这一步 —— 与上两条同批,已按同一口径处理。
            //    ⚠️ 委托走**裸烘焙** `BakeFromRepo`(同 S2 的两条):C4 几何门**在 binder 里**,
            //    所以这条委托同时把「手改多边形而不重烘」与「多边形非法却硬塞进产物」都钉住。
            new ArtifactBake(WorldEcozonesBake.CookedFileName,
                root => WorldEcozonesBake.BakeFromRepo(root).Cooked),
            // 2026-10-29(关卡工具 story-004):POI 定义 / 资源点。
            new ArtifactBake(WorldPoiBake.CookedFileName,
                root => WorldPoiBake.BakeFromRepo(root).Cooked),
            new ArtifactBake(WorldResourcesBake.CookedFileName,
                root => WorldResourcesBake.BakeFromRepo(root).Cooked),
        };

        /// <summary>
        /// <b>已知未烘产物登记</b> —— 仓里有 baker 与菜单项,但**有意不落盘**的产物。
        /// <para>⚠️ **这不是豁免清单,是欠账清单**。每条须写明「为什么现在不烘」,
        /// 且当该产物**真的落盘**时,<see cref="test_sweep_noGhostArtifact_onDiskButUnregistered"/>
        /// 会要求把它移进 <see cref="BakedArtifacts"/>(否则红)。</para>
        /// <para><b>2026-10-20:本表已清空(6 → 0)。</b>原记 6 份从未落盘的产物
        /// (<c>interaction_kinds</c> / <c>diagnosis_negative_confidence</c> /
        /// <c>prescription_actions</c> / <c>disease_action_axis</c> /
        /// <c>53_outcome_table</c> / <c>53_delay_table</c>)已由用户裁定**全部补烘**,
        /// 六份均已移入 <see cref="BakedArtifacts"/> 并接受漂移门保护。
        /// 原欠账理由(其归属 story 全标 Complete 却零产物)保留在 git 历史中。</para>
        /// <para>⚠️ **表清空 ≠ 判据面缩小**:门② 仍要求「盘上每一份都登记」,
        /// 门②b 仍要求「源码声明的每份都登记」,门③ 仍要求本表零理由空缺
        /// (空表天然满足)。新增欠账仍须在此加条目并同步改
        /// <see cref="test_registry_knownUnbakedCount_isPinned"/> 的钉定值。</para>
        /// <para><b>2026-10-22:0 → 1。</b>新增 <c>disease_registry.cooked.bytes</c> ——
        /// 9 注册表的**写方已立、读方 codec 未立**,产物此刻无人消费。
        /// 详见表内条目。⚠️ 这一条与 2026-10-20 清空的那六份**性质不同**:
        /// 那六份是「story 标 Complete 却零产物」(漏做),本份是「写方先行、读方后补」(有意分步)。
        /// 两者共用本表是因为**门要的判据相同**(声明了但盘上/消费面缺失须被点名),
        /// 不是因为欠账理由相同。</para>
        /// <para><b>2026-10-22(同日)1 → 0。</b>读方 codec 与运行期接线同批落地
        /// (<c>DiseaseRegistryCookedCodec</c> + <c>AddressablesDataProvider.EnsureDiseaseRegistry</c>
        /// + <c>CompositionRoot</c> 的 provider 分支),产物已烘落盘并迁入
        /// <see cref="BakedArtifacts"/>。**原欠账理由中的两条前提均已不成立**:
        /// ① 「读方 codec 尚不存在」→ 已存在;② 「运行期仍走 CreateFallbackRegistry()」
        /// → provider != null 时走真表。原文不删(append-only),此处仅登记结清。</para>
        /// </summary>
        private static readonly Dictionary<string, string> KnownUnbaked =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
        };

        // ══════════════════ 门 ①:已烘产物逐字节比对 ══════════════════

        /// <summary>
        /// 登记表驱动:<b>每一份已烘产物</b>的盘上字节 == 其 baker 现烘输出。
        /// <para>⚠️ 用 <c>TestCaseSource</c> 逐份展开 —— 一份红了能直接看出是<b>哪一份</b>,
        /// 不是一个笼统的「有漂移」。</para>
        /// </summary>
        public static IEnumerable<TestCaseData> BakedArtifactNames()
        {
            foreach (ArtifactBake a in BakedArtifacts)
                yield return new TestCaseData(a.FileName).SetName($"drift[{a.FileName}]");
        }

        [TestCaseSource(nameof(BakedArtifactNames))]
        public void test_drift_eachArtifact_matchesFreshBakerOutput(string fileName)
        {
            ArtifactBake entry = BakedArtifacts.Single(a => a.FileName == fileName);
            string path = Path.Combine(CookedDir, fileName);

            Assert.That(File.Exists(path), Is.True,
                $"登记为「已烘」的产物在盘上不存在:{fileName}。" +
                "要么重烘它,要么把它移进 KnownUnbaked(并写明理由)—— " +
                "「登记了却不存在」是本门最想抓的静默失败。");

            byte[] fresh = entry.Bake(RepoRoot);
            byte[] onDisk = File.ReadAllBytes(path);

            Assert.That(onDisk, Is.EqualTo(fresh),
                $"{fileName} 漂移:盘上字节 ≠ baker 现烘输出 —— 改了源却未重烘。" +
                "重烘:Unity 菜单「大医精诚/数据管线/烘焙 …」对应项," +
                "或 batch:unity build <unity/> --target StandaloneLinux64 " +
                "--execute-method <DataBakeMenu 的该菜单方法全名>。" +
                "(`*.cooked` 是入 git 的出货产物,ADR-014;漂移 = 运行期读陈旧数据而测试全绿)");
        }

        // ══════════════════ 门 ②:禁幽灵产物(盘上有但没登记)══════════════════

        /// <summary>
        /// 盘上每一份 <c>*.cooked.bytes</c> 都必须在 <see cref="BakedArtifacts"/>
        /// 或 <see cref="KnownUnbaked"/> 里登记过。
        /// <para>⚠️ 这条是**防门自身变松**的:有人删了某条登记,本测立刻红 ——
        /// 于是「删条目」不能用来让漂移门放过一份产物。承 ADR-024 A5「双向差集归零」同型。</para>
        /// </summary>
        [Test]
        public void test_sweep_noGhostArtifact_onDiskButUnregistered()
        {
            var registered = new HashSet<string>(
                BakedArtifacts.Select(a => a.FileName), StringComparer.Ordinal);
            var knownUnbaked = new HashSet<string>(KnownUnbaked.Keys, StringComparer.Ordinal);

            Assert.That(Directory.Exists(CookedDir), Is.True,
                $"产物目录不存在:{CookedDir}");

            string[] onDisk = Directory
                .GetFiles(CookedDir, "*.cooked.bytes", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .ToArray();

            Assert.That(onDisk, Is.Not.Empty,
                "DataCooked/ 下零产物 —— 目录被清空或路径算错(空面会让本门恒绿)");

            string[] ghosts = onDisk
                .Where(f => !registered.Contains(f) && !knownUnbaked.Contains(f))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();

            Assert.That(ghosts, Is.Empty,
                "盘上存在未登记的产物(幽灵产物):[" + string.Join(", ", ghosts) + "]。\n" +
                "两种处置,二选一:\n" +
                "  ① 它确实该有 ⇒ 加进 BakedArtifacts(带现烘委托),纳入漂移门;\n" +
                "  ② 它暂不该有 ⇒ 加进 KnownUnbaked 并写明「为什么现在不烘」。\n" +
                "⚠️ 不得靠「不登记」让它逃过漂移检查。");
        }

        // ══════════════════ 门 ②b:禁「新增 baker 忘了登记」══════════════════

        /// <summary>
        /// 扫 <c>unity/Assets/**/*.cs</c>(排除 <c>/Tests/</c>,剥注释)取全部
        /// <c>"&lt;名&gt;.cooked.bytes"</c> 字面量,要求每个都在两张登记表里。
        /// <para>⚠️ 这条堵的是**门② 的镜像缺口**:门② 只看「盘上有什么」——
        /// 新增一个 baker + 产物名却从未落盘时,盘上无物 ⇒ 门② **看不见**。
        /// 本测把**源码声明的**产物名全集拉出来比对 ⇒ 新 baker 必须显式登记
        /// (已烘 / 或欠账+理由),不能悄悄溜过。承 ADR-024 A5「双向差集归零」同型。</para>
        /// <para>⚠️ 与门② 合起来才封闭:Baked ⊆ 盘上(门① 的 <c>File.Exists</c>)·
        /// 盘上 ⊆ Baked ∪ Known(门②)· 声明 ⊆ Baked ∪ Known(本测)。</para>
        /// </summary>
        [Test]
        public void test_sweep_declaredArtifactNames_allTriaged()
        {
            string assetsRoot = Path.Combine(RepoRoot, "unity", "Assets");
            Assert.That(Directory.Exists(assetsRoot), Is.True, $"期望目录不存在:{assetsRoot}");

            var declared = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/")) continue;
                foreach (Match m in Regex.Matches(StripComments(File.ReadAllText(file)),
                             "\"([A-Za-z0-9_]+\\.cooked\\.bytes)\""))
                    declared.Add(m.Groups[1].Value);
            }

            // 扫描器完备性哨兵 —— 剥离器若过度吞字(如把逐字串 `@"…"` 的 `"` 当转义),
            // 声明集会**变小**,本行先红。⚠️ 这是本门唯一能自证「扫全了」的判据。
            // 2026-10-26:22 → 24(+clinicmachine_room_names / clinicmachine_context_names,
            // 裁定 ①-C / ②-C 的两张词表)。前值 22 = 2026-10-22 实测(+disease_registry 前值 21)。
            // 2026-10-26(同日,S20-3 W-3):24 → 25(+foraging_constants,甲案补烘落盘)。
            // 2026-10-28(S20-20):25 → 26(+world_buildslots,关卡工具导出 + 烘焙落盘)。
            // 2026-10-29(关卡工具 story-002):26 → 28(+world_geometry / world_terrain,几何 + 地形表)。
            // 2026-10-29(关卡工具 story-003):28 → 29(+world_ecozones,生态区多边形)。
            // 2026-10-29(关卡工具 story-004):29 → 31(+world_poi / world_resources,POI 定义 / 资源点)。
            const int pinnedDeclared = 31; // 2026-10-29 实测(31 份)
            Assert.That(declared.Count, Is.EqualTo(pinnedDeclared),
                $"源码声明的产物名 = {declared.Count},钉定值 = {pinnedDeclared}。" +
                "新增/移除产物名须同时改本数字;数量异常(尤其**变少**)= 扫描器被改坏。\n" +
                "实测集合:[" + string.Join(", ", declared) + "]");

            var triaged = new HashSet<string>(
                BakedArtifacts.Select(a => a.FileName), StringComparer.Ordinal);
            triaged.UnionWith(KnownUnbaked.Keys);

            string[] untriaged = declared.Where(n => !triaged.Contains(n)).ToArray();
            Assert.That(untriaged, Is.Empty,
                "源码声明了但**两张表都没登记**的产物名:[" + string.Join(", ", untriaged) + "]。\n" +
                "新增 baker / 产物名后必须二选一登记:\n" +
                "  ① 该落盘 ⇒ 加进 BakedArtifacts(带现烘委托)并**重烘落盘**;\n" +
                "  ② 暂不落盘 ⇒ 加进 KnownUnbaked 并写明理由。");
        }

        /// <summary>
        /// 剥除 <c>//</c> 与 <c>/* */</c> 注释 —— **字符串字面量内容保留**(本测要读它)。
        /// <para>承 item-database <c>AC-21a-48</c> 侧 <c>RuntimeSourceGuard</c> 的同款剥离口径
        /// (「剥离注释后扫,字符串字面量保留」)。</para>
        /// <para>⚠️ **已知限制**:逐字串(<c>@"…"</c>)里的反斜杠不构成转义,
        /// 本实现按普通串处理 ⇒ 若某处写 <c>@"…\"</c>,其收尾引号会被误判为转义。
        /// 实测源码零处(产物名全为 <c>a.cooked.bytes</c> 形态,无反斜杠);
        /// 一旦发生,<see cref="test_sweep_declaredArtifactNames_allTriaged"/> 的
        /// <c>pinnedDeclared</c> 哨兵会先红。</para>
        /// </summary>
        private static string StripComments(string src)
        {
            var sb = new StringBuilder(src.Length);
            bool inStr = false, inChar = false, inLine = false, inBlock = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';

                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }

                if (inStr || inChar)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < src.Length) sb.Append(src[++i]);   // 转义:吞下一个字符
                    else if (inStr && c == '"') inStr = false;
                    else if (inChar && c == '\'') inChar = false;
                    continue;
                }

                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        // ══════════════════ 门 ③:登记表自身的一致性 ══════════════════

        /// <summary>
        /// 登记表内部零重复、零交叉 —— 同一文件名不得同时出现在两张表里,
        /// 也不得在 <see cref="BakedArtifacts"/> 里出现两次。
        /// <para>⚠️ 交叉登记会让门② 与门① 给出矛盾结论(既「应比对」又「不烘」)。</para>
        /// </summary>
        [Test]
        public void test_registry_noDuplicateOrCrossRegistration()
        {
            string[] dup = BakedArtifacts
                .GroupBy(a => a.FileName, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();
            Assert.That(dup, Is.Empty,
                "BakedArtifacts 内重复登记:[" + string.Join(", ", dup) + "]");

            string[] cross = BakedArtifacts
                .Select(a => a.FileName)
                .Where(KnownUnbaked.ContainsKey)
                .ToArray();
            Assert.That(cross, Is.Empty,
                "同一产物既登记为「已烘」又在 KnownUnbaked 里:[" + string.Join(", ", cross) + "]");

            string[] badReason = KnownUnbaked
                .Where(kv => string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => kv.Key)
                .ToArray();
            Assert.That(badReason, Is.Empty,
                "KnownUnbaked 有条目未写理由:[" + string.Join(", ", badReason) + "] —— " +
                "「欠账清单」必须写明为什么现在不烘,否则退化成豁免名单");
        }

        /// <summary>
        /// <b>KnownUnbaked 的「反成长期」守卫</b> —— 一张欠账清单若只增不减,
        /// 迟早变成永久豁免。本测钉住「它当前是 0 条」,
        /// 逼使任何新增/移除都必须**显式改这个数字**并说明理由。
        /// <para>⚠️ 数字本身不是目的 —— 它是**让人停下来看一眼**的闸。
        /// 若确需变更:同时改 <see cref="KnownUnbaked"/> 与本节数字 + 注释理由。</para>
        /// <para><b>2026-10-20:钉定值 6 → 0</b> —— 用户裁定六份欠账全部补烘,
        /// 故清单清空。<b>这不是「删条目让门变松」</b>:那些产物已进入
        /// <see cref="BakedArtifacts"/> 并**逐字节**受门① 保护(比欠账登记更严)。</para>
        /// </summary>
        [Test]
        public void test_registry_knownUnbakedCount_isPinned()
        {
            const int pinned = 0; // 2026-10-22 值(0 → 1 → 0:同日 disease_registry 补立读方 + 落盘后迁入 BakedArtifacts)

            Assert.That(KnownUnbaked.Count, Is.EqualTo(pinned),
                $"KnownUnbaked 条数 = {KnownUnbaked.Count},钉定值 = {pinned}。" +
                "变更此清单须同时改本数字并在类头 §未登记产物 更新理由 —— " +
                "防「欠账清单」无声地长成「永久豁免名单」。");
        }
    }
}
