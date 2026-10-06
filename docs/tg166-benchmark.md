# TG-166 benchmark validation plan

BioRT uses AAPM TG-166 as the commissioning framework for biological metrics.

TG-166 recommends independent verification of EUD/TCP/NTCP and provides:
- a simple water-phantom benchmark;
- three clinical IMRT cases (head & neck, prostate, brain);
- tabular DVH data;
- reference gEUD values for Monaco, Pinnacle and Eclipse.

The published benchmark metadata/reference values are stored in:

```text
validation/tg166_reference_values.json
```

## Stage A — generalized EUD mathematics

Implemented.

BioRT now has a dedicated `GeneralizedEud` calculator implementing:

```text
gEUD = (sum_i vi * Di^a)^(1/a)
```

for cumulative DVHs by first reconstructing differential volume bins.

Coverage includes:
- positive `a` (serial/OAR-like emphasis);
- `a = 1` (arithmetic mean);
- `a -> 0` geometric-mean limit;
- negative `a` (target/cold-spot emphasis);
- explicit zero-dose behavior for negative `a`.

The LKB implementation now reuses the same generalized-EUD calculation through `a = 1/n`.

Analytic unit tests cover arithmetic, RMS, harmonic and geometric means plus the LKB/gEUD equivalence.

## Stage B — official TG-166 DVH / DICOM datasets

Pending import of the original AAPM benchmark files.

TG-166 lists these locations:

- `TG166prostate.zip`
- `TG166headneck.zip`
- `TG166brain.zip`
- `EUD_Monaco_Pinnacle_Eclipse.xls`

The current automated environment can verify the report and reference tables, but the historical binary download endpoints are not currently retrievable through the available web connector. We therefore **do not fabricate DVHs from Table VIII**.

When the official files are available, they should be stored as a versioned validation fixture (or downloaded by a documented setup script if redistribution is not appropriate), and BioRT should reproduce Table VIII from the original tabular DVHs.

## Stage C — Table VIII acceptance criteria

For the official source DVHs, BioRT should reproduce the reported power-law gEUD values with a numerical tolerance defined from:
- source DVH bin resolution;
- cumulative-to-differential conversion;
- rounding in the published table.

TG-166 reports spreadsheet-vs-TPS agreement below 0.1% for the benchmark phantom when the same DVHs and `a` values were used.

Important: Table VIII contains **different plans from three TPSs**, so Monaco/Pinnacle/Eclipse values are not expected to equal each other. The benchmark is to reproduce the value for the corresponding source DVH.

## Stage D — Table VII TCP/NTCP

Table VII is retained as a future commissioning target, but it must not be confused with the current BioRT LKB branch.

The Table VII NTCP values come from Pinnacle's Biological Response implementation using a **relative-seriality model**. Therefore reproducing them requires implementing that exact model family and its LQ handling.

Reference values:
- TCP: 94.1%, 80.3%.
- NTCP: 26.6%, 18.1%, 23.5%, 29.5%.

BioRT will only mark this benchmark as passed after the model equations and source DVHs are implemented independently and the published values are reproduced.

## Stage E — routine regression QA

Once official TG-166 fixtures are available, GitHub Actions should run:
1. DICOM/DVH import regression tests;
2. gEUD Table VIII comparisons;
3. selected TCP/NTCP benchmark comparisons;
4. checks after every model-engine change.

This follows the TG-166 recommendation that biological metrics be independently verified at commissioning and rechecked after major upgrades.
