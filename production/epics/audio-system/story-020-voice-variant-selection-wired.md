# Story 020: 语声变体选择接进运行期(AC-44-02「变体库混合」的运行期半边)

> **Epic**: 音频系统
> **Status**: Complete(实现轮 + 重裁轮 2026-10-18;评审 3 BLOCKING 已结,残留归 `TD-audio-034`)
> **Layer**: Foundation(44 呈现层的选择语义;不改 sim、不进三流)
> **Type**: Integration
> **Estimate**: 1 人日
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-18

## Context

**GDD**: `design/gdd/audio-system.md` —— AC-44-02(语声**变体库混合**,非参数调制)
**Requirement**: `TR-diag-022`(**运行期半边**;`docs/architecture/tr-registry.yaml:1338-1344`
的 `status: covered` 兑现的是**素材/库面** —— 变体行存在 + 分桶边界解耦 + 烘焙门
`ValidateVoiceVariantLib` —— **不含运行期选择器**,读者**不得**据它推定 AC-44-02 已落地)
**缺口来源**: `TD-audio-030`(`docs/tech-debt-register.md` §「2026-10-18 · story-018 补评批」)
**ADR Governing Implementation**: ADR-018 §一(44 只触发 / 只渲染)· ADR-014 §五(烘焙数据读取面)
**前置事实(2026-10-18 重扫)**:12 个 `vo_patient_cough_damp_{m,f}{1,2,3}_{1,2}.wav` **已全部落地**
(`unity/Assets/Audio/`,事件表 `Voice_Cough_Damp.assets[]` 12 条已列)⇒ **本件的阻塞不是素材,是选择器**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 选择逻辑 + 既有 `AudioClipLibrary` 读取面改造;零新 `AudioClip`、零引擎 API。

### 判据事实(实测,本件的宾语)

| 实测 | 出处 |
|---|---|
| `AudioClipLibrary.cs:84` 对多素材行**恒取 `row.Assets[0]`**,头注自陈「这不是随机挑一条 —— 是确定性的第 0 条」(**装载面**语义,非选择器) | `TD-audio-030` 实测 |
| `VoiceVariantLib`(分桶 / `SelectVariant` / 烘焙门 `ValidateVoiceVariantLib`)**生产零调用** —— 全仓 grep 仅命中其自件与注释 | 同上 |
| 12 个变体文件**已在场**且事件表已列 | 2026-10-18 重扫,本卡 |

### 边界裁定

1. **只接线,不改选择语义的数值**:分桶边界 / 变体量(`OQ-44-2` 已结)× **数值归用户**,本件不改。
2. **选择必须是确定性的**:同一 `(CueId, Tier, 患者身份, tick)` ⇒ 同一变体(可重放);
   **禁墙钟 / 禁 `UnityEngine.Random`**(承 ADR-016 §一 三源不变量在呈现侧的同型口径)。
3. **不改 `AudioCueDto` 形状**、不改事件表 schema。
4. 本件**不是** AC-44-03 的 `[L]` 听测:那归 `TD-audio-102`(user 实听),本件只让「被听的对象」真的在变。

## Tasks

- [x] **AC-020-1**[A]: `AudioClipLibrary` 多素材行的取用路径由「恒取 `Assets[0]`」改为
  **经 `VoiceVariantLib.SelectVariant` 的选择器**;判据宾语 = **真生产类**,
  **禁测试内联自造选择器**(承假绿复核病根)。
  **证伪判据**:把取用回退为恒第 0 条 ⇒ 本断言转红。
- [x] **AC-020-2**[A]: 选择键 = `(CueId × Tier × 桶 × 患者身份)`,且**确定性**
  (同输入两次调用 ⇒ 同变体;不同患者在同一 Tier 下**不得**恒同一条 —— 给出分布判据,
  禁「随机」措辞)。
  **证伪判据**:把身份维度从键里删掉 ⇒ 分布断言红。
  > ⚠️ **2026-10-18 重裁订正两处**(原文两错):
  > ① 原写「选择键 = `(CueId × Tier × 桶)`」**不含身份**,与同段「桶的派生输入来自患者身份」
  > 自相矛盾 —— 已补全为四元组。
  > ② 原证伪判据「把身份维度从键里删掉」**不成立**:身份经**两条路径**进选择
  > (桶 + 槽内位置),单删一条**不红**。实测矩阵:F3(只删槽内)绿 · F9(桶→常量)绿 ·
  > F10(桶→固定桶)绿 · **F11(两条全删)才红×2**。⇒ 该判据的真实形态是「**整体抽掉身份维**」,
  > 不是「删一条」。**重裁 ③ 后立方体只有一条路径**,判据恢复为单点可证伪(F2 红×6)。
