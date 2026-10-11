# Sprint 07 — 内容解锁轮(把灰盒变真循环)

> **2026-10-11 开 · 上游**:sprint-06 收口(`8457d5b` / `e805d72`;G1–G5 全绿 + 第四轮 playtest 5A)
> **本 sprint 判据**:核心循环七环节(走近→查体→对照→落笔→开方→施治→纸面反馈)
> **首次由真实内容驱动一次**(值区非空 · 玩家按键可操作 · 候选列表真实例化)
> **规模**:≈3–5 人日(三件接线 + 一条 bake 管线 + 结构纪律)
> **规划依据**:全周期在轨分析(9-13 至今 665 commits)发现的结构问题 —— 「灰盒测试不对」
> 三层根因(内容层 / 输入层 / 判据层),本 sprint 对症解锁

---

## §1 背景:为什么「灰盒测试」是结构问题,不是风格选择

第四轮 playtest 5A 全绿,但 **Q2(值区空行)与 Q3(零急救)是「空态正确」,不是「内容正确」**:
值区空 = 输入入口没接 + 词表零字;零急救 = 手动化预期,但玩家也**没有任何方式**触发急救。
自动化同构:大量既有判据是「零事件 / 空集 / 无调用点」形态 —— 不是判据写错,
是**内容没立起来,唯一可测的形态就是空态**。

**三层根因(互相锁死)**:
1. **内容层**:`diagnosis_signs.json` 34 条**零 `display_words`** · `read_floor` 未烘 ·
   F2 `Signs` 恒空 ⇒ 值区物理上永远空;
2. **输入层**:Interact(E)/Emergency(F) 键位存在但**零运行期消费** ⇒ 玩家零操作入口;
3. **判据层**:因 1+2,呈现/交互判据退化为「零/空/无」断言。

**后果**:全量门 3185/133 全绿 + playtest 5A 全绿,但核心循环七环节**从未被真实内容
驱动过一次**。绿得太容易 = 门在测「系统不崩 + 空态正确」,不是「玩法可玩」。

本 sprint = 对症解锁:**让第一个「有内容的真循环」跑起来**,判据从空态转向内容态。

---

## §2 任务清单

### S1 词表/门槛真装载(内容层解锁 · 8)

| 项 | 落点 | 依赖 | 判据(可证伪) |
|---|---|---|---|
| S1.1 `DiagnosisSignBaker` / `DiagnosisReadFloorBaker` 跑 bake | `diagnosis_signs.json`(34 条,**词已在库** —— 见下方勘误)烘 `diagnosis_signs.cooked.bytes` · `diagnosis_read_floor.json` 烘 `diagnosis_read_floor.cooked.bytes`(含 floor 数组展开) | bake 管线已就绪(Editor.Tools.Bake 两 Baker + CookedWriter + 运行期 Codec 镜像俱在) | bake 双跑逐位一致 · 解码 34 行 != 0 · ConfigVersion 非零 · roundtrip |
| S1.2 运行期装载 | `AddressablesDataProvider` 新增两数据集 + `DataCooked` 入库 + 组合根注入 `ExamineDriver` | S1.1 | 查体后 `RevealedRows` 非空 + 值区出现**词条**(非空行);变异:删装载 ⇒ 仍空恰红 |

> ✅ **2026-10-11 S1.2 完成注记(见下方 §2.1 判据修订)**:
> - 装载面三件套已接通:`AddressablesDataProvider` 增 `DiagnosisSignDataSet`/`DiagnosisReadFloorDataSet`
>   (经 using 别名)装载(`SignTableAddress`/`ReadFloorTableAddress`)· `DiagnosisCookedDisk`(EditMode
>   兜底,直读同一份 baked 字节)· `CompositionRoot.Assemble` dual-path(provider≠null ⇒ Addressables,
>   null ⇒ Disk,零第二真源)+ `ExamineDriver.SetLexicon` + fail-loud 空词表。
> - **全量绿**:EditMode 3235 = 3188 passed · 0 failed · 1 inc · 46 skip;
>   PlayMode 133/133。S1.2 三测全绿。前红两门
>   (`test_fullGate_currentTree_zeroErrors` / `test_ac816_productionAssembly_zeroPrngCallSites`)
>   随 [D-ESC] 判据修订转绿。
> - **硬化受检**:全仓扫描零「非 8 前缀文件声明 8 类型」;IL 层 `matchedTypeCount` 兜底保留 ⇒
>   判据收窄不放行**实现躲出前缀**碎发路径。

