# Tech Debt Register

> **本册登记「已知偏离 / 未闭项」—— 不阻塞交付但须留痕的技术债。**
> 每条须带**出处**(评审原件条目号 / 文件行)与**触发条件**(何时须还)。
> 与 `production/qa/evidence/review-*.md` 分工:原件记**判定与修复**,
> 本册记**已知残留的债**——原件结案后仍悬的项。

---

## 2026-10-16 · medcons story-002 重评件 §五 9 条 Minor(sprint-13 S13-8)

**出处**:`production/qa/evidence/review-medcons-story-002-rework-2026-10-16.md` §五
(裁定 **APPROVED WITH SUGGESTIONS** —— 9 条为 Minor / 登记项,不阻塞转 Complete)。

| # | 债 | 出处 | 触发条件(何时须还) |
|---|---|---|---|
| TD-medcons-01 | `golden-v1.txt` 的 `C01_degraded_uraw 32768` 是**孤儿行** —— 无测试读取,退化值由测试本地字面量钉(自证)。应改为被测读取 | rework §五:1 | 数值轮冻结 `uraw` 退化值后;或任一 golden 行被改动时 |
| TD-medcons-02 | 「表值与 Python 参考实现同表」对 18 组合中 **5 组合**只由测试内镜像 `FixtureOutcome` 兜底 —— story 表述对这 5 行过强(行值已声明归数值轮,不构成 AC 级假绿) | rework §五:2 | 数值轮定稿行值后,须让 5 行也由真表读取 |
| TD-medcons-03 | AC-53-03 后半句「`FixParse` 为唯一数值解析入口(旋钮表)」**无载体且未登记** —— 可辩称「结构上虚空」(表内字段是枚举名 / `int`,无 `Fix` 字段) | rework §五:3 | 任一向 53 表引入 `Fix` 字段时(届时须补载体) |
| TD-medcons-04 | 门的**构建期注册**层无夹具:`AssemblyGates.RunAll()` 已接入,但 `test_assemblyGates_runAll_zeroErrors_*` **检不出注册被删**(删了反而更空 = 仍绿) | rework §五:4 | 下次动 `AssemblyGates.RunAll` 注册段时(同 b7「扫描面非空」防护同族) |
| TD-medcons-05 | `Materialize` 键碰撞硬失败分支(`internal`)无直驱载体(修正后构面不可达,纯纵深防御) | rework §五:5 | 若该分支的构面变得可达(如枚举扩展) |
| TD-medcons-06 | `Judge.Evaluate(inputs, null)` 的 `ArgumentNullException` **无测试** | rework §五:6 | 下次动 `Judge.Evaluate` 签名时 |
| TD-medcons-07 | `FindFloatLiterals` 已取全部匹配,但**同形态多字面量**的诊断完备性无测试 | rework §五:7 | 门报错信息被抱怨不够定位时 |
| TD-medcons-08 | 共享 `static readonly` `SharedPool`/`Encoder`/`Decoder` 跨测试**无 teardown** —— 对照 `.claude/rules/test-standards.md`「never shared mutable state」属偏离(当前无断言依赖 blob id,风险低) | rework §五:8 | 任一测试开始依赖 blob id 时 |
| TD-medcons-09 | EditMode 单元测试做**真实文件 I/O** + 依赖 `Application.dataPath` —— 违反「Unit tests do not call … file I/O」;`LoadTable` 走 `dataPath/../../` 解析仓根,在 **CI 检出布局变化时即红**(返工期已因此红过一次) | rework §五:9 | **CI runner 落地时**(ADR-012 矩阵)—— 高优先 |

**共同性质**:9 条均**不阻塞** story-002 转 Complete(重评已裁定),但**须留痕** ——
其中 **TD-medcons-09 是唯一有明确外部触发点(CI runner 落地)的高优先项**,
其余 8 条随「数值轮」或「下次动该文件」自然触发。

---

## 2026-10-16 · medcons story-003 评审件具名待办(sprint-13 S13-2)

