# RFC: BioRT ↔ HFC — единая радиобиологическая платформа без преждевременного monorepo

Status: proposed; 2026-11-06. This RFC is architectural, **not** clinical clearance.

Source projects:
- BioRT: https://github.com/mesava/BioRT
- HFC: https://github.com/mesava/HFC-HypoFracCalc-

## Решение

Развивать оба проекта как части одной экосистемы, сохраняя отдельные репозитории и отдельные release/CI gates на текущем этапе.

| Responsibility | Owner | Rationale |
|---|---|---|
| RTPLAN / RTDOSE / RTSTRUCT import; DICOM geometry and contour topology | BioRT | C# fo-dicom, existing browser/console commissioning |
| DVH, geometric structure masks, physical PTV/OAR metrics | BioRT | Must be commissioned against independent TPS/QA references |
| Fractionation, BED/EQD2, repair, repopulation, course calendar, reirradiation methodology | HFC | Advanced endpoint-linked scientific workflow and literature audits |
| Endpoint/source-specific α/β, T½, time parameters and HyTEC evidence | HFC evidence registry | Retain per-record provenance and release gates |
| TCP/NTCP equations and model-specific coefficient sets | Both, by independently verified model IDs | Do not merge distinct endpoints, equation forms or populations |
| Clinical applicability/research warning | Both | No clinical endorsement from matching implementation alone |

Current HFC main uses React/TypeScript/Vite. BioRT uses C#/.NET 9/Blazor WebAssembly. A full monorepo or UI rewrite brings no immediate scientific benefit.

## Existing overlap already found

HFC has an **unmerged experimental** branch `voxel-eqd2-core-v0.1` with:
- `src/domain/doseGrid.ts`: explicit DICOM-like grid geometry and voxel values;
- `src/workflows/voxelEqd2.ts`: per-course voxel BED/EQD2, geometry equality, conservative accumulation, user-explicit recovery discount.

This branch diverges substantially from HFC main and must **not** be merged wholesale without rebase/port and new tests. Its design expressly forbids unregistered dose-grid accumulation and assumes uniform fraction-by-fraction spatial dose distribution. There is no deformable registration or resampling.

BioRT's current `DoseVolume` indexes `Dose[column,row,frame]`; HFC's prototype uses flattened **frame-major** dose values. A cross-language bridge MUST define and test the mapping:

`flatIndex = frame * rows * columns + row * columns + column`.

Never transpose `PixelSpacing = [row_mm, column_mm]`. DICOM ImagePositionPatient refers to the **centre of first transmitted voxel** (not voxel corner). The bridge must preserve ImageOrientationPatient, GridFrameOffsetVector interpretation and FrameOfReferenceUID, and must block invalid/unsupported geometry.

## Integration stages

### S0 — repository and documentation link (now)

- Keep separate sites/releases.
- Declare this RFC and a tracked integration issue in BioRT.
- Do not change clinical model defaults.
- Create an independently auditable **golden numerical vector** set for LQ/BED/EQD2 in both languages.

### S1 — shared scientific contract

Define versioned JSON schema (or typed package) for:
- `calculation_contract_version`;
- `dose_grid` geometry and optional dose-vector payload;
- fractions, total dose, per-fraction uniform-dose assumption;
- target/OAR identity and ROI contouring convention;
- `endpoint_id`, `model_id`, `source_id`, DOI/PMID;
- dose basis (`physical`, `BED`, `EQD2`, study-specific);
- temporal modifiers and units (`Gy_BED/day` vs `Gy_EQD2/day`);
- release status, missing inputs, evidence uncertainty and warnings.

Retain model version and evidence-dataset version separately. A model must not be selected solely from an organ name.

### S2 — cross-language regression

For the same synthetic spatial dose cases, require:
- C# `FractionationCorrector` vs HFC TS `lq.ts` mathematical parity;
- explicit dose-zero, nonuniform prescription dose, invalid α/β, and high-dose warning tests;
- voxel-value and array-order parity with fixed tolerances;
- no implicit BED/EQD2 summation of physical dose from different courses;
- no automatic biological recovery factor.

Regression numerical tolerances are not clinical acceptance limits.

### S3 — use HFC scientific capabilities from BioRT

After validating the contract, expose **pure HFC math** as a versioned, local module (e.g. an ESM bundle via JS interop inside the same BioRT origin), or port explicitly reviewed algorithms with parity tests. Avoid coupling BioRT to the HFC React UI.

Potential workflows:
- RTDOSE -> voxel BED/EQD2 -> structure DVH on transformed dose;
- compare fractional regimens for a specific ROI/endpoint;
- multi-course accumulation **only after anatomical registration/resampling QA**;
- display source-specific HyTEC constraints together with model limitations.

The website must never transmit raw patient DICOM to another origin or an HFC server as an automatic integration mechanism.

### S4 — unified UX

After both scientific cores and a single dataset contract are stable, introduce a shared landing page/visual system:
- **HFC / Regimens & Biology**
- **BioRT / DICOM & Plan Analysis**

A true single frontend or monorepo is a later decision, based on maintainability and user testing, not a prerequisite for cross-project scientific consistency.

## Blocking gates

1. BioRT issue #29: correct RTDOSE orientation/pixel centring, RTSTRUCT mask topology/XOR, small-structure volume QA.
2. First realistic Web baseline is recorded, but same-case Console equality remains to be demonstrated.
3. HFC evidence remains research/draft; source audits do not imply clinical approval.
4. No routine TCP/NTCP coefficients or α/β defaults can be transferred between projects without endpoint, source, dose basis, timepoint, cohort and model-equation review.
5. On multiple-course doses, image registration/resampling and accumulation assumptions must be explicit and independently QA'd.

## Immediate next technical artifact

A small synthetic golden-case format for fractionation/maths, consumed by **independent C# and TS tests**, without any patient information. Then an explicit, clinically conservative dose-grid contract extending HFC's existing prototype only after geometry tests pass.
