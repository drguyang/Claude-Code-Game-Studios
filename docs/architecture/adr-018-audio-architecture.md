# ADR-018: 音频架构(44 的呈现层边界 · 混音拓扑 · 无提示音铁律的机械化)

## Status

Accepted

> **2026-09-15 起草并转 Accepted。** 一条核心裁定已锁:**44 与 42 同构 ——
> 「只触发 / 只渲染,永不持有游戏状态」**(`AudioCueDto` 为其呈现 DTO)。
> 其余裁决由技术面推出:混音拓扑 · 无提示音铁律的**机械化验收** · 8 的四条硬需求落 AC ·
> 联机单 `AudioListener` 约束 · ~~VR 音频推 P1a~~ → **2026-09-18 订正为 P1b**。
> 直接依据 = **`diagnosis-system.md` V-8.7**(8 自陈「44 是本节最重的技术债」)与
> **`docs/engine-reference/unity/modules/audio.md`**(仓库自有权威件)。
>
> **🔧 2026-09-18 本次修订(44 GDD 首轮 `/design-review` 的跨 ADR 涟漪;用户裁定 A/B/C/D 照准)**:
> ① **§五 的「单 `AudioListener` ⇒ 每设备一条总线」前提为假** —— 三个专家独立证伪
> (`audio.md:256-266` 只约束空间化参考点,不约束总线数;联机 = 各自设备各自 mixer)。
> **裁决 D-A = 重开 2026-09-14 用户裁定 → `SetTier(TierSource.Local)`(各按本机技能档)**;
> §五 理由重写、Alt-4 拒绝理由改、`AC-44-07` 与 44 GDD 规则七同步改判。
> ② **§三 / Summary / Decision 的总线数漂移**(写 6 条、漏 `UICue`)→ 统一 **7 条**。
> ③ **§六 白名单增 `trigger_source` 字段**(堵「阈值时刻的咳嗽」类时机违规)。
> ④ **§七 P1a → P1b**(VR 功能本身在 P1b,音频不可能先于模式存在)。
> ⑤ **§一 表删 `VitalsDto` 入口**(幽灵旁路,见 44 GDD 规则一注)。
> ⑥ **§Key Interfaces 的 schema 示例行补 `whitelist_category` / `trigger_source`**(见 ③)。
> ⑦ **§五「远端玩家空间化」的通道来源改写为 `ADR-001 §一之二 IPositionalChannel`**
> (per-`ActorId` latest-value 表,2026-09-18 自 `IReplayPipe` 拆出;**同轮改 ADR-001** ——
> 其原文零处提及音频,44 的 §Dependencies 声明依赖却无处落地);44 只读声源锚点、**cue 非复制**。

## Date

2026-09-15

## Last Verified

2026-09-15

## Decision Makers

dr_guyang(用户 · **2026-09-15 一项核心裁定:44 与 42 同构的呈现层边界**)· technical-director(起草与裁决)
· audio-director(44 方向)· sound-designer(SFX spec / 事件表)· unity-specialist(引擎侧:AuidoMixer 快照 / 空间化)
· 系统 8 诊断与体征(四条硬需求来源)· 9 疾病与伤情(呼吸层)· 13 病人 AI(咳嗽呻吟触发)· 42 拟物 UI / 2 摄像机 / 52 随机事件导演

## Summary

**44 音频系统是 P0 里最名不副实的一项**:Foundation 层、零依赖、全案**只有两行描述**,
却在 P0 就要求它**承担状态反馈职责**(`systems-index.md:254,513`)。同时,
`diagnosis-system.md` **V-8.7** 把它定为「本节最重的技术债」,并登记了 **8 对 44 的四条硬需求**
(呼吸两层渲染 · 语声变体库混合 · 暴露接触噪声参数 · **联机取主机技能**)——
> **⚠️ 第四条已于 2026-09-18 修订**:见 §五,现为「各机玩家自己技能档」。
**44 至今零 ADR、零 GDD**。
> ⚠️ **2026-09-21 历史注**:「零 GDD」「`design/gdd/` 下无任何 audio 文档」是本 ADR(2026-09-15)
> 时的状态,44 后已立 `audio-system.md`(下条 Current State 同此)。裁决内容不受影响。
本 ADR 裁决:**44 与 42 同构 —— 只触发 / 只渲染,永不持有游戏状态**;
其载体是 **`AudioCueDto`**(呈现层 DTO);**音频事件表走 ADR-014 烘焙管线**;
**混音拓扑**(Master / Music / Ambience / Voice / SFX / Stethoscope / UICue,共 7 条)与快照清单定型;
**「无提示音」铁律机械化**(EditMode 断言音频触发源 ∈ 白名单**且 `trigger_source` ∈ 合法集**,禁 sting / ducking 播报状态);
**8 的四条硬需求登记为 AC**;**听诊音精度 = 各机玩家自己技能档**(`SetTier(TierSource.Local)`,2026-09-18 修订);
**VR 音频推 P1b**(与 VR 功能档位一致)。

> **2026-09-18 修订**:原文此处写「联机精度统一取主机技能(单 `AudioListener` 结构性约束)」——
> 前提已证伪(见 §五 修订);总线数原文漏 `UICue`,此处补为 7 条。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Audio(混音 / 空间化 / 事件触发) |
| **Knowledge Risk** | **MEDIUM** —— **`modules/audio.md:4` 自陈 Knowledge Gap:「Unity 6 audio mixer improvements」**。AudioMixer 快照 / ducking / 空间化的**基础 API** 为长期稳定(`audio.md` 285 行覆盖 AudioSource · 3D 空间化 · AudioMixer 组与 exposed params · `AudioMixerSnapshot.TransitionTo` · **Single Listener Rule**:`audio.md:256-266`「Only ONE AudioListener active at a time」)。裁决的**接口层**(`AudioCueDto` / 事件表)为纯 C# 契约,不受影响 |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` · **`docs/engine-reference/unity/modules/audio.md`**(AudioMixer 快照 / ducking / 3D 空间化 / Single Listener Rule)· `docs/engine-reference/unity/modules/ui.md:240`(`AudioListener.volume`)· `docs/architecture/adr-013-skeuomorphic-ui-framework.md`(**42 只渲染不持有状态** + `PresentationDtoGuard`)· `docs/architecture/adr-014-data-pipeline-and-json-parser.md`(烘焙管线)· `docs/architecture/adr-015-world-geometry-fixed-world-lattice.md`(整数格 / 禁反推)· `docs/architecture/adr-016-ai-architecture.md:232-236`(13 拥有咳嗽呻吟触发,属表现层)· `docs/architecture/adr-001-networking-pipe-abstraction.md`(第二 QoS 通道)· `design/gdd/diagnosis-system.md` **V-8.7 §1553-1632** · `design/gdd/disease-simulation.md:1111-1121` · `design/gdd/case-system.md:539-548` · `design/gdd/random-events.md:977,1058` · `design/gdd/systems-index.md:78,254,513,645` |
| **Post-Cutoff APIs Used** | **None 承诺** —— 基础 AudioMixer / AudioSource API 为长期稳定;`modules/audio.md` 自陈的「6.3 mixer improvements」**不在本裁决的依赖面**,实现期若需新 API 须实测并登记 |
| **Verification Required** | ~~① 单 `AudioListener` 约束实测~~(**2026-09-18 消解:前提已证伪,见 §五**);② 呼吸两层渲染 + 接触噪声参数**可听性实测**(低/中/高三档的「通带与噪声底」差异是否可辨);③ **VR 空间化 + `<50 ms` 路径实测**(音频触发是否在延迟预算内);④ **无提示音白名单断言**在 EditMode 生效;⑤ 变体库混合的交叉淡化无爆音(spike) |

