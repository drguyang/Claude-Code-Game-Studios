# Session State — 2026-10-07(**当前阶段 = Pre-Production · Sprint 04 Phase 1 ✅ 已收口 · Phase 2 进行中**)

## 🔄 本轮 = disease-simulation **story-007 重开 9 落地**(2026-10-07 · 进行中)

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。

### 交付物
- **数据**:`assets/data/disease_action_axis.json`(新建,9 的处置轴 + treatable_by 关系,单一 master)
- **9 侧烘焙三件套**:`DiseaseActionAxisBaker.cs` / `DiseaseActionAxisBinder.cs` / `DiseaseActionAxisCookedWriter.cs` / `DiseaseActionAxisValidator.cs`(新建)
- **9 侧 schema**:`RegistrySchema.cs` 增 `TreatableByEntry` + R1-18/R1-19 校验
- **10 侧**:`EmergencyAction.cs` DC-4 判据源改指 9(文档已更新,实现仍用 `Enum.IsDefined`)
- **11 侧**:`PrescriptionActionIdRegistry.cs` 影子→真源(闭集 = 9 的轴,地板 = `NOISE_BAND_POTENCY_9` = 100 raw)
- **11 侧**:`PrescriptionActionsBinder.cs` DC-2/DC-6 从 warnings 升格为 errors(硬失败)
- **11 侧**:`prescription_actions.json` 的 `action_id` 重排 1→10
- **菜单**:`DataBakeMenu.cs` 增 `BakeDiseaseActionAxis` 菜单项
- **文档**:`entities.yaml` / `disease-simulation.md` / `prescription-and-medication.md` / `architecture.yaml` / `tr-registry.yaml` / `traceability-index.md` / `EPIC.md` / `index.md` / `story-007` 全量更新

### 测试(实测)
- 9 侧:`unity/Logs/disease_axis.xml` = **55 / 55 passed / 0 failed**
- 11 侧:`unity/Logs/prescription_axis.xml` = **174 / 174 passed / 0 failed**
- 10 侧:`unity/Logs/emergency_dc4.xml` = **115 / 115 passed / 0 failed**(4 skipped 既有)
- 全量:`unity/Logs/editmode_full_axis.xml` = **2935 / 2888 passed / 0 failed / 46 skipped / 1 inconclusive**(与基线一致)

### 待办
- ⬜ 双代理一轮评审(代码面 + 测试面)
- ⬜ 修复评审发现
- ⬜ 复跑绿
- ⬜ 评审原件落 `production/qa/evidence/review-disease-action-axis-2026-10-07.md`
- ⬜ 收口提交推送

### 未闭登记(禁借绿)
- **9 的 C# 16 字段 + 17 条区间校验 vs GDD §R1 的 17 条语义检查** —— 结构性断裂,归 9 的下一轮
- **`清创`** —— 9 点名、10 无实现,归 10 的 GDD 轮
- **10 侧 `ValidateActionId` 实现** —— 文档说读 9 的轴,实现仍用 `Enum.IsDefined`,须后续改为读 9 的烘焙产物

---

## ✅ 上一轮 = prescription-medication **story-002 补评审件**(2026-10-06 · 已收口)

> 承「补002评审件」。依 `.claude/docs/coding-standards.md` §Review Evidence Standards:
> 缺原件的对象**出路 = 补做一次评审(评当下)并落新原件**,**不追认**原判定。

- **原件**:`production/qa/evidence/review-prescription-story-002-2026-10-06.md`(新建)
  —— 含 **评审时点声明**(不追认 `f2bad6b`)· 原判定 → 修复落点 → 验证命令 · 变异证明 · 未闭登记
- **判定**:QA 侧 **2 BLOCKING · 3 MAJOR · 6 MINOR · 2 NIT**;
  结构侧代理**正文未回收**(交付前被协调方中断),其探索轨迹与 QA 侧 B1/B2 **独立重合**
  —— 原件 §〇 已**如实登记该回收缺口**,不凭记忆补写
- **修复落点**:
  - **B1(AC-11-09)** 新建 `PrescriptionDerivedBaker.cs`(唯一派生点,经 `DoseCalculator` 不重写 F-11.1)
    + Binder/CookedWriter/Baker/Probe 全链接线 + 4 测
  - **B2(AC-11-08)** 反射扫描(除两所有者外零 `axis_offset` 消费点)+ **阳性对照** +
    **双实现对拍**(`HalfLifeCalculator` ↔ `QualityTimelineSolver` 在 `half_life` 轴逐位相等)
  - **M1/M2/M3** loCarry 夹具 · 禁 `Int128`/`BigInteger` 扫描路径 · 新建 `PerceptibleFloorComparator.cs`
  - **m1/m2/m4/m6** 负 dose 两测 · `SignBound` 两测(恰 `2^63`)· 三处改名 · 扫描器注释剥离重写
- **变异证明 7 项全落盘**(MUT-1…7);
  ⚠️ **MUT-3 首轮存活 127/127**(原夹具全被更早的 `qHi != 0` 守卫拦下,`SignBound` 不可达)
  ⇒ 补 `test_dose_quotientAtSignBound_throws` 后**恰 1 红** —— 印证 QA 侧 m2 判定为真
- **复跑绿**:过滤套件 `dose_fix_2.xml` = **129/129 / 0 failed**;
  全量 `full_editmode_story002.xml` = **2890 / 2843 passed / 0 failed / 1 inconclusive / 46 skipped**
  (基线 2826 ⇒ +17 = 本卡 13 + 表卡 4)
- **未闭(禁借绿)**:AC-11-11④(BL-7)· AC-11-19 断言本体(BL-2)· AC-11-15 矩阵(ADR-012)·
  TR-prescription-007 21a 半边(BL-1)· **F5 双实现合并归 21a**(本卡只把重复可证伪)
- **诚实边界**:① `single_dose_max` **消费侧未接线**(9 的 F1 clamp 归 disease story-004);
  ② 生产 `ConfigVersion` **不含 `item_database_items.json`** ⇒ 「改药 ⇒ 哈希变」**当前不成立**;
  ③ F5 双实现**异常契约不一致**,对拍只证合法域内逐位相等

---

## 🔄 上一轮 = prescription-medication story-005(戥子输入与方笺呈现 —— 离散整数档与黄铜读数)

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> **本件 = prescription-medication epic 末件**;⚠️ **但 epic 未全闭** —— story-003 未开工(见下方 EPIC 表)。

### 交付物
- **生产**:`unity/Assets/Gameplay.UI/Skeuomorphic/DentchDoseSelector.cs`
  —— `DentchDetent`(实现 `IFocusable`)· `PrescribeStrokeIntent` / `PrescribeStrokeResult`(呈现 → 11 唯一输入形状)
  · 档位序列生成 / `DoseValueOf` 档值换算 / `LoadInto(BrassScaleElement)` 黄铜刻度装载
- **测试**:`unity/Assets/Tests/PlayMode/PrescriptionMedication/dentch_input_test.cs`(**49 条**)
- **装配**:`unity/Assets/Tests/PlayMode/PlayMode.asmdef` 增 `Gameplay.UI` / `Gameplay.Presentation` 引用
- **证据**:`production/qa/evidence/review-prescription-story-005-2026-10-06.md`

### 单轮评审 → 修复轮(要点)
- **结构侧** `APPROVED WITH SUGGESTIONS`(0 BLOCKING · 3 MAJOR · 7 MINOR · 3 NIT)· **QA 侧** `APPROVED WITH SUGGESTIONS`(0 BLOCKING · 4 MAJOR · 7 MINOR · 3 NIT)
  —— **两位评审均无 BLOCKING 落在生产件上**,全部 MAJOR 集中在**测试证伪力**与**登记完备性**
- **MAJOR-1(共指)** `Assert.AreEqual(sel.Detents.Count, sel.TickCount)` = `x == x` **恒真重言**(同源同字段),跨组件绑定从未被验证
  ⇒ 生产件新增 `LoadInto(BrassScaleElement)` **真装载路径**;测试改 `test_loadInto_brassElement_tickCountIsBound`(**反控**:未装载前刻度数须为默认 0)+ null 负测
- **MAJOR-2(QA)** 「零第二 EventSystem」断言面 = `Gameplay.UI` 程序集,该程序集**不引用** `UnityEngine.EventSystems` ⇒ 谓词**结构上不可能为真** = 恒真空真;故事卡点名的**双 EventSystem 夹具未交付**
  ⇒ 新增夹具 `DualEventSystemProbe : EventSystem` + 谓词正控(正控 + 反控)
- **MAJOR-3(共指)** `IsFocusEnabled` 文档称「满档 / 缺药时为 false」但生成路径**恒传 true**,分支从未产生也从未断言;常规档 `IsWholeDose` / `PresentationLabel` 零断言
  ⇒ 门控位改**可从装载期注入**(`BuildDetents(..., isFocusEnabled)` + ctor 重载)+ 文档收窄「判定源归 20」;补 4 条注入/常规档断言
- **MAJOR-4(QA)** `FocusRank` 值域(AC-42-B1 满射)**实为空判据** —— `FocusBoundaryAssertions.AssertRankDataSurjective` 只查 null(其源码自陈「不限制具体数值范围」);变异 `focusRank: i` 可存活
  ⇒ 补真判据 `CollectionAssert.AreEqual(Enumerable.Range(1,K), ranks)` + 单射 distinct 计数
- **MAJOR-4b(QA)** TR-prescription-011 ③「禁忌命中 ⇒ UI 无差异」`[A]` 条**零证据且未登记**
  ⇒ 生产件 `PrescribeStrokeIntent` **结构上不含**禁忌 / 拦截 / 扣减语义 ⇒ DTO 洁净扫描 + 字段面负断言覆盖结构半边;端到端等价性登记 **NOT-RUN 7**
- **MINOR-1(共指)** 两条「正控」近恒真(调被测方法 / 复算正式期望值)⇒ 换 `test_positiveControl_mutatedReferenceDiffersFromProduction`(**独立参考实现 + 变异公式**)
- **MINOR-2(共指)** `PrescribeStrokeIntent` / `Result` 全库**零生产消费方**,而 XML 声称「唯一输入形状」当前为假且未登记 ⇒ NOT-RUN **5 → 7 处**;补同域性真断言
- **MINOR-4(QA)** 零降级读数扫描面**只收单文件**(真正挂角标的 `Brass/` 不在面内)+ 禁词含中文字面量经剥离后**永不可能命中**(死词条)
  ⇒ 扫描面扩到 `Brass/` 目录 · 删死词条 · 补词面正控(证可命中 + 注释须被剥离)
- **MINOR-6(结构)** `MAX_DOSE_DETENTS` 单位与 GDD `:497`「`hi − lo` 档数上限」**差一** ⇒ 生产件旋钮文档显式钉「**单位 = 落点数**(= 焦点路径长度,承 Fitts 理据)」;GDD 侧登记订正
- **MINOR-7(共指)** 「同键双触发禁止」/「焦点单栈门」`[A]` 条除 EventSystem 计数外无判据且未登记 ⇒ 由 MAJOR-2 夹具覆盖结构半边;运行期实跑登记 **NOT-RUN 4**
- **NIT-1(QA)** `RegisteredMaxDoseDetents` **自守其门** ⇒ 合成扫描改以测试内**字面量**为循环上界 + 断言常量值域
- **NIT-2(QA)** 故事卡 QA 行 `dose_range=(2,5) ⇒ 长度 3` 是**算术笔误**(权威公式 `hi−lo+1` = **4**)⇒ 测试内显式标注笔误并取 4(**未镜像错误**)
- **QA 追加** `PresentationLabel` 原生成 `"第 {i+1} 档"`(含阿拉伯数字)与 AC-11-12「零数字读数」张力 ⇒ 改**零数字**标签「戥子档」/「整剂」
- **QA 追加** 本目录受 **AC-42-F3 词面门**扫**原文(注释也扫)** ⇒ 生产件注释改写为释义表述 + 文件头加注该纪律(实测原稿 `world_billboard_test` 报 3 处)

### 验证(实测)
- filter(评审前):`unity/Logs/dentch_input_6.xml` = **41 / 41 passed / 0 failed**
- filter(修复后):`unity/Logs/dentch_fix_2.xml` = **49 / 49 passed / 0 failed**(41 → 49,补 8 条)
- PlayMode 全目录(修复后):`unity/Logs/dentch_playmode_full3.xml` = **85 / 85 passed / 0 failed**(77 → 85,新增 8 条随套件全绿)
- 全量 EditMode(修复后):`unity/Logs/dentch_editmode_full3.xml` = **2826 passed / 0 failed / 46 skipped / 1 inconclusive**(既有,非失败;含 AC-42-F3 词面门通过)
- **变异证明(五条全部 ≥ 1 红,逐条命中预期新断言)**:
  MUT-A `focusRank: i` ⇒ 1 红 `test_detents_focusRankIsSurjectiveAndInjective` ·
  MUT-C 标签回带数字 ⇒ 1 红 `test_presentationLabel_containsNoDigits` ·
  MUT-D 忽略门控注入 ⇒ 1 红 `test_focusEnabled_injectedFalse_propagatesToAllDetents` ·
  MUT-E `LoadInto` 空体 ⇒ 1 红 `test_loadInto_brassElement_tickCountIsBound`(**原稿恒真断言 0 红**)·
  MUT-F 越界抛改 `Math.Clamp` ⇒ 2 红(源码面 + 行为面);
  变异后 `diff /tmp/Dentch.orig.cs <生产件>` **无输出 = 干净回滚**

