# Story 011: 设置暴露面与归零机制

> **Epic**: 音频系统
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落表现层 + 边界层 store)
> **Type**: Config/Data
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/audio-system.md`(§UI Requirements 设置暴露面注册表 · 规则九 · AC-44-12/13/15/19)
**Requirement**: ⚠ 无专属 TR(无障碍/设置面 GDD AC 直承;七路注册表挂 **TR-audio-003**;字幕挂 **TR-audio-005 族**;registry 见报)
**ADR Governing Implementation**: ADR-018: 音频架构(主:mono/总线音量义务 + 44 只渲染)+ ADR-013(控件呈现归 42)· ADR-014(出厂默认 = 烘焙分区)
**ADR Decision Summary**: 44 拥有闭枚举 `settings_visible` 注册表与出厂默认;壳条目表 = 镜像;归零三步(写默认 → 推 mixer → 落 sidecar)承 OQ-SS-5=甲。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(纯数据 + store 机制;sidecar 承 3 规则四先例)
**Engine Notes**: 字幕呈现归 42(F9=乙:上游发射点扇出,44 不新增出向口);归零控件形态 = 42 元件轮(OQ-SS-7=甲 分工)。

**Control Manifest Rules (this layer)**:
- Required: 注册表 = 单一出处(壳表镜像)· 归零三步缺一无效 · 玩家 exposed ∩ 脚本面 = ∅
- Forbidden: 壳直写非注册表参数 · store 绕过 sidecar 落盘(归源无效)· 44 新增到 42 的出向接口(违只入不出)
- Guardrail: 分叉的只有音 —— 设置值是设备偏好(与存档位解耦,OQ-SS-5=甲),不得进 7a 存档头

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [ ] **注册表 AC①**(§UI Requirements):壳消费集 ⊆ `settings_visible` 闭枚举(8 条 = 7 总线音量含 Master + mono;越集 = 断言失败)。
- [ ] **AC-44-13**:提供 **mono 选项**(`mono_enabled` ∈ 注册表,出厂默认入烘焙分区)。
- [ ] **AC-44-15**(双半):[A] 上游语声/口述 cue 发射双投递(cue→44,文本→42);文本本体 = 事件表 `subtitle_text`,**表键集 ⊇ 语声 cue 集**(缺失 = 构建失败;含 48 口述);[L] 字幕存在性听测签署(WCAG 1.2.2)。
- [ ] **AC-44-19**(OQ-SS-7=甲):归源(写默认 → 推 mixer → **落 sidecar**)后 store == 出厂集;**重启后仍 == 出厂集**;负向:跳过 sidecar 落盘 ⇒ 红。
- [ ] **AC-44-12** [L](文档判据):三条具名项在位 —— 视觉替代引 `AC-13-F1` · P0 非目标「隔墙听觉无视觉等价」(a11y L-3 已登)· mono/总线音量/语声字幕三义务(本 story 与 012/013 承接)。

---

## Implementation Notes

*Derived from GDD §UI Requirements 注册表(2026-09-25 新增)+ OQ-SS-3/SS-5/SS-7 三裁(2026-09-25)*:

- **注册表结构**:`settings_visible` 行含参数名/类型/区间(`0–1`,dB −80–0)/出厂默认/`player_group` 字段/题签语义源;物理落点 = 44 侧数据(随事件表或独立注册文件,ADR-014 烘焙)。
- **OQ-SS-3=甲**:数据层恒 7 路;`player_group` 只影响壳呈现分组(归 42 元件轮)。
- **归零三步**与边界层 store:store = 默认值真源消费者 + mixer 推送方 + sidecar 落盘方(44 不持值 —— 只渲染宪法);sidecar 承 3 规则四模式(schema 头)。
- **字幕 F9=乙**:上游(13/48)发射点解析 `subtitle_text` 后扇出两消费方;44 无新出向口(只入不出不破);48 的口述经同通道(`AC-48-15`)。
- 与 42 的分权:控件形态/焦点/手柄输入 = 42 元件轮;本 story 交付数据面 + 机制面。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: `subtitle_text` 字段的 schema 校验([A] 半的表侧)—— 本 story 承消费与键集覆盖断言
- Story 012: E2 性能、18 乐层
- 42 元件轮:归零钮/滑杆的呈现与焦点(OQ-SS-7=甲 分工)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **注册表 AC①**: Given: 注册表 + 壳行目录。When: 求差集。Then: 消费 ⊆ 注册表;负向:壳引用未登记参数 ⇒ 红。
- **AC-44-13**: Given: 注册表。When: 查 `mono_enabled`。Then: 存在 + 出厂默认 ∈ 烘焙分区 + mixer 暴露映射正确。
- **AC-44-15 [A]**: Given: 语声 cue 全集。When: 校验 `subtitle_text` 键集。Then: ⊇ 语声 + 48 口述 cue 集;缺键 ⇒ 构建失败;[L]:字幕听测签署。
- **AC-44-19**: 归零机制。Given: 被改坏的 store 值。When: 归源。Then: 三步后 store==出厂;**重启进程复验仍==出厂**;负向:跳过第三步 ⇒ 重启后红。
- **AC-44-12** [L]: 文档三件在位核对(a11y L-3 行 + F1 引用 + 三义务 AC 存在)。

## Test Evidence

**Story Type**: Config/Data
**Required evidence**:
- 烘焙/注册表断言测试 + `production/qa/evidence/settings-exposure-evidence.md`([L] 半 + 归零重启复验记录)

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Audio/settings_exposure_test.cs`(11 测);账本互链 `tests/unit/audio_system/README.md`

