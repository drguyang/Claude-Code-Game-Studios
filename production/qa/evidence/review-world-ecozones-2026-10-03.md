# 代码评审 —— world-ecozones(系统 6b 生态区与 POI)

> **评审对象**:epic `world-ecozones`(6b)· HEAD 工作树 · 引擎 Unity 6000.3.24f1
> **评审日期**:2026-10-03
> **评审类型**:独立代码评审(逐条 BLOCKING 对账)
> **评审员**:独立评审员(未参与本 epic 任何实现或前次对账)

## 🔍 独立复核批注(2026-10-03 · 由主会话复核,非 agent 自述)

复核人**逐条实测**了本报告的关键结论:

| 报告条目 | 复核结果 |
|---|---|
| **N1** host gate 返回 `PoiNotFound` 与「POI 不存在」混同 | ✅ **成立** —— `PoiStateMachine.cs:100`(gate)与 `:103`(真·不存在)**同码** ⇒ 调用方不可区分 |
| B1 已修 | ✅ 成立(`:123-124` 经 `IPayloadEncoder`) |

⚠️ 报告未跑 Unity 测试(自陈),其 B2 的「形式已闭」仅据静态检查 —— **转 Complete 前须以当前 HEAD 重跑**。

## ⚠️ 评审时点声明(必读)

**本报告评的是「补做时点」的代码,不追认原判定。**

本 epic 曾于 2026-10-01 有一次双代理评审(结论 REQUEST_CHANGES,4 BLOCKING),
但**该评审报告原件从未落盘、已不可得**(`git log --all --diff-filter=D` 对 `review|评审` 零命中,
即从未写过;详见 commit `7fc7ec9` 的三条独立证据)。因此:

1. 本报告**不是「补录」**原报告,而是**新评一次**;
2. 下文的「原判定」= 仅据 **commit message 摘要**(`bb477e6`)与后继对账件
   (`reconciliation-world-ecozones-2026-10-02.md`)转述的**4 条 BLOCKING 命题**,
   **不代表**当时评审员的具体措辞或上下文;
3. 本报告**逐条独立复核**,不采信任何 commit message / story Completion Notes 的自述;
4. 凡**未亲自验证**的条目,一律显式标 `未验证`;仅读文档未读源码的标 `仅文档,未验证源码`。

**验证基线**:本报告全部结论基于 2026-10-03 工作树的**实际文件内容**(grep / sed / 读源码),
每条附可复现命令。**未运行 Unity 测试**(本评审为静态源码复核;见 §转 Complete 前置 P4)。

---

## 结论摘要表

| # | 命题 | 判决 | 依据(简述) |
|---|------|------|------------|
| B1 | `PoiStateChanged` 未走 `Sim.Codec` | ✅ **已修**(2026-10-02 story-006) | 写侧经 `IPayloadEncoder.Encode`;手搓 `new PayloadRef(` 代码零命中;TODO 已消失 |
| B2 | 13 个 BLOCKING AC 未勾选 | ✅ **已闭**(形式)+ 复跑取证已补 | story-003 = 7/8 勾 · story-004 = 7/7 勾;87/87 复跑见 `editmode-full-rerun-2026-10-02.md` |
| B3 | 无 host-only write gate(`IEventAuthority.IsHost`) | ✅ **已修** + 负向夹具已补 | gate 在写入口;`SwitchableEventAuthority` 6 例负向夹具已落 |
| B4 | Discovery gate 未实现(`TryDiscover`) | ✅ **已修** | `TryDiscover` 已实现并复用 `TryAdvance` |
| **N1** | host gate 返回码与 `PoiNotFound` 混同 | 🔴 **真缺陷,未修**(本评审确认) | `PoiStateMachine.cs:100` 与 `:103` 同返回 `PoiNotFound` |
| **N2** | `RebuildFromDecoded`(读侧甲案)**无生产调用方** | ⚠️ **接缝未闭环**(新发现) | 全库仅测试调用;7a / 45 的真实读档/回放调用点不存在 |
| **N3** | story-004「白名单静态断言」AC 已勾但**未验证到实现** | 🔴 **AC 可疑**(新发现) | 仅注释提及 {4,25,37},grep 未命中任何断言代码 |
| **N4** | story-004「`World.unity` 零 gameplay 对象扫描」AC 已勾但**未验证到实现** | ⚠️ **AC 可疑**(新发现) | grep 未命中任何场景扫描代码 |
| **N5** | story-004 发现门**集成**无测试(仅纯函数拓扑测试) | 🔴 **取证缺口**(新发现) | `chunk_activation_test.cs` 7 例全为 chunk 拓扑,零 `TryDiscover`/`ActorCellEntered` |
| **N6** | Story 005 真身状态 | ⏸️ **确认 Pending** | story-005.md 与走查件均 Pending;走查件 AC 编号错标(见详节) |

