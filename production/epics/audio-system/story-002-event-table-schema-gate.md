# Story 002: 音频事件表 schema 与白名单门(BLOCKING)

> **Epic**: 音频系统
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落表现层 + 构建门)
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-26

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

- [x] **AC-44-09**(BLOCKING):每行 `whitelist_category` ∈ 五类 且 `trigger_source` ∈ 合法集(禁止集任一 = 失败)。**三重负向夹具**:`Sting` 类别行 ⇒ 失败;类别合法但 `trigger_source: VitalsCrossed` ⇒ 同样失败;**空表/缺必需 cue 行 ⇒ 失败**(2026-09-25 二轮 Q5);`category × trigger` 混搭行(配对表规则 6)⇒ 失败。**NOT-RUN 守卫**:字段缺失 ⇒ 报错退出。
- [x] **AC-44-D9**:烘焙时每行含 `whitelist_category` + `trigger_source` + `schema_version`(缺一 ⇒ 构建失败)。
- [x] **schema 校验规则 1–9**(GDD §Event Table Schema):`tier_map` 三键 + 六列集 · per-row `tier_params` 禁滤波列 · `clock_ref` 悬空 ⇒ 失败 · `trigger_phase ∈ [0.8,1.0]×吸气段` · `xfade_ms` 存在性 · `subtitle_text` 键集覆盖语声 cue(AC-44-15 的 [A] 半在此承接)。

---

## Implementation Notes

*Derived from ADR-014 §三 + GDD §Event Table Schema(2026-09-18 升格 + 2026-09-25 二轮字段增补)*:

- **本 story 创建 `assets/data/audio_events.json`**(schema + 构建期最小必需 cue 常量种子;文件当前不存在,创建属本 story 交付物);wav 素材与完整 cue 内容归 content 批(Story 010 承接 D6 的素材存在性面)。
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
- **schema 规则 5–8**: 单测逐条。Given: 各注入夹具(`tier_params` 含滤波列 / 悬空 `clock_ref` / **`trigger_phase=0.5`**(相对吸气段分数,出窗 [0.8,1.0] —— 2026-09-26 顾问改判:原「0.95×全周期」在分数编码下不可判) / 语声 cue 缺 `subtitle_text`)。When: 门。Then: 各自红;合法行绿。
- **2026-09-26 评审补测**(review-driven,非创作期自拟):`tier_params` **扁平残形** ⇒ 档位键 ∉ {"0","1","2"} 拒(与嵌套滤波列各一测,堵「扁平绕开 GDD 嵌套形状」的假绿)· 顶层 `schema_version` 非整数 ⇒ 显式红 · 规则 8 第四入口(`assets[]` 含附加音层素材族,前缀 / Policy / ClockRef 三入口全不命中时仍判)· **规则 9** 同名 cue 双行 ⇒ 红 · 附加音层行 `loop ≠ true` ⇒ 红 · 禁止集大小写敏感性(小写 `vitalscrossed` 不落禁止集,但**必然不 ∈ 合法集** ⇒ 仍红)· 扫描器结构截断 ⇒ `PresentKeys` 注入 `__truncated__` 被三层白名单拒收。
- **2026-09-26 复审补测**(第二轮评审 5 REC 的执行体):**行身份守卫**(规则 3 补 —— 行缺 `cue` / `rows` 混入 `null` 行 / `Validate(null)`,三条都是「跑过了却什么都没查」的形态)· 零覆盖分支八条(行级 `PresentKeys` 缺失 · `tier_params` 档位值缺对象体 · `jitter` 缺失 / 非数字 / 负值三态 · `tier_map` 多余档位键 `"3"` / 档位值 `null` · **嵌套值未闭合 ⇒ 哨兵注入**)· 顺序耦合断言改 `AnyError`(次序非契约)· 扁平夹具内层标量不再经 `ReadMembers`(避免被当结构截断注入哨兵,夹具与解析器解耦)。

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- `unity/Assets/Tests/EditMode/Audio/event_table_gate_test.cs`(真身,Unity 编译) — must exist and pass;
- 账本互链 `tests/unit/audio_system/README.md`(AC→测映射 + 夹具清单)· 负向夹具 `tests/unit/audio_system/fixtures/*.json`

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Audio/event_table_gate_test.cs`(类 `EventTableGateTest`);账本互链 `tests/unit/audio_system/README.md`
**Evidence**: 过滤 62/62 · 全量 EditMode **647/647**(`unity/Logs/s002-r2.xml` · `s002-r2-full.xml`,2026-09-26 复审修批后)

---

## Dependencies

- Depends on: Story 001(装配边界先立,门代码本身要过 B1)
- Unlocks: Story 003 · 004 · 005 · 006 · 008 · 010 · 011(tier_map/字段载体是它们的断言前提)

## Completion Notes
**Completed**: 2026-09-26
**Criteria**: 3/3 passing(AC-44-09 · AC-44-D9 · schema 校验规则 1–9;无 deferred)
**Deviations**(均 ADVISORY):
1. Test Evidence 登记路径原写仓库根 `tests/unit/...` —— Unity 只编译 `unity/Assets/` 树,该路径物理上不被编译。项目既有先例(item-database / story-001)= 账本在仓库根、真身在 `unity/Assets/`,由 `tests/unit/audio_system/README.md` 互链;本轮已把 Required 行改为真身路径,消除歧义。
2. 改动了 GDD `design/gdd/audio-system.md` §校验规则 第 3 条(补「`cue` 必填」)。起因 = 第二轮 code-review REC:门与 GDD 对「三字段齐但无 `cue` 的行」均无规则,该行此前可过完 13 段校验进烘焙。属**补齐规格**而非实现漂移;未新增条号(并入规则 3),免掉全库 12 处 `规则 1–9` 计数的二次刷。
3. 两条非阻塞残留(复审 NEW ISSUES,登记不修):① 截断哨兵只覆盖 `rows` / 顶层 / `tier_map` 三层白名单,`adventitious_policy` 与 `tier_params` 两层字典的结构异常仍静默(REC-1 覆盖面残留,非回归);② 规则 1 / 规则 2 / AC-44-D9 用 `row == null || 缺字段` 合并条件,对 null 行会报出「缺 whitelist_category」这类错措辞 —— 门注释已如实描述,测试亦不锁错误总数,行为未改。
**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Audio/event_table_gate_test.cs`(62 测)· 过滤 62/62 · 全量 EditMode 647/647(`unity/Logs/s002-r2.xml` / `s002-r2-full.xml`)
**Code Review**: Complete —— 两轮 `/code-review`(首轮 unity-specialist 判 CHANGES REQUIRED:1 BLOCKING + 4 REC;第二轮 0 BLOCKING · 5 REC 全修)+ 一轮聚焦复审 **Verdict: APPROVED**。review mode = lean,QL-TEST-COVERAGE / LP-CODE-REVIEW 门按 lean 规则跳过。
**ADR Compliance**: ADR-014 §三(三层白名单硬失败 · schema_version 版本化 · 负向夹具路径)· ADR-018 §六(五类 / 合法集 / 禁止集三常量逐字一致)· ADR-025(`Editor.Tools.Gates`,Editor-only)—— 全 COMPLIANT,无 VIOLATION / DRIFT。
**Tech Debt**: 未立文件;NICE 榜单(category 侧大小写 · `Fmt` 字面量断言 · 测试命名缺 expected 段 · 魔法串 `"ContinuousPhysiology"` · 相位同因双报去重 · `renamed.PresentKeys` 交叉校验)与上条 3 的两项残留一并留待排期。
