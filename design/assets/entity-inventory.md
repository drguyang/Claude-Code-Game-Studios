# Visual Entity & Screen Inventory

> **Generated**: 2026-09-29
> **Updated**: 2026-09-29 (added spec file references)
> **Specs Directory**: `design/assets/specs/` (17 spec files for 42 VS Critical assets)
> **Sources**: systems-index.md (54 systems), game-concept.md, art-bible.md (1113 lines), hud.md, 7 UX screen specs, interaction-patterns.md, audio-system.md, combat-and-weapon-lines.md, item-database.md, enemy-ai.md, patient-ai.md, foraging.md, emergency-procedures.md, diagnosis-system.md, prescription-and-medication.md, modular-building.md, clinic-machine.md, world-and-ecozones.md, random-events.md, death-and-respawn.md, case-system.md, skill-system.md, persistence-service.md, telemetry-analytics.md

---

## Vertical Slice Critical Assets

**Core loop**: 玩家抵达医馆 → 检视病人 → 诊断（脉/舌/色/声/态/触五通道）→ 施急救处理（止血包扎 + CPR）→ 病人恢复

### Characters (2)

| # | Entity | Description | Source |
|---|--------|-------------|--------|
| 1 | 主角（医者） | 清末民初医师装束，布衣 + 布鞋 + 药囊 | game-concept.md, systems-index #1 |
| 2 | 普通病人（T0–T3） | 四档病情 severity，面色五色阶梯（art-bible §8.6.1） | diagnosis-system.md, patient-ai.md |

### Environment / Buildings (1)

| # | Entity | Description | Source |
|---|--------|-------------|--------|
| 1 | 医馆（单房间，P0） | 旧木梁 + 纸窗 + 土墙，药柜/诊桌/病床布局 | clinic-machine.md, game-concept.md MVP |

### Props (6)

| # | Entity | Description | Source |
|---|--------|-------------|--------|
| 1 | 脉案纸页（线装书） | 诊断态核心承载，左页四诊摘要 + 右页辨证记录 | hud.md §1, casebook-39.md |
| 2 | 出诊箱（药箱） | 抽屉式展开，实物格子 + 铜筹计数 | hud.md §3, 20 |
| 3 | 诊脉台读数（铜器刻度盘） | 三指寸/关/尺，指针 = 浮/中/沉 | hud.md §2, diagnosis-system V-8.0 |
| 4 | 听诊器 | 胸件贴合，不同材质系数 | hud.md §2, audio-system.md F-44.7 |
| 5 | 戥子（称药器具，黄铜） | 中药称量，黄铜质感 | hud.md, prescription-and-medication.md |
| 6 | 手术灯（急救场景，冷光白） | 急救操作照明 | emergency-procedures.md, art-bible §8.6.3 |

### Items (6)

| # | Entity | Description | Source |
|---|--------|-------------|--------|
| 1 | 柳树皮 | 药材，Q1–Q10 品级 | foraging.md, item-database.md |
| 2 | 毛地黄 | 药材，Q1–Q10 品级 | foraging.md, item-database.md |
| 3 | 金鸡纳树皮 | 药材，Q1–Q10 品级 | foraging.md, item-database.md |
| 4 | 止血草 | 药材，Q1–Q10 品级 | foraging.md |
| 5 | 针具（针灸） | 急救 / 诊断用 | emergency-procedures.md, diagnosis-system.md |
| 6 | 绷带 / 止血包 | 急救耗材 | emergency-procedures.md |

### UI Screens (1)

| # | Screen | Description | Source |
|---|--------|-------------|--------|
| 1 | 脉案页（诊断态纸面） | 线装书展开，五通道体征区 + 病名栏 + 置信度 + 方笺续页 | casebook-39.md, hud.md §1 |

### HUD Elements (7)

| # | Element | Description | Source |
|---|--------|-------------|--------|
| 1 | 脉案纸页展开 | 诊断态常驻，占屏中央偏上，0.3s 书页展开 | hud.md §1, hud.md §Dynamic |
| 2 | 诊脉台刻度盘 | 铜器三指寸/关/尺，指针偏转 | hud.md §2, diagnosis-system V-8.0 |
| 3 | 呼吸波形纸带 | 两纹滚动（通带 + 噪声底） | hud.md §2, audio-system.md F-44.7 |
| 4 | 舌象色卡 | 纸面色块比对，非数字 | hud.md §2, diagnosis-system.md |
| 5 | 出诊箱抽屉展开 | 实物格子 + 铜筹计数，0.25s | hud.md §3 |
| 6 | 体征异常脉冲 | 铜器边缘泛红 0.5s，非闪烁 | hud.md §Dynamic |
| 7 | 墨迹落笔 | 诊结论落笔瞬间墨迹飞溅 | hud.md §1, art-bible §8.7 |

