# 评审报告原件 —— diagnosis(8)story-006 脉案呈现/词表路由/反幻想护栏(单轮双代理)

- **对象**: `production/epics/diagnosis-system/story-006-casebook-presentation-routing-anti-fantasy.md`
  及本轮新落生产三件(`DiagnosisActionLexicon` · `CasebookScreen.BuildUI` 订正 · `Casebook39.uxml` 订正)
  + 测试两件(`casebook_lexicon_test.cs` 7 测 / `casebook_render_test.cs` 8 测,后者 = Required evidence)
  + 金标重钉 `7ce0ce27→b711a17c` + 证据目录首批(`diagnosis-system/README.md` + 走查件)
- **日期**: 2026-10-08 · **轮次**: 单轮双代理(承用户「评审只做一轮」)
- **评审席**: `lead-programmer`(结构侧)+ `qa-lead`(测试面)—— 均只读、评当下
- **两侧总裁定**: **CHANGES REQUIRED**(结构 2 MAJOR + 3 MINOR + 1 NIT / 测试 6 MAJOR + 7 MINOR + 3 NIT)
  → 修复 → 复跑绿(§四)→ 本件落库
- **判定 → 修复 → 验证** 三段式逐条登记;**登记不修项显式**(§五,不静默)

---

## 一、结构侧(lead-programmer)—— 2 MAJOR · 3 MINOR · 1 NIT

**原判定**:CHANGES REQUIRED。前置核验通过:词表四态与卡文逐条吻合、B-1 反射守住双胞结构、
装配边界零违例(词表零 using 纯 BCL;CasebookScreen 零游戏状态)、Gates 源面零命中、
金标重钉有意识且 +8 可复算、ModalGate 门序与词表 Armed 优先相符。

| # | 原判定(原文摘要) | 处置 | 修复落点 |
|---|---|---|---|
| S1 | **MAJOR** · AC-8-51 [I] 端到端腿无生产调用点、欠债埋代码注释无 story ID ⇒ story-done 可被词表单测借绿整条 AC | ✅ **已修**(登记面) | 卡 AC-8-51 行加**半边拆分注**(词表 [A] 已覆盖 / 端到端 [I] NOT-RUN 具名归 case-system 37 + interaction-system 4 接线轮)+ 走查件 §四.4 + Completion Notes §2 |
| S2 | **MAJOR** · 两处生产件声称「差异登记卡 Completion Notes」但卡面**为空**,证据件陈述为假;卡 AC-8-23 旧序会误判合规实现 | ✅ **已修** | 卡 Completion Notes 全量回填(焦点序差异裁定 = 权威 GDD UI-8.2 2026-10-06)+ AC-8-23 行就地订正注 + Test Evidence 回填 |
| S3 | MINOR · 同屏双真源(UXML + BuildUI)已漂移且无交叉断言 | ✅ **已修** | `CasebookScreen` 头注双真源声明 + BuildUI 补 `casebook-title` 对齐;PlayMode 新增 `test_cross_uxmlSpec_buildUi_nameSequence_equivalent`(XDocument 解析 UXML ≡ 代码树 name 序全等);唯一运行面选择归 42 接线轮(卡 Notes §4) |
| S4 | MINOR · B-2「零 Modal 成员」按成员名匹配,类型面漏检 | ✅ **已修** | `test_ac8_52_2` 增类型面扫描:8 前缀成员字段/属性/参数/返回类型**零 Gameplay.UI 来源**(名含/不含 modal 均覆盖) |
| S5 | MINOR · 词表 B-4 注释转述失真(「动作行」→「枚举成员」,引导空间分流回退) | ✅ **已修** | `DiagnosisActionLexicon` 头注忠实转述:望闻问切 = 模态内行级动作进表为新动作行,不经 `Resolve`、不扩粗态枚举 |
| S6 | NIT · 置信度「?」恒渲染无状态接缝登记 | ✅ **已修**(登记) | `CasebookScreen` 头注登记「? 显隐归 39/11 喂值接缝,屏不持置信度状态」 |

## 二、测试面(qa-lead)—— 6 MAJOR · 7 MINOR · 3 NIT

**原判定**:CHANGES REQUIRED。已核验为真(未重复报):三处跑数口径逐 XML 吻合、影子与正测同机、
`ShadowCasebookDto` 类型名零诊断 token、金标史注可复算、AC-8-42 依赖(disease story-003 Complete)属实。

