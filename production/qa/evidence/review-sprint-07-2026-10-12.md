# 评审原件 — sprint-07 双代理评审(恰一轮 · 2026-10-12)

> **义务出处**:`.claude/docs/review-workflow.md` 🔴 原件落盘义务(BLOCKING 级)·
> sprint-07 §3 结构纪律 **C3**(本 sprint 起每 sprint ≥1 轮双代理评审;
> G3/G4/G5 欠账补课同批)。
> **流程**:双代理并行(代码面 = lead-programmer · 测试面 = qa-lead)恰一轮
> → 主会话独立复核(抽查 5 处关键 finding,grep 复核)→ 本原件。
> ⚠️ 评审评的是**特定时点**:5 提交 `8457d5b` / `12b4947` / `34a63d0` / `ff2d8dd` / `2fa5d3b`
> (HEAD = `2fa5d3b`,工作树干净)。事后重做 = 新判定,不追认本判定。

## 一、原判定(双代理 · 恰一轮)

**总判定:CHANGES REQUIRED**(两面独立收敛;无 BLOCKING 级假绿、无越权改域、
无未登记 EventKind)。

**总判定转 APPROVE 的最小充要 = 8 项**(见 §四 修复清单)。

### 分提交裁决

| 提交 | 代码面 | 测试面 | 合并 |
|---|---|---|---|
| `8457d5b` sprint-06 G3/G4/G5(欠账补课) | CHANGES(F-1 在场前置) | **APPROVE**(F5/F8 建议) | 欠账主体成立,1 中项待修 |
| `12b4947` S1 词表装载 + D-ESC 收窄 | CHANGES(H-1 + H-2) | CHANGES(F1 BLOCKING + F4) | 必改最多 |
| `34a63d0` S2 设备级输入 | **APPROVE** | CHANGES(F3 + F7) | 代码面过,测试面补 |
| `ff2d8dd` S3 候选列表载体 | **APPROVE** | CHANGES(F3 半边) | 封装小项 |
| `2fa5d3b` 42 接线轮 | CHANGES(F-10/F-11/F-12) | CHANGES(F3 + F6) | 必改 |

### 两面一致的通过项(禁空泛通过 —— 各自验证面)

- **假绿红线 PASS**:零输入 ⇒ 零事件(按下沿门控 · EmergencyAttemptDriver 退役壳实证
  · 无自动开方/落笔/跳过路径);测试面亦无「自动行使冒充玩家意图」。
- **越权 PASS**:5 提交零改 `Sim/` 源 · 零动 GDD · **零新增 EventKind**(ADR-024);
  `SkillGrown` 豁免撤除与写者落地同批。
- **ADR-005 / 013 C3 / 029 PASS**:写者全走 `IEventSink.Append` + `IPayloadEncoder`
  (新代码零 `new PayloadRef(`);三宿主全住 `Gameplay.Boot`,UI 侧只加只读 `GetUiRoot`;
  tick 同源 `SimTickDriver.CurrentTick`。
- **SetAuthor 同步核对无遗漏**:`JudgmentWriter` 全仓唯一实例化点在宿主构造内,
  不存在外部改写致失步;`BoundPatient` 无第二副本(写者侧病人是逐次形参)。
- **测试纪律**:命名/AAA/确定性合规;精确值断言(墨龄卡 1、剂量卡 65536L、
  `CountOf==1` 查全流)变异能力成立;1 inc / 46 skip 全为既有 NOT-RUN,
  与本 5 提交零交集(测试面逐名核对)。
- **禁借绿合规**:范围注(交互面归 10/42、跳过「可用 ≠ 能跳过」)测试头注 + sprint
  文档双落,无一处表述成完成态 —— **唯 §三 C-F12 例外**(收口行自相矛盾,见修复清单)。

## 二、主会话独立复核记录(抽查 · 全部属实)

| finding | 复核手段 | 结果 |
|---|---|---|
| H-1 生产 provider 零调用点 | `grep DataCorePreloader.Preload`(排除自文件) | **零命中,属实**;`BootRoot:105` = `Assemble()` 无第二实参 |
| H-2 D-ESC 替选集无 delegate | grep `DiagnosisBoundaryGates.cs` | `EscapedDeclPattern` 仅 class/struct/interface/enum/record,**属实** |
| C-F1 急救无在场前置 | grep `EmergencyManualDriver.cs` 的 `IPresenceQuery/IsPresent` | **零命中,属实** |
| T-F1 值区零正向断言 | grep `RevealedRows` 全 Tests | 仅 `==0` 断言(`composition_root_test:135`)与 UI 空态测,**属实** |
| C-F12 文档矛盾 | grep `输入入口全接` | `sprint-07.md:157`,与 §2 ◐ 矛盾,**属实** |

