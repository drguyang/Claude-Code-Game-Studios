# 架构可追溯性索引(Traceability Index)

> **Last Updated**: 2026-09-18(**P0 收尾批六份 GDD 一次性落盘** —— #4 交互系统 · #5 时间与天气 ·
> #10 急救动作 · #11 处方用药 · #18 炮制 · #20 库存与物品。按代生约定**各追加新 slug 六条**
> (`interaction` / `timeweather` / `emergency` / `prescription` / `processing` / `inventory`)
> —— 283 → **385** 条 TR(**+102**);**✅ 至此 P0 的 31 项全部有 GDD**。
> 六份**都无单一权威 ADR**(同 25 的形态),需求文本由各 GDD 生成。
> **本轮三条实质裁决**:① **#10 结清 `OQ-3-5`**(`EmergencyReading` 增设独立模拟量通道
> `magnitude`)+ 跳过路径落定(跳过的 `JudgeResult = Applied ≠ Missed` —— 无障碍入场券);
> ② **#5 结清 #1 的 `OQ-1-6`** —— 天气对移动的影响 **P0 不启用**(用户裁定 2026-09-18);
> ③ **#20 载重呈现 = 拟物器具(药箱满溢感)**(用户裁定;机制侧只有一条布尔 `CanCarry`)。
> **#11 兑现跨文档义务 D-21-11**(品级 → 时间轴作用点的求值点 = F-11.2,11 是 21a F5 的唯一调用点)。
> **六条 🔴 硬前置**(各自首轮评审前须裁):`OQ-4-1` · `OQ-5-1`(= `OQ-8-11` / `OQ-17-1`)·
> `OQ-10-1` · `OQ-11-1` · `OQ-18-5` · `OQ-20-1`。✅ **2026-09-18 当日全部结清**(用户裁定,逐条落点见 §汇总 后的结清注与 §变更历史末行):
> 4 条 ❌ → ✅ · 2 条 ❌ → ⚠️(承载方 / 数值仍未落地)。**合计 245 ✅ / 40 ⚠️ / 100 ❌**(385 不变)。
> ⚠️ 本批状态由撰写方置入,**待六份各自的首轮 `/design-review`(须新会话)确认**。
> **2026-09-19 更新(动作词表结案轮 · D-8-12)**:#8 侧用户裁定路线甲(模态分流)落盘
> `diagnosis-system.md` **S-8.4 动作词表** + `AC-8-51`/`AC-8-52` ⇒ 翻转 2 条(`TR-interaction-015` ⚠️→✅ ·
> `TR-case-036` ❌→✅),**ID 恒 387**。**2026-09-20 补记(OQ-8 / OQ-25-8 结案批次)**:裁定方就地翻转 3 条(`TR-disease-021` ❌→✅ ·
> `TR-patient-006` ⚠️→✅ · `TR-combat-023` ❌→✅),**ID 恒 387**;⚠️ 三条翻转**待各自下轮 `/design-review` 确认**(裁定 ≠ 验收)。
> **当前实测合计 251 ✅ / 43 ⚠️ / 93 ❌**(以 registry `status:` 字段计数为准)。
> 前一轮:**#25 格斗与武器线 GDD 落盘 + 首轮 `/design-review`(MAJOR REVISION NEEDED · Scope L)修订全量落盘**
> —— 按代生约定追加新 slug `combat` 24 条 TR(259 → **283**);**状态翻转 2 条**:
> `TR-enemy-017` ⚠️ → ✅(根因「25 无 GDD」已除,R2/R3 兑现)· `TR-skill-007` ❌ → ⚠️(25 侧兑现,30 自身待修)。
> 25 **无单一权威 ADR** —— 它是多 ADR 汇合点(ADR-005/006/008/009/016/018/020/021)。
> ⚠️ 该轮状态由撰写方置入,**待 25 二轮 `/design-review` 确认**。
> 再前一轮:**#37 病例系统二轮 `/design-review`** —— 判 `NEEDS REVISION`
> (22 项阻断)⇒ 用户裁定 [A] ⇒ 22 项当日全部修订落盘。按其**已 Accepted 的权威件**
> ADR-008 / ADR-006 / ADR-014 追加 `TR-case-028…036` 9 条(250 → **259**);
> 并**原地回填 4 条既有条目**(`TR-case-005` / `008` / `023` / `025` —— 因二轮复核判定
> 其口径为错:载荷字段增删、窗口方向写反、盐的派生域自相矛盾)。
> 9 新增 = 6 ✅ / 3 ❌(`TR-case-036` 归 8/10 的动作词表义务 ⇒ `gap`;**2026-09-19 8 侧 S-8.4 落盘 ⇒ 转 ✅**)。
> 再前一轮:**#27 敌人 AI GDD 落盘** —— 按其权威件 ADR-016 的回溯约定追加新 slug `enemy-ai` 21 条 TR(229 → 250))
> **Mode**: `/architecture-review`(full)
> **Engine**: Unity 6.3 LTS
> **上游报告**: `docs/architecture/architecture-review-2026-09-15.md`
> **机器可读件**: `docs/architecture/tr-registry.yaml`
> **基线**: 385 条 TR(首建 148 条;ADR-017/018/019 轮增补 6 条;ADR-020 轮增补 9 条;
> **#13 GDD 回溯追加 24 条** —— 按 ADR-016 Ordering Note 的约定,与 21a / 52 同法;
> **ADR-021 代生追加 9 条** —— 6 无独立 GDD,同 1 / 2 例 ⇒ **✅ 2026-09-16 其 GDD 落盘,9 条转 covered**;
> **#3 GDD 回溯追加 18 条** —— ADR-011 落盘时未生成 TR,按其裁决回溯补齐,同 1 / 2 例;
> **#3 评审轮增补 3 条**(`TR-input-019…021`)—— 并**原地回填 9 条既有条目**,因初稿口径经
> `/design-review` 判定为错;连带 **ADR-011 Amendment A**(六项口径修正);
> **#1 评审轮增补 4 条**(`TR-player-005…007` · `TR-camera-006`)—— 六条根因的登记面兑现;
> 其中三条 ⚠️ 的**根因不在 1 的仓里** —— ✅ **2026-09-16 全部结清**:2 已落盘 ⇒ `O-8` 结清;
> ADR-015 §一之补已点名 `slopeLimit` / `stepOffset` 的几何取值 ⇒ `O-9` 结清;
> ADR-009 Amendment G 已就地订正 `tick` 措辞 ⇒ `O-10` 结清;
> **ADR-022 代生追加 8 条**(`TR-leveltool-001…008`)—— 54 关卡工具无独立 GDD(编辑期工具非玩法系统),
> 需求由 ADR-022 + 6 的 GDD 登记义务生成;
> **#27 GDD 回溯追加 21 条**(`TR-enemy-001…021`)—— 按 ADR-016 Ordering Note 的约定,与 13 同法;
> 27 的绝大多数边界**不是 GDD 新立的,是 ADR-016 强加的**;
> **#25 GDD 回溯追加 24 条**(`TR-combat-001…024`)—— 25 **无单一权威 ADR**(多 ADR 汇合点),
> 需求文本由 GDD 生成;同轮翻转 `TR-enemy-017` ⚠️→✅ · `TR-skill-007` ❌→⚠️;
> **P0 收尾批六份 GDD 各追加 15 / 15 / 21 / 17 / 18 / 16 条**(4 / 5 / 10 / 11 / 18 / 20,共 **102** 条)
> —— 六份**均无单一权威 ADR**(同 25 的形态);**6 条 🔴 硬前置已于 2026-09-18 全部裁定**(4 ✅ / 2 ⚠️,见 §变更历史末行))

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
| `case-system.md` | 37 病例系统 | **36** | **26** | 4 | **6** |
| `item-database.md` | 21a 物品与配方 | 32 | **13** | 1 | **18** |
| `random-events.md` | 52 随机事件导演 | **32** | **13** | 2 | 17 |
| `disease-simulation.md` | 9 疾病与伤情 | 22 | **18** | **2** | **2** |
| `diagnosis-system.md` | 8 诊断与体征 | **25** | **9** | 5 | 11 |
| `skill-system.md` | 30 技能与熟练度 | 8 | 0 | **1** | **7** |
| `game-concept.md` | 全案 | 8 | 5 | 0 | 3 |
| `design/gdd/player-controller-and-movement.md` | **1 玩家控制器与移动** | **7** | **6** | **1** | 0 |
| `design/gdd/camera-and-viewpoint.md` | **2 摄像机与视角** | **6** | **6** | 0 | 0 |
| `patient-ai.md` | 13 病人 AI 与行为 | 24 | **19** | **0** | **5** |
| `design/gdd/world-and-ecozones.md` | **6 世界与生态区** | **9** | **9** | 0 | 0 |
| `input-system.md` | 3 输入与设备 | **21** | **14** | **3** | **4** |
| `docs/architecture/adr-022-level-tool.md` | **54 关卡工具**(Tooling) | **8** | **8** | 0 | 0 |
| `design/gdd/enemy-ai.md` | **27 敌人 AI** | **21** | **19** | **2** | 0 |
| `design/gdd/combat-and-weapon-lines.md` | **25 格斗与武器线** | **24** | **19** | **4** | **1** |
| `design/gdd/interaction-system.md` | **4 交互系统** | **15** | **14** | **1** | **0** |
| `design/gdd/time-and-weather.md` | **5 时间与天气** | **15** | **11** | **2** | **2** |
| `design/gdd/emergency-procedures.md` | **10 急救动作** | **21** | **12** | **4** | **5** |
| `design/gdd/prescription-and-medication.md` | **11 处方用药** | **19** | **10** | **5** | **4** |
| `design/gdd/processing.md` | **18 炮制** | **18** | **10** | **5** | **3** |
| `design/gdd/inventory-and-items.md` | **20 库存与物品** | **16** | **10** | **1** | **5** |
| **合计** | | **387** | **251** | **43** | **93** |

