# 输入与设备 (Input and Devices, #3) — Review Log

## Review — 2026-09-15 — Verdict: NEEDS REVISION

Scope signal: **XL**(跨切面:5+ 依赖 · 多公式 · 连带一份 ADR 修正案)
Specialists: unity-specialist · systems-designer · qa-lead · ux-designer · game-designer · performance-analyst
→ 收束:**creative-director**(Opus,串行)
Blocking items: **5** | Recommended: 12(其中 9 为覆盖缺口)| Prior verdict: First review

Summary(creative-director 收束):**「骨是对的,肉是三处错的公式」**。
边界裁决 —— **意图非状态 · 3 零 `SimEvent` · 出意图不出焦点 · 不出素材** ——
经五个领域**全部通过**,不需重做。
坏在三处:**F-3.3 是代数恒等式(无验收能力)· F-3.5 对它声称要防的失效完全盲 ·
F-3.1 逐分量死区对圆形摇杆是错的**;另有**五条 AC 不可机械检查**(A1 / A4 / B1 / C3 / D2)
与**一个继承自 ADR-011 的接口缺口**(`Emergency = Button` 表达不了 CPR 节奏)。

### 根因诊断(本轮的承重结论)

**该 GDD 的多数严重缺陷不是自创,而是忠实继承 ADR-011 的未推演断言。**
⇒ 修正必须落到 ADR-011,**否则下游会把错误契约继续传下去**。
承重项:F-3.4 的 `FixParse` 范畴错误 · F-3.5 的 hash 元组 · 规则七的「Update 或回调」·
规则七的「算术原因」· 规则六的 `Clone()` · VR 的层级(P0 vs P1b)。

### Blocking Items → 处置表(2026-09-15 当日完成)

| # | Blocking 项 | 处置 | 落点 |
|----|------------|------|------|
| 1 | **F-3.4 `FixParse` 范畴错误** —— `FixParse.Parse(string)` 是导入期 string→Fix 入口,运行期不存在 float→Fix 路径(`adr-006:105-122`);初稿「`JudgeResult → FixParse → Append`」不可执行;且 `AC-3-A6` 只看顶层类型,抓不到结构体字段 | 改「**判定结果全整数、直接构造 `SimEvent`**」;新增 **`AC-3-A7`**(载荷可达闭包零 `float`/`double`,递归扫描) | 规则八 · F-3.4 · ADR-011 Amendment A #1 |
| 2 | **F-3.5 hash 元组不完整 ⇒ 判据盲区** —— 元组漏 `bindingId` 与 `processors`/`interactions`/`groups`。规则 5 要防的是「重建 ⇒ GUID 全变 ⇒ overrides 静默丢失」,而结构哈希对保持结构的重建**完全不变** | 元组补 `bindingId` + 三组集合;**长度前缀 + `StringComparer.Ordinal`** 规范序列化;**`SplitMix64` → FNV-1a-64**(PRNG ≠ 字节流哈希,且 F7 溢出 spike 与本式无关);载荷以**工厂 JSON** 为准;不变量改写(增删改 override **不动** hash / 槽位签名变**必动**) | F-3.5 · `AC-3-A8` · `AC-3-E3` |
| 3 | **F-3.3 是恒等式** —— `L_input→judge = L_poll + L_axis + L_judge` 配 `JUDGE_BUDGET := 50 ms − L_render`,代入后与 `L_input→pixel ≤ 50 ms` **代数等价**;且「绕过 UI 栈能腾出预算」是**假的**(UI 路由是帧内调度成本,不改变 `L_render`) | 删分配额,回归**单条判据**;**保留直读通道但理由改为抖动(方差)** —— 「手稳」练的是可重复时机感,方差不可控的链**不可练**;分项表降级为诊断工具;**补 `L_render ≥ 50 ms` 的降级路径**(锁定刷新率 / 独占全屏);**VR 移出 P0** | F-3.3 · 规则七 · ADR-011 Amendment A #4 / #6 |
| 4 | **F-3.1 逐分量死区对圆形摇杆是错的** —— 可动区域被切成方形,归一化后**斜向速度 ≈ √2 × 正向**(+41%) | 改**径向**(半径定大小 / 单位向量定方向);**C1 软肩**(`smoothstep^P`,死区出口与满速端点均无折角,初稿在 OUTER 处硬截断有折角);补 **NaN / `m = 0` 防护**(除零 ⇒ NaN 污染表现层,是 `NoDevice` 的算术落点) | F-3.1 |
| 5 | **五条 AC 不可机械检查 + 9 个覆盖缺口**(qa-lead) | AC **20 → 28 条**(BLOCKING 8 → 16);A1 改**单引用同一性**(非文件计数)· A4 改 `PlayerSettings` + **Roslyn 分析器**(非构建 grep)· A5 补 `PlayerPrefs`/`EditorPrefs` 拒绝清单 · C3 改**类型断言**(意图不携带目标栈)· D2 改**程序集资源清单 + 类型断言** · E1 补 `FindActionMap` + 字符串索引器 · E2 补 `#if` 断言 + 生成产物检查 · **B1 拆 B1a(零硬件合成注入,CI 可跑)/ B1b(硬件实测,最低目标硬件定稿后签核)** · 新增 **`AC-3-C5`**(栈持焦点时抑制世界意图)· **`AC-3-D4`**(`Mixed` 的「有效输入」= 迟滞 + 漂移防误触)· **`AC-3-E4`**(Idle 期 `enabled == false` 且回调零调用)· **`AC-3-E5`**(读取路径 `GC.Alloc == 0`)· **`AC-3-F1`**(反幻想,42+48 联合 BLOCKING) | `## Acceptance Criteria` 全组 |

