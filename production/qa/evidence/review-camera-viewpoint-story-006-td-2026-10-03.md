# Story 006 TD 架构一致性评审 —— 跨系统义务对账与舒适度签核面

> Reviewer: Technical Director
> Date: 2026-10-03
> Object: `production/epics/camera-viewpoint/story-006-obligations-comfort.md`
> Code/Test: `unity/Assets/Tests/EditMode/CameraViewpoint/camera_obligations_reconciliation_test.cs`
> 独立核实方式:逐段定位 §Dependencies 节边界 + 全仓 grep + 接口源码对照 ADR-020 §Key Interfaces

## Verdict

REJECT —— 对账件的**核心价值 = 真实性**,而本件在四处交付了「假绿 / 未坐实的证据」:
① 判据实现弱于 AC 明文(整档 grep,非 §Dependencies 行级)· ② 「突变测试坐实」是**不存在**的证据 ·
③ ④ 陈旧措辞实测**在仓**,测试却静默通过 · ④ ⑥ 的绿证明的是**另一条边**而非 O-16。
`Status: Complete` 与 EPIC「6/6 Complete」在当前事实下**不成立**。

## 1. AC-2-22 六项义务的真实状态 —— 独立核实

**AC 明文判据**(story:64 与 GDD):「对方 §Dependencies 节内**行级**含 `camera-and-viewpoint`
或「系统 2」**+ 义务编号(O-12…O-16)至少其一**;只 grep 文件名全文命中**不算**」。
**实测实现**(`AssertReverseReference`,test:34-42):仅 `text.Contains("camera-and-viewpoint")`
**整档全文**——**正是 AC 自己点名的「假绿」失效模式**。且**全程未断言 O 编号**,AC 判据的第二半**未实现**。

逐项独立核实(§Dependencies 节边界已实测):

| 子项 | 接收方 GDD | §Dependencies 内实测 | 测试绿是否有效 |
|---|---|---|---|
| ① O-12 | `emergency-procedures.md` | ✅ 行 762「档位意图 … 结清 O-12 的 10 侧」 | 绿成立,但靠整档 grep(节内恰有) |
| ② O-12 | `casebook.md` | ✅ 行 317「档位意图 Casebook / Explore（结清 O-12）」 | 同上;**「39 唯一请求方 / 8 无残留」额外断言未实现**(story:95 要求) |
| ③ O-13 | 「关卡内容 + ADR-015 §一」 | 🔴 **无 GDD** | 测试 `Assert.Ignore` 显式登记不签 —— **诚实,认可** |
| ④ O-14 | `player-controller-and-movement.md` | ⚠️ 行 1048 有反向引用,但行 1632 **仍写旧措辞「一帧内恒定」**(见 §7) | 绿**掩盖**了陈旧引用差异 |
| ⑤ O-15 | `skeuomorphic-ui.md` | ✅ 行 1030 有反向引用 | 绿成立;**「F6 声明读哪一档」额外断言未实现**(story:95 要求) |
| ⑥ O-16 | `input-system.md` | ⚠️ §Dependencies 内的引用是**`Look` 轴边**(行 758/768);**O-16 本体只在「规则三」正文(行 17/173),不在 §Dependencies** | 绿证明的是 Look 轴边,**不是 O-16** |

**结论**:五项有 GDD 的接收方**确有反向引用**(独立复核认可);但**测试的判据形状与 AC 不符**,
故「六项实测 ✅」是**以弱判据冒充强判据**。③ 的 NOT-APPLICABLE 处置是唯一**完全诚实**的一项。

**计数错**:story 自陈「11 例」,文件实测 **12 个 `[Test]`**。

## 2. `ICameraRig` 对 ADR-020 §Key Interfaces 的兑现

