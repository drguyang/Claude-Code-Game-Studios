# ADR-014: 数据管线与 JSON 解析器(作者态外部化 · 构建期烘焙 · 两阶段工具链)

## Status

Accepted

> **2026-09-15 起草。** 三条用户裁定已锁:① **R-7 + R-8 合并为一份 ADR** —— 出货形态这一
> 承重决策把「解析器选型」与「Addressables 数据管线」锁在一起,拆成两份会互相预判对方的裁决;
> ② **出货形态 = 构建期烘焙** —— JSON 是唯一作者态,`FixParse` 在导入期跑,出货的是
> **烘焙产物**(`Fix` 的 raw `long`),玩家构建内**零解析器 · 零 `FixParse`**;
> ③ **解析器 = Newtonsoft 仅作词法器 + 自研 per-schema 绑定层** —— 用 `com.unity.nuget.newtonsoft-json`
> 的 `JsonTextReader` 只做 tokenize,**禁用 `JsonConvert.DeserializeObject<T>` 这类对象反序列化**
> (它把 JSON 数字经 `double`/`decimal` 中转,与 ADR-006 §一 的「拒浮点、唯一入口」直接相冲)。
> §S1 的基线口径(外置到文本是既有先例,非本文新立)与 §六 的 random-events 调和亦已锁定。
> 独立评审由下一轮 `/architecture-review` 进行。

## Date

2026-09-15

## Last Verified

2026-09-15

## Decision Makers

dr_guyang(用户 · **2026-09-15 三条裁定,均照准**)· technical-director(起草与裁决)
· unity-addressables-specialist(数据管线 / 分组 / 预载)· unity-specialist(引擎复核)
· 系统 21a 物品与配方 · 52 随机事件导演 · 9 疾病与伤情(数据加载端)

## Summary

**21a / 52 / 9 三份 GDD 都把数值的「唯一合法作者态」指向 `assets/data/*.json`**
(`item-database.md:290` · `random-events.md:617-628` / `:1023`)**,但这条管线的承重件 ——
作者态怎么出货、解析器选谁、`FixParse` 住哪、Addressables 怎么分组预载 —— 全案无 ADR**
(架构复核 R-7 / R-8)。本 ADR 裁决:**作者态 = `assets/data/*.json` 文本**(基线与仓库既有
先例一致,**非本 ADR 新立**);**经构建期两阶段工具链(NEWTONSOFT 词法器 → 自研绑定 + 校验)烘焙为
deterministic 的烘焙产物**,`Fix` 字段落盘为 raw `long`;**玩家构建只加载烘焙产物,运行期不含
任何 JSON 解析器与 `FixParse`**;Addressables 单一 `data-core` 组启动预载;`ConfigVersion`
由源数据集内容哈希派生,与 ADR-010 §七 存档头联动;E-13 的 6.2+ 抛异常行为在启动期硬失败兜住。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core / Asset Pipeline(Addressables)+ Editor 工具链 |
| **Knowledge Risk** | MEDIUM —— `com.unity.nuget.newtonsoft-json` 的 API 在训练数据内;**Addressables 6.2+ 的失败抛异常行为是 post-cutoff,须实测**(`docs/engine-reference/unity/breaking-changes.md:71-89`)。本裁决的结构面(两阶段工具链 / 烘焙产物 / 零运行期解析器)不依赖任何 post-cutoff API |
| **References Consulted** | `docs/engine-reference/unity/breaking-changes.md:71-89`(Addressables 6.2+ 抛异常)· `.claude/rules/data-files.md`(数据文件规制)· `docs/engine-reference/unity/VERSION.md` |
| **Post-Cutoff APIs Used** | Addressables 6.2+ 的失败抛异常语义与 `handle.Valid` / `TryLoad` 变体(须实测) |
| **Verification Required** | ① Unity 6.3 的 Addressables 加载失败究竟抛异常还是返 null(E-13 实测);② `JsonTextReader` 对 JSON 字符串 token 的取值路径;③ 烘焙产物跨运行逐位确定性(进 ADR-012 黄金夹具) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-005**(Accepted —— 定点域 · 零 `UnityEngine` 的门 A / 门 B)· **ADR-006**(Accepted —— `FixParse` 唯一入口 · 拒浮点 · 自定义编码器 · 导入期转换)· **ADR-010**(Accepted —— 存档头 · `ConfigVersion` u32 · §七 迁移协议)|
| **Enables** | 21a 数据文件实现 · 52 数据加载 · 9 数据加载(本文解其硬前置) |
| **Blocks** | **21a 物品与配方的数据文件落地**(`TR-itemdb-025/026/027`)—— 在本文 Accepted 前,数据文件无出货形态 |
| **Ordering Note** | 本文定的是**管线形状**;真正的烘焙工具实现在 21a 进实现时随第一个数据文件一并落地。**不阻塞** 52 / 9 的 GDD 撰写 |

