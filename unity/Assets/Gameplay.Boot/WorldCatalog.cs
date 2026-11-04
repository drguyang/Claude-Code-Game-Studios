// 多世界存档的**目录与索引**(ADR-040 D1 / D2 / D3 / D7 的兑现件 · 2026-11-04)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 它在管什么
// ─────────────────────────────────────────────────────────────────────────────
//   <savesRoot>/worlds.index        索引(每个世界的最后游玩时刻)
//   <savesRoot>/w1/save_exit.bin    世界 1 —— **世界内的槽位语义一字未改**
//   <savesRoot>/w1/save_auto.bin
//   <savesRoot>/w1/save_3.bin…
//   <savesRoot>/w2/…
//
// 每个世界目录就是一个 `SaveService` 的 `basePath`(`CompositionRoot.Assemble(saveBasePath:)`
// 早已收这个参数)⇒ 存档体、槽位台账、折叠谓词、配置版本段、载荷段**全部零改动**。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么本类住 `Gameplay.Boot` 而不是 `Sim.Codec`(ADR-040 D8 的裁定依据)
// ─────────────────────────────────────────────────────────────────────────────
// `SaveWritePathGate` 只允许 **`SaveService` 一个类**在 `Sim.Codec` 里碰写盘 API,
// 且方法级只放行 `WriteAtomic`;`Directory.CreateDirectory` 也只有「类级」豁免。
// 而本类需要建目录 / 搬文件 / 写索引 ⇒ 放进 `Sim.Codec` 必撞门。
// ⇒ 落 `Gameplay.Boot`。索引是**元数据,不是存档体**,因此不受「存档体只有一个写入口」约束;
// 但它自己仍然做**原子写**(临时文件 → 覆盖),不让「写一半的索引」被读进来。
//
// ─────────────────────────────────────────────────────────────────────────────
// 索引的形状与失败方向
// ─────────────────────────────────────────────────────────────────────────────
// 二进制,每项 `<i32 worldSeq><i64 lastPlayedUtcTicks>`(小端),按 worldSeq 升序。
// ⚠️ **不用 JSON**:ADR-014 明定玩家构建零 JSON 解析器。
// ⚠️ **目录是权威,索引只是时刻表**:索引缺失 / 损坏 / 与目录不符 ⇒ 以目录为准重建,
//    缺时刻的世界按 0 处理(排在最后)。不静默丢世界 —— 世界目录本身还在。
//
// ─────────────────────────────────────────────────────────────────────────────
// 一次性迁移(ADR-040 D7)
// ─────────────────────────────────────────────────────────────────────────────
// 多世界形态之前,存档直接躺在 `<savesRoot>/` 下。首次以本形态启动 ⇒ 把那些文件整体搬进 `w1/`,
// **字节零改动**(不是存档格式迁移:不走 `ISaveMigration`、不改 `SaveVersion`)。
// ⚠️ 判定用「根目录里有没有 `save_*.bin` / `save_exit.bin` / `session_incomplete.flag`」,
//    不靠版本号 —— 存档头里没有「布局版本」这种东西,而且不该为它加一个。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>一个世界的目录条目(目录名即身份;没有别的身份字段)。</summary>
    public readonly struct WorldEntry
    {
        /// <summary>世界序号(≥ 1)。目录名 = <c>w&lt;Seq&gt;</c>。</summary>
        public readonly int Seq;

        /// <summary>该世界的存档目录(可直接作 `SaveService` 的 `basePath`)。</summary>
        public readonly string Directory;

        /// <summary>最后游玩时刻(UTC ticks;0 = 索引里没有,未知)。</summary>
        public readonly long LastPlayedUtcTicks;

        /// <summary>构造。</summary>
        public WorldEntry(int seq, string directory, long lastPlayedUtcTicks)
        {
            Seq = seq;
            Directory = directory;
            LastPlayedUtcTicks = lastPlayedUtcTicks;
        }
    }

    /// <summary>
    /// 多世界的目录与索引(ADR-040)。**只管目录与「最后游玩时刻」**,不碰存档体。
    /// </summary>
    public sealed class WorldCatalog
    {
        /// <summary>世界目录名前缀(目录名 = 前缀 + 序号)。</summary>
        public const string WorldDirPrefix = "w";

        /// <summary>索引文件名(与 <c>save_*.bin</c> 命名空间不相交)。</summary>
        public const string IndexFileName = "worlds.index";

        /// <summary>索引每项的字节数:<c>i32 seq + i64 ticks</c>。</summary>
        public const int IndexRecordSize = 12;

        private readonly string _root;
        private readonly Func<long> _clock;

        /// <summary>构造。</summary>
        /// <param name="root">存档根目录(其下是 <c>w1/</c>、<c>w2/</c>…)。</param>
        /// <param name="clock">「现在」的来源(UTC ticks);<c>null</c> ⇒ <see cref="DateTime.UtcNow"/>。
        /// <para>⚠️ 注入缝**只给夹具**:本量只用于**选择面**(哪个世界算「最近」),不进 sim、
        /// 不进存档体(ADR-040 D4)。</para></param>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> 为 null / 空白。</exception>
        public WorldCatalog(string root, Func<long> clock = null)
        {
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentNullException(nameof(root),
                    "存档根目录为 null / 空白 —— 未装配存档时不要构造本类(与 `saveBasePath == null` 同口径)");
            _root = root;
            _clock = clock ?? (() => DateTime.UtcNow.Ticks);
        }

        /// <summary>存档根目录。</summary>
        public string Root => _root;

        /// <summary>某个世界的目录路径(不保证存在)。</summary>
        public string DirectoryOf(int seq) => Path.Combine(_root, WorldDirPrefix + seq);

        // ══════════════════════════════════════════════════════════════════
        // 一次性迁移(ADR-040 D7)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 把「多世界形态之前」直接躺在根目录下的存档整体搬进 <c>w1/</c>。
        /// <para>幂等:根目录已无存档文件 ⇒ 什么都不做。**字节零改动**(纯搬移)。</para>
        /// </summary>
        /// <returns>搬了多少个文件(0 = 无需迁移 / 已经迁移过)。</returns>
        /// <exception cref="IOException">搬移失败 —— **向上抛**,由启动序按 E-13 处理。
        /// 静默吞会让玩家存档留在一个读不到的位置。</exception>
        public int MigrateLegacy()
        {
            if (!Directory.Exists(_root)) return 0;

            var legacy = new List<string>();
            foreach (string file in Directory.GetFiles(_root))
            {
                string name = Path.GetFileName(file);
                if (IsLegacySaveFile(name)) legacy.Add(file);
            }
            if (legacy.Count == 0) return 0;

            string target = DirectoryOf(1);
            Directory.CreateDirectory(target);

            foreach (string file in legacy)
            {
                string dest = Path.Combine(target, Path.GetFileName(file));
                if (File.Exists(dest))
                {
                    // 目标已存在(半迁移过的现场)⇒ 不覆盖,把来源改成带后缀的名字留着,
                    // 由人判读。**不删任何东西**。
                    dest = dest + ".migrated-" + DateTime.UtcNow.Ticks.ToString();
                }
                File.Move(file, dest);
            }
            return legacy.Count;
        }

        /// <summary>根目录里的这个文件名是否属于「旧形态的存档」。</summary>
        private static bool IsLegacySaveFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            if (fileName == "session_incomplete.flag") return true;
            if (fileName.EndsWith(".bak", StringComparison.Ordinal)
                || fileName.EndsWith(".tmp", StringComparison.Ordinal))
            {
                // `save_3.bin.bak` 这类:剥掉后缀再判一次
                string stripped = fileName;
                while (stripped.EndsWith(".bak", StringComparison.Ordinal)
                       || stripped.EndsWith(".tmp", StringComparison.Ordinal))
                    stripped = Path.GetFileNameWithoutExtension(stripped);
                return IsLegacySaveFile(stripped);
            }
            if (fileName == "save_exit.bin") return true;
            return fileName.StartsWith("save_", StringComparison.Ordinal)
                && fileName.EndsWith(".bin", StringComparison.Ordinal);
        }

        // ══════════════════════════════════════════════════════════════════
        // 列表 / 创建 / 最近
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 列出全部世界,**按最后游玩时刻降序**(最近在前;未知时刻的排最后,序号小的在前)。
        /// <para>⚠️ **目录是权威**:索引里有、目录不在的条目**不列**;目录在、索引里没有的**照列**
        /// (时刻 = 0)。索引损坏不会让世界消失。</para>
        /// </summary>
        public IReadOnlyList<WorldEntry> ListWorlds()
        {
            var ticks = ReadIndex();
            var result = new List<WorldEntry>();
            if (!Directory.Exists(_root)) return result;

            foreach (string dir in Directory.GetDirectories(_root))
            {
                if (!TryParseWorldDir(Path.GetFileName(dir), out int seq)) continue;
                ticks.TryGetValue(seq, out long t);
                result.Add(new WorldEntry(seq, dir, t));
            }

            result.Sort((a, b) =>
            {
                int byTick = b.LastPlayedUtcTicks.CompareTo(a.LastPlayedUtcTicks);
                return byTick != 0 ? byTick : a.Seq.CompareTo(b.Seq);
            });
            return result;
        }

        /// <summary>取最近游玩的世界(有则 <c>true</c>)。</summary>
        public bool TryGetMostRecent(out WorldEntry entry)
        {
            IReadOnlyList<WorldEntry> all = ListWorlds();
            if (all.Count == 0)
            {
                entry = default;
                return false;
            }
            entry = all[0];
            return true;
        }

        /// <summary>
        /// 新建一个世界目录,分配下一个序号。
        /// <para>⚠️ 只建目录 —— 第一份存档由启动序的 `FirstSessionSave` 写(它会看到空目录)。</para>
        /// </summary>
        public WorldEntry CreateWorld()
        {
            Directory.CreateDirectory(_root);

            int next = 1;
            foreach (string dir in Directory.GetDirectories(_root))
                if (TryParseWorldDir(Path.GetFileName(dir), out int seq) && seq >= next)
                    next = seq + 1;

            string path = DirectoryOf(next);
            Directory.CreateDirectory(path);
            Touch(next, _clock());
            return new WorldEntry(next, path, _clock());
        }

        /// <summary>
        /// 记「这个世界的最后游玩时刻」。
        /// <para>⚠️ 只用于**选择面**(ADR-040 D4):不进 sim、不进存档体。写失败**不抛** caller
        /// 的存档流程 —— 但会向上抛,由调用方决定是否只记日志(装配层的选择:不阻塞玩法)。</para>
        /// </summary>
        public void Touch(int seq, long utcTicks)
        {
            var index = ReadIndex();
            index[seq] = utcTicks;
            WriteIndex(index);
        }

        // ══════════════════════════════════════════════════════════════════
        // 索引读写(二进制 · 原子写)
        // ══════════════════════════════════════════════════════════════════

        private string IndexPath => Path.Combine(_root, IndexFileName);

        private Dictionary<int, long> ReadIndex()
        {
            var map = new Dictionary<int, long>();
            try
            {
                if (!File.Exists(IndexPath)) return map;
                byte[] bytes = File.ReadAllBytes(IndexPath);
                if (bytes.Length % IndexRecordSize != 0) return map;   // 损坏 ⇒ 当空,以目录为准

                for (int at = 0; at < bytes.Length; at += IndexRecordSize)
                {
                    int seq = BitConverter.ToInt32(bytes, at);
                    long ticks = BitConverter.ToInt64(bytes, at + 4);
                    if (seq <= 0) continue;
                    map[seq] = ticks;
                }
            }
            catch (Exception)
            {
                // 读不出来 = 没有时刻表。**世界目录还在**,故不是丢世界(见类头注)。
                return new Dictionary<int, long>();
            }
            return map;
        }

        private void WriteIndex(Dictionary<int, long> map)
        {
            Directory.CreateDirectory(_root);

            var seqs = new List<int>(map.Keys);
            seqs.Sort();

            var bytes = new byte[seqs.Count * IndexRecordSize];
            int at = 0;
            foreach (int seq in seqs)
            {
                byte[] rec = BitConverter.GetBytes(seq);
                Array.Copy(rec, 0, bytes, at, 4);
                rec = BitConverter.GetBytes(map[seq]);
                Array.Copy(rec, 0, bytes, at + 4, 8);
                at += IndexRecordSize;
            }

            string tmp = IndexPath + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            // 覆盖式落盘:索引是元数据,后写胜最新(不像存档体那样保留历史)。
            if (File.Exists(IndexPath)) File.Delete(IndexPath);
            File.Move(tmp, IndexPath);
        }

        /// <summary>从目录名解析世界序号(<c>w12</c> ⇒ 12)。</summary>
        public static bool TryParseWorldDir(string name, out int seq)
        {
            seq = 0;
            if (string.IsNullOrEmpty(name)) return false;
            if (!name.StartsWith(WorldDirPrefix, StringComparison.Ordinal)) return false;
            if (name.Length <= WorldDirPrefix.Length) return false;

            string digits = name.Substring(WorldDirPrefix.Length);
            if (!int.TryParse(digits, System.Globalization.NumberStyles.None,
                               System.Globalization.CultureInfo.InvariantCulture, out int parsed))
                return false;
            if (parsed <= 0) return false;
            seq = parsed;
            return true;
        }
    }
}
