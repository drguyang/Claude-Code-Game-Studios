# T2 注册表两视图 + 病行两列 —— 实施与证据(2026-11-09)

> **范围**:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §7 的 **T2**。
> **前置**:T1(`027b065d`,源文件拆 `diseases` / `injuries` 两数组)。
> **未做**:T3(技法解锁的权威载体 ADR)及以后。**T2 不声称「病例已按技法门投放」** ——
> 那要等 T4;本步只把**数据形状**与**加载期视图**备好。

---

## 1. 判据

| # | 判据 | 载体 |
|---|---|---|
| 1 | 真表分区**不丢不重**:`Diseases ∪ Injuries == Rows` 且两侧无交 | `test_realSource_partitionIsCompleteAndDisjoint` |
| 2 | `injuries` 数组只收 `kind: injury`,放错侧**硬失败** | `test_injuriesArray_withDiseaseKind_isRejected` |
| 3 | 缺 `kind` 两侧都拒(回归锚) | `test_missingKind_isRejectedOnEitherSide` |
| 4 | `min_skill_level` / `teach_point` **进字节且往返等值** | `test_minSkillLevelAndTeachPoint_surviveCookedRoundTrip` |
| 5 | 未写 `teach_point` ⇒ 往返后仍 `null`;缺省 `min_skill_level = 0` | `test_absentTeachPoint_staysNull` |
| 6 | `min_skill_level ∉ [0,60]` ⇒ 拒 | `test_minSkillLevel_outOfRange_isRejected` |
| 7 | 空串 `teach_point` ⇒ 拒 | `test_emptyTeachPoint_isRejected` |

---

## 2. 改动清单

| 文件 | 行 | 改动 |
|---|---|---|
| `unity/Assets/Sim/RegistrySchema.cs` | 283 / 290 | `DiseaseRegistryEntry` 增 `MinSkillLevel`(int)· `TeachPoint`(string) |
| `unity/Assets/Editor.Tools.Bake/DiseaseRegistryBinder.cs` | 41-45 / 78-80 | 两键入 `DiseaseRowKeys` / `BoundRowKeys` |
| 同上 | 49 | `MaxSkillLevel = 60` —— **镜像值**,真源 = 30 §3.2,注释写明 |
| 同上 | 366-369 | 绑定两字段(`min_skill_level` 缺省 0;`teach_point` 可选) |
| 同上 | 374-380 | 值域门 `[0,60]`,报文点名「越界 = 该病永远进不了池」 |
| 同上 | 296-309 | **分区一致性**:`injuries` 里 `kind ≠ injury` ⇒ 硬失败(反向刻意不查,见 §4.2) |
| 同上 | 604 | 新增 `ReadOptionalString`(缺省 null;空串/空白串拒收) |
| `unity/Assets/Editor.Tools.Bake/DiseaseRegistryCookedWriter.cs` | 170-171 | 行尾追加 `i32 min_skill_level` + `string32 teach_point` |
| `unity/Assets/Gameplay.Presentation/DiseaseRegistryCookedCodec.cs` | 55 / 207-208 | `ExpectedSchemaVersion` 3u;`ReadRow` 读两字段 |
| `unity/Assets/Gameplay.Presentation/DiseaseRegistryDataSet.cs` | 46-59 / 76-96 | 增 `Diseases` / `Injuries` 视图——**装载期分桶一次** |
| `unity/Assets/Editor.Tools.Bake/DiseaseRegistryBaker.cs` | 262-265 | `Materialize` 的逐字段往返比对加两项 |
| `assets/data/disease_registry.json` | — | `schema_version` 3;9 条病行加 `"min_skill_level": 0` |
| `unity/Assets/DataCooked/*.cooked.bytes` | — | 重烘注册表(4171 → **4299 B**,+128 = 16 行 × 8 B)+ 代价表 |

---

## 3. 回归

**三元组**:`2026-11-09T06:27Z`(EditMode)/ `06:29Z`(PlayMode)· **全量** · `027b065d` + 工作树。

| 套件 | 本次 | 对照基线(T1 后) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7178 / 7141 / 0 / 37`** | `7171 / 7134 / 0 / 37` | **+7 / +7** |
| PlayMode(全量) | **`370 / 366 / 0 / 2 / 2`** | 同值 | 0 |
| `DiseaseRegistryPartitionTest`(新) | `7 / 7` | — | — |
| `DiseaseRegistryBakePipelineTest` | `32 / 32` | — | — |
| `cooked_drift_all_artifacts_test` | `47 / 47` | — | — |
| `CostTableBinderTest` | `34 / 34` | — | — |

**等式核对**:`7178 − 7171 = 7` = 新增 `[Test]` 数 **7**(`disease_registry_partition_test.cs`)。

### 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# 产物长度:T2 前 4171 → 后 4299(= +16 行 × (4 B int + 4 B null 字符串标记))
ls -l unity/Assets/DataCooked/disease_registry.cooked.bytes
python3 -c "
import json;d=json.load(open('assets/data/disease_registry.json'))
print('schema', d['schema_version'], '| diseases', len(d['diseases']), '| injuries', len(d['injuries']))"
# 期望:schema 3 | diseases 9 | injuries 7
```

---

## 4. 本轮的三个判断(与取舍理由)

### 4.1 `teach_point` 放进 `DiseaseRegistryEntry`(而不是侧信道)

- 备选 = 学 `terminal_state` 走侧信道(不进 entry / 不进 cooked)。**未采纳**,理由:
  侧信道的合法性前提是「**唯一消费方在构建期**」(`terminal_state` 只给 53 的 N-5)。
  `teach_point` 的消费方是**教学/手册侧**(运行期),侧信道会让它**永远取不到**。
- 代价如实记账:一个**显示用字符串**进了 `Sim` 的数据形状。它不参与任何 sim 求值
  (注释已写明),但这是本轮的取舍,不是「本来就该这样」。

### 4.2 分区门是**单向**的(查 `injuries`,不查 `diseases`)

- 查 `injuries` 侧:`kind ≠ injury` ⇒ 拒。这是 T2 的明文契约。
- **反向刻意不查**:拆表前「伤行写在 `diseases` 里」是合法形态,多个合成源仍这么写
  (`disease_registry_bake_pipeline_test` 的 `MinimalJson` 就是单条 injury 放在 `diseases`)。
  现在收紧会让那些夹具**因与 T2 无关的理由**转红。
- ⇒ **收紧时点 = T4**(抽取接线时才真正需要「病侧不含伤」的不变式)。届时同批改夹具。

### 4.3 `min_skill_level` 的值**没填**(全是 0)

- 它是**内容节奏**参数,不是结构参数;且病种集正在重梳(水泻 / 蛇咬尚未入表)。
  现在填任何值都会在下一轮被推翻。
- 结构已就位(字段、值域门、往返、视图),值归内容/数值轮。

---

## 5. 未做(不得借绿)

| 项 | 状态 |
|---|---|
| 技法解锁状态(T3 的 ADR) | **未做** —— 它是 `treatable_by` 过滤的输入源,T4 的前置 |
| 抽取改「技法门 + 等级门」 | **未做**(T4) |
| 病侧剔除伤行的**反向**门 | **未做**(T4 同批) |
| 钩子病例 / 手册缺页 | **未做**(T5 / T6) |
| `teach_point` 的实际文案 | **未做**(内容轮) |
| `min_skill_level` 的梯次值 | **未做**(内容轮) |
