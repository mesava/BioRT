# Web / console commissioning

BioRT.Web and BioRT.App call the same `PlanAnalysisService`, but they use different DICOM input surfaces:

```text
BioRT.App
  physical folder -> DicomImporter -> shared analysis

BioRT.Web
  browser streams -> DicomBundleImporter -> shared analysis
```

The commissioning question is therefore primarily the **input-path equivalence**: does the same RTPLAN / RTDOSE / RTSTRUCT + JSON context produce the same scientific result through both front ends?

## Current status

### Automated synthetic cross-input commissioning — passed

`BioRT.Tests/SyntheticDicomCommissioningTests.cs` constructs a fully synthetic RT dataset in memory:
- RTPLAN: 30 × 2 Gy;
- RTDOSE: small deterministic multi-frame grid;
- RTSTRUCT: synthetic PTV and OAR contours.

The same serialized DICOM bytes are analyzed through both paths:

```text
folder -> DicomImporter + RtStructReader -> PlanAnalysisService
stream -> DicomBundleImporter -> PlanAnalysisService
```

Acceptance requires:
- exact SHA-256 analysis fingerprint equality;
- tolerance-based `PlanAnalysisComparator` equality;
- expected physical metrics for the synthetic PTV/OAR.

This test is now part of CI.

### De-identified clinical commissioning — pending

Synthetic equivalence does not replace commissioning on a realistic de-identified clinical DICOM case. That is the remaining patient-like validation step before patient-specific browser use.

## Analysis fingerprint

Both front ends calculate the same deterministic SHA-256-based analysis fingerprint.

Console:

```text
Analysis fingerprint: 0123456789abcdef
```

Web:

```text
Commissioning fingerprint: 0123456789abcdef
```

The displayed value is the first 16 hexadecimal characters of the full SHA-256 digest.

The fingerprint includes:
- fraction count and dose/fraction;
- RTDOSE grid geometry;
- analyzed structure names and volumes;
- full calculated cumulative DVHs;
- PTV D2/D98/D95/D50/HI/CI/GI;
- clinical criterion value + PASS/FAIL;
- NTCP model/status/probability/effective dose;
- TCP model/status/probability/effective dose;
- analysis warnings.

It deliberately does **not** include Patient ID.

Therefore an exact Web/Console fingerprint match provides a strict regression check that the two input paths produced the same analyzed result.

## Tolerance-based comparison

Fingerprint equality is intentionally exact.

BioRT additionally contains `PlanAnalysisComparator` for cross-version regression, where harmless floating-point representation changes may otherwise alter a hash.

It uses metric-specific numerical tolerances for:
- dose;
- volume;
- geometry;
- DVH volume percentage;
- HI/CI/GI;
- TCP/NTCP probability.

The built-in defaults are intentionally tiny **numerical regression tolerances**, not clinically justified acceptance limits.

The exact fingerprint remains the first acceptance criterion for same-version Web-vs-Console commissioning.

See `docs/tolerance-comparison.md`.

## First clinical commissioning procedure

Use a **de-identified** clinical case with:
- one RTPLAN;
- one RTDOSE;
- one RTSTRUCT;
- Monaco criteria JSON;
- optional `clinical_context.json`.

Do not upload an identifiable clinical DICOM case to the public BioRT GitHub repository.

### Console

Run the existing BioRT.App workflow on the de-identified folder and record:

```text
Analysis fingerprint: ...
```

Retain the generated BioRT log for detailed comparison.

### Web

Open the deployed BioRT website:

```text
https://mesava.github.io/BioRT/
```

Select the same RTPLAN / RTDOSE / RTSTRUCT / JSON files and record:

```text
Commissioning fingerprint: ...
```

### Acceptance

Initial strict criterion:

```text
web fingerprint == console fingerprint
```

Then confirm `PlanAnalysisComparator` agreement.

If the fingerprints differ, compare in this order:

1. fraction count and dose/fraction;
2. RTDOSE grid dimensions / spacing / Z positions;
3. structure volume;
4. DVH mean/max and cumulative bins;
5. PTV metrics;
6. clinical criteria;
7. NTCP;
8. TCP;
9. warnings.

A mismatch must be explained before patient-specific browser use.

## Privacy

The production design is local browser computation. Nevertheless:
- use de-identified cases during development and commissioning;
- do not commit patient DICOM to the public repository;
- do not add Patient ID to fingerprints, URLs, logs intended for publication, or test names.