> **Note**: Knowledge Risk **MEDIUM** —— `modules/audio.md` 的「6.3 mixer improvements」是**唯一的
> post-cutoff 悬置**。升级引擎版本时须重读本 ADR 的**混音拓扑**部分;接口层不受影响。

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-013**(Accepted —— **42 只渲染,永不持有游戏状态**;本 ADR 依此把 44 定为同构层)· **ADR-014**(Accepted —— **音频事件表走烘焙管线**,玩家构建零 JSON 解析器)· **ADR-015**(Accepted —— 世界坐标为整数格;音频空间化**不得从视觉坐标反推逻辑格**)· **ADR-016**(Accepted —— §六:13 拥有「咳嗽呻吟 → 音频」的表现映射,属表现层,**不经 sim**)· **ADR-005 / 006**(Accepted —— 音频是表现层,不触 sim 的禁 float 边界)· **ADR-001**(Accepted —— 表现态走第二 QoS 通道,音频复用其位置来源)|
| **Enables** | **44 的 GDD 撰写**(本 ADR 是其权威件)· **8 的四条硬需求可立 AC**(V-8.7 登记的债务清偿)· **`TR-randomevents-024` 之外的 52 ↔ 44 契约**(预告线索音频侧)· **13 的咳嗽呻吟触发的落点** · 9 的呼吸层音频规格 |
| **Blocks** | **系统 44 的实现**;8 的音频侧 AC(V-8.7 四条);52 的预告线索音频 / 9 的呼吸层 / 37 的结案纸页声 / 13 的咳嗽呻吟 —— 凡依赖「音频事件表 + 混音拓扑」的 AC |
| **Ordering Note** | 本 ADR 是 **R-12 的落点**。**先 Accepted 本 ADR,再写 44 的 GDD 与实现**(V-8.7 明写「必须在 44 写 GDD 之前定」)。**不阻塞** 8 / 9 / 13 / 37 / 52 的既有实现(它们是 44 的上游语义提供者)。`/art-bible` 的音频方向与 44 同批定(`systems-index.md:645`)|

## Context

### Problem Statement

44 的处境是本项目**唯一一处「系统在 P0 有承重职责却没有权威件」**:

- `systems-index.md:254` —— **「44 音频系统 — 承担状态反馈职责」**(全案唯一职能声明);
- `systems-index.md:513` —— 「**升 C 档**」,理由:「Foundation 层零依赖,全案却只有 2 行描述
  (`game-concept.md:140/538`),**最名不副实的一项**」;
- `diagnosis-system.md` **V-8.7** —— 「**44 是本节最重的技术债**」,并列 **8 对 44 的四条硬需求**:
  「**必须在 44 写 GDD 之前定**」;
- 而 `docs/registry/architecture.yaml:266` 已把 **44 音频**归入**表现层产物**(与 `NavMeshAgent` / `Animator` 并列)。

**不裁决的代价** —— 三类**静默**失败:

1. **音频越过呈现边界**:若 44 直接读 sim 状态(而非经 `VitalsDto` / 表现层事件),
   则「42 只渲染不持有状态」的纪律在此**失效**,且 `PresentationDtoGuard` 的 `disease_id` 禁入
   可能被音频侧绕过 —— 这是本项目最不能容忍的泄漏面(AC-37-15)。
2. **「无提示音」铁律落不了地**:铁律散落在 9 / 8 / 37 三份 GDD(见 §Constraints),但**无机械验收** ——
   实施者加一句 `PlayOneShot(ding)` 就能破坏支柱一(「判断为骨」),而这是**听不出来是 bug** 的那一类。
3. **四条硬需求无载体**:V-8.7 明确登记「本次只登记为债务,未立 AC,44 写出 GDD 后回溯补 AC」
   (OQ-8-4)—— 没有 ADR,债务只会继续挂账。

### Current State

- **44 零 ADR、零 GDD**;`design/gdd/` 下无任何 audio 文档。
- **全案无任何 AudioMixer / 快照 / 总线 / ducking 的落地设计**。唯一出现的混音约束是
  `diagnosis-system.md:1579`「Unity **单 `AudioListener` 每设备只有一条混音总线**」——
  **2026-09-18 注:这句本身是误读**(见 §五)。
- **不存在 sound-bible 实例**;仅有模板 `.claude/docs/templates/sound-bible.md`
  (含 `:77 Mix Bus Structure` 表 · `:69` 空间音频规则「gameplay SFX 3D / UI SFX 2D」·
  `:70` ducking 规则 · `:126-127` 无障碍:关键音须有视觉替代 / mono 选项)。
- **上游语义已齐备,只差 44**:
  - `disease-simulation.md:1111-1121` §三「音频需求(归 42 / 音频)」:病人呼吸层(随病情平稳→急促→浅慢→停顿)·
    **咳嗽/呻吟由 13 触发不由 9** · **无提示音硬约束** · 昏迷/死亡听觉差异(「呼吸停顿 vs 呼吸停止,
    **必须能被听出来,但不许用音效硬报**」);AC-21:`1235`「9 **不产出任何音频资源**」。
  - `case-system.md:539-548`:结案纸页翻合声(禁「成就达成」音效)· **同源触发 = 无任何提示音** · 立案落纸声;
    硬约束「任何呈现不得暴露 `disease_id`」。
  - `random-events.md:977`:契约行「44 音频 | 44 ← 52 | 预告线索的**音频侧**(狗吠 / 马蹄 / 锣声;原表漏)」;
    `:1058` 档位表:威胁=狗吠/马蹄/锣声/鸟群惊飞,机会=门外铃声/货郎吆喝,灾难=哭喊/钟声。
  - `diagnosis-system.md:1627` V-8.9 素材命名归属(`sfx_breath_base_loop_small.wav` /
    `sfx_breath_adventitious_*.wav` / `sfx_steth_contact_*.wav`)。
  - `item-database.md:909`「全部视觉与音频归 20 / 42 / 44 拥有」。
- **VR 音频:全库零条**。**联机**仅 ADR-001 第二 QoS 承载表现态位置(2026-09-18 起 = `IPositionalChannel` per-`ActorId` latest-value 表,44 已登记为消费者,见 §一/§五);无语音规划。

### Constraints

- **无提示音铁律散布三处,须一并机械化**:
  - `disease-simulation.md:1117,1120-1121`:"一旦加了『叮』的一声表示病人转危,就把 9 变回**报警器**,
    玩家不再需要判断 —— 这是对**支柱一「判断为骨」的直接破坏,比数值错误严重得多**";AC-21。
  - `diagnosis-system.md:1597`:「不许用 audio sting / ducking **报**病人状态变化」
    —— 但**允许**纸面的物理声(笔尖走路 / 翻纸 / 纸的摩擦),因其属**行为反馈**;
    `:1478` 同调「不许感叹号、红色、弹窗、『叮』」;AC-8-30(误诊零提示音,**BLOCKING**)。
  - `case-system.md:548`:「**无**任何提示音 | 优先级 高」。