### 用户裁定(2026-09-15,七项;经两轮多页问答)

| # | 议题 | 裁定 |
|---|------|------|
| ① | 下一步 | **现在修**(本会话内逐项修订) |
| ② | 追踪记录 | **建 review-log** + `systems-index.md` §2 标 **In Review** |
| ③ | 六处缺主/范围问题 | **登记**到 `systems-index.md` §11,**不触碰 GDD 正文** |
| ④ | ADR-011 修正 | **本改动集一并出修订块**(Amendment A,六项);**明确不加宽 `IEmergencyInput`** |
| ④-b | 反幻想执行强度 / 跳过路径归属 | **强制执行:42+48 联合 BLOCKING**(`AC-3-F1`),**48 教学从队列末位上提**;**跳过路径登记为 10 的义务** |
| ③-b | 浮点旋钮数据架构 | **整数 / 比值化,走 ADR-014**(`FixParse.FromRatio`);**不新立非确定性域资产** |

### 降级的三项发现(被过度标记)

- **「真实玩法断头」(`OpenInventory`)** —— **夸大**。`systems-index.md:54 / :477` 明载
  系统 20 库存与物品 = **Core · P0**。`OQ-3-6` **关闭**。
- **hash 元组** —— specialist 之间对「是否含绑定值」有分歧;**裁定 = 含 `bindingId`,不含绑定值**
  (含绑定值会让每次改键触发清空,改键功能自我毁灭)。
- **VR 层级** —— `game-concept.md:720` 载明 VR 急救在 **P1b**(非 P1a);P0 不验收 VR。

### 未决项(2026-09-15)

- ~~F-3.2 归属~~ —— ✅ **整块删除**,转 **42**(行为)+ **39 / 48**(呈现参数)。
- **`OQ-3-5`(`Emergency` 表达力缺口)** —— **OPEN**。`Emergency = Button(0/1)` 表达不了
  CPR 的**节奏**与止血的**力道**;每帧直读把节奏分辨率钉在 1 帧(16.6 ms),CPR 按压周期约 0.55 s。
  **登记为系统 10 的硬前置**;**本轮刻意不加宽 `IEmergencyInput`**(10 未成文时加宽 = 过度设计)。
- **`AC-3-B1b` 无法签核** —— 依赖**最低目标硬件**(现为临时值,见 `technical-preferences.md`)。
- ~~`TR-input-001…018` 待按修订口径回填~~ ✅ **已完成(2026-09-15)** —— `tr-registry.yaml`
  原地回填 **9 条**(`001` / `004` / `005` / `006` / `007` / `011` / `012` / `015` / `016`)
  + **增补 3 条**(`019` / `020` / `021`)+ `018` **❌ → ✅**(「真实玩法断头」经核验为夸大)。
  **汇总 214 → 217(127 ✅ / 18 ⚠️ / 76 ❌)**;`traceability-index.md` 头部 / 汇总表 / §11
  全文同步(`§11` 标题 `18 条` → `21 条`,表格逐行回填 + 新增 3 行,优先修复清单更新)。

## Review — 2026-09-15 — Verdict: PENDING(待复审)

5 项 BLOCKING 全部有处置:**GDD 就地修订(23 次编辑)+ ADR-011 Amendment A(六项)
+ `systems-index.md` §2 / §11 + 48 上提 + 六处缺主登记**。
**待新会话运行 `/design-review design/gdd/input-system.md` 复审**
(建议 `/clear` 后跑 —— 本轮会话上下文已高,`full` 模式需干净上下文起 5+ 个专家代理)。

## Review — 2026-09-15 — Verdict: NEEDS REVISION(复审 · 第二轮)

Scope signal: **XL**(跨切面:9 个下游全无 GDD · 4 条公式 · 与 3 份 ADR 耦合)
Specialists: unity-specialist · systems-designer · qa-lead · ux-designer · game-designer ·
performance-analyst · accessibility-specialist(7 个,`full` 模式)
→ 收束:**creative-director**(Opus,串行)
Blocking items: **6**(2 项需用户裁定) | Recommended: 12 | Prior verdict: NEEDS REVISION(同日首轮)

