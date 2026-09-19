# 病人 AI 与行为(系统 13)评审日志

> 评审史轨道:`design/gdd/reviews/[doc-name]-review-log.md`。每条评审按时间倒序追加。

## 结案追记(2026-09-18 · 用户裁定)—— ✅ **Approved**

首轮 9 阻断 + 8 推荐**已全部落盘**,**用户裁定接受修订、**免二轮** → **13 结案(Approved)****。
**⭐ 免二轮 = 显式风险接受,不是「已核对」** —— 重开触发四条件见 §收尾,四者有任一命中即须重评。
文件头 / `systems-index.md`(row 49 + §10 已有 GDD 行)已同步为 Approved。
**残留外向义务五条** `O-13-5…O-13-9` + **待解一条** `OQ-13-5` + **新 AC 的 TR-ID 补录** —— 均**不阻断结案**
(结案 ≠ 义务豁免;义务落到对应下游系统 / 实现期门)。

## Review — 2026-09-18 — Verdict: **NEEDS REVISION**(首轮)→ 9 阻断 + 8 推荐全部落盘 → 用户裁定 Approved(免二轮)

Scope signal: **L**(若走「新增世界流 Kind 承载诊所知识」路径则为 XL;实际修订**未新增 Kind**,维持 **L**)
Specialists: `game-designer` · `systems-designer` · `ai-programmer` · `qa-lead` · `ux-designer` ·
`accessibility-specialist` · `audio-director` · `network-programmer`(八名 · 并行 haiku)+
`creative-director`(**opus 终裁 · 单发串行**)· `unity-specialist`(lean 引擎复核)
Blocking items: **9** | Recommended: **8** | Re-review: **No — first review**
Prior verdict resolved: **First review**

### 判据来源与判分口径

首轮 `/design-review`(`full`)—— **评审者独立于撰写上下文**(新会话)。
评审**未推翻任何支柱 / 边界**:13 的架构姿态(只读消费 `VitalsDto` · 决策住边界层 ·
出 `IPresentPatients` 供 37 · 与 27 共享基础设施不共享内容)被**确认合格**。
九项阻断**全部是「文档级规格漏洞」**:口径过期 · 公式与状态机不同步 · 悬空符号进入决策分支 ·
AC 不可验收 —— **零机制重裁、零新真源、零新增 Kind**。

### 9 项 BLOCKING(全部落盘)

