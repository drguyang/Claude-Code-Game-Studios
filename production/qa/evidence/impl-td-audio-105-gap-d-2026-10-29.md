# 实现证据 —— `TD-audio-105` 缺口 D(13 的可见对应物)

- **日期**:2026-10-29
- **用户指令**:「完成DD」
- **缺口定义出处**:`production/qa/evidence/td-audio-105-closure-requirements-2026-10-29.md` §二 缺口 D
- **对象**:
  - `unity/Assets/Gameplay.UI/Skeuomorphic/PatientCueMark.cs`(新)
  - `unity/Assets/Gameplay.UI/Skeuomorphic/PatientCueMarkView.cs`(新)
  - `unity/Assets/Gameplay.Boot/PatientCueVisualMap.cs`(新)
  - `unity/Assets/Gameplay.Boot/PatientCueDriver.cs`(改)
  - `unity/Assets/Gameplay.Boot/CompositionRoot.cs`(改)
  - `unity/Assets/Gameplay.Boot/BootRoot.cs`(改 · **唯一生产装配点**)
  - `unity/Assets/Tests/EditMode/SkeuomorphicUI/patient_cue_mark_view_test.cs`(新)
  - `unity/Assets/Tests/EditMode/Boot/patient_cue_mark_delivery_test.cs`(新)

---

## 一、缺口的权威原文(逐字)

| 出处 | 原文 |
|---|---|
| `design/gdd/patient-ai.md:1088` **AC-13-F1** | 「**咳嗽 / 呻吟 / 呼吸三类 cue 均有一个可见对应物**(姿态变化 / 面色变化 / 呼吸幅度变化),且该对应物在**默认设置**下亦可见(**不需额外开关**)」 |
| 同处 **2026-09-25 二轮 44 无障碍回填** | 「对应物须**非色相可辨**(**形状 / 明度 / 姿态**承载)」—— 原句的「面色变化」是纯色通道(WCAG 1.4.1),色觉盲玩家读不出 |
| `design/gdd/audio-system.md:1131` **AC-44-12** ① | 「视觉替代 = 引用 `AC-13-F1`(13 已交付,默认可见)」 |
| 归口 | `td-audio-105-closure-requirements-2026-10-29.md` §二:「**归属 = 42**(13 已交付它该交付的一半)」 |

**13 侧自陈**:`presentation_projection_test.cs:921` 的两条夹具逐字写着
「本测只判 AC-13-F1 的 **13 侧结构前提**,**不**判 AC 原文的『可见对应物 / 非色相可辨』
—— 那两条的**达成面在 42 的冗余通道**」。

---

## 二、为什么落在 UI 层(而不是「姿态 / 面色」)

AC 原文括注的三例是 **姿态变化 / 面色变化 / 呼吸幅度变化** —— 都是**病人本体**的
world-space 表现。而:

- **面色变化已被 2026-09-25 回填明确否定**(纯色通道,WCAG 1.4.1);
- **姿态 / 呼吸幅度**需要病人本体渲染 —— P0 **没有**这个载体:
  `Gameplay.Presentation` 下与病人相关的只有音频面(`BreathLayerDriver` / `CoughChannel` …),
  零病人 view / avatar。

⇒ 本轮交的是回填**明确允许**的第三条承载:**形状**(+ 字重,即明度)。
**不主张**已交付「姿态变化」,也不主张「面色变化」—— 见 §六。

---

## 三、落点

| # | 落点 | 内容 |
|---|---|---|
| D-1 | `PatientCueMark.cs` | `PatientCueVisual` 三态枚举 · `PatientCueMarkData`(全整数,无 `disease_id`)· `IPatientCueMarkSink`(`Mark` / `ClearPatient` / `ClearAll`)· `PatientCueMarks`(形状标记 + USS 类的**单一出处**) |
| D-2 | `PatientCueMarkView.cs` | UI Toolkit 容器;三态各一形状标记;容量有界;默认可见 |
| D-3 | `PatientCueVisualMap.cs` | 13 的 `CueKind` → 42 的三态(装配层唯一的跨层翻译点) |
| D-4 | `PatientCueDriver` | 一次性 cue → `Mark(Cough/Groan)`;呼吸 `Begin` → `Mark(Breath)`;`PruneOneAbsent` → `ClearPatient`;`StopAll` → `ClearAll` |
| D-5 | `CompositionRoot` | `Assemble` 三层签名增 `cueMarkSink`(默认 null) |
| D-6 | `BootRoot` | **唯一生产装配点**:装配前造元件、装配后挂 `UI` 根 |

