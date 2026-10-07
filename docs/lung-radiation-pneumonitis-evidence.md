# Lung / radiation pneumonitis — evidence extraction

BioRT treats radiation pneumonitis (RP) as a separate NTCP domain with explicit distinction between:
- pooled LKB/mean-dose models;
- QUANTEC dose-only models;
- multivariable clinical models;
- modern external validation/recalibration;
- clinical guideline constraints;
- SBRT-specific evidence.

A single field named "lung NTCP" is not scientifically adequate.

## 1. Semenenko & Li 2008 — pooled LKB

For symptomatic RP with the lung considered as a paired organ, the combined analysis reported:

```text
TD50 = 29.9 Gy
95% CI 28.2–31.8 Gy
m = 0.41
95% CI 0.38–0.45
n = 1 (fixed)
```

For ipsilateral-lung dose:

```text
TD50 = 37.6 Gy
95% CI 34.6–41.4 Gy
m = 0.35
95% CI 0.29–0.43
n = 1 (fixed)
```

When contributing studies used daily fractions other than 2 Gy, the authors converted **mean organ dose** to 2-Gy-fraction equivalent dose using `alpha/beta = 3 Gy`.

BioRT therefore uses the same source-specific mean-dose EQD2 transform already implemented for the Semenenko xerostomia model. It does not silently substitute generic per-bin EQD2.

Source:
- Semenenko VA, Li XA. Phys Med Biol. 2008.
- PMID 18199912.
- DOI 10.1088/0031-9155/53/3/014.

## 2. QUANTEC 2010 — conventional fractionation

The QUANTEC lung review fitted symptomatic RP against mean lung dose using a logistic model:

```text
NTCP = 1 / (1 + exp(-S))
S = -3.87 + 0.126 * MLD(Gy)
```

Equivalent reported summary quantities:

```text
TD50 = 30.75 Gy
gamma50 = 0.969
```

A probit/Lyman fit to the same pooled MLD response yielded:

```text
TD50 = 31.4 Gy
m = 0.45
n = 1
```

The review also recommended, for definitively treated NSCLC with conventional fractionation, keeping approximately:

```text
V20 <= 30–35%
MLD <= 20–23 Gy
```

if the intent is to keep symptomatic RP risk around or below 20%.

These are gradual risk relationships, not hard biological thresholds.

BioRT limits the continuous QUANTEC models to a conventional `1.8–2.0 Gy/fx` runtime domain. QUANTEC itself cautions that historical MLD relationships may transport poorly to nonstandard distributions such as SBRT, IMRT or proton therapy.

Source:
- Marks LB et al. *Radiation dose-volume effects in the lung*. IJROBP. 2010.
- PMID 20171521.
- DOI 10.1016/j.ijrobp.2009.06.091.

## 3. Appelt 2014 — individualized QUANTEC-based model

Appelt et al. combined the QUANTEC dose response with clinical risk factors.

The later external-validation literature reproduces the model in standard logistic form:

```text
S =
  -4.76
  + 0.138 * MLD
  - 0.48 * current_smoker
  - 0.37 * former_smoker
  + 0.82 * pulmonary_comorbidity
  + 0.51 * age_over_63
  + 0.47 * sequential_chemotherapy
  + 0.63 * tumor_mid_or_inferior
```

with `NTCP = logistic(S)`.

The primary paper reported the baseline no-risk-factor response as:

```text
D50(0) = 34.4 Gy
gamma50(0) = 1.19
```

and showed improved risk stratification over the dose-only model in an independent 103-patient dataset.

BioRT requires all clinical variables explicitly; none are inferred from DICOM.

Source:
- Appelt AL et al. Acta Oncol. 2014.
- PMID 23957623.
- DOI 10.3109/0284186X.2013.820341.

## 4. Niezink et al. 2023 — updated model in modern RT

A prospective 612-patient cohort externally evaluated the QUANTEC and Appelt models.

Patients received high-dose RT of 45–60 Gy in 25–30 fractions. SBRT was excluded. Lungs were automatically contoured as **Lungs-GTV**.

Grade >=2 RP was scored using CTCAE v4.0 from 6 weeks to 6 months.

