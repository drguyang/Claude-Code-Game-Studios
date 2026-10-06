# 评审报告原件 — prescription-medication story-005(戥子输入与方笺呈现)

**日期**: 2026-10-06
**评审对象**: `unity/Assets/Gameplay.UI/Skeuomorphic/DentchDoseSelector.cs` + `unity/Assets/Tests/PlayMode/PrescriptionMedication/dentch_input_test.cs`
**评审轮次**: 单轮(承「评审只做一轮」协议)

## 交付物

### 生产代码
- `unity/Assets/Gameplay.UI/Skeuomorphic/DentchDoseSelector.cs` — 戥子档位选择器(焦点落点序列生成 · 档值换算 · 黄铜刻度装载)
  - `DentchDetent`(实现 `IFocusable`)· `PrescribeStrokeIntent` / `PrescribeStrokeResult`(呈现 → 11 的唯一输入形状)
  - `unity/Assets/Gameplay.UI/Skeuomorphic/DentchDoseSelector.cs.meta`
- `unity/Assets/Tests/PlayMode/PlayMode.asmdef` — 增 `Gameplay.UI` / `Gameplay.Presentation` 引用

### 测试代码
- `unity/Assets/Tests/PlayMode/PrescriptionMedication/dentch_input_test.cs` — **49 条测试**
- `unity/Assets/Tests/PlayMode/PrescriptionMedication/dentch_input_test.cs.meta`

## 测试结果

- **filter(评审前)**: `unity/Logs/dentch_input_6.xml` = **41 / 41 passed / 0 failed**
- **filter(修复后)**: `unity/Logs/dentch_fix_2.xml` = **49 / 49 passed / 0 failed**(41 → 49,补 8 条)
- **PlayMode 全目录(修复后)**: `unity/Logs/dentch_playmode_full3.xml` = **85 / 85 passed / 0 failed**(77 → 85,新增 8 条随套件全绿)
- **全量 EditMode(修复后)**: `unity/Logs/dentch_editmode_full3.xml` = **2826 passed / 0 failed / 46 skipped / 1 inconclusive**(exit 8 源自既有 Skipped/Inconclusive,非失败;含 AC-42-F3 词面门 `world_billboard_test` 通过 —— 生产件注释改写后已消红)

## 评审结果

### 结构侧评审(unity-specialist)

**判定**: **APPROVED WITH SUGGESTIONS**(0 BLOCKING · **3 MAJOR** · 7 MINOR · 3 NIT)

> 生产件逻辑本身对「档位序列 = 焦点落点」契约是**正确**的(生成公式、整剂单落点、零 clamp 行为、
> 越界如实抛均与 GDD 规则十三 / AC-11-17/18 一致)。缺陷集中在**测试强度**与**一处文档 vs 实现的承诺差**。

### QA 侧评审(qa-lead)

**判定**: **APPROVED WITH SUGGESTIONS**(0 BLOCKING · **4 MAJOR** · 7 MINOR · 3 NIT)

> **没有任何 BLOCKING 缺陷落在生产件上**。全部 MAJOR 均落在**测试证伪力**与**登记完备性**。

## 修复记录

