# SIGN-OFF —— patient-ai(13)story-003 `[L]` 无障碍三判据

> **对象**: `design/gdd/patient-ai.md` **AC-13-F1 / F2 / F3**(§Visual/Audio 三,`[A]` ADVISORY)
> **story**: `production/epics/patient-ai/story-003-presentation-projection-and-view-apis.md`(Test Evidence 第 2 项)
> **夹具**: `unity/Assets/Tests/EditMode/PatientAI/presentation_projection_test.cs`
> **日期**: 2026-10-05 · **轮次**: 单轮(承用户「评审只做一轮」)

---

## 〇、本文件的性质

**story Implementation Note 5 逐字**:
> [L] 无障碍项需 44 侧素材与 42 冗余通道就位 —— 未就位时 SIGN-OFF 记 BLOCKED-BY 外部项,**禁借绿**。

本文件**不是**「三条 AC 已达成」的宣告。它是**分层落点登记**:
- 13 侧(本 story 交付面)**能判且已判**的部分 = 数据的**结构前提**;
- 依赖 44 素材 / 42 冗余通道的部分 = **BLOCKED-BY 外部项,NOT-RUN,不记绿**。

⚠️ **判据面与被测面必须错位声明**:13 生产 `CueDispatch`(整数语义 cue + 呼吸层动作),
**不生产**「可见对应物」—— 那是 42 的呈现职责(ADR-013 §9 C3:呈现层只渲染)。
故凡 AC 原文要求「对玩家**可见/可闻**」者,其**达成**在 13 之外。

---

## 一、逐条判定

### AC-13-F1 —— 听障可玩性:三类 cue 均有**非色相可辨的可见对应物**,且默认可见

**GDD 原文**(`patient-ai.md:1083`):
> 开启「视觉冗余」选项后,咳嗽 / 呻吟 / 呼吸三类 cue 均有一个可见对应物(姿态变化 / 面色变化 / 呼吸幅度变化),
> 且该对应物在**默认设置**下亦可见(不需额外开关)。⚠️ 2026-09-25 二轮 44 无障碍回填:对应物须**非色相可辨**(形状 / 明度 / 姿态承载)。

**判定:部分闭(结构前提已闭)· 达成面 BLOCKED-BY 42 —— NOT-RUN。**

| 半边 | 判据 | 落点 | 状态 |
|---|---|---|---|
| 「三类 cue 语义可分」 | 咳嗽/呻吟是不同的**语义 id**(非同一 cue 的强度差 / 非色相梯度) | `test_ac13f1_structuralHalf_cueKindsAreDistinctSemanticIds` | ✅ 已闭(13 职责内) |
| 「呼吸层默认在」 | 活着的病人(任一非终局态)⇒ `BreathAction.Begin`(**默认**,非可选开关) | `test_ac13f1_structuralHalf_breathLayerIsDefaultOn` | ✅ 已闭(13 职责内) |
| **「可见对应物」** | 姿态/面色/呼吸幅度变化的**呈现** | **42 的冗余通道** | ❌ **BLOCKED-BY 42 —— NOT-RUN** |
| **「非色相可辨」** | 形状 / 明度 / 姿态承载(非纯色相) | **42 的呈现设计** | ❌ **BLOCKED-BY 42 —— NOT-RUN** |

**BLOCKED-BY 依据(实测)**:`unity/Assets/Gameplay.UI/` 下 grep `冗余通道|视觉冗余|非色相` = **零命中**;
`assets/data/ai_patient.json` **不存在** ⇒ 44 侧的 cue→材质表亦未就位。
⇒ AC-13-F1 的**达成**在 P0 当前物质条件下**不可判**。13 只交付了它那一半(语义可分 + 呼吸层默认在)。

---

### AC-13-F2 —— 视障可玩性(有限):会移动的病人在 `PERCEPT_R` 内至少呼吸层可闻

**GDD 原文**(`patient-ai.md:1089`):
> 对**会移动**的病人(`Idle` / `Seeking`),脚步声的缺失**不使病人完全不可察觉** —— 至少呼吸层 cue 在 `PERCEPT_R` 内可闻

**判定:部分闭(13 侧结构前提已闭)· 「可闻」达成面 BLOCKED-BY 44 —— NOT-RUN。**

| 半边 | 判据 | 落点 | 状态 |
|---|---|---|---|
| 「呼吸层与移动与否无关」 | `Idle` / `Seeking` 皆 `Begin`(移动不改变呼吸层存在性) | `test_ac13f2_structuralHalf_breathIsIndependentOfMovement` | ✅ 已闭(13 职责内) |
| **「在 `PERCEPT_R` 内可闻」** | 距离衰减 + 空间化 ⇒ 玩家在半径内**真能听到** | **44 的 3D 空间化**(F-13.5 明写「距离衰减归 44」) | ❌ **BLOCKED-BY 44 —— NOT-RUN** |

