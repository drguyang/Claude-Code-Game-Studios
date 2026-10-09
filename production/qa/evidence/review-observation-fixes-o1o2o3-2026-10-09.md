# 评审原件 — O-1/O-2/O-3 观察项修复轮 · 2026-10-09

> **对象**(三轮一批):
> 1. **O-1**: `SimEvent.Seq` 哨兵冲突(0 → -1)—— `SimEvent.cs` / `EventStream.cs` + 10 处生产调用方 + `event_stream_test.cs`
> 2. **O-2**: `IPresenceQuery` 无生产实装 —— 新建 `PresenceRegistry.cs`(表现层)+ 9 条测试
> 3. **O-3**: `PatientSpawner` 先发号后 Append 的 ID 空洞 —— `IRollbackableIdAuthority`(契约)+ `IdAuthority` 回滚 + `SpawnNext` try/catch + 3 条测试 + CAP 三方交互测试
> **轮次**: 双代理评审**恰一轮**(用户指令)—— ✅ **已完成:两面 FIX-THEN-APPROVE → 修复 → 9 发变异全中 → 终态复跑全绿 → APPROVE**
> **前置**: 本批源自 `review-disease-onset-writer-2026-10-09.md` §七 的三条新登记观察项,用户裁定「开这三个修复轮」。

## 一、原判定

**代码面 lead-programmer**:**FIX-THEN-APPROVE** —— 修法本体全部正确、完备、可测;
门 A 与 ADR-025/029 红线未动。核过零问题 12 项(调用方完备性 · 无旧哨兵残留 ·
codec/黄金夹具零影响 · 流内容逐位等价 · O-3 机制 A 论证 · CAP 三方判别力 · Sim.Contracts 纯度等)。

| # | 级 | 位置 | 问题 |
|---|----|------|------|
| 1 | **BLOCKING** | `PresenceRegistry.cs:13,43-74` ↔ `IPresenceQuery.cs:31-33` | **占格语义自相矛盾**:`IsPresentAt` 契约含玩家/敌人(`PlaceableChecker:95` 条件⑥ 消费),但头注称 MovePresent 同步玩家 —— 而 `MovePresent` 对不在场者忽略、唯一入口 `AddPresent` 必计入 `_present` ⇒ 玩家要么吃 CAP 名额(场景 a)、要么占格恒不可见(场景 b)。**二选一**:占格/在场分离,或收窄契约 |
| 2 | ADVISORY | `Abstractions.cs:50-60` | 回滚接口缺「Append 原子(失败=零写入)」前提声明(非原子 sink 会重发流中号) |
| 3 | ADVISORY | `IdAuthority.cs:55-63` | 回滚缺非负下界:`_nextPatient==0` 时传 `PatientId(-1)` 会把计数器降到 -1 |
| 4 | ADVISORY | `EventStream.cs:69` | 去重键在发号**前**取 Seq:同 (kind,tick,patient) 两条未发号事件静默坍缩(既有问题,O-1 后需重登记;`StructureKinds:224` 同 tick 两结构最现实)→ 建议立 **O-4** |
| 5 | ADVISORY | `EventStream.cs:59-66` | CAP 判据对 `PatientId.None` 世界事件同样生效:在场满 24 时 StructurePlaced/POI/跨格全被拒(既有;O-2 是首次让 CAP 生产可达的前置)→ 建议立 **O-5** 归组合层接线轮 |
| 6 | ADVISORY | `PresenceRegistry.cs` | 无通知面:组合层须双写(登记簿+13 侧视图),漏写即第二真源 → 建议暴露 `event PresentChanged` |
| 7 | ADVISORY | `PrescribeFlow.cs:161,338` | `TreatmentEvent.Seq` 现为 -1 但字段注未标口径(全库无消费者,无回归;防 45 当已发号传输) |
| 8 | ADVISORY | 本原件 §一/§五 | 未完稿 ⇒ 不得转 Complete(§五已填;§一即本节) |

**测试面 qa-lead**:**FIX-THEN-APPROVE** —— 3 BLOCKING(补测级,其中 B3 附生产一行守卫)
+ 8 ADVISORY;O-1/O-3 主体测试质量高,缺口集中在 O-2 写面未测分支。核过:CAP 夹具正确
避开坑 · 哨兵闭环无遗留 0 调用点 · 确定性/隔离通过 · 假绿扫描未发现新假绿 · 计数自洽(+13)。