| # | 问题 | 落点 |
|---|------|------|
| **MAJOR-1**(结构/QA 共指) | `Assert.AreEqual(sel.Detents.Count, sel.TickCount)` 是 `x == x` **恒真重言**(两者同源同一字段),跨组件绑定从未被验证 | 生产件新增 `LoadInto(BrassScaleElement)` 真装载路径;测试改 `test_loadInto_brassElement_tickCountIsBound`(**反控**:未装载前元件刻度数须为默认 0)+ `test_loadInto_nullElement_throws` |
| **MAJOR-2**(QA) | 「零第二 EventSystem」断言面 = `Gameplay.UI` 程序集,该程序集不引用 `UnityEngine.EventSystems` ⇒ 谓词**结构上不可能为真** = 恒真空真;故事卡 QA 卡点名的**双 EventSystem 夹具未交付** | 新增夹具 `DualEventSystemProbe : EventSystem` + `test_eventSystemPredicate_positiveControl_dualEventSystemFixtureIsDetected`(正控 + 反控) |
| **MAJOR-3**(结构/QA 共指) | `IsFocusEnabled` 文档称「戥子满档 / 缺药时为 false」但生成路径**恒传 true**,该分支从未产生也从未断言;常规档 `IsWholeDose` / `PresentationLabel` 零断言(变异可存活) | 门控位改**可从装载期注入**(`BuildDetents(..., isFocusEnabled)` + ctor 重载)+ 文档收窄为「判定源归 20,未落地 ⇒ NOT-RUN 6」;新增 `test_focusEnabled_injectedFalse_propagatesToAllDetents` / `_wholeDosePath_respectsInjection` / `test_regularDetents_areNotWholeDose` / `test_presentationLabel_containsNoDigits` |
| **MAJOR-4**(QA) | `FocusRank` 值域(AC-42-B1 满射)**实为空判据** —— `FocusBoundaryAssertions.AssertRankDataSurjective` 只查 null,其源码自陈「不限制具体数值范围」;变异 `focusRank: i`(0 起)可存活 | 测试补真判据:`CollectionAssert.AreEqual(Enumerable.Range(1,K), ranks)` + 单射 distinct 计数;42 侧断言照走(接口面) |
| **MAJOR-4b**(QA) | TR-prescription-011 ③「禁忌命中 ⇒ UI 无差异」`[A]` 条**零证据且未登记** | 生产件 `PrescribeStrokeIntent` **结构上不含**禁忌 / 拦截 / 扣减语义 ⇒ 以 DTO 洁净扫描 + 字段面负断言覆盖结构半边;端到端等价性(同请求两输入下视图逐项相等)登记为 **NOT-RUN 7**(依赖 8 侧落笔接线) |
| **MINOR-1**(结构/QA 共指) | 两条「正控」近恒真 —— 调用的正是被测生产方法,复算的正是正式测试已断言的期望值,无变异体可注入 | 换为 `test_positiveControl_mutatedReferenceDiffersFromProduction`:测试侧自写**独立参考实现** + **变异公式**(`hi−lo+2` / 漏 `−1`),证两者与生产件输出可分辨 |
| **MINOR-2**(结构/QA 共指) | `PrescribeStrokeIntent` / `Result` 全库**零生产消费方**,而 XML 声称「呈现层交给 11 的唯一输入形状」—— 该声称当前为假,且**未登记** | 文件头 NOT-RUN 由 5 处扩至 **7 处**(新增 6 = 门控判定源未落地;7 = 落笔接线归 8 侧 `S-8.4` 路线甲 + 39 方笺页);补 `test_strokeIntent_doseOrdinalSharesDomainWithPrescribeRequest`(同域性真断言) |
| **MINOR-3**(结构/QA 共指) | `test_modalId_closedSetIsStillSeven` 名说 seven,体内断 8(None + 7)—— 名实略拧 | 改名 `test_modalId_closedSetIsNonePlusSeven` |
| **MINOR-4**(QA) | 零降级读数扫描面**只收单文件**,真正会挂角标的 `Brass/` 不在面内;禁词含中文字面量,经剥离后**永不可能命中**(死词条) | 扫描面扩到 `Brass/` 目录(`test_brassDir_hasNoNumericBadgeToken`);删死词条;补 `test_forbiddenTokenScan_positiveControl_detectsKnownBadgeFragment`(证词面可命中 + 注释须被剥离) |
| **MINOR-5**(结构/QA 共指) | 两处测试名承诺 ≠ 断言内容(`casebookIsTheSingleHostOfFormula` 未验「唯一宿主」;`isOneEntryPerItem` 未验「一药一条目」) | 改名 `test_modalId_casebookExists_distinctFromPaperCloseup48` / `test_lexiconBakedSource_hasEfficacyWordField_andNoDiseaseKey`,名实对齐 |
| **MINOR-6**(结构) | `MAX_DOSE_DETENTS` 单位与 GDD `:497`「`hi − lo` 档数上限」**差一**(code 用落点数 `hi−lo+1`) | 生产件旋钮文档显式钉「**单位 = 落点数**(= 焦点路径长度,承 Fitts 理据)」;GDD 侧登记订正 |
| **MINOR-7**(结构/QA) | 「同键双触发禁止」/「焦点单栈门」的 `[A]` 条除 EventSystem 计数外无判据,亦未登记 | 由 MAJOR-2 的夹具 + 正控覆盖结构半边;运行期实跑登记 NOT-RUN 4 |
| **NIT-1**(QA) | `RegisteredMaxDoseDetents` **自守其门**(测试把被测件常量同时当输入与上限) | 合成扫描改以测试内**字面量**为循环上界 + 断言常量值域(`Assert.LessOrEqual(9, ...)`) |
| **NIT-2**(QA) | 故事卡 QA 行 `dose_range=(2,5) ⇒ 长度 3` 是**算术笔误**(= 4) | 测试内显式标注笔误并取权威公式 4(未镜像错误) |
| **NIT-3**(结构/QA) | 跨 epic 耦合:11 套件内断言 42 元件 `BrassScaleElement` | 保留(消费方镜像,AC-42-F3 是 42 侧义务的镜像断言);已在注释标注 42 侧改动会红在 11 套件 |
| **QA 追加** | `PresentationLabel` 生成 `"第 {i+1} 档"`(含阿拉伯数字)与 AC-11-12「零数字读数」存在张力 | 改为**零数字**标签「戥子档」/「整剂」,并加注「不入可见呈现,只供辅助技术辨识两类落点」 |
| **QA 追加** | 本目录受 **AC-42-F3 词面门**扫原文(注释也扫),生产件里出现「用于声明禁止」的禁用词即红(实测 `world_billboard_test` 报 3 处) | 生产件注释改写为释义表述,不得照抄禁用词;文件头加注该纪律 |

