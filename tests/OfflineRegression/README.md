# AllLive offline regression tests

These tests cover the repairs based on commit `c6951df959b7ead0d93edc087188788b49f1e273` without an account, live stream, or user database.

## Run

Install an official .NET 8 SDK. From the repository root:

```sh
bash tests/OfflineRegression/run.sh
```

An explicit SDK executable can be supplied with `DOTNET_BIN=/path/to/dotnet`. The test folder's SDK selection is independent of the application's .NET 9 SDK selection. First restore downloads signed/package-managed dependencies from NuGet.org using normal certificate verification. Cache and CLI state default to this test folder; no system trust, network settings, account, or global SDK configuration is changed.

On Windows, open a terminal in `tests/OfflineRegression` and run each project with `dotnet run --project main/AuditHarness.csproj -c Release` (and equivalently `douyin/DouyinHarness.csproj`, `sync/SyncHarness.csproj`, `tls-sites/SiteTlsHarness.csproj`, `warnings/WarningHarness.csproj`). The Douyin suite intentionally waits through real five-second retry intervals and takes about 82 seconds.

## Final results

| Suite | Passed | What it executes |
| --- | ---: | --- |
| Main | 43/43 | Actual Douyu/signing/parser and room view-model source; real WebSocketSharp 1.0.3 validator configuration; no-network HTTP/UI/danmaku doubles |
| Four-site TLS entry points | 8/8 | All four actual danmaku classes invoke the actual shared validator before simulated transport headers, with valid/invalid certificate decisions |
| Douyin lifecycle | 11/11 | Actual Douyin class with controlled signatures/socket events; full retry limits, stop/restart, stale callbacks and ACK |
| Sync and history | 48/48 | Actual SyncVM/DatabaseHelper with real Microsoft.Data.Sqlite 8.0.6, SQLitePCLRaw 2.1.8, temporary SQLite file, transactions and failure triggers; asynchronous close-event fixture self-check |
| Warning cleanup | 17/17 | Actual Core assembly, RelayCommand and script loader; event subscriptions, header isolation, async exception/cancellation contracts, embedded scripts and Brotli |
| Total | 127/127 | Focused offline regression assertions, not end-to-end application acceptance |

The original 29-case main audit had five failed product assertions. Their safety expectations are retained: reject bad certificates, reuse after Stop, latest URL wins, no playback after Stop, reject a bare directory. TLS now targets the application's explicit configuration of the unchanged vulnerable package; the package's unsafe built-in default was not claimed to be patched. Tests verify the actual published package callback, individual name/chain/missing-certificate errors, a self-signed untrusted certificate, and a valid locally generated CA/leaf chain without installing trust.

The original five Douyin scenarios now satisfy the safe behavior. The original twelve sync/history scenarios retain their IDs and safe expectations: all twelve pass on repaired code. Running the same real-SQLite suite against the original source still fails the original nine defect scenarios and passes its three controls. Additional cases trigger a second SQL write failure and prove earlier DELETE/INSERT/UPDATE operations are rolled back, including row IDs and sqlite_sequence; a subsequent retry succeeds.

`Compat.cs` only disambiguates the original BrotliSharpLib type from .NET 8's added `BrotliStream`. It does not alter production code or implement a codec. `FavoriteJsonItem.extracted.cs` is the unchanged DTO extracted from `FavoriteVM.cs`; it avoids compiling unrelated UI dependencies.

## Boundaries

The complete repaired `AllLive.Core` netstandard2.0 Release project separately compiled successfully (zero errors and warnings after the warning cleanup). This is not a new complete Windows/UWP build. Real Windows UI, media playback, WinUI/dispatcher timing, WebSocket/TLS handshakes, platform availability, real credentials, SignalR service, protobuf interoperability and a user's database have not been exercised. The TLS transport fixtures are not a real MITM test. SQLite validation uses disposable fixture databases, never real user data.

Public production APIs are retained; the two batch import methods are additive. `WatchTime == default(DateTime)` retains the existing “time omitted” convention and uses the current time; non-default historical times are retained. No UI redesign, framework replacement, account/permission change, merge or deployment is included.

## Auditable evidence

The checked-in `evidence/` folder contains the original baseline results, repaired results and run logs. `baseline-sync-same-suite.json` is the same real-SQLite test suite executed against the original production files. `baseline-core-build.txt` confirms the two compiler warnings also occur in the original Core project. Generated test reruns go to ignored `results/` instead of overwriting this snapshot. The `Offline regressions` GitHub Actions workflow runs all five suites without accounts or signing secrets and uploads fresh JSON results.

## Independent review correction

A separate read-only review identified an introduced SC countdown initialization bug in the first patch: the real page loads settings before room data, so the first timer captured a previous room generation. Before publication, room loading was changed to rebuild its SC timer for the active load and timer callbacks check their instance. Tests VM-011 and VM-012 execute settings-before-room and Stop/reload countdowns and expiry; VM-014 covers queued stale SC ticks. Message queue entries also carry the receive generation so a callback that enqueues after Stop cannot reach the new room (VM-013). These final changes passed the rerun; the first patch was not treated as final.

`portable-all-suites-run.txt` records a successful full portable-layout replay before the four final VM regression additions; `portable-final-main-run.txt` reruns the final 43-case main suite after those additions. The other three production/test inputs did not change. VM-013 injects a late old-generation queue item directly; it validates the filter without claiming a real concurrent Windows callback schedule.

## Warning cleanup regressions

The original 109 product regression assertions remain. Main now links the production `RelayCommand` rather than an unused-event double. Sync adds one self-check of its offline `Closed` event simulator, including waiting for callbacks and unsubscribing. The fifth suite adds 17 warning-cleanup checks. The separate login suite still has 30 checks.

`WarningHarness` references the complete Core project and links the actual UWP `RelayCommand` and `LoggingDouyinScriptRunner` sources. Its only replacement is logging, to inject failures without accessing Windows storage. It checks both command types' event sender, arguments, calling thread, subscribe/unsubscribe behavior, no automatic notifications, and existing delegate/constructor behavior. Header checks preserve copies, forced defaults and search-cookie isolation; an injected header failure proves the public async API still returns a faulted task without making an HTTP request. The actual embedded scripts must match byte-for-text in their original order; logging failures retain empty fallback, faulted-task and canceled-task semantics. In-memory Brotli decompression and legacy fallback are also exercised. Three injected I/O failures prove the actual Tars request reader, packet decoder and type utility retain the exact original exception object and originating stack frame; the protocol logic is unchanged.

No warning suppression, fake scheduling or `Task.Run` is introduced. The original Core CS1998 was in the private synchronous header helper, not category network fetching. The unreachable Brotli return also existed in the original master baseline. Windows/UWP compilation remains a separate CI check; these tests do not run platform UI, real streams or logins.
