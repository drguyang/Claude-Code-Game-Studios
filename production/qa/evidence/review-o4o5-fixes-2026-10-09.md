# 评审原件 — O-4/O-5 观察项修复轮 · 2026-10-09

> **对象**(两轮一批):
> 1. **O-4**: `EventStream` 去重键在发号**前**取 Seq —— 未发号事件同 `(Kind, Patient, Tick)` 坍缩,
>    第二条静默丢弃(现实触发:同 tick 放置两个结构 ⇒ 第二条 `StructurePlaced` 丢失)。
>    落点:`EventStream.cs` 去重键 + `event_stream_test.cs` 2 条。
> 2. **O-5**: CAP 判据对 `PatientId.None` 世界事件同样生效 —— 在场满 24 时
>    `StructurePlaced` / `PoiStateChanged` / `ActorCellEntered` / 急救两支全被 AC-15 误拒。
>    落点:`EventStream.cs` CAP 分支 + `event_stream_test.cs` 1 条。
> **来源**: 上轮 `review-observation-fixes-o1o2o3-2026-10-09.md` 代码面评审 ADVISORY #4/#5,
> 当时裁定「登记不修」;用户 2026-10-09 指令「继续修复O4O5」开启本轮。
> **轮次**: 双代理评审**恰一轮**(用户长期指令)—— ✅ **已完成:代码面 APPROVE + 测试面 FIX-THEN-APPROVE → 补测/注释全落 → 5 发变异全中 → 终态复跑全绿 → APPROVE**

## 一、原判定

**代码面 lead-programmer**:**APPROVE**(0 BLOCKING + 4 ADVISORY,均不要求本轮改码)。
核过零问题 11 项:同实例重发幂等保住 · 已发号路径/7a 重放不受影响(SimEventCodec 恒显式
Seq ⇒ 恒四元组)· 35 支 Kind 全 PascalCase 键互撞不可能 · 门 A 零破 · 全库 10 处 Append
写者扫描(None×8 全覆盖,真 id 两写者语义不变)· CAP 无「该拒的没拒」· ADR 一致 ·
变异判别力 · 全量零回归 · 变更面仅两文件 · 注释与实现对齐。

| # | 级 | 发现 | 处置 |
|---|----|------|------|
| F1 | ADVISORY | **重编码重发不再被幂等拒收**(相对修复前的行为回归):`PayloadEncoder.Store` 每次发新 BlobId ⇒ 同语义事件重新编码后 Append = 新键 = 双双进流;修复前粗键会误打误撞拒收。当前暴露面低(写者均有守卫,45 意图层未落地);AC-15「重发拒收」的重编码半边自此不成立(dose_seq 五元组键本就未实现,既有缺口) | **不改码**。本件 §二 补边界声明;45 轮须知悉 |
| F2 | ADVISORY | **O-4 漏网:`ActorCellEntered` 两写者绕过 encoder**,坐标手搓 `PayloadRef(cell.X,cell.Y,cell.Z)` 伪引用(无 actor 身份)⇒ 联机两 actor 同 tick 同格仍坍缩。根因先于本轮(registry 已定义 `ActorCellEnteredPayload` 未被用;b6 门只扫 `Sim/`) | 不阻断;**单独立项**(登记 O-6) |
| F3 | ADVISORY | 条件键第二分支(已发号/显式 Seq 重发)**零直接测试** | 补 `test_dedup_signedResend_explicitSeq_deduped` |
| F4 | ADVISORY | `EventStream.cs:12` 文件头仍写「去重(五元组键)」,与实现矛盾 | 一行注释修复 |

**测试面 qa-lead**:**FIX-THEN-APPROVE**(2 BLOCKING 补测 + 2 ADVISORY;生产代码无须改动)。
核过:主判别力实证 · O-4 幂等反向面被既有测试抓住 · CAP 放松族全被 `test_cap` 抓住 ·
夹具/AAA/确定性通过 · 假绿扫描无发现 · 写者回归面结构性为零 · 计数自洽。

| # | 级 | 发现 | 失效场景 |
|---|----|------|----------|
| F-1 | **BLOCKING(补测)** | 条件键第二分支(显式 Seq)零覆盖:「重传带原 Seq 命中同键」是无测试纸面契约 | 变异 M1(条件改恒真)47 条全幸存 ⇒ 45 重编码重传假阴性去重、事件膨胀;变异 M2(issued 键丢 Seq)亦幸存 |
| F-2 | **BLOCKING(补测)** | CAP 满 × **在场**真实病人组合缺测(既有两测合抓不住「删 `!IsPresent`」) | 变异 M3 幸存 ⇒ 满员稳态下 24 个在场病人自己的事件全抛 AC-15,全局冻结 |
| F-3 | ADVISORY | `Clear()` 清键/复位无测试(既有缺口,非本轮引入) | 将来 Clear 漏清 `_dedupKeys` 无人抓 |
| F-4 | ADVISORY | CAP 检查在去重**之前**(既有次序):满员时重发先抛而非走去重短路 | 非本轮触碰,登记观察(归 45 重传轮) |