| # | 阻断 | 根因 | 修法 |
|---|------|------|------|
| **R1** | `signs[]` 不同步 | 13 写「只拿到**两个**原始量」,而 9 的 AC-20 已于 **2026-09-16** 三轮扩为**三字段**(`position` / `trend` / 只读 `signs[]`,`disease-simulation.md:1759`)—— 两份 GDD 直接矛盾,**且矛盾静默**(无 AC 覆盖) | 规则三 重写 + **规则三-bis**(消费边界白名单表)+ §Formulas **F-13.8**(`signs[]` → 材质映射)+ **AC-13-A5**(反射白名单断言) |
| **R2** | 三处引用**已废止**的 ADR-016 §九 条款 | ADR-016 §九 于 **2026-09-17** 就地修订:原「冻结不改变判定」**被证伪**(对积分量为假)。13 的 `p.Cell` / `acc` **正是积分量**,却仍引用旧口径 | §States 三 重写为**三条现行判据表**;§Formulas F-13.4 注 · **AC-13-E2 重写**(同配置逐位一致)+ **AC-13-E4 / E5 新增**(镜像 27 的 AC-27-04/05) |
| **R3** | 状态机与 `Map()` 不同步 | 原六态表声称「进入条件全为 `position` / `trend` 的函数」,但 `Waiting` / `InTreatment` / `Terminal` 三态的进入条件**都含 DTO 里没有的量** ⇒ **三态在 F-13.1 中不可达**,而状态机与公式**都宣称自己是权威** | **分离两个正交维度**:`BehaviorState`(三值,纯 DTO)× `SessionState`(边界层赐予)+ `Terminal` 降级为**锁存位**;**`Waiting` 吸收进 `Seeking` 的空间子相**;**规则十二**(`OnExamSessionChanged`)+ **AC-13-B5 / AC-13-C5** |
| **R4** | `KnowsClinic` 无产出方 + `HomeRegion` 未定义 | 两个符号**同时悬空**,而 `KnowsClinic` 是 `Map()` 的**输入分支** ⇒ 悬空符号进了行为决策 | F-13.2 重写为**从已有事实派生**:`KnowsClinic := ∃ CLINIC POI ∈ POI_DEF(R) ∨ ∃ StructurePlaced(R)` —— 二者**都已在三源内**(6 的烘焙 POI 定义 + 23 的世界流 Kind),**无需新 Kind、无需新烘焙表** |
| **R5** | `position` 越界致 `byte` 静默溢出 | `round(1.5 × 255) = 383` 塞进 `byte` ⇒ **回绕成 127**(重病看起来像轻病);且 §Edge Cases 一 明写 13 **不夹取** `position` —— **两条规则直接打架** | 分流为**两处口径**:决策侧仍不夹取(13 不改 9 的真值)· **产物侧必须 `ClampByte`**(F-13.5)+ **AC-13-D4** |
| **R6** | `p.Cell` 无步进公式 | 全文用 `p.Cell` 作决策输入(F-13.3 格距 / F-13.4 `d²`),却**无任何规则说它怎么变** ⇒ 不可实现 | 新增 **F-13.7 逻辑格步进**(`acc` 纪律 + `Moving(p)` + 防跳格断言),**照搬 27 的 F-27-4 纪律**(独立一份,不共享代码)+ **EC-13-04** |
| **R7** | cue 契约四处漏洞 | ①「**距离档**」是误植(13 不设距离档);② `lerp(CUE_SLOWEST, CUE_FASTEST, position)` = **「把血条塞进耳朵」**(可读出连续数值,破反幻想);③ **随机性无来源**(13 禁 PRNG,规则六);④ `SymptomTier` **从未到达 44** | ① 删距离档;② 改为 **`CUE_INTERVAL[SymptomTier]` 分段常函数**(档内固定 ⇒ 数不出连续值)+ **AC-13-D6**;③ 去同步改 **`PatientId` 派生相位**(纯函数)+ **AC-13-D5**(零 PRNG);④ `MaterialTable` 两栏(SymptomTier × `signs[]`) |
| **R8** | 联机项**错误分层** | `network-programmer` 报 3 项 BLOCKING(病人位置同步 / 断线重连 / 主机迁移)。创意总监判:**发现真实,但层级错** —— 13 住**表现层**,P0 **不制作网络代码** | §Edge Cases 五 重写为 **V8 裁决**:13 **只主机运行、客户端不重算**(`VitalsDto` 是 float,ADR-012 逐位判据**只覆盖整数定点域**)· 病人位姿走 **ADR-001 第二 QoS**(与 **ADR-020 §四 玩家位移完全对称**) |
| **R9** | AC 组四项系统性缺陷 | ① 18 条里 **11 条无级别**;② 三条**不可验收**(B1 未含 `prev` / E2 引用废条款 / B2 断言「表现态位移 = 0」而 13 **不拥有**表现态位置);③ **零 ADVISORY**;④ **零无障碍 AC** | **全组重写**:18 → **29 条**(`[B]` 22 · `[A]` 7)· 新增 **F 组无障碍** · 逐条重判级别 |

### 8 项 Recommended(全部落盘)