**出处**:`production/qa/evidence/review-medcons-story-003-2026-10-16.md` §六 结账表。

| # | 债 | 出处 | 触发条件 |
|---|---|---|---|
| TD-medcons-10 | **B-W1**:共享 JSON 词法器(`JsonStage1Lexer`)不拒重复 property —— 影响全部 10+ binder | story-003 评审 §二 B-W1 | 任一 binder 因重复键静默丢字段时;或词法器统一加固轮 |
| TD-medcons-11 | **B-W2**:`53_delay_table` 等表**无 CLI / CI 烘焙入口**(仅 `DataBakeMenu` 菜单项) | story-003 评审 §二 B-W2 | 运行期读方立案批须补 CLI 入口 |
| TD-medcons-12 | **B-W3**:`DelayTableBinder.DomainRows` ordinal 与 Sim 侧 `DomainOutcomes` 的一致性断言**只防值分歧,不防改名** | story-003 评审 §二 B-W3 | 任一侧枚举改名时(当前由 `AssertDomainDeclarationsMatch` 兜值) |
| TD-medcons-13 | **B-W4**:`DelayTableCookedWriter` 产出的 `*.cooked` **无镜像读方**(装载归装配轮) | story-003 评审 §二 B-W4 | 装配轮装载 53 表时 |

---

## 2026-10-16 · medcons story-003 跨域缺口(sprint-13 S13-2)

| # | 债 | 出处 | 触发条件 |
|---|---|---|---|
| TD-medcons-14 | **W-5**:`ConsequenceResolved` 带真 `patient_id` ⇒ 进 `EventStream` AC-15 CAP 分支(`!IsPresent && PresentCount >= 24` ⇒ throw);当前**照发不 catch**,doc comment seam 已登记 | story-003 Completion Notes W-5 | 9 / 7a 轮裁定「离屏后果」语义时 |
| TD-medcons-15 | **W-4**:AC-5 / OQ-53-8「禁忌 / 过量代价表**永不致死**形状门」只兑现枚举域部分(结局轴 / NoEcho 作 key ⇒ schema 错);**代价表本身未建** | story-003 Completion Notes W-4 | GDD / 数值轮启动代价表时 |

---

## 2026-10-16 · 11 处方用药走查缺口(sprint-13 S13-7)

**出处**:`production/playtests/playtest-2026-10-16-s11-ac11-12-13-21-walkthrough.md` §9 前置勘察结论
(S13-7 走查包;用户 2026-10-16 裁定「照原样落包,走查用于证实缺口」)。
**性质**:本批为 **ADVISORY 级走查**,**执行 + 三签归真人**(S13-7 卡面「agent 不可代劳」)——
下列为 **agent 侧静态勘察**已确认的缺口,真人走查负责**目视证实**并触发还债。

