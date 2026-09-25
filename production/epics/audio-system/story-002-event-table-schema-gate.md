# Story 002: 音频事件表 schema 与白名单门(BLOCKING)

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(系统分类;实现落表现层 + 构建门)
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

## Context

**GDD**: `design/gdd/audio-system.md`(§Event Table Schema + §Core Rules 规则二)
**Requirement**: TR-audio-002(无提示音铁律机械化:白名单断言 + 双负向夹具)· TR-audio-010(事件表走 ADR-014 烘焙,零 JSON 解析器)

**ADR Governing Implementation**: ADR-014: 数据管线(主:两阶段烘焙 + 硬失败)+ ADR-018: 音频架构(§六 白名单/音乐三闸)
**ADR Decision Summary**: BLOCKING 门必须有载体 —— schema 升格定型(`whitelist_category` 内容侧 + `trigger_source` 时机侧);玩家构建零 JSON 解析器,事件表经烘焙。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(ADR-014 Addressables 6.2+ 抛异常 post-cutoff)
**Engine Notes**: 门为 Editor/构建期(不进 player);烘焙失败 = `BakeValidationException` 聚合 throw 不降级。

**Control Manifest Rules (this layer)**:
- Required: 音频事件表走 ADR-014 两阶段烘焙 · 双负向夹具(缺一不可)· NOT-RUN 守卫(字段缺失=报错退出)
- Forbidden: sting/成就音经改名绕过白名单(靠 `trigger_source` 时机侧 + 配对表堵)· 空表静默通过 · 枚举定义在数据里
- Guardrail: `trigger_source` 是**申报非检测** —— 不得宣称它「堵住」谎报;重开触发条件 3 持续盯防

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [ ] **AC-44-09**(BLOCKING):每行 `whitelist_category` ∈ 五类 且 `trigger_source` ∈ 合法集(禁止集任一 = 失败)。**三重负向夹具**:`Sting` 类别行 ⇒ 失败;类别合法但 `trigger_source: VitalsCrossed` ⇒ 同样失败;**空表/缺必需 cue 行 ⇒ 失败**(2026-09-25 二轮 Q5);`category × trigger` 混搭行(配对表规则 6)⇒ 失败。**NOT-RUN 守卫**:字段缺失 ⇒ 报错退出。
- [ ] **AC-44-D9**:烘焙时每行含 `whitelist_category` + `trigger_source` + `schema_version`(缺一 ⇒ 构建失败)。
- [ ] **schema 校验规则 1–8**(GDD §Event Table Schema):`tier_map` 三键 + 六列集 · per-row `tier_params` 禁滤波列 · `clock_ref` 悬空 ⇒ 失败 · `trigger_phase ∈ [0.8,1.0]×吸气段` · `xfade_ms` 存在性 · `subtitle_text` 键集覆盖语声 cue(AC-44-15 的 [A] 半在此承接)。

---

## Implementation Notes

*Derived from ADR-014 §三 + GDD §Event Table Schema(2026-09-18 升格 + 2026-09-25 二轮字段增补)*:

- 表 = `assets/data/audio_events.json` → ADR-014 阶段 1 词法(Newtonsoft 仅词法)→ 阶段 2 per-schema 绑定 + 本门校验 → `*.cooked`。
- 校验规则全为**编辑期纯函数**(落 `Editor.Tools.Gates` 族,与 `RecipeValidationGates` 同格);规则 7 的「必需 cue 清单」= 构建期常量(语声/呼吸/世界语境族最小集)。
- **枚举定义在代码**(闭枚举 C# 类型),数据只承载字面量 —— 防「扩枚举=改数据过 CI」(2026-09-25 二轮纪律)。
- 双重负向夹具走 `tests/unit/audio_system/fixtures/`(`invalid_*` 系列,承 item-database 夹具先例);第三负向 = 空表夹具 `empty_table.json`。
- `trigger_source` 的诚实申报残余(可谎报)已记 GDD 注;本 story 不解决谎报,只落实体。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 010: 素材 `assets[]` ↔ 文件存在谓词(D6)与 Addressables 组门(D5)
- Story 011: `subtitle_text` 的呈现与注册表消费面([L] 半)
- Story 012: `xfade_ms ≥ MUSIC_XFADE_MIN_MS` 的数值断言(AC-44-18;本 story 只管字段存在)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-09**: 白名单双查 + 三重负向。Given: 合法 `audio_events` 夹具表。
  - When: 跑门。
  - Then: 合法表绿;注入 `whitelist_category: Sting` ⇒ 红;注入 `PlayerAction` × `VitalsCrossed` 混搭 ⇒ 红;注入空表 ⇒ 红;删除必需 cue 行 ⇒ 红;删除 `trigger_source` 字段 ⇒ **报错退出**(与「检查通过」可区分)。
  - Edge cases: 禁止集 token 大小写敏感性;`EncounterMusicLayer` vs `MusicLayer` 配对(规则 6)单查各自 ∈ 集合时放行的混搭必须被配对表抓住。
- **AC-44-D9**: 烘焙字段齐。Given: 缺 `schema_version` 的行。When: 烘焙。Then: 构建失败(非 warn)。
- **schema 规则 5–8**: 单测逐条。Given: 各注入夹具(`tier_params` 含滤波列 / 悬空 `clock_ref` / `trigger_phase=0.95×全周期` / 语声 cue 缺 `subtitle_text`)。When: 门。Then: 各自红;合法行绿。

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `tests/unit/audio_system/event_table_gate_test.cs` — must exist and pass(夹具随文件)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(装配边界先立,门代码本身要过 B1)
- Unlocks: Story 003 · 004 · 005 · 006 · 008 · 010 · 011(tier_map/字段载体是它们的断言前提)
