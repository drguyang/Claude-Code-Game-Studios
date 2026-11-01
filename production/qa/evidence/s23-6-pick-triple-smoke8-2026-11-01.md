# S23-6 实施证据 — sprint-23 拾取三段式(smoke #8 载体)(2026-11-01)

| 字段 | 值 |
|---|---|
| **对象** | sprint-23 `S23-6`(smoke `#8` 载体 · S 级;依赖 S23-2 已兑现) |
| **轮次** | sprint-23 · `S23-6`(实施 → 证伪 → 回归) |
| **结论** | 编排面 + E 路由 + 生产装配;10 测;smoke #8 `none → test`;证伪 3/3 各自精确转红 |

---

## 1. 落盘件

| 文件 | 内容 |
|---|---|
| `Sim/DropClaimOrchestrator.cs`(新) | 拾取三段式编排:意图(E 沿)→ 当下判距(经流确立格,调用方传入)→ 宽容半径(注入);候选 = 投影 `InstancesOf(NoOwner)`(id 升序),**选中 = 第一可拾**(确定性;「就近」需锚点二次扫流 + tie-break,登记为后续轮可选项);claim 经 `DropEventWriter.RecordClaimed` 裸 long 重载(**零铸造**,AC-17-05 同 S23-2 惯例);拒收零事件 |
| `Sim/World/DropEventWriter.cs` | `RecordClaimed` 裸 long 重载(判据与 struct 版逐字同构 —— 投影行 id 原样,免 Sim 侧 struct 包装) |
| `Gameplay.Boot/InteractClaimRoute.cs`(新) | E 按下沿路由:**拾取 claim 优先 → 采集 → 查体**(独立类 —— 路由序有夹具,非注释接线,F6 纪律) |
| `Gameplay.Boot/CompositionRoot(.Services)` | 编排面生产装配(`PickToleranceCells = 1` 装配常量,实值归数值轮)+ 服务袋成员 |
| `Gameplay.Boot/BootRoot.cs` | 4.5a claim 钩子改走 `InteractClaimRoute.TryConsume`(拾取优先,拒 ⇒ 零事件落回采集桥) |
| `Tests/EditMode/DeathRespawn/drop_claim_pick_test.cs`(新,10 测) | 见 §3;走**生产装配**的编排面(`_bag.DropClaimOrchestrator`) |
| `tests/smoke/critical_paths.json` + `critical-paths.md` | `#8` `none → test`(双载体正例/负例)+ md 侧同批注 |

## 2. 轮内就地裁定(已回写)

- **E 路由序 = 拾取优先 → 采集**:掉落物是玩家的「拿取」affordance,优先于环境资源点;
  拾取拒(无候选/出半径)⇒ 零事件落回采集链(S22-9 的「闸门拒绝也算消费」语义不变)。
- **选中 = 第一可拾(id 序最小)**:确定性由投影序直接保证;「就近选择」需二次扫流 +
  同距 tie-break,登记为后续轮可选项(不阻塞本卡判据)。

## 3. AC → 测试映射(10 条)

| 判据 | 测试 |
|---|---|
| smoke #8 正例(三段全链) | `test_smoke8_pickTriple_inRange_claimsGroundInstance`(**先断 seed 生效** —— 禁空集正控;载荷 id = 播种行原样;库存恢复经 20 fold) |
| smoke #8 负例(离屏拒非崩溃) | `test_smoke8_offRange_rejected_zeroEvents`(独立负控:先断候选在场,再断拒 + 零事件 + 归属不变) |
| 宽容半径边界 | `test_smoke8_boundaryTolerance_atRadius_allowed_beyond_rejected`(距离 1 == 容差允许;距离 2 拒 —— 半径 = 装配常量,改值须同批) |
| 幂等 | `test_secondClaim_sameInstance_rejected_idempotent` |
| 空场 | `test_noGroundCandidates_false_zeroEvents` |
| E 路由 | `test_route_claimConsumes_press` + `test_route_claimReject_fallsThrough_toHarvest`(落回半边;采集真消费正例归 S22-9 夹具)+ `test_route_bothUnwired_false` |
| 结构判据 | `test_claimOrchestrator_zeroMint_zeroDirectAppend`(剥注释:零 `new ItemInstanceId(` / 零 `.Append(`) |
| 参数守卫 | `test_claimOrchestrator_invalidArgs_throw` |