### 3.1 「非色相」是怎么做到的(不是形容词)

两通道**同时**承载,任一条单独存在都不够:

| 通道 | 载体 | 三态取值 |
|---|---|---|
| **形状** | 渲染出的形状标记 | `✳`(爆点 = 突发)/ `〰`(波形 = 绵长)/ `◎`(环 = 周而复始) |
| **明度** | USS 类(字重) | `cue-mark-cough` / `cue-mark-groan` / `cue-mark-breath` |

机械判据 = **渲染文本本身两两不同**(`test_renderedTexts_differPerKind`)。
若日后有人把区分退化成「同一字形换颜色」,三态文本会变得相同 ⇒ 该测**转红**。

### 3.2 形状所有权:新增族,不是借用

`skeuomorphic-ui.md` §Visual 二:「每个形状只有一个拥有者、一个语义;**新增语义须新增形状,
不得借用**」。`MarkRegistry` 已有六形(勾 / 点 / 叠角 / 划改痕 / 印 / 折角)。
本件的三形与六形**不相交**,由 `test_shapes_disjointFromMarkRegistry` 钉住。

### 3.3 为什么必须有界

一次性 cue(咳嗽 / 呻吟)**没有「结束」信号** —— 上游发一次就完,42 又不能自己计时
(计时 = 判时机,`E-12b` 禁)。若无限累积,屏上会堆满字形。
⇒ 容器只留末 `MaxMarks = 3` 条。**这是渲染容量,不是业务保留期**(与 `SubtitleLineView.MaxLines` 同性质)。

---

## 四、四处设计判断(都不是照抄)

**① 映射按语义,不按 ordinal。**
`CueKind` 是 13 的枚举;ordinal 是事件表行序(ADR-014 §五 登记的脆弱面)。
拿 ordinal 选可见物会让「重排 `audio_events.json` ⇒ 咳嗽显示成呻吟」成为静默故障。

**② 未知语义**抛,不回落。**
`PatientCueVisualMap.Of` 三分支具名;13 日后扩枚举时**具名抛出** ——
「新语义静默显示成咳嗽」比崩溃更难发现。

**③ 可见物**不**依赖 `audio != null`。**
字幕那一跳挂在 `audio` 上(文本本体就在事件表里,表没有则无字幕);
但可见物是给听障玩家的替代通道 —— 若也挂在音频装配上,**音频失败会连带夺走无障碍面**。

**④ 字段叫 `ShapeByKind`,不叫 `GlyphByKind`(全量回归抓到后改名)。**
首版命名为 `GlyphByKind`,撞上 **AC-42-G6 字形门**
(`data_boundary_final_test.cs:230` `test_ac42g6_noGlyphGeneration_noGlyphFields`):

```
PatientCueMarks.GlyphByKind 是字形生成字段
```

该门的判据是「42 **只消费 8 交来的字形变体族,不自行生成字形**」—— 字段名词表
`{glyph, font, character, typeface}`(限引用类型)。
**处置 = 改我的命名,不放宽门**:AC-13-F1 原文用的词就是「**形状** / 明度 / 姿态承载」,
而这三样本来就是**形状标记**(不是字形的变体族)⇒ `ShapeByKind` / `ShapeOf` 更准。
门的要求(font / character / typeface / 字形变体生成)一格未动。

---

## 五、证伪轮

### 5.1 轮次设置

