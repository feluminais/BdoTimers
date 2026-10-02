using System.Net;
using System.Text;
using System.Text.Json;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Scheduling;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Updates;

namespace BdoTimers.Core.Tests;

[Collection(nameof(Log))]
public class ReleaseUpdateTests
{
    [Theory]
    [InlineData("v1.0.100", "1.0.99", 1)]
    [InlineData("1.10.0", "1.9.99", 1)]
    [InlineData("2.0.0", "1.99.999", 1)]
    [InlineData("1.0", "1.0.0.0", 0)]
    [InlineData("v1.0.42+build.abc", "1.0.42+deadbeef", 0)]
    [InlineData("1.0.42.1", "1.0.42", 1)]
    [InlineData("1.0.9", "1.0.10", -1)]
    public void Versions_compare_numeric_components(string release, string installed, int expected)
    {
        Assert.True(ReleaseVersion.TryParse(release, out var next));
        Assert.True(ReleaseVersion.TryParse(installed, out var current));
        Assert.Equal(expected, Math.Sign(next.CompareTo(current)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("release-1.0.1")]
    [InlineData("1.0.1-rc.1")]
    [InlineData("v1.0.1-beta+build")]
    [InlineData("1.-1.0")]
    [InlineData("1. 0.1")]
    [InlineData("1.0.1.0.1")]
    [InlineData("1.0.2147483648")]
    [InlineData("1.0.1+")]
    public void Unusable_or_prerelease_versions_are_rejected(string? text) =>
        Assert.False(ReleaseVersion.TryParse(text, out _));

    [Fact]
    public async Task Highest_stable_numeric_version_wins_regardless_of_response_order()
    {
        using var http = Http(_ => Json("[" + string.Join(',',
            Release("v1.0.9"), Release("v99.0.0", draft: true), Release("v98.0.0", prerelease: true),
            Release("v97.0.0-rc.1"), Release("v1.0.100"), Release("v1.0.10")) + "]"));
        var release = await new GitHubReleaseSource(http).LatestStableAsync(default);
        Assert.Equal(new Version(1, 0, 100, 0), release!.Version);
        Assert.Equal("1.0.100", release.Number);
        Assert.Equal("https://github.com/feluminais/BdoTimers/releases/tag/v1.0.100", release.Page.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://github.com/feluminais/BdoTimers/releases/tag/v1.0.100+build.abc")]
    [InlineData("https://github.com/feluminais/BdoTimers/releases/tag/v1.0.100%2Bbuild.abc")]
    public async Task Equivalent_official_page_encodings_accept_numeric_build_metadata(string page)
    {
        using var http = Http(_ => Json("[" + Release("v1.0.100+build.abc", page: page) + "]"));
        using var fixture = new CheckFixture(new GitHubReleaseSource(http));
        var result = await fixture.Checker.CheckAsync();
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.0.100", result.Release!.Number);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"draft\":true,\"prerelease\":false}]")]
    [InlineData("[{\"draft\":false,\"prerelease\":true}]")]
    public async Task No_stable_releases_means_no_update(string body)
    {
        using var http = Http(_ => Json(body));
        using var fixture = new CheckFixture(new GitHubReleaseSource(http));
        Assert.Equal(UpdateStatus.UpToDate, (await fixture.Checker.CheckAsync()).Status);
    }

    [Fact]
    public async Task Checks_subsequent_release_pages()
    {
        var requests = new List<string>();
        using var http = Http(request =>
        {
            requests.Add(request.RequestUri!.AbsoluteUri);
            Assert.Contains("BdoTimers", request.Headers.UserAgent.ToString());
            Assert.Contains("application/vnd.github+json", request.Headers.Accept.ToString());
            return Json(requests.Count == 1
                ? "[" + string.Join(',', Enumerable.Repeat(Release("v1.0.9"), 100)) + "]"
                : "[" + Release("v1.0.100") + "]");
        });
        var release = await new GitHubReleaseSource(http).LatestStableAsync(default);
        Assert.Equal("1.0.100", release!.Number);
        Assert.Equal(new[]
        {
            "https://api.github.com/repos/feluminais/BdoTimers/releases?per_page=100&page=1",
            "https://api.github.com/repos/feluminais/BdoTimers/releases?per_page=100&page=2",
        }, requests);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(404)]
    [InlineData(302)]
    public async Task Http_failure_is_a_result_without_retries(int code)
    {
        var calls = 0;
        using var http = Http(_ => { calls++; return new((HttpStatusCode)code); });
        using var fixture = new CheckFixture(new GitHubReleaseSource(http));
        Assert.Equal(UpdateStatus.CouldNotCheck, (await fixture.Checker.CheckAsync()).Status);
        Assert.Equal(1, calls);
        Assert.Null(fixture.Checker.Current.Release);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[{}]")]
    [InlineData("[{\"draft\":false,\"prerelease\":false,\"tag_name\":42}]")]
    [InlineData("[{\"draft\":\"false\",\"prerelease\":false,\"tag_name\":\"v1.0.100\"}]")]
    public async Task Malformed_responses_are_a_failure(string body)
    {
        using var http = Http(_ => Json(body));
        using var fixture = new CheckFixture(new GitHubReleaseSource(http));
        Assert.Equal(UpdateStatus.CouldNotCheck, (await fixture.Checker.CheckAsync()).Status);
    }

    [Theory]
    [InlineData("https://example.com/feluminais/BdoTimers/releases/tag/v1.0.100")]
    [InlineData("https://github.com/other/project/releases/tag/v1.0.100")]
    [InlineData("http://github.com/feluminais/BdoTimers/releases/tag/v1.0.100")]
    [InlineData("https://github.com/feluminais/BdoTimers/releases/tag/v1.0.9")]
    public async Task Only_the_official_page_for_the_selected_tag_is_offered(string page)
    {
        using var http = Http(_ => Json("[" + Release("v1.0.100", page: page) + "]"));
        using var fixture = new CheckFixture(new GitHubReleaseSource(http));
        Assert.Equal(UpdateStatus.CouldNotCheck, (await fixture.Checker.CheckAsync()).Status);
        Assert.Null(fixture.Checker.Current.Release);
    }

    [Theory]
    [InlineData("v1.0.41", UpdateStatus.UpToDate)]
    [InlineData("v1.0.42", UpdateStatus.UpToDate)]
    [InlineData("v1.0.43", UpdateStatus.UpdateAvailable)]
    public async Task Installed_product_version_prevents_equal_versions_and_downgrades(string tag, UpdateStatus expected)
    {
        using var http = Http(_ => Json("[" + Release(tag) + "]"));
        using var fixture = new CheckFixture(new GitHubReleaseSource(http), installed: "1.0.42+commit");
        var result = await fixture.Checker.CheckAsync();
        Assert.Equal(expected, result.Status);
        Assert.Equal(expected == UpdateStatus.UpdateAvailable, result.Release is not null);
    }

    [Fact]
    public async Task Invalid_installed_version_cannot_offer_an_update()
    {
        using var fixture = new CheckFixture(new StubSource(_ => Task.FromResult<ReleaseInfo?>(NewRelease)), installed: "unknown");
        Assert.Equal(UpdateStatus.CouldNotCheck, (await fixture.Checker.CheckAsync()).Status);
    }

    [Fact]
    public async Task Offline_and_timeout_failures_do_not_escape()
    {
        foreach (var error in new Exception[] { new HttpRequestException("offline"), new TaskCanceledException("timeout"), new IOException("broken body") })
        {
            using var fixture = new CheckFixture(new StubSource(_ => Task.FromException<ReleaseInfo?>(error)));
            Assert.Equal(UpdateStatus.CouldNotCheck, (await fixture.Checker.CheckAsync()).Status);
        }
    }

    [Fact]
    public async Task Timeout_bounds_even_a_source_that_does_not_complete()
    {
        var pending = new TaskCompletionSource<ReleaseInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var fixture = new CheckFixture(new StubSource(_ => pending.Task), timeout: TimeSpan.FromMilliseconds(30));
        var result = await fixture.Checker.CheckAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(UpdateStatus.CouldNotCheck, result.Status);
    }

    [Fact]
    public async Task Automatic_attempt_is_saved_before_network_and_suppressed_for_24_hours_across_restarts()
    {
        using var fixture = new CheckFixture(new StubSource(_ => Task.FromException<ReleaseInfo?>(new HttpRequestException("offline"))));
        Assert.Equal(UpdateStatus.CouldNotCheck, (await fixture.Checker.CheckAutomaticallyAsync())!.Status);
        Assert.Equal(fixture.Clock.UtcNow, fixture.SavedState.LastAttemptUtc);
        var restarted = fixture.CreateChecker();
        fixture.Clock.UtcNow += TimeSpan.FromHours(24) - TimeSpan.FromTicks(1);
        Assert.Null(await restarted.CheckAutomaticallyAsync());
        fixture.Clock.UtcNow += TimeSpan.FromTicks(1);
        Assert.Equal(UpdateStatus.CouldNotCheck, (await restarted.CheckAutomaticallyAsync())!.Status);
        Assert.Equal(fixture.Clock.UtcNow, fixture.SavedState.LastAttemptUtc);
    }

    [Fact]
    public async Task Manual_check_bypasses_daily_limit_and_moves_the_next_automatic_attempt()
    {
        using var fixture = new CheckFixture(new StubSource(_ => Task.FromResult<ReleaseInfo?>(NewRelease)));
        await fixture.Checker.CheckAutomaticallyAsync();
        fixture.Clock.UtcNow += TimeSpan.FromHours(1);
        Assert.Equal(UpdateStatus.UpdateAvailable, (await fixture.Checker.CheckAsync()).Status);
        fixture.Clock.UtcNow += TimeSpan.FromHours(23);
        Assert.Null(await fixture.Checker.CheckAutomaticallyAsync());
        fixture.Clock.UtcNow += TimeSpan.FromHours(1);
        Assert.NotNull(await fixture.Checker.CheckAutomaticallyAsync());
    }

    [Fact]
    public async Task Disabled_automatic_check_neither_uses_network_nor_changes_state()
    {
        using var fixture = new CheckFixture(new StubSource(_ => throw new InvalidOperationException("Must not call")), automatic: false);
        Assert.Null(await fixture.Checker.CheckAutomaticallyAsync());
        Assert.Null(fixture.State.Current.LastAttemptUtc);
        Assert.Equal(UpdateStatus.Idle, fixture.Checker.Current.Status);
    }

    [Fact]
    public async Task Debug_policy_still_allows_a_manual_check()
    {
        using var fixture = new CheckFixture(new StubSource(_ => Task.FromResult<ReleaseInfo?>(NewRelease)), automatic: false);
        Assert.Equal(UpdateStatus.UpdateAvailable, (await fixture.Checker.CheckAsync()).Status);
    }

    [Fact]
    public async Task Clock_moved_back_does_not_repeat_automatic_checks()
    {
        using var fixture = new CheckFixture(new StubSource(_ => Task.FromResult<ReleaseInfo?>(null)));
        await fixture.Checker.CheckAutomaticallyAsync();
        fixture.Clock.UtcNow -= TimeSpan.FromDays(1);
        Assert.Null(await fixture.Checker.CheckAutomaticallyAsync());
    }

    [Fact]
    public async Task Overlapping_checks_share_a_request_and_publish_checking_then_result()
    {
        var pending = new TaskCompletionSource<ReleaseInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var fixture = new CheckFixture(new StubSource(_ => { calls++; return pending.Task; }));
        var seen = new List<UpdateStatus>();
        fixture.Checker.Changed += () => seen.Add(fixture.Checker.Current.Status);
        var first = fixture.Checker.CheckAutomaticallyAsync();
        Assert.Equal(UpdateStatus.Checking, fixture.Checker.Current.Status);
        Assert.Equal(fixture.Clock.UtcNow, fixture.State.Current.LastAttemptUtc);
        var second = fixture.Checker.CheckAsync();
        pending.SetResult(NewRelease);
        Assert.Equal(UpdateStatus.UpdateAvailable, (await first)!.Status);
        Assert.Equal(UpdateStatus.UpdateAvailable, (await second).Status);
        Assert.Equal(1, calls);
        Assert.Equal(new[] { UpdateStatus.Checking, UpdateStatus.UpdateAvailable }, seen);
    }

    [Fact]
    public async Task Unwritable_attempt_state_fails_without_network()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "blocked");
        Directory.CreateDirectory(path);
        var state = new PersistentState<UpdateCheckState>(new JsonFileStore<UpdateCheckState>(path, () => new()), new());
        var checker = new UpdateChecker(new StubSource(_ => throw new InvalidOperationException("Must not call")),
            "1.0.42", new FakeClock(), state);
        Assert.Equal(UpdateStatus.CouldNotCheck, (await checker.CheckAsync()).Status);
        Assert.Null(state.Current.LastAttemptUtc);
    }

    static readonly ReleaseInfo NewRelease = new(new Version(1, 0, 100, 0), new Uri("https://github.com/feluminais/BdoTimers/releases/tag/v1.0.100"));
    static string Release(string tag, bool draft = false, bool prerelease = false, string? page = null) =>
        JsonSerializer.Serialize(new { tag_name = tag, draft, prerelease, html_url = page ?? "https://github.com/feluminais/BdoTimers/releases/tag/" + Uri.EscapeDataString(tag) });
    static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    static HttpClient Http(Func<HttpRequestMessage, HttpResponseMessage> respond) => new(new StubHandler(respond));

    sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }

    sealed class StubSource(Func<CancellationToken, Task<ReleaseInfo?>> read) : IReleaseSource
    {
        public Task<ReleaseInfo?> LatestStableAsync(CancellationToken cancel) => read(cancel);
    }

    sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    }

    sealed class CheckFixture : IDisposable
    {
        readonly TempDir _temp = new();
        readonly IReleaseSource _source;
        readonly string _installed;
        readonly bool _automatic;
        readonly TimeSpan? _timeout;
        readonly JsonFileStore<UpdateCheckState> _file;
        public FakeClock Clock { get; } = new();
        public PersistentState<UpdateCheckState> State { get; }
        public UpdateCheckState SavedState => _file.Load().Value;
        public UpdateChecker Checker { get; }

        public CheckFixture(IReleaseSource source, string installed = "1.0.42", bool automatic = true, TimeSpan? timeout = null)
        {
            _source = source; _installed = installed; _automatic = automatic; _timeout = timeout;
            _file = new(Path.Combine(_temp.Path, "update-check.json"), () => new());
            State = new(_file, _file.Load().Value);
            Checker = new(source, installed, Clock, State, automatic, timeout);
        }

        public UpdateChecker CreateChecker() => new(_source, _installed, Clock,
            new PersistentState<UpdateCheckState>(_file, _file.Load().Value), _automatic, _timeout);
        public void Dispose() => _temp.Dispose();
    }
}
