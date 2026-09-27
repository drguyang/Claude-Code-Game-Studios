# EditMode Console 警告归因报告(905 测全绿 · 90 行日志)

- **来源 XML**: `unity/Logs/s005-final2.xml` — testcasecount=**905**, passed=**905**, failed=**0**, skipped=0
- **日志总量**: **90 行**,分布在 **32 个测试**中,全部 `Passed`
- **关键结论**: **没有一条是 bug** —— 全部是「负例测试的真实输出」或「诊断探针残留」
- **生成时间**: 2026-09-27

> 说明:`unity/Logs/` 按 .gitignore 设计不入库;本报告落 `production/qa/` 作持久记录。

---

## 一、总量与分布

| 测试类 | 日志行 | 性质 |
|---|---:|---|
| `HashMismatchRecoveryTest` | 50 | 负例的真实输出(见 ②) |
| `BreathLayersTest` | 38 | 诊断探针残留(见 ①)+ H-B 事实记录(见 ③) |
| `OverridesSidecarTest` | 23 | 负例的真实输出(同 ②) |
| 其余 29 个测试 | 8 | 单行诊断/ADVISORY |
| **合计** | **90** | — |

---

## 二、四族归因

### 族 ① 诊断探针残留 —— 28 行(应删)

**位置**: `unity/Assets/Tests/EditMode/Audio/breath_layers_test.cs`
**归属测试**: `test_busVolumeExposedConfig_matchesGroupVolumeHash_andGetFloatReadable`
**行首标记**: `[诊断·反射·读内部面]`

这些是 **Story 004 定性 exposed 通路时临时加的诊断代码**,探针使命(H-B 定性)完成后**未清理**。逐行内容:

```
[诊断·反射·读内部面] GetType() = UnityEditor.Audio.AudioMixerController;
   GetProperty("exposedParameters") = found(UnityEditor.Audio.ExposedAudioParameter[])
[诊断·反射·读内部面] 读到 12 条;元素类型 = UnityEditor.Audio.ExposedAudioParameter;
   逐项 GetFields + GetProperties 全量 dump(不预设字段名)
[诊断·反射·读内部面] element dump: | field guid(GUID)=765acee8… | field name(String)=bus_volume_master
[诊断·反射·读内部面] 返回值非数组:<类型全名>
[诊断·反射·读内部面] 属性不存在 —— [停手出口2]
[诊断] 反射读回 12 条;YAML 写入 12 条
[诊断] exposed: name=… guid_type=… guid_raw=… | YAML 写入 guid=…
[诊断结论·停手出口1/2/3] …(各出口一行)
[判据记录·H-B] 7×SetFloat 在 EditMode 全部返回 false;对照组 …亦 false;GetFloat 对同一真实名字返回 true
```

**为何该删**: 它们是 `Debug.Log`(Debug 级),不是断言;记录的是一次性定性结论,结论已落进 `active.md` 与本报告。

**风险**: 低。纯删除,不触碰任何断言与控制流。
**对照**: 同文件其他破坏性测试都带「夹具自证」,独这批没有 —— 因为它们**不是测试**,是调试代码。

---

### 族 ② 负例测试的真实输出 —— 50 行(保留)

**位置**: input-system `HashMismatchRecoveryTest`(50 行)+ `OverridesSidecarTest`(23 行中的一部分)
**行首标记**: `绑重 schema hash 失配` / `绑重 sidecar 失配` / `绑重载荷被出厂 API 拒收` / `绑重 sidecar 半截` / `绑重 sidecar 头部缺 schema_hash`

这些是**实现侧 `Debug.LogWarning` 的真实输出** —— 测试主动构造损坏/失配 sidecar,实现按设计降级为默认绑定并记录。

**为何必须保留**: 删掉等于让「损坏时静默降级」—— 而这正是该测试要证明的行为。
**它们被谁认领**: 由 `LogAssert.Expect(LogType.Warning, …)` 逐条认领,所以测试全绿。

> 这条与昨天那条 `SetError`(`绑重载荷被出厂 API 拒收…`)是**同一现象**:消费了但原始行照样输出。

---

### 族 ③ H-B 事实记录 —— 1 行(保留,但应降为注释)

```
[判据记录·H-B] 7×SetFloat 在 EditMode 全部返回 false;
对照组 SetFloat("___this_param_does_not_exist___") 亦 false;
GetFloat 对同一真实名字返回 true ⇒ SetFloat 的按名查表 native 路径在此环境不通。
```