### ⬜ 待办 / 未闭登记(禁借绿 —— 覆盖缺口,非安全洞)
测试文件头显式登记 **7 处**(本轮由 5 处扩至 7 处):
1. **AC-11-18 ②** 42 侧「下一档」焦点落点缺失 + `hi` 档纸面反馈 —— BLOCKED-BY-42 元件落地;本件只证**消费方**序列长度
2. **手柄(无指针)路径** —— BLOCKED-BY 桌面调试集中轮 + ADR-013 假设 6 spike(半可信)
3. **AC-11-13 / AC-11-12 / AC-11-21 走查子项** —— 可判 ≠ 已判,须实现轮人工执行 + 签核(主创 / 主创+医学从业 / 音频 lead)
4. **真实 UX 夹具下的焦点单栈门实跑** —— 本件只做**结构**断言;引擎运行期实跑判据归 42 侧 spike
5. **`materia_lexicon.cooked` 真装载** —— 21a 产出方未落 C# 字段(承 story-004 NOT-RUN 3);本件以烘焙 JSON 源件 + 结构扫描走通
6. **`IsFocusEnabled = false` 分支的判定源** —— 门控位已可注入(本件有判据),但**判定源**(满档 / 缺药)归 20 库存扣减面,未落地 ⇒ 恒 true 是当前唯一实跑路径
7. **`PrescribeStrokeIntent` / `Result` 的生产消费方** —— 全库零生产接线;落笔 → 11 接线归 8 侧 `S-8.4` 路线甲 + 39 方笺页

### 跨故事缺口(本故事范围外,但影响判据完整性)
- **AC-11-18 ② 的实体元件**(42 侧「下一档」落点 + `hi` 档纸面反馈)本 epic **不实现** ⇒ 该 `[A]` BLOCKING 的 ② 半边**当前无判据**;建议 skeuomorphic-ui epic 收口时确认承担方
- **3 侧 float→int 量化实现**(input-system epic story 006/007)未落地 ⇒ 本件只断言入参形状为整数,**未断言量化真在 3 侧发生**
- **`ModalId` 闭集与 `PaperCloseup48` 的「勿混读」**两处警示本件均已通过(闭集仍 7 员 · 方笺不成新模态 · 第七员在集内且与本条无关)

---

## 📋 历史状态(2026-10-06)—— prescription-medication story-004(Prescribe 流程 —— 域检查、原子扣减与成长门)—— ✅ 收口 · commit `db369d0` · 已推送

### 交付物
- **生产**:`PrescribeFlow.cs`(五步编排:域检查 → F-11.1/F-11.2 求值 → 20 扣减 → 发事件 → 成长门)
- **测试**:`prescribe_flow_test.cs`
- **证据**:`production/qa/evidence/review-prescription-story-004-2026-10-06.md`

### 单轮评审 → 修复轮(要点)
- **结构侧** `APPROVED WITH SUGGESTIONS`(0 BLOCKING · 1 MAJOR · 6 MINOR · 4 NIT)· **QA 侧** `CHANGES REQUIRED`(0 BLOCKING · 4 MAJOR · 6 MINOR · 5 NIT)
- **M1(结构 · 唯一 MAJOR = 文档冲突非代码 bug)**:AC-11-05③ / Edge Case 写「给错药 ⇒ `SkillGrown` 零发出」,与 F-11.5 **BL-3 改判**(门不读 `treatable_by`)相抵 ⇒ **零代码改动**,订正 GDD `:942`/`:645` + story 卡 `:73` 三处陈旧字面为「**成长照发**」
- **m1(结构)** `IPortionsConversion` 把 `dose × per-dose` 推给兄弟 epic(与 GDD `:408`「求值在 11」冲突)⇒ 端口改 `PortionsPerDose(ItemKey)` 只查表,**乘法移回 11**(宽算防溢出 + 上下界守卫)
- **m2 / QA M1+M2** 两处**恒真反射断言**(比对固定类型名 / 手写类型数组)⇒ 改枚举 11 全成员**符号名**差集 + 真取构造签名形参(变异:声明 `SkillMul` ⇒ 2 红)
- **m4(结构)** `PrescribePorts` 不校验 `doseBase > 0`(错误推迟到步骤⑤,事件已进流)⇒ 装配期 fail-fast + 2 条负测
- **QA M3** 零浮点扫描**无正控**(改成恒空 ⇒ story-002/003/004 三处共享断言**全部真空绿**)⇒ 新增临时目录正控
- **QA M4** `HasPortions` 实参无界(全用 `PerDose = 1` ⇒ `portions == dose`)⇒ `LastHasPortions` 记录 + 2 条有界测(变异:传错实参 ⇒ 2 红,原稿 **0 红**)
- **QA m2/m3** `EvaluateGateHit` 的 `doseLegal == false` 分支永不执行 ⇒ 补直调负支;五处空值守卫无测试 ⇒ 补 5 条负测
- **QA m6** 同 tick 双剂「Seq 各不同」未实现也未登记 ⇒ 文件头 NOT-RUN 5 处 → **6 处**
- **m5/m6/n1/n2/n4**:测名名副其实化 · 混堆注释收窄(集合选择归 20)· `TreatmentEvent != null` · `Seq` 占位显式断言 · `StripComments` 统一剥注释+字符串

### 验证(实测)
- filter(修复后):`unity/Logs/prescribe_flow_5.xml` = **53 / 53 passed / 0 failed**(原 41 条 → 补 12 条)
- PrescriptionMedication 全目录:`prescribe_flow_all4.xml` = **103 / 103 passed / 0 failed**
- 全量 EditMode:`prescribe_flow_full5.xml` = **2826 passed / 0 failed / 46 skipped / 1 inconclusive**(既有,非失败)
- **变异证明**:MUT-B 2 红 · MUT-C 2 红 · MUT-D 7 红 · MUT-E 2 红 · MUT-F 1 红(后两者原稿 **0 红**);变异后均 `diff` 验证干净回滚

