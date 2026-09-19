# EventStoreDB integration

## Objective
Provide the shared EventStoreDB infrastructure that connects Service A (commands) and Service B (projections), and document the operational handoff to Persona 5.

## Problem and why
Services A and B already use the same `EVENTSTORE_URL`, but the repository has no Compose definition to run the EventStoreDB dependency or explain how to inspect streams in its UI.

## Authorized scope
- Add only the `eventstore` infrastructure block to `docker-compose.yml`.
- Keep connection variables centralized in `.env`.
- Document EventStoreDB UI access and stream inspection in the root README.
- Verify the existing .NET services build and their EventStore contract remains compatible.

## Constraints
- Do not change command/query business logic owned by Personas 2 and 3.
- Persona 5 owns the service blocks, `depends_on`, and run scripts; this work must be additive and easy to merge with those changes.
- TDD: disabled/unknown; no existing test runner or session configuration was found. Use build and Compose validation.

## Tasks
- [x] ES-1 Add the EventStoreDB Compose service with persistent volumes and UI/TCP ports.
- [x] ES-2 Verify both services share the `EVENTSTORE_URL` contract and build successfully.
- [x] ES-3 Document UI access, stream lookup, limitations, and the Persona 5 integration handoff.
- [x] ES-4 Pull and start the EventStoreDB image, then inspect a real `battle-{id}` stream in the UI.

## Acceptance criteria
- `docker-compose.yml` contains an EventStoreDB service accessible on host port 2113 with persistent data.
- Both service projects compile without source changes to their domain logic.
- The README explains `http://localhost:2113` and how to find a `battle-{id}` stream.
- Persona 5 can add service blocks that reuse the `eventstore` service name and `.env` variables.

## Checks
- `dotnet build service-a/service-a.csproj`
- `dotnet build service-b/service-b.csproj`
- `docker compose config` (if Docker Compose is available)

## Progress
- ES-1 complete: `docker-compose.yml` defines a single insecure EventStoreDB 24.10 node on host port 2113, with data/log volumes and AtomPub enabled for the stream browser.
- ES-2 complete: both projects build successfully (`dotnet build --no-restore`, 0 warnings and 0 errors). Service A appends `BattleStarted`, `AttackPerformed`, and `HealUsed` to `battle-{id}`; Service B subscribes from `$all` and filters `battle-{id}`. Both read `EVENTSTORE_URL`; `.env` already defines the Docker-network URL `esdb://eventstore:2113?tls=false`.
- ES-3 complete: root README documents the UI, stream convention, boundary of insecure mode, and the Compose handoff.
- ES-4 complete: EventStoreDB is running and healthy on `localhost:2113`. Service A appended `BattleStarted` and `AttackPerformed` to `battle-integration-p4-1`; Service B projected the result (`Bug`: 80 → 50 HP, 30 total damage). EventStoreDB's stream endpoint returned 200 and contained both event types. The local test service processes were stopped to avoid conflicting with Persona 5's Docker service ports; the EventStoreDB Compose container remains running.
- `docker compose config` passed.

## Next step
Persona 5 can add the two application service blocks and their `depends_on` entries. For a clean rerun, use `docker compose down -v` only when intentionally deleting the persisted demo events and logs.
