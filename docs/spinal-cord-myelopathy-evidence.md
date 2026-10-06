# Spinal cord / radiation myelopathy — evidence extraction

BioRT treats spinal-cord myelopathy as a second validation domain after parotid/xerostomia because the spinal cord is a strongly serial organ and therefore stresses a very different region of the LKB volume-effect parameter space.

## Legacy LKB benchmark

### Burman et al. 1991

Legacy LKB parameters fitted to the Emami tolerance compilation:

```text
TD50 = 66.5 Gy
m    = 0.175
n    = 0.05
```

Endpoint: myelitis / necrosis.

BioRT stores this as:

```text
burman_1991_spinal_cord_lkb_myelopathy
```

Status: `legacy_reference`.

It is useful as a transparent historical LKB benchmark, but not as a modern universal absolute-risk model. Runtime evaluation is deliberately limited to approximately conventional 1.8–2.0 Gy/fraction.

## QUANTEC 2010 — conventional fractionation

Kirkpatrick, van der Kogel and Schultheiss reviewed human spinal-cord myelopathy data.

For conventional irradiation of the full cord cross-section at about 2 Gy/day, the report gives approximate myelopathy risks:

```text
50 Gy  -> 0.2%
60 Gy  -> 6%
~69 Gy -> 50%
```

For the fitted cervical-cord data:

```text
D50 = 69.4 Gy
95% CI = 66.4–72.6 Gy
alpha/beta = 0.87 Gy
95% CI = 0.54–1.19 Gy
```

These values are **not encoded as an LKB parameter set**. They belong to the probability-distribution analysis used in the QUANTEC review.

BioRT therefore stores QUANTEC as `reference_risk_table` evidence and does not interpolate the three risk points into a continuous NTCP curve.

Source:
- Kirkpatrick JP, van der Kogel AJ, Schultheiss TE. *Radiation dose-volume effects in the spinal cord*. IJROBP. 2010.
- PMID 20171517.
- DOI 10.1016/j.ijrobp.2009.04.095.

## HyTEC — de novo spine SBRT

The 2021 HyTEC spinal-cord review is stored separately from conventional-fractionation evidence.

For de novo spine SBRT, point maximum doses associated with an estimated 1–5% risk of radiation myelopathy are approximately:

```text
1 fraction : 12.4–14.0 Gy
2 fractions: 17.0 Gy
3 fractions: 20.3 Gy
4 fractions: 23.0 Gy
5 fractions: 25.3 Gy
```

BioRT intentionally does not obtain these numbers by EQD2 extrapolation from the legacy Burman LKB model.

Source:
- Sahgal A et al. *Spinal Cord Dose Tolerance to Stereotactic Body Radiation Therapy*. IJROBP. 2021.
- PMID 31606528.
- DOI 10.1016/j.ijrobp.2019.09.038.

## HyTEC — reirradiation

For spine SBRT reirradiation, lower-risk factors reported by HyTEC include:

```text
cumulative thecal-sac EQD2_2 Dmax <= 70 Gy
SBRT-component EQD2_2 Dmax <= 25 Gy
SBRT / cumulative EQD2 Dmax ratio <= 0.5
minimum interval >= 5 months
alpha/beta = 2 Gy
```

This requires prior-course information and spatially corresponding maximum-dose information. It cannot be evaluated correctly from a single RTPLAN/RTDOSE folder.

BioRT therefore stores it as a non-runtime reference model.

## ReTEC 2026

The 2026 ReTEC proof-of-concept study compiled 13 publications with 282 lesions and six myelopathy events.

The fitted model suggested more than 50% recovery within one year, but uncertainty was large (bootstrap 95% CI approximately 31–95% at one year).

The authors explicitly recommend remaining within current standard guidelines until stronger evidence is available.

BioRT stores the ReTEC item as `experimental` and does not calculate it.

Source:
- Grimm J et al. *Reirradiation treatment effects in the clinic (ReTEC) proposal – proof of concept based on spinal cord dose tolerance for reirradiation with stereotactic body radiotherapy*. JACMP. 2026.
- PMID 41954028.
- DOI 10.1002/acm2.70557.

## Implementation rule

The spinal cord illustrates why one field named `NTCP spinal cord` is scientifically inadequate.

BioRT now keeps these domains separate:

```text
legacy conventional LKB
QUANTEC conventional human-risk evidence
HyTEC de novo SBRT
HyTEC reirradiation
ReTEC experimental reirradiation
```

A dose distribution may be mathematically convertible to EQD2 while still being outside the applicability domain of a model. Fractionation conversion is therefore not used as permission to extrapolate a model.