### ⬜ 待办 / 未闭登记(禁借绿)
- **NOT-RUN 6 项**:AC-11-16 正式对拍(BLOCKED-BY-30 实现)· AC-11-15 三格矩阵(ADR-012)· 换算表真源(21a 未落 C# 字段,BLOCKED-BY-OQ-11-10)· 省料数值(BL-6)· 非主机传输(BLOCKED-BY-45)· 同 tick 双剂 Seq(归 45/7a)
- **跨故事缺口**:story-003 卡要求的 `drug_event_test.cs` **全库不存在** ⇒ AC-11-01① / AC-11-22 / AC-11-10 assembly 面**三处 BLOCKING 无真判据**,story-003 收口前不得记为已有证据

---

## 📋 历史状态(2026-10-06)—— prescription-medication story-003(F-11.2 半衰期)—— ✅ 收口 2026-10-06 · 已提交推送

### 交付物
- **生产**:`HalfLifeCalculator.cs`(F-11.2 半衰期计算器,走 `Fix.operator+` 加法)
- **测试**:`half_life_test.cs`(**24 条**)· `PrescriptionFloatScan.cs`(零浮点扫描共享实现)
- **证据**:`production/qa/evidence/review-prescription-story-003-2026-10-06.md`

### 单轮评审 → 修复轮(要点)
- **结构 B-1**: raw `long` 加法绕过 `Fix.operator+` 的 `checked` 溢出保护(静默回绕;IL2CPP 下 UB)
  ⇒ 改走 `Fix effective = axisBase + offset;`(`Fix.cs:124-127` 的 `operator+` 抛 `OverflowException`)
- **结构 M-1**: `CalculateForDrug` 纯透传无价值 ⇒ 改为读 `DrugProfile` 的真实组合入口(可空校验)
- **结构 M-2**: 缺溢出行为测试 ⇒ 补 3 条(正向/负向/边界)
- **结构 m-1/m-2/m-3**: 误导性注释删 · 扫描面加注说明 · `EffectiveQuality` 透传注释
- **QA M1/M2 + m1~m4/m7/m8**: 溢出显式检测 · `Assert.Ignore`→硬失败 · 扫描面扩至 `ToFloat()`/`Math.*`/`decimal`/大小写不敏感 · 补上界与单元素测试
- **修复轮连带发现(非评审提出)**: 两测试共享扫描面的**重复实现漂移** ⇒ 抽共享 `PrescriptionFloatScan.Scan()`

### 验证(实测)
- filter:`unity/Logs/half_life_fix4.xml` = **59 / 59 passed / 0 failed**(PrescriptionMedication 全目录)
- 全量:`unity/Logs/half_life_full3.xml` = **2773 passed / 0 failed**(exit 2 = 既有 Inconclusive)

### ⬜ 待办 / 未闭登记(禁借绿)
- **NOT-RUN 3 项**:AC-11-08 ②(21a 构建期断言不存在,BL-1)· AC-11-15(三格矩阵,ADR-012 未实跑)· TR-prescription-008(21a 半边)

---

## 📋 历史状态(2026-10-06)—— prescription-medication story-001(处方表与本草词表 —— 双表 polarity 硬门)—— ✅ 收口 2026-10-06 · 已提交推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:结构侧 CHANGES REQUIRED(3 BLOCKING + 3 MAJOR + 3 MINOR + 2 NIT)+ QA 侧 NOT APPROVED(3 BLOCKING + 3 MAJOR + 2 MINOR + 1 NIT),
> **6 BLOCKING 双侧**;全部落点 ⇒ 复跑绿。

### 交付物
- **作者态**:`assets/data/prescription_actions.json`(处方表种子)+ `assets/data/materia_lexicon.json`(本草词表种子)
- **生产**:`PrescriptionActionsBaker.cs`(仓根装载器)· `PrescriptionActionsBinder.cs`(阶段 2 绑定 + **唯一**校验点 DC-1/DC-3/DC-5/DC-7/AC-11-20)·
  `PrescriptionActionsCookedWriter.cs`(确定性写入器)· `PrescriptionActionsBinderProbe.cs`(薄转发)·
  `DataBakeMenu.BakePrescriptionActions`(菜单调用点)
- **测试**:`prescription_tables_test.cs`(**12 条**)
- **证据**:`production/qa/evidence/review-prescription-story-001-2026-10-06.md`

### 单轮评审 → 修复轮(要点)
- **结构 B-1**: 绑定器不读 `item_database_items.json` — DC-1/DC-5(覆盖)/DC-7(上界)/AC-11-20 在生产路径上未强制
  ⇒ `Bind` 签名加 `itemsJson` 参数,绑定阶段执行跨文件校验
- **结构 B-2**: AC-11-02 注释误导(说"扫描源文本"但无代码)⇒ 改为说明"由 RejectUnknownKeys 隐式满足"
- **结构 B-3**: 测试用 regex 解析 JSON 驱动断言,而非驱动生产绑定器 ⇒ 全部改为调用 `PrescriptionActionsBinderProbe.Bind`
- **QA B-1**: `test_bakeDeterminism` 恒真(只比较两次 ReadAllText)⇒ 改为调用 `BakeFromRepo` 两次比较字节
- **QA B-2**: `test_dc7_doseBaseWithinUpperBound` 空集真空真 ⇒ 内联构造带非 null dose_range 的夹具
- **QA B-3**: 四条负夹具是正向测试复制品 ⇒ 构造违反条件数据喂给 Binder 断言拒绝

### 验证(实测)
- filter:`unity/Logs/prescription_tables_fix5.xml` = **12 / 12 passed / 0 failed**
- 全量:`prescription_tables_full.xml` = **2796 / 2749 passed / 0 failed / 46 skipped / 1 inconclusive**

### ⬜ 待办 / 未闭登记(禁借绿)
- **NOT-RUN 3 项**:DC-2(action_id 闭集,依赖 OQ-11-2)· DC-6(依赖 9 侧 NOISE_BAND_9)· AC-11-07(双表 polarity 交叉硬门,依赖 9 侧 disease_registry.json)
- 下一件:prescription-medication **story-003**(F-11.2 半衰期),同协议

---

## 📋 历史状态(2026-10-05)—— diagnosis-system story-003(F-8.1 可读地板与 F-8.2 精度档槽)—— ✅ 收口 2026-10-05 · 已提交推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:结构侧 CHANGES REQUIRED(1 MAJOR + 5 MINOR)+ QA 侧 CHANGES REQUIRED(1 MAJOR + 6 MINOR),
> **0 BLOCKING 双侧**;2 MAJOR + 11 MINOR 全部落点(**全为文本/文档 + 判据强度,零运行期改动**)⇒ 复跑绿 + 两处新 MUT 证明。

### 交付物
- **作者态**:`assets/data/diagnosis_read_floor.json`(合成系数:base_read=1 · read_floor_min=1/4 · read_gamma=2;**数值归用户数值轮**)
- **生产**:`DiagnosisReadFloorBinder.cs`(阶段 2 绑定 + **唯一**校验点 C-1/C-5/skill_cap)·
  `DiagnosisReadFloorBaker.cs`(仓根种子 → 产物)· `DiagnosisReadFloorCookedWriter.cs` +
  `DiagnosisReadFloorCookedCodec.cs`(严格镜像;固定头 **32 B**)· `DiagnosisReadFloorBinderProbe.cs`(薄转发)·
  `DiagnosisReadFloorTable.cs`(运行期定表 + `DiagnosisReadFloorEvaluator` 求值器 + `DiagnosisSlot`/`SignReadState` 枚举)·
  `DiagnosisChannelMaskMap.cs`(**Note 6** 通道序数↔位掩码映射 + 双向断言,接生产路径)·
  `DataBakeMenu.BakeDiagnosisReadFloor`(菜单调用点)
- **测试**:`read_floor_slots_test.cs`(**28 条**)· `DiagnosisGoldenScan.cs`(story-002/003 **共享**金标扫描真源 —— 兑现 story-002 头注「扩金标」承诺)·
  `tests/unit/diagnosis_system/fixtures/read_floor_*.json`(**10 夹具**)+ README 账本补 Story 003 段
- **金标**:`GoldenConstantsHash = f75a8170`(`b9354110` s002 → `5bba361c` s003 扩枚举/映射面 → `f75a8170` 修复轮,
  由 `FixedHeadBytes` 36→32 的**有意识**代码常量修正驱动)
- **证据**:`production/qa/evidence/review-diagnosis-story-003-2026-10-05.md`

### 单轮评审 → 修复轮(要点)
- **MAJOR-1(结构)** AC-8-35 金标「覆盖」声明 **over-claim**:`read_floor_slots_test` 写「F-8.1 参数半边转 covered」
  与 `sign_table_test` 头注矛盾 ⇒ 四处统一为准确边界(扫描面 = 前缀 ns **代码常量** + DIAG_TIERS;
  **曲线参数**住 `assets/data/*.json` 由 ConfigVersion 覆盖,**仍 NOT-RUN**)
- **M-1(QA)/MINOR-3** `README.md` 缺 Story 003 段(标称夹具 21 实存 **31**;无 AC→测映射;未登记 NOT-RUN)⇒ 补全段
- **MINOR-1(结构)** codec `FixedHeadBytes` 36 → **32**(注释双错订正;E-13 文本阈值)
- **MINOR-4(结构)** GDD §F-8.2 回退散文**示例**方向反 + 永不触发(`sign_rales` 粗档实有词)⇒
  登记 **GDD 散文勘误**(待设计轮),就地注 `DisplayWord` doc;**不改 GDD 权威件**
- **MINOR-5** 删死 import(`DiagnosisReadFloorTable.cs` `Sim.Contracts`)
- **m-1** `test_ac89` 去恒真 `DoesNotContain(...,99)` ⇒ 改断**枚举值域**(零锁闭成员)+ 五手段**各有真读数**(`DisplayWord` 非 null)
- **m-2** `test_g1` 去同参自等循环(纯函数必等)⇒ 改断**表项=存储值**(`FloorAt`) + 跨档区分(非退化)
- **m-3/m-4/m-5** 反射幂名清单扩 9 名 + 明写「真守卫在边界门」· AC-8-7 代理判据登记 · AC-8-46「词变粗」NOT-RUN 登记
- **m-6** story Test Evidence / Status / AC 勾选回填

### 设计决定(2)
1. **空白档回退方向 = 严格向下**(GDD §F-8.2 规则字面;koplik 粗/中为空 ⇒ 粗/中档读不出,细档起出词)。
2. **金标两次重钉均有意识**(扩面 + 修复轮常量修正),理由已登记。

### 验证(实测)
- filter:`unity/Logs/s003-fix3.xml` = **94 / 93 passed / 0 failed / 1 skipped**
- 全量:`s003-fixfull.xml` = **2630 / 2583 passed / 0 failed / 46 skipped / 1 inconclusive**
- MUT-D(`DisplayWord` 下界 `i>=0`→`i>=1`)⇒ **恰 3 红**(含 `test_ac89` —— 证 m-1 增强判据承重;旧 `DoesNotThrow` 版不红)
- MUT-E(`FloorAt` 返 0f)⇒ **恰 1 红**(`test_g1` —— 证 m-2 增强判据承重;旧自等版不红)

### ⬜ 待办 / 未闭登记(禁借绿)
- **NOT-RUN 6 项**(本 story):AC-8-F3 σ 联动(归 disease 004)· AC-8-9 EmitGrowth 门控(story 005)·
  AC-8-46「词变粗」+ 病名半边(story 006/37)· 跨会话/跨平台逐位(AC-8-F5/story 004)·
  AC-8-35 曲线参数半边(ConfigVersion 覆盖)· AC-8-7 AC 字面浮点用例(待 `Precision`)
- **GDD 散文勘误 1 项**:`diagnosis-system.md` §F-8.2 回退示例(方向反 + 永不触发),待设计轮
- **TR-registry 回填**(TR-diag-008/009/011/012 等 gap→covered)= 独立 docs 轮,未动
- 下一件:diagnosis-system **story-004(F-8.3 阴性把握度与不泄漏不变量)**,同协议;
  epic 链 diagnosis(**3/6**)→ case → prescription

## 📋 历史状态(2026-10-05)—— diagnosis story-002(体征词条表 schema 与 P0 数据行)—— ✅ 收口 · commit `f27ca3e` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:结构侧 CHANGES REQUIRED(1 MAJOR + 10 MINOR)+ QA 侧 CHANGES REQUIRED(2 MAJOR + 10 MINOR),
> **0 BLOCKING 双侧**;3 MAJOR + 20 MINOR 全部落点 ⇒ 复跑绿 + MUT 变异证明。

### 交付物
- **作者态**:`assets/data/diagnosis_signs.json`(R-8.2 冻结 34 行 = 阳 30 / 阴 4)
- **生产**:`DiagnosisSignTable.cs`(validator + 三枚举 + `SlotBounds` **首次成文**)· `DiagnosisSignBinder`
  (**唯一 `Validate` 调用点**,聚合硬失败 + 空表拒收)· Baker / Writer / Codec(严格镜像 +
  schema 期望比对 + count 钳制 + 枚举序数界)· Probe · 菜单 `BakeDiagnosisSigns`
- **测试**:`sign_table_test.cs`(**42 条**)+ `tests/unit/diagnosis_system/`(**21 夹具** + README 账本)
- **双金标**:`GoldenConstantsHash = b9354110`(AC-8-35)· `GoldenContentHash = 3954e294`(R-8.2 内容逐字)
- **证据**:`production/qa/evidence/review-diagnosis-story-002-2026-10-05.md`

### 单轮评审 → 修复轮(要点)
- **S-MAJOR** `SignChannel` 同名不同物(本表序数 vs `Sim.Contracts` AC-21 位掩码)→ 枚举 doc 警示
  + **story-003 Note 6 登记映射表与双向断言义务**(未立映射前禁 cast)
- **Q-MAJOR-1** R-8.2 逐行内容无守卫 → `test_r82_contentFrozen_golden` 内容金标
- **Q-MAJOR-2** F-8.1/F-8.3 参数半边零覆盖 → 头注 + story AC 显式 NOT-RUN
- **MINOR ×20 全落点**:binder `catch (Exception)`(防 `/0` 逃逸)+ 空表拒收 + BindResult doc ·
  codec schema/钳制/序数界 · `SlotBounds` 收只读视图 · writer CS0104 别名 · 文件族名注 ·
  story-004 占位值预警 · `BakeFails` **恰一条排他** · P1a 8 值 TestCase 全值 · +4 违例夹具 ·
  tier 触底 / C-6 双标 / addRow 绑金标 / 切点上下文 / FormatConst 兜底 · README 账本 · Test Evidence 回填
- **两处 story 文本订正**(实现前对账登记):依赖「disease story 003 载体」不实 → `Editor.Tools.Bake` 模式;
  Note 1 三态可分错引 AC-8-F → **AC-8-21(+ V-8.2 / AC-8-24)**
- **MUT-Validate**:注释唯一调用点 ⇒ **恰 8 条校验器路径红**(tier35/tier15/阳性带权/阴性缺权/
  阴性零权/重复主键/lab/病史白名单),绑定层全绿 —— 承重面从推断变实测

### 验证(实测)
- bootstrap:`unity/Logs/s002-bootstrap.xml`(金标 PENDING→打出实际值;途中自查出 reveal
  TestCase 传裸串 bug,改数组后绿)
- filter:`s002-run2.xml` = **67 / 66 passed / 0 failed / 1 skipped**(skipped = 反向孤儿 [Ignore])
- MUT:`s002-mut-validate.xml` = **恰 8 红**;还原后复绿
- 全量:`s002-full.xml` = **2603 / 2556 passed / 0 failed / 46 skipped / 1 inconclusive**(基线 2561 + 42)

### ⬜ 待办 / 未闭登记(禁借绿)
- **NOT-RUN 8 项**全表见评审原件 §4:反向孤儿(BLOCKED disease 006)· 正向接线(tripwire)·
  F-8 金标半边(story 003/004 扩)· 跨会话确定性 · 人工核对字面 · cooked.bytes 数据轮口径 · …
- `neg_weight = "1"` 占位已挂 story-004 Note 7;枚举↔位掩码映射已挂 story-003 Note 6
- **TR-registry 回填**(TR-diag-014 等 gap→covered)= 独立 docs 轮,未动
- 下一件:diagnosis-system **story-003(F-8.1 可读地板 + F-8.2 精度档槽)**,同协议;
  epic 链 diagnosis(2/6)→ case → prescription

## 📋 历史状态(2026-10-05)—— diagnosis story-001(程序集边界与 VitalsDto 只读门面)—— ✅ 收口 · commit `6685046` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:双代理评审 **结构侧 CHANGES REQUIRED(3 MAJOR)+ QA 侧 APPROVED(2 MAJOR 收口前置)**,
> 无 BLOCKING;5 MAJOR + 8 MINOR 逐条修复 ⇒ 复跑绿。

### 交付物
- **生产**:`DiagnosisVitalsFacade`(静态零字段唯一取数门面)· `DiagnosisGrowthExit`(7 参纯转发
  出口,IL 调用点恰=1)· `DiagnosisBoundaryGates`(Cecil IL + 源文本 + 逃逸,11 个 tag)·
  `AssemblyGates` RunMenu + BuildGate 接线(reload hook 刻意不加,承 Required-7c)
- **测试**:`unity/Assets/Tests/EditMode/DiagnosisSystem/boundary_guard_test.cs`(**25 条**,
  含 EOF 真 IL 负例夹具)
- **证据**:`production/qa/evidence/review-diagnosis-story-001-2026-10-05.md`

### 单轮评审 → 修复轮(结构 3 MAJOR + QA 2 MAJOR + 8 MINOR)
- **S-1** `Mathf` 双层补入(G-4 邀请式绕行)· **S-2** `IModifierType` 修饰符侧遍历(b5 Required-2)
- **S-3** `DateTimeOffset`/`Stopwatch`/`TickCount` 时钟补全 · **S-4** 深度超限改落红
- **S-5** 缺失不叠报 + `[D-TREF]`→`[D-0]` tag 统一 · **S-6** 哈希段标结构占位(禁称已生效)
- **S-7** 消费者住前缀纪律登记 · **S-8** `IVitalsQuery` 成员集锁 · **S-9** BuildGate WARN 口径
- **Q-1** 全量复跑 · **Q-2** AC 逐条括注 NOT-RUN · **Q-3** `RecipeDataSet` 替代括注
- **Q-4** 铁律③ 判据等价性(IL+源 ⊃ 反射)入 Completion Notes · **Q-5** 三处 `Is.Not.Empty` 自证
- **Q-6** 漏测分支负例补齐(`Fix.One` 字段 / 源层 `IEventSink`+`FixParse` / `Mathf` / 时钟三族)

### 验证(实测)
- 过滤:`unity/Logs/s001-diag-fix.xml` = **25 / 25 passed / 0 failed**
- 全量:`unity/Logs/full-diag-001.xml` = **2561 total / 2515 passed / 0 failed / 45 skipped /
  1 inconclusive**(增量 115 = patient-ai s003+s004+本批;根 `Skipped:Ignored` 与基线 019d 同态)

### ⬜ 待办 / 未闭登记(禁借绿)
- 残余 NOT-RUN 全表见评审原件 §四:AC-8-1 全流程脚本+哈希鉴别力(005/006)· 跨 epic 基线(CI)·
  AC-8-3 四输出(002/003)· AC-8-6 定表+G-4 IL2CPP(003/004)· AC-8-4 11 侧+D-11 词面回补
  (prescription story 003)· `typeof(Fix)` 构造性绕行复查(11/005 轮)
- **TR-registry 回填**(TR-diag-004 gap→covered、002/010 partial 等)= 独立 docs 轮,未动
- 下一件:diagnosis-system **story-002(词条表 schema)**,同协议;epic 链 diagnosis → case → prescription

### 前一收口(同日,已推送)= patient-ai story-004 `0b6f948`
- 4/4 全闭;过滤 168/166/0/2;残余 AC-13-V8(BLOCKED-BY 45)· CrossPlatform(CI)·
  [L] 五档(BLOCKED-BY 42/44)。⚠️ patient-ai EPIC 行仍 `Ready`(先例账,归该 epic 收尾轮)。

---

## 📋 历史状态(2026-10-05)—— 拟物 UI story-019-d(接图落地 + C4 消红)—— commit `155a9b1` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:上一轮评审后用户裁定「**先清 44 条 C4,再收口 019-d**」+「**抽主题变量 + 铜色,一并解决 4 条 C4**」。

### 交付物
- **接图(C7 接图半)**:四基类 `paper`/`scroll`/`ink`/`seal` 加 `background-image: url("guid:…")`;
  `.paper-aged` 变体随 `paper` 落地 ⇒ **落地 5 处选择器 / C7 判据面 4 注册项**
- **判据收窄**:`SkeuoComponentRegistry` 增列 `IsTextureContainer`(默认 `false`,须显式传 `true`)
  ⇒ C7 判据 = 「贴图容器类」(2026-10-05 用户裁定,取代首轮窄读法 4 类)
- **C4 消红 44 → 0**:`SkeuoThemeVariables.uss` layer **5 → 9 员**(brass/implement/marks/focus 抽变量)·
  `brass-bg` 取 art-bible §4.1 权威值 **`#B8863B`**(订正原内联 `#b87333`,绿通道差 19,非本项目裁定值)·
  `.brass-scale` 底色与 border **解耦**(评审 M4,独立命名 `--skeuo-brass-scale-color`)·
  焦点载体 **墨 → 铜**(承 `art-bible §7.4` Amendment + GDD 规则十注记)
- **夹具补真缺口**:新增 `validate_all_aggregate_test.cs`(**7 条**)—— `ValidateAll()` 此前**全 `Tests/` 零调用**,
  C1/C2/C4/C5/C6 在 CI **长期无覆盖**;`texture_binding_gate_test.cs` 两处 `null` 桩改真
  `AssetDatabase.GUIDToAssetPath`(019-c 遗留,接图后误判悬空)
- **文档订正 4 处**:「贴图容器 5 项」→「**4 注册项 / 5 落地选择器**」(story-019 ×2 · art-assets ×1 · GDD ×1)

### 验证(实测)
- 过滤:`unity/Logs/skeuo-019d-final.xml` = **224 / 211 passed / 0 failed / 13 skipped**
- 全量 EditMode:`unity/Logs/full-editmode-019d.xml` = **2446 / 2402 / 0 / 43 / 1**(基线 2445 ⇒ **+1,零回归**;
  唯一 inconclusive = 既有 `SettingsExposureTest.test_monoOption_existsWithValidDefault`)
- **门探针**:复刻 `RunMenu()` 契约(反射先调 private `InitializeDefaults()`)⇒ `ValidateAll()` = **0 条 · VERDICT=GREEN**
  (探针已删,日志 `unity/Logs/probe-019d-build.log`)
- **变异**:删 `.ink` 接图 ⇒ C7 精确报 `.ink` 缺贴图;还原 `#b87333` ⇒ C4 精确报 line 4;均还原后全绿

### ⬜ 待办 / 未闭登记(禁借绿)
- ⚠️ **019-d 残余义务**:`-unity-slice-*` 的**运行期实测 + 截图签核**仍未做 —— 现仅断言「有引用」,
  **证明不了「贴对了」**;切片值待 019-f 冻结件(故 C8 未闭)
- ⚠️ **未闭色值(待裁)**:`--skeuo-brass-aged`(`#A0653A`,注释自称「铜锈」)与 art-bible §4.1/§8.6.3 的
  铜锈 `#4F7A6B`(青绿)**语义冲突**;`#8C5A2B` art-bible 全文无出处。经 `git show HEAD` 确证均为**既有值**,
  本轮保值抽变量**未纠正**,已就地加警示注释
- **019-f(C8 冻结件)** = BLOCKED-BY 美术(九宫格 slice 真值;现 `spriteBorderActual=(0,0,0,0)`)
- **019-b(图集预算 C9)** = Blocked,`PAGES_MAX` 未冻结
- m1:`seal_red` 全库零挂载
- 下一件:**见 Phase 2 关键路径**(interaction-system 已 7/7 全闭;余 patient-ai / diagnosis / case / prescription)

## 📋 历史状态(2026-10-05)—— 拟物 UI story-019-e(导入格式订正)—— commit `7739142` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:用户裁定「拆成 DEF,c7 范围窄读法写死」⇒ 019-d 拆为 d(接图,美术)/e(格式,零依赖)/f(C8 冻结件,美术)。

### 交付物
- **生产**:16 个 `*-final.png.meta`(spriteMode 0→1 = **Single** · textureType 0→8 · alphaIsTransparency 0→1;`spriteBorder` 留零哨兵)·
  `TextureBindingGates.cs` 增 `ValidateSlicedTextureImportFormat` + `ValidateSpriteBorderLeftAsSentinel`
- **测试**:`texture_binding_gate_test.cs` **+5 条 E1 夹具**(22 条本件)

### 单轮评审 → 关键取证(QA 侧 BLOCKING 被实测推翻)
- QA 侧判「`spriteMode:1` = Multiple ⇒ 与单图九宫格矛盾」(据本机文档**文本顺序**推断)
- **独立取证推翻**:运行期反射 `SpriteImportMode` = **`None=0/Single=1/Multiple=2/Polygon=3`** ⇒ `spriteMode:1` **= Single**;
  引擎回读 16 张全 `mode=Single · spriteCount=1`(临时探针,日志 `unity/Logs/probe-enum.xml` / `probe-sprite-mode.xml`,探针已删)
- **但暴露真缺陷(文档级)**:原文括注 `(Multiple)` 是**误标**(自 commit `6049204` 引入,从未核过)
  ⇒ **已修** story-019 §AC-42-E1 + `art-assets-required-for-019`(改为「= `SpriteImportMode.Single`」+ 枚举真值)
- **绿**:过滤 217/204 passed/0 failed · 全量 EditMode 2439/2395/0/43/1(基线 2434/2390,+5 零回归)
- **变异**:MUT-E1a(spriteMode 回落)⇒1 红 · MUT-E1b(border 注入非零)⇒1 红(日志留档)
- **原件**:`production/qa/evidence/review-skeuomorphic-ui-story-019e-2026-10-05.md`

### ⬜ 待办 / 未闭登记(禁借绿)
- **019-d(接图)** = BLOCKED-BY 美术(逐变体映射语义;导入格式前提已由 019-e 解除)
- **019-f(C8 冻结件)** = BLOCKED-BY 美术(九宫格 slice 真值;现 `spriteBorderActual=(0,0,0,0)`)
- **019-b(图集预算 C9)** = Blocked,`PAGES_MAX` 未冻结
- 未闭:018-e 的 `-unity-slice-*` **运行期实测**归 019-d(截图签核)· `nPOTScale` 无门覆盖(登记为引擎派生值)
- 下一件:见 Phase 2 关键路径

---

## 📋 历史状态(2026-10-05)—— 拟物 UI story-019-c(贴图接入护栏)—— commit `a10fa6c` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。

### 交付物
- **生产**:`unity/Assets/Editor.Tools.Gates/TextureBindingGates.cs`(纯逻辑,零引擎依赖,照 `PresentationDtoGuard` 先例)·
  `SkeuomorphicUiGates.cs` 门聚合(C10/C11/C7 骨架半 → `ValidateAll`)
- **测试**:`unity/Assets/Tests/EditMode/SkeuomorphicUI/texture_binding_gate_test.cs`(**17 条**)

### 单轮评审(结构侧 unity-specialist + QA 侧 qa-lead)→ 修复轮
- **两侧独立收敛同一根因 = C10 主入口假绿**:`Directory.GetCurrentDirectory()` 在 Unity CLI EditMode 下
  = **`<repo>/unity`(工程根)**,非 `<repo>`;门以 `Path.Combine(cwd,"Assets",…)` 拼路径 ⇒ 拼不中 ⇒ 扫描**静默空跑**
  ⇒ `Assert.IsEmpty` 恒真。**变异测试证伪不了这类假绿**(注入物与扫描面同落泄漏目录)。
  铁证 `unity/Logs/probe.xml:46`;同族订正先例 `modal_gate_test.cs:502`(早已自陈「cwd = unity/」)
- 修复:① `DefaultRepoRoot` 上溯寻含 `Assets/` 的那层;② 三扫描函数加**反空跑守卫**(缺失/空 ⇒ 硬报错);
  ③ `UrlTargetsTextures` 由死代码降为诊断分级;④ 新增 5 条夹具(12→17)
- **变异**:MUT-C10 / MUT-C11 各 **2 红**(含真扫描面锚 —— 证 B1 关闭),两变异文件均还原
- **绿**:过滤 212/199 passed/0 failed · 全量 EditMode 2434/2390 passed/0 failed/43 skipped/1 inconclusive(基线 2429/2385,+5 零回归)
- **原件**:`production/qa/evidence/review-skeuomorphic-ui-story-019c-2026-10-05.md`

### 同批产出 = 019 完全实现所需美术资产清单
- `production/qa/evidence/art-assets-required-for-019-2026-10-05.md`
- **结论**:美术侧瓶颈**仅 3 件** —— ① 五族九宫格切图边界元数据(冻结件)· ② 逐变体映射语义裁定 ·
  ③ 铜族焦点黄铜 2px 最小切片(**唯一新出图**;M2 硬前置,E 裁)。16 张主贴图早已入库,非缺口。
- **实测附加发现**:元件库 USS 实有 **24 个类选择器**,而 `SkeuoComponentRegistry` 仅登记 4 类
  (记号族住 `MarkRegistry`;黄铜/器具/焦点族**零登记表**)⇒ **C7「已注册类」范围待裁**(决定 019-d 接图量 4 vs 24)。

### ⬜ 待办 / 未闭登记(禁借绿)
- **019-d(接图)** = BLOCKED-BY 美术(冻结件 + 映射裁定 + 导入格式订正 16 `.meta` 全 `spriteMode:0`)
- **019-b(图集预算 C9)** = Blocked,`PAGES_MAX` 未冻结(ADR-013 §6.6 假设 6 spike 未跑)
- **待裁**:C7「已注册类」范围 · AC-42-C7/C8/C9/C10/C11 未入 GDD(架构侧治理项)· C11 未覆盖 `resource()`/`.uxml`
- 下一件:见 Phase 2 关键路径(patient-ai story-003 = ViewState 投影 / cue 发射 / Material 映射)

---

## 📋 历史状态(2026-10-04)—— patient-ai(13)story-001 协议步骤 2/5

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。
> **评审只做一轮**。

### 步骤 1 ✅ 已完成 —— 实现 + Unity CLI 测试
- **生产**(`unity/Assets/Gameplay.Presentation/PatientAI/`):`BehaviorBands.cs` · `BehaviorState.cs` ·
  `BehaviorMap.cs`(决策核心,修掉了 `_ = trend;` 不可达缺陷)· `PatientBehavior.cs`(滞回 + Terminal 闩锁 + 三判据)·
  `PresentPatientView.cs`(F-13.6 优先级表)· `PatientCue.cs`(13 命名空间:纯函数核 + `CueKind`/`CueIntervals`)·
  **`PatientCueEmit.cs`**(44 前缀命名空间:唯一发射桥)
- **测试**:`unity/Assets/Tests/EditMode/PatientAI/behavior_map_test.cs`(**38 条** —— 含后补的
  `test_ac13a5_trendIsInertInMap_branching`(MUT-B 首轮 0 红暴露的缺口)+ NaN/±Infinity 边界)
- **⚠️ 本轮最重的接线发现 = AC-44-B1 ① / AC-44-B3 双门**:
  ① **b5① 逃逸谓词按文件判** —— 含 44 契约 token(`AudioCueDto`/`IAudioCueSink`)的文件**必须且只能**
     声明 44 前缀命名空间 ⇒ 13 的调度面与发射桥**拆成两个文件**;
  ② **AC-44-B3 入口白名单扫「44 前缀下每个类型的公开方法签名」** ⇒ `Emit` 签名缩成**纯基元**
     (`int patientId` / `int kind` / `int cellX/Y/Z`),13 命名空间类型一律在**体内**装配。
- **绿**:patient-ai 38/38 · 全量 EditMode **2375 / 2331 passed / 0 failed / 43 skipped / 1 inconclusive**
  (inconclusive = 既有 `SettingsExposureTest`,与本 story 无关;全量数取自 36 条时点,38 条后未重跑全量)
- **变异证明**:MUT-A(删夹取)⇒2 红 · MUT-B(给 `Map` 加 trend 分支)⇒ 首轮 **0 红(缺口)**,
  补 `test_ac13a5_trendIsInertInMap_branching` 后 ⇒ 红 · MUT-C(删 Terminal 闩锁)⇒2 红

### 步骤 2 ✅ 已完成 —— 双代理评审(单轮)
- 结构侧(`aa64bdaa32ad46640`)= **CHANGES REQUIRED**(无 BLOCKING;F-1…F-7)
- QA 侧(`a21992cb2ace8b063`)= **REJECT**(2 BLOCKING:B1 扫描根阴性恒真 · B2 只证「持了接口」;
  M1/M2 · m1/m2/m3 · NOT-RUN 10 条)
- ⚠️ 两位曾停在轮次上限(转录 idle ≈73 min),按「子代理满轮可以接着再送」显式重送后收回
- **原件落盘**:`production/qa/evidence/review-patient-ai-story-001-2026-10-04.md`

### 步骤 3 ✅ 已完成 —— 修复轮(逐条落实 + 变异坐实)
- **B1/M2**:`ScanClosureForNames` 生产根改 `ProductionScanSeeds()`(13 前缀 **∪** 44 桥前缀)
  + 新增 `test_ac13a1_scanRootCoversAudioBridgeNamespace_b1` 锁根枚举
- **B2**:新增 `test_ac13a2_vitalsProducedOnlyByIVitalsQuery_sourceClosure` + 负夹具
- **F-3/M1**:`test_ac13b5_sessionWriterIsUnique_reflection` 重写为 **IL 写入点扫描**
  (`ScanSessionWriters`:stfld 后备字段 ∪ call set_Session)+ 负夹具 `ShadowSessionWriter`
- **F-2**:`PatientBehaviorDirector` ctor 调 `Validate`,破表 `throw ArgumentException`
- **m3**:`BehaviorBands.Validate` 补 `SeekMin < DeathBandMin` 独立项
- **m1**:trend 惰性断言补带符号邻域球(±1e-6/±1e-3/±0.01/ε)
- **m2**:`ResetForLoad` 补方向对照负夹具
- **M2 注释订正**:如实声明扫描器归一化口径 ≠ `PresentationDtoGuard.NormalizeMemberName`

### 步骤 4 ✅ 已完成 —— 复跑绿
- **patient-ai 45/45**(38 → 45,+7)· 全量 EditMode **2384 / 2340 / 0 / 43 / 1**
- **变异 5/5 各恰一条红**(MUT-B1/B2/F2/F3/m3,日志 `unity/Logs/mut-*.xml`)

### 步骤 5 ✅ 已完成 —— 收口提交推送(`4076e1e`,已 push)
- 22 文件 · 排除 `unity/Assets/unity.meta` + `unity/Assets/unity/Logs.meta` + `.gitignore`
- ⬜ 提交 → ✅ 推送 origin/main

### 待办(已闭)
- ✅ 评审回收 → 修复轮 → 复跑绿 → 收口提交推送(`4076e1e` / `eb8b718`)
- ✅ `sprint-04.md` §Phase 2 表与关键路径图更新(interaction 全闭、patient-ai story-001 已收口)
- ⛔ `unity/Assets/unity.meta` + `unity/Assets/unity/Logs.meta`(早前相对路径测试输出误建的空树,
  `Logs/` 本身已被 `.gitignore` 覆盖)—— 已排除出提交;删除需用户批准(`rm -rf` 被拒)

---

## ✅ patient-ai(13)story-002 —— 空间行为(轨 A)—— 收口 2026-10-05

### 交付物(4 生产件 + 1 测试件 = 78 条)
- `SpatialPerception.cs`(F-13.3 整数平方和禁 sqrt · F-13.4 LOD 三档 + 节流判据 · EC-13-02 量程护栏)
- `ClinicKnowledge.cs`(F-13.2 `HomeRegion` / `KnowsClinic` 两事实析取)
- `LogicalStepper.cs`(F-13.7 累加器步进 `acc` · `Moving(p)` 五合取项 · 防跳格断言)
- `PatientSpatialDirector.cs`(在场循环 · 升序求值 · `AtClinic` 格成员判定 · `PhaseOf` / `EcozoneOfCallCount` 可观测面)
- `spatial_behavior_test.cs` = **78/78 绿**

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(4 MAJOR)· **QA 侧 REJECT**(F-1 MAJOR + F-2/3/4 MAJOR)—— 两侧独立收敛到**同一组四条**
- 修复:**F-1 `AtClinic` 改回 GDD 的格成员判定**(注入 `Func<WorldPos,bool> inClinicCells`;
  旧实现用路径游标代偿 ⇒ 假阳/假阴/单格路径三向皆错,下游 F-13.6 `AwaitingCare` 直接受害)·
  **F-2 TC-7 换顺序敏感场景**(哈希序 ≠ 升序序键集 + `onEvaluated` 求值序探针)·
  **F-3 `EcozoneOfCallCount`**(「HomeRegion 只求一次」可证伪)·
  **F-4 AC-13-E3 IL 引用扫描**(`Math.Sqrt`/`Physics.Raycast`/NavMesh,影子件共用机器)
- **变异证明**:MUT-F1a(还原旧 `atClinic` ⇒ 恰 2 红)· MUT-F2(删 `ids.Sort()` ⇒ 恰 1 红)·
  MUT-F3(`HomeRegion` 每 tick 重求 ⇒ 恰 1 红)· MUT-F4(生产件注入 `Math.Sqrt` ⇒ 恰 1 红)
- **评审原件**:`production/qa/evidence/review-patient-ai-story-002-2026-10-05.md`

### 未闭登记(NOT-RUN,禁借绿)
- **NR-S2-1 `ClinicCells` 真实数据接入**(本 story 只接注入谓词,未接 24 `CONTEXT_TABLE` ∪ 52 `CLINIC_FRONT`)
  ⇒ **story-003 前置**(F-13.6 `AwaitingCare`)· NR-S2-5 `ShouldDecideNow` 驱动接入 ⇒ story-003/004 ·
  NR-S2-6 `ResetForLoad` 真负夹具 · NR-S2-7/8 story-001 遗留 5 项仍开

### 待办
- ✅ 收口提交推送
- ✅ **patient-ai story-003 已收口**(见下节)
- ⬜ **下一件 = patient-ai story-004**(或按 epic 内剩余 story 序 —— 见 sprint-04 关键路径)

---

## ✅ patient-ai(13)story-003 —— 呈现投影与视图 API —— 收口 2026-10-05

> 承「严格执行:创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送。评审只做一轮」。

### 交付物
- **生产**:`PresentationProjection.cs`(ViewState 投影链 / `IPresentPatients` 视图 / `MaterialTable` 查表器)·
  `PatientCueSchedule.cs`(cue 调度 + 呼吸层生命周期;终局 = 呼吸停止 + 姿态落最静止档)
- **测试**:`presentation_projection_test.cs`(**131 条**,AC-13-A4/A5/C1…C5/D1…D6/F1…F3)
- **证据**:`production/qa/evidence/patient-ai/story-003-accessibility-signoff.md`([L] 项)·
  `production/qa/evidence/review-patient-ai-story-003-2026-10-05.md`(评审原件)

### 单轮评审 → 修复轮(三条 BLOCKING)
- **G(真 bug)**:`Decide` 首拍 `firstDue = phase` 把相位当**绝对 tick** ⇒ 真实入场 tick 下恒真 ⇒
  **去同步彻底失效**(实证 6 id 全 due)。修复:增 `entryTick` 形参,`firstDue = entryTick + phase`
- **A/C2**:AC-13-C2 走程序集引用面而 37 程序集**不存在** ⇒ 恒真借绿 ⇒ 改**源码面 grep**
- **B/C4**:AC-13-C4 只扫成员名 ⇒ 改 **IL 引用面**(看得见 new GameObject/Object.Destroy/Instantiate)
- MAJOR:AC-13-D3 姿态半边零承载(增 `PostureTier` 字段)· D6 分段常函数测恒真(改行为面)·
  C3 负夹具恒真(影子真计数)· A4 名字面(改 IL 面 + NOT-RUN 剥离半边)· F1/F2 过claim(改名 `*_structuralHalf_*`)
- **变异证明**:MUT-G/B3/H2/C2 逐个注入 ⇒ 必红且点名。⚠️ MUT-H 首轮暴露 D6 修复只测首拍会漏网 ⇒ 补稳态半边

### 验证
- patient-ai **131/131 绿**(`unity/Logs/s003-final.xml`)
- 全库 EditMode **2455 passed / 0 failed / 1 inconclusive / 43 skipped**(`s003-fixgreen.xml`,**零回归**)
- 提交 `a1f7a36` 已推送 origin/main

### 未闭登记(NOT-RUN,禁借绿)
AC-13-D1 达成(表落盘 + 44 签署)· AC-13-A4 剥离半边(构建产物探针,EditMode 不可达)·
[L] 无障碍达成面(AC-13-F1 归 42 冗余通道 / AC-13-F2 归 44 空间化 + 用户拍定 `PERCEPT_R`)·
AC-13-D3 姿态的**呈现**归 42 —— 见 signoff NR-S3-1…4。承 story-002 的 NR-S2-1(真 `ClinicCells`)继续滚入。

---

## 📌 历史 —— patient-ai(13)story-002 规格与依赖面(已兑现)

> 用户令「马上开始轨 A」⇒ 关键路径续行:**13 story-002/003/004 → 8 → 37 → 11**。
> 承「严格执行:创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送。评审只做一轮」。

**story-002 规格**(`production/epics/patient-ai/story-002-spatial-behavior-perception-and-stepping.md`)——
感知格距 `Perceives = Δx²+Δy²+Δz² ≤ PERCEPT_R²`(禁 sqrt)· HomeRegion = `EcozoneOf(spawn_anchor(p))` ·
LOD 按 d² 三档 · 逻辑格步进(定点累加器 `acc`,Q16.16) · `Moving(p) := ¬Frozen ∧ State ∈ {Seeking}` ·
`Seeking{EnRoute/AtClinic}` 双相 · 升序 id 求值 · 解冻不补算。

**依赖面**:story-001 ✅ · 6 的 `EcozoneOf` / `spawn_anchor`(world-ecozones Story 002/003 — 需确认落点)·
9 的在场判定接口 · 整数导航格(23/27 基础设施,消费级)。

**下一步动作**:① 确认 `EcozoneOf` / `spawn_anchor` / 在场判定 三处消费面**是否已在库**
(未落则登记消费点桩 + NOT-RUN,禁借绿);② 读 `design/gdd/patient-ai.md` F-13.2/F-13.3/F-13.4/F-13.7 + B/E 组 AC 原文;③ 落实现。


## 📊 全项目进度总览（2026-10-03 实测重算）

> ⚠️ **本表于 2026-10-03 按各 story 真件逐件重算**(口径:`> **Status**:` 首行 + 体 `**Status**: [x]`)。
> 下表为**实测值**;旧值(124 Complete / 6 Ready / 77 In Progress / 59.9%)系**陈旧转录**,已废。

### 阶段状态

| 项 | 值 |
|---|---|
| **Stage** | Pre-Production |
| **Sprint** | sprint-03 ✅ 已闭(17/17) · **sprint-04 Phase 1 ✅ 已收口** · **Phase 2 进行中 = 4/7 系统完成**(patient-ai story-001/002/003/004 已收口)—— 关键路径移至 diagnosis-system |
| **Gate Check** | CONCERNS（2026-09-29 二轮，无 NOT READY 阻塞） |
| **ADRs** | 28/28 Accepted |
| **P0 GDDs** | 31/31 Approved |
| **Commits since 09-22** | 375 |

### Epic 故事进度

> **列语义(防混计 · 2026-10-03 明写)**:四列为**互斥且穷尽**的分类,口径 = story 件 `> **Status**:` 首行。
> - **Complete** = 首行含 `Complete`(或体 `**Status**: [x]`)
> - **Ready** = 首行为 `Ready` **或 `In Review`** —— 二者皆「story 已实现但 epic 未收口」,
>   本表**不分开列**(此为既定口径,非疏漏;`In Review` 的明细读「备注」列)
> - **In Progress** = 首行含 `In Progress`(**实测恒为 0**)
>
> ⚠️ **旧表误读的成因**:把「`In Review`」当成 `In Progress` 计 ⇒ 虚报 77。
> **`In Review` ≠ `In Progress`** —— 前者是「已实现待收口」,后者是「实现进行中」。
> 同理 **`In Review` ≠ `Complete`**(「不得借绿」)。

| Epic | Stories | Complete | Ready | In Progress | 备注 |
|------|---------|----------|-------|-------------|------|
| **audio-system (44)** | 14 | **14** | 0 | 0 | ✅ 全收口 |
| **skeuomorphic-ui (42)** | 18 | **18** | 0 | 0 | ✅ 全收口 |
| **skill-system (30)** | 8 | **8** | 0 | 0 | ✅ 全收口 |
| **telemetry-analytics (51)** | 10 | **10** | 0 | 0 | ✅ 全收口 |
| **input-system (3)** | 13 | **13** | 0 | 0 | ✅ 全收口 |
| **item-database (21a)** | 12 | **12** | 0 | 0 | ✅ 全收口 |
| camera-viewpoint (2) | 6 | **6** | 0 | 0 | ✅ 全收口(2026-10-03) |
| casebook (39) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| case-system (37) | 4 | **4** | 0 | 0 | ✅ 全收口(2026-10-06) |
| clinic-machine (24) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| combat-weapons (25) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| death-respawn (29) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| diagnosis-system (8) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| disease-simulation (9) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| emergency-procedures (10) | 7 | **7** | 0 | 0 | 🔶 **story 7/7 Complete;EPIC 未收口**(A1/A2/A6/B4/C4/C5 已闭;✅ 评审原件两份均在库,**旧记「评审原件缺」为方向性错记**;**真实残留 = D1/D2/D3 文档对齐 + 实跑测试套件**(round2 自陈未实跑,「0 红」系读源码而非执行)) |
| enemy-ai (27) | 5 | **5** | 0 | 0 | ✅ 全收口 |
| foraging (17) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| interaction-system (4) | 7 | **7** | 0 | 0 | ✅ **全收口 2026-10-04**(001…006 + **007 装载接线 · NR-1 已闭**;**未闭登记 = NR-2 真表数值轮 · NR-3/4/5 三条 `[L]` 走查 · NR-6 `OQ-17-3` · 007 新增 4-DC-4①/跨会话/§三其余规则 等七条 NOT-RUN**) |
| inventory-items (20) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| medical-consequences (53) | 4 | 0 | 4 | 0 | ⬜ 未启动 |
| modular-building (23) | 7 | **7** | 0 | 0 | ✅ **Complete ✅ 2026-10-03**（C1/C2/N-r1/C8-ID 全闭 · 本轮 72/72 绿 · 全量 2204/2163/0红，`9bb912b`+`bfa6234`;**未闭登记 = N-r2 生产装配根 + AC-23-09 跨平台签名**） |
| patient-ai (13) | 4 | **4** | 0 | 0 | ✅ **全收口 2026-10-05**(story-001/002/003/004;未闭登记 = V8 联机 BLOCKED-BY 45 · 跨平台 EXTERNAL · [L] 五档可读性部分闭) |
| player-controller (1) | 6 | **6** | 0 | 0 | ✅ **Complete ✅ 2026-10-03**（两轮评审判据缺陷已修;88 过 + 3 NOT-RUN，`a78c27a`） |
| prescription-medication (11) | 5 | **4** | **1** | 0 | 🔄 **In Progress(未全闭)** — ✅ 001 / 002(**评审原件已补 2026-10-06**)· 004 · 005(结构半边);❌ **story-003 未开工** —— 其标题所指的 `DrugTreatmentApplied` 构造点 / 写者独占**库内无实现件**(提交的 story-003 实为 F-11.2 半衰期,与本卡范围不符),`Required evidence` 的 `drug_event_test.cs` 全库不存在 ⇒ **AC-11-01① / AC-11-22 / AC-11-10 三条 BLOCKING 无真判据**;**epic 收口前置 = producer 裁定补做 story-003 或改派这三条 AC 并同步 TR 登记**;另 story-005 走查半边(AC-11-12/13/21 人工签核)+ AC-11-18 ② 实体元件承担方(skeuomorphic-ui)待闭 |
| processing (18) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| random-events (52) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| time-weather (5) | 5 | **5** | 0 | 0 | ✅ 全收口 |
| tutorial-onboarding (48) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| world-ecozones (6) | 6 | **5** | 1 | 0 | ✅ **Complete ✅ 2026-10-03**（6/6 story;N1/N5 已修 · 本轮 109/109 绿;**未闭登记 = N3 白名单判据(待 27 侧落地)+ Story 005 [L] 走查 EXTERNAL**) |
| persistence-service (7a) | 2 | **2** | 0 | 0 | ✅ 全收口（002 = ADR-029 契约支） |
| save-slot-ui (7b) | 1 | 0 | 1 | 0 | ⬜ 未启动 |
| **合计** | **207** | **137** | **70** | **0** | **66.2% 完成(137/207)** |

