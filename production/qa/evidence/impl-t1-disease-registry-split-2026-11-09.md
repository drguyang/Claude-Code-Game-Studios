# T1 病种注册表拆表 —— 实施与证据(2026-11-09)

> **范围**:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §7 的 **T1**(纯结构拆分,行为零变化)。
> **未做**:T2(`min_skill_level` / `teach_point` / `DiseaseRegistryDataSet` 两视图)及以后各步。
> **结论**:落盘 + 定向 3 套 / 全量双平台回归零失败。**不声称 T2–T7 的任何判据**。

---

## 1. 判据(执行期订正)

| 原判据 | 实测结论 |
|---|---|
| ~~产物逐字节不变~~ | **做不到**:`ConfigVersion` 由源文本哈希派生,写在 cooked 头 **offset 12–15**;拆数组必然改源文本 ⇒ 这 4 字节必变(承 `DiseaseRegistryCookedWriter.Write` 的头部布局) |
| **订正后判据** | offset 0–11 与 16–末尾**逐字节相同**,差异**恰为 offset 12–15**;且该 4 字节 = 新源文本派生的 `ConfigVersion`(日志显式打印 `0x01A56126`) |

实测(逐字节 diff):

```
len:  4171 → 4171        (等长)
diff: [12, 13, 14, 15]    (恰 4 字节)
payload 自 offset 20 起: 完全相同
before sha256: 881736cb0e05c43e4ea5fc896c0ec2e8d904dc1ebce176a8383fc20f2ea288ea
after  sha256: 1cc73882ffe7231b19fc70fc0809a068c377059ef2c17bafd2f7f21ea3a5f011
```

---

## 2. 改动清单

| 文件 | 行 | 改动 |
|---|---|---|
| `assets/data/disease_registry.json` | 15 / 400 | 单 `diseases` 数组 → `diseases`(9 行,id 7–14/17)+ `injuries`(7 行,id 2–6/15/16)。**行文本逐字未改**(含每行 `_rowNote`) |
| `unity/Assets/Editor.Tools.Bake/DiseaseRegistryBinder.cs` | 39 | 根键白名单加 `injuries` |
| 同上 | 217–230 | 两个数组走同一 `BindRows`;绑定后**按 `DiseaseId` 升序并集**(真表书写序本就是 id 升序 ⇒ 载荷段不变) |
| 同上 | 262 | 新增 `BindRows`(行绑定路径**不复制** —— 承 GDD 规则九「伤情与病种同一 schema」) |
| `unity/Assets/Editor.Tools.Bake/CostTableBinder.cs` | 62 / 273 | N-4′ canary 原只扫 `diseases`;**拆表后必须两数组都扫**,否则伤情行会静默逃过「compounds 到期复查」 |
| `unity/Assets/DataCooked/disease_registry.cooked.bytes` | — | 重烘(除 ConfigVersion 外字节不变) |
| `unity/Assets/DataCooked/53_cost_table.cooked.bytes` | — | 重烘(**必然**:`CostTable53Bake.cs:91` 的 ConfigVersion 把「代价表源 + 注册表源」两份一起哈希 ⇒ 注册表源变了,版本号必变) |

### 语义等价自检(脚本内断言,非人工 eye-ball)

```
diseases: [7, 8, 9, 10, 11, 12, 13, 14, 17]
injuries: [2, 3, 4, 5, 6, 15, 16]
merged == orig (按 id 排序后逐对象相等): True
原行数 16 · 现 16
injury 行 kind 全为 injury: True
diseases 数组里无 injury 行: True
```

---

## 3. 回归

