# 裁定与实现原件 —— `OQ-20-11`(搜箱交互的形状)

> **对象**:`OQ-20-11`(`design/gdd/inventory-and-items.md` §Open Questions)
> **批次**:2026-11-02 · 用户指令「裁 OQ-20-11」→ 两轮 `AskUserQuestion` 裁定
> **裁定 ①**:甲 —— 搜箱 = **瞬时开关**(种子行改 `false` / `None` / `0`)
> **裁定 ②**:门同批 —— 连 17 的 `duration_ticks` 一起定,`4-DC-6` 第二半同批落
> **截至时刻 / commit**:2026-11-02 20:14:37 · `S23-26` · `5c48bbaa` + 工作树(读数见 §6.3)

---

## 一、先订正一处我自己的误引

上一轮(`5c48bbaa`)我在这条残余上写「归 `OQ-20-11`」。**引错了**,出处是 `BootRoot` 的一句旧注释。

| | 实况 | 复算 |
|---|---|---|
| `OQ-20-11` 的真实内容 | **搜箱交互的形状**(是不是持续交互)+ **时长归谁 / 多长** | `design/gdd/inventory-and-items.md:924`(改前) |
| 「箱内 vs 身上不可分」 | **至今没有 ID** —— 它是 20 的**容器语义缺口**(投影无「在哪个容器里」这一维) | 该行通篇未提「容器语义」四字 |

⚠️ **形态记一笔**:我复述了一条**别人写的注释**里的编号,没回去读那一条本身。
**编号的正确性不能靠转述传递。**

---

## 二、开工前的实测(裁定所依据的四个事实)

| # | 事实 | 复算 |
|---|---|---|
| 1 | 种子行已有值且**无出处**:`kind 4` ⇒ `suppresses_motor: true` · `duration_owner: "RoutedSystem"` · `id: 20` | `assets/data/interaction_kinds.json`;唯一一次提交 = `7eacc12`(2026-10-04) |
| 2 | 这两列**全仓零运行期读者** —— 只有 `4-DC-6` 的形状门与烘焙写入读它们 | `grep -rn "SuppressesMotor" unity/Assets --include=*.cs \| grep -v /Tests/` ⇒ 3 处,全是契约 / 门 / 烘焙 |
| 3 | `LeaseSource.Self`(4)全仓 **零生产 acquire** ⇒ 模态摊开时玩家仍能走 | `grep -rn "LeaseSource.Self" unity/Assets --include=*.cs \| grep -v /Tests/` ⇒ 仅 `ModalGate.cs:140`(定义处) |
| 4 | 17 的时长**根本没落值**:GDD 调参表写「未定」,数据表无该字段,代码无持续窗口 | `foraging.md:553` · `python3 -c "print(list(json.load(open('assets/data/foraging_constants.json'))))"` ⇒ 8 键无 `duration_ticks` · `grep -n "duration\|hold" HarvestInteractionDriver.cs` ⇒ 零命中 |

**⇒ 事实 3 与事实 4 是裁定中最重要的两条**:它们说明「既有填值」与「既有声明」都**没有行为**。

---

## 三、裁定

### 甲 —— 搜箱 = 瞬时开关

| 判据 | 内容 |
|---|---|
| ① | 容器屏是**模态**(`ModalId.InventoryContainer`),开多久由**玩家**决定 ⇒ 定值 `duration_ticks` 对它**语义上不成立**(对比 17 采集 = 「站在某处做一会儿」的世界动作) |
| ② | 「模态摊开时玩家仍能移动」是**全部 8 个模态共有**的问题 ⇒ 不该由 20 按 Kind 逐屏表态;8 屏各写一份 = 8 条 Release 路径,漏一条即永久定身 |
| ③ | ⇒ 种子行改 `suppresses_motor: false` / `duration_owner: "None"` / `duration_owner_system_id: 0` |

### 门同批 —— `4-DC-6` 的第二半