| 项 | 值 |
|---|---|
| 脚本 | `/home/gu/.claude/jobs/181888b1/tmp/falsify_d.py`(16 变异)+ `falsify_d5.py`(重做第 16 条) |
| 变异面 | `PatientCueMark.cs` / `PatientCueMarkView.cs` / `PatientCueDriver.cs` / `PatientCueVisualMap.cs` |
| 靶夹具 | 2 件 19 条(元件级 12 · 投递级 7) |
| 还原判据 | 每轮 `finally` 写回原文 + **SHA-256 逐字节比对**;脚本末再整体比一次四件 |
| 基线 | `c` = 12/0 · `b` = 7/0 —— **两件全绿,零失败** |

### 5.2 结果 —— 16/16 全部转红

| # | 变异(停用什么守卫) | 红 | 转红的测试 |
|---|---|---|---|
| D-V1 | 三态共用同一字形 | 2/12 | `test_threeKinds_haveDistinctShapes` · `test_renderedTexts_differPerKind` |
| D-V2 | 三态共用同一 USS 类 | 1/12 | `test_threeKinds_haveDistinctUssClasses` |
| D-V3 | 构造即 `display: None` | 1/12 | `test_defaultVisible_withoutAnyToggle` |
| D-V4 | 加一个 `VisualRedundancyEnabled = false` | 1/12 | `test_noToggleMembers_reflection` |
| D-V5 | 加一个 `_autoHideSeconds` 成员 | 1/12 | `test_noTimingMembers_reflection` |
| D-V6 | 同病人同类不刷新(直接堆) | 1/12 | `test_samePatientSameKind_refreshesInsteadOfDuplicating` |
| D-V7 | 容量无界(删 trim) | 1/12 | `test_capacity_boundedAtMaxMarks` |
| D-V8 | `ClearPatient` 清掉别家 | 1/12 | `test_clearPatient_removesOnlyThatPatient` |
| D-V9 | `ClearAll` 只清表不重建(子件残留) | 1/12 | `test_clearAll_empties` |
| D-V10 | 越界不抛(取默认) | 1/12 | `test_outOfRangeKind_throws` |
| D-D1 | 咳嗽不投递可见物 | 1/7 | `test_cough_markDelivered` |
| D-D2 | 呼吸不投递可见物 | 2/7 | `test_breath_markDeliveredOnBegin` · `test_breath_notRedeliveredEveryTick` |
| D-D3 | `PruneOneAbsent` 不摘可见物 | 1/7 | `test_pruneAbsent_clearsThatPatient` |
| D-D4 | `StopAll` 不清可见物 | 1/7 | `test_stopAll_clearsEverything` |
| **D-D5b** | **未知语义回落咳嗽**(把 `throw` 本身换成 `return`) | 1/7 | `test_visualMap_coversBothCueKinds_andRejectsUnknown` |
| D-D6 | 呻吟映射成咳嗽 | 1/7 | `test_visualMap_coversBothCueKinds_andRejectsUnknown` |

四件 SHA **`OK` ×4**;D-D5b 单独重跑时亦报「还原: OK」,并用 `grep -c "throw new System.ArgumentOutOfRangeException"` = **1** 复核。

### 5.3 一条**假存活**:病因是变异打错靶,不是夹具弱(如实登记)

首轮 **D-D5 报「假存活」**。查因:我把回落写成

```csharp
throw new ArgumentOutOfRangeException(...);   // 原句
#pragma warning disable CS0162
return PatientCueVisual.Cough;                // [MUTATED] —— 死代码
```

`return` 在 `throw` **之后**,不可达 ⇒ `throw` 照常执行 ⇒ 夹具当然还是绿。
**这条变异根本没打到靶上。** 改成「把 `throw` 语句本身替换为 `return`」后重跑 ⇒ **RED 1/7**。

> ⚠️ 记法:`x = File.ReadAllText()` → `Replace` → **回读文件确认目标字符串真的变了**
> —— 只核「已应用」不够,还要核「改掉的是不是执行路径上的那一句」。
> 同族纪律见 `project-falsification-probe-pitfalls`「变异打错靶」。

### 5.3b 三条变异因**后续改动**失效,已重做(否则记录失真)

