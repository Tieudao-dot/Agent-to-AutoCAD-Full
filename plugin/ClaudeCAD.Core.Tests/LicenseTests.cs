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
        private const string ProductionLicense = "CCL1|CC-0000-0000-0000|2026-11-09|gwfpRDunxHsXloT9P_oyqhYHzF8ohOVVfH7edtEfRMGzmmKKFlpYkC7ivYQJ00LTEbKLqpJ4lSfddzJzvDbNOjorVsa6g7rd4V16fE5QFTWgHqN9RoczTBS1M8OpZ1Xamkfr4oFDnivkMVftAMbYlkBmWKtsX79SnQExWnZRs0xfRmzCiz0NYSaoEDRT4ePIXTpDGngxituP7qUAaUhov8KovwwSdLvRbeqz2kV2cAoIeg8mZIpF-BvJFS9EYFjQ_R0baWhxMMs9oDFu1IQB_CCPwcNObF9FjP1enFhxU_h-duRYMAvxm55L44htpBI5wyGq28hugqNQ1lYrEq8xlQ";

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
            var html = LicenseLogic.ParseResponse("<!DOCTYPE html><html>Sign in</html>");
            Assert.False(html.ok);
            Assert.Contains("Anyone", html.error);
            Assert.Equal("https://x/exec?action=request&device=M%C3%A1y%201",
                LicenseLogic.BuildUrl("https://x/exec", new[] { new KeyValuePair<string, string>("action", "request"), new KeyValuePair<string, string>("device", "Máy 1") }));
            Assert.Equal("https://x/exec?a=1&b=2", LicenseLogic.BuildUrl("https://x/exec?a=1", new[] { new KeyValuePair<string, string>("b", "2") }));
        }
    }
}
