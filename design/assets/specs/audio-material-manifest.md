# Audio Material Manifest

> **Tier**: Full Production
> **Category**: Audio
> **Source**: audio-system.md event table schema, diagnosis-system.md V-8.7, emergency-procedures.md
> **ADR Ref**: ADR-018（音频架构 · 只渲染不持状态 · 无提示音铁律）; ADR-028（世界语境声源归属）
> **Schema**: `assets/data/audio_events.json` → ADR-014 两阶段烘焙 → `audio_events.cooked`

---

## 白名单约束

所有 cue 必须落在以下五类之一（ADR-018 §一 / §六 无提示音铁律机械化）：

| Category | 语义 | 数量上限 |
|----------|------|----------|
| `ContinuousPhysiology` | 持续生理声（呼吸 / 咳嗽 / 附加音层） | 与在场病人数成正比 |
| `PlayerAction` | 玩家主动触发声（拾取 / 建造 / 命中 / 倒地） | 按动作类型 |
| `PlayerExamAction` | 玩家诊断触发的接触声（听诊 / 诊脉 / 包扎 / 翻纸） | 按诊察类型 |
| `WorldPerceptible` | 世界可感知声（脚步 / 环境 / 远声） | 按 POI / 材质 |
| `EncounterMusicLayer` | 遭遇紧张乐层（G1–G3 约束，不独立播放） | 1 层 |

> **禁用**：sting / jingle / ducking / 素材切换**报**状态（AC-44-09 BLOCKING）
> **混音总线**：Master / Music / Ambience / Voice / SFX / Stethoscope / UICue

---

## 素材清单草案

> ### ⚠️ cue_id 真源声明(2026-10-16 立)
>
> **`cue_id` 的唯一真源 = `assets/data/audio_events.json`**(经 ADR-014 两阶段烘焙 →
> `audio_events.cooked`;白名单/校验 = `AudioEventTableGates`)。
> **本表只承载三件事:素材族划分 · 变体量(`variant_count`)· 调参旋钮。**
>
> **本表左列的「草案 cue_id」不是运行期 cue id** —— 它是本规格件 2026-09-30 起草时的
> **工作名**(小写下划线式),与事件表真源(PascalCase 式)**零重合**。
> 右列「事件表真源 cue_id」= 逐行对账结果;**内容批录素材时以右列为准**。
>
> 立此声明的理由(实测):两套命名此前**无任何门会报** —— `AC-44-D6` 的存在性门只查
> 「`rows[].assets[]` 引的文件在不在 `unity/Assets/Audio/`」,**不查 cue 名**。
> 内容批照左列录素材,会录出一批 `cue_id` 对不上真源的文件而全绿。