- **42 只渲染,永不持有游戏状态**(ADR-013 §9 C3);UI 只读 `VitalsDto`;
  `disease_id` 不进呈现层(`PresentationDtoGuard` **递归**反射扫描,AC-37-15)。
- 呈现层**只读**禁的是**回写 sim 真值**(三流),**不禁表现层输出**(ADR-016 §六)——
  音频触发是表现层产物,**不进事件流**。
- 音频**不是** sim 的一部分:`diagnosis-system.md:1582`「音频是表现层」;
  sim 程序集零 `UnityEngine` 引用(ADR-005 门 A)。
- 表现层**禁从视觉坐标反推逻辑格**(ADR-015);音频空间化须从**表现态位置**取,不回写 sim。
- 单 `AudioListener`(`audio.md:256-266` Single Listener Rule)—— **仅约束空间化参考点**,
  **不约束总线数**(2026-09-18 订正;原写「每设备一条混音总线」为误读)。
- 素材 = `assets/audio/`(不进 ADR-014 的 `data-core` 数据烘焙? — §五 定)。
- 帧预算 16.6 ms 平面 / 11.1 ms VR(VR 硬性 90 fps);急救输入延迟 < 50 ms。

### Requirements

- 44 必须有**可机械验收的呈现层边界**(与 42 同构),含 `disease_id` 禁入。
- 「无提示音」铁律必须有**机械化的失败模式**,而非仅靠 code review。
- 8 的四条硬需求必须**登记为 AC**(清偿 V-8.7 / OQ-8-4 债务)。
- 混音拓扑与快照清单必须**单一定义**,且**不得用快照播报状态变化**。
- 音频事件表必须走 **ADR-014 烘焙管线**(玩家构建零 JSON 解析器)。
- 联机音频必须**在单 `AudioListener` 约束下自洽**,且与 ADR-001 的分层一致。
- VR 音频必须**明确 P0 不做**,并给出 **P1b** 的交付切面。

## Decision

**裁决:44 与 42 同构 —— 只触发 / 只渲染,永不持有游戏状态(`AudioCueDto`);
音频事件表走 ADR-014 烘焙管线;混音拓扑(Master / Music / Ambience / Voice / SFX / Stethoscope / UICue,共 7 条)
与快照清单定型;无提示音铁律机械化(白名单断言 + `trigger_source` 时机字段);
8 的四条硬需求登记为 AC;**听诊音精度 = 各机玩家自己技能档**;VR 音频推 P1b。**

> **2026-09-18 修订**:原 Decision 段写 6 条总线(漏 `UICue`)、「联机精度统一取主机技能」、
> 「VR 音频推 P1a」—— 三处同批订正为 7 条 / 各机本地档 / P1b。

### 一、44 的呈现层边界 —— 与 42 同构

**44 是表现层,不是模拟层,也不是 UI 层。** 采纳 ADR-013 §9 C3 的同构规则:

| 维度 | 42 拟物 UI(ADR-013) | 44 音频(**本 ADR**) |
|------|---------------------|---------------------|
| 核心纪律 | **只渲染,永不持有游戏状态** | **只触发 / 只渲染,永不持有游戏状态** |
| 数据入口 | `IVitalsQuery → VitalsDto`(唯一 float 出口) | **`AudioCueDto`**(见 §二)—— **唯一入口** |
| 写入 sim | **禁**(不写三流) | **禁**(不写三流) |
| `disease_id` | **禁入**(`PresentationDtoGuard` 递归扫描) | **禁入**(**同一守卫覆盖**) |
| 触发来源 | 玩家输入 / 呈现事件 | **语义 cue id**(由上游系统的表现映射发出) |

**关键点**:音频**不订阅 sim 真值**。它接收的是**上游系统在表现层发出的语义 cue**
(如「呼吸层:急促」「纸面:翻页」「威胁线索:狗吠」);
cue 的**语义归属**在上游(13 的咳嗽呻吟 · 37 的结案纸页 · 52 的预告线索 · 8 的听诊层),
44 只负责**把 cue 变成声音**。

> **2026-09-18 修订(幽灵入口删除)**:原表列 44 的数据入口 = `AudioCueDto` **+ `VitalsDto`**,
> 但 44 全文**无任何消费者 / 公式 / AC 引用 `VitalsDto`** —— 一个未定义的第二入口
> = **可绕开上游 cue 语义、直接用体征驱动呼吸声**的旁路,<u>从而绕开 AC-44-09 的白名单</u>。
> **已删除**;44 的**唯一**数据入口 = `AudioCueDto`。

### 二、`AudioCueDto` —— 音频的呈现 DTO

```
// 呈现层契约程序集(仅 BCL 依赖)。不承载 disease_id。字段用原语(int3 / int)。
public readonly struct AudioCueDto
{
    public readonly CueId     Cue;          // 语义 cue 枚举/id(如 BreathLayer_Urgent)
    public readonly byte      Intensity;    // 0–255 归一强度(整数,与 sim 的定点域无涉)
    public readonly int3      Cell;         // 空间化锚点(整数格,ADR-015;用 int3 不用 sim 的 WorldPos)
    public readonly int       Source;       // 声源实体 id(可为 None = -1 —— 环境音);用 int 不用 PatientId
    public readonly bool      Looped;       // 循环(呼吸层)/ 一次性(咳嗽)
}
```

- **`Intensity` 是 `byte`** —— 它在**呈现层**是渲染参数,**不回写 sim**,
  不受 ADR-006 的禁 float 约束(它在定点域之外)。但**取整数**是为了与上游的整数语义一致,
  避免在边界再造一层 float 换算。
- **`Cell` 是整数格**:空间化由表现层把整数格换算为 float 世界坐标(ADR-015 已定
  「`WorldPos` 单一整数格」);**禁**从此 float 反推逻辑格。
- **`disease_id` 不在 DTO 中**。上游若需按病种选素材(`sfx_breath_adventitious_*.wav`),
  其解析在**上游**完成(8 的通道 × 档位数据表),44 只收结果 cue id。
  `PresentationDtoGuard` 的**递归**扫描同样覆盖 `AudioCueDto`(AC-37-15 一致性)。

> **2026-09-18 修订(两处)**:
> ① **`Tier` 字段移除** —— `Tier` 是**设备级**的(由 `SetTier` 设定),一个 `AudioMixer` 组
> **全局共享**;逐 cue 带 `Tier` 会让同格内不同档声源**互相踩滤波**。逐源滤波须用
> **逐源** `AudioLowPassFilter` / `AudioHighPassFilter`,不用共享组。
> ② **契约程序集定型(仅 BCL)** —— 原 DTO 承载 `WorldPos` / `PatientId`。若这些类型住 sim 程序集,
> 则 44 引用它们即**违反 AC-44-B1**。故契约程序集字段一律用**原语**(`int3` / `int`),
> **不引用 sim 的程序集**;sim → 契约的类型映射由上游在边界处完成。

### 三、混音拓扑与快照(单一定义)