## 三、Findings(两面合并 · 按严重度)

### BLOCKING / 高

**B1(H-1)· 生产 Addressables 路径是死代码,player 构建启动必抛** — `12b4947`
- 位置:`unity/Assets/Gameplay.Boot/BootRoot.cs:105` · `CompositionRoot.cs:169` 注释
  · `Gameplay.Presentation/DataCorePreloader.cs:17-18`(仅 `<example>` 文档)
- 失效场景(可证伪):player 构建 `Application.dataPath = <build>_Data` ⇒
  `DiagnosisCookedDisk` 拼路径 `File.Exists == false` ⇒ 抛「烘焙产物不存在」⇒ `Start()`
  中断。编辑器内 `dataPath == Assets` 恰好全绿 ⇒ 现有 3246+136 测不到它。
  「声明(生产 = Addressables)与实现(恒磁盘兜底)互斥,二者必有一假」。
- 修复落点:`BootRoot.cs:105` 前调 `DataCorePreloader.Preload()` 并传 provider;
  或改两处声明为现状 + 立 tech-debt(由用户裁)。

**B2(H-2)· D-ESC 收窄留两条可写出的绕过 + IL 兜底注释口径错误** — `12b4947`
- 位置:`unity/Assets/Editor.Tools.Gates/DiagnosisBoundaryGates.cs:495-508`、`:493-494`
- 绕过①:`delegate void DiagnosisFoo(int x);` 不在声明位替选集 ⇒ 绿(delegate 是真类型声明)。
- 绕过②:同文件双命名空间,`:506-508` 见前缀命名空间即整文件 return ⇒ 第二段逃逸永不检。
- IL 兜底失效:前缀下合法类型存在 ⇒ `matchedTypeCount > 0` ⇒ 对**混入的单个逃逸类型**
  恒不触发;注释把它写成通用兜底是口径错误。
- 已排除(防误报):`record class DiagnosisX` 被子串命中不绕;注释已被剥离;
  字符串字面量是误报面非漏检面。
- 修复落点:替选集补 `delegate`;按命名空间块判定;订正注释;
  `boundary_guard_test.cs:474-496` 补两条负测(delegate 逃逸红 · 双命名空间第二段红)。

**B3(T-F1)· 「值区非空」头号判据零正向断言** — 测试面 BLOCKING
- 位置:`Tests/EditMode/Boot/examine_driver_test.cs:94-112` · `composition_root_test.cs:134-135`
- 失效场景:词表装入但求值链/queryLevel/门槛全挡 ⇒ 值区物理恒空,3246 全绿 ——
  「内容态解锁」仅人工目视兜底,正是本 sprint 要治的「空态正确」在测试面的残留。
- 修复落点:补 `test_s12_examineRealLexicon_revealedRows_nonEmpty`(真装配 +
  position≥floor 病人 ⇒ `OnExamine` 后 `RevealedRows.Count > 0`),并使「删装载」也红。

**B4(T-F2)· S2.1 判据悬空(既未实现也未登记 NOT-RUN)**
- 位置:`production/sprints/sprint-07.md:76`
- 修复落点:文档改判「S2.1 = 过渡直读达成,InputService 运行期装载延后(独立 story)」
  + NOT-RUN 登记;或补 `InputService.Actions` 存在性测。

**B5(T-F3)· BootRoot 三处接线面零测试 —— 「删调用恰红」对启动序不成立**
- 位置:`BootRoot.cs:347`(`OnFrame`)/ `:172`(`SetAuthor`)/ `:210-214`(`MountTo`)
- 失效场景:三行任一被删 ⇒ 全部自动化恒绿;`:172` 删 ⇒ 真机落笔/开方 fail-loud 崩;
  `:210-214` 删 ⇒ 游戏内候选列表永不出现 —— 只能靠人工撞见。
- 修复落点:照 `emergency_attempt_wiring_test` 反射 + 真启动序范式补一条
  PlayMode boot wiring 测;或把三行显式降级登记为人工 playtest 项(二选一,须落盘)。

### 中

**M1(C-F1)· 急救手动链缺在场前置** — `8457d5b`
- `EmergencyManualDriver.cs:76-85` 文档称「须在场」,实现与
  `HostEmergencyProcessor.cs:94-97` 均不查(只拒负值);处方侧 1c 显式查 `Presence` 不对称。
  当前被唯一调用方「只选在场病人」**遮住而非堵上**。
