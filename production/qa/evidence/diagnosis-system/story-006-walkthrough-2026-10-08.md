# diagnosis story-006 走查件 —— 脉案呈现 / 词表路由 / 反幻想护栏

- **对象**: `production/epics/diagnosis-system/story-006-casebook-presentation-routing-anti-fantasy.md`
- **日期**: 2026-10-08 · **类型**: 走查([V]/[U] 半边,截图 + 签核)
- **自动化半边**: EditMode `casebook_lexicon_test.cs`(7 测)+ PlayMode `casebook_render_test.cs`(8 测,
  Required evidence)—— 跑数见卡 Test Evidence;本件只收**不可自动化抵**的部分(Guardrail 原文)。
- **签核位**: 🔲 = 未签。**签核不得以开发自审抵**(Implementation Note 6)。

---

## 一、[V] 视觉走查(需截图;真机/编辑器 Play Mode)

| AC | 判据 | 截图 | 签核 |
|---|---|---|---|
| AC-8-23 [U] 截图半边 | 五行 + 问诊栏截图俱全;只查视诊 ⇒ 触诊等行**空行**截图(空行场景的数据喂入腿 NOT-RUN,归 §四.5) | 🔲 | 🔲 主创 |
| AC-8-36 | 解析带宽 = 信息不遮挡(五通道读数同屏可读);「不遮挡」QA case 的**零遮挡滤镜节点断言子半边**无测试落点 ⇒ NOT-RUN(§四.6),本位以截图判 | 🔲 | 🔲 主创 |
| AC-8-38 | 阴性不抹世界体征。**[L] 断言半边已自动化** —— EditMode `test_ac8_38_negativeRenderPath_8prefix_zeroWorldLayerTypes`(8 前缀零 13 动画/shader 类型 + 影子负夹具);[V] 半边 = 世界侧渲染未变的截图 | 🔲 | 🔲 |
| AC-8-40 | 主角侧渗墨不进脉案(渗墨只在主角侧) | 🔲 | 🔲 主创 |
| AC-8-41 | 两套语汇不互染(脉案语汇 ↔ 主角渗墨语汇) | 🔲 | 🔲 |
| AC-8-42 | V-8.6 神经衰弱:五行实笔无异常(R3 行已在 registry —— disease story-003 **Complete**,不记 BLOCKED-BY;视觉半边签此位) | 🔲 | 🔲 **医学** |
| AC-8-19 | 泄漏像素测:两病人截图逐像素哈希相等(**像素腿 NOT-RUN,batch 无渲染面 ⇒ 归本位真机执行**;PlayMode `test_smoke_buildDeterminism_*` 仅为构建确定性冒烟,**不承担本 AC 落点**) | 🔲 | 🔲 |
| AC-8-29 | 截图半边(半边自动化,半边签此位) | 🔲 | 🔲 主创 |
| AC-8-24 / 8-28 | 走查子项(态数无第四态、无「未查」文案;空白病名栏 = 可写性可见) | 🔲 | 🔲 |

## 二、[U] 用户体验签核

| AC | 判据 | 签核 |
|---|---|---|
| AC-8-37 | 三点不装傻(呈现不替玩家诊断;**医学签核**) | 🔲 **医学** |
| AC-8-43 | UI-8.4 负向声明 —— **自动化树遍历半边已落**(PlayMode `test_ac8_43_negativeDeclarations_treeWalk` + EditMode UXML 属性/文本面);QA case 7 明文「树遍历 **+ 走查双签**」⇒ **[U] 双签在本位**,未签 = 本 AC 未收口(禁借绿) | 🔲 主创 🔲 双签人 |
| AC-8-23 | 焦点序走查半边(键鼠:七停序 + 落空行反馈无提示文案) | 🔲 主创 |
| AC-8-31 | 无灰按钮(P0 无不可用动作)与「等级不锁手段」两向走查互证 | 🔲 |
| AC-8-46 | 技能等级不进呈现(等级提示零出现) | 🔲 主创 |

## 三、手柄与焦点

| 项 | 状态 |
|---|---|
| AC-8-44 | **NOT-RUN**(卡 Status 明文:缓办 + ADR-013 §6.6 假设 6 spike 未跑)—— 禁借绿 |
| 键鼠焦点序(UI-8.2 订正面) | 已自动化(`casebook_render_test.test_ac8_23_focusOrder_*`) |
| 手柄同键双触发 | 未跑(归 AC-8-44 / spike) |

## 四、NOT-RUN 清单(不静默)

1. **AC-8-44 全项**:手柄走查 + 假设 6 spike —— 缓办(卡面登记)。
2. **AC-8-19 像素腿**:batch 无渲染面,截图逐像素哈希归本走查件真机执行(§一)。
3. **[V]/[U] 全部签核位**:截图与双签未执行(本文件即收口处)。
4. **AC-8-51 端到端腿**:词表半边(四态路由)已自动化覆盖;「裸 Interact ⇒ 37 `CaseOpened`」的
   **接线集成半边无生产调用点** —— 具名归 case-system epic(37)/ interaction-system epic(4)接线轮,
   届时补端到端测试(评审 S1 登记,半边拆分见卡 AC-8-51 注)。
5. **AC-8-23「不因已查集合重排」数据场景**:39 接线前无数据喂入 API,重排腿不可执行 ⇒ 归本件
   截图半边(自动化只断初始树序)。
6. **AC-8-36「零遮挡滤镜节点断言」子半边**:滤镜节点属渲染栈,当前脉案无后处理接入 ⇒ 无落点;
   本位以截图判 + 登记(评审 q13)。
7. **AC-8-13 [I] 端到端呈现半边**:依赖注记(`confidence_leak_test.cs` 头注)称 9 侧
   `Project(Sign_j)` 未落地 —— **该注记已陈旧**(disease-simulation story-004 现 Status: Complete),
   但 9→8 投影接线不在本 story 授权面 ⇒ 本轮 NOT-RUN,**依赖状态须在下轮回写更新**(评审 q6)。
8. **焦点序卡文差异**:卡 AC-8-23 措辞「…→ 病名 → 置信度」为 OQ-CB-5 订正**前**旧序;
   GDD UI-8.2(2026-10-06 订正)权威序 = **面色 → 语声 → 姿态 → 呼吸 → 触感 → 问诊栏 → 病名**。
   实现与测试以 GDD 为准,**已随本轮回填卡 Completion Notes**(原「评审可证」声明曾悬空,评审 S2 后补齐)。

## 五、验证命令(可证伪)

```bash
# EditMode 静态半边(7 新测:词表/形状/DTO/词表/UXML/AC-8-38)
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.DiagnosisSystem" \
  --output unity/Logs/story006-editmode.xml
# ⇒ 157 total / 156 过 / 0 红 / 1 跳(跳 = 既有 test_ac834)

# PlayMode 结构半边 + 双真源交叉(Required evidence:casebook_render_test,8 测)
unity test unity --mode PlayMode --filter "DaYiJingCheng.Tests.PlayMode.DiagnosisSystem" \
  --output unity/Logs/story006-playmode.xml
# ⇒ 11 / 11 过 / 0 红(3 既有 + 8 新)

# 全量 EditMode(基线 2978/2931)
unity test unity --mode EditMode --output unity/Logs/story006-full.xml
# ⇒ 2979 / 2932 过 / 0 红 / 1 inconclusive(既有 SettingsExposure)/ 46 跳
```

> 全量 CLI `exit 2` = 既有 `SettingsExposureTest` 1 条 Inconclusive 所致,基线同形。