```
AudioMixer: Master
├── Music         (京戏 / 氛围曲)
├── Ambience      (环境:风 / 鸟 / 村落底噪)
├── Voice         (语声:病人语声变体库)      ← §四 需求②
├── SFX           (通用:脚步 / 纸面 / 器物)
├── Stethoscope   (听诊:呼吸层 + 接触噪声)     ← §四 需求①③
└── UICue         (UI 行为反馈:笔尖 / 翻纸)   ← 与「状态播报」严格区分(§六)
```

> **2026-09-18 补注**:以上 **7 条**为**总线**;此外须有 **Aux / Reverb send 拓扑**
> (室内 / 室外 / 洞窟预设)—— 否则快照只能改增益、**改不了混响**
> (44 GDD §Visual/Audio 已登记 `reverb_preset_*`)。
> **总线数漂移订正**:本 ADR 早先的 Summary 与 Decision 段写 6 条(漏 `UICue`),
> 与本节及 44 GDD 的 7 条不一致 —— **统一为 7 条**(`AC-44-E3` 断言)。

**快照清单(初始,数值待调)**:

| Snapshot | 用途 | 触发 |
|----------|------|------|
| `Default` | 常规游玩 | 默认 |
| `StethoscopeFocus` | 听诊聚焦(压低 Ambience / Music,抬 Stethoscope) | **玩家主动**进入听诊动作 |
| `DialogueFocus` | 语声优先(压 Ambience / Music) | 病人语声播放 |
| `Paused` | 全总线衰减 | 暂停 |
| `VRComfort` | VR 模式(见 §七) | **P1b** |

> **快照切换一律由「玩家行为 / 场景状态」驱动,绝不用于「播报病人状态变化」。**
> 这是「无提示音」铁律在**混音层**的判据(§六):`StethoscopeFocus` 是玩家**自己**拿起听诊器,
> 不是系统**提示**玩家「该听了」—— 前者是行为反馈,后者是状态播报。

**ducking 规则**:允许**玩家驱动的聚焦** ducking(`StethoscopeFocus` 压低环境);
**禁止**用 ducking **报**状态(如「病人转危 ⇒ 环境音突然压低以突出心跳」)。
诊断领域内的 ducking 属**行为反馈**;诊断领域外的 ducking 属**状态播报** —— 见 §六 白名单。

### 四、8 的四条硬需求 → AC(清偿 V-8.7 / OQ-8-4)

`diagnosis-system.md` V-8.7 登记的四条债务,**逐条落 AC**:

| # | 需求(V-8.7 原文) | AC(可验证) |
|---|------------------|-----------|
| **①** | **呼吸分两层渲染**:基础气流层 + 附加音层;**精度档 = 通带与噪声底,不是静音开关** | **AC-44-01**:呼吸音频在低/中/高三档下,**附加音层始终非零**(只是通带更窄 / 噪声底更高);断言**不存在任何「档位 ⇒ 该层静音」的实现路径**(BLOCKING)。**AC-44-02**:三档的差异经听测确认**可辨**且**非静音型**差异 |
| **②** | **语声走「变体库混合」**(2026-09-14 复核**降格**自「运行时参数调制」) | **AC-44-03**:语声素材形态 = **变体库**(男/女 × 强/中/弱 × 数种句尾),运行时按 **(病种语义, 精度档, 强度)** 选变体并**交叉淡化**;**禁**对已录语声做五项独立调制(音高/气声/语速/断句/共鸣)—— 那是「变声器」不是「病人说话」。**AC-44-04**:交叉淡化无爆音(spike)、无相位抵消 |
| **③** | **暴露「接触噪声 / 信噪比」参数** | **AC-44-05**:`Stethoscope` 总线暴露**接触噪声底**与**信噪比**参数,由精度档驱动;**AC-44-06**:`READ_FLOOR_MIN > 0` 在音频侧有对应实现 —— **背景噪声永不为零**,但也不毁掉信号(`diagnosis-system.md:1591`) |
| **④** | ~~联机时精度统一取主机技能~~ → **各机玩家自己技能档**(2026-09-18 裁定 D-A) | **AC-44-07**(见 §五 联机,已重写) |

> **第 ①③ 条的设计价值**(V-8.7 原话):「**低技能 ≠ 把啰音静音;低技能 = 更高的接触噪声底、
> 更窄的通带、更少的频段细节。** 这正是真实的实习医生体验。」本 ADR 把这句话**落成 AC-44-01**:
> 「档位 ⇒ 静音」是**断言失败**,不是「可以接受的简化」。

**听诊音准确性(医学受众约束)**:`diagnosis-system.md:1593-1595` 明写「细湿啰音 = 吸气末、断续、
细密的爆裂音(搓头发 / 撕 velcro 那种),**咳嗽后不消失** —— **不是连续水声**」;
**AC-44-08**:附加音层**不得**实现为连续水声(医学受众的第一个出戏点)。

### 五、联机音频 —— 各设备按本机技能档(2026-09-18 修订)

> **⚠️ 本节原理由已证伪并作废** —— 原文:
> 「Unity **单 `AudioListener` 每设备只有一条混音总线**,同屏广播式听诊音**不可能**
> 对四个人各跑一套(那是每设备四条 DSP 链)」**⇒ 统一取主机技能**。
>
> **此前提为假**:钉死的 `audio.md:256-266`(Single Listener Rule)只说
> 「**同一时刻只有一个 `AudioListener` 处于活动状态**」—— 那是**空间化参考点**的约束,
> **从未**说「每设备一条混音总线」。而本作的联机是 **1-4 名玩家各自一台机器、
> 各自一个 Unity 实例、各自一套完整 `AudioMixer`**(`game-concept.md:27`)。
> 「四人共用一条总线」**只在 split-screen 下成立,本作没有 split-screen**。
> 三个专家(unity-specialist / game-designer / network-programmer)独立证伪。

**裁决(2026-09-18 用户裁定 D-A —— 重开 2026-09-14 裁定②)**:
**听诊音精度 = 各设备按本机玩家自己的技能档**(`SetTier(TierSource.Local)`)。

| 项 | 内容 |
|----|------|
| **精度来源** | **本机玩家自己的技能档**(各客户端读取,零同步成本) |
| **边界** | 脉案**每人一本、各按自己技能显字**(铁律⑤)—— **音与字都分叉**;**统一的只有事实**(病情由主机唯一执行,ADR-005 不变) |
| **理由(决定性)** | 原裁决会让**非主机玩家的听诊保真度冻结在主机档**,听觉侧技能成长**永不发生** —— 与 `game-concept.md:124`「你认得出」及 44 的幻想「**你的耳朵提醒你:你没练过听**」**直接冲突**;并制造**反向激励**(新手当主机拖低全队保真度 / 老手当主机则新手跳过学习)。技能档的数据路径**本就存在** —— 客户端已必须读本地技能值显脉案字。 |
| **远端玩家空间化** | 复用 ADR-001 §一之二的 **`IPositionalChannel`**(per-`ActorId` latest-value 表);44 **只读**取声源锚点,**不自建位置通道**、**不新增 QoS 通道**、**禁读其做判定** |
| **语音通话** | **超出 ADR-001 范围** —— 本 ADR **不做语音通道**;若日后需要,另开 ADR(且属 P1+ 范围) |
| **split-screen(假设性)** | 若日后支持 split-screen,则「单总线」约束在该形态下**真实成立**,须**另开 ADR** 登记例外 —— **不得以 GDD 或本 ADR 修订打补丁** |

