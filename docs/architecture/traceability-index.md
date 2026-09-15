# 架构可追溯性索引(Traceability Index)

> **Last Updated**: 2026-09-15(ADR-020 落盘后同步 —— R-1…R-15 全部结清)
> **Mode**: `/architecture-review`(full)
> **Engine**: Unity 6.3 LTS
> **上游报告**: `docs/architecture/architecture-review-2026-09-15.md`
> **机器可读件**: `docs/architecture/tr-registry.yaml`
> **基线**: 163 条 TR(首建 148 条;ADR-017/018/019 轮增补 6 条;ADR-020 轮增补 9 条)

## 怎么读这张表

| 列 | 含义 |
|----|------|
| TR-ID | 稳定需求 ID。**永不复用、永不重编号、永不删除**,只追加 |
| GDD | 需求来源设计文档 |
| 需求 | 架构必须提供的东西 |
| ADR | 覆盖它的架构决策 |
| 状态 | ✅ 已覆盖 · ⚠️ 部分覆盖 / 有歧义 · ❌ 无覆盖 |

**状态口径**:✅ 需有 ADR 明文或可辩护的隐含覆盖;⚠️ 覆盖不完整、或有第二条 ADR 与之冲突;
❌ 无任何 ADR 触及。GDD 内部参数与 schema 形状若无 ADR 即为 ❌ —— 它们不需要 ADR 裁决,
但**没有别处可登记**,故全部入表。

---

## 汇总

| GDD | 系统 | TR 数 | ✅ | ⚠️ | ❌ |
|-----|------|-------|---|----|----|
| `case-system.md` | 37 病例系统 | 27 | 17 | 4 | 6 |
| `item-database.md` | 21a 物品与配方 | 32 | **13** | 1 | **18** |
| `random-events.md` | 52 随机事件导演 | **32** | **13** | 2 | 17 |
| `disease-simulation.md` | 9 疾病与伤情 | 22 | **17** | **2** | 3 |
| `diagnosis-system.md` | 8 诊断与体征 | **25** | **9** | 5 | 11 |
| `skill-system.md` | 30 技能与熟练度 | 8 | 0 | 0 | 8 |
| `game-concept.md` | 全案 | 8 | 5 | 0 | 3 |
| *(无独立 GDD)* | **1 玩家移动 + 2 摄像机** | **9** | **9** | 0 | 0 |
| **合计** | | **163** | **83** | **14** | **66** |

> 与首次落表(47 / 21 / 80)之差 = B-2 修正使 `TR-case-008`、`TR-randomevents-005`
> 由 ⚠️ 升为 ✅;复查轮(`consistency`,2026-09-15)重裁
> `TR-disease-013` / `014`(❌ → ✅)、`TR-disease-019`(⚠️ → ✅)、`TR-disease-008`(✅ 口径重述,
> 不改状态)共 4 条 —— ✅ +3 / ⚠️ −1 / ❌ −2。**ID 无增删改,总量 148 不变。**
> 随后 ADR-012(2026-09-15)再推 `TR-disease-003/015/016`、`TR-itemdb-022`(⚠️ → ✅,共 4 条)
> —— ✅ +4 / ⚠️ −4。
> 随后 ADR-013(2026-09-15)`TR-concept-008`(⚠️ → ✅,R-6 呈现侧落定)+ `TR-diag-020`(❌ → ⚠️,机制定)
> —— ✅ +1 / ⚠️ +1 / ❌ −1。
> 随后 ADR-014(2026-09-15,数据管线,合并 R-7 + R-8)`TR-itemdb-025/026/027`(❌ → ✅,共 3 条)
> —— ✅ +3 / ❌ −3。`TR-itemdb-032` 加 `adr: ADR-014`(状态不变)。
> 随后 ADR-015(2026-09-15,世界几何,合并 R-9 + R-10)`TR-randomevents-018`(❌ → ✅,
> 生态区绑定退化为整数查表)· `TR-randomevents-021`(❌ → ⚠️,坐标类型 + 锚点来源侧已覆盖,
> 逐枚举解析规则仍待)—— ✅ +1 / ⚠️ +1 / ❌ −2。`TR-itemdb-031` 加位置侧 note(状态不预判)。
> **汇总 64 → 65 ✅ / 13 → 14 ⚠️ / 71 → 69 ❌。ID 无增删改,总量 148 不变。**
> 随后 ADR-016(2026-09-15,AI 架构,合并 R-14)`TR-randomevents-028` · `TR-randomevents-029`
> (❌ → ✅,共 2 条:52→37→13 单向链;「不锁定玩家」= 27 侧实现约束)—— ✅ +2 / ❌ −2。
> 随后 **ADR-017 / 018 / 019**(2026-09-15,**R-11 是否 DOTS / R-12 音频 / R-15 遥测**):
> **ID 增补 6 条**(148 → **154**)—— `TR-randomevents-032`(预告线索音频侧,ADR-018)
> + `TR-diag-021…025`(V-8.7 的 D-8-8 四条硬需求 + 无提示音铁律,ADR-018 立 AC,共 5 条)。
> 状态变更:`TR-randomevents-024` ❌ → ✅(52↔51 边界 = 三流本身,ADR-019 §七);
> `TR-diag-020` 加 ADR-018 §二 note(守卫覆盖 `AudioCueDto`,状态不变)。
> **ADR-017 不新增 TR**(它兑现 ADR-004「是否 DOTS」,是「不引入」的裁决;OQ-8 为其复评前置,
> 登记在 `technical-preferences.md` 的性能预算侧,不入 TR 表)。
> **汇总 67 → 74 ✅ / 14 ⚠️ / 67 → 66 ❌(合计 148 → 154)。**
> 随后 **ADR-020**(2026-09-15,**R-13 玩家控制器与相机**):**ID 增补 9 条**(154 → **163**)——
> `TR-player-001…004`(移动模型 = `CharacterController` · **位移 = 表现态 / 跨格事件是唯一 sim 投影** ·
> 输入面承 ADR-011 · 手感旋钮留白)+ `TR-camera-001…005`(平面越肩 / VR 第一人称双路径 ·
> **自建机位不引 Cinemachine** · 相机只读不持状态 · 镜头效果归属与 VR 禁用 · 44 的 `AudioListener`
> 单挂点落定)。**系统 1 / 2 无独立 GDD** —— 需求文本由 ADR-020 裁决 + `systems-index.md` 依赖图条目
> 生成,GDD 撰写时须回溯核对(与 13 / 27 同例,见 `TR-registry` 的 `revision_note`)。
> **汇总 74 → 83 ✅ / 14 ⚠️ / 66 ❌(合计 154 → 163)。R-1…R-15 至此全部结清,无残留。**
>
> **口径校正(2026-09-15)**:本表此前 `item-database`(8/2/22)与 `game-concept`(2/1/5)
> 两行与各自明细表、与 `tr-registry.yaml` 不一致(明细行早已是 10/1/21 与 4/1/3)。
> 本次以 **registry 为准**回填。系既有笔误,非 TR 变更。