## 4. 证伪轮(S12-4 · 同命令 改→跑→还原 + SHA-256 逐字节)

| 变异 | 靶点 | 实测 |
|---|---|---|
| **M-A** 容差失守(`_tolerance` → `+100`,判距恒过) | `DropClaimOrchestrator.cs` policy 实参 | ✅ 恰红 **2** —— `offRange`(离屏拒)+ `boundaryTolerance`(半径外拒);正例/幂等仍绿;RESTORE-OK `aa0efe34b277` |
| **M-B** 假消费(返回 true 不入流) | `DropClaimOrchestrator.cs`(RecordClaimed 剥除) | ✅ 红 4 —— `pickTriple`(恰一条断言)+ `secondClaim`(首拍假真 ⇒ 二拍未拒)+ `boundary` + `route_claimConsumes`;红集合 = 「claim 真的落流」的依赖面;RESTORE-OK `aa0efe34b277` |
| **M-C** 路由短路(claim 结果被丢弃) | `InteractClaimRoute.cs` | ✅ 恰红 `route_claimConsumes`(拾取优先序的载体);⚠️ 首版变异写成 `=> { … }` 非法块体 ⇒ 编译失败无结果,**重写为合法锚点**(`(false) //` 丢弃)后重跑 —— 首版无效变异如实登记;RESTORE-OK `de509bfee4a4` |

三个红集合各自覆盖其守卫。残留 `grep FALSIFY` 零命中。
日志:`unity/Logs/s236-fals{A,B,C}.{xml,log}`。

## 5. 回归(三元组 · 终值)

| 轮 | 元素 | 值 |
|---|---|---|
| **截至时刻** | | 2026-11-01 **15:22:23 CST**(EditMode)/ 15:23:1x CST(PlayMode) |
| **批次** | | S23-6(编排面 / 裸 long 重载 / E 路由 / 装配 + 10 新测;含 S23-5 的闭包入集修复) |
| **commit** | | 本卡提交(见 git log S23-6) |

| 套件 | 结果 | 前次(S23-5 批次) | 等式 |
|---|---|---|---|
| EditMode | `total=6425 passed=6388 failed=0 skipped=37` | `6415 / 6377 / 1 / 37`(15:07:31) | `6425 − 6415 = 10`(新测)✅;passed `6388 − 6377 = 11`(10 新测 + 1 闭包修复)✅;**failed 1 → 0** = S23-5 的闭包入集修复获干净全量确认 ✅ |
| PlayMode | `total=273 passed=269 failed=0 skipped=2` | `273 / 269 / 0 / 2` | `±0` ✅ |

**smoke 实跑**(`run_smoke.py --results s236-em.xml s236-pm.xml`):**自动 PASS 16 · FAIL 0 ·
未跑(需结果件)0** · 具名人工 3 · 无名 3(#9/#18/#22,EXTERNAL/运行期判据)—— **#8 由
`none` 转 `test` 且 PASS(2 载/全绿)**,即 sprint-23 DoD 预言的 **15 → 16**(SMOKE-EXIT=3 =
尚有 6 条无可执行判据,如实登记,不是零)。

复算命令:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/s236-em.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/s236-pm.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
cd .. && python3 tests/smoke/run_smoke.py --results unity/Logs/s236-em.xml unity/Logs/s236-pm.xml | tail -3
```

## 6. 已知边界(不借绿)

- **宽容半径实值 = 1**(装配期常量):policy 文档「实值归 4 装配轮/数值轮」口径不变;
  数值轮改值须同批改边界测试(值与判据是同一裁定)。
- **拾取的表现层**(高亮/动画/音效)不在本卡 —— 4 的表现侧与 42 同格,只渲染不持状态。
- **路由正例的采集半**未在本件真链验证(采集真消费 = S22-9 夹具;本件钉落回半边)。
- **模态抑制**:E 分支整体被 `InteractionInputDriver` 的 allowed 前置(既有 F-4.2 面),
  拾取与采集/查体同被抑制 —— 本卡未新增判据面(行为面由 4 的既有夹具承载)。
