# Story 016: 音频资产接入(余 7 项源音覆盖 · R-2 归属表 9 项逐项对账)

> **Epic**: 音频系统
> **Status**: Ready ⬜(依赖素材本体产出)
> **Layer**: Foundation(实现落素材产出 + 门检验)
> **Type**: Integration
> **Estimate**: 待估(依赖素材本体产出)
> **Manifest Version**: 2026-10-04
> **Last Updated**: 2026-10-04

## Context

**GDD**: `design/gdd/audio-system.md`
**Requirement**: `TR-audio-010` 的**素材本体半边**(第 ① 半「烘焙管线」已由 story 010 兑现)+
`R-2` 归属表的 **9 项 audio-system 资产**逐项落地

**ADR Governing Implementation**: ADR-018 §五(素材 = `assets/audio/` Addressables 流式)+ ADR-014: 数据管线与 JSON 解析器
**ADR Decision Summary**: 素材缺失 = 编辑期门拒绝(非运行期降级);玩家构建零 JSON 解析器;素材与 `data-core` 组区分

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(Addressables 6.2+ 抛异常 post-cutoff)
**Engine Notes**: 同 story-015 —— 本 story 不引入新 API 面,它把已有机制喂真素材。

### ⚠️ 本 story 的存在理由(实测,2026-10-04)

**R-2 归属表裁给 `audio-system` 的是 9 项**(`r2-asset-ownership-ruling-2026-10-03.md` §`audio-system`(9 项)),
而 **`story-015` 的实际覆盖只有 4 项**(它以 `assets/data/audio_events.json` 里**已被引用的 5 个 `.wav` 文件名**
为界,不是以 R-2 的 9 项资产为界)。

逐项对账(**本 story 的立项依据**):