| # | 项 | 修法 |
|---|---|---|
| 1 | **滞回缺失** —— `position` 在 `SEEK_MIN` 附近漂移 ⇒ 病人**逐 tick 翻转**(门口反复转身),**且静默**(不比轨迹就全绿) | F-13.1 的 `Map` 补 `prev` 参数 + **`HYST` 旋钮**;§Tuning 一 补行 + 硬约束;EC-13-01 |
| 2 | `trend` 与 9 的 `trend_backward` 对账 | 13 的分档是**表现**不是状态转移 ⇒ 用 `trend` 合法;登记 **`O-13-8`**(与 9 联合对账) |
| 3 | `Tier` 命名歧义(13 的「距离档」 vs 44 的「听诊精度档」) | §Tuning 三 明写 `Tier` 归 44、13 不设此旋钮(原「画掉的空行」已删) |
| 4 | §Interactions 悬空「规则十六」 | 改为正确引用(规则一 / 规则六) |
| 5 | §Interactions 上游表未含 6 / 23 / 52(上游从 1 个变 5 个) | 上游表补 6 · 23 · 52 · 8/10 四行;§Dependencies 一同步 |
| 6 | 无障碍只有 `OQ` 无 AC | **裁定 甲**:具名 **AC-13-F1/F2/F3** + **显式非目标**(P0 不支持视障玩家独立定位不移动的重症病人) |
| 7 | 「TR 零登记」是**过期信息** | §Dependencies 五 重写(TR 早已回溯追加 `TR-patient-001…024`);改为登记**本轮新增 AC 的 TR-ID 补录** |
| 8 | 场外死亡「零前兆」 | **裁定 V2**:必须留痕(可感知、不必可预知)· 归 **52/37** · 13 只发需求;新增 **§Edge Cases 七** + **`OQ-13-5`**(留痕具体形态) |

### 专家分歧(已向用户呈现,由用户裁定)

| 分歧 | 双方 | 裁定 |
|------|------|------|
| **无障碍是否 P0 阻断** | `accessibility-specialist` 判 **P0 阻断**(13 的信息高度依赖视听);`creative-director` **部分不同意**(真实缺口,但 13 天生双通道) | **用户裁 甲**:具名 AC + **显式非目标**(先 ADVISORY) |
| **联机三 B 是否阻断** | `network-programmer` 判 **3 项 BLOCKING**;`creative-director` 判**发现真实但层级错**(13 住表现层,P0 不制作网络代码) | **用户裁 V8**:13 **不可跨机重算,主机唯一** |
| **`RECOVER_MAX < COLLAPSE_MIN` 联合约束缺失** | `systems-designer` 报为独立缺口;`creative-director` **驳回** —— §Tuning 一 的 `RECOVER_MAX < MILD_MIN < SEEK_MIN < COLLAPSE_MIN` 传递闭包已排除所给反例 | 采纳 CD 判定;**仅补「烘焙期断言」的落点** |
| **F-13.5 `lerp` 参数序** | 主评审一度疑其反向;自查 `§Tuning 三` 的 `0 < CUE_FASTEST < CUE_SLOWEST` 后**主动撤回**;CD 独立确认并驳回为误报 | 误报(但该式本身因**反幻想**被 R7 重写) |

### 收尾(2026-09-18)—— ✅ **Approved**(免二轮)

用户裁定 **[A] 现在修订** → 9 阻断 + 8 推荐**全部落盘** → 用户裁定 **[B] 接受修订、标记 Approved、免二轮**。
**免二轮 = 显式风险接受,不是「已核对」**。重开触发条件:
① `OQ-13-5`(场外死亡留痕形态)落地后被实现证伪;
② F-13.1 滞回 / F-13.7 步进 在实现期被证伪(如 `AC-13-B1` 真值表不吻合);
③ **`OQ-8`(9 的在场范围)标定**后与规则四冲突;
④ 8 / 10 的 `OnExamSessionChanged` 发出侧实现与规则十二不符。

**新增外向义务(五条)**:`O-13-5`(52 · `spawn_anchor` 病人路径)· `O-13-6`(ADR-016 · §九 补 13 行)·
`O-13-7`(8 / 10 · 会诊接口发出侧)· `O-13-8`(9 · `trend` 对账)· `O-13-9`(无障碍文档 · 引用 AC-13-F3)。
**新增待解 1 条**:`OQ-13-5`。
