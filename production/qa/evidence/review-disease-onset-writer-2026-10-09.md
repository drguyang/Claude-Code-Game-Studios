# 评审原件 — ADR-030 ② 9 的 DiseaseOnset 写者(PatientSpawner)· 2026-10-09

> **对象**: `unity/Assets/Sim/DiseaseSimulation/PatientSpawner.cs`(新文件,69 行)·
> `unity/Assets/Tests/EditMode/DiseaseSimulation/patient_spawner_test.cs`(新文件,5 条测试)·
> `unity/Assets/Tests/PlayMode/vertical_slice_test.cs` 改动部分(病人腿转真验证 + `fullCoreLoop` 三条事件链)
> **轮次**: 双代理评审**恰一轮**(用户指令)
> **判定**: 代码面 **APPROVE**(0 BLOCKING + 4 ADVISORY)· 测试面 **APPROVE**(0 BLOCKING + 4 ADVISORY)
> → 去重 6 项 → 同批修复全落 → 补 2 发修复判别力变异恰红 → 复跑绿 → 转 APPROVE
> **交付件**: `PatientSpawner.cs`(9 的 DiseaseOnset 写者)· `patient_spawner_test.cs` 5 条 AC ·
> `vertical_slice_test.cs` 病人腿转真验证(零桩)

## 一、原判定(两代理逐条)

### 代码面(lead-programmer): APPROVE — 0 BLOCKING + 4 ADVISORY

| # | 级 | 位置 | 问题 |
|---|---|---|---|
| A1 | ADVISORY | `PatientSpawner.cs:59-66` | **先发号后 Append**: `SpawnNext` 先调 `_idAuthority.NextPatientId()` 再 `_sink.Append`。若 `EventStream.Append` 因 CAP 满抛异常,ID 已分配但事件未写入 ⇒ ID 空洞(跳号)。当前不可达(生产路径 `PresentCount` 恒 0,CAP 永不触发),但顺序脆弱 |
| A2 | ADVISORY | `PatientSpawner.cs:61` | **载荷 `seq` 恒 0**: `DiseaseOnsetPayload` 的 `seq` 字段恒为 0,由 `EventStream` 发号的是 header `Seq`。载荷 `seq` 是占位,与 `PrescribeFlow.cs:330` 逐字相同(承现状)。该占位已被 10 的 `test_ac1039_seqIsExplicitlyPlaceholder` 与 11 的 review n2 广泛登记 |
| A3 | ADVISORY | `PatientSpawner.cs:49` | **`SpawnNext` 无入参校验**: `diseaseId` / `tick` 无范围检查。Sim 侧无病种域常量,校验无处可依。登记为观察项 |
| A4 | ADVISORY | `PatientSpawner.cs:31-41` | **ctor 无 doc comment**: 与 `EventStream.cs:39` / `PrescribeFlow.cs:382` 同款先例。登记为观察项 |

**核过零问题**: ① 门 A 合规(`Sim/` 内零 `new PayloadRef(`,经 `IPayloadEncoder` 编码)✅
② `StreamRouting.Of(DiseaseOnset)` → `StreamId.History` ✅ ③ `patient_seed = unchecked((long)SplitMix64.Hash(...))`
按位承载(不截断/不取模)✅ ④ 五元组去重键 `{Kind}_{Patient}_{Tick}_{Seq}` 正确 ✅
⑤ `EventStream.Append` 三机制(CAP / 去重 / Seq 发号)全链路正确 ✅

### 测试面(qa-lead): APPROVE — 0 BLOCKING + 4 ADVISORY

