using AutoCaptureOCR.Core.Services;
using FluentAssertions;
using Xunit;

namespace AutoCaptureOCR.Tests.Services;

public class TextStitcherTests
{
    [Fact]
    public void StitchNewLines_FirstFrame_ReturnsAllLines()
    {
        var stitcher = new TextStitcher();
        var result = stitcher.StitchNewLines("Line 1\nLine 2\nLine 3");

        result.Should().BeEquivalentTo(new[] { "Line 1", "Line 2", "Line 3" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void StitchNewLines_IdenticalConsecutiveFrames_ReturnsEmpty()
    {
        var stitcher = new TextStitcher();
        stitcher.StitchNewLines("Line 1\nLine 2\nLine 3");
        var result = stitcher.StitchNewLines("Line 1\nLine 2\nLine 3");

        result.Should().BeEmpty();
    }

    [Fact]
    public void StitchNewLines_ScrolledDown_ReturnsOnlyNewBottomLines()
    {
        var stitcher = new TextStitcher();
        // Frame 1 shows lines 1, 2, 3
        stitcher.StitchNewLines("Line 1\nLine 2\nLine 3");

        // Frame 2 scrolled down: lines 1 scrolled out of view, now shows 2, 3, 4, 5
        var result = stitcher.StitchNewLines("Line 2\nLine 3\nLine 4\nLine 5");

        result.Should().BeEquivalentTo(new[] { "Line 4", "Line 5" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void StitchNewLines_CompletelyNewScreen_ReturnsAllNewLines()
    {
        var stitcher = new TextStitcher();
        stitcher.StitchNewLines("Alpha\nBeta");
        var result = stitcher.StitchNewLines("Gamma\nDelta");

        result.Should().BeEquivalentTo(new[] { "Gamma", "Delta" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void StitchNewLines_NullOrEmpty_ReturnsEmpty()
    {
        var stitcher = new TextStitcher();
        stitcher.StitchNewLines(null).Should().BeEmpty();
        stitcher.StitchNewLines("   ").Should().BeEmpty();
    }

    [Fact]
    public void Reset_ClearsHistory_NextFrameYieldsAllLines()
    {
        var stitcher = new TextStitcher();
        stitcher.StitchNewLines("Line 1\nLine 2");
        stitcher.Reset();

        var result = stitcher.StitchNewLines("Line 1\nLine 2");
        result.Should().HaveCount(2);
    }
}
