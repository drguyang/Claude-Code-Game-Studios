# S25-3 炮制屏收口 —— 实现与判据(2026-11-06)

| 字段 | 值 |
|---|---|
| **卡** | sprint-25 · S25-3(炮制链收口) |
| **规格** | `design/ux/processing-18.md`(Draft · 用户 2026-11-06 四项裁定) |
| **债** | `TD-skeuo-21`(**已结案** —— 代码可判面) |
| **commit** | `2c1ea80a` + 工作树 |
| **回归** | EditMode `6968/6931/0/0/37` @ `16:30 CST` · PlayMode `359/355/0/2/2` @ `16:24 CST` |

---

## 1. 结论(三条)

1. **spec 的四条实现差全部落地**:行内二段确认 · 三种空态状态注 · 开屏落焦 + 重绑保持 · 点火后留在屏内。
2. **判据 5 → 13 条,全绿**;证伪轮 **M1–M4 各自转红**,四次还原 SHA-256 逐字节一致。
3. ⚠️ **真包 `[L]` 未跑**:「点火 → 完成 → 起货」与「二段态可辨」需要**重出一次包** ——
   **本件不主张这两条已验**(见 §5)。

---

## 2. 改动清单

| 文件 | 改了什么 |
|---|---|
| `Gameplay.UI/Skeuomorphic/Screens/Processing18Screen.cs` | 二段确认(第一次 `Submit` 零意图 → 行内 `── 落火` → 焦点移开即撤销 → 第二次恰一次)· 三种空态注(`炉上有药,火未熄` / `案上无料` / `无方可起`,优先序 炉忙 > 无料 > 无方,不可聚焦)· 开屏落焦 + 重绑按 `Recipe.Ordinal` 还原 · 点火被接受后留在屏内 + 候选清零 |
| `Gameplay.Boot/CompositionRootServices.cs` | 新增 `FurnaceBusy`(`ISingleFurnaceQuery`)读取面 —— **只给空态选词**,不参与准入判定 |
| `Gameplay.Boot/CompositionRoot.cs` | 把**同一个** `FurnaceBusyQuery` 实例挂上服务袋(一处真源,不新开实现) |
| `Gameplay.Boot/BootRoot.cs` | 开屏时把 `_services.FurnaceBusy` 传给 `screen.Bind` |
| `Tests/PlayMode/SkeuomorphicUI/processing18_screen_test.cs` | 5 → **13** 条 |

**边界**(未越过):
- 屏内**零 `Append`**、零 `ICameraRig.SetMode`(原有属性未变);
- 准入仍只问 `IProcessingAdmissionQuery` 一个函数 —— 单炉查询**只用于选词**,不参与过滤
  (`AC-18-20` 的「零第二实现」未被破坏);
- 42 仍不持游戏状态:候选 / 落火态都是**开屏时喂入的呈现缓存**。

---

## 3. 判据(`processing18_screen_test`,13 条)

| # | 判据 | 对应 spec |
|---|---|---|
| ① | 不可发起 ⇒ 不建行,且树上**无**灰置形态 | `AC-18-10b` / §13-1 |
| ② | 可发起 ⇒ 建行,文案 = `RecipeId` | §5.3 |
| ③ | 全不可发起 ⇒ 零行 + 无焦点落点 + 注「案上无料」 | §6.1 |
| ④ | 二段:第一次零 `Ignite`、第二次恰一次 | §7 |
| ⑤ | 落火态行内文案可辨(后缀) | §7 · 非颜色单通道 |
| ⑥ | 焦点移开 ⇒ 撤销 + 文案回退 | §7 |
| ⑦ | 点火被接受 ⇒ 留在屏内 + 零行 + 注「炉上有药,火未熄」 | §6.2 |
| ⑧ | 被拒 ⇒ 行数不变、零注(零弹窗 / 零文字报错) | §6.2 |
| ⑨ | 三种空态互斥 + 优先序 + 注**不可聚焦** | §6.1 |
| ⑩ | 开屏落焦第一行 | §7 · `TD-skeuo-21` |
| ⑪ | 重绑按 `Ordinal` 保持焦点 | §7 |
| ⑫ | 屏自有文案零数字 / 零倒计时词形 | `UI-18.2` |
| ⑬ | 准入查询 null ⇒ `Bind` 抛(fail-closed) | `AC-18-10b` 反面 |