| # | 级 | 对象 | 发现 |
|---|----|------|------|
| B1 | **BLOCKING** | 缺测 / `PresenceRegistry.cs:68` | `MovePresent` from==to 短路无测:删守卫变异幸存(独占格自移 ⇒ 自格被误让) |
| B2 | **BLOCKING** | 缺测 / `PresenceRegistry.cs:72-73` | `Move` 共享格让格无测:删 `CellOccupiedByOther` 守卫变异幸存(A 移走 ⇒ B 的格被夺) |
| B3 | **BLOCKING** | `IdAuthority.cs:55` | `TryRollback(PatientId.None)` 无测且**现行为违契约**:`_nextPatient==0` 时 None(-1)==-1 误命中 ⇒ 计数器降到 -1,下号发哨兵 |
| A1 | ADVISORY | `PresenceRegistry.cs:55-60` | 删 `_cellOf.Remove` 变异全套绿:幽灵占格 ⇒ 格永不释放 |
| A2 | ADVISORY | 幂等测 | 重复 Add **异格**语义未钉(现早退保留旧格) |
| A3 | ADVISORY | `EventStream.cs:78` | 复位条件 patient 分量无测:同 tick 换患者变异幸存 |
| A4–A6 | ADVISORY | 测试登记 | A4 `Count==3` 是 payload-blind 去重有意防线(改去重键时预期红,勿绕过)· A5 `test_o1`③ 钉计数器含显式事件前进(有意)· A6 `test_o1/2/3` 命名承 repo 先例,登记不强改 |
| MUT-O2 | 变异缺口 | — | 评审时 MUT-O2 尚在跑;**实测恰红 2**(addPresent_readableViaQueryFace + sameCell_twoPatients)⇒ 已闭(见 §四) |

## 二、修复落点(已实施)

### O-1:Seq 哨兵 0 → -1

| # | 落点 | 内容 |
|---|---|---|
| 1 | `SimEvent.cs:36-39` | Seq doc comment:明写哨兵口径(**-1 = 未发号;0 是合法首号值**) |
| 2 | `EventStream.cs:90` | `if (e.Seq == 0)` → `if (e.Seq < 0)` |
| 3 | 10 处生产调用方 | 待发事件 Seq 实参 `0` → `-1`(PrescribeFlow · PatientSpawner · StructureKinds×3 · HostEmergencyProcessor×2 · PoiStateMachine · PlayerController · CellTransitionDetector) |
| 4 | `event_stream_test.cs` | 4 组真流 Append 改 -1;`test_seq_resetsEachTick` 补发号值断言;新增 `test_o1_seqSentinel_negativeIssued_zeroExplicitPreserved` |
| 5 | `vertical_slice_test.cs` | 2 处直写 FakeEventSink 的 Seq 0 → -1(语义统一) |

**刻意保留的既有语义(登记不隐藏)**:去重键用**入流 Seq**(发号前)——
同 `(kind, tick, patient)` 的未发号重发 ⇒ 同键 ⇒ **幂等去重**(重试不产生重复)。
这是**既有行为**(旧哨兵 0 下同样坍缩),不是本轮引入;O-1 测试已按此语义落
(未发号重发用例断言去重;三条语义验证用不同 Kind 避开)。

### O-2:PresenceRegistry(IPresenceQuery 生产实装)

| # | 落点 | 内容 |
|---|---|---|
| 1 | `Gameplay.Presentation/PatientAI/PresenceRegistry.cs`(新) | `IPresenceQuery` 生产实装:读面(IsPresent/PresentCount/IsPresentAt)+ 写面(AddPresent/RemovePresent/MovePresent/ResetForLoad);同格多实体不夺格;Move 不隐式入场 |
| 2 | `Tests/EditMode/DiseaseSimulation/presence_registry_test.cs`(新) | 9 条:读写闭环 · 幂等 · 离开让格 · 同格保留 · 跨格 · 不隐式入场 · ResetForLoad · **CAP 三方交互**(灌满 24 → SpawnNext 抛 → 回滚 → 让格后重发同号) |

