# U0a 工具链核验 · 桌面操作卡

> **Status**: **CLOSED(2026-09-22)**(U0a = Pre-Production 实质总前置,见 `production/gate-reports/2026-09-21-technical-setup-to-pre-production.md:102-106`)
> **执行地点**: 用户自己的桌面机(Windows / macOS / Linux 任一,**要有真桌面**)—— 本集群无 X11 / 无 libGL / 无 sudo,编辑器装也起不来(license 激活须 Hub 浏览器登录,无纯命令行路径)。
> **不要**: 把 Unity 工程根放在 `/XYFS01` NFS 挂载上跑编辑器(`Library/` 碎文件 IO 慢到不可用,且与集群侧 git 互相踩)。桌面机本地盘 clone 一份 repo 干 U0a/U1 的活,文档与 sim 代码继续在集群推进,两边靠 git 同步。

## 步骤(验收判据 = 门报告口径)

- [x] **1. 装 Unity Hub** —— unity.com/download(或 Linux:`wget https://download.unity3d.com/download_unity/linux/UnityHubSetup.AppImage && chmod +x && ./UnityHubSetup.AppImage`)
- [x] **2. 装 Unity 6.3 LTS 编辑器** —— Hub → Installs → Unity 6.3 LTS 最新点版本(形如 `6000.3.xxfy`);勾选 Windows/Mac/Linux 64-bit 模块 + IL2CPP(ADR-012 对拍需要)
- [x] **3. 激活 license** —— Hub 内登录,Personal 即可。⚠️ **已裁口径:CI 用的 `UNITY_LICENSE` secret 手工配置,不自动化 license**(ADR-012 / 门报告)
- [x] **4. 记下确切补丁号** —— 版本号 + changeset 哈希(Hub → Installs → 版本齿轮 → About This Installer)⇒ **`6000.3.24f1` (`4e7b9b5b6244`)**
- [x] **5. 建空 URP 工程能进 Play Mode** —— 验证安装完整(模板选 URP;工程放桌面机本地盘)⇒ 已验进 Play Mode

## 本机环境登记(U0a 实测面)

| 项 | 值 |
|---|---|
| **桌面机 OS** | Ubuntu 22.04 |
| **编辑器** | Unity `6000.3.24f1` |
| **changeset** | `4e7b9b5b6244`(`ProjectSettings/ProjectVersion.txt` 之 `m_EditorVersionWithRevision`) |
| **IL2CPP 模块** | 已装(ADR-012 双格对拍的前置) |
| **license** | Personal,已激活 |
| **URP 空工程 Play Mode** | 已验通过 |

## 装完后回填(已落,2026-09-22)

- [x] `docs/engine-reference/unity/VERSION.md` —— Project Pinned 行补点版本 + changeset + 验证日期(Engine Version / Editor Revision / Pinned On / Project Pinned / Last Docs Verified 五行已刷)
- [x] `.github/workflows/tests.yml:28` —— `UNITY_VERSION: "6000.3.TBDf1"` 占位符换真值 `"6000.3.24f1"`(原「在 U0 之前 CI 无法跑」注释同步改为已回填口径)
- [x] 门报告 U0a 行 + 本卡勾选 → 解锁 U0(工程根 + ADR-025 六装配 asmdef)与 U1 spike 批的实测面(门报告已于开工序第 1 条后加闭合注,原文未改)

## U0a 之后的队列(承 gate-report 开工序)

```
U0a(本卡)→ U0(src/ 工程根 + Sim / Sim.Contracts / Sim.Codec /
              Gameplay.Presentation / Gameplay.UI / Editor.Tools 六装配,
              种子测试 tests/unit/sim/sim_fixedpoint_test.cs 转绿)
           → U1 spike 批:R-A 手柄焦点桥 · R-B 门 A 引用集 · R-C int64 溢出 UB
              · OQ-1-12 · ADR-023 S1/S3/S4 · 10 的两动作手感原型
```