---

## 1. 病例系统 `design/gdd/case-system.md`(#37)| 27 条

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-case-001 | `case_id` 为 `(Tick, Patient, Seq)` 三元组,可从事件流重构 | ADR-008 | ✅ |
| TR-case-002 | 病例事件落独立于病史流的第二条逻辑流 | ADR-008 | ✅ |
| TR-case-003 | 病例流不参与终态物理折叠 | ADR-008 | ✅ |
| TR-case-004 | 跨流全序键 `(Tick, StreamPriority, Patient, Seq)` | ADR-008 | ✅ |
| TR-case-005 | `CaseOpened` 载荷 `{patient_id, opened_tick, disease_snapshot}` | ADR-008 | ✅ B-2 已修 |
| TR-case-006 | `CaseClosed.disease_set` 为集合(可空),支持共病一案进多组 | ADR-008 | ✅ |
| TR-case-007 | `treated` 布尔快照进 `CaseClosed`,重放不跨流查询 | ADR-008 | ✅ |
| TR-case-008 | `PatternRecognized` 载荷携带冻结的 `MemberSet` 与锚点 `anchor_case` | ADR-008 | ✅ B-2 已修 |
| TR-case-009 | 世界级事件用 `PatientId.None` 哨兵,不污染高水位 | ADR-008 | ✅ |
| TR-case-010 | `JudgmentRecorded` 事件化(落笔:病名 + 置信度) | ADR-008 | ✅ |
| TR-case-011 | `JudgmentRevised` 事件化,永不进计分 | ADR-008 | ✅ |
| TR-case-012 | 规则二:立案的两条路径定义 | — | ❌ |
| TR-case-013 | 规则三:病例串接(链)语义 | — | ❌ |
| TR-case-014 | 规则四:改写史永久可回看 | ADR-008 | ✅ |
| TR-case-015 | 规则五:处置记录的写入边界 | ADR-008 | ⚠️ 仅 `treated` 快照,处置内容形状未定 |
| TR-case-016 | 规则六:结案为唯一出口、玩家显式、单例、不可撤销 | — | ❌ |
| TR-case-017 | 规则七:未结案病例永久开着(系统永不自动结案) | ADR-008 | ✅ |
| TR-case-018 | 规则八:同源检测算法 | — | ❌ |
| TR-case-019 | 规则九:`disease_id` 不进呈现层 | ADR-008 + ADR-013 | ⚠️ 落点 AC-37-15 DTO 静态检查;机制 = `PresentationDtoGuard` 递归反射扫描(ADR-013 §三) |
| TR-case-020 | 规则十:37 不拥有什么的边界清单 | — | ❌ |
| TR-case-021 | F-37.1 fires-once:同病种 ≥3 例结案只触发一次 | ADR-008 | ✅ |
| TR-case-022 | F-37.1 性质 5:MemberSet 成员 `patient_id` 互异 | ADR-008 | ⚠️ 未机械化(AC-37-22 待实现) |
| TR-case-023 | F-37.2 可结案 = 病史流 ≥1 处置事件(窗口内)∧ 玩家勾选 | ADR-008 | ✅ |
| TR-case-024 | F-37.3 一案 overlap 多病种时进多组 | ADR-008 | ✅ |
| TR-case-025 | 盐键 `SplitMix64(WorldSeed, "case-salt")` | ADR-008 | ✅ |
| TR-case-026 | 重复结案幂等拒收,判定 = 流前缀纯函数 | ADR-008 | ⚠️ 未机械化 |
| TR-case-027 | 图样可辨识度须 playtest 验证 | — | ❌ |