> ⚠️ **2026-10-03 实测重算(当前口径)**:按各 story 真件逐件解析 ⇒ **207 / 136 Complete / 0 In Progress / 71 Ready**。
> 与上一版(124 / 6 / 77 / 59.9%)的差额来源:
> ① `modular-building` 6 → **7** 且 0 → **7 Complete**(story-007 = ADR-029 接线支;本轮并闭 C1/C2/N-r1/C8-ID → 转 Complete);
> ② `world-ecozones` 5 → **6**,4 → **5 Complete**(story-006 = ADR-029 接线支;转 Complete);
> ③ `player-controller` / **`emergency-procedures`** 由「Complete」/「In Review」重分类 —— 前者转 Complete,后者按 story 真件归 In Review(7 件,非旧记 6);
> ④ `persistence-service` 002 契约支 ⇒ 1 → **2 Complete**;`save-slot-ui` 归 Ready(非 In Progress);
> ⑤ **`camera-viewpoint` 6 Complete** —— 旧表已记 ✅,本轮无变;
> ⑥ 旧「In Progress 77」系把 `In Review` 与 `Ready` 混同计;实测 **In Progress = 0**(无 story 件标 `In Progress`)。
> 重算口径:逐 story 件解析 `> **Status**:` 首行 + 体 `**Status**: [x]`;`Complete*` / `In Review*` / `In Progress*` / 其余=Ready。

