using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Playwright;
using Xunit;

namespace Metalpol.Complaints.Tests;

public sealed class UiE2ETests
{
    [Fact]
    public async Task ControlCenterLoadsBusinessStoryInitialStateAndScenarioNavigation()
    {
        await using var app = await TestApiApp.StartAsync();
        await using var browser = await PlaywrightUiSession.StartAsync();
        var page = await browser.OpenAsync(app.BaseUrl);

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Metalpol Complaint Automation Control Center" }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("#healthBadge")).ToContainTextAsync("API OK");
        await Assertions.Expect(page.Locator("#dashboard")).ToContainTextAsync("Problem biznesowy");
        await Assertions.Expect(page.Locator("#dashboard")).ToContainTextAsync("opóźnione maile");
        await Assertions.Expect(page.Locator("#dashboard")).ToContainTextAsync("mock AI triage");
        await Assertions.Expect(page.Locator("#architecture")).ToContainTextAsync("AI obsługuje nieustrukturyzowany język");
        await Assertions.Expect(page.Locator("#architecture")).ToContainTextAsync("deterministycznymi mockami");
        await Assertions.Expect(page.Locator("#dashboardKpis")).ToContainTextAsync("Reklamacje razem");
        await Assertions.Expect(page.Locator("#dashboardKpis")).ToContainTextAsync("Do review teraz");
        await Assertions.Expect(page.Locator("#scenarioSelect")).ToContainTextAsync("Happy path: wada wizualna");
        await Assertions.Expect(page.Locator("#complaintSummary")).ToContainTextAsync("Brak wybranej reklamacji");
        await Assertions.Expect(page.Locator("#nextActionHint")).ToContainTextAsync("następny najlepszy krok");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("Timeline pojawi się po intake reklamacji");
        await Assertions.Expect(page.Locator("[data-review-decision='ConfirmDefect']")).ToBeDisabledAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Reset demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task HappyPathCanBeProcessedAndConfirmedWithCorrectionFromUi()
    {
        await using var app = await TestApiApp.StartAsync();
        await using var browser = await PlaywrightUiSession.StartAsync();
        var page = await browser.OpenAsync(app.BaseUrl);

        await ProcessScenarioAsync(page, "happy-path-visual-defect", "CMP-SCENARIO-HAPPY-VISUAL-DEFECT");

        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("ResponseDrafted");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("Visual");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("ORDER-1001");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("BATCH-1001");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("0.90");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("COMPLAINT-1001");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("EmailReceived");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("ComplaintParsed");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("OrderVerified");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("BatchVerified");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("JiraComplaintCreated");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("ResponseDrafted");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("Intake");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("AI triage");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("Walidacja biznesowa");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("Jira i draft");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("Audit log");

        await page.Locator("[data-review-decision='ConfirmDefect']").ClickAsync();

        await Assertions.Expect(page.Locator("#reviewResult")).ToContainTextAsync("CorrectionCreated");
        await Assertions.Expect(page.Locator("#reviewResult")).ToContainTextAsync("CORRECTION-2001");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("CorrectionCreated");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("CORRECTION-2001");
        await Assertions.Expect(page.Locator("#nextActionHint")).ToContainTextAsync("mock Jira Correction");
        await Assertions.Expect(page.Locator("[data-review-decision='ConfirmDefect']")).ToBeDisabledAsync();
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("HumanReviewCompleted");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("CorrectionTicketCreated");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("Quality action");
        await Assertions.Expect(page.Locator("#kpiDetails")).ToContainTextAsync("Correction tickets");
        await Assertions.Expect(page.Locator("#kpiDetails")).ToContainTextAsync("1");
    }

    [Fact]
    public async Task HumanReviewDecisionsRequestMoreInfoAndRejectComplaintFromUi()
    {
        await using var app = await TestApiApp.StartAsync();
        await using var browser = await PlaywrightUiSession.StartAsync();
        var page = await browser.OpenAsync(app.BaseUrl);

        await ProcessScenarioAsync(page, "missing-order-number", "CMP-SCENARIO-MISSING-ORDER-NUMBER");

        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("HumanReviewRequired");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("orderNumber");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("Missing required fields");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("HumanReviewRequested");

        await page.Locator("[data-review-decision='RequestMoreInfo']").ClickAsync();

        await Assertions.Expect(page.Locator("#reviewResult")).ToContainTextAsync("MissingData");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("MissingData");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("CustomerClarificationRequested");

        await ProcessScenarioAsync(page, "prompt-injection-attempt", "CMP-SCENARIO-PROMPT-INJECTION-ATTEMPT");

        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("HumanReviewRequired");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("Prompt injection");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("true");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("HumanReviewRequested");

        await page.Locator("[data-review-decision='RejectComplaint']").ClickAsync();

        await Assertions.Expect(page.Locator("#reviewResult")).ToContainTextAsync("Closed");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("Closed");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("ComplaintClosed");
    }

    [Fact]
    public async Task RiskScenariosShowExpectedHumanReviewAndKpiSignalsFromUi()
    {
        await using var app = await TestApiApp.StartAsync();
        await using var browser = await PlaywrightUiSession.StartAsync();
        var page = await browser.OpenAsync(app.BaseUrl);

        await ProcessScenarioAsync(page, "dimensional-defect-low-confidence", "CMP-SCENARIO-DIMENSIONAL-LOW-CONFIDENCE");

        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("HumanReviewRequired");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("Dimensional");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("0.70");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("AI confidence below threshold");

        await ProcessScenarioAsync(page, "sap-order-not-found", "CMP-SCENARIO-SAP-ORDER-NOT-FOUND");

        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("HumanReviewRequired");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("ORDER-9999");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("SAP order not found.");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("HumanReviewRequested");
        await Assertions.Expect(page.Locator("#timeline")).Not.ToContainTextAsync("JiraComplaintCreated");

        await ProcessScenarioAsync(page, "logistics-complaint", "CMP-SCENARIO-LOGISTICS-COMPLAINT");

        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("ResponseDrafted");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("Logistics");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("JiraComplaintCreated");

        await Assertions.Expect(page.Locator("#dashboardKpis")).ToContainTextAsync("Reklamacje razem");
        await Assertions.Expect(page.Locator("#dashboardKpis")).ToContainTextAsync("3");
        await Assertions.Expect(page.Locator("#dashboardKpis")).ToContainTextAsync("Low confidence");
        await Assertions.Expect(page.Locator("#kpiDetails")).ToContainTextAsync("Dimensional: 1");
        await Assertions.Expect(page.Locator("#kpiDetails")).ToContainTextAsync("Logistics: 1");
    }

    [Fact]
    public async Task DuplicateAndMaterialCorrectionPathsStayIdempotentFromUi()
    {
        await using var app = await TestApiApp.StartAsync();
        await using var browser = await PlaywrightUiSession.StartAsync();
        var page = await browser.OpenAsync(app.BaseUrl);

        await ProcessScenarioAsync(page, "happy-path-visual-defect", "CMP-SCENARIO-HAPPY-VISUAL-DEFECT");
        await page.Locator("#scenarioSelect").SelectOptionAsync(new[] { "duplicate-message" });
        await Assertions.Expect(page.Locator("#scenarioInfo")).ToContainTextAsync("Najpierw uruchom happy path");
        await ProcessScenarioAsync(page, "duplicate-message", "CMP-SCENARIO-HAPPY-VISUAL-DEFECT");

        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("DuplicateLinked");
        await Assertions.Expect(page.Locator("#timeline .audit-list").GetByText("JiraComplaintCreated")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("COMPLAINT-1001");

        await ProcessScenarioAsync(page, "material-defect-requires-correction", "CMP-SCENARIO-MATERIAL-DEFECT-CORRECTION");

        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("Material");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("ResponseDrafted");

        await page.Locator("[data-review-decision='ConfirmDefect']").ClickAsync();

        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("CorrectionCreated");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("CORRECTION-2001");
        await Assertions.Expect(page.Locator("#timeline")).ToContainTextAsync("CorrectionTicketCreated");
    }

    [Fact]
    public async Task StaleSavedComplaintAfterApiRestartDoesNotShowBlockingError()
    {
        await using var app = await TestApiApp.StartAsync();
        await using var browser = await PlaywrightUiSession.StartAsync();
        var page = await browser.OpenAsync(app.BaseUrl, "CMP-NOT-IN-MEMORY");

        await Assertions.Expect(page.Locator("#errorBanner")).ToBeHiddenAsync();
        await Assertions.Expect(page.Locator("#selectedComplaintBadge")).ToContainTextAsync("Brak wybranej reklamacji");
        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("Krok 1");

        await ProcessScenarioAsync(page, "happy-path-visual-defect", "CMP-SCENARIO-HAPPY-VISUAL-DEFECT");
    }

    [Fact]
    public async Task ResetDemoClearsTerminalComplaintAndAllowsFreshReviewFlow()
    {
        await using var app = await TestApiApp.StartAsync();
        await using var browser = await PlaywrightUiSession.StartAsync();
        var page = await browser.OpenAsync(app.BaseUrl);

        await ProcessScenarioAsync(page, "happy-path-visual-defect", "CMP-SCENARIO-HAPPY-VISUAL-DEFECT");
        await page.Locator("[data-review-decision='ConfirmDefect']").ClickAsync();

        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("CorrectionCreated");
        await Assertions.Expect(page.Locator("[data-review-decision='ConfirmDefect']")).ToBeDisabledAsync();
        await Assertions.Expect(page.Locator("#nextActionHint")).ToContainTextAsync("Reset demo");

        await page.GetByRole(AriaRole.Button, new() { Name = "Reset demo" }).ClickAsync();

        await Assertions.Expect(page.Locator("#complaintSummary")).ToContainTextAsync("Brak wybranej reklamacji");
        await Assertions.Expect(page.Locator("#dashboardKpis")).ToContainTextAsync("Reklamacje razem");
        await Assertions.Expect(page.Locator("#dashboardKpis")).ToContainTextAsync("0");

        await ProcessScenarioAsync(page, "happy-path-visual-defect", "CMP-SCENARIO-HAPPY-VISUAL-DEFECT");

        await Assertions.Expect(page.Locator("#complaintDetails")).ToContainTextAsync("ResponseDrafted");
        await Assertions.Expect(page.Locator("[data-review-decision='ConfirmDefect']")).ToBeEnabledAsync();
    }

    private static async Task ProcessScenarioAsync(IPage page, string scenarioId, string expectedComplaintId)
    {
        await page.Locator("#scenarioSelect").SelectOptionAsync(new[] { scenarioId });
        await Assertions.Expect(page.Locator("#scenarioInfo")).ToContainTextAsync($"{scenarioId}.json");

        await page.GetByRole(AriaRole.Button, new() { Name = "Przetwórz scenariusz" }).ClickAsync();

        await Assertions.Expect(page.Locator("#complaintDetails"))
            .ToContainTextAsync(expectedComplaintId, new() { Timeout = 10_000 });
        await Assertions.Expect(page.Locator("#selectedComplaintBadge"))
            .ToContainTextAsync(expectedComplaintId);
    }

    private sealed class PlaywrightUiSession : IAsyncDisposable
    {
        private readonly IPlaywright _playwright;
        private readonly IBrowser _browser;

        private PlaywrightUiSession(IPlaywright playwright, IBrowser browser)
        {
            _playwright = playwright;
            _browser = browser;
        }

        public static async Task<PlaywrightUiSession> StartAsync()
        {
            try
            {
                var playwright = await Playwright.CreateAsync();
                var browser = await playwright.Chromium.LaunchAsync(new()
                {
                    Headless = true
                });

                return new PlaywrightUiSession(playwright, browser);
            }
            catch (PlaywrightException exception) when (exception.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Playwright Chromium is not installed. Run: pwsh tests/Metalpol.Complaints.Tests/bin/Debug/net10.0/playwright.ps1 install chromium",
                    exception);
            }
        }

        public async Task<IPage> OpenAsync(string baseUrl, string? savedComplaintId = null)
        {
            var context = await _browser.NewContextAsync(new()
            {
                BaseURL = baseUrl,
                ViewportSize = new ViewportSize
                {
                    Width = 1440,
                    Height = 1100
                }
            });

            if (!string.IsNullOrWhiteSpace(savedComplaintId))
            {
                await context.AddInitScriptAsync(
                    $"localStorage.setItem('metalpol.selectedComplaintId', '{savedComplaintId}');");
            }

            var page = await context.NewPageAsync();
            await page.GotoAsync("/", new() { WaitUntil = WaitUntilState.NetworkIdle });

            return page;
        }

        public async ValueTask DisposeAsync()
        {
            await _browser.DisposeAsync();
            _playwright.Dispose();
        }
    }

    private sealed class TestApiApp : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly StringBuilder _logs = new();

        private TestApiApp(Process process, string baseUrl)
        {
            _process = process;
            BaseUrl = baseUrl;
        }

        public string BaseUrl { get; }

        public static async Task<TestApiApp> StartAsync()
        {
            var repoRoot = FindRepoRoot();
            var port = GetFreePort();
            var baseUrl = $"http://127.0.0.1:{port}";
            var apiProject = Path.Combine(repoRoot, "src", "Metalpol.Complaints.Api", "Metalpol.Complaints.Api.csproj");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --no-build --project \"{apiProject}\" --urls {baseUrl}",
                    WorkingDirectory = repoRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            var app = new TestApiApp(process, baseUrl);
            process.OutputDataReceived += (_, args) => app.AppendLog(args.Data);
            process.ErrorDataReceived += (_, args) => app.AppendLog(args.Data);

            if (!process.Start())
            {
                throw new InvalidOperationException("API process did not start.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await app.WaitForHealthAsync();

            return app;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }

            _process.Dispose();
        }

        private async Task WaitForHealthAsync()
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(1)
            };
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            while (!timeout.IsCancellationRequested)
            {
                if (_process.HasExited)
                {
                    throw new InvalidOperationException($"API process exited before /health was ready.{Environment.NewLine}{_logs}");
                }

                try
                {
                    var response = await client.GetAsync($"{BaseUrl}/health", timeout.Token);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return;
                    }
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                }

                await Task.Delay(200, CancellationToken.None);
            }

