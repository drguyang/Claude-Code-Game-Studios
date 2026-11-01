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
| **S23-11** | `camera-viewpoint/007` 档位请求覆盖矩阵门(表 → 代码 → 行为) | Integration | **In Progress 🟡** | 计划外追加 · **实现已落,等全量回归**。用户裁定:表 = 独立 `camera-mode-requests.yaml`;先补 39 接线(S23-10 已完成)再上门;判据 = 动态矩阵 + 静态防绕过 |

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

**S23-11 实现进展(2026-11-02 · 收口读数待全量回归回来再补)**:
本件是 `S23-7 / S23-9 / S23-10` 三条的**上位** —— 那三条各自补了表中两行请求点,
但「表 ↔ 代码 ↔ 行为」的系统性判据仍不存在。**修完三个实例 ≠ 修掉那一类。**
⚠️ 用户 2026-11-02 三项裁定已写进卡面:① 表真源 = 独立 `camera-mode-requests.yaml`;
② **先补 39 接线,再上门**(⇒ 本件排在 S23-10 之后);③ 判据 = 动态矩阵 + 静态防绕过。
⚠️ **静态层单独存在会是第二扇假门** —— `EmergencyChannelDriver` 的调用是
`rig.SetMode(mode, RequesterId)`,mode 是**形参**,字面量在调用方 ⇒ 静态扫描只能证明
「有人**声明**要发」,证不了「它**会**发」。这条已写进卡面的「抓不到什么」节,不得被读成「档位全对」。
⚠️ **实现前先读卡面末尾的编排预警** —— 六行在一次 boot 里驱动齐,是本件最可能卡住的地方。

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
