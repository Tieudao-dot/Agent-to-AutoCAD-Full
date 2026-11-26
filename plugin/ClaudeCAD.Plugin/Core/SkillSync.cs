using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Nâng cấp đợt H1 (docs/ban-giao/nang-cap-h1.md): the shipped skills on a machine that is upgraded. The installer (and
    /// the in-app update) put the skills of the new version in Contents\skills-ban-moi; on its first start the plugin brings
    /// them into Contents\skills: a file the user never changed is replaced by the new one, a file the user changed is kept
    /// and the new one is put next to it (SKILL.ban-moi.md), and the chat says so once. Nothing of the user's is ever deleted.
    /// "Never changed" = its content is the one this plugin installed (recorded in .ban-da-cai.json) or one of the skill
    /// versions ever published (KnownShipped, from the git history: v0.9.3 on main, the internal test builds…).
    /// </summary>
    public static class SkillSync
    {
        /// <summary>The folder of the new version's skills, next to Contents\skills.</summary>
        public const string StagedFolder = "skills-ban-moi";
        /// <summary>The record of what was installed, in the skills folder (kept on uninstall like the skills).</summary>
        public const string ManifestFile = ".ban-da-cai.json";
        /// <summary>The new version next to a file the user changed: SKILL.md → SKILL.ban-moi.md.</summary>
        public const string SideSuffix = ".ban-moi";

        /// <summary>SHA-256 of the content with line ends as LF and no UTF-8 BOM (a copy through Windows / git is the same file).</summary>
        public static string Hash(byte[] content)
        {
            var s = Encoding.UTF8.GetString(content ?? new byte[0]);
            if (s.Length > 0 && s[0] == '﻿') s = s.Substring(1);
            s = s.Replace("\r", "");
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(s)).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }

        public static string SidePath(string path)
        {
            var dir = Path.GetDirectoryName(path) ?? "";
            return Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + SideSuffix + Path.GetExtension(path));
        }

        public sealed class Manifest
        {
            /// <summary>Relative path (with '/') → hash of the file this plugin put there.</summary>
            public Dictionary<string, string> Installed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            /// <summary>Relative path → hash of the new version already put next to the user's file (said once).</summary>
            public Dictionary<string, string> Noticed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            /// <summary>Skills to name in the chat at its next opening (then cleared).</summary>
            public List<string> Pending = new List<string>();
        }

        public static Manifest ReadManifest(string path)
        {
            var m = new Manifest();
            try
            {
                if (!File.Exists(path) || !(JsonCodec.Parse(File.ReadAllText(path)) is Dictionary<string, object> d)) return m;
                void Map(string key, Dictionary<string, string> into)
                {
                    if (d.TryGetValue(key, out var v) && v is Dictionary<string, object> o)
                        foreach (var kv in o) if (kv.Value is string s) into[kv.Key] = s;
                }
                Map("installed", m.Installed);
                Map("noticed", m.Noticed);
                if (d.TryGetValue("pending", out var p) && p is List<object> l) m.Pending.AddRange(l.OfType<string>());
            }
            catch (Exception) { /* a broken record: start again, nothing is deleted anyway */ }
            return m;
        }

        public static void WriteManifest(string path, Manifest m)
        {
            var d = new Dictionary<string, object>
            {
                ["installed"] = m.Installed.ToDictionary(kv => kv.Key, kv => (object)kv.Value),
                ["noticed"] = m.Noticed.ToDictionary(kv => kv.Key, kv => (object)kv.Value),
                ["pending"] = m.Pending.Distinct(StringComparer.OrdinalIgnoreCase).Cast<object>().ToList(),
            };
            File.WriteAllText(path, JsonCodec.Serialize(d), new UTF8Encoding(false));
        }

        public sealed class Result
        {
            public List<string> Added = new List<string>(), Replaced = new List<string>(), KeptWithNew = new List<string>();
            /// <summary>The skills to name in the chat now (folder names, e.g. "ve-kien-truc").</summary>
            public List<string> Notice = new List<string>();
        }

        /// <summary>The skill a file belongs to: its first folder ("ve-kien-truc/SKILL.md" → "ve-kien-truc"), else the file name.</summary>
        public static string SkillOf(string rel)
        {
            var parts = rel.Replace('\\', '/').Split('/');
            return parts.Length > 1 ? parts[0] : Path.GetFileNameWithoutExtension(rel);
        }

        /// <summary>
        /// Brings the staged skills into the skills folder (see the class). known = hashes of every version ever shipped.
        /// Returns what was done; the manifest is written in the skills folder.
        /// </summary>
        public static Result Sync(string stagedDir, string skillsDir, ISet<string> known)
        {
            var r = new Result();
            if (string.IsNullOrEmpty(stagedDir) || !Directory.Exists(stagedDir) || string.IsNullOrEmpty(skillsDir)) return r;
            Directory.CreateDirectory(skillsDir);
            string mpath = Path.Combine(skillsDir, ManifestFile);
            var m = ReadManifest(mpath);
            foreach (var src in Directory.GetFiles(stagedDir, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                string rel = src.Substring(stagedDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (rel.Length == 0 || Path.GetFileName(rel).StartsWith(".", StringComparison.Ordinal)) continue;
                if (Path.GetFileNameWithoutExtension(rel).EndsWith(SideSuffix, StringComparison.OrdinalIgnoreCase)) continue;
                var bytes = File.ReadAllBytes(src);
                string newH = Hash(bytes);
                string dst = Path.Combine(skillsDir, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(dst))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    File.WriteAllBytes(dst, bytes);
                    m.Installed[rel] = newH;
                    r.Added.Add(rel);
                    continue;
                }
                string curH = Hash(File.ReadAllBytes(dst));
                if (curH == newH) { m.Installed[rel] = newH; continue; }
                bool untouched = (m.Installed.TryGetValue(rel, out var mine) && mine == curH) || (known != null && known.Contains(curH));
                if (untouched)
                {
                    File.WriteAllBytes(dst, bytes);
                    m.Installed[rel] = newH;
                    r.Replaced.Add(rel);
                    continue;
                }
                // the user's own version: kept as it is, the new one next to it, said once per new version
                string side = SidePath(dst);
                if (!File.Exists(side) || Hash(File.ReadAllBytes(side)) != newH) File.WriteAllBytes(side, bytes);
                r.KeptWithNew.Add(rel);
                if (!(m.Noticed.TryGetValue(rel, out var said) && said == newH))
                {
                    m.Noticed[rel] = newH;
                    m.Pending.Add(SkillOf(rel));
                }
            }
            r.Notice = m.Pending.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            m.Pending.Clear(); // the caller says it now
            try { WriteManifest(mpath, m); } catch (Exception) { /* read-only folder: the same work is redone next time, nothing lost */ }
            return r;
        }

        /// <summary>The line of the chat naming the skills whose new version waits next to the user's own.</summary>
        public static string NoticeText(IList<string> skills) => skills == null || skills.Count == 0 ? null :
            $"Skill {string.Join(", ", skills)} có bản mới. Bản cậu đã sửa được giữ nguyên; bản mới nằm cạnh nó (tên có \"{SideSuffix}\", vd SKILL{SideSuffix}.md) để cậu tự gộp khi muốn.";

        /// <summary>Hashes (Hash above) of every skill file version in the repository history (main = v0.9.3, test builds, the Full branches).</summary>
        public static readonly HashSet<string> KnownShipped = new HashSet<string>(StringComparer.Ordinal)
        {
            // generated 26/11/2026: every version of every file under skills/ in the git history (installer/skill-hashes.sh)
            "0e82bdefc700c962ea187b1f53eea1827856018c71dc0b9e6b39bfbec73aa744",
            "104c605314364b77030305ad786d44fcd33ce07a0cf775f486825951392d6f70",
            "11ffc34cc6a847ef6e1849f3e0826cd679031c7a4a4849ea452c6ddf99e24d03",
            "17433650840204eadcaf0d98cb9f4a66b17fb85ac037f48a429eb4fdc199d72a",
            "1f5341c265085b947b36f1d357738fb4240e54411395a4fbd1364323d76938dc",
            "202e249d60ee0877c5238d409f08efb3b1ebbc8e3524d57f253e19b325764146",
            "22884ae8358e1f7ad3fadcd35878fae4de33154e768a66aec20c83364f7b2419",
            "254edd51be51589dee4622ffb82823cdb26e34ed8ff0dab3b251fb5b2a86ed8b",
            "291f20735072468a2e9752c27691d7f93c3448fe2046a4d1c902b8617d59fb60",
            "4009a6845b67eb7199313f6133c2e4b7aa33d05eb58abe7c2ededb3a4ca9a8bc",
            "524cff7011f13b7c20c9a836db1925db3b66647a789496739faf6a5d6732eee4",
            "5ad51db33dbe4df9706847ebdeba99f9fd709e73a73a219be9873e79984c7ae6",
            "5b0551e503db1a6fa7558ea46652024628490d59d10181e9d50f4b21969a2878",
            "5e8b329b0c2c18b45c16171a38c33ec1c44165177d85028d63d0daeb2bd57d66",
            "6005a411de74d5d2f587ffa2d767d5fb21f4ba39d451da1c921048357bc68b50",
            "623a4e6f8386471759fff682f1a97825fa0301dc4915a9fe3f3cab49f69c828e",
            "6284ac2fedf26e247b4971c904f272cb18130e1f647930a7666cfbe88042d92d",
            "6acff0108959464060b5104369e32c7321386463358c5b596e7a9b196906bff8",
            "6dc1cc430a59689a89d43f8fad19fec3e501d0fd968f0a4a5178dbf83632c647",
            "6ee6d27576e962e52c6e54e729a9131c60cd08eee3a5d0935147afc82df96f2b",
            "74d00cbf567f148ed36587534edb3dc5b0865fd4df75973d015c58b82d49b4ef",
            "78dda1dc22f29276a9c23e2b97cfc8c2be0ec62b2d3800fe492ef69a8dfda031",
            "7bc0bda89e87b818fca6cc946a22a5bdf9607533c1c63192b681b69563ca3d39",
            "7e59aab79b1dc50ed439187d830a569162f96c912d28a9c0c986500c29eaae5d",
            "817c6adc589e5e53a29526397092d1422de803f43d3ce6593559b9d45875ed62",
            "8b9b2ded6f529f9954c23dc8ea847e0a592d0015088da8e501a40e164815312f",
            "8f2cbaa0a98c0d65a52117a421e795e5634bcd7b22a8cd6e736c1fbe0c06dc16",
            "90ea3789013a515b442cb9de0e1d2bed17f0b489d56ff642a67875fbd28f46b4",
            "949e11cc9b7c55fd0aca19eb872e20d5d73f9be61de42b682e3f2acf6b3d7103",
            "9fff1d097f8e398d898b2588027b3a862985dd3f7400057bf64528f478e8fb45",
            "a66aaed0ac108e156af2e072d30fc34d5d273caee299aa6daae96d0916d7c950",
            "a7cd0df72b9b3bfb7de0ed4ac60916e5cd010a1669306ed2efe0464892508b3d",
            "a8ff2970b559df9c3ae85785c955ec1a2c391a87c7dab0e71e34486b7c4c0c59",
            "aa8c799b48c8733978dd7b7f61bb8405787491438dda0db8cf64b8fd03602dfc",
            "b15e296f2dba1144b9010d52cb27d4db7a616ebfb49b850875f083a75b1e64f3",
            "b2f8f95d1456d82ed1fa2bf0e56d1f3e0f5d80d99db7f2e3b269ef246101e79f",
            "b6b0e7a85227f684f6cb2830e4709566b71cd50cc7d0bb05e8abca25f5a3ad86",
            "bc4957344339724c37700a5435e349dd75744017f4a2951584f1d631d922c3c8",
            "be8924d66faa2bf2e593ed89428362ce130b11c63dea51655f93ec827faba417",
            "c15ecb9b7e40ca59c04ab6d9dbb8dfce5dd7a4af40ed36907b5f023dcb176528",
            "d93f0951890d8da4fe9c08eabcc5e226bc836fecd68c98bc8de40f118d8acf9b",
            "dd6d1f1d1d1db8d386cdf4a7cc8db5a1602d7dc5ef7ca0cccdaa7badad3df3a1",
            "e0343f66465742f7c0c05d3fdc8215f8b37c3c26cde3e00f275bb097754038c0",
            "ef32866cec9d9ad8e320e1db2f54bb6e43c4dd0557df2b273ab108f6a38842d7",
            "f093618bea69f5c5827f2987fca8a65702b4a084d3286483bb972611d9c7c395",
            "f3c9b91b9db95dc41039be6c2f335d9bd82d008044ed882306ae80e0a16a0ae4",
            "f5ccc76746accb2a4456c267b4c7660533f149e580d98ebb6e80666de252523d",
        };
    }
}
