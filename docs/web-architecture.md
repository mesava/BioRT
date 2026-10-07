# BioRT Web architecture

## Product decision

BioRT is developed as a **website first**.

The existing Windows console application remains a regression/commissioning harness, but it is no longer the intended end-user interface.

The production web target is a static **Blazor WebAssembly** application so the radiobiology engine stays in C# and the same `BioRT.Core` / shared analysis implementation is reused in the browser.

## Privacy-first architecture

For patient-specific calculations the implemented architecture is:

```text
browser
  |
  +-- local RTPLAN / RTDOSE / RTSTRUCT selection
  |
  +-- stream-based DICOM parsing
  |
  +-- PlanAnalysisService
  |
  +-- DVH / physical metrics
  |
  +-- clinical criteria
  |
  +-- TCP / NTCP
  |
  +-- warnings / provenance / commissioning fingerprint
```

The DICOM payload remains in browser memory and is not uploaded to a public BioRT server merely to perform the calculation.

This matters because radiotherapy DICOM commonly contains direct patient identifiers.

Until the client-side path has passed independent commissioning on de-identified clinical cases, the public site must be used only with synthetic or de-identified data.

## Why Blazor WebAssembly

The scientific engine is implemented and regression-tested in C#.

A WebAssembly client lets the project:
- reuse the same scientific code instead of porting models to JavaScript;
- use one source of truth for TCP/NTCP equations;
- publish as a static site;
- avoid a patient-data backend for ordinary calculations;
- keep GitHub Actions building and testing the same model code.

## Current web state

`BioRT.Web` currently provides:
- Russian-language site shell and safety messaging;
- live TCP/NTCP model catalogue from the working JSON libraries;
- local selection of one coherent RTPLAN / RTDOSE / RTSTRUCT set;
- stream-based fo-dicom parsing in the browser;
- safe rejection of missing or ambiguous multiple RT objects;
- optional Monaco criteria JSON;
- optional `clinical_context.json`;
- full shared `PlanAnalysisService` calculation;
- structure volumes and cumulative DVHs;
- PTV D2 / D98 / D95 / D50 / HI / CI / GI;
- Dmean / Dmax / Dxx / Dcc / Vxx% / Vxx cm³ clinical criteria with PASS/FAIL;
- NTCP evaluation with model identity, applicability and provenance;
- explicit TCP evaluation when requested by clinical context;
- analysis warnings;
- deterministic Web ↔ Console commissioning fingerprint;
- GitHub Pages deployment workflow.

## Shared analysis path

The console and website now use the same reusable calculation orchestration:

```text
PlanAnalysisRequest
  -> PlanAnalysisService
  -> PlanAnalysisResult
```

The console is therefore a regression/commissioning renderer around the same scientific result rather than an independent second implementation.

The two front ends intentionally retain different DICOM input paths:

```text
BioRT.App
  physical folder -> DicomImporter -> shared analysis

BioRT.Web
  browser streams -> DicomBundleImporter -> shared analysis
```

This makes the remaining commissioning question primarily an input-path equivalence problem.

## Milestones

### W1 — stream-based DICOM import — implemented

`BioRT.IO` contains `DicomBundleImporter` and stream-backed input support for browser-selected RT files.

### W2 — reusable client-side analysis service — implemented

`PlanAnalysisService` returns structured physical, clinical, NTCP/TCP and warning results without UI/file-system dependencies.

Both BioRT.Web and BioRT.App consume it.

### W3 — browser file workflow — implemented

Current input:
- RTPLAN;
- RTDOSE;
- RTSTRUCT;
- optional Monaco criteria JSON;
- optional `clinical_context.json`.

ZIP import remains a convenience enhancement rather than a scientific requirement.

### W4 — result UI — implemented

The browser reports:
1. physical plan evaluation;
2. clinical criteria;
3. NTCP;
4. TCP;
5. provenance / applicability warnings;
6. commissioning fingerprint.

No single TCP/NTCP percentage is intended to be interpreted without its endpoint and model identity.

### W5 — independent browser commissioning — current milestone

Before patient-specific browser use:
- run the same de-identified clinical case through Web and Console;
- require exact commissioning fingerprint agreement as the first strict acceptance criterion;
- compare detailed outputs if fingerprints differ;
- verify structure volumes, DVHs, PTV metrics and clinical criteria;
- verify NTCP/TCP model selection and applicability warnings;
- test failure cases and ambiguous structure matching.

The implemented commissioning procedure is documented in `docs/web-console-commissioning.md`.

A later comparator should additionally support justified numerical tolerances for cross-version regression, where harmless floating-point changes may alter a strict hash.

## Hosting

The static site is designed for GitHub Pages:

```text
https://mesava.github.io/BioRT/
```

A custom domain can be attached later without changing the computation architecture.