**总判**:4 条原 BLOCKING 中 **B1/B3/B4 已修,B2 形式已闭**;但**本评审新查出 4 处此前未登记或未闭环的问题(N1–N5)**,其中 N1/N3/N5 为实质缺陷。**epic 不应转 Complete**(详见 §转 Complete 的前置)。

---

## 逐条详节

### B1 —— `PoiStateChanged` 未走 `Sim.Codec`

- **原判定**(转述):`PoiStateChanged` 载荷未走 `Sim.Codec`,手搓 `PayloadRef`。
- **中间态**(已归档):`43400dc` 仅「加 TODO 注释」,未真修
  —— 由 `reconciliation-world-ecozones-2026-10-02.md` 判为 🔴 未修。
- **修复落点**(2026-10-02,commit `59f87db`,story-006):
  - `unity/Assets/Sim/World/PoiStateMachine.cs:123-124` ——
    `_encoder.Encode(EventKind.PoiStateChanged, new PoiStateChangedPayload(poiId, (int)toState))`;
  - 构造注入 `IPayloadEncoder`(`PoiStateMachine.cs:47`、`:59-64`,null 抛 `ArgumentNullException`);
  - 接口 `unity/Assets/Sim.Contracts/IPayloadEncoder.cs:35`(第七抽象点,住 `Sim.Contracts`);
  - 实现 `unity/Assets/Sim.Codec/PayloadEncoder.cs:91`(分派 `PoiStateChanged → PayloadCodec.Encode`)。
- **实测证据**(2026-10-03 工作树):
  - `PoiStateMachine.cs` 内 **`new PayloadRef(` 代码零命中** —— 仅剩 2 处**注释**
    (`:57` 规则声明 · `:120` 修复记录),`StripComments` 后为零;
  - **TODO 已消失**(`grep TODO` 零命中);
  - 编码字节真的进池 —— 测试 `poi_payload_encoder_test.cs:56` 用 `_pool.TryGetBlob` 验证。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  grep -n "TODO\|new PayloadRef" unity/Assets/Sim/World/PoiStateMachine.cs
  # 期望:仅 :57 与 :120 两处注释行,无代码命中、无 TODO
  grep -n "_encoder.Encode" unity/Assets/Sim/World/PoiStateMachine.cs
  # 期望::123
  ```
- **判决**:**✅ 已修**。原判定成立且已真修;「免责不成立」的对账判定也被 story-006 正确处置(接 `IPayloadEncoder` 而非打开被禁的 `Sim → Sim.Codec` 引用边)。

### B2 —— 13 个 BLOCKING AC 未勾选

- **原判定**(转述):`story-003` / `story-004` 共 13 个 BLOCKING AC 未勾。
- **修复落点**:
  - `production/epics/world-ecozones/story-003-poi-state-machine-and-world-stream.md:34-41`
    —— 8 条 AC 中 **7 条已勾 `[x]`,第 41 条未勾**
    (`entities.yaml` ↔ kindgen 路由一致性 A1–A5,属 ADR-024 侧);
  - `production/epics/world-ecozones/story-004-chunk-activation-and-consumption-boundary.md:34-40`
    —— **7 条全勾 `[x]`**。
- **实测证据**:AC 勾选**形式已闭**;复跑取证已由 `editmode-full-rerun-2026-10-02.md`
  (WorldEcozones 87/87 逐例全绿)补齐 —— 该件为**转录/摘要**,原始 XML `/tmp/editmode-align.xml` 未入仓。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  grep -c "^- \[x\]" production/epics/world-ecozones/story-003-poi-state-machine-and-world-stream.md   # 7
  grep -c "^- \[ \]" production/epics/world-ecozones/story-003-poi-state-machine-and-world-stream.md   # 1
  grep -c "^- \[x\]" production/epics/world-ecozones/story-004-chunk-activation-and-consumption-boundary.md  # 7
  ```