### §二之二:双代理评审后的修复(2026-10-09)

| 来源 | 级 | 修复 |
|---|---|---|
| 测试 F-1 + 代码 F3(合并) | **BLOCKING(补测)** | 新增 `test_dedup_explicitSeq_payloadBlind_andSeqDistinguished` —— 一测三面:① 显式同 Seq 不同 PayloadRef ⇒ 去重(杀 M1);② 不同显式 Seq ⇒ 都入流(杀 M2);③ 发号器续号语义(显式事件前进,得 2) |
| 测试 F-2 | **BLOCKING(补测)** | `test_o5` 并入:CAP 满 + `PatientId(0)` **在场** Append 真实病人事件 ⇒ 不抛且 Count+1(杀 M3) |
| 测试 F-3 | ADVISORY | 新增 `test_clear_resetsDedupKeysAndSeq`(Append→Clear→同事件再入 = 1,且复位后得首号 0) |
| 代码 F4 | ADVISORY | `EventStream.cs` 文件头「五元组键」订正为条件键 + 次序警示行(承测试面 F-4) |
| 代码 F1 | ADVISORY(不改码) | **边界声明(补入本件 §二 O-4 段末)**:未发号重发幂等边界 = 同一 `SimEvent`/同一 `PayloadRef`;**重编码重发的去重责任在写者幂等/45 意图层** —— 45 轮须知悉 |
| 代码 F2 | ADVISORY(不改码) | **登记新观察项 O-6**:`ActorCellEntered` 两写者(PlayerController/CellTransitionDetector)绕过 `IPayloadEncoder`,坐标手搓 `PayloadRef` 伪引用(无 actor 身份)⇒ 联机两 actor 同 tick 同格仍坍缩;根因先于本轮(registry 已定义 `ActorCellEnteredPayload` 未被用;b6 门只扫 `Sim/`)。**归:encoder 接线轮 + b6 门扩扫** |

**登记不修**:测试 F-4(CAP/去重次序 → 45 轮)· 代码 Suggestions 1(`BuildDedupKey` 抽取)·
`_dedupKeys` 只增不清(既有)· story-002 AC-15 dose_seq 五元组键未实现(既有缺口,
F1 结构性根因,归处置去重轮)。

## 二、修复落点(已实施)

### O-4:去重键补载荷身份

| # | 落点 | 内容 |
|---|---|---|
| 1 | `EventStream.cs` Append 去重段 | **条件键**:未发号(`Seq < 0`)⇒ 键补 `PayloadRef` 三字段 `(BlobId, Offset, Length)`;已发号/显式 Seq ⇒ 维持四元组(Seq 即身份,重编码免疫) |

**设计论证(三条)**:
- **为何不能用发号后 Seq**(评审原备选一):发号后键 ⇒ 重发事件得新 Seq ⇒ 键必不同 ⇒
  **幂等去重彻底失效**(每次重发都进流)。
- **为何不能读 payload 首字段**(评审原备选二):`Sim` 门 A 引用集 = {BCL, `Sim.Contracts`},
  **物理上够不着 `Sim.Codec`** —— 只能取 `PayloadRef` 三字段。身份 = ref 本身。
- **为何已发号事件不补载荷**:重传/重建路径可能**重编码**(同语义新 BlobId)⇒
  补载荷会让同一已发号事件的重传命中不了原键 ⇒ 假阴性去重(重复进流)。
  显式 Seq 已是充分身份。**条件键 = 两条路径各取其充分身份**。
- **重编码来源实例**:`PayloadEncoder.Encode` 每次 `Store` 发新 BlobId ⇒ 语义相同事件的
  两次编码 ref 必不同 —— 这正是「未发号靠 ref、已发号靠 Seq」分治的根据。
- **⚠️ 边界声明(代码面 F1,2026-10-09)**:未发号重发的幂等边界 = **同一 `SimEvent`
  (同一 `PayloadRef`)**;**重编码重发**(同语义重新 Encode)得新 BlobId ⇒ 新键 ⇒
  **不去重**。该半边的去重责任在**写者幂等 / 45 意图层** —— 修复前的粗键会「误打误撞」
  拒收重编码重发,修复后此偶合消失,是**登记在案的行为变化**。45 轮须知悉;
  story-002 AC-15 的 dose_seq 五元组键(重编码免疫)仍未实现(既有缺口,归处置去重轮)。