## 2. 物品与配方 `design/gdd/item-database.md`(#21a)| 32 条

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-itemdb-001 | 物品数值在整数定点域(Q16.16) | ADR-005/006 | ✅ |
| TR-itemdb-002 | `weight` / `stack_max` 为 `int` 计数,移出 `Fix` 解析集(D-21-17) | ADR-006 | ✅ |
| TR-itemdb-003 | `Fix` 不可经 Unity 内置序列化器承载(D-21-18) | ADR-006 | ✅ |
| TR-itemdb-004 | 守恒律在整数域内求值,`weight` 归一(D-21-19) | ADR-006 | ✅ |
| TR-itemdb-005 | `FixParse` 为外部数据 → `Fix` 的唯一入口,拒浮点字面量 | ADR-006 | ✅ |
| TR-itemdb-006 | 单一舍入模式 `ROUND_HALF_AWAY_FROM_ZERO` | ADR-006 | ✅ |
| TR-itemdb-007 | 配方 schema(输入/产出/工时) | — | ❌ |
| TR-itemdb-008 | 药材 schema(性味/归经/功效键) | — | ❌ |
| TR-itemdb-009 | 炮制方法 schema | — | ❌ |
| TR-itemdb-010 | 药物 `potency` / `half_life` 字段定义 | ADR-006 | ⚠️ 与 9 侧字段名未对齐 |
| TR-itemdb-011 | Schema E:库存槽形状 | — | ❌ |
| TR-itemdb-012 | **`ActualConsumed` 仅派生自 `Craft` 事件** | — | ❌ **NO-ADR** |
| TR-itemdb-013 | 配方产出在定点域内确定性可重放 | ADR-005 | ✅ |
| TR-itemdb-014 | `Craft` 事件的载荷形状 | — | ❌ |
| TR-itemdb-015 | **可感知底线 vs 9 的噪声带对齐** | — | ❌ **NO-ADR** |
| TR-itemdb-016 | 品质分级与品质轴定义 | — | ❌ |
| TR-itemdb-017 | 21a 与 17 采集系统的数据边界 | — | ❌ |
| TR-itemdb-018 | **9 侧字段名对齐 `drug_potency` / `half_life`** | — | ❌ **NO-ADR** |
| TR-itemdb-019 | **`instance_id` 权威(`IIdAuthority` 缺 `ItemInstanceId Next()`)** | — | ❌ **NO-ADR** |
| TR-itemdb-020 | **D-21-27:仅主机铸币** | — | ❌ **NO-ADR** |
| TR-itemdb-021 | **D-21-28:`Craft` 事件的全序键** | — | ❌ **NO-ADR** |
| TR-itemdb-022 | AC-21a-29 跨平台确定性 | ADR-012 | ✅ 双级黄金夹具矩阵(报告 B-9 解决;F7 spike 前置) |
| TR-itemdb-023 | 物品数据在存档中的编码 | ADR-010 | ✅ 编码器 ADR-006 §五;存档布局 ADR-010 §三 义务 9 |
| TR-itemdb-024 | 配方解锁与技能门(与 30 的接口) | — | ❌ |
| TR-itemdb-025 | 数据文件格式与位置(`assets/data/*.json`) | ADR-014 | ✅ 作者态 JSON + 构建期烘焙出货;命名遵 `data-files.md` |
| TR-itemdb-026 | Addressables 分组与预载 | ADR-014 | ✅ 单一 `data-core` 组 + 首次 `Step` 前预载(报告 R-7) |
| TR-itemdb-027 | 构建期 schema 校验 | ADR-014 | ✅ 两阶段工具链,白名单/守恒/区间/长度全烘焙期硬失败 |
| TR-itemdb-028 | `axis_offset_by_quality[]` 可为负 ⇒ 负数舍入方向须定义 | — | ❌ 见报告 E-8 |
| TR-itemdb-029 | **`Σ(weight × InstanceWeight)` 的 int64 溢出上限** | — | ❌ **NO-ADR** |
| TR-itemdb-030 | 21a 与 20 库存的所有权边界 | — | ❌ |
| TR-itemdb-031 | 掉落实体的世界状态事件化边界 | — | ❌ 待 R-1 / B-7(位置侧坐标已由 ADR-015 §三 定型,状态待下一轮重裁) |
| TR-itemdb-032 | 配置版本号与存档头联动 | ADR-010 + ADR-014 | ✅ 存档头联动(ADR-010 §七);装载与 `ConfigVersion` 派生归 ADR-014 §五 |

