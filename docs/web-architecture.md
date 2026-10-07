# BioRT Web architecture

## Product decision

BioRT is developed as a **website first**.

The existing Windows console application remains useful as a regression/commissioning harness, but it is no longer the intended end-user interface.

The production web target is a static **Blazor WebAssembly** application so the radiobiology engine can stay in C# and the same `BioRT.Core` implementation can be reused in the browser.

## Privacy-first target

For patient-specific calculations, the preferred architecture is:

```text
browser
  |
  +-- local file / ZIP selection
  |
  +-- RTPLAN / RTDOSE / RTSTRUCT parsing
  |
  +-- DVH / physical metrics
  |
  +-- TCP / NTCP
  |
  +-- result export
```

The DICOM payload should remain in browser memory and must not be uploaded to a public BioRT server merely to perform the calculation.

This matters because radiotherapy DICOM commonly contains direct patient identifiers.

Until the client-side DICOM path is commissioned, the public site must be used only with synthetic or de-identified data.

## Why Blazor WebAssembly

The scientific engine is already implemented and regression-tested in C#.

A WebAssembly client lets the project:
- reuse `BioRT.Core` rather than porting every model to JavaScript;
- use one source of truth for TCP/NTCP equations;
- publish as a static site;
- avoid a patient-data backend for ordinary calculations;
- keep GitHub Actions capable of building and testing the same model code.

Fellow Oak DICOM targets modern .NET / .NET Standard runtimes and has prior browser/Blazor usage examples, making a browser-side DICOM path technically plausible. Browser compatibility of the exact BioRT RT import path still has to be verified with our own RTPLAN/RTDOSE/RTSTRUCT regression fixtures.

## Current web milestone

`BioRT.Web` currently provides:
- the Russian-language site shell;
- project and safety messaging;
- a live model catalogue loaded from the same embedded `ntcp_parameters_v2.json` and `tcp_parameters_v2.json` used by the scientific code;
- runtime/reference status;
- endpoint, model family, equation and PubMed provenance;
- responsive layout;
- GitHub Pages deployment workflow;
- local browser selection of one coherent RTPLAN / RTDOSE / RTSTRUCT set;
- stream-based fo-dicom parsing without uploading the files to a BioRT server;
- safe rejection of missing or ambiguous multiple RT objects.

At this milestone the browser validates/imports the RT bundle and reports fractionation and RTSTRUCT ROI count. Full DVH / physical metrics / TCP / NTCP are the next web stage.

## Next implementation stages

### W1 — stream-based DICOM import — implemented

`BioRT.IO` now contains `DicomBundleImporter` and `DicomInputFile`, so browser and local callers can supply streams instead of physical paths.

Current API shape:

```text
IReadOnlyCollection<InputFile>
  -> DicomBundleImporter
  -> PlanData + RTSTRUCT contours
```

with each input backed by a `Stream`.

This API must be usable by both:
- console/local files;
- browser-selected files / ZIP entries.

### W2 — client-side analysis service

Move the calculation orchestration currently embedded in `BioRT.App/Program.cs` into a reusable service.

Target shape:

```text
PlanAnalysisRequest
  -> PlanAnalysisService
  -> PlanAnalysisResult
```

The shared `PlanAnalysisService` now returns structured results rather than printing:
- structure volume;
- D2 / D98 / D95 / D50 / HI / CI / GI;
- dose criteria and PASS/FAIL;
- NTCP results;
- TCP results;
- warnings / provenance.

BioRT.Web already consumes this service. BioRT.App now uses the same service as its console/commissioning renderer, so both front ends share one analysis orchestration path.

### W3 — browser file workflow

The web UI will accept:
- RTPLAN;
- RTDOSE;
- RTSTRUCT;
- Monaco criteria JSON;
- optional `clinical_context.json`.

ZIP import can be added for clinical convenience.

### W4 — result UI

Results should be grouped into:
1. physical plan evaluation;
2. clinical criteria;
3. NTCP;
4. TCP;
5. provenance / applicability warnings.

No single TCP/NTCP percentage should be shown without its endpoint and model identity.

### W5 — independent browser commissioning

Before patient-specific browser use:
- compare browser vs console results on the same de-identified DICOM case;
- require exact agreement within defined numerical tolerances;
- rerun TG-166/gEUD/TCP analytical tests;
- verify structure volumes and DVH metrics;
- test failure cases and ambiguous structure matching.

## Hosting

The static site is designed for GitHub Pages initially.

Expected project-site URL after Pages deployment is enabled:

```text
https://mesava.github.io/BioRT/
```

A custom domain can be attached later without changing the computation architecture.