| # | 债 | 出处 | 触发条件(何时须还) |
|---|---|---|---|
| TD-prescription-01 | **42 侧戥子形态元件缺失**(根因)—— GDD §Game Feel(`prescription-and-medication.md:868`)与 `skeuomorphic-ui.md:1617` 均登记「戥子档位读数元件 = 42 元件库黄铜侧(戥杆倾角 / 药包鼓胀)」;但 42 epic(18 story + 019–024)**无任何一条 story 交付该形态元件**。实际落地的 `BrassScaleElement` 仅 `int TickCount`(总档数)+ 黄铜色条,档位行 = N 个**同形** `Paper` + 同标签「戥子档」⇒ 相邻档**视觉完全相同**。**义务被登记、被「认领」,但形态从未实现** | 走查包 §9 根因;`CasebookScreen.cs:93`(「行内容恒定」自陈) | **真人走查确认后**——新立 42 侧 story 交付形态元件,或裁定 P0 接受无形态读数(须改 GDD,另开裁定) |
| TD-prescription-02 | **AC-11-13 结构上必红**(TD-prescription-01 的直接后果)—— 色盲模拟 + 静音下「仅凭形态分辨相邻两档」无形态可辨 ⇒ 落其 `ELSE` 分支「记为 42 元件库缺口」 | 走查包 §3/§9;`prescription-and-medication.md:978` | 同 TD-prescription-01(同一根因,两 AC 共同触发) |
| TD-prescription-03 | **AC-11-12 的「拟物载体可读」半必红**(零数字半 ✅)—— 同 TD-prescription-01 根因;`BrassScaleElement.TickCount` 是**总档数**(与当前档无关)⇒ 拟物载体**不可读当前档** | 走查包 §2/§9;`prescription-and-medication.md:977` | 同 TD-prescription-01 |
| TD-prescription-04 | **全案音频链未接通 ⇒ AC-11-21 不可判** —— 人工实测进 Play 后场景**完全无任何声音**。根因:全案**零 `IAudioCueSink` 实现**(仅接口定义)· `Boot.unity` **零 `AudioSource`**(仅 1 个 `AudioListener`)· `assets/audio/` **空目录** · 44 epic 的 **story-015/016 仍 `Ready`**(音频资产接入两条未做)· `audio_events.json` 仅 5 条(零处方条目)。⚠️ **证伪原预判「结构性静默」** —— 真相是整条链没通,「无音」不能归因于「设计成静默」 | 走查包 §11.7;44 EPIC §范围边界声明(2026-10-03) | **44 story-015 / 016 素材接入后**须重走 AC-11-21。⚠️ **影响超出 11**:全案任何音频类 AC 的听测半边(含 `AC-44-09`)在素材接入前**均不可判**,建议 44 侧一并登记 |
| TD-prescription-05 | **色盲模拟前置不可执行** —— UI 面板 `m_RenderMode: 0`(ScreenSpaceOverlay)⇒ 走相机后处理的色盲模拟**盖不到 UI**;Unity 6 URP `Rendering Debugger` 无 colorblind 栏。⇒ AC-11-13 的「色盲模拟下」前置**结构上无法满足** | 走查包 §11.4;`PanelSettings.asset:18` · `rendering.md:230` | **真人走查实测确认后** —— 若需真色盲模拟,须改 UI 渲染模式(ScreenSpaceCamera)或另立无障碍模拟通道(归 49 无障碍 / ADR-013 修订) |
| TD-prescription-06 | **病人占位胶囊**(非 11 范围,附带发现)—— 方笺页截图中 `rgb(255,0,255)` 品红占 13.89%(x 165..592, y 264..391),遮挡画面右半 ⇒ 影响方笺页视觉完整性 | 走查包 §11.5;截图 `unity/Logs/shots/20261016_111446_1x.png` | 13 病人 AI / 美术资产轮交付真病人模型时 |

**共同性质**:6 条**均不阻塞 story-005 转 Complete**(走查为 ADVISORY 级,且缺口本身即 AC-11-13 `ELSE` 分支的设计预期),
但**须留痕**。**TD-prescription-01 是根因**(02/03 是其直接后果),还债须先处置它;
**TD-prescription-05 / 06 是走查执行期新发现**(非前置勘察所得)。

---

## 2026-10-16 · 44 音频素材内容需求(sprint-14 S14-6 收尾登记)

**出处**:`production/qa/evidence/impl-s14-6-asset-pipeline-2026-10-16.md` §5 如实登记 ·
`story-015-audio-asset-binding.md` 卡面 · `design/gdd/audio-system.md:1099-1106`(组八)· `:639-646`(Edge 素材段)。
**背景**:S14-6 落地的 **5 个 `.wav` 是占位静音**(2044 B = 44 B RIFF/WAVE 头 +
2000 B 全零 PCM;单声道 44100 Hz / 16 bit / 1000 样本 ≈ **22.7 ms 全零**)。
**只保证「文件存在」这一条判据成立** —— 机制侧(门 / 烘焙管线 / Addressables 组 / 接线)已通并经证伪轮,
**素材本体这一侧仍为零**。下列为**内容批须交付的东西**,不是「补个文件」。

