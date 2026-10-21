# Sprint 14 — 44 运行期音频链接通

**周期**:不设结束日期(承 sprint-12/13 口径)—— **做完即收,收口时补记结束日**
**状态(2026-10-18 收口)**:✅ **已收口(end = 2026-10-18)** —— 11/11 件 done(7 计划 + 4 计划外追加:
S14-8/9 提前兑现 · S14-10 装配补装+听测 · S14-11 戥子);DoD 8 条逐条处置见下。
⚠️ **merge 归用户自行执行**(2026-10-18 裁定「merge 我自己来」)—— 本文件所在分支
`fix/playmode-datacore-precheck` 领先 main,收口账面两笔 = `d389276`(story-018 补评批)
+ `bc80cfb`(story-019 立卡)。⚠️ **sprint-14 收口 ≠ epic 44 收口** —— 后者仍 ⛔
(`TD-audio-101…108` 未清,详见 `audio-system/EPIC.md` 头部 2026-10-18 刷账注)。
**规划依据**:`production/retrospectives/retro-sprint-13-2026-10-16.md`(行动项 1–4)
+ `production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md` §4(须实现生产类清单 · 依赖序)
**前置勘察(Phase 1 硬前置)**:已重扫 44 epic 全部 16 条 story 的 `Status:` ——
**007–012 六条现为 `In Review`**(2026-10-16 由 S13-9 撤回)·
001–006/013/014 `Complete`(未普查,行动项 3)· 015/016 `Ready`(素材,界外)。
⚠️ `audio-system/EPIC.md` 的 stories 表 6 行**仍写 `Complete ✅`**(stale)—— 刻意为之,
reconcile 归 S14-7(避免 epic 表与 story 头在实现期反复不同步)。

> **🔁 2026-10-18 勘察快照失效注**:上段是 **2026-10-16 规划时点**的真值,保留为记录。
> 现状:007–012 已由 S14-1…6 落地 + **S14-7 复评批(2026-10-16)转 `Complete`**,EPIC 6 行已 reconcile;
> 015/016/017 亦 `Complete`;**018 由「计划外追加」于 2026-10-18 立件并结案**(S14-10)
> ⇒ **001–018 全 `Complete`**。**⚠️ 但 epic 本身仍 ⛔ 不得收口** —— DoD 第 2 条
> 「36 条 AC 全部验证」的残余面见 `TD-audio-100…109`(本批只结 `AC-44-09` 听测半边一条 + 链路接通)。

## Sprint Goal

把 44 音频 epic 的 6 条 `In Review` story 从「**契约 + 门 + 测试就绪,但生产实现不存在**」
推到「**运行期链路真实可播,AC 语义层第一次被生产代码保证**」——
即兑现评审原件 §4 的须实现清单,并经证伪轮复评后转 `Complete`。

> **本 sprint 不做**:
> ① 素材本体接入(归 story-015/016,仍 `Ready`);
> ② 听测类 AC(`AC-11-21` / `AC-44-09` 听测半边)—— **仍不可判,禁借绿**;
> ③ 44 epic 001–006/013/014 的假绿普查(retro-13 行动项 3,另轮)。
>
> **🔁 2026-10-18 就地订正(原「不做」清单的三条现状态均已变,原文保留不删)**:
> ① ⇒ **已做** —— story-015 / 016 于 **2026-10-17** 提前兑现(S14-8 / S14-9),两条均 `Complete`;
> ② ⇒ **已做** —— 阻塞根不是「素材未接入」而是**运行期链路未通**(`TD-audio-023`),
> 由 S14-10 / story-018 于 10-18 补装 + 用户实机听测 ⇒ **`AC-11-21` 与 `AC-44-09` 听测半边同批 ✅**;
> ⚠️ **仅此两条** —— 其余音频 `[L]` 半边(`AC-44-02/03/04/10/11/12/13/15/17` + 联机对拍 + DSP P95)
> **仍不可判或未执行**,`AC-44-03` 的阻塞仍是 12 个 `vo_` 变体**未录制** ⇒ **44 epic 仍 ⛔ 不得收口**
> (`TD-audio-100…109` 逐条未动);
> ②-b **听测批同时新立 `TD-audio-027`**(听测在编辑器 Mixer 窗口牵出 `.mixer` 副作用 ⇒ 暴露
> **AC② 门不覆盖「快照捕获 `Master` 原始电平」**)—— 提交前复跑 `MixerTopology` EditMode **21/21** +
> `AudioWiringTest` PlayMode **5/5** 通过,故**不阻塞收口**,但**不得**把该改动读成任何 AC 的证据。
> 详 `impl-s18-*` §十。
> ③ ⇒ **仍未做**,另轮(不受本批影响)。

