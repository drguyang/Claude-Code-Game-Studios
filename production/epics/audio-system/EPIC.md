# Epic: 音频系统

> **Layer**: Foundation
> **GDD**: design/gdd/audio-system.md
> **Architecture Module**: L5 Presentation(PRES)+ L3 契约程序集(`Sim.Contracts`: `AudioCueDto` / `IAudioCueSink`)
> **Status**: **001–015 全 `Complete`** · **016 = Blocked 🔴**(BLOCKED-BY:**素材产出批次**)· **⚠️ 本 epic 尚未收口**
> (015 = 5 个被引 `.wav` 的真实内容落地 —— ✅ **2026-10-17 已落**;016 = 余 7 项源音覆盖 —— 🔴 **阻塞于素材**)
> **收口口径(2026-10-17 用户裁定)**:**待素材接入、016 收口后,再行收口本 epic** —— 在此之前
> 不得以「001–015 全 Complete」推定为 epic 已闭。
>
> **⚠️ 状态沿革(2026-10-16 · 保留不改写)**:007–012 六条曾于 2026-10-16 由 `Complete` 撤回
> `In Review` —— S13-9 假绿补评(`production/qa/evidence/review-audio-epic-fake-green-2026-10-16.md`)
> 判定其 AC 语义层未被验证(测试自造断言顶替)。六条的生产实现已于 **sprint-14**(S14-1…S14-6)全数落地、
> 经**证伪轮**(停用生产码守卫 ⇒ 断言转红)全红,并由 **S14-7 复评批**转回 `Complete`。
> 原「**Complete ✅ 2026-09-28**(14/14 stories)」为 2026-09-28 的历史陈述。
> **2026-10-17 沿革**:story-015 收口(`Complete`)后,epic 头部原「**Complete ✅ 2026-10-16**(14/14 ·
> 001–014 全 `Complete`)」的表述**已改为「001–015 全 `Complete` · 016 Blocked · epic 尚未收口」** ——
> 理由:016 的 `Ready` 名不副实(其 E2/E3/E4 结构上依赖素材产出批次),`Status` 已改 `Blocked`;
> epic 的最终收口推迟至素材接入、016 收口之后(用户 2026-10-17 裁定)。
>
> ### ⚠️ 范围边界声明(2026-10-03 补 · 实测)
>
> **本条防的是一个已被实测证实的误读:「烘焙管线 Complete」≠「素材本体已接入」。**
> 本 epic 的 14 条 story **全部真做真测**,但 story 010 的 AC-44-D6(素材缺失⇒编辑期门拒绝)
> 的 11 测**用的是夹具,不是真 `assets/audio/` 里的文件**。
>
> **实测(2026-10-03)**:
> - `assets/audio/` = **空目录**
> - 全库 `.wav` 文件数(非 `.git`)= **0**
> - 但 `assets/data/audio_events.json` **引用了 5 个具体 `.wav` 文件名**(逐个均不存在)
>
> ⇒ **现状 = 管线与门已建,素材未落地。** 同型先例 = `skeuomorphic-ui` 的 16 张 `*-final.png`
> (GUID 全库引用 = 0)。两者失败模式一致:**引用存在、资产不存在,而它无任何告警**。
>
> **⚠️ 2026-10-16 更新(承上,实测已变)**:S14-6 落 **5 个占位静音 wav** 至
> `unity/Assets/Audio/`(44 B RIFF/WAVE 头 + 2000 B 全零 PCM = 1000 样本 ≈ 22.7 ms;
> 内容批后替换)⇒ **`assets/data/audio_events.json` 的 5 个 bare 名不再指向不存在之物**
> (物理根由 `assets/audio/` 逻辑路径订正为 `unity/Assets/Audio/`,ADR-018 §五 已加补注)。
> **真实素材内容需求已立册**:`docs/tech-debt-register.md` §「2026-10-16 · 44 音频素材内容需求」
> (逐族声学判据 + 系统级要求 4 条 + 触发条件表)。**Import Settings 定稿仍归 content 批(`OQ-44-2`)。**
>
> **✅ 2026-10-03 已立补件**:该缺口由 **`story-015 音频资产接入`**(~~Ready ⬜~~ **✅ Complete 2026-10-17**)承接 ——
> AC = 被引 `.wav` 真实存在 / 删任一 ⇒ 构建期硬失败(真素材复验,非夹具)/
> 5 个 `.wav` 列入 Addressables 素材组 / 双向差集归零。
> ⚠️ **015 Ready ≠ 缺口已闭** —— 本行登记的是「已有承接件」,非「已完成」。
>
> ✅ **2026-10-17 更新:缺口已闭** —— story-015 经 `/story-done` 收口为 `Complete`(**4/4 AC 全勾**):
> 5 个被引 `.wav` 已由**真内容**替换 S14-6 的占位静音(2044 B → 110–2056 KB),
> **存在性门首次在真素材上跑通**;删任一被引 `.wav` ⇒ **构建期硬失败**(证伪轮 15 红);
> 回归 `unity/Logs/s15-final.xml` = `total 395 / passed 395 / failed 0`。
> 证据原件 = `production/qa/evidence/impl-s15-asset-content-2026-10-17.md`。
> ⚠️ **上句原文保留**(承本 epic「保留不改写」体例)—— 它记的是 2026-10-03 → 10-17 之间的真实状态。
>
> ### ⚠️ 范围边界声明 二(2026-10-04 补 · 实测 · 对上述缺口的**二次分件**)
>
> **015 的界划在「`audio_events.json` 已引用的 5 个 `.wav`」上,不是划在「R-2 归属表的 9 项」上**
> ⇒ 015 只覆盖了 9 项中的 **4 项**(呼吸两层 / 语声合计 4 个 wav = 2 项资产)。
> **余 7 项**(诊脉接触音 · 听诊器接触音 · 纸面物理声 · 脚步声 · 药柜/器物声 · 世界声 · 馆内环境音)
> **在 015 的界外,且其中三项连事件表条目都没有**(双重缺口:未引用 + 未产出)。
>
> > ⚠️ **对上游计数的订正(登记不隐藏)**:制作人报告记该缺口为 **4 项**,实测**是 7 项** ——
> > 漏数了 [SFX #1] 诊脉接触音 / [SFX #2] 听诊器接触音 / [Ambient #1] 医馆室内环境音
> > (它按「事件表未引用的 SFX」计数,而这三项连被引用都没有)。
>
> **✅ 2026-10-04 已立第二补件**:`story-016 音频资产接入(余 7 项源音覆盖 · R-2 归属表 9 项逐项对账)`
> (~~Ready ⬜~~ **🔴 Blocked 2026-10-17** —— BLOCKED-BY:**素材产出批次**,余 7 项 `.wav` 未交付)。
> **015 与 016 刻意分件,不合并** —— 015 = 「已引用但不存在」的修复;016 = 「本应有但连引用都没有」的补齐;
> **两者的门不同**(015 查被引 `.wav` 存在性;016 查 R-2 九项逐项有事件表条目 + 素材文件)。
> ⚠️ **合并会让两种失效模式再次混同**(本 epic 的混计已发生过一次 —— 见上方声明一)。
>
> ### ⚠️ 范围边界声明 三(2026-10-16 补 · 实测 · 由 11 走查上溯)
>
> **前两条声明都只覆盖「素材不在」;本条防的是第三种失效模式 ——「素材在不在都放不出声」:
> 整条运行期音频链未接通。**
>
> **实测(2026-10-16 · sprint-13 S13-7 11 处方用药人工听测)**:
> - 进 Play 后场景**完全无任何声音**(呼吸 / 咳嗽 / 环境音 / 音乐皆无)
> - `Boot.unity` 内 `AudioSource` 数 = **0**(仅 1 个 `AudioListener`);全 `unity/Assets/Scenes/`
>   目录 grep `AudioSource` **零命中**
> - 全案 **零 `IAudioCueSink` 运行期实现** —— 唯三实现类(`RecordingCueSink` ×2 / `TierRecordingSink`)
>   **全部是 EditMode 测试夹具**,无一处接真 `AudioSource`
> - `assets/audio/` = 空目录(承声明一)· `assets/data/audio_events.json` 仅 **5 行**
>   (Voice / Stethoscope×2 / Ambience / Music),**零处方条目**
>
> ⇒ **全案任何音频类 AC 的「听测半边」在素材与链路接通前均不可判。**
> 首个撞上的是 **11 的 `AC-11-21`**(零状态播报音),同型含本 epic 的 **`AC-44-09`**
> (无提示音铁律的白名单听测部分)。
> ⚠️ **不可判 ≠ ✅** —— 当前「无音」是**偶然**(链未通),**不是设计保证**;
> 判「✅」等于声称「设计正确地实现了静默」,而链没通证明不了任何设计意图。
> ⚠️ **与本表 story-013 不冲突**(防混计,同声明一同款纪律):story-013 的 `Complete`
> **只覆盖 AC-44-17 的文档判据**(AC 存在且挂点正确 + 证据文档登记),其**听测本身**在
> story-013 自己的 Completion Notes 里就登记为「**待执行**」(证据文档
> `main-channel-listen-evidence.md` 状态 = 待执行)。⇒ **两者说的是同一件事** ——
> 「文档面已就位,听测面未跑」;本声明把该口径从 story-013 **推广到全案音频 AC**。
> ⚠️ **本声明只登记事实,是否在本 epic 正式立案 / 是否需在素材接入时专门复验处方音效
> 确不在白名单内,待音频 lead 签核裁定**(签核件 §3 处置问 3/4,
> `production/qa/evidence/prescription-medication/story-005-ac11-12-13-21-signoff.md`)。
> **本条不声称已复核其余音频 AC 的可判性** —— 只覆盖走查实测命中的「链路未通」这一事实。
>
> **Stories**: 16 created(13 on 2026-09-26 `/create-stories` + **014 on 2026-09-27 用户裁定**:tier 滤波载体缺口独立成 story + **015 on 2026-10-03** 素材本体缺口 + **016 on 2026-10-04** R-2 归属闭合缺口)

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | 装配边界与 DTO 护栏 | Integration | **Complete ✅ 2026-09-26**(4/4 · 24 测 · 审查 7 修复验 568/568) | ADR-018(+025/017 引用) |
| 002 | 音频事件表 schema 与白名单门(BLOCKING) | Logic | **Complete ✅ 2026-09-26**(6/6 · 62 测 · 双评审 5 REC 修复验 647/647) | ADR-014(+018) |
| 003 | 混音拓扑与快照纪律 | Integration | **Complete ✅ 2026-09-26**(4/4 · 28 测 · 双评审 3 BLOCKING 修复验 723/723) | ADR-018(+020) |
| 004 | 听诊呼吸两层与精度档 | Logic | **Complete ✅ 2026-09-26**(3/4 · 37 测 · 双评审 4 BLOCKING 修复验 822/822) | ADR-018 |
| 005 | SNR 分析域与噪声下界 | Logic | **Complete ✅ 2026-09-26**(4/4 · 20 测 · 双评审 B1+REC2 修复验 822/822) | ADR-018 |
| 006 | 语声变体库与 Intensity 分桶 | Logic | **Complete ✅ 2026-09-26**(4/4 · 21 测 · 双评审 B1-B3 修复验 986/986) | ADR-018 |
| 007 | 联机分叉与远端派生(静态半) | Integration | **Complete ✅ 2026-10-16**(3/3 · **34 测** · S14-2 真生产断言 + 证伪 7 例 · 回归 3538/3492/0/46) | ADR-001(+018)·运行面 BLOCKED-BY-45 |
| 008 | 空间化与世界语境呼吸 | Integration | **Complete ✅ 2026-10-16**(5/5 · **28 测** · S14-1 真生产断言 + 证伪 8 例 · 回归 3523/3477/0/46) | ADR-028(+015/018) |
| 009 | 声源生命周期与贴耳交接 | Logic | **Complete ✅ 2026-10-16**(4/4 · **16 测** · S14-3 真生产断言 + 证伪 8 例 · 回归 3542/3496/0/46) | ADR-001 §一之三裁决二(+028) |
| 010 | 素材管线与事件表烘焙门 | Integration | **Complete ✅ 2026-10-16**(4/4 · **52 测** · S14-6 真生产断言 + 证伪 11 例 · 回归 3613/3568/0/45) | ADR-014(+018) |
| 011 | 设置暴露面与归零机制 | Config/Data | **Complete ✅ 2026-10-16**(5/5 · **31 测** · S14-5 真生产断言 + 证伪 12 例 · 回归 3572/3526/0/46) | ADR-018(+013/014) |
| 012 | 乐层护栏与性能/VR 切面 | Logic | **Complete ✅ 2026-10-16**(3/4 · **22 测** · S14-4 真生产断言 + 证伪 11 例 · 回归 3552/3506/0/46 · AC-44-11 随 VR P1b) | ADR-018(+020) |
| 013 | 主通道听测验收 | Visual/Feel | **Complete ✅ 2026-09-28**(1/1 · 7 测文档判据 + 证据文档 · 双评审 REC-1/R1-R3 修复验 1173/1175) | ADR-018 |
| 014 | tier 滤波载体与 mixer 暴露参数 | Integration | **Complete ✅ 2026-09-27**(5/5 · 11 测 · 审查 7 修复验 872/872) | ADR-018 §四 需求③ |
| 015 | 音频资产接入(5 个被引 `.wav` 落地 · 存在性门真素材复验 · 缺失⇒构建期硬失败) | Integration | **Complete ✅ 2026-10-17**(4/4 · **52 测** · 证伪 3 例全红 · 回归 395/395/0 · **代码评审 APPROVED WITH SUGGESTIONS** · 6 条 ADVISORY 见 story §Completion Notes) | ADR-018 §五(+014) |
| 016 | 音频资产接入(余 **7** 项源音覆盖 · R-2 归属表 9 项逐项对账 · 双向差集归零) | Integration | **Blocked 🔴**(BLOCKED-BY:**素材产出批次** —— 余 7 项 `.wav` 未交付;承下方 §范围边界声明 **二**) | ADR-018 §五(+014) |

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
GDD Approved(**现行 = 2026-09-25 二轮**:9 专家 full 独立复核判 MAJOR(14 阻断)→ 批次 0 七拍板 + 批次 1/2 同日全修 → 用户裁定接受、**免三轮**;首轮 2026-09-18 MAJOR 当日全修、免二轮为前史);TR 14 = 13 covered +
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
- All acceptance criteria from `design/gdd/audio-system.md` are verified(**36 条 AC**;2026-09-25 二轮 32→36)
- All Logic and Integration stories have passing test files in `tests/`
- All Visual/Feel and UI stories have evidence docs with sign-off in `production/qa/evidence/`

## Next Step

**Epic 全部 14 个 story 已完成**。下一步 = 推进其他 epic 或处理跨系统待办(45 联机夹具 / ADR-023 spikes / 七屏走查)。