`4-DC-6b`:凡 `SuppressesMotor == true` 的行,其归属方**必须真的声明过持有时长**
(值 ≥ 1),映射由**烘焙期从真实数据表派生**(当前唯一项:17 → `duration_ticks`),**表外系统 = 未声明**。

⚠️ **门不能只查「GDD 里有声明字样」** —— 那是 **F6 自述顶替门**,本仓已抓过五次。

---

## 四、⚠️ 我在落地前更正的一个假前提

我给用户的门选项写了「17 已由 `OQ-17-3` 声明过 ⇒ 门可以安全落下」。**不成立**:

| 面 | 实况 |
|---|---|
| `OQ-17-3` 裁了什么 | 只裁**形状**(采集 = 持续交互)+ **位置**(时长归 17 的动作数据表);值**明确未定** |
| GDD 调参表 | `采集动作时长` = **未定**(`foraging.md:553`) |
| 数据表 | `foraging_constants.json` **没有** `duration_ticks` |
| 代码 | `HarvestInteractionDriver` **没有**持续窗口 —— 按下即结算 |

⇒ 若照原计划直接落门,**烘焙会因 `kind 1`(17)转红,阻塞出货** —— 正是 4 的 GDD 警告的形态。
⇒ 用户裁定「连 17 的时长一起定」,本轮**先给 17 定值,再落门**。

---

## 五、落地清单

| # | 件 | 落点 | 复算 |
|---|---|---|---|
| 1 | 种子行改值 + 重烘 | `assets/data/interaction_kinds.json`(kind 4) | 产物 330 B / 10 行 / `ConfigVersion = 0x3629E170` |
| 2 | **`4-DC-6b` 校验器** | `InteractionKindTableValidator.ValidateDurationDeclared` | 见 §六 夹具 |
| 3 | **烘焙期调用点** | `InteractionKindBaker.BakeFromRepo`(映射从真实数据表派生) | 见 §六 探针 |
| 4 | **17 的时长落值** | `duration_ticks = 20`(1.0 s @20 Hz;`[1, 200]`) | 重烘 `ConfigVersion = 0xABE70250` |
| 5 | 全链穿透 | `foraging_constants.json` → Schema 白名单 → Binder → CookedWriter → CookedCodec → `ForagingConstants` | 6 个文件 |
| 6 | 夹具 | `data_contract_validation_test` **+4** · `foraging_data_load_test` **+2** | 见 §六 |
| 7 | 文档 | `inventory-and-items.md` 规则十补注 + `OQ-20-11` 结案 · `interaction-system.md` 对照表 + 四处回刷 + 结案块 · `foraging.md` 调参表填值 + `OQ-17-3` 订正 | — |

### 值为什么是 20 tick

`skeuomorphic-ui.md` / `foraging.md §Game Feel` 只约束「原子性 + 中段之前可零效果中止」,
没给数。同族的既有量纲:CPR 动作周期 ≈ 550 ms(11 tick)、配方 `duration_ticks` = 30/60/90/120、
急救 `MinHoldTicks = 5`(250 ms)。**采集 = 1.0 s** 落在「蹲下 → 伸手 → 拔起」的常识区间,
且长于配方最短档的 1.5 s 之下 —— 采集是高频动作,不应比一次炮制还慢。
**安全区间 `[1, 200]`**:下界 1(0 = 瞬时,与 `SuppressesMotor = true` 矛盾)、上界 200
(= 10 s,超过即玩家会以为卡死)。**值本身仍可调**(数据驱动)。

---

## 六、验证

### 6.1 定向夹具

| 文件 | 条数 | 覆盖 |
|---|---|---|
| `EditMode/Interaction/data_contract_validation_test.cs` | +4 | 4-DC-6b:合法通过 / 归属方未声明 / 声明 0 / 容器行不触发 |
| `EditMode/Foraging/foraging_data_load_test.cs` | +2 | `duration_ticks` 域守卫(0 / 201) |

### 6.2 门**真的会红**(探针轮,两次,同进程「改 → 跑 → 还原」)

