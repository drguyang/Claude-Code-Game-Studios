# Story 015: 音频资产接入(5 个被引 `.wav` 落地 · 存在性门真实检验 · 缺失⇒构建期硬失败)

> **Epic**: 音频系统
> **Status**: Ready
> **Layer**: Foundation(实现落素材产出 + 门检验)
> **Type**: Integration
> **Estimate**: 待估(依赖素材本体产出)
> **Manifest Version**: 2026-10-03
> **Last Updated**: 2026-10-03

## Context

**GDD**: `design/gdd/audio-system.md`
**Requirement**: TR-audio-010 的**素材本体半边**(第 ① 半「烘焙管线」已由 story 010 兑现)

**ADR Governing Implementation**: ADR-018 §五(素材 = `assets/audio/` Addressables 流式)+ ADR-014: 数据管线与 JSON 解析器
**ADR Decision Summary**: 素材缺失 = 编辑期门拒绝(非运行期降级);玩家构建零 JSON 解析器;素材与 `data-core` 组区分

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(Addressables 6.2+ 抛异常 post-cutoff)
**Engine Notes**: 预载机制 = `DownloadDependenciesAsync` / `LoadAudioData`;失败语义承 ADR-014 挂账(启动不崩,听诊不可入)。本 story **不引入新 API 面** —— 它把已有机制**第一次喂真素材**。

### ⚠️ 本 story 的存在理由(实测,2026-10-03)

story 010 的 **AC-44-D6 已要求**「素材缺失 ⇒ 编辑期烘焙门拒绝,负向:删一个 wav ⇒ 构建失败」,
且该 story 已标 `Complete ✅`(11 测)。但实测:

| 实测项 | 结果 |
|---|---|
| `assets/audio/` 内容 | **空目录**(仅 `.` 与 `..`) |
| 全库 `.wav` 文件数(非 `.git`) | **0** |
| `assets/data/audio_events.json` 引用的 `.wav` | **5 个具体文件名**(逐个均不存在) |

被引 5 个文件名:

```
music_encounter_layer_loop.wav
sfx_breath_adventitious_fine.wav
sfx_breath_base_loop_small.wav
vo_patient_cough_damp_f1.wav
vo_patient_cough_damp_m1.wav
```

⇒ **门已建、判据已写,但从未被真素材检验过** —— story 010 的 11 测用的是**夹具**,
不是真 `assets/audio/` 里的文件。**失效模式与 skeuomorphic-ui 的 16 张 `*-final.png` 完全同型**:
引用存在、资产不存在,而**无任何告警**(静默缺资产让「未接入」不可见)。

⚠️ **本 story 不是可选补件** —— `audio_events.json` 已引用这 5 个文件名,若不落地,
**运行期这 5 条 cue 全部落空**,而 story 010 的门**不会报错**(它只检查「表里引的文件是否存在于
`assets/audio/`」,而现在这条检查从未在真素材上跑过)。

**前置**:素材本体产出(归**内容批**,`OQ-44-2`)。本 story 只做**接入与检验**,不产出音频素材。

---

## Acceptance Criteria

*承 story 010 的 AC-44-D6;补「真素材检验」半边。*

- [ ] **AC-44-D6b(新)**: `assets/data/audio_events.json` 引用的**全部 `.wav`** 在 `assets/audio/` 内**真实存在**;
      存在性门须对**真素材**跑过一次(非仅夹具)—— 证据 = 一次真实烘焙 + 门通过的日志
- [ ] **AC-44-D6c(新)**: 删除任一被引 `.wav` ⇒ **构建期硬失败**(`throw`,非 `Debug.Assert`,
      非运行期 fallback 播空 cue)—— 这是 AC-44-D6 负向半的**真素材复验**,story 010 只验了夹具
