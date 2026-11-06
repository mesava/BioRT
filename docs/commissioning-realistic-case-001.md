# Realistic commissioning case 001

Status: **historical realistic Web baseline captured before the geometry fixes; post-fix re-commissioning is pending.**

The source archive is a hypothetical/synthetic-patient RT export supplied for commissioning. It is intentionally **not committed** to the public repository.

## Input

The case contains:
- one RTPLAN;
- one RTDOSE;
- one RTSTRUCT;
- one Monaco dosimetric-criteria JSON;
- no clinical_context.json.

Plan context:
- prescription: 60 Gy;
- fractions: 30;
- nominal dose/fraction: 2 Gy.

RTDOSE:
- 411 columns × 220 rows × 185 frames;
- in-plane spacing: 1.3 × 1.3 mm;
- Z spacing: 1.3 mm;
- standard axial ImageOrientationPatient: 1\0\0\0\1\0;
- physical dose range in the exported grid: 0 to approximately 65.14 Gy.

All 21 Monaco dose goals are parsed by the current criteria parser.

## Commissioning defect findings

This case exposed three real analysis defects in the pre-fix PlanAnalysisService.

### 1. Brainstem could resolve to Brain

The old criterion matcher selected the first bidirectional substring match.

With RTSTRUCT containing both:
- Brain;
- Brainstem;

a Brainstem criterion could resolve to Brain because "Brainstem" contains "Brain".

In this case that changed the evaluated object dramatically:
- correct Brainstem Dmax: approximately 36.85 Gy;
- whole-Brain maximum: approximately 65.14 Gy.

PR #28 changes criterion structure resolution to:
1. exact case-insensitive match first;
2. unique containment fallback only;
3. ambiguous partial matches fail safely with a warning.

### 2. Decorated Monaco structure names could disappear after DVH construction

The criterion name:
- patient(Unsp.Tiss.)

uniquely resolves to the RTSTRUCT ROI:
- patient.

The old workflow could build the patient DVH and then fail to recover it during clinical-criterion evaluation because a different one-way matching rule was used.

PR #28 preserves the resolved criterion-name -> RTSTRUCT-name mapping through the analysis.

### 3. PTV clinical goals were omitted from PASS/FAIL

D50% >= 60 Gy is used to identify the PTV prescription.

The old workflow then excluded every criterion for that target from ClinicalCriteria.

For this case that omitted:
- D2% <= 64.2 Gy;
- D50% >= 60 Gy;
- V59.7Gy >= 90%;
- V58.8Gy >= 98%;
- V57Gy >= 98%.

PR #28 evaluates every explicit clinical goal while still using D50% >= Rx for PTV prescription identification.

## Historical physical results after structure matching, before geometry corrections

PTV:
- sampled dose-grid volume: approximately 274.44 cm3;
- D2%: 63.4 Gy;
- D98%: 59.7 Gy;
- D95%: 60.3 Gy;
- D50%: 61.7 Gy;
- HI = D2/D98: approximately 1.062;
- CI: approximately 0.919;
- GI: approximately 3.014.

Selected OAR metrics:
- patient D2%: 62.0 Gy;
- Brainstem Dmax: approximately 36.85 Gy;
- Cochlea_L Dmean: approximately 1.59 Gy;
- Cochlea_R Dmean: approximately 1.55 Gy;
- Eye_L Dmax / Dmean: approximately 16.95 / 3.63 Gy;
- Eye_R Dmax / Dmean: approximately 8.67 / 2.22 Gy;
- Lens_L Dmean: approximately 2.00 Gy;
- Lens_R Dmean: approximately 1.79 Gy;
- OpticChiasm Dmax: approximately 9.23 Gy;
- OpticNrv_L Dmax: approximately 7.79 Gy;
- OpticNrv_R Dmax: approximately 6.57 Gy;
- SpinalCord Dmax: approximately 0.69 Gy.

All 21 supplied Monaco criteria pass in this dataset after the PR #28 fixes.

## NTCP

With no clinical_context.json, only models that can run from DICOM/DVH plus fractionation context can be calculated automatically.

The supplied structures map only SpinalCord to the current runtime NTCP organ library.

For the legacy Burman spinal-cord LKB benchmark:
- n = 0.05;
- TD50 = 66.5 Gy;
- m = 0.175;
- prescription context = 2 Gy/fraction, within the configured conventional-fractionation domain;
- effective LKB dose is approximately 0.51 Gy;
- calculated NTCP is effectively zero (approximately 7e-9 as probability).

