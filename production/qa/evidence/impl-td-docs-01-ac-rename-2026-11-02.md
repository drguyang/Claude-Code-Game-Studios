# 实现原件 —— `TD-docs-01` 改名轮:ADR-020 自有 AC 全 17 个改名

> **对象**:`TD-docs-01`(已 `closed`;本轮是**同日改判**的兑现,不是新债)
> **批次**:2026-11-02 · 用户指令「选 2 来进行修复」
> **裁定**:`AC-20-01…17` → **`AC-ADR020-01…17`**(全 17 个),库存侧 `AC-20-01…25` 一字不动
> **上游件**:`impl-td-docs-01-ac-id-gate-2026-11-02.md`(第一轮:裁定 ① 零改名 + 门)

---

## 一、裁定沿革(两轮,同日)

| 轮 | 裁定 | 覆盖 |
|---|---|---|
| 第一轮 | **① 零改名** —— 引用时写 `ADR-0NN AC-NN` | 17 个撞号**永久接受** |
| **第二轮(现行)** | **全 17 个改名** | 17 个撞号**从 ID 层面消除** |

⚠️ **我给出的第二选项写的是「给 15/16/17 改名」,范围不足**:撞号是 **17 个**
(`AC-20-01…17`),只改三个会留下 14 个,门照样红。该缺陷在提问时**已当面指出**
并按「全 17 个」重新确认后才动手。

**为什么最终不只改写法**:裸串本身仍有歧义 —— `grep AC-20-15` 会同时命中两侧;
改 ID 之后**从字符串层面就不可能再歧义**,而写法约定仍可能被人写错。

---

## 二、改名的判据是**语义**,不是编号(这是本轮的真正难点)

同一个裸串在两个语义面**都在用**,逐处判:

| 裸串 | ADR-020 语义(**改**) | 库存语义(**不改**) |
|---|---|---|
| `AC-20-10` | `BootRoot.cs:835` AudioListener 单挂点 | `InventoryContainerScreen.cs:12` 禁负重条 / 百分比 / 数字 |
| `AC-20-03` | `architecture.md:893` 连续位置不写流 | `CompositionRoot.cs:943` 三流 ∪ 基线扫描域 |
| `AC-20-15` | `architecture.md:915` 相机优先级 | `traceability-index.md:1046` DTO 无 float |
| `AC-20-13` | `tr-registry.yaml:1608` `Append` 权 = 主机 | `BootRoot.cs:2054` `CARRY_CAP` 未定值 ⇒ 硬失败 |

⇒ **凡按文件整片替换都会改错**。分类方式:
- **纯相机 / 玩家语义文件** → 整片改(46 件)
- **混杂文件** → **逐行判**,行号写死进脚本(10 件)
- **故意不改** → 库存侧全部 + **记录本债自身的历史件**(债册 / `sprint-23` / `active.md` /
  `interaction-system.md:671-674` —— 后者是 4 的 GDD 早先对**同一个撞号**的登记,原文不删)

> `interaction-system.md:671-674` 值得单独记:4 的 GDD **在更早的时候就已经发现**
> 「system 20 的 `AC-20-03` 与 ADR-020 的 `AC-20-03` 不同物」,并写下了当时的处置
> ——「引用 ADR-020 判据时一律写全 `ADR-020 AC-20-03`」。**那正是本轮第一轮的裁定 ①**。
> ⇒ 这次不是「从零发现」,是**把一个局部已存在的约定推广到全库**。

---

## 三、落地量

| 项 | 数 | 复算 |
|---|---|---|
| 改名处数(脚本自报) | **245** | 见 §五 的量纲说明 |
| 涉及文件 | **67** | — |
| `unity/Assets` 内新名 | 43 | `grep -rhoE "AC-ADR020-[0-9]+" unity/Assets --include=*.cs \| wc -l` |
| `docs/architecture` 内新名 | 63 | `grep -rhoE "AC-ADR020-[0-9]+" docs/architecture \| wc -l` |
| 纯相机 GDD 残留裸 ID | **0** | `grep -cE "AC-20-(0[1-9]\|1[0-7])" design/gdd/camera-and-viewpoint.md` |
| 库存定义完好 | **25** | `grep -c "^| \*\*AC-20-" design/gdd/inventory-and-items.md` |

---

## 四、门的同步变化 + 本轮抓到的真缺陷

| 项 | 改前 | 改后 |
|---|---|---|
| `acid-baseline.json` | 17 条冻结 | **空**(零容忍) |
| `--check` | ✅ + 「仍存 17 个已知撞号」 | ✅ 无提示 |
| `--mutation-probe` | 拿**真基线 ID**当靶 | **完全合成**,与仓库现状解耦 |

🐞 **`--mutation-probe` 的自崩是本轮实测抓到的真缺陷**:它用
`sorted(base["known_collisions"])[0]` 取一个真实基线 ID 当靶 —— 基线一清空就
`IndexError` 直接崩,**探针自己先死了**。改法 = 合成一套最小定义集(`AC-99-01`),
M1–M4 只测**判据逻辑**,真实接线由 `--check` + 真文件变异(R1–R3)覆盖。
⚠️ 这条值得记:探针依赖了「基线非空」这个**偶然事实**,而那个事实恰恰是本轮要消灭的。

