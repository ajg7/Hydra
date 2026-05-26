# Hydra
A concurrent web scraper and website health checker — built to learn C#/.NET multithreading, concurrency, and Blazor Server.

---

## Project Goal

Build two tools inside a single Blazor Server app:

1. **Website Health Checker** — given a list of URLs, ping each one concurrently and report status code, response time, and up/down status in real time.
2. **Web Scraper** — given a seed URL and a depth limit, crawl and scrape pages concurrently, extracting links and content.

Every phase teaches one new concurrency or Blazor concept. You write all the code — this file is your map.

---

## Tech Stack

- **.NET 10 / C#** — runtime and language
- **Blazor Server** — UI framework (components run on the server, DOM updates pushed via SignalR)
- **HttpClient** — for all HTTP requests
- **HtmlAgilityPack** (NuGet) — HTML parsing for the scraper
- **System.Threading.Channels** — producer/consumer pipeline for the scraper

---

## The 8-Phase Plan

---

### Phase 1 — Understand the Blazor Project Structure

**Goal:** Know how Blazor Server works before touching concurrency.

**What to read/do:**
- Open `Program.cs`. Notice `AddRazorComponents()` and `MapRazorComponents<App>()`. This wires up Blazor.
- Open `Components/App.razor` — this is the root. It loads the router.
- Open `Components/Pages/Counter.razor` — this is the canonical example. Read the `@code` block. Notice `@onclick` and `StateHasChanged` (called implicitly here).
- Open `Components/Layout/NavMenu.razor` — this is where navigation links live.

**Concepts to understand before moving on:**
- A Blazor Server component is a C# class + Razor markup fused together. The `@code` block is just the class body.
- When state changes, you call `StateHasChanged()` to tell Blazor to re-render.
- Blazor Server runs on the server. The browser gets a thin JS client that communicates via WebSockets (SignalR). This matters for concurrency: background threads updating UI must marshal back via `InvokeAsync`.

**Deliverable:** Delete the `Weather.razor` and `Counter.razor` pages. Add two new empty pages: `HealthChecker.razor` and `Scraper.razor`. Add nav links for both in `NavMenu.razor`. Run the app (`dotnet run`) and confirm navigation works.

---

### Phase 2 — Domain Models and Service Setup

**Goal:** Define what data looks like and how services are registered.

**Models to create** (in a `Models/` folder):

- `HealthCheckResult` — holds: `string Url`, `int? StatusCode`, `long ResponseTimeMs`, `bool IsUp`, `string? Error`
- `ScrapeResult` — holds: `string Url`, `List<string> Links`, `string? Title`, `bool Success`, `string? Error`
- `ScrapeJob` — holds: `string SeedUrl`, `int MaxDepth`, `int MaxPages`

**Services to create** (in a `Services/` folder):

- `IHealthCheckerService` with a method signature: `Task<HealthCheckResult> CheckAsync(string url, CancellationToken ct)`
- `IScraperService` with a method signature: `Task ScrapeAsync(ScrapeJob job, IProgress<ScrapeResult> progress, CancellationToken ct)`
- Create concrete implementations of both (leave the bodies throwing `NotImplementedException` for now).

**In `Program.cs`:**
- Register `HttpClient` using `builder.Services.AddHttpClient()`.
- Register your services with `builder.Services.AddSingleton<IHealthCheckerService, HealthCheckerService>()`. Think about whether Singleton, Scoped, or Transient is correct — and why. (Hint: Blazor Server scopes a new DI scope per circuit/connection.)

**Concept: Why IHttpClientFactory?**
Never `new HttpClient()` in a loop — it exhausts socket connections. `IHttpClientFactory` manages a pool. Inject `IHttpClientFactory` into your services and call `CreateClient()` per request.

---

### Phase 3 — Single URL Health Check (async/await foundations)

**Goal:** Implement one URL health check. Learn `async/await`, `Task<T>`, and `HttpClient`.

**In `HealthCheckerService`:**
- Implement `CheckAsync`. Use `HttpClient.GetAsync(url, ct)` inside a `try/catch`.
- Measure response time using `Stopwatch`.
- Populate and return a `HealthCheckResult`.
- Use `CancellationToken` in the HttpClient call — pass `ct` through.

**In `HealthChecker.razor`:**
- Add a text input bound to a `string url` field.
- Add a "Check" button that calls an `async Task Check()` method in `@code`.
- Display the result below the button.
- Mark your event handler as `async Task` not `async void` — understand why (`async void` swallows exceptions in Blazor).

**Concepts to understand:**
- `await` suspends the current method without blocking the thread. The thread is returned to the pool while waiting for I/O.
- `Task<T>` is a promise of a future value. `await` unwraps it.
- `CancellationToken` is a cooperative cancellation signal. The caller creates a `CancellationTokenSource`, passes `source.Token` to the callee, and calls `source.Cancel()` to signal cancellation. The callee checks or passes the token to I/O operations.

