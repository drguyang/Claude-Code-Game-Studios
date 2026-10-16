# Tech Debt Register

> **本册登记「已知偏离 / 未闭项」—— 不阻塞交付但须留痕的技术债。**
> 每条须带**出处**(评审原件条目号 / 文件行)与**触发条件**(何时须还)。
> 与 `production/qa/evidence/review-*.md` 分工:原件记**判定与修复**,
> 本册记**已知残留的债**——原件结案后仍悬的项。

---

## 2026-10-16 · medcons story-002 重评件 §五 9 条 Minor(sprint-13 S13-8)

**出处**:`production/qa/evidence/review-medcons-story-002-rework-2026-10-16.md` §五
(裁定 **APPROVED WITH SUGGESTIONS** —— 9 条为 Minor / 登记项,不阻塞转 Complete)。

| # | 债 | 出处 | 触发条件(何时须还) |
|---|---|---|---|
| TD-medcons-01 | `golden-v1.txt` 的 `C01_degraded_uraw 32768` 是**孤儿行** —— 无测试读取,退化值由测试本地字面量钉(自证)。应改为被测读取 | rework §五:1 | 数值轮冻结 `uraw` 退化值后;或任一 golden 行被改动时 |
| TD-medcons-02 | 「表值与 Python 参考实现同表」对 18 组合中 **5 组合**只由测试内镜像 `FixtureOutcome` 兜底 —— story 表述对这 5 行过强(行值已声明归数值轮,不构成 AC 级假绿) | rework §五:2 | 数值轮定稿行值后,须让 5 行也由真表读取 |
| TD-medcons-03 | AC-53-03 后半句「`FixParse` 为唯一数值解析入口(旋钮表)」**无载体且未登记** —— 可辩称「结构上虚空」(表内字段是枚举名 / `int`,无 `Fix` 字段) | rework §五:3 | 任一向 53 表引入 `Fix` 字段时(届时须补载体) |
| TD-medcons-04 | 门的**构建期注册**层无夹具:`AssemblyGates.RunAll()` 已接入,但 `test_assemblyGates_runAll_zeroErrors_*` **检不出注册被删**(删了反而更空 = 仍绿) | rework §五:4 | 下次动 `AssemblyGates.RunAll` 注册段时(同 b7「扫描面非空」防护同族) |
| TD-medcons-05 | `Materialize` 键碰撞硬失败分支(`internal`)无直驱载体(修正后构面不可达,纯纵深防御) | rework §五:5 | 若该分支的构面变得可达(如枚举扩展) |
| TD-medcons-06 | `Judge.Evaluate(inputs, null)` 的 `ArgumentNullException` **无测试** | rework §五:6 | 下次动 `Judge.Evaluate` 签名时 |
| TD-medcons-07 | `FindFloatLiterals` 已取全部匹配,但**同形态多字面量**的诊断完备性无测试 | rework §五:7 | 门报错信息被抱怨不够定位时 |
| TD-medcons-08 | 共享 `static readonly` `SharedPool`/`Encoder`/`Decoder` 跨测试**无 teardown** —— 对照 `.claude/rules/test-standards.md`「never shared mutable state」属偏离(当前无断言依赖 blob id,风险低) | rework §五:8 | 任一测试开始依赖 blob id 时 |
| TD-medcons-09 | EditMode 单元测试做**真实文件 I/O** + 依赖 `Application.dataPath` —— 违反「Unit tests do not call … file I/O」;`LoadTable` 走 `dataPath/../../` 解析仓根,在 **CI 检出布局变化时即红**(返工期已因此红过一次) | rework §五:9 | **CI runner 落地时**(ADR-012 矩阵)—— 高优先 |

**共同性质**:9 条均**不阻塞** story-002 转 Complete(重评已裁定),但**须留痕** ——
其中 **TD-medcons-09 是唯一有明确外部触发点(CI runner 落地)的高优先项**,
其余 8 条随「数值轮」或「下次动该文件」自然触发。

---

## 2026-10-16 · medcons story-003 评审件具名待办(sprint-13 S13-2)

**出处**:`production/qa/evidence/review-medcons-story-003-2026-10-16.md` §六 结账表。

| # | 债 | 出处 | 触发条件 |
|---|---|---|---|
| TD-medcons-10 | **B-W1**:共享 JSON 词法器(`JsonStage1Lexer`)不拒重复 property —— 影响全部 10+ binder | story-003 评审 §二 B-W1 | 任一 binder 因重复键静默丢字段时;或词法器统一加固轮 |
| TD-medcons-11 | **B-W2**:`53_delay_table` 等表**无 CLI / CI 烘焙入口**(仅 `DataBakeMenu` 菜单项) | story-003 评审 §二 B-W2 | 运行期读方立案批须补 CLI 入口 |
| TD-medcons-12 | **B-W3**:`DelayTableBinder.DomainRows` ordinal 与 Sim 侧 `DomainOutcomes` 的一致性断言**只防值分歧,不防改名** | story-003 评审 §二 B-W3 | 任一侧枚举改名时(当前由 `AssertDomainDeclarationsMatch` 兜值) |
| TD-medcons-13 | **B-W4**:`DelayTableCookedWriter` 产出的 `*.cooked` **无镜像读方**(装载归装配轮) | story-003 评审 §二 B-W4 | 装配轮装载 53 表时 |

---

## 2026-10-16 · medcons story-003 跨域缺口(sprint-13 S13-2)

