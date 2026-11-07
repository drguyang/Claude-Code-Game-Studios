# 实现证据 —— S25-5 零设门 / 零按键提示 / 零状态播报音守卫

**日期**: 2026-11-07
**Story**: `production/epics/tutorial-onboarding/story-005-zero-gate-zero-keyhint-guards-walkthrough.md`
**承接**: `design/gdd/tutorial-and-onboarding.md` 规则五 · AC-48-17 · AC-48-01/02 · AC-48-12 · 第一铁律
**门本体**: `unity/Assets/Editor.Tools.Gates/TutorialZeroGate.cs`
**夹具**: `unity/Assets/Tests/EditMode/Tutorial/tutorial_zero_gate_guard_test.cs`(10 条)

---

## 0. 结论先行

| 项 | 结果 |
|---|---|
| AC-48-17 零设门 | ✅ 有构建期门 + EditMode 正控 + G1/G2 双负夹具 + 空扫描面守卫 |
| AC-48-01/02 素材闭集 | ✅ 同一件的 `ScanAssetIndex`,真 48 目录零违规 + 临时目录负夹具 |
| 零按键提示(联合) | ✅ 复用 `TutorialContentSchema.ExplicitInstructionTokens`,**未另写词表** |
| AC-48-12 零状态播报音 | ✅ 逐 cue 回查 44 事件表(`whitelist_category` / `trigger_source`) |
| **AC-48-07 [L] / AC-48-05 [L]** | ⚠️ **NOT-RUN / BLOCKED-BY-SPEC —— 不代签** |
| **本轮发现并修掉的真实违规** | 🔴 `TutorialProjection.Project` 公开返回 `TutorialProgressView` ⇒ 规则五此前**不成立** |

---

## 1. 修掉的那处真实违规(不是「补判据」,是「既有代码违反既有规则」)

### 症状

`TutorialZeroGate` 首跑(G2 返回类型黑名单)即红:

```
[AC-48-17] Gameplay.UI.Tutorial 的公开成员「TutorialProjection.Project」**返回类型**
「TutorialProgressView」命中门禁词「progress」—— 改名旁路(方法名干净、类型仍露进度)。
```

### 为什么这不只是「命名不好看」

`design/ux/tutorial-48.md:57` 逐字写着:「48 的 API 面被 AC-48-17 断言禁止暴露进度查询;
若本屏画步列表,等于用 UI 绕过该断言」。而 `TutorialProgressView` 携带 `CompletedSteps` 一类的完成态。
⇒ **任何玩法系统(病例生成 / 建造 / 出诊)只要 `using DaYiJingCheng.Gameplay.UI.Tutorial` 就能读到「玩家走到第几步」**,
规则五要求的「想设门也拿不到接口」当时是假的。

### 处置 = 内化,不是改名

| 类型 | 前 | 后 | 理由 |
|---|---|---|---|
| `TutorialStepCompletion` | `public` | `internal` | 步骤完成态的载体,经 `TutorialProgressView` 可达 |
| `TutorialProgressView` | `public` | `internal` | **进度视图本体** |
| `TutorialProjection` | `public` | `internal` | `Project(...)` 返回进度视图 |
| `TutorialTrigger` | `public` | `internal` | `HasFired` / `FireCount` 是完成态查询 |

新增 `unity/Assets/Gameplay.UI/Tutorial/AssemblyInfo.cs`:

```csharp
[assembly: InternalsVisibleTo("Gameplay.Boot")]
[assembly: InternalsVisibleTo("Sim.Contracts.Tests")]
```

⚠️ **为什么不能靠改名**:把 `GetProgress()` 改成 `Get()` 后 G1 不命中,但只要返回类型还是
`TutorialProgressView`,G2 照样抓住 —— 这正是本件的 G2 存在的意义。而 G2 若也绕过(改成返回 `object` / 元组),
真正的封堵是 **`internal`**:`internal` 让「拿到接口」在**编译期**不可能,不依赖任何命名约定。
⇒ 命名黑名单是**机械判据**(会红、可执行),内化是**结构性判据**(做不到)。两者要同时在。

### 连带代价(如实记)

`tutorial_assembly_guard_test.test_ac48_04_exportedSurface_hasNoSimTypes` 的**锚点类型**原本是
`TutorialProjection`(用来断言「扫到的确实是 48 装配、且公开面非空」)。内化后它不在公开面 ⇒ 改为
`TutorialDispatcher`(**仍然 public**,是调度器本体,不含完成态查询)。