## Context

### Problem Statement

21a / 52 / 9 三份 GDD 均已把数值的作者态指向 `assets/data/*.json`,且 21a 明写「唯一合法作者态 =
`assets/data/*.json` 文本」(`item-database.md:290`)。**但「这些文本怎么变成运行期读到的数」
全案无裁决** —— 架构复核把这一整块记为两个承重缺口:

- **R-7**:Addressables 数据管线(分组 / 启动预载 / 配置版本号与存档头联动 / 6.2+ 抛异常行为),
  Foundation / MEDIUM,直接关系 21a / 52 的数据加载;
- **R-8**:JSON 解析器选型(`com.unity.nuget.newtonsoft-json` vs 手写子集读取器),
  Foundation / **LOW**,是 21a 数据导入的硬前置,也是 **`FixParse` 的接缝**。

**不决定的代价**:ADR-006 §一 的「唯一入口 · 拒浮点 · **导入期**一次性转换」在**导入期**这个
词上是**悬空的** —— 没有管线,就没有「导入期」。而 ADR-006 §Effects 明写「`FixParse` 只在导入期
运行,不进入运行期热路径 —— 零运行期成本」,该承诺**无从兑现**,除非先有本文。

### Current State

**仓库当前无任何 Unity 工程骨架**:`Packages/` 不存在,`assets/` / `Assets/` 不存在,
全仓除 `.claude/*.json` 外**无一个游戏数据 JSON**(只读盘点,2026-09-15)。因此本文是
**在第一个数据文件被写下之前**先定形状 —— 这正是抢在 21a 进实现前置前置的正确时机。

### Constraints

- **ADR-006 §一 / §五**:外部数据 → `Fix` 的路径**必须有唯一入口**(`FixParse`)且**拒浮点字面量**;
  `Fix` **不可经 Unity 内置序列化器承载**(`readonly struct` + `private readonly long` ⇒ 静默归零),
  **必须走自定义编码器**。
- **ADR-005 门 A / 门 B**:sim 层零 `UnityEngine` 引用 —— sim **不能**直接调 Addressables,
  数据加载必须落在**边界程序集**,sim 只接收已解析的领域数据。
- **ADR-006 §三**:单一舍入模式 `ROUND_HALF_AWAY_FROM_ZERO`,舍入在整数域内完成。
- **ADR-010 §七**:`ConfigVersion`(u32)进存档头,决定**后续窗口**用什么值;与 `SaveVersion` **分离**。
- **E-13**(`breaking-changes.md:71-89`):Addressables **6.2+ 加载失败抛异常而非返 null**,
  加载失败要能落到**构建期 / 启动期硬失败**,而非运行时 null 解引用。
- **`.claude/rules/data-files.md`**:文件名 `[system]_[name].json` · key 用 camelCase · 每文件须有 schema
  · 破坏性改版须 version。
- **21a AC-21a-41 / 52 / 57**:`Fix` 字段读入类型为 Q16.16 `int64`,**不接受浮点字面量**,
  任一字段写浮点即**导入期硬失败**。

### Requirements

- 作者态与出货态**分离**:作者只写文本,运行期只读确定性产物。
- `Fix` 的转换**只经 `FixParse`**,且发生在**构建期**(兑现 ADR-006 §Effects 的零运行期成本承诺)。
- 玩家构建内**不含 JSON 解析器与 `FixParse`** ⇒ 解析器逐平台逐位性**从运行期确定性面上消失**。
- 全部**构建期校验**(AC-21a-7…12 守恒 / 区间 / 长度、schema 成员、state 必须为字符串)在**烘焙期硬失败**。
- 烘焙产物**deterministic**:同源 JSON ⇒ 逐字节一致(可作 ADR-012 黄金夹具)。
- 数据**启动预载**:首次 `Step` 前数据必须常驻(`Step` 无错误返回通道,ADR-006 §一)。
- 加载失败**硬失败**,绝不 null 解引用(E-13)。
- `ConfigVersion` 与存档头联动,可据以排查回放不符(ADR-010 §七)。

## Decision

### §S1 基线声明:外置到文本是既有先例,不是本文新立

**本文的承重产物是「出货形态 + 工具链」,不是「要不要外置到 JSON」。** 后者**已经**是仓库既有口径:

