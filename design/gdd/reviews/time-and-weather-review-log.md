# time-and-weather GDD — 评审日志(系统 5 时间与天气)

> 文档:`design/gdd/time-and-weather.md` · 逐条修订的完整口径以 GDD 正文为准,本日志只记评审事实。

---

## Review — 2026-09-19 — Verdict: ~~MAJOR REVISION NEEDED~~ → **✅ Approved(当日修订 + 用户裁定免二轮)**

Scope signal: **L**(多系统整合:6/7a/17/18/21a/52/42/44/1/9 · 4+ 公式 · 无需新 ADR)
Specialists: `full` —— game-designer · systems-designer · qa-lead · network-programmer · ux-designer · audio-director(6 并行)+ creative-director(Opus 串行综合)
Blocking items: **9** | Recommended: ~10(当日全量落盘)
Prior verdict resolved: **First review**(2026-09-18 成稿,本条为首轮)

### 九条 BLOCKING 与落点

| # | 根因 | 修订落点 |
| --- | --- | --- |
| B-1 | F-5.1 夜判定的取模语义在 C# `%`(截断余数)下静默判错;原稿「朴素比较恒假」的说法不准确(真实失效 = 漏判环绕段,半对且静默);`NIGHT_END`/`NIGHT_SPAN` 双重参数化 | 前置具名算子 `FMod`/`FDiv` + 单一参数化 `(NIGHT_START, NIGHT_SPAN)` + 数值算例;`AC-5-20` 反例哨兵 |
| B-2 | 季节公式实际不用 `TICKS_PER_SEASON`(全文孤儿声明),把「一季=一天」答死,与 Player Fantasy 冲突 | **R-5-D**:真·季长参数 + `SEASONS_PER_YEAR ≥ 2`(=1 装载拒绝)+ `year_index` 登记 P0 零消费者 |
| B-3 | 钳制口径三处打架(F-5.4 伪代码含 clamp ↔ 规则六禁 5 钳制 ↔ Tuning Knobs 钉死输出域)—— 5 侧钳制 = 好机器无声吞环境惩罚 | `EnvMod_raw` 允许越界、**唯一钳制点在 21a F1**;`AC-5-19` 机器化 |
| B-4 | 天气「逐 tick 独立掷骰」与 Game Feel「连续变化不是瞬切」相悖(TPD=1440 时数秒换天) | **R-5-A**:块哈希 `Roll(WorldSeed, FDiv(t, WEATHER_BLOCK_TICKS), EcozoneOf(cell))`;零历史/不进流的立法意图不变;呈现层平滑被禁(三件套纪律) |
| B-5 | `cell` 指代未定 —— 24 医馆分量与 5 环境分量若不同格,`EnvMod_total` 自相矛盾 | **R-5-B**:消费方传入 + 三调用点钉死(21a=医馆房间格与 24 同格 / 17=资源点格 / 42=玩家格);5 无默认格兜底;`AC-5-14` 同格断言 |
| B-6 | **幻影边两条**:「天气→52 强度轴」在 52 公式(五变量)无落点;「5→9 季节系数」被 9 自禁(`disease-simulation.md:650`) | **R-5-C**:52 边降级 P1a(`OQ-5-8`),9 边撤销;Summary 立「P0 活边清单」(仅 3 条);**P0 天气=纯呈现 = 显式风险接受**;新增正向义务 `AC-5-18` |
| B-7 | 双向记账失称:1/9/18(及 13/17)仍记「5 无 GDD / 未设计」 | Dependencies 注⑥ + **九档涟漪**(1/9/17/18/13/42/44/52/systems-index;52 只订账不改已结案机制) |
| B-8 | AC 可执行性:6 条 grep 式判据 + 3 条依赖未落地的外件却记为可验(「借来的绿」) | AC 全表改造:**分组 A–D · 21 条 · 级别列**(BLOCKING 17 / ADVISORY 1 / **EXTERNAL 4**:AC-5-07 BLOCKED-BY-ADR-012 三格 CI+F7 · AC-5-17 前半 BLOCKED-BY 数值轮 · AC-5-18 数值半 · AC-5-21 BLOCKED-BY-49);判据一律反射/类型/夹具化(仿 AC-20-03 / PresentationDtoGuard / ADR-017 §二) |
| B-9 | 无障碍零兜底:暗夜可读性只有意向句、UX Flag 指向不存在的载体(`design/ux/` 缺、49 无 GDD) | `AC-5-21`(镜像 AC-42-G3 去标注盲测)+ UX Flag **如实 BLOCKED-BY-49**;非颜色冗余载体归 42/44(B-18) |

### 专家分歧与裁决

ux-designer 要求「天气转变须有非视觉冗余通道」vs audio-director 引 AC-44-09 禁转变提示音 ——
creative-director 裁决:**合法通道 = 持续性声床的存在/缺席**(雨声本身),禁的是 onset 音形;
`AC-5-10` 改为去标注盲测(听不出哪段含缺陷 jingle 才算过)。

### 四项用户裁定(2026-09-19,AskUserQuestion,均照准推荐案)

**R-5-A** 块哈希 · **R-5-B** cell 消费方传入钉三调用点 · **R-5-C** 天气→52 降级 P1a+正向 AC · **R-5-D** 真·季长。

### 结案方式

修订当日全部落盘后,用户裁定 **[B] 接受修订、直接标 Approved(免二轮)**。
⚠️ **免二轮 = 显式风险接受,不是「已核对」**。**重开触发条件(四者任一)**:
① **ADR-012 三格 CI 落地时 `AC-5-07` 判红**(尤其 F7:int64 回绕在 IL2CPP 为 UB —— `FMod` 内部乘法与 `SplitMix64` 同踩此线);
② 数值轮对 `g(·)`(`OQ-5-4`)的裁定与 B-3/R-5-B 形状冲突(clamp 回填 5 侧、恒 0 退化、或引入新常量域)⇒ AC-5-18/19 变红;
③ 实现期 `skylight(t)` 被 42/4 误用为**判定输入**(破 B-12 / OQ-5-7 口径 —— 该读数仅呈现);
④ **`OQ-5-8` 在 P1a 被裁接入**(天气→52 强度轴)= 解除「P0 天气=纯呈现」的风险接受,须回 5 与 52 双侧复开。

### 未结(随各轮落地,不阻塞开工)

`OQ-5-2`(P1a 移动通道)· `OQ-5-3`(天气枚举+块内分布,实现轮)· `OQ-5-4`(`g(·)` 形状,数值轮)·
`OQ-5-5` 余量(季长量级,数值轮;形式已结清)· `OQ-5-6b`(联机呈现同步,P1b-45)· `OQ-5-8`(P1a);
数值全归用户。**随他档**:17 的 `SEASON_MULT[]` 值(`OQ-17-1`)+ 5 的 AC 计数在 systems-index 已同步为 21。