**为何单独立册而不并入 story-015**:story-015 的四条 AC(`D6b/D6c/D15/D16`)是
**「机制已在、真素材未验」的接入与检验**面,其 `Out of Scope` 逐字排除素材创作。
内容需求(音色 / 时长 / 变体量 / 编码格式)**无任何 AC 载体**,故落本册。

### 一、内容需求(逐族判据)

| 族 | 文件 | 判据(声学形状) | 出处 |
|---|---|---|---|
| **Voice** | `vo_patient_cough_damp_{m1,f1}.wav` | **阻尼咳嗽**(闷、短、无混响尾)—— 非清脆干咳 | GDD §规则五 · AC-44-05 |
| **Stethoscope** | `sfx_breath_base_loop_small.wav` | **基础气流循环**:通带中心 / 宽度 / 噪声底由 `tier_map` 驱动(档 0:700±500 Hz · −34 dB)⇒ 素材本体须**中性可滤波**(禁自带音高 / 禁自带环境),否则滤波档失效 | AC-44-01 BLOCKING |
| **Stethoscope** | `sfx_breath_adventitious_fine.wav` | **细湿啰音**(fine crackles)—— ⚠️ **禁连续水声**(`AC-44-08` 医学受众第一个出戏点);须可循环 | AC-44-08 |
| **Ambience** | `sfx_breath_base_loop_small.wav`(复用) | 同上;世界语境呼吸另有 `min/max_distance` + `occlusion_lowpass` 空间化 | ADR-028 · AC-44-16 |
| **Music** | `music_encounter_layer_loop.wav` | 遭遇紧张乐层 —— **G1–G3 约束,不独立播放**;须**无缝循环**(`loop: true`);`xfade_ms ≥ 2000` | AC-44-18 · `AudioTuning.MusicXfadeMinMs` |

### 二、系统级要求(跨族)

| # | 要求 | 出处 / 判据 |
|---|---|---|
| 内容-01 | **Import Settings 定稿** —— ✅ **已定档 2026-10-17(承 `OQ-44-2` 结案)**。`music_encounter_layer_loop.wav` = **Streaming**(`loadType: 2`,已落 `.meta`,Unity 导入回写实测稳定);其余 4 个(`vo_*` ×2 · `sfx_breath_*` ×2)= **DecompressOnLoad**(`loadType: 0`,即现值)。判据 = `audio-system.md` §OQ-44-2 附(可复算:驻留 ≤ 512 KB ∧ 时长 < 5 s ⇒ 小档)。⚠️ **残留**:`music_*` 的判定有口径张力(23.3 s 在时长上属中档、2008 KB 驻留在小档之外)⇒ 已在附节登记,**Music 素材若改短须重裁** | `audio.md:213-222` · `story-010` AC-44-D15 · GDD §OQ-44-2 附 |
| 内容-02 | **变体库量** —— ✅ **已裁 2026-10-17 用户 = 每组合 ≥2**(`OQ-44-2`)。**P0 采集量 = 男/女 × 强/中/弱 × 2 = 12 个 `vo_` 文件**;句尾维度**不加**(P0 无消费场景,P1a 再议)。⚠️ **裁定只定「量」,12 个文件尚未录制** ⇒ **`AC-44-03 [L]` 听测仍不可跑**。同时约束 F-44.3 的**回退规则**:烘焙门要求 (CueId × Tier × 桶) 全组合非空,任一轴向空缺 = 构建失败 ⇒ 变体量**不足即构建失败** | `OQ-44-2` · `story-006` AC-44-03 / 回退规则 |
| 内容-03 | **素材族完整性** —— 现事件表 **5 行**;`design/assets/entity-inventory.md` 登记音频 **32 项**(SFX 20 · Ambient 4 · Music Cues 8,其中 **VS Critical 11**);GDD `:804-830` 另列 **8 个补充族**(`sfx_pulse_*` 把脉 · `sfx_footstep_{材质}` 矩阵 · `sfx_process_*` 炮制 · `sfx_enemy_*` · `sfx_animal_*` · `sfx_breath_player_*` 玩家自身 · `amb_weather_*` 天气轴 · `amb_biome_*` 昼夜轴 · `reverb_preset_*`) | `entity-inventory.md:77-100,193-231` · GDD `:804-830` |
| 内容-04 | **命名真源** —— 录素材时 `cue_id` / 文件名以 `assets/data/audio_events.json` 为**唯一真源**;`design/assets/specs/audio-material-manifest.md` 已加真源声明 + 逐行对账(18 行中 3 命中 / 1 拆分 / 14 未登记)。⚠️ **该分叉无门会报**(`AC-44-D6` 只查文件在不在,**不查 cue 名**) | manifest 真源声明(2026-10-16 立) |

