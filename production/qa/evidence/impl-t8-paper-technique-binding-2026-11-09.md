# T8 手册页 ↔ 技法绑定 —— 实施与证据(2026-11-09)

> **范围**:`technique-unlock-map-2026-11-09.md` §5.2(钩子病例的载体 = 手册缺页)的**数据与判定**半边。
> **用户裁定**:**「按丙」**(三候选里不需要任何新裁决的那一条)。
> **上游**:T6(`17067116` 手册缺页元件)· T7(`6e93e025` 技法覆盖)。
> **本件交付「能力」,不交付「可见行为」** —— 见 §6。

---

## 1. 为什么是这一步(而不只是「顺手补个字段」)

侦察(turn 起点)查出:**`TechniqueUnlockWriter.TryUnlock` 全仓只有两个调用点** ——
起步集写入器与它自己。没有任何奇遇 / 事件 / 病例能解锁技法 ⇒ 按 T4 的判定函数,
17 行注册表里只有 **6 行**可投放(4 件伤情 + 两个 `Undeclared`),其余 **11 行永久拒**。

⇒ 链条上有三个断点:「技法怎么解锁」(甲)·「解锁了做不做得成」(乙)·「缺哪一页」(丙)。
丙是唯一**不需要新裁决**的一条(T6 已经把渲染半边做完,缺的只是「哪一页缺」这个判定)。

---

## 2. 改动清单(五层 + 一个纯函数)

| # | 文件 | 层 | 内容 |
|---|---|---|---|
| 1 | `assets/data/48_tutorial_content.json` | 源 | `schemaVersion` 1 → 2 |
| 2 | `TutorialContentSchema.cs` | 常量 | `PaperKeys` +`teachTechnique`;`NoTechnique = -1`;`ExpectedSchemaVersion` 1 → 2 **并与全局 `CookedFormat.SchemaVersion` 解耦** |
| 3 | `TutorialContentBinder.cs` | 绑定 | `TutorialPaper.TeachTechnique`;`ReadOptionalInt`(缺席 ≠ 写错);② 的第三条外部半边 `ValidatePaperTechniqueIds`;`BindResult.TechniqueIdTableReady` |
| 4 | `TutorialTechniqueIdSource.cs` | **新** | 读 9 的处置轴 `actions[].id`(`ITutorialTechniqueIdSource`)。缺席 ⇒ NOT-RUN;损坏 / 零条 ⇒ 抛 |
| 5 | `TutorialContentCookedWriter.cs` | 写 | 纸行尾 `teach_technique:i32`(每行固定 +4 B,无分支) |
| 6 | `TutorialContentTable.cs` | 读 | `TutorialPaperRow.TeachTechnique` / `TeachesTechnique`;`minBytesPerRow` 6×4 → **7×4** |
| 7 | `TutorialPaperMissing.cs` | **新** | 缺页判定纯函数 `Compute(papers, isTechniqueUnlocked)` |
| 8 | `DataBakeMenu.cs` | 装配 | 注入 `TutorialTechniqueIdSource`(默认注入,不靠调用方记得传参) |
| 9 | `48_tutorial_content.cooked.bytes` | 产物 | 1904 → **1908 B**(+4 = 1 张纸 × 4 B)· `0xD82AFB19` |
| 10 | `tutorial_paper_technique_test.cs` | **新** | **18 条**判据 |
| 11 | `tutorial_content_validation_test.cs` | 改 | 两处 `schemaVersion == 1u` → `2u` |

---

## 3. 判据(18 条 · 下表按类别归并成 14 行)

| # | 判据 | 判别力 |
|---|---|---|
| 1 | 不写 `teachTechnique` ⇒ `NoTechnique`(-1) | 缺省读成 0 ⇒ 红 |
| 2 | 哨兵**不得等于任何合法 id**(0 是止血包扎) | 把哨兵改成 0 ⇒ 红 |
| 3 | 显式 0 与缺省**不同值** | 哨兵吞掉合法 id ⇒ 红 |
| 4 | 悬空 id(99)⇒ 构建期拒收,报文点明「悬垂键」 | 说成「非法值」⇒ 红(会让人查格式而不是查轴) |
| 5 | -1 之外的负数 ⇒ 拒收 | 放开负数 ⇒ 红 |
| 6 | 写成字符串 ⇒ 拒收 | 静默回退缺省 ⇒ 红 |
| 7 | 轴缺席 ⇒ **NOT-RUN**(不抛)且 `TechniqueIdTableReady == false` | 把「没查」说成「查过没问题」⇒ 红 |
| 8 | 真仓根 ⇒ `TechniqueIdTableReady == true` | 「校验通过」没有主语 ⇒ 红 |
| 9 | 生产来源读出 `{0,1,2,3,10,11,12}` | 有人另立第二份词表 ⇒ 红 |
| 10 | `actions` 零条 ⇒ 抛 | 零条闭集 = 每条都判悬空(假绿面)⇒ 红 |
| 11 | 文件不存在 ⇒ `false`(不抛) | 把缺席当损坏 ⇒ 红 |
| 12 | cooked 往返:3 与 -1 两种值逐值不丢 | 写 / 读字段序不一致 ⇒ 红 |
| 13 | `schemaVersion` 与常量不符 ⇒ 拒收 | 版本号当装饰 ⇒ 红 |
| 14 | 缺页判定:未解锁才缺 / 已解锁不缺 / **纯知识页永不缺** / 只返被锁的、按输入序 / 查询未注入必须抛 / 无纸返空 | 任一条写反 ⇒ 红 |