## 3. 随机事件导演 `design/gdd/random-events.md`(#52)| 32 条

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-randomevents-001 | 掷骰的每个输入必须可从事件流重构 | ADR-007 | ✅ |
| TR-randomevents-002 | `WorldSeed` 由 7a 存档头持有 | ADR-007 | ✅ |
| TR-randomevents-003 | Hamilton 配额分配算法 | — | ❌ |
| TR-randomevents-004 | `EventRolled` 事件形状 | ADR-007 | ✅ |
| TR-randomevents-005 | `EventArrived` 载荷 `{event_key, tier, spawn_anchor, cause_clue_key?}` | ADR-007 | ✅ B-2 已修 |
| TR-randomevents-006 | **KeyGate 读数契约(与 8 的接口)** | — | ❌ **NO-ADR** |
| TR-randomevents-007 | `ThreatDeferred` / `ThreatDeferralCleared` | ADR-007 | ✅ |
| TR-randomevents-008 | CDF walk 的确定性实现 | — | ❌ |
| TR-randomevents-009 | `HistoryFlagChanged` 事件 | ADR-007 | ✅ |
| TR-randomevents-010 | **asmdef / Roslyn 构建期校验体系** | — | ❌ **NO-ADR** ADR-014 §三 仅覆盖数据 schema 侧硬失败,机制本体仍缺 |
| TR-randomevents-011 | 事件池与 tier 表定义 | — | ❌ |
| TR-randomevents-012 | 世界级事件用 `PatientId.None = -1` | ADR-007 | ✅ |
| TR-randomevents-013 | authority 迁移后掷骰可重放 | ADR-007 | ✅ |
| TR-randomevents-014 | 冷却与去重窗口 | — | ❌ |
| TR-randomevents-015 | 导演的每 tick 性能预算 | — | ❌ |
| TR-randomevents-016 | 事件流体积纪律(有界性) | — | ❌ |
| TR-randomevents-017 | 与 5 时间天气的只读边界 | — | ❌ |
| TR-randomevents-018 | 与 6 生态区的生成点绑定 | ADR-009 + ADR-015 | ✅ 生态区多边形 / POI 落逻辑层整数数据,绑定退化为整数查表 |
| TR-randomevents-019 | 52→37 注入只触发立案 | ADR-008 | ⚠️ 仅方向,接口形状未定 |
| TR-randomevents-020 | **注入接口形状** | — | ❌ **NO-ADR** |
| TR-randomevents-021 | `spawn_anchor` 的确定性坐标解析(枚举 → 世界坐标) | ADR-015 | ⚠️ 坐标类型(`WorldPos` 整数格)+ 锚点来源侧已定;逐枚举解析规则仍待 |
| TR-randomevents-022 | **出诊路径 / 医馆地块的判据来源** | — | ❌ **NO-ADR** |
| TR-randomevents-023 | 事件的玩家可感知门槛 | — | ❌ |
| TR-randomevents-024 | 事件与 51 遥测的边界 | ADR-019 | ✅ 边界 = **三流本身**,52↔51 **零直接接口**(不新增契约) |
| TR-randomevents-025 | 事件文本的本地化载体 | — | ❌ |
| TR-randomevents-026 | 联机语义:多人共享同一事件 | — | ❌ |
| TR-randomevents-027 | 与 29 死亡复活的交互 | — | ❌ |
| TR-randomevents-028 | 与 13 病人 AI 的交互 | ADR-016 | ✅ 52→37→13 单向链;13 只读在场视图 |
| TR-randomevents-029 | **27 敌人 AI 不被事件导演锁定** | ADR-016 | ✅ 「不锁定玩家」= 27 侧实现约束(AC-52-16) |
| TR-randomevents-030 | **外部系统 → 52 的注入接口** | — | ❌ **NO-ADR** |
| TR-randomevents-031 | **构建期校验:17 条拒绝表** | — | ❌ **NO-ADR** |
| TR-randomevents-032 | 预告线索的音频侧(狗吠 / 马蹄 / 锣声 / 铃声 / 钟声) | ADR-018 | ✅ 52 发 cue → 44 播音;∈ 行为反馈白名单,不用 sting |

## 4. 疾病与伤情 `design/gdd/disease-simulation.md`(#9)| 22 条

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-disease-001 | `Fix` = `readonly struct` 内含 `long` 的 Q16.16 | ADR-005 | ✅ |
| TR-disease-002 | 中间乘法落 Q32.32(或更宽)| ADR-005 | ⚠️ 中间类型未钉死(报告 E-2) |
| TR-disease-003 | 手写定点 `Exp`(F1 的 `Base`/`Relapse`/`Decay`)| ADR-005 + ADR-012 | ✅ 库约定 + 单元级黄金哈希夹具(ADR-012 §一) |
| TR-disease-004 | `SplitMix64` 为唯一哈希 | ADR-005 | ✅ |
| TR-disease-005 | `patient_seed = hash(world_seed, patient_id)`,禁 `Random.Range` | ADR-005 | ✅ |
| TR-disease-006 | 单调逻辑 tick 驱动(`ITickProvider`)| ADR-005 | ✅ |
| TR-disease-007 | 病史事件流为唯一真源 | ADR-005 | ✅ |
| TR-disease-008 | P0 预留抽象点(五接口 + `SimEvent` 值类型) | ADR-005 + 007 | ✅ 复查轮重裁:现为六个 |
| TR-disease-009 | 主机唯一执行 `Step` / `CatchUp` | ADR-005 | ✅ |
| TR-disease-010 | 终态折叠(AC-36 `plateau`/`relapse` 豁免)| ADR-005 | ✅ |
| TR-disease-011 | `patient_id` 跨权威稳定(Amendment B)| ADR-006 | ✅ |
| TR-disease-012 | 终态折叠行必须保留 `patient_id` | ADR-006 | ✅ |
| TR-disease-013 | sim asmdef `noEngineReferences: true` | ADR-005 | ✅ 复查轮重裁:门 A + 门 B |
| TR-disease-014 | 零 `System.Math.Exp/Pow` 的类型引用断言 | ADR-005 | ✅ 复查轮重裁:门 B(IL 扫描) |
| TR-disease-015 | AC-1 跨平台位完全相同 | ADR-012 | ✅ 双级黄金夹具矩阵(报告 B-9 解决;F7 spike 前置) |
| TR-disease-016 | AC-5b 定点 `Exp` 位确定性 | ADR-012 | ✅ 单元级黄金哈希含定点 Exp 夹具 |
| TR-disease-017 | F1 的 `Base` / `Relapse` / `Decay` 求值公式 | ADR-005 | ⚠️ 公式在 GDD,ADR 只定域 |
| TR-disease-018 | F4 处置事件携带 `polarity` / `Offset` / `τ_half` | ADR-008 | ✅ |
| TR-disease-019 | 保守带误差预算(`ops × 2⁻¹⁶`,原引 `3×2⁻¹⁶`) | ADR-005 | ✅ 复查轮重裁:两侧口径已一致 |
| TR-disease-020 | 边界扫描的单向性验证 | — | ❌ |
| TR-disease-021 | 「病人出现率上限」配置项 | — | ❌ ADR-008 有界性硬依赖 |
| TR-disease-022 | 128 位中间类型(`System.Int128`)| — | ❌ **IL2CPP 无此类型**(E-2) |

