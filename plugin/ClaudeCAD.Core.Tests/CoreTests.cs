using System.Collections.Generic;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    public class JsonCodecTests
    {
        [Fact]
        public void ParsesSpecToolCall()
        {
            var msg = JsonCodec.ParseObject("{\"id\": \"req-17\", \"type\": \"tool_call\", \"tool\": \"draw_polyline\", \"params\": {\"vertices\": [[0,0],[4000,0],[4000,6000],[0,6000]], \"closed\": true, \"layer\": \"A-WALL\"}}");
            Assert.Equal("req-17", msg["id"]);
            var p = (Dictionary<string, object>)msg["params"];
            var v = (List<object>)p["vertices"];
            Assert.Equal(4, v.Count);
            Assert.Equal(4000.0, ((List<object>)v[1])[0]);
            Assert.Equal(true, p["closed"]);
        }

        [Fact]
        public void RoundTripsUnicodeAndEscapes()
        {
            var src = new Dictionary<string, object> { ["text"] = "VỆ SINH\n\"WC\"\t\\", ["n"] = 1.5, ["i"] = 3, ["nul"] = null, ["b"] = false };
            var json = JsonCodec.Serialize(src);
            Assert.DoesNotContain("\n", json);
            var back = JsonCodec.ParseObject(json);
            Assert.Equal("VỆ SINH\n\"WC\"\t\\", back["text"]);
            Assert.Equal(1.5, back["n"]);
            Assert.Equal(3.0, back["i"]);
            Assert.Null(back["nul"]);
            Assert.Equal(false, back["b"]);
        }

        [Fact]
        public void SerializesResponseEnvelopeCompactly()
        {
            var env = new Dictionary<string, object> { ["id"] = "req-17", ["ok"] = true, ["result"] = new Dictionary<string, object> { ["handles"] = new List<object> { "2F4A" } } };
            Assert.Equal("{\"id\":\"req-17\",\"ok\":true,\"result\":{\"handles\":[\"2F4A\"]}}", JsonCodec.Serialize(env));
        }

        [Fact]
        public void IntegralDoublesHaveNoDecimals_NaNIsNull()
        {
            Assert.Equal("[4000,0.25,null,-1E-05]", JsonCodec.Serialize(new object[] { 4000.0, 0.25, double.NaN, -0.00001 }));
        }

        [Fact]
        public void ParsesUnicodeEscapes()
        {
            Assert.Equal("Vệ", JsonCodec.Parse("\"V\\u1ec7\""));
        }

        [Theory]
        [InlineData("{")]
        [InlineData("{\"a\":}")]
        [InlineData("[1,2")]
        [InlineData("{\"a\":1} x")]
        [InlineData("tru")]
        public void RejectsMalformed(string text)
        {
            Assert.Throws<System.FormatException>(() => JsonCodec.Parse(text));
        }
    }

    public class ParamsTests
    {
        private static Params P(string json) => new Params("t", JsonCodec.ParseObject(json));

        [Fact]
        public void PointsDefaultZToZero()
        {
            var p = P("{\"a\":[1,2],\"b\":[1,2,3],\"c\":{\"x\":5,\"y\":6}}");
            Assert.Equal(new[] { 1.0, 2.0, 0.0 }, p.Point("a"));
            Assert.Equal(new[] { 1.0, 2.0, 3.0 }, p.Point("b"));
            Assert.Equal(new[] { 5.0, 6.0, 0.0 }, p.Point("c"));
        }

        [Fact]
        public void MissingOrBadPointGivesInvalidParams()
        {
            var ex = Assert.Throws<ToolException>(() => P("{\"a\":[1]}").Point("a"));
            Assert.Equal(ErrorCodes.InvalidParams, ex.Code);
            Assert.Contains("'a'", ex.Message);
            Assert.Throws<ToolException>(() => P("{}").Point("start"));
        }

        [Fact]
        public void HandlesAreValidatedAndDeduplicated()
        {
            var p = P("{\"h\":[\"2F4A\",\"2f4a\",\"1B\"]}");
            Assert.Equal(new List<string> { "2F4A", "1B" }, p.Handles("h"));
            Assert.Throws<ToolException>(() => P("{\"h\":[\"zz\"]}").Handles("h"));
        }

        [Fact]
        public void TooManyHandlesUsesTooManyEntities()
        {
            var list = new List<object>();
            for (int i = 1; i <= 1001; i++) list.Add(i.ToString("X"));
            var p = new Params("erase_entities", new Dictionary<string, object> { ["handles"] = list });
            var ex = Assert.Throws<ToolException>(() => p.Handles("handles", 1000));
            Assert.Equal(ErrorCodes.TooManyEntities, ex.Code);
        }

        [Fact]
        public void ColorAcceptsAciAndByLayer()
        {
            Assert.Equal(1, P("{\"color\":1}").ColorOpt());
            Assert.Equal("bylayer", P("{\"color\":\"ByLayer\"}").ColorOpt());
            Assert.Null(P("{}").ColorOpt());
            Assert.Throws<ToolException>(() => P("{\"color\":300}").ColorOpt());
        }

        [Fact]
        public void NumbersAndDefaults()
        {
            var p = P("{\"r\":250,\"s\":\"3.5\",\"n\":2}");
            Assert.Equal(250, p.Positive("r"));
            Assert.Equal(3.5, p.Num("s"));
            Assert.Equal(7.0, p.Num("missing", 7));
            Assert.Equal(2, p.Int("n", 0, 1, 10));
            Assert.Throws<ToolException>(() => P("{\"r\":-1}").Positive("r"));
            Assert.Throws<ToolException>(() => P("{\"n\":1.5}").Int("n"));
        }
    }
}