- `item-database.md:290` —— 「**`ItemDef` / `Recipe` 的唯一合法作者态 = `assets/data/*.json` 文本**」;
- `random-events.md:617-628` —— 「凡承载 `Fix` 数据者**只能**落 `assets/data/*.json` 并经 `FixParse` 读入 …
  json 不是「绕过 Addressables」,而是它的**承载格式**」;
- `skill-system.md:436` / `diagnosis-system.md:1895` 等亦同向。

**本文把「必须外部化」的硬口径收窄到承载 `Fix` 的字段**,并据此记一条理由:
**`Fix` 值在物理上不可能住在 `.asset` / `MonoBehaviour` / `ScriptableObject` 字段里**
(ADR-006 §五:内置序列化器对 `Fix` 静默归零),所以承载 `Fix` 的字段**必须**外置;
而纯 `int` 计数 / 枚举若 GDD 声明为**固定常量**(非调参旋钮),以具名 C# 常量存在是合法的,
本文**不**把「全部数值必须外置」升为铁律。
**⚠️ 这是本文的自加限定,不是仓库既有口径** —— 若用户要求「全部数值(含 `int`)一律外置于数据文件」,
本条需回改;起草时已就此向用户报备(2026-09-15)。

### 一、作者态与出货态分离:三层

| 层 | 形态 | 谁写 | 谁读 | 是否进版本控制 |
|----|------|------|------|----------------|
| **作者态** | `assets/data/[system]_[name].json` 文本 | 内容作者 / 策划 | 烘焙工具 | ✅ 唯一真源 |
| **出货态** | `[system]_[name].cooked`(deterministic 二进制) | 烘焙工具(构建期生成)| 运行期边界程序集 | ✅ **提交并可复现**(见 §三 陈旧门) |
| **运行期** | 领域 struct / 数组(已 `FixParse`) | 边界程序集装载产物 | sim | — |

- **作者态 key 用 camelCase**(`data-files.md`);**文件名 `[system]_[name].json`**
  (21a 原稿写 `assets/data/items.json` **不合规**,应为 `item_database_items.json` 等
  —— `item-database.md:850-851` 已记)。
- **`Fix` 承载字段在 JSON 里写成字符串**(如 `"offset": "3/4"` / `"half_life": "196608"`),
  **不是 JSON 数字**。理由见 §四。
- **🔴 2026-09-17 新增登记:`case_judgment_lexicon.json`(37 病例系统的判词表)** ——
  文件 `assets/data/case_judgment_lexicon.json`,作者态亦然(本层),**烘焙期**产出
  `case_judgment_lexicon.cooked`(同一 `data-core` 组)。
  - **内容**:病名词表条目 ↔ **版本化整数 ordinal**(`Judgment.lexicon_id`,见 ADR-008 §三);
  - **绑定层职责**:**构建期**做「**非一一对应**」校验 —— 该表与 9 的 `disease_registry.json`
    **不得一一对应**(否则「换名不换壳」,病名成了病种 id 的直译,ADR-008 指出的第六处泄漏面照旧敞开);
  - **进 `ConfigVersion` 覆盖集**(§五 D-21-13 承接条 ①)⇒ 表变即版本变;
  - **不进呈现层作为 ordinal**:玩家可见的是**本地化文本**,ordinal 只在载荷与构建期比对中出现。

### 二、出货形态 = 构建期烘焙

**JSON 是唯一作者态;出货的是烘焙产物;玩家构建零解析器、零 `FixParse`。**

- `Fix` 字段在作者态写作**整数字面量或 `分子/分母` 字符串**,由 `FixParse` 在**烘焙期**
  一次性转成 Q16.16 内部 `long`(ADR-006 §一);烘焙产物里该字段就是 raw `long`;
- **运行期不存在从 JSON 文本到 `Fix` 的路径**,因为运行期根本没有 JSON 文本 ——
  作者态的解析失败在**烘焙期**即构建失败,不进运行期(ADR-006 §一 的「导入期转换」由此兑现);
- **收益**:① 解析器 / `FixParse` 的逐平台逐位性风险**从运行期确定性面上清零**
  (R-8 的 LOW 评级由此坐实为「结构性规避」,而非「靠实测赌」);② 兑现 ADR-006 §Effects
  的「零运行期成本」;③ 烘焙产物 deterministic ⇒ 直接复用 ADR-012 的双级黄金夹具
  (单元级黄金哈希 = 烘焙产物字节哈希)。
- **代价(用户已知并接受)**:改一个数值须重跑烘焙(开发期是一条编辑器菜单命令,**非全量构建**),
  而非改文本即时生效。**此为「构建期烘焙」相较「运行期解析」的显式取舍。**

### 三、两阶段工具链

