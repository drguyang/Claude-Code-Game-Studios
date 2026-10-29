# S1 普查 —— `compounds[]` 是否存在「非致死伤 → 复合 → 致死病」形态(2026-10-29)

- **对象**:`ADR-035 §Migration Plan` 的 **S1**(T1 的第一步)· `ADR-035 §Risks R2`(「N-4 的必要性今日未证」)
- **目的**:判定 N-4(`白名单成员不得经 compounds[] 通向 lethal == true 的 target_id`)是一条**真门**、
  还是一条**零输入可达的门**
- **性质**:只读普查。**未改任何数据、未跑构建 / 测试 / 烘焙、未写任何门**
- **执行时刻**:2026-10-29

---

## 一、普查台账(六格,逐格可跑)

| # | 问 | 实测 | 复算 |
|---|---|---|---|
| 1 | 作者态注册表里有 `compounds` 字段吗? | **0 行** | `python3 -I -c "import json;d=json.load(open('assets/data/disease_registry.json',encoding='utf-8'));print(sum(1 for x in d['diseases'] if 'compounds' in x))"` ⇒ `0` |
| 2 | 运行时 schema 能表达 `compounds` 吗? | **不能** —— `DiseaseRegistryEntry` **无此成员** | `grep -c "Compounds" unity/Assets/Sim/RegistrySchema.cs` ⇒ **`0`**;类型定义在 `:178` |
| 3 | spec 侧有 `compounds` 定义吗? | **有,而且很完整** | `design/gdd/disease-simulation.md`:schema `:292`(`{ target_id, when:{component_id, min_position, min_duration}, bonus:{…} }`)· F1 显式项 `:787` · 校验 10(`:341` 禁环 + `target_id` 须存在) |
| 4 | 事件 / 旋钮侧是否已登记? | **已登记** | `design/registry/entities.yaml:1863` `compound_max_triggers` · `:2311-2326` `CompoundTriggered` / `CompoundExpired` 两 Kind(含载荷 schema) |
| 5 | 注册表里 `target_id` 的候选(致死病种)存在吗? | **存在** —— 8 病种中 5 个 `lethal=true` | 复算见 §二 表 |
| 6 | 注册表自述怎么说? | **自陈未建** | `disease_registry.json` 的 `_compoundsNote` 原文:「⚠️ DIS_RHEUMATIC_FEVER 的 compounds → DIS_HEART_FAILURE **未建** —— schema 无 compounds 字段(`DiseaseRegistryEntry` 无此成员)。**旗舰共病线因此当前不可求值**。归 9 的 compounds 实现轮。」 |

### 附:注册表 13 行实况

| key | kind | lethal | severity |
|---|---|---|---|
| `INJ_STAB` | injury | **true** | 4 |
| `INJ_LACERATION` | injury | **true** | 3 |
| `INJ_HEMORRHAGE` | injury | **true** | 5 |
| `INJ_CLAW` | injury | **true** | 4 |
| `INJ_BITE` | injury | **true** | 4 |
| `DIS_MALARIA` | disease | true | 4 |
| `DIS_RHEUMATIC_FEVER` | disease | **false** | 3 |
| `DIS_HEART_FAILURE` | disease | true | 5 |
| `DIS_TYPHOID` | disease | true | 4 |
| `DIS_MEASLES` | disease | **false** | 2 |
| `DIS_DYSENTERY` | disease | true | 4 |
| `DIS_TETANUS` | disease | true | 5 |
| `DIS_NEURASTHENIA` | disease | **false** | 1 |

复算:`python3 -I -c "import json;d=json.load(open('assets/data/disease_registry.json',encoding='utf-8'));[print(x['key'],x['kind'],x['lethal'],x['severity']) for x in d['diseases']]"`

---

## 二、判定 —— **今日零输入可达**(但「想多了」这个结论不成立)

**N-4 今天没有任何输入**,而且是**两层都空**:

1. **表达层空**:`compounds` 既不在作者态数据里,也不在 `DiseaseRegistryEntry` 里 ⇒ 写不出这条规则,
   也读不到它。ADR-014 阶段 2 对未知字段拒收 ⇒ 就算有人手工往 JSON 里塞 `compounds`,也会**构建失败**。
2. **数据层空**:`when.component_id` 的语义要求 component 在表内;R2.2 指定的「任何开放伤 → 感染」
   这条最有威胁的规则,**其 target(感染)在 P0 病种表里不存在**(8 病种无「感染 / 化脓」行)⇒
   即便有了 schema,校验 10(`target_id` 须存在)也会拒收。

**但「设计意图明确存在」,不能说这道门想多了**:

- `disease-simulation.md:382`(R2.2):「不把『感染 / 化脓』登记为独立伤情 —— 它是**并发症**,走 `compounds[]`(任何开放伤 → 感染)」
  ⇒ `component_id` 指向**伤情**是该字段的**既定用法**,不是假想。