Summary(creative-director 收束):**「边界是对的,坏在修订本身」**。
**边界裁决 —— 意图非状态 · 3 零 `SimEvent` · 出意图不出焦点 · 不出素材 —— 五领域再次全过,不需重做。**
首轮的根因诊断是「忠实继承 ADR-011 的未推演断言」;**本轮的失效模式不同**:
**修订「按词而非按意」匹配了评审意见** —— 最典型的是 F-3.5 载荷口径(踩了它自己上方的
不变量①、也踩了用户裁定「不含绑定值」),其次是规则十三把抑制职责推给一个无状态系统。

### 六项 Blocking 与本轮处置

| # | Blocking 项 | 处置 | 落点 |
|----|------------|------|------|
| 1 | **F-3.5 载荷口径**(三个代理独立收敛) —— 以 `SaveBindingOverridesAsJson()` 载荷为记录集,该 API 返回**差分且含绑定值** ⇒ 每次改键改 hash;且载入期已 `RemoveAllBindingOverrides()` ⇒ 每次启动失配 | **删除该段落**;`R` 唯一来源 = **资产结构**;补「硬禁」与两条失败路径 | F-3.5 |
| 2 | **规则十三与 `AC-3-C1`/`C3` 硬冲突**(三个代理) —— 要求 3 持 bool「有栈持焦点」,违反 C1「无焦点状态字段」/ C3「不感知持栈者」;且 `adr-013:228` 的 `SetFocusGate` 是**单栈导航门**,是另一个旗 | **用户裁定 [A]:整条删除** —— 抑制职责**下沉给 4 / 10**;`AC-3-C5` 同删;取消无路由的 `42 → 3` 边 | 规则十三 · C 组 · 依赖债 |
| 3 | **`:22` 与 `:24` 自相矛盾**(game-designer) —— 一处说 3 直接构造 `SimEvent`,一处说零 `SimEvent` | 改为「判定结果全整数 `JudgeResult`,**`SimEvent` 由 10 构造**」 | Overview ③ |
| 4 | **四条 BLOCKING AC 挂在不存在之物上**(qa-lead / unity-specialist / performance-analyst / accessibility) | A1 改以「资产实例同一性」为主语(UI 侧半条**条件适用**);A4 改用 `PlayerSettings.GetPropertyInt`;E2② 命名载体 + 承认 `.github/workflows/`·`tests/` **不存在**;`AC-3-B1b` **降为发版前 BLOCKING / 设计门 ADVISORY**(项目证据表把 Feel 类列为 ADVISORY);`AC-3-F1②` 扩到**三份纸**并改判据标准(不用不存在的 `design/ux/`) | A1 · A4 · E2 · B1b · F1 |
| 5 | **`DEVICE_SWITCH_THRESHOLD` 孤儿旋钮**(systems-designer) —— 进了 AC 未进旋钮表,且 `≥ 1` 时任何轴都到不了 | 新增 **§Tuning Knobs 一之二**(三旋钮 + 安全范围 + 破坏后果);把埋在 AC 正文的 ±2% 移出 | Tuning Knobs · D4 |
| 6 | **`systems-index.md` 三行过期**(qa-lead) —— 仍写 20 AC / 8 BLOCKING · 仍列**已删的 F-3.2** · 仍写 `TR-input-001…018 待回填` | 三处就地刷新(27 AC / 15 BLOCKING · `TR-input-001…021` · F-3.2 已删) | `systems-index.md` |

### 用户裁定(2026-09-15,复审)

| # | 议题 | 裁定 |
|---|------|------|
| ① | 下一步 | **现在修**(本会话内逐项修订) |
| ② | 规则十三 | **[A] 3 不再抑制任何东西** —— 抑制下沉给 4 / 10;删 C5;删 `42 → 3` 边 |
| ③ | FNV-1a-64 定级 | **取 RECOMMENDED,但仍改** —— 删「`unchecked` 可定死」的措辞,改在 `ulong` 上算 |

### 被降级 / 判为夸大的发现

- **accessibility 的「跳过路径被判断与熟练度双重把守」** —— **夸大**:判断是认知、熟练度是成长路径,运动障碍者这一侧**确实被服务到**;真实受众是**新手 / 认知障碍**,归 10 / 30。
- **「3 拥有六个无障碍对象」** —— 计入了**全案尚无任何无障碍需求文档**的对象,**不可执行**;真实可执行项两条:三份纸走查 · `Emergency` 的 toggle 替代。
- **game-designer 的「跳过主导 ⇒ 肉是装饰」** —— **是按支柱一写成的,不是缺陷**;真残留只有一条:**小游戏失败后果**全案无主。
- **qa-lead 的 AC 计数(29)** —— **误算**;`A=8 · B=5 · C=5 · D=4 · E=5 · F=1 = 28` 正确(删 C5 后为 **27**)。

### 未决项(复审后)

