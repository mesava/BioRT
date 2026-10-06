# Parotid / xerostomia — BioRT evidence extraction

This document records the first endpoint-specific scientific extraction for BioRT. It is intentionally stricter than a generic literature review: **every computable parameter set must remain tied to its original model, endpoint, time point, structure definition, cohort and dose convention.**

## Why the old single xerostomia row is invalid

The current legacy file contains:

```text
TD50 = 40.8 Gy
m = 0.54
```

These values are not retained in the new library as a valid published model. They are approximately the arithmetic means of the two distinct Lee/Fang 2013 fits:

- 3 months: TD50 = 37.8 Gy, m = 0.59
- 12 months: TD50 = 43.9 Gy, m = 0.48

Averaging them destroys the endpoint/time-point identity and creates a parameter set that was not fitted or validated as such.

## Computable classical LKB references

| Model ID | Endpoint / time point | Structure definition | TD50 (Gy) | m | n | Key applicability |
|---|---|---:|---:|---:|---:|---|
| eisbruch_1999_parotid_lkb | stimulated parotid function; longitudinal post-RT | individual parotid | 28.4 | 0.18 | 1.0 | historical benchmark; endpoint details require full-text review |
| semenenko_li_2008_xerostomia_lkb_6m | stimulated flow <25% of baseline; within 6 mo | parotid mean dose | 31.4 | 0.53 | 1.0 fixed | combined published datasets; non-2 Gy source doses converted to 2-Gy equivalent with α/β=3 Gy |
| dijkema_2010_parotid_flow_lkb_12m | stimulated individual gland flow <25% baseline; 1 yr | individual parotid mean dose | 39.9 | 0.40 | 1.0 equivalent mean-dose assumption | 222 patients / 384 glands; Michigan + Utrecht; CRT + IMRT |
| miah_2013_parsport_parotid_lkb | parotid saliva-flow recovery | parotid mean dose | 26.3 | 0.25 | 1.0 | 63 PARSPORT patients; objective salivary endpoint |
| lee_fang_2013_qol_lkb_3m | QoL moderate-severe xerostomia; 3 mo | spared parotid mean dose | 37.8 | 0.59 | 1.0 assumption | time-point-specific fit; do not combine with 12 mo |
| lee_fang_2013_qol_lkb_12m | QoL moderate-severe xerostomia; 12 mo | spared parotid mean dose | 43.9 | 0.48 | 1.0 assumption | time-point-specific fit; do not combine with 3 mo |
| mavroidis_2017_hpv_opc_lkb_12m | PRO-CTCAE change ≥2; 12 mo | combined contralateral parotid + SMG | 26.9 | 0.63 | 1.0 | 43 favorable-risk HPV+ OPC patients, 60 Gy IMRT + weekly cisplatin |

**Important:** even when two models share the LKB equation, their percentages are not interchangeable because they predict different clinical/functional outcomes.

## Transparent multivariable logistic comparators

### Beetz et al. IMRT xerostomia model

For patient-rated xerostomia at 6 months:

```text
NTCP = 1 / (1 + exp(-S))

S =
  -1.443
  + 0.047 * Dmean(contralateral parotid, Gy)
  + 0.720 * baseline_xerostomia_a_bit
```

Baseline coding:
- 0 = none
- 1 = a bit

Development cohort: 178 IMRT patients. Reported AUC 0.68 (95% CI 0.60–0.76).

This model is useful in BioRT because it demonstrates that a clinically interpretable NTCP model does not have to be LKB and that baseline symptoms materially change risk.

### LIPP v2.2 modern xerostomia model

The externally evaluated LIPP v2.2 model uses:

```text
NTCP = 1 / (1 + exp(-S))

S =
  -2.295
  + 0.0996 * (sqrt(Dmean(left parotid)) + sqrt(Dmean(right parotid)))
  + 0.0182 * Dmean(combined submandibular glands)
  + baseline_score
```

Baseline score:
- none = 0
- mild = 0.459
- moderate-to-severe = 1.207

Development/update cohort: 1145 patients; endpoint prevalence 46%.

A 2025 external real-world validation used 674 patients (204 events). Before recalibration, calibration slope was 1.16 and intercept -0.12; recalibration was required. After recalibration, discrimination remained limited (AUC 0.62).

**Design implication for BioRT:** the program must distinguish:
1. published coefficients,
2. local/external calibration,
3. model discrimination,
4. absolute-risk interpretation.

A "validated" model may still require recalibration in a new population.

## Modern evidence that should not yet be collapsed into a coefficient row

- **Onjukka 2020:** 753-patient registry; Cox models for grade ≥2/≥3 late xerostomia, with risk estimated at 9/12/24 months. Valuable, but model choice is endpoint- and time-dependent.
- **Mavroidis 2023:** shows explicit dependence on PRO-CTCAE vs CTCAE, gland grouping, and 6/12/18/24-month time point.
- **Van Rijn-Dekker 2023:** regeneration-weighted dose and spatial parotid radiosensitivity; requires substructure segmentation.
- **Chu 2025:** 3D multimodal deep-learning NTCP; external performance fell relative to internal testing and improved with transfer learning.
- **2026 systematic review (PMID 42751136):** 51 studies / 10,789 patients; contralateral parotid Dmean remains the most consistent predictor, but SMG/oral cavity/substructure information adds signal.

## Source hierarchy for this endpoint

1. Primary parameter-fit publication.
2. Independent external validation.
3. Systematic review / evidence synthesis.
4. Clinical protocol using the model (e.g., model-based selection).
5. Exploratory spatial/ML/DL extensions.

BioRT should never replace levels 1–2 with a review-only number when a traceable primary parameter set exists.

## Implementation decision

The new data file is:

```text
BioRT.App/data/ntcp_parameters_v2.json
```

It is **not yet wired into the patient calculation path**. First we validate its schema and compute known synthetic/model-specific examples. Only after that should the legacy `tcp_ntcp_params.json` xerostomia row be retired from runtime use.