- **判决**:**✅ 形式已闭**。但 ⚠️ **勾选的正确性不由本评审背书** ——
  story-004 有 3 条已勾 AC 经本评审独立复核后**存疑**(见 N3/N4/N5)。
  **「已勾」≠「已验」**,这正是原判定 B2 的教训,须以 N3–N5 回填。

### B3 —— 无 host-only write gate(`IEventAuthority.IsHost`)

- **原判定**(转述):`PoiStateMachine` 无「仅主机可写」门,客户端可写世界流。
- **修复落点**:
  - 接口:`unity/Assets/Sim.Contracts/Abstractions.cs:61`(`bool IsHost { get; }`);
  - 门:`unity/Assets/Sim/World/PoiStateMachine.cs:99-100`(`if (!_eventAuthority.IsHost) return PoiNotFound;`);
  - 负向夹具:`unity/Assets/Tests/EditMode/WorldEcozones/poi_state_machine_test.cs:31-41`
    (`SwitchableEventAuthority`,可中途翻转 `IsHost`);
  - 6 例负向测试:`poi_state_machine_test.cs:262-356`(客户端拒写 · 客户端不改状态 ·
    发现门同受覆盖 · 主机降级后拒写 · 只读重建不受门影响 · 主机基线仍写)。
- **实测证据**:门在**写入口每次调用**读取(非构造期缓存)—— 由 `:325-342` 的「先主机后降级」用例守住。
  gate 对**只读重建不设门**(`:305-322`)—— 与 ADR-020 Amendment B ③(客户端须能从流重建)一致。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  grep -n "IsHost" unity/Assets/Sim.Contracts/Abstractions.cs unity/Assets/Sim/World/PoiStateMachine.cs
  grep -c "test_ac626a" unity/Assets/Tests/EditMode/WorldEcozones/poi_state_machine_test.cs   # 6
  ```
- **判决**:**✅ 已修**,负向夹具已补,缺口 ①c 已闭。**但**gate 的返回码本身有缺陷(见 N1)。

### B4 —— Discovery gate 未实现(`TryDiscover`)

- **原判定**(转述):POI 发现门未实现。
- **修复落点**:`unity/Assets/Sim/World/PoiStateMachine.cs:86-89`
  (`TryDiscover(int poiId, long tick = 0) => TryAdvance(poiId, PoiState.Discovered, tick)`)。
- **实测证据**:`TryDiscover` 复用 `TryAdvance`,故自动继承 host gate 与单调性检查;
  负向夹具 `poi_state_machine_test.cs:289-302` 验证客户端发现门同受覆盖。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  grep -n "TryDiscover" unity/Assets/Sim/World/PoiStateMachine.cs
  # 期望::86 定义,:88 委托 TryAdvance
  ```
- **判决**:**✅ 已修**。⚠️ 但**发现门与 chunk 激活的集成闭环无测试**(见 N5)。

### story-006 —— 读侧「甲案」接缝评估

- **接缝设计**:`PoiStateMachine` 住 `Sim`,够不着 `Sim.Codec`(`Sim → Sim.Codec` 边由 ADR-025 §①:111 禁止);
  故读侧改 `RebuildFromDecoded(IReadOnlyList<(int PoiId, PoiState State)>)` —— **解码归调用方**。
  落点:`PoiStateMachine.cs:150-164`;类头注 `:12-17` 说明理由。
- **接缝是否成立**:**设计上成立且与写侧对称**(写侧交出 `PayloadRef`,读侧收下已解码字段;
  `Sim` 侧零 codec 依赖)。构造注入已强制 `IPayloadEncoder` 非 null(`:64`),方向正确。
- **⚠️ 遗漏的调用方(新发现,见 N2)**:本评审 grep 全库,
  **`RebuildFromDecoded` 无任何生产调用方** —— 仅测试调用。
  即:7a 读档 / 45 网络回放 / 边界层这三类「看得见 codec 的一侧」的**真实调用点尚不存在**。
  接缝的**契约侧**成立,但**消费侧未闭环**。
- **判决**:**接缝设计成立,但未闭环**(归 N2)。

---

## 新发现(本次评审新查出)

