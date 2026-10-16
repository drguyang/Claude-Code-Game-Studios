# Story 015: 音频资产接入(5 个被引 `.wav` 落地 · 存在性门真实检验 · 缺失⇒构建期硬失败)

> **Epic**: 音频系统
> **Status**: Complete
> **Layer**: Foundation(实现落素材产出 + 门检验)
> **Type**: Integration
> **Estimate**: 待估(依赖素材本体产出)
> **Manifest Version**: 2026-10-03
> **Last Updated**: 2026-10-17

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
> **⚠️ 内容需求登记处**(2026-10-16 立):素材**要交付什么**(逐族声学判据 + Import Settings 档位 +
> 变体库量 + 素材族完整性 + 命名真源)**不在本卡 AC 面内** —— 见 `docs/tech-debt-register.md`
> §「44 音频素材内容需求」。本卡只验「交付物到位后接入是否成立」。

---

## Acceptance Criteria

*承 story 010 的 AC-44-D6;补「真素材检验」半边。*

- [x] **AC-44-D6b(新)**: `assets/data/audio_events.json` 引用的**全部 `.wav`** 在 `assets/audio/` 内**真实存在**;
      存在性门须对**真素材**跑过一次(非仅夹具)—— 证据 = 一次真实烘焙 + 门通过的日志
      ✅ **2026-10-17 落** —— 5 个真实内容 wav 落 `unity/Assets/Audio/`(替换 S14-6 占位静音);
      `test_d6_realSeed_bakesWithNoMissingAsset` 打真种子 + 真根,**绿**;全量回归 `395/395/0`
- [x] **AC-44-D6c(新)**: 删除任一被引 `.wav` ⇒ **构建期硬失败**(`throw`,非 `Debug.Assert`,
      非运行期 fallback 播空 cue)—— 这是 AC-44-D6 负向半的**真素材复验**,story 010 只验了夹具
      ✅ **2026-10-17 落** —— **证伪轮**:真删 `vo_patient_cough_damp_m1.wav`(表不动)⇒
      `PipelineGateTest` **15 红/37 绿**,根因全为 `[D6·素材存在性]` 硬失败;还原 SHA-256 逐字节一致
- [x] **AC-44-D15(新)**: 5 个被引 `.wav` 全部落地 `assets/audio/`,且列入 Addressables
      素材组(与 `data-core` 组区分,承 AC-44-D5);Import Load Type 按 `audio.md` 建议 pin
      ✅ **2026-10-17 落** —— ① 组分离:`audio` 组 5 条,`data-core` 含 `Assets/Audio` 条目 **0**;
      ② **Load Type 已 pin**(承 `OQ-44-2` 结案):`music_*` = `Streaming`(`loadType: 2`,
      Unity 导入回写实测稳定),其余 4 个 = `DecompressOnLoad`(`0`,现值即正确);
      判据可复算 = GDD §OQ-44-2 附
- [x] **AC-44-D16(新)**: `audio_events.json` ↔ `assets/audio/` 的**双向差集归零** ——
      既无「被引但不存在」,也无「存在但未被引」(孤儿素材)
      ✅ **2026-10-17 落** —— 实测:表引用 5 / 盘上 5 / 缺项 **0** / 孤儿 **0**

> **证据原件**:`production/qa/evidence/impl-s15-asset-content-2026-10-17.md`
> (§2 实测判据 · §3/§3.5 三轮证伪 · §3.6 Load Type 定档 · §4 欠账)

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
- `production/qa/evidence/impl-s15-asset-content-2026-10-17.md`(含**真实烘焙日志** —— 证明门在真素材上跑过)
  ⚠️ **路径订正(2026-10-17 收口)**:原写 `audio-binding-evidence.md`,该文件**从未创建**;实际落盘为上式。
- 构建期断言测试(扩展 story 010 的门测试,补真素材用例)

**Status**: ✅ RUN —— 2026-10-17(`unity/Logs/s15-final.xml`:`total="395" passed="395" failed="0"`)
**Test File**: `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`(**52 测**)
· 账本并列:`tests/integration/audio_system/README.md`

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

