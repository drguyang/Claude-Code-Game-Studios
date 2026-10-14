# 评审原件 — B-6-2 焦点可见性(2026-10-14)

> 依 `.claude/docs/review-workflow.md` 评审报告原件 BLOCKING 级义务落盘:
> **原判定 → 修复落点 → 验证命令**(可证伪)。对账件 / commit message 不替代本件。

## 对象与原判定

- **对象**:sprint-08 第六轮 playtest(`playtest-2026-10-13-m2-round6.md` §6)**B-6-2 · P2**
  —— 「键盘玩家看不到焦点在哪」。
- **原判定**:焦点环样式与契约 story-009(`SkeuoFocusVisible.uss` + `FocusVisibleStyle`)已全做完,
  但「焦点转移 → 施加/移除 `.focus-visible`」的**施加者从未接线**
  (story-009 注记明写「接线留待焦点接线轮」)。B-6-1 接通导航后引擎 `FocusController`
  已在移动焦点,缺的只是焦点态 → 呈现态的翻译。
- **裁定**(2026-10-14 用户):**甲案 overlay** —— 环走获焦目标内部匿名子元素
  (absolute 四向 0),不直接给目标挂类。story-021 登记的「`.focus-visible` × `.ruled`
  加载序冲突」与单槽铁律「纸纹被顶掉」两风险**结构性注销**(overlay 与目标永不同元素)。

## 修复落点

| # | 修复 | 文件 | 判据 |
|---|------|------|------|
| 1 | 焦点环施加者:面板根监听 `FocusIn/FocusOut` 冒泡,获焦 append 环 overlay 子元素(`pickingMode=Ignore`、防重入不翻倍),失焦移除 | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoRuntimeDriver.cs`(`AttachFocusRingApplier`,Awake 尾调) | 环随焦点转移(旧摘新挂)/ 防重入 / 零残留 —— `focus_ring_applier_test` 2/2 |
| 2 | USS `.focus-visible` 转 overlay 语义(absolute 四向 0 + 环图 slice;`border-width` 维持 0,2px 厚度全由环图承担) | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoFocusVisible.uss` | 黄铜 2px 贴图环冻结件(slice=2,C8 门比对件)不动 |
| 3 | 翻页焦点转移:`SubmitPageNext`/`SubmitPagePrev` 尾调 `FocusFirstOf(新页)` —— 戥子行是翻页那一刻才建的,旧页 `display:None` 后 `FocusController` 在可见树找不到可焦目标,焦点死在隐藏旧页(第一轮人工复验红根因) | `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/CasebookScreen.cs` | ⑤b 直调路径 + ⑤c 事件派发内翻页路径,两判据真 panel 断言 `focusedElement` 落新页 |
| 4 | **tick 重建竞态**(第二轮人工复验红根因):真机 20 Hz tick → `ApplyData` → 方笺页在场时 `RefreshDentchDoses` **每 tick 全量摘掉重建档位行** ⇒ 翻页落焦的持焦行 50 ms 后被移出层级 ⇒ 引擎**静默丢焦** ⇒ 方向键死在无焦点态。修 = 幂等短路:**行集只由档数单值决定**(行内容恒定、userData = 显式 ordinal),档数未变不拆建。**零缓存语义不动**(DTO 每 tick 仍重读,短路只省无变化的 DOM 拆建);不持焦点、不自实现导航(ADR-013 §五) | `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/CasebookScreen.cs`(`_builtDetentCount` 判据) | ⑤d `test_tickRebuild_focusSurvivesRepeatedApplyData`:翻页落焦 → 连续 10 轮同源 `ApplyData` → 焦点仍活 |
| 5 | 换药路径不受短路影响:档数变化 ⇒ 不短路 ⇒ 全量重建;detentCount=0(无方笺数据)留空零假数据纪律不动 | 同上 | `test_zeroDetents_noRows_zeroFakeData` / `test_dentchRows_matchDetentCount_withExplicitOrdinals` 不红 |

## 验证命令(可证伪)

```bash
# 全量 PlayMode(修复态):175/175,0 红(含 ⑤b/⑤c/⑤d + 环施加者 2 测)
cd unity && /home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode \
  -projectPath "$PWD" -testPlatform PlayMode \
  -testResults "$PWD/Logs/b62_final.xml" -runTests -logFile "$PWD/Logs/b62_final.log"
python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('unity/Logs/b62_final.xml').getroot(); \
  print(r.get('total'), r.get('passed'), r.get('failed'))"   # → 175 175 0

# 变异验证(2026-10-14 实测):撤 RefreshDentchDoses 幂等短路 ⇒ 只跑
# casebook_page_turn_test ⇒ ⑤d 恰红(15 条中唯一红,其余 14 全绿 = 无误伤),
# 失败信息即「持焦行被移出层级、引擎静默丢焦」;恢复短路 ⇒ 复绿。
# 变异轮日志:unity/Logs/b62_mutation.xml(15/14/1)

# 真机人工半(2026-10-14 用户复验):翻到方笺页按 ↓,环落第一档位行、
# 方向键逐档可走;翻回脉案页环跟行走 —— 用户原话「环跟行走」判结。
```

## 两轮人工复验红 → 根因链(存档)

1. **第一轮红**「环没跟过去」:环本身没 bug(①②已落),焦点死在**隐藏旧页**
   —— 翻页切 display 后 `FocusController` 无处落焦。修复 = ③ 翻页焦点转移。
2. **第二轮红**「还是没修好」:测试全绿(⑤b/⑤c)而真机红 —— 差异面 = 生产
   20 Hz tick 循环(测试夹具没有)。每 tick 全量重建摘掉持焦元素 ⇒ 静默丢焦。
   修复 = ④ 幂等短路 + ⑤d 竞态回归测(变异恰红实证)。

## 人工半

- 用户真机复验(2026-10-14):**「真机复验过了,环跟行走」** —— 方笺页方向键逐档可走、
  环随焦点移动;翻回脉案页同判。
- 自动化半:PlayMode **175/175**(修复态)+ 变异恰红证据 + 恢复态 175/175。
