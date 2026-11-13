using System;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// Names of the product. Decided 12/11/2026: on a machine that has the original ClaudeCAD (2D only), the Full
    /// edition replaces it like an upgrade. So everything technical keeps the ClaudeCAD identity: the installer AppId,
    /// the ClaudeCAD.bundle folder, ProductCode / UpgradeCode of PackageContents.xml, the CLAUDECHAT / CC commands,
    /// %LocalAppData%\ClaudeCAD and %AppData%\ClaudeCAD (chats, settings and the Claude sign-in are kept). Only the
    /// name people read changes: DisplayName (installer, Windows app list, palette title, ⚙, self-test report).
    /// </summary>
    public static class ProductInfo
    {
#if BAN_THU_NOI_BO
        /// <summary>Bản thử nội bộ (branch full/ban-thu-noi-bo only): the name says so everywhere people read it.</summary>
        public const string DisplayName = InternalBuild.Label;
#else
        public const string DisplayName = "Agent to AutoCAD Full";
#endif
        /// <summary>Contents\edition.json (bundle/edition.json): tells this edition apart without the update link.</summary>
        public const string Edition = "full";
        public const string Repo = "Tieudao-dot/Agent-to-AutoCAD-Full";

        /// <summary>"0.9.4" for an assembly version 0.9.4.0 (four parts only when the revision is set).</summary>
        public static string ShortVersion(Version v) =>
            v == null ? "" : v.Revision > 0 ? v.ToString(4) : v.ToString(v.Build >= 0 ? 3 : 2);

        /// <summary>"Agent to AutoCAD Full 0.9.4" (the name alone without a version).</summary>
        public static string WithVersion(string version) =>
            string.IsNullOrWhiteSpace(version) ? DisplayName : DisplayName + " " + version.Trim();
    }

    /// <summary>The edition already in the bundle folder an installer is about to overwrite.</summary>
    public enum InstalledEdition
    {
        /// <summary>No ClaudeCAD.bundle folder.</summary>
        None,
        /// <summary>This edition (edition.json says full, or a Full build from before edition.json: its update link).</summary>
        Full,
        /// <summary>The original ClaudeCAD: no edition.json, its update-config.json points to the original repository.</summary>
        Original,
        /// <summary>A bundle folder whose edition cannot be told (no readable edition.json / update-config.json, another edition).</summary>
        Unknown,
    }

    /// <summary>
    /// How an installer tells which edition is on the machine (decided 12/11/2026). installer/ClaudeCAD.iss applies the
    /// same rules in Pascal (DetectEdition, ExistingAppVersion); EditionTests keeps the two in step. The plugin uses it
    /// for the self-test / bug report line. Texts are compared lower case without white space, like the Pascal does.
    /// </summary>
    public static class EditionLogic
    {
        public const string EditionFile = "edition.json";
        public const string UpdateConfigFile = "update-config.json";
        /// <summary>The original repository in the update link: "…/Tieudao-dot/Agent-to-Autocad/releases/…".</summary>
        public const string OriginalRepoMarker = "agent-to-autocad/";
        /// <summary>This repository ("agent-to-autocad-full/" does not contain the original marker: the "/" follows "autocad").</summary>
        public const string FullRepoMarker = "agent-to-autocad-full/";
        public const string FullEditionMarker = "\"edition\":\"full\"";
        public const string EditionKeyMarker = "\"edition\":\"";

        /// <summary>Lower case without spaces, tabs or line breaks ("edition": "full" reads like "edition":"full").</summary>
        public static string Squeeze(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var c in s)
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n') sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        /// <param name="bundleExists">The ClaudeCAD.bundle folder exists.</param>
        /// <param name="editionJson">Contents\edition.json, or null when missing / unreadable.</param>
        /// <param name="updateConfigJson">Contents\update-config.json, or null when missing / unreadable.</param>
        public static InstalledEdition Detect(bool bundleExists, string editionJson, string updateConfigJson)
        {
            if (!bundleExists) return InstalledEdition.None;
            var ed = Squeeze(editionJson);
            if (ed.Contains(EditionKeyMarker))
                return ed.Contains(FullEditionMarker) ? InstalledEdition.Full : InstalledEdition.Unknown;
            var up = Squeeze(updateConfigJson);
            if (up.Contains(OriginalRepoMarker)) return InstalledEdition.Original;
            if (up.Contains(FullRepoMarker)) return InstalledEdition.Full;
            return InstalledEdition.Unknown;
        }

        /// <summary>AppVersion="…" of the ApplicationPackage in PackageContents.xml ("" when missing or not a short version).</summary>
        public static string VersionFromPackageContents(string xml)
        {
            if (string.IsNullOrEmpty(xml)) return "";
            const string key = "AppVersion=\"";
            int p = xml.IndexOf(key, StringComparison.Ordinal);
            if (p < 0) return "";
            var rest = xml.Substring(p + key.Length);
            int q = rest.IndexOf('"');
            return q > 0 && q <= 20 ? rest.Substring(0, q) : "";
        }

        /// <summary>The installer warns before replacing these (the original ClaudeCAD, or a bundle it cannot identify).</summary>
        public static bool NeedsWarning(InstalledEdition e) => e == InstalledEdition.Original || e == InstalledEdition.Unknown;

        /// <summary>One word for the self-test / bug report line.</summary>
        public static string Label(InstalledEdition e)
        {
            switch (e)
            {
                case InstalledEdition.Full: return "full";
                case InstalledEdition.Original: return "original";
                case InstalledEdition.None: return "none";
                default: return "unknown";
            }
        }
    }
}
