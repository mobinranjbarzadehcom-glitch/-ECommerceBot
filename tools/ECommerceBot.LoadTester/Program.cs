// ECommerceBot Load Tester v1.0.0
// Simulates bot webhook traffic without calling real Telegram.
// Usage: dotnet run -- --url https://localhost:8080 --users 100 --scenario start

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

var url = "http://localhost:8080";
var users = 100;
var scenario = "start";
var secret = string.Empty;
var concurrency = 10;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--url":      url         = args[++i]; break;
        case "--users":    users       = int.Parse(args[++i]); break;
        case "--scenario": scenario    = args[++i]; break;
        case "--secret":   secret      = args[++i]; break;
        case "--parallel": concurrency = int.Parse(args[++i]); break;
        case "--help":
            PrintHelp();
            return;
    }
}

Console.WriteLine("╔══════════════════════════════════════════════╗");
Console.WriteLine("║       ECommerceBot Load Tester v1.0.0        ║");
Console.WriteLine("╚══════════════════════════════════════════════╝");
Console.WriteLine($"  Target  : {url}/api/telegram/webhook");
Console.WriteLine($"  Users   : {users}");
Console.WriteLine($"  Scenario: {scenario}");
Console.WriteLine($"  Parallel: {concurrency}");
Console.WriteLine();

var runner = new LoadTestRunner(url, secret, concurrency);

var result = scenario switch
{
    "start"    => await runner.RunAsync(users, Scenarios.Start),
    "browse"   => await runner.RunAsync(users, Scenarios.Browse),
    "order"    => await runner.RunAsync(users, Scenarios.Order),
    "ticket"   => await runner.RunAsync(users, Scenarios.Ticket),
    "all"      => await runner.RunAllAsync(users),
    _          => throw new ArgumentException($"Unknown scenario: {scenario}. Valid: start, browse, order, ticket, all")
};

result.Print();

static void PrintHelp()
{
    Console.WriteLine("""
    ECommerceBot Load Tester — simulates Telegram webhook traffic

    Usage:
      dotnet run -- [options]

    Options:
      --url       <url>      Base URL of the API (default: http://localhost:8080)
      --users     <n>        Number of simulated users (default: 100)
      --scenario  <name>     Scenario to run: start | browse | order | ticket | all
      --secret    <token>    Webhook secret token (X-Telegram-Bot-Api-Secret-Token header)
      --parallel  <n>        Max concurrent requests (default: 10)
      --help                 Show this help

    Scenarios:
      start    - /start command flow (user registration + menu)
      browse   - Category and product browsing
      order    - Full order creation flow (product → playerId → receipt)
      ticket   - Support ticket creation
      all      - Run all scenarios sequentially

    Examples:
      dotnet run -- --url https://bot.yourdomain.com --users 100 --scenario start
      dotnet run -- --url http://localhost:8080 --users 500 --scenario all --parallel 25
    """);
}

// ── Result ────────────────────────────────────────────────────────────────────

class LoadTestResult
{
    public string Scenario { get; init; } = string.Empty;
    public int TotalRequests { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<double> LatenciesMs { get; } = new();

    public double AvgLatencyMs => LatenciesMs.Count == 0 ? 0 : LatenciesMs.Average();
    public double MaxLatencyMs => LatenciesMs.Count == 0 ? 0 : LatenciesMs.Max();
    public double P95LatencyMs => Percentile(95);
    public double SuccessRate => TotalRequests == 0 ? 0 : (double)Succeeded / TotalRequests * 100;

    private double Percentile(int pct)
    {
        if (LatenciesMs.Count == 0) return 0;
        var sorted = LatenciesMs.Order().ToList();
        var idx = (int)Math.Ceiling(pct / 100.0 * sorted.Count) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
    }

    public void Print()
    {
        Console.WriteLine($"\n── {Scenario} Results ─────────────────────────────");
        Console.WriteLine($"  Total requests : {TotalRequests}");
        Console.WriteLine($"  Succeeded      : {Succeeded} ({SuccessRate:F1}%)");
        Console.WriteLine($"  Failed         : {Failed}");
        Console.WriteLine($"  Avg latency    : {AvgLatencyMs:F1} ms");
        Console.WriteLine($"  P95 latency    : {P95LatencyMs:F1} ms");
        Console.WriteLine($"  Max latency    : {MaxLatencyMs:F1} ms");

        if (SuccessRate >= 99)
            Console.WriteLine("  Result         : ✅ PASS");
        else if (SuccessRate >= 95)
            Console.WriteLine("  Result         : ⚠️  DEGRADED");
        else
            Console.WriteLine("  Result         : ❌ FAIL");
    }
}

// ── Runner ────────────────────────────────────────────────────────────────────

class LoadTestRunner
{
    private readonly string _baseUrl;
    private readonly string _secret;
    private readonly int _concurrency;
    private readonly HttpClient _http;