**Deliverable:** Single-URL check works and displays result on screen.

---

### Phase 4 — Concurrent Health Checking (Task.WhenAll + SemaphoreSlim + real-time UI)

**Goal:** Check many URLs at the same time. This is the core concurrency phase.

**New method on `IHealthCheckerService`:**
```
Task<List<HealthCheckResult>> CheckManyAsync(
    IEnumerable<string> urls,
    int maxConcurrency,
    IProgress<HealthCheckResult> progress,
    CancellationToken ct)
```

**Step 4a — Naive Task.WhenAll**

Inside `CheckManyAsync`, create a `Task<HealthCheckResult>` for every URL using LINQ `.Select()`, then `await Task.WhenAll(tasks)`.

Run it. Watch all requests fire simultaneously. This works, but if given 1000 URLs it would open 1000 simultaneous connections. That is the problem `SemaphoreSlim` solves.

**Step 4b — Add SemaphoreSlim**

`SemaphoreSlim(initialCount, maxCount)` is a lightweight semaphore safe for async use. Wrap each task in:
```
await semaphore.WaitAsync(ct);
try { ... } finally { semaphore.Release(); }
```
Set `maxConcurrency` to something like 10. Now at most 10 requests run at once.

**Concept:** `SemaphoreSlim` is the right tool for throttling async I/O. Do not use `Thread.Sleep` or blocking locks in async code.

**Step 4c — IProgress<T> for real-time UI updates**

`IProgress<T>` decouples the service from the UI. The service calls `progress.Report(result)` each time a check finishes. The UI creates the concrete `Progress<T>` object, passing a callback:
```csharp
var progress = new Progress<HealthCheckResult>(result => {
    results.Add(result);
    InvokeAsync(StateHasChanged); // REQUIRED: marshal to Blazor's sync context
});
```

**Why `InvokeAsync(StateHasChanged)`?**
Background threads are not on Blazor's synchronization context. Calling `StateHasChanged()` directly from a background thread is a race condition. `InvokeAsync` marshals the call to the correct context safely.

**Step 4d — Add a Stop button**

Add a `CancellationTokenSource? _cts` field to the component. On "Start", create `_cts = new CancellationTokenSource()`. On "Stop", call `_cts.Cancel()`. Pass `_cts.Token` to the service.

**Deliverable:** Paste a list of URLs, click Start, watch results appear one by one as they complete. Stop button cancels in-flight work.

---

### Phase 5 — Web Scraper (Channels + producer/consumer)

**Goal:** Crawl pages concurrently without revisiting URLs. Learn `System.Threading.Channels` and `ConcurrentDictionary`.

**Add NuGet package:**
```
dotnet add package HtmlAgilityPack
```

**The architecture — a producer/consumer pipeline:**

```
[Producer]          [Channel<string>]       [Consumers (N workers)]
seed URL ──────▶   unbounded channel   ──▶  worker 1: fetch + parse + enqueue new links
new links ──────▶                      ──▶  worker 2: fetch + parse + enqueue new links
                                       ──▶  worker 3: ...
```

**Step 5a — Create a Channel**

`Channel<string>` is a thread-safe async queue. Create it with `Channel.CreateUnbounded<string>()`. Write URLs to `channel.Writer`, read from `channel.Reader`.

This is the modern replacement for `BlockingCollection<T>`. It is fully async — `await channel.Reader.ReadAsync()` does not block a thread.

**Step 5b — Track visited URLs**

Use `ConcurrentDictionary<string, byte>` as a thread-safe HashSet. Before enqueuing a URL, try to add it: `visited.TryAdd(url, 0)`. If it returns false, the URL was already seen — skip it.

**Concept:** `Dictionary<K,V>` is not thread-safe. Two threads adding at the same time corrupts internal state. `ConcurrentDictionary` uses fine-grained locking internally.

**Step 5c — Implement the workers**

Spawn N worker tasks. Each worker loops: read a URL from the channel, fetch it, parse links with HtmlAgilityPack, enqueue new links back to the channel (if within depth and not visited).

**Step 5d — Shutdown**

When does the crawl end? When the channel is empty AND all workers are idle. A clean pattern:
- Track in-flight count with `Interlocked.Increment` / `Interlocked.Decrement` on an `int` counter.
- When a worker picks up a URL, increment. When it finishes, decrement.
- If the counter hits 0 and the channel is empty, call `channel.Writer.Complete()`.
- Workers exit their loop when `channel.Reader.Completion` is done.

