# 待评审 GDD 三份 · 逐份新会话提示词便签

> **建立日期**:2026-09-18 · **状态**:待执行(每份单独会话)
> **上游裁定**:2026-09-18 用户裁定 —— 三份全纳入 · **逐份新会话跑**(评审者独立于撰写上下文)
> **权威进度**:`production/session-state/active.md` §当前任务·第三步
> **本文件性质**:会话间便签(ephemeral)。执行完毕可删。

---

## 纪律(每份都适用)

1. **一会话一份**。`/design-review` 内部为每份文档 spawn 一簇**独立子代理**(自有上下文窗口)——
   该子代理的上下文即等于「新会话」,满足「评审者独立于撰写上下文」要求。
   但**同一主会话连跑两份**会让第二份继承第一份的上下文中枢 ⇒ **不要**。
2. **跑前** `git status` 确认工作树干净(或已提交),免得评审结论与被污染的中间态混淆。
3. **跑后**回到一份(可以是新会话)做主会话收尾记账,**不要**在评审会话里顺手改 GDD
   (评审会话按 skill 设计是 read-only 到 Phase 4;改动交回给你裁定后另做)。
4. 13 / 44 是 **P0 系统** ⇒ 首次评审须**新建** `design/gdd/reviews/[name]-review-log.md`。
5. `concept-benchmark.md` 是 **Draft 非系统 GDD** ⇒ 不套 8 必备节的机制可测性标尺。

---

## 第 1 份 · `patient-ai.md`(系统 13 病人 AI)

**命令**

```
/design-review design/gdd/patient-ai.md --depth full
```

**为什么 full**:13 是 P0 Feature 层,横跨 AI 行为 / 官能表现 / 音频触发 / 与 37 的契约接缝,
`full` 的专家域表会命中 `ai-programmer` + `game-designer` + `audio-director`(呻吟咳嗽触发)+
`network-programmer`(主机唯一判定)+ engine specialist。体量 704 行 / 11 节 / 51 小节,够吃 full。

**权威件**:`docs/architecture/adr-016-ai-architecture.md`(§一 分层与三源不变量 · §三 感知读粗粒度整数格 · §六 只读通道与在场视图 · §九 冻结/LOD)。

**重点靶子**

- **决策归属**:13 的决策 = **派生态住边界层(呈现侧)**,不写三流、不进 sim 程序集 ——
  与 27(决策进 sim)刻意分叉。评审须核对这条层次是否自洽、有无第四来源(表现态位置 / 墙钟)。
- **13 ↔ 37 接缝**:13 出只读视图 `IPresentPatients`(`PatientId` + 整数格 `WorldPos` + 粗状态枚举),
  37 读它立案;**13 不引用 37**(单向无环)。核这条是否真的无环、消费者侧是否被 37 的现状兑现。
- **感知禁读表现态位置**(ADR-016 §三):核有无漏网的表现态读。
- **反转义务**:`O-27-*` / `O-1-*` 里 13 作为「对侧」欠的回填(25/27 评审时记的),核是否已结。
- **旁证**:25 评审的 R-4 曾要求「`patient-ai.md:459` 更新 1 为 ✅」—— 核该处状态是否已在文件内对齐。

**已知外部事实(供评审者参考,勿当缺陷误报)**:13 的 TR 已回溯追加 `TR-patient-001…024`;`V2` 涟漪(可订阅 onset 的表现映射侧)已落盘于本文件。

---

## 第 2 份 · `audio-system.md`(系统 44 音频)

**命令**

```
/design-review design/gdd/audio-system.md --depth full
```

**为什么 full**:P0 Foundation,但域面宽(音频实现 / 医学准确性 / 白名单守门 / 手柄与 VR 边界 /
联机混音精度);`full` 会命中 `audio-director` + `qa-lead`(27 条 AC 的白名单断言)+
`game-designer` + engine specialist。581 行 / 11 节 / 27 AC。

**权威件**:`docs/architecture/adr-018-audio-architecture.md`(与 42 同构 —— 只触发/只渲染,永不持有游戏状态)。
**姊妹件**:`docs/architecture/adr-013`(拟物 UI)· `docs/architecture/adr-020 §七`(`AudioListener` 单挂点)。

