# Realistic commissioning case 001

Status: **scientific review completed; fixes merged; ZIP upload deployed; final live-browser fingerprint capture pending**.

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

PR #28 changed criterion structure resolution to:
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

PR #28 is merged into main and passed both BioRT CI and BioRT Web CI.

## Physical results after corrected structure matching

PTV:
- sampled dose-grid volume: approximately 274.45 cm3;
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
- Eye_R Dmax / Dmean: approximately 8.67 / 2.21 Gy;
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

The dose grid is standard axial, so the present MaskBuilder coordinate assumptions are satisfied for this case.

RTSTRUCT contours are generally spaced at about 1.25 mm while RTDOSE is sampled at 1.3 mm. The current implementation assigns each contour plane to its nearest dose plane.

A secondary geometric sanity check showed:
- PTV dose-grid mask volume differs from direct contour-plane integration by about 1%;
- Brainstem by about 2.5%;
- very small structures can show much larger relative volume differences because a 1.3 mm dose voxel is large relative to their total volume.

This does not invalidate the present dose-grid DVH commissioning case, but it identifies a next scientific-validation item:
- validate/interpolate RTSTRUCT-to-RTDOSE rasterization for small structures;
- distinguish contour-derived geometric volume from sampled dose-grid mask volume where absolute-volume metrics Dcc/Vcc are used.

This work is tracked in issue #29.

## Browser commissioning workflow

PR #30 is merged and deployed.

BioRT.Web now accepts either:
- individual RTPLAN / RTDOSE / RTSTRUCT + JSON files; or
- one ZIP containing those files.

The ZIP is expanded only in browser memory with limits on supported entry count and cumulative uncompressed size.

The Web UI now displays the full 64-character SHA-256 commissioning fingerprint rather than only the first 16 characters.

For this case the intended final live-browser step is therefore:

1. open https://mesava.github.io/BioRT/;
2. select the original commissioning ZIP as one file;
3. confirm 30 fractions, 2 Gy/fx, 14 RTSTRUCT ROI and 21 criteria;
4. confirm the physical metrics above;
5. record the full Commissioning SHA-256.

No clinical_context.json is required for this phase.

## Acceptance status

Passed:
- DICOM object completeness for current BioRT workflow;
- RTPLAN fractionation extraction independently confirmed;
- RTDOSE geometry and pixel payload consistency;
- RTSTRUCT parsing;
- 21/21 Monaco criteria parsing;
- physical DVH/metric calculation sanity review;
- identification and regression-test coverage of the three matching/PTV-goal defects;
- PR #28 merged and deployed;
- PR #30 ZIP upload merged and deployed;
- current main BioRT CI;
- current main BioRT Web CI;
- current main GitHub Pages deployment.

Still required:
- one final live-browser run of the original ZIP to capture the authoritative BioRT.Web full SHA-256 for this realistic commissioning case.

The source DICOM archive must remain outside the public repository.