**阶段 1 —— 作者文本 → token 流(词法)**:`com.unity.nuget.newtonsoft-json` 的 `JsonTextReader`。
**只做 tokenize,不做对象反序列化。**

- **词法器默认值 pin(2026-09-21 · 承 `architecture-review-2026-09-20.md` RC-7)**:
  构造 `JsonTextReader` 时**必须显式设 `DateParseHandling = None` 与 `FloatParseHandling = None`**
  —— Newtonsoft 在 `DateParseHandling` 非 None 时会把**看起来像日期的字符串 token**(如 `"3/4"`)
  转成 `DateTime` ⇒ `Fix` 字段原文被改写;`FloatParseHandling` 决定数字 token 落 `long` / `double` /
  `decimal`。两条改为**硬要求**(§Implementation Guidelines 补充),并配**负向夹具**:`"3/4"` 被当日期 /
  数字 token 落在 float 路径的夹具断言**烘焙期硬失败**。
- **禁用 `JsonConvert.DeserializeObject<T>` / `JObject.Parse` 等对象反序列化 API** ——
  它们把 JSON 数字经 `double` / `decimal` 中转后填入对象,是一条约 `FixParse` 的**静默浮点泄漏通道**,
  与 ADR-006 §一 直接相冲。**该禁令由一条 grep / IL 守卫守住**(见 §Validation)。

**阶段 2 —— token 流 → 领域对象 + 校验 + 烘焙(绑定)**:**项目自有的 per-schema 绑定层**。

- 逐 schema 维护**已知键白名单**:**未知键 = 烘焙期硬失败**(防拼写错误导致的静默丢字段);
- 对**每个 `Fix` 承载字段**调用 `FixParse.Parse(字符串字面量)`;
- 对**纯 `int` 计数字段**(`weight` / `stack_max` / `qty` / `duration_ticks` / `skill_gate` /
  `min_quality` 等)按 `int` 读,**不列入 `Fix` 解析集**(D-21-17);
- 枚举(`processing_state` / `category`)**按明文字符串**读入并映射(21a AC-21a-26:**禁 int 编码的 state**);
- **数据 schema 版本化**:每个数据文件带 `schema_version`,绑定层据此分派;**版本不匹配 = 硬失败**
  (`data-files.md`:破坏性改版须 version)。

**校验(与绑合同期,全部硬失败)**:

- **守恒上界**(AC-21a-8,含产出侧乘子:`QTY_MULT_MAX × Σ(weight × outputs_i.qty) > EFF_MAX × Σ(weight × inputs_j.qty)` 拒);
- **区间校验**(AC-21a-9…12:`cap` 和 / `QTY_MULT` 区间 / `RETAIN` 区间 / `ENV_MOD` 区间);
- **长度校验**(`axis_offset_by_quality[]` / `quality_character[]` 长度 **必须 = `MAX_QUALITY`**);
- **全量校验失败 = 构建失败**,不是警告 —— ADR-006 §一 的「解析失败即构建失败」由此机械化。

**负向夹具**:`tests/unit/item_database/fixtures/invalid_[rule].json`(`item-database.md:974`),
每条断言**烘焙期硬失败**。

### 四、`FixParse` 接缝

- `FixParse.Parse(string literal, RoundMode mode = ROUND_HALF_AWAY_FROM_ZERO)` 是**唯一的
  字符串 → `Fix` 入口**(ADR-006 §一);绑定层**只**以 JSON 字符串 token 的原文喂它;
- **`Fix` 承载字段必须是 JSON 字符串,不是 JSON 数字** ——
  **理由**:JSON 数字 token 会被 Newtonsoft 自身解析成 `long` / `double` / `decimal`(`JsonTextReader.Value`),
  **数字一旦被词法器转成 CLR 基元,浮点即已在内存**。把 `Fix` 字段写成字符串(`"offset": "3/4"`),
  词法器交出的就是 `String` token,**浮点从不进入内存**,「拒浮点」由**结构性**保证而非行为约束;
- 该写法**收紧**了 ADR-006 §一 的作者指引(原文「写作整数字面量或 `分子/分母`」未限定引号),
  但**不与之冲突**(两者都拒浮点)—— 系本 ADR 在编码层的收紧;
- **舍入**在整数域内完成,`ROUND_HALF_AWAY_FROM_ZERO` 为全局常量(ADR-006 §三)。

### 五、Addressables 数据管线

- **分组**:烘焙产物归**单一 `data-core` 组**。理由:全部数据是 sim 的**启动前置**
  (无数据不能跑 `Step`),拆组不换来任何收益,徒增预载编排复杂度;