The final updated three-predictor model after ridge shrinkage was:

```text
S =
  -7.880
  + 0.250 * MLD
  + 0.049 * age_years
  - 0.902 * current_or_quit_less_than_3_months
```

```text
NTCP = 1 / (1 + exp(-S))
```

Reported AUC was approximately 0.79 and bootstrap-corrected slope 0.95.

Important provenance point: this paper externally validated the older models, but the final New-RP model was **derived/updated in the 612-patient cohort and internally validated by bootstrapping**. BioRT therefore does not label it independently externally validated.

Source:
- Niezink AGH et al. Radiother Oncol. 2023;186:109735.
- PMID 37327975.
- DOI 10.1016/j.radonc.2023.109735.

## 5. ESTRO 2025 clinical guideline

The contemporary ESTRO guideline emphasizes that RP risk depends on more than DVH dose alone.

For curative-intent conventionally fractionated lung RT it identifies examples such as:

```text
Typical:
  V20 < 35%
  MLD < 23 Gy

Medium-risk examples:
  V20 > 35%
  MLD > 23 Gy

High-risk examples:
  V20 > 45%
  MLD > 30 Gy
```

It recommends defining normal lung for MLD/V20 as **both lungs minus GTV**.

BioRT stores these as clinical guideline evidence, not as a continuous probability model.

Source:
- De Ruysscher D et al. Radiother Oncol. 2025;207:110837.
- PMID 40185160.
- DOI 10.1016/j.radonc.2025.110837.

## 6. SBRT / HyTEC-era evidence

Contemporary SBRT analyses summarize HyTEC-era lung guidance around:

```text
MLD < 8 Gy
V20 < 10–15%
```

BioRT keeps this evidence separate from conventional-fractionation LKB/QUANTEC models.

An EQD2 conversion does **not** grant permission to apply a conventional RP model to SBRT.

The current library stores the SBRT guidance as reference-only pending a direct review of the complete HyTEC source chain and a dedicated SBRT evaluator.

## 7. External validation and calibration drift

### 2023

The Niezink cohort found that both original QUANTEC and Appelt models needed updating. The updated Appelt/New-RP model performed better than the recalibrated dose-only QUANTEC model.

### 2026

Chen et al. externally evaluated QUANTEC/Appelt-type models in a contemporary IMRT/multimodal cohort. Historical models again underestimated RP risk.

Their locally developed Model D had:
- development AUC ~0.708;
- external AUC ~0.718;
- external Brier score 0.207;
- external calibration intercept -1.043;
- external calibration slope 1.133.

Thus patient ranking transferred better than absolute risk calibration.

BioRT does **not** implement Model D yet because the publication text says tumor location is retained in the final model, while the displayed final coefficient table/equation does not provide a tumor-location coefficient. This is recorded as an unresolved source inconsistency rather than silently guessed.

Source:
- Chen Z et al. Front Oncol. 2026.
- PMID 42130623.
- DOI 10.3389/fonc.2026.1777999.

## Implementation consequence

BioRT now recognizes at least three different lung structure concepts:

```text
lung_total
lung_total_minus_gtv
lung_ipsilateral
```

They are intentionally not interchangeable.

The program will only auto-evaluate a model when the matched structure corresponds to the model's canonical structure definition. Left/right individual lung contours are not silently combined into a total-lung model.

## Current model hierarchy

```text
Lung / radiation pneumonitis
├── Semenenko 2008 total-lung LKB
├── Semenenko 2008 ipsilateral-lung LKB
├── QUANTEC 2010 logistic MLD
├── QUANTEC 2010 probit/LKB MLD
├── Appelt 2014 individualized model
├── Niezink 2023 updated New-RP model
├── ESTRO 2025 guideline evidence
├── SBRT / HyTEC-era reference evidence
└── Chen 2026 Model-D evidence (not executable)
```

The next technical dependency for multivariable lung models is a patient-level clinical-context input (age, smoking, pulmonary comorbidity, chemotherapy sequence, tumor location, etc.). Until that is implemented, BioRT correctly returns `MissingInputs` rather than inventing those values.