## 变异证明(修复后补测的证伪力)

| 变异 | 描述 | 结果(红测点名) |
|------|------|------|
| **MUT-A** | `focusRank: i + 1` → `i`(值域 0..K−1) | **1 红 / 48 绿** — `test_detents_focusRankIsSurjectiveAndInjective`(证 MAJOR-4 修法生效;原稿该变异存活) |
| **MUT-C** | `presentationLabel` 改回 `$"第 {i+1} 档"`(带数字) | **1 红 / 48 绿** — `test_presentationLabel_containsNoDigits` |
| **MUT-D** | `isFocusEnabled: isFocusEnabled` → `true`(忽略门控注入) | **1 红 / 48 绿** — `test_focusEnabled_injectedFalse_propagatesToAllDetents` |
| **MUT-E** | `LoadInto` 空体(不装载刻度数) | **1 红 / 48 绿** — `test_loadInto_brassElement_tickCountIsBound`(原稿恒真断言 **0 红**) |
| **MUT-F** | `DoseValueOf` 的越界抛改 `Math.Clamp` 夹回 | **2 红 / 47 绿** — `test_dentchSelector_sourceHasNoClampCall` + `test_doseValue_outOfRange_throwsInsteadOfClamping` |

> 命令模板:
> `unity test unity --mode PlayMode --filter "DaYiJingCheng.Tests.PlayMode.PrescriptionMedication.dentch_input_test" --output unity/Logs/<name>.xml`
> 变异运行日志:`dentch_mut_A.xml` · `dentch_mut_C.xml` · `dentch_mut_D.xml` · `dentch_mut_E.xml` · `dentch_mut_F.xml`;
> 五条变异**全部 ≥ 1 红**(`Failed(Child)`),逐条证「把生产件对应一行改坏 ⇒ 套件必红」;
> 变异后以 `diff /tmp/Dentch.orig.cs unity/Assets/Gameplay.UI/Skeuomorphic/DentchDoseSelector.cs` 验证**干净回滚**(无输出 = 逐字节一致)。