## Capacity

- **不用 `estimate_days`**(依 `production/CAPACITY-MODEL.md`)· 复杂度 = S/M/L
- **件/日基线**:sprint-12 = 10 · sprint-13 = 8 ⇒ 本 sprint 按 **8–10 件/日**监督
- **并行纪律**:同时在跑 ≤ 3 件;超限即拆分或顺延讨论
- **本 sprint 预算**:Must 3 件(2×L + 1×M)+ Should 3 件(1×S + 2×M)+ Nice 1 件(1×M)
  —— ⚠️ **含 2 个 L 件,是本 sprint 最大风险**(见 Risks)

## Tasks

### Must Have (Critical Path —— Wave 1 + 2)

| ID | Task | Story | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|-------|-------|-----|--------------|-------------------|
| **S14-1** | **008 生产实现**:`LatticeToWorld` · `cell_jitter` · `rank_key` · **44 声源池**(ADR-028 唯一所有者)· 遮挡驱动器 · 空间化装配入口 | story-008 | Claude | **L** | 无 | ① 20 测由自造 Fake 换成真生产类 ② **`FakeSourcePool` 内部须有真 `AudioSource`**(AC-44-D8 ② 判据逐字是「N 条独立 `AudioSource`」)③ **证伪轮:停用池 ⇒ ≥3 测红** ④ AC-44-16 的「bus ∈ Ambience = 构建失败」真门半边落地(现真门 `RequiredCues` 只查存在性,`grep Ambience` 零命中) |
| **S14-2** | **007 生产实现**:cue 发射派生器 + **`IAudioCueSink` 生产实现** + 锚点门 + EndLoop 看门狗 | story-007 | Claude | **L** | S14-1 | ① 19 测换真 ② **重排差分哈希的被测物须是派生器本体**(非测试自写 `DeriveCueHash`)③ 新文件含 `AudioCueDto`/`IAudioCueSink`/`AudioCueHandle`/`TierSource` token ⇒ **须声明 44 前缀命名空间**(b5 门 `AssemblyGates.cs:995-1009`)④ `Sim/` 内 `new PayloadRef(` 零(ADR-029) |
| **S14-3** | **009 生产实现**:EndLoop 自评看门狗 · `HandoverMux` 交接 FSM · 句柄生命周期管理器 | story-009 | Claude | **M** | S14-1 | ① 12 测换真 ② `LOOP_EVAL_MAX_TICKS` / `HANDOVER_MS` 落 `AudioTuning` ③ 时钟源 = `ITickProvider`(**禁墙钟**)④ **顺手补 AC 编号**(该 story 4 条 AC 现无 `AC-44-xx` 编号,用中文描述名 —— 独立瑕疵,`AC-44-` 全文仅 1 处命中且是引用 Story 004) |

### Should Have (Wave 3 —— 三件相互独立)

| ID | Task | Story | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|-------|-------|-----|--------------|-------------------|
| **S14-4** | **012 生产实现**:`xfade_ms ≥ MUSIC_XFADE_MIN_MS` 下界门 + 常量 | story-012 | Claude | **S** | 无 | ① 兑现**唯一具名空位** `AudioEventTableGates.ValidateXfade`(XML doc 逐字写「**不做** `≥ MUSIC_XFADE_MIN_MS` 的数值断言(AC-44-18,Story 012)」)② `MUSIC_XFADE_MIN_MS` 落 `AudioTuning` ③ **构建期真门:改种子 `xfade_ms` 至 1999 ⇒ 红** ④ DSP 预算探针(60s 窗 P95)归 evidence 文档,非 EditMode 断言 |
| **S14-5** | **011 生产实现**:`settings_visible` 注册表 + `ISettingsStore` + 归零机制 + sidecar | story-011 | Claude | **M** | 无 | ① 11 测换真 ② `assets/` 下须有 `settings_visible`(现全库 `assets/` 零命中)③ `ISettingsStore` 落 `Gameplay.Presentation`(**禁 `Sim`**,GDD :1204「sim 无关」)④ **恢复 AC-44-13 被删空的「出厂默认 ∈ 烘焙分区」断言** ⑤ sidecar 承 3 规则四模式(先例 `Gameplay.Input/BindingsStore.cs`) |
| **S14-6** | **010 生产实现**:事件表 Baker/Binder/CookedWriter + Addressables 音频组 + 听诊预载门 | story-010 | Claude | **M** | 无 | ① `Editor.Tools.Bake/` 须有 audio 文件(现**零**)② 素材存在性谓词入 ADR-014 阶段 2 ③ 组区分门读 `AddressableAssetSettings`(现全库零门读它)④ **清除测试写仓库 `assets/audio/` 的副作用**(`pipeline_gate_test.cs:213-227` 违 `test-standards.md`「Unit tests must not depend on filesystem」) |

