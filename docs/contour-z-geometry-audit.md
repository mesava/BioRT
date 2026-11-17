# RTSTRUCT contour-Z ↔ RTDOSE frame geometry audit (experimental)

Status: **independent QA primitives and analytic tests**. The routines in
`ContourZGeometryAudit` and `SimpleContourInterplaneVolume` are **not** a
replacement for commissioned DVH or an independent clinical TPS reference.

Issue: https://github.com/mesava/BioRT/issues/29

## Why this is necessary

The current `MaskBuilder` takes each axial contour plane and assigns it
to its **nearest RTDOSE Z frame**. It then combines polygons on that frame
using ROI-level union or XOR.

Several contour planes can be assigned to the same dose frame when the
RTSTRUCT contour-Z spacing differs from the RTDOSE Z spacing. A contour
plane may also lie beyond the sampled RTDOSE slab. The existing mask
algorithm does not resample along Z or guarantee equivalence to the
contour-defined geometric volume.

For example: contour spacing about 1.25 mm versus dose frame spacing
1.3 mm in the realistic synthetic-patient commissioning case. This
difference alone does not specify the size of any resulting error.

## Stage 1: non-destructive Z mapping audit

`ContourZGeometryAudit.Analyze(contours, dose)` reports:
- total contour count and distinct Z-plane count (within 0.001 mm);
- nearest RTDOSE frame and absolute centre-to-plane distance;
- number of dose frames receiving >1 **distinct** contour-Z plane;
- number of contour planes outside the first/last dose-frame centre
  extended by half the known regular dose-frame spacing;
- whether the current grid has a known frame spacing
  (a single-frame RTDOSE does not).

The status `NoDetectedZMappingIssues` is only a **limited diagnostics
check**. It is not clinical geometry acceptance and it does not quantify
DVH uncertainty, interpolate structure masks, or measure contour volume.

The audit rejects non-axial contours and irregular/nonmonotonic
manual dose-Z arrays rather than silently producing a positive result.

This PR does **not** alter existing Web/Console dose calculation or the
commissioning SHA-256.

## Stage 2: independent inter-plane area integral (limited)

`SimpleContourInterplaneVolume.Estimate(roi)` computes
`sum(0.5*(A_i+A_(i+1))*(Z_(i+1)-Z_i)) / 1000` cm³.

`A_i` is polygon area from the shoelace formula in **mm²**.

Crucial distinctions:
- This is the integral of **linearly interpolated slice area between
  the first and last supplied contour planes**.
- It does not infer the structure beyond the end planes or use an
  unspecified contour-slab thickness.
- It is not necessarily the true 3D geometric ROI volume or an
  independent Monaco/TPS measurement.
- A single contour plane is **not sufficient** to infer thickness.
- Any plane with multiple polygons requires an explicit polygon
  union and is marked `Unavailable`. This includes common multi-island
  RTSTRUCT data.
- `CLOSEDPLANAR_XOR` requires validated Boolean polygon area
  operations and is marked `Unavailable` for this estimator, even
  though the dose-grid mask supports XOR.
- Self-crossing/keyhole contours are not accepted as simple polygons.

The returned `InterplaneVolumeCc` must **not** be substituted for the
dose-grid-sampled mask volume in Dcc/Vcc or NTCP; those computations
currently rely on a cumulative DVH sampled over the same grid.

## Analytic regression

- A 10×10 mm constant-area contour at Z=0 and Z=10 mm
  integrates to 1.0 cm³ over the interval.
- Areas of 100, 200, 100 mm² at Z=0,5,10 mm integrate to 1.5 cm³
  by the trapezoidal rule.
- 3 contour planes mapped to 2 dose frames are flagged as a collision;
  repeated contours on exactly the same plane are not.
- A plane beyond the dose-grid slab is reported.
- A one-frame dose grid cannot be certified as having known Z thickness.
- Invalid self-intersecting, multi-polygon or XOR contour estimations
  return unavailable, not numerical guesses.

## Still needed for issue #29

1. Add a **read-only** per-ROI diagnostic panel to BioRT Web, separate
   from the fingerprint and clinical criteria (after test review).
2. Compare computed reports against the original synthetic case 001
   and independently measured Monaco contours.
3. Explicit, tested Z-resampling/interpolation with clinically
   validated handling of contour endpoints and anatomical topology.
4. Separate robust contour-derived volume with validated multi-island,
   XOR and keyhole support, plus small-OAR error/uncertainty studies.
5. Same-case, same-commit Console↔Web full SHA-256 equality.

Original hypothetical-patient DICOM remains local; do not commit it.