**AC-44-07**(2026-09-18 重写):联机下**每台设备按本机玩家自己技能档**渲染听诊音
(`SetTier(TierSource.Local)`);脉案的字体量亦各自分叉(回归测试覆盖)。

**本次修订同时作废的从属论断**:
- `OQ-44-3`(「单 `AudioListener` 在联机下的实际混音行为」spike)**消解** —— 前提已不成立;
- **Alt-4「联机时分人分混」的拒绝理由作废** —— 「结构上不可能」为假,现**改为采纳**
  (见 §Alternatives 修订);

### 六、无提示音铁律的机械化(核心)

三处铁律(§Constraints)统一为**一条可机械验收的规则**:

> **音频触发源必须来自「行为反馈白名单」,不得来自「状态播报」。**

**白名单(允许 —— 行为反馈)**:

| 类别 | 例 | 归属 |
|------|-----|------|
| 玩家自身动作 | 笔尖走路 / 落笔 / 翻纸 / 器物放置 / 脚步 | 42 / 44 |
| 玩家主动的检查动作 | 听诊器接触声 / 摩擦声 / 衣料声 | 44(听诊层) |
| 玩家可感知的世界声 | 狗吠 / 马蹄 / 锣声 / 鸟群惊飞 / 哭喊 / 钟声 | 52 → 44(预告线索) |
| 持续生理声(非状态播报) | 呼吸层的**连续**变化(平稳→急促→停顿) | 9 语义 → 13 触发 → 44 |

**黑名单(禁止 —— 状态播报)**:

- ❌ 任何 **sting / jingle / 提示音** 宣告状态变化(「病人转危」「误诊」「结案」「成就达成」);
- ❌ 用 **ducking 报**状态(「转危 ⇒ 压低环境音以突出心跳」);
- ❌ 用**素材切换**报状态(「结果正确 ⇒ 播正确音」;`case-system.md:539-545` 明禁);
- ❌ 用**音频**表达本来该由**玩家读数**得出的结论。

**机械化(AC-44-09,BLOCKING)**:

```csharp
[Test] void AudioCues_AllFromBehaviorWhitelist()
{
    // ① 每行 whitelist_category ∈ { PlayerAction, PlayerExamAction, WorldPerceptible,
    //                               ContinuousPhysiology, MusicLayer }
    // ② 每行 trigger_source ∈ 合法集
    //    { PlayerAction, PlayerExamAction, WorldPerceptible, ContinuousPhysiology, EncounterMusicLayer }
    //    禁止集任一 = 失败: { VitalsCrossed, ThresholdCrossed, OutcomeResolved }
    // ③ PresentationDtoGuard 递归扫描覆盖 AudioCueDto(disease_id 禁入)
    // ④ NOT-RUN 守卫:字段缺失 ⇒ 报错退出(不得静默通过)
    // 双重负向夹具:注入 Sting 类别 → 失败;注入合法类别 + VitalsCrossed → 亦须失败
    // 失败即 BLOCKING —— 铁律不是 code review 项,是构建门
}
```

> **2026-09-18 修订(为何增 `trigger_source`)**:
> 原断言**只查内容,不查时机**。反例:13 恰在病人跨过危险阈值的**那一 tick** 发一条咳嗽 cue ——
> 咳嗽的类别合法、断言全绿,但**那条咳嗽就是警报**。同理 52 的档位表
> (威胁=狗吠 / 灾难=锣声)是「状态枚举 → 声音」的映射**穿了件拟物外衣**。
> `trigger_source` 字段迫使上游**声明**它为什么发这条 cue;
> 声明为 `VitalsCrossed` = **构建失败**。字段定型见 **44 GDD §Event Table Schema**
> (该节由 `OQ-44-4` 升格 —— BLOCKING 门必须有载体,不能骑在 Open Question 上)。

**AC-44-10**:呼吸层的**连续**变化必须可用,但**不得**用「突变音效」标记阈值跨越 ——
「病人转危」必须是**玩家自己听出来的**(`disease-simulation.md:1120-1121`)。

**音乐层例外(2026-09-17 由 25 GDD 回填 · V4 · 白名单第 5 类 + 护栏 G1–G3 全文)**:

> 战斗**乐层**(连续织体)是白名单的**第五类** —— 它不描述某次接触,描述**环境正在发生什么**。
> 与上四类的「物理动作声」性质不同,故**单列且逐条护栏**;无护栏即等于从后门把
> 「音乐报状态」放进来。**例外只豁免「音乐可随遭遇态变化」这一句,G1–G3 是其成立前提,
> 不落地则例外不成立。**

| 护栏 | 内容 | 理据 |
|------|------|------|
| **G1 禁帧对齐** | 乐层变化的**可听输出**不得在 `EncounterStarted` / `EncounterEnded` 到达后立刻起变,切换必须**整体落在交叉淡入淡出区间内**;淡变时长下界 = 旋钮 `MUSIC_XFADE_MIN_MS`(G2 盲测的通过依赖该下界足够长)。**〔2026-09-18 二轮重述〕**原文「不得同 tick / 同帧触发」按字面不可满足 —— 本地任何实现的输出必然落在某渲染帧;判据改钉在**可听层** | 「切歌那一拍」本身就是可感知的状态播报(离散事件感),与 jingle 同罪 —— 连续织体合法的前提正是它**没有边界拍** |
| **G2 去标注盲测** | 试听测试中受试者**不得从音乐指出「遭遇开始的哪一帧」** —— 能指出即违规回炉(= 25 侧 AC-25-V 族的 [V] 判据) | 把 AC 从「听起来自然」升为可判据(G1 的运行时侧证明) |
| **G3 本地设备侧触发** | 乐层 cue **只允许由本地设备侧触发**(本机的遭遇态读数);**不进 27↔44 的任何数值接口,27 永不直接点歌**。**〔2026-09-18 二轮补半边〕**裁定[丙] 的「**昏迷后静默**」语义由 `EncounterEnded`(遭遇收束)承载,**与遭遇开始同一条触发通道;不设独立「昏迷瞬间」音乐触发** —— 静默的**可听时刻**仍受 G1 淡变约束 | 否则音乐触发成为第二条未登记的「27→44 运行时接口」—— 与 ADR-009 §三「引用而未登记」同型失败;触发方登记义务见 25 §6.5(V7:27 侧已补订阅行,44 侧 `CueId` 白名单注明第 5 类) |

> **与黑名单的关系**:黑名单四禁(sting / jingle / ducking 报 / 素材切换报)**对乐层原文适用、
> 不豁免** —— 乐层被允许的仅是「连续织体随环境缓变」;任何**边界拍子、结果标注、
> 成就式正反馈音**仍逐条落黑名单,AC-44-09 的白名单断言把「乐层」作为独立类别校验,
> 断言本体不因本例外降级。

### 七、VR 音频 —— 推 P1b(2026-09-18 订正)

> **⚠️ 2026-09-16 / 2026-09-18 层级订正**:本节标题原写「推 P1a」。**VR 这个功能本身在 P1b**
> (`game-concept.md:720` 范围阶梯「P1b 完整成品 = + 1-4 人联机、**VR 急救动作**、…」
> + `systems-index.md:178`)。**VR 音频不可能先于 VR 模式存在** ⇒ **订正为 P1b**。
> ADR-013 §二 的「VR / world-space 实现推 P1a」指的是 **world-space UI 栈**,不是 VR 功能的档位 ——
> 原 §七 沿用时误读。**44 GDD 硬约束 8 与 AC-44-E1 同批订正为 P1b**(三方矛盾已消解:
> 原文 ADR §七 = P1a、GDD 硬约束 = P1b、GDD `AC-44-E1` = P1a)。