> 📌 **历史转录订正记录(保留闭环)**:原记「191 / 93 / 95 / 6 / 49%」及 2026-10-02 口径「203 → 206」
> 均已作废,勿再引用。成因同族:总数漏计 `persistence-service` / `save-slot-ui`,
> 且把 `In Review` 与 `In Progress` 混计。

### 测试状态

| 指标 | 值 |
|------|---|
| EditMode | **2204 total = 2163 Passed · 0 Failed · 40 Skipped · 1 Inconclusive**（2026-10-03 batchmode 实测，Unity 6000.3.24f1） |
| PlayMode | 25/25 Passed · 0 Failed（2026-10-01 实测） |
| 确定性验证 | F7 反汇编 CLEAN · AC-29 三平台逐位一致 |

> ⚠️ **上表 EditMode 一行 2026-10-03 更新为当前 HEAD 实测**(2204/2163/0/40/1)。
> 历史链条(保留闭环):原记「1831/1858」出自 `185063f`,该提交**编译未通过**
> (`Scripts have compiler errors`)⇒ 测试从未跑起来,数字**无源**;`5572d66` 修复编译后 = 1989/2022;
> 2026-10-02 = 1995/2028;2026-10-03(本轮,三 epic 收口后)= **2204/2163**。
> ⚠️ 旧行把 `total` 写成分子分母两个数(1995/2028),易误读为「1995 通过 / 2028 应为」——
> 现行口径按 `total = passed + failed + inconclusive + skipped` 记账。
> exit code 2 源自唯一 Inconclusive(`Audio/SettingsExposureTest.test_monoOption_existsWithValidDefault`,既有项),**非失败**。