- 修复落点:构造注入 `IPresenceQuery`,不在场 ⇒ `return new EmergencyManualResult(false)`;
  或改文档为调用方契约并登记。

**M2(C-F10)· 宿主 null 依赖静默降级(违反自身 fail-loud 口径)** — `2fa5d3b`
- `PrescriptionPageHost.cs:64-66`(`portionsStore`/`tickDriver` 无 `?? throw`)+
  `:124`(`_portions?.Grant`)+ `:141`(`_ticks?.CurrentTick ?? 0L`)。
  缺 ticks ⇒ 落笔/开方全写 tick 0,后续 tick 推进即被 Append 拒。
- 修复落点:两参 `?? throw new ArgumentNullException` + 负测(与既有 fail-loud 测同款)。

**M3(C-F11 ≡ T-F6)· 逐次 Grant 自证库存前置(双代理独立收敛)** — `2fa5d3b`
- `PrescriptionPageHost.cs:121-127`:每次开方前无条件 `Grant(1)` ⇒ 20 的 `HasPortions`
  前置在宿主路径上恒真(空检查);失败可无限重复 ⇒ 库存单调增长。
- 修复落点:种子只发一次(`SetAuthor` 或装配期),`Prescribe` 内不 Grant;
  并在文件头 + sprint-07 §5 登记「20 真库存落地时删种子」移除义务。

**M4(C-F12)· 文档账目失真** — `sprint-07.md:157`
- 「本 sprint 完成:七环节玩家可操作(输入入口全接)」与同文件 §2 落笔/开方 ◐
  「交互面归 42」自相矛盾;宿主两方法 `Gameplay.UI/Presentation` 零调用点(反向缺口,
  非假绿)。
- 修复落点:收口行改「七环节中 5 环设备级可达;落笔/开方 = 接线宿主就位、
  交互面待 42」(与 §2 ◐ 对齐)。

### 低(建议项 · 不阻断转 APPROVE)

- **T-F4**:`examine_driver_test.cs:77` 恒真断言(`again.Rows.Count == itself`)+
  同源自比名不副实;产物 = 源 JSON 的漂移门不存在(改源不重烘 ⇒ 行数不变恒绿)。
  ⇒ 改 Baker 输出 vs 入库字节逐字节比对,删恒真行,read_floor 对称补一条。
- **T-F5**:EditMode 零输入窗只 `Advance` 未调 `OnTickEdge`(头注与实现不符);
  承重在 PlayMode 测 —— 补调用或改头注。
- **T-F7**:键盘 E/F 生产分支 CI 零执行(batch 注入不上)⇒ §3 补 NOT-RUN 登记
  (归第五轮人工 playtest 目视)。
- **T-F8**:`zerolInput` 拼写;`prescription_page_host_test` 直构实例加注释防误读。
- **C-F2**:退役壳 `_presence` 零读取(误导 + 告警面);**C-F3**:死 try/catch +
  4 处数值字面量立 tech-debt 债;**C-F7**:`OnFrame` 恒假 null 判 + `_actorId` 未读字段;
- **C-F8**:绑定双真源(`CasebookSource.ChangePatient` vs `PrescriptionPageHost.BindPatient`)
  分叉风险 —— 42 翻页 story 前收敛单入口或加恒等断言;
- **C-F9**:`DefaultCandidateLabels` 静态数组元素可被外部改写(测试间污染面)
  ⇒ 改 `IReadOnlyList<string>`;
- **C-F6(.gitignore)**:`.gitignore:191-198` 三组条目随 `12b4947` 入库,与用户裁定
  「.gitignore 本地改动永不提交」冲突 —— **须用户裁定**:回滚留工作树,或修订该规则。
  评审不擅动。
- 急救侧无 `isHost` 形参(处方侧有)—— 联机对称性缺口,登记归 45 轮(P1b)。

### 未及查(如实登记)

- 代码面:`HostEmergencyProcessor.Process` 生产调用点未穷举;`data-core.asset` 组条目;
  `DiagnosisBakeCli` 全文;`.meta`/asmdef 引用边完整性;**未运行任何测试**(全静态核验)。
- 测试面:IL 层 `matchedTypeCount` 断言本体;生产 Addressables 装载路径测试
  (疑零测);Editor 侧「重烘比对」菜单/CI 门;第五轮 playtest 排期。

## 四、修复清单(转 APPROVE 最小充要 · 8 项)