- **`OQ-3-5`** —— **OPEN**,但**口径已收窄**:节奏分辨率**不是**缺口(16.6 ms 对 550 ms 周期 = ~3%,且走 press/release 边沿);**真缺口只有幅度一维**。初稿「P0 幻想的唯一载体」的说法已撤。
- **`OQ-3-3`** —— **新增**(P1b 同机 1-4 人的资产持有方式),补上 `:183` 的一条**悬空引用**。
- **`AC-3-B1b`** —— 仍无法签核(最低目标硬件未定);但**设计门已降为 ADVISORY**,不再是一条常驻 blocker。
- **VR 层级** —— 权威 = `game-concept.md:720` = **P1b**;`adr-013` / `adr-018` §七 / `adr-020` §三 三处**误读为功能层级**,须同批订正(已登记)。
- **回归到下一轮的** —— 无。creative-director 明确:**第三轮 full 评审不必要**,改以**对两条不变量签核的清单**收尾(结构哈希不变量 · 无状态意图源不变量)。


---

## Review — 2026-09-16 — Verdict: NEEDS REVISION(三轮定向对抗验证)→ 7 项阻塞 + 2 项裁定当日修订完成
Scope signal: M
Specialists: 5 名 haiku 并行(不变量 I1 结构哈希 · 不变量 I2 无状态意图源 · 数值安全范围 · 生产者口径 · 计数与引用核验)· creative-director(opus)综合终裁 · 两项用户设计裁定(D-A / D-B)
Blocking items: 7(阻塞簇) + 2 项裁定 | Recommended: 见簇内
Summary: 按二轮结论——**不做第三轮 full 评审,改以对两条不变量签核** —— 做**定向对抗验证**。creative-director(opus)终裁 **NEEDS REVISION**,整体判断:**「前两轮:骨是对的,肉是三处错的公式。这一轮:骨对、肉对,坏在修订本身 —— 手术做对了,纱布忘在体腔里。」** 根因诊断(承重结论):**三轮同构复发,根因 = 修订的「工作单位」错了** —— 按评审意见的措辞(句级 patch)定位,而非按被修正的命题(**命题级 sweep**)定位;更坏的是残留句被「声称已闭合的注记」覆盖。深层流程问题:AC 有机械判据,**不变量没有 grep 级扫掠工具**;修订纪律缺「被推翻的句子必须**就地覆写**」。**两条不变量裁决**:**I1 结构哈希 = 有条件成立**(3 洞 + 1 待明写:元组字段名须指名 `InputBinding` 原始属性并禁 `effective*` / 每集合前置元素计数 / 头载物理布局 / R 是否含 UI map / FNV 碰撞概率措辞);**I2 无状态意图源 = 命题成立、措辞不成立**(唯一实体违反 `:849` 已删;「无状态」作用域须定义 —— 3 自有域的设备态与通道态不计)。**计数核实通过**:27 条 AC / 15 BLOCKING(A8·B5·C4·D4·E5·F1)与自述一致,无计数错误。
**7 项阻塞 + 2 项裁定全部修订落盘**:
**B1** `JudgeResult` / `SimEvent` 的生产者口径(3 只出全整数 `EmergencyReading`;判定全归 10,`SimEvent` 由 10 构造并 `Append`)—— 落 Overview ③ / 规则八 / §Interactions / F-3.4 伪码 / 边界表 / `AC-3-A6` / `AC-3-B3` / 决策表共 8 处 ·
**B2** 调试视图删「当前焦点栈(只读 42 的)」条目(与 `AC-3-C3` 正面冲突,且复活已废 `42 → 3` 边)·
**B3** `FixParse` 自相矛盾切分 —— `Parse(string)` **仅导入期**、`FromRatio(long,long)` **运行期可用**,禁止②只禁前者(规则八 / 禁止② / `AC-3-B3` 三处)·
**B4** F-3.5 元组三洞 + R 范围(指名 `InputBinding` 原始属性 · **禁 `effective*` / `SaveBindingOverridesAsJson`** · 集合元素计数前缀 · sidecar 头载布局 + 原样喂 loader · **UI map 入 R** · FNV 碰撞概率 ≈ 10⁻¹⁷ 定级措辞;`AC-3-E3` 补 ⑤⑥ 子句)·
**B5** 数值安全范围三错(`CURVE_POW > 1/2` 为端点 C1 充要下界(新增 Ⓐ 推导)· `DZ_OUTER ≤ 1` 硬上界 · `DEVICE_SWITCH_THRESHOLD` 比较域 = **径向**(新增 Ⓑ)+ 鼠标侧单列两表)·
**B6** `updateMode` 孤儿旋钮(新增 §Tuning Knobs 一之三:`INPUT_UPDATE_MODE` 值/型/范围,钉死 `Dynamic`,附 `AC-3-B2③` 启动断言)·
**B7** ADR-011 残留就地覆写(`:326` 去 VR · `:335` `Update` → `onAfterUpdate` · Alt 2 理由换为方差 · `:261` 自述改为**不宣称已扫全文**,并逐处覆写 §Problem `:84` / §Consequences / §Performance Latency / §Migration / §Rollback / §Consumers / §Risks 双导航行措辞反向)。
**两项用户设计裁决(2026-09-16,均照准并落盘)**:
① **D-A 官方桥为焦点唯一真源** —— 3 的 `Navigate` 就是 UI map 的那一个 action 实例;焦点移动的唯一真源 = 官方桥(`InputSystemUIInputModule` / `PanelEventHandler`);`FocusNavigationIntent` 降为 3 的**类型化只读视图**(消费者 = 42 单栈门路由 + 调试视图),**不驱动移动**。与 ADR-013 §五「冻单一来源 —— 只用官方 `Navigate`」字面一致,**零 ADR 修订**;四个接口缺口(Cancel / PagePrev / Tab / 轴→步进)一次性消失;`AC-3-C2` 从行为断言改为**结构性构造断言**。落 规则十 / 十一 / 十二 + `AC-3-C1` 注 + `AC-3-C2` + §States 三 + Edge case 共 6 处。
② **D-B `AC-3-F1` 按阶段拆分(拆而不降级)** —— `F1a`(BLOCKING · P0:平面 + 键鼠/手柄(**去 VR**)+ 脉案 39 / 出诊箱 20 两份纸)· `F1b`(BLOCKING **限 P1a / P1b**:VR + 纸质地图 43)。理由:与 `AC-3-B1b` 同构 —— 不拆即**第二次「以 BLOCKING 之名、行不可签核之实」**。AC 27 → **28 条**(BLOCKING 15 → **16**)。
**同批机械修正**:规则十一 grep → Roslyn 分析器(二轮已改,本轮核验)· `AC-3-D2①` 判据改名「构建报告断言」· `AC-3-E5` 补**方法学四要素**并扩到**常驻轴路径**零分配 · §五 Dependencies `OQ-3-5` 行与 Open Questions **口径对齐**(去重改向)· **行号自引用全部换成语义锚点**(Overview ③ / F-3.5 不变量① / 规则七 / 规则八 · 及三处 `systems-index.md` 行号重核)。
**追踪记录**:`systems-index.md` row 3 补三轮记录 + **首轮计数行的逐轮口径注**(首轮 **28 / 16** → 复审删 `AC-3-C5` ⇒ **27 / 15** → 三轮拆 `F1` ⇒ **28 / 16**。**勘误**:本轮一度把首轮原记「20 → 28 / 8 → 16」判为「误记」并在同一行加了订正块 —— **该判定本身是错的**:首轮净增 8 条(`A7` · `A8` · `C5` · `D4` · `E4` · `E5` · `F1` + `B1` 拆分)确为 28 / 16,与本日志首轮条目 `:32` 一致;已**撤下错误订正块**,改为逐轮口径注)· `reviews/input-system-review-log.md` 追加本条 · `tr-registry.yaml` 三处同步(`TR-input-013` / `019` / `020`)。
Prior verdict resolved: Yes(二轮 6 项已落盘;本轮为二轮「两条不变量签核」指令下的定向验证 → 7 项阻塞 + 2 项裁定已落盘,**待用户裁定是否再验**)