- `:430-431`(R3.3):旗舰共病 `DIS_RHEUMATIC_FEVER(lethal=false)` → `DIS_HEART_FAILURE(lethal=true)`
  ⇒ **「非致死 → 复合 → 致死」这一形态在设计里已有一例**,只是源是病不是伤。
- ⇒ 只要 9 的 compounds 轮落地,「非致死伤 → 致死病」就**不是需要发明的东西,是照规格实现**。

**结论**:N-4 今天**零输入可达**,按 `ADR-035 §Risks R2` 的预置处置登记为**「零输入可达的门」**,
**不得**记作已兑现守卫。但它**有明确对象**,只是今天的数据还写不出那个对象。

---

## 三、⚠️ 额外发现(比 N-4 更要紧):N-1 的**可行集今日为空**

`ADR-035 §Decision ③` 的 **N-1** 要求 `rows[].injury_id` 所指的注册表行 **`lethal == false`**。
但注册表里 **5 件伤情全部 `lethal = true`**(见 §一 附表),**非致死行只有 3 个,全是病种**
(`DIS_RHEUMATIC_FEVER` / `DIS_MEASLES` / `DIS_NEURASTHENIA`),而它们被 **N-3** 排除
(不得是结局轴成员 / 且「伤」支要的是伤情)。

⇒ **53 的代价表照现有注册表,`injury_id` 一列「无可填」**。「永不致死」会以**「无行可填」**的方式
自动成立 —— 这是**真空真**,不是门在工作。

这不是 N-4 那类「将来会有的形态」,而是 **T2(禁忌支真跑)的一条硬前置**:

> **9 的注册表必须出现 ≥ 1 件 `lethal == false` 的伤情条目。**

最自然的落点是 `INJ_BLUNT`(钝击 / 挫伤)—— GDD `:366` 明确标注它是「**非致命路线的主力**」,
而它**未建**(注册表只建了 R2 十一项里的战伤白名单五项;其余六项 `INJ_BLUNT` / `INJ_FRACTURE` /
`INJ_DISLOCATION` / `INJ_BURN` / `INJ_COMA` / `TOX_ENVENOMATION` 均未建,`_whitelistSource` 写的是
「刻意不建 —— 它们不在白名单内」)。

---

## 四、本件**不**主张

| 项 | 状态 |
|---|---|
| N-4 该不该写进 binder | ❌ **未裁** —— 本件只证「零输入可达」,写法(不写 / 写但恒真 / 写成 canary)是 ADR 层的选择 |
| 「9 的注册表缺非致死伤情」是否登记为债 | ❌ **未登记** —— 本件只把事实摆出来;登不登、挂谁,待裁 |
| 6 项未建伤情是「债」还是「刻意」 | ⚠️ **两种读法都在册**:`_whitelistSource` 写「刻意不建」(设计选择),但对 53 的 T2 而言它是**硬前置**(后果) |
| 注册表 `_note` 的一处陈旧 | ⚠️ 该 note 前半句仍写「病种(DIS_*)一行未建」,后半句 2026-10-22 增补已说「八病种已建(id 7–14)」—— **同一条注内自相矛盾,未改**(不属本件范围,如实登记) |
| `compounds` 落地后 N-4 是否真的有对象 | ❌ **未证** —— 本件证的是「今天没有」;将来 `when.component_id` 能否指向伤情、伤能否通向致死病,须在 9 的 compounds 轮**复算** |
| 任何构建 / 测试 / 烘焙 | ❌ **一条未跑** —— 本件纯静态普查 |

---

## 五、裁定结果(2026-10-29,用户两项裁定)

| 问题 | 裁定 | 落点 |
|---|---|---|
| N-4 怎么写(今日零输入可达) | **写成 canary** | `ADR-035 §Decision ③` 新增 **N-4′**:注册表里一旦出现 `compounds` 字段,`CostTableBinder` 即 `throw` 并指明「这是到期复查提醒,不是拒绝」。**落点在 S3**,判据 = **V-9**(注入 `compounds` ⇒ 红;不动 ⇒ 绿) |
| 「9 无非致死伤情 ⇒ N-1 可行集为空」怎么处置 | **立债 `TD-sim-03`** | 债册(owner `disease-simulation` · `P0 面 = Y` · R1);`ADR-035 §Risks R5` 同步登记。`T2` 前置由「`TD-prescription-09` 一项」改为**两项**(加 `TD-sim-03`);`ADR-035 §Validation` 增 **V-10**(S3 须在「零 `lethal=false` 伤情」时**告警**并点名 `TD-sim-03`,防绿灯被读成已证明) |

⚠️ **本文件 §四「本件不主张」中的前两行仍成立为历史记录**(普查当时未裁)——裁定结果以本节为准,**原文不删**。
⚠️ **本轮仍未写任何代码 / 数据 / 门**:S1 是只读普查;canary 与 V-9 / V-10 的落点全在 **S3**(未开工)。