- **P0 不做 VR 音频**(`game-concept.md:682` P0 明确不做 VR)。
- **P1b 交付切面**:VR 急救模式(站定式)的空间化音频策略 ·
  单 `AudioListener` 挂点(**VR 头显** vs 平面**摄像机**)· `VRComfort` 快照 ·
  `<50 ms` 输入延迟预算是否含音频触发路径。
- **AC-44-11**(P1b):VR 空间化音频无晕动症相关副作用(听测);头显挂单 `AudioListener`。

### 八、无障碍(承 sound-bible 模板)

`templates/sound-bible.md:126-127` 的两条规则**保留并落 AC**:

- **AC-44-12**:关键音频(cue 承担**信息**而非纯氛围者)必须有**视觉替代**
  (与 42 的呈现一致;「唯一不看也能听出来的通道」是 9 的呼吸 —— 但**信息不得仅存在于音频**)。
- **AC-44-13**:提供 **mono 选项**(听力辅助 / 单声道设备)。

### Architecture

```text
  上游语义提供者(表现层)                    44 音频系统                        输出
  ┌──────────────────────────┐        ┌───────────────────────────┐      ┌────────────┐
  │ 13 病人 AI               │        │  ① 音频事件表(烘焙产物)   │      │ AudioMixer │
  │  → 咳嗽 / 呻吟 cue        │        │     CueId → 素材 + 参数    │      │  Master    │
  ├──────────────────────────┤        │  ② AudioCueDto 解析       │─────▶│  ├ Music   │
  │ 37 病例系统               │──cue──▶│     (无 disease_id)       │      │  ├ Ambience│
  │  → 结案纸页 / 立案落纸 cue │        │  ③ 变体库混合(语声)      │      │  ├ Voice   │
  ├──────────────────────────┤        │  ④ 两层呼吸 + 接触噪声     │      │  ├ SFX     │
  │ 52 随机事件导演           │        │  ⑤ 白名单断言(无提示音)   │      │  ├ Stetho  │
  │  → 狗吠 / 马蹄 / 锣 cue    │        │  ⑥ 快照切换(行为驱动)     │      │  └ UICue   │
  ├──────────────────────────┤        └───────────┬───────────────┘      └────────────┘
  │ 8 诊断(通道 × 档位表)     │                    │
  │  → 听诊层 / 精度档 cue     │                    │ 空间化锚点
  ├──────────────────────────┤                    ▼
  │ 9 疾病模拟(呼吸语义)     │        ┌───────────────────────────┐
  │  → 呼吸连续变化语义       │        │ 表现态位置(整数格 → float)│
  └──────────────────────────┘        │  联机:ADR-001 第二 QoS     │
                                       │  精度档:本机技能(§五)     │
   ✗ 读 sim 真值            ────────▶  └───────────────────────────┘
   ✗ 写三流                 ────────▶   禁止(§一)
   ✗ disease_id 入本层      ────────▶   禁止(PresentationDtoGuard 递归)
```

### Key Interfaces

```csharp
// ① 呈现层契约(§二)。整数语义;不回写 sim;无 disease_id。
public readonly struct AudioCueDto
{
    public readonly CueId     Cue;
    public readonly byte      Intensity;   // 0–255
    public readonly byte      Tier;        // 0 低 / 1 中 / 2 高
    public readonly WorldPos  Cell;        // 整数格(ADR-015)
    public readonly PatientId Source;      // 可为 None
    public readonly bool      Looped;
}

// ② 44 对上游的唯一界面(只入不出,不持有状态)
public interface IAudioCueSink
{
    void Emit(in AudioCueDto cue);              // 一次性 cue(咳嗽 / 翻页 / 狗吠)
    AudioCueHandle BeginLoop(in AudioCueDto cue);// 循环 cue(呼吸层 / 环境)
    void EndLoop(in AudioCueHandle handle);     // 由唯一持有者(上游)负责收尾
    void SetTier(TierSource source);            // 一律本机玩家档;TierSource.Local(§五)
}

// ③ 音频事件表(作者态 → ADR-014 烘焙产物)。Fix 不在其中(全是呈现参数)。
//     assets/data/audio_events.json  →  *.cooked
//     { "cue": "BreathLayer_Urgent",
//       "assets": ["sfx_breath_base_loop_urgent.wav", "sfx_breath_adventitious_fine.wav"],
//       "tier_params": { "0": {...}, "1": {...}, "2": {...} },   // 通带 / 噪声底(§四 需求③)
//       "loop": true, "bus": "Stethoscope",
//       "whitelist_category": "ContinuousPhysiology",   // 内容侧(§六)
//       "trigger_source": "ContinuousPhysiology" }       // 时机侧(§六,2026-09-18 增)

// ④ 白名单断言(§六),EditMode + CI 门
[Test] void AudioCues_AllFromBehaviorWhitelist() { /* BLOCKING */ }
```

### Implementation Guidelines

1. **44 不订阅 sim 真值、不写三流。** 若你发现需要「病人当前的病种」才能选素材 ——
   **那是上游(8 / 13)的责任**:上游把语义解析成 cue id,44 只收 cue。
2. **`AudioCueDto` 不放 `disease_id`。** 递归 `PresentationDtoGuard` 会扫描它 —— 放进去会失败。
3. **呼吸层的档位差异改的是「通带 + 噪声底」,不是「音量」,更不是「静音」。**
   AC-44-01 会断言「档位 ⇒ 静音」路径不存在。
4. **不要加 sting。** 若你认为「玩家可能听不出来病人转危」—— 那正是设计意图
   (支柱一「判断为骨」);正确做法是**让呼吸的连续变化更可读**,不是加提示音。
5. **快照由行为驱动。** `StethoscopeFocus` 是玩家拿起听诊器触发的;不要用它来「提示该听了」。
6. **音频素材走 ADR-014 的烘焙管线**(§五 补注 —— 事件表烘,音频 clip 走 Addressables 流式)。
7. **VR 音频不在 P0 范围。** 若实现期遇到 VR 需求,登记到 **P1b**,不要顺手做。

## Alternatives Considered

### Alternative 1: 44 与 42 同构(只触发不持有状态)(本裁决)

- **Description**:44 是表现层,`AudioCueDto` 为其 DTO,不读 sim 真值、不写三流、`disease_id` 禁入。
- **Pros**:与 ADR-013 的既有纪律**一致**(无第二套边界哲学);`PresentationDtoGuard` 可复用;
  上游语义归属清晰(13 / 37 / 52 / 8 各发各的 cue);44 的实现面**最小**(只做 cue → 声音)。
- **Cons**:上游系统需要各写一小段「语义 → cue」的映射(但那是它们本来就拥有的知识)。
- **Estimated Effort**:基线。
- **Rejection Reason**:**采纳**。

### Alternative 2: 44 直接读 sim 状态(如 `IVitalsQuery`)

