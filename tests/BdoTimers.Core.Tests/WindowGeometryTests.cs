using BdoTimers.Core.Model;

namespace BdoTimers.Core.Tests;

public class WindowGeometryTests
{
    [Fact]
    public void MonitorGapDoesNotCountAsVisibleWorkspace()
    {
        WindowRect[] areas = [new(0, 0, 1000, 800), new(1400, 0, 1000, 800)];
        Assert.Equal(new WindowRect(800, 100, 200, 400), WindowGeometry.Clamp(new(1050, 100, 200, 400), areas));
    }

    [Fact]
    public void NegativeCoordinateMonitorRetainsPlacement()
    {
        WindowRect[] areas = [new(-1920, 80, 1920, 1000), new(0, 0, 1920, 1040)];
        Assert.Equal(new WindowRect(-1500, 200, 900, 720), WindowGeometry.Clamp(new(-1500, 200, 900, 720), areas));
    }

    [Fact]
    public void RemovedMonitorMovesWholeWindowIntoRemainingWorkArea()
    {
        Assert.Equal(new WindowRect(980, 320, 940, 720),
            WindowGeometry.Clamp(new(2400, 600, 940, 720), [new(0, 0, 1920, 1040)]));
    }

    [Fact]
    public void HighDpiSmallWorkspaceShrinksWindowAndKeepsTaskbarClear()
    {
        Assert.Equal(new WindowRect(0, 0, 960, 500),
            WindowGeometry.Clamp(new(10, 20, 1100, 720), [new(0, 0, 960, 500)]));
    }

    [Fact]
    public void LargestOverlapChoosesMonitor()
    {
        WindowRect[] areas = [new(0, 0, 1000, 800), new(1000, 0, 1000, 800)];
        Assert.Equal(areas[1], WindowGeometry.WorkAreaFor(new(900, 100, 800, 500), areas));
    }

    [Fact]
    public void ResizeKeepsTheCentre()
    {
        Assert.Equal(new WindowRect(100, 140, 1200, 900), WindowGeometry.Resize(new(300, 290, 800, 600), 1200, 900));
        Assert.Equal(new WindowRect(300, 290, 800, 600), WindowGeometry.Resize(new(100, 140, 1200, 900), 800, 600));
    }
}
