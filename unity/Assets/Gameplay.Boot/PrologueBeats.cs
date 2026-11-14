// 序章节拍表 P1-01 … P1-05(第一段 · 失去)。
//
// 权威来源:
//   `design/levels/prologue-beat-script.md` §第一段(每拍的触发 / 呈现 / 判据)
//   `design/levels/chapter-1-tent-clinic.md` §3 分区(A1 教堂 4–15 × 14–29 · A2 前庭 16–21 × 18–25)
//
// ⚠️ **本表只放第一段的前五拍**。D1 按脚本逐段接,不一次接完 —— 理由:
//    每多接一拍就多一处「玩家实际会走到哪」的假设,而灰盒世界里这些假设只有真包能证伪
//    (本轮走查已两次证伪:出生点、病人格)。先让 P1-01…P1-05 在真包里走通,再据实测坐标扩表。
//
// ⚠️ **字幕文本是占位**:正式文案归 writer(节拍脚本 §3 已登记);此处只保证
//    「一拍一句、语义与正典一致」,便于 D1 的真包验证。
//
// 🔁 **2026-11-14(《留人》集 1 落位 · 正典重写稿同步)**:三处旁白改写,依据
//    `design/gdd/content/campaign-arc.md` 的 2026-11-14 全剧情重写稿(改动 ①③⑥)。
//    只动**文本**,触发 / 判据 / 载体一字不动。
//    ① P1-01 补抬埋工身份(改动 ①:主角不是白纸,他有过往);
//    ② P1-04 核心道具有变(改动 ③):不是神父的登记簿,是**你自己的抬埋簿** ——
//       一百四十七个编号全是**你的**笔迹,你只会写数字和几个姓;
//    ③ P1-05 保留「挂在你脖子上」(改动 ⑥):听诊器是**托付的手势**,不是遗弃的动作。
//       遗弃在第四章才揭晓 —— 名单末尾「留下他们」= 这批不带了。**手势是真的,注解不是**;
//       这正是「你听了三年的嘱托,是一句遗弃」的反差来源。⚠️ 不得改写成「扔在案上」,
//       那会让第四章「他给你听诊器,是因为他带不走」失去对手戏。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // EventKind(第二段的事实读面)

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>序章第一段(P1-01 … P1-05)的节拍表。</summary>
    public static class PrologueBeats
    {
        /// <summary>教堂(A1 区)的格矩形,**闭区间**(权威:`chapter-1-tent-clinic.md` §3 分区)。
        /// <para>⚠️ 这四值同时服务两处:节拍表的区域判定、灰盒地坪(`CompositionRoot` 的
        /// 「关内地面」)。写第二份 = 让「玩家踩到的范围」与「看起来是屋里的范围」分叉。</para>
        /// <para>🔁 2026-11-13 实测:数据里教堂确实存在(不可走格围出的 x∈[4,15] / z∈[14,29],
        /// 东墙 z∈[20,23] 有塌口),但此前**没有任何网格** —— 玩家在教堂里看见的是旷野。</para></summary>
        public const int ChurchX0 = 4;
        /// <summary>见 <see cref="ChurchX0"/>。</summary>
        public const int ChurchX1 = 15;
        /// <summary>见 <see cref="ChurchX0"/>。</summary>
        public const int ChurchZ0 = 14;
        /// <summary>见 <see cref="ChurchX0"/>。</summary>
        public const int ChurchZ1 = 29;

        /// <summary>
        /// 组装第一段的五拍。
        /// </summary>
        /// <param name="regions">
        /// 区域触发驱动(N1,2026-11-14 引入)。三拍的「走到某处」判定从此读它,
        /// **不再**各自捕获玩家格闭包。
        /// </param>
        /// <param name="onSeventeenArrive">
        /// P1-07 的**动作**(N2,2026-11-14):十七个难民出现。必需 —— 传 `null` 会让
        /// 「人群出现」这一拍退化成纯字幕,而它本来就不是一句台词。
        /// </param>
        /// <remarks>
        /// 🔁 **2026-11-13(乙)三处改判**,依据是真包走查的实测序列
        /// (`tick=980 (12,21) P1-04` → `tick=1080 (9,19) P1-05` → `tick=1180 (8,18) P1-02`):
        /// <list type="number">
        /// <item>三拍从**大矩形**改成**锚点邻格**(`PrologueAnchors`)。旧矩形压在进门走廊上,
        ///       玩家只是路过就把「登记簿」读了。</item>
        /// <item>三拍都加 `RequiresInteract` —— 脚本的完成判据是「读毕」,不是「路过」。</item>
        /// <item>`P1-05`(神父走)从「只要求 P1-04」改成**同时要求 P1-02 / P1-03 / P1-04** ——
        ///       否则神父会在玩家看台阶与告示之前就走了。</item>
        /// </list>
        /// 🔁 **2026-11-14(N1)**:签名由 `Func&lt;WorldPos&gt; playerCell` 改为 `RegionTriggerDriver`。
        /// 语义**不变**(仍是「此刻在锚点邻格」),变的是**谁持有那份格判定** —— 从三条各写一份的闭包
        /// 收到一个可复用、可测、有边的驱动器。⚠️ 这里刻意用 `IsInside`(此刻)而**不是**
        /// `HasEntered`(进过):这三拍的完成判据是「站在这儿按 E」,走开再回来按仍然算数。
        /// </remarks>
        public static IReadOnlyList<PrologueBeat> BuildFirstAct(RegionTriggerDriver regions,
                                                               Action onSeventeenArrive)
        {
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            if (onSeventeenArrive == null) throw new ArgumentNullException(nameof(onSeventeenArrive));

            // 锚点邻格判定 —— 与 `PrologueAnchors` 的体块位置**同源**(经 `ToRegion`)。
            //   判定无副作用,每 tick 会被反复调用。
            Func<PrologueAnchor, Func<bool>> near =
                anchor => () => regions.IsInside(anchor.BeatId);

            return new[]
            {
                // P1-01 开场:首 tick 即播(无条件)。
                new PrologueBeat("P1-01", () => true,
                    "光绪二十六年。城北的教堂只剩半座。你在教会义地抬了三年死人 —— 今天抬的是自己的教堂。"),

                // P1-02 石阶:走到门口那一级 + 按 E。
                new PrologueBeat("P1-02", near(PrologueAnchors.Steps),
                    "你每天擦这级台阶。上面刻着:光绪二十六年。",
                    requiresInteract: true),

                // P1-03 告示:门外一步 + 按 E。
                new PrologueBeat("P1-03", near(PrologueAnchors.Notice),
                    "告示上洋文在上、汉文在下:各国侨民,即日撤离。你认得另外两个字:拳、乱。",
                    requiresInteract: true),

                // P1-04 抬埋簿:屋内案头(故意远离进门走廊)+ 按 E。
                //   🔁 2026-11-14:核心道具由「神父的登记簿」改为**主角自己的抬埋簿**
                //      (正典改动 ③)。一百四十七个编号全是**你的**笔迹 —— 你只会写数字和几个姓。
                new PrologueBeat("P1-04", near(PrologueAnchors.Desk),
                    "你的簿子:三年,一百四十七个编号,全是你的笔迹。最后一行墨还没干。" +
                    "你只会写数字和几个姓。",
                    requiresInteract: true),

                // P1-05 神父走:**三样都读过**之后才发生。
                //   ⚠️ 听诊器是**托付的手势**,不是遗弃的动作 —— 遗弃要到第四章才揭晓
                //      (名单末尾「留下他们」= 这批不带了)。**手势是真的,注解不是。**
                new PrologueBeat("P1-05", () => true,
                    "那天晚上,他把听诊器挂在你脖子上,说了半句洋文。你只听懂两个词:留下,他们。",
                    requires: new[] { "P1-02", "P1-03", "P1-04" }),

                // P1-06 那一夜(N2 引入,2026-11-14):纯台词拍。
                //   ⚠️ 脚本的呈现是「黑底 + 推门声 + 镜头固定约 20 s」,**那些归 N3**;
                //      本拍现在只有旁白。**推进时间到夜同样未做** —— 它要么是一次 sim tick 跳变
                //      (病人在「一夜」里该不该恶化?没人裁过),要么是纯呈现的昼夜外观,
                //      两条都不是本拍能自决的。见 `chapter-plan.md` §四。
                new PrologueBeat("P1-06", () => true,
                    "那一夜。天亮的时候,孩子哭了。你没有哭。",
                    requires: "P1-05"),

                // P1-07 十七个人(N2 的第一个真实动作):**人群出现**。
                //   ⚠️ 人群不是病人(§0.1 裁定)⇒ 纯视觉,不写三流、不建 `PatientState`。
                //   ⚠️ 本拍**没有台词** —— 脚本只写了「人群中三个可交互(各一张教学纸)」,
                //      而那三张纸是 P1-07 自己的内容,尚未接。人群出现本身就是这一拍的内容。
                new PrologueBeat("P1-07", () => true, null,
                    requires: "P1-06", effect: onSeventeenArrive),
            };
        }

        /// <summary>
        /// 组装第二段(上路 · P2-01 … P2-09)的节拍。
        /// </summary>
        /// <param name="regions">区域触发驱动(第一段与第二段的区域**同放一张表**)。</param>
        /// <param name="factOf">事件事实计数(`PrologueFactCounter.Of`)。</param>
        /// <param name="onChildArrives">P2-02:王嫂的孩子出现(脚本产病人)。</param>
        /// <param name="onPorterArrives">P2-07:乙 · 挑夫出现(脚本产病人)。</param>
        /// <remarks>
        /// <para><b>2026-11-14 首建。</b>判据全部来自脚本 §第二段,没有一处自拟。</para>
        /// <para>⚠️ <b>P2-03(砸石头)刻意不在表里</b> —— 它的触发是**玩家按 `Attack`**,
        /// 而节拍驱动的条件闭包没有输入形参。硬把它写成 `() => true` 会让它在 P2-02 之后
        /// 下一 tick 就"完成",等于把一场战斗演成一句旁白。归 27 的敌人/战斗落地后再接。</para>
        /// <para>⚠️ <b>52 侧：门读真值，但「遇狗」本体仍未验</b>。`TD-randomevents-05`（三项旗标硬编码
        /// false）已于 <b>2026-11-05 结案</b>，威胁档链于 2026-11-11 接线，本轮的
        /// `random_event_context_gate_test` 把「门读的是玩家真格」钉成了夹具（变异 = 把
        /// `playerActorId` 退回 `NoPlayer` ⇒ 该夹具当场红）。<b>仍不成立的是两件</b>：
        /// ① 池里 10 条 `handler = encounter` 条目全部 `is_house_call = false` ⇒ `IsOnExpedition`
        /// 恒 false（`TD-randomevents-06`，等 37 回填内容）；
        /// ② 一条 encounter **降临之后**是否真的长出野狗，归 27 的遭遇实体，本表与上述夹具都不作证明。</para>
        /// <para>⚠️ P2-01 的判据写的是「`ResourceHarvested` ≥ 5」,**不是**脚本原话的
        /// 「5 个资源点全采完」。差别在关卡有 8 个点(3 止血草 + 5 木柱):按事件计数会把
        /// 采三株草算进去。**这是本表唯一的近似**,精化(改成查背包里木柱 ≥ 5)归 20/23 的读面。</para>
        /// </remarks>
        public static IReadOnlyList<PrologueBeat> BuildSecondAct(
            RegionTriggerDriver regions,
            Func<EventKind, int> factOf,
            Action onChildArrives,
            Action onPorterArrives)
        {
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            if (factOf == null) throw new ArgumentNullException(nameof(factOf));
            if (onChildArrives == null) throw new ArgumentNullException(nameof(onChildArrives));
            if (onPorterArrives == null) throw new ArgumentNullException(nameof(onPorterArrives));

            return new[]
            {
                // P2-01 拾材料 ×5:采满五件。
                new PrologueBeat("P2-01", () => factOf(EventKind.ResourceHarvested) >= 5,
                    "门板,房梁,一块布。你把它们捆在一起,才发现自己已经很久没为什么东西弯过腰了。",
                    requires: "P1-07"),

                // P2-02 野狗:走到路上那一段 ⇒ 孩子出现(被咬的是他 —— 狗的**攻击**归 52 / 27)。
                new PrologueBeat("P2-02", () => regions.IsInside(SecondActAnchors.DogBeatId), null,
                    requires: "P2-01", effect: onChildArrives),

                // P2-04 第一次包扎:10 的止血真的落流。
                new PrologueBeat("P2-04", () => factOf(EventKind.EmergencyTreatmentApplied) >= 1,
                    "你只会缠布。你把他抱起来的时候,手在抖,但布缠得很紧。",
                    requires: "P2-02"),

                // P2-05 回城:走进城市入口(x ≤ 31)。旁白逐字取自脚本。
                new PrologueBeat("P2-05", () => regions.IsInside(SecondActAnchors.CityGateBeatId),
                    "你只会缠布。你答不上「这是什么病」。你需要一本书。",
                    requires: "P2-04"),

                // P2-06 地痞:进药铺废墟的范围。对话本体归 52(今日不响,见 remarks)。
                new PrologueBeat("P2-06", () => regions.IsInside(SecondActAnchors.DrugshopBeatId), null,
                    requires: "P2-05"),

                // P2-07 挑夫:药铺旁的墙角 + 按 E ⇒ 乙 · 挑夫出现。
                new PrologueBeat("P2-07", () => regions.IsInside(SecondActAnchors.PorterBeatId), null,
                    requires: "P2-06", requiresInteract: true, effect: onPorterArrives),

                // P2-08 半本手册:进救助站 + 按 E。
                new PrologueBeat("P2-08", () => regions.IsInside(SecondActAnchors.AidStationBeatId), null,
                    requires: "P2-07", requiresInteract: true),

                // P2-09 回程:回到营地(x ≥ 96)。
                new PrologueBeat("P2-09", () => regions.IsInside(SecondActAnchors.CampReturnBeatId), null,
                    requires: "P2-08"),
            };
        }
        /// <summary>
        /// 组装第三段(落脚 · P3-01 … P3-05)的节拍。
        /// </summary>
        /// <param name="regions">区域触发驱动(三段同放一张表)。</param>
        /// <param name="factOf">事件事实计数。</param>
        /// <param name="placedOf">按模块统计的放置数(帐篷建成判据)。</param>
        /// <remarks>
        /// <para><b>2026-11-14 首建。</b></para>
        /// <para>⚠️ <b>P3-02a 刻意不在表里</b>:它是**按 Enter 的即时反馈**(「还缺 N 件。」),
        /// 不是一次性节拍 —— 同一屏上按两次就该提示两次,而节拍驱动的闩锁会让第二次静默。
        /// 它已由 <see cref="BuildShortfallHint"/> 承载(N4)。</para>
        /// <para>⚠️ <b>P3-03(听诊第一课)不在表里</b>:陈婆婆是「咳」的在场者,而她要不要被
        /// 建成**病人**是内容裁定(甲′ 只裁了孩子与挑夫)。没有那个裁定,写出来的条件只能靠猜。</para>
        /// <para>⚠️ P3-05 的判据写 `CaseOpened`(**立案**),而脚本原话是「被看过一次」——
        /// 本仓的立案唯一触发就是「玩家对病人的交互」(`BootRoot` 4.5 段),故二者同事件。
        /// 若将来立案改成别处触发,这里要跟着改。</para>
        /// </remarks>
        public static IReadOnlyList<PrologueBeat> BuildThirdAct(
            RegionTriggerDriver regions,
            Func<EventKind, int> factOf,
            Func<int, int> placedOf)
        {
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            if (factOf == null) throw new ArgumentNullException(nameof(factOf));
            if (placedOf == null) throw new ArgumentNullException(nameof(placedOf));

            return new[]
            {
                // P3-01 到营地:走进田野营地范围。旁白逐字取自脚本。
                new PrologueBeat("P3-01", () => regions.IsInside(ThirdActAnchors.CampBeatId),
                    "一片被弃的田地。几棵树。",
                    requires: "P2-09"),

                // P3-02b 搭建:帐篷(模块 10)真的落了流。
                new PrologueBeat("P3-02b",
                    () => placedOf(ThirdActAnchors.TentModuleId) >= 1,
                    "四根柱子,一块布。它立起来的时候,你才知道自己要在这儿待多久。",
                    requires: "P3-01"),

                // P3-04 人越来越多:第一个病人出现(阶段门在帐篷建成后解除)。
                new PrologueBeat("P3-04", () => factOf(EventKind.DiseaseOnset) >= 1, null,
                    requires: "P3-02b"),

                // P3-05 序章结束:第一个病人被看过一次(= 立案)。
                new PrologueBeat("P3-05", () => factOf(EventKind.CaseOpened) >= 1,
                    "序章 · 神父走了", requires: "P3-04"),
            };
        }
    }

    /// <summary>第三段的区域与常量(2026-11-14)。</summary>
    public static class ThirdActAnchors
    {
        /// <summary>营地区域触发器的 id(x ≥ 96,全 z 带;与 P2-09 同矩形但独立 id)。</summary>
        public const string CampBeatId = "P3-01";

        /// <summary>帐篷模块 id(`build_modules.json` 的 `Tent` = 10,cost 5)。</summary>
        public const int TentModuleId = 10;

        /// <summary>第三段的区域表。</summary>
        public static RegionTrigger[] BuildRegions() => new[]
        {
            new RegionTrigger(CampBeatId, SecondActAnchors.CampXMin, 127, 0, PrologueBeats.ChurchZ1),
        };
    }

    /// <summary>
    /// 第二段的区域(2026-11-14)。
    /// <para>⚠️ **药铺废墟 / 救助站 / 营地火堆三处不是自拟坐标** —— 它们是关卡逻辑层里已有的
    /// POI(`world_poi.json` 的 1 / 2 / 3)。坐标为副本,故有夹具钉「与烘焙数据逐格相同」
    /// (`prologue_second_act_test`),漂移即红。</para>
    /// <para>⚠️ 野狗段与墙角两处**没有**对应的关卡对象(野狗属 52 池、墙角是叙事位),
    /// 这两处是节拍表自己的锚点,登记在此不假装是数据。</para>
    /// </summary>
    public static class SecondActAnchors
    {
        /// <summary>野狗段(P2-02):路上 x≈44。半径 3 —— 玩家沿路走必经过。</summary>
        public const int DogX = 44;
        /// <summary>见 <see cref="DogX"/>。</summary>
        public const int DogZ = 22;

        /// <summary>城市入口(P2-05 / P2-09 的反向):x ≤ 31,全 z 带(脚本逐字「x ≤ 31」)。</summary>
        public const int CityGateXMax = 31;

        /// <summary>药铺废墟(P2-06):关卡 POI 1。</summary>
        public const int DrugshopX = 24;
        /// <summary>见 <see cref="DrugshopX"/>。</summary>
        public const int DrugshopZ = 8;

        /// <summary>墙角(P2-07):药铺东侧一格,叙事位(关卡无对象)。</summary>
        public const int PorterX = 25;
        /// <summary>见 <see cref="PorterX"/>。</summary>
        public const int PorterZ = 9;

        /// <summary>坍塌救助站(P2-08):关卡 POI 2。</summary>
        public const int AidStationX = 8;
        /// <summary>见 <see cref="AidStationX"/>。</summary>
        public const int AidStationZ = 8;

        /// <summary>营地(P2-09):x ≥ 96,全 z 带。</summary>
        public const int CampXMin = 96;

        /// <summary>P2-02 的触发器 id。</summary>
        public const string DogBeatId = "P2-02";
        /// <summary>P2-05 的触发器 id。</summary>
        public const string CityGateBeatId = "P2-05";
        /// <summary>P2-06 的触发器 id。</summary>
        public const string DrugshopBeatId = "P2-06";
        /// <summary>P2-07 的触发器 id。</summary>
        public const string PorterBeatId = "P2-07";
        /// <summary>P2-08 的触发器 id。</summary>
        public const string AidStationBeatId = "P2-08";
        /// <summary>P2-09 的触发器 id。</summary>
        public const string CampReturnBeatId = "P2-09";

        /// <summary>第二段的区域表(**与第一段同放一张表** —— 一个驱动只该有一份成员集)。</summary>
        public static RegionTrigger[] BuildRegions() => new[]
        {
            RegionTrigger.Around(DogBeatId, DogX, DogZ, radius: 3),
            new RegionTrigger(CityGateBeatId, 0, CityGateXMax, 0, PrologueBeats.ChurchZ1),
            RegionTrigger.Around(DrugshopBeatId, DrugshopX, DrugshopZ, radius: 2),
            RegionTrigger.Around(PorterBeatId, PorterX, PorterZ, radius: 1),
            RegionTrigger.Around(AidStationBeatId, AidStationX, AidStationZ, radius: 1),
            new RegionTrigger(CampReturnBeatId, CampXMin, 127, 0, PrologueBeats.ChurchZ1),
        };
    }
}