            throw new TimeoutException($"API did not become healthy at {BaseUrl}.{Environment.NewLine}{_logs}");
        }

        private void AppendLog(string? line)
        {
            if (line is null)
            {
                return;
            }

            lock (_logs)
            {
                _logs.AppendLine(line);
            }
        }

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Metalpol.Complaints.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root with Metalpol.Complaints.sln.");
        }

        private static int GetFreePort()
        {
            while (true)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();

                if (!IsChromiumBlockedPort(port))
                {
                    return port;
                }
            }
        }

        private static bool IsChromiumBlockedPort(int port)
        {
            int[] blocked =
            {
                1, 7, 9, 11, 13, 15, 17, 19, 20, 21, 22, 23, 25, 37, 42, 43, 53, 69, 77, 79,
                87, 95, 101, 102, 103, 104, 109, 110, 111, 113, 115, 117, 119, 123, 135,
                137, 139, 143, 161, 179, 389, 427, 465, 512, 513, 514, 515, 526, 530, 531,
                532, 540, 548, 554, 556, 563, 587, 601, 636, 989, 990, 993, 995, 1719,
                1720, 1723, 2049, 3659, 4045, 5060, 5061, 6000, 6566, 6697, 10080
            };

            return blocked.Contains(port) || port is >= 6665 and <= 6669;
        }
    }
}
