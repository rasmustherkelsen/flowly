# Sample Documentation Convention

Every sample under `Samples/` must have a `README.md` at its root. The `Samples/README.md` acts as an index and must be updated whenever a sample is added or removed.

## Structure

```
Samples/
  README.md                        ← index (always update this)
  <Transport>/
    <SampleName>/
      README.md                    ← required for every sample
      ...
```

## Samples/README.md (index)

Group entries by transport. Each sample gets one row in a markdown table with:
- A relative link to the sample's `README.md`
- A one-line description of what it demonstrates

## Per-sample README.md

Each sample README must cover these sections in order:

1. **Title + one-liner** — what transport and what scenario
2. **Projects table** — one row per `.csproj` with its purpose
3. **What it demonstrates** — bullet list of Flowly features exercised (omit for minimal samples where the title is self-explanatory)
4. **Prerequisites** — .NET SDK version, Docker, any workloads or tools
5. **How to run** — exact commands, in order
6. **What to observe** — what the reader should see to confirm it works

## Azure Service Bus samples

For any sample that uses Docker Compose (rather than Aspire) with the ASB emulator, include a dedicated step for generating `sbconfig.json` before starting the emulator:

```powershell
./GenerateSbConfig.ps1
```

Explain that this script builds and installs the `flowly` CLI, introspects the project(s), and writes the queue configuration file that the emulator requires. Note that it must be re-run whenever message contracts change.

## Checklist when adding a new sample

- [ ] Create `Samples/<Transport>/<SampleName>/README.md` following the structure above
- [ ] Add a row to `Samples/README.md`
- [ ] For ASB Docker Compose samples: include a `GenerateSbConfig.ps1` and document its use