- **启动预载**:`data-core` 组在**首次 `Step` 前**全量预载并常驻(`Step` 无错误返回通道,
  ADR-006 §一)。装载发生在**边界程序集**(ADR-005 门 A:sim 零 `UnityEngine`,不能直调 Addressables),
  边界程序集把**已解析的领域 struct / 数组**交给 sim;
  - 接口形如 `IDataProvider { TDataSet Load<TDataSet>() where TDataSet : struct; }`,住边界程序集;
  - **2026-09-16 补注(承 `O-6-12`,结清与 ADR-015 §四 的驻留冲突)**:**「常驻」只约束小体量的
    启动前置数据**(物品 / 配方 / 病种 / 事件表 / 几何 / 生态区 / POI 定义)。**逻辑导航格除外** ——
    它**按 chunk 切片**(`world_nav_{chunk}.json`、产出于 ADR-022 §五),**按需经 Addressables 加载,
    不要求全图常驻**;未驻留 chunk 视为全 `block`,**驻留与否不改变判定**(表现态降级)。
    故本节与本 ADR 的「单一 `data-core` 组预载」**不冲突** —— 切片产物仍属 `data-core` 组,
    只是**按需激活**(地址可寻址条目,非首帧全量解压)。
- **E-13 兜底**:所有加载走 `try/catch` + `handle.Valid` 检查(或 `TryLoad` 变体),
  **失败 = 启动期硬失败并给出清晰错误**,绝不 null 解引用(`breaking-changes.md:71-89`);
- **`ConfigVersion` 联动(兑现 ADR-010 §七 / TR-itemdb-032)**:
  - 烘焙工具在**每个**产物里戳 `ConfigVersion`(u32);
  - **`ConfigVersion` 由源数据集内容哈希派生**(对 `assets/data/` 源文本集求哈希后截断 u32)——
    任何数值改动**自动**改版本号,免去「改了值忘了手动加版本号 ⇒ 存档回放静默不符」的手工失误;
    (单调计数器是备选,但需人肉维护,故不取。**此为设计裁定,可调。**)
  - 启动时比对产物 `ConfigVersion` 与存档头 `ConfigVersion`(ADR-010 §一 头部 / §七):
    **不匹配非致命**(ADR-010 §七:已发生事件不受影响,后续窗口用新配置),
    启动期仅**记录并据此排查回放不符**;**致命**的只有产物 `schema_version` 不可读(安装损坏)。

#### D-21-13 收窄的承接:ordinal 映射表进覆盖集 + append-only 硬门(2026-09-17)

> **背景**:ADR-006 §Decision 二 于 2026-09-17 收窄 D-21-13 —— **载荷内枚举**改用
> **版本化整数 ordinal**(`DiseaseIdSet` / `Judgment.lexicon_id` / `ItemId` / `SimEvent.Kind`),
> 其映射表须进 `ConfigVersion` 覆盖集(用户裁定「① ordinal + 进 `ConfigVersion`」)。
> **本节是该裁定的落点** —— 收窄不能只写在 ADR-006,否则「进覆盖集」无实现。

- **① 覆盖集成员登记**。`ConfigVersion` 的内容哈希(`assets/data/` 源文本集)
  **显式包含**下列**映射表源文件**,即有它们即入哈希,无需另写穷举清单:
  - `disease_registry.json`(9 —— `DIS_*` ↔ ordinal)
  - `case_judgment_lexicon.json`(37 —— 病名词表 ↔ ordinal,**本 ADR 新增登记**)
  - `ai_enemy.json`(27)· `item_database.json`(21a)· `random_events.json`(52)—— 同性质

- **② 🔴 与 ADR-010 §七 的冲突与择一(本轮发现)**。ADR-008 起草时曾写
  「表变更 ⇒ 旧存档**拒载**」,而 ADR-010 §七 与本节上一条明写
  「`ConfigVersion` **不匹配非致命**」—— **两者不可同时成立**:
  若仅靠 `ConfigVersion` 且不匹配非致命,则 *ordinal 重排 ⇒ 旧存档把 `麻黄汤` 读成 `桂枝汤`,
  且不报错*(**静默错读**),这正是本收窄要买断的失败模式。
  **择一(不推翻 ADR-010)**:`ConfigVersion` 不匹配**仍为非致命**(原语义一字不动),
  **真正的守护另起一道更严的门** ——