**Concept:** `Interlocked.Increment(ref count)` is an atomic operation — no lock needed for a simple counter.

**Step 5e — Report progress**

Call `progress.Report(scrapeResult)` from each worker after processing a page. Wire this to the UI exactly like Phase 4.

**Deliverable:** Enter a seed URL and max depth, click Scrape, watch pages appear as they are scraped. Visited URLs are never re-fetched.

---

### Phase 6 — Thread Safety Deep Dive

**Goal:** Understand what goes wrong without proper synchronization, and why the tools from Phases 4–5 work.

**Experiments to try (before Phase 7):**

1. Replace `ConcurrentDictionary` with a plain `Dictionary` and run the scraper with high concurrency. Observe the exception or corrupted state.
2. Call `StateHasChanged()` directly from a background thread instead of `InvokeAsync`. Observe the error.
3. Use a shared `List<T>` across worker threads without locking. Add items from multiple threads. Observe data loss.

**The right tools for each scenario:**

| Problem | Tool |
|---|---|
| Shared counter across threads | `Interlocked.Increment/Decrement` |
| Thread-safe key lookup/insert | `ConcurrentDictionary<K,V>` |
| Thread-safe queue | `ConcurrentQueue<T>` or `Channel<T>` |
| Protecting a critical section | `lock(obj) { }` or `SemaphoreSlim` |
| Limiting concurrent async work | `SemaphoreSlim(n, n)` |
| Marshaling to Blazor's context | `InvokeAsync(StateHasChanged)` |

**Concept — lock vs SemaphoreSlim:**
`lock` is synchronous and blocks the thread. Never use `lock` around an `await`. Use `SemaphoreSlim` when you need an async-compatible mutex.

---

### Phase 7 — Cancellation and Timeout

**Goal:** Make cancellation robust. Add per-request timeouts.

**Per-request timeout:**
Combine a `CancellationTokenSource` timeout with the caller's token using `CancellationTokenSource.CreateLinkedTokenSource(callerCt, timeoutCts.Token)`. This fires if either the timeout expires or the user clicks Stop.

```csharp
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
// use linked.Token for the HttpClient call
```

**Cooperative cancellation pattern:**
Services should check `ct.IsCancellationRequested` at loop boundaries, not just pass it to I/O calls. If the channel has many items queued, check for cancellation before dequeuing each one.

**OperationCanceledException:**
When a cancellation token fires, I/O operations throw `OperationCanceledException`. Catch it separately from other exceptions — it is not an error, it is a clean stop.

**Deliverable:** Stop button stops work cleanly. Individual requests that take too long are abandoned after 10 seconds. The UI reflects partial results collected before stopping.

---

### Phase 8 — Polish and UI

**Goal:** Make the app actually usable and solid.

**Health Checker page:**
- Textarea input for multiple URLs (one per line).
- Table showing each result: URL, status code, response time, up/down badge.
- Progress bar showing X of N checked.
- Start / Stop buttons (disable Start while running, disable Stop when idle).

**Scraper page:**
- Input for seed URL, max depth, max pages, max concurrency.
- Live feed of scraped pages as they arrive.
- Summary: total pages, total links found, elapsed time.

**Shared concerns:**
- Wrap all service calls in `try/catch`. Surface errors per-URL, not as crashes.
- Use `ILogger<T>` (already wired up in the default template) for structured logging.
- Validate inputs before starting (empty URL, invalid URL format).

**Blazor lifecycle hooks to use:**
- `OnInitializedAsync` — for any async setup when the component loads.
- `IAsyncDisposable.DisposeAsync` — cancel and clean up `CancellationTokenSource` when the user navigates away. Otherwise background work keeps running after the component is gone.

---

## Concurrency Concepts Checklist

Track these as you implement each phase:

- [ ] `async/await` and `Task<T>`
- [ ] `HttpClient` via `IHttpClientFactory`
- [ ] `Task.WhenAll` for fan-out
- [ ] `SemaphoreSlim` for async throttling
- [ ] `IProgress<T>` for background-to-UI reporting
- [ ] `InvokeAsync(StateHasChanged)` for thread-safe UI updates
- [ ] `CancellationTokenSource` and `CancellationToken`
- [ ] Linked cancellation tokens (timeout + user cancel)
- [ ] `ConcurrentDictionary` for thread-safe shared state
- [ ] `Channel<T>` for async producer/consumer pipelines
- [ ] `Interlocked` for atomic counters
- [ ] `lock` vs `SemaphoreSlim` — when to use each
- [ ] `IAsyncDisposable` for component cleanup

---

## Running the App

```bash
cd Hydra
dotnet run
```

Navigate to `https://localhost:<port>` shown in the terminal.

## Adding NuGet Packages

```bash
dotnet add package HtmlAgilityPack
```
