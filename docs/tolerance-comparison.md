# Tolerance-based analysis comparison

BioRT has two complementary commissioning/regression mechanisms.

## 1. Exact fingerprint

For the same input files and the same software version, the strict first acceptance criterion remains:

```text
web fingerprint == console fingerprint
```

The SHA-256 fingerprint is deliberately exact. It is useful for detecting any difference between the browser-stream and local-folder input paths.

## 2. Tolerance-based comparator

`PlanAnalysisComparator` is intended for cross-version regression testing, where an implementation change may alter floating-point representation without changing the scientific result.

It compares:
- plan fractionation;
- RTDOSE grid dimensions, spacing, origin and Z positions;
- structure names and volumes;
- full cumulative DVHs;
- PTV D2/D98/D95/D50/HI/CI/GI;
- clinical criterion values and PASS/FAIL;
- NTCP model identity, status, probability, effective dose, warnings and missing inputs;
- TCP model identity, status, probability, effective dose, warnings and missing inputs;
- global analysis warnings.

Patient ID is not compared.

## Default tolerance profile

The built-in defaults are intentionally **numerical regression tolerances**, not clinical-equivalence tolerances:

- dose absolute tolerance: 1e-6 Gy;
- dose relative tolerance: 1e-9;
- volume absolute tolerance: 1e-6 cm³;
- volume relative tolerance: 1e-9;
- geometry tolerance: 1e-6 mm;
- HI/CI/GI tolerance: 1e-9;
- probability tolerance: 1e-10;
- cumulative-DVH volume tolerance: 1e-8 percentage points.

These values are meant to absorb only very small floating-point noise.

They must not be presented as clinically justified acceptance limits.

## Future clinical/commissioning profiles

If BioRT later adds looser named tolerance profiles, each tolerance should be justified by the relevant numerical method and commissioning purpose.

Examples that require explicit validation before adoption:
- volume tolerance related to dose/structure voxelization;
- dose-metric tolerance related to DVH bin width;
- CI/GI tolerance;
- TCP/NTCP probability tolerance.

A looser comparator must never silently replace the exact fingerprint for first Web-vs-Console commissioning.
