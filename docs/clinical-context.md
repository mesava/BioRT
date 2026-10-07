# Clinical context input

BioRT can optionally read a patient-folder file named:

```text
clinical_context.json
```

This file contains clinical predictors that are not reliably available from RTPLAN / RTDOSE / RTSTRUCT but are required by multivariable NTCP models and, later, disease-specific TCP model selection.

The file is optional. If it is absent, BioRT continues to calculate physical plan metrics and any radiobiological models that do not require the missing variables. Models requiring unavailable predictors return `MissingInputs`; BioRT does not invent values.

A non-identifying example is provided at:

```text
examples/clinical_context.example.json
```

## Schema v0.2.0

Supported fields currently include:

```text
patient.age_years
patient.smoking_status
patient.months_since_smoking_cessation
patient.pulmonary_comorbidity

tumor.diagnosis
tumor.histology
tumor.stage
tumor.risk_group
tumor.location
tumor.laterality

treatment.setting
treatment.chemotherapy_sequence
treatment.immunotherapy

baseline.xerostomia

tcp.model_id
tcp.target_structure_name
tcp.target_role

model_overrides.numeric
model_overrides.categorical
```

### Allowed categorical values

`smoking_status`
- `never`
- `former`
- `current`

`tumor.location`
- `superior`
- `middle`
- `inferior`

`tumor.laterality`
- `left`
- `right`
- `midline`
- `bilateral`

`chemotherapy_sequence`
- `none`
- `concurrent`
- `sequential`

`baseline.xerostomia`
- `none`
- `a_bit`
- `mild`
- `moderate_to_severe`

## Conservative mappings

BioRT only derives a model predictor when the generic clinical field maps unambiguously.

Examples:

```text
age_years=68
  -> age_years=68
  -> age_over_63=yes
```

```text
smoking_status=current
  -> smoking_status=current
  -> smoking_current_or_quit_lt3m=yes
```

For a former smoker, the Niezink current/recent-smoker variable is only derived when `months_since_smoking_cessation` is provided. A former smoker with no cessation interval remains a missing predictor.

Likewise, `baseline.xerostomia=mild` is not silently equated to the exact Beetz questionnaire category `a bit`, and `a_bit` is not silently mapped into the LIPP three-level baseline variable.

## Model overrides

`model_overrides` exists for explicit model-specific inputs that cannot be safely represented or derived from the generic schema.

Example:

```json
{
  "model_overrides": {
    "numeric": {
      "some_exact_numeric_predictor": 1.0
    },
    "categorical": {
      "some_exact_category": "yes"
    }
  }
}
```

Overrides are applied last and therefore replace any automatically derived value with the same key.

They should be used only when the user intentionally knows the exact model coding. They are not a mechanism for silently filling missing clinical information.

## File discovery

The Monaco criteria JSON is now detected by its required `prescriptions` array rather than by simply choosing the first JSON file in the patient directory. This prevents `clinical_context.json` from being mistaken for the dose-criteria file.

## Privacy

Do not put patient names, IDs or other direct identifiers into `clinical_context.json`.

For public GitHub fixtures, use synthetic/non-identifying examples only.


## Explicit TCP request

TCP is intentionally **opt-in**. BioRT does not choose a tumor model from a structure name such as `PTV_70`.

To request TCP, specify an exact model ID from `tcp_parameters_v2.json`.

### Prescription-dose model example

```json
{
  "tumor": {
    "diagnosis": "prostate_cancer",
    "histology": "adenocarcinoma",
    "risk_group": "high"
  },
  "treatment": {
    "setting": "definitive_sbrt"
  },
  "tcp": {
    "model_id": "royce_2021_prostate_sbrt_ffbr_5y_high",
    "target_role": "prescription_course"
  }
}
```

For a prescription-dose model, BioRT uses the RTPLAN fraction count and target prescription dose/fractionation after checking the selected model's source domain.

### Target-DVH model example

For a model derived from an actual target DVH, the exact RTSTRUCT ROI and its semantic role must both be explicit:

```json
{
  "tumor": {
    "diagnosis": "prostate_cancer"
  },
  "treatment": {
    "setting": "definitive_ebrt"
  },
  "tcp": {
    "model_id": "sachpazidis_2020_prostate_gland_lq_poisson_mixed_followup",
    "target_structure_name": "Prostate",
    "target_role": "prostate_gland"
  }
}
```

BioRT performs a case-insensitive **exact** ROI-name lookup for TCP targets. It does not use substring/fuzzy matching. Therefore `Prostate` cannot silently resolve to `PTV_Prostate`.

The explicit `target_role` must also equal the source model target definition. A `target_role` of `ptv` is rejected for the Sachpazidis prostate-gland model even if a PTV DVH is available.

This separates two questions:

```text
Which RTSTRUCT contour should be sampled?
        target_structure_name

What biological target does that contour represent?
        target_role
```

Both are required for a target-DVH TCP model.

### Risk group and treatment setting

`tumor.risk_group` and `treatment.setting` use the canonical values expected by the selected model record.

Current examples include:

```text
risk_group:
  low_intermediate
  high

setting:
  definitive_ebrt
  definitive_sbrt
  reirradiation_sbrt
```

These fields are intentionally not guessed from prescription dose, fraction count, PTV name or diagnosis.

### Backward compatibility

Schema `0.1.0` remains readable for NTCP-only workflows. New TCP-selection fields were introduced in schema `0.2.0`.

If `tcp.model_id` is omitted, BioRT performs no TCP calculation and all existing physical-plan/NTCP processing continues unchanged.
