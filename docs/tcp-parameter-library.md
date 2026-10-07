# Provenance-aware TCP parameter library

The executable TCP model library is:

```text
BioRT.App/data/tcp_parameters_v2.json
```

It is deliberately separate from the legacy `tcp_ntcp_params.json`.

The legacy file mixes alpha, alpha/beta, SF2, N0, Tpot, Tk, D50 and gamma values from different sources. BioRT does **not** combine those values into a synthetic TCP model.

## Selection principle

A TCP record is an indivisible source-specific object:

```text
disease / setting / risk group
target or source dose descriptor
endpoint / time point
equation family
fractionation convention
parameter set
source
validation status
applicability limits
```

A PTV name by itself is not sufficient to identify tumor biology.

## Initial executable/reference models

### Prostate gland — Sachpazidis et al. 2020

This is currently the strongest target-DVH TCP record in the initial library because the source used the **individual planned DVH**, rather than prescription dose alone.

Source cohort:
- 129 intermediate/high-risk cN0 cM0 patients;
- definitive EBRT ± ADT;
- median follow-up 81.4 months;
- Phoenix biochemical relapse-free survival;
- 28–42 fractions;
- 1.7–2.7 Gy/fraction;
- 66–78 Gy prescription range.

For the CT-defined prostate gland:

```text
D50 = 66.8 Gy
95% CI 64.6–69.0 Gy

gamma = 3.8
95% CI 2.6–5.2

alpha/beta = 1.3 Gy
```

A separate 5-year fit is also stored:

```text
D50 = 64.6 Gy
gamma = 3.1
alpha/beta = 2.2 Gy
```

The source reported the prostate-gland model as better behaved than the mpMRI-GTV model. BioRT therefore does not substitute an arbitrary PTV DVH for the source-defined prostate-gland DVH.

Source:
- Sachpazidis I et al. Radiat Oncol. 2020;15:242.
- PMID 33081804.
- DOI 10.1186/s13014-020-01683-4.

### Prostate SBRT — Royce et al. 2021 / HyTEC

The source used pooled **prescription doses**, not patient-level target DVHs, and converted them to EQD2 using alpha/beta = 1.5 Gy.

The published 5-year high-risk fit is:

```text
D50 = 84.2 Gy
gamma = 4.50
```

The source states approximately:

```text
EQD2 97 Gy  -> 90% TCP
EQD2 102 Gy -> 95% TCP
```

Those values are numerically reproduced by BioRT's implementation of the displayed source equation and are regression-tested.

The fit is nevertheless context-specific because it was based on only 85 high-risk patients from 3 studies.

#### Unresolved low/intermediate-risk source inconsistency

The same paper reports for low/intermediate risk:

```text
D50 = 20.6 Gy
gamma = 0.15
```

but also states:

```text
EQD2 71 Gy -> 90% TCP
EQD2 90 Gy -> 95% TCP
```

Inserting the published D50/gamma pair into the paper's displayed Poisson equation does **not** reproduce those two stated TCP points.

BioRT therefore:
- preserves the published numbers for provenance;
- marks the record `source_inconsistency_unresolved`;
- disables runtime evaluation;
- does not infer a corrected gamma or D50.

The low/intermediate record can only be enabled after the discrepancy is resolved from supplementary/source data or author clarification.

Source:
- Royce TJ et al. IJROBP. 2021;110:227–236.
- PMID 32900561.
- DOI 10.1016/j.ijrobp.2020.08.014.

### Recurrent previously irradiated head and neck — Vargo et al.

The AAPM working-group analysis pooled more than 300 recurrent head-and-neck cases from 8 publications.

Source dose was weighted PTV marginal/prescription dose and was converted to a **5-fraction-equivalent total dose** using an LQ/Withers isoeffect transform with alpha/beta = 10 Gy.

Two time-specific logistic fits are stored:

```text
2-year local/locoregional control:
D50 = 45.1 Gy
gamma = 0.56

3-year local/locoregional control:
D50 = 49.8 Gy
gamma = 0.94
```

The source reports approximately 41% 3-year local control at 45 Gy in 5 fractions. BioRT reproduces that value as a regression test.

These records apply to **malignant locally recurrent, previously irradiated head-and-neck disease treated with SBRT reirradiation**. They are not generic HNSCC TCP models and are not applicable to primary definitive treatment.

Source:
- Vargo JA et al. IJROBP. 2021;110:137–146.
- PMID 29477291.
- DOI 10.1016/j.ijrobp.2018.01.044.

## Evidence retained but not yet executable

Early-stage NSCLC SBRT evidence from Liu et al. 2017 is recorded as evidence-only.

That pooled analysis compared six biophysical model families and found meaningful model dependence. BioRT will not choose one row from that multi-model analysis until the exact model-specific equations/parameters are extracted and independently regression-tested.

Source:
- PMID 27871671.
- DOI 10.1016/j.radonc.2016.11.006.

## Runtime safety

The library/engine supports explicit:
- `Calculated`;
- `MissingInputs`;
- `NotApplicable`;
- `RuntimeDisabled`;
- `Unsupported`.

Fractionation and clinical-context domains are checked before evaluation.

Examples:
- Sachpazidis EBRT is rejected for a 5-fraction SBRT course.
- Vargo reirradiation is rejected when the clinical setting is definitive primary treatment.
- Royce low/intermediate is blocked even when all clinical inputs are present because its source inconsistency remains unresolved.

## Current limitation before patient runtime

The model engine is implemented and tested, but BioRT does **not yet automatically select a TCP model from a patient PTV**.

Before patient runtime is connected, `clinical_context.json` must be extended with:
- explicit TCP risk group;
- explicit treatment setting;
- explicit TCP target structure/role or model selection.

This is necessary to prevent a structure such as `PTV_70` from being interpreted as prostate, HNSCC, NSCLC, or another tumor entity by name alone.
