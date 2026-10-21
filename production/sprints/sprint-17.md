# Sprint 17 — 2026-10-21 起 · **批 A-1 收口**

**周期**:`2026-10-21` → **`""`**(不设结束日期口径,做完即收,**收口时补记实际收口日** —— 承 sprint-14 `2026-10-18` / sprint-15 `2026-10-20` / sprint-16 `2026-10-21` 同例)
> 🔁 **2026-10-21 收口**:实际收口日 = **`2026-10-21`**(单日 sprint)。
> QA 签核 = `production/qa/qa-signoff-sprint-17-2026-10-21.md` · **APPROVED WITH CONDITIONS**(C-1/C-2/C-4 开,C-3 已结)。
> 🔁 **2026-10-21 收口时订正(晚于上句)**:该括号内的条件现状已过期 —— **C-1 / C-2 / C-3 三条均已结清**,
> 未结仅 **C-4** 一条(见本节末「✅ 收口记录」)。⚠️ 签核件的 **verdict 不改判** —— 它评的是签核时点的事实。
> DoD 11/11 全勾(逐条附可复算判据,见本节 §Definition of Done)。
**规划依据**:`production/milestones/p0-remaining-roadmap-2026-10-20.md` §6.5:783(段 = **批 A-1 收口**,Exit = **4 件 AC 勾满**)· §6.5 注(`clinic-001` 已于 2026-10-20 本体交付)·
`production/sprint-status-sprint16-archive.yaml` §`moved_out`(A5 → sprint-17)·
`docs/architecture/control-manifest.md`(Manifest Version = `2026-10-15`)
**Review Mode**:`lean`(Phase 4 的 `PR-SPRINT` 生产门按 skill **跳过** —— 非 PHASE-GATE)

---

## Phase 1 硬前置:重扫核出的五处前提错位(2026-10-21 实测)

> **本节的定位**:这四件**不是**照抄 10-20 快照写的。skill Phase 1 的硬前置要求逐件重读 `EPIC.md` §Epic Status
> 与每件 `Status:` 行 —— 本节的五条就是重扫的产物,其中 ①②④ 会改变计划的措辞,③ 改变开跑顺序,⑤ 只是台账。
> 承 sprint-15 §前提订正的形态:**规划期不订正,估计与验收判据从起点就错**。

| # | 项 | 旧说法 | 今日实测(可复算) | 对规划的影响 |
|---|---|---|---|---|
| **①** | `clinic-001` 状态 | 路线图 §6.5 注「已本体交付」 | `Status: In Review`(`story-001-baked-tables-schema-gates.md:4`)。实现**已落**:`Editor.Tools.Bake/ClinicMachine{TablesBake,Schema,ContextBinder,RoomBinder,AdjBinder,CookedWriter}.cs` 六件 + `assets/data/clinicmachine_{adj,context,room}.json` 三份种子(1 474 / 1 137 / 1 705 B)+ `clinicmachine_tables_schema_test.cs`(42 测)+ 复核原件 `production/qa/evidence/review-clinic-001-2026-10-20.md`。两条 AC 仍挂 `BLOCKED-BY-O-24-1` | `clinic-002/003` 的上游契约**实存**;状态不满足 `/dev-story`「依赖须 Complete」⇒ **见下方「偏离登记 ①」** |
| **②** | `casebook-001` 状态 | `casebook-002` 登记 `Depends on: Story 001(病例流只读投影的加载路径就位)` | `casebook-001-volume-state-contract-and-save-segment.md:4` = **`Ready`,全未实现**(`casebook/` 六件全 `Ready`) | 但 `casebook-002` 的 `Implementation Notes` ① 逐字写「本 story 以**「事件列表参数」形态解耦**」,其 6 条 AC 全打纯函数 + 反射断言,**无一条需要加载路径** ⇒ 依赖是**名义的**,可独立实施 |
| **③** | Manifest 版本 | 四件均 `2026-10-02` | 现行 `docs/architecture/control-manifest.md:5` = **`2026-10-15`**;期间新增 Cross-Cutting **之七(处方前置检查)** / **之八(判据不得用文化敏感字符串比较)** | 每件开跑前须走 `/dev-story` Phase 2 的 A/B/C 判定。⚠️ **之八直接命中 `tutorial-001`**(它要扫病名闭集词表 = 字符串比较)⇒ **见下方「偏离登记 ②」** |
| **④** | 命名撞车 | — | `unity/Assets/Sim/EnvMod.cs`(94 行纯函数)= **系统 5** 的 `EnvMod_raw`,测试 `Tests/EditMode/TimeWeather/envmod_raw_test.cs` 在册;系统 24 的 **F-24-1 也叫 EnvMod** | `clinic-003` 的落点命名须区分。实测 `grep -rn 'EnvMod' unity/Assets/Tests` 命中 **8** 个文件,**0 个属系统 24** |
| **⑤** | `A5` 现役状态 | 「实质已修完,只差刷 retro 行动项表那一行」 | `docs/tech-debt-register.md:209` 的 `TD-player-01` 已 `closed`;`production/retrospectives/retro-sprint-14-2026-10-18.md:213` 行动项 5 仍写「要么修(`AC-1-33` ③ 语义层),要么立正式 flaky registry 条目」 | 性质**属实** —— 纯台账刷账,零工程面 |