| # | 落点 | 验证命令(可证伪) |
|---|---|---|
| 1 | B1:BootRoot 接 `Preload()` + 传 provider,或改声明立债 | grep `DataCorePreloader.Preload` 生产调用点 ≥1(或声明已改) |
| 2 | B2:替选集补 `delegate` + 命名空间块判定 + 2 负测 | `boundary_guard_test` 新增两测红例通过;手动放 `delegate DiagnosisFoo` 构建门红 |
| 3 | B3:补真词表端到端正向测 | 新测在 EditMode 全量绿;变异(删 SetLexicon)恰红 |
| 4 | B4+S2.1 文档改判 + NOT-RUN 登记 | `grep NOT-RUN sprint-07.md` 命中 S2.1 行 |
| 5 | B5:补 boot wiring 测或三行显式降级登记 | 新 PlayMode 测绿;或文档登记行存在 |
| 6 | M1:急救注入在场查询 | 新负测(不在场 ⇒ 零事件)绿 |
| 7 | M2+M3:`?? throw` + 负测;Grant 改一次性种子 | 负测绿;开方失败后库存不增(新测) |
| 8 | M4:`sprint-07.md:157` 与 §2 ◐ 对齐 | grep 该行不再含「输入入口全接」 |

**修复后复跑**:`EditMode`(基线 3246 = 3199/0/1/46)+ `PlayMode`(136/136)全绿;
验证命令逐项贴本表第 3 列。

## 五、G3/G4/G5 欠账补课(C3 后半)结论

**测试面 APPROVE** —— `prescribe_driver_test`(7 判据全测 · 载荷量纲自检 ·
负向四条齐 · WriterExistenceGate 豁免双侧守)、`emergency_manual_driver_test`
(双事件计数/次序/路由/归因全断)、`emergency_attempt_wiring_test` 反向改造
(`presentCount≥1` 防空真)质量高于仓库平均;**代码面附 M1 一中项**(急救在场前置)。
欠账主体(8457d5b)在 M1 修复后即可结。

## 附录 A · 代码面报告全文(lead-programmer)

<details>
<summary>展开</summary>

# 代码面评审报告 — sprint-07 双代理评审(代码面)

**评审官**:lead-programmer(只读,未写任何文件)
**评审对象**:5 个提交(`8457d5b` / `12b4947` / `34a63d0` / `ff2d8dd` / `2fa5d3b`)
**基准**:CLAUDE.md 编码规范 · ADR-005 / 011 / 013 / 014 / 024 / 029 · 假绿红线 · fail-loud 口径 · sprint-07 §2 判据

## 0. 总判定

**CHANGES REQUIRED**(无 BLOCKING 级假绿 / 无越权改域 / 无未登记 EventKind)。

两条高优先:H-1 生产 provider 未接线(死代码,player 构建启动即抛)·
H-2 D-ESC 收窄有具体可绕样例(≥2 种)+ IL 兜底对单个逃逸结构性失效。

| 提交 | 判定 | 主因 |
|---|---|---|
| `8457d5b` G3/G4/G5 | CHANGES REQUIRED | 急救手动链缺在场前置 + 文档与实现不符 |
| `12b4947` S1 | CHANGES REQUIRED | H-1 + H-2 |
| `34a63d0` S2 | APPROVE(附建议) | 按下沿/次序/成本均验证通过 |
| `ff2d8dd` S3 | APPROVE(附建议) | 仅封装性小项 |
| `2fa5d3b` 42 接线轮 | CHANGES REQUIRED | fail-loud 缺口 + 逐次 Grant 自证库存 |

## 1. `8457d5b` — CHANGES REQUIRED

**已验证通过**:假绿红线 PASS(退役壳实证 + 反向判据测试)· 处方侧三合取域检查
(1a 剂量 / 1b 换算+HasPortions / 1c 在场)齐全 · 主机唯一 Append + 零手搓
`PayloadRef` · fail-loud 具名断言 · WriterExistenceGate 豁免同批撤 · 越权 PASS。

**F-1(中)急救手动链缺在场前置**:`EmergencyManualDriver.cs:76-85` 文档称
「须在场」,`HostEmergencyProcessor.cs:94-97` 只拒负值,driver 不注入 `IPresenceQuery`
—— 与处方侧 1c 不对称。可证伪:对已离场 id 调 `TryManual` ⇒ 双事件照常入流;
唯一调用方只选在场病人把洞遮住而非堵上。修复:注入 `IPresenceQuery`,不在场 ⇒
`EmergencyManualResult(false)`;或改文档为调用方契约。

**F-2(低)退役壳无用依赖**:`EmergencyAttemptDriver.cs:36-42` `_presence` 赋值零读取;
`CompositionRoot` 空壳 null 断言无意义。修复:删字段或改注释;更彻底(删壳 +
撤 `BootRoot:333` 调用)需用户点头。