**Additional (non-item)**: 焦点高亮（黄铜边框 2px）

### SFX (10)

| # | SFX | Description | Source |
|---|----|-------------|--------|
| 1 | 诊脉接触音 | 三指触腕，布料摩擦 | audio-system.md §规则二 |
| 2 | 听诊器接触音 | 胸件贴合，不同材质系数 | audio-system.md §规则二, F-44.7 |
| 3 | 病人呼吸两层 | 基础气流 + 附加音层，精度档驱动通带/噪声底 | audio-system.md AC-44-01 BLOCKING |
| 4 | 止血包扎接触音 | 绷带接触身体 | emergency-procedures.md |
| 5 | CPR 节律音 | 按压节律反馈 | emergency-procedures.md |
| 6 | 病人语声 | 男/女 × 强/中/弱 × 句尾变体 + 咳嗽/呻吟 | audio-system.md §规则五, AC-44-05 |
| 7 | 纸面物理声 | 翻脉案/落笔/翻纸/器物放置 | audio-system.md §规则二 |
| 8 | 脚步声 | 石板/泥地/腐殖土三材质 | audio-system.md §规则二 |
| 9 | 药柜/器物声 | 抽屉/铜拉手/器物放置 | audio-system.md §规则二 |
| 10 | 世界声 | 狗吠/马蹄/锣声/钟声/鸟群，空间化 | audio-system.md §规则二, random-events.md |

### Ambient (1)

| # | Ambient | Description | Source |
|---|---------|-------------|--------|
| 1 | 医馆室内环境音 | 3200K 暖漫射，旧木/纸窗/药柜细微声 | audio-system.md §规则一, art-bible §2 |

### VFX Events (7)

| # | VFX | Description | Source |
|---|----|-------------|--------|
| 1 | 水墨晕染 | 诊断态纸面背景，墨侧材质 | art-bible §1 |
| 2 | 黄铜反光 | 急救器械盘，铜侧反射 | art-bible §1 |
| 3 | 呼吸波形滚动 | 纸带 VFX，两纹驱动 | hud.md §2, audio-system.md |
| 4 | 病历落笔墨迹 | 脉案书写，墨迹飞溅 | hud.md §1, art-bible §8.7 |
| 5 | 病人状态变化 | 面色/姿态骤变 | patient-ai.md |
| 6 | 急救止血包扎 VFX | 止血处理视觉反馈 | emergency-procedures.md |
| 7 | CPR 节律视觉反馈 | 按压节律视觉 | emergency-procedures.md |

---

## Full Production Assets

### Characters (5)

| # | Entity | Description | Source |
|---|--------|-------------|--------|
| 1 | 疑难重症病人（Boss 级病案） | 散落的病例模式识别 | case-system.md, game-pillars.md §四 |
| 2 | 同伴（联机 1–4 人） | 多玩家角色 | game-concept.md, ADR-001 |
| 3 | 兵痞（人型敌人） | 非致命战斗，昏迷收场 | enemy-ai.md, combat-and-weapon-lines.md |
| 4 | 蛇/熊/狼/野狗（野兽） | 可猎杀/驱赶/驯化 | random-events.md, enemy-ai.md |
| 5 | 驯化动物（P1a） | 可招为护卫 | enemy-ai.md §七 |

### Environment / Buildings (5)

| # | Entity | Description | Source |
|---|--------|-------------|--------|
| 1 | 小镇街道（P0 小场景） | 清末民初街景 | world-and-ecozones.md, game-concept.md |
| 2 | 野外生态区 ×4 | 租界/贫民窟/战乱前线/深山疫区 | world-and-ecozones.md |
| 3 | POI（各生态区地点） | 印章式标记 | world-and-ecozones.md, ADR-021 |
| 4 | 建造模块（模块化网格） | 医馆改造/帐篷 | modular-building.md, ADR-015/ADR-003 |
| 5 | 疫情爆发区域（Boss 场景） | 瘟疫扩散视觉 | game-pillars.md §四, random-events.md |