- **③ 🔴 ordinal 映射表 append-only(本 ADR 新增硬门,守静默错读)**:
  - **只增不改**:新增条目取**新号**;**既有条目的号永不重用、永不改义**;
  - **构建期对基线断言**:仓库内提交一份 `assets/data/_ordinal_baseline.json`(序号 ↔ 名称的
    冻结快照)。烘焙时逐条比对 —— **任何既有条目改义 / 改号 / 复用 ⇒ 构建期硬失败**;
  - **仍走 ADR-010 §七 写契约**:新号 + `ConfigVersion` 变,旧存档照常加载(旧号含义未变 ⇒ 读数正确),
    新存档用新表。**无需运行期拒载**。
  - **既有先例**:本仓 `docs/CLAUDE.md` 的 TR Registry 纪律
    「**Never renumber existing IDs — only append new ones**」是同一条纪律的既有形态;
    本门只是把它从「人工约定」升为「构建失败」。

- **④ `PATTERN_THRESHOLD` 的覆盖登记(37)**:37 的 `PATTERN_THRESHOLD`(三案链阈值)
  **是 `assets/data/` 内的数值常量**,故**自动**入 `ConfigVersion` 哈希 ——
  改它 ⇒ 版本变 ⇒ 已发生事件不受影响、后续窗口用新值(ADR-010 §七)。
  **⚠️ 非对称性(必须记明)**:阈值可改而**旧档可正常加载**(非致命),但**同一存档读两次
  可能得出不同的 `FiredSet`**(旧窗口用旧阈值、新窗口用新阈值)。
  **这是 ADR-010 §七 的既有语义,不是本条引入的** —— 37 的 `FiredSet` 定义(**派生自读流**)
  亦然;仲裁见 `case-system.md` 的 `FiredSet` 标签订正。

### 六、与 `random-events.md:617-628` 的调和

`random-events.md:617-628` 写:「凡承载 `Fix` 数据者**只能**落 `assets/data/*.json` 并经 `FixParse` 读入 …
**json 经 Addressables 以 `TextAsset` 载入**」。**本文保留其意图、改写其字面**:

- **意图**(Addressables 是承载通道 · 数据外置 · 经 `FixParse` 读入)—— **完全保留**;
- **字面**(运行期以 `TextAsset` 载入 **JSON**)—— **修正**:运行期载入的是**烘焙产物**,
  作者 JSON 在烘焙后**不上构建**(它是源,不是产物)。

**已在该处加一条 note 指向本文**(见 §Ripples)。

## Consequences

### Positive

- **R-8 的 LOW 评级坐实为「结构性规避」**:解析器不进玩家构建,逐平台逐位性问题从根上不存在,
  无需为它赌跨平台实测;
- **ADR-006 §Effects 的「零运行期成本」由承诺变为事实**;
- **构建期硬失败**把 21a 的全部校验(守恒 / 区间 / 长度 / 类型 / 未知键 / schema 版本)钉在 CI,
  而非留到运行期;
- **烘焙产物 deterministic** ⇒ 直接进 ADR-012 单元级黄金夹具(编码器往返之外,再加「烘焙字节哈希」);
- **E-13 的抛异常行为**在启动期兜住,是明确硬失败而非 null 解引用。

### Negative / Costs

- **改数值须重跑烘焙**(用户已知并接受);开发期一条编辑器菜单命令,非全量构建;
- **多一层构建产物要守陈旧**:产物须与源同步(§Validation 的陈旧门),否则源改了产物没重烘 ⇒ 漂移;
- **绑合层是自研代码**:每 schema 一份绑定 + 白名单,是**新增维护面**(但换来的是「无浮点泄漏」的硬保证);
- **`ConfigVersion` 内容哈希**使版本号不可人为排序(它不是「第几版」而是「哪一版」);
  代价可接受(用途是排查,不是排序)。

## Alternatives Considered

### Alternative 1: 运行期解析 JSON(不烘焙)

**否决(用户裁定②)。** JSON 随构建出货,启动预载后运行期经 `FixParse` 解析。
**否决理由**:① 解析器进玩家构建 ⇒ 其逐平台逐位性**必须纳入 ADR-012 矩阵实测**,
把 R-8 从 LOW 抬成需实测项;② 运行期解析失败要硬失败而非静默,风险面更大;
③ 与 ADR-006 §Effects「`FixParse` 只在导入期、零运行期成本」的明文**相冲**。

### Alternative 2: 全量 Newtonsoft(`JsonConvert.DeserializeObject<T>`)

**否决(用户裁定③)。** 直接用对象反序列化绑定到 DTO。
**否决理由**:该路径把 JSON 数字经 `double` / `decimal` 中转填入对象,是**绕过 `FixParse` 的
静默浮点泄漏通道** —— 正是 ADR-006 存在的理由。**列为 Forbidden Pattern。**

### Alternative 3: 全手写 JSON 子集读取器