| # | 原判定(原文摘要) | 处置 | 修复落点 |
|---|---|---|---|
| M1 | **MAJOR** · UXML 孤儿资产,EditMode 打 UXML 文本、PlayMode 打代码树,改任一侧另一侧不红(「测的不是跑的」) | ✅ **已修**(与 S3 同批) | cross 断言 `test_cross_uxmlSpec_buildUi_nameSequence_equivalent` + 头注/卡面权威面声明 |
| M2 | **MAJOR** · AC-8-51 无枚举闭合断言(新增第五态 / 治疗类路由成员不红) | ✅ **已修** | `test_ac8_51_fourCoarseStates_*` 补闭集断言:粗态恰 4 名、路由恰 4 名(四项穷尽) |
| M3 | **MAJOR** · `LeafNumericTypes` 对引用类型叶子零产出不报错(字段换 string 漏判);selector 类型按全名解析可打孤儿 | ✅ **已修** | 新增 `AssertIntegralShape`(引用类型/非整数基元/零字段值类型 ⇒ 显式红)+ 消费点绑定:按 `InteractionSelector.Select` 形参取类型(`in` 参数先解 byref) |
| M4 | **MAJOR** · AC-8-43 [U] 走查双签无落点(QA case 7 明文「树遍历 + 走查双签」) | ✅ **已修** | 走查件 §二补 AC-8-43 行(双签位,未签 = 未收口,禁借绿) |
| M5 | **MAJOR** · AC-8-38 走查件称「[L] 已自动化」但全 Tests 零命中 = 疑似借绿 | ✅ **已修** | 新增 `test_ac8_38_negativeRenderPath_8prefix_zeroWorldLayerTypes`(8 前缀零 13 动画/shader 类型 + 影子负夹具可红);走查件改引该测试,[V] 半边留签核位 |
| M6 | **MAJOR** · AC-8-13 双双无落点,且 `confidence_leak_test` 依赖注记已陈旧(disease story-004 现 Complete) | ✅ **已修**(登记) | 走查件 §四.7 NOT-RUN + 陈旧注记点名(回写归 9/8 投影接线轮);卡 Test Evidence NOT-RUN 清单列入 |
| M7 | MINOR · 「差异登记卡 Completion Notes」不实;卡 AC-8-44 措辞同样陈旧未登记 | ✅ **已修** | 见 S2(卡面回填);AC-8-44 旧措辞随走查件 §三/卡 Test Evidence NOT-RUN 登记 |
| M8 | MINOR · 走查件缺 AC-8-23、AC-8-31 行 | ✅ **已修** | 走查件 §一/§二补两行(含空行截图与重排场景 NOT-RUN 指针) |
| M9 | MINOR · 禁词表未覆盖 AC 点名项(READ_FLOOR/精度/图鉴/词条/已发现/对错);两套清单不一致 | ✅ **已修** | 统一 `ForbiddenUiTokens` 集合(17 token)两处同名同集,头注互指;name token 补 `read_floor`;树遍历/ UXML 属性文本两面全覆盖 |
| M10 | MINOR · AC-8-19 结构代理主张超证据(空树双建近恒真) | ✅ **已修**(降格) | 改名 `test_smoke_buildDeterminism_*`,头注明示「不是 AC-8-19 落点,像素腿 NOT-RUN 归走查件」 |
| M11 | MINOR · UXML 文本断言跑在含注释原文上(注释误红/假绿) | ✅ **已修** | `test_ui81` 改 **XDocument 元素/属性面解析**(注释天然不参与) |
| M12 | MINOR · 「不因已查集合重排」断言消息超主张;QA case 1 数据场景未登记 | ✅ **已修** | 消息收敛为「初始树序」+ NOT-RUN 明示;走查件 §四.5 登记 |
| M13 | MINOR · QA case 2「零遮挡滤镜节点断言」子半边无落点 | ✅ **已修**(登记) | 走查件 §四.6 NOT-RUN 登记(截图判) |
| M14 | NIT · 「?」断言过紧(病名行本身合法)且只查 Label | ✅ **已修** | 放宽祖先链为 `right-column` 必经 + `five-channels` 必不经;查询改 `TextElement` 全载体 |
| M15 | NIT · 两套禁词清单漂移 | ✅ **已修**(与 M9 同批) | 统一集合 + 头注互指 |
| M16 | NIT · 右栏裸数字 badge 未覆盖 | ✅ **已修** | `test_ac8_23_emptyRows_*` 补右栏断言:文本 ⊆ {病名, ?} 且零数字 |

---

## 三、AC ↔ 覆盖表(评审时点 → 修复后)

| AC | 评审时点 | 修复后 |
|---|---|---|
| AC-8-23 结构/焦点序 | Covered(初始树序) | + 消息收敛 + NOT-RUN 登记(M12);截图/重排半边走查件 |
| AC-8-51 词表 | Covered 但缺闭集(M2) | ✅ 枚举闭合四项穷尽;端到端半边拆分登记(S1) |
| AC-8-52① 形状 | Covered 但叶子漏判(M3) | ✅ `AssertIntegralShape` + 消费点绑定 |
| AC-8-52② 裁决输入 | 名字面 Covered(S4 漏) | ✅ 名字面 + 类型面(零 Gameplay.UI)双扫 |
| AC-8-38 [L] | **Defective**(M5 借绿) | ✅ 新测试 + 影子负夹具;[V] 归走查件 |
| AC-8-13 | **缺落点**(M6) | ✅ NOT-RUN + 陈旧注记登记 |
| AC-8-43 | 树遍历 Covered,双签缺(M4);禁词不全(M9) | ✅ 双签行 + 禁词 17 token 两面 |
| AC-8-39 | Covered 但过紧(M14) | ✅ 放宽右栏 + TextElement 全载体 |
| AC-8-19 | 代理超主张(M10) | ✅ 降格冒烟;像素腿 NOT-RUN 如实 |
| TR-019/020 | Covered + 影子 | 不变 |
| 双真源 | **未守护**(M1/S3) | ✅ cross 断言 + 突变 2 证红 |
| 焦点序差异 | 登记位置为假(S2) | ✅ 卡面订正注 + Completion Notes 回填 |

