using System;
using System.Collections.Generic;
using ClaudeCAD.Plugin.Core;
using Xunit;

namespace ClaudeCAD.Core.Tests
{
    public class GeometryTests
    {
        private static void Near(double expected, double actual, double tol = 1e-6) => Assert.True(Math.Abs(expected - actual) < tol, $"expected {expected}, got {actual}");

        [Fact]
        public void RectangleFromCornersIsCcwAndNormalized()
        {
            var r = Geometry.Rectangle(Geometry.P(4000, 6000), Geometry.P(0, 0));
            Assert.Equal(4, r.Count);
            Assert.Equal(new[] { 0.0, 0, 0 }, r[0]);
            Assert.Equal(new[] { 4000.0, 6000, 0 }, r[2]);
            Near(24e6, Geometry.SignedArea(r));
            Near(20000, Geometry.Perimeter(r));
        }

        [Fact]
        public void RectangleWidthHeightRotated()
        {
            var r = Geometry.Rectangle(Geometry.P(0, 0), 1000, 500, 90);
            Near(0, r[1][0]); Near(1000, r[1][1]);
            Near(500000, Math.Abs(Geometry.SignedArea(r)));
            Assert.True(Geometry.SignedArea(r) > 0);
        }

        [Fact]
        public void DegenerateRectangleRejected()
        {
            Assert.Throws<ToolException>(() => Geometry.Rectangle(Geometry.P(0, 0), Geometry.P(0, 100)));
        }

        [Fact]
        public void PolygonInscribedAndCircumscribed()
        {
            var ins = Geometry.RegularPolygon(Geometry.P(0, 0), 4, 1000, true, 0);
            Near(1000, ins[0][0]); Near(0, ins[0][1]);
            var cir = Geometry.RegularPolygon(Geometry.P(0, 0), 4, 1000, false, 0);
            // square circumscribed about r=1000 has side 2000 => area 4e6
            Near(4e6, Geometry.SignedArea(cir), 1e-3);
            Assert.Throws<ToolException>(() => Geometry.RegularPolygon(Geometry.P(0, 0), 2, 1, true, 0));
        }

        [Fact]
        public void ArcThroughThreePoints()
        {
            // upper half circle from (1000,0) through (0,1000) to (-1000,0): CCW 0..180
            var (c, r, s, e) = Geometry.ArcFrom3Points(Geometry.P(1000, 0), Geometry.P(0, 1000), Geometry.P(-1000, 0));
            Near(0, c[0]); Near(0, c[1]); Near(1000, r);
            Near(0, s); Near(Math.PI, e);
            // lower half (clockwise input) gives CCW arc 180..360
            var (_, _, s2, e2) = Geometry.ArcFrom3Points(Geometry.P(1000, 0), Geometry.P(0, -1000), Geometry.P(-1000, 0));
            Near(Math.PI, s2); Near(0, Geometry.NormalizeRad(e2));
            Assert.Throws<ToolException>(() => Geometry.ArcFrom3Points(Geometry.P(0, 0), Geometry.P(1, 1), Geometry.P(2, 2)));
        }

        [Fact]
        public void RevCloudSubdividesEdgesWithOutwardBulges()
        {
            var square = new List<double[]> { Geometry.P(0, 0), Geometry.P(0, 1000), Geometry.P(1000, 1000), Geometry.P(1000, 0) }; // CW input
            var (v, b) = Geometry.RevCloud(square, 250);
            Assert.Equal(16, v.Count);
            Assert.Equal(v.Count, b.Count);
            Assert.All(b, x => Assert.True(x > 0));
            Assert.True(Geometry.SignedArea(v) > 0); // reordered to CCW
        }

        [Theory]
        [InlineData(0.25, 25)]
        [InlineData(0.5, 50)]
        [InlineData(35.0, 35)]
        [InlineData(0.22, 20)]
        [InlineData(1.0, 100)]
        public void LineWeightSnaps(double input, int expected)
        {
            Assert.Equal(expected, Geometry.LineWeightFrom(input));
        }

        [Fact]
        public void LineWeightKeywords()
        {
            Assert.Equal(-1, Geometry.LineWeightFrom("bylayer"));
            Assert.Equal(-3, Geometry.LineWeightFrom("default"));
            Assert.Throws<ToolException>(() => Geometry.LineWeightFrom(-1.0));
        }

        [Fact]
        public void DirectionFromVectorOrAngle()
        {
            var d = Geometry.Direction(new[] { 0.0, 5, 0 }, null);
            Near(0, d[0]); Near(1, d[1]);
            var a = Geometry.Direction(null, 90);
            Near(0, a[0]); Near(1, a[1]);
            Assert.Throws<ToolException>(() => Geometry.Direction(null, null));
        }
    }
}