### 关键里程碑

| 日期 | 事件 |
|------|------|
| 2026-09-20 | `/create-architecture` 完成，architecture.md v1.0 |
| 2026-09-20 | ADR-023/024/025 起草并 Accepted（Required #1/#2/#3） |
| 2026-09-21 | Gate Check 一轮 FAIL → 用户承接 → CONCERNS |
| 2026-09-22 | U0a 工具链闭合 · UX Review Phase 3A · 批裁轮 OQ 全结 |
| 2026-09-23 | ADR-026/027/028 Accepted（Required #4/#5 + 音频归属） |
| 2026-09-24 | AC-29 三平台确定性验证通过 · F7 反汇编 CLEAN |
| 2026-09-25 | 数值批三批全拍 · R13 回写闭环 · 44 二轮修订 |
| 2026-09-26 | 音频 Story 001-004 完成 · 技能/遥测/输入/UI 全推进 |
| 2026-09-27 | 音频 Story 005/006/014 完成 · 986/986 测试全绿 |
| 2026-09-28 | 音频 Story 007-013 完成 · 1175/1175 测试全绿 |
| 2026-09-29 | Gate Check 二轮 CONCERNS（无阻塞） |
| 2026-09-30 | active.md 刷新 |
| 2026-10-01 | Sprint 02 全部完成（16/16）· Sprint 03 部分完成（11/17） |
| 2026-10-01 | modular-building / world-ecozones 越序实现（架构依赖先行） |
| 2026-10-01 | 冲突解决提交 c291873 |

### Sprint 状态

| Sprint | 计划时间 | 实际状态 | 备注 |
|--------|---------|---------|------|
| Sprint 01 | 10-05 ~ 10-18 | ✅ 8/8 Complete | 提前完成 |
| Sprint 02 | 10-19 ~ 11-01 | ✅ 16/16 Complete | 提前完成 |
| Sprint 03 | 11-02 ~ 11-15 | ✅ 17/17 story Complete | 完成（2026-10-02）· ⚠️ **AC-S03-5 黄金夹具未兑现**（emergency NOT-RUN · combat 零存在;根因 = ADR-012 矩阵未激活） |

### 交付物
- **生产**:`InteractionSelector.cs` —— 第一键改测 `d∞(playerCell, cell)`(评审外发现:原测「到原点」,
  与 F-4.1 判定式不符,是 F-4.1b 三例失败真因)· `IsBetter` static + 三键字典序 +
  `KindPriorityOf` 查表 + `Chebyshev(a,b)` int64 两参
- **测试**:`target_selection_test.cs`(**13 条**)· `replay_selection_test.cs`(**4 条**)—— 全部断言**直接驱动生产**
- **评审原件**:`production/qa/evidence/review-interaction-story-002-2026-10-04.md`

### 实跑(2026-10-04 修复轮)
- `unity/Logs/interaction-s002-final.xml` = **41/41 green**(24 边界 + 13 + 4)

### 双代理评审 → 修复轮(已完成)
- **结构评审 REJECT**(3 BLOCKING)· **QA 评审 REJECT**(F-1…F-13)⇒ 逐条修复
- 修复:全部断言改绑生产 · int64 夹具命中真陷阱 · 哈希序负夹具驱动生产 · F-4.1b 三例逐字照抄 ·
  补两条 QA 案例(到达序打乱 + 缓存上一目标负夹具)· 闭集计数改 Enum.Length
- **变异测试证可红**:MUT1(删 int64 拓宽)/MUT2(d∞ 从原点)/MUT3(删键③)/MUT4(键③ 哈希序)/
  MUT5(删键②)—— 逐项令对应测试红,生产已还原

### 待办
- ✅ story-002 收口提交推送(12133f7 前的 13891b3/893f3a3)

---

## story-003(候选集四源构造)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`SourceDtos.cs`(九个具名输入形状)· `CandidateSources.cs`(四源只读接口 +
  装配包)· `CandidateSetLoader.cs`(四源合并 · **无玩家格形参** = 取路 (a) 结构保证 ·
  不裁剪 · 不持 sink)
- **测试**:`candidate_dto_reflection_test.cs`(**14 条** AC-4-03)·
  `candidate_sources_test.cs`(**11 条** AC-4-20/22 + 装载形状)
- **评审原件**:`production/qa/evidence/review-interaction-story-003-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s003-final.xml` = **56/56 green**(14 + 11 + story-001 24 + story-002 7)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **QA ACCEPT**;F-3(装箱判据循环论证)/ F-4(基类展开零覆盖)/ F-5(边缘登记)逐条修复
- 结构侧无独立代理原件(代理触顶未回)⇒ 主会话复核通过,缺口登记在案
- **变异证明可红**:MUT-base(删基类展开 ⇒ 恰单条红 55/56)

### 待办
- ✅ 收口提交推送(12133f7,已 push)
- ⬜ story 004(R_INTERACT 邻域裁剪)—— interaction-system 下一件

### 交付物
- **生产**:`DiscoveryRequest.cs`(F-4.3 三字段载荷)· `InteractionRadius.cs`(单源持有者,4-DC-1 下界)·
  `KindRouteTable.cs`(路由表双向闭合,RegisteredSystems 10 值)· `NeighbourhoodReporter.cs`(广播自报)·
  `IDiscoveryReporter.cs`(Request 签名改为载 `DiscoveryRequest`)· `InteractionSelector.cs`(半径单源化)
- **测试**:`discovery_report_test.cs`(F-4.3/4.3b/4.4 + AC-4-13)· `radius_single_source_test.cs`(AC-4-17 + 4-DC-1 下界)·
  `kind_route_closure_test.cs`(AC-4-18 + 4-DC-5)
- **评审原件**:`production/qa/evidence/review-interaction-story-004-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s004-final2.xml` = **86 / 83 passed / 0 failed / 3 skipped(NOT-RUN 机检)**

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 APPROVED WITH SUGGESTIONS** · **QA 侧 ACCEPT-WITH-FIXES**;两件原件皆落盘
- 修复:扫描器分叉(删属性分支)· 6 半改机检 NOT-RUN · 帧率测试重写为「计数=调用次数」·
  新增「无主动交互 ⇒ 零出境」负夹具 · 半径扫描器已知限制登记 · RegisteredSystems 7→10(PF-1)
- **变异证明可红**:mut-cheb(丢 int64 拓宽 ⇒ 恰 1 红)· mut-gate(删主动交互门 ⇒ 恰 1 红)

### 待办
- ✅ 收口提交推送
- ⬜ 系统 6 Epic(接收侧:latch/幂等/判距复验/唯一 Append)—— 转绿 AC-4-13 6 半 + AC-4-17 6 消费半

<!-- STATUS -->
Epic: diagnosis-system
Feature: 阅读与判断
Task: story-005 断点(ReadingFSM/JudgmentFSM 未开工)
<!-- /STATUS -->

---

## story-005(模态门与路由边沿)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`ModalGate.cs`(`IModalGateState` 单布尔消费契约 + `IArmedState` + `ModalGate.Accept/Select/SetMotorSuppression`)
- **测试**:`modal_gate_test.cs`(AC-4-09/10/19 + 规则九,含选择器自报计数接缝、单 bool 可执行影子、闭集基数守卫、零出现 `ModalId` 断言)
- **评审原件**:`production/qa/evidence/review-interaction-story-005-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s005-final.xml` = **101 / 98 passed / 0 failed / 3 skipped**(3 = story-004 NOT-RUN)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(F-1 BLOCKING;F-2…F-11)· **QA 侧 ACCEPT-WITH-FIXES**(F1…F6);两件判定皆回填原件
- 修复:F-1 丢弃接缝改选择器自报计数 · F-2 删 ordinal 死代码 · F-3 闭集基数守卫 · F-4 单 bool 可执行影子 ·
  F-6 cref 订正 · F-8 零出现 `ModalId` · F-9 正向对照反空转门
- **变异证明 6 项全可红**:MUT-A(删模态门 → 2 红)· MUT-B(排队替代丢弃 → 2 红)· MUT-C(清全位图 → 3 红)·
  MUT-D(闭集 +1 员 → 1 红)· MUT-E(生产引用 `ModalId` → **编译错**,方向性实证)· MUT-E2(代码级串 → 3 红)

### 未闭登记(NOT-RUN,禁借绿)
- NR-1 方向① 生产实现体(归 10)· NR-2 方向② 适配器(归 42)· NR-3 AC-4-10 3 侧扫描(归 story-006)·
  NR-4 动态第 8 屏夹具 · NR-5 运行期消费者接线

### 待办
- ✅ 收口提交推送
- ⬜ story-006(数据契约构建期校验与呈现/手柄验收面)—— interaction-system 下一件


---

## story-006(数据契约构建期校验与呈现/手柄验收面)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`InteractionKindTable.cs`(`KindContractRow` + `DurationOwnerKind` + `InteractionKindTableValidator` 六条校验
  + **`KindPriorityTable`** 第二键唯一真源)· `RoutedSystems.cs`(被路由系统集单一来源)
- **测试**:`data_contract_validation_test.cs`(**13 条**:6 条 DC 夹具 + 4-DC-4 W/H/D + 4-DC-6 归属方 + 4-DC-3 行⟷表
  + AC-4-21 机器代理 + 反空转门 + 独立性)
- **评审原件**:`production/qa/evidence/review-interaction-story-006-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s006-final.xml` = **116 / 113 passed / 0 failed / 3 skipped**(3 = story-004 NOT-RUN 机检)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(F-1 BLOCKING 死代码;F-2…F-9)· **QA 侧 REJECT(AC-4-15)**(F-0 skip 归因更正;F-1…F-7)
- 修复:**真表单一来源接线**(F-2,消除「校验的表 ≠ 选择的表」)· 补 4-DC-4 W/H/D 半边(F-1/F-2)·
  `DurationOwner` 拆形态 + id 并断登记(F-3)· `RegisteredSystems` 单一来源(F-4/F-5)·
  **AC-4-21 机器代理**落地(F-3)· story 登记 NOT-RUN + 实际夹具数(F-6/F-7)
- **连带定向修复 story-002**:真表接线后 `target_selection_test` 4 条断言方向相反 ⇒ 按真表更新(MUT-7 先复现)
- **变异证明 10 项全落盘**:MUT-1…6(六条 DC 逐条可红)· **MUT-7**(接线真表 ⇒ story-002 恰 4 红,证接缝真实)·
  MUT-A(4-DC-4 W/H/D)· MUT-B(4-DC-6 归属方)· MUT-C(4-DC-3 行⟷表)

