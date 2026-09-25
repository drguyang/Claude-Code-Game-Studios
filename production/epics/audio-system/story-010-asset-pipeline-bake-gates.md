# Story 010: 素材管线与事件表烘焙门

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(系统分类;实现落编辑期工具链)
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/gdd/audio-system.md`(硬约束 7 · Edge Cases 素材与管线段 · §Dependencies ADR-014 行)
**Requirement**: TR-audio-010(音频事件表走 ADR-014 烘焙:玩家构建零 JSON 解析器)

**ADR Governing Implementation**: ADR-014: 数据管线与 JSON 解析器(主:两阶段 · 硬失败 · Addressables §五)+ ADR-018 §五(素材 = `assets/audio/` Addressables 流式,与 `data-core` 组区分)
**ADR Decision Summary**: 编辑期烘焙门拒绝缺失/越界;玩家构建零 JSON、零 `FixParse`;素材流式预载策略有归属。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(Addressables 6.2+ 抛异常 post-cutoff;E-13 先例 = 启动期硬失败不降级)
**Engine Notes**: 「听诊窗预载」原指针悬空(ADR-014 §五只管 `data-core`)—— 二轮 REC 落点:预载机制 = `DownloadDependenciesAsync`/`LoadAudioData`(`addressables.md`/`audio.md` 已核),门与 AC 在本 story 落;运行期失败语义承 ADR-014 既有挂账。

**Control Manifest Rules (this layer)**:
- Required: 素材缺失 = 编辑期门拒绝(非运行期降级)· 事件表走两阶段烘焙 · `assets/audio/` 与 `data-core` 组区分登记
- Forbidden: 玩家构建带 JSON 解析器 · 运行期静默播「空 cue」· 开发构建直读原文件进玩家路径
- Guardrail: 高频 cue(呼吸/脚步)进对应生态区/场景时预载;听诊窗预载(运行期 AC)

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [ ] **AC-44-D4**:音频事件表走 ADR-014 烘焙管线;玩家构建**零 JSON 解析器**(构建产物扫描:无 Newtonsoft/解析器符号进 player 程序集)。
- [ ] **AC-44-D5**:素材本体 = `assets/audio/`(Addressables 流式),与 `data-core` 组区分(组条目扫描断言;`Editor.Tools.Gates` 读 `AddressableAssetSettings`)。
- [ ] **AC-44-D6**:素材缺失 = **编辑期烘焙门拒绝**(事件表 `assets[]` ↔ 文件存在谓词,入 ADR-014 阶段 2 谓词集;负向:删一个 wav ⇒ 构建失败,不是运行期降级)。
- [ ] **听诊窗预载**(Edge Cases 素材段):进入 `StethoscopeFocus` 前听诊族 cue `loadState == Loaded`(PlayMode 断言,未就绪不进听诊);一次性 cue 迟发 ≤ 登记常量(「极小迟发」量化)。

---

## Implementation Notes

*Derived from ADR-014 §三/§五 + GDD(2026-09-25 二轮指针修)*:

- **谓词集追加**:ADR-014 阶段 2 现有谓词(白名单/守恒/区间/长度/schema_version)增「素材文件存在」—— `Editor.Tools.Bake` 显式加列。
- 预载策略**不再指 ADR-014 §五**(该节只管 `data-core`,二轮已确认悬空)—— 归本 story + `audio.md` 机制(`LoadAudioData` + `loadState`);E-13 失败抛异常形态须 try/catch 显式处理(启动不崩,听诊不可入)。
- 开发构建允许直读原文件(Edge 玩家构建零 JSON 红线不破)。
- 素材 Import Load Type(呼吸/语声族 = DecompressOnLoad 族)按 `audio.md` 建议 pin,防流式放大首次迟发 —— 具体值随 content 批。
- 高频 cue 预载触发点 = 进入生态区/场景(与 ADR-014 的 `data-core` 首次 `Step` 前预载并列,组不同)。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: 事件表**内容**校验(白名单/schema)—— 本 story 是**管线与素材存在性**面
- 内容批:wav 素材本体产出与 Import Settings 定稿(`OQ-44-2`)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-D4**: 零解析器。Given: 玩家构建产物。When: 扫 player 程序集符号。Then: 无 Newtonsoft JSON 解析路径;事件表经 cooked 读取。
- **AC-44-D5**: 组区分。Given: Addressables 设置。When: 枚举组条目。Then: 素材 ∈ `assets/audio` 组,`data-core` 组仅烘焙数据;混组 ⇒ 红。
- **AC-44-D6**: 存在性门。Given: 表中引用缺失 wav 的夹具。When: 烘焙。Then: 构建失败(聚合 throw);合法表绿。
- **听诊窗预载**: PlayMode。Given: 冷启动(素材未载)。When: 触发进入听诊。Then: 进入前听诊族 `loadState == Loaded` 才放行,否则不进(或预载完成后进);负向:跳过预载直入 ⇒ 断言红。

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `tests/integration/audio_system/pipeline_gate_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002(事件表内容先合法)· 011 无
- Unlocks: Story 004/006 的 [L] 听测(素材就位前提)· content 批交付接口
