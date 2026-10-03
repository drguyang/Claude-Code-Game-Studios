# 代码评审(第二轮)—— world-ecozones(系统 6b 生态区与 POI)

> **评审对象**:epic `world-ecozones`(6b)· HEAD 工作树 · 引擎 Unity 6000.3.24f1
> **评审日期**:2026-10-03
> **评审员**:独立评审员(未参与本 epic 任何实现或前次对账)
>
> ## ⚠️ 本轮为第二轮;评的是修复后的代码
>
> 第一轮 `review-world-ecozones-2026-10-03.md` 查出 N1–N5(含 C3/B1/B2/B3)。
> 其后 2026-10-03 有一批修复(C3 加 `NotHost` · B2 补集成测试 · B1/B3 撤勾)。
> **本报告评的是修复后的工作树**,不追认任何 commit message / story Completion Notes 的自述。
>
> **基线**:2026-10-03 工作树实际文件内容(grep / sed / 读源码),每条附可复现命令。
> **⚠️ 本评审未运行 Unity EditMode 测试** —— 全为**静态源码复核**;凡「测试通过」均引自仓库摘要件,
> 未独立复跑(见 §未验证项)。

---

## 结论摘要表

| # | 项 | 第一轮判定 | 本轮判决 | 依据(简述) |
|---|---|---|---|---|
| **C3** | host gate 返回码 | 🔴 真缺陷 | ✅ **已修(真到位)** | `NotHost` 已加(`PoiStateMachine.cs:48`)· gate 返回它(`:111`)· 测试**钉死**(`:277-278`)+ **双向可区分**断言(`:291-292`) |
| **B2** | 发现门集成无测试 | 🔴 取证缺口 | ⚠️ **形式已补,实质仍弱** | 3 例已落(`chunk_activation_test.cs:151-205`);但**无真 `ActorCellEntered` 事件**,「同 tick 求值」判据**未真验**(见 N2-1) |
| **B1** | 白名单断言 | 🔴 AC 可疑 | ✅ **撤勾到位** | story-004:44 已改 `[ ]`,撤勾理由 + 重开条件写明 |
| **B3** | World.unity 扫描 | ⚠️ AC 可疑 | ✅ **撤勾到位** | story-004:52 已改 `[ ]`,理由写明(场景文件本身尚不存在) |
| **B1(原)** | PoiStateChanged 走 encoder | ✅ 已修 | ✅ **维持已修** | `_encoder.Encode` 在 `:134`;`new PayloadRef(` 仅剩注释(`:66`/`:131`) |
| **B2(原)** | 13 BLOCKING AC | ✅ 形式已闭 | ✅ **维持**(story-003 7/8 · story-004 **5/7**,较前 **-2** 因撤勾) | 撤勾是诚实选项 |
| **B3(原)** | host gate `IsHost` | ✅ 已修 | ✅ **维持已修** | `:110-111` 每次调用读 `IsHost`;6 例负向夹具仍在 |
| **B4(原)** | `TryDiscover` | ✅ 已修 | ✅ **维持已修** | `:95-98` 定义并委托 `TryAdvance` |
| **C6** | `RebuildFromDecoded` 无生产调用方 | ⚠️ 接缝未闭环 | ⚠️ **仍成立**(未变) | 全库生产代码零调用,仅测试;归 7a/45 |
| **Story 005** | 真身状态 | ⏸️ 确认 Pending | ⏸️ **维持 Pending** | story-005:4 与走查件均 Pending |
| **N2-1** | B2 新测试实质弱(新) | — | ⚠️ **新发现** | 无 `ActorCellEntered` 真事件;`blockedOnUnloadedChunk` 不碰状态机 |
| **N2-2** | 陈旧残留注释(新) | — | 🟡 **新发现(低)** | `poi_state_machine_test.cs:355-368` §已知缺陷 仍写「未修 PoiNotFound」 |

**总判**:**C3/B1/B3 三处修复真到位、可复现**;4 条原 BLOCKING(B1–B4)当下**全部处于「已修/已诚实撤勾」**。
但 **B2 的补测是「形式已补、实质仍弱」** —— `[B]` AC 的核心判据(同 tick 求值 + `ActorCellEntered`)
**仍未真验**。另有 2 处新发现(测试实质 + 陈旧注释)。**epic 仍不应转 Complete**。

---

## 逐条详节

### C3 —— host gate 返回码与 `PoiNotFound` 混同(第一轮 🔴)