> ✅ **2026-10-11 勘误(订正本 sprint 开轮时的误判 —— 留痕,不删原文记录)**:
> 开轮初判「`diagnosis_signs.json` 34 条零 `display_words`」为**误读** —— 那是用 python
> 下划线 key(`display_words`)查,真实 key 是中文 `display_词`。实测 **34/34 条三档词俱在库**
> (R-8.2 2026-09-14 冻结表,`sign_koplik` 粗/中档 null = 空白档回退语义,细档唯一)。
> ⇒ **S1 无内容缺口,只有管线缺口**:Baker/CookedWriter/运行期 Codec 镜像全就绪,
> 缺的是 bake 未跑(DataCooked 零产物)+ Provider 不识两数据集 + 组合根不调 bake。
> 本草词表转出方案**作废**(materia_lexicon 是「药→功效」轴映射,与体征呈现词不同域,本就不适用)。

## §2.1 D-ESC 判据修订(S1.2 接缝 · 2026-10-11)

- **触发**:S1.2 把真词表经 `AddressablesDataProvider` 装载(边界程序集,ADR-014 §五 指定落点),
  逃逸判据按「文件内任意 `Diagnosis[A-Z]\w*` token」判定 ⇒ 装载层/组合根一引用 8 产数据集即红,
  两条既有门(`test_fullGate_currentTree_zeroErrors` / `test_ac816_*_zeroPrngCallSites`)红。
- **修订**:token 面收窄为 **类型声明位**(`EscapedDeclPattern` = `\b(?:class|struct|interface|enum|record)\s+Diagnosis[A-Z]\w*`)——
  逃逸真实信号 = 非 8 前缀文件**定义了** 8 实现;**引用/消费** 8 公开 API(经 using/别名)= 合法面放行。
- **证伪侧不缩**:8 实现真躲出前缀 ⇒ IL 层 `CheckDiagnosisIl` 前缀下 `matchedTypeCount == 0` ⇒ `[D-0]` 红;
  全仓实测零「非前缀声明 8 类型」。
- **落点**:`DiagnosisBoundaryGates.cs` · 测试 `boundary_guard_test.cs`(`test_sourceEscape_declareWithoutPrefix_red_referenceGreen`)。

### S2 玩家输入入口(输入层解锁 · 3/4/8/10/11/37)

| 项 | 落点 | 依赖 | 判据(可证伪) |
|---|---|---|---|
| S2.1 `InputService` 装载 | `InputSystem_Actions.inputactions` 运行期装载(恰一 asset 实例,InputService 已就绪)+ BootRoot 每帧读 Interact/E/F | — | 启动后 `InputService.Actions` 非 null · 恰一实例断言绿 |
| S2.2 近病人 Interact → 查体 | 读 Interact 按下 + 近病人判定(复用在场查询)→ `ExamineDriver.OnExamine`(带真词表/门槛) | S1.2 | 按 E 近病人 ⇒ 值区出现词条(4/5 通道行);变异:删调用 ⇒ 按 E 仍空恰红 |
| S2.3 落笔 → 墨龄 | 方笺页落笔动作 → `JudgmentWriter.TryRecord`(生产调用点首现) | S2.2 | 落笔后 `InkAgeTicks > 0` + `JudgmentRecorded` 入病例流;变异:删调用 ⇒ 墨龄恒 0 恰红 |
| S2.4 开方 → 处方 | 方笺选档确认 → `PrescribeRequestDriver.TryPrescribe` | S2.3 | 开方 ⇒ `DrugTreatmentApplied` 入病史流 → 体征变化(有内容的管道终点) |
| S2.5 急救 → 手动链 | 按 F → `EmergencyManualDriver.TryManual` | — | 按 F ⇒ 双事件入病史流(Skip 载荷 method 判别);变异:删调用 ⇒ 零事件恰红 |