| # | 债 | 出处 | 触发条件 |
|---|---|---|---|
| TD-medcons-14 | **W-5**:`ConsequenceResolved` 带真 `patient_id` ⇒ 进 `EventStream` AC-15 CAP 分支(`!IsPresent && PresentCount >= 24` ⇒ throw);当前**照发不 catch**,doc comment seam 已登记 | story-003 Completion Notes W-5 | 9 / 7a 轮裁定「离屏后果」语义时 |
| TD-medcons-15 | **W-4**:AC-5 / OQ-53-8「禁忌 / 过量代价表**永不致死**形状门」只兑现枚举域部分(结局轴 / NoEcho 作 key ⇒ schema 错);**代价表本身未建** | story-003 Completion Notes W-4 | GDD / 数值轮启动代价表时 |

---

## 2026-10-16 · 11 处方用药走查缺口(sprint-13 S13-7)

**出处**:`production/playtests/playtest-2026-10-16-s11-ac11-12-13-21-walkthrough.md` §9 前置勘察结论
(S13-7 走查包;用户 2026-10-16 裁定「照原样落包,走查用于证实缺口」)。
**性质**:本批为 **ADVISORY 级走查**,**执行 + 三签归真人**(S13-7 卡面「agent 不可代劳」)——
下列为 **agent 侧静态勘察**已确认的缺口,真人走查负责**目视证实**并触发还债。

| # | 债 | 出处 | 触发条件(何时须还) |
|---|---|---|---|
| TD-prescription-01 | **42 侧戥子形态元件缺失**(根因)—— GDD §Game Feel(`prescription-and-medication.md:868`)与 `skeuomorphic-ui.md:1617` 均登记「戥子档位读数元件 = 42 元件库黄铜侧(戥杆倾角 / 药包鼓胀)」;但 42 epic(18 story + 019–024)**无任何一条 story 交付该形态元件**。实际落地的 `BrassScaleElement` 仅 `int TickCount`(总档数)+ 黄铜色条,档位行 = N 个**同形** `Paper` + 同标签「戥子档」⇒ 相邻档**视觉完全相同**。**义务被登记、被「认领」,但形态从未实现** | 走查包 §9 根因;`CasebookScreen.cs:93`(「行内容恒定」自陈) | **真人走查确认后**——新立 42 侧 story 交付形态元件,或裁定 P0 接受无形态读数(须改 GDD,另开裁定) |
| TD-prescription-02 | **AC-11-13 结构上必红**(TD-prescription-01 的直接后果)—— 色盲模拟 + 静音下「仅凭形态分辨相邻两档」无形态可辨 ⇒ 落其 `ELSE` 分支「记为 42 元件库缺口」 | 走查包 §3/§9;`prescription-and-medication.md:978` | 同 TD-prescription-01(同一根因,两 AC 共同触发) |
| TD-prescription-03 | **AC-11-12 的「拟物载体可读」半必红**(零数字半 ✅)—— 同 TD-prescription-01 根因;`BrassScaleElement.TickCount` 是**总档数**(与当前档无关)⇒ 拟物载体**不可读当前档** | 走查包 §2/§9;`prescription-and-medication.md:977` | 同 TD-prescription-01 |
| TD-prescription-04 | **全案音频链未接通 ⇒ AC-11-21 不可判** —— 人工实测进 Play 后场景**完全无任何声音**。根因:全案**零 `IAudioCueSink` 实现**(仅接口定义)· `Boot.unity` **零 `AudioSource`**(仅 1 个 `AudioListener`)· `assets/audio/` **空目录** · 44 epic 的 **story-015/016 仍 `Ready`**(音频资产接入两条未做)· `audio_events.json` 仅 5 条(零处方条目)。⚠️ **证伪原预判「结构性静默」** —— 真相是整条链没通,「无音」不能归因于「设计成静默」 | 走查包 §11.7;44 EPIC §范围边界声明(2026-10-03) | **44 story-015 / 016 素材接入后**须重走 AC-11-21。⚠️ **影响超出 11**:全案任何音频类 AC 的听测半边(含 `AC-44-09`)在素材接入前**均不可判**,建议 44 侧一并登记 |
| TD-prescription-05 | **色盲模拟前置不可执行** —— UI 面板 `m_RenderMode: 0`(ScreenSpaceOverlay)⇒ 走相机后处理的色盲模拟**盖不到 UI**;Unity 6 URP `Rendering Debugger` 无 colorblind 栏。⇒ AC-11-13 的「色盲模拟下」前置**结构上无法满足** | 走查包 §11.4;`PanelSettings.asset:18` · `rendering.md:230` | **真人走查实测确认后** —— 若需真色盲模拟,须改 UI 渲染模式(ScreenSpaceCamera)或另立无障碍模拟通道(归 49 无障碍 / ADR-013 修订) |
| TD-prescription-06 | **病人占位胶囊**(非 11 范围,附带发现)—— 方笺页截图中 `rgb(255,0,255)` 品红占 13.89%(x 165..592, y 264..391),遮挡画面右半 ⇒ 影响方笺页视觉完整性 | 走查包 §11.5;截图 `unity/Logs/shots/20261016_111446_1x.png` | 13 病人 AI / 美术资产轮交付真病人模型时 |

**共同性质**:6 条**均不阻塞 story-005 转 Complete**(走查为 ADVISORY 级,且缺口本身即 AC-11-13 `ELSE` 分支的设计预期),
但**须留痕**。**TD-prescription-01 是根因**(02/03 是其直接后果),还债须先处置它;
**TD-prescription-05 / 06 是走查执行期新发现**(非前置勘察所得)。

---

## 登记格式(后续沿用)

```
- **[YYYY-MM-DD]** (<story / 对象>):<债描述> — 出处 <原件 §N:行> — 触发条件:<何时须还> — tracked from <story file path>
```