- **原判定**:`PoiStateMachine` 的 host gate 与「POI 不存在」**同返回 `PoiNotFound`** ⇒ 调用方不可区分。
- **修复落点**(2026-10-03):
  - `unity/Assets/Sim/World/PoiStateMachine.cs:41-48` —— 枚举新增 `NotHost` 成员,
    带完整 doc(注明「2026-10-03 新增(评审 C3)」及病因);
  - `unity/Assets/Sim/World/PoiStateMachine.cs:110-111` ——
    `if (!_eventAuthority.IsHost) return PoiStateTransferResult.NotHost;`
    (⚠️ 关键:**gate 在 `poi_id` 存在性检查 `:113-114` 之前** ⇒ 客户端下必得 `NotHost`,语义正确)。
- **测试钉死**(`unity/Assets/Tests/EditMode/WorldEcozones/poi_state_machine_test.cs`):
  - `:277-278` —— `Assert.AreEqual(NotHost, result, ...)` **正面钉死**;
  - `:279-280` —— `Assert.AreNotEqual(PoiNotFound, result, ...)` **反向可区分**;
  - `:285-292` —— 对照用例:主机身份 + 未登记 poi_id(999) ⇒ `PoiNotFound`,
    且 `Assert.AreNotEqual(NotHost, notFound, "主机身份不得返回 NotHost")` ⇒ **两码双向可区分**已钉。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  sed -n '110,114p' unity/Assets/Sim/World/PoiStateMachine.cs
  # 期望::111 return NotHost;:114 return PoiNotFound(gate 先于 id 检查)
  grep -n "NotHost" unity/Assets/Sim/World/PoiStateMachine.cs          # 期望::48 定义 · :111 使用
  grep -n "NotHost" unity/Assets/Tests/EditMode/WorldEcozones/poi_state_machine_test.cs  # 期望:钉死 + 可区分
  ```
- **判决**:**✅ 已修,真到位**。原缺陷消失;两码**可区分**且测试**双向钉死**。
  ⚠️ 唯一残留 = 文件末 `:355-368` 的陈旧注释(见 N2-2,不影响行为)。

### B2 —— 发现门集成无测试(第一轮 🔴)

- **原判定**:`chunk_activation_test.cs` 7 例**全为 chunk 拓扑纯函数**,`TryDiscover`/`ActorCellEntered`
  引用数 = 0 ⇒ `[B]` AC-6-23 **无集成取证**。
- **修复落点**(`unity/Assets/Tests/EditMode/WorldEcozones/chunk_activation_test.cs`):
  - `:151-175` `test_ac623_discoverGate_firesOnCellEntry_sameTick`;
  - `:177-191` `test_ac623_discoverGate_idempotentOnSecondEntry`;
  - `:193-205` `test_ac623_discoverGate_blockedOnUnloadedChunk`;
  - 辅助:`SpyEventSink` / `AlwaysHostAuthority` / `NoopEncoder`(`:208-227`)。
- **实测证据**:文件内 `TryDiscover|ActorCellEntered|PoiStateMachine` 引用数由 **0 → 9**(grep -c)。
  三例均**真构造 `PoiStateMachine`** 并断言 `TryDiscover` 的行为 + `AppendedEvents` 条数/tick。
- **⚠️ 但判据实质仍弱(新发现 N2-1,见下)** —— 三例**未产生任何 `ActorCellEntered` 事件**,
  亦**未驱动激活 ↔ 发现门的真实接线**:`firesOnCellEntry_sameTick` 是「先手工断言 `IsChunkActive`,
  再手工 `TryDiscover(1, 100)`」——两步**无因果关系**;`blockedOnUnloadedChunk` **完全不碰状态机**,
  仅测 `ChunkActivator`(与旧 7 例同面)。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  grep -c "TryDiscover\|ActorCellEntered\|PoiStateMachine" unity/Assets/Tests/EditMode/WorldEcozones/chunk_activation_test.cs  # 期望:9
  grep -n "ActorCellEntered" unity/Assets/Tests/EditMode/WorldEcozones/chunk_activation_test.cs  # 期望:仅 :147 注释
  ```
- **判决**:**⚠️ 形式已补(3 例真落),但实质判据未闭合**。AC 原文「`ActorCellEntered` 计数与
  激活/门判定**同 tick 求值**」**仍无测试证明**。较第一轮的「零用例」有实质进步,但**不得据此记 `[B]` AC 已验**。

### B1 —— 「白名单静态断言」AC 已勾但无实现(第一轮 🔴)