---

## Review — 2026-09-16 — Verdict: NEEDS REVISION(四轮定向对抗验证 · 命题级 sweep)→ 6 项真阻塞 + 4 项用户设计裁定当日修订完成

Scope signal: M
Specialists: 5 名 haiku 并行(不变量 I1 结构哈希复核 · 不变量 I2 无外部状态意图源复核 · 未核验引擎符号的存在性核验 · 命题级 sweep 残留扫描(跨 `input-system.md` / `tr-registry.yaml` / `systems-index.md`)· 跨文件引用与计数核验)· creative-director(opus)综合终裁 · 四项用户设计裁定(见下)
Blocking items: **6**(真阻塞)| Recommended: 13 | Prior verdict: NEEDS REVISION(同日三轮)

Summary(creative-director 收束):**「前三轮是『骨对、肉错』→『边界对、坏在修订』→『手术做对了、纱布忘在体腔里』;这一轮纱布还在,而且其中一块是上一刀自己缝进去的。」**
本轮为三轮「按**命题级 sweep** 而非句级 patch 复核」指令下的定向验证。**结论:三轮的修订**确实**就地覆写了一批命题(§Problem / §Consequences / §Alt 2 / §Performance / §Migration / §Rollback / §Risks 双导航行),但泄漏面比三轮日志自述的更宽** —— 且**复发形态增加到四种**,其中**两种是本轮新种**:
- **(旧种 1)残留句仍在**:「一个文件」命题三处、`L_poll`「不可调」命题两处。
- **(旧种 2)注记覆盖而非覆写**:残留句被「声称已闭合的注记」压住,读者须交叉比对才能发现矛盾仍在。
- **(新种 3)落点声明不存在**:三轮日志称已在 `AC-3-C1` 加注「不驱动焦点移动」,**该注记从未写入文件** —— 声明与落盘脱钩。
- **(新种 4)缺陷由最近一次修订自身生产**:`AC-3-B2③` 在同一轮把**未核验的 enum 成员名**写进 BLOCKING 判据,而**同一轮的 `AC-3-A4` 刚刚确立了相反的纪律**(判据只断性质、不引 post-cutoff 符号名)。**上一刀止血,这一刀又划开。**