**四件 AC 面逐条读过**:各 **6** 条 `[ ]`,共 **24** 条;四件 `Status: Ready` · `Type: Logic` · `Last Updated: 2026-09-28`。

---

## 两处偏离登记(显式,不隐藏)

### 偏离 ① —— `clinic-001` 依赖状态不满足,按 `/dev-story` `[A]` 分支承风险

**用户裁定(2026-10-21)**:**承风险照排**。

- 取 `[A]` 而非 `[C]`:⚠️ **`[C]`「依赖其实做完了,只是状态没更新」在此不成立** —— `clinic-001` 的两条 AC
  (`AC-24-08` / `AC-24-12` 半边)真挂着 `BLOCKED-BY-O-24-1`(`O-24-1` 未闭)⇒
  **现在把 001 转 `Complete` 就是借绿**(本仓明令形态之一)。
- 本 sprint **不为 `clinic-001` 转任何状态**;它的结案归它自己的复核轮。
- `S17-1` / `S17-2` 的卡面须在 Completion Notes 里记这条偏离的**依据**(六件 + 三份种子 + 42 测 + 复核原件在册),
  并注明「上游**契约**实存 ≠ 上游 **story** Complete」。

### 偏离 ② —— 四件 manifest 落后 13 天,逐件开跑前判定,不预判结论

**用户裁定(2026-10-21)**:**逐件开跑前判定**。

- **不为四件预先改 `Manifest Version` 字段** —— 那等于替四件预设了 `/dev-story` 的 `[A]` 判定,且未逐件核对命中面。
- 每件开跑时按其 Phase 2 规则走 A/B/C:现读 manifest 之七/之八/delta,判定命中与否,再决定改字段(→`2026-10-15`)或记 `Manifest-Note`。
- ⚠️ **已知命中面(实施前必须核)**:`tutorial-001` 的第 ④ 项校验「病名闭集词表扫描」= 在**中文散文文本**上做子串判定,
  与 Cross-Cutting 之八**正面相交**。
  **⚠️ 2026-10-21 规划期订正(原文保留不删)**:本条初稿写「改法是把宾语从文本换成**词表 id 整数域**」——
  **该改法不可行**:扫描宾语是散文,必须先做文本匹配才能映射到 id,即「映射」这一步本身就依赖被禁的那种比较。
  之八 全文(`control-manifest.md:571-590`)的真实要求是**两条**:
  ① 谓词处**显式** `StringComparison.Ordinal`(`IndexOf` / `Contains` / `Equals` 一律带,禁默认重载);
  ② **更强形态 = 判定真源走结构化整数 / 布尔,字符串仅作人读展示** ⇒ 落在本件的形态是
  **扫描结果不得是「按前缀/标记字符判真假」,而须是结构化计数**(命中数 / 命中词条 id 列表),
  **失败与否由计数派生**,不由字符串派生。
  ⇒ 实施时按这两条落,并在该 AC 面**如实登记形态**;**不得**只改测试不断言面。

### 🔁 开跑前追加裁定 —— `S17-1` 成员判据取「甲案」（不计入上方「两处」）

> **本节的定位**:上方标题写「两处」是**成文时刻**的事实,本节是**开跑前**(2026-10-21)才产生的第三项裁定,
> 故另立而不改标题(承「原文不删」纪律)。全文记录在 `story-002` 的 **Implementation Notes 第 7 条**。

**背景(实测)**:GDD F-24 公共定义写 `S` 成员判据 = `type(c) ∈ FURN_TYPES`,而

| 对象 | 实测 |
|---|---|
| `FURN_TYPES` 闭集 | **全库零定义** |
| `FURN_TABLE` 数据文件 | **不存在** |
| `IFurnTypeSource` 生产实现 | **零**(仅 `clinic-001` 的 schema 侧接口) |
| `build_modules.json`(`module_id` 的目录) | **同样不存在** |

即 `TD-clinic-01` 自陈的那句「封 001→005 全链入口」属实。

**裁定(用户 2026-10-21)**:**甲案 —— 代理判据 + 缺口哨兵**。
家具格 = **被 `StructurePlaced` 事件占用、且在该只读视图内可达的格**。四条实施约束与两条实现陷阱见 story。

⚠️ **不得误读**:这是**代理**,不是 `type(c) ∈ FURN_TYPES` 的等价物 —— 代理**更宽**(任何放置件都算家具格)。
`O-24-1` 落地时须**重判**;若真判据与代理不等价,既有测试须**转红**,届时改判据并如实登记。

**一个有利事实(实测)**:`BakedInitial`(壳格来源)住在 `Gameplay.Presentation`,**不在 `Sim`**
⇒ 用 Sim 注册表作输入视图时,壳格**天然不在集合内**,AC 第 3 条的「壳格非格」无需额外剔除逻辑即可成立
(但「壳格内嵌家具 ⇒ 家具照常析出」仍须测试覆盖)。

