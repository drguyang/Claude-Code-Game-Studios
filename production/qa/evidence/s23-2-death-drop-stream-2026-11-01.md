# S23-2 实施证据 — story-002 掉落清单流派生与回捡(2026-11-01)

| 字段 | 值 |
|---|---|
| **对象** | `production/epics/death-respawn/story-002-drop-manifest-stream-reclaim.md`(Integration · 6h) |
| **轮次** | sprint-23 · `S23-2`(实施 → 证伪 → 回归) |
| **结论** | 生产码两新件 + 消费者回调 + 装配;12 测全绿;证伪 3/3 各自精确转红 |

---

## 1. 流形状(本卡的承重设计判定 —— 轮内就地裁定,已回写 story 卡)

GDD 规则三(`death-and-respawn.md:136-144`)的伪码只写 `DropSpawned`;实施期发现它的
**机械补全必须两事件**:

```
逐件(InventoryOf(player)@死亡tick,instance_id 升序):
    ① DropDespawned { i.id, i.qty, reason = DeathDrop(占序 0) }   —— 消耗持有
    ② DropSpawned  { i.id, spawn_anchor = death_cell, i.key, i.qty }  —— 同 id 落地
```

- 只 Spawn 不消耗 ⇒ fold 报 `DuplicateInstanceId`(未归零实例不得二次出生,
  ADR-006 机制 A 不变量②守卫)**且** 同一件物品在库存与地面各一份(双份)。
- 「29 零铸造」由**同 id 复用**结构性满足 —— 载荷 id = 20 库存行 id 原样(`:142`
  `instance_id = i.id`),`DropEventWriter.RecordSpawnedWithId` 是 20 为已铸 id
  留的通道;29 结算面源码零 `IIdAuthority`(测试钉住,含剥注释扫描)。
- 写入通道 = 20 的唯一写者 `DropEventWriter`(29 不直接 Append)—— 死亡散落与
  17 采集**共用同一生产实例**(构造点前移到死亡消费侧装配段,17 段复用)。
- `reason = 0` 是 **R-2 枚举未定稿**下的首个生产取值占序登记(R-2 定稿挪号须同批改)。

## 2. 落盘件

| 文件 | 内容 |
|---|---|
| `Sim/DeathDropSettlement.cs`(新) | 结算面:逐件 Despawn→Spawn(同 id);投影来源**后置绑定**(`BindProjectionSource`,未绑定即结算 ⇒ 抛;二次绑定 ⇒ 抛);每 tick 每件两事件,空背包零事件 |
| `Sim/DeathDropReclaimPolicy.cs`(新) | 回捡约束(20 R7 的整数格面):未归属 ∧ 流里最近出生锚 ∧ Chebyshev ≤ 宽容半径(注入参,实值归 4 装配轮/数值轮);倒序扫流取锚(「消耗→再落地」天然取最新) |
| `Sim/DeathCandidateConsumer.cs` | 可选 `dropSettlement` 回调(null = 001 行为不变);**只在 `Written` 后**以同格同 tick 调用(结算段次位;deathCell 只查一次,载荷与散落同值) |
| `Gameplay.Boot/CompositionRoot.cs` | `DropEventWriter` 构造前移 + 结算构造 + 投影后置绑定 + 回调转序适配(λ) |
| `Tests/EditMode/DeathRespawn/death_drop_stream_test.cs`(新,12 测) | 见 §3 |
| story-002 卡 | 实施判定回写 Implementation Notes |

## 3. AC → 测试映射(12 条,全绿 `Logs/s232-drv5.xml` @ 2026-11-01)

| AC | 测试 |
|---|---|
| AC-29-02(半边) | `test_settle_emitsDespawnThenSpawnPerItem_anchorIsDeathCell`(序 + 锚)+ `test_settle_anchorTakesCellNotContinuousPosition`(签名无 float/Vector3) |
| 空背包 | `test_emptyInventory_settleWritesNothing`(零事件) |
| 清单派生 | `test_afterSettle_rebuiltProjection_playerEmpty_groundNoOwner` + `test_replay_sameSequence_identicalCanonicalForm`(重放逐位) |
| instance_id 来源 | `test_settlement_zeroMinting`(剥注释源码扫描 —— 首版被文件头注记自命中,已修) |
| 位置非快照 | `test_dropSpawnedPayload_integerOnly`(四字段整数域反射) |
| 回捡约束 | `test_reclaim_offCell_isRejected` / `test_reclaim_atCell_withinTolerance_isAllowed`(含 d∞ == R 边界)/ `test_reclaim_secondClaim_rejected`(幂等 + 库存恢复经 20) |
| 消费者接线 | `test_consumerWired_settlementRunsAfterPlayerDied` + `test_consumerUnwired_zeroSettlement_001BehaviorPreserved` |