    public LoadTestRunner(string baseUrl, string secret, int concurrency)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _secret = secret;
        _concurrency = concurrency;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        if (!string.IsNullOrEmpty(secret))
            _http.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", secret);
    }

    public async Task<LoadTestResult> RunAllAsync(int users)
    {
        var combined = new LoadTestResult { Scenario = "all" };

        var scenarios = new (string Name, Func<int, object> Scenario)[]
        {
            ("start",  Scenarios.Start),
            ("browse", Scenarios.Browse),
            ("order",  Scenarios.Order),
            ("ticket", Scenarios.Ticket),
        };
        foreach (var (name, scenario) in scenarios)
        {
            Console.Write($"  Running {name,-8}... ");
            var r = await RunAsync(users, scenario);
            combined.TotalRequests += r.TotalRequests;
            combined.Succeeded += r.Succeeded;
            combined.Failed += r.Failed;
            combined.LatenciesMs.AddRange(r.LatenciesMs);
            Console.WriteLine($"{r.Succeeded}/{r.TotalRequests} ok  avg={r.AvgLatencyMs:F0}ms  p95={r.P95LatencyMs:F0}ms");
        }

        return combined;
    }

    public async Task<LoadTestResult> RunAsync(int users, Func<int, object> scenarioFactory)
    {
        var result = new LoadTestResult { Scenario = scenarioFactory(0).GetType().Name };
        var semaphore = new SemaphoreSlim(_concurrency);
        var tasks = Enumerable.Range(1, users).Select(async userId =>
        {
            await semaphore.WaitAsync();
            try
            {
                var update = scenarioFactory(userId);
                var sw = Stopwatch.StartNew();
                try
                {
                    var json = JsonSerializer.Serialize(update);
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await _http.PostAsync($"{_baseUrl}/api/telegram/webhook", content);
                    sw.Stop();

                    lock (result)
                    {
                        result.TotalRequests++;
                        result.LatenciesMs.Add(sw.Elapsed.TotalMilliseconds);
                        if (response.StatusCode == HttpStatusCode.OK)
                            result.Succeeded++;
                        else
                            result.Failed++;
                    }
                }
                catch
                {
                    sw.Stop();
                    lock (result)
                    {
                        result.TotalRequests++;
                        result.Failed++;
                        result.LatenciesMs.Add(sw.Elapsed.TotalMilliseconds);
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return result;
    }
}

// ── Scenarios — Telegram Update payloads ─────────────────────────────────────

static class Scenarios
{
    // /start message
    public static object Start(int userId) => new
    {
        update_id = 1000 + userId,
        message = new
        {
            message_id = userId,
            date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            from = new { id = 90000 + userId, is_bot = false, first_name = $"TestUser{userId}", language_code = "fa" },
            chat = new { id = 90000 + userId, type = "private" },
            text = "/start"
        }
    };

    // Category browse (callback: menu:products)
    public static object Browse(int userId) => new
    {
        update_id = 2000 + userId,
        callback_query = new
        {
            id = $"browse_{userId}",
            from = new { id = 90000 + userId, is_bot = false, first_name = $"TestUser{userId}", language_code = "fa" },
            message = new
            {
                message_id = 1,
                date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                chat = new { id = 90000 + userId, type = "private" }
            },
            data = "menu:products"
        }
    };

    // Order flow start (callback: prod:1)
    public static object Order(int userId) => new
    {
        update_id = 3000 + userId,
        callback_query = new
        {
            id = $"order_{userId}",
            from = new { id = 90000 + userId, is_bot = false, first_name = $"TestUser{userId}", language_code = "fa" },
            message = new
            {
                message_id = 2,
                date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                chat = new { id = 90000 + userId, type = "private" }
            },
            data = "prod:1"
        }
    };

    // Support ticket creation
    public static object Ticket(int userId) => new
    {
        update_id = 4000 + userId,
        message = new
        {
            message_id = userId + 100,
            date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            from = new { id = 90000 + userId, is_bot = false, first_name = $"TestUser{userId}", language_code = "fa" },
            chat = new { id = 90000 + userId, type = "private" },
            text = "🎫 Support"
        }
    };
}
