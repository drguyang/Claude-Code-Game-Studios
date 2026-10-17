# Story 017: 音频门加固(组判据收紧 · 跨平台大小写 · ValidateEntries 拆分)

> **Epic**: 音频系统
> **Status**: Complete
> **Layer**: Foundation(加固既有门/工具,**不新增功能**)
> **Type**: Integration
> **Estimate**: 待估(小 —— 三处均为既有方法内的局部改动)
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-17

## Context

**GDD**: `design/gdd/audio-system.md`
**Requirement**: TR-audio-010 的**门侧加固半边**(素材本体半边归 story-015 / 016)

**ADR Governing Implementation**: ADR-018 §五 · ADR-014 §三/§五 · ADR-022 §一
**ADR Decision Summary**: 素材与 `data-core` 组区分;阶段 2 谓词聚合 `throw`;Tooling 不进构建

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(纯 C# 谓词与 BCL 文件枚举,**不触 post-cutoff API**)
**Engine Notes**: 本 story **不引入新 API 面** —— 只在既有 `AudioAssetGroupGates` / `AudioAssetExistence` 内做局部加固。

### ⚠️ 本 story 的存在理由(2026-10-17 `/code-review` 产出)

story-015 的收口轮代码评审(`production/qa/evidence/review-s15-code-2026-10-17.md`)
判 **APPROVED WITH SUGGESTIONS**,产出 7 条 WARNING。其中两条落在 **story-010 的既有门代码**上
(非 story-015 引入),用户 2026-10-17 裁定**新立加固件**承接:

| 评审条目 | 判定 | 出处 |
|---|---|---|
| **W-2** | `Directory.GetFiles(dir, "*.wav")` **大小写敏感** —— Linux(CI 平台,ADR-012)下 `.WAV` 漏扫 ⇒ 盘上有却判「不存在」,**跨平台行为分叉** | `AudioAssetGroupGates.cs:197` |
| **W-3** | `ValidateEntries` **45 行 / 圈复杂度 15** —— 双超标(标准 = ≤40 行 / <10) | `AudioAssetGroupGates.cs:76-120` |

> **同源但不在本件面内**(用户 2026-10-17 裁定归 **`TD-audio-01`**):W-4(组判据弱于 AC 语义)·
> W-5(`EnsureAudioGroup` 不完备)· W-7(`CreateGroup` 全新 clone 路径未验)。
> **W-1 / W-6**(`music_*` 的 `loadType` 判据张力)**不改** —— GDD §OQ-44-2 附已覆盖。
> ⚠️ **W-4 与 W-3 同处 `ValidateEntries`**:本件只动**结构与行数**(W-3),
> **不动组归属判据的语义**(W-4)—— 后者归 `TD-audio-01`,避免同一方法被两条工作流同时改。

---

## Acceptance Criteria

- [x] **AC-44-D5b(新)**: `AudioAssetGroupGates.AudioAssetsOnDisk()` 的扩展名过滤**跨平台一致** ——
      Linux 与 Windows 下对同一目录返回**同一集合**;`.WAV` / `.Wav` / `.wav` 三形态**均被识别**
      (判别力:停用修复 ⇒ 大小写混写夹具转红)
- [x] **AC-44-D5c(新)**: `AudioAssetGroupGates.ValidateEntries` 满足项目编码标准 ——
      **≤ 40 行**(不含数据声明)· **圈复杂度 < 10**;拆分后**全部既有 D5 夹具逐条仍绿**
      (判别力:拆分若改变行为,`test_d5_pure_*` 四条须有一条转红)
- [x] **AC-44-D5d(新)**: 加固**不改变门的判定结果** —— 对同一输入,加固前后
      `ValidateEntries` 的**返回错误集逐字相同**(纯重构,非语义变更)

> **证据原件**:`production/qa/evidence/impl-s17-gate-hardening-<日期>.md`(实现轮落盘)

---

## Implementation Notes

*Derived from `review-s15-code-2026-10-17.md` §三 3.1 W-2 / W-3:*

1. **W-2 修法**:把 `Directory.GetFiles(dir, "*.wav")` 的 pattern 过滤改为
   **先取全量再按扩展名判**(`Path.GetExtension(p)` + `StringComparison.OrdinalIgnoreCase`),
   或显式枚举三形态。⚠️ **不得**只改成 `"*.WAV"` —— 那只是把分叉挪到另一边。
2. **W-3 修法**:`ValidateEntries` 拆为两个私有方法(建议按 (a) 期望集归属 / (b) `data-core` 纯洁性
   两半拆 —— 正好对应方法内已有的两段注释),主方法退化为编排。⚠️ **拆分不得改变判定语义**
   (AC-44-D5d 钉死)。
3. **夹具**:本件须**新增**大小写混写夹具(`.WAV` 副本 ⇒ 应被识别)+ 重构等价性夹具
   (同一输入 ⇒ 错误集逐字相同)。既有 **17** 条 D5/D6 测**不得删改**。

   > ⚠️ **陈旧计数就地订正(2026-10-17 实现轮 · 2026-10-17 二次订正)**:本条原写「既有 **14** 条 D5/D6 测」。
   > **实测**(HEAD `d21e43a`,`grep -oE 'public void test_d[56][a-zA-Z0-9_]*' … | wc -l`)= **17**(D5 = 10 · D6 = 7)。
   > 「14」疑为只数了 `test_d5_pure_*`(6)+ `test_d5_realSettings_*`(1)+ D6(7) 的口径,
   > **漏计 `test_d5_audioAssetsOnDisk_nonEmpty` / `test_d5_audioGroupName_isNotDataCore` /
   > `test_d5_menu_settingsMissing_throwsHard`**。
   > ⚠️ **二次订正说明**:上一版此处写「落盘前 **18**(D5 = 11 · D6 = 7)」—— **该数为误**,
   > 疑把本轮新增的 `test_d5_menu_groupMissing_throwsHard` 计入了「既有」半边。HEAD 实测 D5 = 10。
   > 订正后 D5/D6 面 = **23**(17 既有 + 6 本轮新增:`test_d5b_*` 3 + `test_d5c_*` 1 +
   > `test_d5d_*` 1 + `test_d5_menu_groupMissing_throwsHard` 1)。
   > ⚠️ **口径(三次核对定案,2026-10-17)**:**「既有」有两个合法含义,别混** ——
   > ① **HEAD 源码**(`d21e43a`)= **17**;② **基线 XML `s19d-full-final.xml` 采集时点** = **18**
   > (多出的 1 条 = `test_d5_menu_groupMissing_throwsHard`,TD-audio-01 补口轮落的,早已被 `s19d` 采集)。
   > **参考实现时以 17 为准**(数源码);**对账回归时以 18 为准**(数同一采集方式的两份 XML)。
   > 两者**不可直接相减**。逐 fullname 差集:`s22` vs `s19d` = **新增 5 条、删除 0 条**
   > (`d5b` ×3 / `d5c` ×1 / `d5d` ×1)⇒ 回归对账 `3635 − 3630 = 5` **成立**。
4. **证伪轮(BLOCKING)**:两条新 AC 各须一条反证 —— 停用 W-2 修复 ⇒ 大小写夹具红;
   停用 W-3 拆分(还原为长方法)⇒ **不红**(行数/CC 无夹具钉),故 AC-44-D5c 的
   **判别力载体须显式设计**(建议:断言私有方法存在 + 主方法行数,或引入行数守卫测试)。
   ⚠️ 若 AC-44-D5c 无法造出有判别力的夹具,**须如实登记为「标准类无界检查项」**,
   不得标「已证」(承 `review-workflow.md` Phase 8b 双列纪律)。

   > ✅ **实现轮结论(2026-10-17)**:**判别力载体已造成,AC-44-D5c 转「有界」**。
   > 载体 = `test_d5c_validateEntries_withinLineAndComplexityBudget` —— **自校准计数**
   > (从源码里抽出方法自身文本再量,不写死数字)+ ≤40 行 + CC<10 + **两个私有半各自 ≤40 行**。
   > **反证两条**:F7(填充注释行推过 40 行)⇒ 红;F8(追加语义恒真、体为空的 `if` + 8×`&&`
   > ⇒ CC 11)⇒ 红。**两条都只红这一条测试**(`exactRed: true`)。
   > ⚠️ **口径须写明**:本件行数 = **注释剥离后「签名行首 → 闭括号行尾」的物理行数**
   > (HEAD 实测 **48**),story-017 卡面记的「45 行」是**方法体**口径 —— 两个数都 >40,
   > 故「夹具能否抓住加固前状态」不受影响,但**不得混引**。CC 两处一致(15)。
   > ⚠️ **仍未证的一件**(不掩盖):F7 证明的是「行数守卫对方法长度敏感」,**不是**
   > 「撤销拆分」的等价物 —— 要造后者的等价物得把两个判据半内联回去(30+ 行),
   > 本件**未做**。当前形态下「有人把拆分内联回去」会被 ≤40 行断言接住,故缺口不实质。
5. **`ValidateEntries` 住 `Editor.Tools.Gates`** ⇒ 改它须复核 `Editor.Tools.Gates.asmdef`
   的 `includePlatforms: ["Editor"]` 不变(ADR-022 §一)。

---

## Out of Scope

*Handled by neighbouring stories / 债册 — do not implement here:*

- **W-4 / W-5 / W-7**:组判据语义收紧 · `EnsureAudioGroup` 自举 · `CreateGroup` 全新 clone 路径
  —— 归 **`TD-audio-01`**(用户 2026-10-17 裁定)
- **W-1 / W-6**:`music_*` 的 `loadType` 判据张力 —— **不改**(GDD §OQ-44-2 附已覆盖)
- **story-015 / 016 的素材面**:本件只动门代码,**不碰任何 `.wav` / `.meta` / 事件表**
- **组定义入库(笔 1 收窄 `.gitignore`)**:已于 2026-10-17 随本裁定落地,不属本 story

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-44-D5b**:
- Given: `unity/Assets/Audio/` 内存在 `.wav` 与 `.WAV` 两种大小写副本
- When: `AudioAssetsOnDisk()`
- Then: 两者**均被返回**
- Edge cases: 同名不同大小写共存;无扩展名文件;`.wav` 大小写混写(`.Wav`)

**AC-44-D5c**:
- Given: `AudioAssetGroupGates.ValidateEntries` 源码
- When: 行数 / 圈复杂度实测
- Then: ≤ 40 行 ∧ CC < 10
- Edge cases: 拆分后主方法只剩编排;私有方法各自 ≤ 40 行

**AC-44-D5d**:
- Given: 一组固定输入(混合:纯音频在 `data-core` / 纯音频未登记 / 干净集 / null 集)
- When: 加固前后各跑一次 `ValidateEntries`
- Then: 返回的错误集**逐字相同**(顺序 + 内容)
- Edge cases: null 输入;空集;同时命中 (a) 与 (b) 两半

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `production/qa/evidence/impl-s17-gate-hardening-<日期>.md`(含**加固前后错误集对照**)
- 测试扩展:`unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`(既有 **52** 测 **不得删改**,
  本件**追加**)
  · 账本并列:`tests/integration/audio_system/README.md`

> ⚠️ **陈旧计数就地订正(2026-10-17 实现轮 · 2026-10-17 二次订正)** —— 本节原写「既有 **52** 测」,
> 系立件时照抄 story-010 段的旧值。**实测**(HEAD `d21e43a`,命令同上)= **52** ——
> **原值「52」对(数源码的口径)**,但上一版把它改成「69」**也不是凭空编的** ——
> 69 是**基线 XML `s19d-full-final.xml` 采集到的用例数**(另一种口径,见下)。
> 上一版还写了一条演进序列「52 → 61 → 68 → 69」——
> **它把两种口径缝成了一条曲线**(52/52 是源码数,61/68/69 是三次 XML 采集数),
> 这才是那处真正的错。**源码口径下,提交历史只有三个点**:
> `22581ca` = 11 · `8d852fe`(S14-6)= **52** · `d21e43a`(TD-audio 加固,= HEAD)= **52**。
> **订正后(源码口径):既有 52(HEAD 实测)· 本件落盘后 74。**
> ⚠️ **对账口径不同**:基线 XML `s19d-full-final.xml` 记的是 **69**(采集时点),
> 逐 fullname 差集 `s22` vs `s19d` = 新增 5 条 ⇒ `74 − 69 = 5` 成立。
> 「源码 52 / 74」与「XML 69 / 74」**基准不同**,数字上的 74 重合只是巧合。
> 同型订正见 `## Implementation Notes` 第 3 条。

**Status**: ✅ **RUN —— 74/74 全绿(过滤 `s23f`)· 3635/3590/0/45(全量 `s23g`,零回归)**
> ⚠️ **2026-10-17 复跑补记**:内容批接入后本件三测曾**红**(种子行数 5→15 / 退役文件名空转 / 听诊族 2→4),
> 已修并重跑全绿;逐条见证据原件 §四 后附「复跑」表。**原判定的全绿只对当时那棵树成立,不追认。**
**Test File**: `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`(追加 5 条,现 **74** 测)
**证据原件**:`production/qa/evidence/impl-s17-gate-hardening-2026-10-17.md`

> ⚠️ **AC-44-D5c 是「标准类无界检查项」的高风险面** —— 行数 / CC 无天然夹具。
> 实现轮须显式设计判别力载体,否则按 Phase 8b 纪律标 `Landed=✅ / Proven=✗`。

---

## Dependencies

- Depends on: **Story 010**(烘焙管线与门机制 —— 本件改的就是它产出的门)
- Depends on: **story-015**(评审产出的 W-2/W-3 即本件输入)
- BLOCKED-BY: **无** —— 本件**不依赖素材产出批次**,可在素材接入前独立完成
- Unlocks: 无(story-010 的代码质量面收敛)

---

## Completion Notes

**Completed**: 2026-10-17
**Criteria**: **3/3 passing**(AC-44-D5b / D5c / D5d,均 BLOCKING 级)
**Deviations**: 无 BLOCKING 偏离。三处**如实登记**(详见证据原件 §五):
① 点文件策略不对称(`FilterAudioNames` 剔 `.` 开头,`AudioImportSettingsGate.Validate` 不剔)——
   **本件发现但刻意未改**,两门语义选择,不在 AC-44-D5b 面内;
② story-017 卡面陈旧计数(`52` / `14`)—— **已就地订正**(见上);
③ AC-44-D5c 的「撤销拆分」等价反证**未做**(F7 证明的是行数守卫敏感性)—— 缺口不实质。
**Test Evidence**: Integration —— 真身 `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`
(账本 `tests/integration/audio_system/pipeline_gate_test.cs`)· **74/74 过滤绿** ·
**全量 `3635/3590/0/45`**(基线 `3630/3585/0/45`,**Δtotal = +5 = 新增测数**,零回归)
**Falsifiability Gate**: ✅ **PASS** —— 10 变异全部 `exactRed: true`,exit 0,
SHA-256 逐字节还原校验通过,受保护路径指纹未变(证据原件 §三)
**Code Review**: 本件由 story-015 收口轮 `/code-review` 的 W-2 / W-3 派生;
实现轮未另起评审轮(用户 2026-10-17 裁定「现在整件开工」)
**证据原件**: `production/qa/evidence/impl-s17-gate-hardening-2026-10-17.md`

### 同批不同账(用户裁定 #11 · 归 `TD-audio-01f`,**不属本 story 的 AC**)

`U1SpikeSetup.Teardown` 加 Play Mode 复原委派(`Editor.Tools.Spike.asmdef` +`Editor.Tools.Bake`
单向引用 + `DataBakeMenu` 文档注 + ADR-025 §① 注记)。
⚠️ **`Landed = ✅ / Proven = ✗`** —— `Teardown` 是编辑器生命周期回调,EditMode 下不触发,
本件**未**为它造源码扫描或 PlayMode 手跑判据 ⇒ **不得标已证**(承 Phase 8b 双列纪律)。
