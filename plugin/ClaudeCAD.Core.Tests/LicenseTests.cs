using System.Collections.Generic;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    /// <summary>v0.9.4 per-machine activation: machine code, key normalisation, licence signature.</summary>
    public class LicenseTests
    {
        // throw-away test keypair (the private half is not kept anywhere)
        private const string TestModulus = "0V+Bpb3lCQeXMrGOx3x2fO7bBX0f3KtWsAKLoP6PlyAqcbFIQFyg1UAbBpHkmvbTs8irFCMgY7juPNB+JfZhBsMc2EjvCfgkiebMCBjzCbBAV/O0SfmYWAiWeXccWfOsu1Hf/pgSrF7LVJbG3WgrCr0MZ+B2/oO2951gleOZ9HIu1BEFHadIDjaGJowMlaauC+bKR3JxmTzfjSeAG8uW1ZGqifyS33SCBUX5C4vYibTp8RKkXpcT3M2vZW4gzDnF9RmQcwoX5E+8cVIuoVdztqz0TSGyXkk0zJ2hsI1uCHpzwhDcbutw3gGkYf5fPPxdkVYk53I7bHBFd4hL07cVDw==";
        private const string TestLicense = "CCL1|CC-1A2B-3C4D-5E6F|2026-11-09|lpDMHR2fKXtrS04nhNFzmaXhltPp88AQjwonYTLn6L2WwZ4CjFSBLM71HiVPLJd_zRzlJhpAsqbiAQGkS6lyuUwkSPWE2TnNk5BlD0s-m6sl1GdaPL4oiPDIdyhIHPBIklB9giunIV3S0Gvb5O1RAPsW5wA-HTdALimiy5_eB-uamNxuK2rmER9WJEajn5KJZ41_xr6LjTkVV--v3ug75g3-HY7_kHB-E5i7rmAf3FtId17Xy0jLZNP6edjzp94lCJyl23cNH0rJjjAysiHUwwag0k_Ckdl2uwcw--HpVjH4CqV-NTFijBAnnsXsR_fhGmXiD4r2C7ccpLyaKqNfOg";
        // signed with the real activation-service key for a machine that does not exist: the embedded public key matches it
        private const string ProductionLicense = "CCL1|CC-0000-0000-0000|2026-11-11|LP3BJ0ySIW3mmzE5OvRSZqb0bBeYw0tJy3Z6sWs0dKW5BIkCHYojZCm5ICvF_zJUJzlJDlwNHdObed-PdAEDUn4EMivnVjWh0c6F-zJRdIq7607vvBgREd5U1exf2NSpAvHXGyn7EHjU_daAsXELIrZVpj7hKWzS--1bIMYJ53HAc_z3DmdevJCtby-vnosadRCNccl3YfvT08Wf9en3KKM9O-nKTg_VsNGReyOildTdGjhAhaFdS6AeV_-fjjVBiTpMJjrPYAjOxTqYfL6rzHUWuoNEffhQLJxPAR4rY0p7DRW460bUpr39a8IVwz24BLNXm3FiO5gFtvU3oFP6AQ";
        private const string ProductionStatus = "CCS1|CC-0000-0000-0000|offline|2026-11-11|oI_WBQeJyLxyXuHVlt8neLOuhlAucTwf_My9-dLre8ktLfxHubJSiw0zAoGo0MxSzfqoHw-AkFNMZS1dfoxv8s6SYStBx79zmc-udl30PVRJesjHPY1O3t8TGJPBqvusiAHHwrI72WauTCCEfaYhnPnXsj8RBFgygkfEg0LEagaV_NeRTsO6LJTlA57EYng1qSL3vnfhnophGOowlmXmgya3j0v6xizNizWJsY3Hjzxs40za_81Kn61G6D-ZJ1BV0hWKv9Yr_96WJxx_EQwPWM_2PL6oUwL5Z2QwdXmpp6DVLURIziexhzkjMBoDolalPeumQf0KGpuqCer67qMFqA";

        [Fact]
        public void MachineCodeIsStableAndMatchesTheInstallerFormula()
        {
            // SHA-256("ClaudeCAD|" + lower-case MachineGuid), first 12 hex digits — the same formula as ClaudeCAD.iss
            Assert.Equal("CC-3B28-85F3-67B7", LicenseLogic.MachineCode("8D3C1A52-6E4B-4F0A-9C27-3B5E7A1D9F40"));
            Assert.Equal("CC-3B28-85F3-67B7", LicenseLogic.MachineCode(" 8d3c1a52-6e4b-4f0a-9c27-3b5e7a1d9f40 "));
            Assert.Equal("CC-20C7-AAE7-2F7D", LicenseLogic.MachineCode(null, "LAPTOP-TUAN"));
            Assert.Matches("^CC-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}$", LicenseLogic.MachineCode("x"));
        }

        [Fact]
        public void KeysAreNormalised()
        {
            Assert.Equal("ABCD0123456789XY", LicenseLogic.NormalizeKey(" abcd-O123-4567 89xy "));
            Assert.Equal("1111", LicenseLogic.NormalizeKey("iIlL"));
            Assert.True(LicenseLogic.IsShortKey("7K2M-9QXR-4TBN-H8WZ"));
            Assert.False(LicenseLogic.IsShortKey("7K2M-9QXR-4TBN"));
            Assert.False(LicenseLogic.IsShortKey("7K2M-9QXR-4TBN-H8WU")); // U is not in the alphabet
            Assert.True(LicenseLogic.IsLicenseText(" " + TestLicense));
            Assert.False(LicenseLogic.IsLicenseText("7K2M-9QXR-4TBN-H8WZ"));
        }

        [Fact]
        public void ValidLicenceOfThisMachinePasses()
        {
            var r = LicenseLogic.Verify(TestLicense, "CC-1A2B-3C4D-5E6F", TestModulus, "AQAB");
            Assert.True(r.Ok, r.Reason);
            Assert.Equal("2026-11-09", r.Issued);
            Assert.True(LicenseLogic.Verify(TestLicense + "\r\n", "cc-1a2b-3c4d-5e6f", TestModulus, "AQAB").Ok);
        }

        [Fact]
        public void EmbeddedPublicKeyMatchesTheActivationService()
        {
            Assert.True(LicenseLogic.Verify(ProductionLicense, "CC-0000-0000-0000").Ok);
            Assert.False(LicenseLogic.Verify(TestLicense, "CC-1A2B-3C4D-5E6F").Ok); // signed by another key
            // the same production key signs the device statuses (CCS1)
            var st = LicenseLogic.VerifyStatus(ProductionStatus, "CC-0000-0000-0000", new System.DateTime(2026, 11, 11, 12, 0, 0, System.DateTimeKind.Utc));
            Assert.True(st.Ok, st.Reason);
            Assert.True(st.Fresh); // "offline" never goes stale
        }

        [Fact]
        public void OtherMachinesTamperingAndJunkFail()
        {
            var other = LicenseLogic.Verify(TestLicense, "CC-9999-9999-9999", TestModulus, "AQAB");
            Assert.False(other.Ok);
            Assert.Contains("máy khác", other.Reason);
            // same signature, machine changed in the text
            var forged = TestLicense.Replace("CC-1A2B-3C4D-5E6F", "CC-9999-9999-9999");
            Assert.False(LicenseLogic.Verify(forged, "CC-9999-9999-9999", TestModulus, "AQAB").Ok);
            // date changed
            Assert.False(LicenseLogic.Verify(TestLicense.Replace("2026-11-09", "2099-01-01"), "CC-1A2B-3C4D-5E6F", TestModulus, "AQAB").Ok);
            // one character of the signature changed
            var sig = TestLicense.Substring(TestLicense.LastIndexOf('|') + 1);
            var bad = TestLicense.Substring(0, TestLicense.Length - sig.Length) + (sig[0] == 'A' ? 'B' : 'A') + sig.Substring(1);
            Assert.False(LicenseLogic.Verify(bad, "CC-1A2B-3C4D-5E6F", TestModulus, "AQAB").Ok);
            Assert.False(LicenseLogic.Verify("", "CC-1A2B-3C4D-5E6F", TestModulus, "AQAB").Ok);
            Assert.False(LicenseLogic.Verify(null, "CC-1A2B-3C4D-5E6F", TestModulus, "AQAB").Ok);
            Assert.False(LicenseLogic.Verify("CCL1|CC-1A2B-3C4D-5E6F|2026-11-09|%%%", "CC-1A2B-3C4D-5E6F", TestModulus, "AQAB").Ok);
            Assert.False(LicenseLogic.Verify("CCL2|CC-1A2B-3C4D-5E6F|2026-11-09|" + sig, "CC-1A2B-3C4D-5E6F", TestModulus, "AQAB").Ok);
        }

        // throw-away keypair for the signed device statuses (CCS1) — the private half is not kept anywhere
        private const string StatusModulus = "95gAmtgE2sCqPsfBhlrm4aSowRpEq6KUfM6iOkfuPB5DfUheAfN4O7nXfcnq+RLaFyjwcihBoN1FAAkK/Ezfuc1oeMRe4HcZVu4KJ5UNCVO88S0tgJSjHTTa+TU7n44hQBVYEBGl29hYCVPZP1sbsq/Tbzw5K3D208iagjtAVYeCd29dNcAH425rLPYCF5prI59SHg4T08DL56FhHAkvFxCBonj+p0XufqxOM5THBphAlEvL7mTmRlJlz3NaeUiv6VPof147S/pBTdBiT35WRXk2nhREx4yFoYRUOrbzdG+1lGTxDW7wg1fS0shLrnREQ7IbUam9V+NnrIjT1yXoaQ==";
        private const string StatusActive = "CCS1|CC-1A2B-3C4D-5E6F|active|2026-11-09|crloiZaRmkjGjd5LEyowXWTJVGusIk6KiKvWYI3t4vNWIXCdVRvT52u-IsKFtiTF4HwPVxTIrvGgPpdIY1sot6vi9hXD6Xgv58hAp-T3cqSaRs6cVG7PUtVqjyQZ6ijCb4wLaZAYFd_oHQ0l1khAaqzQYXWxi7jqoaSgt1NO1iF-kCiwT_bSZnL59buAfCF86ryYC-YZJ1MNGtwFulXQCu-N2ZLgbIE3uMljfX6fb8dDRMVmnfWr4qQUzzEIyodCf0d6eMnfHUNXLiive7DQk7r00jP8mwDsYIwi5M6qRJEkslh8GnbOQbCbQBYkEiEOy2SL7DiPl-TznPCu01X_Yw";
        private const string StatusDisabled = "CCS1|CC-1A2B-3C4D-5E6F|disabled|2026-11-09|OntxvyIZr-DtERvVxIr1mX7r20nndoTiJQ-6p4S1GOh1rSUEA3XKZexOFPEQlSKITOwsU2qPN_jjSBX4TiFE8za5Bj6b2YOwWZiRUEFLZCJv4iRnhdGiQriVaOegDPapT4zdJwAqRruqf7DhfWCbrT6SCJ7g5zGPcKrIxpUI1jNeCRzj752LyKvT-qy0_2-bWPtRvV34wEzA6_533sBBOZksqhbazcRk63jy5jIZS05EC2ihu64S6PnVlNhZ4M5uf1eQJSLq_TuEHfwP2p9lV9UnxDxw6XQ5Patn0OjBTm9KbyUFlWMu-WMnkHW8KkEvu5PZerFKk5M21P4Xi9woQA";
        private const string StatusOffline = "CCS1|CC-1A2B-3C4D-5E6F|offline|2025-01-01|jkIvrRm9ry5m7ugvffylVfUW0n1hYj31b6YyX2V2IpmnDNvPDblGfkZtbuwGDZHEGiKs662Bq2sehiP4fZxPiQdOtzIWyPIIYY0ja6_vIpgGbKGwtgf1dpgk9asBEZ1hoh1JN7v9zrt-9Pp3E82_nfiVSOe_YqEWNMXvFOKKf2zFos0VSSoyKj5kdMzhAVIz1dZL4xL_9wTMfKD2ONVHCPhvE_SCcg25egNkC0L-FFAFAxYwuA3ZMrOTGrhGHlaaxXuB_lwcFzxlxmQH22Ov7rpIf8ysQFxQFJIZuAnCuHE4NiYVn3A6jh3RmZ9nPy1q5grjYJa4T1Qy13R_CjUbZg";
        private const string StatusFuture = "CCS1|CC-1A2B-3C4D-5E6F|active|2026-12-31|faNnBKQs6T3MbwEc0rjymnRBFhsEQ8sehsl9jj0-izhEZKcqDBK96RuYQq3mVXQVdMVHM8UjHvK68RF4bpMdIJbMD5RRtNguYC9gunMMvAEgnwa-YwPsvIL9T-Ueqx0LHYuUytIs1OGy7sdGG1KOTll6TDKyzNmizaVEw0_pIIvjpfiCzVubn22cIoJDD3sSUomNmxegZoOCsGHlVt9LlgYzTae-bhKmFwUj4H4bidp-xowg5RYGlxGju7L788bYP0tNfmwPhFu3RcmOF69vUhUSaNSdXFRsADA7NLIrMOn-G9SVl4Gll8UhPmHemt1rRXRa4kvLQKOS4a0_ANzx9w";
        private static readonly System.DateTime Nov10 = new System.DateTime(2026, 11, 10, 12, 0, 0, System.DateTimeKind.Utc);

        [Fact]
        public void FreshSignedStatusOfThisMachinePasses()
        {
            var r = LicenseLogic.VerifyStatus(StatusActive, "CC-1A2B-3C4D-5E6F", Nov10, StatusModulus, "AQAB");
            Assert.True(r.Ok, r.Reason);
            Assert.True(r.Fresh, r.Reason);
            Assert.Equal("active", r.Status);
            Assert.Equal(1, r.AgeDays);
            Assert.True(LicenseLogic.VerifyStatus(StatusActive + "\r\n", "cc-1a2b-3c4d-5e6f", Nov10, StatusModulus, "AQAB").Fresh);
            var d = LicenseLogic.VerifyStatus(StatusDisabled, "CC-1A2B-3C4D-5E6F", Nov10, StatusModulus, "AQAB");
            Assert.True(d.Ok); // the signature is good — the *status* says disabled
            Assert.Equal("disabled", d.Status);
            Assert.True(LicenseLogic.IsStatusText(StatusActive));
            Assert.True(LicenseLogic.IsStatusText("CCL1|x\n" + StatusActive)); // pasted together with the licence
            Assert.False(LicenseLogic.IsStatusText("CCL1|x"));
        }

        [Fact]
        public void StatusGoesStaleAfterSevenDaysOfflineButTheOfflinePassDoesNot()
        {
            // 8 days after the last successful check: the machine must reach the service again
            var stale = LicenseLogic.VerifyStatus(StatusActive, "CC-1A2B-3C4D-5E6F", Nov10.AddDays(7), StatusModulus, "AQAB");
            Assert.True(stale.Ok);
            Assert.False(stale.Fresh);
            Assert.Contains("ngày", stale.Reason);
            // exactly at the edge of the window it still runs
            Assert.True(LicenseLogic.VerifyStatus(StatusActive, "CC-1A2B-3C4D-5E6F", Nov10.AddDays(6), StatusModulus, "AQAB").Fresh);
            // the owner's no-network pass (status "offline") never goes stale
            var off = LicenseLogic.VerifyStatus(StatusOffline, "CC-1A2B-3C4D-5E6F", Nov10.AddYears(3), StatusModulus, "AQAB");
            Assert.True(off.Ok);
            Assert.True(off.Fresh);
            Assert.Equal("offline", off.Status);
            // a date in the future (clock set back to stretch the window) is not fresh
            var future = LicenseLogic.VerifyStatus(StatusFuture, "CC-1A2B-3C4D-5E6F", Nov10, StatusModulus, "AQAB");
            Assert.True(future.Ok);
            Assert.False(future.Fresh);
        }

        [Fact]
        public void ClockSetBackToStretchTheOfflineWindowIsCaught()
        {
            // the plugin already saw Nov 20; the clock now says Nov 12 (inside the 7 days of the Nov 9 status)
            var seen = new System.DateTime(2026, 11, 20, 9, 0, 0, System.DateTimeKind.Utc);
            var back = LicenseLogic.VerifyStatus(StatusActive, "CC-1A2B-3C4D-5E6F", new System.DateTime(2026, 11, 12, 9, 0, 0, System.DateTimeKind.Utc), StatusModulus, "AQAB", seen);
            Assert.True(back.Ok);
            Assert.False(back.Fresh);
            Assert.True(back.ClockBack);
            Assert.Contains("đồng hồ", back.Reason);
            // a few hours back (time zone, daylight saving, a small correction) is fine
            Assert.True(LicenseLogic.VerifyStatus(StatusActive, "CC-1A2B-3C4D-5E6F", Nov10, StatusModulus, "AQAB", Nov10.AddHours(20)).Fresh);
            Assert.True(LicenseLogic.VerifyStatus(StatusActive, "CC-1A2B-3C4D-5E6F", Nov10, StatusModulus, "AQAB", null).Fresh);
            // the owner's no-network pass does not care about the clock
            Assert.True(LicenseLogic.VerifyStatus(StatusOffline, "CC-1A2B-3C4D-5E6F", Nov10, StatusModulus, "AQAB", Nov10.AddYears(1)).Fresh);
            Assert.False(LicenseLogic.ClockRolledBack(Nov10, null));
            Assert.True(LicenseLogic.ClockRolledBack(Nov10, Nov10.AddDays(2)));
        }

        [Fact]
        public void StatusOfOtherMachinesTamperingAndJunkFail()
        {
            Assert.False(LicenseLogic.VerifyStatus(StatusActive, "CC-9999-9999-9999", Nov10, StatusModulus, "AQAB").Ok);
            // disabled machine editing its saved status to "active": the signature no longer matches
            Assert.False(LicenseLogic.VerifyStatus(StatusDisabled.Replace("disabled", "active"), "CC-1A2B-3C4D-5E6F", Nov10, StatusModulus, "AQAB").Ok);
            // date stretched by hand
            Assert.False(LicenseLogic.VerifyStatus(StatusActive.Replace("2026-11-09", "2027-11-09"), "CC-1A2B-3C4D-5E6F", Nov10, StatusModulus, "AQAB").Ok);
            Assert.False(LicenseLogic.VerifyStatus("", "CC-1A2B-3C4D-5E6F", Nov10, StatusModulus, "AQAB").Ok);
            Assert.False(LicenseLogic.VerifyStatus(null, "CC-1A2B-3C4D-5E6F", Nov10, StatusModulus, "AQAB").Ok);
            Assert.False(LicenseLogic.VerifyStatus("CCS1|CC-1A2B-3C4D-5E6F|active|2026-11-09|%%%", "CC-1A2B-3C4D-5E6F", Nov10, StatusModulus, "AQAB").Ok);
            Assert.False(LicenseLogic.VerifyStatus(TestLicense, "CC-1A2B-3C4D-5E6F", Nov10, StatusModulus, "AQAB").Ok); // a licence is not a status
            // signed by another key (the production key): refused with this modulus, and vice versa
            Assert.False(LicenseLogic.VerifyStatus(StatusActive, "CC-1A2B-3C4D-5E6F", Nov10).Ok);
        }

        [Fact]
        public void ServiceAnswersAndUrls()
        {
            var ok = LicenseLogic.ParseResponse("{\"ok\":true,\"license\":\"CCL1|x\"}");
            Assert.True(ok.ok);
            Assert.Equal("CCL1|x", ok.license);
            var no = LicenseLogic.ParseResponse("{\"ok\":false,\"error\":\"Key không đúng\"}");
            Assert.False(no.ok);
            Assert.Equal("Key không đúng", no.error);
            Assert.True(LicenseLogic.ParseResponse("{\"ok\":true,\"sent\":true}").sent);
            Assert.Equal("CCS1|x", LicenseLogic.ParseResponse("{\"ok\":true,\"status\":\"CCS1|x\"}").status);
            var html = LicenseLogic.ParseResponse("<!DOCTYPE html><html>Sign in</html>");
            Assert.False(html.ok);
            Assert.Contains("Anyone", html.error);
            Assert.Equal("https://x/exec?action=request&device=M%C3%A1y%201",
                LicenseLogic.BuildUrl("https://x/exec", new[] { new KeyValuePair<string, string>("action", "request"), new KeyValuePair<string, string>("device", "Máy 1") }));
            Assert.Equal("https://x/exec?a=1&b=2", LicenseLogic.BuildUrl("https://x/exec?a=1", new[] { new KeyValuePair<string, string>("b", "2") }));
        }
    }
}