- [x] **AC-020-3**[A]: 12 个 `vo_` 变体**全部可达**(逐个可被选中至少一次)——
  断言宾语 = 真种子事件表 + 真 `VoiceVariantLib`,**禁夹具内联清单**。
  **证伪判据**:让选择器只覆盖前 6 条 ⇒ 本条红。
- [x] **AC-020-4**(账面): `TD-audio-030` 结案注(原判定 → 修复落点 → 验证命令);
  `TR-diag-022` 补**运行期半边**判据引用。
  ⚠️ **本卡不重算 epic 44 的「16 条未全验证」计数**(承 2026-10-18 听测批同口径)。

## Dependencies

- 素材:✅ 12 个 `vo_` 文件已在场(2026-10-17 落地)
- `VoiceVariantLib` / `AudioClipLibrary` / 事件表烘焙管线(S14-6)—— 均已在盘
- **下游**:`TD-audio-101/102` 的听测批**宜排在本件之后**(否则听的是恒取 `Assets[0]` 的形态)

## Out of Scope

- AC-44-03 的 `[L]` 听测(`TD-audio-102`,用户实听);
- 分桶边界 / 变体量的数值调整(归用户数值轮);
- `TD-audio-033`(`knowsClinic` 硬编码)—— 同住 `PatientCueDriver` 但判据不同,独立成卡(story-021)。

## Test Evidence

- 主件:`unity/Assets/Tests/EditMode/Audio/` 下新增/扩测(真 `AudioClipLibrary` + 真 `VoiceVariantLib` + 真种子);
- 证伪轮:逐条「停用什么 ⇒ 哪条红」登记在 `impl-s20-*.md`(证据原件,承 S12-4 门)。

## Definition of Done(本卡)

- AC-020-1…3 全 [A] 绿 + 证伪转红记录齐;
- `TD-audio-030` 结案注落账,`TR-diag-022` 运行期半边判据登记;
- ⚠️ **44 epic 仍不得收口**(本卡只动 `TD-audio-030` 一条)。

---

## Completion Notes(实现轮 2026-10-18)

> ⚠️ **2026-10-18 重裁覆盖本节部分内容** —— 首版实现经评审 + 用户重裁三项后已大改。
> **现行交付以下文「重裁轮」为准**;本节保留作历史记录(首版的 `BucketBand` / `SelectSlot` /
> `VariantTableOf` / `VoiceBucketOf` 四个符号**已删除**,首版 F1–F8 反证随之作废)。
> 原件:`production/qa/evidence/review-s20-voice-variant-selection-2026-10-18.md`。



**交付**:`AudioClipLibrary` 逐条装行内全部 `assets[]` + `Resolve(cue, tableCueId, tier, identity)`
经真 `VoiceVariantLib.SelectVariant` / `SelectSlot` 选变体;`ResolveCue(in AudioCueDto)` 取 `Source`
作身份;`AudioSourceRenderTarget` 增整条 cue 解析缝(既有 `Func<int, AudioClip>` 保留,62 处调用零改动);
组合根改走该路径。`VoiceVariantLib` 新增 `BucketBand` / `IdentityScalar` / `SelectSlot`(纯整数,零随机源)。

**测试**:`unity/Assets/Tests/EditMode/Audio/voice_selection_wiring_test.cs` **9 测**(新)。
断言宾语 = 真 `AudioClipLibrary` + 真 `VoiceVariantLib` + 真种子事件表;唯一替身 = 生产代码自己声明的
装载缝(`Preload(loader:)`)。**无测试内联选择器**。

**证伪**:F1–F8 八例全转红;一处失败尝试(F8-a 对调解析缝优先级 ⇒ 不红)已如实记录 ——
真正承重的是 `ResolveCue` 的身份来源(F7)。

**回归**:全量 EditMode `3757 / 3712 / 0 / 45`(`unity/Logs/s20-full-editmode.xml`),
对 S19 基线 `3748 / 3703 / 0 / 45` ⇒ **+9 = 新增 9 条**,等式严丝合缝。音频全域 `492/492` 零回归。

**账面**:`TD-audio-030` 已结案(含「人耳半边未证」的残留声明);`TR-diag-022` note 已补运行期半边判据。
**未重算** epic 44 的「16 条未全验证」计数(承本卡边界)。

**⚠️ 未证(不借绿)**:AC-44-02 的**人耳听测**(`TD-audio-102`);真 Addressables 装载 12 条变体的
PlayMode 夹具(现存 PlayMode 测只断言行数);`assets[]` 重排无专门构建期门;IL2CPP 三格。
原件:`production/qa/evidence/impl-s20-voice-variant-selection-2026-10-18.md`。

---

## Completion Notes(重裁轮 2026-10-18 · **现行**)

**触发**:首版评审出 **3 条 BLOCKING**(Unity 专家 B1/B2 + 编排方实算补的 B3),用户裁定重裁三项。