## 5. 诊断与体征 `design/gdd/diagnosis-system.md`(#8)| 25 条

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-diag-001 | 8 位于唯一浮点出口之后(`IVitalsQuery → VitalsDto`)| ADR-005 | ✅ |
| TR-diag-002 | 铁律①:8 与 11 之间无数据流 | — | ❌ |
| TR-diag-003 | 铁律②:8 不向 9 写任何东西 | ADR-008 | ✅ |
| TR-diag-004 | 铁律③:8 不拥有持久化 | — | ❌ |
| TR-diag-005 | 铁律④:`EmitGrowth` 仅主机侧由 `IIdAuthority` 门控 | ADR-005/007 | ⚠️ 抽象点有,门控契约未钉 |
| TR-diag-006 | 铁律⑤:每名玩家一本脉案(联机)| — | ❌ |
| TR-diag-007 | F-8.6:8 不在 ADR-005 的定点域内 | ADR-005 | ✅ |
| TR-diag-008 | G-1:禁 libm 超越函数;指数仅取整数或 1/2 | — | ❌ |
| TR-diag-009 | G-3:途径整数等级的档位判定 | — | ❌ |
| TR-diag-010 | G-4:位宽固定 `System.Single`;禁 FMA 收缩依赖 | — | ❌ 缓解措施不可执行(E-3) |
| TR-diag-011 | D-8-4:`Project(Sign_j) → [0,1]` 归 9 | — | ❌ |
| TR-diag-012 | D-8-5(登记项)| — | ❌ |
| TR-diag-013 | D-8-6:共病体征合并(升级为 P0 阻塞)| — | ❌ |
| TR-diag-014 | D-8-9:`SLOT_BOUNDS ← DIAG_TIERS` 双向耦合 | — | ❌ |
| TR-diag-015 | D-8-10(登记项)| — | ❌ |
| TR-diag-016 | 读数存档的归属(39 或病例流)| ADR-008 | ⚠️ ADR-008 显式留白给 39 |
| TR-diag-017 | AC-8-47 结案时冻结判断链 | ADR-008 | ✅ |
| TR-diag-018 | AC-8-F5 表现层 float 跨平台一致 | ADR-012 | ⚠️ 矩阵提供执行载体;判据待 E-4 spike |
| TR-diag-019 | `disease_id` 不进呈现层 | ADR-008 + ADR-013 | ⚠️ 落点 AC-37-15;机制定(ADR-013 §三),待 39 实现 |
| TR-diag-020 | 脉案 DTO 的静态检查 | ADR-013 + ADR-018 | ⚠️ 机制定:递归反射扫描 `PresentationDtoGuard`;ADR-018 §二 明确该守卫**递归覆盖** `AudioCueDto`(音频 cue 不得携带 `disease_id`);待 39 DTO 定稿 |
| TR-diag-021 | **D-8-8 硬需求①:湿啰音 / 干啰音的音频可分辨**(细湿啰音 = 非连续性,不是水声) | ADR-018 | ✅ AC-44-08 医学准确性断言;`AudioCueDto` 只承载 `CueId`+`Tier`,音频资产侧定分辨度 |
| TR-diag-022 | **D-8-8 硬需求②:体征音与背景音的分层**(听诊音不被环境掩蔽) | ADR-018 | ✅ `Stethoscope` 混音总线独立 + 快照;§三 拓扑表 |
| TR-diag-023 | **D-8-8 硬需求③:听诊音的空间化语义**(声源 = 病人格) | ADR-018 | ✅ `AudioCueDto.Cell`(`WorldPos` 整数格,ADR-015)+ 就诊室空间化规则 |
| TR-diag-024 | **D-8-8 硬需求④:联机时全队听到同一体征** | ADR-018 | ✅ §五 单 `AudioListener` 约束 ⇒ 精度统一取主机技能;频谱一致 |
| TR-diag-025 | **无提示音铁律**(不得用 sting 播报「你确诊了」类状态) | ADR-018 | ✅ §六 白名单 / 黑名单 + AC-44-09 断言(BLOCKING) |

## 6. 技能与熟练度 `design/gdd/skill-system.md`(#30)| 8 条

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-skill-001 | `XP_to_next(n) = C × n^P`,`P = 1.4` | — | ❌ 违反 8 的 G-1(指数须整数或 1/2) |
| TR-skill-002 | 熟练度成长在整数定点域内求值 | — | ❌ 全文零 ADR 引用(报告 B-6) |
| TR-skill-003 | `CombatPower = (CombatSkillLevel × WeaponMultiplier) × (1 + 医术修正)` | — | ❌ |
| TR-skill-004 | `医术修正 = 0.20 × (关联医术等级 / 60)` | — | ❌ |
| TR-skill-005 | 运行时调参表的默认值须定点化 | — | ❌ 现为纯浮点 |
| TR-skill-006 | 19 项技能的依赖与解锁关系 | — | ❌ |
| TR-skill-007 | 30 与 25 格斗线的数据边界 | — | ❌ |
| TR-skill-008 | 技能成长的存档持久化 | — | ❌ |

## 7. 全案 `design/gdd/game-concept.md` | 8 条

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-concept-001 | 技术栈:Unity 6.3 LTS / URP / OpenXR | `technical-preferences.md` | ✅ |
| TR-concept-002 | 联机 1–4 人,P0 起架构预留 | ADR-001 | ✅ pipe 抽象 P0 生效;库 P1b swap 评审 |
| TR-concept-003 | MVP 的 8 条定义 | — | ❌ |
| TR-concept-004 | P0 排除项清单 | — | ❌ |
| TR-concept-005 | P0 = 31 系统 / 6–9 个月基线 | 用户裁定 2026-09-14 | ✅ |
| TR-concept-006 | 60 fps(平面)/ 90 fps(VR)帧预算 | — | ❌ |
| TR-concept-007 | 急救动作输入延迟 < 50 ms | ADR-011 | ✅ 直读通道 + 三端实测;联机判定归表现层 |
| TR-concept-008 | 拟物 UI 须同时支持键鼠与手柄焦点导航 | ADR-011 + ADR-013 | ✅ 接口归 ADR-011;呈现归 ADR-013(UI Toolkit 官方桥 + 焦点单栈门) |