## NOT-RUN 登记(禁借绿 —— 覆盖缺口,非安全洞)

测试文件头显式登记 **7 处**,全部保持未勾:

1. **AC-11-18 ②**(42 侧无「下一档」焦点落点 + `hi` 档纸面反馈)—— BLOCKED-BY-42 元件落地;本件只证**消费方**序列长度正确。
2. **手柄(无指针)路径** —— BLOCKED-BY 桌面调试集中轮 + ADR-013 假设 6 spike(半可信)。
3. **AC-11-13 / AC-11-12 / AC-11-21 走查子项** —— 可判 ≠ 已判,须实现轮人工执行 + 签核(主创 / 主创+医学从业 / 音频 lead)。
4. **真实 UX 夹具下的焦点单栈门实跑** —— 本件只做**结构**断言(装配面零第二 EventSystem + 谓词正控);引擎运行期双 EventSystem 的实跑判据归 42 侧 spike。
5. **`materia_lexicon.cooked` 真装载** —— 21a 产出方未落 C# 字段(承 story-004 NOT-RUN 3);本件以烘焙 JSON 源件 + 结构扫描走通。
6. **`IsFocusEnabled = false` 分支的判定源** —— 门控位已可从装载期注入(本件有判据),但**判定源**(戥子满档 / 缺药)归 20 库存扣减面,尚未落地 ⇒ 恒 true 是当前唯一实跑路径。
7. **`PrescribeStrokeIntent` / `Result` 的生产消费方** —— 全库零生产接线(仅定义处 + 本测试);落笔 → 11 的接线归 8 侧 `S-8.4` 路线甲 + 39 方笺页落地。

## 跨故事缺口(本故事范围外,但影响判据完整性)

- **AC-11-18 ② 的实体元件**(42 侧「下一档」落点缺失 + `hi` 档纸面反馈)本 epic **不实现** ⇒ 该条 `[A]` BLOCKING 的 ② 半边**当前无判据**;建议 skeuomorphic-ui epic 收口时确认承担方。
- **3 侧 float→int 量化实现**(input-system epic story 006/007)未落地 ⇒ 本件只断言 11 / 呈现侧**入参形状为整数**,**未断言量化真在 3 侧发生**。
- **`ModalId` 闭集与 `PaperCloseup48` 的「勿混读」**两处警示本件均已通过(闭集仍 7 员、方笺不成新模态、第七员在集内且与本条无关)。

## 亮点(应保留)

- **主动订正故事卡的算术笔误**:故事卡 QA 行写「`(2,5)` ⇒ 长度 3」,测试 `test_detents_countEqualsHiMinusLoPlusOne` 显式标注为笔误并取权威公式 4(= 5−2+1),未镜像错误。
- **反向守卫防「啥都没实现冒充绿」**:`test_selector_doesNotImplementFocusAlgorithm` 在禁 token 之外**要求** `IFocusable` 在场,精准回应「实现接口 ≠ 违例」——原稿把接口名本身当违例(断言过宽 = 逼着不实现契约),已收窄为「焦点算法实现类符号」。
- **守卫缺席 = 硬失败**:`ScanWithDtoGuard` 经反射取用编辑期门(PlayMode 不可编译期引用),守卫未装载即断言失败,**拒绝「守卫未装载即静默返回空集」**。
- **零进程态可重建**有真判据:全只读属性 + 无可写实例字段 + 同档案两次构造逐项相等。
- **NOT-RUN 登记详实且与故事卡一致**,且本轮**由 5 处扩至 7 处**(新增门控判定源 / 落笔接线两处真实缺口)。