**F-3(低)死 try/catch + 数值字面量**:`PrescribeRequestDriver.cs:140-147` 只 rethrow;
`PrescriptionPorts.cs:55-70` / `EmergencyManualDriver.cs:57-66` /
`CompositionRoot.cs:~175` 4 处字面量(均带数值轮注记)。修复:删 try/catch;
tech-debt 立一条具名列 4 处行号。

## 2. `12b4947` — CHANGES REQUIRED

**已验证通过**:双路径同字节同 codec 无第二真源 · 装配期 0 行即抛 · `SetLexicon`
拒 null · D-ESC 测试同步改判据 · 越权 PASS。

**H-1(高)生产 provider 死代码**:`BootRoot.cs:105` 无第二实参;
`DataCorePreloader.Preload` 全仓零生产调用点;声明与实现互斥。可证伪:player 构建
`dataPath` 变 ⇒ `File.Exists` false ⇒ 抛 ⇒ 启动中断;编辑器恰全绿测不到。
修复:`BootRoot` 调 `Preload()` + 传 provider,或改 `DiagnosisCookedDisk.cs:9-10` /
`CompositionRoot.cs:169` 声明 + 立债;顺补 Edit≠build 路径可证伪测。

**H-2(高)D-ESC 两条绕过**:`DiagnosisBoundaryGates.cs:495-496` 替选集无 `delegate`;
`:506-508` 见前缀命名空间整文件 return ⇒ 同文件双命名空间第二段永不检;
`:493-494` IL 兜底注释口径错(前缀类型存在即 >0,混入单个逃逸恒不触发)。
已排除:`record class` 子串命中;注释已剥离;字符串是误报面。另注:扫描根固定
`Gameplay.Presentation`(`:524`)属既有覆盖面边界。修复:补 delegate + 按命名空间块
判定 + 订正注释 + 2 负测。

**F-6(低·用户规则面).gitignore 入库**:`.gitignore:191-198` 三组条目随本提交入库,
与「.gitignore 本地改动永不提交」裁定冲突。**须用户裁定**:回滚留工作树,或修订规则。
评审不擅动。

## 3. `34a63d0` — APPROVE(附建议)

按下沿门控实证(无输入不进两方法)· `Update` 单线程次序自洽(体征→视觉→交互→UI)
· O(n) 扫描只在按下沿 · 依赖方向合规(视觉位置只选对象,效果进流由写者复核 ——
急救侧除外,见 F-1)· ADR-011 过渡直读有登记 · 越权 PASS。

**F-7(低)**:`InteractionInputDriver.cs:66` 恒假 null 判;`BootRoot.cs:52,149`
`_actorId` 零读取。**F-8(建议)**:绑定只在按 E 一处更新,42 翻页路径出现即双真源
分叉 ⇒ 收敛单入口或加恒等断言。

## 4. `ff2d8dd` — APPROVE(附建议)

零写者零事件 · 范围注措辞与 `HasSkipEntry`「结构承诺」一致无过claim ·
42 不持状态 · asmdef 边界合规 · 挂载时机在 UI 就绪后 · 越权 PASS。

**F-9(低)**:`EmergencyCandidateRuntime.cs:33/:68` 静态/实例数组元素可被外部改写
(测试间污染)⇒ 改 `IReadOnlyList<string>`。

## 5. `2fa5d3b` — CHANGES REQUIRED

**已验证通过**:SetAuthor 双处同步核对无遗漏(`JudgmentWriter` 唯一实例化点在宿主内)
· 无 BoundPatient 第二副本 · fail-loud 声明两项 PASS · 接线期限 1「失败可重试、
成功即锁」PASS · tick 同源单调 PASS · 越权 PASS。

**F-10(中)**:`portionsStore`/`tickDriver` 无 `?? throw`,null 静默降级
(缺 ticks ⇒ 全写 tick 0,后续推进即被拒)—— 与同构造 `prescribeDriver` 传 null 会抛
自相矛盾。修复:两参 `?? throw` + 负测。

**F-11(中)**:逐次 Grant 自证库存(判定前喂饱 `HasPortions`;失败可无限重复
致库存单调增长)。修复:种子只发一次;或头注写明「前置被自身种子架空,20 落地时删」
+ tech-debt。

**F-12(中)**:宿主两方法 UI 侧零调用点(反向缺口,非假绿)vs
`sprint-07.md:157`「输入入口全接」—— 与 §2 ◐ 矛盾,账目失真。修复:收口行对齐。

## 6. 横切 · ADR 合规

