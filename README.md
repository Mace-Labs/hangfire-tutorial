# GamingSeeds Hangfire Demo

A runnable .NET 10 sample app that demonstrates the four core [Hangfire](https://www.hangfire.io/) job types — **fire-and-forget**, **delayed**, **recurring**, and **continuation** — plus automatic retries and the dashboard.

It accompanies the video *"Hangfire Tutorial in .NET 10: Full Setup, 4 Job Types, and the Dashboard Explained"*. The scenarios (confirmation emails, account cleanup, image processing) are the real ones running on [GamingSeeds.com](https://www.gamingseeds.com).

Every job type is **shown running**: a small Bootstrap UI at `/` has one tab per concept — trigger the scenario and watch the side effects live (users, an email outbox, an image-processing pipeline), while the Hangfire dashboard at `/hangfire` shows the job states.

![Job types](https://img.shields.io/badge/job%20types-4-blue) ![.NET](https://img.shields.io/badge/.NET-10-512BD4)

---

## Run it

You need the [.NET 10 SDK](https://dotnet.microsoft.com/) and [Docker](https://www.docker.com/).

```bash
# 1. Start SQL Server (Hangfire's persistent job store)
docker compose up -d

# 2. Run the app
dotnet run --project src/GamingSeeds.Hangfire.Demo
```

Then open:

- **http://localhost:5180/** — the demo UI (one tab per job type)
- **http://localhost:5180/hangfire** — the Hangfire dashboard

Hangfire creates its own tables on first run; the app creates its `Users`/`SentEmails` tables via `EnsureCreated`. Both live in the `GamingSeedsHangfireDemo` database.

> Running SQL Server in a container? See my video [.NET Docker Tutorial: Containerize a .NET and SQL Server App (Step by Step)](https://youtu.be/sl9gH8HU0qA) for a full walkthrough.

---

## Follow along, chapter by chapter

The repo has a **git tag per video chapter**, so you can check out exactly the code state each chapter starts from:

| Tag | State |
|---|---|
| `00-starting-point` | The app before Hangfire: users register and the confirmation email is sent **inline during the request** (the slow way) |
| `01-setup` | Hangfire installed: packages, SQL Server storage, server, dashboard |
| `02-fire-and-forget` | Confirmation email moved to a background job via `Enqueue` |
| `03-delayed` | Reminder email scheduled with `Schedule`; users can confirm |
| `04-recurring` | Daily `cleanup-unconfirmed-users` job |
| `05-continuations` | Image pipeline: process → thumbnail → optimize |
| `06-retries` | Simulated API outage + `[AutomaticRetry]` (identical to `main`) |

```bash
git checkout 02-fire-and-forget   # code exactly as it is in that chapter
```

---

## How it's built (code-along)

### 1. Install the packages

```bash
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.SqlServer
```

`Hangfire.AspNetCore` is the core integration; `Hangfire.SqlServer` is the storage provider that persists jobs so they survive restarts.

### 2. Configure Hangfire + storage, start the server, enable the dashboard

In [Program.cs](src/GamingSeeds.Hangfire.Demo/Program.cs):

```csharp
builder.Services.AddHangfire(config => config.UseSqlServerStorage(
    builder.Configuration.GetConnectionString("HangfireConnection"),
    new SqlServerStorageOptions { QueuePollInterval = TimeSpan.FromSeconds(1) }));

builder.Services.AddHangfireServer(options =>
    options.SchedulePollingInterval = TimeSpan.FromSeconds(1));
```

```csharp
app.UseHangfireDashboard();
```

Both poll intervals are lowered to **1 second so jobs, delays, and retries fire promptly on camera** — the 15-second defaults are fine in production.

> The dashboard only answers **local requests by default**, which is why the demo needs no auth. To expose it remotely, add an `IDashboardAuthorizationFilter`.

### 3. Define jobs as DI-resolved classes — not static methods

Most Hangfire examples show `BackgroundJob.Enqueue(() => SendEmail(id))`, which forces a **static** method. Static methods can't take constructor-injected dependencies, and Hangfire is built to resolve a job **instance** from the DI container in a fresh scope per execution. So define jobs behind interfaces and register them:

```csharp
public interface IEmailJobs
{
    Task SendConfirmationEmail(int userId);
}

public class EmailJobs(AppDbContext db, ILogger<EmailJobs> logger) : IEmailJobs
{
    public async Task SendConfirmationEmail(int userId) { /* uses injected db */ }
}

builder.Services.AddScoped<IEmailJobs, EmailJobs>();
```

Now the scoped `AppDbContext`, logger, and any other service are injected for free.

### 4. Enqueue through `IBackgroundJobClient`, not the static facade

The static `BackgroundJob.Enqueue(...)` works, but injecting `IBackgroundJobClient` keeps your endpoints and services **unit-testable** (you can mock the client). Same story for recurring jobs with `IRecurringJobManager`. See [UserEndpoints.cs](src/GamingSeeds.Hangfire.Demo/Endpoints/UserEndpoints.cs):

```csharp
// Fire-and-forget: the slow SMTP round-trip is moved off the request path.
jobs.Enqueue<IEmailJobs>(x => x.SendConfirmationEmail(user.Id));

// Delayed: nudge the user later if they still haven't confirmed.
jobs.Schedule<IEmailJobs>(x => x.SendConfirmationReminder(user.Id), options.Value.ConfirmationReminderDelay);
```

### The four job types

| Type | API | Where in this app |
|---|---|---|
| **Fire-and-forget** | `jobs.Enqueue<IEmailJobs>(x => x.SendConfirmationEmail(id))` | `POST /users/register` |
| **Delayed** | `jobs.Schedule<IEmailJobs>(x => x.SendConfirmationReminder(id), delay)` | `POST /users/register` |
| **Recurring** | `recurring.AddOrUpdate<IUserMaintenanceJobs>("cleanup-unconfirmed-users", x => x.DeleteUnconfirmedUsers(), Cron.Daily)` | startup |
| **Continuation** | `jobs.ContinueJobWith<IImageProcessingJobs>(prevId, x => x.GenerateThumbnails(id))` | `POST /images/upload` |

---

## Best practices this app demonstrates

- **Jobs are DI classes, not static methods** — so they can inject scoped services like `DbContext`.
- **Enqueue via `IBackgroundJobClient` / `IRecurringJobManager`** — testable instead of the static `BackgroundJob` facade.
- **Pass IDs, not entities.** Job arguments are serialized to JSON and stored. Pass a `userId` and load the entity fresh inside the job — never a whole `User` object.
- **Make jobs idempotent.** A job can run more than once (retries). `SendConfirmationReminder` no-ops if the user already confirmed.
- **Put job-filter attributes on the *interface* method.** When you enqueue via `Enqueue<IFlakyJobs>(...)`, Hangfire reads `[AutomaticRetry]` from the **interface** method, not the implementation. Put it on the class and it's silently ignored (you'll get the default 10 retries).

```csharp
public interface IFlakyJobs
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = [3, 6, 9])]   // <-- here, on the interface
    Task CallUnreliableApi();
}
```

---

## Try each scenario

| Tab | What to do | What to watch |
|---|---|---|
| **Fire-and-forget** | Click *Register — inline send*, then *Register — background job* | The elapsed-ms chips: ~2,000&nbsp;ms vs a few dozen ms. The email still lands in the **Outbox** a moment later |
| **Delayed** | Register a user; confirm one before its reminder fires | The reminder waits in the dashboard's **Scheduled** tab (due "in a few seconds"); confirmed users' reminders no-op |
| **Recurring** | *Seed stale accounts*, then *Trigger now* on `cleanup-unconfirmed-users` in the dashboard | The 30-day-old accounts vanish; recent users survive the 7-day cutoff |
| **Continuations** | *Upload seed image* | The pipeline fills **Source → Processed → Thumbnail → Optimized**, one step at a time; two jobs wait in the dashboard's **Awaiting** state |
| **Retries** | *Take the API down*, then *Call unreliable API*. After it fails, bring the API up and hit *Retry* in the dashboard | Retries after 3, 6, 9 seconds → **Failed** with full exception → manual retry goes green |

The **Reset demo data** button wipes users, emails, and generated images between takes (Hangfire's own job history is untouched).

Delays are shortened for filming — see `DemoOptions` in [appsettings.json](src/GamingSeeds.Hangfire.Demo/appsettings.json) (`ConfirmationReminderDelay`, `SimulatedWorkDuration`). In production these would be `TimeSpan.FromHours(24)` and `Cron.Daily`.

---

## Production next steps (out of scope here)

- Real SMTP for emails and a real object store for images.
- An `IDashboardAuthorizationFilter` so the `/hangfire` dashboard can be exposed beyond localhost.
- Failure alerting on top of the dashboard.
- Named queues and multiple servers for scaling.