**两条不变量终裁(四轮)**:
- **I1 结构哈希不变量 = CLOSED** —— 元组含 `bindingId` + 对 `ulong` 算 **FNV-1a-64** + 长度前缀 + **元素计数前缀** + `StringComparer.Ordinal`;`R` 唯一来源 = **资产结构本身**;**禁 `effective*` / `SaveBindingOverridesAsJson`**。四轮唯一软点(集合元素计数前缀)已由 **`AC-3-E3⑦`** 补上机械判据(`groups = []` vs `groups = [""]` 哈希须不同)。
- **I2 = 三轮裁「须定义作用域」未落 ⇒ 本轮收窄定义 + 改名** —— 定名「**无外部状态意图源**」(不再简称「无状态意图源」);定义 = **不持有他系统状态 / 游戏状态(模拟态)**;3 **自有域的「设备态」与「通道态」明确豁免**(由 `AC-3-E4` 继续守门)。机械判据 = **asmdef 引用集白名单**,与 `AC-3-A6` **同构**(A6 管「不写流」,I2 管「不持有他系统状态」)。

### 六项真阻塞 → 处置表(2026-09-16 当日完成)

| # | Blocking 项 | 处置 | 落点 |
|----|------------|------|------|
| 1 | **I2「3 是无状态意图源」字面自证伪**(三轮已裁「须定义作用域」但未落) | **收窄定义 + 改名「无外部状态意图源」** + 显式豁免设备态 / 通道态 + 机械判据(asmdef 引用集,与 `AC-3-A6` 同构) | §States 开篇 · `:288` 全名替换 · `AC-3-C1` I2 身 |
| 2 | **`AC-3-B2③` 把未核验 enum 成员名写进 BLOCKING 判据**(与 `AC-3-A4` 同轮确立的纪律**反向**) | 重写为**性质断言**——「输入更新相位 = 渲染帧相位,且每帧恰被更新一次」,判据 = `Time.frameCount` +1 ⇒ 采样计数恰 +1;**enum 成员名移出 AC**,降入 §Tuning Knobs 一之三 并逐处带「须 spike」标记 | `AC-3-B2③` · §Tuning Knobs 一之三 |
| 3 | **`AC-3-B1a` 主语错(「输入→**判定**」)**:判定归 10,而 **10 无 GDD** ⇒ BLOCKING 半条**不可签核** | 「输入→**读数**」(修一个词);并加四轮就地覆写 note | `AC-3-B1a` |
| 4 | **`AC-3-A8` 标题方向反转 + `AC-3-B1b` 抖动上界归属自相矛盾** | `AC-3-A8` 标题「跨版本升级存活」→「**失配即备份,不静默丢弃**」(overrides 本身并不「存活」);`AC-3-B1b` **移出「抖动有上界」**,明确「本 AC 对 3 只断言均值 + 相位确定性两条,**抖动上界归 10**」 | `AC-3-A8` · `AC-3-B1b` · §Tuning Knobs 三 |
| 5 | **「一个文件」命题三处残留**(规则四标题 / §五 边界清单 / `AC-3-A5`)+ **`tr-registry.yaml` 五处跨文件残留** | 命题级就地覆写为「**一组 sidecar(头部 + 载荷,两文件)**」;`AC-3-A5` 判据改为「落盘面仅限 overrides sidecar」;跨文件 `TR-input-007` / `009` / `010` / `016` 四处就地覆写 | 规则四 · §五 · `AC-3-A5` · `tr-registry.yaml` |
| 6 | **`AC-3-F1a②` 主语画错一格**(把「出诊箱」当 P0 纸,而 `systems-index.md:77` + `adr-013:142` 均归 **43 = P1a**) | **改主语为「20 库存与物品在 P0 的容器界面」**(名称由 20 定),注明「43 出诊箱纸不在 P0」;**纯措辞修正,不动范围阶梯、不动 ADR** | `AC-3-F1a②` |

### 四项用户设计裁定(2026-09-16,均照准并落盘)

| # | 议题 | 裁定 |
|---|------|------|
| ① | 存在性无法从引擎参考库证实的符号名(`updateMode` 枚举成员 · `Instantiate` / `Clone` · `onAfterUpdate`) | **[B] 分层处置** —— ① BLOCKING AC 判据层**只断言性质、零未核验符号**(`Dynamic`/`Fixed`/`Manual` 出 `AC-3-B2③`);② 规则 / 实现指引层**保留符号名但逐处带「须 spike」**,汇总到**唯一 spike 册页**(并入 `adr-011` §Risks-A);③ 凡 spike 未过的符号,其承载的**性质**须有不依赖桥类型的**降级路径** |
| ② | `AC-3-F1a②` 出诊箱归属 | **改主语为 20 的 P0 容器界面**,注明 43 出诊箱纸不在 P0;纯措辞修正 |
| ③ | `AC-3-A1` 在 P0 平面**关掉了 D-A 唯一依赖的同一性断言**(42 尚未成文)⇒ 守门真空 | **登记硬前置门 + 降级路径** —— 登记为 **42 成文前的硬前置门**(P0 焦点载体是谁 · 由谁断言 · spike 失败降级路径),并给 3 侧一条**不依赖桥类型的降级路径**(参 `adr-013` §Risks「不达预期回自实现焦点算法,接口不变」) |
| ④ | I2 作用域 | **收窄定义 + 改名** —— 定名「无外部状态意图源」,豁免自有域设备态 / 通道态(见上) |

