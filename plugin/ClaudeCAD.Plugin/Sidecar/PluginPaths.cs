using System;
using System.IO;
using System.Linq;

namespace ClaudeCAD.Plugin.Sidecar
{
    /// <summary>
    /// Resolves where the sidecar, the skills and node.exe live.
    ///
    /// Order of precedence for each path:
    ///   1. environment variable (CLAUDECAD_AGENT_DIR / CLAUDECAD_SKILLS_DIR / CLAUDECAD_NODE)
    ///   2. next to the plugin dll (bundle layout: Contents\agent, Contents\skills, Contents\node\node.exe)
    ///   3. walking up from the dll folder (dev layout: repo\agent, repo\skills)
    ///   4. node only: PATH, then %ProgramFiles%\nodejs\node.exe
    /// </summary>
    public static class PluginPaths
    {
        public static string DllDirectory =>
            Path.GetDirectoryName(typeof(PluginPaths).Assembly.Location) ?? Environment.CurrentDirectory;

        public static string AgentDirectory => Resolve("CLAUDECAD_AGENT_DIR", "agent", d => File.Exists(Path.Combine(d, "dist", "index.js")));

        public static string SkillsDirectory => Resolve("CLAUDECAD_SKILLS_DIR", "skills", Directory.Exists);

        /// <summary>Shipped quick prompts (templates\templates.json); null if absent.</summary>
        public static string TemplatesFile
        {
            get
            {
                var dir = Resolve("CLAUDECAD_TEMPLATES_DIR", "templates", d => File.Exists(Path.Combine(d, "templates.json")));
                return dir == null ? null : Path.Combine(dir, "templates.json");
            }
        }

        public static string AgentEntry
        {
            get
            {
                var dir = AgentDirectory;
                return dir == null ? null : Path.Combine(dir, "dist", "index.js");
            }
        }

        private static string Resolve(string envVar, string name, Func<string, bool> ok)
        {
            var env = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(env) && ok(env)) return Path.GetFullPath(env);

            var dir = new DirectoryInfo(DllDirectory);
            for (int i = 0; dir != null && i < 8; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, name);
                if (ok(candidate)) return candidate;
            }
            return null;
        }

        public static string NodeExecutable
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("CLAUDECAD_NODE");
                if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;

                var bundled = Path.Combine(DllDirectory, "node", "node.exe");
                if (File.Exists(bundled)) return bundled;

                var path = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (var p in path.Split(Path.PathSeparator).Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    try
                    {
                        var exe = Path.Combine(p.Trim().Trim('"'), "node.exe");
                        if (File.Exists(exe)) return exe;
                    }
                    catch (ArgumentException) { }
                }

                foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                             Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Programs" })
                {
                    if (string.IsNullOrEmpty(root)) continue;
                    var exe = Path.Combine(root, "nodejs", "node.exe");
                    if (File.Exists(exe)) return exe;
                }
                return null;
            }
        }
    }
}