**三条 BLOCKING**:
- **B1** 桶的输入轴与 GDD 不符 —— 用了患者身份,而 `audio-system.md:362` 定的是 `AudioCueDto.Intensity`;
  该字段在生产选择路径**零消费**(只有写侧 `CueEmitDeriver` 塞进 DTO)。
- **B2** 末槽截余与槽宽定义打架 —— 真种子表实算:`4` 条行索引 3 **永不可达**;
  `2` 条行**强桶为空** ⇒ `SelectVariant` 返 −1 ⇒ 槽钳回 0 ⇒ **强强度听到最弱素材**。
  `AC-020-3` 全绿是因为只测了 12 条那一行(恰好 `%3==0`)。
- **B3**(评审未报,编排方实算):**把语义不同的素材当成同义变体**。真种子 8 行多素材里**只有 1 行是变体**:
  `World_Threat` = 狗/马/锣/鸟(GDD `:72` 逐字并列的**四种不同事件**)、`Footstep_Base` = 四种**地表材质**
  (GDD `:817`)、`Steth_Contact` = 胸/背、`Page_Turn` = 前翻/后翻、`Cabinet_Prop` = 三种物体。
  8 行 wav **全部在场** ⇒ 生产上患者**可能听到马而不是狗**、翻书**往后翻**、**雪地响石头脚步**。

**重裁与修复(用户裁定 ①②③)**:
- **① 变体轴显式声明** —— 新字段 `variant_axis`(构建期白名单 + 闭集 + 三判门);
  **只有声明过的行才走变体选择**,未声明的行**恒取 `assets[0]`**(= 本卡改造前的行为)。
  真种子只给 `Voice_Cough_Damp` 声明。贯穿 8 层:数据 → 门白名单 → 门闭集 → 门逻辑 →
  binder → 绑定行 → cooked 写 → cooked 读 → 运行期行。
- **② 桶输入改回 `Intensity`** —— **本卡未落**,登记 **`TD-audio-034`**。理由:该输入轴在语声 cue 上
  **未接线**(13 的产侧已产 `PatientCue.Intensity`,但 `ResolveCue` 没往下传),接线属 13 的轮次;
  且本卡边界裁定 3 明写「不改 `AudioCueDto` 形状」。**当前实现仍以身份作桶输入,已在代码注释如实标注**。
- **③ 槽宽改本槽实际宽度** —— 改为**三维立方体**:`VariantCubeSize` = `total % 3 == 0 ? total/3 : -1`
  (**不猜、不截余**),`SelectVariantCube` = `桶 × size + 层内位置`。素材数整除 3 由构建期门保证
  ⇒ 三桶恒等宽 ⇒ **无死变体、无空桶**。首版四符号(`BucketBand`/`SelectSlot`/`VariantTableOf`/`VoiceBucketOf`)全删。

**自捕的一处自证循环**:重裁中我先把测试期望值改成调 `ExpectedAddress`,而它内部调的就是
`Resolve` 用的同一个 `SelectVariantCube` ⇒ **两边同源,选择器怎么坏都相等**。
已删 `ExpectedAddress`,改为核对「选中位置」这一可独立断言的事实。

**证伪(F1–F4,全转红)**:删 `!row.IsVariantRow` 守卫 ⇒ 红×1 · 变体行也短路 ⇒ 红×6 ·
`VariantCubeSize` 退回截余 ⇒ 红×1 · 停用整除判据 ⇒ 红×1。
⚠️ **首版 F1–F8 已作废** —— 那批打的是被重裁删掉的选择语义,留着会误导后来人。

**回归**:全量 **`3769 / 3724 / 0 / 45`**,exit 0(`unity/Logs/s20-reselect-full-editmode.xml`)。
对 S21 基线 `3765 / 3720 / 0 / 45` ⇒ `+4` = 新增 4 条 `[Test]`;`passed +4`,严丝合缝。
(中间轮曾出现 1 红 = `MovementFeedTest` 已知间歇 `TD-player-01`,单跑 5/5 绿,终轮未复现。)
音频全域 `505 / 505 / 0`(首版 492 ⇒ +13 = 9 旧 + 4 新)。
**cooked 产物已重烘**(3241 → 3323 B)—— 改行布局后 5 条 `StateReportWalkthroughProbe` 曾报 E-13 越界,重烘后 13/13 绿。

**⚠️ 未落 / 未证(禁借绿)**:桶输入 `Intensity`(`TD-audio-034`)· gender 轴未接线(同批)· 人耳听测
(`TD-audio-102`)· 真 Addressables 装载 12 条 · IL2CPP 三格。
原件:`production/qa/evidence/review-s20-voice-variant-selection-2026-10-18.md`。