## 8. 玩家移动与摄像机 `design/gdd/systems-index.md`(#1 / #2)| 9 条

> **无独立 GDD。** 需求文本由 ADR-020 的裁决与 `systems-index.md:34-35` 的依赖图条目生成;
> GDD 撰写时须回溯核对(与 13 / 27 同例)。`system:` slug = `player-movement` / `camera`。

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-player-001 | 移动模型 = `CharacterController`(不参与 PhysX 求解) | ADR-020 | ✅ §一;本作移动不与物理世界耦合(建造走整数格邻接,ADR-015 §五) |
| TR-player-002 | 玩家位移 = **纯表现态**;sim 中唯一投影 = **跨格世界流事件** | ADR-020 | ✅ §四 = ADR-016 §三 的**发出侧对称**;频率 = 格穿越率,与帧率无关;AC-20-03 BLOCKING |
| TR-player-003 | 移动 / 旋转轴由 ADR-011 的动作映射供给 | ADR-011 + ADR-020 | ✅ 输入面 = `MoveInput`;`<50 ms` 预算与移动手感同向 |
| TR-player-004 | 移动手感旋钮(移速 / 加速度 / 转向速率)的形状与归属 | ADR-020 | ✅ §一 AC-20-11:**数值全部留白**(用户手调) |
| TR-camera-001 | 平面模式视角 = **第三人称(越肩)** | ADR-020 | ✅ §三;承 `game-concept.md:32`(「全程第三人称」),限定平面模式 |
| TR-camera-002 | VR 模式视角 = **第一人称(头显)**;VR 不承担开放世界移动 | ADR-020 | ✅ §三;承 `technical-preferences.md:34`;两条路径独立,实现推 P1a |
| TR-camera-003 | 相机**只读,永不持有游戏状态** | ADR-020 | ✅ §五;承 ADR-013 §9 C3 / ADR-018 §一 同构体例;AC-20-05 |
| TR-camera-004 | 镜头效果归属 + VR 禁用清单 | ADR-020 | ✅ §六;归属 = 8 语义 + 2 实现;**VR 全禁**;不得用于报状态 |
| TR-camera-005 | 44 的 `AudioListener` 单挂点落定 | ADR-020 | ✅ §七;结清 `adr-018:295` 留白(平面主相机 / VR 头显);AC-20-10 |

---

## 优先修复清单(按层)

### Foundation 层缺口(最高优先)

| TR-ID | 需求 | 建议动作 |
|-------|------|---------|
| TR-itemdb-019/020/021/029 | `instance_id` 权威 · 仅主机铸币 · `Craft` 全序键 · 溢出上限 | 立 ADR(报告 R-2 / B-8) |
| ~~TR-itemdb-025/026/027~~ | ~~数据文件格式 · Addressables 分组预载 · 构建期 schema 校验~~ | ✅ **已落盘**(2026-09-15,ADR-014 数据管线 —— R-7 + R-8 合并;构建期烘焙 + 两阶段工具链) |
| ~~TR-randomevents-018/021~~ | ~~与 6 生态区的生成点绑定 · `spawn_anchor` 确定性坐标解析~~ | ✅ **已落盘**(2026-09-15,ADR-015 世界几何 —— R-9 + R-10 合并;生态区落逻辑层整数数据 ⇒ 绑定退化为整数查表;`021` 逐枚举规则仍待 52/24/17) |
| ~~TR-randomevents-028/029~~ | ~~与 13 病人 AI 的交互 · 27 不被事件导演锁定~~ | ✅ **已落盘**(2026-09-15,ADR-016 AI 架构 —— R-14;52→37→13 单向链;「不锁定玩家」= 27 侧实现约束) |
| ~~TR-concept-002~~ | ~~联机预留~~ | ✅ **已落盘**(2026-09-15,ADR-001 pipe 抽象 + §四 swap 评审) |
| ~~TR-disease-013/014~~ | asmdef 零引用 + `System.Math` 盲区 | ~~补 ADR-005 判据行~~ ✅ **已落盘**(2026-09-15 复查轮,ADR-005 门 A + 门 B) |
| ~~TR-disease-015/016 · TR-itemdb-022 · TR-diag-018~~ | 跨平台确定性 | ✅ **已落盘**(2026-09-15,ADR-012 双级黄金夹具矩阵;`TR-diag-018` 判据待 E-4 spike;报告 R-5 / B-9) |
| TR-skill-002 | 定点纪律 | 立 ADR 或并入 ADR-005 修正案(报告 B-6) |

### Core 层缺口

| TR-ID | 需求 | 建议动作 |
|-------|------|---------|
| TR-diag-002/004/006 | 8 的铁律①③⑤ | 由 8 / 39 的 ADR 承接 |
| TR-disease-020/021/022 | 扫描单向性 · 出现率上限 · 中间类型 | ADR-005 修正案 + 9 的 GDD 修订 |
| TR-diag-010 | G-4 的 FMA 缓解 | 改措辞为「定表化」(报告 E-3) |

### Feature / Presentation 层缺口(较低)

