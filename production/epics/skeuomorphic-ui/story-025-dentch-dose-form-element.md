# Story 025: 戥子档位形态元件(戥杆倾角 / 药包鼓胀 · 离散档 ↔ 离散形态)

> **Epic**: 拟物 UI 框架
> **Status**: Complete
> **Layer**: Foundation
> **Type**: UI / Visual-Feel
> **Estimate**: 1 人日
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-18

## Context

**GDD**: `design/gdd/prescription-and-medication.md`(`:867-868` ① `dose_range.hi` = **物理极限,无 `hi+1`**;
② 非颜色档位读数 = **戥杆倾角 / 药包鼓胀**,离散档 ↔ 离散形态;`:875` 登记入 42 黄铜侧;
规则十三 = 档位是**离散整数焦点序列**,浮点 → 整数量化归 3 / 表现层)
+ `design/gdd/skeuomorphic-ui.md`(`:1219` 戥子档位读数元件 = **黄铜侧**;两条硬约束见下;
`AC-42-F3` 材质分支断言)

**Requirement**: `TR-prescription-014`(戥子输入 → 脉案呈现)· `TR-skeuomorph-*`(黄铜侧元件库)
**ADR Governing Implementation**: ADR-013(拟物 UI 框架 · 42 **只渲染,永不持有游戏状态** · 两栈元件库)
**判据权威**: `AC-11-12`(拟物载体可读半)· `AC-11-13`(相邻档无色条件下可区分)

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(UI Toolkit USS + `VisualElement` 生成子件;
`style.rotate` 属 post-cutoff 面,见 Engine Notes)
**Engine Notes**: 本 story **优先不用 `style.rotate`** —— 戥杆倾角可用**离散的高度 / 边距组合**
表达(每档一组整数几何值),完全避开 post-cutoff API。若实现期确需旋转,须先 spike 并在
`## Test Evidence` 登记 spike 结论;不得在未 spike 的情况下假定 `style.rotate` 在 UI Toolkit
运行期可用。

**Control Manifest Rules (this layer)**:
- Required: 42 **只渲染**;形态全走**主题变量**(C2,零内联色);元件归**黄铜侧**;
  **selector → element 绑定点唯一** = `DentchDoseSelector.LoadInto(BrassScaleElement)`
  —— ⚠️ **2026-10-18 评审 R-8 就地订正**:原行写「绑定点**唯一**」而**未限定轴**,与 `AC-025-4`
  (逐字要求 `CasebookScreen.RefreshDentchDoses()` 每行携带**该行自己的**形态)**自相矛盾**。
  **「行 → 形态」是另一条轴**:档序真源 = 行自身的 `userData`;`DentchDoseSelector` 刻意零可变状态、
  不持有「当前档」,**无法**驱动逐行形态。矛盾调用点 `CasebookScreen.cs:529` 系 `c125742a`
  (**2026-10-12,既有债**),非本件引入;本件新增的是 `:557` 的行内 `SetDetents`。
- Forbidden: **数字角标 / `X/N` 计数**(两栈皆禁,无例外 —— `skeuomorphic-ui.md:1219` 硬约束 a);
  42 自定义剂量语义(归 11);新贴图;**缓存「当前剂量」可变状态**(见边界裁定 3)
- Guardrail: `DentchDoseSelector` 保持零可变状态;`CasebookScreen` 的行构建只改**形态载体**,不改焦点/提交链

### 病灶(2026-10-17 走查实测,非推测)

