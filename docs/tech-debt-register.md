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
| 内容-02 | **变体库量 + 命名 + 挂接** —— ✅ **量已裁 2026-10-17 用户 = 每组合 ≥2**(`OQ-44-2`)。**P0 采集量 = 男/女 × 强/中/弱 × 2 = 12 个 `vo_` 文件**;句尾维度**不加**(P0 无消费场景,P1a 再议)。✅ **命名与挂接已同批冻结(2026-10-17 第三轮裁定)**:12 个**全为阻尼咳嗽** ⇒ **挂既有 `Voice_Cough_Damp`**(`assets[]` **2 → 12**);命名 = `vo_patient_cough_damp_{m\|f}{1\|2\|3}_{1\|2}.wav`(**1 = 强 · 2 = 中 · 3 = 弱**);**现 `m1` / `f1` 退役**。冻结件 = `design/assets/specs/audio-material-manifest.md` §「冻结命名与 bus 裁定」。⚠️ **裁定只定「量 / 命名 / 挂接」,12 个文件尚未录制** ⇒ **`AC-44-03 [L]` 听测仍不可跑**。**两条落地约束**:① **12 文件 + `assets[]` 更新须同批落地** —— 烘焙门要求每个被引 `.wav` 都存在,分批 = 当场 `throw`;② 现 `m1`/`f1` 的真内容是否复用,归 content 批自决(**默认不复用**)。 | `OQ-44-2` · `story-006` AC-44-03 / 回退规则 · manifest §冻结 |
| 内容-03 | **素材族完整性** —— 现事件表 **5 行**;`design/assets/entity-inventory.md` 登记音频 **32 项**(SFX 20 · Ambient 4 · Music Cues 8,其中 **VS Critical 11**);GDD `:804-830` 另列 **8 个补充族**(`sfx_pulse_*` 把脉 · `sfx_footstep_{材质}` 矩阵 · `sfx_process_*` 炮制 · `sfx_enemy_*` · `sfx_animal_*` · `sfx_breath_player_*` 玩家自身 · `amb_weather_*` 天气轴 · `amb_biome_*` 昼夜轴 · `reverb_preset_*`) | `entity-inventory.md:77-100,193-231` · GDD `:804-830` |
| 内容-04 | **命名真源** —— 录素材时 `cue_id` / 文件名以 `assets/data/audio_events.json` 为**唯一真源**;`design/assets/specs/audio-material-manifest.md` 已加真源声明 + 逐行对账(18 行中 3 命中 / 1 拆分 / 14 未登记)。⚠️ **该分叉无门会报**(`AC-44-D6` 只查文件在不在,**不查 cue 名**) | manifest 真源声明(2026-10-16 立) |

### 三、触发条件(何时须还)

| 触发 | 须做的 |
|---|---|
| **任一条 cue 从「占位」转「真素材」** | 先过 `story-015` 的 `AC-44-D6b/D6c/D15/D16`(真素材烘焙 · 删除即硬失败 · 组归属 · 双向差集)—— ✅ **5 条已全过**(2026-10-17,证据原件 `impl-s15-asset-content-2026-10-17.md`) |
| ~~**`OQ-44-2` 素材量裁定后**~~ | ✅ **已结 2026-10-17** —— 本表 内容-01 与 内容-02 **已同批定稿**(档位判据 = GDD §OQ-44-2 附;变体量 = 每组合 ≥2 / 12 个文件)。**残余**:12 个变体文件的**录制**仍待办 ⇒ 下一行「变体素材落地后」未解锁 |
| **变体素材落地后**(12 个 `vo_` 文件录制完成) | `story-006` `AC-44-03 [L]` 听测可跑;**`story-013`(主通道听测验收)** 与 **`story-016`(残余声源覆盖)** 随之解锁 |
| 🔴 **R-2 余 7 项 `.wav` 交付后**(诊脉接触音 / 听诊器接触音 / 纸面 / 脚步 / 药柜 / 世界声 / 馆内环境音) | ✅ **命名与 bus 已于 2026-10-17 第三轮冻结**(**18 个文件**,逐项见下「R-2 冻结清单」;权威件 = `design/assets/specs/audio-material-manifest.md` §「冻结命名与 bus 裁定」)。**录制完成后** ⇒ **`story-016` 解锁** —— 其 `Status` 已于 2026-10-17 由 `Ready` 改 **`Blocked`**(E2/E3/E4 结构上依赖素材)⇒ 收口 016 ⇒ **随后收口 44 epic**(用户 2026-10-17 裁定:**素材接入后方可收口 epic**;在此之前不得以「001–015 全 `Complete`」推定 epic 已闭) |
| **任何音频类 AC 的 `[L]` 半边** | ⚠️ 在素材接入前**均不可判** —— 含 `AC-44-09`(无提示音铁律,BLOCKING)、`AC-11-21`(处方侧)。见同册 **TD-prescription-04** |