### 🔴 N1 —— host gate 返回码与 `PoiNotFound` 混同(确认,未修)

- **事实**(已亲自验证):
  - `PoiStateMachine.cs:99-100` —— 非主机 ⇒ `return PoiStateTransferResult.PoiNotFound;`
  - `PoiStateMachine.cs:102-103` —— `poi_id` 未登记 ⇒ `return PoiStateTransferResult.PoiNotFound;`
  - 二者**返回同一个枚举值**,调用方**无法区分**「我不是主机」与「该 POI 不存在」。
- **严重度**:**中**(Medium)。理由:
  - **不破坏 sim 正确性**(拒写行为正确、不 Append、不改状态);
  - 但**破坏调用方可诊断性** —— 客户端拿到 `PoiNotFound` 会误判为「POI 数据缺失」而非「我无写权」,
    可能触发错误的降级路径(如重新加载 POI 定义);
  - 联机场景下这条错误码会经日志/UI 暴露,误导排障。
- **现状登记**:测试**刻意不钉死**该错误码(`poi_state_machine_test.cs:358-369` 有登记注释),
  仅断言「非 Success」—— 这是**正确的自保**,但**缺陷本身仍在**。
- **修法**(story-006 已登记方向,未实施):新增 `PoiStateTransferResult.NotHost` 结果码,
  并同步 story-004 的消费面。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  sed -n '99,103p' unity/Assets/Sim/World/PoiStateMachine.cs
  # 期望:两处 return 同一 PoiNotFound
  grep -n "NotHost" unity/Assets/Sim/World/PoiStateMachine.cs   # 期望:零命中(未修)
  ```
- **判决**:🔴 **真缺陷,未修**。**本评审确认此前的「已登记未闭」判定成立**,严重度中。

### ⚠️ N2 —— `RebuildFromDecoded` 无生产调用方(接缝未闭环)

- **事实**(已亲自验证):全库 `grep -rn "RebuildFromDecoded"` 命中**仅测试**
  (`poi_payload_encoder_test.cs` · `poi_state_machine_test.cs`),**零生产代码调用**。
- **严重度**:**中**。理由:
  - 甲案接缝**契约侧成立**(接口形状正确、写读对称),这是本 story 的正确部分;
  - 但**读档/回放的真实闭环未建立** —— 没有任何一方实现「从 `Sim.Codec` 解码 + 喂入」的调用方。
    这意味着 **TR-worldeco-006(重放/迁移后状态从世界流重建)在运行期无实际执行路径**,
    目前仅在测试里被模拟;
  - 不构成「错误」,构成**未完成**(7a 读档 / 45 回放尚属未实现或未接线)。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  grep -rn "RebuildFromDecoded" unity/Assets/ | grep -v "/Tests/" | grep -v "\.meta"
  # 期望:零命中(仅测试侧有调用)
  ```
- **判决**:⚠️ **接缝未闭环**。非缺陷,但**「甲案已落地」不等于「重建链已通」** ——
  EPIC 转 Complete 前须确认该调用方的归属轮(7a / 45)已登记。

### 🔴 N3 —— story-004「白名单静态断言」AC 已勾,但未验证到实现

- **事实**(已亲自验证):
  - `story-004-chunk-activation-and-consumption-boundary.md:38` 该 AC **已勾 `[x]`**,
    并自述「**白名单断言已实现**」;
  - 但本评审 grep:
    - `unity/Assets/Editor.Tools.Gates/` 内**零** POI/`PoiState` 相关白名单断言
      (仅 ItemDb / Mixer / Audio 的白名单,与本 AC 无关);
    - 测试侧仅 `chunk_activation_test.cs:6` 与 `ChunkActivator.cs:7` 有**注释**提及 `{4,25,37}`,
      **无任何断言代码**。