ADR-005 COMPLIANT(注:急救无 `isHost` 形参,对称性缺口归 45 轮)·
ADR-011 COMPLIANT(过渡注记)· ADR-013 C3 COMPLIANT · **ADR-014 §五 DRIFT(H-1)** ·
ADR-024 COMPLIANT(零新 Kind)· ADR-029 COMPLIANT(零手搓 PayloadRef)。

编码规范:公共 doc 注释 PASS(仅 `OnFrame` 缺 summary)· 复杂度 PASS ·
DI PASS(唯一例外 F-9 静态数组)· 配置数据化 = 4 字面量立债(F-3)· 抽象点 PASS。

**未及查**:HostEmergencyProcessor 调用点枚举 · data-core.asset 组条目 ·
DiagnosisBakeCli 全文 · emergency_attempt_wiring_test 全文 diff ·
**未运行任何测试**(全静态核验)· .meta/asmdef 引用边。

**必改 6 项**:H-1 · H-2 · F-1 · F-10 · F-11 · F-12。

</details>

## 附录 B · 测试面报告全文(qa-lead)

<details>
<summary>展开</summary>

# 测试面评审报告 · sprint-07 双代理评审(C3 恰一轮)

评审对象:5 提交测试面 · 基线核验:`s7_s23_s24_editmode_v2.xml` = 3246/3199/0/1inc/46skip ·
`s7_s23_s24_playmode.xml` = 136/136(新测均在列,逐名核对)· 只读,grep + 读源 + 抽 XML。

## ① 覆盖-判据对账表

### S1(`12b4947`)

| 判据(出处) | 对应测试 | 判定 |
|---|---|---|
| S1.1 bake 双跑逐位一致 | `sign_table_test.test_bakeDeterminism_doubleRun_byteEqual`(既有,同进程;跨会话 NOT-RUN 已登记) | 真覆盖(既有) |
| S1.1 解码 34 行 != 0 | `examine_driver_test.test_s12_compositionRoot_loadsRealLexicon_fromBaked`(断 34) | 真覆盖 |
| S1.1 ConfigVersion 非零 | `examine_driver_test:79` | 覆盖但弱(F4) |
| S1.1 roundtrip | `examine_evaluator_test.test_g1_0_bake_..._roundtripDecode`(既有) | 真覆盖(既有) |
| S1.1 入库产物 = 源 JSON 烘出(漂移门) | **无**(全仓 `DataCooked` 零测试/零门引用) | **无 → F4** |
| **S1.2 查体后 `RevealedRows` 非空 + 值区词条** | **无**(全仓只有 0/空断言;正向 `SignCount>0` 仅合成夹具) | **无 → BLOCKING(F1)** |
| S1.2 变异:删装载 ⇒ 仍空恰红 | `HasLexicon` + `LexiconRowCount==34` + `withoutLexicon_failsLoud` | 真覆盖(装载面) |
| read_floor 61 档内容正确 | 无观测面 | 部分 |
| D-ESC 判据修订 | `boundary_guard_test.test_sourceEscape_declareWithoutPrefix_red_referenceGreen` | 真覆盖 |

### S2(`34a63d0` + `2fa5d3b`)

| 判据 | 对应测试 | 判定 |
|---|---|---|
| **S2.1 `InputService.Actions` 非 null + 恰一实例** | **无**(实现走 `PlayerInputReader` 直读过渡) | **无 → BLOCKING(F2)** |
| S2.2 按 E ⇒ 查体落地 | `interaction_input_chain_test.test_virtualE_readsAnd_triggersExamine`(含探针) | 部分(driver 层真,BootRoot 转发无测 → F3) |
| S2.2 按 E ⇒ 值区词条 | 无(承 F1) | 无 |
| S2.2 变异:删调用恰红 | 删 driver 红 ✓;删 `BootRoot:347` ⇒ 恒绿 | 部分(F3) |
| S2.3 落笔 ⇒ JudgmentRecorded + 墨龄 | `prescription_page_host_test.test_s23_writeJudgment_...`(卡精确 1) | 真覆盖(宿主层) |
| S2.3 BootRoot `SetAuthor`(`:172`) | 无(测试自调) | 无(F3) |
| S2.4 开方 ⇒ DrugTreatmentApplied | 两条(含 `CountOf==1` 查全流) | 真覆盖(宿主层) |
| S2.4 → 体征变化 | 宿主链未断;终点由既有 `vertical_slice_test` 覆盖(不经宿主) | 部分 |
| S2.5 按 F ⇒ 双事件 | `test_virtualF_..._doubleEvent` + `emergency_manual_driver_test` 手动/Skip 两条 | 真覆盖(driver 层) |
| S2.5 变异 | driver 层红 ✓;BootRoot 转发删 ⇒ 绿 | 部分(F3) |
| 收口「值区渲染待人工目视」◐ | 人工 playtest(待用户指令) | 合规登记(未借绿) |

