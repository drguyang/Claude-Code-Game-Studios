# S22-8 甲案落地 · 帐篷(P0 唯一新建例外)+ smoke `#13` 转 test

| 字段 | 值 |
|---|---|
| **卡** | sprint-22 `S22-8`(用户指令「甲案,落地」) |
| **对象** | `tests/smoke/critical_paths.md` `#13`「建造:模块对齐整数格;**帐篷(P0 唯一新建例外)可放置**且不改世界流骨架」 |
| **执行轮** | 2026-11-01 · commit `1825dac7` + 未提交工作树 |
| **前序件** | `production/qa/evidence/s22-8-smoke13-tent-two-options-2026-11-01.md`(勘察 + 两案,不实现) |

---

## 1. 落地内容(逐件)

| # | 件 | 改动 |
|---|---|---|
| ① | `unity/Assets/Sim/World/BuildSlotCatalog.cs` | `SlotType` 闭集 **6 → 7 员**:`Tent = 7` |
| ② | `assets/data/build_modules.json` | +1 条:`module_id 10` / `module_type "Tent"` / `furn_type null` / `occupancy_local [[0,0,0]]` / `cost 5` / `refundable true` / `build_item_key 8` |
| ③ | `assets/data/world_buildslots.json` | +1 格:`(12,0,12)` / `Tent` / `baked_initial false` / `module_id null` —— **骨架外**(医馆槽位簇在 `x∈[1,4]` 与 `x,z∈[6,8]`) |
| ④ | 两侧重烘 | `build_modules.cooked.bytes` **563 → 611 B / 9 → 10 模块**;`world_buildslots.cooked.bytes` **348 → 372 B / 13 → 14 槽** |
| ⑤ | `unity/Assets/Tests/EditMode/ModularBuilding/tent_placement_test.cs`(新) | 3 条判据,读**真 cooked 产物** |
| ⑥ | `production/epics/modular-building/story-008-tent.md`(新) | 第 8 张 story;EPIC `7/7 → 8/8` |
| ⑦ | `tests/smoke/critical_paths.json` `#13` | `none` → `test`,**3 载体**;原 reason **原文保留** |

**没有做的事**:零新 `Kind`、零新写者、零新判定函数 —— 帐篷复用 `PlaceableChecker.Check`
(五条件)与 `StructureWriter.Place`(既有 `IPayloadEncoder` 路径),载荷五字段形状不变。

---

## 2. 判据(3 条,全部读真烘焙产物)

📖 **只读外部夹具声明**(`test-standards.md` 具名义务):读
`unity/Assets/DataCooked/{world_buildslots,build_modules}.cooked.bytes`,零写盘。
**必须读真产物** —— 本条判据的对象就是「出货数据里能不能放下帐篷」;内联假表会让
「数据被改坏」仍然绿。

| 判据 | 钉什么 |
|---|---|
| `test_shippedData_hasTentSlotAndModule` | 恰 **1** 个 Tent 槽 ∧ 掩码**恰** `1 << 7`(≠ 多类型开放)∧ `baked_initial = false` ∧ 恰 **1** 个 Tent 模块 |
| `test_tentPlacement_writesExactlyOneStructurePlaced` | 五条件通过 ⇒ `Place` ⇒ **恰一条** `StructurePlaced` ∧ `Patient = None` ∧ `Tick` 取自调用方 ∧ 槽位不变 |
| `test_tentModule_onDecorSlot_isRejected` | 帐篷放 `Decor` 槽 ⇒ `TypeMismatch` ∧ **零 Append** |

---

## 3. 三条既有哨兵当场转红(这是本卡最有价值的一半)

扩员与加数据**必然**触发它们。三条都**不是缺陷**,而是设计好的「人必须来做决定」出口。
处置口径统一:**复核契约后同步常量** —— 不是「改期望值凑绿」。

| 哨兵 | 症状 | 复核结论 |
|---|---|---|
| `slot_type_vocabulary_test.test_slotTypeVocabulary_nameToOrdinal_isLiteralPinned` | 闭集序列不等(六对 vs 七员) | 词表由 `Enum.GetValues` 派生,机制没问题;补第七对 `("Tent", 7)` + `ClosedSetText` 文案 |
| `world_buildslots_codec_validation_test.test_...maxAllowedMask_isCurrentEnumCeiling` + `...allowedMaskBeyondEnumBits_isRejected` | 上界 `127` vs 实测 `255`;**`128` 从「越界位」变成「合法位」** | `MaxAllowedMask` **派生**自枚举(`(1 << (max+1)) - 1`),写读两端**都无写死常量**;行布局零变化 ⇒ **`schema_version` 不必演进**。上界 127 → **255**;越界负载 128 → **256**;另加 `tentBitMask_isAccepted`(128 放行 = 越界判据的正向半边) |
| `clinic_room_matching_e2e_test.test_catalogue_idsMatchBuildModulesArtifact` | `_catalogue.Count` 期望 9、实测 **10** | 该断言原文就写着「行数漂移 ⇒ 同步本测常量」;新行 `furn_type = null` 不影响本测其余 `GetFurnType` 断言 ⇒ 9 → **10** |