> ✅ **2026-10-11 S2 设备级入口批完成(接线面注记)**:
> - **S2.2/S2.5 已接**:新 `PlayerInputReader`(E/F 按下沿直读,承 MovementInputReader 过渡
>   最小面)+ `InteractionInputDriver`(设备级 E→`ExamineDriver.OnExamine`+切脉案绑定 ·
>   F→`EmergencyManualDriver.TryManual`),BootRoot 启动序装配、Update 在**视觉实体刷新后 /
>   UI 刷新前**转发。**全绿**:EditMode 3237 = 3190/0 · PlayMode 136/136
>   (新测:`player_input_reader_test` ×2 + `interaction_input_chain_test` ×3 全绿;
>   后者的按键注入实证键盘 E/F **位不上**、须回落手柄 buttonNorth/buttonWest —— 已留档)。
> - **S2.3(落笔→墨龄)/ S2.4(开方→处方)登记为缺口**:入口在 42 侧(方笺落笔 / 戥子选档
>   确认),是 UI 事件 → 装配层的接线故事 —— **不做设备级自动行使**(自动开方/落笔 =
>   玩家零输入出事件 = 假绿,承「灰盒测不对」根因)。归下一拍(42 接线轮),
>   `InteractionInputDriver` 文件头已登记。

> ⚠️ **S2 判据形态订正(2026-10-11,对照既有红线不偏离)**:
> - S2.5 判据「按 F ⇒ 双事件」以 **PlayMode 虚拟按键注入**(手柄回落)**全绿**达成。
> - S2.2 判据「按 E 近病人 ⇒ 值区出现词条」:接线面只落到「查体落地 + 切脉案绑定 +
>   病史流零新事件(乙案)」三个可用判据;**「值区真的渲染出词条」需一次人工 playtest
>   目视确认**(词条 = 8 真表,S1.2 已装载;UGUI 脉案值区渲染 = 42 屏)。

### S3 ③ 候选列表生产实例化(形态件施加点③闭合 · 42/10)

| 项 | 落点 | 依赖 | 判据(可证伪) |
|---|---|---|---|
| S3.1 `EmergencyCandidateList` 生产实例化 | 运行期创建列表载体 + 跳过入口 + 候选动作标签喂入(止血/通气) | S2.5 | M2 #8 施加点③闭合:运行期列表实体存在 + 跳过入口可用;变异:删实例化 ⇒ 运行期零载体恰红 |

> ✅ **2026-10-11 S3.1 完成(载体生产实例化 · 范围注)**:
> - 新 `EmergencyCandidateRuntime`(组合根装配 + BootRoot 挂 UI 根):实例化候选列表、
>   喂 10 最小动作标签(止血/通气)、跳过入口恒在。**判据 = 「运行期列表实体存在 +
>   跳过入口可用」**(story-023 边界①的载体半边)。
> - **全绿**:EditMode 3240 = 3193/0 · PlayMode 136/136(新测 `emergency_candidate_runtime_test` ×3)。
> - **范围注(未假装完成)**:动作判定 / Idle 可达时序 / **跳过入口真的能跳过**的交互面
>   归 10 + 数据绑定轮 —— 若自动跳过 = 玩家零输入出事件 = 假绿。候选列表的**可见/时序**
>   归 10 状态机(story-023 边界① 已登记),本宿主零时序。

### 结构纪律(治「周期性账目失真」· 每 sprint 生效)

| 项 | 落点 | 判据 |
|---|---|---|
| C1 头部状态必刷 | active.md 头部 + STATUS 块 + sprint 行状态每 sprint 收口更新 | 收口 commit 含 active.md 头部变更(git 可验) |
| C2 美术待裁排期 | 「美术待裁」裁定上日程(见 §4 美术提前提案) | 裁定落盘即闭 |
| C3 评审门回归常态 | 本 sprint 起每 sprint ≥1 轮双代理评审;G3/G4/G5 欠账补课 | 评审原件落 `production/qa/evidence/` |

---

## §3 分阶段验收

- **S1 ✅ 完成(2026-10-11)**:查体后值区非空(词条来自真烘焙产物,非测试直喂)——
  bake 两份产物入库(S1.1)+ 运行期装载 + 组合根注入(S1.2)全绿
  (EditMode 3235 = 3188/0 · PlayMode 133/133);副作用 = D-ESC 判据修订见 §2.1。
- **S2 ◐ 设备级入口批(2026-10-11)**:S2.2(按 E 查体)+ S2.5(按 F 急救双事件)已接并全绿
  (PlayerInputReader + InteractionInputDriver + BootRoot 转发);
  S2.3(落笔→墨龄)/ S2.4(开方→处方)**登记缺口**,归 42 接线轮(UI 事件 → 装配层)。
  判据:EditMode 3237 = 3190/0 · PlayMode 136/136。