---

## Sprint Goal

把 P0 的 **A-1 批四件**从 `Ready` 推到 **AC 勾满**,并在同一 sprint 内清掉它们上游的三处前提错位
(`clinic-001` 状态口径 / `casebook-001` 名义依赖 / 四件 manifest 落后)。

> **本 sprint 不做**:
> - **不改变 M3 分母** —— `未闭 P0 面 = 72` 一位不动(四件 AC 勾满 ≠ 债还;承 `qa-signoff-sprint-16-2026-10-21.md` §8.3「不得声明 M3 达成」)
> - **不为 `clinic-001` 转状态**,**不替 `casebook-001` 提前实现**
> - 不做 `clinic-004/005`(路线图 §6.5:sprint-20,前置 = sprint-18 的 G-8)
> - 不排 `tutorial-002…005`(sprint-25)

---

## Capacity(承 `production/CAPACITY-MODEL.md` §3.1 三元 + §3.2 三条约束,**不估天**)

| 元 | 值 | 出处 |
|---|---|---|
| **件数** | 计划 **5 件**(4 story + 1 记账)· 计划内完成 0 · 计划外追加 0(待收口记)<br>🔁 **2026-10-21 收口实测**:计划 **5 件** + **计划外追加 1 件**(S17-6,`AC-24-03` 无主补立)· **完成 6/6** · **追加率 20%**(落在预留区间 `[0%, 57%]` 内) | 本节 Tasks 表 + `sprint-status.yaml` |
| **复杂度分布** | **M × 4** + **S × 1** | §3.2「C」= 依赖数 + 判别力要求(人判);**卡面 6h/6h/4h/3h 仅作参考,不作排期单位** |
| **追加率预留** | 按实测区间 **[0%, 57%]** 预留额度 ⇒ 批 1 只塞 3 件,留 1 件额度 | §3.1 第三元 |
| **约束** | 每批 **2–4** 件 · 并发 **≤ 3** · 每件须有**可证伪判据**(回退即转红)才计入 done | §3.2 三条 |

**复杂度逐件理由**:

| 件 | C | 理由 |
|---|---|---|
| `S17-1` clinic-002 | **M** | 依赖 2(`clinic-001` 契约 + 系统 23 只读视图);判据单一域(连通性),但须区分 4/8 邻接与壳格 |
| `S17-2` clinic-003 | **M** | 依赖 2(`clinic-001` + `S17-1`);判别力要求高(**逐位**定点比对 + 溢出安全),但落点单文件 |
| `S17-3` tutorial-001 | **M**(不判 L) | 形式依赖 3,但其中 2 个(ADR-014 管线 / `entities.yaml`)是**已落既有基建**,第三个(词表 `.cooked`)故事自带降级路径 ⇒ 不满足「L = ≥3 依赖」的**实质**条件。**如实登记:这是一次口径判断,不是实测** |
| `S17-4` casebook-002 | **M(最轻)** | 依赖 1 且为**名义**(见前提错位 ②);6 AC 全纯函数 —— 本批唯一可在零前置下独自完成的件 |
| `S17-5` A5 回写 | **S** | 零依赖、判据单一(两处台账一致) |

**分批表**(`S17-2` 依赖 `S17-1`,须串行;其余三件互不依赖):

| 批 | 件 | 并发 |
|---|---|---|
| **批 1** | `S17-1` · `S17-3` · `S17-4` | 3 |
| **批 2** | `S17-2` · `S17-5` | 2 |

---

## Tasks

### Must Have(关键路径)

