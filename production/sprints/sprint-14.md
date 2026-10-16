# Sprint 14 — 44 运行期音频链接通

**周期**:不设结束日期(承 sprint-12/13 口径)—— **做完即收,收口时补记结束日**
**规划依据**:`production/retrospectives/retro-sprint-13-2026-10-16.md`(行动项 1–4)
+ `production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md` §4(须实现生产类清单 · 依赖序)
**前置勘察(Phase 1 硬前置)**:已重扫 44 epic 全部 16 条 story 的 `Status:` ——
**007–012 六条现为 `In Review`**(2026-10-16 由 S13-9 撤回)·
001–006/013/014 `Complete`(未普查,行动项 3)· 015/016 `Ready`(素材,界外)。
⚠️ `audio-system/EPIC.md` 的 stories 表 6 行**仍写 `Complete ✅`**(stale)—— 刻意为之,
reconcile 归 S14-7(避免 epic 表与 story 头在实现期反复不同步)。

## Sprint Goal

把 44 音频 epic 的 6 条 `In Review` story 从「**契约 + 门 + 测试就绪,但生产实现不存在**」
推到「**运行期链路真实可播,AC 语义层第一次被生产代码保证**」——
即兑现评审原件 §4 的须实现清单,并经证伪轮复评后转 `Complete`。

> **本 sprint 不做**:
> ① 素材本体接入(归 story-015/016,仍 `Ready`);
> ② 听测类 AC(`AC-11-21` / `AC-44-09` 听测半边)—— **仍不可判,禁借绿**;
> ③ 44 epic 001–006/013/014 的假绿普查(retro-13 行动项 3,另轮)。

## Capacity

- **不用 `estimate_days`**(依 `production/CAPACITY-MODEL.md`)· 复杂度 = S/M/L
- **件/日基线**:sprint-12 = 10 · sprint-13 = 8 ⇒ 本 sprint 按 **8–10 件/日**监督
- **并行纪律**:同时在跑 ≤ 3 件;超限即拆分或顺延讨论
- **本 sprint 预算**:Must 3 件(2×L + 1×M)+ Should 3 件(1×S + 2×M)+ Nice 1 件(1×M)
  —— ⚠️ **含 2 个 L 件,是本 sprint 最大风险**(见 Risks)

## Tasks

### Must Have (Critical Path —— Wave 1 + 2)

| ID | Task | Story | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|-------|-------|-----|--------------|-------------------|
| **S14-1** | **008 生产实现**:`LatticeToWorld` · `cell_jitter` · `rank_key` · **44 声源池**(ADR-028 唯一所有者)· 遮挡驱动器 · 空间化装配入口 | story-008 | Claude | **L** | 无 | ① 20 测由自造 Fake 换成真生产类 ② **`FakeSourcePool` 内部须有真 `AudioSource`**(AC-44-D8 ② 判据逐字是「N 条独立 `AudioSource`」)③ **证伪轮:停用池 ⇒ ≥3 测红** ④ AC-44-16 的「bus ∈ Ambience = 构建失败」真门半边落地(现真门 `RequiredCues` 只查存在性,`grep Ambience` 零命中) |
| **S14-2** | **007 生产实现**:cue 发射派生器 + **`IAudioCueSink` 生产实现** + 锚点门 + EndLoop 看门狗 | story-007 | Claude | **L** | S14-1 | ① 19 测换真 ② **重排差分哈希的被测物须是派生器本体**(非测试自写 `DeriveCueHash`)③ 新文件含 `AudioCueDto`/`IAudioCueSink`/`AudioCueHandle`/`TierSource` token ⇒ **须声明 44 前缀命名空间**(b5 门 `AssemblyGates.cs:995-1009`)④ `Sim/` 内 `new PayloadRef(` 零(ADR-029) |
| **S14-3** | **009 生产实现**:EndLoop 自评看门狗 · `HandoverMux` 交接 FSM · 句柄生命周期管理器 | story-009 | Claude | **M** | S14-1 | ① 12 测换真 ② `LOOP_EVAL_MAX_TICKS` / `HANDOVER_MS` 落 `AudioTuning` ③ 时钟源 = `ITickProvider`(**禁墙钟**)④ **顺手补 AC 编号**(该 story 4 条 AC 现无 `AC-44-xx` 编号,用中文描述名 —— 独立瑕疵,`AC-44-` 全文仅 1 处命中且是引用 Story 004) |

### Should Have (Wave 3 —— 三件相互独立)

