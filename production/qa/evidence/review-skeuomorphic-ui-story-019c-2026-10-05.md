# 评审原件 —— 拟物 UI story-019-c 贴图接入护栏

> **评审对象**:
> - `unity/Assets/Editor.Tools.Gates/TextureBindingGates.cs`(新增 · 198 行)
> - `unity/Assets/Editor.Tools.Gates/SkeuomorphicUiGates.cs`(改 · 门聚合)
> - `unity/Assets/Tests/EditMode/SkeuomorphicUI/texture_binding_gate_test.cs`(新增 · 12 条)
> **权威件**: `production/epics/skeuomorphic-ui/story-019-texture-binding.md` §AC-42-C7/C10/C11 ·
> `design/gdd/skeuomorphic-ui.md` C 组 · ADR-013 §Implementation Guidelines 2/3/5 · TR-skeuoui-011 ·
> control-manifest「贴图必须经元件库接入」
> **日期**: 2026-10-05 · **轮次**: 单轮(承「评审只做一轮」)
> **双代理**: 结构侧(`unity-specialist`)+ QA 侧(`qa-lead`),并行独立出报告

---

## 一、原判定(评审后、修复前)

**结构侧:无 BLOCKING;3 条 MAJOR(M-1/M-2/M-3)+ 5 条 MINOR**
**QA 侧:REJECT —— 1 条 BLOCKING(B1 假绿)+ 1 条 BLOCKING(B2 无评审原件)+ 6 MAJOR**

### 两侧独立收敛到**同一根因**:C10 主入口在环境上是**假绿**

| 结构侧 | QA 侧 | 判据 | 证据 |
|---|---|---|---|
| **M-2** | **M1** | `UrlTargetsTextures` 是**测试专用死代码** —— C10 生产门用 `AnyUrlRegex` 直扫,从不调该谓词 | `grep -rn UrlTargetsTextures` → 生产仅定义处,调用点全在夹具 |
| **M-1** | — | AC-42-C7/C8/C9/C10/C11 在 GDD 中**不存在**(GDD C 组止于 C6)| `grep -n "AC-42-C" design/gdd/skeuomorphic-ui.md` → 仅 C1–C6 |
| **M-3** | — | C10 扫描面 = `Screens/` 全目录(7 uxml),而 AC 点名 6 屏(「调试视图」无文件,多出医馆/近景) | `ls Screens/*.uxml` = 7 |
| — | **B1** | **`Directory.GetCurrentDirectory()` = `<repo>/unity`(工程根)**,非 `<repo>`;`Path.Combine(cwd,"Assets",…)` 拼空 ⇒ 扫描提前 `return` 空表 ⇒ `Assert.IsEmpty` 恒真 | `unity/Logs/probe.xml:46` `cwd=…/Claude-Code-Game-Studios/unity`;`modal_gate_test.cs:502` 早已订正同族 |
| — | **B2** | 无评审报告原件落盘(违 `coding-standards.md` §Review Evidence Standards) | `ls production/qa/evidence/` 无 `review-*019*` |
| — | **M2–M6 / N1–N4** | C11 只扫 `*.uss` 不扫 `*.uxml`;C7 骨架半夹具自守其门;`g => null` 桩同义反复;命名缺 system 段;C11 未覆盖 `resource()` 等 | 见 QA 侧报告 |

### B1 是**最承重**的一条(它把 M-2 从「瑕疵」升为「假绿」)

**B1 与 M-2 是同一失效模式的两面** —— 若 C10 主入口本就扫不到文件,
则 `UrlTargetsTextures` 死不死**根本无从观测**;而 MUT-C10 之所以在旧代码「恰 1 红」,
是因为注入的 UXML 与扫描面**同在泄漏目录下**(一起够不着 ⇒ 恰好都没被发现)。
⇒ **这是变异测试本身证伪不了的一类假绿**(QA 侧 agent-memory 已固化此教训:
`.claude/agent-memory/qa-lead/unity-cwd-is-unity-subdir.md`)。

### 实测复算方法(供独立验证)