**为什么单独立 `TutorialPaperMissing`**:缺页判定有两个容易写错的自由度 ——
① 纯知识页(如「望诊四要」)若被当成「查不到解锁 ⇒ 缺」,**整本手册会全白**;
② 查询未接线若默认 `false`,会把「系统没接上」渲染成「整本被撕光」。
散在屏里只能靠人读代码;做成纯函数则每条都能被夹具证伪。

---

## 4. 版本号为什么必须解耦

`TutorialContentSchema.ExpectedSchemaVersion` 原写 `= CookedFormat.SchemaVersion`(全局基线 **1**)。
T8 给纸行尾追加 `i32` ⇒ 48 的载荷布局**领先基线一格**。若仍绑全局常量,只有两种出路:

- 48 谎报版本(布局变了版本没变)⇒ 旧产物被当新布局解;
- 为 48 一件事把**全部 16 件产物**的版本抬上去。

⇒ 48 自带版本号。**这不破坏单一出处**:`CookedFormat.SchemaVersion` 是「格式基线」,
各产物的 `schemaVersion` 是「该产物自己的布局代数」—— 二者本就是两个量,原写法只是把默认值当成了等号。

---

## 5. 回归

**三元组**:`2026-11-09T14:07:25Z`(EditMode 结束)/ `14:09:00Z`(PlayMode 结束) · **全量** · `6e93e025` + 工作树。

| 套件 | 本次 | 对照基线(T7 后) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7264 / 7227 / 0 / 0 / 37`** | `7246 / 7209 / 0 / 0 / 37` @ `2026-11-09T12:46:34Z` · T7 批 · `6e93e025` | **+18 / +18** |
| PlayMode(全量) | **`371 / 366 / 0 / 2 / 3`** | `371 / 366 / 0 / 2 / 3` @ `2026-11-09T12:48:37Z` · T7 批 · `6e93e025` | **零变化** |

**等式核对**:EditMode 增量须 = 新增夹具条数 **18**(`tutorial_paper_technique_test`)
· 产物字节增量须 = **+4**(纸行数 1 × i32)

### 5.1 中途一轮红(如实登记:是夹具打错靶,不是实现错)

首轮 **`7264 / 7226 / 1 / 0 / 37`**,唯一红 = `tutorial_content_validation_test.test_check1_unknownKey_throws`:

> `夹具前提:种子须含被替换片段 → "schemaVersion": 1,`

**根因**:那条夹具有个「单点替换」助手,锚点是 `"schemaVersion": 1,` —— 它拿**会变的值**当锚点。
T8 把版本抬到 2,锚点当场失配。**实现没有问题,夹具的前提假设过期了。**

**处置**:锚点改为根对象里唯一的**结构**片段 `"meta": {`(不变),并在源码里写明
「锚点应当锚结构,不锚会变的值」。这是「变异打错靶」的教科书形态 ——
`Mutate` 自带的前置断言(`Assert.GreaterOrEqual(idx, 0)`)正是把它从「静默失效」变成「响亮失败」的那道门。

---

## 6. 未做(不得借绿)

| 项 | 状态 | 理由 |
|---|---|---|
| **谁调用 `SetMissingPaperIds`** | **未建** | 42/48 的交互宿主不存在 —— 与 T5b 的转诊写者同格:有载体 ≠ 已接线 |
| **哪一页教哪门技法**(内容) | **未填** | 今日种子那唯一一张「望诊四要」是**纯知识页**(诊断侧,不是技法)⇒ 全库 `teachTechnique` **一处未填**,缺页集合恒空。**本步交能力,不交可见行为** |
| 手册页内容本身 | 归内容轮 | `papers` 仍只有 1 条 |
| 「技法怎么解锁」(甲) | **未做** | 需要裁决(见侦察)** |
| 清创 / 蛇伤处置的执行体(乙) | **未做** | 需要数值裁定 |

> \*\* 侦察结论记录在此以免丢失:`TechniqueUnlockWriter.TryUnlock` 的生产调用点 = 起步集写入器(`{0,1}`)。
> ⇒ P0 里其余技法**永远锁着**,11 行注册表永久不进池。
