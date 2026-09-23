# Epic: 音频系统

> **Layer**: Foundation
> **GDD**: design/gdd/audio-system.md
> **Architecture Module**: L5 Presentation(PRES)+ L3 契约程序集(`Sim.Contracts`: `AudioCueDto` / `IAudioCueSink`)
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories audio-system`

## Overview

音频系统(44)是表现层的一条「语义 cue → 声音」通道,与 42 拟物 UI 同构 ——
只触发 / 只渲染,永不持有游戏状态(ADR-018)。上游系统在表现层发出语义 cue
(13 的咳嗽呻吟 · 37 的结案纸页 · 8 的听诊层 / 9 的呼吸语义),44 只把 cue 变成声音:
经 `AudioCueDto`(无 `disease_id`,整数语义,受 `PresentationDtoGuard` 递归扫描)进
`IAudioCueSink`,由七条总线(`Master / Music / Ambience / Voice / SFX / Stethoscope / UICue`)
与行为驱动的快照清单渲染;音频事件表走 ADR-014 烘焙管线(玩家构建零 JSON 解析器)。
玩家侧幻想 = 「先听见后看见的那个人」:隔壁房间听见病人呼吸变坏、听诊器里听见细湿啰音,
而系统**永不叮一声**告诉你「他转危了」—— 「无提示音」铁律由白名单断言机械化
(AC-44-09 BLOCKING)。44 不订阅 sim 真值、不写三流、不进 sim 程序集。
GDD Approved(2026-09-18,首轮 MAJOR REVISION 当日全修,免二轮);TR 14 = 13 covered +
1 partial(008 挂 OQ-44-8 发布者实现随 45 走 P1b,禁借绿)+ 0 gap;
GDD Requirements Covered by ADRs = 14 / 14;Untraced = None。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-018: 音频架构 | 权威件 —— 44 与 42 同构(只触发/只渲染);`AudioCueDto` 整数语义无 `disease_id`;七总线混音拓扑 + 快照清单(禁状态播报);无提示音铁律机械化(AC-44-09);8 的四条硬需求落 AC-44-01…07;联机分叉的只有音 | MEDIUM(AudioMixer 6.3 post-cutoff) |
| ADR-028: 世界语境声源归属 | 44 持有运行期表现层声源池唯一所有权;零场景预摆(`AudioSource` 非 Boot 场景内容 = 构建失败);位置真源 = 只读 `AudioCueDto.Cell` / 远端 `IPositionalChannel`;总线 = Ambience 世界语境子通道;生命周期 cue 驱动,存在性不进三流 | LOW |
| ADR-014: 数据管线与 JSON 解析器 | 音频事件表走两阶段烘焙管线:玩家构建零 JSON 解析器、零 `FixParse`;`ConfigVersion` 内容哈希派生 | MEDIUM(Addressables 6.2+ 抛异常 post-cutoff) |
| ADR-020: 玩家控制器与相机 | §七 `AudioListener` 单挂点:平面 = 主相机,VR = 头显(结清 ADR-018 §七 留白);相机只读不持状态 | LOW |
| ADR-023: 渲染与场景加载 | 三场景制 Boot 常驻含相机 + `AudioListener` + tick driver,全程不卸载;零 gameplay 对象铁律 | HIGH(RenderGraph / Addressables post-cutoff) |
| ADR-001: 联机 pipe 抽象 | 远端空间化复用 `IPositionalChannel`(per-`ActorId` latest-value 表,不新增通道);§一之三 裁决二:cue 非复制 ⇒ `EndLoop` 不作网络消息,44 自评兜底升格架构义务(TR-audio-012 结清) | HIGH(NGO / Fusion 库面 post-cutoff) |

**Engine Risk**: **HIGH**(ADR-023 / ADR-001 并列最高)。ADR-023 的 RenderGraph 迁移与
Addressables 6.2+ 抛异常行为、ADR-001 的 NGO / Fusion 库面均 post-cutoff,须 spike;
ADR-018 的 AudioMixer 6.3 mixer improvements 为 MEDIUM 悬置(`OQ-44-5` 实现期实测)。
接口层(`AudioCueDto` / `IAudioCueSink` / 事件表 schema)为纯 C# 契约,不受引擎版本影响。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-audio-*`,Manifest Version 2026-09-21)。
> 计数(2026-09-23 实测):**14 条 = 13 covered + 1 partial + 0 gap + 0 no-adr-by-design**;
> **GDD Requirements Covered by ADRs = 14 / 14**(covered+partial);**Untraced = None**。
> 1 条 partial 为**禁借绿**登记:008 锚点发布者**契约已定**(ADR-001 §一之二 发布者表),
> 但发布者**实现随 45 走 P1b**、`OQ-44-8` 接口细节未裁 —— 不阻塞 epic 建置,story 按
> BLOCKED-BY 处理。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-audio-001 | 44 与 42 同构:只触发/只渲染,永不持有游戏状态;输入 = `AudioCueDto`(整数语义) | ADR-018 ✅ |
| TR-audio-002 | 无提示音铁律机械化:触发源白名单断言 + 双负向夹具(sting/ducking 构造即拒) | ADR-018 ✅ |
| TR-audio-003 | 混音拓扑单一出处:Master/Music/Ambience/Voice/SFX/Stethoscope/UICue 七总线 + 快照清单 | ADR-018 ✅ |
| TR-audio-004 | `DialogueFocus` 为非拟物例外通道(限 UI cue 总线,不占世界声像) | ADR-018 ✅ |
| TR-audio-005 | 听诊呼吸两层 = 通带+噪声底(禁静音路径);细湿啰音非连续水声;与体征相位锁定 | ADR-018 ✅ |
| TR-audio-006 | 语声变体库混合(禁参数调制装多样)+ 50ms ramp 防爆音 | ADR-018 ✅ |
| TR-audio-007 | 听感精度档 = 各设备按本机技能档(单机本地/联机各自分叉,分叉的只有音) | ADR-018 ✅ |
| TR-audio-008 | 远端空间化经 `IPositionalChannel` 复用 ADR-001 第二 QoS 位置(不新增通道) | ADR-001 + ADR-018 ⚠️ partial(OQ-44-8 发布者实现随 45 走 P1b,禁借绿) |
| TR-audio-009 | cue 载荷不复制三源事实(音频只读 DTO 派生,不镜像 sim 量) | ADR-018 ✅ |
| TR-audio-010 | 音频事件表走 ADR-014 烘焙管线:玩家构建零 JSON 解析器 | ADR-014 + ADR-018 ✅ |
| TR-audio-011 | 世界语境呼吸:听诊主通道的呼吸声源于世界(非 UI 层常驻音) | ADR-028 ✅ |
| TR-audio-012 | `EndLoop` 兜底:事件终止信号丢失时的停止路径 | ADR-001 + ADR-018 ✅ |
| TR-audio-013 | `AudioListener` 单挂点:平面=主相机,VR=头显,常驻归 Boot 场景 | ADR-020 + ADR-023 ✅ |
| TR-audio-014 | 战斗乐层 G1–G3:层间切换经混音参数,禁 sting/层素材报状态 | ADR-018 ✅ |

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`
- All acceptance criteria from `design/gdd/audio-system.md` are verified(32 条 AC)
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

Run `/create-stories audio-system` to break this epic into implementable stories.