**首轮三红(夹具 bug,如实登记)**:①② 计数把 `GivePlayer` 播种的 2 条 `DropSpawned`
算进断言(改增量断言);③ 零铸造扫描没剥注释,被文件头自己的「不触 IIdAuthority」注记
命中(注释即接线,F6 同族 —— 剥注释后重扫)。**生产码零改**。

## 4. 证伪轮(S12-4 · 同命令 改→跑→还原 + SHA-256 逐字节)

| 变异 | 靶点 | 实测 |
|---|---|---|
| **M-A** 结算锚点改错格 `(9,9,9)` | `DeathDropSettlement.cs` RecordSpawnedWithId 实参 | ✅ 红 3 条 —— anchor 断言 / 接线断言 / **回捡正例**(锚错 ⇒ 玩家不在锚旁 ⇒ 策略拒;红集合 = 锚点依赖面,判别力真实);RESTORE-A-OK |
| **M-B** 策略判距恒真 | `DeathDropReclaimPolicy.cs` ③ | ✅ 恰红 `offCell`(负例);RESTORE-B-OK |
| **M-C** 消费者接线摘除(`Invoke` → no-op) | `DeathCandidateConsumer.cs` | ✅ 恰红 `consumerWired`;RESTORE-C-OK |

残留 `grep FALSIFY` 零命中。日志:`unity/Logs/s232-fals{A,B,C}.{xml,log}`。

## 5. 回归(三元组 · 终值)

| 轮 | 元素 | 值 |
|---|---|---|
| **截至时刻** | | 2026-11-01 **14:00:25 CST**(EditMode 13:59:44) |
| **批次** | | S23-2(结算面/策略/消费者回调/写者 long 重载 + 12 新测) |
| **commit** | | `29988749` + 工作树(提交前) |

| 套件 | 结果 | 前次(S23-1 终值) | 等式 |
|---|---|---|---|
| EditMode | `total=6391 passed=6354 failed=0 skipped=37` | `6379 / 6342 / 0 / 37`(13:06:29) | `6391 − 6379 = 12` ✅ |
| PlayMode | `total=273 passed=269 failed=0 skipped=2`(+2 Inconclusive 存量) | `273 / 269 / 0 / 2`(13:07:10) | `±0` ✅ |

> **中间态如实登记**:首轮全量 EditMode(13:52:22)`failed=3` —— 三个**铸造点独占门**
> (`AC-17-05` IL 站点扫描 / `AC-20-01` / `AC-21a-63`)当场拦下结算面的
> `new ItemInstanceId(row.InstanceId)`(包装投影里的裸 long)。**门是对的** —— struct 构造点
> 唯一落发号权威的 IL 不变量,正是「第二 id 出生源」的藏身形态所在;正确修法 = `DropEventWriter`
> 加**裸 long 重载**(判据与 Append 与 struct 版逐字同构,payload 本吃 long),**不扩允许集**。
> 这是本 sprint 第二次「门在起作用」的实证(S23-1 的门链清单之后)。复跑终值本表。

复算命令:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/s232-final-em2.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/s232-final-pm2.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
# 铸造点门复算(期望零越界):
grep -rn "new ItemInstanceId(" Assets/Sim --include=*.cs | grep -v "IdAuthority" | wc -l   # 期望 0
```

## 6. 已知边界(不借绿)

- **4 的拾取意图管线未接线**(登记项):`DeathDropReclaimPolicy` 的生产消费方 = 4 的
  拾取装配轮(轮到时与表现态判距串联);本卡按卡面口径以策略直测。
- **读档位置 = 格锚 + 确定性格内偏移**(ADR-023 ⑦):表现态面,归 7a/23 —— 本卡只断
  载荷零连续坐标(§3 结构判据)。
- **quality 不在 `DropSpawnedPayload`**(R18 账):死亡掉落携带的品级随实例在
  `ResourceHarvested` 里,流上可重建;本类不扩载荷(扩 = ADR-009 窄修订)。
- **AC-29-02 仍记半边**:全链绿(真病死于真玩家 → 真散落)与 S23-1 同口径,归 G-1 门行收口核。