### S3(`ff2d8dd`)

| 判据 | 对应测试 | 判定 |
|---|---|---|
| 列表实体 + 标签 + 跳过入口恒在 | `test_s3_assemble_createsRuntime_withLabelsAndSkip`(2 标签逐字断言) | 真覆盖(组合根层) |
| 挂载面可用 | 测试自调 `MountTo` | 部分 |
| BootRoot 挂载(`:210-214`) | **无**(删两行 3 测恒绿) | **无(F3)** |
| 变异:删组合根实例化恰红 | `IsNotNull(bag.EmergencyCandidates)` | 真覆盖 |
| 范围注(交互面归 10 · 自动跳过禁) | 测试头注 + sprint 文档双落 | 合规,未借绿 |

### G3/G4/G5 欠账(`8457d5b`)

| 判据 | 对应测试 | 判定 |
|---|---|---|
| G3.1 五步终点 + 载荷五字段 | `prescribe_driver_test.test_prescribe_selectedDose_...`(treatment_id/polarity/actor/potency/half_life 全精确) | 真覆盖 |
| G3.1 ≥2 档不同 dose | `test_prescribe_twoDetents_...`(卡 65536L / 2×65536L 量纲) | 真覆盖 |
| G3.1 不选档零事件 | `test_prescribe_noStroke_emitsNothing` | 真覆盖 |
| G3.2 无货拒开 / 未知药 / 档越界 / 负 tick | 四条负向 | 真覆盖 |
| G3.2 SkillGrown 写者 + 豁免撤 | `test_prescribe_growthGateHit_...` + `writer_existence_gate_test` 三处豁免断言 | 真覆盖 |
| **G4.1 零自动事件** | EditMode `test_zeroInput_window`(弱,F5)+ PlayMode `..._zerolInput_window_noEmergency`(强:真 BootRoot + presentCount≥1 防空真) | 真覆盖(靠 PlayMode 承重) |
| G4.2 双事件 + 次序 + 路由 + 归因 | `test_manualEntry_writesBothEvents_intoHistory`(before/after · `[before]`/`[before+1]` · `StreamRouting.Of` · Patient) | 真覆盖 |
| G4.2 Skip method 判别 | `test_manualSkip_methodSurvivesIntoPayload` | 真覆盖 |
| method/tick/actorId 闭集 | 两条 fail-loud | 真覆盖 |

### 批次一致性

- Inconclusive 1 = `Audio/SettingsExposureTest.test_monoOption_existsWithValidDefault`(既有)。
- Skipped 46 = 既有 NOT-RUN 面(CameraViewpoint ×8 · Telemetry ×12 · SkeuomorphicUI
  走查/截图 ×12 · EmergencyProcedures 手感 ×4 · Interaction ×3 · PatientAI ×2 ·
  PlayerController ×3 · Audio ×1 · Diagnosis ×1 等)—— **与本 5 提交零交集**;
  sprint-06 基线同为 1inc/46skip ⇒ 非新债,APPROVE。

## ② 逐提交 Finding

**BLOCKING(3)**

- **F1 · S1.2 头号判据「值区非空」零正向断言** — `examine_driver_test.cs:94-112`。
  正向测只断 `HasExamined` + 零新事件;`composition_root_test:134-135` 只断空;
  全仓无任何测试用真词表实例调 `OnExamine`。失效:求值链/queryLevel/门槛全挡 ⇒
  值区物理恒空,3246 全绿。修复:补 `test_s12_examineRealLexicon_revealedRows_nonEmpty`
  (真装配 + position≥floor ⇒ `RevealedRows.Count>0`),「删装载」也须红。
- **F2 · S2.1 判据悬空** — `sprint-07.md:76`。零对应测试;实现走 `PlayerInputReader`
  (头注自陈动作资产装载未交付);S2 完成注记只点名 S2.2/S2.5,S2.1 无状态。
  失效:收口复评时被默认读成已完成。修复:文档改判 + NOT-RUN,或补存在性测。
- **F3 · BootRoot 接线面零测试**:`BootRoot:347` / `:172` / `:210-214` 三行
  删掉全套测试恒绿,与判据明写的「BootRoot 每帧读 / 挂载」直接对不上。
  修复:照 `emergency_attempt_wiring_test` 反射+真启动序范式补 boot wiring 测,
  或三行显式降级登记为人工 playtest 项。

**CHANGES REQUIRED(4)**