### 未闭登记(NOT-RUN,禁借绿)
- NR-1 装载器/烘焙接线(归 **story 007**)· NR-2 `KindPriorityTable` 真表(数值轮)·
  NR-3/4/5 三条 `[L]` 走查(可玩构建)· NR-6 4-DC-6 对 17/20 的真实时长登记(`OQ-17-3`)

### 待办
- ✅ 收口提交推送
- ✅ **interaction-system Epic 收口**(6/6 Complete · 2026-10-04;未闭项已显式登记)
- ✅ **story-007 装载接线收口**(NR-1 已闭 · 2026-10-04;见下)

---

## story-007(interaction_kinds 烘焙接线 · NR-1)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`InteractionKindBinder.cs`(阶段 2 绑定 + **唯一 `Validate(...)` 调用点** —— 消解 story-006 F-1 死代码)·
  `InteractionKindBaker.cs`(仓根种子 → 产物)· `InteractionKindCookedWriter.cs` + `InteractionKindCookedCodec.cs`(镜像编解码)·
  `InteractionKindBinderProbe.cs`(测试可见薄转发)· `DataBakeMenu.BakeInteractionKinds`(菜单调用点)
- **测试**:`interaction_kinds_bake_test.cs`(**19 条**)· `tests/unit/interaction/fixtures/`(15 夹具 / 11 负)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(F-1 BLOCKING = 首轮自造 4-DC-4 ③)· **QA 侧 REJECT**(无 BLOCKING 安全洞;F-0/F-3/F-6/F-7 MAJOR)
- 修复:**删自造判据 ③**(收回 4-DC-4 ①②,与 GDD 一字对齐)· **4-DC-4 ② 接生产接缝**(W/H/D 只注入 `SlotLinearKey` 行 + 行内自报)·
  **维度随产物落盘**(writer/reader 头部扩展,(D) 改读 `ds.*`)· **ConfigVersion 测试前置守卫** · **跨会话范围订正**
- **变异证明**:MUT-A(删 `Validate` 调用 ⇒ 7/7 负夹具红)· MUT-B′(4-DC-4 ② 承重)· MUT-F7(维度落盘可证伪,`unity/Logs/s007-mut-f7.xml`)
- **评审原件**:`production/qa/evidence/review-interaction-story-007-2026-10-04.md`

### 未闭登记(NOT-RUN,禁借绿 —— 覆盖缺口,非安全洞)
- 4-DC-4 ①(类型面恒真不可达)· 跨会话逐位一致(只证同进程)· 4-DC-6 归属方未登记半边(编译期常量无注入点)·
  `schema_version` 交叉一致性(守卫短路)· 聚合多错纪律 · 4-DC-6 对 17/20(`OQ-17-3`)· §三其余绑定层规则

### 待办
- ✅ 收口提交推送
- ⬜ interaction-system 7/7 全闭;下一系统见 Phase 2 关键路径

---

## diagnosis-system story-004(F-8.3 阴性把握度与不泄漏不变量)—— ✅ 收口 2026-10-06

### 交付物
- **生产**:`DiagnosisNegativeConfidenceBinder.cs`(阶段 2 绑定 + **唯一校验点**)·
  `DiagnosisNegativeConfidenceBaker.cs`(仓根种子 → 产物)· `DiagnosisNegativeConfidenceCookedWriter.cs` +
  `DiagnosisNegativeConfidenceCookedCodec.cs`(镜像编解码)· `DiagnosisNegativeConfidenceBinderProbe.cs`(薄转发)·
  `DiagnosisNegativeConfidenceTable.cs`(定表 + 求值器,运行期读侧)· `DataBakeMenu.BakeDiagnosisNegativeConfidence`(菜单调用点)
- **作者态**:`assets/data/diagnosis_negative_confidence.json`(5 合成旋钮)
- **测试**:`confidence_leak_test.cs`(**39 条**)· `tests/unit/diagnosis_system/fixtures/neg_conf_*.json`(**14 个** / 9 负)

### 实跑
- filter `unity/Logs/story004-fix2.xml` = **39 / 39 passed / 0 failed**
- 全量 `unity/Logs/story004-fix3-full.xml` = **2669 / 2622 passed / 0 failed / 46 skipped / 1 inconclusive**

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **两位评审均无 BLOCKING**(A:1 MAJOR · 5 MINOR · 4 NIT;B:APPROVED WITH SUGGESTIONS · 1 MAJOR · 3 MINOR · 4 NIT)
- 修复:**MAJOR-1** AC-8-12 (b) 承重断言恒真 ⇒ 改证接缝实参表 + 反向响应(登记口径替代)·
  **MAJOR-2** 消重 `Q16One`(唯一换算出口 `Table.RawToFloat`)+ `test_q16one_matchesFixCanonical` 逐位锚 `Fix.ToFloat()`·
  `CurveAt` 退化表**硬失败**(原静默 `0f` 会伪装 C-3 报警)· `schema_version` 构建期**同源**校验(堵「漏到运行期装载」)·
  AC-8-14 扫描面扩至类型名/属性名 + 非空守卫 · AC-8-10 删不可达分支 · AC-8-18 去 `??` 静默回退
- **变异证明**:MUT-A(权重路径)7 红 · MUT-B(去 clamp)首轮 0 红 → 补测后**恰 1 红** ·
  MUT-C(去极性门)首轮 0 红 → 补 `neg_conf_high_fallback.json` + 测后**恰 1 红** · MUT-D 恰 1 红
- **金标重钉**:`f75a8170` → `a7bfec31`(story-004 四常量)→ **`72db379c`**(修复轮消重少一行)
- **评审原件**:`production/qa/evidence/review-diagnosis-story-004-2026-10-06.md`

### 未闭登记(NOT-RUN,禁借绿 —— 覆盖缺口,非安全洞)
- AC-8-F5 跨平台三格矩阵(ADR-012 未实跑;只证 Mono 侧自洽)· AC-8-16 跨进程半边(只证同进程 N ≥ 10⁴)·
  AC-8-13 端到端(依赖 9 侧 `Project` 未落地)· AC-8-35 曲线参数半边(ConfigVersion 覆盖)·
  `ReadF32` 不拒 NaN/Inf(结构性不可达)· codec 不校验 `skillCap == SKILL_CAP`(姊妹件同形,须两处同改)·
  运行期/编辑期镜像无机械约束(技术债)· `DiagnosisReadFloorBinder.ReadSchemaVersion` 同形缺口(归 story-003 后续轮)

### 待办
- ✅ 收口提交推送
- ⬜ diagnosis-system 4/4 全闭;下一系统见 Phase 2 关键路径

---

## 2026-10-06 · prescription-medication story-003 收口

### 任务
「开工003」—— `DrugTreatmentApplied` 构造、零病名与写者独占(Integration / 8h)。

### 交付
- **新建门**:`unity/Assets/Editor.Tools.Gates/PrescriptionWriterGates.cs`(AC-11-22 写者独占 /
  AC-11-10 算法独占 + 引用面 / AC-11-01① 病名面 / AC-10-06b 载荷成对)
- **接线**:`AssemblyGates.cs` —— **菜单**(`RunMenu`)+ **构建前门**(`BuildGate.OnPreprocessBuild`)
- **测试**:`unity/Assets/Tests/EditMode/PrescriptionMedication/drug_event_test.cs`(**26 条**)

### 实跑
- filter `unity/Logs/drug_event_final.xml` = **155 / 155 passed / 0 failed**
- 全量 `unity/Logs/full_editmode_story003_final.xml` = **2916 / 2869 passed / 0 failed / 1 inconclusive / 46 skipped**

### 单轮双代理评审(承「评审只做一轮」)→ 修复轮
- **结构侧**:1 BLOCKING · 3 MAJOR · 2 MINOR · 2 NIT
- **QA 侧**:1 MAJOR · 3 MINOR · 2 NIT · **6 变异(MUT-A…F,其中 MUT-B / MUT-D 存活)**
- **B-1 全闭**:`IsBannedName` 对**属性访问器调用点恒假阴性**(`get_Indications` 无词边界)
  ⇒ 增 `get_`/`set_` 前缀展开 + 原测**重写**(阳性对照改打真调用点 `Editor.Tools.Bake`)+ 新增单元判据
- **M-1 全闭(两侧独立重合)**:门补接 `BuildGate` + `CheckSimReferenceFace` 补阳性对照
- **M-2 全闭(显式记账式)**:`PayloadCodec` 移出生产白名单 → 新立 `DecoderCtorSites`,
  `RunAll` 摘要报「⚠️ 判据-文本背离」;**AC 文本收窄归 producer / TD**