| TR-ID | 需求 | 建议动作 |
|-------|------|---------|
| ~~TR-concept-008~~ | ~~拟物 UI 手柄焦点导航~~ | ✅ **已落盘**(2026-09-15,ADR-013 UI Toolkit 主 + UGUI 补 world/XR;报告 R-6 / E-16) |
| TR-diag-019/020 · TR-case-019 | `disease_id` 不进呈现层 / 脉案 DTO 静态检查 | 机制已定(ADR-013 §三 `PresentationDtoGuard`);待 39 脉案 GDD 与实现 |

其余 ❌ 多为 GDD 内部 schema 与参数,按各系统进入实现时逐个收口。

---

## 变更历史

| 日期 | 动作 | 说明 |
|------|------|------|
| 2026-09-15 | 首次建立 | 148 条 TR,来自 7 份 GDD。`tr-registry.yaml` 此前为空 |
| 2026-09-15 | 复查轮(`consistency`) | TR-ID **无增删改**(148 条不变)。新发现跨 ADR 冲突 C-11…C-18 并全部就地修正(见报告 §13)。**随后重裁 4 条 TR**:`TR-disease-013/014`(❌ → ✅,门 A+门 B 判据落盘 ADR-005)、`TR-disease-019`(⚠️ → ✅,误差预算口径两侧一致)、`TR-disease-008`(✅ 口径重述为六抽象点,状态不变)—— 汇总升为 **52 ✅ / 18 ⚠️ / 78 ❌**。状态字段**不加括号**,重裁理由记入 `tr-registry.yaml` 的 `note`/`revised` |
| 2026-09-15 | ADR-012(跨平台确定性 CI 门) | 重裁 4 条 TR:`TR-disease-003/015/016`、`TR-itemdb-022`(⚠️ → ✅,双级黄金夹具矩阵 = 执行载体,报告 B-9 解决);`TR-diag-018` 加 `adr: ADR-012` 但**保持 ⚠️**(矩阵提供执行载体,判据待 E-4 spike)。ID 无增删改。汇总 **56 → 60 ✅ / 14 → 13 ⚠️ / 78 → 75 ❌**(后两行含口径校正) |
| 2026-09-15 | 汇总口径校正 | `item-database`(8/2/22 → 10/1/21)与 `game-concept`(2/1/5 → 4/1/3)两行与明细表 / registry 不符,以 registry 为准回填。**系既有笔误,非 TR 变更** |
| 2026-09-15 | ADR-013(拟物 UI 框架) | `TR-concept-008`(⚠️ → ✅,R-6 呈现侧落定:UI Toolkit 官方桥 + 焦点单栈门)· `TR-diag-020`(❌ → ⚠️,检查机制定 `PresentationDtoGuard`);`TR-diag-019`、`TR-case-019` 加 `adr: ADR-013` 保持 ⚠️(机制定,待 39 实现)。ID 无增删改。汇总 **60 → 61 ✅ / 13 ⚠️ / 75 → 74 ❌** |
| 2026-09-15 | ADR-014(数据管线与 JSON 解析器) | 合并报告 R-7 + R-8。重裁 3 条 TR:`TR-itemdb-025/026/027`(❌ → ✅,作者态 JSON + 构建期烘焙 + 两阶段工具链 + `data-core` 组);`TR-itemdb-032` 加 `adr: ADR-014` 保持 ✅;`TR-randomevents-010` 仅加 note(机制本体仍缺)。ID 无增删改。汇总 **61 → 64 ✅ / 13 ⚠️ / 74 → 71 ❌** |
| 2026-09-15 | ADR-015(世界几何:手工烘焙固定世界) | 合并报告 R-9 + R-10。重裁 2 条 TR:`TR-randomevents-018`(❌ → ✅,生态区多边形 / POI 落逻辑层整数数据 ⇒ 绑定退化为整数查表);`TR-randomevents-021`(❌ → ⚠️,坐标类型 `WorldPos` 整数格 + 锚点来源侧已覆盖,逐枚举解析规则仍待);`TR-itemdb-031` 加位置侧 note(状态不预判,归下一轮 ADR-009 全量复核)。**就地修订 ADR-009 §一/§二/§四/§五**(§四 纯函数地形 → 烘焙逻辑层加载;§五 `spawn_anchor` 定点坐标 → 整数格)。ID 无增删改。汇总 **64 → 65 ✅ / 13 → 14 ⚠️ / 71 → 69 ❌** |
| 2026-09-15 | ADR-016(AI 架构:分层确定性 · 行为烘焙 · 整数导航格) | 合并报告 R-14(13 病人 AI + 27 敌人 AI)。重裁 2 条 TR:`TR-randomevents-028`(❌ → ✅,52→37→13 单向链,13 只读在场视图)· `TR-randomevents-029`(❌ → ✅,「遭遇体不锁定玩家」= 27 侧实现约束)。**13 / 27 无 GDD,其 TR 须待 GDD 撰写时回溯追加**。ADR-006 Amendment B 的 id 空间语义由本 ADR §二 扩大(病人 → 受伤实体)。ID 无增删改。汇总 **65 → 67 ✅ / 14 ⚠️ / 69 → 67 ❌** |
| 2026-09-15 | ADR-017(是否 DOTS:sim 结构性排除 + 表现层复评门) | 兑现 ADR-004「是否 DOTS」。**ID 无增删改**:ADR-017 是「不引入」的裁决,不新增 TR;OQ-8 为其复评前置,登记在 `technical-preferences.md` 性能预算侧。**§二 首次成文**门 A(`"noEngineReferences": true`)↔ `Unity.Entities` 的结构性冲突(此前全文无记载)。**§四 撤回 ADR-016 §九 的「以便平移 DOTS」理据**(形状保留,理由改为缓存局部性 / 可序列化 / 可测性 / 无 GC 抖动)。**汇总不变:67 ✅ / 14 ⚠️ / 67 ❌** |
| 2026-09-15 | ADR-018(音频架构:44 与 42 同构 · 无提示音铁律) | 报告 V-8.7(登记处作 R-12)。**ID 增补 6 条**(148 → **154**):`TR-randomevents-032`(预告线索音频侧,✅)+ `TR-diag-021…025`(D-8-8 四条硬需求 + 无提示音铁律,✅,共 5 条)。`TR-diag-020` 加 note(守卫递归覆盖 `AudioCueDto`,状态不变)。**汇总 67 → 69 ✅ / 14 ⚠️ / 67 → 66 ❌** |
| 2026-09-15 | ADR-020(玩家控制器与相机:CharacterController · 自建机位) | 报告 R-13。**ID 增补 9 条**(154 → **163**),全部为新 `system:` slug —— `player-movement`(TR-player-001…004)+ `camera`(TR-camera-001…005)。**系统 1 / 2 无独立 GDD**,需求文本由 ADR-020 裁决 + `systems-index.md:34-35` 依赖图条目生成。**零第三方第七处一致性**:Cinemachine 不引入(DOTS 亦然 —— ADR-017)。**§四 = ADR-016 §三 的发出侧对称**(§三 定读方:13/27 读格;§四 定写方:玩家跨格发事件,连续位置永不写流)。**§七 结清** `adr-018:295` 留白的 `AudioListener` 单挂点。**汇总 74 → 83 ✅ / 14 ⚠️ / 66 ❌**(合计 154 → 163)。**R-1…R-15 全部结清,报告残留清零** |