**2026-11-06 16:20 CST 实测**:`total=13 passed=13 failed=0`(`s25-3-playmode.xml`)。

---

## 4. 证伪轮(M1–M4,逐个转红 + 逐字节还原)

| 变异 | 内容 | 红 |
|---|---|---|
| **M1** | 注掉 `FocusAfterRebuild(focusedOrdinal)` | **3 红**:开屏落焦 / 重绑保持 / 焦点移开撤销 |
| **M2** | 去掉二段分支(`if (false && _armedRowIndex != rowIndex)`) | **3 红**:落火后缀 / 撤销 / 第二次恰一次 |
| **M3** | 删「落火后候选清零」分支 | **1 红**:留屏 + 零行 + 状态注 |
| **M4** | 空态优先序对调(无料先于炉忙) | **1 红**:三种空态互斥 |

**还原判据 = SHA-256 逐字节**:四次还原后均为 `cf778fe2bd167a1aa2bea3ea62f3c6024f3e698260335a665d5aba68efe55601`,
源码 `grep -c MUTATION` = 0。

⚠️ **M4 首枪打错靶(如实登记)**:第一版替换把 `}` 吃进了注释行 ⇒ **编译失败**,XML 根本没产出
(`m4.xml` 不存在)。这不是「变异没抓住」,是**变异本身没编译**;重打后 1 红。承
`tools/falsify/README.md` 的同族警告 —— **变异脚本自己也会写坏文件**。

---

## 5. 回归三元组

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | **`6968 / 6931 / 0 / 0 / 37`** | `2026-11-06 16:30 CST` | S25-3 收口全量 | `2c1ea80a` + 工作树 |
| PlayMode | **`359 / 355 / 0 / 2 / 2`** | `2026-11-06 16:24 CST` | 同上 | 同上 |

**等式判据**:PlayMode `359 − 351 = 8` = 本件新增 8 条夹具(5 → 13),零回归;
EditMode 与基线**逐位相同**(本件无 EditMode 新增)。

复算:

```bash
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('/home/gu/.claude/jobs/6001633c/tmp/s25-3-editmode-full2.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('/home/gu/.claude/jobs/6001633c/tmp/s25-3-playmode-full.xml').getroot();print({k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})"
```

---

## 6. ⚠️ 本轮自伤一次:`AC-42-F3` 词门抓到了我自己的注释

首轮全量 EditMode **1 红**:`test_ac42f3_componentLibrary_noForbiddenItems`,
报 `Processing18Screen.cs 含禁用项` 九项全中。

**根因**:我在文件头写了「命名避让」注释块,**把避让表里那九个词逐字抄了进去** ——
而该门扫的是 **`Skeuomorphic/**/*.cs` 全文(含注释)**。

**处置**:注释改成「只写『见避让表』、不复述词形」;九个词 grep 全为 0;重跑全量 **0 红**。

**登记(可复用教训)**:避让表的正确引用方式是**指回**,不是**复述** ——
「我知道这张表」这件事本身不能以表的内容写进受扫描的文件。

---

## 7. 未做(不得借绿)

1. **真包 `[L]`**:「点火 → 完成 → 起货」与「二段态可辨」「三种空态可读」——
   需重出一次玩家包;本件只覆盖**结构面**(无像素,`-nographics`)。
2. **`AC-42-F1` ⑨ 仍 `NOT-RUN` 且阻塞签核** —— 需手柄走查 + 该 spec 过 `/ux-review`。
3. **`AC-18-24`(手柄走查)未跑** —— 与上条同批。