**重点靶子**

- **无提示音铁律的机械化**:AC-44-09(BLOCKING)= 白名单断言(触发源 ∈ 行为反馈白名单;
  禁 sting / jingle / ducking / 素材切换**报**状态)。核该断言是否**真的可执行**(不是同义反复)。
- **V-8.7 四条硬需求是否逐条落 AC**(AC-44-01…08):① 呼吸两层「通带 + 噪声底」差异**禁静音路径**
  ② 语声**变体库混合**(非参数调制)③ 接触噪声 / 信噪比参数 ④ 联机取主机技能。
- **AC-44-08 医学准确性**:细湿啰音**非连续水声** —— 医学受众第一个出戏点,核规格有无医学错误。
- **`AudioCueDto` 不含 `disease_id`**,受 `PresentationDtoGuard` **递归**扫描覆盖(AC-37-15 一致性)。
- **混音拓扑单一定义**(7 条总线)+ **明写快照不得用于状态播报**。
- **联机**:单 `AudioListener` ⇒ 精度统一取主机技能;不做语音通道。

**已知外部事实(勿误报)**:27 评审的 V4/V7 涟漪已落本文件(乐层 = 白名单第 5 类;
`adr-018` §六 G1 禁帧对齐 / G2 去标注盲测 / G3 本地触发);25 的音频触发订正注已落。

---

## 第 3 份 · `concept-benchmark.md`(竞品对标 · Draft)

**命令**

```
/design-review design/gdd/concept-benchmark.md --depth lean
```

**为什么 lean 不 full**:这是 **Draft 战略对标文档,不是系统 GDD** ——
无机制可测性标尺,`full` 的专家对抗(边界值 / AC 可测性 / 经济)多半会打空靶。
`lean`(全阶段、不委托)足够审「定位一致性 + 背离项风险记账」。

**权威件**:`design/gdd/game-concept.md`(支柱 / 核心动词 / 目标受众)。

**重点靶子**

- **对标断言 vs 支柱一致**:三大对标(英灵神殿框架 / 我的世界创造思维 / 核心动词「杀→判」反转)
  是否与 `game-concept.md` 的支柱一二三四五 一致,有无自相矛盾。
- **「背离项」是否诚实记账**:文档自称标出「我们刻意背离的地方(高风险)」——
  核这些背离项是否都有对应的**风险登记**(如「核心动词反转 = 最大风险」是否真被下游 GDD / systems-index 承接)。
- **8 必备节是否只是形式**:该文件有 `## Formulas` / `## Acceptance Criteria` 等节 ——
  核这些节是**实质内容**还是为套模板而填的占位(若是后者,建议降格为 research 文档而非 GDD 目录内文档)。

**注意**:它不在 systems-index 的系统枚举里(非 54 项之一)。评审结论**不必然**建系统 review-log。

---

## 每份跑完后的收尾记账清单(回主会话做)

- [ ] 用户裁定:改 / 免二轮 / 保持
- [ ] GDD 文件头 Status 回填
- [ ] `systems-index.md`:§2 对应行(row 49 / 80)+ §10 已有 GDD 行 + §11 队列行
- [ ] `design/gdd/reviews/[name]-review-log.md`(13 / 44 新建;benchmark 可选)
- [ ] TR:`tr-registry.yaml` + `traceability-index.md`(若新增/翻转 TR)
- [ ] 交叉注:被本份 GDD 触达的邻接 GDD 依赖表双向化

---

## 当前状态速查(跑前不必重读全索引)

| 系统 | 状态 | 权威件 | 体量 |
|------|------|--------|------|
| 13 病人 AI | ✅ **Approved**(2026-09-18 首轮 → 9 B + 8 R 全落盘 → 免二轮) | ADR-016 | 11 节 · 1192 行 |
| 44 音频 | **Designed · 待首轮** ← **下一份** | ADR-018 | 11 节 · 27 AC · 581 行 |
| concept-benchmark | Draft(非系统 GDD) | game-concept.md | 8 节 · 176 行 |
