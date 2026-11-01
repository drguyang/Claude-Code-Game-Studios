# QA 签核 — sprint-22(2026-11-01)

| 字段 | 值 |
|---|---|
| **对象** | `production/sprints/sprint-22.md`(8 卡:Must 4 · Should 2 · Nice 2) |
| **执行轮** | 2026-11-01 · sprint-22 收口 |
| **三元组** | 截至 `2026-11-01 10:06:00 CST` · 批次 `S22-8` · `1825dac7` + 工作树(= `04a29f0d`) |
| **判定** | **APPROVED WITH CONDITIONS**(C1–C4) |
| **本件不代记绿** | sprint Exit 判据的达成与否见 `sprint-22.md` §收口判定(本件**不裁决** Exit) |

---

## 一、逐卡证据形态核账

| 卡 | 类型 | 要求的证据 | 实测载体 | 结论 |
|---|---|---|---|---|
| `S22-1` | Logic | E1 + E2 | `EditMode/Foraging/node_remaining_prefix_test.cs`(15 测)+ 证伪 3 条各 exact 1 红 | ✅ 齐 |
| `S22-2` | Integration | E1 + E2 + `[L]`×3 | `harvest_action_atomicity_test.cs`(8)+ `foraging_float_gate_test.cs`(8)+ 证伪 M1–M5 | ✅ 齐(3 条 `[L]` 仍 EXTERNAL,见 C3) |
| `S22-3` | 载体 | E1 + E2 + E5 | `PlayMode/boot_e13_preload_chain_test.cs`(2)+ 证伪 3 变异;smoke `#1` 实跑 PASS | ✅ 齐 |
| `S22-4` | 载体 + 集成 | E1 + E2 + E5 | `random_event_spawn_chain_test.cs`(6,合成输入)+ 证伪 4 变异;`#14` PASS | ✅ 齐 |
| `S22-5` | 测试卫生 | E1 + E2 | `craft_save_roundtrip_test.cs`(1)+ 证伪 2 条(整组丢流 / 信封 tick +1) | ✅ 齐 |
| `S22-6` | 裁定 | E3 | `s22-6-smoke10-conflict-2026-11-01.md` 两案对照 + 裁后 `#10` 实跑 PASS(2 载) | ✅ 齐(零产品码 ⇒ 证伪轮 N/A,逐卡已注) |
| `S22-7` | 测试卫生 | E3 + E1 | `method_line_budget_test.cs`(8)+ `save_slot_contract_test.cs`(+1)+ 证伪 M1′/M2/M3′ | ✅ 齐 |
| `S22-8` | 勘察 →(用户裁定后)落地 | E3 + E1 + E5 | `tent_placement_test.cs`(3)+ 证伪 4 变异;`#13` PASS(3 载) | ✅ 齐 |

`FALSIFY-` 残留:`grep -rn "FALSIFY-" unity/Assets --include=*.cs | wc -l` ⇒ **0**。

---

## 二、回归与 smoke(可复算)

| 面 | 值 | 复算 |
|---|---|---|
| EditMode | `6345 / 6308 / 0 / 37` | `unity/Logs/full-EditMode-tent2.xml` |
| PlayMode | `273 / 269 / 0 / 2 / 2` | `unity/Logs/full-PlayMode-tent2.xml` |
| 等式 | `6345 − 6341(前次 S22-7) = 4` = 本轮新增 4 条(tent 3 + codec 1) | ✅ |
| smoke | 自动 PASS **15** · FAIL **0** · 人工 3 · 无名 **4**,exit 3 | `production/qa/smoke-2026-11-01.md` |
| 债册 | `--check` exit **0**;未闭 P0 面 **0**(规划期亦 0 ⇒ 不涨) | `python3 tools/debtcount/debtcount.py --check` |

---

## 三、条件

### C1 —— 台账行未同批更新(本件收口时已补)

- **症状**:`smoke-carrier-backlog-2026-10-31.md` 的 `#1` / `#14` / `#13` 三行仍是**计划态**
  (#13 停在「待用户裁」),§三 归类统计仍是规划期的 5 类。
- **DoD 原文**:「台账的对应行**同批**更新(类型列 → 已解除 / 仍缺)」。
- **处置**:2026-11-01 收口轮**已补**(三行各加 🔁 落地注 + §三 统计改为「已解除 4 / 余 4」,
  并附订正块保留原文)。
- **⚠️ 定性**:这不是「补了就算了」—— 它是 DoD 的一项**在收口当日才被满足**,
  详见 `sprint-22.md` §收口判定 的非绿面第 3 条。

### C2 —— `S22-1` 的两条 AC 未闭

- `AC-17-06` 🟡 只落**谓词半**(「余量不足 ⇒ 零事件」那一跳由 `S22-2` 的动作路径承载);
- `AC-17-13` 🟡 `BLOCKED-BY-OQ-17-2`(差分值未裁,数值轮)。
- **解除动作**:前者随发起路径一并验(`CanCarry` / `CanHarvest` 真值进 `Resolve`);后者归数值轮。

### C3 —— `[L]` 三条仍 `EXTERNAL`

- `AC-17-07b` / `AC-17-12` / `AC-17-13b`:载体归 **42 的「三类失败反馈」UX Flag ③**,未交付。
- **不勾、不记绿**(承 sprint-21 同口径)。

### C4 —— 本 sprint 新立债仍开

- `TD-skeuo-01`(九宫格黑块归因 / 缺尺寸下限门)`open`,`P0 面 = N`;
- `TD-foraging-03` 本 sprint **立后即结**(`DECAY_RATE 1/40 → 1/80`)。
- **无新增 P0 面债**(未闭 P0 面 0 → 0)。

---

## 四、签核结论

**APPROVED WITH CONDITIONS** —— 8/8 卡的证据形态逐条齐备(E1/E2/E3/E5 各按卡面判定),
回归零失败、证伪轮全红、账面无新增 P0 债、smoke 无自动失败。

**不得读成**:「sprint-22 已达成 Exit」或「smoke 22 条全绿」。两者都不成立:
前者见 `sprint-22.md` §收口判定(Exit 判据的实测结论),后者见 `smoke-2026-11-01.md`(无名 4 条)。

---

## 五、复算命令(自包含)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -rn "FALSIFY-" unity/Assets --include=*.cs | wc -l          # 期望 0
python3 tools/debtcount/debtcount.py --check; echo "exit=$?"      # 期望 exit=0
python3 tests/smoke/run_smoke.py --results unity/Logs/full-EditMode-tent2.xml unity/Logs/full-PlayMode-tent2.xml | tail -4
python3 -c "import json;d=json.load(open('tests/smoke/critical_paths.json'));print([x['id'] for x in d['items'] if x['status']=='none'])"   # 期望 [8, 9, 18, 22]
```