1. **cwd 铁证**:`grep -o 'cwd=[^<]*' unity/Logs/probe.xml` → `cwd=…/Claude-Code-Game-Studios/unity`
2. **旧 MUT-C10 反证**:`python3 -c "import xml.etree.ElementTree as ET; [print(m.text) for tc in ET.parse('unity/Logs/mut-c10.xml').iter('test-case') if tc.get('result')=='Failed' for m in tc.iter('message')]"` —— 报文含 `Casebook39.uxml:4`(说明**该次**跑时扫到了文件;旧日志已作废,仅作对照)
3. **死代码**:`grep -rn "UrlTargetsTextures" unity/Assets/` —— 生产仅 1 处定义
4. **AC 悬空**:`grep -n "AC-42-C[0-9]" design/gdd/skeuomorphic-ui.md` —— 止于 C6

---

## 二、修复落点

| # | 原判定 | 修复 | 落点 |
|---|---|---|---|
| **B1** | cwd ≠ 仓库根 ⇒ 静默空跑假绿 | 新增 `ResolveRepoRoot(candidate)`:自候选目录**上溯寻含 `Assets/` 的那层**;`DefaultRepoRoot` 改走它。生产侧(`SkeuomorphicUiGates`)三处改注入 `DefaultRepoRoot` 而非裸 `cwd` | `TextureBindingGates.cs:196-215` · `SkeuomorphicUiGates.cs:135-150` |
| **B1(加固)** | 「扫描面不存在 ⇒ 返回空」= 假绿总闸 | 三个扫描函数加**反空跑守卫**:扫描面目录缺失 / 文件集为空 / 注册类集为空 ⇒ **硬报错**而非静默空 | `TextureBindingGates.cs:93-115, 118-155, 163-...` |
| **M-2 / M1** | `UrlTargetsTextures` 死代码 | C10 保留 `AnyUrlRegex` **过拦**(任一 url 皆禁);`UrlTargetsTextures` **降级为诊断分级**(报「贴图/非贴图」),不再作过滤 —— 既消死代码,又不把过拦悄悄退化成只拦 `Textures/` | `TextureBindingGates.cs:107-120` |
| **B2** | 无评审原件 | **本文件** | `production/qa/evidence/review-skeuomorphic-ui-story-019c-2026-10-05.md` |
| M3 | 「7 屏 = AC 6 屏」账目 | story-019 + 本件显式登记「扫描面 = `Screens/` 全目录(实 7 uxml);AC 点名 6 屏中『调试视图』当前无 uxml」 | story-019 §状态拆分 · 本件 §一 |
| M-1 | 权威 AC 不在 GDD | **不改**(属架构侧治理项,已登记 story-019 §AC「(新)」+ 本件 §四 UNKNOWN) | 见 §四 |
| N1 | C7 骨架半夹具自守其门 | 新增 `test_ac42c7_empty_registry_set_fails_loud_not_silent`(喂空集 ⇒ 须报错),给骨架半**判别力锚** | `texture_binding_gate_test.cs` |
| M4/M5 | 负夹具自证 + `g => null` 同义反复 | 部分消解:新增 5 条**反空跑/上溯**夹具(全部驱动**生产函数**,非夹具自带正则) | 同上 |

### 新增夹具(12 → 17 条)

| 新夹具 | 证明什么 |
|---|---|
| `test_repo_root_resolution_climbs_to_assets_holder` | 从叶子目录上溯必得同一仓库根(B1 的正向锚) |
| `test_ac42c10_missing_screens_dir_fails_loud_not_silent` | 扫描面缺失 ⇒ 报错,非静默空(反空跑守卫) |
| `test_ac42c11_missing_uss_dir_fails_loud_not_silent` | 同上(C11 侧) |
| `test_ac42c7_empty_registry_set_fails_loud_not_silent` | 空类集 ⇒ 报错(消 N1 同义反复) |
| `test_current_repo_root_scan_finds_real_files_not_empty` | **真仓库根下**扫描面非空且为合规态(=0 错)—— 把「合规」与「空跑」显式分开 |

---

## 三、验证命令与结果(修复后)