**⚠️ `PERCEPT_R` 值本身「待调」(GDD `:528` / `:939`)** —— 故「半径内」这一几何判据当前**无量纲**,
即使 44 就位也须先由用户拍定该旋钮(项目铁律:数值用户自己调)。

---

### AC-13-F3 —— 非目标显式化:**文档判据**(非运行期判据)

**GDD 原文**(`patient-ai.md:1090`):
> 本 GDD 的 §UI Requirements 三 明写「P0 不支持视障玩家独立定位不移动的重症病人」,
> 且 `design/accessibility-requirements.md` **须引用本条 —— ✅ 引用义务已兑现 2026-09-20**;⚠️ 承「引用 ≠ 验收」

**判定:✅ 已闭(文档判据 = 该行存在且被引用)。**

**证据(实测)**:
- `design/accessibility-requirements.md` **存在**(25991 字节,2026-09-25)。
- 该件 `:163` 显式点名:**「13 病人 AI 的 `AC-13-F3` 显式非目标」**;`:202` 引用 `AC-13-F1`。
- 夹具 `test_ac13f3_nonTargetClause_referencedByAccessibilityDoc` 断言「文件存在 + **显式点名 `AC-13-F3`** + 与「非目标/不可及」语义**同窗口**」(2026-10-05 订正:原判据 `Contains("13")` 在 26KB 文档里近乎恒真,已收窄)。

**⚠️ 但本条**不是**运行期判据 —— GDD 自己写明「文档判据,非运行期判据」。
**它绿,不代表玩家体验可及性达标;只代表「做不到的事被显式写下来了」。**

---

## 二、NOT-RUN 汇总(禁借绿)

| # | 对象 | 归属 | 解除条件 |
|---|---|---|---|
| NR-S3-1 | AC-13-F1「可见对应物」+ 「非色相可辨」 | **42 冗余通道** | 42 呈现接线 + 非色相设计就位 |
| NR-S3-2 | AC-13-F2「`PERCEPT_R` 内可闻」 | **44 空间化** + 用户拍定 `PERCEPT_R` | 44 素材/混音 + 距离衰减就位 |
| NR-S3-3 | AC-13-D1 `assets/data/ai_patient.json` 烘焙 + 44 签署 | **44 + ADR-014 烘焙管线** | 表内容落盘 + 签署记录文件 |
| NR-S3-4 | TR-patient-023「调试视图条件编译剥离」 | **构建期探针(Tooling 层)** | 玩家构建产物反编译断言(EditMode 单测不可达) |

> **本文件不记 NR-S3-1/2/3/4 为绿。** story-003 的 `[L]` 项**整体状态 = 部分闭 + 四项 BLOCKED-BY 外部**。
>
> ⚠️ **NR-S3-4 的性质与前三条不同**:它**不是**外部系统未就位,而是**判据层级错配** ——
> 「条件编译剥离」的真判据 = 构建产物面,EditMode 单测跑在 Editor 程序集里,**`#if` 已按 Editor 求值**,
> 看不见玩家构建的面。故 story-003 只判其**可判的那半**(13 数据层零调试 UI 引用,IL 引用面),
> 剥离半边**显式 NOT-RUN**,出路 = 另立构建期探针(承 ADR-022 Tooling 层)。

---

## 三、声明

- 13 侧交付面**结构前提**已闭(语义可分 / 呼吸层默认在 / 与移动解耦)—— 由夹具断言,绿。
- **AC 原文的达成**(玩家真能看见/听见)依赖 42 / 44 未就位的外部件 ⇒ **NOT-RUN**。
- 「引用字符串就位」**不构成** AC-13-F3 之外的任何记绿理由(承「引用 ≠ 验收」)。

---

## 四、AC-13-D3 姿态半边(2026-10-05 修复轮补记)

**背景**:AC-13-D3 的原文是「终局 = **呼吸层停止 + 姿态落最静止档**」——**两半**。
修复前的 `CueDispatch` **只有呼吸层字段、无姿态字段** ⇒ 姿态半边既**零断言**也**零承载**,
属「断言了不存在之物」的恒真式(双代理评审 finding E)。

**修复**:`CueDispatch` 新增 `PostureTier`(int,int 档位非 float),`Decide` 在
① 终局锁存 ⇒ 恒 0;② 未终局 ⇒ `Bedridden`(昏迷) = 0,余 = 1。夹具
`test_ac13d3_terminal_endsBreathAndEmitsNoOneShot` 断言 `PostureTier == 0`(终局);
`test_ac13d2_oneShotCarriesNoStateChangeSemantics` 字段清单随之增列 `PostureTier`
(**并注明它是档位、非 from/to 状态对**,故不引入状态播报语义 —— AC-13-D2 不受损)。

**⚠️ 姿态档的呈现映射归 42**(13 只发档位,不渲染)—— 故本条只闭 13 侧**决策值**,
「落最静止档」在玩家屏幕上的**呈现**仍 BLOCKED-BY 42(与 NR-S3-1 同族)。