AC-48-04 的另一半(编译产物引用集不含 `Sim*.Contracts`)是**装配级**判据,与公开面无关,不受影响。

---

## 2. 门本体:`TutorialZeroGate`(构建期)

接进 `AssemblyGates.RunAll()`,三处强制点:

1. 调用:`RunAll()` 内 `TutorialZeroGate.RunAll();`
2. `EntryWiringManifest` 声明 `TutorialZeroGate.RunAll`
3. `ClassVerdict` 声明该类的归属

⚠️ **第一步落地时 `gate_wiring_manifest_test` 红 1 条**(新入口未声明)。**门在正常工作**,
处置 = 补声明(不是改夹具)。这与 S25-1 的 `b2-exact` 首跑同型。

### 三条判据

| 判据 | 实现 | 空集行为 |
|---|---|---|
| **G1 标识符黑名单** | 公开类型名 + `DeclaredOnly` 公开成员名,小写子串匹配 `{progress, unlocked, gate, intutorial, tutorialstate, canaccess}` | — |
| **G2 返回类型黑名单** | 方法返回类型 / 属性类型 / 字段类型 / 事件委托类型,同一词表 | — |
| **资产后缀闭集** | 48 目录只允许 `.cs/.asmdef/.meta/.md` | 空目录 / 目录不存在 ⇒ **不报错**(48 今日零自有资产是合法态) |
| **扫描面非空** | 扫描到 0 个类型 ⇒ 报错 | 装配载入失败 / 命名空间搬家都会走到这里,**不静默** |

### 装配缺失不静默

`Assembly.Load("Gameplay.UI.Tutorial")` 失败 ⇒ 返回错误串而非空表。
理由写在源码注释里:静默跳过会让 AC-48-17 **悄然消失**(F6「自述顶替门」的同族形态)。

---

## 3. 证伪轮(4 条变异,各自单红)

| # | 变异 | 靶 | 期望 | 实测 |
|---|---|---|---|---|
| **M1** | `HitToken` 恒 `return false`(词表永不匹配) | G1 名称判定 | 负夹具 + 真装配正控转红 | ✅ **2 条红** |
| **M2** | `TutorialProgressView` + `TutorialStepCompletion` 改回 `public` | 真 48 装配的公开面 | `test_ac48_17_publicSurface_hasNoGateApi` 红 | ✅ 红 |
| **M3** | 删掉 `TryReturnTypeName` 分支(停用 G2) | 改名旁路 | `test_ac48_17_fixture_cleanNameButProgressReturnType_isRed` 红 | ✅ 红 |
| **M4** | `AllowedFileSuffixes` 加 `.uxml` | 资产闭集 | `test_ac48_01_fixture_tooltipUxml_inTutorialDir_isRed` 红 | ✅ 红 |

### M2 的一个如实记录

首次 M2 只把 `TutorialProgressView` 改 public,**编译失败**(`CS0053`/`CS0051`:
方法返回 public 类型但 `TutorialStepCompletion` 仍 internal)⇒ 变异未生效、测试自然不红。
**补全变异**(两类型同时改回 public)后才转红。

⚠️ 这类「变异编译不过 ⇒ 看起来没抓住」是证伪轮的已知假存活形态之一
(`tools/falsify/README.md`),记在此处以免后人误读为「门没牙齿」。

### 还原

**备份 + `cp` 回 + SHA-256 逐字节比对**(不用 `git checkout` —— 它会抹掉未提交改动):

| 文件 | SHA-256 |
|---|---|
| `unity/Assets/Editor.Tools.Gates/TutorialZeroGate.cs` | `9b70f2a3dcfaa9e8384caa5eb6bd2169fed721251bc22641728a1a40120b8f01` |
| `unity/Assets/Gameplay.UI/Tutorial/TutorialProjection.cs` | `cf1c78908cc981b1e516e40eaf08d1da66d2fed41e3ed664358ee7a2cb8eba06` |

`grep -rn "FALSIFY" unity/Assets` = **0**。

---

## 4. 零按键提示 —— 复用同一张词表,不另写一份

Story 的 Control Manifest Guardrail 要求「黑名单词族与 39 Story 004 扫描器**共享夹具源**(一处扩两处生效,禁分叉)」。