| 套件 | 结果 | 对照基线 | 截至时刻 · commit |
|---|---|---|---|
| EditMode(全量) | **`7171 / 7134 / 0 / 37`** | 同值 —— **零回归,零新增** | `2026-11-09T05:55Z` · `839eebcc` + 工作树 |
| PlayMode(全量) | **`370 / 366 / 0 / 2 / 2`** | 同值 —— 零回归 | `2026-11-09T06:0xZ` · `839eebcc` + 工作树 |
| `cooked_drift_all_artifacts_test` | `47 / 47` | — | 同上 |
| `DiseaseRegistryBakePipelineTest` | `32 / 32` | — | 同上 |
| `CostTableBinderTest` | `34 / 34` | — | 同上 |

**等式核对**:`total 7171 − 前次 7171 = 0` = 本批新增 `[Test]` 数 **0**(T1 是纯结构,按设计零新增)。

基线出处:上一次全量为 `7171/7134/0/0/37` · `370/366/0/2/2` @ `2026-11-08T07:19:20Z`(见 `production/session-state/active.md`)。

### 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# 1) 拆表前后语义等价(对账:合并按 id 排序 == 原数组)
python3 - <<'PY'
import json, subprocess
new = json.load(open("assets/data/disease_registry.json"))
old = json.loads(subprocess.check_output(
    ["git", "show", "HEAD:assets/data/disease_registry.json"]).decode())["diseases"]
merged = sorted(new["diseases"] + new["injuries"], key=lambda r: r["id"])
print("相等:", merged == sorted(old, key=lambda r: r["id"]),
      "| diseases", len(new["diseases"]), "injuries", len(new["injuries"]))
PY

# 2) cooked 只在 offset 12–15 不同
python3 - <<'PY'
import subprocess
a = subprocess.check_output(["git", "show", "HEAD:unity/Assets/DataCooked/disease_registry.cooked.bytes"])
b = open("unity/Assets/DataCooked/disease_registry.cooked.bytes", "rb").read()
print("len", len(a), len(b), "diff", [i for i in range(min(len(a), len(b))) if a[i] != b[i]])
PY
# 期望: len 4171 4171 diff [12, 13, 14, 15]
```

---

## 4. 途中发现(两条,均已处置 / 登记)

### 4.1 词表数据模型无法表达「同一病的两个时代俗名」

- **现象**:先前未提交的改动把「湿温」由孤儿行改为挂病种 10,与「伤寒→10」并存 ⇒ 烘焙拒收
  `[lexicon-multi-row-link]`(`JudgmentLexiconTable.cs:114-118`)。
- **根因不是数据写错,而是模型缺一位**:该门只允许「一个词 → 多个病」(行内多链),
  不允许「多个词 → 同一个病」(两条单链同病);而后者正是「伤寒 / 湿温」这种时代俗名的形态。
  `AC-37-35` 的非双射要求也把「全行单链且互异」判为违规,方向相同。
- **处置**:该文件**已还原到 HEAD**(湿温回到孤儿行),漂移门因此转绿。
- **归属**:词表重做轮(T6 前后的「每病至少一词」)必须先裁这条模型缺口,否则写不出两个俗名。

### 4.2 代价表的 ConfigVersion 依赖病种注册表源文本

- `CostTable53Bake.cs:91` 用 `{代价表源, 注册表源}` 两份一起派生 ConfigVersion
  ⇒ 拆表后**必须**重烘 `53_cost_table.cooked.bytes`,否则漂移门红(`Expected 9 / But was 12`)。
- 这不是缺陷,是 ADR-014 §五 的直接后果;已在改动清单里点名,免得下一个人误判成「无关产物也要动」。

---

## 5. 未做(不得借绿)

| 项 | 状态 |
|---|---|
| `injuries` 里的行 `kind` 必须是 `injury` 的一致性校验 | **未做** —— 属 T2(`DiseaseRegistryDataSet` 增视图时同批) |
| `DiseaseRegistryDataSet.Diseases` / `.Injuries` 两视图 | **未做**(T2) |
| `min_skill_level` / `teach_point` 两列 | **未做**(T2) |
| 抽取的技法门 + 等级门 | **未做**(T4) |
| 钩子病例 | **未做**(T5) |