- **原判定**:story-004 该 AC 已勾并自述「已实现」,但 `Editor.Tools.Gates/` 零 POI 白名单断言。
- **修复落点**:`production/epics/world-ecozones/story-004-chunk-activation-and-consumption-boundary.md:44-51`
  —— AC 已改 **`- [ ]`(撤勾)**,并附完整理由:
  - 实测无实现(`Editor.Tools.Gates/` 内零 POI 白名单断言);
  - **且判据对象尚不存在**(27 侧零 `PoiState` 引用 ⇒ 负向判据无夹具);
  - **撤勾理由** + **重开条件**均已写明。
- **实测复核**:`grep -rn "PoiState" unity/Assets/Editor.Tools.Gates/` ⇒ 仅 `AssemblyGates.cs:205-206`
  的**注释**(讲 b6 门,与本 AC 无关);`grep "PoiState" unity/Assets/Sim/EnemyAI/` ⇒ **零命中**(27 侧确无引用)。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  sed -n '44,51p' production/epics/world-ecozones/story-004-chunk-activation-and-consumption-boundary.md  # 期望:- [ ] + 撤勾理由
  grep -rn "PoiState" unity/Assets/Sim/EnemyAI/    # 期望:零命中(负向夹具无对象)
  ```
- **判决**:**✅ 撤勾到位且诚实**。撤勾理由与重开条件俱在 —— 符合「留勾而实现不存在 = 虚报」的处置纪律。

### B3 —— `World.unity` 零 gameplay 对象扫描 AC 已勾但无实现(第一轮 ⚠️)

- **原判定**:story-004 该 AC 已勾并自述「Tooling 层扫描」,但 grep 零命中场景扫描代码。
- **修复落点**:`story-004-...:52-57` —— AC 已改 **`- [ ]`(撤勾)**,理由写明:
  全仓零 `World.unity` 引用;**且场景文件本身尚不存在**;重开条件 = ADR-023 三场景制落地时补扫描。
- **实测复核**:`find unity/Assets -name "World.unity"` ⇒ **零命中**(仅有 Boot.unity + spike 场景)。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  sed -n '52,57p' production/epics/world-ecozones/story-004-chunk-activation-and-consumption-boundary.md  # 期望:- [ ] + 撤勾理由
  find unity/Assets -name "World.unity"   # 期望:零命中
  ```
- **判决**:**✅ 撤勾到位且诚实**(判据对象不存在 ⇒ 无法勾)。

### B1(原)—— `PoiStateChanged` 走 `IPayloadEncoder`(第一轮 ✅)

- **本轮复核**:`PoiStateMachine.cs:134` —— `_encoder.Encode(EventKind.PoiStateChanged,
  new PoiStateChangedPayload(poiId, (int)toState))` 仍在;`new PayloadRef(` 仅剩 **2 处注释**(`:66`/`:131`),
  `StripComments` 后为零;`TODO` 零命中。构造注入 `IPayloadEncoder` 非 null(`:73`)。
- **判决**:**✅ 维持已修**(与第一轮一致,无回退)。

### B3(原)—— host-only write gate `IsHost`(第一轮 ✅)

- **本轮复核**:`PoiStateMachine.cs:110-111` gate 仍在**写入口每次调用读取**(非构造期缓存);
  6 例负向夹具仍在(`poi_state_machine_test.cs:260-356`:`test_ac626a_*`)。
- **判决**:**✅ 维持已修**。⚠️ 注:该组文件末的 §已知缺陷 注释已陈旧(见 N2-2)。

### B4(原)—— `TryDiscover`(第一轮 ✅)

- **本轮复核**:`PoiStateMachine.cs:95-98` —— `TryDiscover(int poiId, long tick = 0) =>
  TryAdvance(poiId, PoiState.Discovered, tick)` 仍在,自动继承 host gate 与单调性。
- **判决**:**✅ 维持已修**。

### C6 —— `RebuildFromDecoded` 无生产调用方(第一轮 ⚠️)

- **本轮复核**:`grep -rn "RebuildFromDecoded" unity/Assets/` ⇒ 命中**仅测试**
  (`poi_payload_encoder_test.cs` ×4 · `poi_state_machine_test.cs` ×2)+ 定义处 `PoiStateMachine.cs:161`。
  **生产代码零调用**(7a 读档 / 45 回放 / 边界层调用点均不存在)。
- **判决**:⚠️ **仍成立,未变**。非缺陷,属**接缝未闭环**;归 7a/45 轮。与第一轮定性一致。

