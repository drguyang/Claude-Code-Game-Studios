# 逐 BLOCKING 对账件 —— world-ecozones(系统 6b)

> **用途**:补齐 `production/epics/index.md` §越序实现登记 的**缺口 ①a**。
> **编制**:2026-10-02 · **编制方式 = 逐条独立复核**(非转录 commit message)
> **HEAD**:`1a884c9` · **引擎**:Unity 6000.3.24f1

## 结论摘要

| # | 原判定 | 实测判决 |
|---|---|---|
| B1 | `PoiStateChanged` 未走 `Sim.Codec` | 🔴 **未修 —— 降级为 TODO 注释**(缺口 ①b,本件坐实) |
| B2 | 13 个 BLOCKING AC 未勾选 | ⚠️ **已勾选,但缺同批复跑取证**(缺口已由 `bc7657e`/`c683aa5` 补) |
| B3 | 无 host-only write gate | ✅ **已修**(但**负向夹具缺** —— 缺口 ①c) |
| B4 | Discovery gate 未实现 | ✅ **已修** |

**4 条中 2 条确认修复,B1 未修(仅 TODO 化),B2 形式已闭。**
故本 epic 维持 `In Review`,**不转 Complete**。

---

## 🔴 B1 —— `PoiStateChanged` 未走 `Sim.Codec`(**未修,降级 TODO**)

- **原判定**(`bb477e6`):`PoiStateChanged` 载荷未走 `Sim.Codec`。
- **修复落点**(`43400dc` 自述):「添加 TODO 注释(需 IBlobPool 基础设施)」——
  **自陈的修法即「加注释」,非修复**。
- **实测**:TODO 仍在,`Append` 仍手搓 `PayloadRef`:

```
unity/Assets/Sim/World/PoiStateMachine.cs:105  TODO: 接入 Sim.Codec 真实 payload_schema(需 IBlobPool 支持)
unity/Assets/Sim/World/PoiStateMachine.cs:131  TODO: 接入 Sim.Codec 真实 payload_schema(需 IBlobPool 支持)

unity/Assets/Sim/World/PoiStateMachine.cs:104:
    var payloadRef = new PayloadRef(blobId: poiId, offset: (int)toState, length: 8);
```

两处语义冲突(与 modular B4 残留 ① 同型):
- `blobId` 位置放 `poiId` —— **业务 id 当 blob 池索引**;
- `offset` 位置放 `newState` —— **枚举值当字节偏移**,与 `PayloadRef` 契约
  (`Offset` = 字节偏移,非业务字段)冲突。

- **「前置未就绪」的免责不成立** —— codec **已在库**:

```
unity/Assets/Sim.Codec/PayloadCodec.World.cs:378  Encode(in PoiStateChangedPayload p)
unity/Assets/Sim.Codec/IBlobPool.cs               (已在库)
design/registry/entities.yaml:2005                payload_schema: "actor_id: i32; cell: …"   ← 本 Kind 有登记
```

⇒ **可接而未接**,非被前置阻塞。

- **判决**:🔴 **未修**。`43400dc` 的处置是「把缺陷降级为 TODO 注释」——
  而 `production/epics/index.md` 已明文判定该处置的**免责不成立**。本件坐实该判定。

## B2 —— 13 个 BLOCKING AC 未勾选

- **原判定**:`story-003` / `story-004` 共 13 个 BLOCKING AC 未勾。
- **修复落点**:`43400dc` 更新两 story 的 AC 状态。
- **实测**(HEAD):
  ```
  story-003-poi-state-machine-and-world-stream.md:              已勾 7 / 共 8
  story-004-chunk-activation-and-consumption-boundary.md:       已勾 7 / 共 7
  ```
- **判决**:⚠️ **形式已闭,但原处置有取证缺口** —— `43400dc` 勾 AC 时**未同批复跑**。
  该缺口已由 `production/qa/evidence/editmode-full-rerun-2026-10-02.md` 补齐
  (WorldEcozones **87/87 逐例全绿**)。
  ⚠️ `story-003` 剩余 1 条未勾 = `entities.yaml` ↔ kindgen 路由一致性(A1–A5),
  与缺口 ①a 同类,可同批处理。

## B3 —— 无 host-only write gate(**已修,但负向夹具缺**)

- **原判定**:`PoiStateMachine` 无「仅主机可写」门,客户端可写世界流。
- **修复落点**:`IEventAuthority` 加 `IsHost`,`TryAdvance` 入口判。
- **实测**:
  ```
  unity/Assets/Sim.Contracts/Abstractions.cs:61:  bool IsHost { get; }
  unity/Assets/Sim/World/PoiStateMachine.cs:83:   if (!_eventAuthority.IsHost)
  ```
- **判决**:✅ **已修**(接口落 `Sim.Contracts` 边界程序集,门在写入口)。
- 🔴 **但负向判据未执行(缺口 ①c)**:`AC-6-26a [B]` 要求「客户端调用写通道 ⇒ 断言失败/拒写」,
  而测试夹具恒真:
  ```
  unity/Assets/Tests/EditMode/WorldEcozones/poi_state_machine_test.cs:21:
      public bool IsHost => true;
  ```
  全文 `IsHost` **仅 1 处**,13 个 `[Test]` 中**无一**注入 `IsHost => false` 验证拒写路径
  ⇒ **只有正路径,负向判据未执行**。

## B4 —— Discovery gate 未实现

- **原判定**:POI 发现门未实现。
- **修复落点**:新增 `PoiStateMachine.TryDiscover`。
- **实测**:
  ```
  unity/Assets/Sim/World/PoiStateMachine.cs:70:  public PoiStateTransferResult TryDiscover(int poiId, long tick = 0)
  ```
- **判决**:✅ **已修**。

---

## 验证命令(可复现)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -n "TODO\|new PayloadRef" unity/Assets/Sim/World/PoiStateMachine.cs
grep -n "PoiStateChangedPayload" unity/Assets/Sim.Codec/PayloadCodec.World.cs
grep -n "IsHost" unity/Assets/Sim/World/PoiStateMachine.cs unity/Assets/Sim.Contracts/Abstractions.cs
grep -c "IsHost" unity/Assets/Tests/EditMode/WorldEcozones/poi_state_machine_test.cs   # → 1
grep -n "TryDiscover" unity/Assets/Sim/World/PoiStateMachine.cs
```

**测试证据**:全量 EditMode batchmode 复跑 WorldEcozones **87/87 全绿**
—— `production/qa/evidence/editmode-full-rerun-2026-10-02.md`。
⚠️ **87/87 绿不覆盖 B1 / B3 负向** —— 该 fixture 不含「载荷经 codec」与「客户端拒写」判据。

## 转 Complete 的前置

1. **B1 闭合路径已改判(2026-10-02)** —— 原写「接 `Sim.Codec`(或确认引用边)」,
   该措辞**有误**:`Sim` → `Sim.Codec` 引用边**已由 ADR-025 §①:111 禁止且 b2 门强制**,
   不是待裁项。正确路径 = **ADR-029 的 `IPayloadEncoder`**(住 `Sim.Contracts`),
   由 `Sim.Codec` 实现 —— 见 `docs/architecture/adr-029-payload-encoder-abstraction.md`;
2. **B3 补 `IsHost => false` 负向夹具**(缺口 ①c);
3. `story-003` 最后 1 条 AC(`entities.yaml` ↔ kindgen 一致性)勾选。