| ID | 任务 | 层/型 | C | 依赖 | 验收判据(可复算) | 测试载体 |
|---|---|---|---|---|---|---|
| **S17-1** | `clinic-002` 房间析出与连通性 | Core / Logic | M | `clinic-001`(**状态偏离,见 ①**)· 系统 23 `structure_at` 只读契约(**实存**:`StructureKinds.cs:111 TryGet` / `:117 GetInstanceAt`) | 6 条 AC 全勾:① `AC-24-10` 断开两簇 fixture **整盘** `ROOM_NONE`(单簇 L/T 形正常析出)② 4 邻接判据 —— 对角接触(`\|dx\|==1 ∧ \|dy\|==1`)不连通,与 8-邻接反例**可区分** ③ 外壳格与空地板:不产生成员、不阻断两簇判定(壳格内嵌家具照常析出)④ 开顶簇照样成房 —— **算法中无封闭性检查** ⑤ 房间标识稳定:同布局两次求值同 room id(纯函数)⑥ 输入仅 `structure_at(cell)` 只读视图:零调用 23 写路径、不持建造状态 | `unity/Assets/Tests/EditMode/ClinicMachine/clinic_room_extraction_test.cs` |
| **S17-2** | `clinic-003` 乘子求值(F-24-1 / F-24-2) | Core / Logic | M | **S17-1** | 6 条 AC 全勾:① `AC-24-01` 与手工按 F-24-1 线性求值**逐位一致**(定点 raw long)② `AC-24-01b` 超界 env_score **原样透传**(24 侧不 clamp;与 21a 的 clamp 配对归 `clinic-004`)③ `AC-24-02` `EquipMod = clamp(Σtier × 65536, 0, EQUIP_MOD_CAP)`,**`tier=0` 不进和** ④ `EQUIP_MOD_CAP < 1` 的二值语义可观测 —— score ≥ 阈值即钉顶,**不产生 1 与 CAP 之间的中间值** ⑤ `adj_sum` 逐对格查 `ADJ_TABLE`,每对有界(`n_max` 内),O(1) 查表,**只 +x/+y 各计一次** ⑥ 溢出安全:全部乘加在 int64 内,`n_max×TIER_MAX < 2^31` 下无环绕(fixture 取域边界值) | `unity/Assets/Tests/EditMode/ClinicMachine/clinic_modifier_eval_test.cs` |
| **S17-3** | `tutorial-001` 内容 schema + 六项构建期校验 | Foundation / Logic | M | ADR-014 管线(**已落**)· `entities.yaml` registry(**已落**)· 9/44/13 词表 `.cooked`(**部分在**:`assets/data/audio_events.json` 在;病名闭集表须实施期落定) | 6 条 AC 全勾:① schema 完整可绑定(`steps[]` id ∈ 6 员闭集;`predicateKind` ∈ registry ∪ `{Checkpoint}`;`narration[]` cueId ∈ 44 表 + text ≤170 且自含;`demonstrations[]` actor = mentor + clipKey;`papers[]` title ≤2 行 + body ≤170 + aspectRatio + illustrationKey)② **6 项校验全为硬失败**(JSON 合法 / 引用完整性 / 长度上限 / 病名闭集扫描 / 显式指令词族 / `predicateKind` 白名单)③ 烘出的 `48_tutorial_content.cooked` **确定性**(同输入两次字节相同,`ConfigVersion` 内容哈希一致)④ P0 种子 **17 条**过全部 6 项 ⑤ 运行期 48 **只读** `.cooked`,构建产物内零 JSON 解析器符号 ⑥ `steps` id 闭集**恰 6 员**(多/少 = 校验失败),观察步并入 ① 锚 | `unity/Assets/Tests/EditMode/Tutorial/tutorial_content_validation_test.cs` |
| **S17-4** | `casebook-002` 病例列表全序纯函数 | Foundation / Logic | M(最轻) | `casebook-001`(**名义**,见前提错位 ②) | 6 条 AC 全勾:① `SortKey(c) = (patient_id, Tick(CaseOpened), Seq(CaseOpened))` 三元升序,纯函数、**零落盘** ② **全序性**(`AC-39-02`):任意事件集合输出确定全序;同病人多病例按开案 tick;同 tick 按 `Seq` ③ **流前缀一致性**(`AC-39-03`):`OrderedList(A)` 是 `OrderedList(B)` 的前缀(A ⊂ B)④ 可序列化类型中**零**「display order / index」类 int 字段(**反射断言**)⑤ 串接组(同病人多病例「线订成叠」)**单元**由本函数输出分组标记,组内顺序同由三元组定 ⑥ 空流 ⇒ 返回空列表,不报错(**与「未结 N 计数」无关** —— 计数是 `casebook-004` 禁项) | `unity/Assets/Tests/EditMode/Casebook/casebook_sort_test.cs` |

### Should Have

| ID | 任务 | C | 依赖 | 验收判据(可复算) |
|---|---|---|---|---|
| **S17-5** | `retro-14` 行动项 5(`TD-player-01` 归口)回写 | S | 无 | `retro-sprint-14-2026-10-18.md:213` 的行动项行补结案注(**原文不删,就地加注**):引 `production/qa/evidence/impl-td-player-01-2026-10-20.md` + 溯源三元 `3938/3901/1 → 3940/3904/0/36`(2026-10-20 · 用户裁定 B 完整修复)。判据 = 债册 `:209` 的 `closed` 与 retro 行动项行**两处同值**且可 grep |

### Nice to Have

**无** —— 批 A-1 已占满「每批 2–4 件 × 2 批」的额度。追加件按实测追加率 [0%, 57%] 从预留额度承接,**规划期不预设**。

---

## Carryover(自 sprint-16 `moved_out`,逐条落点)

| 任务 | 来源 | 理由 | 落点 |
|---|---|---|---|
| `retro-14 A5`(`TD-player-01` 回写) | `sprint-status-sprint16-archive.yaml:94-96` → `to: sprint-17` | 实质已于 2026-10-20 修完,只差台账 | **`S17-5`** |
| `TD-epic-01 清 P0 面` | 同上 → `to: sprint-19` | 工序须按实测重定(telemetry 10 条 story 全 `Complete`,`In Review` = 0) | 不在本 sprint |
| `retro-14 A3`(「已证」补分支覆盖判据) | 同上 → `to: sprint-19` | 与 AC 判据同族 | 不在本 sprint |