收口时全量回归抓出两处必须改的东西(见 §4 ④ 与 §7.2):`Glyph` → `Shape` 改名、
`EmitOneShotIfDue` 拆出 `DeliverVisualChannels`。**这三处改动让三条变异的锚点文本不复存在** ——
若照抄首轮结果,记录的就是「改之前的代码被证伪」,不是当下的树。

| 原编号 | 失效原因 | 重做结果 |
|---|---|---|
| D-V1 | 锚点 `GlyphByKind = { ... }` 已改名 `ShapeByKind` | **RED 2/12** → `test_threeKinds_haveDistinctShapes` · `test_renderedTexts_differPerKind` |
| D-V10 | 锚点 `i >= GlyphByKind.Length` 已改名 | **RED 1/12** → `test_outOfRangeKind_throws` |
| D-D1 | 锚点 `if (_cueMarkSink != null)` 已随拆方法消失 | **RED 1/7** → `test_cough_markDelivered` |

脚本 `falsify_d_redo.py` 单独跑这三条,基线 12/0 + 7/0,还原 SHA **`OK` ×2**,总判定「全部转红」。
⇒ **16 条变异全部在当下这棵树上各有一次转红记录。**

### 5.4 按 S12-4 命中形态归类

| 命中形态 | 本轮的落点 | 反证记录 |
|---|---|---|
| **共存/互斥** | 三态字形**两两互异**(非色相的机械判据) | 停用字形差异 ⇒ 2 条红(含「渲染文本须两两不同」) |
| **阈值边界** | 容量上限 `MaxMarks = 3` | 删 trim ⇒ `test_capacity_boundedAtMaxMarks` 红 |
| **一次性/幂等** | 同病人同类只留一条;呼吸形不逐拍重投 | D-V6 / D-D2 各红 |
| **定义域收窄** | 越界枚举值须抛出;未知语义不得回落 | D-V10 / D-D5b 各红 |
| —— | 其余(默认可见 / 无开关 / 无计时 / 指定摘除 / 全清含子件 / 两条投递路径 / 离场摘除 / StopAll / 呻吟映射) | 各红 ≥ 1 条 |

### 5.5 已知的判别力边界(**不是**假绿,是覆盖范围)

| 面 | 说明 |
|---|---|
| 「真屏幕上看得见」 | EditMode 无 panel(逻辑树在、不渲染)。本件判到 `display` / `Label.text` / 子件数为止;**实际像素**不在本机可判面内 |
| `Groan` 端到端 | 13 的调度在 P0 恒传 `CueKind.Cough` ⇒ 该支只有**映射级**判据,**零端到端** |
| `BootRoot` 的挂载(D-6) | 场景启动序,EditMode 无载体;其存在性由 `grep` 佐证,**有效性**归 PlayMode/真机 |

---

## 六、本件**不**主张(禁借绿)

| 项 | 状态 |
|---|---|
| 「姿态变化 / 面色变化」已交付 | ❌ 两者都需要病人本体渲染,P0 无载体;本件只交回填允许的**形状 / 明度**承载 |
| `Groan` 端到端可达 | ❌ 13 的调度在 P0 **恒传 `CueKind.Cough`**(`PatientCueSchedule.Decide` 的调用点)⇒ 呻吟这一支**今日无生产生产者**,只由夹具直测映射 |
| 呼吸的「渐弱」有可见形态 | ❌ 终局的连续衰减属动效面(42 不判时机),登记为 P1 细化项 |
| `AC-13-F1` 已绿 | ❌ AC 是 13 侧登记的 `[A]` 项,其 signoff(`NR-S3-1`)未跑;本件只把 42 侧的**达成面**补上 |
| `AC-13-D3` 姿态档的呈现 | ❌ `CueDispatch.PostureTier` 的呈现映射同样归 42,**本轮未做**(与缺口 D 同族但不同项) |
| `TD-audio-105` 可结案 | ❌ 见下表 —— 四条判据仍有 2 条未绿 |

### 6.1 结案判据四条现值