| # | 草案 cue_id(工作名) | 事件表真源 cue_id | category | trigger_source | loop | bus | variant_count (草案) | 说明 |
|---|--------|--------|----------|----------------|------|-----|---------------------|------|
| 1 | `phys_breath_base` | ⚠️ **1 → 2**(见下) | ContinuousPhysiology | 病人体征状态 | ✅ | Ambience | 4 (T0–T3 流速) | 基础气流，精度档驱动通带/噪声底（AC-44-01） |
| 2 | `phys_breath_rale` | `BreathLayer_Adventitious_Fine` | ContinuousPhysiology | 病人体征状态 | ✅ | Stethoscope | 3 (细湿啰音密度) | 附加音层，非连续水声（AC-44-08） |
| 3 | `phys_cough` | `Voice_Cough_Damp` | ContinuousPhysiology | 病人体征触发 | ❌ | ⚠️ SFX → **Voice** | 6 (男/女 × 强/中/弱) | 咳嗽单发，变体库混合 |
| 4 | `action_cpr_press` | —(事件表未登记) | PlayerAction | CPR 动作 | ❌ | SFX | 1 | 按压节律 550ms，承 O-10-3 |
| 5 | `action_pickup` | —(事件表未登记) | PlayerAction | 拾取物品 | ❌ | SFX | 2 (药材/工具) | 不同材质系数 |
| 6 | `action_build_place` | —(事件表未登记) | PlayerAction | 建造放置 | ❌ | SFX | 1 | 模块落地 |
| 7 | `action_weapon_hit` | —(事件表未登记) | PlayerAction | 非致命命中 | ❌ | SFX | 3 (短兵/徒手/钝) | 非致命音色 |
| 8 | `action_enemy_fall` | —(事件表未登记) | PlayerAction | 敌人昏迷 | ❌ | SFX | 2 (人型/野兽) | 倒地，承 ADR-016 §二 |
| 9 | `exam_stethoscope_contact` | —(事件表未登记) | PlayerExamAction | 听诊器贴合 | ❌ | Stethoscope | 2 (胸件/背件) | 不同材质系数 |
| 10 | `exam_pulse_contact` | —(事件表未登记) | PlayerExamAction | 诊脉三指触腕 | ❌ | SFX | 1 | 布料摩擦 |
| 11 | `exam_bandage_contact` | —(事件表未登记) | PlayerExamAction | 包扎接触身体 | ❌ | SFX | 1 | 绷带接触 |
| 12 | `exam_page_turn` | —(事件表未登记) | PlayerExamAction | 翻脉案纸 | ❌ | UICue | 2 (正向/回翻) | 纸面物理声 |
| 13 | `exam_save_seal` | —(事件表未登记) | PlayerExamAction | 存档封条 | ❌ | UICue | 1 | 印泥 + 纸声 |
| 14 | `world_footstep` | —(事件表未登记) | WorldPerceptible | 玩家移动 | ❌ | Ambience | 3 (石板/泥地/腐殖土) | 三材质，承 audio-system.md §规则二 |
| 15 | `world_clinic_ambience` | —(事件表未登记) | WorldPerceptible | 医馆内常驻 | ✅ | Ambience | 1 | 3200K 暖漫射 + 旧木/纸窗/药柜细微声 |
| 16 | `world_distant_bark` | —(事件表未登记) | WorldPerceptible | 世界事件 | ❌ | Ambience | 2 (近/远) | 空间化，承 ADR-028 |
| 17 | `music_encounter_tense` | `Music_Encounter_Layer` | EncounterMusicLayer | 遭遇触发 | ✅ | Music | 1 | G1–G3 约束，不独立播放 |
| 18 | `phys_groan` | —(事件表未登记) | ContinuousPhysiology | 病人体征触发 | ❌ | SFX | 6 (男/女 × 强/中/弱) | T1–T3 呻吟，承 AC-44-05 语声变体 |

