# S22-5 · `TD-processing-06` 跨存档往返夹具 · 执行原件

| 字段 | 值 |
|---|---|
| **卡** | sprint-22 `S22-5`(= `TD-processing-06` 的解除动作) |
| **对象** | 「`Craft` 事件经 `compose → 写盘 → 读回 → 重放` 后,`CompleteTick` 与直算逐位相同」 |
| **执行轮** | 2026-11-01 · 用户指令「修理这些遗留(PASS WITH WARNINGS / APPROVED WITH CONDITIONS / C1–C3)」 |
| **引擎** | Unity 6000.3.24f1 · Linux · batchmode |
| **判据载体** | `unity/Assets/Tests/EditMode/PersistenceService/craft_save_roundtrip_test.cs`(1 条) |

---

## 1. 债的前提被实测推翻

债正文(`docs/tech-debt-register.md` §TD-processing-06)写:

> 「真正的往返需要 7a 的 `SaveComposer` / `SaveReader` 真装配 …… **`SaveReader` 全仓不存在**
> ⇒ 造样本即造一个不存在的路径」

实测:

```bash
find unity/Assets -name "SaveReader*"          # 零命中 —— 债说的是**类名**
grep -n "public void Load(" unity/Assets/Sim.Codec/SaveService.cs   # :807 —— **能力**在
```

⇒ **类名不存在 ≠ 能力不存在**。读路径住 `SaveService.Load`,且 ADR-034 实现轮之后载荷段已落地,
`save_replay_roundtrip_test.cs` 早已用同款装配做 Checkpoint → Load → Restore。
**本卡因此不走卡面预置的「转勘察卡」分支** —— 那个分支的前提不成立。

---

## 2. 判据设计(为什么不是一条恒真夹具)

| 编号 | 断言 | 防的假绿形态 |
|---|---|---|
| A | 读回事件条数 == 存前条数;`Craft` 行数仍为 3 | **F4 空洞正控** —— 少了这条,「两边都空」时下面的等值断言恒真 |
| B | 24 个采样点 × 2 actor,`TryCompleteTickOf` 的「点过火没有」与完成时点逐点相同 | 主判据(往返保真) |
| C | 字面量哨兵:`100 + 50 ⇒ 150` · `200 + 40 ⇒ 240`;`BusyActorCount` 在 120 ⇒ 2、160 ⇒ 1 | **F3 自指判据** —— 若写成 `occ.CompleteTick == StartTick + DurationTicks`,变异时两边一起动 |
| D | `BusyActorCount` 逐采样点与直算相同 | AC-18-23 的往返半边 |

---

## 3. 证伪轮(2 变异,2/2 红,`SaveComposer.cs` SHA-256 逐字节还原)

| 变异 | 内容 | 实测 |
|---|---|---|
| **M1**(存在性) | `Compose` 里把世界流的 `Craft` 全部丢弃 | **A 红**:`Expected: 3 But was: 0` |
| **M2**(值) | 行数不变,把每条事件的**信封 tick 整体 +1** | **B 红**:`tick 100 · actor 1:「点过火没有」Expected: True But was: False` |

> **M2 的价值**:它是 **B 面单独转红、A 面仍绿** —— 证明等值断言不是「A 面存在性」的附庸,
> 往返保真这一面有独立判别力。

还原:`SaveComposer.cs` 与 `HEAD` 逐字节一致(`git status --short` 零输出),
`grep -c "FALSIFY-"` = **0**。

---

## 4. 回归(三元组)

**截至 `2026-11-01 08:33:56 CST` · 批次 `S22-5` · commit `32e15f3b` + 未提交工作树**

| 平台 | 本次 | 前次(本日 08:15–08:17) | 差 | 等式 |
|---|---|---|---|---|
| EditMode | `6331 / 6294 / 0 / 37` | `6330 / 6293 / 0 / 37` | +1 / +1 | `6331 − 6330 = 1` = 新件 `[Test]` 数 ✅ |
| PlayMode | `271 / 267 / 0 / 2 / 2` | `271 / 267 / 0 / 2 / 2` | 0 | 无新增 PlayMode 件 ✅ |

两轮 `error CS` = **0**;`failed = 0`。复算见 §5。

---

## 5. 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -projectPath "$PWD/unity" \
  -runTests -testPlatform EditMode \
  -testFilter "DaYiJingCheng.Tests.PersistenceService.CraftSaveRoundtripTest" \
  -testResults "$PWD/unity/Logs/em-craft-roundtrip.xml" \
  -logFile "$PWD/unity/Logs/em-craft-roundtrip.log"
grep -c "error CS" unity/Logs/em-craft-roundtrip.log        # 期望 0
python3 tools/debtcount/debtcount.py --check; echo "exit=$?" # 期望 0
```

---

## 6. 本卡**不**覆盖(不借绿)

- **跨构建逐位一致** —— `TD-ci-01` EXTERNAL(ADR-012 三格矩阵,凭证在用户侧)。
  本卡只做**单构建内**的落盘往返,不得读成 AC-18-15。
- **世界流折叠** —— 世界流不折叠(ADR-008 §六),本件没有覆盖「折叠段对 `Craft` 的影响」,
  因为该形态不存在。