### O-5:CAP 判据跳过 `PatientId.None`

| # | 落点 | 内容 |
|---|---|---|
| 1 | `EventStream.cs` Append CAP 分支 | `e.Patient != PatientId.None && !IsPresent(...)` 才进 CAP 检查 |

**覆盖性核对(全库写者扫描)**:带 `PatientId.None` 的五个写者
(`StructureKinds`×3 · `PoiStateMachine` · `CellTransitionDetector` / `PlayerController` ·
`HostEmergencyProcessor` 急救两支)全部被守卫覆盖;带真实病人 id 的写者
(`PatientSpawner` DiseaseOnset · `PrescribeFlow` DrugTreatmentApplied)**语义不变**
(CAP 满 + 病人不在场 ⇒ 仍拒收 —— 这正是「拒收新病人」的本义)。
既有 `test_cap_rejectsBeyond24` 用真实 id 100,不受影响。

**登记边界(承上轮)**:真实 id 但**非病人**的实体(玩家/敌人,ADR-016 §二 共用 id 空间)
在 CAP 满时仍会被拒 —— 玩家跨格目前用 None(未触发),敌人写者 25 尚未实现;
该边归 25 写者轮/组合层接线轮(不在本轮裁决面)。

## 三、验证命令(可证伪)

```bash
unity test unity --mode EditMode --filter "EventStreamTest|PatientSpawnerTest|PresenceRegistryTest|IdAuthorityTest" \
  --output unity/Logs/editmode_o4o5_fix1.xml   # → 47/47(44 + O-4×2 + O-5×1)
unity test unity --mode EditMode \
  --output unity/Logs/editmode_full_20261009_o4o5.xml   # → 3047/3000/0 红/46 跳/1 inc(基线 3044/2997 +3)
unity test unity --mode PlayMode \
  --output unity/Logs/playmode_full_20261009_o4o5.xml   # → 98/98/0 红(= 基线)
```

## 四、判别力变异

| 发 | 注入 | 期望 | 实测 |
|---|---|---|---|
| MUT-O4 | 去重键退回四元组 | test_o4_distinctPayloads | ✅ 恰红 1(EventStreamTest 10 → 9) |
| MUT-O5 | 删 None 守卫 | test_o5_worldEvents_nonePatient_bypassesCap | ✅ 恰红 1(同上) |
| MUT-M1 | 条件键改恒真(issued 也补载荷) | test_dedup_explicitSeq 面① | ✅ 恰红 1(12 → 11) |
| MUT-M2 | issued 键丢 Seq 分量 | test_dedup_explicitSeq 面② | ✅ 恰红 1(同上) |
| MUT-M3 | CAP 删 `!IsPresent` | test_o5 F-2 面 | ✅ 恰红 1(同上) |

恢复:逐发 python 反向恢复,净态 `MUT-` 残留 0,两锚点(`PatientId.None && !IsPresent`、
`e.Payload.BlobId`)各 1。**5 发全中**(评审前 2 + 评审后 3)。

## 五、复跑实数(终态 · 评审后补测 + 5 发变异全部恢复后的净态)

| 面 | 产物 | 实测 | 基线对照 |
|---|---|---|---|
| 过滤 | `editmode_o4o5_final.xml` | **49/49 绿 / 0 红** | 44 + O-4×2 + O-5×1 + 显式Seq 1 + Clear 1 |
| 全量 EditMode | `editmode_full_20261009_o4o5final.xml` | **3049 / 3002 绿 / 0 红 / 46 跳 / 1 inc** | 基线 3044/2997 → **+5 零回归** |
| 全量 PlayMode | `playmode_full_20261009_o4o5final.xml` | **98 / 98 绿 / 0 红** | = 基线 |

## 六、判定链

**代码面 lead-programmer:APPROVE**(0B + 4A,均不要求改码)·
**测试面 qa-lead:FIX-THEN-APPROVE**(2B 补测 + 2A)→
**补测/注释全落**(§二之二:显式 Seq 键三面测试 + CAP 满 × 在场病人断言 + Clear 键测试
+ 文件头订正;F1 边界声明入 §二;O-6 登记)→
**判别力变异 5 发全中**(评审前 MUT-O4/O5 各恰红 1;评审后 MUT-M1/M2/M3 各恰红 1;
逐发 python 反向恢复,`MUT-` 残留 0,锚点各 1)→
**终态复跑全绿**(过滤 49 · 全量 EditMode/PlayMode 见 §五)→
**APPROVE**(两面必修项全部闭环,可转 Complete)。