| 事实 | 证据 |
|---|---|
| `BrassScaleElement` 只承载 `int TickCount`(= **总档数**,不是当前档) | `unity/Assets/Gameplay.UI/Skeuomorphic/Brass/BrassScaleElement.cs:14` |
| `RefreshDentchDoses()` 造 N 行 `Paper` + `new Label("戥子档")` | `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/CasebookScreen.cs`(`RefreshDentchDoses`) |
| 走查像素比对:除焦点框外行内容**逐像素相同** | `production/qa/evidence/prescription-medication/story-005-ac11-12-13-21-signoff.md` §2 ① |
| 唯一形态件是 `.brass-scale`(一条 2px 蚀刻条),且**不携带逐档信息** | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoBrass.uss` |

⇒ 玩家**看得出选中第几行**,但**看不出这一行代表第几档**。这是 `TD-prescription-01` 的本体,
其处置已于 2026-10-17 由主创签署为「**须补**」(临床风险 P0)。

### 边界裁定(2026-10-17 立卡轮定,不再逐条问用户)

1. **只交形态半** —— 逐档形态载体 + USS 主题变量 + `CasebookScreen` 行构建改造归本 story。
   **剂量数值语义**(`DoseValueOf` / 档位 → 剂量的换算)归 **11**,本件**只读不改**。
2. **不新增状态** —— `DentchDoseSelector` 刻意**零可变状态**(档位真相在引擎焦点位置)。
   ⇒ 形态必须由**渲染时传入的序数**派生,**不得**引入 `CurrentDose` 之类缓存字段。
3. **不新增贴图 / 不新增色** —— 形态走几何(高度 / 边距 / 边框宽)与既有 brass 主题变量;
   若确需新变量,须进 `SkeuoThemeVariables.uss` 并挂 `brass` 层注释(承 C2 单一出处)。
4. **色盲模拟不在本件** —— `TD-prescription-05`(ScreenSpaceOverlay 结构上不可行)已裁定归
   **49 无障碍 / ADR-013 修订**,未排期。本件以**无色形态差异**满足 `AC-11-13` 的语义,
   **不声称**已做色盲模拟走查。
5. **走查重跑归收口轮** —— 本件交付后须重走 `S13-7`(`AC-11-12` 形态半 + `AC-11-13`);
   本件**不得**自行判定这两条 AC 转绿。

## Tasks

### 步① 逐档形态载体(戥杆倾角 / 药包鼓胀)

- [x] **AC-025-1**: `BrassScaleElement` 扩展为**逐档形态载体** —— 新增
      `SetDetents(int count, int focusedOrdinal)`(或等价具名 API):
      ① 按 `count` 生成 N 个**形态子件**;② 第 `focusedOrdinal` 档与其余档**几何可分**
      (高度 / 边距 / 边框宽的**整数**差异,**非颜色**);③ `count ≤ 0` fail-loud;
      ④ `focusedOrdinal` 越界 fail-loud(**禁钳位** —— 承 `DoseValueOf` 的零钳位口径)。
      形态子件的几何值全部来自**主题变量**,零内联字面量。
      *判别力*:停用「focused 档与其余档的几何差异」⇒ 相邻档可分断言须转红。

- [x] **AC-025-2**: 形态**不缓存剂量状态** —— `BrassScaleElement` 上**不存在**任何
      `CurrentDose` / `SelectedOrdinal` 之类持久字段;形态完全由 `SetDetents` 的入参决定,
      同一入参重复调用 ⇒ 同一形态(幂等)。
      *判别力*:把 `focusedOrdinal` 存进字段并在下次调用时忽略新入参 ⇒ 幂等断言须转红。

### 步② USS 主题变量(黄铜侧)

- [x] **AC-025-3**: `SkeuoBrass.uss` 追加逐档形态类(建议 `.brass-detent` 族 + `.brass-detent--focused`),
      全部值走 `SkeuoThemeVariables.uss` 的 `--skeuo-brass-*` 变量;新增变量须带 `brass` 层注释。
      断言:① 新类存在;② 类内**零裸色值 / 零裸尺寸**(全 `var(--…)`);③ 块内**零数字文本**
      (即 USS 注释与类名不含数字角标语义)。
      *判别力*:把某条 `var(--…)` 换成裸色值 ⇒ C2 断言转红。

### 步③ 行构建改造(每行携带自己的档位形态)

- [x] **AC-025-4**: `CasebookScreen.RefreshDentchDoses()` 的每一行携带**该行自己的**档位形态
      (第 k 行显示「第 k 档」的形态),而不是全部相同的 `"戥子档"` 文本 + 同形。
      ① 行序 = 档位序(`userData` 仍为 `rowOrdinal`,提交链**不变**);
      ② **零数字文本**(行内不得出现 `1` / `1/3` / `X N` 形态的计数);
      ③ 幂等短路(`_builtDetentCount`)行为保留;④ 焦点 / 提交链零改动。
      *判别力*:把所有行设成同一形态 ⇒ 「相邻行形态不同」断言转红。

- [x] **AC-025-5**: **零数字铁律机械化断言**(两栈) —— 戥子相关代码与 USS 中**不存在**
      数字角标 / `X/N` 计数的产生路径。断言面:① 行内 `Label` 文本不含十进制数字;
      ② `BrassScaleElement` 不暴露任何 `string` 数字读数 API;③ USS 无 `content:` 数字。
      *判别力*:加一个 `new Label($"{ordinal + 1}/{count}")` ⇒ 断言转红。

- [ ] **AC-025-6**(**2026-10-18 评审 R-1 重锚 —— 原判据不可判,已撤勾**)——
      原措辞要求「材质分支断言(`ink` / `brass` / `implement` **三枚举**)判定为 `brass`」,
      而**该分类器全库不存在**(实测:`grep -rn "classify\|BranchOf\|ResolveBranch\|MaterialOf\|MaterialBranch" unity/Assets/` **零命中**;
      `SkeuoElement.cs:4-10` 是**四员** `{Paper, Scroll, Ink, Seal}`,**无 `brass` / 无 `implement`**;
      GDD 侧载体 `world_billboard_test.cs:87` 仍是占位符 `实现后补充:…`)
      ⇒ 原判据的**宾语不存在**,夹具只能以自造的 `ClassListContains("brass")` 顶替 AC 语义层
      ——「**AC 语义层被测试自造断言顶替**」的第 4 次实例(前三次:case-system 001–004 补评轮 · 44 音频 epic 六条假绿)。
      **重锚为两条可判真项**:
      ① ✅ **形态载体归 `brass` 类名级** —— `test_ac025_6_formCarrier_staysInBrassBranch_notInk`
         **只声称已实现的那一半**;
      ② ⛔ **三枚举分类器缺口** → **`TD-prescription-07`**(归 42 epic 独立 story,**本件不修**)。
      *判别力*(仅对 ① 成立):撤掉 `brass` 类 ⇒ ① 转红(F15 已证)。
      ⚠️ **本项维持不勾** —— ② 未落地;分类器到位前 ① 只是**类名代理**,不等于 AC 原文的分支判定。

## Dependencies

- **Blocked-by**: 无(story-021 的 `.empty-row` / story-024 的通道区均不冲突)
- **Enables**: `TD-prescription-01` 结清 · `AC-11-12` 形态半与 `AC-11-13` 转可判 ·
  `S13-7` 走查重跑
- **不阻塞**: 色盲模拟通道(`TD-prescription-05` → 49)· 剂量数值语义(→ 11)

## Acceptance Criteria

- [ ] `AC-025-1` … `AC-025-6` **全勾**(**未达成 —— `AC-025-6` 已于 2026-10-18 评审 R-1 撤勾**;
      见 §Tasks 步③ 与 §评审轮记录):过滤测试绿 + 变异恰红(23/23 绿 · 证伪轮见 §评审轮记录「判据」表)
- [x] 全量 EditMode / PlayMode 留痕,与基线对账**零回归**(等式判据 `本轮 − 前次 = 新增数`)
      —— **实现轮已跑**(EditMode `3732/3687/0/45` vs 基线 `3709/3664/0/45` ⇒ `+23 = +23`;
      PlayMode `197/197/0/0` vs 基线同值 ⇒ `+0 = +0`;口径 = 「**两棵树在新增测上零回归**」,非无限定「零回归」);
      **✅ 评审修复轮已重跑**:EditMode `3732/3687/0/45` · PlayMode `197/197/0/0`,与实现轮**逐位同值**(夹具已改,数字须重取,不沿用旧值);
      **✅ 截图轮(USS 甲案改 1 行)已再重跑**:EditMode `3732/3686/**1**/45`(total 与基线逐位同值;
      唯一红 = `MovementFeedTest` 已知间歇红(`TD-player-01` 下游,失败信息与实现轮首轮同型,**单跑 5/5 绿**
      ⇒ 非本件引入、不得读成已修)· PlayMode `197/197/0/0` 逐位同值。见证据原件 §10.5
- [x] 文档同批回刷:EPIC 行 + Counts(行 025 → `In Progress ◐`;Counts 23 Complete + 2 ◐)·
      `docs/tech-debt-register.md` 的 `TD-prescription-01`(加 2026-10-18 实现轮注;
      ⚠️ **仍未结案** —— 结案前置 = `/code-review` + 截图 + `S13-7` 走查重跑)
- [x] `production/sprint-status.yaml` 登记 —— 2026-10-18 用户裁定登记为 **S14-11(计划外追加)**,
      照 S14-8/9/10 先例落 `status: in-progress` + 立件依据 / 交付 / 判据 / NOT-RUN 注。
      (story-025 原**不在 sprint-14 面内** —— 该文件此前 15 条 S14-* 无 025。)
- [x] **零新贴图 · 零新色 · 零剂量语义改动 · 零焦点链改动**

## Test Evidence

- **载体**:`unity/Assets/Tests/EditMode/SkeuomorphicUI/dentch_dose_form_test.cs`(**新建,23 测**)
- **证据原件**:`production/qa/evidence/impl-s25-dentch-dose-form-2026-10-18.md`
  (交付面 → AC 载体 → 证伪轮 15 例 → 回归对账 → NOT-RUN 登记,逐条可复算)
- **证伪轮**(命中形态:定义域收窄 / 幂等 / 阈值边界 / 共存互斥 / 定序):脚本 `/tmp/fz-s25-r1.py`
  —— **17 例**(实现轮 15 例 + 评审修复轮新增 **F16** 钉 R-5 白名单守卫 · **F17** 钉 R-6 层归属判定)。
  **改 → 跑 → 还原在同一命令内**;`finally` 还原后对 **4 个**受保护文件做 **SHA-256 逐字节校验**。
  ✅ **本轮结果:`RED-OK 17/17`**(2026-10-18;F16 / F17 双双转红 ⇒ 新守卫有判别力)。逐条见证据原件 §三。
  ⚠️ **实现轮 F1 曾假绿,夹具已修**:`test_ac025_1_setDetents_countNotPositive_throws` 原只断异常类型,
  撤掉「档数 ≤ 0」守卫后仍被**档序守卫**抛出同型异常 ⇒ 被掩蔽(实红 0)。
  改断 **`ParamName == "count"`** 后复跑 RED-OK。
  **教训**:两条守卫抛同型异常时,「断类型」的夹具对其中一条**零判别力**。
  ⚠️ **夹具一改,判别力须重证** —— 故修复轮扩至 17 例,不只复跑旧 15 例。
- **视觉证据**:`production/qa/evidence/skeuomorphic-ui/dentch-form-2026-10-18.png`(N 档截图)
  —— ✅ **已跑(2026-10-18 截图轮,两轮迭代后判绿;详见 §截图轮记录)**。
  首图(修复前)实测**判红**:5 行逐像素相同(评审原件 §五 未及核查项 #5 命中);
  甲案修复(`.brass-detent` 底色 → `--skeuo-brass-bg`)后复图**判绿**:
  金档块位置逐行阶梯(第 k 行第 k 档)· 全画面零数字。
  判红首图亦存档:`…/dentch-form-2026-10-18-before-fix.png`。
  ⚠️ 产出方式(结构性事实,留档):batch 出不了图(`ScreenCapture` 无渲染宿主抛异常;
  batch 下 UI Toolkit 纹理永不进图集 —— 见 `ScreenshotTool.cs` 头注「硬边界」段),
  图由**编辑器 Play 聚焦 Game 视图按 F12(1x)** 产出。
  ⚠️ **截图不替代 `S13-7` 真人走查**;本件只交「形态存在且相邻可分」的机械证据

## Out of Scope

- 色盲模拟通道(`TD-prescription-05` → 49 无障碍 / ADR-013 修订,未排期)
- 剂量数值语义 / 档位 → 剂量换算(归 11)
- 焦点导航算法(归 ADR-011 / 引擎 `FocusController`,42 不自实现)
- `S13-7` 走查判定本身(须真人重走)

## 实现轮记录(2026-10-18)

**状态:代码与自动化判据全部落地并自证;两条 NOT-RUN(截图 / 走查重跑)未闭 ⇒ 本 story 留 `In Progress`,待 `/code-review` + `/story-done`。**

### 交付(5 文件 · 零新贴图 · 零新色 · 零剂量语义改动)

| # | 文件 | 动作 |
|---|---|---|
| 1 | `SkeuoThemeVariables.uss` | 黄铜层追加 7 个 `--skeuo-brass-detent-*` 变量 |
| 2 | `SkeuoBrass.uss` | 追加 `.brass-detent-band` / `.brass-detent` / `.brass-detent--focused` |
| 3 | `Brass/BrassScaleElement.cs` | 新增 `SetDetents(count, focusedOrdinal)` + `DetentCount` + `FocusedDetentOrdinal` |
| 4 | `Screens/CasebookScreen.cs` | `RefreshDentchDoses()` 每行改为携带**该行自己的**档位形态 |
| 5 | `Tests/EditMode/SkeuomorphicUI/dentch_dose_form_test.cs` | **新建,23 测** |

**形态 = 几何可分,颜色不变**:落焦块**零颜色声明** ⇒ 「非颜色」(AC-025-1 ②)在 USS 文本层可判真。
**未用 `style.rotate`**(承 §Engine Notes 的优先口径)—— 戥杆倾角由离散几何组合表达,完全避开 post-cutoff API。

### ⚠️ 附带改动(须评审):`.brass-scale` 的 `height` → `min-height`

原 2px 硬高会把新形态子件夹扁(UI Toolkit 默认 `overflow: visible`,形态溢出到相邻行)。
**改前已 grep 复核**:`--skeuo-brass-scale-height` 全库仅主题表声明 + `SkeuoBrass.uss` 引用,**零测试断言该值**。

### 判据

- **过滤**:`total=23 passed=23 failed=0`
- **证伪轮**:`RED-OK 15/15`(改→跑→还原同命令内 + SHA-256 还原校验通过)。首轮 F1 假绿已修(见 §Test Evidence)
- **回归**:EditMode `3732/3687/0/45`(基线 `3709/3664/0/45` ⇒ `+23 = +23`)·
  PlayMode `197/197/0/0`(基线同值 ⇒ `+0 = +0`)

### 本轮发现(2 条)

1. **42 侧成员命名须避让 `rip` 子串**(首轮回归抓到,**本件引入的红**):
   `NoHealthbarFeedbackTest` 的破纸词表 `{tear, rip, break, split, fracture}` 扫的是**成员名**(限引用类型),
   `DetentStripClass` / `_detentStrip` 的 `strip` 含 `rip` ⇒ 误判。**已改 `band`**。
   该事实此前全库无登记 ⇒ 2026-10-18 用户裁定**已入** `.claude/docs/coding-standards.md`
   新立「Naming — 机械门避让表」节(含 `AC-42-F3` 词门同族提醒:该门扫全文**含注释**)。
2. **同型异常掩蔽**(证伪轮 F1)—— 见 §Test Evidence。

### 边界裁定落实

① 只交形态半(`DentchDoseSelector` / `DoseValueOf` **零改动**)· ② 不新增状态(`FocusedDetentOrdinal` 由形态**反查**)·
③ 零新贴图零新色(复用既有 `--skeuo-brass-scale-color`)· ④ 色盲模拟未触碰 · ⑤ **不判 `AC-11-12` / `AC-11-13`**。

### ⚠️ 一处留待评审裁定的解释

`Control Manifest Rules` 写「绑定点**唯一** = `DentchDoseSelector.LoadInto`」,而 `AC-025-4` 逐字要求
`CasebookScreen` 每行携带自己的形态 ⇒ 本件在 `CasebookScreen` 内新增了调用点。
理由:`DentchDoseSelector` 刻意零可变状态、不持有「当前档」,**无法**驱动逐行形态(它只知道总档数)。
读法:该行描述的是**改动前**的既有状态;`LoadInto` 仍是**唯一的 selector→element 绑定点**(本件未新增第二条 selector 绑定)。
**未自行改注该行,留待评审/用户裁定。**

## 评审轮记录(2026-10-18 · `/code-review`)

**裁定**:**CHANGES REQUIRED** —— 唯一 BLOCKING = **R-1**。
**报告原件**:`production/qa/evidence/review-story-025-2026-10-18.md`
(三专家并行:`qa-tester` / `unity-ui-specialist` / `unity-specialist`;逐条含 `Landed` / `Proven` 双列结账)。

### 十项判定

| 级别 | 编号 | 摘要 | 处置 |
|---|---|---|---|
| **BLOCKING** | **R-1** | `AC-025-6` 的三枚举材质分支分类器**全库不存在**;夹具以自造类名断言顶替 AC 语义层 | **已落**:AC-025-6 重锚(撤勾)+ 落债 `TD-prescription-07` |
| WARNING | R-2 | `.brass-detent` 未声明 `flex-shrink` ⇒ 几何可分性随行宽漂移 | ✅ 已修(`flex-shrink: 0`) |
| WARNING | R-3 | `.brass-scale` 因 `min-height` 改造而**两种观感并存**,不变量失效但注释未登记 | ✅ 已修(注释就地登记) |
| WARNING | R-4 | `BrassScaleElement.TickCount` 生产侧零读 ⇒ write-only 死状态 | **落债 `TD-prescription-08`**(不在本轮修复范围) |
| WARNING | R-5 | 白名单含字面量 `"TickCount"`,而背衬字段实名 `<TickCount>k__BackingField` ⇒ **死码** | ✅ 已修(改 `BackingFieldName()` + 非空守卫;F16 钉住) |
| WARNING | R-6 | 层归属只用全文件 `Contains` 代理 ⇒ 变量搬到任何层都通过 | ✅ 已修(`LayerRange` + `AssertDeclaredInLayer`;F17 钉住) |
| WARNING | R-7 | `root.Query<Label>()` 覆盖整棵脉案树,而 AC-025-5① 只要求「行内」 | ✅ 已修(收窄到 `prescription-page` 子树) |
| MINOR | R-8 | `Control Manifest Rules` 的「绑定点唯一 = `LoadInto`」与 `AC-025-4` 自相矛盾(既有文档漂移,`git blame` = `c125742a` 2026-10-12) | ✅ **已就地改注**(用户 2026-10-18 裁定):限定为「**selector → element** 绑定点唯一」+ 声明「行 → 形态」另一条轴 |
| MINOR | R-9 | 构造器用 `hierarchy.Add`,全库惯用 `Add` | ✅ 已修 |
| MINOR | R-10 | 负向门测 `N/M` 分式,而 AC-025-5③ 逐字是 `content:` 数字 | ✅ 已修(主断言对齐 AC;`N/M` 降为附加面) |

### 评审轮的正面观察(修复中不得损毁)

- **落焦块内零颜色声明**是**结构性**达成「非颜色」的正确手法(两 class 同挂,颜色唯一来源 = `.brass-detent`),
  非「颜色改得不一样」。
- **`FocusedDetentOrdinal` 由形态反查** —— 以 O(N≤9) 换「零第二真源」,取舍正确。
- **档子件未设 `focusable`** ⇒ 默认 false ⇒ 不污染焦点环(本屏有焦点序断言,这是必要条件)。

### ⚠️ 三专家自陈「未及核查项」(禁借绿)

三专家**均未运行 Unity**;所有几何 / Yoga / `Q<T>()` 结论为**静态推导**。
根因:`docs/engine-reference/unity/modules/ui.md` 对 `flex-shrink` / `min-height` / `overflow` /
`Clear()` / `ClassListContains` **零覆盖**。真值须由「编辑器 Play 按 F12 截图 + `S13-7` 走查」取得
(见证据原件 §五)。

### 本轮落债(2 条)

| 债 ID | 内容 | 触发条件 |
|---|---|---|
| `TD-prescription-07` | `AC-42-F3` 的**分支半边**分类器未实现(`ink`/`brass`/`implement` 三枚举) | 42 epic 独立 story;届时本件 `test_ac025_6_*` 须**改打真分类器**,禁当已满足 |
| `TD-prescription-08` | `BrassScaleElement.TickCount` write-only(勿与 `DentchDoseSelector.TickCount` 混读) | 42 epic 清理轮 |

### 判据(修复轮复算)

| 判据 | 状态 |
|---|---|
| **过滤测** `--filter dentch_dose_form_test` | ✅ **已跑**:`total=23 passed=23 failed=0`(修复后复跑,不误伤) |
| **证伪轮** `/tmp/fz-s25-r1.py`(17 例 = 原 15 + **F16** 钉 R-5 + **F17** 钉 R-6) | ✅ **已跑**:`RED-OK 17/17` · **SHA-256 还原校验 全部通过**。F16 / F17 双双转红 ⇒ 两条新守卫有判别力。逐条见证据原件 §三 |
| **两棵树全量**(EditMode / PlayMode) | ✅ **已跑**:EditMode `3732/3687/**0**/45` · PlayMode `197/197/0/0` —— 与实现轮(`3732/3687/0/45` / `197/197/0/0`)**逐位同值** ⇒ `+0 = +0` ✅(修复轮**只改既有夹具、未新增 `[Test]`**,故新增数 = 0,等式成立) |

> ⚠️ **本表禁预写**:未跑出的数字**不得**先写成既成事实(承 `feedback_borrowed_green_regression_counts`)。
> **完整复算命令**见证据原件 §七。

### ⚠️ 本 story 仍未 `Complete`(**2026-10-18 截图轮口径;已由 §走查重跑记录解除**)

- **R-1 只解决「不实声称」,不解决分类器本身的缺失**(转 `TD-prescription-07`)—— ⚠️ **该债仍在**,
  但它**不阻塞本件**(重锚后 AC-025-6 只声称已实现的一半);
- ~~截图 NOT-RUN~~ ✅ **已跑并判绿(见 §截图轮记录)** · ~~`S13-7` 走查重跑未执行~~ ✅
  **已跑并改判 ✅✅(见下方 §走查重跑记录)**;
- **R-8 ✅ 已就地改注**(用户裁定,见 §Context `Control Manifest Rules`);
- ~~走查重跑后本件方可进 `/story-done`~~ ⇒ **前置全部达成,本件可进 `/story-done`**。

## 截图轮记录(2026-10-18 · 编辑器 Play · F12 · 1x · 两轮迭代)

> 评审原件 §五 **未及核查项 #5**(「档块底色与所在板同色的可辨性 —— 需人眼读数」)
> 被首图**实测命中**。这不是 R-1…R-10 的任何一条 —— 它是 23 条自动化夹具**结构性测不到**的缺陷类:
> 夹具断 USS 文本 / resolved style,**渲染层零对比度照样全绿**(「全绿 ≠ 可见」的渲染层版本)。

### 首图判红(修复前)→ 根因(机械定位)

- 首图 `…/dentch-form-2026-10-18-before-fix.png`(vision 判读 + 本地逐像素扫描双通道一致):
  **5 行高度、宽度、颜色、描边、块尺寸均一致**,仅第 1 行叠焦点高亮 ⇒ 判红。
- 根因:`.brass-detent` 底色 `--skeuo-brass-scale-color` `#8C5A2B` = 描边 `--skeuo-brass-border`
  `#8C5A2B` = 所在板 `.brass-scale` 底色 —— **三色全同** ⇒ 档块溶进铜条。
  `TD-prescription-01`(「除焦点框外行内容逐像素相同」)的病根在渲染层依旧成立。