This remains a historical reference-model result, not a modern absolute-risk claim.

## Geometry observations

The dose grid is standard axial. However, the previously computed mask used a half-voxel XY offset: DICOM ImagePositionPatient already denotes the centre of voxel [0,0,0]. PR #38 removed that offset. All historical DVH, volume, NTCP and SHA-256 outputs below predate this correction and must NOT be treated as current BioRT expected results.

RTSTRUCT contours are generally spaced at about 1.25 mm while RTDOSE is sampled at 1.3 mm. The current implementation assigns each contour plane to its nearest dose plane.

A secondary geometric sanity check showed:
- PTV dose-grid mask volume differs from direct contour-plane integration by about 1%;
- Brainstem by about 2.5%;
- very small structures can show much larger relative volume differences because a 1.3 mm dose voxel is large relative to their total volume.

This does not invalidate the present dose-grid DVH commissioning case, but it identifies a next scientific-validation item:
- validate/interpolate RTSTRUCT-to-RTDOSE rasterization for small structures;
- distinguish contour-derived geometric volume from sampled dose-grid mask volume where absolute-volume metrics Dcc/Vcc are used.

## Historical browser commissioning result (pre-voxel-centre correction)

The original local ZIP was loaded successfully in the deployed BioRT.Web build after the ZIP async-read fix and workflow-trigger fix.

Authoritative full Web fingerprint:

```text
51dde8070330bd1f518651e6527fe7ef187abb942f2b5090177ee9aad68a47ae
```

Short form:

```text
51dde8070330bd1f
```

The browser run completed the full local pipeline:
- ZIP expansion in browser memory;
- RTPLAN / RTDOSE / RTSTRUCT import;
- Monaco criteria parsing;
- DVH and physical metrics;
- clinical PASS/FAIL;
- NTCP model selection/evaluation;
- deterministic SHA-256 generation.

## Cross-input equivalence status

BioRT.Web and BioRT.App do not use separate scientific implementations:
- both use the same `RtPlanReader`;
- both use the same `RtDoseReader`;
- RTSTRUCT is read by the same `RtStructReader`;
- both call the same `PlanAnalysisService`;
- both use the same parameter libraries and structure aliases.

The automated synthetic commissioning test already exercises the folder-style and stream-style input paths on identical serialized DICOM bytes and requires exact fingerprint equality plus `PlanAnalysisComparator` agreement.

For this realistic case, the Web fingerprint above is retained as the first **historical pre-geometry-correction baseline**, not the correct expected fingerprint for current main. A new live Web run and exact same-version physical-folder Console comparison are required before accepting the post-correction commissioning case.

BioRT.App now prints the full 64-character SHA-256 as well as the 16-character short form, so any future same-case Console run can be compared directly against the Web baseline.

## Acceptance status

Passed:
- DICOM object completeness for the current BioRT workflow;
- RTPLAN fractionation extraction independently confirmed;
- RTDOSE geometry and pixel payload consistency;
- RTSTRUCT parsing;
- 21/21 Monaco criteria parsing;
- physical DVH/metric calculation sanity review;
- identification and regression-test coverage of the three matching/PTV-goal defects;
- async-only browser ZIP regression test;
- Web dependency / Pages redeploy trigger correction;
- current main BioRT CI;
- current main BioRT Web CI;
- current main GitHub Pages deployment;
- live realistic-browser run;
- authoritative full Web SHA-256 captured.

After PR #38/#39, acceptance must be reconsidered:
- PR #38 fixes in-plane voxel-centre alignment for axis-aligned RTDOSE; this may change which voxels intersect an ROI.
- PR #39 rejects unsupported `ContourGeometricType` entries instead of silently treating XOR/open/point contours as filled polygons; it also validates point counts and axial contour planes.
- Original RTSTRUCT contains 545 `CLOSED_PLANAR` contours and none of the newly rejected types.
- New expected structure volumes, DVHs, PTV/OAR metrics, clinical criteria, model outputs and full SHA-256 have **not yet** been captured on the corrected release.
- The 21/21 PASS and the historical numerical values above are not automatically carried over to the corrected mask algorithm.
- The strict realistic Web↔Console same-case match remains pending.

Next geometry work remains tracked in issue #29:
- actual XOR/keyhole topology support;
- RTSTRUCT-to-dose-grid Z sampling and plane interpolation QA;
- small-structure contour-derived volume and reliable Dcc/Vcc;
- new same-version Web/Console commissioning after the corrected geometry is published.

The source DICOM archive remains outside the public repository.
