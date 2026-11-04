# Douyu signing regression checks

Run `dotnet run --project tests/Douyu.Regression --configuration Release` with .NET 9.

These 19 deterministic checks compile the production DouyuSignHelper and replace only HTTP, timestamp/MD5/query utilities with small test implementations. They cover login DID preservation, control characters, request identity, encoding, descriptor cache invalidation (including same-DID token replacement), 1/16/17/1000/4096-round hashes, invalid bounds, descriptor expiry, delayed account-switch responses and logout.

The optional `-- --live` path makes anonymous network calls to Douyu for three rooms and prints only numeric status and quality counts. It never loads user cookies or prints signed stream URLs. Availability and API responses can change. This is not a UWP UI or video-rendering test.

If package restore is unavailable, pass `-p:OfflineNewtonsoftPath=/absolute/path/to/Newtonsoft.Json.dll` for a local test-only reference. Production uses its normal NuGet dependency.

Run `dotnet run --project tests/Douyu.TransportRegression` for three loopback wire-level checks against the production HttpUtil. They seed the legacy cookie jar, verify Douyu's opt-in stateless GET/POST send only their explicit Cookie, and confirm the default transport still retains its original behavior.
