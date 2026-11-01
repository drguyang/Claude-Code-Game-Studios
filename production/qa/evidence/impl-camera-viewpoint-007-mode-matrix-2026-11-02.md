# 实现原件 —— camera-viewpoint/story-007 档位请求覆盖矩阵门

> **对象**:`production/epics/camera-viewpoint/story-007-mode-request-coverage-matrix.md`
> **批次**:`S23-11`(sprint-23 §八 第五条;用户 2026-11-02 指令「开工」)
> **截至时刻**:`2026-11-02 06:0x CST` · **commit**:`5351c69d` + 本批未提交工作树
> **裁定**:用户 2026-11-02 三项 —— ① 表真源 = 独立 `camera-mode-requests.yaml`;
> ② **先补 39 接线,再上门**(已由 `S23-10` 完成);③ 判据 = **动态矩阵 + 静态防绕过**

---

## 一、这一件修的是「那一类」,不是「那三个实例」

2026-11-02 之前 `R-2-5` 表有 6 条有效行,生产侧只有 4 条有请求点,而
`CameraSetModeRequesterGate` **全绿** —— 该门只判语法(`.SetMode(` 是否双参、
`requesterId` 是否字面 `-1`),**从不看那张表**。

三个缺口实例已分别由 `S23-7`(10)、`S23-9`(29)、`S23-10`(39)补上;
本件补的是**判据**。两者不可互替 —— **修完三个实例 ≠ 修掉那一类**。

---

## 二、三层判据(各自抓什么,写死)

| 层 | 载体 | 抓什么 | **抓不到什么** |
|---|---|---|---|
| **表** | `docs/architecture/camera-mode-requests.yaml` | 给门一个对照物;`R-2-5` 的 markdown 表降级为注记(承 ADR-024) | —— |
| **静态** | `CameraSetModeRequesterGate.RunModeRequestTable` | B1 系统号须是 `const RequesterId = N`;B2 该类型源码里须出现 `CameraMode.X`;B3 evidence 文件存在且**点名该行 id** | **「声明了但真的不发」** —— `EmergencyChannelDriver` 的 `rig.SetMode(mode, RequesterId)` 里 **mode 是形参**,静态扫描抓不到实参 |
| **动态** | `camera_mode_request_matrix_test.cs` | **表 ⊆ 记录**(逐行驱动,行 id 写在测试方法名里)· **记录 ⊆ 表**(发了没登记 = 第二真源) | 时机 / 次数 / 相机是否接受 |

> ⚠️ **静态层单独存在会是第二扇假门。** 它只能证明「有人声明要发」。
> 这条已写进门的不动串与卡面,不许被读成「档位全对」。

---

## 三、AC 记分

| AC | 判定 | 载体 |
|---|---|---|
| S7-01 表存在(6 active + 11 除外) | ✅ | `s7_01_*` |
| S7-02 B1 系统号常量 | ✅ | `s7_02_03_gateReportsNoModeTableViolations` |
| S7-03 B2 表 ⊆ 代码 | ✅ | 同上 |
| S7-04 表 ⊆ 记录(逐行) | ✅ | `test_cmr_001…006` 六条 |
| S7-05 记录 ⊆ 表 | ✅ | `test_s7_05_recordedRequestsAreAllInTable` |
| S7-06 记录器不进发布构建 | 🟡 **半证** | 源面 ✅(`s7_06_*`);**Release 变体编译 `NOT-RUN`** |
| S7-07 记录器不参与判定 | ✅ | `s7_07_recorderIsWriteOnlyInProduction` |
| S7-08 `TR-camera-008` 登记 | ✅ | 注册表 504 / 索引 347 |

---

## 四、证伪轮(6 条 · 四文件 SHA-256 逐字节还原 6/6)

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **P1** | 删 `SetMode` 的 `Record(...)` | **7 红**(6 行 + `s7_05`) | exact(全域) |
| **P2** | yaml 删 CMR-003/004 | 1 红:`s7_02_03` | exact |
| **P3** | `RequesterId` 39 → 40 | 1 红:`s7_02_03` | exact |
| **P4** | 生产凭空多 `Record(Treatment, 99)` | 1 红:`s7_05` | exact |
| **P5** | 生产码读 `TotalRecorded` | 1 红:`s7_07` | exact |
| **P6** | 撤 `#if`(连同 `#endif`) | 1 红:`s7_06` | exact |