- [ ] **AC-44-D15(新)**: 5 个被引 `.wav` 全部落地 `assets/audio/`,且列入 Addressables
      素材组(与 `data-core` 组区分,承 AC-44-D5);Import Load Type 按 `audio.md` 建议 pin
- [ ] **AC-44-D16(新)**: `audio_events.json` ↔ `assets/audio/` 的**双向差集归零** ——
      既无「被引但不存在」,也无「存在但未被引」(孤儿素材)

---

## Implementation Notes

*Derived from ADR-018 §五 + ADR-014 阶段 2:*

1. **接入面**:5 个 `.wav` 按族归位 —— `music_*`(Music Cues)· `sfx_*`(SFX)· `vo_*`(Voice)
2. **门检验**:复用 story 010 已建的 ADR-014 阶段 2 谓词(「素材文件存在」),**不改门本身** ——
   只补一次**真素材跑通**的取证
3. **Addressables**:素材 ∈ `assets/audio` 组;组条目扫描断言(承 AC-44-D5 的既有机制)
4. **孤儿扫描**:反查 `assets/audio/` 内未被 `audio_events.json` 引用的文件 ⇒ 报告(不硬失败,
   因内容批可能先于表更新落素材)
5. ⚠️ **不得**因 story 010 已 Complete 而推定「门已在真素材上验过」—— 本 story 的存在即是反证

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 010: 烘焙管线与门**机制**(本 story 只喂真素材 + 复验负向半)
- Story 002: 事件表**内容**校验(白名单/schema)
- 素材本体**创作**:5 个 `.wav` 的音频内容归内容批(`OQ-44-2`);本 story 消费交付件

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

**AC-44-D6b**:
- Given: `assets/data/audio_events.json` 的真引用集 + `assets/audio/` 实体
- When: 真实烘焙 + 存在性门
- Then: 零缺失,门通过,日志落盘
- Edge cases: 文件存在但为 0 字节;扩展名大小写不一致

**AC-44-D6c**:
- Given: 任一被引 `.wav` 被删除的夹具(基于真素材集,非合成夹具)
- When: 烘焙
- Then: **硬失败**(`throw`),退出码非零
- Edge cases: 删的是孤儿素材(不应失败 —— 未被引);删的是被引素材(应失败)

**AC-44-D15**:
- Given: Addressables 设置
- When: 枚举组条目
- Then: 5 个 `.wav` ∈ `assets/audio` 组,`data-core` 组不含音频
- Edge cases: Import Load Type 未 pin(须报)

**AC-44-D16**:
- Given: `audio_events.json` 引用集 + `assets/audio/` 实体
- When: 双向差集
- Then: 两个方向均为空
- Edge cases: 存在但未引(孤儿,报告不失败);引了但不存在(失败)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- `production/qa/evidence/audio-binding-evidence.md`(含**真实烘焙日志** —— 证明门在真素材上跑过)
- 构建期断言测试(扩展 story 010 的门测试,补真素材用例)

**Status**: [ ] NOT-RUN
**Test File**: TBD(待实现)

> ⚠️ **本 story 的 AC-44-D6b/D6c 是「机制已在、真素材未验」的补证** ——
> story 010 的门测试全绿**不蕴含**「真素材通过」(它跑的是夹具)。
> 按 `.claude/docs/coding-standards.md` §Review Evidence Standards 落 `production/qa/evidence/`。

---

## Dependencies

- Depends on: **Story 010**(烘焙管线与门机制)· **素材本体产出**(内容批 · `OQ-44-2`)
- BLOCKED-BY: 5 个 `.wav` 的音频内容交付
- Unlocks: **M4 Content Complete 的音频面** —— 42 VS Critical 中的 10 项 SFX + 1 项 Ambient 依赖此

---

## Completion Notes

(未开工 —— 本件为 2026-10-03 补立,填「素材本体接入」这个 story 010 **机制已建但真素材未验**的面。
同型先例 = `skeuomorphic-ui/story-019 贴图接入`。)