| # | 级 | 位置 | 问题 |
|---|---|---|---|
| A1 | ADVISORY | `patient_spawner_test.cs:69` | **同源重算**: `expectedSeed = unchecked((long)SplitMix64.Hash((long)WorldSeed, patientId.Value))` 与生产代码 `PatientSpawner.cs:54` **逐字同表达式**。判别力限于「是否调 Hash / 参数是否正确」(MUT-1 已验证),不覆盖「Hash 算法本身被换」 |
| A2 | ADVISORY | `vertical_slice_test.cs:84-101` | **Seq 断言缺失**: 测试用了真 `EventStream`,后者在 `Append` 时发号(`EventStream.cs:90-93`),但测试未断言 `e.Seq`。`EventStream` 的 Seq 发号逻辑仅由 `event_stream_test.cs:test_seq_resetsEachTick` 覆盖(用 `ActorCellEntered`,非 `DiseaseOnset`) |
| A3 | ADVISORY | `patient_spawner_test.cs:97-104` | **零事件断言**: `test_spawnNext_incrementsPatientId` 只验 ID 递增,完全不查事件。MUT-2(不调 `Append`)证实此测试在事件完全不写入时仍绿。设计意图清晰(聚焦 ID 分配),登记为观察项 |
| A4 | ADVISORY | `vertical_slice_test.cs:193-234` + `patient_spawner_test.cs` 全部 | **CAP 交互未覆盖**: `EventStream.Append` 的有界性检查(`:59-66`)在 `IsPresent(newPatient) == false` 时触发(新建病人恒不在场)。`test_diseaseOnset_writesToHistoryStream` 不调用 `AddPresent`,故 CAP 检查**执行但未触发**(count=0 < 24)。`FakePresenceQuery.AddPresent` 是测试侧代偿(生产 `IPresenceQuery` 无写面,已登记为已知缺口)。CAP 触发路径由 `event_stream_test.cs:test_cap_rejectsBeyond24` 覆盖,但 `PatientSpawner + EventStream + CAP-full` 的**三方交互**无测试 |

**核过零问题**: 断言精度达标(5 条测试均断言具体值)· 真生产路径 vs 假夹具分工合理
(EditMode 用 `FakeEventSink` 做单元级验证;PlayMode 用真 `EventStream` 做集成验证)·
`patient_seed` 断言部分自指(见 A1)· `Seq == 0` 断言能区分(断言的是解码后 payload 的 Seq 字段,
非 event header 的 Seq)· `test_spawnNext_incrementsPatientId` 不查事件是设计意图 ·
`FakePresenceQuery.AddPresent` 代偿合理(生产无写面,已登记)· 确定性 ✅ · 命名规范 ✅ · 门式声明合规 ✅

**变异覆盖缺口**登记: G1(Seq 发号在 DiseaseOnset 路径)· G9(CAP-full 交互)。两者风险低(既有测试部分覆盖)。

## 二、修复落点(全 6 项 · 同批)

| # | 修复 | 落点 |
|---|---|---|
| A1(码) | **先发号后 Append → 注释 + 登记**: 核实 `IdAuthority.NextPatientId = _nextPatient++`(不扫流)⇒ 空洞**不可自愈**;但生产路径 `PresentCount` 恒 0(IPresenceQuery 无写面),CAP 永不触发 ⇒ 当前不可达。真正修法 = 发号时机后移(Append 成功后才 NextPatientId),需改 `IIdAuthority` 契约,超本轮 ⇒ 加注释 + 登记观察项 O-3 | `PatientSpawner.cs:73-78` |
| A2(码) | **载荷 `seq` 注释订正**: `seq: 0` 注释「Seq 由 EventStream 发放」误导 → 改为「载荷 Seq = 占位 0;header Seq 由主机 Append 时发号(承 10 的同一现状)」 | `PatientSpawner.cs:61` |
| A3(码) | **`SpawnNext` 入参校验**: 补 `diseaseId >= 0` / `tick >= 0` 范围检查(最小可行校验,不依赖病种域常量)。核实生产实现唯一(`IdAuthority`,不抛)、生产调用方零 ⇒ 可安全落 | `PatientSpawner.cs:58-59` |
| A4(码) | **ctor 补 doc comment**: 对齐 `EventStream.cs:39` / `PrescribeFlow.cs:382` 先例 | `PatientSpawner.cs:31-41` |
| A1(测) | **`patient_seed` 断言注释订正**: 「期望值独立重算而非复用生产代码」→「与生产侧同口径:哈希 64 位按位承载,期望值独立重算(不复用生产代码结果,但复用同一表达式)」 | `patient_spawner_test.cs:68` |
| A2(测) | **补 header Seq 发号断言**: `test_diseaseOnset_writesToHistoryStream` 补 `Assert.AreEqual(0L, e.Seq)`(首个事件 Seq = 0) | `vertical_slice_test.cs:99` 后 |
| A3(测) | **补入参校验测试**: `test_spawnNext_rejectsNegativeDiseaseId` / `test_spawnNext_rejectsNegativeTick`(负值应抛 `ArgumentOutOfRangeException`) | `patient_spawner_test.cs:108-121` |

**未采纳**: 测试面 A3(`test_spawnNext_incrementsPatientId` 零事件断言 —— 设计意图清晰,
事件写入由其余 4 条覆盖,不修)· A4(CAP-full 三方交互 —— 生产路径 `PresentCount` 恒 0,
CAP 永不触发,当前不可达,登记为观察项)。

## 三、验证命令(可证伪)