| ID | Task | Story | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|-------|-------|-----|--------------|-------------------|
| **S14-4** | **012 生产实现**:`xfade_ms ≥ MUSIC_XFADE_MIN_MS` 下界门 + 常量 | story-012 | Claude | **S** | 无 | ① 兑现**唯一具名空位** `AudioEventTableGates.ValidateXfade`(XML doc 逐字写「**不做** `≥ MUSIC_XFADE_MIN_MS` 的数值断言(AC-44-18,Story 012)」)② `MUSIC_XFADE_MIN_MS` 落 `AudioTuning` ③ **构建期真门:改种子 `xfade_ms` 至 1999 ⇒ 红** ④ DSP 预算探针(60s 窗 P95)归 evidence 文档,非 EditMode 断言 |
| **S14-5** | **011 生产实现**:`settings_visible` 注册表 + `ISettingsStore` + 归零机制 + sidecar | story-011 | Claude | **M** | 无 | ① 11 测换真 ② `assets/` 下须有 `settings_visible`(现全库 `assets/` 零命中)③ `ISettingsStore` 落 `Gameplay.Presentation`(**禁 `Sim`**,GDD :1204「sim 无关」)④ **恢复 AC-44-13 被删空的「出厂默认 ∈ 烘焙分区」断言** ⑤ sidecar 承 3 规则四模式(先例 `Gameplay.Input/BindingsStore.cs`) |
| **S14-6** | **010 生产实现**:事件表 Baker/Binder/CookedWriter + Addressables 音频组 + 听诊预载门 | story-010 | Claude | **M** | 无 | ① `Editor.Tools.Bake/` 须有 audio 文件(现**零**)② 素材存在性谓词入 ADR-014 阶段 2 ③ 组区分门读 `AddressableAssetSettings`(现全库零门读它)④ **清除测试写仓库 `assets/audio/` 的副作用**(`pipeline_gate_test.cs:213-227` 违 `test-standards.md`「Unit tests must not depend on filesystem」) |

### Nice to Have

| ID | Task | Story | Owner | Est | Dependencies | Acceptance Criteria |
|----|------|-------|-------|-----|--------------|-------------------|
| **S14-7** | **6 条复评 + 转 `Complete` + 44 EPIC 6 行 reconcile** | 007–012 | Claude | **M** | S14-1…6 | ① 每条:证伪轮全红 + 复评原件落 `production/qa/evidence/` ② EPIC 表 6 行与 story 头**逐字一致** ③ ⚠️ **判据须是变异测试**(停用生产码 ⇒ 断言转红),非「测试名 + 行号」(retro-13 行动项 2) |

## Carryover from Previous Sprint

| Task | Reason | New Estimate |
|------|--------|--------------|
| S13-7 11 人工走查三签 | **agent 不可代劳** —— 需真人 + 三签(项目外部,不可催) | 不占本 sprint 治理容量;三签稿已备,待真人 |
| —(新)| sprint-13 的 S13-9 产出 = 本 sprint 全部 7 件的**来源** | — |

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| **含 2 个 L 件(S14-1/2),工作量 ≈15 生产类** —— 单 sprint 可能不足 | **高** | 中 | Should 三件可顺延;不硬凑 —— 承 CAPACITY-MODEL「件数与依赖」口径 |
| **008 是根,阻塞 007/009** —— 008 未落地则 Wave 2 无法开工 | 中 | **高** | S14-1 先行;Wave 2 在 S14-1 完成前不开工 |
| **修复轮验收判据若仍是「断言仍绿」⇒ 假绿复现**(retro-13 行动项 2;评审原件 §3 三处实证) | **中** | **高** | 本 sprint 起,「已证」须附**变异测试记录**(停用生产码守卫 ⇒ 断言转红) |
| 素材仍在 015/016 界外 ⇒ 听测类 AC(AC-11-21 / AC-44-09 听测半边)仍不可判 | **确定** | 中 | 本 sprint **不声称**听测可判;禁借绿,听测仍挂 015/016 |

## Dependencies on External Factors

- **S13-7 三签**(主创 / 医学从业 / 音频 lead)—— 项目外部,不可催;本 sprint 观察其进展。
- 素材本体(story-015/016)—— 不在本 sprint 界内。
- CI 真 runner 环境(`UNITY_LICENSE` 未配)—— 本 sprint 的 IL2CPP 三格仍登记 **NOT-RUN**(禁借绿)。

## Definition of Done for this Sprint

- [ ] All Must Have tasks completed(S14-1 + S14-2 + S14-3)
- [ ] 各任务按衍生验收标准通过,**每条附可证伪判据**
      (**变异测试**:停用生产码守卫 ⇒ 该断言转红;retro-13 行动项 2)
- [ ] **QA plan 存在**(`production/qa/qa-plan-sprint-14-*.md`)—— 收口前必跑
- [ ] 6 条 story 由 `In Review` 转 `Complete`(经证伪轮复评)
- [ ] IL2CPP 三格 **NOT-RUN 如实登记**(禁借绿,挂 ADR-012 矩阵)
- [ ] Design documents updated for any deviations
- [ ] Code reviewed(`/code-review`)and merged
- [ ] **`end` 日期在收口时补记**;若 `start` 与实际执行日不符,**矛盾留档不改写**

> **Scope check**:本 sprint 全部 7 件**均在 44 epic 已有 story 的界内**(007–012),
> **不新增 epic 范围** —— 是「把已登记但未实现的东西做出来」,非 scope creep。