- **S3 ✅ 完成(2026-10-11)**:候选列表生产实例化 + 跳过入口可用
  (`EmergencyCandidateRuntime` + 组合根 + BootRoot 挂载;范围注 = 交互面归 10)。
  判据:EditMode 3240 = 3193/0 · PlayMode 136/136。
- **本 sprint 收口判据**:核心循环七环节 —— 走近(玩家位移链)✅ · 查体(E 键 → 真词表
  装载 → 值区有词,S2.2 设备级 ✅ · 值区渲染待人工目视)◐ · 对照(脉案五通道行渲染,42 屏)◐ ·
  落笔(→ 墨龄,S2.3 归 42 接线轮)◐ · 开方(→ 处方,S2.4 归 42 接线轮)◐ · 施治(F 键 →
  双事件,S2.5 设备级 ✅)◐ · 纸面反馈(值区/候选列表载体 ✅)→ **玩家可操作的第一截已立**;
  完整七环节收口待 42 接线轮 + 人工 playtest 目视。
- **本 sprint 完成**:核心循环七环节**玩家可操作**(输入入口全接)+ 第一次被真实内容驱动
- **判据基线**:EditMode 3185 → ≈3210–3230 · PlayMode 133 → ≈136–140(qa-lead 估)

## §4 风险与依赖

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| `display_words` 转出源未裁 | 中 | 高(S1 全堵) | 见 §2 S1 红线,用户裁前可先做 bake 管线其余面 |
| InputService 装载踩 Unity 6 坑 | 低 | 中 | InputService 已就绪恰一实例;装载走既有 spike 结论 |
| 近病人判定误差(Interact 距离) | 低 | 低 | 复用在场查询 + 表现层格判距(ADR-009 §七 形状) |
| **美术提前待裁**(见下) | — | 决定 M4/M5 日历 | 用户裁定后单列 story,不进本 sprint 关键路径 |

## §5 不做

- **数值轮**(阈值 / 系数 / 词表数值精调 —— 承「数值用户自己调」)
- **S4 = 9 侧 F2 病种级投影**(`ProgressionEvaluator.Signs` tripwire)—— 登记 NOT-RUN,归 sprint-08
- **联机 / VR / 手感打磨 / 教学流程**(P1a+)

---

## §6 美术提前提案(2026-10-11 · 待用户裁定)

> **依据**:用户授权「如果需要美术资产提前,可以提出,推翻以往设定也是允许的」。

**勘察事实**:
- 纸面五族 16 张 `*-final.png` **已入库**且被 USS 引用(SkeuoPaper/Ink/FocusVisible)✓ —— **纸面零成本,不重做**;
- 42 项 VS Critical 真资产 **0/42**,全部灰盒豁免,归属 11 epic(R-2 已裁);
- M5 核心假设 = 「判断 → 施治」**本身好玩**(立项问题)—— 灰盒胶囊病人 + 虚拟施治
  **永远证不了好玩**。

**提案(甲案 · 最小破局,推荐)**:**仅提前两条关键路径真资产**,其余 40 项维持 M4 时序:
- **P1 病人真模型**(角色类 · 消费系统 = patient-ai/diagnosis/case):灰盒胶囊 → 可识别病人
  (本游戏核心内容面;玩家对「病人」的认知 = 零 → 有人形);
- **P2 施治动作真资产**(急救动作 2 个 · 止血/通气 SFX + 手势 VFX):M5「施治好玩」的
  证伪前提 —— 无真动作感则核心假设**不可判**;
- 纸面/HUD 脉案面 = 已有真图,不重复出。

**乙案(全面提前)**:推翻「形态优先」(10-03 用户裁定),42 项全真资产进入 sprint 序列 ——
成本大、阻塞 M3 排期,不推荐。

**所需资源**:P1/P2 需**外部美术或用户自产**(程序占位胶囊不可升级为人形)。
裁定后单列 story 进 sprint 序列,不进本 sprint 关键路径。

---

## §7 收口(待用户指令)

- S1–S3 + C1–C3 全绿 → 第五轮人工 playtest(此时**应有内容**:值区有词 · E/F 可操作 ·
  候选列表可见)→ M2 Exit #8 复评(施加点③闭合 + ②④ 真值源)→ 全量门 + 提交推送