**否决。** 零依赖、零隐形浮点,与 ADR-005 门 A 精神一致。
**否决理由**:JSON 转义 / Unicode / 数字格式 / 深层递归的坑要全部自踩,错误信息质量与维护成本自负;
**仅在「出货形态 = 运行期解析」且要求玩家构建零第三方依赖」时才值得** —— 本文已达 §二 的结构性规避,
手写读取器的唯一收益(玩家构建零依赖)被烘焙形态**顺带达成**(反正解析器不进构建),
故不值。

### Alternative 4: 双轨(烘焙为主 + JSON 调试回退)

**否决。** 正常出烘焙件,调试开关回退运行期解析。
**否决理由**:两套路径必须产出**逐位一致**的结果,否则调试态与出货态漂移 ——
**恰是本项目最忌的静默失败**;且回退路径把 Alternative 1 的全部成本又搬了回来。

## Risks

| 风险 | 概率 | 影响 | 缓解 |
|------|------|------|------|
| 有人用 `JsonConvert.DeserializeObject` 反序列化某 DTO,浮点悄然泄漏 | 中 | **高**(全案数值) | Forbidden Pattern + grep / IL 守卫断言「Newtonsoft 入口仅 `JsonTextReader`」 |
| 烘焙产物与源 JSON 漂移(源改了没重烘) | 中 | 中 | **CI 陈旧门**:重烘提交的源,断言产物**逐字节一致**,否则构建失败 |
| `FixParse` / 烘焙期 Q16.16 中间乘触发 int64 回绕(与 ADR-012 的 F7 同源) | 中 | 高 | ADR-012 **F7 表示选择**(原 BLOCKING spike,2026-09-21 承 RC-4 降级 · 2026-09-23 已绿):`SplitMix64` / Q16.16 中间乘改住 `ulong` ⇒ IL2CPP C++ 有符号溢出 UB **结构性消除**;烘焙期在 Editor(Mono)跑,回绕为定义行为 |
| 白名单绑定漏收一个合法未来字段 ⇒ 烘焙硬失败挡住进度 | 高 | 低 | 硬失败是**期望行为**(静默丢字段才是灾难);新增字段时同步更新白名单即可 |
| Addressables 组漏配 ⇒ 数据未进构建 | 中 | 中 | E-13 启动期硬失败兜住(不 null 解引用);CI 冒烟断言 `data-core` 组在构建内 |
| `ConfigVersion` 派生口径与 ADR-010 解读不一致 | 低 | 中 | 本文钉「内容哈希派生」;ADR-010 §七 只要求「u32 进头部 + 决定后续窗口」,二者相容 |

## Validation Criteria

- [ ] `assets/data/[system]_[name].json` 存在且文件命名合规(`data-files.md`)。
- [ ] **烘焙确定性**:同源 JSON 两次烘焙 ⇒ 产物**逐字节一致**(字节哈希进 ADR-012 单元级黄金夹具)。
- [ ] **`Fix` 字段写浮点字面量** ⇒ 烘焙期硬失败(AC-21a-41 realized)。
- [ ] **`Fix` 字段写成 JSON 数字(非字符串)** ⇒ 烘焙期硬失败(§四)。
- [ ] **未知键** ⇒ 烘焙期硬失败。
- [ ] **守恒 / 区间 / 长度校验**(AC-21a-7…12)在烘焙期硬失败(负向夹具 `invalid_[rule].json`)。
- [ ] **`state` 为 int 编码** ⇒ 烘焙期硬失败(AC-21a-26)。
- [ ] **玩家构建内无 Newtonsoft 引用、无 `FixParse`** —— 程序集 / IL 守卫断言(与 ADR-005 门 B 同法)。
- [ ] **启动预载**:首次 `Step` 前 `data-core` 常驻;加载失败 ⇒ **启动期硬失败**,无 null 解引用(E-13)。
- [ ] **陈旧门**:CI 对提交的源重烘,产物与提交的产物**逐字节一致**。
- [ ] **`ConfigVersion`** 存在于产物中,且与存档头比对逻辑符合 ADR-010 §七。
- [ ] **ordinal 映射表 append-only 硬门**(2026-09-17 新增):构造一个「改既有条目号 / 改义 / 复用号」
      的夹具 ⇒ **构建期硬失败**;构造「只追加新号」的夹具 ⇒ 通过,且 `ConfigVersion` 变、旧档仍可加载。
- [ ] **`case_judgment_lexicon` 非一一对应校验**:构造一张与 `disease_registry` 一一对应的词表 ⇒ 构建期硬失败。
- [ ] **词法器默认值 pin(2026-09-21 承 RC-7)**:负向夹具 —— `"3/4"` 被 `DateParseHandling` 当日期改写 ⇒ 硬失败;数字 token 落 float 路径 ⇒ 硬失败