| 探针 | 期望 | 实测 |
|---|---|---|
| `duration_ticks: 20` → `0` | 烘焙 `Failure`,日志含 `4-DC-6b` | ✅ `hit=True bakedGreen=False` |
| 删除 `duration_ticks` 键 | 烘焙 `Failure`,日志含「读不到 duration_ticks」 | ✅ `hit=True bakedGreen=False` |

两次均 **SHA-256 逐字节还原**,`FINAL-OK`。

⚠️ **第二探针特意加的**:`ReadRequiredInt` 对缺键是**静默 false**(它假定 `RequiredKeys` 那一遍报过)。
若不把它转成显式错误,「缺 `duration_ticks`」会让声明集为空 ⇒ 门静默失效 ——
**正是 `OQ-20-11` 那个「看不见的绿」的同一种形态**。同批删掉了校验器里
「空声明集 ⇒ 直接 `return`」那一行(那是一门在空输入上恒绿的**空洞正控**,F4)。

### 6.3 全量回归

**截至时刻 · 批次 · commit**:2026-11-02 · `S23-26` · `5c48bbaa` + 工作树。

| 平台 | 读数(total / passed / failed / inconclusive / skipped) | 时刻 | 基线(`S23-25`) | 等式 |
|---|---|---|---|---|
| EditMode | `6613 / 6576 / 0 / 0 / 37` | 20:13:49 | `6607 / 6570 / 0 / 0 / 37` | **`+6`** = 本轮新增夹具(`data_contract_validation_test` ×4 + `foraging_data_load_test` ×2) |
| PlayMode | `311 / 307 / 0 / 2 / 2` | 20:14:37 | `311 / 307 / 0 / 2 / 2` | `+0` |

本仓 PlayMode 常态退出码 = 2;判据取 XML 的 `failed` 计数 ⇒ `failed = 0`。

等式复算:
```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/oq2011-full-em.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/oq2011-full-pm.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
grep -c "public void test_ac415_dc6b_" unity/Assets/Tests/EditMode/Interaction/data_contract_validation_test.cs   # 期望 4
grep -c "public void test_simSide_durationTicks" unity/Assets/Tests/EditMode/Foraging/foraging_data_load_test.cs  # 期望 2
```

---

## 七、证明不了的事 / 仍未闭(禁借绿)

1. **17 的持续窗口行为未实现** —— 值有了、门有了,但 `HarvestInteractionDriver` 仍是
   **按下即结算**。⇒ **不得读成「采集已是持续交互」**:今天它只有声明,没有行为。
   已立 **`TD-foraging-04`**。
   ⚠️ 连带的已知后果:`OQ-17-3` 自己写过「若采集瞬时,`AC-17-08`(中途被打断 ⇒ 零效果)
   **恒真不可判**」⇒ 该 AC 今天处于**判据空转**。
2. **「模态摊开时玩家仍能移动」未修** —— 8 个模态全部如此。已立 **`TD-interaction-04`**(归 4/1)。
3. **「箱内 vs 身上不可分」** —— 与 `OQ-20-11` 无关(见 §一),仍未闭、仍无 ID。
4. **4-DC-6b 的映射表是手写的**(`{17 → foraging_constants.duration_ticks}`)。
   表外系统 = 未声明 ⇒ 这是**刻意的登记面**,不是自动化发现;新增 `SuppressesMotor` 行时必须同步登记。

---

## 八、本轮如实登记的过程事故

| # | 事故 | 处置 |
|---|---|---|
| 1 | 上一轮把「箱内 vs 身上」误引为 `OQ-20-11` | 见 §一;三处口径已订正 |
| 2 | 给用户的门选项里给了**假前提**(「17 已声明」) | 落地前自查发现并更正,顺序改为「先给 17 定值再落门」 |
| 3 | 加必填键导致 `foraging_data_load_test` 的三条合成 JSON 缺键 | 三个夹具就地补 `duration_ticks`(不是放宽判据) |
| 4 | 校验器首版「空声明集 ⇒ return」= 空洞正控 | 删除该分支 + 加一条空集夹具钉住 |