- 通过的一半:全画面**零数字 / 零分数**(行内文本已消失 ⇒ 亦证明截图出自本件后的新构建)。

### 修复(用户裁定甲案;倍率维持 1x)

`SkeuoBrass.uss` **1 行**:`.brass-detent` 的 `background-color:
var(--skeuo-brass-scale-color)` → **`var(--skeuo-brass-bg)`**(`#B8863B`,art-bible §4.1,
`:root` 可继承)。**零新增色 · 零新贴图**;落焦块**仍零颜色声明** ⇒ AC-025-1② 不破坏。

### 复图判绿(修复后)

`…/dentch-form-2026-10-18.png` 像素判据(每行恰一个金档块,位置阶梯):

| 行 | 金档块 x | 档位 |
|---|---|---|
| 1 | 18–19 | 第 1 档 |
| 2 | 22–23 | 第 2 档 |
| 3 | 26–27 | 第 3 档 |
| 4 | 30–31 | 第 4 档 |
| 5 | 34–35 | 第 5 档 |

步进 4px = 一个档块节距 ⇒ **AC-025-4「行序 = 档位序」首次在渲染层可见**;
旧图同扫描 = 零金色簇(唯一变量 = 那行 USS)。Vision 独立通道交叉确认:各行凸起位置不同 · 零数字 ·
焦点高亮在第 1 行。⚠️ 观感限制如实登记:常态档在 Game view 缩放下仅约 2px,
**每行实际只有落焦档金芯露出**(低密度读数)—— 够不够归 `S13-7` 走查人眼裁定;
增强(档块尺寸 / 节距)= 44 调参旋钮、数值归用户,**不在本件范围**。