- **严重度**:**中高**。理由:这是**「AC 已勾但判据未落地」**——
  即 AC-6-26a 同族失败模式(B2 原判定的教训)在 story-004 内**重演**。
  「27 引用 POI 状态 ⇒ 构建/测试失败」这一 negative 判据**当前不可执行**。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  grep -rn "PoiState\|POI 状态" unity/Assets/Editor.Tools.Gates/          # 期望:零命中
  grep -rn "ConsumerWhitelist\|AllowedReaders\|25.*37" unity/Assets/Sim/ unity/Assets/Tests/  # 期望:零命中
  ```
- **判决**:🔴 **AC 可疑**。**「白名单断言已实现」的自述本评审未验证到** ——
  要么实现藏在未检索到的位置(请实现方举证),要么该 AC 应回退为未勾。

### ⚠️ N4 —— story-004「`World.unity` 零 gameplay 对象扫描」AC 已勾,但未验证到实现

- **事实**(已亲自验证):
  - `story-004-...:39` 该 AC **已勾 `[x]`**,自述「Tooling 层扫描」;
  - 本评审 grep `World.unity` / `SceneScan` / `ZeroGameplay` / `gameplay GameObject`
    **零命中**任何场景扫描代码。
- **严重度**:**中**。理由:ADR-023 ② 的「构建期 `throw` 级扫描」是**结构性纪律**,
  若未实现则 `World.unity` 预摆 gameplay 对象**不会被拦**。
  ⚠️ 该 AC 自述挂 Tooling 层(ADR-022),可能属**未实现的外部工具**——
  但若如此,AC 不应勾 `[x]`(应如 AC-6-25/26 走 EXTERNAL NOT-RUN)。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  grep -rln "World.unity\|SceneScan\|ZeroGameplay\|gameplay GameObject" unity/Assets/   # 期望:零命中
  find unity/Assets -name "*.unity" | head                                               # 期望:场景文件是否存在
  ```
- **判决**:⚠️ **AC 可疑**(未验证到实现,可能属 EXTERNAL 误勾)。

### 🔴 N5 —— story-004 发现门集成无测试(仅纯函数拓扑测试)

- **事实**(已亲自验证):
  - `story-004-...:36` AC(AC-6-23 `[B]`「发现门在真实跨格会话中触发 `PoiStateChanged{Discovered}`,
    `ActorCellEntered` 计数与激活/门判定同 tick 求值,无表现态读取」)**已勾 `[x]`**;
  - 但 `chunk_activation_test.cs` 的 **7 例全为 chunk 拓扑纯函数测试**
    (`ComputeActiveChunks` / `WorldToChunk` / `IsChunkActive` 等),
    **零** `TryDiscover` / `ActorCellEntered` / `PoiStateMachine` 集成用例;
  - `TryDiscover` 的测试全在 `poi_state_machine_test.cs`(单元级),**不覆盖「跨格事件 → 激活 → 发现门」闭环**。
