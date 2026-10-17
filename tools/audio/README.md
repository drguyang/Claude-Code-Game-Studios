# StepFun 音频工具(`tools/audio/`)

`.claude/skills/audio-*` 五个技能共用的**唯一一份**音频代码。

## 为什么固化成共享工具

同型论证见 `tools/falsify/README.md`:

> 此前这些反证一律写在 `/tmp` 的一次性 shell 里,**每次重写、每次靠人记得还原**。

音频这边是同一回事,而且是五份:五个技能各自要做的
**重采样 / 去直流 / 归一化 / 裁静音 / 交叉淡化环绕 / 档位预判**
完全同构。抄五份 = 五处会各自腐烂的判据。

`.claude/skills/` 下 75 个技能**全是孤立 `SKILL.md`、零辅助文件** —— 本工具刻意放在
`tools/` 而不放进技能目录,既避免破那个先例,又让 `tools/` 的既有形状
(`kindgen/` `falsify/` `test-precheck/` `player-symbol-check/`,一目录一工具)继续成立。

> 原则:**重复调用,不重复实现。** 五个 `SKILL.md` 里那几行同构的调用命令是**应该**重复的;
> 同构的 Python 不是。

## 单一出处(本工具的立身之本)

**本文件不含任何档位阈值或白名单的字面量副本。** 每次启动都从 C# 现读:

| 判据 | 出处 |
|---|---|
| `SmallResidentBytes` / `LargeResidentBytes` / `ShortSeconds` / `LongSeconds` / `MusicPrefix` / 三个档位枚举值 | `unity/Assets/Editor.Tools.Bake/AudioImportSettingsGate.cs` |
| `WhitelistCategories` / `ForbiddenTriggerSources` / `RequiredCues` | `unity/Assets/Editor.Tools.Gates/AudioEventTableGates.cs` |

**锚点找不到 ⇒ 退出码 2(判据过期)**,而不是拿默认值静默失准。

理由:`AudioEventTableGates.cs` 自己的头注释写着「键集白名单的单一出处 = 本文件 …… **禁另抄一份**」
(两处执行 = 两处分叉)。Python 里重打一遍阈值,就是给同一件事开第二个真源 ——
阈值一改,一处动一处不动,而且不动的那一处不会有任何症状。

**静态数组里的常量名也要解析**(`CategoryMusicLayer` / `WorldBreathCue`),
只抓引号字符串会把这两项**静默丢掉** —— 少一项白名单比报错更坏。

## 只出测量,不出结论

| 类型 | 例子 | 可信度 |
|---|---|---|
| **测量**(事实) | 时长 / 字节 / peak / rms / dc / 削顶连续长度 | 直接读文件头与样本算出来的 |
| **判据**(照 C# 算) | `required_load_type` | 与 C# 逐条同构,可用 `verify` 跟 `.meta` 对账 |
| **advisory**(提示) | `seam_discontinuity_db` | **只能提示接缝存在,证明不了无缝** |
| **判断** | 「中性可滤波」「不报状态」「像不像病人」 | **机器给不了**,交人耳 |

侧车 JSON 里**只有前两类**,永远不写 `"status": "verified"` —— 生产脚本给自己发合格证,
就是本仓库最怕的那种绿。

## 环境约束

本机**没有** ffmpeg / sox / soundfile / librosa。可用:`numpy` + `scipy.signal.resample_poly`
+ stdlib `wave`。同型口径见 `tools/kindgen/kindgen.py` 头注释(「本机无 .NET SDK,故纯 Python」)。

- 重采样**只走 `resample_poly`**。`np.interp` 是混叠路径;48000→44100 是 **147:160 非整数比**,
  混叠出来的毛刺人耳常错记成「音色不对」,而不是「重采样没做对」。
- 因此所有请求强制 `response_format=wav`,落到非 RIFF/WAVE 当场拒绝。

## 退出码

| 码 | 含义 |
|---|---|
| `0` | 通过 |
| `1` | 失败(静音件、表头不符、名字不在冻结真源、档位不符、非语言素材走了 ASR …) |
| `2` | **判据过期** —— C# 里的锚点找不到,先修判据,别信本工具的任何输出 |

## 与 C# 闸门的分工

本工具**不**替代任何闸门,它是**前置**的:让不合格的件根本到不了
`AudioImportSettingsGate` / `AudioEventTableBinder` 面前。

`promote` 明确**不碰** `assets/data/audio_events.json`:

- cue 名是**闭集**(`RequiredCues` 实测只有 4 支),自造 cue 名没有意义;
- `rows[]` 的**顺序即 cue 序号** —— 插入一行会**静默重编号**,同一个 cue id 换到另一个文件上。

⇒ 事件表接线**归人**,且**只能追加行**。
