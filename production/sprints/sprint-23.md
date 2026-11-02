# Sprint 23 — 2026-11-01 起(不设结束日期,做完即收 · 承 sprint-12…22 同例)

> **三元组**:规划轮截至 **`2026-11-01`** · 批次 **`sprint-23 规划`** · commit **`1adb1ad8`**。
>
> **规划依据**:`p0-remaining-roadmap-2026-10-20.md` §6.5 sprint-23 行 + **§2.4.3 四条复核注(2026-11-01,
> `1adb1ad8`)** · `production/CAPACITY-MODEL.md` §3.1(记录三元)· §3.2(约束三条)· §3.3(追加额度两分)
> · `production/epics/death-respawn/EPIC.md` · 四张 story 卡面(含 2026-11-01 R-2 注)。
>
> ⚠️ **开工前置(不是建议,是判据)**:本 sprint **无 QA 计划**。承 sprint-21/22 同例,
> **`S23-1` 开工之前须跑 `/qa-plan sprint`**,产物落 `production/qa/qa-plan-sprint-23-<date>.md`。
>
> ⚠️ **取件前置(承 `/sprint-plan` Phase 1 硬前置)**:每件开工前**重读**该 epic 的 `EPIC.md` §Epic Status
> + story 的 `Status:` 行,不得照抄本文件快照。本 sprint 的四条复核已于 2026-11-01 实跑
> (R-1/R-2/R-3/R-4,结论落 §2.4.3 复核注与卡面 dated 注),但那是**规划时点**的读数。

---

## 〇、与总体规划的对账(先对账,再排卡)

**路线图 §6.5 的 sprint-23 原行(逐字)**:

| sprint | 段 | 件集 | 前置 | Exit 判据 |
|---|---|---|---|---|
| **sprint-23** | **death-respawn 全链**(**L 级,独占额度**) | `death-respawn-001` … `005` | **sprint-18(G-1)** | 全链 `BLOCKED-BY-9` 解除 |

**四处对账结论**(证据 = §2.4.3 复核注,`1adb1ad8`):

1. **件集实为 4 件**:`001` · `002` · `003` · `005`(R-1 实测四卡 `Ready`)。
   `004` **已 `Complete`**(2026-10-24,谓词半边;`AC-29-08/10` 全链自挂 BLOCKED-BY-9 不记绿)
   ⇒ 其**全链半**并入 `S23-1` 验收面;`006` 硬阻(死亡过渡 UX spec 不存在)**不在件集**,
   其 Ready 状态不得读成「可开工」。

2. **Exit = 全链 `BLOCKED-BY-9` 解除**,且门表 **G-1 行结案**的清偿载体 = 本 sprint 的
   `S23-1`(001 + 004 全链半)收口核 —— R-2 实测根因已大部落地(谓词层 + 接线层,
   `TD-sim-01` closed),门行按纪律**不由记账轮自裁**,归本 sprint 收口核。

3. **前置实质满足**:sprint-18(G-1)的债面已还(G-2 关联的 `TD-persist-01` 亦 closed,
   `AC-29-16` 已转可判);G-1 门行的「open」挂的是**验收核**,不是**开工阻**。

4. **序位检查**:sprint-23 在 §6.5 序列位于 sprint-22 之后(已收口 ✅ · 复评 PASS `2407d04b`);
   sprint-26 的前置含 sprint-22 不含 23,无被挤压约束。**硬序未违反。**

---

## Sprint Goal

**死亡与复活全链(玩家侧)收尾。** 把 29 的四件 Ready 做完:`PlayerDied` 结算入口
(补结算链余段,9 侧生产件已在)、掉落清单流派生、技能掉级(int 截断 + 折叠重放)、
复活传送命令(桩先行)。收口时 `AC-29-01…10/16/17` 全链可判,G-1 门行结案,
路线图 Exit「全链 `BLOCKED-BY-9` 解除」达成。**本轮独占额度,不塞任何计划外件**
(承 CAPACITY-MODEL §3.2「L 件单独占批」;sprint-22 的 6 件追加教训不得重演)。

---

## 一、分母实测(2026-11-01 · `1adb1ad8` · 复算命令 = 路线图 §1.1 脚本 + 债册/smoke 脚本)

| 量 | 实测 | 与上一真源对比 |
|---|---|---|
| P0 story 总数 | **237** | §1.1 原 226 ⇒ +11(其间立卡) |
| 已 `Complete` | **213**( Complete 族,含 4 个带括号注变体) | §1.1 原 182 ⇒ +31(sprint-21/22 两轮 + 债清轮) |
| `Ready` / `In Review` / 部分完成 / Pending | **21 / 1 / 1 / 1** | ⇒ **剩余 24** |
| 债册条目 | **144**(`closed` 127 · `open` 13 · `partial` 3 · `done` 1) | sprint-22 收口 142/127/13/3 ⇒ closed +3 |
| **未闭 P0 面** | **0** | M3 Exit 第二条合取仍**真** |
| smoke 22 条 | `test` **15** · `manual` 3 · **`none` 4**(`#8`/`#9`/`#18`/`#22`) | sprint-22 收口同值 |

> ⚠️ **剩余 24 里与本 sprint 相邻的**:sprint-24 的 `processing/004` 前置含本 sprint 的 002 无关面,
> 不构成挤压;sprint-26 前置 = 19+22(均已收口)⇒ 26 随时可排,23 不挡它。

---

## 二、Capacity(承 §3.1 三元 + §3.2 三条约束,不估天)

| 元 | 值 |
|---|---|
| **件数** | 计划 **5** 卡(Must 4 · Should 1 · Nice 0) |
| **复杂度分布** | `M×4 + S×1` —— **链级 = L(独占额度)**:本 sprint 只做这一条链,不混批 |
| **追加率** | **散件 0 / 链条 0**(本链即计划内;§3.3 两分口径,新增链须显式裁定) |

**三条约束(§3.2)**:
- **并发上限 P = 3** —— `S23-2/3/5` 均只依赖 `S23-1`,可三路并行;`S23-1` 先行。
- **L 件单独占批** —— 无 Nice、无填充;任何计划外新件 = 违反本文件,须另开裁定。
- **判别力门槛 D** —— 每卡证伪轮「回退即转红」,否则不计 done。

---

## 三、Tasks

### Must Have(关键路径 · 四件全为路线图件集)