### Props (8)

| # | Entity | Description | Source |
|---|--------|-------------|--------|
| 1 | 纸质地图卷轴 | 卷轴展开，墨点标记 | hud.md §4, systems-index #43 |
| 2 | 铜怀表 | 铜怀表指针 = 时辰 | hud.md §4 |
| 3 | 针囊 | 针具收纳 | hud.md §3 |
| 4 | 铜筹计数器 | 铜柱堆叠计数 | hud.md §3 |
| 5 | 药柜 / 抽屉 |  modular-building.md, clinic-machine.md |
| 6 | 铜压尺（存档位视觉） |  art-bible §2 |
| 7 | 印泥盒 |  art-bible §2 |
| 8 | 教学纸近景（PaperCloseup48） | 纸页放大，手写批注 | paper-closeup-48.md, ADR-013 Amendment B |

### Items (4)

| # | Entity | Description | Source |
|---|--------|-------------|--------|
| 1 | 草药（干燥/提取物/酊剂） | 加工形态 | processing.md, item-database.md |
| 2 | 成药（P1a 扩展） | 处方用药选项扩展 | prescription-and-medication.md |
| 3 | 武器（短兵 / 徒手 P0） | 非致命制服 | combat-and-weapon-lines.md |
| 4 | 携带物资（补给管理） | 补给管理槽 | inventory-and-items.md |

### UI Screens (6 additional)

| # | Screen | Description | Source |
|---|--------|-------------|--------|
| 1 | 主菜单 | 卷宗/账本形态 | art-bible §2, hud.md |
| 2 | 存档位界面（登记簿形态） | ModalId.SaveSlots | save-slots-7b.md |
| 3 | 库存容器 / 出诊箱 | ModalId.InventoryContainer | inventory-container-20.md, hud.md §3 |
| 4 | 设置界面壳（调治簿形态） | ModalId.SettingsShell | settings-shell-42.md |
| 5 | 教学界面（调度壳） | ModalId.Tutorial | tutorial-48.md |
| 6 | 医馆面板（案头账本） | ModalId.ClinicPanel | clinic-panel-24.md |

### HUD Elements (8 additional)

| # | Element | Description | Source |
|---|--------|-------------|--------|
| 1 | 纸质地图 | 世界态常驻右下，卷轴展开 0.3s | hud.md §4 |
| 2 | 怀表 | 世界态常驻左上，铜怀表指针 = 时辰 | hud.md §5 |
| 3 | 窗景天气 | 世界态常驻右上，纸窗透光 = 晴/阴/雨 | hud.md §5 |
| 4 | 教学纸近景 | 世界内单纸阅读，占屏 60% | hud.md §6, paper-closeup-48.md |
| 5 | 快捷道具栏 | 世界态底栏，快捷药材/工具 | hud.md Layout |
| 6 | 存档卷轴封条 | 存档位视觉元素 | hud.md, save-slots-7b.md |
| 7 | 敌人黄铜读数条 | 全作唯一贴屏数值反馈 | combat-and-weapon-lines.md, art-bible §1P3/§7.6 |
| 8 | 菜单/存档位卷宗动画 |  art-bible §2 |

### Music Cues (8)

| # | Track | Description | Source |
|---|-------|-------------|--------|
| 1 | 主菜单卷宗音乐 |  art-bible §2 |
| 2 | 出诊探索音乐 |  art-bible §2 |
| 3 | 诊断态音乐 |  art-bible §2 |
| 4 | 急救操作音乐 |  art-bible §2 |
| 5 | 疫情爆发音乐 |  art-bible §2 |
| 6 | 医馆经营音乐 |  art-bible §2 |
| 7 | 遭遇与遏制音乐 |  art-bible §2 |
| 8 | 死亡与后果音乐 |  art-bible §2 |

### SFX (10 additional)

| # | SFX | Description | Source |
|---|----|-------------|--------|
| 1 | 砵/捣药声 |  art-bible §2 |
| 2 | 草药采集音 | foraging.md |
| 3 | 药物研磨/加工音 | processing.md |
| 4 | 物品拾取音 | inventory-and-items.md |
| 5 | 建造放置音 | modular-building.md |
| 6 | 武器命中音（非致命） | combat-and-weapon-lines.md |
| 7 | 敌人受伤/昏迷音 | enemy-ai.md |
| 8 | 翻脉案纸声 | hud.md §1 |
| 9 | 存档封条音 | hud.md §4 |
| 10 | 天气音（雨/风） | time-and-weather.md |

