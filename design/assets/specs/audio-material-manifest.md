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

| # | cue_id | category | trigger_source | loop | bus | variant_count (草案) | 说明 |
|---|--------|----------|----------------|------|-----|---------------------|------|
| 1 | `phys_breath_base` | ContinuousPhysiology | 病人体征状态 | ✅ | Ambience | 4 (T0–T3 流速) | 基础气流，精度档驱动通带/噪声底（AC-44-01） |
| 2 | `phys_breath_rale` | ContinuousPhysiology | 病人体征状态 | ✅ | Stethoscope | 3 (细湿啰音密度) | 附加音层，非连续水声（AC-44-08） |
| 3 | `phys_cough` | ContinuousPhysiology | 病人体征触发 | ❌ | SFX | 6 (男/女 × 强/中/弱) | 咳嗽单发，变体库混合 |
| 4 | `action_cpr_press` | PlayerAction | CPR 动作 | ❌ | SFX | 1 | 按压节律 550ms，承 O-10-3 |
| 5 | `action_pickup` | PlayerAction | 拾取物品 | ❌ | SFX | 2 (药材/工具) | 不同材质系数 |
| 6 | `action_build_place` | PlayerAction | 建造放置 | ❌ | SFX | 1 | 模块落地 |
| 7 | `action_weapon_hit` | PlayerAction | 非致命命中 | ❌ | SFX | 3 (短兵/徒手/钝) | 非致命音色 |
| 8 | `action_enemy_fall` | PlayerAction | 敌人昏迷 | ❌ | SFX | 2 (人型/野兽) | 倒地，承 ADR-016 §二 |
| 9 | `exam_stethoscope_contact` | PlayerExamAction | 听诊器贴合 | ❌ | Stethoscope | 2 (胸件/背件) | 不同材质系数 |
| 10 | `exam_pulse_contact` | PlayerExamAction | 诊脉三指触腕 | ❌ | SFX | 1 | 布料摩擦 |
| 11 | `exam_bandage_contact` | PlayerExamAction | 包扎接触身体 | ❌ | SFX | 1 | 绷带接触 |
| 12 | `exam_page_turn` | PlayerExamAction | 翻脉案纸 | ❌ | UICue | 2 (正向/回翻) | 纸面物理声 |
| 13 | `exam_save_seal` | PlayerExamAction | 存档封条 | ❌ | UICue | 1 | 印泥 + 纸声 |
| 14 | `world_footstep` | WorldPerceptible | 玩家移动 | ❌ | Ambience | 3 (石板/泥地/腐殖土) | 三材质，承 audio-system.md §规则二 |
| 15 | `world_clinic_ambience` | WorldPerceptible | 医馆内常驻 | ✅ | Ambience | 1 | 3200K 暖漫射 + 旧木/纸窗/药柜细微声 |
| 16 | `world_distant_bark` | WorldPerceptible | 世界事件 | ❌ | Ambience | 2 (近/远) | 空间化，承 ADR-028 |
| 17 | `music_encounter_tense` | EncounterMusicLayer | 遭遇触发 | ✅ | Music | 1 | G1–G3 约束，不独立播放 |
| 18 | `phys_groan` | ContinuousPhysiology | 病人体征触发 | ❌ | SFX | 6 (男/女 × 强/中/弱) | T1–T3 呻吟，承 AC-44-05 语声变体 |

> **变体库混合**：`variant_count` 为每个 cue 的 `.wav` 文件数，不是播放实例数。
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