> **⚠️ 实现期问句加注(2026-10-16 · S14-6 收尾)** —— 上表 **S14-4** 的 AC ④ 原文写「构建期真门:改种子 `xfade_ms` 至 1999 ⇒ 红」,**该句是实现期问句(设想),不是判据**。
> 实际落地 = `AudioEventTableGates.ValidateXfade` 的**常量下界比较**,下界 = `AudioTuning.MusicXfadeMinMs`(2000,**单一出处,门不另抄**);判据面 = 「`xfade_ms < 2000` ⇒ 门报错」,同效且更强
> (常量下界门对**任意**越界输入生效,不依赖某一次改种子的样例)。原文保留不改写,加注订正
> (承 ADR-015 修订 ADR-009 先例)。

### Nice to Have

| ID | Task | Story | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|-------|-------|-----|--------------|-------------------|
| **S14-7** | **6 条复评 + 转 `Complete` + 44 EPIC 6 行 reconcile** | 007–012 | Claude | **M** | S14-1…6 | ① 每条:证伪轮全红 + 复评原件落 `production/qa/evidence/` ② EPIC 表 6 行与 story 头**逐字一致** ③ ⚠️ **判据须是变异测试**(停用生产码 ⇒ 断言转红),非「测试名 + 行号」(retro-13 行动项 2) |

## Carryover from Previous Sprint

| Task | Reason | New Estimate |
|------|--------|--------------|
| S13-7 11 人工走查三签 | **agent 不可代劳** —— 需真人 + 三签(项目外部,不可催) | 不占本 sprint 治理容量;~~三签稿已备,待真人~~ ⇒ ✅ **2026-10-17 三签完成** · **2026-10-18 走查重跑改判 A3 ✅ / B2 ✅**(TD-01/02/03 结案)· 同批遗留的**腿 C(`AC-11-21` 听测半边)亦已于 2026-10-18 由 S14-10 实机听测兑现 ⇒ ✅**(`TD-prescription-04` 结案,签核件 §7)⇒ **S13-7 全链结清** |
| —(新)| sprint-13 的 S13-9 产出 = 本 sprint 全部 7 件的**来源** | — |

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| **含 2 个 L 件(S14-1/2),工作量 ≈15 生产类** —— 单 sprint 可能不足 | **高** | 中 | Should 三件可顺延;不硬凑 —— 承 CAPACITY-MODEL「件数与依赖」口径 |
| **008 是根,阻塞 007/009** —— 008 未落地则 Wave 2 无法开工 | 中 | **高** | S14-1 先行;Wave 2 在 S14-1 完成前不开工 |
| **修复轮验收判据若仍是「断言仍绿」⇒ 假绿复现**(retro-13 行动项 2;评审原件 §3 三处实证) | **中** | **高** | 本 sprint 起,「已证」须附**变异测试记录**(停用生产码守卫 ⇒ 断言转红) |
| ~~素材仍在 015/016 界外 ⇒ 听测类 AC(AC-11-21 / AC-44-09 听测半边)仍不可判~~ ⇒ **已消解,且消解路径与原预判不同** | ~~确定~~ **未发生** | 中 | 原缓解「听测仍挂 015/016」**口径有误**:10-17 素材即已齐而链仍无声 ⇒ 真阻塞是 **`TD-audio-023` 运行期装配**,由 S14-10 于 10-18 补装;同日用户实机听测 ⇒ `AC-11-21` / `AC-44-09` 听测半边 **✅**。⚠️ **不外推**:其余音频 `[L]` 半边仍不可判(见 Sprint Goal 注)|