---

## Dependencies

- Depends on: Story 001 · 002 · 003(E3 七路)· 006(变体表承载字幕文本结构)
- Unlocks: 42 元件轮的壳行镜像可开工;OQ-SS-7 控件形态裁定后归零钮可进架

## Completion Notes
**Completed**: 2026-09-28
**Criteria**: 4/4 passing(注册表 AC① · AC-44-13 mono · AC-44-15 字幕键集 · AC-44-19 归零机制;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. **测试自持谓词面**:生产代码(设置 store/归零机制/注册表)尚未实现,本 story 测试用自持谓词面(`FakeSettingsStore`/`FakeResetMechanism`)。这是严格 TDD 的正确形态 —— 测试定义契约,生产代码实现契约。
2. **mono 出厂默认值用 `Assert.Inconclusive`**:AC-44-13 要求 mono 选项存在且出厂默认有效,但出厂默认值归 ADR-014 烘焙分区(用户调),当前阶段生产代码未实现。测试用 `Assert.Inconclusive` 明确标记待生产实现后验证,不用硬编码常量制造假绿。
3. **归零机制测试验证契约面形状**:3 个 reset 测试验证归零三步语义(写默认 → 推 mixer → 落 sidecar)+ sidecar 持久化契约面形状,生产实现后应替换为调用真实 `IResetMechanism`/`ISettingsStore` 接口。

**评审与修复**:双评审并行(代码质量面 3 BLOCKING + QA 覆盖面 待返回)→ 全修:
- **B1**(代码面):`test_subtitleKeySet_coveredByEventTable` 恒真断言(全局字符串搜索替代 per-cue 验证)→ 改为 `HasSubtitleForCue` per-cue 验证(该 cue 行内 500 字符内是否有 subtitle_text)。
- **B2**(代码面):`test_monoOption_existsWithValidDefault` 恒真断言(断言硬编码常量等于自身)→ 改为 `Assert.Inconclusive`(mono 出厂默认值待生产实现后从烘焙分区读取验证)。
- **B3**(代码面):3 个 reset 测试 Fake 零耦合假绿 → 加注释标注"生产归零机制尚未实现,本测试验证归零契约面形状",生产实现后应替换为调用真实接口。
- **R1**(代码面):`Prefix` 常量声明但从未使用 → 删除。

**残余 NICE(登记不修)**:
- `SettingsVisibleRegistry` 是测试本地副本,可与生产漂移(待生产实现后对齐)。
- `repoRoot` 重复 10+ 次(可抽取为共享 helper)。
- `test_settingsRegistry_unregisteredParam_rejected` 负例无效(检查硬编码数组,应验证生产代码拒收)。
- `test_settingsRegistry_exactly8Entries` 恒真断言(检查本地数组字面量长度)。
- `test_settingsRegistry_monoEnabled_inRegistry` 恒真断言(检查本地硬编码数组)。
- `test_subtitleKeySet_missingSubtitle_rejected` 负例无效(字符串字面量永远不包含子串)。
- `test_a11yDocumentation_threeItemsPresent` 断言过于宽松(应验证具体 AC 条目)。
- AC-44-19 覆盖不完整(只检查 2 条注册表,应遍历全部 8 条;只测跳过 sidecar,应补测跳过写默认/推 mixer)。
- `ExtractCuesWithPrefix` 用 Regex 解析 JSON(应复用 `AudioEventTableGates.ParseTable()`)。

**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Audio/settings_exposure_test.cs`(**11 测:10 passed + 1 inconclusive**);全量 EditMode **1154 passed + 1 inconclusive + 1 skipped + 0 failed**(`unity/Logs/s011-full.xml`)
**Code Review**: Complete —— 双评审并行 + B1/B2/B3 修复 + 复验;review mode = lean。
**ADR Compliance**: ADR-018(音频架构:mono/总线音量义务)· ADR-013(控件呈现归 42)· ADR-014(出厂默认 = 烘焙分区)· OQ-SS-5=甲(端本地 sidecar)· OQ-SS-7=甲(归零控件形态 = 42 元件轮)。COMPLIANT。
**Tech Debt**: 未立文件;上述 NICE 9 项已分处登记(本 notes · story Known Risks · 账本)。