### 三、触发条件(何时须还)

| 触发 | 须做的 |
|---|---|
| **任一条 cue 从「占位」转「真素材」** | 先过 `story-015` 的 `AC-44-D6b/D6c/D15/D16`(真素材烘焙 · 删除即硬失败 · 组归属 · 双向差集)—— ✅ **5 条已全过**(2026-10-17,证据原件 `impl-s15-asset-content-2026-10-17.md`) |
| ~~**`OQ-44-2` 素材量裁定后**~~ | ✅ **已结 2026-10-17** —— 本表 内容-01 与 内容-02 **已同批定稿**(档位判据 = GDD §OQ-44-2 附;变体量 = 每组合 ≥2 / 12 个文件)。**残余**:12 个变体文件的**录制**仍待办 ⇒ 下一行「变体素材落地后」未解锁 |
| **变体素材落地后**(12 个 `vo_` 文件录制完成) | `story-006` `AC-44-03 [L]` 听测可跑;**`story-013`(主通道听测验收)** 与 **`story-016`(残余声源覆盖)** 随之解锁 |
| 🔴 **R-2 余 7 项 `.wav` 交付后**(诊脉接触音 / 听诊器接触音 / 纸面 / 脚步 / 药柜 / 世界声 / 馆内环境音) | **`story-016` 解锁** —— 其 `Status` 已于 2026-10-17 由 `Ready` 改 **`Blocked`**(E2/E3/E4 结构上依赖素材)⇒ 收口 016 ⇒ **随后收口 44 epic**(用户 2026-10-17 裁定:**素材接入后方可收口 epic**;在此之前不得以「001–015 全 `Complete`」推定 epic 已闭) |
| **任何音频类 AC 的 `[L]` 半边** | ⚠️ 在素材接入前**均不可判** —— 含 `AC-44-09`(无提示音铁律,BLOCKING)、`AC-11-21`(处方侧)。见同册 **TD-prescription-04** |

> **登记理由**:S14-6 的证据原件 §5 第 1/2 行**只登记了「NOT-RUN + 解除条件 = 素材定稿」**,
> 已含本册一半内容;本册补的是**「素材定稿」本身要交付什么**(逐族声学判据 + 4 条系统级要求),
> 以及**「§5 未登记内容需求」这个缺口本身**。

---

## 2026-10-17 · 44 音频 story-015 收口 ADVISORY(sprint-14 S14-8)

**出处**:`production/epics/audio-system/story-015-audio-asset-binding.md` §Completion Notes
ADVISORY ⑥(2026-10-17 收口轮新发现)· `production/qa/evidence/impl-s15-asset-content-2026-10-17.md` §3.5.4。
**性质**:story-015 的 6 条 ADVISORY 中**唯一具外部触发点**的一条,其余 5 条为过程记录(已就地订正或设计使然),
不入册。**本册只收 ⑥。**

> **⚠️ 2026-10-17 三笔裁定后重写**(用户裁定:笔 1 = 收窄 `.gitignore`;笔 2 = 评审 7 WARNING 分三路归属)。
> 本债自此**分两个面**,已修面与未修面**分列,禁混记**。