### 证伪轮(3 条真文件变异,全红;SHA-256 逐字节还原 3/3)

基线已空 ⇒ C2/C3 无法再靠「删定义」触发,改为**临时给基线塞一条**再打:

| 变异 | 打的是什么 | 结果 |
|---|---|---|
| **R1** 给 `adr-020` 补 `- [ ] **AC-20-18(PROBE)**:` | 库存侧本就有 `AC-20-18` ⇒ **新撞号** | `exit=1`,命中 **C1**;`RESTORE OK` |
| **R2** 给基线塞一条不撞号的 `AC-20-25` | 基线记着一个已不存在的撞号 ⇒ **基线陈旧** | `exit=1`,命中 **C3**;`RESTORE OK` |
| **R3** 基线声明 `AC-20-02` 单方 + 探针文件补第二方 | 实测定义方 > 基线声明 ⇒ **撞号扩大** | `exit=1`,命中 **C2**;基线复原 + 探针删除 |

`FINAL-OK` · `SUMMARY failures=none`。
另有内存内 `--mutation-probe`(M1–M4 全合成)⇒ `PROBE-OK`。

---

## 五、回归

**截至时刻 · 批次 · commit**:2026-11-02 · `S23-32` · 工作树(基线 = `1fb81d00`)。

| 平台 | 读数(total / passed / failed / inconclusive / skipped) | 时刻 | 基线 | 等式 |
|---|---|---|---|---|
| EditMode | `6624 / 6587 / 0 / 0 / 37` | 21:59:35 | `6624 / 6587 / 0 / 0 / 37` | **`+0`** |
| PlayMode | `311 / 307 / 0 / 2 / 2` | 22:00:26 | `311 / 307 / 0 / 2 / 2` | **`+0`** |

本仓 PlayMode 常态退出码 = 2;判据取 XML 的 `failed` 计数 ⇒ `failed = 0`。

⚠️ **`+0` 是预期结论**:本轮改的是注释 / 文档 / 错误信息文本,**零行为改动**。
等式 `total − 基线 total = 0` 正是在验它 —— 若不为 0,说明改名动到了别的东西。

**另核一处易踩的雷**:`downed_camera_requester_test` 会**真读**
`tr-registry.yaml` 与 `traceability-index.md`(S29-08 登记面判据)。
已核其断言只认 `TR-camera-007`、**不含任何 AC 串**;全量 EditMode 里该条仍绿(见读数)。