判据直接用 `TutorialContentSchema.ExplicitInstructionTokens`(烘焙校验 5 的**同一个常量对象**),
扫描面 = 48 内容表里**全部用户可见文本**:

- `step.title`
- `narration.text`
- `paper.title` / `paper.body`

实测**零命中**。若有人在校验器里扩充词族,本判据自动跟随 —— 因为它们读的是同一个字段,不存在「两处表」。

---

## 5. AC-48-12 零状态播报音

48 的每条口述 cue 逐条回查 44 事件表(`AudioEventCookedCodec.Read`):

| 检查 | 判据 |
|---|---|
| cue 存在 | `byCue.TryGetValue(n.CueId)` 命中 |
| 白名单类别 | `whitelist_category ∈ AudioEventTableGates.WhitelistCategories` |
| 非状态播报 | `trigger_source ∉ AudioEventTableGates.ForbiddenTriggerSources` |

后者 = ADR-018 §六 的「无提示音铁律」的机械形态:不得用声音**报**状态
(「学会了 X」jingle / sting)。

---

## 6. 回归三元组

| 套件 | 计数 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7086 / 7049 / 0 / 0 / 37` | `2026-11-07T14:42:13Z → 14:44:12Z` | sprint-25 收口后 S25-5 | `923890a5` + 工作树 |
| PlayMode | `365 / 361 / 0 / 2 / 2` | `2026-11-07T14:32:13Z → 14:32:34Z` | 同上 | 同上 |

**基线** `7076 / 7039` @ `2026-11-07T13:46:21Z` ⇒ **+10 / +10,零回归**。

**等式判据**:`7086 − 7076 = 10` = `tutorial_zero_gate_guard_test.cs` 的 `[Test]` 计数。

```bash
grep -c "\[Test\]" unity/Assets/Tests/EditMode/Tutorial/tutorial_zero_gate_guard_test.cs   # ⇒ 10
```

⚠️ `tutorial_assembly_guard_test.cs` 本轮**只改锚点**(`git diff --stat` = 6 插 2 删,零新增 `[Test]`),
其 `test_ac48_11_buildGateWhitelist_matchesTheAsmdefFace` 早在 `cdbefccc` 入库并已计入 `7076` 基线。

**复算命令**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 - <<'PY'
import xml.etree.ElementTree as ET
for f in ("unity/Logs/s25-5-em-final.xml","unity/Logs/s25-5-pm.xml"):
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ("total","passed","failed","inconclusive","skipped")})
PY
```

---

## 7. 仍未做(不得借绿)

| 项 | 状态 | 原因 |
|---|---|---|
| **AC-48-07 [L] 无视教学零惩罚走查** | **NOT-RUN** | 走查对象不全:六步里 `archive` 生产恒不成立(7a 无回执读面)、示范 / 纸两腿无触发面 |
| **AC-48-05 [L] 手柄六步全程走查** | **BLOCKED-BY-SPEC** | `OQ-C4` 三媒体节奏 spec 仍欠;`OpenModal(PaperCloseup48)` 今日**零调用点** ⇒ 「读纸」那一步走不了 |
| `TutorialTrigger` 的对外暴露设计 | 残留判断 | 本轮整类内化以避开 G1;若将来 48 确需对外暴露它,须重判「`HasFired`/`FireCount` 该不该存在」 |
| 联机面 | BLOCKED-BY-45 | 同 S25-1/S25-2 |

> **口径句**:「守卫组全绿」= 「48 **不可能**设门」(静态、结构性);
> 「AC-48-07 走查通过」= 「玩起来**真的**没惩罚」(动态、观测性)。**不同层,不得互相顶替。**

---

## 8. 与 S25-1 / S25-4 的边界(勿混记)

| 件 | 管什么 | 一句话 |
|---|---|---|
| S25-1 `b2-exact` | 48 **引用了谁** | 引用集恰 = `{Gameplay.Presentation}`(编译期) |
| **S25-5 `TutorialZeroGate`** | 48 **暴露了什么** | 公开面零门禁词(构建期 + EditMode) |
| S25-4 | 三媒体**怎么播** | 调度 / 字幕 / 音频投递 |

前三者**正交**:任一单独存在都不足以支撑规则五 —— `b2-exact` 防「48 自己设门」,
`TutorialZeroGate` 防「48 把门卖给别人」,S25-4 管的是媒体呈现,与门禁无关。