- **F4 · 恒真断言 + 同源自比** — `examine_driver_test.cs:71-80`:
  `:77 AreEqual(again.Rows.Count, again.Rows.Count)` 永不可红;测试名
  `matchesBakerRows` 名不副实(两头都来自 `DiagnosisCookedDisk`);产物漂移门不存在
  (改源不重烘 ⇒ 行数不变恒绿)。修复:Baker 输出 vs 入库字节逐字节比对 +
  删恒真行 + read_floor 对称补。
- **F5 · EditMode 零输入窗名不副实** — `emergency_manual_driver_test.cs:42-55`:
  头注称跑 tick 边沿,实现只 `Advance` 未调任何 `OnTickEdge`;自动触发写回 EditMode
  面该测恒绿(承重在 PlayMode)。修复:显式调 `EmergencyAttemptDriver.OnTickEdge` ×N
  或头注写明守门责任在 PlayMode。
- **F6 · 宿主自动 Grant = 脚手架未挂账** — `PrescriptionPageHost.cs:121`:
  每次开方前发 1 份 ⇒ 库存门恒通过(driver 层 outOfStock 测已覆盖,非覆盖洞,
  是生产面免费药路径未登记)。修复:头注 + sprint-07 §4/§5 登记移除义务 +
  补宿主级「无货 ⇒ false」预期测(删 Grant 时红 = 删除时机信号)。
- **F7 · 键盘 E/F 生产分支 CI 未实测,未登记 NOT-RUN** —
  `interaction_input_chain_test.cs:197-225` 探针设计正确、workaround 留档充分,
  但 `kb.eKey/fKey` 分支(玩家主路径)CI 全程零执行。修复:§3 补 NOT-RUN 登记
  (归第五轮人工 playtest)。
- **F8(S4)· 拼写 `zerolInput`;宿主直构 fail-loud 测与装配袋实例不同,建议注释点明。**

**APPROVE(验证过 · 非空泛)**:prescribe_driver 7 判据全测 + 量纲自检 + 负向四条 +
gate 豁免双侧守 · 手动侧双事件五面全断 · wiring 测防空真 · host 测墨龄卡 1/全流计数/
异常类型逐一对齐/第 2 次开方是拒绝路径(非自动行使)· AssembleReady 直调
`CaseOpenWriter` 与先例一致(夹具合理)· D-ESC 测试四面齐 · 候选标签独立硬编码 ·
测试独立性(每测新 Assemble · PlayMode 全局态 TearDown 恢复)· 禁借绿合规双落 ·
1inc/46skip 零交集。

**未及查**:IL `matchedTypeCount` 断言本体 · 生产 Addressables 装载路径测试(疑零测)
· BootRoot.cs diff 逐行 · Editor 侧重烘比对门 · 第五轮 playtest 排期。

## ③ 总判定

**CHANGES REQUIRED**(非全盘否决;G3/G4/G5 补课与 S2.3/S2.4 宿主测质量高于仓库平均)。

- 测试纪律:命名/AAA/确定性合规,精确值断言与量纲自检是亮点;仅 F4 恒真 + F8 瑕疵。
- 判据可证伪:宿主/driver 层成立;**启动序接线面不成立(F3)—— 本次最重要的结构性发现,
  与本 sprint 自立的 C3/变异判据直接冲突**。
- 假绿红线:未见自动行使冒充玩家意图;需挂账的是 F6(生产面脚手架)与 F5(弱化,
  承重在 PlayMode)。
- 禁借绿:合规,范围注双落。

分提交裁决:`8457d5b` APPROVE(附 F5)· `12b4947` CHANGES(F1+F4)·
`34a63d0` CHANGES(F3+F7)· `ff2d8dd` CHANGES(F3 半边)· `2fa5d3b` CHANGES(F3+F6)。

**转 APPROVE 最小充要(3 项)**:① 补真词表端到端正向测(F1);
② 补 boot wiring 测覆盖三行或显式降级登记(F3);③ 文档登记 S2.1 改判(F2)+
键盘位 NOT-RUN(F7)+ 修 F4。

</details>

---

**原件元数据**:双代理并行恰一轮(不复评)· 主会话独立复核 5/5 属实 ·
被评审 HEAD = `2fa5d3b` · 修复复跑基线:EditMode 3246 = 3199/0/1/46 · PlayMode 136/136。
**C3 收口**:本原件即 sprint-07 §3 C3「评审原件落 `production/qa/evidence/`」判据的满足件;
被评审 5 提交的 `Complete` 判定**引用本路径**,8 项修复清单完成前 sprint-07
不得转 Complete(不借绿)。