> **登记理由**:S14-6 的证据原件 §5 第 1/2 行**只登记了「NOT-RUN + 解除条件 = 素材定稿」**,
> 已含本册一半内容;本册补的是**「素材定稿」本身要交付什么**(逐族声学判据 + 4 条系统级要求),
> 以及**「§5 未登记内容需求」这个缺口本身**。

### R-2 冻结清单(2026-10-17 第三轮裁定 · **18 个文件** · 内容批照此录制)

**裁定原则**:① **bus 以 GDD `:798-810` 分层表为准**(未批准的草案清单有 3 处与 GDD 明文冲突);
② 覆盖范围 = **`entity-inventory.md` 全集**;③ 脚步材质 = **3 材质 + 雪档**。
**权威件** = `design/assets/specs/audio-material-manifest.md` §「冻结命名与 bus 裁定」。

| R-2 项 | **cue id(冻结)** | bus | loop | category / trigger | **文件(冻结)** | 量 |
|---|---|---|---|---|---|---|
| [SFX #1] 诊脉接触音 | `Pulse_Contact` | **Stethoscope** | ✗ | PlayerExamAction | `sfx_pulse_contact_01.wav` | 1 |
| [SFX #2] 听诊器接触音 | `Steth_Contact` | Stethoscope | ✗ | PlayerExamAction | `sfx_steth_contact_{chest,back}_01.wav` | 2 |
| [SFX #7] 纸面物理声 | `Page_Turn` | UICue | ✗ | PlayerExamAction | `ui_page_turn_{fwd,back}_01.wav` | 2 |
| [SFX #8] 脚步声 | `Footstep_Base` | **SFX** | ✗ | WorldPerceptible | `sfx_footstep_{stone,dirt,humus,snow}_01.wav` | 4 |
| [SFX #9] 药柜/器物声 | `Cabinet_Prop` | SFX | ✗ | PlayerAction | `sfx_cabinet_{drawer,handle}_01.wav` · `sfx_prop_place_01.wav` | 3 |
| [SFX #10] 世界声 | `World_Sfx_Base` | **SFX** | ✗ | WorldPerceptible | `sfx_world_{dog,horse,gong,bell,birds}_01.wav` | 5 |
| [SFX #10] 世界声 · **预告线索补 4 项** | `World_Sfx_Base`(同一 cue,`assets[]` 共 **9**) | **SFX** | ✗ | WorldPerceptible | `sfx_world_{door_bell,vendor_cry,throat_clear,crying}_01.wav` | 4 |

> ⚠️ **2026-10-17 第二轮裁定:上两行世界声已改为拆 4 条 cue** ——
> `World_Threat`(狗/马/锣/鸟) · `World_Opportunity`(门铃/货郎) ·
> `World_Reaction`(清嗓子) · `World_Disaster`(哭喊/钟声)。**9 个文件不变**。
> 依据 = 9 项并入一条 cue 时事件表无「档位」字段 ⇒ 运行期无法按档选音。
> 权威件 = `design/assets/specs/audio-material-manifest.md` §一之二。
| [Ambient #1] 医馆室内环境音 | `Clinic_Room_Tone` | Ambience | ✓ | WorldPerceptible | `amb_clinic_room_loop.wav` | 1 |

> ⚠️ **顺序约束**:这 7 项目前在 `assets/data/audio_events.json` 内**零条目**。
> 正确顺序 = **先录素材 → 我补 `rows[]` 条目 → 重烘焙**。
> **反过来(先补条目)会让烘焙门当场 `throw`** —— 门要求被引文件存在。
> 这也正是 story-016 的 `AC-44-E2` 与 `AC-44-E3` 的判据面。

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
| **TD-audio-01b** | **自举**(⚠️ 前提已被 01a 改变) | ~~`settings == null` 只 `LogError` 就 return ⇒ 须先手点 Addressables Groups 窗口,**不能自举**~~ —— **01a 落地后该前提失效**:组定义入库,「设置缺失」的含义由「没初始化过」变为「**入库文件被删/损坏**」 | ✅ **2026-10-17 已修**(用户裁定:**硬失败 + 指引恢复,不自举**)。`DataBakeMenu.EnsureAudioGroupCore` 分支 ① 改为 `throw`,报文指向 `git checkout -- unity/Assets/AddressableAssetsData`。⚠️ **原「自举」方向被裁为不采纳** —— `GetSettings(create:true)` 会建一份只含默认组、与入库定义**分叉**的 settings = **假绿**(看着绿,组是错的)。**残余**:~~W-5② `CreateGroup` 只给默认 schema —— 仍未修~~ **✅ 2026-10-17 已修**(见下「残余修复方向」) |
| **TD-audio-01c** | **路径未验**(⚠️ 原判据已失效 · ✅ 真复测已补) | `CreateGroup` + schema 的「**全新 clone 首跑**」路径 —— 原为**读码推断** | ✅ **2026-10-17 真·全新 clone 复测已跑**(用户裁定)。`git clone` 到全新空目录(实测:无 `Library/` · 无 `Linux/content_state.bin` · 组定义齐备)⇒ `unity test … --filter PipelineGateTest` = **54/54/0**,决定性的一条 `test_d5_realSettings_audioAssetsSeparateFromDataCore` **Passed** ⇒ **`Settings` 在真检出上解析成功**。⚠️ **原模拟方式已作废**(「移走目录」模拟的是 01a **之前**的状态);⚠️ **且 `CreateGroup` 分支在全新检出上不再被走到** —— 组已随仓库到达 ⇒ 01c 的**原始关切**(CreateGroup + schema 首跑)**因 01a 而结构性消解**。**残余**:schema 定稿问题另立(见下 W-5②) |
| **TD-audio-01d** | **判据弱于 AC** | ~~`ValidateEntries` (a) 半不断言「登记在 `audio` 组」⇒ `.wav` 落在 `Default Local Group` 亦**判过**,**素材仍被打进主包**,破 AC-44-D5 意图~~ | ✅ **2026-10-17 结案 —— 原判定的危害陈述证伪**(用户裁定:维持 GDD 口径,**不改代码**)。**证伪证据(实测)**:`BuildPlayerContent` 产物 = `defaultlocalgroup_scenes_all_*.bundle` / `audio_assets_all_*.bundle` / `data-core_assets_all_*.bundle` —— **每组各自成 bundle** ⇒ 不存在「被打进主包」。**且判据语义另有明文**:GDD `design/gdd/audio-system.md:1104-1105`「判据 = **组区分** …**不是**「组叫什么名字」」。**残余(如实登记,不隐藏)**:`.wav` 落在 `Default Local Group` 时本门**判过** —— 此为 GDD 明文裁定的范围,**不是缺陷** |
| **TD-audio-01e** | **静默失败**(01c 实测**新发现**) | `EnsureAudioGroup` 在 `settings == null` 时**只 `LogError` 并 return**,`moved == 0` 时只 `LogWarning` 并 return —— **两处都不让进程失败** ⇒ 以 `-executeMethod` 作 CI 前置步时**报绿而组实际缺失** = **假绿**(本项目最忌的失效模式) | ✅ **2026-10-17 已修**(用户裁定:**同文件两处都修**)。`EnsureAudioGroupCore` **两条**分支均改 `throw`;并**先验后改** —— 两条都在任何写入之前判定 ⇒ 失败时设置零改动(夹具可安全注入真设置)。**证伪轮 2/2 全红且各只红 1 条**:停用分支 ① ⇒ `test_d5_menu_settingsMissing_throwsHard` 红;停用分支 ② ⇒ `test_d5_menu_noAssets_throwsHard` 红。**且进程级端到端实测**(真·全新 clone,`unity build --execute-method`):**完好 ⇒ `EXIT=0`** + `audio 组就绪:5 个素材已入组并打标签`;**移走设置 ⇒ `EXIT=6`** + `InvalidOperationException` 含 `git checkout` 指引 —— **同样输入下旧行为的退出码是 `0`**(`s17-freshclone-20261017-051600.log`)⇒ **静默已闭**。⚠️ ~~**`EnsureDataCoreGroup` 的同款两分支未修**(用户裁定 01e 范围限于 `EnsureAudioGroup`)~~ ⇒ **✅ 2026-10-17 已一并修**(补口轮 —— 01e 的窄范围已扩)。拆出可注入的 `EnsureDataCoreGroupCore(settings, cookedDirRel, groupName)`,**三处硬失败**(设置缺失 / 产物目录零 `*.cooked.bytes` / 组缺失);且零产物扫描**提到任何写入之前**(先验后改 —— 原实现是「先建组、先打标签才 `LogWarning`」)。**判据**:`test_dcore_menu_{settingsMissing,noCooked,groupMissing}_throwsHard`;**证伪轮 3/3 全红**(停用 `guids.Count == 0` ⇒ `noCooked` 红;停用 `group == null` ⇒ `groupMissing` 红) |
| **TD-audio-01f** | **跨机复现**(2026-10-17 **新发现** · S14-6 补口) | 入库的 `AddressableAssetSettings.asset` 带 `m_ActivePlayerDataBuilderIndex: 2`(= `BuildScriptPackedMode`)⇒ **PlayMode 下 Addressables 从预建 catalog 解析**(`unity/Library/com.unity.addressables/aa/<platform>/catalog.bin`,**gitignored · 机器本地**)。实测后果:新增组条目(`audio_events.cooked.bytes` / `audio_settings_registry.cooked.bytes`)后未重建 ⇒ `AddressablesDataProvider.Preload() → EnsureAudioEvents() → FetchBytes()` 抛 `InvalidKeyException: No Location found for Key=…` ⇒ **9 条 PlayMode 启动序测试全红**。⚠️ **潜伏期**:该炸点自 **2026-10-16 `8d852fe`**(S14-6 把 `EnsureAudioEvents()` 加进 `Preload()`)起就在,而 **PlayMode 自 2026-10-15 起没跑过全量**(`s11-b6-playmode.xml` = 186/186/0 是最后一次)⇒ 不是本轮引入的,是本轮**发现**的 | ✅ **2026-10-17 已解**(重建 catalog ⇒ 全量 PlayMode **189/189/0/0**,基线 186/186/0/0,零回归)。**新增菜单**「大医精诚/数据管线/**构建 Addressables 内容(BuildPlayerContent)**」。**⚠️ 2026-10-17 同日二次订正 —— 原「残留」的诊断是错的**:**根因不是 `m_ActivePlayerDataBuilderIndex`(那是 Build 脚本,入库值 `2` = `BuildScriptPackedMode`,**正确**),而是 **Play Mode 脚本** —— 它住 `ProjectConfigData.ActivePlayModeIndex`,落 **`unity/Library/AddressablesConfig.dat`**(**机器本地 · gitignored · 不入库**,包源码 `ProjectConfigData.cs:390`)。**默认值 0 = Use Asset Database ⇒ 全新检出本来就没有这个问题**;坏的是**被改过的那台机器** —— 实测本机值为 `1`(Use Existing Build),元凶 = `U1SpikeSetup.ConfigurePlayMode`(`Editor.Tools.Spike/U1SpikeSetup.cs:444`,**刻意不复原**)。**已修**:① `EnsureAssetDatabasePlayMode()` 由 `private` 升为**可注入 public**(纯谓词 `FindAssetDatabaseBuilderIndex` + 真环境半)并挂菜单「**Play Mode 归位(Use Asset Database)**」—— 本机已跑,日志实证 `原「Use Existing Build (requires built groups)」`;② 新增**机器前置门** `test_playModeScript_isAssetDatabase`(漂移 ⇒ **一条具名红**,不再表现为 9 条神秘红)。**残留(未修,如实登记)**:① 该设置**不可入库、不可 `git checkout` 恢复** —— 只能靠菜单/门;**新机器默认值恰好是对的**,故风险面仅限「跑过 U1 spike 的机器」;② `U1SpikeSetup` 本身**仍未**在 Teardown 复原该设置(本轮只在 `DataBakeMenu` 侧加了修复入口,未改 spike —— 它是历史 spike,改它需先裁「spike 是否保留」) |

**触发条件(全部五个面共用)**:**CI 的 Unity 测试 job 真正启用时**(ADR-012 跨平台确定性矩阵)—— 与 **`TD-medcons-09`** 同族。
> ⚠️ **2026-10-17 实测订正:该触发条件「尚未落地」** —— CI workflow **已存在**但 Unity job 是 **no-op**:
> `tests.yml` 的 Unity job 已于 **2026-09-29 移除**(无 license),`guard-assertions` 是 `exit 0` 占位;
> `unity-tests.yml` 的 EditMode/PlayMode 由 secret 门控(**未配 `UNITY_CLIENT_ID` 即跳过**),IL2CPP job 是占位。
> ⇒ 本债处于「**待发**」而非「已红」。**优先级维持「高」**,但还债窗口在 Unity job 恢复之时。

> **为何单列而不并入 TD-medcons-09**:两者**同族不同因** —— medcons-09 是「测试读真实文件系统 + 路径解析依赖检出布局」;
> 本债是「**被断言的对象本身不在版本控制里**」。前者的出路是改测试的路径解析;后者的出路是给组创建提供确定性基线(01a ✅)+ 可自举入口(01b)。
>
> **残余修复方向(2026-10-17 三笔裁定后重写;同日补口轮再订正 —— 三项均已落)**
> - ~~**W-5② `CreateGroup` schema 不复现** —— **仍 ✗ 未修**~~ ⇒ **✅ 已修(甲案:删掉重建,改硬失败)**。
>   原前置「**须先裁「哪些 schema 值算定稿」**」**随之消解** —— 既然**不建组**,默认值与入库值的差异
>   **不再是本菜单需要知道的量**。`EnsureAudioGroupCore` / `EnsureDataCoreGroupCore` 的 `CreateGroup`
>   分支已删,组缺失 ⇒ `throw` + `git checkout -- unity/Assets/AddressableAssetsData` 指引。
>   **判据**:`test_d5_menu_groupMissing_throwsHard` / `test_dcore_menu_groupMissing_throwsHard`
>   (**先验后改**:抛异常后 `settings.FindGroup(absent)` 仍为 `null`)。
> - ~~**01c 真·全新 clone 复测** —— 须 `git clone` + 全量导入(慢)~~ ⇒ **✅ 已跑**,见 `01c` 行。
> - ~~**`EnsureDataCoreGroup` 同款静默失败** —— 留待后续~~ ⇒ **✅ 已一并修**,见 `01e` 行。
> - ~~**`01f` 的残留** ⇒ **未修**(`m_ActivePlayerDataBuilderIndex: 2` 仍在入库设置;新检出后须重跑
>   「构建 Addressables 内容」菜单)。**出路未裁**。~~ ⇒ **✅ 已修,且原诊断本身是错的** ——
>   入库的 `m_ActivePlayerDataBuilderIndex: 2` 是 **Build 脚本**(`BuildScriptPackedMode`),**正确**;
>   真正的旋钮是 **Play Mode 脚本**,住 `Library/AddressablesConfig.dat`(**机器本地、不入库、默认值即正确**)。
>   详见 `01f` 行与 §「证伪轮副作用」条。
> ⚠️ 原「②③ 都须**先补 `GetSettings(create:true)` 自举**」**已作废** —— 01b 裁定为**不自举**(见上)。
> ⚠️ ① 与 ADR-023 ②「零 gameplay 对象」/ ADR-025 装配清单无冲突(已实测);Addressables 6.2+ 目录契约的 Knowledge Risk MEDIUM 随 01a 落地**降为已观测形态**(组定义确为可移植 YAML)。
>
> **同批实测附带发现(非本债面内,登记不隐藏)**:`Editor.Tools.Kindgen.asmdef` 与 `Editor.Tools.Level.asmdef`
> 两个 Tooling 程序集**无任何脚本关联**,Unity 每次编译输出 `LogWarning`(Kindgen 实现为 Python,承 ADR-024 §⑤;
> Level 工具尚无 C#)。**无害但噪声** —— 若将来 Tooling 程序集增多,建议评估是否保留空 asmdef。

---

## 2026-10-17 · 证伪轮副作用:失败路径污染入库资产(44 音频补口轮)

**出处**:`production/qa/evidence/impl-td-audio-01-fix-2026-10-17.md` §10.6 / §10.7 ·
同型**第一次**见 `production/sprint-status.yaml:149`。
**性质**:**方法论级**债 —— 不是某一条代码的 bug,而是「**变异测试会执行生产码的写入路径**」这一固有风险
在 Unity 资产上的具体化。**已发生两次**:

| # | 时间 / 出处 | 对象 | 现象 | 处置 |
|---|---|---|---|---|
| 1 | 2026-10-17 · S14-8 | `unity/Assets/Audio/audio.asset` | 证伪轮触发 Unity 资产维护,**静默改写**(5 → 4 条) | 跑生产菜单项 `EnsureAudioGroup` 修复 |
| 2 | 2026-10-17 · 补口轮 | `AddressableAssetsData/AddressableAssetSettings.asset` | 变异 M1/M3 停用「组缺失 ⇒ throw」⇒ 生产码**落入分支 ④**,先 `AddLabel(假组名)` 再 `SaveAssets()` ⇒ **两个假标签入库** | `git checkout` 还原 + **夹具加固**(`AssertThrowsAndLeaveNoTrace`:try/finally 清理 + 「查组 + 查标签」双断言) |

**处置(2026-10-17 当日,三项全落)**:

- ~~**无通用防线**~~ ⇒ **✅ 已落,并升格为可复用驱动** —— `tools/falsify/unity_falsify.py`
  (含 `README.md` + `specs/`)。原「建议(未裁)」的 `git diff --quiet …` 一次性判据被
  **更强的形态**取代:**受保护路径的「前后指纹相同」断言**(`guards_fingerprint()`
  = `git status --short` + `git diff` 合并文本),默认守卫
  `unity/Assets/AddressableAssetsData` + `unity/Assets/Audio`。
  **为何不用「起跑必须干净」**(首版实测教训):开发中的仓本就常带未提交的正当改动
  (本例 `data-core.asset` 是 S14-6 的既有改动)⇒ 要求起跑干净会让驱动在**绝大多数真实场景**
  下直接退出码 2 —— 那不是严谨,是**不可用**。正确判据 = **证伪轮不得新增任何痕迹**。
  三条纪律已固化为代码(非留在人的记忆里):① 改→跑→还原同进程 `try/finally`;
  ② 还原后 **SHA-256 逐字节校验**;③ 跑测后**受保护路径指纹未变**。
  **退出码**:0 全符合预期 / 1 逃逸或红得比预期多 / 2 夹具过期 / **3 还原或守卫失败(最严重)**。
- ~~**第一次的对象没有防线**~~ ⇒ **✅ 同一条通用防线已覆盖** —— `S14-8` 的污染对象
  (`unity/Assets/Audio/audio.asset`)正落在默认守卫路径内 ⇒ 再发生即**退出码 3 拦下**,
  不必为它单写包装器。⚠️ 但仍**如实登记**:本驱动只保证「**被检出**」,
  **不保证「不被写入」** —— 防写入仍需夹具侧包装器(`AssertThrowsAndLeaveNoTrace`),
  两者是**检出 vs 预防**的分工,不可互相替代。
- **判据口径(未修,方法论)**:「无残留」不能只查**对象本身**(组/条目),须查**该方法能写到的每一处**
  (本例 = **标签表**)。**本驱动不改变这条** —— 它守的是**路径**不是**语义**:
  若污染写到守卫路径之外,指纹判据看不见。⇒ 新证伪轮**仍须逐例人判**「该守卫能写到哪」。

**同轮落地判据(证伪轮自证)**:
`python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/audio-import-settings-and-playmode.json`
⇒ `FALSIFY_EXIT=0`;4 个变异(F1 前缀覆盖 / F2 小档闭区间 / F3 PlayMode 谓词 `-1` /
F4 不可读 meta 静默归零)**各自恰如预期转红**,还原 SHA-256 通过,指纹未变。

**触发条件**:任何以「**停用生产守卫**」为手段的证伪轮(即 S12-4 形态的每一次)。

---

## 2026-10-17 · Unity 资产维护静默改写 `data-core.asset`(44 音频 story-017 收口轮)

**出处**:story-017 收口轮实跑「确保 audio Addressables 组」
(`unity/Logs/build-StandaloneLinux64-1792219838345.log`)·
同型**第三次**见上节「证伪轮副作用」表(第 1 条为图形资产,第 2 条为设置文件,本条为**组数据**)。

**现象(实测,`git diff` 原文)**:`EnsureAudioGroup` 的 batch 启动时,
`unity/Assets/AddressableAssetsData/AssetGroups/data-core.asset` 被改写 **+18 / −6 行**:

- **补入两条 `*.cooked.bytes` 条目** —— `audio_events.cooked.bytes`、
  `audio_settings_registry.cooked.bytes`
  (这两条是 S14-6 的产物,本就该入组,**GUID 无变化**);
  `case_judgment_lexicon.cooked.bytes` 在 HEAD 里**已存在** —— 它出现在 diff 里是**顺序重排**所致,
  **不是新增**(实测:HEAD 5 条 → 现 7 条,差集恰为上两条);
- **重排既有条目顺序**(序列化顺序,内容语义不变)。

*(**二次精算,2026-10-17** —— 本节初稿写「三条」,把重排显示成「删除 + 新增」的
`case_judgment_lexicon` 误计为新增;按条目差集复算,新增实为 **2** 条。)*
*另:初稿还曾把它描述成「ADR-030 的 `DiseaseOnset`」—— 亦误。`data-core` 里的都是
**cooked 产物条目**(`*.cooked.bytes`),与 `DiseaseOnset` 这个 `SimEvent.Kind` **无关**。*

**来源归因(排除本菜单代码路径)**:
`DataBakeMenu.EnsureAudioGroupCore`(`DataBakeMenu.cs:671-726`)对 `data-core` **零写入** ——
其三条硬失败**先于任何写入**判定,日志无告警;且条目内容与 `EnsureAudioGroup` 的产物
(`audio.asset` 条数变化)完全不相干。⇒ 来源 = **Unity 资产维护**(batch 启动时的隐式 `SaveAssets`)。

**处置(2026-10-17 · 用户裁定「按你的建议修改」)** —— **本条债已结**:

- 已按「S14-6 既有改动」与「本次 Unity 维护改动」**二分拆账**
  (做法:以 HEAD 的条目顺序为骨架 + 取当前版本各条目块,生成「S14 序」版;
  该版与 HEAD **逐字节相同** —— 因为 Unity 维护补的两条 `audio_*.cooked.bytes`
  本来就是 S14-6 的产物、`data-core` 组条目本就该有它们 ⇒ 差额 = 零);
- `data-core.asset` 已**恢复为 HEAD 原样**,`git status` 中**不再出现本文件**;
- ⇒ 「提交时须人工拆账」这条触发条件**已满足并消除**;
- 同时复核 `audio.asset`:差分恰为 **+196 / −4**,删的 4 行 = 两个退役 `.wav` 条目
  (`vo_patient_cough_damp_{m1,f1}.wav`),**同一来源**(退役裁定 + `EnsureAudioGroup` 重跑),
  不构成混账。

**仍保留的观察(不作为本债)**:Unity 资产维护**会把两版都合法的条目重排顺序** ——
这种改写**无语义**,但会让 diff 看起来像「删了又加」。**判据 = 比条目差集,不比 diff 行数**。

**既有防线为何未拦**:`tools/falsify/unity_falsify.py` 的守卫在**证伪轮**内生效;
本次是**生产菜单项实跑**,不经过该驱动 ⇒ 指纹判据未参与。这是**防线范围**问题,
不是防线失效(该驱动不覆盖非证伪轮的单次菜单执行)。

**触发条件(本债已结,以下为未来项)**:若将来再次出现「非证伪轮的单次菜单执行改写
`AddressableAssetsData`」,须评估是否把菜单执行也纳入 `unity_falsify.py` 的守卫面
(**须另裁,不在本件**)。

---

## 2026-10-17 · `TD-audio-01f` 残留② 结清 —— `U1SpikeSetup.Teardown` 复原的判据补口

**原状态**:`Landed ✅ / Proven ✗` —— 理由是「`Teardown` 是编辑器生命周期回调,EditMode 下不触发,
无夹具面」(登记的补法 = 源码扫描或 PlayMode 手跑)。

**本轮做法(用户 2026-10-17 裁定「按这个顺序做」)**:把复原动作抽成**可注入纯半**
`U1SpikeSetup.RestorePlayModeCore(settings)`,`Teardown` 的 `finally` 改为委派它
(判据仍单一出处 = `DataBakeMenu.AssetDatabaseBuilderIndex`)。

**结账(双列)**:

| 半边 | Landed | Proven | Proven-by |
|---|---|---|---|
| 复原动作本身 | ✅ | ✅ | `test_u1teardown_restorePlayMode_corePullsDriftedStateBack`;反证 F1(空体)⇒ **恰红 1 条** |
| null 稳健性 | ✅ | ✅ | `test_u1teardown_restorePlayMode_nullSettings_doesNotThrow` |
| **接线**(finally 段真的调复原) | ✅ | ✅ **执行判据** | 见下「第二版」 |

**第二版(2026-10-17 同日,用户裁定「现在就修」)** —— 首版接线半边靠**源码扫描**,是词法判据,
抓不住「调用挪出 `finally`」「异常被吞」。已把 `Teardown` 的 **`finally` 段整体抽成依赖注入的**
`U1SpikeSetup.RunTeardownFinally(TeardownDeps)`(`GetSettings` / `Restore` / `ClearProgress` / `Warn`
四项可注入),夹具**执行真 finally 逻辑**:

| 测 | 断言 | 反证(各恰红对应条) |
|---|---|---|
| `runs_restoreOnSettingsFromDeps` | 复原被调用,且拿到 `GetSettings()` 的返回值 | G1 删调用 / G2 传 `null` |
| `restoreThrows_warnsButStillClearsAndDoesNotRethrow` | 复原抛 ⇒ 告警含原因与补救菜单、**不向上抛**、进度条仍清 | G3 / G4 |
| `nullDeps_throws` | null 依赖集立即抛,不静默空转 | G5 |

**仍未覆盖(如实登记)**:`Teardown` 方法体里「调用 `RunTeardownFinally(RealTeardownDeps())`」这一行
本身 —— 那是 `[MenuItem]` 入口,只能 PlayMode 手跑或反射驱动。但它若被删,`finally` 段即整体失效
且无替代调用,风险面已从「词法不可测」收窄到「入口一行」。

⚠️ **历史记录(不删)**:首版 `isWiredIntoTeardown` 的**首跑是假绿** —— 扫描窗一路扫到文件尾,
而文档注释与方法定义里也有同名符号,删掉调用照样命中。该测已于第二版**整体删除**,由上面三条执行判据取代。

**残余(不变)**:「Setup 跑了但 Teardown 从没跑」的机器仍会漂移 ⇒ 兜底 = 菜单
「Play Mode 归位」+ 机器前置门 `test_playModeScript_isAssetDatabase`。

---

## 登记格式(后续沿用)

```
- **[YYYY-MM-DD]** (<story / 对象>):<债描述> — 出处 <原件 §N:行> — 触发条件:<何时须还> — tracked from <story file path>
```