## Dependencies on External Factors

- ~~**S13-7 三签**(主创 / 医学从业 / 音频 lead)—— 项目外部,不可催;本 sprint 观察其进展。~~
  ⇒ ✅ **已结清**:三签 2026-10-17 完成;2026-10-18 走查重跑改判(`AC-11-12`/`AC-11-13` ✅✅,
  `TD-prescription-01/-02/-03` 结案);~~唯 AC-11-21 听测半边转入本 sprint 观察面 = S14-10~~
  ⇒ **S14-10 已结**:2026-10-18 story-018 实机听测签核(`Voice_Cough_Damp` 可闻 / `Voice` 滑块生效 /
  零状态播报)⇒ `AC-11-21` ✅ + `AC-44-09` 听测半边 ✅,`TD-audio-023/-024` / `TD-prescription-04` 结案。
  **S13-7 及其全部转存项至此零余项。**
- 素材本体(story-015/016)—— 不在本 sprint 界内。
- CI 真 runner 环境(`UNITY_LICENSE` 未配)—— 本 sprint 的 IL2CPP 三格仍登记 **NOT-RUN**(禁借绿)。

## Definition of Done for this Sprint

- [x] All Must Have tasks completed(S14-1 + S14-2 + S14-3)✅ 2026-10-16(yaml 三条 done)
- [x] 各任务按衍生验收标准通过,**每条附可证伪判据**
      (**变异测试**:停用生产码守卫 ⇒ 该断言转红;retro-13 行动项 2)
      ⇒ ✅ **判据形态本身已兑现**:S14-1…6 各带证伪轮(007–012 复评批)+ story-018 证伪 14/14
      (F1…F11/P1/P3/P4 全转红,`impl-s18-*` §三)· story-025 证伪(`review-story-025-*` R 族)。
      ⚠️ **如实保留一条反面**:story-018 补评审出「AC-018-4 判别力声称无证伪支撑」(S3-a,14 例无一打
      「自建第二池」分支)⇒ 已挂 **`TD-audio-028`** 还债时补负例 —— 本条因此记「已兑现 + 一处已知欠例」,
      **不记全净**。
- [x] **QA plan 存在**(`production/qa/qa-plan-sprint-14-*.md`)—— 收口前必跑
      ⇒ ✅ 件在盘:`production/qa/qa-plan-sprint-14-2026-10-16.md`(221 行)。
      ⚠️ **其 Smoke 判据「进 Play 能听见声音」的兑现形态 = 用户 2026-10-18 实机听测**(人耳陈述,
      四值记录齐:可闻 / `Voice` 总线 / 滑块生效)—— **无独立 `/smoke-check sprint` 自动冒烟件**;
      按 sprint-13 同款口径登记(「以用户人耳陈述补记为人工冒烟证据,无可复算判据」,承 `TD-s13-03` 先例)。
      ⚠️ 该 plan §Playtest:「本 sprint 全部 Logic / Integration / Config 类,**不需真人 playtest**」⇒ 无此项欠账。
- [x] 6 条 story 由 `In Review` 转 `Complete`(经证伪轮复评)✅ 2026-10-16(S14-7 复评批)
- [x] IL2CPP 三格 **NOT-RUN 如实登记**(禁借绿,挂 ADR-012 矩阵)✅ —— 本文件 §External Factors
      逐字登记 NOT-RUN(本机仅 Mono,`UNITY_LICENSE` 未配真 runner);**未跑 ⇒ 不得标绿**<br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。
- [x] Design documents updated for any deviations ✅(债册 / GDD 补注 / EPIC / ADR 侧散见各批;
      story-018 三处口径差均**就地加订正注不删原文** —— S3-c「默认 SFX」· 听测件 §四 预判被实机推翻注 ·
      AC-44-09 归属合取收窄)
- [x] Code reviewed(`/code-review`)and merged
      ⇒ **reviewed ✅**(2026-10-18 批)· **merged ❌ 归用户自行执行**(本仓「merge 我自己来」裁定)。
      覆盖面如实记:007–012 已由 S14-7 复评批(= 该批的评审形态)+ story-015/025 各有原件 +
      **story-018 由本日补评落原件**;⚠️ **story-016 未走 `/code-review`**(其卡面自述:E5 证据 =
      签字走查记录,代码面由 12 例证伪轮覆盖)⇒ **本条按「已评审(story-016 为已登记的豁免形态)」计**,
      不掩盖该例外。