**Completed**: 2026-10-17
**Criteria**: **4/4 passing**(AC-44-D6b / D6c / D15 / D16 全勾,无 DEFERRED)
**Deviations**: 6 条 ADVISORY(见下,**均不阻塞**)
**Test Evidence**: Integration —— `unity/Assets/Tests/EditMode/Audio/pipeline_gate_test.cs`(52 测)
· 账本 `tests/integration/audio_system/README.md`
· 证据原件 `production/qa/evidence/impl-s15-asset-content-2026-10-17.md`
**Code Review**: **✅ 已跑 2026-10-17** —— 判定 **APPROVED WITH SUGGESTIONS**(无 BLOCKING ·
无 ARCHITECTURAL VIOLATION · 无 ADR DRIFT)。三位专家并行(`unity-specialist` 15 调用 ·
`unity-addressables-specialist` 10 · `qa-tester` 8,均在 Phase 7b 预算内交付)⇒ 产出
**7 WARNING + 4 无载体子句 + 1 条 CI 必红测试**;两处文档硬错(D-1 计数 53→52 · D-2 证据 §5
自相矛盾)已就地修复。**报告原件 = `production/qa/evidence/review-s15-code-2026-10-17.md`**
(含 Phase 8b 双列结账表:结清 2/14,余 12 条如实留作未结)。
> ⚠️ **原件义务说明**:该义务依 `.claude/docs/coding-standards.md` 只在**产出 BLOCKING** 时强制;
> 本评审无 BLOCKING ⇒ 不强制,用户 2026-10-17 裁定**仍落原件**(见 §收口裁定)。
> **7 WARNING 的后续归属**(未裁定,登记待议):W-2/W-3 → story-010 加固轮;
> W-4/W-5/W-7 → 并入 `TD-audio-01` 面;W-1/W-6 → 已由 GDD §OQ-44-2 附覆盖,不改。

### Falsifiability Gate(S12-4)= **PASS**

| 命中形态 | 主要断言 | 反证:停用什么 ⇒ 哪条转红 |
|---|---|---|
| 定义域收窄 | 素材存在性 | 真删被引 `.wav`(表不动)⇒ **15 条红**,根因 `[D6·素材存在性]` |
| 定义域收窄 | 缺失集去重 | 停用 `AudioAssetExistence.Missing` 的 `seen.Add` ⇒ `test_d6_missing_isOrderPreservingAndDeduped` 红 |
| 共存/互斥 | 组不相交 | 停用 `AudioAssetGroupGates.ValidateEntries` 的 `inDataCore` 分支 ⇒ `test_d5_pure_audioAssetInDataCoreGroup_rejected` 红 |

### ADVISORY 登记(6 条)

1. **Manifest 陈旧** —— story `2026-10-03` vs `control-manifest.md` `2026-10-15`(收口时未回填版本号)。
2. **证据路径订正** —— 原声明 `audio-binding-evidence.md` 从未创建;真件 = `impl-s15-asset-content-2026-10-17.md`(已就地订正)。
3. **`## Test Evidence` 段曾陈旧** —— 原写 `Status: [ ] NOT-RUN` / `Test File: TBD(待实现)`;已就地订正为实测值。
4. **AC-44-D15「Load Type pin」半无自动化载体** —— 证据 = `.meta` 一行 diff + Unity 批导入回写实测;
   story 自己的 QA 边界用例写「Import Load Type 未 pin(**须报**)」—— **该「报」无实现**。
5. **AC-44-D16「孤儿」半无载体** —— 生产码零孤儿谓词(`Implementation Note 4` 设计即「报告不硬失败」)。
6. **AC-44-D15 的组半不可从全新 clone 复现** ⚠️ —— `unity/Assets/AddressableAssetsData/`
   被 `.gitignore:85` **整目录忽略**,`audio` 组与其 5 条目只存在于机器本地;
   新检出后须**手跑** `DataBakeMenu.EnsureAudioGroup` 才成立。
   同族先例 = `TD-medcons-09`(测试依赖 `Application.dataPath`,CI 布局变化即红)。
   ✅ **2026-10-17 已落册** = `docs/tech-debt-register.md` **`TD-audio-01`**
   (触发条件 = CI runner 落地时,与 `TD-medcons-09` 同族;**高优先**)。

### 收口裁定(2026-10-17 用户)

- **评审报告原件义务 = 窄读** —— 依 `review-workflow.md` 原文「无原件的**被评审对象**不得转 Complete」,
  义务范围 = 被评审对象;story-015 **从未被评审过**(一直 `Ready`,从未从 `Complete` 撤回)⇒ **不在义务面内**。
  epic 级义务已由 007–012 各自的 S14-7 复评件结清。
  (story-done 触发文字较宽 —— 「本 story **或其所归属的 epic**」—— 用户裁定取窄读。)
- **代码评审** = 待跑(见上)。

### 沿革

本件为 2026-10-03 补立,填「素材本体接入」这个 story 010 **机制已建但真素材未验**的面。
同型先例 = `skeuomorphic-ui/story-019 贴图接入`。
