# 证伪轮驱动(`tools/falsify/`)

`/story-done` 的 **Falsifiability Gate**(S12-4,2026-10-15 立)要求:凡命中
**定序 / 共存互斥 / 定义域收窄 / 一次性幂等 / 阈值边界** 五类形态的夹具,
须逐条反证 —— 「停用生产码的对应守卫 ⇒ 该断言转红」;仍绿 = **假绿夹具** = BLOCKING。

此前这些反证一律写在 `/tmp` 的一次性 shell 里,**每次重写、每次靠人记得还原**。

---

## 为什么把它固化成代码

2026-10-17 补口轮实测到「一次性 shell」做法的**具体代价**:

> M1/M3 停用「组缺失 ⇒ `throw`」后,生产码落入分支 ④,而该分支
> **先 `settings.AddLabel(假组名)` 再 `AssetDatabase.SaveAssets()`**
> ⇒ 守卫一破,**失败的测试把两个假标签写进了入库的**
> `unity/Assets/AddressableAssetsData/AddressableAssetSettings.asset`。
> 夹具原有的「无残留」断言只查**组** —— 组确实没建,**建的是标签**。

同型事故在 `production/sprint-status.yaml:149` 已登记过一次(S14-8 静默改写 `audio.asset`)。
⇒ 本驱动把两条纪律固化为**代码**,而不是留在人的记忆里:

| # | 纪律 | 落点 |
|---|------|------|
| ① | **改 → 跑 → 还原在同一进程内完成** | `try/finally`;崩溃也还原 |
| ② | 还原后 **SHA-256 逐字节校验** | `restore_all()` |
| ③ | 跑测后 **受保护路径指纹必须未变** | `guards_fingerprint()` |

③ 的理由:证伪轮的目的**不是**「弄红一个测试」,而是「证明守卫在承重」;
**过程中污染入库资产 = 这次证伪本身有缺陷**,断言是否转红都不作数。

> ⚠️ ③ **刻意不要求「起跑时干净」** —— 开发中的仓本来就常带未提交的正当改动
> (本例:`data-core.asset` 是 S14-6 的既有改动)。要求起跑干净会让驱动在绝大多数
> 真实场景下直接退出码 2 —— 那不是严谨,是**不可用**。
> 正确判据 = **前后指纹相同**:证伪轮不得**新增**任何痕迹。

---

## 用法

```bash
# 干跑:只校验前提(行号 + 原文锚点),不跑 Unity —— 秒级
python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/<name>.json --dry-run

# 真跑
python3 tools/falsify/unity_falsify.py --spec tools/falsify/specs/<name>.json

# 追加受保护路径(默认见 unity_falsify.py 的 DEFAULT_GUARDS)
python3 tools/falsify/unity_falsify.py --spec … --guard unity/Assets/DataCooked
```

⚠️ **Unity batch 独占工程** —— 起跑前确认编辑器已关(`unity/Temp/UnityLockfile` 不存在)。
本驱动**不**代你查(竞态仍在);它只保证自己不并发。

### 退出码

| 码 | 含义 | 处置 |
|---|---|---|
| **0** | 全部变异**恰如预期**转红,且受保护路径指纹未变 | 通过 |
| **1** | 有变异**逃逸**(断言没红 = 假绿夹具),或**红得比预期多**(判别面不清) | 修夹具 |
| **2** | **夹具前提不成立**(目标行原文对不上)= **夹具过期信号**,不是判别力缺口 | 更新 spec 的行号/原文 |
| **3** | **还原或守卫失败**(SHA-256 不符 / 受保护路径被改动) | ⚠️ 最严重,立即人工介入 |

---

## 规格格式(`specs/*.json`)

```json
{
  "name": "人类可读的一轮证伪",
  "mode": "EditMode",                 // EditMode | PlayMode
  "filter": "PipelineGateTest",       // unity test --filter
  "unityProject": "unity",
  "guards": ["unity/Assets/AddressableAssetsData", "unity/Assets/Audio"],
  "mutations": [
    {
      "name": "F1-…",
      "file": "unity/Assets/…/Foo.cs",
      "line": 75,                       // 1-based
      "expect": "            <原文逐字>",  // 对不上 ⇒ 退出码 2
      "replace": "            <变异后>",
      "expectRed": ["test_a", "test_b"],
      "exactRed": true,                 // true ⇒ 红集须**恰好**等于 expectRed
      "_why": "这条守卫为什么承重"
    }
  ]
}
```

**`exactRed: true` 的用意**:只写 `expectRed ⊆ actual` 会让「变异打到了预期之外的断言」
蒙混过关(判别面不清)。边界类 / 定义域类变异应开 `exactRed`。

**每个变异从干净基线出发**(逐个施加、逐个还原,**不叠变异**)——
叠变异会让红的归属无法拆解。

---

## 与 `tools/kindgen/mutation_probe.py` 的分工

| | `kindgen/mutation_probe.py` | 本驱动 |
|---|---|---|
| 变异施加在 | **内存**(monkeypatch) | **工作树**(必须 —— 被测物是 Unity 编译产物) |
| 依赖 | 纯 Python + PyYAML | **Unity batch**(独占工程) |
| CI 常驻 | ✅(`tests.yml` 的 `kindgen-mutation-probe`) | ❌ 桌面轮(需 Unity license) |
| 风险面 | 零(不碰工作树) | **有**(故需 ①②③ 三条纪律) |

两者**互补不替代**:kindgen 那侧能在 CI 常驻,因为它变异的是纯 Python 判据;
Unity 侧判据只能走桌面 + 本驱动。

---

## 现存规格

| spec | 覆盖 | 变异数 | 立档 |
|---|---|---|---|
| `specs/audio-import-settings-and-playmode.json` | OQ-44-2 Import Load Type 门 + TD-audio-01f Play Mode Script 门 + wav 扩展名跨平台大小写 | **5**(F1–F5) | 2026-10-17 |

> **F5 的由来(2026-10-17 自查)**:本驱动**自己发现不了**它 —— 那是**语义**缺陷(漏扫),
> 不是**路径**污染,指纹守卫看不见。⇒ 它是人复核本轮新落生产件时抓到的,
> **正是「守卫 ≠ 自查」的实例**(见本目录 README 的「判据口径」条)。