**边界(登记)**:在场集 = **派生态**(ADR-009)—— 登记簿**不自行推导**谁在场,
由组合层(9 的出现 / 6 的 chunk 激活)灌入;13 侧 `PresentPatientsView` /
`PatientSpatialDirector` 的同步接线**归组合层**(P0 尚无组合根,接线登记为后续义务)。

### O-3:可回滚发号

| # | 落点 | 内容 |
|---|---|---|
| 1 | `Sim.Contracts/Abstractions.cs` | 新接口 `IRollbackableIdAuthority : IIdAuthority`(`TryRollbackLastPatientId`;**可选能力**,不动既有实现) |
| 2 | `IdAuthority.cs` | 实现:仅最后号可回滚;非最后号拒绝(防重号)。头注论证与机制 A「计数器永不复位」**不冲突**(回滚只发生在事件未进流、无持久引用时) |
| 3 | `PatientSpawner.cs` | Encode+Append 包 try/catch:失败 ⇒ 回滚刚发放的号 ⇒ rethrow |
| 4 | `id_authority.cs` | 3 条:最后号回滚后重发 · 非最后号拒绝 · 未发放号拒绝 |

### §二之二:双代理评审后的修复(2026-10-09)

| 来源 | 级 | 修复 |
|---|---|---|
| 代码 #1 | **BLOCKING** | **甲案:占格集与在场集分离** —— `PresenceRegistry` 新增 `SetOccupant(int, WorldPos)` / `RemoveOccupant(int, WorldPos→int)` 写面(玩家/敌人只写 `_cellOf`/`_occupiedCells`,**不入 `_present` ⇒ 不计 CAP**);`AddPresent`/`RemovePresent`/`MovePresent` 仍同时维护两集;头注改写(消除「MovePresent 同步玩家」自相矛盾);抽 `Occupy`/`VacateCell`/`MoveCell` 私有三元组供两写面复用。契约 `IPresenceQuery` **不动**(`IsPresentAt` 玩家/敌人语义保留 ⇒ `PlaceableChecker` 条件⑥ 消费面不变) |
| 代码 #2 | ADVISORY | `IRollbackableIdAuthority` 文档补「前提:Append 必须原子(失败=零写入),非原子 sink 不得实现本接口」 |
| 代码 #7 | ADVISORY | `PrescribeOutcome.TreatmentEvent` 字段注补 Seq=-1 口径(传输前须主机发号) |
| 测试 B1 | **BLOCKING** | 补 `test_o2_movePresent_sameCell_keepsCellOccupied` |
| 测试 B2 | **BLOCKING** | 补 `test_o2_movePresent_sharedCell_coOccupantKeepsCell` |
| 测试 B3 | **BLOCKING** | `IdAuthority.TryRollbackLastPatientId` 加负域守卫 `id.Value < 0 ⇒ false` + 补 `test_o3_rollback_noneSentinel_rejected` |
| 测试 A1 | ADVISORY | 补 `test_o2_removePresent_thenOtherEntersSameCell_cellReleases`(幽灵占格) |
| 测试 A2 | ADVISORY | 补 `test_o2_addPresent_duplicateDifferentCell_keepsOriginalCell`(钉死「重复 Add 不改格」) |
| 测试 A3 | ADVISORY | 补 `test_ac16_seqResets_onPatientSwitch_sameTick`(复位条件 patient 分量) |
| 代码 #6 | ADVISORY | `PresenceRegistry` 暴露 `event PresentChanged`(进/出各触发一次;幂等 Add / 跨格 / 不存在的 Remove 不触发)—— 防组合层双写漂移 + 补 `test_o2_presentChanged_firesOnAddAndRemoveOnly` |
| #1 配套 | BLOCKING 必需 | 补玩家占格用例 `test_o2_setOccupant_playerVisibleButNotCountedTowardCap`(可见性 + CAP 分母不变 + 病人/玩家共格互动) |

**登记不修(归后续轮)**:代码 #4 → **新观察项 O-4**(去重键在发号前取 Seq,同
`(kind,tick,patient)` 未发号事件坍缩;`StructureKinds:224` 同 tick 两结构最现实;
归事件流/建造轮)· 代码 #5 → **新观察项 O-5**(CAP 判据对 `PatientId.None` 世界事件
同样生效,满 24 时世界事件被拒;O-2 是首次让 CAP 生产可达的前置;
归组合层接线轮)· 代码 #3 已并入 B3 守卫 · A4/A5/A6 仅登记(见 §一)。