### Story 005 —— 真身状态

- **本轮复核**:`story-005-...md:4` Status = **Pending**;`:77` 亦 Pending(桌面 + EXTERNAL CI 两项未跑)。
- **判决**:⏸️ **维持 Pending**,与 EPIC 登记一致。**非代码缺陷**,是未执行的 [L]/EXTERNAL 项。
  (第一轮登记的「走查件 AC 编号错标」低危项本轮**未复核**——见 §未验证项。)

---

## 新发现(本轮新查出)

### ⚠️ N2-1 —— B2 的补测「形式已补、实质仍弱」

- **事实**(已亲自验证):
  - 三例新测试**未产生任何 `ActorCellEntered` 事件** —— 全库 `chunk_activation_test.cs` 内
    `ActorCellEntered` 仅出现在 `:147` 的**注释**,零代码引用;
  - `firesOnCellEntry_sameTick`(`:151-175`):先 `Assert.IsTrue(activator.IsChunkActive(chunk))`
    (手工查激活),再 `machine.TryDiscover(1, Tick)`,并断言 `event.Tick == Tick`。
    **两段无因果关系** —— `Tick` 是**测试直接传给 `TryDiscover` 的入参**,故「同 tick」断言
    (`:173-174`)**恒真**,不构成「`ActorCellEntered` 计数与激活/门判定同 tick 求值」的证据;
  - `blockedOnUnloadedChunk`(`:193-205`)**完全不构造 `PoiStateMachine`、不调 `TryDiscover`** ——
    仅测 `ChunkActivator`,与旧 7 例同面。
- **严重度**:**中**。理由:story-004 的 AC-6-23 是 **`[B]`(BLOCKING)** 级,其**核心判据**
  (「`ActorCellEntered` 计数与激活/门判定**同 tick 求值**,无表现态读取」)本轮**仍无测试证明**。
  较第一轮「零用例」是进步,但**「补了 3 例」≠「判据已验」** —— 正是 B2 原判定的同型教训
  (「AC 已勾 ≠ 判据已执行」)在**补测本身**上的**局部重演**。