### 🔴 P1 首轮抓出了我自己的空洞正控

记录器**停写**时,`test_s7_05`(记录 ⊆ 表)的违规集合恒为空 ⇒ 断言**恒真** ——
它是 7 条里唯一没红的,于是首次 P1 是 **6 红而非 7 红**。

处置:补正控(「本次必须真的采到东西」),复跑 P1 ⇒ **7/7 全红**。

> 这条比本轮任何一条断言都值钱:**没有正控的「无违规」断言,在什么都没发生时最绿。**
> 它和 F4(空洞正控)是同一族 —— 而且这次它藏在我**刚写好的**门里。

---

## 五、回归(三元组 + 等式)

| 套件 | 本批 | 基线(`S23-10`) | 等式 |
|---|---|---|---|
| EditMode | `6523 / 6486 / 0 / 0 / 37` | `6518 / 6481 / 0 / 0 / 37` | `6523 − 6518 = 5 = 5 新 − 0 删` ✅ |
| PlayMode | `290 / 286 / 0 / 2 / 2` | `283 / 279 / 0 / 2 / 2` | `290 − 283 = 7 = 7 新 − 0 删` ✅ |

`@ 2026-11-02 06:0x CST` · 批次 `S23-11` · commit `5351c69d` + 工作树。
PlayMode `exit=2` 伴 `failed=0`,系既有 2 条 `inconclusive`(与基线同形)。

**复算**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import xml.etree.ElementTree as ET
for f in ['unity/Logs/s23-em5.xml','unity/Logs/s23-pm5.xml']:
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})
"
# 期望: EditMode total=6523 passed=6486 failed=0
#       PlayMode total=290  passed=286  failed=0 inconclusive=2 skipped=2
```

---

## 六、实现期四次红(全部是我自己的问题,如实登记)

1. **命名空间遮蔽** —— 矩阵夹具命名空间写成 `...Tests.PlayMode.Camera`,
   在同一装配里**遮蔽 `UnityEngine.Camera`** ⇒ 一次红 **10 处既有夹具**。
   改 `...PlayMode.CameraMatrix`。**起作用的是命名空间,不是目录名。**
2. **`AC-2-01a` 判红** —— 相机命名空间禁可变静态字段,记录器有两个。
   **处置 = 搬位置到 `Presentation.Diagnostics`,不是豁免**(它不回流任何相机计算;
   「不参与」由 S7-07 守着)。搬完后漏了两处 `using`,连吃两次编译红。
3. **门链接线清单** —— `EntryWiringManifest` 须同批声明新接线。**门自己也受门管。**
4. **🔴 跑错工程根** —— `-projectPath "$PWD"` 而 cwd 在仓库根 ⇒ Unity 在**无 `Assets/` 的
   空工程**上反复初始化,**42 分钟零产出**(`CLAUDE.md` 早已写死此坑)。已 kill;
   `Library/` `Temp/` `Logs/` 三目录均 gitignore,不影响仓库,但**仍留在盘上**(约 308 M),
   待用户批准后删除。

---

## 七、证明不了的事(禁借绿)

1. **`S7-06` 的 Release 变体编译 `NOT-RUN`** —— 源面只证「有 `#if`」。
   「发布构建里真的编不出来」须真跑变体,本批未做。
2. **静态层 B2 只证「有人声明」** —— 见 §二。
3. **矩阵只证存在性** —— 时机 / 次数 / 相机是否接受,均不在本件面内。
4. **手写 YAML 解析器** —— 表格式大改(多行标量 / 嵌套)会静默漏行;
   `active != 6` 兜得住**漏行**,兜不住**错位**。
5. **「该发没发」仍有残余面:一次 boot 里驱动不出来的行** —— 本件按卡面允许的做法
   拆成**逐行多次 boot**,每行点名自己的测试方法;没有做「一次 boot 覆盖全部六行」的
   汇总断言。⚠️ 这意味着**行与行之间的相互干扰**不在覆盖范围内(例如 10 的动作期间 29 恰好
   死亡这种组合),未验证。
