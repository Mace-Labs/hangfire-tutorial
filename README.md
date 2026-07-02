# GamingSeeds Hangfire Demo

A runnable .NET 10 sample app that demonstrates the four core [Hangfire](https://www.hangfire.io/) job types — **fire-and-forget**, **delayed**, **recurring**, and **continuation** — plus automatic retries and the dashboard.

It accompanies the video *"Hangfire Tutorial in .NET 10: Full Setup, 4 Job Types, and the Dashboard Explained"*. The scenarios (confirmation emails, account cleanup, image processing) are the real ones running on [GamingSeeds.com](https://www.gamingseeds.com).

Every job type is **shown running**: a small Bootstrap UI at `/` triggers each scenario and shows the side effects live (users, an email outbox, a processed-image gallery), while the Hangfire dashboard at `/hangfire` shows the job states.

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

- **http://localhost:5180/** — the demo UI (trigger jobs, watch results)
- **http://localhost:5180/hangfire** — the Hangfire dashboard

Hangfire creates its own tables on first run; the app creates its `Users`/`SentEmails` tables via `EnsureCreated`. Both live in the `GamingSeedsHangfireDemo` database.

> Running SQL Server in a container? See the companion video **".NET Docker Tutorial: Containerize a .NET and SQL Server App (Step by Step)"** for the full walkthrough.

---

## How it's built (code-along)

The steps below mirror the video. Each one is a small, self-contained change.

### 1. Install the packages

```bash
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

`Hangfire.AspNetCore` is the core integration; `Hangfire.SqlServer` is the storage provider that persists jobs so they survive restarts.

### 2. Configure Hangfire + storage

In [Program.cs](src/GamingSeeds.Hangfire.Demo/Program.cs):

```csharp
builder.Services.AddHangfire(config => config
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("HangfireConnection"), new SqlServerStorageOptions
    {
        QueuePollInterval = TimeSpan.FromSeconds(1),
        DisableGlobalLocks = true   // recommended for throughput
    }));

builder.Services.AddHangfireServer();   // the worker pool that processes jobs
```

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

The static `BackgroundJob.Enqueue(...)` works, but injecting `IBackgroundJobClient` keeps your endpoints and services **unit-testable** (you can mock the client). Same story for recurring jobs with `IRecurringJobManager`.

```csharp
app.MapPost("/users/register", async (RegisterRequest req, AppDbContext db, IBackgroundJobClient jobs) =>
{
    var user = new User { Email = req.Email, RegisteredAt = DateTime.UtcNow };
    db.Users.Add(user);
    await db.SaveChangesAsync();

    jobs.Enqueue<IEmailJobs>(x => x.SendConfirmationEmail(user.Id));  // fire-and-forget
    return Results.Ok(user);
});
```

### The four job types

| Type | API | Where in this app |
|---|---|---|
| **Fire-and-forget** | `jobs.Enqueue<IEmailJobs>(x => x.SendConfirmationEmail(id))` | `POST /users/register` |
| **Delayed** | `jobs.Schedule<IEmailJobs>(x => x.SendConfirmationReminder(id), delay)` | `POST /users/register` |
| **Recurring** | `recurring.AddOrUpdate<IUserMaintenanceJobs>("cleanup-unconfirmed-users", x => x.DeleteUnconfirmedUsers(), Cron.Daily)` | startup |
| **Continuation** | `jobs.ContinueJobWith<IImageProcessingJobs>(prevId, x => x.GenerateThumbnails(id))` | `POST /images/upload` |

### 5. Dashboard

```csharp
app.UseRouting();
app.UseHangfireDashboard();   // must come after UseRouting
```

Open `/hangfire` to watch jobs move through **Enqueued → Processing → Succeeded**, see **Scheduled** jobs count down, inspect **Failed** jobs, and trigger **Recurring** jobs manually.

> For this demo the dashboard is open. In production, secure `/hangfire` with authorization.

---

## Best practices this app demonstrates

- **Jobs are DI classes, not static methods** — so they can inject scoped services like `DbContext`.
- **Enqueue via `IBackgroundJobClient` / `IRecurringJobManager`** — testable instead of the static `BackgroundJob` facade.
- **Pass IDs, not entities.** Job arguments are serialized to JSON and stored. Pass a `userId` and load the entity fresh inside the job — never a whole `User` object.
- **Make jobs idempotent.** A job can run more than once (retries). `SendConfirmationReminder` no-ops if the user already confirmed.
- **Put job-filter attributes on the *interface* method.** When you enqueue via `Enqueue<IEmailJobs>(...)`, Hangfire reads `[AutomaticRetry]` from the **interface** method, not the implementation. Put it on the class and it's silently ignored (you'll get the default 10 retries).

```csharp
public interface IEmailJobs
{
    [AutomaticRetry(Attempts = 5)]   // <-- here, on the interface
    Task SendConfirmationEmail(int userId);
}
```

---

## Try each scenario

| Button (UI) | Endpoint | What to watch |
|---|---|---|
| Register user | `POST /users/register` | Confirmation email lands in the **Outbox**; a reminder shows as **Scheduled** in the dashboard |
| Seed stale accounts | `POST /users/seed-stale` | Adds 30-day-old unconfirmed users; then hit **Trigger now** on `cleanup-unconfirmed-users` in the dashboard and watch them disappear |
| Upload seed image | `POST /images/upload` | Three chained jobs run in order; source → processed → thumbnail → optimized images appear in the **gallery** |
| Trigger failing job | `POST /demo/fail` | The job retries with backoff and lands in **Failed**; retry it manually from the dashboard |

Delays are shortened for filming — see `DemoOptions` in [appsettings.json](src/GamingSeeds.Hangfire.Demo/appsettings.json) (`ConfirmationReminderDelay`, `SimulatedWorkDuration`). In production these would be `TimeSpan.FromHours(24)` and `Cron.Daily`.

---

## Production next steps (out of scope here)

- Real SMTP for emails and a real object store for images.
- Authentication on the `/hangfire` dashboard.
- Failure alerting on top of the dashboard.
- Named queues and multiple servers for scaling.