**性质**: 一条 `Debug.Log`,是 **H-B 定性结论的唯一书面落点**(写在测试里)。
**为何保留**: 它记录了「EditMode 下 `SetFloat` 对任何名字都 false」这一关键事实 —— 后来者若误以为「`SetFloat` 坏了」,这条能直接回答。

**建议**(可选): 从 `Debug.Log` 降为普通 `//` 注释,语义不变、噪声归零。
**风险**: 极低,但会改动一处文本,故本轮不动,先登记。

---

### 族 ④ 纯防线日志 —— 6 行(必须保留)

**位置**: Story 004 交付的纯函数库
```
[FilterRamp]  ramp 时长须 > 0(硬下界 = AudioTuning.RampSeconds)—— 按 0 处理(直接到位,不产生斜坡)
[FilterRamp]  dt < 0 —— 忽略本帧推进
[FilterRamp]  频率须 > 0 Hz —— 返回 0 轴(安全值)
[BreathPhaseGate] 相位分母非法(clip 未就绪 / timeSamples < 0)—— 返回 0(周期原点)
[BreathPhaseGate] INSPIRE_FRACTION 须 ∈ (0,1) —— 本周期不触发(GDD §Tuning Knobs 安全范围)
[InspirePhaseMapping] 完整呼吸周期 T 须 > 0 —— 返回 0
```

**为何必须保留**: 这是**防御性降级路径的唯一可观测证据**。删掉后,「配置非法时静默返回 0」与「配置合法但算错了」将无法区分 —— 属于**用日志换可诊断性**的正当用法。

---

### 族 ⑤ 探针/ADVISORY —— 6 行(应删)

```
[MixerAssetGenerator] [exposed·YAML] exposed 名「tier_passband_center_hz」查不到同名组的 m_Volume 哈希 —— 拒绝写假 guid
[b5 WARN]
[b5-W] 引用「Sim」= 既有工程债(基线放行,WARN 不红)
[C4] UnityEngine.EventSystems 未装载 —— FocusController 反射面跳过
[AC-42 观察] Unity 内置序列化器往返:原 raw=…,观察 raw=…(相等=False)—— 仅记录,不作断言
[AC-10-ADVISORY] 单次 F8+F9 求解 = N µs(阈值 N µs);最后结果 OutputQty[N]=N
```

**性质**: Story 014/Story 005 加的一次性诊断探针,使命已完成。
**为何该删**: 与族 ① 同理 —— 是调试残留,不是测试。

---

## 三、处置建议

| 族 | 行数 | 处置 | 风险 |
|---|---:|---|---|
| ① 诊断探针残留 | 28 | **删**(逐条,一次一处,每处验编译) | 低 |
| ② 负例真实输出 | 50 | **保留** —— 删了等于让损坏静默降级 | — |
| ③ H-B 事实记录 | 1 | **保留**(可选降为注释) | 极低 |
| ④ 纯函数防线 | 6 | **必须保留** —— 可诊断性来源 | — |
| ⑤ 探针/ADVISORY | 6 | **删** | 低 |

**净效果**: 90 → **57 行**(砍掉 38%),且砍的全是调试残留;所有「证明防线会响」的日志一条不动。

---

## 四、未解问题(归你裁定)

1. **族 ③ 是否降为注释?** 语义不变、噪声归零,但会改动一处文本。我倾向降,但不在本轮擅自做。
2. **族 ① 的删除需逐条进行** —— 上一轮我按行批删搞坏了文件两次(删掉 3 个 helper、5 个方法声明)。按你的要求一次一处,每处跑一次编译验证。**预计 14 轮工具调用**。
3. **是否要连 input-system 那 50+23 行一起处理?** 那是 Story 005/008 的负例输出,我建议**保留** —— 与族 ② 同理。
4. **跨平台 flaky 风险**: 族 ④ 的日志用 `Debug.LogError`,在 IL2CPP 下若钳位边界因末位差恰越 ±24,`test_computeSnrDb_exactMinus24_notClamped` 可能变红。建议补一次 IL2CPP 构建跑测,但不阻塞本次清理。

---

## 五、验证基线

- 过滤 `SnrAnalysis|TierFilterCarrier` → **32/32 Passed**
- 全量 EditMode → **905/905 Passed** · 0 失败 · 0 跳过