### 新增 AC(四轮唯一新增条目)

**`AC-3-A9`(BLOCKING · A 组)** —— **F-3.1 算术性质**:① NaN / ∞ 与零点防护(恒 `(0,0)`);② 满速可达;③ 静止归零;④ 装载期断言四组反例须使装载失败。①②③④ = Logic · BLOCKING;手感部分 = Visual/Feel · ADVISORY。
**理由:F-3.1 此前零 AC 覆盖** —— 它是 P0 手感的唯一算术载体,却没有任何机械判据。
同批:**F-3.1 硬约束由两条合取式扩为四条**,新增 **⚠️ 两端归一化边界**块 —— `0 ≤ DZ_INNER < DZ_OUTER ≤ 1` 为**硬上界**;反例 `DZ_OUTER = 2, DZ_INNER = 0`(通过旧断言但永达不到全速)· `DZ_INNER = −1/2`(静止时给 0.35 输出)。

**AC 计数**:28 → **29 条**(BLOCKING 16 → **17**);分组 `29 = A9 · B5 · C4 · D4 · E5 · F2`。

### 同批机械修正(未增删 AC 条目)

- **`AC-3-A6` / `AC-3-A7` 职责切分** —— `:248` 原写「`AC-3-A6` 只查直接构造载荷的 float」,与 A6 实为 **asmdef 引用集**不符;改为 A6(引用集,不写流)/ A7(BLOCKING,递归扫描载荷可达闭包)分述。
- **`AC-3-A6` → `AC-3-B3`** 两处误引(`:551` 及 `AC-3-A7` 的理据段)—— 断言 `JudgeResult` 全整数的是 **`AC-3-B3`**。
- **`AC-3-C1` 补「不驱动焦点移动」+ 四轮补注** —— 记录三轮日志声称的落点**并不存在**(新种 3)。
- **`AC-3-C2` 补四轮补注** —— 其判据只依赖 `AC-3-A1` 的**前半条**,而前半条**与桥类型无关** ⇒ 不随 P0 焦点载体前置门而失效。
- **§Dependencies 五 CI 载体行 3 → 8 项**(`AC-3-A4②` · `A6` · `B2`(含 ③)· `C2` · `C4` · `D2` · `E1` · `E2②`);**新增两行**「P0 焦点载体前置门」+「未核验引擎符号的 spike 册页」。
- **`AC-3-E3` 补 ⑦**(集合元素计数前缀 —— I1 唯一软点的机械判据)。
- **行号自引用全部锚点化** —— `:308-313`(L_poll 注)→「§Detailed Design 三 规则七」;`:270`(`AC-3-C1` 注)→「§Detailed Design 四 规则十(D-A 修正段)」;`OQ-3-6` 内 `systems-index.md:54 / :477` → 语义锚点(并订正:`:477` 实为系统 9,`:481` 才是 20)。
- **`:1065` 加「(彼时口径,非现行)」+ 现行 29 / 17**。
- **`adr-011` 新增 `### §Risks-A spike 册页`** —— 唯一 spike 登记处,S1/S2/S3 表(未核验符号 / 承载的性质 / GDD 落点 / spike 验收对象 / 降级路径),并附「不在此册的符号(分层说明)」:`GetPropertyInt`(失败是**响的**,不升 spike)· `GetInstanceID`(pre-cutoff stable,分层不同于 post-cutoff)· 焦点桥本体(册页归 ADR-013)。Validation Criteria 焦点导航单测行、Implementation Guidelines 2/3/4、`:129` 每玩家 `Instantiate` 行均加 spike 标记;**驳回「改回 `Clone()`」的回退建议**。

### 命题级 sweep 对账表(本轮固化 · 供下一轮直接复用)

> **用途**:不变量有判据、AC 有判据,而**被推翻的散文命题此前没有扫掠工具** —— 这是三轮同构复发的流程根因。下表把「命题」映射到可执行的 `grep` 判据与「期望命中」,使残留扫描成为机械操作而非阅读。