- **M-3 全闭**:`DiseaseNameForbidden` 补 GDD 逐字点名的 `DiagnosisResult`
- **MUT-A 的真实事故版**:修复前门只扫 `newobj`,而载荷是 **struct**(C# 发 `call .ctor`)
  ⇒ 站点集恒空、白名单「零命中」误报 **3 测红** —— 已改双指令收口

### 未闭登记(NOT-RUN,禁借绿)
- AC-11-22 的「影子装配注入」半边 · **AC-11-22 / TR-prescription-018 的文本收窄**(归 producer / TD)·
  `CheckPayloadPairing` 不覆盖 codec tag 序 · AC-11-01① 的 `DiagnosisResult` 反射面(8 侧未落型)·
  AC-11-15 三格矩阵与跨进程半边 · AC-11-10「双路径对拍」降级为 AC-10-06b 逐位比较 ·
  AC-11-22 的 7a 白名单本体(BLOCKED-BY-7a)

### 评审原件
`production/qa/evidence/review-prescription-story-003-2026-10-06.md`

### 待办
- ✅ 收口提交推送
- ⬜ prescription-medication epic **5/5 全闭**(story-005 走查半边仍 NOT-RUN)


## 2026-10-07 — story-001 DC-2/DC-6 校验机制落地(已提交 6fdd86b)

- **交付**:`PrescriptionActionIdRegistry.cs`(新)+ binder 接线 + 覆盖率记账 + 菜单侧生产报出。
  **判据本体维持 NOT-RUN**(处置 id master 未登记 · `NOISE_BAND_9` 未立 BL-2)。
- **测试**:filtered **174/174/0** · 全量 EditMode **2935/2888/0 红**。
- **双代理一轮**:代码面 8 条(1 高/3 中/4 低)+ 测试面 4 条必须修 —— 全部处置或显式登记。
- **突变验证**:5 条新守卫注入改坏点 ⇒ 全部实测红 ⇒ 还原。
- **评审原件**:`production/qa/evidence/review-prescription-dc26-shadow-2026-10-07.md`。
- **登记的两条已知弱点(不静默)**:M1 反守卫用干净夹具 · M4 正向判别力过窄。
- **仍未闭**:DC-6 对当前数据集零求值(`salicylic_acid` 的 `dose_range = null`)。


## 2026-10-07 — disease-simulation story-007 重开轮闭环(处置轴 + NOISE_BAND_9 真源)

- **交付**:`assets/data/disease_action_axis.json`(单一 master:处置轴 + treatable_by)·
  9 侧烘焙三件套 + Validator(9-DC-1…7)· `NOISE_BAND_PROGRESS_9` / `NOISE_BAND_POTENCY_9`
  双常量(值 = 100 raw,用户裁定)· 11 侧 `PrescriptionActionIdRegistry` 真源接线 ·
  10 侧 `ValidateActionIdFromAxis` 接线点 · `architecture.yaml` 幽灵引据订正 ·
  D-9-J / O-11→9 结案 · `salicylic_acid` action_id 1→10。
- **双代理一轮**:原判代码面 CHANGES REQUIRED(2B/6M/6m/3n)+ 测试面不予通过(S1×5 借绿…)。
  **修复轮全落**:B1 聚合化(消息聚合 + 首个违规规则号,保测试契约)· B2/M1/M4/M6 ·
  S1-1/1-2 真源读烘焙产物(⚠️ repoRoot 曾用 Assembly.Location+5层.. 落错,改
  `Application.dataPath/../..` 同 DataBakeMenu 先例)· S1-3 接线点 + 真源测试 ·
  S1-4 canary 验行为 · S2-1/2-2 补 DC-1/DC-2 负夹具 · S2-3 判**误报**(4 红系突变短路副作用,
  复验 1 红与结构一致)· S3 全修 · S4-1/4-2 补 AC-9-11…13 / AC-9-17 测试。
- **测试**:9 侧 **67/67** · 11 侧 **174/174** · 10 侧 **116/116**(+4 skipped)·
  全量 **2948/2901/0 红**(基线 2935/2888,+13 = 本轮新增)。
- **突变复验**:9-DC-1…7 全红;10 侧 DC-4 真源点首跑**突变存活** ⇒ 补反向断言(轴外 id=12)
  ⇒ 复跑红 ⇒ 还原绿(评审式自查抓到判别力缺口)。
- **评审原件**:`production/qa/evidence/review-disease-action-axis-2026-10-07.md`。
- **已知弱点(不静默)**:DC-4 生产烘焙侧接线归后续 story(`ValidateActionId` 仍影子)·
  R1-19 = NotImplementedException 哨兵 · DC-6 当前数据集零求值 · MINOR/NIT 登记不修 ·
  `清创`(O-9→10)归 10 GDD 轮。
- **状态**:disease-simulation epic **7/7 全闭**。

## 2026-10-07 — Phase 2 三层账目回刷 + 生产代码口径二次订正(✅ 提交 `4d1bfc0`)

- **回刷(第一轮)**:EPIC 头行 ×3(patient/case/diagnosis)+ case 4 张 story 卡 + index 5 行 + sprint-04(头行/Phase2 表/关键路径图/:176 待办行)—— 纯账目对齐,零实现改动。
- **二次订正(生产代码口径复查)**:patient-ai **实为 4/4 实现全闭**(004 = `0b6f948` 2026-10-05:37 测试 + 5B/6M 修复轮 + 走查证据,账面从未回刷)⇒ 13 行改「4/4 + 原件 caveat」。
- **真未做的 4 个 story(已核,token/测试/commit 三零)**:diagnosis-005(ReadingFSM/JudgmentFSM 零命中)· diagnosis-006 · case-005 · case-006。
- **新立治理缺口**:`review-patient-ai-story-004` **评审原件缺**(001/002/003 均在库)—— 须**补做一次评审(评当下,不追认)**方可转 Complete;已登记进 patient-ai EPIC 头行/卡/index/sprint。
- **Phase 2 现状**:4/7 实现全闭(13 含 caveat)+ 2 有残余(8/37 各 4/6)+ 1 未达 DoD(11)。
- **当前关键路径断点 = `diagnosis/story-005`**。
- ✅ 本批账目文件已由 `4d1bfc0` 提交(远端)。
## 2026-10-07 — OQ C 类轮(6 条裁定 + 一条自噬险礁)

- **提交**:`71292c8`(8 文件:medical-consequences / review-log / skeuomorphic-ui /
  systems-index / tr-registry / traceability-index / EPIC / story-003)。
- **OQ-53-2(DelayTable 值)** —— ⚠️ **第一版值自己违约**:原写 `PatientLives → 0` 特例,
  而 `AC-53-06` / story-003 明写「每条 > 0,≤0 构建期 throw」。0 在域里 = 破 BLOCKING。
  解 = **收窄定义域为回响轴三分立值**(结局轴由 9 结算、53 只读,物理上不走这张表),
  不是给结局轴开豁免。值:`Residual 600` / `Recurrence 1200` / `TrialHistory 2400` @20 Hz。
- **OQ-53-5**(取值集已在正文定名,原行把答案记成问题)· **OQ-53-6**(载体 = 13 + 在场 NPC)。
- **OQ-42-9**(读数条最小观感)—— 三问逐答,新增走查级 `AC-42-C9`(ADVISORY);
  穿墙 = 已接受的 P0 限制,禁补遮挡剔除。AC 计数 43 → **44**(38 BLOCKING 不变)。
- **OQ 未结 36 → 34**(本轮 -2 净:53-2 / 53-5 / 53-6 / 42-9 四条裁定里,53-5 与 42-9 早已在前轮以「答案在正文」形式处理过登记口径,故按表内行数计净 -2;复扫 `design/gdd/` 未划除 OQ 行 = **34**,含 4 行在 reviews 子目录的历史留档)。
  ⚠️ 剩余 30 条**大半自带 deadline**(P1a / P1b / 实现期 / playtest / content 批),
  真「P0 前必须裁」的只剩少数:`OQ-1-12`(接地模型 spike)· `OQ-42-4`(首帧焦点 spike)·
  `OQ-11-3`(等 `O-11→21a`)· `OQ-17-10`(CDF walk 对拍,等 ADR-012 CI)。
- **下一步候选**:`OQ-11-3` 的前置只剩 `O-11→21a`(21a 重开:落断言 + 声明 `drug_potency` 域)
  —— 这是唯一「一个 ADR 动作解一串」的杠杆点。

## 2026-10-07 — **patient-ai story-004 补做评审**(评当下,不追认)—— ✅ 已闭环

> 协议逐字执行:**补做评审(双代理单轮)→ 修复 → 复跑绿 → 收口提交推送;评审只做一轮**。

- **双代理评审**(单轮,均 CHANGES REQUIRED):
  - **测试面 15 条**(1 BLOCKING + 7 MAJOR + 4 MINOR + 3 NIT):B1 程序集负夹具恒真/断言倒置 ·
    M2 IL token 不命中具体类型 · M3 A5 方法体/静态私有盲区 · M4 spasm 同机器 · M5 case 流装饰 ·
    M6 AC1 代理未登记 · M7 V8 桩恒真 · M8 走查件双错 · m9/m10/m11/m12 · n13/n14/n15。
  - **结构侧 12 条**:S1/S2/S4 `ResetForLoad` 非全重置 + 「重新求值」无路径 + 三处口径不一致 ·
    S3 LOD 死链 · S5 唯一消费点双主张 · S6 `IsVisible` 恒真 · S7 平行 Material · S8/S10/S12 通过 ·
    S9 未核面由 M3 补齐 · S11 随 S1 消除。
- **修复落笔**:生产 3 文件(`PatientSpatialDirector` Clear 三件 + doc · `PatientBehavior` 导演 Clear +
  Material 注释 · `PresentationProjection` 删 IsVisible)+ 测试 2 文件(`reconstruction_and_write_path_test`
  B1/M2/M3/M4/M5/M7/m9/m10/m11/n15 · `spatial_behavior_test` B4 重写);
  **文档轮**:走查件整体重写(档1 判据订正 Idle⇒Begin + 五档落点改真身测试 + 签署行)· story 卡回填
  (Status Complete / AC 勾选+代理登记 / Test Evidence `[x]` 真身路径)。
- **复跑绿**:**PatientAI 175/173/0 红/2 跳**(`patientai-fix-2026-10-07.xml`,基线 168/166,+7)·
  **全量 2955/2908/0 红**/1 inconclusive/46 跳(`editmode-full-2026-10-07.xml`,基线 2948/2901,+7;
  inconclusive = 音频 mono 出厂默认既有项,CLI exit 2 系其所致)。
- **登记不修(不静默)**:S3 LOD 死链 · S7 平行类型 · n13/n14 · TC-4 迁移腿(归 7a)·
  AC3 端到端(BLOCKED-BY 10)· AC5(BLOCKED-BY 45)· AC6(EXTERNAL)· [L] 整体签署(待 42/44)。
- **评审原件**:`production/qa/evidence/review-patient-ai-story-004-2026-10-07.md`(原判定 → 修复落点 → 验证命令)。
- **账目**:patient-ai EPIC 头行/004 行 · `index.md:32` · sprint-04(四处)全部摘 caveat ⇒ **13 epic Complete ✅**。
- **当前关键路径断点 = `diagnosis/story-005`**(13 全闭,8 的 005/006 解锁)。

### 追记 · 边界评估轮(同日,用户指令「三个如实边界调用子代理评估修复」)✅ 闭环

- **双席只读评估**(lead-programmer 结构面 + qa-lead 测试面)→ 判定:可修 7 / 真不可修 5 / 维持 2。
- **修复落笔(`75754e9`)**:S7 孤儿 `PatientMaterial`+`Material()` 删除(零生产调用方,原「需 44 契约」理由证伪)·
  AC6 空体补 `Assert.Fail` 防借绿(与 M7 同型首轮漏项)· 补具体类型 IL 负夹具(M2 回归守卫 —— 原接口夹具对
  新旧 token 均命中 = 突变存活)· 卡面五处措辞(Completion Notes 空壳 / AC5 转勾判据 / AC6 拆腿 + CI TODO 桩指针 /
  Test Evidence must-exist 矛盾 / Note 3 未兑现主张)· 走查件 `[ ] Approved` 机器形态 · 原件 §四 两处失实判据订正(B1 真行=编译炸 / M2 判别力零)+ §五 S3 精确化(悬空落点 + F-13.3/13.4 整链死 + 13 零生产构造点根因)。
- **突变实跑(原件 §七)**:批次A 6 注入合跑 → **8 红全点名命中,零存活**;批次B token 回退 → **恰 1 红**(M2 守卫);
  还原复跑 → **175/173/0/2 回绿**。
- **维持登记**:AC5(YAGNI 接缝)· AC8(纯外部)· n13/n14(降级突变代证)· TC-4 归 7a · AC3 归 10 ·
  S3 落点待裁(新 story-005 或挂 tick 接线 epic —— 归 TD 裁)。


---

## 追记 · diagnosis/story-005 收口(2026-10-07)✅ 全闭环

**协议执行**:创建并 unity cli 测试 → 双代理评审(单轮)→ 修复 → 复跑绿 → 原件 → 收口提交推送。

- **生产四件**(`Gameplay.Presentation/Diagnosis/`):`DiagnosisReadingFsm`(S-8.2 四态 + 两窗口 +
  **单一完成入口**)/ `DiagnosisJudgmentFsm`(S-8.3 三态 + 关病例三步次序)/ `DiagnosisSnapshotSampler`
  (快照冻结 + 持续刷新,签名无 currentTick = 结构防错)/ `DiagnosisGrowthGate`(`IEventAuthority.IsHost` 门控)。
- **测试两件**:`reading_state_test.cs` 16 测 + `snapshot_staleness_test.cs` 3 测(PlayMode,新建目录)+
  `boundary_guard_test` 新增 gate 负例 1 条。
- **Gates**:`[D-EXIT-GATE]` 谓词(出口唯一合法调用方 = GrowthGate,铁律④静态闭合)。
- **双代理单轮**(lead-programmer 8 条 / qa-lead 10 条,均 CHANGES REQUIRED)→ **18 条全处置**
  (修复:S-1 单一入口 · M1 恒真假流换类型面+影子 · M2 负夹具收编共用机器 · M3 谓词 · M4 API恰三 ·
  M5 三段源 · S-5 真源参数面;登记:S-2/S-3/S-4 语义裁定 · 门控措辞订正 · 金标重钉 · NOT-RUN 族)。
- **复跑绿**:DiagnosisSystem 过滤 **150/149/0 红/1 跳** · 全量 **2972/2925/0 红/1 inc/46 跳**
  (基线 2955/2908,+17)· PlayMode **3/3** · 突变 `[D-EXIT-GATE]` 失活 ⇒ 恰 1 红点名,还原 26/26。
- **金标**:`72db379c` → **`7ce0ce27`**(ReadingForm 4 + JudgmentState 3 枚举字面入面,+7 行,有意识重钉)。
- **原件**:`production/qa/evidence/review-diagnosis-story-005-2026-10-07.md`;卡 Status → Complete ✅。
- **NOT-RUN(不静默)**:AC-8-45 网络子句(BLOCKED-BY-45)· 9/37/39 真接线集成(39 Ready)·
  IsHost 恒 true 下真客户端端到端(归 45/P1b)· [L] 走查归 story-006。
- **卡面语义裁定三条(39 接线前生效)**:S-2 回溯抹除 · S-3 状态判重替代当帧集 · S-4 抑制复查不刷新。

## 2026-10-07/08 — P0 四前置清轮(O-11→30 / OQ-10-7 / OQ-42-4 + OQ-1-12 传导订正)

- **提交**:`ccb3fa8`(O-11→30 半闭)· `bb92d43`(OQ-42-4)· `ba4f244`(OQ-10-7)·
  `70c3f21`(OQ-1-12 传导订正 · push 曾被 GitHub main ref 500 挡,新分支探测证明
  对象上传无阻 ⇒ 服务端瞬时故障,后重推成功)。
- **四条 P0 前置全部处理完**,其中**三条是「假前置」**(方案早已裁/答案早已在正文):
  | 前置 | 真相 |
  |---|---|
  | `OQ-1-12` 接地模型 | 方案甲 **2026-09-29 已由用户裁完**,GDD 3 处 + EPIC 3 处 + story-003 5 处仍在写「待 spike 回填」= 传导滞后 |
  | `OQ-42-4` 首帧焦点 | 原问把**描述性问题**(引擎默认是什么)当**规范性问题**(应该如何);spike 答不出规范,判据改写为不变量后**不需等 spike** |
  | `OQ-10-7` 急救乘子落点 | 「住 3 的动作资产侧」**结构性不可行**(`.inputactions` 是引擎资产,装不了 `Fix` 字面量,ADR-014);「3 补 Amendment」= 空转义务 |
  | `O-11→30` 等级→EFF | 唯一真欠,但只欠**形状**;已裁 = 单调阶梯 + 两端对齐 21a + 省料不改 `dose`,逐档填表归 30 自己数值轮 |
- **两个抓到的真缺陷(比「清了 OQ」值钱)**:
  1. **引擎 `skinWidth` 默认值本机无权威件**(`PhysicsModule.xml` 只有 summary)⇒ 方案甲的
     成立前提 `0.015 > skinWidth` 不能靠注释 ⇒ 新立 **`AC-1-34`**(装载期断言)。
  2. **`AC-11-11` ④ 的算术错**:原稿「`2^47 × 2^16 = 2^63` ⇒ int64 合法」——
     signed int64 上确界是 `2^63 − 1`,**恰好溢出 1**;真上界取决于 `dose ≤ 7` ⇒ 安全。
- **新增三条 AC**:`AC-1-34`(下压量不等式)· `AC-42-C10`(首次导航必落焦)·
  `AC-21a-38c`(`drug_potency ∈ (0, 2^47]`);`AC-21a-38b` 尺 `> 0` → `≥ 100 tick`。
- **未结 OQ**:31 条,其中**仅剩 `OQ-1-12` 的 ε 实测**是真 P0 前置(归【桌面】PlayMode,
  不阻塞文档);其余自带 P1a/P1b/实现期/数值轮 deadline。
- **下一步候选**:①【桌面】PlayMode 跑 `AC-1-21` 的 ε 与 `AC-42-C10` 落焦断言;
  ② 11/9/3/25 各 GDD 的 review-log 与本轮裁定回写;③ 21a 的 F5 偏移可感知地板
  (判据须写成**逐药求值式** `|potency × [ (H+off)(1−e^{−W/(H+off)}) − H(1−e^{−W/H}) ]| ≥ NOISE_BAND_POTENCY_9`,
  不能拍固定 tick 常量)。