- **修法建议**:新增一例**真驱动**用例 —— 由 `ActorCellEnteredPayload` 事件(或等价的跨格输入)
  **驱动** `TryDiscover`,断言「激活判定与门判定**读同一 tick**」;`blockedOnUnloadedChunk` 应补
  「未驻留 ⇒ **门不被触发**」的状态机侧断言(现仅测 `ChunkActivator`)。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  grep -n "ActorCellEntered" unity/Assets/Tests/EditMode/WorldEcozones/chunk_activation_test.cs  # 期望:仅 :147 注释
  sed -n '151,175p' unity/Assets/Tests/EditMode/WorldEcozones/chunk_activation_test.cs  # 看「无因果」两段
  ```
- **判决**:⚠️ **新发现,实质判据未闭合**。

### 🟡 N2-2 —— 陈旧残留注释(C3 修复后未同步)

- **事实**(已亲自验证):
  - `unity/Assets/Tests/EditMode/WorldEcozones/poi_state_machine_test.cs:355-368`
    §已知缺陷 段仍写:gate 返回 `PoiNotFound`(与「POI 不存在」混同)、
    「正确修法是新增 `NotHost`…归 we 的实现轮」—— **与已修状态直接矛盾**
    (同文件 `:275-278` 已钉死 `NotHost`);
  - `production/epics/world-ecozones/story-003-...md:36` 的 AC 残留注仍写
    「⚠️ 残留:gate 返回码 `PoiNotFound` 与「POI 不存在」混同(**未修,归实现轮**)」—— 亦陈旧;
  - 同源:`story-004-...md:43` / `story-003-...md:38` 的 AC 引用方法名 **`RebuildFromEvents`**,
    但实际方法为 **`RebuildFromDecoded`**(全库无 `RebuildFromEvents`)—— 命名陈旧。
- **严重度**:**低**。理由:**不影响运行行为**,但属**文档/注释与代码不一致**,
  会误导后续评审者以为 C3 未修(与第一轮 D 类同型)。**属收尾卫生项**。
- **可复现验证命令**:
  ```bash
  cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
  sed -n '355,368p' unity/Assets/Tests/EditMode/WorldEcozones/poi_state_machine_test.cs  # 陈旧 §已知缺陷
  grep -n "PoiNotFound.*未修\|未修.*PoiNotFound" production/epics/world-ecozones/story-003-poi-state-machine-and-world-stream.md
  grep -rn "RebuildFromEvents" unity/Assets/ production/epics/   # 期望:仅 story 的陈旧 AC 文本,零代码
  ```
- **判决**:🟡 **新发现(低)**。修法 = 同步注释/story AC 文本至已修状态。

---

## 转 Complete 的前置

> 本轮判决:**epic 仍不应转 Complete**。较第一轮,阻塞项已大幅收窄。按阻塞强度排序:

1. **⚠️ N2-1(阻塞)** —— `[B]` AC-6-23 的**实质判据**须有**真驱动**用例:
   `ActorCellEntered` → 激活 → `TryDiscover` → `PoiStateChanged{Discovered}` **同 tick** 闭环;
   `blockedOnUnloadedChunk` 补状态机侧断言。**现 3 例不足以记该 AC 已验**。
2. **✅ C3 / B1 / B3** —— 三项修复**已到位**,从阻塞清单**移除**(本轮回填)。
3. **⚠️ C6 读侧调用方闭环** —— 确认 `RebuildFromDecoded` 的生产调用方(7a 读档 / 45 回放)
   **已登记归属轮**(与第一轮一致,未变)。
4. **🟡 N2-2 陈旧注释同步**(低) —— `poi_state_machine_test.cs:355-368` §已知缺陷 ·
   `story-003:36` 残留注 · `RebuildFromEvents` 陈旧方法名。
5. **P4(本评审的边界)** —— 本报告为**静态源码复核**,**未运行 Unity EditMode 测试**。
   转 Complete 前须以**当前 HEAD 重跑** EditMode,**不得引用任何历史摘要件借绿**。
6. **story-005** —— [L] 桌面走查 + EXTERNAL 工具 CI 执行(epic 既有 Pending 项,非本评审新增)。

---

## 验证命令汇总(可复现)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# C3(host gate 返回码 —— 已修)
sed -n '110,114p' unity/Assets/Sim/World/PoiStateMachine.cs
grep -n "NotHost" unity/Assets/Sim/World/PoiStateMachine.cs
grep -n "NotHost" unity/Assets/Tests/EditMode/WorldEcozones/poi_state_machine_test.cs

# B2(发现门集成 —— 形式已补)
grep -c "TryDiscover\|ActorCellEntered\|PoiStateMachine" unity/Assets/Tests/EditMode/WorldEcozones/chunk_activation_test.cs  # 9
grep -n "ActorCellEntered" unity/Assets/Tests/EditMode/WorldEcozones/chunk_activation_test.cs  # 仅 :147 注释

# B1 / B3(撤勾 —— 已到位)
sed -n '44,57p' production/epics/world-ecozones/story-004-chunk-activation-and-consumption-boundary.md
grep -rn "PoiState" unity/Assets/Sim/EnemyAI/     # 零命中
find unity/Assets -name "World.unity"             # 零命中

# B1 原(encoder 接线)
grep -n "new PayloadRef\|_encoder.Encode\|TODO" unity/Assets/Sim/World/PoiStateMachine.cs

# C6(RebuildFromDecoded 生产调用方)
grep -rn "RebuildFromDecoded" unity/Assets/ | grep -v "/Tests/" | grep -v "\.meta"   # 零命中

# N2-2(陈旧注释)
sed -n '355,368p' unity/Assets/Tests/EditMode/WorldEcozones/poi_state_machine_test.cs
grep -rn "RebuildFromEvents" unity/Assets/ production/epics/
```

---

## 未验证项(诚实声明)

- **未运行 Unity EditMode 测试** —— 本评审为静态源码复核;所有「测试通过」均引自仓库摘要件,未独立复跑。
- **未审阅** `EcozoneQuery.cs` / `EcozoneRegistry.cs` / `WorldLattice.cs` / `ChunkActivator.cs` 的
  实现细节(本报告聚焦 C3/B1/B2/B3 四条 + 原 BLOCKING 四条的当下状态)。
- **第一轮 N6 的「走查件 AC 编号错标」低危项本轮未复核** —— 未读 `story-005-discovery-walkthrough-2026-10-01.md`。
- **N2-1** 为「测试内容实质」型结论 —— 若实现方认为现有 3 例已充分覆盖 AC-6-23,请举证「同 tick 求值」的**非恒真**判据。
- **`editmode-full-rerun-2026-10-02.md`** 原始 XML 未入仓(仅摘要),本评审**未核验其原始产物**。