### USS 改动后判据(§十.4 复算命令见证据原件)

| 判据 | 状态 |
|---|---|
| 过滤测 `--filter dentch_dose_form_test` | ✅ **23/23 绿**(`unity/Logs/s25-ussfix-editmode-filtered.xml`) |
| 两棵树全量 | ✅ **已跑**(`unity/Logs/s25-ussfix-full-editmode.xml` / `-playmode.xml`):EditMode **`3732/3686/1/45`**(total 与基线**逐位同值** `+0 = +0`;唯一红 = `MovementFeedTest` 已知间歇红 `TD-player-01` 下游,失败信息与实现轮首轮同型,**单跑 5/5 绿** ⇒ 非本件引入、不得读成已修)· PlayMode **`197/197/0/0`** 逐位同值。详见原件 §10.5 |
| 证伪轮 17 例 | 未重跑(本轮只改 USS 值引用,夹具无改动;原件 §9.2 的 F16/F17 判别力记录仍有效) |
## 走查重跑记录(2026-10-18 · S13-7 重跑批 · 真人目视 · 双签)

> 报告:`production/playtests/playtest-2026-10-18-s13-7-rerun-dentch-form.md`
> 签核:`story-005-ac11-12-13-21-signoff.md` §6(2026-10-18 重跑改签)
> **本件不自判** —— 判定权在走查(§边界裁定 5);本节只登记走查结论对本件的结转影响。