> **下一轮 full 复核待办(本轮登记,已就地执行后状态如下)**:
> ① GDD 侧的硬编码行号引用腐化 —— ADR-006 / ADR-008 / ADR-005 中对
>    `disease-simulation.md`、`diagnosis-system.md`、`random-events.md` 的行号引用
>    **本轮已全部锚点化**(§Dependencies / §Core Rules 等章节名),GDD 侧零剩余;
> ② ~~`TR-disease-008` 的「五抽象点」口径重述~~ ✅ 本轮已重裁(六抽象点);
> ③ ~~`TR-disease-013` / `TR-disease-014` 状态重裁~~ ✅ 本轮已重裁(门 A + 门 B);
> ④ ~~跨平台确定性四条无执行载体~~ ✅ ADR-012 落盘(双级黄金夹具矩阵);
> ⑤ ~~拟物 UI 手柄焦点导航(R-6)~~ ✅ ADR-013 落盘(UI Toolkit 主 + UGUI 补 world/XR);
> ⑥ F7 int64 回绕 spike(F7 BLOCKING)· E-4 表现层 float spike · UI Toolkit 焦点导航质量 spike
>    (报告 §6.6 假设 6)待实测后回填 ADR-012 / ADR-013 §Validation;
> ⑦ 其余 ❌ 缺口仍留给下一轮 full 复核(多为 GDD 内部 schema,无 ADR 承接)。
> ⑧ ~~`TR-itemdb-025/026/027`(数据文件 / Addressables / 构建期校验)~~ ✅ ADR-014 落盘
>    (2026-09-15,数据管线合并 R-7 + R-8);`TR-itemdb-032` 装载侧亦归 ADR-014 §五。
> ⑨ ~~`TR-randomevents-018/021`(生态区生成点绑定 / `spawn_anchor` 坐标解析)~~ ✅ ADR-015 落盘
>    (2026-09-15,世界几何合并 R-9 + R-10);R-9 / R-10 判据的「工具逐位实测」部分**随工具移出运行期作废**。
> ⑩ ~~R-11(是否 DOTS)~~ ✅ **ADR-017 落盘**(2026-09-15)。裁决 = **sim 结构性排除**
>    (门 A `"noEngineReferences": true` 与 `Unity.Entities` 天然冲突,**不依赖 OQ-8 标定**)
>    + **表现层留复评门**(触发 = OQ-8 标定 + 阈值待用户裁定;登记在 `technical-preferences.md`)。
>    兑现 ADR-004;OQ-8 同时是 ADR-005 性能表量纲与 ADR-017 复评门的共同前置。
> ⑪ ~~R-12(音频架构)~~ ✅ **ADR-018 落盘**;~~R-15(遥测与隐私)~~ ✅ **ADR-019 落盘**
>    (同日)。报告 V-8.7 的四条硬需求落为 `TR-diag-021…025` + AC-44-01…09。
> ⑫ ~~R-13(玩家控制器 / 相机)~~ ✅ **ADR-020 落盘**(2026-09-15)。裁决 = **`CharacterController`**
>    + **自建相机机位**(**不引入 Cinemachine** —— 与运行期零第三方第七处一致;Cinemachine 3.0
>    post-cutoff 风险由「不使用它」消解)+ 平面越肩 / VR 第一人称双路径。
> ⑬ **待跑 spike 批次(同一批实测,§Validation 回填)**:ADR-013(42)焦点导航质量 ·
>    E-4 表现层 float · **ADR-020 相机(越肩遮挡 / 舒适度)** · F7 int64 回绕(BLOCKING,ADR-012)。
> **残留:无。** 架构复核 R-1…R-15 **全部结清**(本轮 ADR-020 为末项);报告
> `architecture-review-2026-09-15.md` 的残留清单可标为 closed(报告 §10 已加结清注)。下一轮 full 复核另起会话
> (复核 ADR-016/017/018/019/020)。