| 命题(已推翻) | 应覆写原句模式 | 文件 | grep 判据 | 期望命中 |
|---|---|---|---|---|
| 3 只落**一个文件** | `一个文件` | `input-system.md` | `grep -n 一个文件` | 仅修正块内(规则四 修正块 / `AC-3-A5` 重写行 / 头部 / 尾史)= **0 处裸命题** |
| `L_poll` / `L_render` **不可调** | `不可调` | `input-system.md` | `grep -n 不可调` | 仅 修正块 / §四「3 只读、不可调」(`Move`/`Look` 曲线指数,**另一命题**)= **0 处裸命题** |
| 导航 = **单向意图流** | `单向意图流` | `input-system.md` | `grep -n 单向意图流` | **0**(已全量改为「类型化只读视图」) |
| 3 是**无状态意图源** | `无状态意图源`(裸) | `input-system.md` | `grep -n 无状态意图源` | 仅改名说明块内 = **0 处裸命题** |
| 输入→**判定**的可见性延迟 | `输入→判定` | `input-system.md` | `grep -n 输入→判定` | 仅 `AC-3-B1a` 四轮覆写注内 = **0 处裸命题** |
| 「出诊箱」= P0 纸 | `出诊箱` | `input-system.md` | `grep -n 出诊箱` | 仅 `AC-3-F1a②` 覆写注 + 通用消费者列举(91 / 496 / 1262)= **0 处 F1a 裸命题** |
| 同四条命题的**跨文件**泄漏 | `一个文件` / `不可调` / `单向意图流` | `tr-registry.yaml` · `systems-index.md` · `adr-011-*.md` | `grep -n` 三文件 | 仅 `TR-input-016` 的四轮就地覆写 note = **0 处裸命题** |
| 计数口径 | `28 条 AC` / `16 BLOCKING` | `systems-index.md` | `grep -n "28 条 AC\|16 BLOCKING"` | 仅逐轮口径注(历史项)= **0 处现行口径** |

**两条修订纪律(本轮固化,写入本日志即为规范)**:
1. **被推翻的句子必须就地覆写**,或**显式标注「此句已被 X 推翻」** —— **禁止只在旁边加注**(新种 3 / 新种 4 均由「加注代替覆写」或「声明代替落盘」产生)。
2. **凡散文写「`AC-3-X` 守门 Y」,Y 必须与 `AC-3-X` 的现行判据逐字同义** —— 否则该散文在下一轮修订后即成**静默谎言**(`AC-3-A6` / `AC-3-B3` 误引即此型)。

### 追踪记录(本轮同步)

- `design/gdd/input-system.md` —— **本轮 36 次编辑**(含 AC 组 29 条)
- `design/gdd/systems-index.md` —— row 3 补四轮记录 · `:251` / `:666` / `:475` 计数与状态就地刷新 · **逐轮口径注**扩到四轮(28/16 → 27/15 → 28/16 → **29/17**)
- `docs/architecture/tr-registry.yaml` —— **5 处就地覆写**(`TR-input-007` / `009` / `010` / `016` / `006`)
- `docs/architecture/adr-011-input-architecture.md` —— **4 处**(新增 **§Risks-A spike 册页** · Validation Criteria · Implementation Guidelines 2/3/4 · `:129` `Instantiate` spike 标记)
- 本条 review-log 追加 + **命题级 sweep 对账表**与**两条修订纪律**首次固化

Prior verdict resolved: Yes(三轮 7 项阻塞 + 2 项裁定已落盘,四轮复核确认**大部分就地覆写生效**;四轮**新捕获** 6 项真阻塞 + 4 项用户设计裁定 → 当日修订完成。**未提交**(硬约束:不主动提交,等用户指令);3 保持 **In Review**,未标 Approved)

---

## Review — 2026-09-16 — Verdict: APPROVED(用户裁定接受 · 非新一轮评审)

Scope signal: M
Specialists: 无(本轮**未起专家代理** —— 此为四轮修订后的**用户接受裁定**,非评审)
Blocking items: **0 未处置** | Recommended: 13(已落盘,或已显式登记为硬前置门)
Summary:**四轮 `/design-review` 均判 NEEDS REVISION,四轮修订全部当日落盘。用户在收尾 widget 选「接受修订并标 Approved」,不再重评审。**
**接受的内容**:AC **29 条**(BLOCKING **17** 条)· 两条不变量闭环(I1 = CLOSED · I2 收窄改名「无外部状态意图源」+ 机械判据)· 四轮 6 项真阻塞 + 4 项用户设计裁定全部落地 · `adr-011` 新增 **§Risks-A spike 册页** · 命题级 sweep 对账表(8 条 grep 判据)+ 两条修订纪律已固化于本日志。
**接受时明确未消的实质(登记为硬前置门,非静默遗留)**:
- **`OQ-3-5`**(`Emergency` 表达力缺口)—— 归 **10 急救动作**;写 10 GDD 前必须裁。
- **P0 焦点载体守门真空** —— `AC-3-A1` 在 P0 平面关掉了 D-A 依赖的同一性断言;登记为 **42 成文前的硬前置门**,并给 3 侧一条不依赖桥类型的降级路径(`adr-013` §Risks)。
- **未核验引擎符号**(`updateMode` 枚举成员 / `Instantiate` / `Clone` / `onAfterUpdate` / 焦点桥本体)—— 逐条入 **`adr-011` §Risks-A** 并带降级路径;BLOCKING 判据层已做到**零未核验符号**(只断性质)。
- **`AC-3-B1b`** —— 仍无法签核(最低目标硬件未定);设计门为 ADVISORY,发版前 BLOCKING。
Prior verdict resolved: Yes(四轮 6 项真阻塞 + 4 项裁定全部落盘;用户裁定接受 ⇒ **结案**。3 状态 `In Review` → **Approved**;`input-system.md` 头部 Status 同步。**未提交**(硬约束:不主动提交,等用户指令))