| 原批(2026-10-16)红 | 重跑裁定(真人) | 结转 |
|---|---|---|
| A3 `AC-11-12` 拟物载体可读半 🔴 | **✅ 可读** | 二分签转**两半均 ✅ ⇒ 整体 ✅**;`TD-prescription-03` 结案 |
| B2 `AC-11-13` 仅凭形态分辨 🔴 | **✅ 可辨**(`ELSE` 不触发) | `TD-prescription-01`(根因)/ `-02` **结案**;主创+医学从业双签 |
| (截图轮登记的观感限制)低密度读数 | 用户裁「**够用,不增强**」 | 不触发 44 调参增强债;裁定留痕 |

⚠️ **两条刻意不随本件结转**:`TD-prescription-05`(色盲前置结构性不可执行,**49 通道仍 open** ——
本批按「零颜色读数通道」口径豁免,非前置满足)· **AC-11-21**(腿 C 未重走,仍 ⚫ 不可判,归 S14-10)。


## Completion Notes

**Completed**: 2026-10-18
**Criteria**: AC-025-1…5 ✅(23 测全绿 · 证伪轮 17/17 RED-OK · SHA-256 还原通过);
**AC-025-6 维持不勾**(用户裁定重锚:① 类名级 brass 归属 ✅,② 三枚举分类器 → `TD-prescription-07`,
归 42 epic 独立 story,**不阻塞本件结案**);全量 EditMode `3732/3686/1/45` / PlayMode `197/197/0/0`
—— total 与基线逐位同值(唯一红 = `MovementFeedTest` 已知间歇红,`TD-player-01` 下游,单跑 5/5 绿)。
**判据权威**: `AC-11-12` 载体半 **✅** / `AC-11-13` **✅**(由 2026-10-18 走查重跑真人判定,主创+医学从业双签,
`production/playtests/playtest-2026-10-18-s13-7-rerun-dentch-form.md`)⇒ **`TD-prescription-01/-02/-03` 结案**。
**Deviations**:
- R-2(`flex-shrink`)/ R-7(查询收窄)= `Landed ✅ / Proven ✗` 诚实留白(无宾语可断言,评审原件 §四);
- 截图轮发现的**渲染层零对比度**(三色全同)不在 R-1…R-10 面内 ⇒ 甲案修复(1 行 USS)后判绿 ——
  教训入档:**自动化断样式值,断不了可见性**;
- `TD-prescription-05`(色盲前置)与 **AC-11-21**(腿 C)刻意**不随本件结转**。
**Test Evidence**: `unity/Assets/Tests/EditMode/SkeuomorphicUI/dentch_dose_form_test.cs`(23 测)·
证据图 `production/qa/evidence/skeuomorphic-ui/dentch-form-2026-10-18.png`(+ `-before-fix.png`)·
原件 `impl-s25-dentch-dose-form-2026-10-18.md`(§一–§十一)· 评审原件 `review-story-025-2026-10-18.md`。
**Code Review**: **Complete**(2026-10-18 `/code-review` CHANGES REQUIRED → 修复 8 项 + 落债 2,结账表见原件 §四)。
**状态**: **Complete** —— 待 commit(承「不经用户指令不 commit」)。