**sprint-16 无 story 级 carryover** —— 它是零 story 交付 sprint,5 个工作项全部 `done`、`conditions_open: []`。

---

## Risks

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| **`clinic-001` 仍 `In Review`** ⇒ `/dev-story` 依赖判据不满足 | **确定** | 中 | 已由用户裁定「承风险照排」(偏离 ①);`S17-1/2` 的 Completion Notes 须记依据;**不转 001 状态** |
| **`tutorial-001` 撞 Cross-Cutting 之八**(判据禁文化敏感字符串比较),而它要扫病名闭集词表 | 中 | 中 | 实施前读之八原文;若冲突 ⇒ 判据宾语从文本改**词表 id 整数域**,并在 AC 面如实登记形态变更(偏离 ②) <br>🔁 **2026-10-21 规划期订正(原文保留不删)**:**本条缓解栏的改法不可行** —— 扫描宾语是散文,映射到 id 这一步本身就依赖被禁的比较。真实要求见上方「偏离 ②」的订正段:**显式 `StringComparison.Ordinal` + 结构化计数派生失败**。以订正段为准。 |
| **借绿:四件全 Logic** ⇒ 判据易滑向「自造断言顶替 AC 语义层」(本仓已三发) | 中 | **高** | 每件过**证伪轮**(改坏生产码 ⇒ 指定断言转红);`exactRed` 须**先论证后定预期**,禁把实得抄进 `expectRed`;`Proven` 列填可复算判据,禁写动作描述 |
| `clinic-003` 的 `EnvMod` 与系统 5 `Sim/EnvMod.cs`(`EnvMod_raw`)命名撞车 | 中 | 中 | 24 侧落点用限定名(如 `ClinicEnvModEvaluator`),文件头写明「与系统 5 `EnvMod_raw` 无关」;`grep -rn 'EnvMod'` 的判据须限定命名空间 |
| **manifest 版本落后 13 天**(四件 `2026-10-02` vs 现行 `2026-10-15`) | **确定** | 低-中 | 逐件开跑前判定(偏离 ②);**不得默认取 `[A]`** |
| `tutorial-001` 的 17 条种子要求过全部 6 项校验,而病名闭集词表未落定 | 中 | 中 | 若词表缺 ⇒ AC ④ 记 **`NOT-RUN`,不勾**;**不得**用语料外的弱词表凑绿 |
| ADR-012 IL2CPP 三格仍 **`NOT RUN`**(本机仅 Mono) | **确定** | 低 | 本批零跨平台新增面;`clinic-003` 的逐位判据**只在 Mono 立**,不声称跨平台 <br>⚠️ **2026-10-21 订正:本句「本机仅 Mono」为假** —— ADR-012 §Validation 2026-10-17 已逐格实测:Linux-x64-Mono ✅ 09-24(3/3)· **Linux-x64-IL2CPP ✅ 09-24**(StandaloneLinux64 player 3/3,与独立 Python 金标准 + Mono 三方 19/19 逐位相同)· Linux-ARM64-IL2CPP ❌ **目标不存在**(pinned 工具链 46 个 BuildTarget 无任何 arm 目标;用户裁定长期未达)· CI 常驻 ⚠️ job 已建但从未跑(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET`)。⇒ **矩阵级,非本件个体判据**。原文保留不删。<br>🔁 **2026-10-21 再订(CI 常驻)**:性质经用户裁定为 **EXTERNAL(无法完成)** —— 凭证须用户侧申请,agent 侧无路径。已入债册 **`TD-ci-01`** · 见 `p0-remaining-roadmap-2026-10-20.md` §4.5.1。⚠️ **「从未跑」仍属实,变的只是性质**;不得读成「已解决」。 |

---

## Dependencies on External Factors

- **无外部阻塞** —— 四件的上游契约全部为仓内既有件,已逐项实测在位(`StructureKinds.cs` / `entities.yaml` 35 Kind / ADR-014 管线 / `assets/data/48_tutorial_content.json`)。
- `tutorial-001` 的第三依赖(9/44/13 词表 `.cooked`)为**并行产出**:故事已设计降级路径(「依赖表未就绪」报错不崩),但**种子 17 条过全部 6 项**时词表须真在 ⇒ 缺则该条 AC 记 `NOT-RUN`。

---

## Definition of Done for this Sprint

- [x] Must Have 四件全部完成,**每件 6 条 AC 逐条勾选并附可复算判据**(禁止「已落盘」当判据)
      <br>实测:五件(S17-1/2/3/4/6)均 `[x]=6 · [ ]=0`;`grep -c '^- \[x\]'` 与 `'^- \[ \]'` 逐件核。
- [x] **每件过证伪轮**:改坏生产码 ⇒ 指定断言转红;`exactRed` 由**论证**给出后才定预期(禁把实得抄进 `expectRed`)
      <br>实测:`tools/falsify/specs/s17-*.json` 五份;`python3 tools/falsify/check_all_anchors.py` ⇒ **18/18 全绿**。
      S17-1(F1/M3)· S17-3(M1–M8/M11)的反证记录在评审原件 §六;S17-2(9 变异 + CA-M1/M2 EXACT)·
      S17-6(7/7)有独立 spec。⚠️ ~~**S17-2 的 CA-M3 未跑(NOT-RUN)**~~
      🔁 **2026-10-21 补跑结清**:**EXACT**(`ca-m3.xml` = 28/27/1,恰红 `sumExactlyMax_doesNotThrow`;
      还原后 28/28/0;还原 sha256 逐字节一致)。见签核件 §4.1。
- [x] 四份测试件在册并通过(路径见 Tasks 表)
      <br>实测:五份载体全在册且全绿(见签核件 §1)。
- [x] 全量 EditMode / PlayMode 对账,**三元组齐备**(截至时刻 · 批次 · commit),
      等式 `Δtotal = Δpassed = 新增条数` 成立,且**「消失 0」独立判据**成立(排除「新增 N 同时坏 N」的掩盖形态)
      <br>实测:`2026-10-21 16:51 CST` · `sprint-17 QA 签核轮全量 EditMode` · `e0ad379`+工作树 ⇒
      `4350/4314/0/36`,对基线 `4347/4311/0/36` ⇒ `Δtotal=3 Δpassed=3 新增=3 消失=0`(按 `fullname` 集合差独立复算)。
      ⚠️ **PlayMode 全量 NOT-RUN** —— 只有 5 测子集;见签核件 §4 第 3 行。
      🔁 **2026-10-21 回刷(晚于上句)**:**PlayMode 全量已补跑结清** ——
      `production/qa/evidence/c4-playmode-full-2026-10-21.md` ⇒ **`202/202/0/0`**,对上一全量 197
      ⇒ 集合差「新增 5 · 消失 0」= 零回归。⚠️ 跑的是 **PlayMode in Editor(Mono)**,
      **不是** IL2CPP player 内的 PlayMode(后者仍只有 S17-6 那 5 条,两者不得互替)。
- [x] `S17-5` 两处台账同值(retro 行 + 债册行)
      <br>实测:债册 `:301` = `| TD-player-01 | player-controller | closed | **Y** | R1 |`;retro `:213` 行动项行含结案注。
- [x] QA plan 存在(`production/qa/qa-plan-sprint-17-*.md`)
      <br>`production/qa/qa-plan-sprint-17-2026-10-21.md`(317 行,签核轮就地增补 S17-6 段 + 实测对账节)。
- [x] Smoke check 通过(`/smoke-check sprint`)
      <br>`production/qa/smoke-2026-10-21-sprint17.md` — Verdict **PASS**(5 PASS + 1 N/A)。
- [x] QA 签核 `APPROVED` 或 `APPROVED WITH CONDITIONS`(`/team-qa sprint`)
      <br>`production/qa/qa-signoff-sprint-17-2026-10-21.md` — **APPROVED WITH CONDITIONS**。
      条件 C-1(S17-3 AC 第 2 条内容半边,归 44/13)· ~~C-2(CA-M3 未跑)~~ · C-4(矩阵级)。
      🔁 **2026-10-21 更新**:C-2 已补跑结清(**EXACT**)⇒ **未结条件 = C-1 / C-4**;
      **C-1 是 verdict 拉档的唯一直接原因**。C-3(记账)亦已同日结清。
      C-3(记账)已于同日结清。
      🔁 **2026-10-21 回刷(晚于上句)**:C-1 亦已结清 —— `impl-c1-tutorial-media-keys-2026-10-21.md`;
      实测 cue 19 行(含 4 条 `Narration_Teach*_01`)· clipKey 6 条 · wav 41 个 ⇒ 两个 `*TableReady` **双 true**。
      ⇒ **未结条件 = C-4 一条**,且 C-4 三半已结其二(PlayMode 全量 202/202/0/0 · IL2CPP 两格已跑/第三格不可达),
      **仅 CI 常驻 job 仍开**(缺 `UNITY_CLIENT_ID` / `UNITY_CLIENT_SECRET`)。
      ⚠️ 签核件的 verdict **不改判** —— 它评的是签核时点的事实;要改须另跑一次签核。
      🔁 **2026-10-21 再订(晚于上句,用户裁定)**:CI 常驻 job **「无法完成」**,性质 = **EXTERNAL**
      (凭证须用户侧申请,agent 侧无路径)⇒ 已入债册 **`TD-ci-01`** · `p0-remaining-roadmap-2026-10-20.md` §4.5.1。
      ⚠️ **C-4 仍不结清** —— 登记性质 ≠ 满足判据;本 sprint 不因这条裁定而关闭 C-4。
- [x] 无 S1 / S2 缺陷
      <br>实测:`production/qa/bugs/` 仅 `README.md` ⇒ 零在册 bug。
- [x] 代码评审完成(`/code-review`)
      <br>五份原件齐:`review-sprint17-batch1`(S17-1/3/4)· `review-s17-2-clinic003` ·
      `review-s17-5-retro-backfill` · `review-s17-6-room-matcher`。**五件全覆盖**。
- [x] **不得**把本 sprint 的绿引作 M3 进展(~~`未闭 P0 面 = 72`~~ **73** 一位不动;承 `qa-signoff-sprint-16` §8.3)
      <br>🔁 **2026-10-21 订正**:原文 `72` 已过期 —— `TD-building-01` 立债(六订)后为 **73**。
      本 sprint 方向是 **`72 → 73`**(立债,不是清债)。独立复算:`TD-INDEX` 98 条 · P0=Y 88 · 未闭 P0 面 **73**。

---

## QA Plan

**规划时状态:不存在** —— `production/qa/` 下无 `qa-plan-sprint-17-*`。
**用户裁定(2026-10-21)**:**先跑 `/qa-plan sprint`**,先于任何实施。四件均为 Logic 型,
QA plan 定的正是「每件的测试要求 + AC → 载体映射」;不写它,「什么算 done」只能由 `/dev-story` 逐件临场判。

**⇒ 下一步(按序)**:`/qa-plan sprint` → `/story-readiness` → `/dev-story`(批 1)。

> ### 🔁 2026-10-21 当日加注:QA plan 已产出(上段原文保留不删)
>
> 上述「规划时不存在」是**本文件成文时刻**的事实。同日 `/qa-plan sprint` 已跑,
> 产物 = **`production/qa/qa-plan-sprint-17-2026-10-21.md`**(260 行),并已把增量判据回填进四件 story 的
> `## QA Test Cases`(逐条标「新增」,**原文保留**):
>
> | 件 | 回填增量 | 估计测试数 |
> |---|---|---|
> | `S17-1` clinic-002 | 5 条(含「判别力来源显式登记」) | ~12 |
> | `S17-2` clinic-003 | 5 条(含 **GDD 算例作黄金夹具 `env_score = 28000`** · `CAP < 1` 二值语义 · int64 域边界) | ~14 |
> | `S17-3` tutorial-001 | 5 条(含 🔴 **之八 `Ordinal` + 结构化计数** 强制项 · `Count > 0` 守卫) | ~20 |
> | `S17-4` casebook-002 | 1 条测试判据 + 2 条边界登记(**既有规格已完整,不 padding**) | ~12 |
>
> ⚠️ **两条边界**:
> ① QA plan 的产物是**判据面**,不是「AC 已过」;**四件 AC 一格未勾**(实施未完)。
> ② 之八 的落地形态已在 `production/sprints/sprint-17.md` §偏离登记 ② 订正留痕
> (原写「换词表 id」**不可行**,已改);QA plan 与 story 回填块采用**订正后**口径。

---

## 溯源

- **M3 Exit ②**:实测 `NOT MET` —— `未闭 P0 面 = 72` / 12 个 epic(债册 §五 求值命令;`条目 95 | {open:71, partial:5, closed:19} | P0 面 {Y:87, N:8}`)。
  本 sprint **不清偿任何 P0 面债**,不得据此推进 M3。
- **基线三元组**:`2026-10-21` · `sprint-17 规划轮(Phase 1 重扫)` · `96ecea2`。
  全量 EditMode 现值 = `4086 / 4050 / 0 / 36`(2026-10-21 05:08 CST · sprint-16 收口记账轮 · `6855ba7`)。
  > 🔁 **2026-10-21 回刷:该「现值」已过期三次,真源见 `production/sprint-status.yaml` 的 `baseline.editmode_current`**
  > —— 现为 **`4354 / 4318 / 0 / 36`**(`2026-10-21 19:09 CST` · `C-1 结清后全量` · `1cd5780`+工作树;
  > XML = `unity/Logs/regress-1cd5780.xml`,另有 `c1-full4.xml` 逐条同名同结果互为印证)。
  > PlayMode 同批回刷:`197/197/0/0` → **`202 / 202 / 0 / 0`**(19:10 CST · `regress-playmode-1cd5780.xml`)。
  > 原文保留作沿革 —— 它记的是**规划轮时点**的事实,不是今日真值。

---

## ✅ 收口记录(2026-10-21)

> **本节定位**:承 sprint-14 `:143` 同款形态。**以上全文一字未删** —— 它记的是**执行过程**;
> 本节记**收口时点**的结论。二者冲突时以本节为准,但**不得据此回写上文**。

### 一、件数:6/6 完成,其中 1 件为计划外追加

| 件 | 计划内外 | 状态 |
|---|---|---|
| `S17-1` clinic-002 房间析出与连通性 | 计划内 | ✅ done |
| `S17-2` clinic-003 乘子求值(F-24-1 / F-24-2) | 计划内 | ✅ done |
| `S17-3` tutorial-001 内容 schema + 六项构建期校验 | 计划内 | ✅ done |
| `S17-4` casebook-002 病例列表全序纯函数 | 计划内 | ✅ done |
| `S17-5` A5 回写(retro-14 行动项 5) | 计划内 | ✅ done |
| `S17-6` clinic-006 房间类型匹配 + `AC-24-03` | **计划外追加** | ✅ done |

⇒ **完成 6/6**;追加率 **1/5 = 20%**,落在本 sprint 预留区间 `[0%, 57%]` 内(见 §Capacity)。
追加理由:该 review 挂账的唯一未闭项是「上游 `ROOM_TABLE` 匹配缺口」,而 **`AC-24-03` 此前无主**
(`grep -l AC-24-03 story-*.md` 实测为空)⇒ 立卡认领并交付(24 测 + 证伪 7/7)。

### 二、Sprint Goal:达成

四条 `must-have` story 均由 `Ready` 推到 **AC 勾满**(逐件 6/6,`grep -c '^- \[x\]'` 与 `'^- \[ \]'` 复算);
三处上游前提错位(`clinic-001` 状态口径 / `casebook-001` 名义依赖 / 四件 manifest 落后)均已在
§Phase 1 逐条登记并处置。

### 三、DoD:11/11 全勾

见 §Definition of Done,逐条附可复算判据。零在册缺陷(`production/qa/bugs/` 仅 `README.md`)。

### 四、条件:C-1 / C-2 / C-3 结清,C-4 未结

| 条件 | 现状 | 依据 |
|---|---|---|
| `C-1` S17-3 AC 第 2 条内容半边 | ✅ **已结清** | `impl-c1-tutorial-media-keys-2026-10-21.md` —— cue 19 行 · clipKey 6 条 · wav 41 个 ⇒ 两个 `*TableReady` 双 true |
| `C-2` CA-M3 未跑 | ✅ **已结清(EXACT)** | `ca-m3.xml` = 28/27/1,恰红 `sumExactlyMax_doesNotThrow`;还原后 28/28/0;`sha256sum -c` 通过 |
| `C-3` 台账订正 | ✅ **已结清** | `OQ-24-7` 无裸「待裁」 |
| `C-4` 矩阵级 | ⛔ **不结清** | 三半结其二(PlayMode 全量 `202/202/0/0` · IL2CPP 两格已跑 / ARM64 不可达);余下一半已由用户裁定登记到项目规划,见 `TD-ci-01` |

⚠️ **`C-4` 不结清 ⇒ 本 sprint 不得写「条件全清」**。登记性质 ≠ 满足判据。

### 五、回归对账(三元组齐备)

| 套件 | 现值 | 截至时刻 · 批次 · commit | 对基线 |
|---|---|---|---|
| EditMode | **`4354 / 4318 / 0 / 36`** | `2026-10-21 19:09 CST` · C-1 结清后全量 · `1cd5780`+工作树 | `4347/4311/0/36` ⇒ `Δtotal=+7 Δpassed=+7 新增=8 消失=1 同名变结果=0` |
| PlayMode | **`202 / 202 / 0 / 0`** | `2026-10-21 19:10 CST` · C-4 补跑(全量)· `1cd5780`+工作树 | `197/197/0/0` ⇒ `Δtotal=+5 Δpassed=+5 新增=5 消失=0` |

> 集合差按 `fullname` 独立复算。EditMode 的「消失 1」= 改判前旧名(`settings_exposure`),
> 与「新增 8」中的新名同源,非丢失。详见 `impl-c1-tutorial-media-keys-2026-10-21.md` §4.1。
> ⚠️ PlayMode 跑的是 **PlayMode in Editor(Mono)**,**不是** IL2CPP player 内的 PlayMode(后者仅 S17-6 那 5 条),两者不得互替。

### 六、M3 分母:一位不动(且方向是「立债」不是「清债」)

`未闭 P0 面` 自 `72` **升到 `73`**(`TD-building-01` 立债,六订);epic 分布 `13/31`。
`TD-ci-01` 入册后条目 `99`,但它是**治理面**(`P0 面 = N`),**不进 P0 面分母**。
⇒ **本 sprint 的绿不得引作 M3 进展**(承 `qa-signoff-sprint-16` §8.3)。

### 七、收口时仍未做的一件(如实,不留白)

- **`/retrospective sprint-17` 未写** —— `production/retrospectives/` 最新为 `retro-sprint-14-2026-10-18.md`,
  **retro-15 / 16 / 17 三份均不存在**。⚠️ 这是**累计缺口**,不是本 sprint 独有。
  非 DoD 条目 ⇒ **不阻断收口**,列为下一步。
  ⚠️ **不得**把它写成「已排期」—— 未排期。

### 八、本 sprint 收口**不**代表以下任一

- **不代表 `clinic-machine/story-001` 收口** —— 它仍 `In Review`,三条现役理由:`FurnTypeClosureEnforced == false` /
  评审残余 🟡 未修 / IL2CPP 三格 NOT-RUN。
- **不代表 `ADR-012 §Validation` 的「CI 常驻矩阵」项可勾** —— 仍不得勾。
- **不代表签核件 §4 第 5 行的 5 项「未核」已核** —— 它们仍未核;要补须**另跑一次复核**并落新原件
  (承「评审不可事后追补」,原 verdict 不可改写)。

> **不经用户指令不 commit。**