- [x] **`end` 日期在收口时补记** ⇒ **`end` = 2026-10-18**;若 `start` 与实际执行日不符,**矛盾留档不改写**
      ⇒ ⚠️ **`start` 空白(规划时未记)→ 不追补**(承 sprint-13 同口径);实际执行跨 **2026-10-16 → 2026-10-18** 三日。

> **✅ 收口记录(2026-10-18)**
> - **11 件 done**:计划 7 件(S14-1…7,均 2026-10-16/17)+ **计划外追加 4 件**(S14-8 story-015 与
>   S14-9 story-017 = 2026-10-17 提前兑现素材批 · S14-10 story-018 = 2026-10-18 装配补装 + 实机听测 ·
>   S14-11 story-025 = 2026-10-18 戥子逐档形态,原属 skeuomorphic-ui epic 非本 sprint 面内)。
>   ⚠️ **Sprint Goal 只主张 6 条 `In Review` story 转 `Complete`** —— 追加 4 件属「做完即收」的顺势扩面,
>   非 scope creep(44 epic 界内 + 用户逐件指令)。
> - **Sprint Goal 达成**:6 条 story 由「契约 + 门 + 测试就绪、生产实现不存在」推到
>   「运行期链路真实可播,AC 语义层第一次被生产代码保证」⇒ ✅(S14-1…6 落地 + S14-7 复评转 Complete)。
> - **回归对账(等式判据,非照抄总数)**:EditMode `3682 → 3709 = +27`(story-018 新增测数)·
>   PlayMode `192 → 197 = +5` ⇒ **两棵树新增测上零回归**(`impl-s18-*` §回归对账)。
>   ⚠️ 首跑的 1 条既有间歇红 = **`TD-player-01`**(`AC-1-33` ③ 语义层未验证)的下游症状,**非本 sprint 引入**。
> - **收口账面两笔提交**:`d389276`(story-018 补评 + `TD-audio-028…033` 立案 + `TD-audio-106/-109` 刷账)
>   · `bc80cfb`(story-019 立卡兑 `TD-audio-100`)。
> - **merge**:**未做,归用户**(分支 `fix/playmode-datacore-precheck` 领先 main)。
> - **⚠️ 本 sprint 收口不代表 epic 44 收口**:epic 44 维持 **⛔**(16 条未全验证 AC 的残余面 ——
>   `TD-audio-101/102` 听测排期 · `103` 被试 ≥2 人 · `104` P1b · `105` 无障碍走查 · `107` 45 层 ·
>   `108` 目标硬件;**另本批新立 `TD-audio-028…033` 六条链路未闭环**)。
> - ~~**收口时仍未做的一件(如实,不留白)**:**`/retrospective sprint-14` 未写**(前三个 sprint 均有复盘,
>   且 sprint-14 规划依据 = retro-13 的行动项 ⇒ 行动项兑现核查该由 retro-14 做)。
>   非 DoD 条目 ⇒ **不阻断收口**,列为下一步。~~
>   ✅ **2026-10-18 已补写**(列为下一步的当日兑现):`production/retrospectives/retro-sprint-14-2026-10-18.md`。
>   **该 retro 的三个核心结论**(原文不删,留作闭环记录):
>   ① **retro-13 四项行动项只兑现 2/4** —— 行动项 2(证伪轮门)全面落地;行动项 1(链接通)达成但**被证成两层**;
>   **行动项 3(006/013/014 假绿普查)与 4(跨 epic 抽查)仍未做,而同型样本已累积 3 个**;
>   ② **本 sprint 抓出的问题多于修完的问题** —— 收口新立 7 条债,其中 6 条是「代码在、语义层没接」,TODO 计数对它们完全失明;
>   ③ **件/日基线已不可跨 sprint 比**(3.7 vs 基线 8–10,因件变大非产能降)⇒ 需重标定 `CAPACITY-MODEL.md`。
>   ⚠️ 该 retro 起草时在 TODO 计数与提交计数上**各犯一次口径误读并已就地留档**(最终以 `rev-list --count` / 递归 grep 实测为准)。

> **Scope check**:本 sprint 全部 7 件**均在 44 epic 已有 story 的界内**(007–012),
> **不新增 epic 范围** —— 是「把已登记但未实现的东西做出来」,非 scope creep。
