# Login regression tests

Run `bash tests/LoginRegression/run.sh` from any directory with .NET SDK 8 installed. The script restores Newtonsoft.Json from nuget.org. To run fully offline with a populated local package cache, set `LOGIN_NUGET_SOURCE` to that cache. `DOTNET_BIN`, `DOTNET_CLI_HOME`, and `NUGET_PACKAGES` can also be supplied.

The harness links the production C# login controls, account helpers, HTTP helper and Settings page directly. Platform UI, settings storage, and WebView2 are fakes; Bili account responses are injected through its internal transport constructor. All cookie values and identities are fabricated. The actual `LoginHttp` implementation is compiled but never invoked. There are no real service requests, sign-ins, browser profiles or credentials in the test run.

Coverage includes candidate-cookie exact names and expiry, platform-domain cleanup boundaries, explicit saving, no automatic persistence, repeated clicks, cancellation and late callbacks, newer-page retries, initialization/cleanup failures, bounded cleanup without deleting a later login, popup completion ordering, account switching, Bili identity validation, Sync-replaced identity invalidation, Settings subscriptions and logout propagation. Results are written to `results/login.json`.

## Account-state contract

- Bili: only a successful existing account endpoint response with a positive account ID and nonempty name commits a new QR session and reports login success.
- Douyu/Douyin: `Logined` remains a legacy saved-session compatibility flag. `VerificationState` is `Unverified`; neither cookie presence nor HTTP 200 is presented as server-verified login. Existing request-cookie sources are unchanged. The official website remains visible for scan confirmation/captcha, and “保存会话” is an explicit user action.
- Settings allow re-login and clearing a saved-but-unverified session. Platform-specific browser cookies are cleared before a fresh official flow and on cancellation/completion/logout. Cleanup failures are reported and the next login retries the scoped reset.

## Verification limits and Windows checklist

These tests do not build or run UWP, render XAML, exercise real WebView2, or authenticate a real account. The separate `Login and unsigned UWP validation` workflow compiles the actual UWP project and XAML on Windows 2022, x64 Release, without signing, certificate secrets, manifest changes or package publication. Its current-commit result must be checked separately; Linux harness success is not Windows compilation success.

After Windows compilation succeeds, use a manually operated test account to check each platform on Windows: open/close and Escape/Back during initialization and confirmation; repeated clicks; QR expiry/refresh and network failure; captcha remains reachable; save/return/Settings navigation; logout then re-login with a different account; other platforms retain their sessions. Confirm the draggable Douyu popup resolves correctly and that status text does not cover the official page. Verify actual WebView2 cleanup on normal close and Runtime failures. Do not copy real cookies into test files or logs.

The repository's existing `Build UWP` signing workflow is unchanged. This validation workflow is restricted to development branches and uploads logs/test results and successful unsigned compile output for inspection. This output is not a signed installable package.