---

## 四、验证(可证伪)

**修复后复跑**(2026-10-08,批独占锁确认后执行):

```bash
# 1) DiagnosisSystem 过滤(150 → 157:7 新测含 AC-8-38)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiagnosisSystem" \
  --output unity/Logs/story006-emfinal.xml
# ⇒ 157 total / 156 过 / 0 红 / 1 跳(跳 = 既有 test_ac834_reverseOrphan_blockedish)

# 2) 全量 EditMode(基线 2978/2931)
unity test unity --mode EditMode --output unity/Logs/story006-full3.xml
# ⇒ 2979 / 2932 过 / 0 红 / 1 inconclusive / 46 跳(+1 = AC-8-38 新测)

# 3) PlayMode(Required evidence:casebook_render_test 8 测)
unity test unity --mode PlayMode --filter "DaYiJingCheng.Tests.PlayMode.DiagnosisSystem" \
  --output unity/Logs/story006-pmfinal.xml
# ⇒ 11 / 11 过 / 0 红(3 既有 + 8 新)
```

**XML 解析口径**:按根节点 `type="TestSuite" name="unity"` 取 total/passed/failed。
> 全量 CLI `exit 2` = 既有 `SettingsExposureTest` 1 条 Inconclusive 所致,基线同形。

**突变实跑记录**(§六);编译期修复两笔(CS0104 歧义 typeof / `in` 参数 byref Name 带 `&`),
均由测试自身失败暴露、当场修正。

---

## 五、残余与边界(不静默)

- **NOT-RUN 全清单**(走查件 §四 + 卡 Test Evidence):AC-8-44 手柄(缓办 + 假设 6 spike)·
  AC-8-19 像素腿(batch 无渲染面)· [V]/[U] 签核位全部未签 · AC-8-51 端到端接线(具名 37/4)·
  AC-8-13 [I](依赖注记陈旧,回写归 9/8 投影接线轮)· AC-8-23 重排数据场景与空行截图半边 ·
  AC-8-36 遮挡节点断言子半边。**全部禁借绿。**
- **登记不修**:S6 显隐接缝(归 39/11 接线)· 双真源唯一运行面选择(归 42 接线,
  `SkeuoRuntimeDriver` 现 TODO)· M9 禁词集为命名面防线(改文案即绕 —— 与走查双签互补)。
- **金标重钉** `7ce0ce27` → `b711a17c` = **有意识重钉**(+8 = PatientCoarseState 4 +
  VisitRoute 4 枚举字面,史注写入 `DiagnosisGoldenScan`,结构侧核验非掩盖)。
- **焦点序裁定**:权威 = GDD UI-8.2(2026-10-06 订正面),卡 AC-8-23 旧序加订正注 ——
  非静默,双处登记(卡行注 + Completion Notes §1)。
- 本件即 `review-workflow.md` 要求的评审报告原件;评的是**修复轮落笔后的当下代码**,
  原报告经单轮双代理产出、未改任何文件。

---

## 六、突变实跑记录(2026-10-08)

| 注入 | 期望红 | 实测 |
|---|---|---|
| ① `DiagnosisActionLexicon.Resolve` 的 Armed 压制分支改 `if (false && …)` | `test_ac8_51_fourCoarseStates_pressed_alwaysVisitArmedZero` | ✅ **恰 1 红且点名**(157 条:155 绿 / 1 红,零附带) |
| ② `CasebookScreen.BuildUI` 删 `casebook-title` 行 | `test_cross_uxmlSpec_buildUi_nameSequence_equivalent` | ✅ **恰 1 红且点名**(11 条:10 绿 / 1 红,零附带) |

两笔均 python 反向替换还原(`grep -c "false &&"` = 0 / `grep -c "MUT"` = 0)⇒ 复跑
EM-final **157/156 回绿**、PM-final **11/11 回绿**(§四)。

> M2/M3/M5/M14 的判别力为测试内置双侧自证(闭集 AreEquivalent + 影子 IsNotEmpty 同机 +
> `AssertIntegralShape` 显式 Fail),其余可证伪注入见 §三逐条;本轮两笔突变覆盖
> **词表语义**与**双真源交叉守护**两条新增承重断言。