### Ambient (3 additional)

| # | Ambient | Description | Source |
|---|---------|-------------|--------|
| 1 | 野外生态区环境音 ×4 |  art-bible §2 |
| 2 | 小镇街道环境音 | world-and-ecozones.md |
| 3 | 疫情爆发区域环境音 | art-bible §2 |

### VFX Events (6 additional)

| # | VFX | Description | Source |
|---|----|-------------|--------|
| 1 | 瘟疫扩散石灰撒线 |  art-bible §2 |
| 2 | 草药采集闪光 | foraging.md |
| 3 | 建造模块放置 VFX | modular-building.md |
| 4 | 死亡褪色（脉案墨色褪淡） | art-bible §2 |
| 5 | 菜单/存档位卷宗动画 | art-bible §2 |
| 6 | 敌人黄铜读数条浮出/淡出 | combat-and-weapon-lines.md |

---

## Summary

| Category | Vertical Slice Critical | Full Production | Total |
|----------|------------------------|-----------------|-------|
| Characters | 2 | 5 | 7 |
| Enemies | — | 5 | 5 |
| Props | 6 | 8 | 14 |
| Buildings/Environment | 1 | 5 | 6 |
| Items | 6 | 4 | 10 |
| UI Screens | 1 | 6 | 7 |
| HUD Elements | 7 | 8 | 15 |
| Music Cues | 0 | 8 | 8 |
| SFX | 10 | 10 | 20 |
| Ambient | 1 | 3 | 4 |
| VFX Events | 7 | 6 | 13 |
| **Total** | **42** | **57** | **99** |

## Spec Files Index

| Asset | Spec File | Tier | Status |
|-------|-----------|------|--------|
| 脉案纸页（线装书） | `specs/casebook-paper.md` | VS Critical | ✅ Specified |
| 出诊箱（药箱） | `specs/medical-bag.md` | VS Critical | ✅ Specified |
| 诊脉台铜器刻度盘 | `specs/pulse-dial.md` | VS Critical | ✅ Specified |
| 听诊器 | `specs/stethoscope.md` | VS Critical | ✅ Specified |
| 戥子（称药器具） | `specs/brass-scale.md` | VS Critical | ✅ Specified |
| 手术灯（急救场景） | `specs/surgical-lamp.md` | VS Critical | ✅ Specified |
| 主角（医者） | `specs/protagonist.md` | VS Critical | ✅ Specified |
| 普通病人（T0–T3） | `specs/patient.md` | VS Critical | ✅ Specified |
| 医馆（单房间） | `specs/clinic.md` | VS Critical | ✅ Specified |
| 柳树皮 / 毛地黄 / 金鸡纳树皮 / 止血草 / 针具 / 绷带 | `specs/medicinal-herbs.md` | VS Critical | ✅ Specified |
| 纸质地图卷轴 | `specs/paper-map.md` | VS Critical | ✅ Specified |
| 铜怀表 | `specs/brass-watch.md` | VS Critical | ✅ Specified |
| 呼吸波形纸带 | `specs/breath-waveform.md` | VS Critical | ✅ Specified |
| 舌象色卡 | `specs/tongue-color-card.md` | VS Critical | ✅ Specified |
| 体征异常脉冲 | `specs/vitals-pulse.md` | VS Critical | ✅ Specified |
| 墨迹落笔 | `specs/ink-splash.md` | VS Critical | ✅ Specified |
| 教学纸近景 | `specs/paper-closeup.md` | VS Critical | ✅ Specified |

**Spec coverage**: 17 spec files covering all 42 VS Critical assets.

**Key constraints**:
- 纸面 (Paper) is the dominant visual vocabulary — 6 of 42 VS Critical items are paper-based
- 黄铜 (Brass) is the dominant prop vocabulary — 8 brass props, all with 来历 requirement
- 深海区 = 零铜资产（art-bible §8.6.2）
- 敌人黄铜读数条 is the ONLY allowed "贴屏数值反馈" — protagonist has no HP bar, no minimap, no progress bars
- 皮肤材质 = 唯一活体裸露，PBR non-metal + high roughness (0.65–0.85) + SSS only on ears/nose