**对账结果**:18 行中 **3 行命中**真源(草案 #2 / #3 / #17),**1 行是 1 → 2 拆分**(草案 #1),
**14 行事件表未登记**(表当前只有 5 行)。

**两处结构性分叉(不是笔误,是两件不同的事)**:

1. **草案 #1 `phys_breath_base` → 事件表拆成两条**:
   `BreathLayer_Base_Calm`(**bus = Stethoscope**,听诊窗内的基础气流)与
   `WorldBreath_Baseline`(**bus = Ambience**,带 `min_distance` / `max_distance` /
   `occlusion_lowpass`,世界语境呼吸,承 ADR-028)。
   草案用一行 + `bus = Ambience` 把两者并了 —— 若照草案录,**听诊窗会缺基础气流**
   (`AC-44-01` BLOCKING:「绝不静音」)。以事件表为准。
2. **草案 #3 `phys_cough` 的 bus 与真源不符**:草案写 SFX,事件表 `Voice_Cough_Damp`
   是 **Voice**(且带 `subtitle_text` —— `AC-44-15 [A]` 的字幕半边挂在 Voice 行上)。
   以事件表为准。

> **变体库混合**：`variant_count` 为每个 cue 的 `.wav` 文件数，不是播放实例数。

---

## ✅ 冻结命名与 bus 裁定(2026-10-17 · 用户裁定,内容批照此录制)

> **本节优先于上文「素材清单草案」** —— 草案是 2026-09-30 的**工作名**,本节是**已裁定的冻结值**。
> 两节冲突处**以本节为准**。三问均已由用户 2026-10-17 裁定,见 `tech-debt-register` 内容-02 / 内容-04。

### 一、R-2 余 7 项 —— 冻结清单(**18 个新文件**)

**裁定原则**:① **bus 以 GDD `:798-810` 分层表为准**(草案清单 3 处与 GDD 明文冲突,已改写);
② 覆盖范围 = **`entity-inventory.md` 全集**;③ 脚步材质 = **3 材质 + 雪档**。

| R-2 项 | **cue id(冻结)** | bus | loop | whitelist_category | trigger_source | **文件(冻结)** | 量 |
|---|---|---|---|---|---|---|---|
| [SFX #1] 诊脉接触音 | `Pulse_Contact` | **Stethoscope** | ✗ | `PlayerExamAction` | `PlayerExamAction` | `sfx_pulse_contact_01.wav` | 1 |
| [SFX #2] 听诊器接触音 | `Steth_Contact` | **Stethoscope** | ✗ | `PlayerExamAction` | `PlayerExamAction` | `sfx_steth_contact_{chest,back}_01.wav` | 2 |
| [SFX #7] 纸面物理声 | `Page_Turn` | **UICue** | ✗ | `PlayerExamAction` | `PlayerExamAction` | `ui_page_turn_{fwd,back}_01.wav` | 2 |
| [SFX #8] 脚步声 | `Footstep_Base` | **SFX** | ✗ | `WorldPerceptible` | `WorldPerceptible` | `sfx_footstep_{stone,dirt,humus,snow}_01.wav` | 4 |
| [SFX #9] 药柜/器物声 | `Cabinet_Prop` | **SFX** | ✗ | `PlayerAction` | `PlayerAction` | `sfx_cabinet_{drawer,handle}_01.wav` · `sfx_prop_place_01.wav` | 3 |
| [SFX #10] 世界声 | `World_Sfx_Base` | **SFX** | ✗ | `WorldPerceptible` | `WorldPerceptible` | `sfx_world_{dog,horse,gong,bell,birds}_01.wav` | 5 |
| [SFX #10] 世界声 · **预告线索补 4 项** | `World_Sfx_Base`(同一 cue,`assets[]` 共 9) | **SFX** | ✗ | `WorldPerceptible` | `WorldPerceptible` | `sfx_world_{door_bell,vendor_cry,throat_clear,crying}_01.wav` | 4 |

> ⚠️ **2026-10-17 第二轮裁定:上两行已作废 —— 世界声拆成 4 条 cue。**
> 见下 §一之二 的**最终归属**。保留原行作变更记录,不删。
| [Ambient #1] 医馆室内环境音 | `Clinic_Room_Tone` | **Ambience** | ✓ | `WorldPerceptible` | `WorldPerceptible` | `amb_clinic_room_loop.wav` | 1 |

**与草案清单的四处差异(逐处登记,非静默改写)**:

| 行 | 草案清单 | **冻结值** | 依据 |
|---|---|---|---|
| 诊脉接触音 | `SFX` | **`Stethoscope`** | GDD `:810`「把脉(腕部脉动)—— **与听诊同层**」 |
| 纸面物理声 | `UICue` | **`UICue`**(不变) | GDD 规则二 UICue「笔尖 / 翻纸(**仅行为反馈**)」—— 与草案一致;⚠️ `entity-inventory.md` #7 把它编在 **SFX** 类,**类别 ≠ bus**,不冲突 |
| 脚步声 | `Ambience` | **`SFX`** | GDD `:800`「**SFX** \| 纸面 / 器物 / **脚步**」 |
| 世界声 | `Ambience`(且只覆盖「狗吠」1 项) | **`SFX`** + **5 项** | GDD `:800` 分层 + `entity-inventory.md` #10 五项(狗吠/马蹄/锣声/钟声/鸟群) |

> ⚠️ **药柜/器物声在草案清单内无对应行(漏项)** —— 本表补一行(`Cabinet_Prop`,bus `SFX`)。

### 一之二、世界声的 **9 项**归属(2026-10-17 第二轮裁定 · 用户裁 = **9** · **拆 4 条 cue**)

**裁定依据**:`entity-inventory.md` #10 只列 5 项,**漏了 52 档位表的 4 项**;
`random-events.md:1110-1113` 是预告线索的**上游权威件**,逐档列明 9 项。
用户 2026-10-17 两项裁定:① **按 9 项**;② **拆成 4 条 cue**(按 52 的档位)。

**最终归属(冻结)**:

| 52 档 | **cue id(冻结)** | **冻结文件名** | bus |
|---|---|---|---|
| **威胁** | `World_Threat` | `sfx_world_dog_01.wav` | SFX |
| | | `sfx_world_horse_01.wav` | |
| | | `sfx_world_gong_01.wav` | |
| | | `sfx_world_birds_01.wav` | |
| **机会** | `World_Opportunity` | `sfx_world_door_bell_01.wav` | SFX |
| | | `sfx_world_vendor_cry_01.wav` | |
| **反应** | `World_Reaction` | `sfx_world_throat_clear_01.wav` | SFX |
| **灾难** | `World_Disaster` | `sfx_world_crying_01.wav` | SFX |
| | | `sfx_world_bell_01.wav` | |

> **为什么拆**:9 项并进一条 cue 时,事件表 `RowKeys` **没有「档位」字段** ⇒
> 运行时无法按威胁/机会/反应/灾难选音。**拆 cue 用 `cue` 键本身承载档位语义**,
> 零 schema 改动 —— 优于「新增 schema 键」(后者须改 `RowKeys` + 绑定层 + 夹具)。
>
> ⚠️ **`entity-inventory.md` #10 的 5 项与 52 的 9 项缺口** —— 本表以 52 为准;
> `entity-inventory.md` 未就地改注(归其所有者)。

### 二、12 个 `vo_` 变体 —— 冻结命名(**退役现 `m1` / `f1`**)

**裁定**:12 个**全为阻尼咳嗽**,**挂既有 `Voice_Cough_Damp`**(`assets[]` **2 → 12**);
命名统一重排为**三段式**;**现 `vo_patient_cough_damp_m1.wav` / `_f1.wav` 退役**。

```
vo_patient_cough_damp_m1_1.wav   vo_patient_cough_damp_f1_1.wav
vo_patient_cough_damp_m1_2.wav   vo_patient_cough_damp_f1_2.wav
vo_patient_cough_damp_m2_1.wav   vo_patient_cough_damp_f2_1.wav
vo_patient_cough_damp_m2_2.wav   vo_patient_cough_damp_f2_2.wav
vo_patient_cough_damp_m3_1.wav   vo_patient_cough_damp_f3_1.wav
vo_patient_cough_damp_m3_2.wav   vo_patient_cough_damp_f3_2.wav
```

| 段 | 取值 | 语义 |
|---|---|---|
| `{m\|f}` | `m` / `f` | 男 / 女 |
| `{1\|2\|3}` | `1` / `2` / `3` | **严重度:1 = 强 · 2 = 中 · 3 = 弱**(承 GDD 文字序「强/中/弱」) |
| `_{1\|2}` | `1` / `2` | 变体序号(每组合 ≥2,承 `OQ-44-2`) |

**声学判据(不变)**:阻尼咳嗽 —— **闷、短、无混响尾**;非清脆干咳。
**档位**:`DecompressOnLoad`(`loadType: 0`)—— 判据见 GDD §OQ-44-2 附(驻留 ≤ 512 KB ∧ 时长 < 5 s)。

> ### ⚠️ 两条落地约束(内容批必读)
>
> **① 12 个文件 + `assets[]` 更新须同批落地 —— 不可分批。**
> 烘焙门(AC-44-D6)要求**每一个被引 `.wav` 都存在**。分批交付 ⇒ 门当场 `throw`。
> 顺序 = **先录齐 12 个 → 再一次改 `assets[]` + 重烘焙**。
>
> **② 现 `m1` / `f1` 的真内容是否复用,归 content 批自决。**
> 二者是 story-015 交付的**真内容**(实测时长 1.2480 s / 1.5840 s),**无严重度标注**。
> 若复用,须先定它们属哪一档 —— **默认不复用**(录制 12 个全新)。

### 三、其余 5 个既有素材的真内容

`sfx_breath_base_loop_small` · `sfx_breath_adventitious_fine` · `music_encounter_layer_loop`
(及上述 2 个 `vo_`)的**声学判据**见 `docs/tech-debt-register.md` §「44 音频素材内容需求」逐族表。
⚠️ `sfx_breath_*` 须**中性可滤波**(禁自带音高 / 禁自带环境)—— 否则 `tier_map` 的精度档失效(`AC-44-01` BLOCKING)。
> 一条 cue 运行时随机抽取变体组合播放（男/女 × 强/中/弱 × 句尾）。

---

## Tuning Knobs（数值归用户）

以下参数在 `assets/data/audio_events.json` 的对应 cue 条目中可调：

| 参数 | 范围 | 说明 |
|------|------|------|
| `volume_db` | −60 … 0 | 每条 cue 独立音量 |
| `stethoscope_lowpass_hz` | 200 … 2000 | 听诊器总线低通截止 |
| `stethoscope_lp_smoothing` | 0 … 1 | 滤波平滑系数 |
| `breath_rate_mod` | 0.5 … 2.0 | 呼吸速率乘数（精度档驱动） |
| `rale_density` | 0 … 1 | 附加音层密度（T1/T2 驱动） |
| `encounter_crossfade_s` | 0 … 5 | 遭遇乐层淡入时长 |
| `spatial_min_distance` | 1 … 10 | 世界声空间化近距 |
| `spatial_max_distance` | 10 … 100 | 世界声空间化远距 |

> **数值轮**：曲线参数 / 半衰期 / 权重等**逐 cue 数值冻结未动**。
> 用户自己调。

---

## Asset Generation Record(§8.10.2)

本件为**音频素材清单**(非位图资产), `§8.10` 的 **AI 文生图**生成记录**不适用**。
音频素材的来源 / 许可证 / 生成方式登记见本件「素材清单草案」表;
若日后引入 AI 音频生成, 另按同型字段(prompt / model / seed)在该表增列。

## Open Questions

- [ ] `variant_count` 上限是否需要配置（编解码器对单 cue 变体数有内存影响）
- [ ] `world_distant_bark` 是否扩展为 `world_world_perceptible` 池（狗吠/马蹄/锣声/钟声/鸟群）
- [ ] 联机时远端病人的 `ContinuousPhysiology` cue 是否走 ADR-001 第二 QoS 位置同步（空间化声源跟随远端实体）

---

## Related Files

- `design/gdd/audio-system.md` — 音频系统 GDD（事件表 schema / 白名单 / 调参旋钮）
- `design/gdd/diagnosis-system.md` V-8.7 — 诊断态音频需求（AC-44-01…08）
- `design/gdd/emergency-procedures.md` — 急救操作音频需求
- `docs/architecture/adr-018-audio-architecture.md` — 音频架构 ADR
- `docs/architecture/adr-028-world-sound-source-ownership.md` — 世界声源归属 ADR
- `design/assets/specs/stethoscope.md` — 听诊器资产 spec
- `design/assets/specs/brass-scale.md` — 戥子资产 spec
