# T1 · S2 —— `53_cost_table.json` 空表骨架(2026-10-29)

- **对象**:`ADR-035 §Migration Plan` 的 **S2**(T1 形状件的第二步;S1 普查已于同日结)
- **动作**:新增 `assets/data/53_cost_table.json`,内容 = **空 `rows` + 结构字段**
- **验收判据**(ADR 原文):「**惰性:既有回归零变动**」

---

## 一、落盘内容

```json
{
  "schema_version": 1,
  "harm_kind_enabled": ["contraindication"],
  "bands": {
    "contraindication": null
  },
  "max_severity_band": null,
  "rows": []
}
```

**逐字段的出处**(不自行发明):

| 字段 | 值 | 出处 |
|---|---|---|
| `schema_version` | `1` | ADR-014 §二(作者态必带)+ 与 `53_delay_table` / `53_outcome_table` 同值同形 |
| `harm_kind_enabled` | `["contraindication"]` | `ADR-035 §Decision ②` 的字面示例;`§Decision ⑥` 规定 P0 **只允许** `contraindication`(含 `overdose` ⇒ 构建期拒收) |
| `bands.contraindication` | `null` | `ADR-035 §Decision ②` 的字面示例 —— 「档数,**值归内容轮**」 |
| `max_severity_band` | `null` | 同上 —— 「值归内容轮;`0` = 只有整件伤、无程度档(合法)」 |
| `rows` | `[]` | `ADR-035 §Decision ②`:「空 = **惰性**,行为与今日逐位一致」 |

### 两处判断(已在 ADR 面内,不是新裁)

1. **`null` 而不是省略字段** —— ADR 的示例写的就是 `null`。省略会让「值归内容轮」与「漏写」在
   JSON 层面不可分,而 `null` 至少显式声明「这个字段我们知道它存在、今天没值」。
2. **不加 `_note` / `_source`** —— `disease_registry.json` 用 `_note` / `_source` 承载说明,
   但 **53 族的两张表都没有**;而 ADR-014 阶段 2 对未知字段**拒收** ⇒ 给本表加 `_note`
   等于**替 S3 的 binder 预支一条「允许 `_note`」的契约**(那不是我该在 S2 决定的事)。
   说明性内容全部留在 ADR-035 与本文件。

---

## 二、现网影响面(实测,非推断)

| 检查 | 结果 | 复算 |
|---|---|---|
| JSON 合法 + **无重复键** | ✅ 解析 OK,重复键 **0** | `python3 -I` + `object_pairs_hook` 扫描(见 §五 命令) |
| 有代码 / 数据引用它吗? | ❌ **零引用**(孤儿) | `grep -rn "53_cost_table" --include=*.cs --include=*.json --include=*.yaml --include=*.sh .` ⇒ 除 `docs/` 与 ADR 外**无命中** |
| 撞数据管线门吗? | ❌ **不撞** | 两道既有门的面各不相同:`test_sweep_noGhostArtifact` 扫 `DataCooked/*.cooked.bytes` **盘上幽灵**(本文件不产 cooked);`test_sweep_declaredArtifactNames_allTriaged` 扫**源码里的产物名字面量**(本文件不在源码里)。**没有任何门枚举 `assets/data/*.json`** |
| 是 Unity 资产吗? | ❌ 不是 | `assets/data/` 在仓库根(非 `unity/Assets/`),该目录**无 `.meta`**(`ls assets/data/*.meta` 零命中)⇒ 不需要导入 |
| 被 `.gitignore` 吞掉吗? | ❌ 没有 | `git check-ignore -v assets/data/53_cost_table.json` ⇒ 无输出 |

⇒ **静态可判:本文件今日不可能改变任何运行期行为**。

---

## 三、⚠️ 与 `data-files.md` 的一条冲突(如实登记)

`.claude/rules/data-files.md` 写:**「No orphaned data entries — every entry must be referenced by code or another data file」**。

**本文件今日正是孤儿** —— 没有 binder、没有读方(那是 **S3 / S4** 的活)。

**为什么仍然落**:
- `ADR-035 §Migration Plan` 的分档把 **T1 的形状件**与**内容**分开,S2 的判据就是「**惰性**」——
  一个没有任何读方的空表,**结构上不可能**影响既有回归;
- 反过来,若等 S3 一起做,S2 就无法单独验收「空表不改变既有行为」这一条。

**消解步骤(S3 落盘即解除)**:`CostTableBinder`(阶段 2)+ `CostTable53` 只读视图(S4)出现后,
本文件被引用 ⇒ 不再是孤儿。⚠️ **在 S3 落盘前,不得引用「本表已被门保护」** —— 它现在只是一份数据。

---

## 四、本件**不**主张

| 项 | 状态 |
|---|---|
| 「既有回归零变动」已验证 | ❌ **未跑任何测试** —— 无读方 ⇒ 没有可执行路径去跑;本件给的是**静态论证**(§二 四格),不是回归数。**不得写成「回归通过」** |
| 表 schema 已定稿 | ❌ `S3` 的 binder 才是 schema 的**执行体**;若 S3 发现 `null` 不便表达,可在 S3 改本文件(今日无读方 ⇒ 改动零成本) |
| `bands` / `max_severity_band` 的语义已定 | ❌ 只有「值归内容轮」这一条;`bands` 的**结构**(kind → 档数)来自 ADR 示例,尚未被任何代码固化 |
| `harm_kind_enabled = ["contraindication"]` 表示禁忌支可用 | ❌ **只表示「将来允许这个 kind」**;`rows` 空 ⇒ `TryLookup` 恒 false ⇒ 与今日行为逐位一致 |
| 数据文件规则的孤儿条款已被豁免 | ❌ **没有豁免** —— 本件是**有时限的偏离**,消解点 = S3 |

---

## 五、可复算命令

```bash
# ① 解析 + 重复键(TD-medcons-10 新口径)
python3 -I - <<'PY'
import json,io
bad=[]
def hook(pairs):
    seen=set()
    for k,v in pairs:
        if k in seen: bad.append(k)
        seen.add(k)
    return dict(pairs)
d=json.load(io.open('assets/data/53_cost_table.json',encoding='utf-8'),object_pairs_hook=hook)
print("keys =", list(d), "| dup =", len(bad), "| rows =", d['rows'])
PY
# 实测输出:keys = ['schema_version', 'harm_kind_enabled', 'bands', 'max_severity_band', 'rows'] | dup = 0 | rows = []

# ② 孤儿面(应无生产命中)
grep -rn "53_cost_table" --include=*.cs --include=*.json --include=*.yaml --include=*.sh . | grep -v "^./docs/"

# ③ 未被忽略
git check-ignore -v assets/data/53_cost_table.json || echo "未被忽略"
```