## 三、验证命令(可证伪)

```bash
unity test unity --mode EditMode --filter "EventStreamTest|PatientSpawnerTest|PresenceRegistryTest|IdAuthorityTest" \
  --output unity/Logs/editmode_o123_filtered2.xml     # → 27/27(其中 PresenceRegistry 9 条另跑 fixed.xml 9/9)
unity test unity --mode PlayMode --filter "VerticalSliceTest" \
  --output unity/Logs/playmode_slice_o123.xml         # → 7/7
unity test unity --mode EditMode \
  --output unity/Logs/editmode_full_20261009_o123b.xml  # → 3036/2989/0 红/46 跳/1 inc(基线 3023/2976 +13)
unity test unity --mode PlayMode \
  --output unity/Logs/playmode_full_20261009_o123.xml   # → 98/98/0 红(= 基线)
```

## 四、判别力变异

| 发 | 注入 | 期望 | 实测 |
|---|---|---|---|
| MUT-O1 | EventStream 恢复旧哨兵 `e.Seq == 0` | test_o1 + seq_resetsEachTick | ✅ 恰红 2 条 |
| MUT-O3 | `TryRollbackLastPatientId` 恒 false | o3_lastId + capFull 三方 | ✅ 恰红 2 条 |
| MUT-O2 | `AddPresent` 不占格(删 `_occupiedCells.Add`) | addPresent_readableViaQueryFace + sameCell | ✅ 恰红 2 条 |

恢复:逐发 python 反向恢复,终态锚点(`e.Seq < 0` ×1 · `_nextPatient--` ×1 ·
`_occupiedCells.Add(cell)` ×1)各 1,零 MUT 残留。

## 五、复跑实数(终态 · 评审后修复 + 9 发变异全部恢复后的净态)

| 面 | 产物 | 实测 | 基线对照 |
|---|---|---|---|
| 过滤(三修复全测试) | `editmode_o123_final.xml` | **44/44 绿 / 0 红**(Presence 15 + IdAuthority 15 + EventStream 7 + Spawner 7) | 修复后新增 8(评审前 36 → 44) |
| 全量 EditMode | `editmode_full_20261009_o123final.xml` | **3044 / 2997 绿 / 0 红 / 46 跳 / 1 inc** | 基线 3023/2976 → **+21 零回归**(O-1 1 条 + O-2 15 条 + O-3 4 条 + A3 1 条) |
| 全量 PlayMode | `playmode_full_20261009_o123final.xml` | **98 / 98 绿 / 0 红** | = 基线 |

## 六、判定链

**代码面 lead-programmer:FIX-THEN-APPROVE**(1B + 8A)·
**测试面 qa-lead:FIX-THEN-APPROVE**(3B + 8A)→
**修复全落**(§二之二:甲案占格/在场分离 + B1/B2/B3 补测守卫 + 5 项轻量 advisory;
O-4/O-5 登记不修)→
**判别力变异 9 发全中**(MUT-O1/O2/O3 各恰红 2;评审后 MUT-B1/B2/1/B3/A3 各恰红 1,
MUT-A1 恰红 2 超集;python 反向恢复,锚点各 1,零 MUT 残留)→
**终态复跑全绿**(过滤 44/44 · 全量 3044/0 红 · PlayMode 98/98)→
**APPROVE**(两面必修项全部闭环,可转 Complete)。

## 七、⚠️ 过程记录:meta GUID 格式坑(第三轮实测)

手写 `.cs.meta` 用 `secrets.token_hex(32)`(64 位 hex)⇒ **Unity 拒收**
(Editor.log:「does not have a valid GUID … Asset file will be ignored」)⇒
`PresenceRegistry.cs` 与其测试**整文件被静默忽略**(过滤跑 27 条全绿的假象:
27 = EventStream 6 + Spawner 7 + IdAuthority 14,**恰不含** Presence 9 条)。
**修复**:改用 `uuid.uuid4().hex`(32 位)。**教训**:新建资产的 meta guid 必须 **32 位**;
判「是否被编译」要看**全量 XML 的 discovered 计数**,不能只看绿。
