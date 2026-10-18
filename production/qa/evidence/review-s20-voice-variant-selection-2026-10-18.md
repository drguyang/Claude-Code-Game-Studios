# 评审 + 重裁原件 · story-020(语声变体选择接线 · S15-2)

> **对象**:`production/epics/audio-system/story-020-voice-variant-selection-wired.md`
> **代码面**:`Gameplay.Presentation/Audio/{AudioClipLibrary,VoiceVariantLib,AudioEventDataSet,AudioEventCookedCodec}.cs`
> · `Editor.Tools.Bake/{AudioEventTableBinder,AudioEventTableCookedWriter}.cs`
> · `Editor.Tools.Gates/AudioEventTableGates.cs` · `assets/data/audio_events.json`
> · `Tests/EditMode/Audio/voice_selection_wiring_test.cs`
> **日期**:2026-10-18
> **方式**:两路并行评审(Unity/C# 专家 + QA 可测性)+ 编排方独立实算 + 用户三项重裁
> **原件性质**:本件评的是**评审当时**的代码(首版 `776ecaf`);重裁后的修复在 §五 逐条结账。

---

## §一 裁定汇总

| 阶段 | BLOCKING | WARNING | INFO |
|---|---|---|---|
| 首轮评审(2026-10-18) | **3**(B1/B2 + 编排方补的 B3) | 8 | 6 |
| 重裁后(用户裁定 ①②③) | **0** | 8(全部登记,不在本卡面内) | 6 |

**用户重裁三项(2026-10-18)**:① 变体轴显式声明 · ② 桶输入改回 `Intensity`(本卡只登记债)
· ③ 槽宽改本槽实际宽度。

---

## §二 BLOCKING 明细

### B1 · 桶的输入轴与 GDD 不符:用了患者身份,弃了 `AudioCueDto.Intensity`

- **落点**:`AudioClipLibrary.cs` 首版 `VoiceBucketOf(int identity)`
- **GDD 原文**:`design/gdd/audio-system.md:362` —— `variant = SelectVariant(CueId, Tier, bucket(u))`,
  `u` = `AudioCueDto.Intensity`;`:370` 强度桶「来自 `AudioCueDto.Intensity` → F-44.6 分桶」
- **实测**:生产树 `grep "\.Intensity"` 只命中**写侧**(`CueEmitDeriver.cs:111` 把 Intensity 塞进 DTO)
  与 sim 侧天气;音频选择路径**零消费** ⇒ DTO 到选变体之间断链
- **后果**:同一病人同一 cue 无论咳得轻重恒走同一桶;F-44.6 的三档分桶在运行期结构性不可达
- **与 story 自述的冲突**:story 边界裁定 1 写「只接线,不改选择语义」,但改的是**输入轴**不是数值

### B2 · 末槽截余与槽宽定义冲突 —— 真表内已有死变体与空桶坍陷

- **落点**:`VoiceVariantLib.SelectSlot`(`end = Math.Min(total, start + BucketBand(total))`)
  与 `AudioClipLibrary.VariantTableOf`(`strongCount = count - 2 * band`)
- **矛盾**:投影把末槽截余全塞进强桶,而 `SelectSlot` 的槽宽恒按 `BucketBand(total)` 算
- **编排方独立实算(真种子 `assets/data/audio_events.json`,非采信自述)**:

| 行 | n | 弱/中/强投影 | 实际可达 | 后果 |
|---|---|---|---|---|
| `World_Threat` / `Footstep_Base` | 4 | 1/1/**2** | 0/1/2 | 索引 3 **永不可达** |
| `World_Opportunity` / `World_Disaster` / `Steth_Contact` / `Page_Turn` | 2 | 1/1/**0** | 0/1/**0** | 强桶为空 ⇒ `SelectVariant` 返 −1 ⇒ 槽钳回 0 ⇒ **强强度听到最弱素材** |

- **测试盲区**:`test_ac020_3_allTwelveVoVariants_reachable` 只钉 12 条那一行(恰好 `%3==0`),
  于是全绿 —— 断言选在了唯一不会出问题的样本上

### B3 ·(编排方补,评审未报)**把语义不同的素材当成同义变体**

- **前提不成立**:首版把「行内多素材」一律当变体,但真种子 8 行多素材里**只有 1 行是变体**
- **实测素材语义**(`assets/data/audio_events.json` + GDD 交叉核对):

| 行 | 素材 | 真实轴 | GDD 依据 |
|---|---|---|---|
| `Voice_Cough_Damp` | `m/f × 3档 × 2变体` = 12 | ✅ **同一句话的变体** | F-44.3 `{gender, 桶→句尾变体集}` |
| `World_Threat` | 狗 / 马 / 锣 / 鸟 | ❌ **四种不同事件** | `:72`「狗吠 / 马蹄 / 锣声 / 鸟群惊飞」逐字并列 |
| `Footstep_Base` | 石 / 泥 / 腐殖 / 雪 | ❌ **地表材质** | `:817`「脚步 × 地形矩阵」 |
| `Steth_Contact` | 胸 / 背 | ❌ **听诊部位** | `:801` |
| `Page_Turn` | 前翻 / 后翻 | ❌ **翻页方向** | — |
| `Cabinet_Prop` | 抽屉 / 把手 / 放置 | ❌ **不同物体** | — |
| `World_Opportunity` / `World_Disaster` | 门铃/叫卖 · 哭喊/钟声 | ❌ **不同事件** | `:72` |

- **生产后果(8 行 wav 全部在场,已实测)**:患者按身份哈希 ⇒ **可能听到马而不是狗**;
  翻书**可能往后翻**;听诊**可能贴到背上**;**雪地上会响石头的脚步**
- **为什么 B2 的修法救不了它**:补可达性测只解决「死变体」,**解决不了「选了错的东西」** ——
  根因是「行内任意素材都可当变体」这个前提本身不成立

---

## §三 重裁与修复(用户裁定 ①②③)

### ① 变体轴显式声明 —— 只有声明过的行才走变体选择

**新字段 `variant_axis`**(构建期白名单 + 闭集):

| 层 | 落点 | 改动 |
|---|---|---|
| 数据 | `assets/data/audio_events.json` | `Voice_Cough_Damp` 行增 `"variant_axis": "voice_gender_intensity"`;**其余 7 行多素材不声明** |
| 门白名单 | `AudioEventTableGates.RowKeys` | 增 `"variant_axis"`(否则烘焙期未知键硬失败) |
| 门闭集 | `AudioEventTableGates.VariantAxes` | `{ voice_gender_intensity }`(P0 唯一轴) |
| 门逻辑 | `AudioEventTableGates.ValidateVariantAxis` | 三判:值 ∈ 闭集 · 只适用于 `Voice_*` 族 · 素材数整除三桶 |
| 绑定 | `AudioEventTableBinder.BuildRow` | `row.VariantAxisRaw = RawOf(obj, "variant_axis")` |
| 绑定行 | `AudioEventTableGates.AudioEventTableRow` | 新增 `VariantAxisRaw` 字段 |
| 编码 | `AudioEventTableCookedWriter.WriteRow` | **末尾追加** `WriteString(row?.VariantAxisRaw)`(既有偏移不变) |
| 解码 | `AudioEventCookedCodec.ReadRow` | 同序追加(写读严格镜像) |
| 运行期 | `AudioEventDataSet.AudioEventRow` | 新增 `VariantAxis` + `IsVariantRow`;构造器末位**可选**参数(既有调用零改动) |
| 选择 | `AudioClipLibrary.Resolve` | **未声明 ⇒ 恒取 `assets[0]`**;声明 ⇒ 走变体选择 |

### ② 桶输入改回 `Intensity` —— **本卡只登记债,不改**

用户裁定原话为「桶的输入改回 `Intensity`」。但实测该输入轴**在语声 cue 上未接线**:
`AudioCueDto.Intensity` 在音频选择路径零消费(见 B1),而接线属 **13 的强度产侧**
(`PatientCue.Intensity(position)` 已产,但 `ResolveCue` 没把它传下去)。

⇒ **本卡登记 `TD-audio-034`**(见 §六),归 13 的强度接线轮。理由:本卡边界裁定 3
明写「不改 `AudioCueDto` 形状」,而桶输入改 `Intensity` 需要 DTO 的 `Intensity` 真的
携带「这次咳嗽有多重」的语义 —— 这要先确认 13 的产侧是否已把它接到 cue 上。
**当前实现**:桶输入仍取身份,**已在代码注释与 GDD 偏差中如实标注**,不谎称已兑现 ②。

### ③ 槽宽改本槽实际宽度 —— 改为三维立方体

- 删除 `BucketBand` / `SelectSlot` / `VariantTableOf` / `VoiceBucketOf` 四个符号
- 新增 `VariantCubeSize(total)` = `total % 3 == 0 ? total/3 : -1`(**不猜、不截余**)
- 新增 `SelectVariantCube(cue, tier, identity, size)` = `桶 × size + 层内位置`
- 因素材数整除 3 由**构建期门**保证 ⇒ 三桶恒等宽 ⇒ **无死变体、无空桶**

---

## §四 证伪轮(每条守卫「停用什么 ⇒ 哪条红」)

> 纪律:每例**改 → 跑 → 还原在同一命令内**(`/tmp/fzrun.py`),还原后 **SHA-256 逐字节校验**
> (`restored sha ok`)。过滤器固定 `DaYiJingCheng.Tests.Unit.Audio.VoiceSelectionWiringTest`。

| # | 停用的守卫 | 期望变红 | 实测 |
|---|---|---|---|
| **F1** | `Resolve` 的 `!row.IsVariantRow` 守卫 | 重裁① 非变体行恒 `assets[0]` | **红 ×1** ✅ |
| **F2** | 变体行也短路成 `assets[0]` | 重裁① 配对半边 + 6 条旧测 | **红 ×6** ✅ |
| **F3** | `VariantCubeSize` 退回 `max(1, total/3)` 截余形态 | 立方体可达性 | **红 ×1** ✅ |
| **F4** | 门的 `n % VariantCubeTiers != 0` 判据 | 构建期拒收不整除 | **红 ×1** ✅ |

**首版 F1–F8(9 测时代)已作废** —— 那批反证打的是被本重裁删掉的选择语义
(`SelectSlot` / `BucketBand`),留着会误导后来人以为当前代码有那些守卫。

---

## §五 修复轮结账(Landed / Proven 分列)

| ID | Landed(已落) | Proven(已证) | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|
| **B3** 变体轴声明 | ✅ | ✅ | `AudioEventTableGates.cs` · `AudioClipLibrary.cs` · `audio_events.json` | §四 **F1**:删 `!row.IsVariantRow` ⇒ `test_ac020_1_nonVariantRow_alwaysTakesAssetsZero_everyIdentity` 红 |
| **B3** 配对半边 | ✅ | ✅ | 同上 | §四 **F2**:变体行也短路 ⇒ `test_ac020_1_variantRow_actuallyVaries_byIdentity` 红 |
| **B2** 立方体槽宽 | ✅ | ✅ | `VoiceVariantLib.cs` | §四 **F3**:`VariantCubeSize` 退回截余 ⇒ `test_ac020_3_variantCube_everySlotReachable_noDeadVariant_noEmptyBucket` 红 |
| **B2** 构建期门 | ✅ | ✅ | `AudioEventTableGates.cs` | §四 **F4**:停用整除判据 ⇒ `test_ac020_3_gate_rejectsVariantAxisWithNonCubeAssetCount` 红 |
| **B1** 桶输入 = `Intensity` | ✗ | ✗ | — | **未落**:登记 `TD-audio-034`,归 13 的强度接线轮(见 §三 ②) |
| **自证循环**(重裁时自捕) | ✅ | ✅ | `voice_selection_wiring_test.cs` | 首版 `test_ac020_1_multiAssetCue_...` 的期望侧调 `ExpectedAddress` → 内部就是 `SelectVariantCube` ⇒ 与 `Resolve` 同源,**选择器怎么坏都相等**。已删 `ExpectedAddress`,改为核对**位置**这一独立事实 |
| **cooked 产物同步** | ✅ | ✅ | `DataCooked/audio_events.cooked.bytes` | 改行布局后 5 条 `StateReportWalkthroughProbe` 报 E-13 越界;重烘 3241→3323 B 后 **13/13 绿** |

---

## §六 未及核查项(禁借绿)

| # | 项 | 状态 | 说明 |
|---|---|---|---|
| 1 | **桶输入 = `Intensity`**(用户重裁 ②) | **未落** | 登记 `TD-audio-034`;须先确认 13 的产侧把「咳得多重」接到 cue 上 |
| 2 | **gender 轴未接线** | **未落** | GDD F-44.3 的变体行是 `{gender(M/F/不拘), 桶→句尾变体集}`,而 `VoiceVariantRow` 的 `gender` 参数在生产恒传 0(首版 `VariantTableOf` 已删,现立方体不再携带 gender)。**gender 是「素材表声明」(GDD 原文,内容侧非运行时输入)** ⇒ 正确落点应是**事件表声明**,但今日表里无该字段。归 `TD-audio-034` 同批 |
| 3 | AC-44-02 **人耳半边** | **NOT-RUN** | 归 `TD-audio-102`;本件只让被听的对象真的在变 |
| 4 | 真 Addressables 装载 12 条变体 | **未证** | EditMode 无 catalog;PlayMode 半边只断言行数 |
| 5 | IL2CPP 三格(ADR-012) | **NOT-RUN** | 本机仅 Mono |
| 6 | 首版 W-1…W-8 | **登记未处置** | 其中 W-2(gender 未接线)并入 `TD-audio-034`;W-3(`BucketBand` 自造语义)随重裁删除;其余(静默回退 / 热路径分配 / 双构造二义)保留登记 |

---

## §七 回归

| 项 | 值 | 文件 |
|---|---|---|
| 基线(S21 终轮) | `3765 / 3720 / 0 / 45` | `unity/Logs/s21-full-editmode.xml` |
| **本轮全量(终轮)** | **`3769 / 3724 / 0 / 45`**,exit 0 | `unity/Logs/s20-reselect-full-editmode.xml` |
| 等式判据 | `total 3769 − 3765 = +4` = 新增 4 条 `[Test]`;`passed 3724 − 3720 = +4` | 严丝合缝 |
| 音频全域 | `505 / 505 / 0`(S20 首版 492 ⇒ +13 = 9 旧 + 4 新) | 无原件(stdout) |

**中间轮曾出现 1 红 = `MovementFeedTest.test_move_afterFreeFall_horizontalDisplacementBoundedByConfig`**
—— 已知间歇红(`TD-player-01`,与 S18/S21 同一判定)。**单跑该 fixture 5/5 绿**(实测),
终轮全量未复现 ⇒ 间歇坐实,**非本件引入**,不用它抵账。
