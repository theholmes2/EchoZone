# Session recovery changes — 2026-08-28

## Policy

Use Unity Lobby/MPS host election, not client self-promotion or custom Cloud Code election.
The desired server host-disconnect grace is 15 seconds. Configure the active Unity
Dashboard environment's **Disconnect Host Migration Time = 15 seconds** and keep
**Disconnect Removal Time > 15 seconds** (120 seconds is the documented default).
Dashboard settings were NOT changed by this task. Host detection latency and Relay
creation/snapshot application are additional to the server grace period.

Sources:
- https://docs.unity.com/en-us/mps-sdk/config-options
- https://docs.unity.com/en-us/mps-sdk/networking/relay-integration

## Modified C# (no new C# files)

- Assets/Script/Data/RelaySessionConfig.cs: hostMigrationGraceSeconds=15 is a local
  display threshold, NOT a server election command. Poll=5 seconds; overall local
  recovery timeout=240 seconds, separate from election grace.
- Assets/Script/Online/Relay/UnityRelaySessionService.cs:
  RefreshSessionForRecoveryAsync refreshes the existing session and reconnects its
  lobby connection without LeaveAsync. SDK Network.MigrationFailed is forwarded.
- Assets/Script/Online/Relay/RelaySessionGlue.cs:
  unexpected disconnect now calls RecoverSessionAsync instead of the leave/rejoin
  path. Poll cloud state, retain membership/event subscriptions, retry NGO client
  transport when stopped, await SDK host-change events. No local host election.
  Generation checks invalidate stale recovery continuations; migration completion
  updates local role/client ID; failure resets loading state; migration watchdog
  bounds completion waits. Explicit disconnect/invalid-ticket test buttons keep
  their previous leave/rejoin test path and are NOT a real internet-outage test.
- Assets/Script/Network/NetworkStartUI.cs: shows the recovery stage.

Also modified Assets/Data/Online/RelaySessionConfig.asset with the new settings.
This document is new. Existing unrelated changes were preserved.

## Validation and remaining limitations

Unity Roslyn compilation passed; modified tracked C# diff whitespace check passed.
No live multiplayer end-to-end test was run by the assistant.

- NGO retry currently reuses the existing UnityTransport Relay configuration.
  If its allocation has expired, retry may not recover the original host connection;
  fresh allocation recovery needs a further implementation/validation pass.
- Refresh failures are labelled cloud-unavailable/unknown, not proof of an internet
  outage. Deleted sessions, authorization or service errors may also cause failures.
- Local grace expiry only changes status; server settings govern election.
- The 240-second deadline does not cancel an already-running SDK HTTP request.
- Existing snapshot scope does not include positions or enemy state.
- Existing migration restoration has no full all-clients-ready barrier.
- This task did not change Cloud Code, deploy services, or modify Dashboard.

## Tests

1. Set Dashboard grace to 15 seconds in the project's active environment; keep
   removal time greater than grace. Restart play on all instances with a new room.
2. Regression: explicit Test Disconnect, invalid ticket rejection, manual rejoin.
3. Save distinct health/inventory and a partial world pickup. Normal host-exit test:
   same Session/RunId, new host, snapshot state, successful incrementing cloud save.
4. Abrupt host-only termination with a connected survivor: recovery UI, no premature
   Lobby leave, Session Host changed, Snapshot applied, new-host cloud save. Record
   actual times; not an exact 15 seconds from clicking terminate.
5. Real client-only network interruption: requires independent devices/network
   isolation; disconnecting a shared PC affects all local instances. Verify same
   host/room recovery before cache expiration, then separately test a longer outage.
6. Three-player case needs maxPlayers>=3 (asset currently remains 2): host exits,
   exactly one remaining host, other client reconnects; inventory/world consistency.

Stop on failure and preserve warning logs including Session recovery and migration
failure messages. Do not treat compilation or a completed Session join as gameplay
recovery success.