| # | 面 | 债 | 状态 / 出处 |
|---|---|---|---|
| **TD-audio-01a** | **跨机复现** | `unity/Assets/AddressableAssetsData/` 原被 `.gitignore:85` **整目录忽略** ⇒ `audio` 组及其 5 条目只存在于机器本地,新检出后 `test_d5_realSettings_audioAssetsSeparateFromDataCore` 正向半边**结构上不成立** | ✅ **2026-10-17 已修** —— `.gitignore` **收窄**为只忽略逐平台 `*/addressables_content_state.bin`;**组定义入库**(39 文件 + 顶层 `.meta`,共 41)。收窄依据:组定义 GUID 逐一命中**已提交**的 `.wav.meta` · profile 变量全为 token 式相对值 · 全树零绝对路径 · `.meta` 齐备。**修复方向 ① 被采纳**(原三选一) |
| **TD-audio-01b** | **自举**(⚠️ 前提已被 01a 改变) | ~~`settings == null` 只 `LogError` 就 return ⇒ 须先手点 Addressables Groups 窗口,**不能自举**~~ —— **01a 落地后该前提失效**:组定义入库,「设置缺失」的含义由「没初始化过」变为「**入库文件被删/损坏**」 | ✅ **2026-10-17 已修**(用户裁定:**硬失败 + 指引恢复,不自举**)。`DataBakeMenu.EnsureAudioGroupCore` 分支 ① 改为 `throw`,报文指向 `git checkout -- unity/Assets/AddressableAssetsData`。⚠️ **原「自举」方向被裁为不采纳** —— `GetSettings(create:true)` 会建一份只含默认组、与入库定义**分叉**的 settings = **假绿**(看着绿,组是错的)。**残余**:W-5② `CreateGroup` 只给默认 schema —— **仍未修**,见下方 |
| **TD-audio-01c** | **路径未验**(⚠️ 原判据已失效) | `CreateGroup` + schema 的「**全新 clone 首跑**」路径 —— 原为**读码推断** | ⚠️ **2026-10-17 状态订正:原实测结论不可再引用**。原跑法「移走 `AddressableAssetsData/`」模拟的是 **01a 之前**的状态;01a 后组定义入库,真·全新检出**带**组定义 ⇒ 该模拟不再成立。**静态核实(已做)**:`DefaultObject.asset` 的 `m_AddressableAssetSettingsGuid = 2c864c592004e1c729cd6c8ed9da4089` = `AddressableAssetSettings.asset.meta` 的 guid ⇒ **引用链完整**。**真·全新 clone 复测未跑**(须 `git clone` + 全量导入)⇒ 如实**登记为未验**,不借绿 |
| **TD-audio-01d** | **判据弱于 AC** | ~~`ValidateEntries` (a) 半不断言「登记在 `audio` 组」⇒ `.wav` 落在 `Default Local Group` 亦**判过**,**素材仍被打进主包**,破 AC-44-D5 意图~~ | ✅ **2026-10-17 结案 —— 原判定的危害陈述证伪**(用户裁定:维持 GDD 口径,**不改代码**)。**证伪证据(实测)**:`BuildPlayerContent` 产物 = `defaultlocalgroup_scenes_all_*.bundle` / `audio_assets_all_*.bundle` / `data-core_assets_all_*.bundle` —— **每组各自成 bundle** ⇒ 不存在「被打进主包」。**且判据语义另有明文**:GDD `design/gdd/audio-system.md:1104-1105`「判据 = **组区分** …**不是**「组叫什么名字」」。**残余(如实登记,不隐藏)**:`.wav` 落在 `Default Local Group` 时本门**判过** —— 此为 GDD 明文裁定的范围,**不是缺陷** |
| **TD-audio-01e** | **静默失败**(01c 实测**新发现**) | `EnsureAudioGroup` 在 `settings == null` 时**只 `LogError` 并 return**,`moved == 0` 时只 `LogWarning` 并 return —— **两处都不让进程失败** ⇒ 以 `-executeMethod` 作 CI 前置步时**报绿而组实际缺失** = **假绿**(本项目最忌的失效模式) | ✅ **2026-10-17 已修**(用户裁定:**同文件两处都修**)。`EnsureAudioGroupCore` **两条**分支均改 `throw`;并**先验后改** —— 两条都在任何写入之前判定 ⇒ 失败时设置零改动(夹具可安全注入真设置)。**证伪轮 2/2 全红且各只红 1 条**:停用分支 ① ⇒ `test_d5_menu_settingsMissing_throwsHard` 红;停用分支 ② ⇒ `test_d5_menu_noAssets_throwsHard` 红。⚠️ **`EnsureDataCoreGroup` 的同款两分支未修**(用户裁定 01e 范围限于 `EnsureAudioGroup`)—— 已在其 doc 就地注记 |