## Implementation Guidelines

1. **烘焙工具**落 `tools/asset-pipeline/`(目录结构规约);以编辑器菜单命令 + `AssetPostprocessor` 触发;
2. 词法器**只**用 `JsonTextReader`;对象反序列化 API 全禁;构造 `JsonTextReader` 时**必须显式设
   `DateParseHandling = None` 与 `FloatParseHandling = None`(2026-09-21 承 RC-7)**;
3. **`Fix` 承载字段在 JSON 里写字符串**;`int` 计数字段写 JSON 数字;
4. **一个逻辑数据集一个产物**(`item_database.cooked` / `random_events.cooked` / …);
5. 边界程序集实现 `IDataProvider`,把已解析领域数据交给 sim(ADR-005 门 A:sim 不碰 Addressables);
6. 首个数据文件落地时,同步落 `invalid_[rule].json` 负向夹具集;
7. 烘焙产物**进版本控制**,由 CI 陈旧门守住与源的一致。

## Ripples(本次改动集)

- **`docs/architecture/adr-014-data-pipeline-and-json-parser.md`**(本文,新建)。
- **`docs/architecture/tr-registry.yaml`**:`TR-itemdb-025 / 026 / 027` 升 `covered`;
  `TR-itemdb-032` 加 `ADR-014`(状态不变);`TR-randomevents-010` 视复核裁定。
- **`docs/architecture/traceability-index.md`**:明细 + 汇总 + 优先修复清单 + 变更历史同步。
- **`docs/registry/architecture.yaml`**:加 `data_pipeline` 接口契约 + API 决策 + Forbidden Pattern。
- **`design/gdd/random-events.md:617-628`**:加一条 note 指向本文 §六(调和运行期 JSON 的字面)。
- **`.claude/docs/technical-preferences.md`**:Architecture Decisions Log 补 ADR-014 条目。
- **🔴 2026-09-17 本轮追加**(承 37 二轮 `/design-review` 的 D-21-13 收窄裁定):
  - **`docs/architecture/adr-006-fixed-point-boundary-contract.md`** §Decision 二 新增
    **D-21-13 口径收窄**小节(载荷内枚举 = 版本化整数 ordinal;跨持久化配置态枚举仍用稳定字符串名);
    §Validation Criteria 补两条门;GDD Requirements 表 D-21-13 行就地更新。
  - **`docs/architecture/adr-008-case-event-stream.md`** §三 `Judgment` 形状(含 `lexicon_id`)
    + Key Interfaces 载荷订正 —— 其 `Judgment.freehand_text` 是 ADR-006 Amendment A
    「`Payload` 无引用字段」的**唯一例外**,已在 ADR-006 就地登记。
  - **`design/gdd/case-system.md`** 规则九 / 规则四 / AC-37-15 —— 词表路径与构建期校验的 GDD 侧对齐。

## GDD Requirements Addressed

| GDD | 系统 | 需求 | 本节 |
|-----|------|------|------|
| `design/gdd/item-database.md` | 21a 物品与配方 | 数据文件格式与位置(`assets/data/*.json`)(TR-itemdb-025) | §一 / §二 |
| `design/gdd/item-database.md` | 21a 物品与配方 | Addressables 分组与预载(TR-itemdb-026) | §五 |
| `design/gdd/item-database.md` | 21a 物品与配方 | 构建期 schema 校验(TR-itemdb-027) | §三 |
| `design/gdd/item-database.md` | 21a 物品与配方 | 配置版本号与存档头联动(TR-itemdb-032) | §五 |
| `design/gdd/random-events.md` | 52 随机事件导演 | `Fix` 数据落 `assets/data/*.json` 经 Addressables 承载 | §二 / §六 |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情 | 数据加载边界(无数据不可运行) | §五 |
| `design/gdd/case-system.md` | 37 病例系统 | 判词表(`lexicon_id`)须版本化 ordinal 并进 `ConfigVersion`(ADR-008 §三 / ADR-006 D-21-13 收窄) | §一(登记)/ §五(D-21-13 承接条 ①②③) |
| `design/gdd/case-system.md` | 37 病例系统 | `PATTERN_THRESHOLD` 为外置数值常量,改动须可追溯 | §五(D-21-13 承接条 ④) |
| `docs/architecture/adr-006-fixed-point-boundary-contract.md` | ADR-006 | D-21-13 收窄后的**承载件**(ordinal 映射表的烘焙 / 覆盖集 / append-only 门) | §一 / §三 / §五 |
| `docs/architecture/adr-006-fixed-point-boundary-contract.md` | ADR-006 | 导入期转换的承接件 | §三 / §四 |