> 第一次全量回归正是被第三条咬住的(`6345 / 6307 / **1** / 37`)。修后复跑全绿。
> ⇒ 「扩员只改一个枚举」是不成立的:4 个文件、3 条哨兵连带。

---

## 4. 证伪轮(4 变异,改 → 跑 → 还原 **同一命令内**完成)

| 变异 | 打哪里 | 结果 |
|---|---|---|
| **M1** 停用五条件 ①(类型匹配) | `PlaceableChecker.Check` | `total=233 / passed=231 / failed=2` —— `test_typeMismatch_returnsTypeMismatch`、**`test_tentModule_onDecorSlot_isRejected`** |
| **M2** `Place` 追加第二条事件 | `StructureKinds.cs` | `total=233 / passed=222 / failed=11` —— 含 **`test_tentPlacement_writesExactlyOneStructurePlaced`** |
| **M3** 掩码上界写死 `127`(退回 A-22 前形态) | `WorldBuildSlotsCookedCodec.ComputeMaxAllowedMask` | `total=233 / passed=215 / failed=18` —— 出货数据带位 7 ⇒ 整表读不进,世界装配系列连锁转红 |
| **M4** `SlotType.Tent` `7 → 8`(位挪走) | `BuildSlotCatalog.cs` | `total=233 / passed=226 / failed=7` —— 词表字面量 + 三条帐篷判据 + `diskMatchesFreshBake` + 两条 codec 上界 |

**还原判据 = SHA-256 逐字节**:4 个被变异文件跑后与跑前一致(`sha256sum -c pre_tent.sha` → **4/4 成功**),
全程 `error CS` = **0**。复算:

```bash
sha256sum -c /home/gu/.claude/jobs/6001633c/tmp/pre_tent.sha   # 4/4 成功
```

---

## 5. 回归与账面(三元组)

**截至 `2026-11-01 10:06:00 CST` · 批次 `S22-8` · commit `1825dac7` + 未提交工作树**

| 平台 | 本次 | 前次(`S22-7` · 09:47) | 差 | 等式 |
|---|---|---|---|---|
| EditMode | `6345 / 6308 / 0 / 37` | `6341 / 6304 / 0 / 37` | **+4** | `6345 − 6341 = 4` = 新增 3 条(tent)+ 1 条(codec `tentBitMask`)✅ |
| PlayMode | `273 / 269 / 0 / 2 / 2` | `273 / 269 / 0 / 2 / 2` | 0 | 零变化 ✅ |

两轮 `error CS` = **0**。ⓘ 中间有一次**非终态**跑(`10:02:16`,`6345 / 6307 / 1`)——
那 1 红是第 3 条哨兵,不是回归;它被修掉后才有上表的终态。

### 5.1 smoke

```bash
python3 tests/smoke/run_smoke.py --results unity/Logs/full-EditMode-tent2.xml unity/Logs/full-PlayMode-tent2.xml
```

实测:**自动 PASS 15 · 自动 FAIL 0 · 未跑 0 · 具名人工 3 · 无名 4**,exit **3**。
`#13` = `PASS(3 载/全绿)`。比对上一轮(`S22-6` 落账后:自动 PASS 14 · 无名 5):
自动 PASS **14 → 15**,无名 **5 → 4**。
**exit 仍是 3** —— 剩余 4 条无名是 `TD-ci-01`(EXTERNAL)与两条生产接线缺口,**不是本卡范围**。

---

## 6. 如实登记的三处

1. **勘察件 §2 的一张表说过头了**:原文写「23 的 `story-001…007` 无一张认领帐篷」。
   实测 `story-001` 实现注 #3 与 `story-006` 实现注 #4 **都提过**,后者还写「(Story 001 数据承载)」。
   **准确的表述是**:提过的只是**实现注里的一句话**,**没有任何 AC 认领**,数据也从未落盘 ——
   这正是「自述顶替门」。story-008 的 Context 已逐字订正这一处(原文保留在勘察件里,不删)。
2. **`cost = 5` / `build_item_key = 8`(木柱)是临时值**,归数值轮。已在 EPIC §未闭项**具名**;
   **不登记为 TD**(与 story-001…007 的目录成本同口径 —— 那些也没进债册)。
3. **`world_size` 未变**(16×1×16),新槽 `(12,0,12)` 在界内;`(12,12)` 落在医馆槽位簇之外,
   符合规则六「骨架外」的措辞 —— 但**未做可达性判据**(帐篷能否走到 = 导航格/关卡设计面,
   归 54 与 6,不在本卡范围)。

---

## 7. 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -rn "Tent" unity/Assets/Sim/World/BuildSlotCatalog.cs | head -3
python3 -c "import json;d=json.load(open('assets/data/build_modules.json'));print(len(d['modules']))"   # 10
python3 -c "import json;d=json.load(open('assets/data/world_buildslots.json'));print(len(d['slots']), [s for s in d['slots'] if s['slot_type']=='Tent'])"  # 14 + 1 条
ls -l unity/Assets/DataCooked/build_modules.cooked.bytes unity/Assets/DataCooked/world_buildslots.cooked.bytes  # 611 / 372
python3 tests/smoke/run_smoke.py --results unity/Logs/full-EditMode-tent2.xml unity/Logs/full-PlayMode-tent2.xml | tail -3
```