```bash
unity test unity --mode EditMode --filter "PatientSpawnerTest" \
  --output unity/Logs/editmode_spawner_final.xml              # → 7/7/0(基线 5/5 +2 新增校验)
unity test unity --mode PlayMode --filter "VerticalSliceTest" \
  --output unity/Logs/playmode_slice_final.xml                # → 7/7/0/0(基线 7/7/0/0 +0)
unity test unity --mode EditMode \
  --output unity/Logs/editmode_full_20261009_spawner.xml     # → 3023/2976/0/46/1inc(基线 3021/2974 +2)
unity test unity --mode PlayMode \
  --output unity/Logs/playmode_full_20261009_spawner.xml     # → 98/98/0/0(基线 98/98/0/0 +0)
```

## 四、判别力变异(4 发全中 · 逐发 python 反向恢复零残留)

| 发 | 注入 | 期望 | 实测 |
|---|---|---|---|
| MUT-1 | `patientSeed = (long)patientId.Value`(去 hash) | `test_spawnNext_patientSeedIsHashOfWorldSeedAndPatientId` | ✅ 恰红 1 条 |
| MUT-2 | 不调 `_sink.Append` | 除 `test_spawnNext_incrementsPatientId` 外 4 条 | ✅ 恰红 4 条 |
| MUT-3 | **A3 修复判别力: 删入参校验** | `test_spawnNext_rejectsNegativeDiseaseId` + `test_spawnNext_rejectsNegativeTick` | ✅ 恰红 2 条(修复前此注入逃逸) |
| MUT-4 | **A2 修复判别力: 删 `e.Seq` 断言** | `test_diseaseOnset_writesToHistoryStream` | ✅ 恰红 1 条(修复前此注入逃逸) |

恢复终态: `PatientSpawner.cs` / `patient_spawner_test.cs` / `vertical_slice_test.cs` 三文件
`grep MUT` = 0;`PatientSpawner.cs` 五锚点(NextPatientId / SplitMix64.Hash / Encode / Append / return)各 1。

## 五、复跑实数(终态 · 修复后净态,不沿用变异前日志)

- 过滤 **7 / 7 passed / 0 红**(`editmode_spawner_final.xml`)—— 基线 5/5 +2(新增 2 条校验测试)
- 垂直切片 **7 / 7 passed / 0 红 / 0 跳**(`playmode_slice_final.xml`)—— 基线 7/7/0/0 +0
- 全量 EditMode **3023 / 2976 / 0 红 / 46 跳 / 1 inc**(`editmode_full_20261009_spawner.xml`)—— 基线 3021/2974 +2,零回归
- 全量 PlayMode **98 / 98 / 0 红 / 0 跳**(`playmode_full_20261009_spawner.xml`)—— 基线 98/98/0/0 +0,零回归
- 变异 4 发恰红 + 零残留(上表)

## 六、判定链

原判定(代码面 APPROVE 0B+4A · 测试面 APPROVE 0B+4A ⇒ 去重 6 项)→ 同批修复全落 →
补 2 发修复判别力变异(MUT-3 顺序 / MUT-4 Seq 断言)恰红 → 净态过滤 + 全量双套零回归 → **转 APPROVE**。

ADR-030 §Migration Plan 步 ②(9 写者实现)就此闭环。

## 七、新登记观察项

| # | 项 | 归属 |
|---|---|---|
| O-1 | `EventStream:90` 的 Seq 哨兵 `e.Seq == 0` 与 `_currentSeq` 首值 0 冲突 ⇒ 断言「Seq == 0」无法区分「发了 0 号」与「没发号」 | 待裁 —— 候选 = EventStream 修复轮 / ADR-006 Amendment 轮 |
| O-2 | `IPresenceQuery` 只有读面、无写面 ⇒ 生产路径下 `PresentCount` 恒 0 ⇒ `PATIENT_APPEARANCE_CAP`(24) 永不触发。测试侧以 `FakePresenceQuery.AddPresent` 代偿(既有 `event_stream_test.cs` 同款) | 待裁 —— 候选 = 9 / 表现层注入点 |
| O-3 | `PatientSpawner.SpawnNext` 先发号后 Append ⇒ 若 Append 抛异常(如 CAP 满),ID 空洞且 `IdAuthority.NextPatientId = _nextPatient++`(不扫流)不可自愈。当前不可达(`PresentCount` 恒 0),真正修法 = 发号时机后移,需改 `IIdAuthority` 契约 | 待裁 —— 候选 = 9 实现轮 / IIdAuthority 契约修订轮 |