| # | 判据(`td-audio-105-closure-requirements-2026-10-29.md` §三) | 现值 | 说明 |
|---|---|---|---|
| 1 | `.mixer` 暴露参数含 `mono_enabled`,开关两态**实测有可闻差异** | ❌ | **判据措辞本身已作废**(§一实测:mono 是**设备级** `AudioSpeakerMode`,不是 mixer 参数)。代之以「mono 开关两态有可闻差异」—— **需人工听测**,未跑 |
| 2 | 发语声 cue ⇒ 42 侧收到 `subtitle_text`(端到端夹具) | ✅ | A/B/C 批已落:`test_withSubtitleSink_bothAudioAndTextAreDelivered` |
| 3 | 42 侧字幕**在屏幕上可见**且符合 49 的呈现规格 | ❌ | 元件存在、呈现规格归 49(P2);**且今日无生产挂载点**(字幕尚未挂到 UI 根 —— 与可见物不同,字幕的 `Show` 无自动收尾,直接挂会留常驻残行) |
| 4 | 咳嗽 / 呻吟 / 呼吸三类各有一个**默认可见 + 非色相**的对应物 | ✅(附注) | 本件;**附注 = `Groan` 无生产生产者**(13 恒传 `Cough`)⇒ 该类只有映射级判据 |

⇒ **`TD-audio-105` 维持 `open`**,余项 = ① 人工听测 · ③ 字幕生产挂载 + 49 的 P2 规格。

---

## 七、回归三元组

### 7.1 结果

| 套件 | total | passed | failed | inconclusive | skipped | 退出码 |
|---|---|---|---|---|---|---|
| **EditMode** | **5974** | **5937** | **0** | 0 | 37 | 0 |
| **PlayMode** | **238** | **234** | **0** | 2 | 2 | 2 |

- **截至时刻**:2026-10-29 19:59 CST
- **批次**:全量(非增量)
- **commit**:`0e6a5152` + 工作树(本轮改动未提交)
- 复算命令 = `/home/gu/.claude/jobs/181888b1/tmp/regress_abc.py`

### 7.2 与基线的对照(等式必须成立)

| 项 | 基线(A/B/C 批) | 本次 | 差 | 等式 |
|---|---|---|---|---|
| EditMode total | 5955 | 5974 | **+19** | 新增夹具 **19** 条(元件级 12 + 投递级 7)= **恒等** |
| EditMode passed | 5918 | 5937 | **+19** | 同上 |
| EditMode skipped | 37 | 37 | 0 | 不变 |
| PlayMode | 238/234/0/2/2 | 238/234/0/2/2 | **逐位相同** | 本轮不动 PlayMode 面(`BootRoot` 挂载未扰) |

> 基线出处:本会话上一批全量 @ 2026-10-29 19:14 CST · `e22d2bd8`+工作树。
> **新增夹具条数可独立复算**:`grep -c 'public void test_'` 两件 ⇒ 12 + 7 = **19**。

### 7.3 首轮回归的 2 红与收口(如实登记)

首轮全量 EditMode = `5974 / 5935 / 2 / 0 / 37`:

| # | 红门 | 病症 | 处置 |
|---|---|---|---|
| 1 | `test_td032_3_splitMethods_withinLineBudget`(`s15_5_hygiene_test.cs:331`) | `EmitOneShotIfDue` 实得 **41** 物理行 > 40 —— 我把字幕 + 可见物两段都塞了进去 | 抽出 `DeliverVisualChannels`,现 **14** 行 |
| 2 | `test_ac42g6_noGlyphGeneration_noGlyphFields`(`data_boundary_final_test.cs:230`) | 字段名 `GlyphByKind` 撞 `{glyph, font, character, typeface}` 词表 | 改 `ShapeByKind` / `ShapeOf` —— **改命名,不放宽门**(理由见 §4 ④) |

**两条都是真阳性,不是误报**,且都是**只跑本件夹具看不见**的跨件机械门。
修后单件复验:行预算 `14/0` · 字形门 `8/0` · 两件 D 夹具 `12/0` + `7/0`;再跑全量 = 上表零失败。
