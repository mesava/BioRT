# Web / console commissioning

BioRT.Web and BioRT.App now call the same `PlanAnalysisService`, but they still use different input surfaces:

```text
BioRT.App
  physical folder -> DicomImporter -> shared analysis

BioRT.Web
  browser streams -> DicomBundleImporter -> shared analysis
```

The remaining commissioning question is therefore primarily the **input-path equivalence**: does the same RTPLAN / RTDOSE / RTSTRUCT + JSON context produce the same scientific result through both front ends?

## Analysis fingerprint

Both front ends now calculate the same deterministic SHA-256-based analysis fingerprint.

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

Therefore an exact web/console fingerprint match provides a strong regression check that the two input paths produced the same analyzed result.

## First patient commissioning procedure

Use a **de-identified** clinical case with:
- one RTPLAN;
- one RTDOSE;
- one RTSTRUCT;
- Monaco criteria JSON;
- optional clinical_context.json.

Do not upload an identifiable clinical DICOM case to the public BioRT GitHub repository.

### Console

Run the existing BioRT.App workflow on the de-identified folder and record:

```text
Analysis fingerprint: ...
```

Also retain the generated BioRT log for detailed comparison.

### Web

Open the deployed BioRT website and select the same RTPLAN / RTDOSE / RTSTRUCT / JSON files.

Record:

```text
Commissioning fingerprint: ...
```

### Acceptance

Initial acceptance criterion:

```text
web fingerprint == console fingerprint
```

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

## Independent numerical tolerances

Fingerprint equality is intentionally strict. For future cross-version commissioning, BioRT should additionally expose a tolerance-based snapshot comparison, because legitimate implementation changes may alter floating-point representation without clinical significance.

Recommended first tolerances to encode in a later comparator:
- structure volume: relative/absolute tolerance defined from voxel volume;
- dose metrics: <= one DVH bin or stricter;
- CI/GI: explicit absolute tolerance;
- TCP/NTCP: explicit probability tolerance.

Those tolerances must be justified and regression-tested rather than silently introduced.

## Privacy

The production design is local browser computation. Nevertheless:
- use de-identified cases during development;
- do not commit patient DICOM to the public repository;
- do not add Patient ID to fingerprints, URLs, logs intended for publication, or test names.