复算:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/s2332-em.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
python3 -c "import xml.etree.ElementTree as ET;r=ET.parse('unity/Logs/s2332-pm.xml').getroot();print(r.get('total'),r.get('passed'),r.get('failed'))"
```

---

## 六、证明不了的事(禁借绿)

1. **「245」与「全库 `grep AC-ADR020-` 的 248」不是同一个量纲。**
   245 = 改名脚本**替换掉的 ID 个数**;248 = 全库命中数,差额来自记录本债自身的件
   (债册 / `sprint-23` / `active.md`)+ `coding-standards.md` 的示例行。**不要互相校验**。
2. **分类是人工判定,不是机械判定。** 门只认「定义」句式,**不管引用语义**。
   ⇒ 若我把某处库存语义误判成相机语义,门**不会红**;本轮靠的是逐行看正文。
   已做的兜底:纯相机文件改完后**裸 ID 归零**(§三 的 0 / 0 两行),库存定义行数不变(25)。
3. **`ADR-019` 的潜伏撞号没动。** `AC-19-01…08` 仍是 ADR 号编号,系统 19 今日无 GDD;
   该系统 GDD 成文那天会被 **C1** 抓住。
4. ~~**CI job 仍未在真实 CI 上跑过** —— 今日有效性 = 桌面可复算。~~
   🔁 **2026-11-02 订正(原句措辞误导)**:真实原因是 **`origin/main` 落后本地 27 笔、从未推送**
   (`git rev-list --count origin/main..HEAD` ⇒ 27;`origin/main` 停在 `ae4f80b6`)——
   即「**还没推**」,不是「CI 配置坏了」。
   ⚠️ 这条与 **`TD-ci-01` 无关**:那个债管的是 `.github/workflows/unity-tests.yml`
   (被 `UNITY_CLIENT_ID/SECRET` 门控、真跑不起来);本门的 job 在 `tests.yml`,
   **不依赖任何 secret**。⇒ 推送后它会照常跑,现在不需要任何工作。
5. **改名不改变任何运行时行为** —— 全部是注释 / 文档 / 错误信息文本。
   ⚠️ `BootRoot.cs:1650` 的 fail-loud 报文**是字符串字面量**,内容变了。
   **没有任何夹具断言这段文本** —— 复核命令:
   `grep -rn "AudioListener 单挂点" unity/Assets/Tests --include=*.cs` ⇒ 只命中
   `camera_follow_wiring_test.cs:104` 的一句**注释**(「未完成 = 单挂点断言可能已抛」),
   不含报文全文;`grep -rn "AC-20-10" unity/Assets/Tests --include=*.cs` 唯一命中是
   `inventory_container_content_test.cs:17` 的**库存语义**注释(满溢感),与本条无关。
   ⇒ 报文改写不影响任何判据。

---

## 七、收口复核:漏改 8 处(2026-11-02 二轮 · 用户问「还有遗留问题吗」)

**触发** —— 用户追问三条残留是否有遗留。逐条复算后,**第 2 条(分类是人工判定)真的有遗留,
且已经实际漏掉 8 处** —— 不是「风险」,是**已发生的漏改**。

### 漏在哪

| 文件 | 处数 | 为何漏 | 语义判定 |
|---|---|---|---|
| `.claude/docs/technical-preferences.md` | **7**(519 / 520 / 524 / 533 / 550×1 行 3 个) | 该文件**不在**改名脚本的 `PURE` / `PURE_DIR_PREFIX` / `LINES` 任一名单内 —— `grep technical-preferences <script>` 零命中 | ADR-020 日志条目:`AC-20-01`(CharacterController)/ `-02`(Cinemachine)/ `-03`(位移边界)/ `-10`(AudioListener)/ `-15`,`-16`,`-17`(Amendment C) |
| `production/epics/prescription-medication/story-005-…md` | 2(39 / 87) | 「`ADR-020 AC-20-03` **前缀式**」写法 —— 带前缀,人读无歧义,脚本的裸 ID 正则也命中,但语义判定时被当成「已明引」放过 | 「承 ADR-020 `AC-20-03` 先例 = 反射断言」→ ADR-020 语义 |
| `production/epics/prescription-medication/story-004-…md` | 1(17) | 同上 | 同上 |
| `production/epics/disease-simulation/story-001-…md` | 1(38) | 同上 | 同上 |
| `production/epics/emergency-procedures/story-001-…md` | 1(27) | 同上 | 同上 |
| `design/gdd/prescription-and-medication.md` | 2(979 / 1040) | 同上 | 同上 |
| `design/gdd/interaction-system.md` | 1(674) | **不是漏改,是「旧约定是活指令」** —— 该行写着「本 GDD 其他处引 ADR-020 判据时,**一律写全**「ADR-020 `AC-20-03`」」,即裁定 ② 之前的写法规则;改名后它对 ADR-020 已不成立 | 已就地订正 + 🔁 注(原文不删) |

### 为什么门没抓到(对 C5/C6 的真实边界)

- **C5 是「混写」判据,不是「正确」判据。** `technical-preferences.md` 里**一处 `AC-ADR020` 都没有**
  ⇒ 它不构成「混写」⇒ C5 结构性沉默。
- ⇒ **C5 的天花板:纯裸名引用 ADR 自有 AC 的文件,门永远看不见。**
  这类只能靠**引用面普查**(本轮 §三 做的是「纯相机文件裸 ID 归零」,但那份名单本身是人工列的,
  `technical-preferences.md` 就没被列进去)。
- 反过来,**C5 在修完 story-005 第 39 行后立刻转红**(第 87 行仍是裸名)——
  这条**正向证明了 C5 在「文件里两种写法并存」时确实有效**,只是管不到「全裸」的文件。

### 修法与复算

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 tools/acid/acid_check.py --check          # 期望:✅(修前因 story-005 转红时 exit=1)
grep -n "AC-20-[0-9]" .claude/docs/technical-preferences.md   # 期望:零输出
```

| 指标 | 修前 | 修后 |
|---|---|---|
| `.claude/docs/technical-preferences.md` 裸 `AC-20-NN` | 7 | **0** |
| 5 个 story/GDD 的 ADR-020 语义裸引用 | 7 | **0** |
| `acid_check.py --check` | — | `exit=0` |

### 仍然留下的(如实登记)

1. **`interaction-system.md:672` / `adr-019:173` / `coding-standards.md:261,301`** 仍含
   「ADR-020 … `AC-20-0N`」字样 —— 全是**记录改名历史**的原文引用(「原文不删」纪律),
   不是活引用。⇒ 不要把这几处当成漏改。
2. **`death-and-respawn.md:577` 的 `AC-20-19`** 是**库存**语义(回捡约束),
   与 ADR-020 同段出现 ⇒ 前缀式正则的假阳性。**未改,也不该改。**
3. **这类漏改今日仍无机械判据。** 未新增 C7 的理由:实测若按「同行出现 ADR-020 名 + 裸 AC-20-NN」
   硬判,命中 **41 行**,其中绝大多数是记录件;按前缀式(40 字符窗)判,命中 **24 行**,
   剔除记录件后仍需 ~5 条白名单 —— 会复制 C5/C6 的「白名单腐烂」问题,收益不抵。
   ⇒ **留作人工面**,登记在此,不假装已机械化。