- **Description**:44 直接查病人体征,自行决定播什么音。
- **Pros**:44 不需要上游提供 cue。
- **Cons**:**越界** —— 44 会持有游戏状态(违反 ADR-013 §9 C3 的同构纪律);
  且它需要读 `disease_id` 才能选素材 ⇒ **直接撞 AC-37-15**;
  且「播什么音」是语义判断,应由拥有语义的系统做。
- **Estimated Effort**:看似更小,实则更大(引入边界违规 + 泄漏面)。
- **Rejection Reason**:**破坏呈现层边界,且打开 `disease_id` 泄漏通道**。

### Alternative 3: 无机械化断言,靠 code review 守「无提示音」

- **Description**:把铁律写进文档,靠评审人把关。
- **Pros**:零实现成本。
- **Cons**:三处 GDD 已各自写了铁律,**仍然**需要一份 ADR 来统一 —— 说明「写下来」不够。
  加一句 `PlayOneShot(ding)` 是**听不出是 bug** 的那类错误,且它在 P0 就会破坏支柱一。
- **Estimated Effort**:小(一份白名单 + 一个断言)。
- **Rejection Reason**:**BLOCKING 级铁律必须有构建门**,不能只有 prose。

### Alternative 4: 联机时分人分混(每人一套 DSP)—— **2026-09-18 改判:采纳**

- **Description**:为四名玩家各自跑一套听诊音层次。
- **Pros**:最忠于「每人按自己技能」的直觉读法 —— **且这正是它应该被采纳的原因**:
  技能成长是核心 SDT 需求(`game-concept.md:124`),冻结非主机玩家的听觉保真度
  与 44 的幻想「你的耳朵提醒你:你没练过听」直接冲突。
- **Cons**:~~结构上不可能~~ —— **原拒绝理由已证伪**。原文「单 `AudioListener` 每设备一条混音总线;
  四人各一套 = 每设备四条 DSP 链」是**误读** `audio.md:256-266`:
  单 Listener 只约束**空间化参考点**,不约束总线数;且联机 = **各自设备各自 mixer**,
  每人一套 DSP = **每台机器各一套**,不是一台机器四套。~~已由 2026-09-14 用户裁定②排除~~
  —— **该裁定已由 2026-09-18 裁定 D-A 重开并改判**。
- **Estimated Effort**:低(各客户端本地 `SetTier`,零同步)。
- **Rejection Reason**:**不拒绝 —— 现采纳**(见 §五 修订)。

### Alternative 5: 音频事件表不进烘焙管线(运行期读 JSON / ScriptableObject)

- **Description**:音频事件表用 ScriptableObject 或运行期 JSON。
- **Pros**:编辑期改素材更快。
- **Cons**:与 ADR-014 已 Accepted 的裁决冲突(玩家构建零 JSON 解析器);
  ScriptableObject 不能承载 `Fix`(本表**不承载** Fix,所以 ScriptableObject *技术上*可行 ——
  但会让「数据管线」出现第二条路径,与 ADR-014 的单一性相悖)。
- **Estimated Effort**:小。
- **Rejection Reason**:**单一数据管线** —— ADR-014 的纪律优于局部便利(且本表全是呈现参数,
  进烘焙管线零成本)。

## Consequences

### Positive

- **44 从「最名不副实」变成有权威件**:V-8.7 的四条硬需求**清偿为 AC**(OQ-8-4 结清)。
- **呈现层边界只有一套哲学**(42 与 44 同构),`PresentationDtoGuard` 覆盖音频。
- **「无提示音」从 prose 升为构建门**(AC-44-09 BLOCKING)—— 支柱一获得机械保障。
- **混音拓扑单一定义**,且**明写快照不得播报状态** —— 一处常见违规被预先封死。
- **联机音频在单 `AudioListener` 约束下自洽**(空间化参考点唯一),且不新增 QoS 通道;
  各机按本机技能档(2026-09-18 裁定 D-A)。
- **医学准确性获得 AC**(AC-44-08:啰音不是连续水声)。

### Negative

- **上游系统需各写一小段「语义 → cue」映射**(13 / 37 / 52 / 8)—— 但这是它们本就拥有的知识,
  且映射小到可以在一张表里看完。
- **VR 音频推到 P1b**,P0 的 VR 急救若需音频会缺件 —— 缓解:P0 不做 VR(`game-concept.md:682`)。
- **白名单机制需要维护**:新 cue 必须归类;缓解:归类是**编辑期**动作(烘焙门会拦)。

### Neutral

- **`audio-director` / `sound-designer` 的分工**随之清晰:前者定方向(与 `/art-bible` 同批),
  后者产 SFX spec / 事件表。
- **sound-bible 实例**成为 44 的 GDD 前置件(模板已存在,`:77 Mix Bus Structure` 可直接对齐本 ADR §三)。

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 上游把「播什么音」的语义判断推给 44(反向越界) | 中 | 中 | §一 / §Implementation 1 明写归属;44 的接口**只入 cue**,设计上无法反向查 |
| 白名单断言过严,拦下合法 cue | 中 | 低(编辑期暴露) | 白名单按**类别**而非逐个 cue;新类别走 ADR 修订(轻量) |
| 呼吸层的三档差异听不出(AC-44-02 失败) | 中 | 高(医学受众) | V-8.7 已定「通带 + 噪声底」为差异维度;须**实测**(Verification ②) |
| 变体库素材量爆炸 | 中 | 中 | 已登记为素材债(V-8.7「素材债」);变体库**远低于逐句重录** |
| ~~单 `AudioListener` 在联机下的实际混音行为~~(**2026-09-18 消解:前提证伪**) | — | — | 用户裁定 D-A:各机本地档,无需 spike |
| 音频触发进入 `<50 ms` 急救延迟路径 | 低 | 高 | 急救判定**本地即时出结果**(ADR-011);音频触发在其**之后**,不在预算内 |
| VR 音频在 P1b 才发现空间化难点 | 低 | 中 | §七 已列 P1b 交付切面(含挂点 / 快照 / 延迟三问) |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU(帧时间) | — | 待实测(音频 DSP 占用,含两层呼吸 + 变体交叉淡化) | 16.6 ms 平面 / 11.1 ms VR |
| Memory | — | 待实测(素材量:呼吸层 + 变体库 + 环境;Addressables 流式) | 待定 |
| Load Time | — | 无影响(音频走 Addressables 流式,非启动预载) | 待定 |
| Network | — | 无新增(空间化复用 ADR-001 §一之二 `IPositionalChannel`;无语音通道) | — |

> **本项目特有约束**:VR 硬性 90 fps(11.1 ms)—— 音频 DSP 占用须在 VR 模式另行实测。
> 音频**不在** `<50 ms` 急救延迟路径内(急救判定本地即时,音频在其后)。

## Migration Plan

**本项目尚无 44 的实现,故无迁移 —— 本 ADR 是「44 的第一次就做对」。**

1. **44 的 GDD 撰写** —— 以本 ADR 为权威件,按 V-8.7 的四条硬需求 + §三 的拓扑写。
   *验证:GDD 含四条硬需求的对应 AC。*
2. **sound-bible 实例** —— 与 `/art-bible` 同批定(`systems-index.md:645`);
   对齐 §三 的 Mix Bus Structure。*验证:总线清单与本 ADR 一致。*
