---
name: unity-cwd-is-unity-subdir
description: Unity EditMode/PlayMode 下 Directory.GetCurrentDirectory() = 仓库根的 unity/ 子目录(非仓库根),任何 Path.Combine(cwd,"Assets",...) 都是错的
metadata:
  type: project
---

**Unity CLI 跑 EditMode/PlayMode 时 `Directory.GetCurrentDirectory()` = `<repo>/unity`,不是 `<repo>`。**

**Why:** 仓库把 Unity 工程放在 `unity/` 子目录(`CLAUDE.md` §Unity Debugging 亦以 `unity build <project>` 为入口)。
实测铁证(可复算):`unity/Logs/probe.xml` 的 `probe_paths` 夹具 CDATA 里有
`cwd=/home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity` —— 且它同时探了
`<repo>/Assets/...`(False)与 `<repo>/unity/Assets/...` 都不对的原因正是这个 cwd。
另有 `unity/Logs/playmode_v2.log:691` 的 `[007] cwd=.../unity` 佐证。

**How to apply:** 任何住 `unity/Assets/**` 的夹具/门,若要用 `Directory.GetCurrentDirectory()`
拼 `Assets/...` 路径,必须**先上溯一层**(如探 `cwd/Assets` 是否存在,否则取 `cwd/../`),
或者用 `Application.dataPath` 派生。直接 `Path.Combine(cwd,"Assets",...)` ⇒ 目录不存在 ⇒
扫描函数提前 `return` 空列表 ⇒ `Assert.IsEmpty` **恒真**(假绿),且变异测试(MUT)注入违规也照绿 ——
**变异测试本身证伪不了这一类假绿**,因为注入物与扫描面同样够不着。

⚠️ 排查要点:stub 夹具(自带数据)会照过,只有**生产调用点夹具**掉进空跑;
故用 `--filter` 单跑该夹具时,若它「很快通过」且输入文件里的违例没被捉到,优先怀疑 cwd。