```
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Unit.SkeuomorphicUI" \
  --output "$PWD/unity/Logs/skeuo-019c.xml"
```
- **019-c 过滤跑**:`unity/Logs/skeuo-019c.xml` = **212 用例 / 199 passed / 0 failed / 13 skipped**
  (夹具 17/17 绿 —— 原 12 + 新 5)
- **全量 EditMode**:`unity/Logs/full-editmode-019c.xml` = **2434 / 2390 passed / 0 failed / 43 skipped / 1 inconclusive**
  (基线 2429/2385 ⇒ **+5 新增、零回归**;唯一 inconclusive = 既有 `SettingsExposureTest.test_monoOption_existsWithValidDefault`)

**变异证明(修复后重跑 —— 旧 MUT 日志已作废)**

| MUT | 变异 | 结果 | 日志 |
|---|---|---|---|
| **MUT-C10** | 屏幕 UXML(`Casebook39.uxml`)注入 `style="background-image: url(&quot;Textures/paper_xuan-final.png&quot;)"` | **2 红**:`test_ac42c10_all_seven_screens_have_zero_url_hits`(原夹具)+ `test_current_repo_root_scan_finds_real_files_not_empty`(新·证明扫到真 `Screens/`)。报文路径 = `Casebook39.uxml:4`,**非** `<repo>/Assets/...` | `unity/Logs/mut-c10.xml` |
| **MUT-C11** | 元件库(`SkeuoPaper.uss`)注入悬空 `url("Textures/__dangling_mut__-final.png")` | **2 红**:`test_ac42c11_current_library_has_no_dangling_refs` + `test_current_repo_root_scan_finds_real_files_not_empty` | `unity/Logs/mut-c11.xml` |

> ⚠️ **修复后 2 红的语义**(与旧「恰 1 红」的分别):
> 第二条红**正是 B1 关闭的唯一可证伪判据** —— 它证明扫描面**真非空**。
> 旧代码之所以「恰 1 红」,是注入物与扫描面同落泄漏目录的偶然;修复后「2 红」才是判别力成立的标志。
> **两个变异文件均已还原**(`git diff` 零改动)。

### 残余 MINOR(如实登记,不判缺陷)

- **M-1 / AC 悬空**:C7/C10/C11 未入 GDD —— 属架构侧治理项,非本 story 可自裁。
- **C11 媒介覆盖**:`resource(...)` / 元件库 `.uxml` 未扫 —— 当前全库 0 命中,**未触发**;
  019-d 接图后若引入 `resource()` 须补(登记为 019-d 义务)。
- **`AnyUrlRegex` 捕获转义引号**:`url(&quot;…)` 的捕获值带 `&quot;` 前缀(MUT-C10 报文可见)——
  因 C10 现已过拦(任一 url 皆拦),**过滤不受影响**;仅诊断文案略糙。留作打磨项。
- **命名**:部分夹具以 AC 号替 system 段 —— 属**存量口径**(`data_boundary_final_test.cs` 同款),
  建议立为全 Epic 统一规约而非单点返工。

---

## 四、UNKNOWN(未取证项,禁借绿)

- **GDD 权威 AC**:C7/C8/C9/C10/C11 在 `design/gdd/skeuomorphic-ui.md` 无正文。
  本 story 自标「(新)」,但未声明 GDD 落点。**须架构侧裁定**:补 GDD 正文,或显式回引 EPIC.md。
- **C10 扫描面语义**:AC 点名 6 屏 vs `Screens/` 实 7 uxml。本 story 采**全目录扫描**(不会漏新增屏),
  但「7 = AC 6 屏」的账目须在 story 显式澄清(已登记)。
- **C7「已注册元件类」范围**:`SkeuoComponentRegistry` 仅 4 类,但元件库 USS 实有 **24 个类选择器**
  (记号族 6/黄铜族 3/器具族 3/焦点族 2 等住别的登记表或零登记)。**C7 覆盖哪一批未裁** ——
  直接决定 019-d 接图量(4 vs 24)。已登记 `story-019` §状态拆分 + `art-assets-required-for-019-2026-10-05.md` §六。