- **严重度**:**高**。理由:该 AC 是 **[B](BLOCKING)** 级,且是 story-004 的**核心集成判据**;
  当前**没有任何测试证明「`ActorCellEntered` 触发发现门且与激活同 tick 求值」**。
  这是 `[B]` AC 的**取证缺口**,与 B2 同型。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  grep -c "TryDiscover\|ActorCellEntered\|PoiStateMachine" unity/Assets/Tests/EditMode/WorldEcozones/chunk_activation_test.cs
  # 期望:0(该文件不测发现门集成)
  ```
- **判决**:🔴 **取证缺口**。`[B]` AC 已勾但无对应测试。

### ⏸️ N6 —— Story 005 真身状态 = Pending(确认)

- **事实**(已亲自验证):
  - `story-005-discovery-walkthrough-and-tooling-consistency.md:4` Status = **Pending**;
  - 走查件 `production/qa/evidence/world-ecozones/story-005-discovery-walkthrough-2026-10-01.md:5`
    Status = **Pending Desktop Verification**,全部 checklist 为 `[ ]`;
  - C1–C6 工具 CI 全为 **Not-RUN**。
- **⚠️ 附带发现(低严重度)**:走查件内 AC 编号**错标** ——
  该件用 `AC-6-27` / `AC-6-28`(第 48 / 55 行)标「P0 范围守门」「帧率无关激活体验」,
  但 **AC-6-27…32 已由 story-006 占用**(ADR-029 接线支);
  story-005 自身的 AC 是 5 条(无编号冲突的四条 + 两条)。⇒ 走查件 AC 编号须订正。
- **判决**:⏸️ **确认 Pending**,与 EPIC 登记一致。**未执行**([L] 桌面 + EXTERNAL CI 均未跑)。

---

## 转 Complete 的前置

> 本评审的判决:**epic 不应转 Complete**。以下按阻塞强度排序。

1. **🔴 N1 修 host gate 返回码**(阻塞):新增 `PoiStateTransferResult.NotHost`,
   同步 story-004 消费面;补一条钉死 `NotHost` 的负向用例(替换当前「不钉错误码」的临时自保)。
2. **🔴 N5 补发现门集成测试**(阻塞):story-004 的 `[B]` AC-6-23 须有真实测试覆盖
   「`ActorCellEntered` → 激活 → `TryDiscover` → `PoiStateChanged{Discovered}` 同 tick」闭环。
3. **🔴 N3 白名单静态断言**:要么举证其实现位置,要么回退该 AC 为未勾并补实现。
4. **⚠️ N4 `World.unity` 零 gameplay 对象扫描**:举证实现,或改标 EXTERNAL NOT-RUN。
5. **⚠️ N2 读侧调用方闭环**:确认 `RebuildFromDecoded` 的生产调用方(7a 读档 / 45 回放)已登记归属轮。
6. **P4(本评审的边界)**:本报告为**静态源码复核**,**未运行 Unity EditMode 测试**。
   转 Complete 前须以**当前 HEAD 重跑** EditMode(本评审未做),不得引用 2026-10-02 的 87/87 摘要借绿。
7. **story-005**:[L] 桌面走查 + EXTERNAL 工具 CI 执行(本 epic 既有的 Pending 项,非本评审新增)。
8. **N6 走查件 AC 编号订正**(低):`story-005-discovery-walkthrough-2026-10-01.md` 的
   `AC-6-27/28` 标签与 story-006 冲突,须订正为 story-005 自身的 AC 编号。

---

## 验证命令汇总(可复现)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# B1
grep -n "TODO\|new PayloadRef\|_encoder.Encode" unity/Assets/Sim/World/PoiStateMachine.cs

# B2
grep -c "^- \[x\]" production/epics/world-ecozones/story-003-poi-state-machine-and-world-stream.md
grep -c "^- \[x\]" production/epics/world-ecozones/story-004-chunk-activation-and-consumption-boundary.md

# B3
grep -n "IsHost" unity/Assets/Sim.Contracts/Abstractions.cs unity/Assets/Sim/World/PoiStateMachine.cs
grep -c "test_ac626a" unity/Assets/Tests/EditMode/WorldEcozones/poi_state_machine_test.cs

# B4
grep -n "TryDiscover" unity/Assets/Sim/World/PoiStateMachine.cs

# N1(host gate 返回码混同)
sed -n '99,103p' unity/Assets/Sim/World/PoiStateMachine.cs
grep -n "NotHost" unity/Assets/Sim/World/PoiStateMachine.cs

# N2(RebuildFromDecoded 无生产调用方)
grep -rn "RebuildFromDecoded" unity/Assets/ | grep -v "/Tests/" | grep -v "\.meta"

# N3(白名单断言)
grep -rn "PoiState\|POI 状态" unity/Assets/Editor.Tools.Gates/

# N4(场景扫描)
grep -rln "World.unity\|SceneScan\|ZeroGameplay\|gameplay GameObject" unity/Assets/

# N5(发现门集成测试)
grep -c "TryDiscover\|ActorCellEntered\|PoiStateMachine" unity/Assets/Tests/EditMode/WorldEcozones/chunk_activation_test.cs

# N6(Story 005 状态)
grep -n "Status" production/epics/world-ecozones/story-005-discovery-walkthrough-and-tooling-consistency.md
```

---

## 未验证项(诚实声明)

- **未运行 Unity EditMode 测试** —— 本评审为静态源码复核;所有「测试通过」均引自仓库摘要件,未独立复跑。
- **未审阅** `EcozoneQuery.cs` / `EcozoneRegistry.cs` / `WorldLattice.cs` 的实现细节
  (本报告聚焦 4 条 BLOCKING + story-006 接缝 + story-005 状态;B 组之外的 A/C 组 AC 未逐条复核)。
- **未审阅** ADR-029 全文(仅据 `IPayloadEncoder.cs` 头注与 story-006 转述其口径)。
- **N3/N4** 为「grep 未命中」型结论 —— 若实现存在但命名/位置超出检索面,请实现方举证反驳。
- **`editmode-full-rerun-2026-10-02.md`** 原始 XML 未入仓(仅摘要),本评审**未核验其原始产物**。