| ID | 卡 | Owner | 复杂度 | 依赖 | 验收判据 |
|---|---|---|---|---|---|
| `S23-1` | **`death-respawn/001` PlayerDied 登记与死亡结算入口**(+ **004 全链半**:`AC-29-08/10` 随本卡闭) | unity-specialist | **M** | 无硬前置(9 侧谓词+生产件已在,`DeathCandidateConsumer` 已 `Register`;**R-3 实测 = 补结算链余段 + AC 闭**,非从零) | `AC-29-01/02/05/08/10/17` 的 29 侧消费形状与**全链**逐条判定;29 **零判死公式**(Forbidden:判死归 9 的 `SelfLimited` 门);`PlayerDied` 落**世界流**(禁病史流);空背包照发(无条件发射);`AC-29-10` 有界性(`≤ 玩家数 / DEATH_COOLDOWN`)与 story-004/005 合验;⚠️ 呈现半(`IsDowned` 第二 QoS)挂 006,本卡不勾 |
| `S23-2` | **`death-respawn/002` 掉落清单流派生与回捡** | unity-specialist | **M** | `S23-1`(死亡时刻输入) | 清单 = **世界流派生**(`InventoryOf` 读流,零独立快照;F-29-4 同族纪律);`death_cell` = 最后一条 `ActorCellEntered` 之格(**禁读实时物理** —— B4 订正);`DropSpawned.spawn_anchor` 整数格 + 逐件一条;29 **零铸造** `instance_id`(发号归 20/`IIdAuthority`);回捡必到死亡格、禁远程拾取(20 R7);载荷零 float(反射断言);⚠️ 与 7a 快照 / ADR-023 读档重建的集成面 = MEDIUM,须实测登记 |
| `S23-3` | **`death-respawn/003` 技能掉级 F-29-1(int 截断)与调 30** | unity-specialist | **M** | `S23-1` | `AC-29-05` 表逐位:`{1,10,20,40,60} → {0,9,19,38,57}`(**C# int 截断**;**禁 `Fix.Div` / `floor_Fix(Mul)`** —— 定点化改错 1→0 语义);`AC-29-14` 掉级公式唯一出处 = 30 §4.3,29 零重复实现;`AC-29-16` **本 sprint 可勾**(折叠重放逐位一致 —— 7a 侧阻塞已清,见卡面 2026-11-01 注);**⚠️ 判断类豁免 = 口径待裁(`R-4-W-6`)**:开工时用户二选一(随 §4.3 正常掉级 / 回写 30 §4.3 立豁免),**裁前不实现豁免行** |
| `S23-5` | **`death-respawn/005` 复活落点与传送命令**(医馆床格 → `TeleportTo`) | unity-specialist | **M** | `S23-1`(+ 与 004 冷却门合验) | `AC-29-09`:29 的 `ActorCellEntered` Append 计数 = **0**(写者 = 1,ADR-020 Amendment B);传送走 `ITeleportCommandSink.RequestTeleport` **整数命令半**(ADR-025 QQ-01);`IClinicReviveCell` 接口先行(桩面明确标注 —— 复核注:24 规则六已落,挂标只挂**运行期实现**);兜底 = ADR-015 世界出生点固定格;复活**不重置**疾病/世界状态;多复活同 tick 按 actor id 定序(禁到达序);落点整格、29 直改连续坐标 = Forbidden |

### Should Have

| ID | 卡 | Owner | 复杂度 | 依赖 | 验收判据 |
|---|---|---|---|---|---|
| `S23-6` | **smoke `#8` 载体**:拾取三段式(意图事件 + 当下判距 + 宽容半径,离屏拾取被拒) | unity-specialist | **S** | `S23-2`(掉落物在盘上才可拾) | `DropClaimed` 走 4 的意图 + 判距 + 宽容半径(ADR-009 §七 拾取三段式 —— 与 17 采集同构先例);**离屏/超距拾取被拒且零事件**(负例);`critical_paths.json` 的 `#8` `status`/`check` 同批改 + `run_smoke.py` 实跑自动 PASS **+1**;⚠️ 合成输入驱动,禁双重空集假绿(承 S22-4 同条) |

> **Nice to Have:无** —— L 级独占额度;「容量填充」在 22 已被收口判定具名为非绿面,不得重演。

---

## 四、Risks

| # | 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|---|
| R1 | **`R-4-W-6` 判断类豁免未裁**(2026-09-20 至今)—— 003 的 EC 行无源,裁前实现 = 无源规则 | 高 | 中(003 全绿挂此) | 卡面已钉两案;`S23-3` 开工时**用户裁定**,裁前只做公式半(AC-29-05/14/16 不含豁免) |
| R2 | **005 桩先行 ≠ 联调** —— `IClinicReviveCell` 运行期实现归 24 侧,本轮只立接口 | 高 | 低(挂标已收窄,如实登记) | 桩面明确标注;`BLOCKED-BY-24` 维持到 24 下一轮;禁自拍「取第一个床格」 |
| R3 | **002 的 7a/ADR-023 集成面**(位置从 7a 快照、读档重建) = MEDIUM engine risk(Addressables 6.2+ 行为) | 中 | 中 | 夹具先合成输入(承 S22-4 纪律);集成面实测结果如实登记,不借 EditMode 绿冒充 |
| R4 | **G-1 门行结案被跳过** —— 001 收口后门行仍 open,复演 sprint-18 的「门从链上掉线无人知」 | 中 | 中 | 收口轮 **Must 回写**:债册门表 G-1 行 + 路线图 §2.4.3/§6.5 + 复评口径(结案判据 = `AC-29-01` 全链绿,非谓词半) |
| R5 | **001 的「结算三分」越界** —— 呈现半(006)与命令半(005)被顺手做进 001 | 中 | 低 | 卡面 Forbidden 已列;越界 = OUT OF SCOPE 记录,评审轮核 |

---

## 五、Dependencies on External Factors

- **用户裁定 1 件**:`R-4-W-6`(判断类豁免)—— 归 `S23-3` 开工时,**唯一**待裁项。
- 无外部素材件(006 的 UX spec 是它自己的前置,不挂本 sprint)。
- `TD-ci-01`(CI 凭证)不影响本 sprint 任何判据面(无跨平台 AC)。

---

## 六、Definition of Done for this Sprint

- [x] QA 计划存在(`production/qa/qa-plan-sprint-23-<date>.md`)且**早于 `S23-1` 开工**
- [x] Must 4 件全部完成,或**逐件具名**留存(留开 = 不记绿)
- [x] 每件有**证伪轮**(「回退即转红」),`grep -rn "FALSIFY-" unity/Assets --include=*.cs` ⇒ **0**
- [x] 回归两套件全量零失败,数字带**三元组**,等式 `total − 前次 = 新增数` 成立
- [x] 债册 `--check` exit **0**;未闭 P0 面 **0 → 0**(不涨)
- [x] smoke:`#8` 若落地 ⇒ 自动 PASS **15 → 16**;未落地维持 `none` 且**不改 json 充数**
- [x] **G-1 门行结案核**(R4):债册门表 + 路线图 §2.4.3/§6.5 收口回写,Exit 判据逐条复算
- [x] 收口判定与收口提交**同日**

---

## 七、收口判定(2026-11-01 · 同日收口)

### DoD 逐条核销

| # | 判据 | 结果 |
|---|---|---|
| ① | QA 计划早于开工 | ✅ `production/qa/qa-plan-sprint-23-2026-11-01.md`(`3c83c1b4`,先于 S23-1 `29988749`) |
| ② | Must 4 件全部完成 | ✅ S23-1(9 测,`29988749`)· S23-2(12 测,`e9dc6531`)· S23-3(13 测,`88ba94cf`)· S23-5(11 测,`4b99fb9f`);Should Have S23-6(10 测,`b47ed1e0`)亦落 |
| ③ | 证伪轮 + 零残留 | ✅ 五件 **16 变异**全数各自精确转红(2+3+4+4+3);`grep -rn "FALSIFY-" unity/Assets --include=*.cs` = **0**(2026-11-01 实跑) |
| ④ | 回归零失败 + 三元组 + 等式 | ✅ EditMode **`6425 / 6388 / 0 / 37`** @ 15:22:23 CST · PlayMode **`273 / 269 / 0 / 2`** @ 15:23 CST,`b47ed1e0` 干净树;等式:`6425 − 6391`(S23-2 终值)`= 34` = S23-3(13)+ S23-5(11)+ S23-6(10)新增测试数 ✅ |
| ⑤ | 债册 --check exit 0 · 未闭 P0 面 0→0 | ✅ `python3 tools/debtcount/debtcount.py --check` ⇒ exit **0**(「§五 输出块与索引块一致」);本 sprint **零新增债项**(实施判定均以卡面注 + 证据原件承载) |
| ⑥ | smoke 自动 PASS 15 → 16 | ✅ `run_smoke.py --results s236-em.xml s236-pm.xml`:**自动 PASS 16 · FAIL 0 · 未跑(需结果件) 0** · 具名人工 3 · 无名 3(#9/#18/#22,EXTERNAL/运行期判据);`#8` = PASS(2 载/全绿);SMOKE-EXIT=3 = 尚有 6 条无可执行判据,如实登记 |
| ⑦ | G-1 门行结案核 | ✅ 债册 §六 G-1 行 **`open → closed`**(四层复算 + `test_registeredPlayer_producesOnePlayerDiedEndToEnd` Passed = AC-29-01 全链绿);路线图 §2.4.3 同日回写 |
| ⑧ | 收口判定与收口提交同日 | ✅ 本判定 2026-11-01,随收口提交落盘 |

### 判定:**PASS**

四件 Must + 一件 Should 全部落地且各有证伪轮;G-1 门行(本 sprint 的 R4 风险项)结案;
smoke 自动判据 15 → 16 且零失败;债册校验 exit 0、零新增债。

**如实登记的残留(不阻塞收口,逐件已具名)**:
- ~~**R-4-W-6 未裁**~~ ✅ **2026-11-01 收口后即结**:该口径**早于本 sprint** 已由
  **2026-09-29 用户裁定**解决(判断类豁免撤销,**全部技能统一掉级**,`death-and-respawn.md`
  规则四撤销注)—— S23-3 勘察漏检该注误标「待裁」;生产装配 `isExempt: null` 恰为裁定值,
  **零行为改动**;四处文档对齐 + 裁定行为钉已落
  (`test_judgmentSkills_exemptRevoked_downgradeAppliesToDiagnosis`,证伪 = 豁免守卫恒开 ⇒ 红)。
- **S23-5 生产接线未落**:`ITeleportCommandSink` 零生产实装(1 侧未建)—— 发令面 + 端口 +
  回调位已交付,接线义务归 1 侧传送实装轮;`ActorCellEntered` 实发数归其联调。
- **30 §4.3 公式行文本订正**仍挂 30 下一轮(S23-3 兑付的是代码半边;GDD 已落注不改写)。
- **sprint-22 存量 carryover 不变**:[L] 三件 EXTERNAL · `TD-skeuo-01` open · 人工 3 条(#19/#20/#4)。

**复算命令(自包含)**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 tools/debtcount/debtcount.py --check; echo "DEBT=$?"
grep -rn "FALSIFY-" unity/Assets --include=*.cs | wc -l   # 期望 0
python3 tests/smoke/run_smoke.py --results unity/Logs/s236-em.xml unity/Logs/s236-pm.xml | tail -3
cd unity && python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('Logs/s236-em.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'),r.get('skipped'))"
git log --oneline -6   # 29988749 / e9dc6531 / 88ba94cf / 4b99fb9f / b47ed1e0 + 收口提交
```

---

## 八、收口后追加件(2026-11-02 · 用户指令「排进 sprint-23」)

> ⚠️ **本节晚于 §七 收口判定**。§七 的所有数字(件集 4 Must + 1 Should、回归
> `6425/6388/0/37` 与 `273/269/0/2`、DoD 勾选)**均为 2026-11-01 的读数**,
> **不含**本节追加件。不要把本节的完成情况读进 §七 的判据面。

| # | 件 | 类型 | 状态 | 说明 |
|---|---|---|---|---|
| **S23-7** | `emergency-procedures/010` 档位意图生产接线(`Treatment`/`Explore` 必发)+ 退役 `ExploreCount` 假绿载体 | Integration | **Done ✅ 2026-11-02** | 计划外追加 · **COMPLETE WITH NOTES**(AC-10-11 跳过半按裁定永久 `NOT-RUN`;S10-09 真机走查 `NOT-RUN`) |
| **S23-8** | `emergency-procedures/011` 移动压制租约生产接线 —— 10 置位/清位 `LeaseSource.Emergency` + **1 的读面** | Integration | **Done ✅ 2026-11-02** | 计划外追加 · **COMPLETE WITH NOTES**(9 条 AC 全证;4 / 25 侧仍零接线、真机观感未走查) |
| **S23-9** | `death-respawn/007` 29 的档位请求生产载体(`Downed` / `Explore`,requesterId 29)+ `TR-camera-007` 补登记 | Integration | **Done ✅ 2026-11-02** | 计划外追加 · **COMPLETE WITH NOTES**(9 条 AC 全证 + `TR-camera-007` 双向登记;S29-09 真机走查 `NOT-RUN`)。证伪轮抓出并修掉了 S29-06 的 F1 型假绿 |
| **S23-10** | `casebook/006` 39 的档位意图接线(`Casebook` / `Explore`,requesterId 39)+ 拆出 007 落笔锚点 | Integration | **Done ✅ 2026-11-02** | 计划外追加 · **COMPLETE WITH NOTES**(修的是「该发没发」的一个实例;`AC-39-09` `NOT-RUN`;启动即脉案屏却用 Explore 档未裁) |
| **S23-11** | `camera-viewpoint/007` 档位请求覆盖矩阵门(表 → 代码 → 行为) | Integration | **Done ✅ 2026-11-02** | 计划外追加 · **COMPLETE**(8/8 AC 全证 —— `S7-06` 已由变体编译补证)。证伪 P1 抓出 `test_s7_05` 的空洞正控用户裁定:表 = 独立 `camera-mode-requests.yaml`;先补 39 接线(S23-10 已完成)再上门;判据 = 动态矩阵 + 静态防绕过 |

**为什么加进本 sprint(而不是新开 sprint-24)**:用户 2026-11-02 指令。
sprint-24 已排 `processing-002/004/005 + foraging-002`(路线图 §6.5),与本件无交集;
本件是**补洞卡**(双参 `SetMode` 规则按 epic 扫描查出的**缺口 B**),体量 M,单件收口。

**与 §二 的冲突如实登记**:§二 明写「L 级独占额度,**不塞任何计划外件**」。
本节即对该条的**显式例外**,由用户直接指令产生 —— **不改 §二 原文**,以本节为准记录本件来源。

**本件的两条硬约束**(详见卡面):
1. `AC-10-11` **拆两半分开记分** —— 「动作结束必发 `Explore`」可判可绿;
   「跳过路径必发 `Explore`」今日不可失败(R-10.7),永远标 `NOT-RUN`,**不得记 ✅**
2. 甲+乙裁定已落(2026-11-02):跳过经 `EmergencyChannelDriver.SkipAction` 拉回相机链
   + 不可达性在两处(卡面 / GDD 规则八 与 `AC-10-11`)如实登记

**S23-7 收口读数(2026-11-02 · 只对本行有效,不进 §七)**:
EditMode `6492 / 6455 / 0 / 0 / 37` · PlayMode `277 / 273 / 0 / 2 / 2`
(基线 `6481/6444` · `275/271`;等式 `6492 − 6481 = 11 = 15 新 − 4 删` · `277 − 275 = 2`)。
证伪 **7 条变异各自转红**,SHA-256 逐字节还原。原件 = `production/qa/evidence/impl-story-010-camera-mode-intent-2026-11-02.md`。
⚠️ **跨 epic**:本件接线了 `CameraRig.Tick`(此前零生产调用),**同时激活** 29 的 `Downed` 与
39 将来的 `Casebook` —— 已在两 epic 的 `EPIC.md` 与相机证据件 §15 登记。

**S23-8 收口读数(2026-11-02 · 与 S23-7 同批)**:
EditMode `6500 / 6463 / 0 / 0 / 37` · PlayMode `279 / 275 / 0 / 2 / 2`
(基线 = `S23-7` 的 `6492/6455` · `277/273`;等式 `6500 − 6492 = 8 = 12 新 − 4 删` · `279 − 277 = 2`)。
证伪 **7 条变异各自转红**(M4 / M7 各恰 1 红,其余超集并已登记耦合),SHA-256 逐字节还原。
原件 = `production/qa/evidence/impl-story-011-motor-suppression-2026-11-02.md`。
⚠️ **跨 epic**:`PlayerController.Move` 自此读 `MotorLease.IsSuppressed`(此前零读点);
已在 `player-controller/EPIC.md` 登记,并同时登记「`LocomotionEvaluator` 乘数链仍只是注释」的欠账。
⚠️ **本轮被机械门抓红一次**:`AC-1-23` 的 IL 扫描按**调用点所在程序集**判白名单,
初版把写面放在 `Gameplay.Boot` ⇒ 红。处置 = 下放到 `Gameplay.Presentation`,**未扩白名单**。

**S23-9 收口读数(2026-11-02 · 与 S23-7 / S23-8 同批)**:
EditMode `6509 / 6472 / 0 / 0 / 37` · PlayMode `281 / 277 / 0 / 2 / 2`
(基线 = `S23-8` 的 `6500/6463` · `279/275`;等式 `6509 − 6500 = 9 = 9 新 − 0 删` · `281 − 279 = 2`)。
证伪 **6 条变异各自转红**,SHA-256 逐字节还原 5/5(重跑后)。
原件 = `production/qa/evidence/impl-story-007-downed-camera-request-2026-11-02.md`。
🔴 **证伪轮 M5 首轮未转红** —— S29-06 初版直调 `requester.Request(true)` 绕开了 `BootRoot`
传给呈现器的装配缝 ⇒ 把第 4 实参换成 `null` 仍然全绿(F1 型:自造断言顶替接线语义)。
已改为走真链(`PlayerDied` 落流 → 呈现器 `Update` → 请求缝 → 相机 `Mode`),复跑 M5 **恰 1 红**。
⚠️ **跨 epic**:本件为 `a5f766f6` 的相机闭包补上具名载体与 AC 载体(缺口 A),
并在 `death-respawn/EPIC.md` 登记 6/7 → 7/7。

**S23-11 收口读数(2026-11-02 · 与 S23-10 同批)**:
本件是 `S23-7 / S23-9 / S23-10` 三条的**上位** —— 那三条各自补了表中两行请求点,
但「表 ↔ 代码 ↔ 行为」的系统性判据仍不存在。**修完三个实例 ≠ 修掉那一类。**
⚠️ 用户 2026-11-02 三项裁定已写进卡面:① 表真源 = 独立 `camera-mode-requests.yaml`;
② **先补 39 接线,再上门**(⇒ 本件排在 S23-10 之后);③ 判据 = 动态矩阵 + 静态防绕过。
⚠️ **静态层单独存在会是第二扇假门** —— `EmergencyChannelDriver` 的调用是
`rig.SetMode(mode, RequesterId)`,mode 是**形参**,字面量在调用方 ⇒ 静态扫描只能证明
「有人**声明**要发」,证不了「它**会**发」。这条已写进卡面的「抓不到什么」节,不得被读成「档位全对」。
**读数**:EditMode `6523 / 6486 / 0 / 0 / 37` · PlayMode `290 / 286 / 0 / 2 / 2`
(基线 = `S23-10` 的 `6518/6481` · `283/279`;等式 `+5` / `+7`,零删除)。
证伪 **P1–P6 全红**,四文件 SHA-256 逐字节还原 6/6。
原件 = `production/qa/evidence/impl-camera-viewpoint-007-mode-matrix-2026-11-02.md`。
🔴 **P1 首轮抓出我自己的空洞正控**:记录器停写时,「记录 ⊆ 表」的违规集合恒为空 ⇒ 该断言
**恒真**(7 条里唯一没红的)。补正控后 P1 = **7/7 全红**。
⚠️ **静态层单独存在会是第二扇假门** —— `EmergencyChannelDriver` 的 `mode` 是形参,
静态只能证「有人声明要发」;真判据在动态矩阵那半。
✅ **S7-06 已补证(2026-11-02 晚)**:非 Development player 产物里记录器类型与命名空间均为 **0**,而撤掉 `#if` 重建后为 **1** ⇒ 判别力成立;还原以「与 HEAD 逐字节一致」为准。

⚠️ **实现前先读卡面末尾的编排预警** —— 六行在一次 boot 里驱动齐,是本件最可能卡住的地方。

**S23-11 收口后的清账轮(B-1 / B-4 · 2026-11-02)**:
用户指令「先修 B-4」→「修 B-1」。两项均落盘,均**不改本 sprint 的 Done 面**(它们是收尾清账,不是新条目)。

- **B-4(`SkeuoUI` 测试间泄漏)** → 登记 `TD-skeuo-02`(`P0 面 = N` / `R4`)。**治标已做**(两件新夹具
  各自 `DestroyAllNamed`,治本方向见该条「修法方向」三条,未裁)。
  🔁 **2026-11-02 · `S23-19` 治本落盘 → 该债 `closed`**:取**甲案**(`FindOrCreateSkeuoUi`
  两条路径都 `SetParent(transform)`,与 `EnsureEventSystem` 同款)⇒ 复用退化为
  「同一 `BootRoot` 内幂等」,销毁即随之销毁。新增 `skeuo_ui_lifetime_test` 两条判据
  (归位 / 销毁闭合 + 新实例干净态)。证伪 N1–N3 全红、SHA-256 逐字节还原 3/3。
  原件 = `production/qa/evidence/impl-td-skeuo-02-ui-lifetime-2026-11-02.md`。
- **B-1(启动档位未裁)** → 用户裁定「连启动占位一起改掉」。
  **裁定依据是设计权威,不是口味**:`design/ux/casebook-39.md:57`「`Interact` = 就诊/立案 → **立案即开册**」
  ⇒ 脉案屏是**玩家动作的结果**,不是启动常驻屏。
  **落地三处** —— 代码默认值 `""` · **`Boot.unity:375` `_screenName` 置空(生产生效的那一处)** ·
  两件 L2 夹具删预热 / 前置改 `None`;`modal_opener_test` 与 `main_menu_wiring_test` 各一处前置随之改。
  **相机侧零新增代码**:启动态 = `None` ⇒ 39 的边沿判据不发请求 ⇒ 档位维持 `Explore`。
  读数 EditMode `6523/6486/0/0/37` · PlayMode `290/286/0/2/2` —— **与 S23-11 逐位相同**
  (本批判据全落在既有夹具的断言改动上,零新增用例)。
  🔴 **连带暴露并已立债**:`TD-casebook-01` —— **生产侧零 `OpenModal(ModalId.Casebook)` 调用点**,
  且立案由 `CaseOpenedDriver` 每 tick 自动开案,与 `case-system.md:176`「立案是一次玩家动作」
  及 `:927`「玩家没去 ⇒ 不立案」**方向相反**。该债 `P0 面 = Y`(**R1**)⇒
  **`未闭 P0 面 0 → 1`,M3 Exit 第二条合取当场转假**(`casebook` epic 名下 1 条)。
  ⚠️ 该缺口**先于**本轮存在;B-1 只是把它从「隐性」变成「玩家可见的缺失」。**不得**因此回填占位。

**S23-12(清账后的追加件 · 2026-11-02):开册入口 —— 结清 `TD-casebook-01`**:
用户指令「修 TD-casebook-01(开册入口)」。债的三条消解判据**全部兑现**:
生产侧出现 `OpenModal(ModalId.Casebook)` 调用点 · 触发源 = 玩家 `Interact` ·
两件夹具守「玩家没交互 ⇒ 零 `CaseOpened`」。

- **退役**:`CaseOpenedDriver.OnTickEdge(tick)`(每 tick 自动开案,与 `case-system.md:176`
  「立案是一次玩家动作」方向相反)整个删除;唯一入口改 `TryOpen(patient, tick)`。
- **新增**:`InteractionInputDriver.TryVisit` = ① 立案 ② 查体(P0 就近)③ 开册;
  `ModalInputDriver` 的 toggle **先判脉案** ⇒ Escape = 合册(不再在册上叠暂停壳)。
- **🔴 必须一起修的一条(否则形式通过、实机不通)**:E 的路由是「拾取 → 采集 → 就诊」,
  而原判据是「半径内**存在**采集点就消费」。`r_interact = 3` 的现世界覆盖全部可站格
  ⇒ **每一次 E 都被采集吞掉**(实测:出生格与病人格都 `harvest=True`),开册永远轮不到。
  改为 `InteractClaimRoute` 的**跨种类比距**(采集只在比最近病人更近时才抢)。
- **读数**:EditMode `6523/6486/0/0/37` · PlayMode `294/290/0/2/2`
  (基线 `S23-11` 的 `6523/6486` · `290/286`;等式 `+0` / `+4`,零删除 —— `case_opened_wiring_test`
  是判据翻转,不计新增)。证伪 **M1–M5 全红**,五文件 SHA-256 逐字节还原 5/5。
  原件 = `production/qa/evidence/impl-casebook-entry-2026-11-02.md`。
- **新揭两条(已立册,均 `P0 面 = N` / R4 自陈)**:`TD-interaction-01`(4 的全序选择只落地 `d∞`,
  病人未接成候选源)· `TD-diagnosis-01`(查体由世界 E 代做,`S-8.4` 要求为模态行级)。
  ⇒ `未闭 P0 面 1 → 0`,**M3 Exit 第二条合取由假回真**。

**S23-13(2026-11-02):统一目标选择 —— 结清 `TD-interaction-01`**:
用户指令「修 TD-interaction-01」。病因 = 「谁是这一帧的目标」由**三处**各算一遍
(采集自选资源点 / 驱动自选**视觉位置**最近的病人 / `InteractClaimRoute` 外挂比距)。

- **现口径**:`InteractionPressRouter` —— **一次 `InteractionSelector.Select` 定种类**
  (F-4.1 三键全序),再按 `InteractableKind` 分派(Patient → 39 · ForageSpot → 17 · Drop → 20)。
  病人正式接入候选集(**`PresencePatientSource`**,`IPatientSource` 的首个生产实装)。
- **删除**:`InteractClaimRoute` + `HarvestInteractionBridge`(外挂仲裁,已是死代码);
  上一轮加的 `PeekNearestForageDistance` 一并删(被路由取代)。
- **顺带纠正**:E / 急救 Arm 的目标选择改读**整数格**(4 规则二「玩家格取路 (a)」),
  不再读 `transform.position` 与视觉位置。
- **读数**:EditMode `6526/6489/0/0/37` · PlayMode `294/290/0/2/2`
  (基线 `S23-12` 的 `6523/6486` · `294/290`;等式 `+3 = +7 新 − 3 删 − 1`,PM `+0`)。
  证伪 **N1–N5 全红**,五文件 SHA-256 逐字节还原 5/5。
  原件 = `production/qa/evidence/impl-interaction-01-unified-selector-2026-11-02.md`。
- 🔴 **一条假红的教训**:`modal_gate_wiring_test` 的正控把玩家挪到 `(0,0,50)` 只为躲开采集点,
  其注释写「查体的最近病人判定**无半径门**」—— 路由接管后 E **有**半径门,半径外不着体 ⇒ 正控假红。
  ⇒ **改夹具不改生产**:那正是行为该变的地方。
- **残余**:`Drop` 仍走 20 的「第一可拾」,另六种候选源仍 `null` ⇒ 新立 **`TD-interaction-02`**。

**S23-14(2026-11-02):查体归位 —— 结清 `TD-diagnosis-01`**:
用户指令「修 TD-diagnosis-01」。病因 = `S-8.4` 路线甲要求「世界 `Interact` = 就诊(单义);
**查体 = 脉案模态行级动作**」,实装却是世界 E 整病人一次查完(`ExamineDriver.OnExamine`),
脉案屏**零**行级查体入口。

- **现口径**:六行 `channel-*` 的 Submit ⇒ `ICasebookCommandSink.ExamineChannel(channel)` →
  `CasebookCommandRouter` → `ExamineDriver.ExamineChannel`(单通道求值 + 单通道落册)。
  世界 E **只**做「立案 → 切病人 → 开册」。
- **配套**:`ExaminationLedger` 改**按通道分片**(六片合并读);求值器增通道过滤重载。
- **读数**:EditMode `6531/6494/0/0/37` · PlayMode `297/293/0/2/2`
  (基线 `S23-13` 的 `6526/6489` · `294/290`;等式 `+5 = +3 路由 +2 分册` · PM `+3` 新件)。
  证伪 **M1–M4 全红**,四文件 SHA-256 逐字节还原 4/4。
  原件 = `production/qa/evidence/impl-diagnosis-01-row-level-examine-2026-11-02.md`。
- 🔴 **抓到一条真缺陷(本轮主要收获)**:六行绑定原先写在 `_root.Add(_casebookFace)` **之前**
  ⇒ `_root.Q(rowName)` 返 `null` ⇒ `if (row == null) return;` 静默返回 ⇒ **六行一个回调都没挂上**。
  生产上「脉案行级查体」完全哑火,而既有全部夹具仍绿。由新增的 `casebook_row_examine_test`
  首跑 2 红抓出;修法 = 绑定点移到 `_root.Add(...)` 之后。
- ⚠️ **一条假存活**:证伪 M2 首轮 12/12 绿 —— 夹具只推到 tick 1,体征读出门槛全挡,
  六通道全读出 0 条 ⇒ 通道过滤有无同结果。修夹具(推到 plateau + 逐行验集合含于已查通道)
  后同一变异转红。**F4 的输入构造变体**。

**S23-15(2026-11-02):掉落进候选集 —— 结清 `TD-interaction-02`**:
用户指令「修 TD-interaction-02」。病因 = 第一源 `IDropSource` 为 `null` ⇒ 掉落不在候选集里,
拾取靠路由的「选择器空 ⇒ 兜底」+ 20 自己的「第一可拾(id 升序)」—— 4 之外的第二处判据。

- **现口径**:`GroundDropSource`(未归属地面实例 × 最近一次 `DropSpawned.spawn_anchor`)
  进 `pressCandidates`;`Select` 直接选中 Drop ⇒ `TryClaimInstance(被指名的 instance_id)`。
  兜底段与「第一可拾」入口(`TryClaim`)**整体删除**。
- **锚点单一真源**:`DeathDropReclaimPolicy.TryGetSpawnAnchor`(候选源与 `CanClaim` 共用)。
- **装载期回捡门**:`LoadWithin(…, dropClaimRadius)` 用 `_drops.Tolerance` 预裁 ——
  防「回捡半径外的掉落赢得全序、结算又被拒 ⇒ 按键被静默吞掉」。
- **读数**:EditMode `6540/6503/0/0/37` · PlayMode `297/293/0/2/2`
  (基线 `S23-14` 的 `6531/6494` · `297/293`;等式 `+9 = 3 第一源 + 6 路由`,PM `+0`)。
  证伪 **M1–M4 全红**,四文件 SHA-256 逐字节还原 4/4;M4 为超集(另两条红是 id=0−1 撞参数守卫)。
- ⚠️ **覆盖一条已裁定项**:S23-6 的「刻意不用就近」已改判 —— 两条原由(锚点第二次扫流 /
  同距平局难破)均被本批事实推翻(锚点已有唯一定义;F-4.1 第三键即 tie-break)。
- ⚠️ **M3 首轮 NO-XML** = 变异写坏(语法错误),不是判据红;改成「比较式恒假」后转红。
- **残余**:六种 `InteractableKind` 仍无候选源(`AC-4-22` 仍 `NOT-RUN`)⇒ 新立 **`TD-interaction-03`**。
  原件 = `production/qa/evidence/impl-interaction-02-drop-source-2026-11-02.md`。

**S23-16(2026-11-02):第二源补齐 —— `TD-interaction-03` → partial**:
用户指令「修 TD-interaction-03」,范围裁定 **C**(连 `BuildSlot` 一起接)。
勘察发现阻塞比债册写的更靠上游:① 生产 reporter 是 no-op 桩、6 的 `PoiStateMachine` 从未构造;
② `BuildSlot` 的既有入口是 23 的 `BuildInputDriver`;③ 另五种 kind **无数据行**。

- **接通两条**:`PoiCell`(第二源 → 4 出境 → `PoiDiscoveryReporter` → 6 的 `TryDiscover`)
  · `BuildSlot`(路由 → `BuildInputDriver.AimAt`:进建造模式 + 对准该格,**不放置**)
- **新增**:`WorldBakedLogicalSource`(POI / 采集点 / 槽位三切片,线性键按 4-DC-4)
  · `PoiDiscoveryReporter`(未绑定状态机 ⇒ **抛**)· `services.PoiStateMachine`
- **读数**:EditMode `6554/6517/0/0/37` · PlayMode `297/293/0/2/2`
  (基线 `S23-15` 的 `6540/6503` · `297/293`;等式 `+14 = 4+4+4+2`,PM `+0`)。
  证伪 **M1–M5 全红**,五文件 SHA-256 逐字节还原 5/5。
- ⚠️ **M1 首轮假存活**:夹具两槽位都在 `y = 0` ⇒ 线性键轴次序错不可观测;第二格改 `y = 2` 后转红。
- ⚠️ **新裁一条语义(未经 UX 复审)**:E 打到槽位 = 进建造模式 + 对准该格 ⇒ 建造模式出现**两个入口**。
- **仍未闭**:`Door` / `Switch` / `ClinicPanel` / `Utensil` / `Container` **无数据行**
  (`build_modules` 只有家具)⇒ `AC-4-22` 实机语义仍不满足 ⇒ 债务转 **`partial`**,不宣称关闭。
  原件 = `production/qa/evidence/impl-interaction-03-poi-and-buildslot-2026-11-02.md`。

**S23-17(2026-11-02):门柜可交互 —— `AC-4-22` 实机语义接通(`TD-interaction-03` 仍 partial)**:
用户指令「修 AC-4-22(门柜可交互)」;两项边界裁定 = **门影响可通行性、状态进世界流** ·
**柜 = 容器物品实例**(`instance_id`,忠于 GDD 规则二)。

- **门(结构件 + 状态进流)**:`SlotType.Door = 8` · 模块 11 + 关卡工具门槽 `(5,0,1)` ·
  `World.IsEffectivelyWalkable` **门格先于占用表分流**(关 = 挡 / 开 = 通)·
  `DoorInteractionDriver.Toggle` → `StructureWriter.Modify` ⇒ `StructureModified` 进世界流
  (**不新开 Kind**;重放 / 存档 / ADR-033 平移全复用)· 路由 `case Door`
- **柜(容器物品实例)**:新烘焙产品 `world_containers`(关卡工具**加性第 8 类** → Binder 五门 →
  cooked → data-core)· `ContainerDto(instance_id)` · 第四源生产实装 `BakedInitialSource` ·
  装配根 `ReconstructItemHighWater` 抬升(防开档后掉号)· 路由 `case Container` → `ModalId.InventoryContainer`
- **受影响的 pin(逐条契约演进)**:`SlotType` 闭集 7→8 · 掩码上界 255→511 ·
  关卡数据集闭集 7→8 · `world_buildslots` 金标准 SHA 重签(1340→1426 B)·
  `build_modules` 行数 10→11 · 产物漂移门 `pinnedDeclared` 38→39
- **读数**:EditMode `6581/6544/0/0/37` · PlayMode `297/293/0/2/2`
  (基线 `S23-16` 的 `6554/6517` · `297/293`;等式 `+27 = 25 新夹具 + 1 Door 位正向例 + 1 漂移门用例`,PM `+0`)。
  证伪 **M1–M9 全红**,九处 SHA-256 逐字节还原 9/9。
- ⚠️ **写夹具前抓到真缺陷(首版实现)**:门格在占用表里恒为 true(基线 / 放置都占格)⇒
  若先判占用,门格**永远不可走**、开与关在可通行性上零差异。已改为**门格先分流**。
- ⚠️ **仍未闭**:`Switch` / `Utensil` / `ClinicPanel` 无 `SlotType`、无数据、无消费面 ⇒ 债维持 **`partial`**。
  新登记残余 R1(玩家新建的门不在第四源)/ R2(读档高水位未并基线容器 id)/
  R3(容器内容与搜箱时长)/ R4(`world_containers` 无 ADR)。
  原件 = `production/qa/evidence/impl-ac422-door-cabinet-2026-11-02.md`。

**S23-18(2026-11-02):剩下三种接通 —— `Switch` / `Utensil` / `ClinicPanel`(`TD-interaction-03` 仍 partial)**:
用户指令「修 TD-interaction-03 剩下三种」。

- **数据**:`SlotType.Utensil = 9` / `ClinicPanel = 10` / `Switch = 11`(闭集 8 → **11**)·
  模块 12 / 13 / 14(`furn_type = null` ⇒ 不进 24 的房间析出)· 槽位 `(10,0,5)` / `(5,0,9)` / `(2,0,4)`
- **分类**:`BakedInitialSource` 的四切片收成一个 `CollectSlots(SlotType)`(依据 = 模块目录的 `SlotType`)
- **分派**:`DoorInteractionDriver` → **`StructureToggleDriver`**(构造参 `expectedType` 钉住类型,
  门 / 开关各一个实例)· 路由新增 `case Switch` / `case ClinicPanel` / `case Utensil`
- **消费面**:`Switch` → `StructureWriter.Modify`(`StructureModified` 进世界流,**与门同路径**);
  `ClinicPanel` → `ModalId.ClinicPanel`;**`Utensil` 恒 `null`** —— 18 的炮制面板
  `AC-18-10b` **EXTERNAL · BLOCKED-BY-42**(`O-18-R6`),`CraftIgnitionTransaction.Process` 零生产调用点
  ⇒ 选中器具**如实落空**,不伪造消费面
- **受影响的 pin**:`SlotType` 8→11 · 掩码上界 511→**4095**(越界例 512→4096)·
  `build_modules` 11→**14** · 槽位 15→**18** · 金标准 SHA 1426→**1697 B**(第三次重签)·
  `clinic_room_matching_e2e` 模块数 11→14
- **读数**:EditMode `6590/6553/0/0/37` · PlayMode `297/293/0/2/2`
  (基线 `S23-17` 的 `6581/6544` · `297/293`;等式 `+9 = 4 + 4 + 1`,按类从 XML 实数取证,PM `+0`)。
  证伪 **N1–N6 全红**,六文件 SHA-256 逐字节还原 6/6。
- ⚠️ **`PM exit=2` 是本仓 PlayMode 的常态**(四次历史运行皆 2)⇒ 判据取 XML 的 `failed`,不取退出码。
- ⚠️ **全量 EditMode 三元组取自订正前树**:跑完后订正了 `door_interaction_test.cs:24` 的**类注释旧名**
  (纯注释)⇒ 补一次 `ModularBuilding` 过滤跑回证 `242/242/0/0/0`,并如实登记该偏离。
- **仍未闭**:`Utensil` 的消费面(消解条件要求「每种至少一个消费者接通」)⇒ 债维持 **`partial`**,
  残余收窄为「一种消费面待 42 认领」。原件 = `production/qa/evidence/impl-tdi03-remaining-kinds-2026-11-02.md`。

**S23-10 收口读数(2026-11-02 · 与 S23-9 同批)**:
EditMode `6518 / 6481 / 0 / 0 / 37` · PlayMode `283 / 279 / 0 / 2 / 2`
(基线 = `S23-9` 的 `6509/6472` · `281/277`;等式 `6518 − 6509 = 9 = 9 新 − 0 删` · `283 − 281 = 2`)。
证伪 **7 条变异全部转红**,SHA-256 逐字节还原 7/7。
原件 = `production/qa/evidence/impl-casebook-006-camera-mode-intent-2026-11-02.md`。
🔴 **本轮方法论收获(`N6`)**:`CloseModal` 拆成 `CloseModalCore`(不发意图)+ 公开版(发)
看着像无害写法整理,但撤掉后「已在脉案时再开一次脉案」会冒出 `[false, true]` 两条中间态意图
⇒ **没有 N6,这次重构不可证伪**。
⚠️ **拆卡**:原 `story-006` 捆「相机意图 + 落笔 + checkpoint 锚点」,后两件与相机无共享判据,
且 `SaveAnchor` 全仓不存在 ⇒ 006 永远无法 `Complete`。拆为 006(相机,本批收口)/
007(落笔锚点,新立未实现)。
⚠️ **本件修的是一个实例,不是那一类** —— 系统性的「表 ↔ 代码 ↔ 行为」判据仍未落地:
`camera-viewpoint/story-007`(档位请求覆盖矩阵门)已立件,`Ready ⬜` 未开工。

⚠️ ~~**§八 里唯一还要动手的是 `S23-9`**(其余两条已 Done)。本 sprint 的 Done 口径对它是:
`Ready` → `In Progress` → `Done`,**判据以 `death-respawn/story-007` 的卡面 AC 为准**
(9 条,含 `TR-camera-007` 的登记面);收口读数届时另记一段,不改本节既有两条的读数。~~
🔁 **2026-11-02 收口**:`S23-9` 已 `Done ✅`(原文保留)。**判据确以卡面 9 条 AC 为准**,
读数另记于下方「S23-9 收口读数」段,**未改动本节既有两条的读数**。

**复算(收口时用)**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
grep -n "S23-[789]" -A6 production/sprint-status.yaml  # 7/8 期望 done;9 期望 ready-for-dev(开工后 in-progress)
grep -rn "ExploreCount" unity/Assets --include=*.cs | wc -l   # 期望 0
```

**S23-20(2026-11-02):读档后 id 高水位按新流重构 —— `TD-persist-12` closed**:
用户指令「按你的建议继续修」。

- **病因**:两个计数器(`ItemInstanceHighWaterCollector.CollectAndReconstruct` /
  `IdAuthority.ReconstructPatientHighWater`)的**生产调用点 = 0** ⇒ 读档换流后计数器停在
  装配那一刻 ⇒ 存档里更大的 id 被后续铸造复用 ⇒ fold 里两件物品静默合并 /
  新病人复用已折叠病人 id
- **落地**:`CompositionRoot` 新增 `Func<IEnumerable<long>> bakedInitialItemIds`(惰性,
  容器段赋值 · `afterRestore` 读取)· `afterRestore` **先**重构两个高水位、**再**重建派生态 ·
  受伤实体半边扫**三流 ∪ 折叠行**
- **原文的一处订正**:「基线不导出 item 实例 id」半边**已在 `AC-4-22` 轮(`43f1d2f3`)落地**,
  本轮补的是**读档路径**那一半
- **新增夹具**:`Tests/EditMode/Boot/load_highwater_wiring_test.cs` 4 条(物品 / 每次重算 /
  受伤实体 / 折叠行参与),全部走真装配根 + 生产 `afterRestore`
- **证伪 M1/M3/M4 全红**(超集 / 超集 / exact),SHA-256 逐字节还原 3/3
- ⚠️ **M2 假存活(本轮主要发现)**:原「基线半边参与」断言**结构上不可判**
  (`IdAuthority` 只升不降 + 装配期已抬到 `baselineMax + 1` ⇒ 传不传 `null` 结果相同)
  ⇒ 断言已删、判据改写、变异分支标作废。这是假绿形态「靶点对但靶场里没有靶」
- 原件 = `production/qa/evidence/impl-td-persist-12-highwater-on-load-2026-11-02.md`

**S23-21(2026-11-02):`BusyActorCount` 单遍扫描 —— `TD-processing-04` closed**:
用户指令「按你的建议继续修」。

- **病因**:旧实现 = 扫一遍收 actor 集合 + **逐 actor 重扫全流** ⇒ 每条 `Craft` 被解码 `A` 次
  (`O(A×E)`)
- **修法**:单遍扫描算出每 actor 的 `FurnaceOccupancy`,再逐 actor 走**同一个** `IsBusyCore`
  —— 债册硬要求「`IsBusy` 仍是唯一判据」逐字兑现(抽出一处谓词核,`IsBusy` 与
  `BusyActorCount` 都调它)
- **两处行为对齐**:时长域先验**不论是否落在前缀内**;前缀与定序都只用 envelope `e.Tick`
- **新增夹具**:`Tests/EditMode/Processing/furnace_busy_count_equivalence_test.cs` 4 条
  (等价性主判据 / 前缀外坏时长 / 同 tick 定序 / 非 Craft 混入)
- **证伪 M1–M4 全红**(四条各 1 红,全 exact),SHA-256 逐字节还原 4/4
- ⚠️ **M1 首轮假存活**:「换成 `tick < CompleteTick` 去掉 `Exists` 守卫」在**该调用点完全等价**
  (`latest` 只存 `Exists=true` 的实例)⇒ F8「行为等价掩盖结构分叉」;改靶为口径真分叉后转红
- ⚠️ **两处不能宣称**:加速幅度**未测**(债册自陈「未用 Profiler」)· 债名写「分配」而实际优化的是
  **解码次数与遍历遍数**(分配块数未必减少)
- 原件 = `production/qa/evidence/impl-td-processing-04-busy-count-2026-11-02.md`

**S23-22(2026-11-02):pipe 抽象 P0 预埋 —— `TD-net-01` closed**:
用户指令「按你的建议继续修,一个个的不要停」。

- **病因**:ADR-001 把 pipe 抽象落地级别定为 **P0**(`:10`/`:59`/`:131`/`:167`/`:448` 五处),
  而 `IReplayPipe` **全树零命中**、`ReorderBuffer` **生产零命中** ⇒ 八个抽象点落 7 个,
  **`IReplayPipe` 是唯一零落点**
- **落地**:`Sim.Contracts/IReplayPipe.cs`(接口 + `sealed ReorderBuffer`)+
  `Sim/LocalReplayPipe.cs`(P0 本地占位,零网络)
- **两处设计判断(本轮就地裁定,未经用户逐条确认)**:
  ① 接口住 `Sim.Contracts`、实现住 `Sim` —— 接口须被 `Sim` 看见(ADR-025 §① 引用集恰 =
     `{BCL, Sim.Contracts}`),而实现要算全序键(`EventOrder.KeyOf` 住 `Sim`)⇒ 方向不可倒
  ② `TryEnqueue` 的键是**形参**而非内部现算 —— 缓冲自己拼一份 = **第二真源**
- **新增夹具**:`Tests/EditMode/Networking/replay_pipe_contract_test.cs` **8 条**
  (抽象点存在 2 · 乱序吸收主判据 · 超界丢弃+告警 · `MaxLag=0` 构造期拒 · 本地能发 · null 抛 · 多订阅追加)
- **读数**:EditMode `6606/6569/0/0/37` @ `15:42:49` · PlayMode `299/295/0/2/2` @ `15:43:58`
  (基线 `S23-21` 的 `6598/6561` · `299/295`;等式 `+8 = 8 新夹具`,PM `+0`)
- **证伪 M1–M7 全红**(M7 打 2 条,其余各 1 条;全 exact),SHA-256 逐字节还原 7/7 + `FINAL-OK`
- ⚠️ **判据 ①/② 无专打变异**:结构性断言(命名空间 + 方法集合),变异等于搬文件或改签名
- ⚠️ **三处不能宣称**:不测网络(只证契约形状可被实现)· 不测「本地路径出现乱序」
  (结构上永远不乱序)· `Subscribe` 的主线程排队义务(ADR-001 F3)在本地是 no-op,未验证
- ⚠️ **关闭不改变 `P0 面 = N`**:归属仍非 31 个 P0 epic 之一;P1b 的库选型 / swap 评审仍归 45 的 P1b 轮
- 原件 = `production/qa/evidence/impl-td-net-01-replay-pipe-2026-11-02.md`

**S23-23(2026-11-02):三条「欠裁决」债 —— 裁决简报 + 按建议执行**:
用户指令「听你的建议」。先落裁决简报 `production/qa/evidence/adjudication-brief-3debts-2026-11-02.md`
(每条的**选项 / 后果 / 建议**),再按建议案执行。

- **`TD-processing-03`(甲)**:同键分支加注释(写明**结构上不可达**的两条源码依据 +
  口径 = 取先入者)· 新增夹具 `test_tdp03_sameOrderKey_twoCrafts_pickFirstEncountered` ·
  **ADR-008 §Validation 的「无平局」项 `[ ]` → `[x]`**(该项此前已有载体,只是无人回填 —— 勾选不是「本轮补了新夹具」)
- **`TD-building-04`(乙)**:**ADR-010 Amendment A** —— `structure_id` 独立空间、不入 `IIdAuthority`;
  义务 12 的机制半边划线撤回、折叠谓词半边不动;**显式登记未来触发条件**(联机跨机 id 一致性 ⇒ 须另开 ADR)。
  依据四条:实现已如此且更安全 · ADR-033 平移量 `K := N` 的充要性证明建立在「基线占 1..N」之上 ·
  ADR-033 已于 10-28 关闭 `TD-building-02`(9 变异全红)· GDD `:648` 的撞号设想在当前实现下不成立
- **`TD-skeuo-01`(丁+丙)**:`SkeuoRuntimeDriver.CreateFocusRing` 加**运行期 fail-loud 守卫**
  (border 之和 > 44 ⇒ 抛,报文点名黑块 + 债号 + 实见值)· 新增夹具 2 条(正常态不抛 / 越界抛)
- **证伪**:TDP03-M1(`<= 0` → `< 0`)恰 1 红 · SK1(删整段守卫)恰 1 红;两文件 SHA-256 逐字节还原
- ⚠️ **三处不能宣称**:① 不可达是**论证**非观测(依据是源码两条事实)② 乙案只**承认**机制不存在,
  联机时本条**重新变 P0 面** ③ 丁案**不阻止新灌注点**,「加通用门」保留为下次触发条件
- ⚠️ **本轮只勾了 ADR-008 的一行 Validation,未改其裁决正文** —— 与 `TD-building-04` 的正式
  ADR 修订**性质不同**,已在债册逐条标注
- **读数**:EditMode **`6607/6570/0/0/37`** @ `16:12:58 CST` · PlayMode **`301/297/0/2/2`** @ `16:14:10 CST`
  (基线 `S23-22` 的 `6606/6569` · `299/295`;等式 EM `+1` = TDP03 夹具 · PM `+2` = 焦点环守卫夹具)
