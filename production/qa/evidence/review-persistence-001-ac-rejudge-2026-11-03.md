# persistence-service-001 —— 7 条 AC 逐条重判（重判轮原件）

**日期**：2026-11-03
**Story**：`production/epics/persistence-service/story-001-storage-abstraction.md`
**Sprint**：24（S24-2）
**背景**：2026-10-25 QA 轮退回（原 `Complete` 但 7 条 AC 全未勾 ⇒ 判为假绿形态）。本轮逐条重判。

---

## 一、逐条判定（7 条）

| AC | 重判 | 载体 | 与 2026-10-25 的差 |
|---|---|---|---|
| **AC-7a-01** | **PASS（前件已限定）** | `save_replay_roundtrip_test.cs::test_ac7a01_noSnapshotSegment_andRestoredFactsEqualDirectReplay` | NOT-RUN → PASS |
| **AC-7a-02** | **NOT-RUN（前件不存在）** | 同上测试的第 ① 断言（`SnapshotOffset == 0`） | 维持 NOT-RUN，**但不再是「没人做」而是「没有宾语」** |
| **AC-7a-03** | **NOT-RUN（EXTERNAL）** | ADR-012 三格矩阵 · `TD-ci-01` | 不变 |
| **AC-7a-04** | **PASS** | `save_service_test.cs` 的 4 条新夹具（真往返 / 三次对拍 / Fix raw / IL 扫描） | NOT-RUN（假绿）→ PASS |
| **AC-7a-05** | **PASS** | `test_checksumValidation_detectsCorruption` | 不变 |
| **AC-7a-06** | **PASS** | `test_atomicWrite_createsFinalFile` + `test_doubleBackup_bakCreatedOnSecondWrite` | 不变 |
| **AC-7a-18** | **PASS（扫描面已加宽）** | 原反射夹具 + 新源码面夹具 `test_ac7a18_sourceSurface_ofSaveCodec_hasNoFloatTypes` | 加宽，措辞差已消 |

**小结**：PASS **4** · NOT-RUN **3**（其中 2 条 EXTERNAL/前件不存在，1 条前件不存在）。
⇒ **不再有假绿**：4 条 PASS 各有「会转红的判据」，3 条 NOT-RUN 各写明为什么**今天无法判**。

---

## 二、本轮的两处实质修复（不是补文档）

### 2.1 `SaveHeaderCodec.HasValidMagic` 用了主机端序（真缺陷）

```csharp
// 修前
&& unchecked((uint)BitConverter.ToInt32(data, 0)) == SaveHeader.Magic;
// 修后
&& BinaryPrimitives.ReadUInt32LittleEndian(data) == SaveHeader.Magic;
```

ADR-010 §一 明禁 `BitConverter`（显式小端强制）。小端机上行为**恰好相同** ⇒
既有四条存档夹具全绿也照不出它 —— 属「行为等价掩盖结构分叉」同族形态。
大端机上的失败模式是静默的：**每份存档都被判成「不是我们的」**。

### 2.2 AC-7a-04 的原夹具是**第六个假绿形态**

```csharp
// 修前（test_simEventCodec_roundTrip）
byte[] bytes = w.ToArray();
Assert.IsTrue(bytes.Length > 0, "编码后应有字节");   // ← 未解码、未比任何字段
```

对 AC 的三项子判据（字段往返 / 三次逐位一致 / IL 零禁用 API）**零判别力**。已整条重写。

---

## 三、证伪轮（3 变异，SHA-256 逐字节还原）

见 `falsify_s24_2` 轮的实跑输出；变异与期望红：

| 变异 | 改什么 | 期望红 |
|---|---|---|
| P1 | `SimEventCodec.Encode` 的 `Seq` 写成 0 | `test_ac7a04_simEventHeader_roundTrip_fieldByField` |
| P2 | `HasValidMagic` 退回 `BitConverter`（主机端序） | `test_ac7a04_assemblyScan_noForbiddenSerializersOrHostEndianApis` |
| P3 | `SaveComposer` 头部写非 0 `SnapshotOffset`（= 快照段已引入） | `test_ac7a01_noSnapshotSegment_andRestoredFactsEqualDirectReplay` |

---

## 四、复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# ① 快照段今天不存在(AC-7a-01/02 的前件)
grep -n "SnapshotOffset = 0\|快照段 / 分册态段留待后续\|0UL)" unity/Assets/Gameplay.Boot/SaveComposer.cs | head -3
# ② 禁用 API 已清零(IL 面由夹具判)
grep -rn "BitConverter" unity/Assets/Sim.Codec --include=*.cs | grep -v "/Tests/" | wc -l   # 期望 0
# ③ 夹具存在
grep -c "public void test_ac7a0" unity/Assets/Tests/EditMode/PersistenceService/save_service_test.cs
grep -c "test_ac7a01_" unity/Assets/Tests/EditMode/PersistenceService/save_replay_roundtrip_test.cs
```