3. **音频事件表** —— `assets/data/audio_events.json`(作者态)→ 烘焙。
   *验证:玩家构建无 JSON 解析器(ADR-014 继承验证)。*
4. **白名单断言** —— AC-44-09 进 EditMode + CI。*验证:故意加一条 sting cue 时断言失败。*
5. **上游 cue 映射** —— 13 / 37 / 52 / 8 各写一段语义 → cue 映射。*验证:每个 cue 可追溯到上游系统。*
6. **联机分混** —— 实现 `SetTier(TierSource.Local)`(各机本机玩家档)。*验证:AC-44-07 回归测试。*

**Rollback plan**:本裁决若需改判,主要面是 §六 的白名单粒度(可扩类)与 §五 的联机分混
(若引擎日后支持多监听器)。**§一 的呈现层边界不可回退** —— 回退即打开 `disease_id` 泄漏面(AC-37-15)。

## Validation Criteria

- [ ] **44 GDD 存在且以本 ADR 为权威件**;含四条硬需求(§四)的对应 AC(核心)
- [ ] **AC-44-01(BLOCKING)**:呼吸附加音层在三档下**始终非零**;断言无「档位 ⇒ 静音」路径
- [ ] **AC-44-02**:三档差异经听测确认可辨且**非静音型**
- [ ] **AC-44-03 / 04**:语声 = 变体库混合(非参数调制);交叉淡化无爆音 / 相位抵消
- [ ] **AC-44-05 / 06**:接触噪声底 + 信噪比参数暴露;`READ_FLOOR_MIN > 0` 在音频侧有实现
- [ ] **AC-44-07**:联机下**每台设备按本机玩家自己技能档**渲染听诊音(`SetTier(TierSource.Local)`);脉案字体量亦各自分叉
- [ ] **AC-44-08**:附加音层**非连续水声**(医学准确性)
- [ ] **AC-44-09(BLOCKING)**:白名单断言在 EditMode + CI 生效;故意加 sting cue 时失败(核心)
- [ ] **AC-44-10**:呼吸的连续变化可用,但无「突变音效」标记阈值跨越
- [ ] **AC-44-12 / 13**:关键音有视觉替代;mono 选项可用
- [ ] **`disease_id` 不进音频层**:`PresentationDtoGuard` 递归扫描覆盖 `AudioCueDto`
- [ ] **混音拓扑与快照清单单一出处**;快照**不得**用于状态播报(§三 断言)
- [ ] **音频事件表走烘焙管线**:玩家构建零 JSON 解析器
- [ ] **VR 音频不在 P0**;P1b 交付切面已登记(§七)
- [ ] ~~单 `AudioListener` 约束实测~~(Verification ①,**2026-09-18 消解**)

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/diagnosis-system.md` | 8 诊断与体征 | **V-8.7 四条硬需求**(`:1557-1586`):呼吸两层 / 语声变体库 / 接触噪声参数 / ~~联机取主机技能~~ → **各机本地档**(2026-09-18 裁定 D-A) | **逐条落 AC**(§四 AC-44-01…07)—— 清偿 V-8.7 / OQ-8-4 债务;第四条口径已改 |
| `design/gdd/diagnosis-system.md` | 8 诊断与体征 | **无提示音铁律延续**(`:1597`);禁 audio sting / ducking **报**状态 | §六 白名单断言(AC-44-09 BLOCKING);§三 快照不得播报状态 |
| `design/gdd/diagnosis-system.md` | 8 诊断与体征 | 听诊音必须准确(`:1593-1595`) | AC-44-08(非连续水声) |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情 | §三 音频需求(`:1111-1121`):呼吸层随病情变化 · 咳嗽呻吟由 13 触发 · 无提示音 · 昏迷/死亡听觉差异 | §一 归属(13 触发);§四 需求①;§六 AC-44-10(转危须玩家听出,不得突变音效) |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情 | AC-21(`:1235`):9 **不产出任何音频资源** | 44 是音频拥有者;9 只提供语义(§一) |
| `design/gdd/case-system.md` | 37 病例系统 | `:539-548`:结案纸页声(禁「成就达成」音效)· 同源触发无提示音 · `disease_id` 禁入呈现层 | §六 黑名单(禁素材切换报状态);§二 `disease_id` 禁入 + 守卫覆盖 |
| `design/gdd/random-events.md` | 52 随机事件导演 | `:977,1058`:预告线索音频侧(狗吠 / 马蹄 / 锣声 / 铃声 / 钟声) | §六 白名单「玩家可感知的世界声」;52 发 cue,44 播音 |
| `design/gdd/systems-index.md` | 44 音频系统 | `:254` 承担状态反馈职责;`:513` 最名不副实 | **本 ADR 即 44 的权威件**;§四 / §六 定义「状态反馈」的合法形态 |
| `design/gdd/game-concept.md` | 全局 | `:571` 音频需求「中等偏重:京戏、环境、把脉音效」;无血条反馈三载体 | §三 Music / Ambience / Stethoscope 总线对应;反馈不可用提示音(§六) |
| `design/gdd/item-database.md` | 21a 物品库 | `:909` 全部音频归 20 / 42 / 44 拥有 | 44 拥有音频播放;条目的音频字段由 44 呈现 |

> **Foundational + Feature 混合** —— 本 ADR 既是 44 的权威件(Feature),
> 也确立呈现层的第二条同构边界(Foundational)。
> **Enables**:44 的 GDD 与实现;8 的音频侧 AC;13 / 37 / 52 的音频落点;
> `TR-concept-*` 中依赖「状态反馈」的条目。

## Related

- **ADR-013**(`adr-013-skeuomorphic-ui-framework.md`)—— **42 只渲染不持有状态**;
  本 ADR 把 44 定为**同构层**,并复用 `PresentationDtoGuard`(AC-37-15)。
- **ADR-014**(`adr-014-data-pipeline-and-json-parser.md`)—— **音频事件表走烘焙管线**;
  素材本体走 Addressables 流式(与 `data-core` 数据分组区分)。
- **ADR-015**(`adr-015-world-geometry-fixed-world-lattice.md`)—— 空间化锚点为**整数格**;
  **禁从视觉坐标反推逻辑格**。
- **ADR-016**(`adr-016-ai-architecture.md` §六)—— 13 拥有「咳嗽呻吟 → 音频」的表现映射,
  属表现层,不经 sim;本 ADR 是其下游。
- **ADR-001**(`adr-001-networking-pipe-abstraction.md`)—— 远端实体空间化复用 **§一之二 `IPositionalChannel`**
  (per-`ActorId` latest-value 表,2026-09-18 自 `IReplayPipe` 拆出);44 只读声源锚点、**不新增通道**、
  **cue 非复制**;无语音通道。
- **ADR-011**(`adr-011-input-architecture.md`)—— 急救判定的 `<50 ms` 路径;**音频触发不在其内**。
- **ADR-017**(`adr-017-dots-decision.md`)—— 音频是表现层,不受门 A 约束,亦不在 DOTS 复评门范围内。
- `docs/engine-reference/unity/modules/audio.md` —— 仓库自有的音频权威件
  (AudioMixer 快照 / ducking / 3D 空间化 / **Single Listener Rule**)。
- `.claude/docs/templates/sound-bible.md` —— 44 的 GDD 前置件模板
  (Mix Bus Structure / 空间音频规则 / 无障碍规则)。