- ADR-020(`adr-020:413-424`)明列 `ICameraRig` 成员 = `Mode{get}` / `SetMode` / `Tick` / `Camera{get}` / `YawBasis`。
- 实测 `ICameraRig.cs:46-63`:仅 `YawBasis` / `Yaw` / `Pitch` —— **四成员仍缺**(001 评审 #2 HIGH **未修**)。
- `OQ-2-6` 的 VR doc comment 义务:`grep OQ-2-6 unity/Assets/Gameplay.Presentation/` = **空** ⇒ **未交付**(001 评审 #2 明列为本故事交付项)。
- story-006 Implementation Notes 自陈交付「`ICameraRig.Camera` 签名未因扩容改动(OQ-2-6 注释在位)」——
  **实测为假**。测试 `test_vrInterface…`(test:191-204)只断言 `YawBasis/Yaw/Pitch` 存在,
  **不验 `Camera`、不验 OQ-2-6 注释、不验平面链零点判据** —— 与 story:116-117 的 QA 用例**不符**。

⇒ 本故事的 VR 接口面交付**未完成且测试不可判**。

## 3. EXTERNAL 21/23 的「不计入就绪度 ≠ 已履行」处置诚实性

- `test_ac221`(test:114-131):未结案 ⇒ `Assert.Ignore`(NUnit = skipped,**非 pass**)——**诚实 ✅**。
- `test_ac223`(test:133-148):恒 `Assert.Ignore`,且前置断言 ADR-020 含「spike」字样 ——**诚实 ✅**。
- 计入就绪度:story 与 EPIC 均**未把两条算绿**——**认可**。
- 但:宣告证据 `tests/unit/camera/`(`obligation_ledger_test.cs` / **`vr_interface_freeze_test.cs`**)
  —— 后者 **`vr_interface_freeze_test.cs` 全仓不存在**;接口面被塞进对账测试。**证据清单与真身不符**。

## 4. P0 舒适度无 BLOCKING 门 —— 有意设计是否被恰当登记

- story:51/148 明确登记「有意设计 + 须 spike 报告显式签核」;**登记面成立 ✅**。
- 但 `find production/qa/evidence -iname "*spike*"` = **空** ⇒ 签核**路径存在但零产物**;
  三条 ADVISORY 一律 NOT-RUN 是**正确**姿态(story:131 已声明「不得记绿」)。本项**认可**。
- 唯一缺口:spike(ADR-020 §Migration #5)是 `AC-2-23` 的**唯一验收证据**,
  却**无 tracked 归口**——登记在 story/EPIC 两处文本里,无独立挂账件。

## 5. EPIC 转 Complete 的正当性 —— 此刻成立吗?

**不成立。** 三处硬矛盾:

1. `EPIC.md:6` 头行 = `Complete ✅ 6/6`,`EPIC.md:57` 「## Epic Status」**仍写 `In Progress`** —— **同文件自相矛盾**。
2. Stories 表(`EPIC.md` 表)**全部仍标 `Ready`**,无一 `Complete` —— 与头行矛盾。
3. `EPIC.md` §Next Step 仍写「**Story 002 优先开工**」——未更新。
4. 事实层:001/002 **刚经评审修复**(git 近史可证);003/004/005 的双代理评审为**骨架/进行中**(`review-*003/004/005` 时间戳 20:37,均未出正式 verdict);006 本件判 REJECT。
   ⇒ 「6/6 Complete」= **以头行措辞单方面宣布**,无逐故事签核支撑。

**建议**:EPIC Status 回滚 `In Progress`,头行去掉 `Complete`;待 006 重审 + 003–005 评审收口再翻。

## 6. VR 语义欠账(`OQ-2-6`)与 P1a 接口扩容成本

- `OQ-2-6`(GDD:1371)自陈:「P0 只落接口、不落实现,**现在改接口近乎零成本**,推到 P1a 则**要动 ADR-020 的 `ICameraRig` 定义**」。
- **实测:接口未按 ADR-020 形状落**(§2)⇒ 欠账**真实存在且已开始计息** —— 「近乎零成本」的窗口**正在关闭**:
  若 P1a 前不动,`Mode`/`SetMode`/`Tick`/`Camera` 四成员与 `OQ-2-6` 的双眼语义**一并**要改 ADR-020。
- story-006 把 `OQ-2-5/2-6` 登记为 P1a 前置 —— **登记正确**;但**未在 P0 偿付**「零成本」的那一半(OQ-2-6 doc comment)。

## 7. 其他发现

- 🔴 **「突变测试坐实」是不存在的证据**:story:145 称「各对方 GDD 均含反向引用,**突变测试坐实**」;
  `grep 突变|mutation|Negative` 于测试文件 = **空**。story:97 要求「Negative fixture: 临时移除某节反向引用行 ⇒ 该项红」—— **未实现**。**撤回该措辞或补夹具。**
- 🔴 **④ 陈旧措辞实测在仓、测试静默通过**:`camera-and-viewpoint.md:709` 已把「一帧内恒定」重写为「**次序不变量**」;
  而 `player-controller-and-movement.md:1632` **仍写旧措辞**。story:63 与 GDD:1297 均要求「登记为**陈旧引用**须回刷」——
  `test_ac222_4` **不检此差异**,结果静默绿。**判据缺陷,须补差异行输出。**
- 全档 grep 的**结构性缺陷**:无法区分「§Dependencies 内」与「正文里」——任何一节被删、引用漂到正文,测试仍绿。AC 明文点名此失效模式,实现却正中此坑。
- story Completion Notes 四项 `_待填_`(Criteria/Deviations/Test Evidence/Code Review)**未填**却标 `Status: Complete`。
- 真身落点 = `unity/Assets/Tests/EditMode/CameraViewpoint/`,story 预期 `…/Camera/` —— 目录名偏差(不影响判据)。

## 8. 修复清单(建议,按优先级)

1. **[BLOCKING]** `AssertReverseReference` 改为**节内抽取**(定位 `## Dependencies` → 下一 `##`),并**同时断言 O 编号**;补 ②/⑤ 的额外断言(F6 档位声明、39 唯一请求方)。
2. **[BLOCKING]** 补 ④ 陈旧措辞差异检测(「一帧内恒定」出现 ⇒ 输出差异行,不静默通过)。
3. **[BLOCKING]** 删「突变测试坐实」措辞,**或**补 Negative fixture(临时移节 ⇒ 该项红、余项不牵连)。
4. **[BLOCKING]** 补 `ICameraRig` 的 `OQ-2-6` doc comment(偿付零成本窗口);或改 story 口径说明该面**不由本故事交付**,并修 `test_vrInterface` 与 story:116 判据一致。
5. **[BLOCKING]** EPIC Status 回滚;头行去 `Complete`;修 Stories 表与 §Next Step。
6. **[ADVISORY]** 修证据清单(删除不存在的 `vr_interface_freeze_test.cs` 引用,或补其实体);填 Completion Notes 四项;案例数改 12。
