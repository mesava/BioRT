# Fractionation correction in BioRT

BioRT keeps **fractionation handling model-specific**. A published NTCP/TCP parameter set must be evaluated on the same dose basis that was used to derive that parameter set.

## Generic LQ conversion

For total physical dose (D) delivered in (N) equal fractions:

```text
d = D / N
BED = D * (1 + d / (alpha/beta))
EQD_ref = D * (d + alpha/beta) / (d_ref + alpha/beta)
```

For EQD2, (d_ref = 2 Gy).

AAPM TG-166 describes LQ-based correction of DVHs by replacing the physical dose of each differential DVH bin with its LQ-equivalent dose in 2-Gy fractions. This is the generic `dvh_bin_eqd2` mode supported by the BioRT fractionation engine.

## Source-reproduction mode

Not every published parameter set was derived using full per-bin DVH correction.

Semenenko & Li (2008) fitted xerostomia LKB parameters with (n = 1) from complication rate versus **mean organ dose**. Their source states that when published cohorts used daily fractions other than 2 Gy, the **mean organ doses** were converted to 2-Gy-fraction equivalent dose using `alpha/beta = 3 Gy`.

Therefore BioRT encodes that model as:

```text
fractionation_transform =
mean_dose_eqd2_if_prescription_fraction_differs
```

This deliberately differs from generic `dvh_bin_eqd2`.

For a course whose prescription daily fraction size is approximately 2 Gy, the Semenenko/Li record uses the physical parotid mean dose, matching the parameter-derivation convention. For a different prescription fraction size, the parotid mean dose is converted using the total number of fractions and `alpha/beta = 3 Gy`.

## Why this distinction matters

A generic per-bin EQD2 transformation and a source-specific mean-dose transformation can produce different values because LQ conversion is nonlinear.

BioRT therefore does **not** silently choose the mathematically most elaborate transformation. It reproduces the dose convention of the source used for the parameter set.

## Equal-fraction assumption

The current LQ engine assumes that the evaluated total dose belongs to one course with:
- a known number of fractions;
- equal fractionation;
- a stable spatial dose pattern between fractions.

A summed/composite dose from sequential phases with different fraction sizes must **not** be passed through a single conversion. Each phase must be converted separately before biological summation.

## High-dose-per-fraction limitation

The LQ formalism remains the conventional isoeffect method used by TG-166, but its validity at large doses per fraction is debated, especially in SRS/SBRT. BioRT should therefore not automatically extrapolate conventional-fractionation parameter sets to high-dose-per-fraction treatments merely because an EQD2 value can be calculated.

Future HyTEC/SBRT model records must carry their own applicability domain.

## Runtime outputs

Each NTCP result can now report:
- probability;
- effective dose used by the model;
- applied dose basis;
- model source;
- warnings;
- missing fractionation inputs.

This makes it possible to distinguish, for example:

```text
physical_mean_dose_source_convention
mean_dose_eqd2
dvh_bin_eqd2
physical_dose
```

instead of presenting all NTCP percentages as if they were calculated from the same dose basis.