**触发条件(全部四个面共用)**:**CI 的 Unity 测试 job 真正启用时**(ADR-012 跨平台确定性矩阵)—— 与 **`TD-medcons-09`** 同族。
> ⚠️ **2026-10-17 实测订正:该触发条件「尚未落地」** —— CI workflow **已存在**但 Unity job 是 **no-op**:
> `tests.yml` 的 Unity job 已于 **2026-09-29 移除**(无 license),`guard-assertions` 是 `exit 0` 占位;
> `unity-tests.yml` 的 EditMode/PlayMode 由 secret 门控(**未配 `UNITY_CLIENT_ID` 即跳过**),IL2CPP job 是占位。
> ⇒ 本债处于「**待发**」而非「已红」。**优先级维持「高」**,但还债窗口在 Unity job 恢复之时。

> **为何单列而不并入 TD-medcons-09**:两者**同族不同因** —— medcons-09 是「测试读真实文件系统 + 路径解析依赖检出布局」;
> 本债是「**被断言的对象本身不在版本控制里**」。前者的出路是改测试的路径解析;后者的出路是给组创建提供确定性基线(01a ✅)+ 可自举入口(01b)。
>
> **残余修复方向(2026-10-17 三笔裁定后重写)**:
> - **W-5② `CreateGroup` schema 不复现** —— **仍 ✗ 未修**。`CreateGroup` 只给默认 schema;
>   今日若手调过 Bundle Mode / 压缩,重跑不复现。**须先裁「哪些 schema 值算定稿」** ——
>   现入库的 `audio_BundledAssetGroupSchema` 有 `m_UseDefaultSchemaSettings: 0`(即**已脱离**默认),
>   故「重建 = 入库值」不能靠默认值白拿。
> - **01c 真·全新 clone 复测** —— 须 `git clone` + 全量导入(慢);静态引用链已核实完整。
> - **`EnsureDataCoreGroup` 同款静默失败** —— 用户裁定 01e 范围不含它,留待后续。
> ⚠️ 原「②③ 都须**先补 `GetSettings(create:true)` 自举**」**已作废** —— 01b 裁定为**不自举**(见上)。
> ⚠️ ① 与 ADR-023 ②「零 gameplay 对象」/ ADR-025 装配清单无冲突(已实测);Addressables 6.2+ 目录契约的 Knowledge Risk MEDIUM 随 01a 落地**降为已观测形态**(组定义确为可移植 YAML)。
>
> **同批实测附带发现(非本债面内,登记不隐藏)**:`Editor.Tools.Kindgen.asmdef` 与 `Editor.Tools.Level.asmdef`
> 两个 Tooling 程序集**无任何脚本关联**,Unity 每次编译输出 `LogWarning`(Kindgen 实现为 Python,承 ADR-024 §⑤;
> Level 工具尚无 C#)。**无害但噪声** —— 若将来 Tooling 程序集增多,建议评估是否保留空 asmdef。

---

## 登记格式(后续沿用)

```
- **[YYYY-MM-DD]** (<story / 对象>):<债描述> — 出处 <原件 §N:行> — 触发条件:<何时须还> — tracked from <story file path>
```