> **2026-09-18 结清轮(六条 🔴 硬前置,不增删 ID)**:上表 4 交互 / 5 时间 / 10 急救 / 11 处方 /
> 18 炮制 / 20 库存 六行的状态**已就地更新**,反映当日用户对六条评审准入硬前置的裁定 ——
> **4 条 ❌ → ✅**(`TR-emergency-016` · `TR-prescription-016` · `TR-processing-016` · `TR-inventory-014`)
> 与 **2 条 ❌ → ⚠️**(`TR-interaction-015` / `TR-timeweather-015`,承载方或数值仍未落地)。
> **本轮"合计"与 `tr-registry.yaml` 文末实测一致:245 ✅ / 40 ⚠️ / 100 ❌**;
> 未能全数转 ✅ 的两条,其残留已逐条登记在该行 TR note 中。

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
> 随后 **#13 病人 AI 与行为 GDD 落盘**(2026-09-15,`design/gdd/patient-ai.md`):**ID 增补 24 条**
> (163 → **187**)—— 新 slug **`patient-ai`**,按 **ADR-016 Ordering Note** 的约定回溯追加
> (13 / 27 的 TR 在 ADR-016 时悬置,撰 GDD 时补登 —— **27 仍待其 GDD**)。其中一条记录了同日裁定:
> **13 的决策层住边界层(呈现侧),不进 sim 程序集**(解 ADR-016 §一 ↔ §六 接缝)。
> **汇总 83 → 101 ✅ / 14 → 15 ⚠️ / 66 → 71 ❌(合计 163 → 187)。**
> 随后 **ADR-021**(2026-09-15,**#6 世界与生态区 POI 状态所有权** —— 结清三方复核登记的洞 **H2**):
> **ID 增补 9 条**(187 → **196**)—— 新 slug **`world-eco`**(`TR-worldeco-001…009`)。
> 裁决 = **POI 定义 = 派生态(烘焙逻辑层,不进流)· POI 状态 = 模拟态(进世界流)**;
> **写者 = 6 世界与生态区**(不新立系统,与「不加 #54」取向一致);新增世界流 Kind
> `PoiStateChanged`;义务已追加进 **ADR-010 §三 第 11 行**(唯一出处);**ADR-009 §二 / §三 / §六
> 三处就地修订**(见其 **Amendment F**)。**6 无独立 GDD** —— 需求文本**代生**(同 1 / 2 例),
> 撰写时回溯核对。唯一 ❌ = `TR-worldeco-009`(`PoiState` 枚举值 + 状态机规则,**ADR-021 刻意不定**,
> 归 6 的 GDD)。
> **汇总 101 → 109 ✅ / 15 ⚠️ / 71 → 72 ❌(合计 187 → 196)。**
> 随后 **#3 输入与设备 GDD 落盘**(2026-09-15,`design/gdd/input-system.md`):**ID 增补 18 条**
> (196 → **214**)—— 新 slug **`input`**(`TR-input-001…018`)。**ADR-011 落盘时未生成 TR**
> (其 §Enables 只声明 `TR-concept-007` gap→covered),本 GDD 按其裁决**回溯补齐**
> (同 1 / 2 的 `TR-player-*` / `TR-camera-*` 例,见 `TR-registry` 的 `revision_note`)。
> **两条用户裁定**:① **P0 不交付改键 UI**(能力已备、界面未交付,推 P1a)② **3 出「绑定 → 图标键」
> 映射,呈现归 42 / 48**(反幻想守门责任随之转移 —— 3 **连「要不要显示」都不知道**)。
> **三条新登记缺口**:`TR-input-016`(`L_render` 实测 → 反推 `JUDGE_BUDGET`,前置 = 可跑 player)·
> `017`(`EmergencyAction` 枚举基数,归 10)· `018`(`OpenInventory` 的 P0 归属 —— **真实玩法断头的登记**)。
> **汇总 109 → 122 ✅ / 15 → 17 ⚠️ / 72 → 75 ❌(合计 196 → 214)。**
>
> 随后 **#3 输入与设备的首轮 `/design-review`**(2026-09-15 **full**,六名专家 + Opus 高阶综合):
> **NEEDS REVISION · Scope XL**(记录见 `design/gdd/reviews/input-system-review-log.md`)。
> **这是本索引首次因「GDD 评审判定既有 TR 口径为错」而回填** —— 共两件事:
> ① **ID 增补 3 条**(214 → **217**):`TR-input-019`(**`Emergency` 表达力缺口** ——
> **真缺口收窄为「仅幅度一维」**(2026-09-16 三轮:节奏分辨率不是缺口);`gap`,归 **10** 且为其**硬前置**)·
> `020`(**反幻想守门 42 + 48 联合 BLOCKING**;`partial` —— 3 侧已定,42 / 48 侧未落)·
> `021`(**跳过路径归 10**;`gap`)。
> ② **原地回填 9 条既有条目**(需求文本 / note,因初稿口径**经核验为错**):
> `001`(改**单引用同一性**,初稿「文件计数 = 1」会放过同文件实例化两次)·
> `004`(hash **须含 `bindingId`** + 改 **FNV-1a-64**;初稿对「重建 ⇒ overrides 静默丢失」**完全盲**)·
> `005`(`Clone` → **`Instantiate()`**;补「不继承 overrides / **不得启用 UI map**」)·
> `006`(F-3.3 **恒等式已删** / 理由改**抖动(方差)** / 回调**非**轮询 / **VR 归 P1b**)·
> `007`(**`FixParse` 范畴错误** —— 初稿「判定结果进流前必须 `FixParse`」**不可执行**)·
> `011`(`Mixed` 的「有效输入」**补定义** = 迟滞 + 漂移防误触)·
> `012`(补禁 `FindActionMap` + 字符串索引器)·
> `015`(VR **P1a → P1b**)·
> `016`(`JUDGE_BUDGET` **已删**,改为「均值达标 + 抖动有上界」两条实测义务)。
> **状态变更 1 条**:`TR-input-018` **❌ → ✅** —— `OpenInventory` 的「**真实玩法断头**」
> **经核验为夸大**(`systems-index.md:54 / :477` 系统 20 = **P0 Core**),缺口撤销。
> **根因诊断**:该 GDD 的多数严重缺陷**不是自创,是忠实继承 ADR-011 的未推演断言**
> ⇒ **ADR-011 同日落 Amendment A**(六项口径修正)。
> **汇总 122 → 123 ✅ / 17 → 18 ⚠️ / 75 → 76 ❌(合计 214 → 217)。**
>
> **口径校正(2026-09-15)**:本表此前 `item-database`(8/2/22)与 `game-concept`(2/1/5)
> 两行与各自明细表、与 `tr-registry.yaml` 不一致(明细行早已是 10/1/21 与 4/1/3)。
> 本次以 **registry 为准**回填。系既有笔误,非 TR 变更。
>
> **2026-09-16 更新:#6 世界与生态区 GDD 落盘**(`design/gdd/world-and-ecozones.md`)。
> 这是 `world-eco` 组(ADR-021 代生)的**回溯核对**:9 条 TR **全部转 `covered`**
> (`TR-worldeco-009` = `PoiState` 枚举 + 状态机规则,由本 GDD 结清:`Undiscovered(0) →
> Discovered(1) → Resolved(2)` 三态单调不可逆 + 4 条平表转移 + 三方非法处置)。
> **ID 不增不减**(仍 **221** 条);**9 条 `gdd:` 指针由「代生 `systems-index.md`」改指本 GDD**
> (从「无独立 GDD」行转为独立 GDD 行);**`TR-worldeco-007` 的有界性口径就地收紧**
> (`|POI| × |STATE|`(3 倍)→ **`2 × |POI_DEF|`**,同步至 `entities.yaml`
> `SimEvent.Kind.PoiStateChanged` 与 `formulas.poi_transition_bound`)。
> **本轮同时校正两处本表自身的陈旧口径**(与 registry 实测对齐,系既有笔误):
> ① **§汇总表把 1 与 2 合并在「1 玩家移动 + 2 摄像机」一行**(13 条 / 10 ✅ / 3 ⚠️),
> 而 2 的 GDD `camera-and-viewpoint.md` 早已落盘 ⇒ **拆为两行** ——
> 1 独立 7 条(6 ✅ / 1 ⚠️)、2 独立 6 条(6 ✅ / 0 ⚠️);② **3 输入与设备行为 15 ✅ / 3 ⚠️ / 3 ❌**,
> 实测「**14 ✅ / 3 ⚠️ / 4 ❌**」(第二行 §Enables 侧长期漏计 `TR-input-021` 的 ❌)。
> **校正后合计 127 ✅ / 19 ⚠️ / 75 ❌(合计 221)** —— 与 `tr-registry.yaml` 逐条实测一致。
> ⚠️ 上述状态变更由**撰写方**置入,**待首轮 `/design-review` 确认**。

---

## 1. 病例系统 `design/gdd/case-system.md`(#37)| 27 条

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-case-001 | `case_id` 为 `(Tick, Patient, Seq)` 三元组,可从事件流重构 | ADR-008 | ✅ |
| TR-case-002 | 病例事件落独立于病史流的第二条逻辑流 | ADR-008 | ✅ |
| TR-case-003 | 病例流不参与终态物理折叠 | ADR-008 | ✅ |
| TR-case-004 | 跨流全序键 `(Tick, StreamPriority, Patient, Seq)` | ADR-008 | ✅ |
| TR-case-005 | `CaseOpened` 载荷 `{patient_id, disease_snapshot}`(立案 tick = 本事件 `SimEvent.Tick`) | ADR-008 | ✅ **2026-09-17 回填**:`opened_tick` 字段**已删**(重复携带 = 同一事实两份真相) |
| TR-case-006 | `CaseClosed.disease_set` 为集合(可空),支持共病一案进多组 | ADR-008 | ✅ |
| TR-case-007 | `treated` 布尔快照进 `CaseClosed`,重放不跨流查询 | ADR-008 | ✅ |
| TR-case-008 | `PatternRecognized` 载荷 `{patient_id: PatientId.None, anchor_case, salted_key, member_set}` | ADR-008 | ✅ **2026-09-17 回填**:补 `salted_key: ulong`(原 struct 缺该字段 ⇒ AC-37-14 空断言 + 53 无法定位 `D`) |
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
| TR-case-023 | F-37.2 可结案 = 病史流 ≥1 处置事件(窗口 `[CaseOpened.Tick, CloseTick]` 内)∧ 玩家勾选 | ADR-008 | ✅ **2026-09-17 回填**:窗口方向原写反(恰好使「复诊凭首诊处置通过」成唯一路径 = 自称要堵的后门未堵) |
| TR-case-024 | F-37.3 一案 overlap 多病种时进多组 | ADR-008 | ✅ |
| TR-case-025 | 盐键**逐 `D`** 派生 `SplitMix64(WorldSeed, "case-salt", ordinal(D))` | ADR-008 | ✅ **2026-09-17 回填**:原写全局单值,与「盐的输入集含病种键」自相矛盾(53 无法区分 `D`) |
| TR-case-026 | 重复结案幂等拒收,判定 = 流前缀纯函数 | ADR-008 | ⚠️ 未机械化 |
| TR-case-027 | 图样可辨识度须 playtest 验证 | — | ❌ |
| TR-case-028 | 载荷内枚举 = 版本化整数 ordinal(`DiseaseIdSet` / `Judgment.lexicon_id`),映射表进 `ConfigVersion` 覆盖集 | ADR-006 + ADR-014 | ✅ **2026-09-17 新增**(修 `FixSet` 型错 —— `disease_id` 是 `DIS_*` 枚举,塞进 Q16.16 正是 D-21-17 的同类错) |
| TR-case-029 | ordinal 映射表 **append-only** + 构建期对 `_ordinal_baseline.json` 断言 | ADR-014 | ✅ **2026-09-17 新增**(本轮自查的 ADR 级冲突的处置:`ConfigVersion` 管可追溯,append-only 门管静默错读) |
| TR-case-030 | 判词表 `case_judgment_lexicon` 经 ADR-014 烘焙;构建期校验与 9 的病种注册表**非一一对应** | ADR-014 | ✅ **2026-09-17 新增**(用户裁定「③ 进 ADR-014 烘焙词表」;第六处泄漏面) |
| TR-case-031 | `PATTERN_THRESHOLD` 进 `ConfigVersion` 覆盖集(**可追溯,不拒载**) | ADR-014 | ✅ **2026-09-17 新增**(订正初稿「旋钮变更 ⇒ 老存档作废」的过度解读) |
| TR-case-032 | 三案链成员判定域:`ScriptedChain(D)` 显式指定 + **一次性**(「Boss 击败后不可重现」) | ADR-008 | ✅ **2026-09-17 新增**(兑现 52 委派;用户自由文本裁定) |
| TR-case-033 | 同一病人同时至多一个开案(`#{c : c.state = 开 ∧ c.patient_id = p} ≤ 1`) | ADR-008 | ✅ **2026-09-17 新增**(AC-37-26 守) |
| TR-case-034 | 判定记录 DTO **须含 `quill_tick`**(供 42 只读计算墨龄) | ADR-013 | ✅ **2026-09-17 新增**(42 的呈现承诺上游落点;旧稿 37 零登记) |
| TR-case-035 | 「37 沉默 ≠ 世界沉默」(支柱四**措辞**订正;53 受「延迟 + 不可归因」两硬约束) | — | ✅ **2026-09-17 新增**(用户裁定「② 接受时序泄漏改写措辞」,机制未变) |
| TR-case-036 | 就诊交互的语义定义归 **8 / 10 的动作词表**(37 只订阅「动作已开始」信号) | ADR-013 + `D-8-12` 结案 | ✅ **2026-09-19 转 covered**(2026-09-17 新增为 8/10 载体义务;两半根因已除 —— 10 的 GDD 2026-09-18 落盘,8 侧 S-8.4 四行路由表首行「就诊→37」2026-09-19 落盘;37 口径不变,仍不定义动作) |

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
| TR-disease-021 | 「病人出现率上限」配置项 | — | ✅ **2026-09-20 ❌→✅(`OQ-8` 结案)**:`PATIENT_APPEARANCE_CAP = 24` 已登记于 9 的 Tuning Knobs + `entities.yaml` + F0 性能表;ADR-008 §六 有界性硬依赖结清。⚠️ 结案的是**上限值源**,不是**内容数量**(P0 仍 8;同批**提级预裁 P1a 起 16-20 不放宽 P0**) |
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
| TR-diag-021 | **D-8-8 硬需求①:湿啰音 / 干啰音的音频可分辨**(细湿啰音 = 非连续性,不是水声) | ADR-018 | ✅ AC-44-08 医学准确性断言;**`AudioCueDto` 2026-09-18 起只承载 `CueId` / `Intensity` / 格 / `Source` / `Looped`(无 `Tier`)**;档次由 44 本地 `F-44.3 SelectVariant(CueId, Tier, Intensity)` 解析,音频资产侧定分辨度 |
| TR-diag-022 | **D-8-8 硬需求②:体征音与背景音的分层**(听诊音不被环境掩蔽) | ADR-018 | ✅ `Stethoscope` 混音总线独立 + 快照;§三 拓扑表(7 总线) |
| TR-diag-023 | **D-8-8 硬需求③:听诊音的空间化语义**(声源 = 病人格) | ADR-018 | ✅ `AudioCueDto.Cell`(`int3` 整数格,ADR-015);**2026-09-18**:远端声源锚点经 ADR-001 §一之二 `IPositionalChannel`(per-`ActorId` latest-value);就诊室空间化规则见 F-44.7 |
| TR-diag-024 | **D-8-8 硬需求④:联机时全队听到的体征音** | ADR-018 + ADR-001 | ✅ **2026-09-18 改判**(D-A):**各设备按本机技能档**(`SetTier(TierSource.Local)`)—— 原「统一取主机技能」的伪前提(`AudioListener` = 总线数约束)已撤销;AC-44-07 据此重写。远端声源锚点走 ADR-001 第二 QoS |
| TR-diag-025 | **无提示音铁律**(不得用 sting 播报「你确诊了」类状态) | ADR-018 | ✅ §六 白名单 / 黑名单 + AC-44-09 断言(BLOCKING);**2026-09-18 硬化**:事件表 schema 须承载 `whitelist_category` + `trigger_source`,黑名单 `VitalsCrossed` / `ThresholdCrossed` / `OutcomeResolved`(捕获时序型状态播报) |

## 6. 技能与熟练度 `design/gdd/skill-system.md`(#30)| 8 条

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-skill-001 | `XP_to_next(n) = C × n^P`,`P = 1.4` | — | ❌ 违反 8 的 G-1(指数须整数或 1/2) |
| TR-skill-002 | 熟练度成长在整数定点域内求值 | — | ❌ 全文零 ADR 引用(报告 B-6) |
| TR-skill-003 | `CombatPower = (CombatSkillLevel × WeaponMultiplier) × (1 + 医术修正)` | — | ❌ |
| TR-skill-004 | `医术修正 = 0.20 × (关联医术等级 / 60)` | — | ❌ |
| TR-skill-005 | 运行时调参表的默认值须定点化 | — | ❌ 现为纯浮点 |
| TR-skill-006 | 19 项技能的依赖与解锁关系 | — | ❌ |
| TR-skill-007 | 30 与 25 格斗线的数据边界 | — | ⚠️ **2026-09-17 ❌ → ⚠️**(25 落盘:25 侧边界已兑现 —— 效能公式定义权归 30、25 只消费输出档、动作表 schema 容纳 P1a 三线 = `TR-combat-010/011`);**仍 partial 的原因在 30 自身**(现 Needs Revision:零 ADR 引用 + `P=1.4` 违反 8 的 G-1)—— 边界另一侧无有效件 |
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
| TR-concept-007 | 急救动作输入延迟 < 50 ms | ADR-011 | ✅ 直读通道 + **硬件实测**(**P0 = K&M / Gamepad**;VR 归 P1b,不入 P0 判据 —— 2026-09-15 `/consistency-check` 订正,原写「三端实测」);联机判定归表现层 |
| TR-concept-008 | 拟物 UI 须同时支持键鼠与手柄焦点导航 | ADR-011 + ADR-013 | ✅ 接口归 ADR-011;呈现归 ADR-013(UI Toolkit 官方桥 + 焦点单栈门) |

## 8. 玩家移动与摄像机 `design/gdd/systems-index.md`(#1 / #2)| 13 条

> ✅ **1 与 2 均已有独立 GDD**(`player-controller-and-movement.md` / `camera-and-viewpoint.md`,均 2026-09-16)——
> 🔴 **本节的「回溯核对」义务已履行**:2 的 GDD 撰写时已回扫 `TR-camera-001…006` 并逐条落点;
> `TR-camera-006` 与 `TR-player-005` 的状态**由 `partial` 转为 `covered`**(`O-8` 兑现)。
> `system:` slug = `player-movement` / `camera`。
> **2026-09-16 系统 1 首轮 `/design-review` 后增补 `TR-player-005…007`**(移动基向量 / 主机唯一 Append /
> `CharacterController` 参数契约)—— 六条根因的登记面兑现。

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-player-001 | 移动模型 = `CharacterController`(不参与 PhysX 求解) | ADR-020 | ✅ §一;本作移动不与物理世界耦合(建造走整数格邻接,ADR-015 §五);判据三并列(`AC-1-01`),断言面含预制体组件清单 |
| TR-player-002 | 玩家位移 = **纯表现态**;sim 中唯一投影 = **跨格世界流事件** | ADR-020 | ✅ §四 = ADR-016 §三 的**发出侧对称**;事件率**上界 = tick 频率**(2026-09-16 订正,原「= 格穿越率」不可证伪);AC-20-03 / `AC-1-02` / `AC-1-03` BLOCKING |
| TR-player-003 | 移动 / 旋转轴由 ADR-011 的动作映射供给 | ADR-011 + ADR-020 | ✅ 输入面 = `MoveInput`;`<50 ms` 预算与移动手感同向;1 不消费 `Look`;`‖MoveInput‖` 是速度因子(`AC-1-09`) |
| TR-player-004 | 移动手感旋钮(移速 / 加速度 / 转向速率)的形状与归属 | ADR-020 | ✅ §一 AC-20-11:**数值全部留白**(用户手调);六组旋钮 + 跨系统调参纪律(`K_*_MAX` 派生自 6 / 24 的表) |
| TR-player-005 | 二维 `MoveInput` → 世界方向的基(相机相对;1 读 2 的只读 yaw basis) | ADR-020 | ⚠️ **首轮评审第一根因**(初稿从未定义投影基 ⇒ 可编译可测试但**不可玩**);ADR-020 **Amendment B** 补 `ICameraRig.YawBasis`;系统 1 立 F-1-8 / `AC-1-31` / `AC-1-35`(单向无环)。✅ **`partial → covered`(2026-09-16)** —— 2 的 GDD 已落盘交付 (F-2-2 + `AC-2-07…10`);`O-8` 结清。残留 `O-11` / `O-14`(2 → 1,不阻塞 covered) |
| TR-player-006 | **跨格事件的 `Append` 权 = 主机唯一**(客户端经第二 QoS 上行其格;不在可靠通道) | ADR-020 + ADR-001 | ✅ **首轮评审第三根因**(原文从未指定谁 Append,与 ADR-005「主机唯一执行 Step」冲突);**Amendment B** 四处裁决;系统 1 落点 = R5 / EC-16 / `AC-1-30`;`OQ-1-9` 结案,实现落点在 45(`O-4`) |
| TR-player-007 | `CharacterController` 参数契约(`slopeLimit` / `stepOffset` / `minMoveDistance` / `skinWidth`)的命名与归属 | ADR-020 | ⚠️ **首轮评审第五根因**(通篇只说 collide-and-slide,未命名任一参数);系统 1 立 F-1-9 / `AC-1-33`。**partial 根因 = ADR-015 §一 未点名 `slopeLimit`/`stepOffset` 几何取值**(登记义务 `O-9`) |
| TR-camera-001 | 平面模式视角 = **第三人称(越肩)** | ADR-020 | ✅ §三;承 `game-concept.md:32`(「全程第三人称」),限定平面模式 |
| TR-camera-002 | VR 模式视角 = **第一人称(头显)**;VR 不承担开放世界移动 | ADR-020 | ✅ §三;承 `technical-preferences.md:34`;两条路径独立,实现推 P1a |
| TR-camera-003 | 相机**只读,永不持有游戏状态** | ADR-020 | ✅ §五;承 ADR-013 §9 C3 / ADR-018 §一 同构体例;AC-20-05 |
| TR-camera-004 | 镜头效果归属 + VR 禁用清单 | ADR-020 | ✅ §六;归属 = 8 语义 + 2 实现;**VR 全禁**;不得用于报状态 |
| TR-camera-005 | 44 的 `AudioListener` 单挂点落定 | ADR-020 | ✅ §七;结清 `adr-018:295` 留白(平面主相机 / VR 头显);AC-20-10 |
| TR-camera-006 | **`ICameraRig.YawBasis`** —— 水平化正交基,只读、单向(供系统 1 的 F-1-8) | ADR-020 | ⚠️ **Amendment B 新立**(系统 1 的移动方向依赖此接口);2 **不引用** 1 ⇒ 两条边方向相反不成环。判据 = AC-20-14 / `AC-1-31` / `AC-1-35` ③④。✅ **`partial → covered`(2026-09-16)** —— `camera-and-viewpoint.md` 落盘:F-2-2(`f̂ := (sin yaw, 0, cos yaw)` / `r̂ := (cos yaw, 0, −sin yaw)`,**构造成立、不靠事后归一**)+ §B 组 `AC-2-07`(水平化)/ `AC-2-08`(正交归一)/ `AC-2-09`(`PITCH_MAX < 90°`)/ `AC-2-10`(帧内恒定);`O-8` 结清。⚠️ **残留 `O-11` / `O-14`** —— 不阻塞 covered |

## 9. 病人 AI 与行为 `design/gdd/patient-ai.md`(#13)| 24 条

> **2026-09-15 回溯追加**(ADR-016 Ordering Note 约定:13 无 GDD 时 TR 待撰 GDD 时回溯追加,与 21a / 52 同法)。
> 权威件 = **ADR-016 §一 / §三 / §四 / §五 / §六 / §九**。`system:` slug = `patient-ai`。
> **本 GDD 里的一条用户裁定已入表**:`TR-patient-007` —— 13 的决策层住**边界层(呈现侧)**。

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-patient-001 | 13 只读不写 —— 不产三流任何事件,不回写 sim 真值 | ADR-016 | ✅ §六;AC-13-A1 BLOCKING |
| TR-patient-002 | 取数唯一入口 = `IVitalsQuery.GetVitals() → VitalsDto`(唯一浮点出口) | ADR-005 + ADR-016 | ✅ §六;AC-13-A2 BLOCKING |
| TR-patient-003 | `VitalsDto` 只含原始量 `position` / `trend`,不含病种 / 九态 / 派生显示量 | ADR-016 | ✅ = `disease-simulation.md:1240` **AC-20 BLOCKING**;2026-09-15 裁以 AC-20 为准 |
| TR-patient-004 | 13 **不重建九态**;行为分化只用自有 `BEHAVIOR_BAND_*` | ADR-016 | ✅ §六;AC-13-A3 BLOCKING;重建 = 第二真源(Forbidden) |
| TR-patient-005 | `BEHAVIOR_BAND_*` 与 9 的九态阈值(`CRITICAL` / `COMA` / `DEATH_THRESHOLD`)的对齐 | — | ❌ **OQ-13-2**;归属 13 + 9 联合;前瞻 = 9 的阈值表定稿 |
| TR-patient-006 | 行为模拟范围 = **在场病人**;13 不自建在场定义 | — | ✅ **2026-09-20 ⚠️→✅(`OQ-8` 结案 = 在场才模拟 · CAP 24)**:13 的在场判据有值源(= 9 的 `IPresentPatients`,13 仍不自建);`AC-13-E1` 可执行性前置解除 |
| TR-patient-007 | 行为决策 = **派生态**;**住边界层(呈现侧),不进 sim 程序集** | ADR-009 + ADR-016 | ✅ **2026-09-15 用户裁定**(解 §一 ↔ §六 接缝;§一 表已补 13 例外注);AC-13-B3 |
| TR-patient-008 | 重建三源不变量:输入 ∈ {事件流, 烘焙数据, 纯函数};禁第四来源 | ADR-016 | ✅ §一 核心不变量;AC-13-B4 |
| TR-patient-009 | 感知 = 粗粒度整数格;禁 `Transform.position` / `Raycast` / NavMesh 采样;禁 `sqrt` | ADR-016 + ADR-020 | ✅ §三(读方)+ ADR-020 §四(写方),**两条腿缺一即不成立**;AC-13-E3 |
| TR-patient-010 | 空间量一律 `WorldPos` 整数格;烘焙行为数据禁 `float` 字段 | ADR-015 + ADR-014 | ✅ §三 + 烘焙校验;**13 运行期 float 合法**(住边界层) |
| TR-patient-011 | 表现映射归 13;8 拥有体征语义 · 9 拥有真值,三者不互窜 | ADR-016 | ✅ §六;AC-13-D2 BLOCKING(无提示音白名单) |
| TR-patient-012 | 出只读视图 `IPresentPatients`(无 `disease_id`);13 不引用 37 | ADR-016 | ✅ §六;**结清** `case-system.md:473` 的「契约暂定」;AC-13-C1/C2/C3 |
| TR-patient-013 | 出现率上限归 9(`TR-disease-021`);13 不自主生成 / 删除病人 | ADR-016 | ✅ 链路 = 52 → 37 → 13(§八);AC-13-C4;**硬依赖** `TR-disease-021` —— ✅ **2026-09-20 该依赖转 covered,链路无悬空前置** |
| TR-patient-014 | 病人 id 经 `IIdAuthority`,与敌人共用同一 id 空间 | ADR-006 + ADR-016 | ✅ §二 扩大 Amendment B 语义;13 不自己发 id |
| TR-patient-015 | 行为载体:编辑器期行为树 → 构建期烘焙整数数据;零第三方行为树库 | ADR-016 + ADR-014 | ✅ §四;纳入陈旧门 |
| TR-patient-016 | 寻路:sim 走整数导航格 A*;NavMesh 仅表现态位移 | ADR-016 + ADR-015 | ✅ §五 兑现 ADR-015 §五 |
| TR-patient-017 | 决策 LOD 节流 / 远区冻结;**节流不改变判定** | ADR-016 | ✅ §九;13 侧通常不彻底冻结(呼吸层不能停);AC-13-E2 BLOCKING |
| TR-patient-018 | 咳嗽 / 呻吟 cue(13 → 44):`AudioCueDto`,不携带「状态变化」语义 | ADR-018 | ✅ §一 / §四;AC-44-09 白名单;**2026-09-18**:`Tier` 已从 `AudioCueDto` 移除(DTO 不再承载档次,由 44 本地 `F-44.3` 解析);远端声源锚点走 ADR-001 §一之二 `IPositionalChannel` |
| TR-patient-019 | 昏迷与死亡靠呼吸层与姿态区分,**禁音效硬报** | ADR-018 | ✅ §四;AC-13-D3;同源 = ADR-020 §六 的视觉侧铁律 |
| TR-patient-020 | 13 的表现层 DTO 受 `PresentationDtoGuard` 递归扫描 | ADR-013 | ✅ §三;与 AC-37-15 / AC-44 族同一守卫 |
| TR-patient-021 | 「查体诱发痉挛」的**写路径**归属 | — | ❌ **OQ-13-1**;`diagnosis-system.md:1197` 与 13 只读冲突;13 侧结论 = 归 10 / 9 |
| TR-patient-022 | 玩家**搬运昏迷病人**的写路径归属 | — | ❌ **OQ-13-3**;13 只读、20 只管自己位移,**二者都不拥有**;P0 不实现 |
| TR-patient-023 | 13 **无玩家可见 UI**;调试视图仅 Dev Build 且不显示病种 | — | ❌ 设计结果(无血条铁律);AC-13-A4 可测;无 ADR 承接(呈现层约束) |
| TR-patient-024 | 13 的听障 / 视障可及性(字幕 / 视觉提示) | — | ❌ **OQ-13-4**;`design/ux/` 尚不存在,无障碍评审待 `/ux-design` |

---

## 10. 世界与生态区 `design/gdd/world-and-ecozones.md`(#6)| 9 条

> **2026-09-15 ADR-021 落盘时生成** —— 结清三方复核登记的洞 **H2(POI 状态无拥有者)**。
> 需求文本**代生**(同 §8 的 1 / 2 例):**6 无独立 GDD**,由 ADR-021 裁决 +
> `systems-index.md` §11 H2 条目生成。
> **✅ 2026-09-16 回溯核对已完成** —— 6 的 GDD 落盘(`world-and-ecozones.md`),
> 9 条逐条落点、**全部转 `covered`**;`gdd:` 指针由「代生 `systems-index.md`」改指本 GDD。
> `system:` slug = `world-eco`。
> 权威件 = **ADR-021**(✅ Accepted);上游 = ADR-009 / ADR-010 / ADR-015。

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-worldeco-001 | POI **定义** = 派生态(烘焙逻辑层,加载期重建,不进流) | ADR-021 + ADR-015 | ✅ §决定①;与静态地形同源 |
| TR-worldeco-002 | POI **状态** = 模拟态;**所有者 + 唯一写者 = 6**;主机唯一 Append | ADR-021 | ✅ §决定②;不新立系统(不加 #54) |
| TR-worldeco-003 | 新世界流 Kind `PoiStateChanged`,载荷 `{ poi_id, new_state }` 均整数 | ADR-021 + ADR-009 | ✅ §决定③;首条以系统 ADR 追加的世界流 Kind |
| TR-worldeco-004 | `Patient = PatientId.None`,不污染 `max(patient_id)` 高水位 | ADR-021 + ADR-007 | ✅ §决定③ + ADR-007 §四 |
| TR-worldeco-005 | 状态变更全部经世界流,**无第二存储**(grep 无旁路) | ADR-021 | ✅ §Validation 1;违反 = 第二真源 |
| TR-worldeco-006 | 重放 / 迁移后**从世界流重建**,不读快照作真源 | ADR-021 + ADR-009 | ✅ §Implementation Guidelines;ADR-010 §三 义务 11 |
| TR-worldeco-007 | 转移数**有界**:≤ `\|POI\| × \|STATE\|`(**2026-09-16 收紧为 `2 × \|POI_DEF\|`**) | ADR-021 + ADR-009 | ✅ §决定④;补 ADR-009 §六 有界性。🔴 **2026-09-16 由 6 的 GDD 收紧**:三态单调不可逆 ⇒ 每 POI 至多 2 次转移(跳级 `0→2` 只算 1 次仍被覆盖)⇒ **F-6-4 / `entities.yaml` 的 `formulas.poi_transition_bound`**。成立前提 = 幂等丢弃生效(6 的 `AC-6-12`) |
| TR-worldeco-008 | 52 的 `spawn_anchor` **读定义不读状态** | ADR-021 | ✅ §决定⑤;否则抽池 = 状态的函数。6 侧落地 = R-6-10 + `AC-6-18`(AST 断言解析调用栈不含状态读取) |
| TR-worldeco-009 | `PoiState` **枚举值** + **状态机规则**(转移合法性 / 可逆性) | — | ✅ **2026-09-16 由 6 的 GDD 结清**(`gap` → `covered`):**三态最小集** `Undiscovered(0) → Discovered(1) → Resolved(2)`,**单调不可逆、无「已锁」/「已荒废」**;状态机 = **4 条平表转移**(T1 `0→1` / T2 `1→2` / T3 `0→2` 跳级 / T4 降级 = **非法**),非法处置三分为**幂等丢弃 / 降级告警 / 未登记 id 报错** ⇒ `world-and-ecozones.md` 的 **R-6-7** + §States 全表 + `entities.yaml` 的 `constants.PoiState`。⚠️ 残留 `OQ-6-7`(单调性是否最终定为全局不变量);本状态由**撰写方**置 `covered`,**待首轮 `/design-review` 确认** |

---

## 11. 输入与设备 `design/gdd/input-system.md`(#3)| 21 条

> **2026-09-15 GDD 落盘时回溯追加** —— **ADR-011 落盘时未生成 TR**(其 §Enables 只声明
> `TR-concept-007` gap→covered),本 GDD 按其裁决**回溯补齐**(同 §8 的 1 / 2 例)。
> **3 在架构复核 §2 记为「零覆盖」**(GDD ❌ / 架构覆盖 ❌ 零),故本节的 21 条全部是**新登记**。
> 权威件 = **ADR-011**(✅ Accepted)**+ 同日 Amendment A**(六项口径修正);下游 = ADR-005 / 006 / 009 / 010 / 012 / 013 / 014 / 020。
> `system:` slug = `input`。**`TR-concept-007` / 008 保持原位不动**(它们是 game-concept 侧的需求)。
>
> **⚠️ 2026-09-15 首轮 `/design-review` 连带修订**(NEEDS REVISION · Scope XL):
> **增补 3 条**(`019` / `020` / `021`)+ **回填 9 条**(见下方 **⟳** 标)+ `018` **❌ → ✅**。
> 回填原因 = 初稿口径**经核验为错**(多数为**忠实继承 ADR-011 的未推演断言**,非自创)。

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-input-001 ⟳ | **全案恰一个动作资产**;UI 模块与 3 的输入服务引用**同一对象**(同一性断言) | ADR-011 | ✅ 规则一;把「禁双触发」前移一步。AC-3-A1 **BLOCKING** |
| TR-input-002 | 三套绑重同表;OpenXR 绑**通用 `XRController` 布局** | ADR-011 | ✅ §一 + F5;绑专属布局 = 换头显即失效 |
| TR-input-003 | **绑重 overrides = 输入层唯一落盘职责**,不进 ADR-010 存档 / 三流 | ADR-011 + ADR-010 | ✅ 规则四;AC-3-A5 |
| TR-input-004 ⟳ | 资产重建致 GUID 失效 → **schema hash 失配优雅清空**;**hash 须含 `bindingId`**(+ 三组集合,`FNV-1a-64`) | ADR-011 | ✅ F3 + F-3.5;**失配时宁失去改键,不错位改键**。AC-3-A3 / **A8** **BLOCKING** |
| TR-input-005 ⟳ | 同机多玩家**每玩家 `Instantiate()` 资产**(不继承 overrides;**不得启用 UI map**;P0 不涉及,形状现定) | ADR-011 | ✅ F3;实现推 P1b |
| TR-input-006 ⟳ | **急救直读通道**(**`onAfterUpdate` 回调**,非轮询)不穿 42 UI 栈;`L_input→pixel ≤ 50 ms`(**P0 = K&M / Gamepad**) | ADR-011 | ✅ §二;**`TR-concept-007` 在 3 侧的落点**;**理由 = 抖动(方差)**(初稿「算术原因」为假)。AC-3-B1a / **B1b** / B2 **BLOCKING** |
| TR-input-007 ⟳ | 直读 `float` = 手感层**不进流**;判定结果**全整数直接构造 `SimEvent`**(载荷闭包零 `float`) | ADR-011 + ADR-006 | ✅ F4 + 规则八;AC-3-A6 / **A7** / B3(**初稿「进流前 `FixParse`」= 范畴错误,不可执行**) |
| TR-input-008 | 联机急救判定**归表现层本地即时**;仅结果事件进流 | ADR-011 + ADR-009 | ✅ F1;走主机权威 ⇒ RTT ⇒ 破 50 ms。AC-3-B4 |
| TR-input-009 | 导航动作 → `FocusNavigationIntent` **单向**;**3 不实现焦点移动** | ADR-011 + ADR-013 | ✅ F2 + 规则十 / 十一;**`TR-concept-008` 的接口侧在 3 的落点**。AC-3-C1/C3/C4 |
| TR-input-010 | **无同键双触发**:不得同时绑官方桥与自实现路径 | ADR-011 | ✅ F2;AC-3-C2 **BLOCKING** |
| TR-input-011 ⟳ | `Mixed` 设备态**以最近有效来源为准**(**「有效」= 迟滞 + 漂移防误触**);移除 / 失焦 → **合成 release** | ADR-011 | ✅ **本 GDD 新裁**(ADR-011 未定);合成 release 防「卡键」。AC-3-D3 / **D4** |
| TR-input-012 ⟳ | **预缓存 `InputAction` 引用**;零每帧字符串查找(禁 `FindAction` / **`FindActionMap`** / **字符串索引器**) | ADR-011 | ✅ F6;AC-3-E1(**Roslyn 分析器**) |
| TR-input-013 | `QueryBinding` → `{ device, bindingPath, iconKey }`;**只给键名** | ADR-011 | ⚠️ **3 侧已定**;**42 / 48 侧的契约形状未定**(`OQ-3-2`)。用户裁定② |
| TR-input-014 | **P0 不交付改键 UI**(能力已备、界面未交付) | ADR-011 | ✅ **用户裁定①**;补界面不返工;后果逐条记账 |
| TR-input-015 ⟳ | 震动触觉:**3 拥有通道,不拥有语义** | ADR-011 + ADR-018 | ⚠️ 通道形状已定;`OpenXRInput` haptics **须 spike**(F5);**P0 只落接口**(`OQ-3-4`);VR 实现推 **P1b** |
| TR-input-016 ⟳ | `L_render` / `L_poll` **实测**;交付给 10 = 「**均值达标 + 抖动有上界**」两条义务 | ADR-011 | ❌ **缺口**:**`JUDGE_BUDGET` 已删**(与 50 ms 判据代数等价)⇒ 改为双实操义务。前置 = **最低目标硬件定稿** + 可跑 player |
| TR-input-017 | `EmergencyAction` **枚举基数**(P0 = 2 个急救动作) | — | ❌ 归 **10 的 GDD**(10 属架构真空 10 项之一);3 只保证「N 个并列动作各一条通道」。**升级为 10 的硬前置**(见 `019`) |
| TR-input-018 ⟳ | **`OpenInventory` 的 P0 归属** —— 由 **20 库存与物品(P0 · Core)** 提供开容器路径 | — | ✅ **2026-09-15 结案(原 ❌)** —— 「真实玩法断头」**经核验为夸大**(`systems-index.md:54/:477` 系统 20 = P0 Core);缺口撤销,`OQ-3-6` 关闭 |
| TR-input-019 ★ | **`Emergency` 表达力缺口 —— 仅幅度一维**:止血 / 按压的**力道(深度)**须有可采集通道 | ADR-011 | ❌ **评审新增**(`OQ-3-5`)**承重未结项**;`Button(0/1)` 表达不了 `game-concept.md:693`;**2026-09-16 三轮收窄**:节奏分辨率**不是**缺口(走 press / release 边沿),真缺口只有幅度;**归 10 且为其硬前置**;**本轮不加宽 `IEmergencyInput`** |
| TR-input-020 ★ | **反幻想守门(42 + 48 联合 BLOCKING,按阶段)**:目视**零按键提示浮层** + **手柄单机**可走查纸页(`AC-8-44`)。**F1a(P0)** = 平面 + 键鼠/手柄(去 VR)+ 脉案 / 出诊箱;**F1b(P1a/P1b)** = VR + 纸质地图 | ADR-011 + ADR-013 | ⚠️ **评审新增**(用户裁定④,强制执行);**3 侧已定**(只给键名);**42 / 48 侧未落** ⇒ partial。守门 = **`AC-3-F1a` / `F1b`**(2026-09-16 三轮按阶段拆分,拆而不降级) |
| TR-input-021 ★ | **跳过路径归 10**:须**焦点可导航、无时序要求、无模拟量** | ADR-011 | ❌ **评审新增**(用户裁定④-b);3 只提供通道形状,**跳过语义归 10**;与 `017` / `019` 同批裁定 |


---

## 12. 关卡工具 `docs/architecture/adr-022-level-tool.md`(#54)| 8 条

> **2026-09-16 ADR-022 落盘时生成** —— 用户裁定「**另开 ADR 立新系统**」,
> 结清 6 世界与生态区的 GDD 登记义务 **`O-6-9` / `R-6-16`**(首轮 `/design-review` 第 4 条根因 🔴)。
> **54 无独立 GDD** —— 它是**编辑期工具不是玩法系统**,系统定义 + 产出契约 +
> 六项一致性检查 **即 ADR-022**;需求文本由 ADR-022 + 6 的 GDD 登记义务
> (`O-6-9` / `O-6-11` / `O-6-12` / `O-6-14`)生成。
> `system:` slug = `level-tool`;归 **Tooling 层**(`systems-index.md` 新类别,行 54,不计入 P0 的 31)。
> 权威件 = **ADR-022**(✅ Accepted);上游 = ADR-015 / ADR-014 / ADR-016 / ADR-021 / ADR-009 / ADR-005。

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-leveltool-001 | **正式系统,归 Tooling 层**,**运行期零存在**(不进构建 · 门 A 不约束 · 不计入 P0 的 31) | ADR-022 | ✅ §一;用户裁定「另开 ADR 立新系统,不并入既有系统行」 |
| TR-leveltool-002 | **逻辑层整数数据的唯一作者**;**第三方工具产物只进视觉层**;**无以 `WorldSeed` 生成地形**(grep 守卫) | ADR-022 + ADR-015 | ✅ §二;承 `adr-015:222,335`(逻辑层永远整数可审 / seed 生成 = 回归) |
| TR-leveltool-003 | **导出契约** `assets/data/world_*.json` → ADR-014 烘焙 → `*.cooked`(玩家构建零解析器);含 **`terrain_id` 表** | ADR-022 + ADR-014 | ✅ §三;**结清 6 的 `O-6-14`**(terrain_id 表产出方) |
| TR-leveltool-004 | **C2 可走性同源 = 硬失败**:`slopeLimit` / `stepOffset` 两侧一致,不一致即构建失败 | ADR-022 + ADR-015 | ✅ §四 C2;**`ADR-015 §Validation:318` 的执行点**;与 `AC-1-33` ② 同名同判据 |
| TR-leveltool-005 | **C4 多边形合法性 = 硬失败**:重叠 / 顶点<3 / 自交 / 零面积 / 重合顶点 | ADR-022 | ✅ §四 C4;与 6 的 `EC-6-7` / `AC-6-19` 同源(5 项退化) |
| TR-leveltool-006 | **C5 边界单一源 = 硬失败**:视觉遮罩**由整数多边形生成**(非手画第二份) | ADR-022 | ✅ §四 C5;**结清 6 的 `O-6-11`**(静默失败类:画面与判定各自成立但不一致) |
| TR-leveltool-007 | **C6 切片完整性 = 硬失败**:导航格按 chunk 切片,每格恰好覆盖一次 | ADR-022 + ADR-015 | ✅ §五;**`O-6-12` 的「切片者」归本工具**;⚠️ ADR-015 §四 就地修订仍须做(退化为一句补注) |
| TR-leveltool-008 | **C1 两层漂移 / C3 导航格↔NavMesh = 告警**(不升硬失败 —— 漂移只影响表现) | ADR-022 + ADR-015 + ADR-016 | ✅ §四;**C3 是 `ADR-016 §Validation:442` 的执行点**;刻意不升硬失败(让美术可迭代) |

---

## 13. 敌人 AI `design/gdd/enemy-ai.md`(#27)| 21 条

> **2026-09-17 #27 GDD 落盘时生成** —— 按其权威件 **ADR-016** 的回溯约定追加
> (同 21a / 52 / 13 的法)。`system:` slug = `enemy-ai`;归 **Feature 层**(`systems-index.md` 行 27)。
> **27 的绝大多数边界不是本 GDD 新立的,是 ADR-016 强加的** —— 本组的职责是把
> ADR-016 §一/§二/§三/§四/§五/§七/§八/§九 的裁决落到可实现的口径上。
> 权威件 = **ADR-016**(✅ Accepted);上游 = ADR-005 / ADR-006 / ADR-009 / ADR-014 / ADR-015 / ADR-020 / ADR-021 / ADR-022 / ADR-012 / ADR-019 / ADR-010。
> **~~🔴 唯一 P0 硬前置 = `O-27-3`(25 格斗与武器线无 GDD)~~ → ✅ 2026-09-17 已除**:25 GDD 落盘,
> `Kind` 三元组定名 ⇒ `TR-enemy-017` 转 ✅(见 §14)。

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-enemy-001 | 决策输入**恰好三源**(世界流 / 烘焙数据 / 二者纯函数);禁第四来源 | ADR-016 | ✅ §一 重建三源不变量;AC-27-02 正面白名单 |
| TR-enemy-002 | 感知 = **玩家粗粒度整数格**,禁读 `Transform.position` | ADR-016 + ADR-020 | ✅ §三(读方)+ ADR-020 §四(写方)对称;**「半格盲区」是设计** |
| TR-enemy-003 | 格距 = **整数平方和**(禁 `sqrt`);视线 = Brick 式格步进(禁 Raycast / NavMesh) | ADR-016 + ADR-006 | ✅ §三;`d2` 须 int64 + 断言 `R_VIS < 46341`(承 24 的 B6 教训) |
| TR-enemy-004 | P0 敌意状态机 = **六态最小集**;`Down` **不是**第七态 | ADR-016 | ⚠️ ADR-016 §九 只定**分层与节流**,未点名状态集 ⇒ 无 ADR 背书但亦不冲突(PR-27-5) |
| TR-enemy-005 | P0 行为面 = **单套程序 × 两类参数**;零 `isBeast` 分支 | ADR-016 | ⚠️ §九「只留**一个**原型」字面与 P0 池含野兽张力;**PR-27-1** 读作**表达能力上限**;须回填 `O-27-4` |
| TR-enemy-006 | 行为载体 = **作者态 → 构建期烘焙**;运行期零第三方行为树 / 寻路库;陈旧门 `throw` | ADR-016 + ADR-014 | ✅ §四 + ADR-014 两阶段烘焙;AC-27-22 / 23 双 BLOCKING |
| TR-enemy-007 | 行为程序**禁 `float`**(空间量整数格 / 非空间量 `Fix` 或 `int`) | ADR-016 + ADR-006 | ✅ §四;AC-27-03(门 B 扩展)+ AC-27-09(烘焙行无 float) |
| TR-enemy-008 | **决策 = 派生态,不进流**(效果进流,决策不进流) | ADR-016 + ADR-009 | ✅ §一 + ADR-009 §一 Q1 判据 |
| TR-enemy-009 | 决策 LOD 节流**不改变判定**;解冻后逐位一致,**与冻结时长无关**(不补算) | ADR-016 | ✅ §九;AC-27-04 / 05 双 BLOCKING;F-27-3 |
| TR-enemy-010 | **「可脱离」是对等态**,非特例;禁硬锁定仇恨 | ADR-016 | ✅ AC-52-16 的 27 侧落点;AC-27-06 图可达性断言 + AC-27-07;F-27-5;**承重约束非旋钮** |
| TR-enemy-011 | 寻路输入 = 23 合成的 **`EffectiveWalkable`**,不读 `Nav` 原件 | ADR-016 + ADR-015 | ✅ §五 补注 + ADR-015 §五;23 是唯一合成点(`O-6-10` 结清);AC-27-12 |
| TR-enemy-012 | 重规划由 27 **轮询**触发;23 无 push 义务;不自建 blocked 副本 | ADR-016 | ✅ §五(第二真源纪律,同 13 规则三);AC-27-13 |
| TR-enemy-013 | sim 走**整数导航格 A***,表现层 NavMesh;漂移处置**单向** | ADR-016 + ADR-015 | ✅ §五 + ADR-015 §五;AC-27-14(围死 ⇒ `Disengage` 且逻辑格连续) |
| TR-enemy-014 | `LogiPose` 在 sim 整数推进;**禁**从 `Transform.position` 反推格 | ADR-016 + ADR-015 | ✅ §一 + `adr-015:157`;AC-27-15 性质测试;F-27-4 防跳格 |
| TR-enemy-015 | 敌人 = **可受伤实体,复用 9 的伤情模型**(归零 = `INJ_COMA`,不新开第二套) | ADR-016 | ✅ §二;`skill-system.md`「人形敌人被误杀不可能发生」获**实现侧**保证;⚠️ 野兽语义分叉(P0 不交付救治动词) |
| TR-enemy-016 | id 经 `IIdAuthority`,**与病人共用 id 空间与高水位**;重构扫三流并集 | ADR-006 + ADR-010 + ADR-016 | ✅ §二 扩大 Amendment B 的 id 空间语义;AC-27-17;**折叠谓词不适用于敌人行** |
| TR-enemy-017 | 敌人伤情事件**落世界流,不污染病史流**;逐 `Kind` 载荷形状归 25 | ADR-016 + ADR-009 | ✅ **2026-09-17 ⚠️ → ✅**(`O-27-3` 的 25 侧兑现:三元组定名 `EnemyInjuryOnset` / `InjuryStateChanged` / `InjuryOnset` 入 `entities.yaml` + ADR-009 §三)。**载荷与 provisional 假设的差**:档位在 9 侧折叠(`QueryHurtLevel`),25 载荷不带严重度枚举 ⇒ 27 改读 9 查询。**待 25 二轮独立确认** |
| TR-enemy-018 | `ITameable` **P0 定型不实现**;命令通道复用行为程序 | ADR-016 | ✅ §七(2026-09-15 定型);AC-27-25 ADVISORY;不新开命令通道的理由 = 第二真源 |
| TR-enemy-019 | 遭遇生命周期 = **两 Kind,写者故意拆开**:`EncounterStarted` = 52 · `EncounterEnded` = 27 | ADR-016 + ADR-021 | ✅ **PR-27-4**;判据 = ADR-021 §① 状态所有权;AC-27-18;已入 `entities.yaml`(`O-27-5`);`reason` 须枚举非布尔(ADR-019 §一) |
| TR-enemy-020 | 27 **不写第二条 `ActorCellEntered`**;敌人格 = **纯派生态,不进流** | ADR-016 + ADR-009 | ✅ **PR-27-3**;AC-27-19;客户端**靠确定性重算**(ADR-016 §一 的核心考验);登记 `O-27-6` |
| TR-enemy-021 | 27 **不做读数条 / 音效 / UI**,只交付触发与信号 | ADR-013 + ADR-018 + ADR-016 | ✅ 承 ADR-013 §9 C3 + ADR-018 §一;AC-27-21(倒地**必须仍可读作「人还在」**);登记 `O-27-1` / `O-27-2` |

---

## 14. 格斗与武器线 `design/gdd/combat-and-weapon-lines.md`(#25)| 24 条

> **2026-09-17 #25 GDD 落盘 + 首轮 `/design-review`(MAJOR REVISION NEEDED · Scope L)修订全量落盘时生成**
> —— 按代生约定追加(同 1 / 2 / 54 的法;27 / 13 是「有权威 ADR 回溯追加」,25 **没有单一权威 ADR**)。
> `system:` slug = `combat`;归 **Core 层**(`systems-index.md` 行 25)。
> **25 是多 ADR 的汇合点**:ADR-005(意图通道 / 主机唯一执行)· ADR-006(Fix 边界 / dose_seq)·
> ADR-008 / 009(Kind 路由 / 三流 / 有界性)· ADR-016 §二(敌伤复用 9 / id 共用)·
> ADR-018 §六(乐层白名单第 5 类)· ADR-020 §四(击退不注入)· ADR-021(append-Kind 先例)· ADR-014(动作表烘焙)。
> **四条首轮评审用户裁定(均 [甲])**:A3 压制 = 只读查询 `IsSuppressed` · D2 敌/兽 `CP:=0` 退化式 ·
> B2 逐 bar 组合上界断言 · 范围 = 全量落盘、**机制与数值冻结**。
> **两处跨系统改判已同日回填 9 侧**:S2 `HurtLevel` 折叠归 9(R17)· S3 击退不注入(结清 1 的 `OQ-1-7`)。
> ⚠️ **本轮所有状态由撰写方置入,待 25 的二轮 `/design-review`(新会话)确认。**

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-combat-001 | 非致命模型 —— 生命归零 = `INJ_COMA` 状态投影(执行者 = 9 的九态机),敌/兽永不为 `death` | ADR-016 | ✅ S4/S5 裁定:25 只写 onset;昏迷 ⇔ `position_agg` 越阈 ∧ 存活,不进 D 集。支柱二的机制地基 |
| TR-combat-002 | 命中判定全部在 sim 整数域(tick 求值 + 整数格距离 + 无表现态输入),可逐位重放 | ADR-005 + ADR-015 + ADR-016 | ✅ F-25-1 五合取项;`OQ-25-5` 只裁格比例值,不裁形状 |
| TR-combat-003 | `magnitude` 为 Fix 域整数(PS 单位),clamp 带 [MAG_FLOOR, MAG_CAP] 且 MAG_FLOOR > 0 构建期硬拒 | ADR-006 + ADR-005 | ✅ F-25-2;A1/A13a/b 断言面 = {逐动作, 逐 bar} × {CP=0, CP_MAX};数值归 `OQ-25-7` |
| TR-combat-004 | 敌/兽 `CP:=0` 退化式 —— 伤害 = MAG_FLOOR + base_step[act],不引入 enemy_cp 旋钮 | — | ⚠️ **D2 裁定 [甲]**;AC-25-2-09。无 ADR 层必要,**⚠️ 仅因未过 25 二轮** |
| TR-combat-005 | onset 三元组 Kind 登记:`InjuryOnset`(病史流)/ `EnemyInjuryOnset`(世界流)/ `InjuryStateChanged`(世界流,写者=9) | ADR-008 + ADR-009 + ADR-016 | ✅ **R2/R3 兑现**:`entities.yaml` + ADR-009 §二/§三 全登记;「一个 Kind 无法同时落两条流」⇒ 病人/敌人各一支 |
| TR-combat-006 | 去重键 = 五元组 (tick, actor, target, injury_id, dose_seq);dose_seq 由事件流纯函数派生,禁可变计数器 | ADR-006 | ✅ 同 tick 两条都收、重传判重(AC-25-5-01);Amendment B 高水位重构不受影响 |
| TR-combat-007 | 占用门 `Occupied(a)` 是事件流求值的纯函数;三边界:压制不占用 / Natural 占用 / 占用不发事件不排队 | ADR-005 | ✅ 规则〇(首轮评审 B 组);占用态不写流 ⇒ 重放时由 cooldown 推出 |
| TR-combat-008 | `IsSuppressed(actor_id)` 公开只读查询;判据分 sim/呈现侧;禁轮询;压制不占用、生效时刻 = 求值 tick | — | ⚠️ **A3 裁定 [甲]**(只读查询非事件);R14 已在 1 侧落 `AC-1-23` 负边界;待 25 二轮 |
| TR-combat-009 | 25 = `MotorSuppressed` 唯三调用者之一(4/10/25);压制只锁走位与 Jump,不锁攻击、不锁相机 | ADR-020 | ✅ R8/R14 兑现:1 的依赖表两行 + `AC-1-23` 负边界 + `O-1` 25 半边补登 |
| TR-combat-010 | `combat_actions.json` 唯一真源(maps_to_injury);21a 的 `inflicts_injury` 降级为集合约束(A20);schema 容纳 P1a 三线 | ADR-014 | ✅ H1 + R13 兑现:`item-database.md` 三处同步(A20 漂移门 + 双 fixture) |
| TR-combat-011 | 战斗效能公式定义权归 30,25 只消费其输出档 | — | ⚠️ `TR-skill-007` 对偶半边;**30 现 Needs Revision**,边界另一侧无有效件 |
| TR-combat-012 | 战伤在 9 的 F1 有加性阶跃入口 Σ magnitude × SCALE_d × Decay_injury(Δ);TRAUMA_CAP 单侧 clamp | ADR-005 + ADR-006 | ✅ **R11 兑现**:F1 式体 + 专注(PS 单位 / 单侧理据 / A25 联动 / CatchUp 闭式 O(1)) |
| TR-combat-013 | 致死判定 = 逐实体门 `LethalFor(entity, d)`;注册表 `lethal` 语义收窄为「现实中可致死」 | ADR-016 | ✅ **R12 兑现**:F4 两支 + 转移表 + `self_limit` 正交调和;25 只登记需求,形态由 9 落字 |
| TR-combat-014 | `HurtLevel` 档位折叠归 9 + 公开查询 `QueryHurtLevel`;25 载荷不携带档位 | — | ✅ **S2 改判 + R17 兑现**:band 阈值 = 注册表数据(归 `OQ-25-7`);消费者 = 27 + 表现层,禁轮询。**折叠式本身待二轮复核** |
| TR-combat-015 | `InjuryStateChanged` emit = `position_agg` 越阈被 Step 求出的 tick;离线 CatchUp 序列与在线 Step 逐条相同 | ADR-005 + ADR-009 | ✅ **R18 兑现**:禁第二套终态直算旁路(Step≡CatchUp 不变量在伤情投影上的具体化) |
| TR-combat-016 | 击退 = 纯表现,位置不变,零注入权;25 无传送源 | ADR-020 | ✅ **S3 裁定 + R8 兑现**:1 的 `OQ-1-7` 结案、`O-3` 的 25 行撤销、`AC-1-22` 半边记结 |
| TR-combat-017 | 战斗乐层 = ADR-018 白名单第 5 类;护栏 G1 禁帧对齐 / G2 去标注盲测 / G3 本地设备侧触发全文入 ADR;触发方登记 | ADR-018 | ✅ **V4+V7 兑现**:护栏全文(非只落「允许切」一句)+ `audio-system.md` 类别行 + `enemy-ai.md` 订阅行;无护栏则例外不成立 |
| TR-combat-018 | 冷却判据 `cd_eff(d)` = min 逐动作 + 逐 bar 组合上界断言(B2);F-25-8 乘法形式判据(零除法路径) | ADR-006 | ✅ A26 = 4-ulp 带(Mono/IL2CPP 对拍验收面,承 ADR-012) |
| TR-combat-019 | 伤口通道 = 9 的第六条感知通道;刃背/`INJ_BLUNT` 绝不出血 = 构建期数据断言;血渍面积禁成伤害条 | — | ✅ **V1/V3 兑现**:六通道表 + AC-21 钝击无出血子句;判据源头 = 25 §七 ③ 层(盲测最后防线) |
| TR-combat-020 | 三时钟时间总图(T1 本地即时起播 / T2 contact 帧 ↔ onset 单向映射 / T3 反应不由攻击 clip 驱动);禁按动画碰撞帧发伤害 | ADR-011 + ADR-016 | ⚠️ §6.6;分层裁决有 ADR-016 §一 背书,但 T2 映射的实现细节(动画事件 → 表现层缓冲)无独立权威件 —— 待实现期升 ADR/登记 |
| TR-combat-021 | 表现层可订阅 onset / `InjuryStateChanged` 做持续读法(不走 `VitalsDto` 差分);禁入决策输入 | ADR-016 | ✅ **D-2 裁定 + V2 兑现**:whiplash = 差分对瞬时事件必然抖;13 决策仍只读 `VitalsDto` 两原始量 |
| TR-combat-022 | 27 → 战斗 = 有入向意图出手、无出向数值通道;依赖是**规格依赖**(共读烘焙动作表)非运行时接口 | ADR-016 | ✅ **R4/R7/R15 兑现**:`enemy-ai.md` 依赖表两处 + `:1094` 措辞订正(原暗示不存在的调用已改) |
| TR-combat-023 | tick 频率标定(`OQ-25-8`)与 `OQ-8` 同批前置 —— 占用门/冷却 ticks/事件率上界的量纲全押其上 | ADR-005 | ✅ **2026-09-20 ❌→✅(`OQ-25-8` 结案 = 20 Hz ⇒ 1 tick = 50 ms)**:量纲源 = 9 的 Tuning Knobs + `entities.yaml`。⚠️ 解除的是**量纲前置**,不是**数值** —— `OQ-25-7` / `TR-combat-024` 不随本条结案 |
| TR-combat-024 | 玩家可被致死伤的形态(`OQ-25-1` 路甲/丙未裁)与 `Down` 覆盖边界含苏醒态(`OQ-25-3`) | — | ❌ 两项未闭合用户裁决定此 TR 去向;路甲/路丙的下游文本已各自预备,**不预支决定**;与数值旋钮轮(`OQ-25-7`)一并交用户 |

---

## 15. 交互系统 `design/gdd/interaction-system.md`(#4)| 15 条

> **2026-09-18 #4 GDD 落盘时生成** —— 按代生约定追加(同 25 / 27 / 13 的法;4 **没有单一权威 ADR**)。
> `system:` slug = `interaction`;归 **Core 层**(`systems-index.md` 行 4)。
> **4 的承重边集中在五份 ADR**:ADR-016 §三(禁读表现态位置 —— 读方)· ADR-020 §四(玩家位移 = 表现态 —— 写方)·
> ADR-021(POI 状态写者 = 6;4 是合法自报方但自报 ≠ 写)· ADR-013 §十 Amendment A(**模态开集 `IModalState`** —— 三审订正:此前误写「焦点单栈门」)· ADR-014(烘焙常量 + 装载期硬失败)。
> **4 的三条铁律**:① 只选目标不结算;② 候选集全是派生态(整数格);③ **不写三流**(零 `Append`,AC-4-02)。
> **核心交付**:兑现 `O-6-7` / 闭合 `EC-6-2` —— 「已发现」的触发 = **玩家主动交互**(非碰撞进入)。
> ⚠️ ~~本轮所有状态由撰写方置入,待 4 的首轮 `/design-review` 确认。~~ **已于 2026-09-19 结清**:4 已跑**首轮(full)→ 二轮(lean)→ 三审(lean)= NEEDS REVISION,当日就地修订**(全属「文件与自身不一致」族,零机制改动)。本组 15 条 TR 的**数量与状态不变**;三审**就地订正 2 条需求文本**:`-002`(「三源」→ **四源**,首轮订正未回刷本表)· `-010`(「焦点单栈门」→ **模态开集 `IModalState`**,规则七首轮订正未回刷本表 / registry)。4 转 Approved 的全案唯一硬前置 = `OQ-4-1` / `D-8-12`(8 的动作词表行)⇒ `TR-interaction-015` 仍 `partial`,不得记绿。
> **⭐ 2026-09-19 同日结案(D-8-12)**:8 侧用户裁定路线甲(模态分流)落盘 `diagnosis-system.md` **S-8.4 动作词表** + `AC-8-51`/`AC-8-52`(就诊→37 / 查体→8 模态内 / 施治→11 方笺 / 急救→10 直读),4 出境载荷逐位不变(B-1)⇒ **`TR-interaction-015` ⚠️ → ✅**(本组 **14 ✅ / 1 ⚠️**,残留 ⚠️ = `-014` 归 21a 数值轮)。4 的全部外部技术前置至此消除 → **同日用户裁定转 ✅ Approved(免四轮复核 = 显式风险接受)**。

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-interaction-001 | 4 = 纯目标选择器,输出 (目标, 目标种类),零 gameplay 结算(`CanCarry` / F1 / `JudgeResult` / `treatable_by` / 容量比较) | ADR-005 | ✅ 规则一 · AC-4-01;ADR-005「输入是意图源,不直接驱动模拟」在目标选择层的落实 |
| TR-interaction-002 | 候选集只由**四源**构造:世界流锚格(`DropSpawned.spawn_anchor`)· 6 的烘焙逻辑层(`WorldPos`)· 13 的 `IPresentPatients` · **23 的 `BakedInitial` 基线**;玩家侧 = 1 的整数格(**2026-09-19 三审订正需求文本**:原写「三源」为首轮订正前旧口径) | ADR-016 + ADR-020 | ✅ 规则二;ADR-016 §三 定读方、ADR-020 §四 定写方,4 是两侧汇合点;第四源由 `AC-4-22` 守 |
| TR-interaction-003 | 禁读表现态位置构造 / 过滤候选集;判据 = **反射断言载荷字段类型,不是 grep**(`Vector3` 作渲染朝向不可避免) | ADR-016 + ADR-020 | ✅ 规则二 · AC-4-03;承 `AC-20-03` 同款纪律。读表现态位置 ⇒ 联机时非主机与主机目标选择可不同 |
| TR-interaction-004 | 目标选择 = 确定性全序(格距 → `KindPriority` → 稳定 id 终局决胜);禁列表遍历序 / 哈希序作决胜键 | ADR-006 + ADR-015 | ✅ 规则三 · F-4.1 · AC-4-06;承 ADR-006 容器序纪律 + ADR-015 §三 单一整数格 |
| TR-interaction-005 | `d∞` = 切比雪夫距离(纯整数,零浮点),与 1 的格语义同源;非欧氏距离 | ADR-015 + ADR-006 | ✅ F-4.1;切比雪夫球在整数格上是方形邻域;纯整数满足门 A |
| TR-interaction-006 | 4 不写三流 —— 零 `IEventSink.Append`;POI「已发现」由 4 自报给 6,写入权 = 6(唯一写者) | ADR-021 | ✅ 规则四 · F-4.3 · AC-4-02;承 ADR-021 §二 + 6 的 R-6-9。自报 ≠ 写 |
| TR-interaction-007 | 自报的幂等由 6 保证;4 零「报过了」记账(每帧照报,重复由 6 的校验吃下) | ADR-021 | ✅ F-4.3 · AC-4-13;幂等校验随写入权同归 6 |
| TR-interaction-008 | 4 不发相机档位意图(`Treatment` / `Casebook` / `Explore`)—— 目标选择不是状态切换 | ADR-020 | ✅ 规则六 · AC-4-11;2 的档位驱动表只有三行(10 · 8/39),4 不在表内;承 ADR-020 §五 |
| TR-interaction-009 | 模态抑制方向 ①:10 的 `Armed` 期压制 `InteractIntent` 与 UI 焦点移动,而 **3 侧零状态** | ADR-011 + ADR-013 | ✅ 规则七 · AC-4-10(与 AC-10-14 联合);抑制是过滤不是拒收 |
| TR-interaction-010 | 模态抑制方向 ②:任一模态打开时 4 拒收世界交互;只读 42 的**模态开集** `IModalState.Modal ≠ ModalId.None`,不自建「是否有 UI 打开」的布尔(**2026-09-19 三审订正需求文本**:原写「焦点单栈门」是错的旗标) | ADR-013 | ✅ 规则七 · F-4.2 · AC-4-09;契约 = `adr-013:326 §十 Amendment A`(`IModalState` + `ModalId`,`None = 0` 成员);自建第二份布尔会与 42 的单栈门双主 |
| TR-interaction-011 | 4 为 1 的 `MotorSuppressed` 合法调用者之一(与 10 / 25 并列);三者互不知晓,抑制请求独立发出由 1 汇总 | ADR-020 | ✅ 规则八;承 25 的共享语义;若须互知,回 1 裁定 |
| TR-interaction-012 | 4 无状态 —— 实例零可变字段;清空重建后同一 (玩家格, 世界状态, 输入) 的目标逐位相同 | ADR-005 | ✅ 规则九 · AC-4-04;缓存「上一帧的目标」会使重放依赖未入流的运行时历史 ⇒ 非确定性 |
| TR-interaction-013 | 4 不接触玩法数值 —— 零 `VitalsDto` / `disease_id` / `tier_named` / `drug_profile` / `EnvMod` 引用;判据是「种类 + 位置」不是「内容」 | ADR-013 | ✅ 规则十 · AC-4-05;与 42 / 44 / 20 的「只渲染 / 只搬运」同构 |
| TR-interaction-014 | 目标种类消歧的四级顺序 + `KindPriority` 为全序烘焙表,缺项 = 构建期硬失败 | ADR-014 | ⚠️ 规则五 · AC-4-15;烘焙与闭集校验归 ADR-014(covered),消歧顺序与取值是 GDD 内部 schema ⇒ partial。`R_INTERACT=0` / 缺项须装载期硬失败 |
| TR-interaction-015 | 病人身上四种路由(37 立案 / 8 查体 / 11 施治 / 10 急救)的语义定义归 8 / 10 的动作词表,不归 4 | ADR-013(模态)+ ADR-011(急救直读)· `D-8-12` 结案 | ✅ **2026-09-19 承载方落盘**:8 侧 S-8.4 动作词表(路线甲 = 模态分流,用户裁定)+ `AC-8-51`/`AC-8-52` 完整承载四路由;4 仍只交 `(病人, InteractIntent)` 零语义字段(B-1) |

---

## 16. 时间与天气 `design/gdd/time-and-weather.md`(#5)| 15 条

> **2026-09-18 #5 GDD 落盘时生成** —— 按代生约定追加(5 **没有单一权威 ADR**,但权威句 = `R-6-13`)。
> `system:` slug = `timeweather`;归 **Core 层**(`systems-index.md` 行 5)。
> **5 的权威件** = `world-and-ecozones.md:271` 逐字:「**时间与季节由 5 拥有;6 只提供『生态区 → 气候属性』的查表输入**」(**R-6-13**)。
> **5 的承重边**:ADR-005(`ITickProvider` = 全案 tick 唯一来源 —— 5 只拥有 tick 的**语义**,不拥有 tick)·
> ADR-007(掷骰的每个输入须可从事件流重构 · `WorldSeed` 归 7a 存档头)· ADR-009 §二(天气 = 派生态)·
> ADR-015(生态区查表归 6)· ADR-014(装载期硬失败)。
> **本轮两条实质结清**:① **`OQ-1-6`**(天气对移动的影响)—— **P0 不启用**(用户裁定 2026-09-18);
> ② **`TICKS_PER_DAY` 的单一定义点 = 5**,52 引用(不重复 `const`)—— 兑现 52 的 DC-3。
> ⚠️ **本轮所有状态由撰写方置入,待 5 的首轮 `/design-review`(新会话)确认。**
> **2026-09-19 首轮 `/design-review` 已跑**(MAJOR REVISION NEEDED · scope L · 9 BLOCKING → 当日全量修订 +
> 四裁定 R-5-A/B/C/D 落盘,待二轮):本组 TR **不增不减(15 条)、状态计数不变**;就地订正 2 条需求文本
> (`003` tick→块索引 · `007` 恒假→漏判环绕段);`015` 的 partial 根因(`SEASON_MULT[]` 值)未动,仍归数值轮。
> **5 侧 AC 由 15 → 21 条**(AC ≠ TR,不影响本组)。幻影边撤销的账在 52 / 9 侧 GDD 行内,不生新 TR。

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-timeweather-001 | tick 唯一来源 = ADR-005 的 `ITickProvider`;5 只读 `CurrentTick`,零写 tick 路径(`Time.deltaTime` / 墙钟 / 自增计数器) | ADR-005 | ✅ 规则一 · AC-5-01;5 不拥有 tick,只拥有 tick 的语义 |
| TR-timeweather-002 | 生态区气候属性由 6 定义、5 消费;5 不得定义生态区 / 气候属性的字面(新增属性须回 6) | ADR-015 | ✅ 规则二 · AC-5-02;权威句 = R-6-13 + `O-6-5`。防越界条款:6 自推季节 / 天气会与 5 形成双主 |
| TR-timeweather-003 | 天气 = `(WorldSeed, 块(tick), EcozoneOf(cell))` 的纯函数 —— 派生态,不进流 / 不存档 / 重建期重建 | ADR-009 + ADR-007 | ✅ 规则三 · F-5.3 · AC-5-04;承 ADR-009 §二。**2026-09-19 首轮评审 R-5-A 订正**:掷骰键 = **块索引**(原逐 tick iid 与 Game Feel 相悖;零历史 / 不进流的立法意图不变) |
| TR-timeweather-004 | 掷骰统一走 `IEventAuthority`;零 `UnityEngine.Random` / 零帧数 / 零墙钟 | ADR-007 | ✅ 规则三 · AC-5-03;ADR-007 核心不变量 = 掷骰输入须可从事件流重构 |
| TR-timeweather-005 | `WorldSeed` 归 7a 持久化的存档头,不占一条 `SimEvent`(避免鸡生蛋) | ADR-007 + ADR-010 | ✅ 规则三;承 ADR-007 §二 用户裁定 + ADR-010 §三 义务汇总 |
| TR-timeweather-006 | `TICKS_PER_DAY` 单一定义点在 5,52 引用(禁 `const` 副本);两者不一致 = 构建期硬失败 | — | ❌ 规则四 · AC-5-05;承 52 的 DC-3。**两常量同值约定须机械守**;无 ADR 承接。漂移症状 = 事件预算在跨日时错位一格 |
| TR-timeweather-007 | 昼夜相位纯派生(`FMod(t, TICKS_PER_DAY)`),夜区间判定须**环绕安全**(`FMod` 式;朴素比较**漏判环绕段**——2026-09-19 订正,原「恒假」不准) | — | ❌ 规则五 · F-5.1 · AC-5-06 / AC-5-20;5 只交 `isNight`;`TODMult` 形状与 `NIGHT_THREAT_MULT` 归 52。C# `%` 截断陷阱由 AC-5-20 反例哨兵守 |
| TR-timeweather-008 | `EnvMod` 的环境分量**可为负**,5 必须原样传递;钳制归 21a F1,5 不得越界钳制 | ADR-006 | ⚠️ 规则六 · F-5.4 · AC-5-14;定点域边界 covered,「负值原样传递」是与 18 的 AC-18-06 同款分量语义 ⇒ partial。负值是结构的一部分 |
| TR-timeweather-009 | 5 不结算任何玩法 —— 只产乘子与读数(`EnvMod_env` / `isNight` / 季节 / 天气 / 强度),由 18 / 17 / 52 / 42 / 44 各自消费 | ADR-009 | ✅ 规则七;与 6 的「气候属性 6 定义 / 5 消费」同款纪律 |
| TR-timeweather-010 | 天气对移动的影响 **P0 不启用**(5 零速度修正);P1a 若启用须走地貌同一条 `K_TERRAIN` 通道,不得新开第二条速度修改源 | ADR-020 | ✅ 规则八 · AC-5-08;**结清 1 的 `OQ-1-6`**(用户裁定 2026-09-18)⇒ F-1-1a `max()` 推导无需改动。P1a 启用时须回 1 补表项(`OQ-5-2`) |
| TR-timeweather-011 | 玩家不在任何生态区(未驻留 chunk)⇒ `EnvMod_env` 取全局默认;不得取「最近区」;`EcozoneOf` 不得假设全图可达 | ADR-015 + ADR-014 | ✅ 规则二 · 边例 · AC-5-12;承 6 的全局默认 + `OQ-6-8` / ADR-022 §五 的 `O-6-12` 工具侧 |
| TR-timeweather-012 | F-5.1 / F-5.2 / F-5.4 零浮点、除法全为整除;全部输出 DTO 递归反射无 `float` | ADR-006 | ✅ AC-5-13 · AC-5-15;门 A 的常规落实 |
| TR-timeweather-013 | 同一 `(WorldSeed, tick, cell)` 跨平台重放 ⇒ `kind` / `intensity` / `EnvMod_env` 逐位相同 | ADR-012 | ✅ F-5.3 · AC-5-07;双级黄金夹具矩阵(三格常驻)提供执行载体 |
| TR-timeweather-014 | 参数装载期硬失败(`TICKS_PER_DAY=0` / `SEASONS_PER_YEAR=0` / `NIGHT_SPAN ≥ TICKS_PER_DAY` / `WorldSeed` 未装载),禁 null 兜底 | ADR-014 | ✅ AC-5-11 · 边例;承 ADR-014 §五 装载期硬失败纪律 |
| TR-timeweather-015 | 深水线的季节修正曲线(形状与数值)—— 5 产 `season_index`,修正曲线的调制归 17 | — | ⚠️ **`OQ-5-1` ✅ 形式已结清(2026-09-18)** —— 逐季乘子表 `Capacity × SEASON_MULT[season_index]`;5 出下标 / **17 持表**(F-17-3)。**残留归数值轮**:`SEASON_MULT[]` 的值 + 17 的 `Capacity` / `RegrowWindow`(`OQ-8-11` / `OQ-17-1`) |

---

## 17. 急救动作 `design/gdd/emergency-procedures.md`(#10)| 21 条

> **2026-09-18 #10 GDD 落盘时生成** —— 按代生约定追加(10 **没有单一权威 ADR**)。
> `system:` slug = `emergency`;归 **Core 层**(`systems-index.md` 行 10)。
> **10 的承重边**:ADR-011(输入架构 —— 急救独立直读通道,不穿 42 的 UI 事件栈)·
> ADR-006(定点域 / 单一舍入)· ADR-005(判定归 sim,生产者口径修正)· ADR-020(`MotorSuppressed` + 档位意图)·
> ADR-013(焦点单栈门)· ADR-012(黄金夹具)。
> **10 是全案 P0 唯一需要「手感」的系统**(`L_input < 50 ms` 硬预算,承 `TR-concept-007`)。
> **本轮两条实质结清**:① **`OQ-3-5`** —— `EmergencyReading` 增设**独立模拟量通道** `magnitude`
> (真缺口收窄为「仅幅度一维」);② **跳过路径落定**(用户裁定)—— `Skip ⇒ JudgeResult = Applied`(≠ `Missed`)。
> ⚠️ **本轮所有状态由撰写方置入,待 10 的首轮 `/design-review`(新会话)确认。**

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-emergency-001 | `EmergencyReading` 增独立模拟量通道 `magnitude`(全整数),结清 `OQ-3-5` 的「真缺口 = 仅幅度一维」 | ADR-011 | ✅ 规则一 · AC-10-01;节奏走边沿(既有),幅度为新增维 |
| TR-emergency-002 | 幅度通道的浮点只活在 3 的手感层;交出的读数是全整数,10 收到的永远是 `int` | ADR-011 + ADR-006 | ✅ 规则一 · F-10.1;承 3 的三轮评审修正 —— 这是门 A 的守门线 |
| TR-emergency-003 | 急救动作输入走 `IEmergencyInput` 回调直读通道,不穿 42 的 UI 事件栈(`L_input < 50 ms` 硬预算) | ADR-011 | ✅ 规则二;UI 事件栈的一次派发就吃掉可观的一部分预算 |
| TR-emergency-004 | 判定全归 10(sim 侧);3 不做判定、不构造 `SimEvent` —— asmdef 白名单 + IL 扫描断言 | ADR-011 + ADR-005 | ✅ 规则三 · AC-10-02;生产者口径的三轮评审修正 |
| TR-emergency-005 | `Judge` 是定点纯函数;10 的判定路径零浮点字面量 | ADR-006 | ✅ 规则四 · AC-10-03;承 ADR-006 定点域边界契约 |
| TR-emergency-006 | `JudgeResult` 是枚举(`Applied` / `AppliedWeak` / `Missed`),非连续分值;零分数 / 评价字段 | — | ❌ 规则四 · AC-10-15;刻意不给连续分值(反幻想;与 53 的 Outcome 同款纪律)。取值集与形状为 GDD 内部 schema |
| TR-emergency-007 | 幅度定点化舍入 = `ROUND_HALF_AWAY_FROM_ZERO`(禁 `Math.Round` 默认 ties-to-even) | ADR-006 | ✅ F-10.1 · AC-10-04;舍入在 3 侧完成,10 的 AC 对拍它 |
| TR-emergency-008 | 处置事件载荷必含 `polarity` / `drug_potency` / `half_life`(承 9 的入向契约),写病史流 | ADR-005 + ADR-009 | ✅ 规则五 · AC-10-06;9 不认识药,只认识「处置 id + 对症/对因 + 偏移 + 半衰期」 |
| TR-emergency-009 | 处置 id ∉ `treatable_by(d)` 时 10 照常发事件 —— 判定「有没有用」是 9 的离牌门,不是 10 的 | ADR-005 | ⚠️ 规则五 · AC-10-07;「照常发事件」= 事件流纪律(covered);离牌门住 9 的 GDD,无 ADR ⇒ partial |
| TR-emergency-010 | 跳过路径:`JudgeResult = Applied`(≠ `Missed`);焦点可导航、无时序要求、无模拟量;不计入熟练度成长 | — | ❌ 规则六 · AC-10-10;无障碍的入场券(用户裁定 4-b 归 10)。若跳过 = `Missed` 则运动障碍玩家永远得不到好结果 |
| TR-emergency-011 | 动作开始发 `Treatment`、结束(含跳过路径)必发 `Explore` 档位意图 —— 否则相机卡在近景 | ADR-020 | ✅ 规则八 · AC-10-11;承 2 的 `O-12` 接收方 + ADR-020 §六。**跳过路径也必发** |
| TR-emergency-012 | 动作期间置位 1 的 `MotorSuppressed`,结束(含跳过)清位;1 不过问原因 | ADR-020 | ✅ 规则九 · AC-10-13;4 / 10 / 25 三者互不知晓,由 1 汇总 |
| TR-emergency-013 | `Armed` 期 10 压制 `InteractIntent` 与 UI 焦点移动;抑制是过滤不是拒收,3 侧保持零状态 | ADR-011 + ADR-013 | ✅ 规则七 · AC-10-14(与 AC-4-10 联合);承 3 的「无外部状态意图源」不变量 |
| TR-emergency-014 | 同一 `(reading, skill_ctx, patient_ctx, WorldSeed)` 跨平台重放 ⇒ `JudgeResult` 逐位相同 | ADR-012 | ✅ AC-10-05;双级黄金夹具矩阵提供执行载体 |
| TR-emergency-015 | 目标硬件上端到端输入延迟 < 50 ms 且抖动可接受(手感须主创签核 + 实测) | ADR-011 | ⚠️ AC-10-08 [L];承 technical-preferences 附加预算 + ADR-011 两条实测义务;前置 = 最低目标硬件定稿 ⇒ 绑定 `/test-setup`。不可自动化测 |
| TR-emergency-016 | `ResultMul[Missed]` = 0 还是非 0 —— 决定「动手但失败」要不要有代价,即 10 与 9 / 53 的耦合强度 | — | ✅ **`OQ-10-1` ✅ 已结清(用户裁定 2026-09-18)** —— `ResultMul[Missed] = 0.25`(**非零**,「失败也留一笔」);`Applied = 1.0` / `AppliedWeak = 0.5`。落点 = 10 的 **F-10.4 取值表 + AC-10-16**。⚠️ 显式记账的代价:「手稳」不再是可失去之物(用户已知并接受) |
| TR-emergency-017 | 四个判定门的具体阈值(`MAG_MAX` / `JITTER_MAX` / `MAG_THRESHOLD` / `MIN_EDGES`)+ `JudgeResult` 取值集 | — | ❌ **OQ-10-2**;`JITTER_MAX` 是最承重的旋钮(杀死「手稳」的是抖动,非平均延迟)。形状已定,值归用户 |
| TR-emergency-018 | 无模拟量设备(纯键盘)的幅度回退形态 —— 离散档还是自动通过 | — | ❌ **OQ-10-3**;键鼠玩家的幅度通道无实现;归用户 + `ux-designer` |
| TR-emergency-019 | 动作被打断的语义 —— 打断 = `Missed`?还是动作中止不发事件?已入流的部分怎么办 | — | ❌ **OQ-10-5**;战斗中急救的行为未定义;归用户 + 10 / 9 |
| TR-emergency-020 | `EmergencyAction` 枚举的取值集与语义归属(10 定 vs 21a 定 —— 动作是否进物品表) | ADR-014 | ⚠️ **OQ-10-6**;承 3 的 `TR-input-017`。烘焙管线 covered,枚举归属未裁 ⇒ partial |
| TR-emergency-021 | `magnitude` 通道在 3 侧的命名与资产表达 —— 须回填 input-system 的 Amendment,不得静默 | ADR-011 | ⚠️ **OQ-10-7**;直读通道裁决 covered,但 3 的 GDD 与实现不同步 ⇒ partial。回归义务 = 3 的修订轮 |

---

## 18. 处方用药 `design/gdd/prescription-and-medication.md`(#11)| 17 条

> **2026-09-18 #11 GDD 落盘时生成** —— 按代生约定追加(11 **没有单一权威 ADR**)。
> `system:` slug = `prescription`;归 **Core 层**(`systems-index.md` 行 11)。
> **11 的承重边**:ADR-005 + ADR-009(处置载荷形状,与 10 同形)· ADR-006(定点域 / 单一舍入)·
> ADR-014(作者态数据表烘焙 + 构建期交叉校验硬门)· ADR-013(`disease_id` 不进呈现层)·
> ADR-020(相机档位;11 不请求)。
> **11 的三条铁律**:① **病名不给 11**(承 `diagnosis-system.md:807-809` —— 8 与 11 刻意无数据流);
> ② 11 不发明结算(药值全来自 21a `drug_profile`);③ 11 不判对错(离牌门归 9)。
> **本轮兑现一条跨文档义务 —— `D-21-11`**:品级 → 时间轴作用点的求值点落在 **F-11.2**
> (`Axis_effective = Axis_base + axis_offset_by_quality[quality − 1]`),**11 是 21a F5 的唯一调用点**。
> ⚠️ **本轮所有状态由撰写方置入,待 11 的首轮 `/design-review`(新会话)确认。**

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-prescription-001 | 病名不给 11 —— 8 与 11 之间刻意无数据流;11 零 `diagnosis` / `disease_id` / `tier_named` 引用,零「遍历 `indications[]` 与病人状态比对」的匹配逻辑 | — | ❌ 规则一 · 规则八 · AC-11-01;承 `diagnosis-system`「8 与 11 无数据流 —— 这是刻意的」。**这是支柱一在代码里的物理形状** |
| TR-prescription-002 | 11 不发明结算 —— 药效 / 半衰期 / 品级偏移表 / 作用轴 / 剂量域全来自 21a 的 `drug_profile`,11 不得重定义 | ADR-014 | ⚠️ 规则二 · AC-11-02;烘焙与「一处声明」归 ADR-014(covered),字段级单一出处属跨 GDD 纪律 ⇒ partial |
| TR-prescription-003 | 处置事件载荷七项齐备(tick / 处置_id / 施予者 / `polarity` / `drug_potency` / `half_life` / `Seq`),与 10 同形 | ADR-005 + ADR-009 | ✅ 规则三 · AC-11-03;9 不区分这一笔来自 10 还是 11 |
| TR-prescription-004 | 11 的作者态数据表 = (药 → 处置_id) 映射 + 呈现层措辞键;21a 的 `ItemDef` 没有 `action_id` | ADR-014 | ✅ 规则四;`assets/data/prescription_actions.json` → 两阶段烘焙。本 GDD 最承重的一处数据边界 |
| TR-prescription-005 | `polarity` 双表一致性:9 的病种注册表 = 判定真源,11 的处方表 = 镜像;构建期交叉校验,不一致 = 硬失败(`throw`) | ADR-014 | ✅ 规则五 · AC-11-07;两表漂移 = 静默撒谎。承 clinic-machine 的 `ADJ_TABLE` 对称性升烘焙硬门先例 |
| TR-prescription-006 | 11 是 F5 的唯一求值点(`half_life := Axis_base + axis_offset_by_quality[quality − 1]`),兑现 `D-21-11` | ADR-014 | ✅ 规则六 · AC-11-08;11 是唯一知道「这一剂的 `quality`」的地方。P0 的 `quality_axis` 唯一 = `half_life`(D-21-23) |
| TR-prescription-007 | `Axis_effective` 的下界恒成立(9 用作除数),由 **21a 的构建期断言**保证;11 不重复 `clamp` | — | ❌ 规则六 · F-11.2;11 侧重复 `clamp` 会掩盖 21a 的断言失败。⚠️ **2026-09-19 二轮(BL-1)订正**:11 现写的下界是 **`≥ MIN_USABLE_HALF_LIFE`**,而 21a 实断 **`> 0`** —— **两者不是同一把尺**(`> 0` 下合法极小值静默退化为「无药效」)。该值**全库零定义** ⇒ 本行**承接件 = 21a**(`O-11→21a` / `item-database.md` `D-21-34`),11 不自行收紧 |
| TR-prescription-008 | 剂量在 `dose_range` 内取整数档;不存在第 `hi+1` 档(限位由**戥子的物理档位集合**给出,非运行期 `clamp`);11 不得读病人严重度替玩家调剂量 | — | ❌ 规则七 · F-11.1 · `AC-11-17` / `AC-11-18`;剂量是玩家的显式输入,不是系统按病情自动缩放的系数。⚠️ **2026-09-19 订正(R-4 + 二轮确认)**:**原文本「域内 `clamp`」已作废** —— 运行期 `clamp` 删除(会静默修正合法输入),限位改由**戥子的物理档位**(42 的规格义务)承担。`OQ-11-3` / `OQ-11-4`(⚠️ 称量手法的代价侧已由 `OQ-53-8` 独立结清) |
| TR-prescription-009 | 单次舍入 = `ROUND_HALF_AWAY_FROM_ZERO`;乘加链只在输出处舍一次,中间域保持 Q16.16 原始整数 | ADR-006 | ✅ F-11.1 / F-11.3 · AC-11-11;承 21a F1 的单一舍入纪律 |
| TR-prescription-010 | `single_dose_max` = 烘焙期派生常量(max over 全部药 × `dose_range.hi` of `|dose_potency|`),零手填,供 9 的 F1 `clamp` 上界 | ADR-014 | ✅ 规则九 · AC-11-09;兑现 9 侧「由 11 / 21a 提供」的残留。派生 = 结构上不可能漂移 |
| TR-prescription-011 | `indications[]` / `contraindications[]` 不拦不扣、只作呈现;病种 id 不得进呈现层(呈现 = 烘焙期转出的古籍功效词) | ADR-013 | ✅ 规则八 · AC-11-06 · AC-11-14;承 ADR-013 §三 + `PresentationDtoGuard` 递归扫描。禁忌若成软门,玩家不再需要判断 |
| TR-prescription-012 | 与 20 的原子性:先验库存再扣,无货不得发事件(否则库存与事件流不一致) | ADR-005 | ✅ 规则十 · AC-11-04;承 20 规则六;破坏 `AC-20-03` 的可重建性 |
| TR-prescription-013 | 11 与 10 的**载荷与流语义唯一**(同写一条病史流、除 `method`/`cause` 外形状一致);11 零第二份 `SkillMul` / `ResultMul` / `JudgeResult` **实现** | — | ⚠️ 规则十一 · AC-11-10;⚠️ **2026-09-19 二轮订正(C5 口径)**:原文本「共用同一个判定 / 熟练度结算函数」**已作废** —— `SkillMul` 经 10 的 A6 改判 = **稳度容差乘子**(只被 `Judge` 的容差消费),11 恒 `Applied` ⇒ 该乘子在 11 **无合法消费点**。C5 在 11 侧**重新解释**为「载荷与流语义唯一」(见 `systems-index.md` §9 C5 行)。`AC-11-10` 断言的是**算法独占**(11 不定义那几个符号),**不约束写者** —— 写者独占由新 **`AC-11-22`** 承担。`OQ-11-6` 已消解 |
| TR-prescription-014 | 11 不请求相机档位(开方不是「动手」的动作);若需近景须回 2 补表行,不由 11 自行发意图 | ADR-020 | ⚠️ **条件式** 规则十二;承 2 的 `R-2-5` 档位驱动表 + ADR-020 §五/§六。⚠️ **2026-09-19 二轮订正**:2 的档位表**当前无 11 行**(`camera-and-viewpoint.md:170-180` 是「待裁」占位,「未裁前本行不生效」)⇒ 本条的「不请求」是**默认态**,**取决于 `OQ-11-7` / `OQ-11-9`**(方笺 = 39 同一本书 ⇒ 维持;= 另一件器物 ⇒ **须回 2 补一行**,届时本行改写)。做成 `Treatment` 会与 10 的形状混淆 |
| TR-prescription-015 | 同一 `(WorldSeed, 药, 剂, 实例, 玩家)` 跨平台重放 ⇒ 处置事件逐位相同 | ADR-012 | ✅ AC-11-15;双级黄金夹具矩阵提供执行载体。⚠️ **2026-09-19 二轮订正**:原文本的输入集含「**技能等级**」**已删** —— R-2 改判后等级**不进载荷**(`AC-11-15` 注明「输入集刻意不含技能等级」),等级只经**省料**改库存,故重放要比对的是**载荷 + 余料**两处 |
| TR-prescription-016 | `polarity` 的判定真源归谁 —— 9 的注册表(本 GDD 裁定)还是 11 的处方表(则 9 的 F1 须改为经烘焙注入) | — | ✅ **`OQ-11-1` ✅ 已结清(用户裁定 2026-09-18)** —— **9 的病种注册表 = 唯一真源**,11 的处方表 = 镜像 + 构建期硬校验(本 GDD 原裁决确认 · 规则五 / AC-11-07)。⚠️ 被否的备选(真源改 11)是结构性改动,须另开 ADR |
| TR-prescription-017 | 处方用药熟练度的 P0 出口范围 —— 30 写「药效 / 副作用 / 用药选项解锁」三项,P0 开多少 | — | ⚠️ **`OQ-11-8`(已收窄)** —— 30 的 `:60` 已就地订正为「**省料 + 用药选项解锁**」(药效删 / 副作用归 P1a)。⚠️ **2026-09-19 二轮**:残留 = **省料的系数映射** —— 单一出处已裁定 = **21a 的 `EFF`**(原 `PROVINCE_SAVE_RATE[]` 已删),30 须登记「等级 → `EFF`」映射(`O-11→30`) |
| **TR-prescription-018** | **具名 `Kind` 的写者独占**:`SimEvent.Kind.DrugTreatmentApplied` 的**构造点仅存在于 11 的程序集**;7a 的序列化器两个 Kind(11 的 + 10 的 `EmergencyTreatmentApplied`)均在白名单内 | — | ⚠️ **`AC-11-22`(BLOCKING,2026-09-19 二轮 BL-4 新立)** —— 反射 / 引用集断言(**非 grep**,承 `AC-20-03` 口径;机制借 ADR-017 §二 白名单)。⚠️ **本条补的是一处从未被断言的缺口**:首轮只断「算法独占」(`AC-11-10`,⇒ `TR-prescription-013`)。**判 partial 而非 covered 的理由**:作者侧「未注册 Kind 构建期失败」的**全局防线**仍缺(10 的 systems-index 行记「五个人独立踩同一块石头」),归下一次 `/architecture-review`,**不在本条内解决** |
| **TR-prescription-019** | **可感知地板的量纲一致**:剂量档位的可感知地板必须与 9 的噪声**同量纲**方可比较;不得把 9 的 `σ`(Progress 域)或 21a 的品级地板(tick 域)的数值挪用到药效幅值域 | — | ❌ **`AC-11-19`** —— 🔴 **2026-09-19 二轮(BL-2)新立**:**9 侧无具名噪声带常量,该 AC 记 `NOT-RUN`**;承接件 = **9**(`O-11→9` / `disease-simulation.md` `D-9-J`)。**本条的价值在「防跨量纲挪用」这一侧**(修法本身可能被误当成「借个数」) |

---

## 19. 炮制 `design/gdd/processing.md`(#18)| 18 条

> **2026-09-18 #18 GDD 落盘时生成** —— 按代生约定追加(18 **没有单一权威 ADR**)。
> `system:` slug = `processing`;归 **Core 层**(`systems-index.md` 行 18)。
> **18 的承重边**:ADR-014(F1 唯一求解器住 21a · 构建期校验)· ADR-006(定点域)· ADR-009 §三(`Craft` 在世界流 Kind 骨架)·
> ADR-005(与 20 的原子性 · `Tick(start) + duration_ticks`)· ADR-012(黄金夹具)。
> **18 的核心纪律 = 「不发明结算」**:F1 唯一求解器住 21a(`item-database.md:450`),
> 18 **只筛子集 / 供料 / 调 F1 / 交 20 执行**;本 GDD **刻意零新数学**(F-18.1 只是调用契约)。
> ✅ 首轮状态已由 **2026-09-19 `/design-review`(NEEDS REVISION · Scope M,11 BLOCKING)** 的当日修订确认并翻转 5 条(003/004/005/007/015;四用户裁定均 [甲] + ADR-009 **Amendment J**),其余 note 同步。

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-processing-001 | 18 不发明结算 —— F1 是唯一配方求解器(住 21a),18 是调用方;结算调用点唯一,零第二实现 | ADR-014 | ⚠️ 规则一 · AC-18-01;承 `item-database`「17/18/19 只产出输入参数」;「唯一求解器」是跨 GDD 纪律(21a 拥有)⇒ partial |
| TR-processing-002 | 18 的代码路径不得求值 `Ceil(base/EFF)` / `Round(qty × QtyMultiplier)` / `clamp(InputQuality × Retain)` —— 它们是 F1 / F2 的正文 | ADR-006 | ✅ 规则一 · AC-18-02;由单调用点断言守住;在 18 侧重写算式 = 第二求解器 |
| TR-processing-003 | 18 传给 F1 的 `SkillMod` 输入是 `Level`(等级),非 `cap × Level / SKILL_CAP` 的结果 —— 否则曲线被压成两段阶跃;判据须机器可验 ⇒ 前置 = 21a newtype `SkillLevel`(**D-21-33**) | — | ⚠️ 规则一 · AC-18-03(spy 入参恒等断言现测);❌→⚠️ 2026-09-19,O-18-R7 / D-21-33 登记 |
| TR-processing-004 | 炮制子集判据 = `Recipe.owner ∈ {process, craft, build}` 显式字段(21a **D-21-30**);「state 是否变化」降为单向校验 | ADR-014 | ✅ 规则二 · R-18-B(OQ-18-1 已裁 [甲]);字段已在 21a schema 落盘 + AC-21a-66;⚠️→✅ 2026-09-19(18 侧 AC-18-18 记 EXTERNAL·BLOCKED-BY-21a) |
| TR-processing-005 | 准入两道门:`skill_gate` 是门槛(非系数,否则与 `SkillMod` 双重计数)+ `min_quality` 是准入闸(不改药效) | — | ⚠️ 规则三 · F-18.2 · AC-18-05/20(`Admissible` 同一函数双路径:客户端预答 + 主机终裁,ADR-011 Amendment B 同构);❌→⚠️ 2026-09-19,仍无 ADR 承接 |
| TR-processing-006 | 四个修正项的输入侧:18 供料(`Level` / 输入实例 / `EquipMod` 读 24 / `EnvMod` 读 5+24),21a 定曲线 | ADR-014 | ✅ 规则四;承 21a F1 变量表。18 供料、21 定曲线的分工是「一处声明」纪律的落实 |
| TR-processing-007 | `EnvMod` 可为负,18 必须原样传递(两分量 `EnvMod_climate` / `EnvMod_clinic` 各自零加法零 clamp);求和+钳制唯一落点 = 21a F1 正文 `EnvMod_total`(**D-21-31**) | ADR-006 | ✅ 规则四 · AC-18-06(反例哨兵夹具);⚠️→✅ 2026-09-19 R-18-C 结案钳制归属(5 侧 AC-5-19 同轮收窄) |
| TR-processing-008 | 调 F1 一次拿三个出参(`ActualConsumed` / `OutputQty` / `OutputQuality`);F-18.1 只是调用契约,零新数学 | — | ❌ 规则五 · F-18.1;本节刻意零新公式。调用契约形状无 ADR |
| TR-processing-009 | `ActualConsumed` 是运行期派生量,须随该次 `Craft` 事件落世界流;18 是该值的唯一产生者(兑现 21a 的 AC-21a-52) | ADR-009 | ✅ 规则五 · AC-18-11;承 ADR-009 §三 + D-21-15(实耗) |
| TR-processing-010 | 产出非零是结构(`max(1, Round(qty × QtyMultiplier))`),18 不得加一层失败判定 / 有概率产出 0 | — | ❌ 规则六 · AC-18-07;加一层骰子 = 越权改 21a 的不变量 |
| TR-processing-011 | 与 20 的原子性:容量不足则 20 整体拒绝 ⇒ 炮制不发生;18 不得先扣后补(部分成功破可序列化前提) | ADR-005 | ✅ 规则七 · AC-18-12;承 20 规则六。部分成功会让重放得到不同的中间态 |
| TR-processing-012 | 完成时点 = `Tick(start) + recipe.duration_ticks` —— tick 计、非墙钟、零浮点 | ADR-005 | ✅ 规则八 · F-18.3 · AC-18-13;与 9 的病程同 tick 域(主机唯一 `Step`) |
| TR-processing-013 | 18 不认识药 —— 零 `drug_profile` / `polarity` / `treatable_by` 引用;18 的输出是物品实例,不是治疗 | — | ❌ 规则九 · AC-18-04;「17/18/19 只产出输入参数」的机制化含义;治疗是 11 + 9 的事 |
| TR-processing-014 | 四 `cap` ↔ `QTY_MULT_MAX` 的构建期校验(Σ 正的 cap + `max(0, ENV_MOD_MAX)` ≤ `QTY_MULT_MAX − 1`)· 主执行 = 21a 烘焙管线,18 侧 = 装载期防御断言半边 | ADR-014 | ⚠️ 规则四 · AC-18-14 · `OQ-18-2` ✅ 已裁 [甲] 2026-09-19(执行方已定;21a 侧执行 AC 待 O-18-R2 回写 ⇒ partial 维持) |
| TR-processing-015 | 并发模型:每玩家单炉、不可取消、拒绝零事件、跨玩家并发允许(器具互斥另归 24 = `OQ-18-8`) | — | ⚠️ **OQ-18-3 ✅ 已裁 [甲] 2026-09-19**(规则八并发模型表 + AC-18-23);❌→⚠️ 残留 = OQ-18-8(归 24) |
| TR-processing-016 | 炮制中途存档的语义 —— 完成 tick 已定、重放可重建?还是须把「进行中」作为进程态存 | ADR-010 | ✅ `OQ-18-5` 已结清(发起即落流,完成 = 派生)+ AC-18-16/17。**2026-09-19 解除其两处派生悬空**:① 载荷三位定稿(R-18-A:`actor_id` / `output_instance_ids[]` / `tool_cell`);② **`D-21-28` 结案** —— 三流全序键 `(Tick, StreamPriority, None, Seq)` [甲] + 主机发号,落 **ADR-009 Amendment J** |
| TR-processing-017 | 同一 `(WorldSeed, recipe, 输入实例集, 四修正项)` 跨平台重放 ⇒ 三个出参逐位相同 | ADR-012 | ✅ AC-18-15;双级黄金夹具矩阵提供执行载体 |
| TR-processing-018 | 参数装载期硬失败(`ENV_MOD_MIN` 未定值 / `outputs[]` 为空等),禁 null 兜底 | ADR-014 | ✅ AC-18-14 · 边例;承 ADR-014 §五 + 21a 的非空约束 |

---

## 20. 库存与物品 `design/gdd/inventory-and-items.md`(#20)| 16 条

> **2026-09-18 #20 GDD 落盘时生成** —— 按代生约定追加(20 **没有单一权威 ADR**)。
> `system:` slug = `inventory`;归 **Core 层**(`systems-index.md` 行 20)。
> **20 的承重边**:ADR-009 + ADR-010(库存 = 世界流纯投影;快照 = 优化非真相)·
> ADR-005 + ADR-001(`instance_id` 铸造权 = 主机唯一;客户端意图上行)·
> ADR-006(定点域 / 高水位重构)· ADR-014(烘焙期上界门)· ADR-013(载重呈现)。
> **20 不是第二真源**:`InventoryOf(player) = fold(世界流, 谓词 ∈ {DropClaimed, 消耗, 转移})` ——
> 它是六份「零涟漪批」(17 / 18 / 29 等)的共同上游回填端。
> **本轮一条实质裁定**:载重呈现 = **拟物器具(药箱满溢感)**(用户裁定 2026-09-18;
> 机制侧只有一条布尔 `CanCarry`,四档是**呈现分档**非机制分档)。
> ⚠️ **本轮所有状态由撰写方置入,待 20 的首轮 `/design-review`(新会话)确认。**

| TR-ID | 需求 | ADR | 状态 |
|-------|------|-----|------|
| TR-inventory-001 | 库存 = 世界流的纯投影(`InventoryOf = fold`);7a 快照是优化非真相,与三流冲突时以三流为准 | ADR-009 + ADR-010 | ✅ 规则一 · AC-20-03;承 ADR-009 §一 Q1 + §二 + ADR-010「快照 = 优化非真相」 |
| TR-inventory-002 | `instance_id` 铸造权 = 主机唯一(`IIdAuthority.ItemInstanceId.Next()`);客户端不自铸,意图上行、id 由主机回填 | ADR-005 + ADR-001 | ✅ 规则二 · AC-20-01 · AC-20-02;兑现 21a 的 D-21-26 / D-21-27 + AC-21a-63 |
| TR-inventory-003 | `instance_id` 的高水位重构 `next = max(instance_id) + 1`,扫三流并集(与 `patient_id` 同机制) | ADR-006 | ✅ 规则二;承 ADR-006 Amendment B,其 id 空间由 ADR-016 §二 扩大。不为 `instance_id` 新开计数器 |
| TR-inventory-004 | 堆叠键 = `(item_key, quality)`(21a F4 唯一出处);容器自身不堆叠(`qty` 恒 1);20 不得改写 `StackKey` | — | ❌ 规则三 · AC-20-04;不同品级不堆、各占一个 `ItemInstance`;20 只执行 |
| TR-inventory-005 | `CarryLoad(p)` = Σ(`ItemDef.weight` × `qty`),整数求和、先乘后加;`weight` 是 `int` 非 `Fix`(D-21-17) | ADR-006 | ✅ 规则四 · F-20.1 · AC-20-08;D-21-17 的 `weight` 口径在本式落地 |
| TR-inventory-006 | `CanCarry ⟺ CarryLoad + InstanceWeight ≤ CARRY_CAP`;硬不变量 ∀p `CarryLoad(p) ≤ CARRY_CAP`(52 的归一化定义域) | — | ❌ 规则四 · F-20.2 · AC-20-09;承 52 的 `clamp(CarryLoad / CARRY_CAP, 0, 1)` 要求该不变量恒成立。**`CARRY_CAP` 值未裁**(`OQ-20-2`)⇒ 不变量无值可验 |
| TR-inventory-007 | 20 不发明结算 —— 只执行上游算好的量;零 `EFF` / `QualityMod` / `quality_distribution` 的求值 | — | ❌ 规则五 · AC-20-05;承 `item-database:450`;20 是同一纪律的下游侧 |
| TR-inventory-008 | 存取原子性:容量不足则整体拒绝(拾取整件拒绝、炮制整体回滚),无半途状态 | ADR-005 | ✅ 规则六 · AC-20-06 · AC-20-07;承 ADR-005「状态可序列化」前提 |
| TR-inventory-009 | 20 不接触战斗与病程 —— 只交读数(`InventoryOf` / `CarryLoad` / 存在性);不判对症、不判伤情用何物 | ADR-009 | ✅ 规则七;它是库房不是医生;消费者 = 29 / 25 / 52 |
| TR-inventory-010 | 溢出界 `|Inventory| × stack_max × max(weight) < 2^31` —— 烘焙期可算常量,构建期硬门断言 | ADR-014 | ⚠️ F-20.1 注;物品种类闭集 ⇒ 上界可算;承 clinic-machine 的 B6 同款纪律。构建期门 covered,具体值班方未裁 ⇒ partial |
| TR-inventory-011 | 载重呈现 = 药箱的满溢感(四档定性),零负重条 / 百分比 / 网格 / 排序 / 自动整理 | ADR-013 | ✅ 规则四 · UI-20.3 · AC-20-10 · AC-20-12;**用户裁定 2026-09-18**(拟物器具);承 ADR-013 §三 + 42 规则五「不得是背包格」 |
| TR-inventory-012 | 全部 20 的输出 DTO 递归反射扫描无 `float` | ADR-006 | ✅ AC-20-15;门 A 的常规落实(与 `AC-44-B1` 同型) |
| TR-inventory-013 | `CARRY_CAP` 未定值 ⇒ `CanCarry` 无法求值 ⇒ 20 装载期硬失败(禁 null 兜底 / 默认容量) | ADR-014 | ✅ AC-20-13 · 边例;承 ADR-014 §五 装载期硬失败纪律(E-13 同款) |
| TR-inventory-014 | 消耗 / 转移的 `Kind` 归属 —— 复用既有 `Craft` / `DropDespawned`,还是须追加 `ItemConsumed` / `ItemTransferred` | ADR-009 | ✅ **`OQ-20-1` ✅ 已结清(用户裁定 2026-09-18)** —— **全部复用既有 Kind,零新增**:炮制 `Craft`(扣减 = `ActualConsumed` 纯函数)· 建造 `StructurePlaced`(扣减 = `cost(m)` · 23 规则七)· 用药 `DropDespawned`(实例级)· 转移 `DropSpawned`→`DropClaimed`。落点 = 20 规则一 + AC-20-16/17。**零 ADR 涟漪**;残留 = R-2 落定 `DropDespawned` 载荷须含 `reason` 枚举 |
| TR-inventory-015 | 容器折重实现 —— 装子件的容器自身重量是否含子件(21a Schema E 登记待裁) | — | ❌ **OQ-20-4**;负重读数可能双重计数;归用户 + 20 / 21a |
| TR-inventory-016 | 联机时的库存同步粒度 —— 逐件同步还是整箱快照 | ADR-001 | ❌ **OQ-20-5**;ADR-001 已立 pipe 抽象与两条 QoS 通道(covered),但库存这一层粒度未裁;P1b 归 45,P0 单机不阻塞 |

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
| TR-input-016 ⟳ | `L_render` / `L_poll` **实测**;**`JUDGE_BUDGET` 已删** ⇒ 交付给 10 = 「**均值达标 + 抖动有上界**」两条义务 | **非 ADR 缺口,是实测前置** —— 原式与 `L_input→pixel ≤ 50 ms` **代数等价**(评审发现),已删;前置 = **最低目标硬件定稿** + **两端**(K&M / Gamepad)可跑 player ⇒ 绑定 `/test-setup` |
| TR-emergency-015 ⟳ | 端到端输入延迟 < 50 ms(`AC-10-08`,**均值达标 + 抖动有上界**两条义务) | **非 ADR 缺口,是实测前置** —— **与 `TR-input-016` 同一前置**(最低目标硬件 + 两端可跑 player)⇒ 绑定 `/test-setup`。**不可自动化测**,须主创签核。10 是 P0 唯一需要「手感」的系统 |

### Core 层缺口

| TR-ID | 需求 | 建议动作 |
|-------|------|---------|
| TR-diag-002/004/006 | 8 的铁律①③⑤ | 由 8 / 39 的 ADR 承接 |
| TR-disease-020/021/022 | 扫描单向性 · 出现率上限 · 中间类型 | ADR-005 修正案 + 9 的 GDD 修订 |
| TR-diag-010 | G-4 的 FMA 缓解 | 改措辞为「定表化」(报告 E-3) |
| TR-patient-005/006 | 分档阈值与九态对齐 · 行为范围 = 在场 | `OQ-13-2`(13 + 9 联合)· **前置 = 9 的 `OQ-8` 标定**(13 不代 9 裁决) |
| TR-player-005 · TR-camera-006 ★ | **移动基向量**(F-1-8 的输入)· **`ICameraRig.YawBasis`** 接口交付 | ✅ **2026-09-16 联合契约已闭合** —— 1 侧 F-1-8 / `AC-1-31` / `AC-1-35` 已定;2 侧 `camera-and-viewpoint.md` 交付 **F-2-2 + `AC-2-07…10`**(登记义务 `O-8` 结清,两条 TR 的 `status: partial → covered`)。⚠️ **残留两条反向义务**:2 → 1 的 **`O-11`**(`PITCH_MAX` 实际取值回填 `OQ-1-14`)与 **`O-14`**(帧内恒定对侧确认);二者**不阻塞本行的结清** |
| TR-player-007 ★ | `CharacterController` 参数契约(`slopeLimit` / `stepOffset` 几何取值) | **1 + ADR-015 的联合契约** —— 1 侧已定(F-1-9 / `AC-1-33`),缺 ADR-015 §一点名几何取值(登记义务 `O-9`) |
| ~~**TR-interaction-015**~~ ✅ | 病人四种路由(37 立案 / 8 查体 / 11 施治 / 10 急救)的语义定义归 8 / 10 的动作词表 | ✅ **已结清(2026-09-19 · `D-8-12`)** —— 8 侧 S-8.4 动作词表(路线甲)落盘,`TR-case-036` 同根同批翻转。**本行退出优先清单** |
| **TR-timeweather-015** ⚠️ | 深水线的季节修正曲线 —— 5 产 `season_index`,调制归 17 | ✅ **`OQ-5-1` 形式已结清(2026-09-18)** —— 逐季乘子表 `Capacity × SEASON_MULT[season_index]`;**5 出下标 / 17 持表**(F-17-3)。**残留归数值轮**:`SEASON_MULT[]` 的值 + `Capacity` / `RegrowWindow`(`OQ-8-11` / `OQ-17-1`) |
| ~~**TR-emergency-016**~~ ✅ | `ResultMul[Missed]` = 0 还是非 0(「动手但失败」要不要有代价) | ✅ **已结清(2026-09-18)**:`ResultMul[Missed] = 0.25`(非零)。落点 = 10 的 F-10.4 + AC-10-16。**本行退出优先清单** |
| **TR-emergency-017/018/019** | 四判定门阈值(含 `JITTER_MAX`)· 纯键盘幅度回退 · 动作打断语义 | **`OQ-10-2` / `OQ-10-3` / `OQ-10-5`**;`JITTER_MAX` 是最承重旋钮(杀死「手稳」的是抖动非均值)。**形状已定,值归用户**(首次 playtest 前) |
| ~~**TR-prescription-016**~~ ✅ | `polarity` 判定真源归谁 | ✅ **已结清(2026-09-18)**:**9 的病种注册表 = 唯一真源**,11 的表 = 镜像 + `AC-11-07` 构建期交叉校验。**本行退出优先清单** |
| ~~**TR-processing-016**~~ ✅ | 炮制中途存档的语义 | ✅ **已结清(2026-09-18)**:**发起即落流,完成 = 派生** ⇒ 无进程态可存。⚠️ 残余:**`D-21-28`(Craft 总序键)严重度上升**,归 ADR-005 / 19 / 45。**本行退出优先清单** |
| ~~**TR-inventory-014**~~ ✅ | 消耗 / 转移的 `Kind` 归属 | ✅ **已结清(2026-09-18)**:**全部复用既有 Kind,零新增**,零 ADR 涟漪。残余 = R-2 落定 `DropDespawned` 载荷须含 `reason` 枚举。**本行退出优先清单** |
| TR-timeweather-006/007 | `TICKS_PER_DAY` 单一定义点(5)vs 52 的 `const` 副本;昼夜相位环绕安全 | 承 52 的 **DC-3**;两常量同值约定须**机械守**(构建期硬失败);相位须取模式,朴素区间比较**恒假** |
| TR-prescription-001/013 | **病名不给 11**(支柱一在代码里的物理形状)· 11 与 10 共用同一判定 / 熟练度函数(C5) | `001`:8 与 11 刻意无数据流(承 9)。`013`:承 `systems-index` **C5**,共享函数落点归 10 的 GDD;`OQ-11-6`(第三 / 四个调用方出现时须重构) |
| TR-processing-001/003/005/008/010/013 | 18 **不发明结算**(F1 唯一求解器住 21a）· 只传 `Level` 不传结果 · 两道准入门的性质 · 调用契约零新数学 · 产出非零是结构 · 18 不认识药 | 承 `item-database:450`「17/18/19 只产出输入参数」。**同一个失败模式的六个面** —— 任何在 18 侧重写的算式都是第二求解器 |
| TR-inventory-004/006/007 | 堆叠键 `(item_key, quality)` · `CarryLoad ≤ CARRY_CAP` 硬不变量 · 20 不发明结算 | `006` 是 **52 的归一化定义域**(`clamp(CarryLoad / CARRY_CAP, 0, 1)` 要求该不变量恒成立),而 **`CARRY_CAP` 值未裁**(`OQ-20-2`)⇒ 无值可验 |
| TR-input-017/019/021 | `EmergencyAction` 枚举基数 · **`Emergency` 表达力缺口** · **跳过路径语义** | 归 **10 的 GDD**,三条**同批 = 10 的硬前置**(`OQ-3-5`);3 只提供通道形状,**不代 10 裁定**。**`019` 承重(2026-09-16 收窄)**:`Button(0/1)` 表达不了止血**力道**;节奏走边沿、非缺口 |
| ~~TR-input-018~~ | ~~`OpenInventory` 的 P0 归属~~ | ✅ **2026-09-15 结案** —— 「真实玩法断头」**经核验为夸大**(系统 20 = P0 · Core);`OQ-3-6` 关闭,缺口撤销(非缺口降级,是**误登记**) |

### Feature / Presentation 层缺口(较低)

| TR-ID | 需求 | 建议动作 |
|-------|------|---------|
| ~~TR-concept-008~~ | ~~拟物 UI 手柄焦点导航~~ | ✅ **已落盘**(2026-09-15,ADR-013 UI Toolkit 主 + UGUI 补 world/XR;报告 R-6 / E-16) |
| TR-diag-019/020 · TR-case-019 | `disease_id` 不进呈现层 / 脉案 DTO 静态检查 | 机制已定(ADR-013 §三 `PresentationDtoGuard`);待 39 脉案 GDD 与实现 |
| TR-patient-021/022/023/024 | 查体诱发痉挛写路径 · 搬运写路径 · 13 无玩家 UI · 无障碍 | 写路径归属待裁(13 只读 ⇒ 归 10 / 9);无障碍评审待 `/ux-design`;`TR-patient-023` 是设计结果非缺口 |
| TR-input-013 | `iconKey` 的**命名空间 / 枚举表签署方** + 48 教学的显示时机 | 归 **42 / 48**(`OQ-3-2`);**3 侧已定,缺的是呈现侧** |
| TR-input-015 | `OpenXRInput` haptics **spike** | ADR-011 §Risks F5;**P0 只落接口**,实现与 ADR-013 / 018 §七 / 020 §三 同批推 P1a(`OQ-3-4`);**VR 整体归 P1b** |
| TR-input-020 ★ | **反幻想守门:目视零按键提示浮层 + 手柄单机可走查脉案** | 归 **42 + 48 联合 BLOCKING**(**`AC-3-F1a`(P0)/ `F1b`(P1a·P1b)**,2026-09-16 按阶段拆分);**3 侧已定**(只给键名),缺 42 / 48 侧落地 ⇒ partial;**48 教学已从队列末位上提**(它是 3 的 AC 的直接依赖) |
| TR-inventory-011 | 载重呈现 = 药箱满溢感(四档定性),零负重条 / 百分比 / 网格 / 排序 / 自动整理 | ✅ **已定**(ADR-013 §三 + 42 规则五「不得是背包格」;**用户裁定 2026-09-18** 拟物器具)。四档是**呈现分档**,机制侧只有一条布尔 `CanCarry` ⇒ 42 落实现时须按定性档位而非百分比 |
| TR-inventory-016 | 联机时的库存同步粒度(逐件 vs 整箱快照) | **P1b 归 45**;ADR-001 已立 pipe 抽象与两条 QoS 通道(covered),粒度未裁 ⇒ gap。**P0 单机不阻塞** |

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
| 2026-09-15 | #13 病人 AI GDD 落盘(TR 回溯追加) | **ID 增补 24 条**(163 → **187**),全部为新 `system:` slug `patient-ai`(TR-patient-001…024)。按 **ADR-016 Ordering Note** 的约定回溯追加(与 21a / 52 同法)—— 13 无 GDD 时其 TR 悬置,撰 GDD 时补登。**含一条用户裁定**:13 的**决策层住边界层(呈现侧),不进 sim 程序集** —— 解 ADR-016 §一(决策层住 sim 整数)与 §六(13 读唯一浮点出口 `VitalsDto`)的接缝;**ADR-016 §一 表已补 13 例外注**。`TR-patient-006` 的 ⚠️ 根因 = 9 的 `OQ-8`(在场判定无值源)。**汇总 83 → 101 ✅ / 14 → 15 ⚠️ / 66 → 71 ❌**(合计 163 → 187) |
| 2026-09-15 | ADR-021(POI 状态所有权与世界流承载) | 结清三方复核登记的洞 **H2**(POI 状态无拥有者)。**ID 增补 9 条**(187 → **196**),新 `system:` slug **`world-eco`**(TR-worldeco-001…009)。裁决 = **POI 定义 = 派生态 / POI 状态 = 模拟态进世界流**;**写者 = 6 世界与生态区**;新增世界流 Kind `PoiStateChanged`;**ADR-009 §二/§三/§六 三处就地修订**(Amendment F)+ **ADR-010 §三 追加义务 11** + `architecture.yaml`(世界流 interface / IEventSink 路由 / `poi_state` 所有权条目)+ `entities.yaml`(Kind)。**6 无独立 GDD**,需求**代生**(同 1 / 2 例)。唯一 ❌ = `TR-worldeco-009`(`PoiState` 枚举 + 状态机,刻意留 6 GDD)。**汇总 101 → 109 ✅ / 15 ⚠️ / 71 → 72 ❌**(合计 187 → 196) |
| 2026-09-15 | #3 输入与设备 GDD 落盘(TR 回溯追加) | **ID 增补 18 条**(196 → **214**),新 `system:` slug **`input`**(TR-input-001…018)。**ADR-011 落盘时未生成 TR**(其 §Enables 只声明 `TR-concept-007` gap→covered),本 GDD 按其裁决回溯补齐(同 §8 的 1 / 2 例)。**3 是架构复核 §2 的「零覆盖」系统**(GDD ❌ / 架构覆盖 ❌ 零),故 18 条全部为新登记。**两条用户裁定**:① **P0 不交付改键 UI**(能力已备、界面未交付,推 P1a;`BindingsStore` 读写 + schema hash 校验已立住 ⇒ 补界面不返工)② **3 出「绑定 → 图标键」映射,呈现归 42 / 48** —— 3 **只给键名不给素材、连「要不要显示」都不知道**,故**反幻想条款的守门责任随之转移**。**三条新登记缺口**:`016`(`L_render` 实测 → 反推 `JUDGE_BUDGET`;非 ADR 缺口,是实测前置)· `017`(`EmergencyAction` 枚举基数,归 10)· `018`(**`OpenInventory` 的 P0 归属 —— 真实玩法断头的登记**,归 20)。**汇总 109 → 122 ✅ / 15 → 17 ⚠️ / 72 → 75 ❌**(合计 196 → 214) |

| 2026-09-15 | **#3 GDD 首轮 `/design-review` 连带修订**(NEEDS REVISION · Scope XL) | **ID 增补 3 条**(214 → **217**):`TR-input-019`(**`Emergency` 表达力缺口**,`OQ-3-5`,gap)· `TR-input-020`(**反幻想守门 42+48 联合 BLOCKING**,partial)· `TR-input-021`(**跳过路径归 10**,gap)。**原地回填 9 条**(`001` 同一性 · `004` hash 须含 `bindingId` + FNV-1a-64 · `005` `Instantiate()` + 不启 UI map · `006` 回调 / 抖动理由 / VR P1b · `007` `FixParse` 范畴错误 · `011` `Mixed` 有效输入定义 · `012` 补 `FindActionMap` + 字符串索引器 · `015` VR P1a→P1b · `016` `JUDGE_BUDGET` 已删)。**状态变更 1 条**:`TR-input-018` **❌ → ✅**(`OpenInventory` 的「真实玩法断头」**经核验为夸大**;`OQ-3-6` 关闭)。**连带 ADR-011 Amendment A**(六项口径修正,**不推翻三条核心裁决**)。**根因诊断**:该 GDD 的多数严重缺陷**非自创**,而是**忠实继承** ADR-011 的未推演断言 ⇒ 修正落到 ADR-011 以防下游继续传错。**汇总 122 → 123 ✅ / 17 → 18 ⚠️ / 75 → 76 ❌**(合计 214 → 217) |
| 2026-09-16 | **#3 GDD 三轮定向对抗验证**(NEEDS REVISION · 7 阻塞 + 2 项用户设计裁定) | 按二轮结论**不做第三轮 full 评审**,改以**对两条不变量签核**(I1 结构哈希 · I2 无状态意图源)。**ID 不增不减**(仍 **217**);**原地回填 3 条**:`TR-input-013`(「AC-3-D1 / D2 BLOCKING」订正为**仅 `AC-3-D2` BLOCKING**)· `TR-input-019`(**真缺口收窄为「仅幅度一维」** —— 节奏分辨率不是缺口)· `TR-input-020`(**`AC-3-F1` 拆 `F1a`(P0)/ `F1b`(P1a·P1b)**,去 VR + 纸质地图归 P1b)。**两项用户裁定**:D-A **官方桥为焦点移动唯一真源**(3 的 `Navigate` = UI map 的那一个 action 实例;`FocusNavigationIntent` 降为**类型化只读视图**,零 ADR 修订)· D-B **`AC-3-F1` 按阶段拆**(AC 27 → **28 条**,BLOCKING 15 → **16**)。**ADR-011 同轮就地覆写四处残留**(`:326` 去 VR · `:335` `Update` → `onAfterUpdate` · Alt 2 理由改方差 · `:261` 撤「已扫全文」自述)。**根因**:三轮同构复发 —— 修订的「工作单位」错了(按措辞句级 patch,而非命题级 sweep)。**汇总不变:123 ✅ / 18 ⚠️ / 76 ❌**(合计 217) |
| 2026-09-16 | **#1 玩家控制器与移动 GDD 首轮 `/design-review` 连带修订**(NEEDS REVISION · 六条根因 + 2 项用户裁定) | **ID 增补 4 条**(217 → **221**):`TR-player-005`(二维 `MoveInput` → **世界方向的基**,⚠️)· `TR-player-006`(**跨格事件 `Append` 权 = 主机唯一**,✅)· `TR-player-007`(**`CharacterController` 参数契约**,⚠️)· `TR-camera-006`(**`ICameraRig.YawBasis`**,⚠️)。**六条根因的登记面兑现**:① 移动基向量缺失(三方独立命中,最高优先级)② 归并算符三版不等价 ③ 谁 Append 从未定义 ④ `F-1-1a` 用错变量(`TICK_PERIOD` → **`MAX_DT`**)+ 所有权反转 ⑤ `CharacterController` 参数从未命名 ⑥ 接地模型三命题不相容。**2 项用户裁定**:冲刺整档 **P0 砍掉**(无代价 ⇒ 严格支配 Walk)· 接地模型**推迟到 spike**(`OQ-1-12` = 系统 1 唯一的 P0 开工阻塞级未决项)。**ADR-020 同日落 Amendment B**(Append 权 = 主机唯一 · `ActorCellEntered` 移出可靠通道 · 新立 `ICameraRig.YawBasis`)+ **AC-20-13/14**;**ADR-009 Amendment G** 的 `tick` 措辞同步为「**观察到**跨格的那一 tick」(`O-10`)。**三条 ⚠️ 的根因均不在 1 的仓里**:`TR-player-005` / `TR-camera-006` = **2 无 GDD**(`O-8`),`TR-player-007` = **ADR-015 §一 未点名 `slopeLimit`/`stepOffset` 几何取值**(`O-9`)。**汇总 123 → 124 ✅ / 18 → 21 ⚠️ / 76 ❌**(合计 217 → 221) |
| 2026-09-16 | **ADR-022 关卡工具落盘(用户裁定「另开 ADR 立新系统」)** | **ID 增补 8 条(221 → 229)** —— 新 slug `level-tool`(`TR-leveltool-001…008`,全 `covered`)。**54 无独立 GDD** —— 编辑期工具非玩法系统,系统定义 + 产出契约 + 六项一致性检查 **C1–C6** 即 ADR-022;需求文本由 ADR-022 + 6 的 GDD 登记义务生成。**结清两条悬空判据**:`ADR-015 §Validation:317` 与 `ADR-016 §Validation:442` 的「在关卡工具中存在」—— 此前**无物被指定为「关卡工具」**,现指向一个有系统行(54, Tooling 层)/ 有输出契约 / 有被命名检查的系统。**结清 6 的 `O-6-9` / `O-6-11` / `O-6-12`(工具侧)/ `O-6-14`(产出方),并裁定 `OQ-6-8`(导航格判「分块流式」),落 `ADR-015 §四` 补注。****合计校正 229 / 135 ✅ / 19 ⚠️ / 75 ❌** | `adr-022-level-tool.md` · `systems-index.md` 行 54 · `architecture.yaml` |
| 2026-09-16 | **#6 世界与生态区 GDD 落盘(TR 回溯核对)** | **ID 不增不减**(仍 **221** 条)—— 本行是 **ADR-021 代生的 `world-eco` 组的回溯核对**,非新登记。**状态变更 1 条**:`TR-worldeco-009` **❌ → ✅**(`PoiState` 枚举 + 状态机规则由本 GDD 结清:`Undiscovered(0) → Discovered(1) → Resolved(2)` 三态**单调不可逆** + 4 条平表转移 + 三方非法处置)。**`gdd:` 指针 9 条改指** `design/gdd/world-and-ecozones.md`(不再代生自 `systems-index.md`)。**口径收紧 1 条**:`TR-worldeco-007` 的有界性 `\|POI\| × \|STATE\|`(3 倍)→ **`2 × \|POI_DEF\|`**(三态单调不可逆 ⇒ 每 POI 至多 2 次转移;跳级 `0→2` 只算 1 次)—— 同步至 `entities.yaml` 的 `SimEvent.Kind.PoiStateChanged.constraint` 与新增 `formulas.poi_transition_bound`。**本轮同时校正本表两处自身陈旧口径**(与 registry 逐条实测对齐,系既有笔误,非 TR 变更):① §汇总表把 **1 / 2 合并在「1 玩家移动 + 2 摄像机」一行**(13 条),而 2 的 `camera-and-viewpoint.md` 早已落盘 ⇒ **拆两行**(1 = 7 条 6✅/1⚠️;2 = 6 条 6✅);② **3 的行为 15✅/3⚠️/3❌**,实测 **14✅/3⚠️/4❌**(§Enables 侧长期漏计 `TR-input-021` 的 ❌)。**校正后合计 127 ✅ / 19 ⚠️ / 75 ❌**(合计 221),与 `tr-registry.yaml` 一致。⚠️ 状态变更由**撰写方**置入,**待首轮 `/design-review` 确认** |
| 2026-09-17 | **#27 敌人 AI GDD 落盘(TR 回溯追加)** | **ID 增补 21 条**(229 → **250**),新 `system:` slug **`enemy-ai`**(`TR-enemy-001…021`)。按 **ADR-016 Ordering Note** 的约定回溯追加(与 13 / 21a / 52 同法)—— 27 无 GDD 时其 TR 悬置,撰 GDD 时补登;**27 的绝大多数边界不是本 GDD 新立的,是 ADR-016 强加的**,本组职责是把 ADR-016 §一/§二/§三/§四/§五/§七/§八/§九 落到可实现口径。**状态:18 ✅ / 3 ⚠️**。**三条 ⚠️ 根因** —— ① `TR-enemy-004`(六态状态集无 ADR 背书但亦不冲突,`PR-27-5`)② `TR-enemy-005`(ADR-016 §九「只留**一个**原型」字面与 P0 威胁池含野兽张力,`O-27-4` 读作**表达能力上限**而非实体种类数,`PR-27-1`)③ **`TR-enemy-017`(🔴 唯一 P0 硬前置 = `O-27-3`「25 格斗与武器线无 GDD」,`PR-27-2` provisional)** —— 敌人伤情事件的逐 `Kind` 载荷形状归 25。**五项轮内裁定 `PR-27-1…5`**。**两处 `entities.yaml` 登记**:`SimEvent.Kind.EncounterStarted`(**写者 = 52**,`PR-27-4`)/ `EncounterEnded`(**写者 = 27**)—— 写者拆分判据 = **ADR-021 §①「状态所有权 = 写入权」**;`constants` 82 → **84**,`O-27-5` 结清。**`O-27-6`**:27 不写第二条 `ActorCellEntered`(敌人格纯派生态)。**汇总 135 → 153 ✅ / 19 → 22 ⚠️ / 75 ❌**(合计 229 → 250) | `enemy-ai.md` · `entities.yaml` · `systems-index.md` 行 27 · `tr-registry.yaml` |

| 2026-09-17 | **#37 病例系统二轮 `/design-review` 连带修订**(NEEDS REVISION · Scope **L** · 22 项阻断 · 4 项用户裁定) | **ID 增补 9 条**(250 → **259**):`TR-case-028`(**载荷内枚举 = 版本化整数 ordinal**,ADR-006 + ADR-014,✅)· `TR-case-029`(**ordinal append-only 构建门**,ADR-014,✅)· `TR-case-030`(**判词表 `case_judgment_lexicon` + 非一一对应校验**,ADR-014,✅)· `TR-case-031`(**`PATTERN_THRESHOLD` 进覆盖集但不拒载**,ADR-014,✅)· `TR-case-032`(**三案链成员判定域 + 一次性**,ADR-008,✅)· `TR-case-033`(**同一病人至多一个开案**,ADR-008,✅)· `TR-case-034`(**`quill_tick` 只读 DTO 义务**,ADR-013,✅)· `TR-case-035`(**支柱四措辞订正**,✅)· `TR-case-036`(**就诊动作定义归 8/10**,❌ `gap`)。**原地回填 4 条**(`TR-case-005` 删 `opened_tick` / `008` 补 `salted_key` / `023` 窗口方向订正 / `025` 盐改**逐 `D` 派生** —— 后三条均属「修订痕迹看似完整、逻辑实则相反」类,是二轮才查出的)。**三条系统性根因**:① **`FixSet` 类型本身是错的**(`disease_id` 是 `DIS_*` 枚举,塞进 Q16.16 算术域正是 ADR-006 **D-21-17** 修掉的同一类错误;破坏面含 `D ∈ disease_set` 退化为**定点相等比较** ⇒ 语义已错却**逐位可回放**,静默失败类)② **`PatternRecognizedPayload` 根本没有 `salted_key` 字段**(ADR-008 §一/三/五 三处宣称而 struct 没有 ⇒ AC-37-14 恒真 + 53 无法定位 `D`)③ **跨域窗口公式方向写反**(恰好使「复诊第二例凭首诊处置通过前置」成为**唯一可通过路径** —— 即紧接下一行自称要堵的 Opus 复核 #4 后门,`公式层实际未堵`)。**🔴 本轮自查出的 ADR 级冲突**(非专家提出):ADR-008 §三 / 37 写「表变更 ⇒ 旧存档**拒载**」vs **ADR-010 §七 / ADR-014 §五「`ConfigVersion` 不匹配非致命」** —— 两者不可同时成立,且**仅靠「进覆盖集」不足以守住 ordinal 重排**。**择一(不推翻 ADR-010)**:`ConfigVersion` 管「**改动可追溯**」,**append-only 构建门**管「**静默错读**」(把「麻黄汤被读成桂枝汤」换成明确构建失败),落 **`adr-014 §五 D-21-13 承接条 ③`**;既有先例 = `docs/CLAUDE.md` 的 TR Registry 纪律「Never renumber existing IDs — only append new ones」。**同轮 ADR 就地回改**:`adr-008`(三处载荷订正 + `Judgment` 形状 + `freehand_text` 例外 + §四 方向订正 + §五 盐改逐 `D`)· **`adr-006`**(**D-21-13 口径收窄两行分流表** + **Amendment A 例外登记** + Validation 两条门)· **`adr-014`**(`case_judgment_lexicon` 登记 + **D-21-13 承接条 ①②③④** + Validation 两条)· `adr-010` §七 补注(**语义一字未动**,写明「改成致命 = 另开 ADR」)。**汇总 153 → 161 ✅ / 22 ⚠️ / 75 → 76 ❌**(合计 250 → 259)。**⚠️ 本次状态变更由撰写方置入,待二轮复核** | `case-system.md` · `adr-006/008/010/014` · `tr-registry.yaml` |
| 2026-09-17 | **#25 格斗与武器线 GDD 落盘 + 首轮 `/design-review`(MAJOR REVISION NEEDED · Scope L)修订全量落盘** | **ID 增补 24 条**(259 → **283**),新 `system:` slug **`combat`**(`TR-combat-001…024`),状态 = **18 ✅ / 4 ⚠️ / 2 ❌**。按**代生约定**追加(25 无单一权威 ADR —— 它是 ADR-005/006/008/009/016/018/020/021 的**汇合点**,与 27 / 13 的「有权威 ADR 回溯追加」不同型)。**四条首轮评审用户裁定(均 [甲])**:A3 压制 = 只读查询 `IsSuppressed` · D2 敌/兽 `CP:=0` 退化式 · B2 逐 bar 组合上界断言 · 范围 = 全量落盘、**机制与数值冻结**。**两处跨系统改判**:S2 `HurtLevel` 折叠归 **9**(R17)· S3 击退**不注入**位移(结清 1 的 `OQ-1-7`,`O-3` 的 25 行撤销)。**状态翻转 2 条**:`TR-enemy-017` ⚠️ → ✅(`O-27-3` 的 25 侧兑现 —— 三元组 `InjuryOnset` / `EnemyInjuryOnset` / `InjuryStateChanged` 定名入 `entities.yaml` + ADR-009 §三,R2/R3)· `TR-skill-007` ❌ → ⚠️(25 侧 schema 兑现;仍悬的原因在 30 自身)。**R1–R18 + V1–V7 涟漪全量落盘**:9 侧 F1 战伤阶跃项 + `LethalFor` + `QueryHurtLevel` + `InjuryStateChanged` emit + 六通道 / 1 侧 `AC-1-23` 负边界 / 21a 侧 `inflicts_injury` 降级 + A20 漂移门 / 27 侧 §一① 订正 + 乐层与订阅登记 / 44 侧白名单第 5 类 / ADR-018 §六 乐层例外(G1–G3 全文)/ ADR-009 §二/§三 追加 Kind / R16 tick 频率登记(`OQ-25-8` 与 `OQ-8` 同批)/ V5-V6 已知代价登记(`production/known-costs.md`)。**两条 ❌** = `TR-combat-023`(tick 频率标定,写第一个 `Step` 前必须)+ `TR-combat-024`(`OQ-25-1` 路甲/丙 + `OQ-25-3` Down 边界,归用户)。**汇总 161 → 180 ✅ / 22 → 26 ⚠️ / 76 → 77 ❌**(合计 259 → 283)。⚠️ **本轮所有状态由撰写方置入,待 25 二轮 `/design-review`(新会话)确认** | `combat-and-weapon-lines.md` · `entities.yaml` · `adr-009` · `disease-simulation.md` · `enemy-ai.md` · `patient-ai.md` · `audio-system.md` · `adr-018` · `player-controller-and-movement.md` · `item-database.md` · `technical-preferences.md` · `adr-005` · `production/known-costs.md` · `tr-registry.yaml` |

| 2026-09-18 | **#44 音频系统 GDD 首轮 `/design-review`(MAJOR REVISION NEEDED · Scope L · 8 位专家 + creative-director)· 修订 + ADR-018 就地改判 + ADR-001 补齐** | **ID 不增不减**(仍 **283**)—— 无新 TR、无状态翻转(两条原地回填:**`TR-diag-024`** 改判 · **`TR-diag-025`** 硬化)。**用户四项裁定**:D-A **重开 2026-09-14「联机分人分混」裁决 → 各设备按本机技能档**(`SetTier(TierSource.Local)`)· D-B 优先级 tie-break 改**距离归一化** · D-C 接受无障碍 P0 非目标(ADVISORY)· D-D **现改 ADR-001**。**三条系统性根因**:① **幻想主通道(门后呼吸)无总线、无学习路径** —— 设计对「不得做什么」的规格远严于「必须做什么」,整个 verdict 即此反转;② **`AudioListener` 单实例约束被误读为总线数约束** ⇒ 得出「分人分混结构上不可能」的**伪前提**(实为空间化参考点约束,与 4 玩家各自技能档正交);③ **事件表 schema 缺 `trigger_source`** ⇒ 白名单只能捕获**具名**触发源,**基于时序**的状态播报(阈值跨越等)从 prose 白名单缝隙漏过。**GDD 侧 8 项阻断全落**:SNR 公式**量纲修复**(原式 dB 相减 = 量纲不齐)· `AudioCueDto` **删 `Tier`** 改由本地 `F-44.3` 解析 · 新增 **F-44.7 世界情境呼吸路径**(兑现幻想主通道)· `OQ-44-4` **升格为 §Event Table Schema**(附 `whitelist_category` + `trigger_source` + 反向夹具)· 规则七**全重写**(本地档)· 规则九引 `AC-13-F1` 先例(无障碍 ADVISORY)· 优先级**单一出处** + 距离归一化 · 资产族补全(把脉音 / 脚步矩阵 / 建造工具 / 敌人 / 动物 / 本机呼吸 / 天气 / Aux)。**AC 27 → 32 条** + `[A]`/`[L]` 证据类型分类。**ADR-018 就地改判**:§五 重写(伪前提证伪 + `SetTier(TierSource.Local)`)· 总线 **6 → 7** · trigger_source 字段 · §七 P1a → P1b · §一 删 `VitalsDto` 行 · Alt-4 **由驳回改采纳**。**ADR-001 补齐**(阻断 7,原文**零处提及音频**):第二 QoS 自 `IReplayPipe` 拆出为 **`IPositionalChannel`**(per-`ActorId` latest-value 表,原单槽无 key、类型未定义)+ **消费者登记**(44 音频 / 动画表现)+ **病人 / 受伤实体锚点发布者** + **cue 非复制**语义。**汇总不变:180 ✅ / 26 ⚠️ / 77 ❌**(合计 283)。⚠️ **本轮状态由撰写方置入,待用户的二轮裁定**(同 42 / 51 的「接受修订免二轮」模式) | `audio-system.md` · `adr-018` · `adr-001` · `tr-registry.yaml` |

| 2026-09-18 | **P0 收尾批六份 GDD 一次性落盘(#4 交互 · #5 时间与天气 · #10 急救动作 · #11 处方用药 · #18 炮制 · #20 库存与物品)** | **ID 增补 102 条**(283 → **385**),**六个新 `system:` slug** —— `interaction`(15)· `timeweather`(15)· `emergency`(21)· `prescription`(17)· `processing`(18)· `inventory`(16)。**✅ 至此 P0 的 31 项全部有 GDD**(`systems-index.md` §11「P0 已设计 31 / 31 … 剩 0 项」)。**六份都无单一权威 ADR**(同 25 的形态 —— 各是多 ADR 的汇合点),故按**代生约定**追加,需求文本由各 GDD 的规则 / 公式 / AC / OQ 生成。**本轮三条实质裁决**:① **#10 结清 `OQ-3-5`** —— `EmergencyReading` 增设**独立模拟量通道** `magnitude`(真缺口收窄为「仅幅度一维」;承 3 的三轮评审修正「浮点活在 3 的手感层、交出的读数是全整数」),并落定**跳过路径**(用户裁定)→ `Skip ⇒ JudgeResult = Applied`(**≠ `Missed`**)—— 无障碍入场券,不计入熟练度成长;② **#5 结清 #1 的 `OQ-1-6`** —— 天气对移动的影响 **P0 不启用**(用户裁定 2026-09-18)⇒ 1 的 F-1-1a `max()` 推导无需改动,P1a 若启用须走地貌同一条 `K_TERRAIN` 通道;③ **#20 载重呈现 = 拟物器具(药箱满溢感)**(用户裁定;机制侧仍只有一条布尔 `CanCarry`,四档是**呈现分档**非机制分档)。**#11 兑现一条跨文档义务 `D-21-11`** —— 品级 → 时间轴作用点的**求值点落在 F-11.2**(`Axis_effective = Axis_base + axis_offset_by_quality[quality − 1]`),**11 是 21a F5 的唯一调用点**;同轮新增两条跨文档硬门:polarity **双表构建期交叉校验**(AC-11-07,违反 = `throw`)与 `single_dose_max` 烘焙期派生常量。**六条 🔴 硬前置**(须在各自首轮评审前裁):`OQ-4-1`(病人四路由消歧归 8 / 10 动作词表)· `OQ-5-1`(= `OQ-8-11` / `OQ-17-1`,深水线季节修正曲线)· `OQ-10-1`(`ResultMul[Missed]` 是否 0 —— 核心机制裁决,关系支柱二的重量)· `OQ-11-1`(polarity 判定真源归 9 注册表还是 11 处方表)· `OQ-18-5`(炮制中途存档语义)· `OQ-20-1`(消耗 / 转移的 `Kind` 归属 —— 不裁则消耗无事件载体,库存不可重建破 AC-20-03)。**本批 `domain:` 分布**:98 Core · 2 Performance · 1 Presentation · 1 Networking。**汇总 180 → 241 ✅ / 26 → 38 ⚠️ / 77 → 106 ❌**(合计 283 → **385**)。⚠️ **本批全部状态由撰写方置入,待六份各自的首轮 `/design-review`(须新会话 —— 评审者独立于撰写上下文)确认** | `interaction-system.md` · `time-and-weather.md` · `emergency-procedures.md` · `prescription-and-medication.md` · `processing.md` · `inventory-and-items.md` · `systems-index.md` · `tr-registry.yaml` |

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

| 2026-09-18 | **六条 🔴 硬前置全部由用户裁定结清(收尾批复评准入轮)** | **ID 不增不减**(仍 **385**)—— 无新 TR、无条目增删。**状态翻转 6 条**:`TR-emergency-016` ❌ → **✅**(`OQ-10-1`:`ResultMul[Missed] = 0.25`,**非零**「失败也留一笔」;10 的 F-10.4 取值表 + AC-10-16)· `TR-prescription-016` ❌ → **✅**(`OQ-11-1`:**9 的病种注册表 = 唯一真源**,11 的表 = 镜像 + `AC-11-07`;本 GDD 原裁决确认)· `TR-processing-016` ❌ → **✅**(`OQ-18-5`:**发起即落流,完成 = 派生**;18 规则七/八 就地重排为「20 裁决先于 `Append`」+ AC-18-16/17)· `TR-inventory-014` ❌ → **✅**(`OQ-20-1`:**全部复用既有 Kind,零新增**;20 规则一 + AC-20-16/17;**零 ADR 涟漪**)· `TR-interaction-015` ❌ → **⚠️**(`OQ-4-1` 归属已定,但**承载方 8 的动作词表未落盘** ⇒ 4 的该段仍不可判)· `TR-timeweather-015` ❌ → **⚠️**(`OQ-5-1` **形式已定** = 逐季乘子表;`SEASON_MULT[]` 的**值** + 17 的 `Capacity` / `RegrowWindow` 仍归数值轮)。**三条跨文档涟漪**:① **17 的 F-17-3** 增设季节项与第三个参数 `SEASON_MULT[]`(回填 `OQ-17-1` 口径:由「形状与数值全未裁」收窄为「**值**未裁」)+ **AC-17-16/17**;② **5 的 F-5.2** 补「只交 `season_index`、不做乘法」的割据注 + **AC-5-16/17**;③ **18 的 `OQ-18-5` 结清暴露 `D-21-28`(Craft 事件总序键缺失)严重度上升** —— 同 tick 点火密度随「发起即落流」上升,该键归 **ADR-005 / 19 / 45**(21a 已登记归属,不由本批裁)。**汇总 241 → 245 ✅ / 38 → 40 ⚠️ / 106 → 100 ❌**(合计 385 不变)。⚠️ **本轮状态由用户裁定直接置入**;六份 GDD 的首轮 `/design-review` 仍须在**新会话**进行(评审者独立于撰写上下文) | `emergency-procedures.md` · `prescription-and-medication.md` · `inventory-and-items.md` · `processing.md` · `interaction-system.md` · `time-and-weather.md` · `foraging.md` · `tr-registry.yaml` |
| 2026-09-19 | **#5 时间与天气 首轮 `/design-review`(MAJOR REVISION NEEDED · scope L · 9 BLOCKING)→ 当日全量修订 + 四裁定 R-5-A/B/C/D + 九档涟漪(1/9/18/17/52/42/44/13/index)→ **用户裁定免二轮 ⇒ ✅ Approved(2026-09-19)** | **ID 不增不减**(仍 385)—— 本组 TR-timeweather-* 15 条数量与状态(11 covered / 2 partial / 2 gap)不变;**就地订正 2 条需求文本**:003(掷骰键 tick → **块索引**,R-5-A)· 007(「恒假」→「漏判环绕段」+ C# 百分号截断陷阱,B-1)。5 侧 **AC 15 → 21 条**(分组 A–D;BLOCKING 17 / ADVISORY 1 / EXTERNAL 4;systems-index 行 5 的旧「15 条 AC」已同步)。幻影边撤销的账落在 52 / 9 的 GDD 行内(只订账、不改机制),不生新 TR;OQ-5-8(天气→52 强度轴 P1a)为登记义务非 TR 缺口 |

| 2026-09-19 | **#18 炮制 首轮 `/design-review`(NEEDS REVISION · Scope M · 11 BLOCKING,7 条同根因「载荷三缺」)→ 当日全量修订落盘(四用户裁定均 [甲])+ 涟漪六件** | **ID 不增不减**(仍 385)—— 无新 TR。**状态翻转 5 条**:003 ❌→⚠️(newtype `SkillLevel` 前置 = 21a **D-21-33** / O-18-R7)· 004 ⚠️→✅(R-18-B `Recipe.owner` = **D-21-30** 落盘 + AC-21a-66)· 005 ❌→⚠️(`Admissible` 同一函数双路径,仍无 ADR 承接)· 007 ⚠️→✅(R-18-C 钳制归属结案 = 21a F1 正文 `EnvMod_total`,**D-21-31**;5 侧 AC-5-19 收窄)· 015 ❌→⚠️(OQ-18-3 已裁单炉/不可取消;残留 OQ-18-8 归 24)。001/006/009/011/012/016/017/018 仅同步 note(载荷三位 R-18-A、**D-21-28 结案走 ADR-009 Amendment J**、AC-18-07 并入 AC-18-01②)。18 侧 **AC 17 → 25 条**(分组 A–D;BLOCKING 20 / ADVISORY 1 / **EXTERNAL·BLOCKED-BY 4**)· **OQ 6 → 9 行**(5 结 4 悬:18-4 内容轮 · 18-7 上行通道(ADR-001 窄修订同批)· 18-8 归 24 · 18-9 归 42 屏清单)。21a 侧 **守恒律真缺陷 D-21-32** 修法落盘(逐条同形极值式 = AC-21a-65;18 的 AC-18-19 在此之前不得记绿)。**涟漪六件**:`item-database.md`(D-21-28 结案 + 30/31/32/33 + AC-65/66)· `inventory-and-items.md`(前置 2/6 认领 + 6b + `mint` 例外)· `time-and-weather.md`(AC-5-19 收窄注)· `audio-system.md`(`sfx_process_*` 发射方改判 18)· `skeuomorphic-ui.md`(O-18-R6 接缝行)· `adr-009`(**Amendment J**)+ `entities.yaml`。**汇总 245→247 ✅ / 40→41 ⚠️ / 100→97 ❌**(合计 385 不变;⚠️ 上一行的 40/100 与文件实测有 1/3 的既有漂移,本轮以 `status:` 字段计数归一)。状态 = ✅ **Approved(2026-09-19 用户裁定接受修订、免二轮 —— 显式风险接受,重开触发条件见 `design/gdd/reviews/processing-review-log.md`)** | `processing.md` · `item-database.md` · `inventory-and-items.md` · `time-and-weather.md` · `audio-system.md` · `skeuomorphic-ui.md` · `adr-009-world-state-event-boundary.md` · `entities.yaml` · `systems-index.md` · `tr-registry.yaml` |
| 2026-09-19 | **#11 处方用药 二轮 `/design-review --stage lean`(MAJOR REVISION NEEDED · Scope L · 7 BLOCKING / 11 Recommended)→ 当日全量修订落盘(四用户裁定:BL-1 开 21a 重开条件 · BL-2 请 9 立噪声带常量 · BL-3/4/5 成长门改为 11 可自判 · BL-6 只认 21a 的 EFF)+ 涟漪四件** | **ID 385 → 387**(新增 `TR-prescription-018` / `-019`)。**状态翻转 3 条**:`TR-prescription-013` ❌→⚠️(C5 重解释:「结算函数唯一」→「**载荷与流语义唯一**」)· `TR-prescription-014` ✅→⚠️(2 的相机表尚无 11 行,`OQ-11-7`/`OQ-11-9` 未裁)· `TR-prescription-017` ❌→⚠️(省料**单一出处 = 21a 的 `EFF`**;30 删 `PROVINCE_SAVE_RATE[]`,只欠「等级 → `EFF`」映射 = `O-11→30`)。**新增 2 条**:`-018` = ⚠️ partial(写者唯一性单点断言;**全局作者侧防线仍缺**,归下次 `/architecture-review`——**不得记绿**)· `-019` = ❌ gap(可感知地板量纲一致性,承接件 = 9 的噪声带常量)。**就地订正 4 条需求文本**:`-003`(防静默拒收)· `-007`(BL-7 域收窄:`drug_potency` Q16.16 ≤ 2^47 时**不**强制 128 位中间积)· `-008`(删「域内 clamp」→ 限位由戥子物理档位集合给出)· `-015`(回放输入集删「技能等级」)。**本轮新命名失效模式:「判据对合法输入类误判」**(BL-7:AC 无条件要求对合法输入类恒成立 ⇒ 把合法实现判红;与「判据空转」对偶)。**四处涟漪**:`item-database.md`(**D-21-34** + 头部 ⑤ + F5 块 = `MIN_USABLE_HALF_LIFE` 重开条件)· `disease-simulation.md`(**D-9-J** + 头部 ⑤ + tuning 块 = 噪声带具名常量)· `skill-system.md`(§3.1 + 头部 ⑤)· `systems-index.md`(row 11 + row 6 陈旧「Draft」订正)。**21a / 9 / 30 三处只加「重开条件 / 具名常量 / 签名登记」三类最小行,不动机制**。11 首次成文 **§数据契约 `11-DC`**(3 资产 + 7 校验;`DC-2`/`DC-6`/`DC-7` 待前置 ⇒ 显式登记不记绿)。状态 = ✅ **Approved(2026-09-19 用户裁定「接受二轮修订、免三次复核」= 显式风险接受结案 —— 二轮为 lean 单会话,撰写者与复核者同源;重开触发条件见 `design/gdd/reviews/prescription-and-medication-review-log.md`)** | `prescription-and-medication.md` · `item-database.md` · `disease-simulation.md` · `skill-system.md` · `systems-index.md` · `tr-registry.yaml` |
| 2026-09-19 | **#4 交互系统 三审 `/design-review --stage lean`(NEEDS REVISION · Scope S · 3 BLOCKING + 3 Recommended,全属「文件与自身不一致」族)→ 用户裁定 [A] 当日就地修订** | **ID 不增不减**(仍 **387**)—— 无新 TR、**无状态翻转**。本轮三处阻断**全部是前两轮修法自身的文书残留**(非新机制面):**BL-1 陈旧引用** —— 全篇四处仍写「42 侧**须新立** `IModalState`」,而契约已于 `adr-013:326 §十 Amendment A` 落盘 ⇒ 四处回刷为 ✅ 已落 + 逐条坐标。**BL-2 文内两个上界** —— 同一 `R_INTERACT` 上界,`F-4.1` 变量表写 `max(W,H,D) − 1`、Tuning / UX Flag 写 `min(W,H,D) − 1`;**用户裁定取 `min(W,H,D) − 1`**(Tuning 自述该上界目的 = 防「候选集 = 全世界」,只有 `min` 达成),四处对齐 + **`4-DC-1` 补上界校验**(原只查 `≥ 1`)。**BL-3 类型误述** —— 规则七伪码块把 `ModalId \| None` 写成并列第二类型,而 ADR-013 逐字声明 `None` 是 `ModalId` 的 `= 0` 成员,且复制体名漂移 ⇒ 伪码块改「引用 ADR-013,不复制成员名」(与 `AC-4-09` 同纪律)。**推荐三件**:规则八场景表补 `Utensil`/`ClinicPanel` 两行并明写该表非权威源(JSON 才是)+ `AC-4-13` 反引号格式 + `F-4.1` 两行误合并拆回。**TR 侧 = 2 条需求文本就地订正**(状态不变):`TR-interaction-002`「三源→四源」· `TR-interaction-010`「焦点单栈门→模态开集 `IModalState`」—— 两处均为前两轮 GDD 订正**未回刷 registry / 本表**的同一病灶(「修订未闭环」),本轮一并补齐。`-015` 仍 `partial` 不得记绿(承载方 8 的动作词表未落盘 = `OQ-4-1` / `D-8-12`,4 转 Approved 的全案唯一硬前置)。**三轮收敛曲线**:首轮 15 条**机制**阻断 → 二轮 3 条**记账**阻断 → 三审 3 条**文书一致性**阻断 ⇒ 4 的机制面自首轮修订后即稳定,三审未再打开任何新机制面。状态 = 🟡 **In Review**(三审修订当日落盘;仍待 8 的动作词表行)。⚠️ 三审为 lean 单会话,与二轮同会话隐含同一复核者 —— 三处阻断均为点对点 grep 可证,不依赖判断力差异 | `interaction-system.md` · `systems-index.md` · `reviews/interaction-system-review-log.md` · `tr-registry.yaml` |
| 2026-09-19 | **D-8-12 动作词表结案(用户裁定路线甲 = 模态分流)—— 8 侧 S-8.4 落盘 + 4 侧全量回刷** | 承接 4 三审收尾的 widget 裁定「[C] 承接 8 的动作词表行 D-8-12」→ 摊开甲(模态分流)/ 乙(病人粗状态分流)两条路线 → 用户裁 **[A] 取路线甲,全量落盘**。**核心裁决**:世界空间裸 `Interact` 打到病人 = **就诊,单义**;查体 / 施治的岔口不在「按下去是哪一义」,在**进模态后选哪一行** ⇒ 三义岔口取消。四行路由:就诊→37 · 查体=脉案 `ModalId.Casebook` 行级动作→8 · 施治=方笺落笔→11 · 急救=10 直读通道(不经 4)。**甲路三条理由**:医学顺序即输入顺序 / 零新裁决输入(复用 ADR-013 §十 `IModalState` 已落裁决)/ 与 ADR-009 §七 拾取「意图 + 当下判距」同构。**四条边界**:B-1 4 出境载荷逐位不变 · B-2 行级动作住模态、与 `AC-4-09` 正交无同键双触发 · B-3 路由表非数值件(D-8-3 / `RECHECK_WINDOW` 归属不变)· B-4 P1a 望闻问切只加行,**改回空间分流 = 重开触发**。**落点**:`diagnosis-system.md` 新立 **S-8.4 + AC-8-51/52**(8 的 AC 50→52,既有 AC/公式/Tuning/状态机逐位不变,头部加最小行集注)+ `D-8-12` 行 🔴→✅;`interaction-system.md` 14 处 `OQ-4-1` 结案回刷(状态头 / 规则五 banner / `AC-4-15` 只剩 ② / `4-DC-5` Patient 半边解除 / 交叉引用三行)。**TR 翻转 2 条,不新增 ID(恒 387)**:`TR-interaction-015` ⚠️→✅(既定触发条件「8 落盘后转 covered」达成)· `TR-case-036` ❌→✅(两半根因「10 无 GDD / 8 未登记」已除)。**汇总 247→248 ✅ / 44 ⚠️ 不变 / 96→95 ❌**(387 自洽;per-group:case 25→26 ✅·7→6 ❌,interaction 13→14 ✅·2→1 ⚠️)。**4 的全部外部技术前置至此消除 ⇒ 同日用户裁定转 ✅ Approved(三轮已跑,免四轮复核 = 显式风险接受;重开触发条件见 `reviews/interaction-system-review-log.md`)**;10 侧 `:744`「8 的动作词表行仍缺」同日做**最小行回刷**(指向 S-8.4,不动机制 —— 承 11 轮对已 Approved 件的最小行集先例)。三处落点:`interaction-system.md`(状态头/前置句/Last Updated)· `systems-index.md`(row 4/§6/§11)· 本表 | `diagnosis-system.md` · `interaction-system.md` · `systems-index.md` · `tr-registry.yaml` · `session-state/active.md` |
| 2026-09-20 | **四值批次裁定落盘:OQ-8 / OQ-25-8 结案 + DOTS 复评门阈值 + 病种提级预裁** | **用户裁定四项**(承本日一致性检查 R-5「横轴无量纲」与 `/review-all-gdds` 的同型根因):① **`TICK_SECONDS = 0.05`(20 Hz ⇒ `TICK_PERIOD = 50 ms`)** —— 全案 `*_ticks` 量纲有值;② **`OQ-8` 结案 = 在场才模拟 · `PATIENT_APPEARANCE_CAP = 24`**;③ **ADR-017 复评门阈值 = 同场**表现层**实体 ≥ 100 或 表现层帧时间 ≥ 8 ms(≈ 16.6 ms 的 48%)** —— ⚠️ 计数对象是表现层实体(群集 / VFX / 批处理)而非 sim 病人数 ⇒ **CAP 24 < 100 使该门在 sim 侧由构造永不触发**;④ **病种提级预裁:P1a 起上限 16-20,P0 仍 8**(承 `content/campaign-arc.md` ### 五 的 15-20 愿景向上取齐为 ≤ 20;预裁只裁方向与天花板区间,**逐轮加行仍逐轮由用户审,裁定权不随预裁移交**)。**同批一并结案**:`OQ-10-4`(急救两动作具名 = 止血包扎 / 节奏型通气动作,年代措辞归 40)· D-1(分段标题「幕」→「章」统一)· 草木灰来源(= **②a 火堆灰烬采集物**,走 17 `ResourceHarvested` + `DropSpawned`/`DropClaimed`,**零新增 Kind**(AC-17-01 ✅)/ **零转化边**(故 ADR-006 §五 守恒律不适用)/ 不触 18「无失败态」结构保证)。**ID 不增不减(恒 387)**;**状态翻转 3 条**:`TR-disease-021` ❌→✅(`CAP=24` 有值源,ADR-008 §六 硬依赖结清)· `TR-patient-006` ⚠️→✅(`AC-13-E1` 前置解除)· `TR-combat-023` ❌→✅(25 侧量纲前置解除);`TR-patient-013` 的硬依赖 note 同步(状态本即 ✅,不重复翻转);⚠️ `TR-combat-024`(值域)与 `TR-patient-*` 其余缺口**不随本批结案**。**汇总 248 → 251 ✅ / 44 → 43 ⚠️ / 95 → 93 ❌**(合计 387 不变;自洽 251+43+93=387)。**三条纪律:① 本次解除的是「量纲前置」,不是「数值」** —— 逐旋钮值仍归用户数值轮(`OQ-25-7` / `OQ-18-4` / 52 的 `ROLL_INTERVAL` 等不结案);**② `L_input` 与 `L_eval` 虽同为 50 ms 量级,不得重新合并**为「端到端 < 50 ms」(承 F-10.6 的切分口径,`AC-10-08` 只测 `L_input`);**③ 20 Hz 与 60 fps 不整除** ⇒ `Step` 须由 `ITickProvider` 驱动、不由渲染帧驱动(实现期义务,不改裁定值)。**仍无值**:`TICKS_PER_DAY` / `TICKS_PER_SEASON`(日 / 季的秒长未裁,归 5 / 52)⇒ `campaign-arc.md` 的愿景小时数**依旧不做换算**。**⚠️ 三条翻转由裁定方置入,待各自下轮 `/design-review`(新会话)确认 —— 裁定 ≠ 验收**(承本文件既有的「撰写方置入」纪律)。**涟漪文件**:GDD 侧 12 份就地回刷(`disease-simulation` · `diagnosis-system` · `time-and-weather` · `emergency-procedures` · `processing` · `combat-and-weapon-lines` · `random-events` · `enemy-ai` · `patient-ai` · `interaction-system` · `input-system` · `skill-system`)+ `content/campaign-arc.md` + `systems-index.md` §8;架构侧 `adr-005`(性能表两条注记 + CPU 行量纲)· `adr-017`(§三 表 / 代码形态 / §Risks / §Validation / §Consequences 等 20 处阈值占位)· `technical-preferences.md`(三条性能预算块 + ADR-004 / ADR-017 日志条目)· `entities.yaml`(`TICK_SECONDS=0.05` · `PATIENT_APPEARANCE_CAP=24` · `TICKS_PER_DAY` 改注「量纲已解除、值仍归 5」· `Dens_s` 输出域注)· `gdd-cross-review-2026-09-20.md`(R-5 节加结案注,**报告不删**)。 | `disease-simulation.md` · `diagnosis-system.md` · `time-and-weather.md` · `emergency-procedures.md` · `processing.md` · `combat-and-weapon-lines.md` · `random-events.md` · `enemy-ai.md` · `patient-ai.md` · `interaction-system.md` · `input-system.md` · `skill-system.md` · `campaign-arc.md` · `systems-index.md` · `adr-005` · `adr-017` · `technical-preferences.md` · `entities.yaml` · `tr-registry.yaml` · `gdd-cross-review-2026-09-20.md` · `session-state/active.md` |
