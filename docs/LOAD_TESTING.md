# ECommerceBot — Load Testing Guide

Version: 1.0.0

---

## Overview

ECommerceBot ships with a load test tool at `tools/ECommerceBot.LoadTester` that simulates Telegram webhook traffic without calling real Telegram APIs. It sends synthetic update payloads directly to the webhook endpoint and measures response times and success rates.

---

## Prerequisites

- .NET 9 SDK installed
- ECommerceBot API running and accessible
- (Optional) `TELEGRAM_WEBHOOK_SECRET` for authenticated requests

---

## Running the Load Tester

```bash
cd tools/ECommerceBot.LoadTester
dotnet run -- --help
```

### Options

| Flag | Default | Description |
|------|---------|-------------|
| `--url` | `http://localhost:8080` | Base URL of the API |
| `--users` | `100` | Number of simulated users |
| `--scenario` | `start` | Scenario: `start`, `browse`, `order`, `ticket`, `all` |
| `--secret` | (empty) | Webhook secret token header value |
| `--parallel` | `10` | Max concurrent requests |

---

## Scenarios

### `start` — /start command
Simulates new users sending `/start`. Tests:
- User registration (GetOrCreateUser)
- CMS welcome message lookup
- Main menu keyboard generation

### `browse` — Category browsing
Simulates users clicking "🛒 Products" (menu:products callback). Tests:
- Active category query
- Keyboard builder performance
- BotSetting cache hit rate

### `order` — Product selection
Simulates users clicking on product ID 1 (prod:1 callback). Tests:
- Product lookup (GetWithKeys)
- Available key count query
- Conversation state machine

### `ticket` — Support ticket flow
Simulates users tapping the Support button. Tests:
- Conversation state transitions
- Message text lookup from CMS

### `all` — All scenarios sequentially
Runs all 4 scenarios in sequence. Produces a combined report.

---

## Example Commands

```bash
# 100 users, start flow, local API
dotnet run -- --url http://localhost:8080 --users 100 --scenario start

# 500 users, all scenarios, with auth secret
dotnet run -- --url https://bot.yourdomain.com \
  --users 500 --scenario all --secret "your-webhook-secret" --parallel 25

# 1000 users, browse scenario only
dotnet run -- --url http://localhost:8080 --users 1000 --scenario browse --parallel 50
```

---

## Output

```
── start Results ──────────────────────────────
  Total requests : 100
  Succeeded      : 99 (99.0%)
  Failed         : 1
  Avg latency    : 23.4 ms
  P95 latency    : 45.1 ms
  Max latency    : 112.0 ms
  Result         : ✅ PASS
```

### Pass/Fail Thresholds

| Metric | Pass | Degraded | Fail |
|--------|------|----------|------|
| Success rate | ≥ 99% | 95–99% | < 95% |

---

## Recommended Test Matrix

Run these scenarios before each production deployment:

| Scenario | Users | Parallel | Expected P95 |
|----------|-------|----------|-------------|
| start | 100 | 10 | < 100 ms |
| browse | 500 | 25 | < 150 ms |
| order | 100 | 10 | < 200 ms |
| all | 100 | 10 | < 200 ms |

**Note**: Latency targets assume a local or low-latency database. Remote database connections will add 20–100ms overhead.

---

## What the Load Tester Does NOT Test

- Real Telegram API calls (bot.SendMessage, etc.) — these are skipped
- Receipt photo uploads (requires actual Telegram file IDs)
- Admin panel approval flows (require admin user in DB)
- Background service execution (order expiration, backup)
- Database migration performance

---

## Extending the Load Tester

Add new scenarios by creating a static method in the `Scenarios` class in `Program.cs`:

```csharp
public static object MyScenario(int userId) => new
{
    update_id = 5000 + userId,
    callback_query = new
    {
        id = $"my_{userId}",
        from = new { id = 90000 + userId, is_bot = false, first_name = $"User{userId}" },
        message = new { message_id = 1, date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), chat = new { id = 90000 + userId, type = "private" } },
        data = "cat:1"
    }
};
```

Then add it to the `RunAllAsync` scenarios array.

---

## Interpreting Results

| P95 Latency | Assessment |
|-------------|-----------|
| < 100 ms | Excellent |
| 100–300 ms | Good |
| 300–1000 ms | Acceptable |
| > 1000 ms | Investigate bottleneck |

Common bottlenecks:
- **Database queries** — check EF Core slow query log
- **BotSetting cache misses** — verify Redis is connected and cache is warm
- **Telegram API calls** — admin notifications slow down order creation (fire-and-forget)
- **Connection pool exhaustion** — increase `Max Pool Size` in connection string
