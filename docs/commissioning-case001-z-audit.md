# BioRT commissioning case 001 — independent Z-geometry audit

Date: 2026-11-17. Source: original hypothetical-patient ZIP `15194691.zip`
(kept local; **not committed** to the repository).

This document records an exploratory independent parsing of DICOM
RTSTRUCT/RTDOSE geometry, not acceptance of the plan or BioRT in clinical use.

Related:
- `docs/commissioning-realistic-case-001.md`
- `docs/contour-z-geometry-audit.md`
- issue #29 and PRs #45/#46.

## Input geometry

- RTSTRUCT: 14 ROIs, 545 contours, all `CLOSED_PLANAR`.
- RTDOSE: 185 Z frames with uniform spacing 1.3 mm.
- RTDOSE frame centres: first -162.5 mm, last +76.7 mm.
- In this dataset, a half-voxel centre-slab coverage model spans approximately
  -163.15 to +77.35 mm (Z only).
- RTSTRUCT coordinates are interpreted in patient mm.
- No frame-orientation problem was seen for the *axial* dose grid.
  This does NOT by itself validate FrameOfReferenceUID consistency or XY coverage.

The independent parser reads the raw implicit-VR little-endian DICOM bytes and
extracts contour Zs, contour types, and geometric coordinates without
loading the RTDOSE pixel payload. A second polygon-geometry validation pass
found 0 invalid/self-intersecting polygons across the 545 contours.

## Observed nearest-frame mapping

Distinct contour Zs are grouped to 0.001 mm. A collision is a **dose frame
receiving more than one distinct RTSTRUCT Z plane**, not multiple contours
on the *same* RTSTRUCT plane.

| ROI | Contours | Unique contour Z planes | Frames with Z collisions | Contour planes outside dose Z slab | Max nearest-frame ΔZ, mm |
|---|---:|---:|---:|---:|---:|
| Brain | 119 | 103 | 4 | 0 | 0.63 |
| Brainstem | 46 | 46 | 1 | 0 | 0.63 |
| Cochlea_L | 4 | 4 | 1 | 0 | 0.63 |
| Cochlea_R | 4 | 4 | 0 | 0 | 0.53 |
| Eye_L | 21 | 21 | 0 | 0 | 0.53 |
| Eye_R | 21 | 21 | 0 | 0 | 0.62 |
| Lens_L | 6 | 6 | 0 | 0 | 0.17 |
| Lens_R | 7 | 7 | 0 | 0 | 0.32 |
| OpticChiasm | 2 | 2 | 0 | 0 | 0.13 |
| OpticNrv_L | 8 | 6 | 0 | 0 | 0.18 |
| OpticNrv_R | 8 | 7 | 0 | 0 | 0.22 |
| patient | 183 | 179 | 7 | 0 | 0.63 |
| SpinalCord | 62 | 62 | 2 | 0 | 0.63 |
| PTV | 54 | 54 | 2 | 0 | 0.63 |

**6 of 14 ROIs** have at least one distinct-contour-Z → same-dose-frame
collision under the present nearest-slice convention. **None** has a
contour plane outside the regular RTDOSE Z slab.

This is evidence that nearest-Z sampling loses some distinct RTSTRUCT
plane information. It is **not** evidence of a particular DVH error in Gy,
cc, percentage or probability. Out-of-slab is evaluated in Z only.

## Inter-plane area-integral comparison

The cross-sectional area integral below is the trapezoid rule applied
between **first and last RTSTRUCT contour centres**:

`V_between = Σ (A_i + A_(i+1))/2 × Δz_i / 1000` cm³.

No end-cap extrapolation, slab thickness, missing-slice interpolation,
dose-grid sampling or TPS-independent "true geometric volume" is implied.

PTV:
- independent simple-polygon area integral: **271.95477 cm³**;
- latest user-confirmed BioRT sampled dose-grid volume: **274.66 cm³**;
- absolute difference: about **+2.70523 cm³** (about **+1%** of the
  between-plane integral).

The difference combines unlike endpoint/volume conventions; therefore
it **must not be interpreted as a measured 1% DVH error**.

For additional context, selected simple-contour integrals (cm³):
- Brainstem: 25.208;
- Cochlea_L: 0.03762;
- Cochlea_R: 0.04394;
- Lens_L: 0.17949;
- Lens_R: 0.19527;
- OpticChiasm: 0.07730;
- SpinalCord: 3.65308.

These values are **research diagnostics**, not acceptance volumes.
Complex multicomponent ROIs require validated polygon operations; zero
invalid individual polygons does not validate their complete 3D
combination or the inter-plane shape.

## Commissioning identity

Historical, pre-XY-geometry Web SHA-256:
```text
51dde8070330bd1f518651e6527fe7ef187abb942f2b5090177ee9aad68a47ae
```

Corrected-XY, user-confirmed Web SHA-256:
```text
d15422b0697ba418e944af9496e209a2d460b7972766d1482b6ecbe250c11dac
```

PRs #45/#46 introduce **read-only** audit data and do not alter the
clinical analysis result, DVH, or fingerprint canonical payload.
The original ZIP is not committed publicly.

## Next independent validation gates

1. Load the same ZIP in the updated BioRT Web; compare its read-only
   Z audit table against the independent counts above.
2. Evaluate an explicitly declared Z-resampling/interpolation algorithm
   on analytically known 3D geometries, including nonuniform RTSTRUCT
   slice spacing, endpoints, disconnected regions and tiny OARs.
3. Compare against exported TPS DVH/contour volumes with an independently
   defined measurement protocol, separating discretization and
   interpolation effects.
4. Do **not** silently redefine clinical Dcc/Vcc, NTCP or commissioning
   reference values when introducing an experimental resampling mode.
5. Obtain independent same-source, same-commit Console SHA-256.