| R-2 的 9 项 | story-015 的 5 个 wav | 覆盖 |
|---|---|---|
| [SFX #1] 诊脉接触音 | — | ❌ **本 story** |
| [SFX #2] 听诊器接触音 | — | ❌ **本 story** |
| [SFX #3] 病人呼吸两层 | `sfx_breath_base_loop_small` + `sfx_breath_adventitious_fine` | ✅ 015 |
| [SFX #6] 病人语声 | `vo_patient_cough_damp_f1` + `vo_patient_cough_damp_m1` | ✅ 015 |
| [SFX #7] 纸面物理声 | — | ❌ **本 story** |
| [SFX #8] 脚步声 | — | ❌ **本 story** |
| [SFX #9] 药柜/器物声 | — | ❌ **本 story** |
| [SFX #10] 世界声 | — | ❌ **本 story** |
| [Ambient #1] 医馆室内环境音 | — | ❌ **本 story** |

⇒ **7 项无承接件**(2 项由 015 承载)。

> ⚠️ **一处对上游计数的订正(登记不隐藏)**:制作人报告记该缺口为 **4 项**
> (脚步/纸面/药柜/世界声)。实测**是 7 项** —— 制作人**漏数了 [SFX #1] 诊脉接触音 /
> [SFX #2] 听诊器接触音 / [Ambient #1] 医馆室内环境音**三项。原因:它按「`audio_events.json` 未引用的
> SFX」计数,而这三项目前**连被引用都没有**(不存在于事件表)⇒ 双重缺口(未引用 + 未产出)。

### ⚠️ 与 story-015 的关系(刻意分件,不合并)

- **015 = 「已引用但不存在」的修复**(引用边存在,资产缺失 ⇒ 断链);
- **016 = 「本应有但连引用都没有」的补齐**(资产需求已由 R-2 裁定归属,**事件表内尚无对应条目**)。
- 两者的**门**不同:015 的门查「被引 `.wav` 是否存在」;016 的门查「R-2 的 9 项是否**逐项**有
  `audio_events.json` 条目 + 素材文件」—— 它是 **R-2 归属表的闭合断言**,不是 015 的扩展。
- ⚠️ **不合并的理由**:合并会让「已引用的断链」与「未建的需求」两种失效模式**再次混同**
  (承 `epics/index.md` 对 audio-system「范围边界声明」的教训 —— 混计已发生过一次)。

---

## Acceptance Criteria

- [ ] **AC-44-E1(新)**: R-2 归属表 `audio-system` 的 **9 项**在 `assets/data/audio_events.json` 内
      **逐项有对应条目**(诊脉接触音 / 听诊器接触音 / 呼吸两层 / 语声 / 纸面 / 脚步 / 药柜 / 世界声 / 馆内环境音);
      缺任一项 ⇒ **构建期硬失败**(非告警 —— 归属已裁而条目缺失 = 归属表落不了地)
- [ ] **AC-44-E2(新)**: 上述 9 项引用的**全部 `.wav`** 在 `assets/audio/` 内真实存在;
      **门须跑真素材,不得只跑夹具**(承 story-010 的教训:其 11 测用夹具 ⇒ 缺口不可见)
- [ ] **AC-44-E3(新)**: 删除任一被引 `.wav` ⇒ **构建期硬失败**(`throw` 非 `Debug.Assert`,同 story-015 AC-44-D6c)
- [ ] **AC-44-E4(新)**: 双向集合差归零 —— R-2 的 9 项 ↔ `audio_events.json` 条目 ↔ `assets/audio/*.wav` **三者互相覆盖**
- [ ] **AC-44-E5(新)**: **无提示音铁律**(`AC-44-09` BLOCKING)对本 story 新增条目同样适用 ——
      新增条目**不得**用于状态播报(无 sting / jingle / ducking / 素材切换报状态)

---

## Implementation Notes

*Derived from ADR-018 §五 / ADR-014:*

1. **补齐顺序**(按「是否已有事件表条目」分两段):
   - 段一:**事件表内已有条目但无素材** —— 若实测存在,优先(与 015 同型);
   - 段二:**事件表内连条目都没有** —— 诊脉接触音 / 听诊器接触音 / 馆内环境音**大概率在此段**
     (须实测确认,不得假设)。
2. **命名**:承 `audio_events.json` 既有前缀规范 —— `music_*` · `sfx_*` · `vo_*` · `amb_*`
   (馆内环境音若无所属前缀,须先定,不得新造不规则名)。
3. **Addressables**:全部落 `assets/audio/` 的 Addressables 资产组(承 ADR-018 §五);
   **不进 `data-core` 组**(素材与数据分组,ADR-014 §五)。
4. **门**:扩展 story-010 已有的构建期扫描 —— 增 E1/E3/E4 三条断言。
5. **诚实性**:本 story **不产出素材** —— 它是**需求闭合件**。素材产出归素材批次(批次归口见 R-2 表
   「素材批次」栏)。

---

## Out of Scope

- Story 015:5 个**已被引用**的 `.wav` 落地(引用存在、资产缺失)
- Story 010:烘焙管线门本体(第 ① 半)
- 急救专用 SFX(**例外**:归 `emergency-procedures`,见 R-2 表)
- 素材的实际录制 / 制作 —— 归素材产出批次

---

## QA Test Cases

**AC-44-E1**:
- Given: R-2 归属表的 9 项清单 + `audio_events.json`
- When: 构建期逐项对账
- Then: 9 项各有条目,零缺失
- Edge cases: 一项拆多条目(合法,须双向覆盖证明);条目存在但 `id` 与归属表对不上(非法)

**AC-44-E2 / E3**:
- Given: 9 项引用的 `.wav` 清单 + `assets/audio/`
- When: 存在性检查;**并**删除任一文件后重跑
- Then: 存在时静默通过;删除后**构建期 `throw`**
- Edge cases: 空文件(非法);同名不同扩展(非法);大小写变体(按平台规则,须显式)

**AC-44-E4**:
- Given: 三方集合(R-2 九项 / 事件表条目 / 磁盘 `.wav`)
- When: 双向差集
- Then: 差集为零
- Edge cases: 有素材无条目(非法);有条目无归属(可能合法 —— 新增内容须回到归属表登记)

**AC-44-E5**:
- Given: 新增的 9 项条目
- When: 对照 `AC-44-09` 白名单断言
- Then: 全部 ∈ 行为反馈白名单,**无一条**用于状态播报
- Edge cases: 边界 —— 「世界声」含随机事件音(random-events 触发);须证明它属**环境**,非**播报**

---

## Dependencies

**前置**:
- `assets/audio/` 目录建立(现为空)
- 素材产出批次(本 story 只写需求与门,不产素材)
- `audio_events.json` 的 `amb_*` 前缀规范(若无,须先定)

**下游**:
- `AC-44-09` 白名单断言的完整覆盖(承 ADR-018 §六)
- 44 的「9 项 R-2 归属闭合」—— 本 story 是它的唯一承接件

**关联**:
- story-015(同型但不同段:已引用 vs 未建需求)
- `r2-asset-ownership-ruling-2026-10-03.md`(归属真源)
