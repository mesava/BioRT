# BioRT — required scientific literature

This file is the literature checklist for scientific validation of the TCP/NTCP engine.

The rule for BioRT is: **a numerical parameter is not accepted into the clinical/reference library unless the model, endpoint, cohort/fractionation context, organ definition and source can be traced together.** Parameters from different papers must not be averaged or mixed into a synthetic parameter set unless that new fit is explicitly performed and validated.

## P0 — mathematical and QA foundation

- [x] AAPM Task Group 166. *The Use and QA of Biologically Related Models for Treatment Planning*. AAPM Report No. 166 (2012).
  - Role: architecture, model definitions, limitations, commissioning and benchmark strategy.
- [ ] Lyman JT. *Complication probability as assessed from dose-volume histograms*. Radiat Res Suppl. 1985;8:S13-S19. PMID 3867079.
  - Role: original Lyman NTCP formulation.
- [ ] Kutcher GJ, Burman C. *Calculation of complication probability factors for non-uniform normal tissue irradiation: the effective volume method*. IJROBP. 1989;16:1623-1630. PMID 2722599.
  - Role: effective-volume reduction for heterogeneous DVHs.
- [ ] Emami B, et al. *Tolerance of normal tissue to therapeutic irradiation*. IJROBP. 1991;21:109-122.
  - Role: historical tolerance dataset. **Legacy/reference only**, not default modern truth.
- [ ] Burman C, Kutcher GJ, Emami B, Goitein M. *Fitting of normal tissue tolerance data to an analytic function*. IJROBP. 1991;21:123-135. PMID 2032883.
  - Role: classic Emami-to-Lyman parameter fits.
- [ ] Kutcher GJ, Burman C, Brewster L, Goitein M, Mohan R. *Histogram reduction method for calculating complication probabilities for three-dimensional treatment planning evaluations*. IJROBP. 1991;21:137-146.
  - Role: DVH reduction/implementation benchmark.
- [ ] Mohan R, et al. *Clinically relevant optimization of 3-D conformal treatments*. Med Phys. 1992;19:933-944. PMID 1518482.
  - Role: Deff formulation and clinically oriented biological evaluation.
- [ ] Niemierko A. *Reporting and analyzing dose distributions: a concept of equivalent uniform dose*. Med Phys. 1997;24:103-110.
  - Role: EUD foundation.
- [ ] Niemierko A. *A generalized concept of equivalent uniform dose (EUD)*. Med Phys. 1999;26:1100.
  - Role: generalized EUD / volume-effect parameter.
- [ ] Deasy JO. *Comments on the use of the Lyman-Kutcher-Burman model to describe tissue response to nonuniform irradiation*. IJROBP. 2000;47:1458-1460. PMID 10939885.
  - Role: interpretation/implementation cautions.
- [ ] Marks LB, et al. *Use of normal tissue complication probability models in the clinic*. IJROBP. 2010;76:S10-S19. PMID 20171502.
  - Role: clinical use and limitations of NTCP.
- [ ] Bentzen SM, et al. *Quantitative Analyses of Normal Tissue Effects in the Clinic (QUANTEC): an introduction to the scientific issues*. IJROBP. 2010;76:S3-S9. PMID 20171515.
- [ ] Jackson A, et al. *The lessons of QUANTEC: recommendations for reporting and gathering data on dose-volume dependencies of treatment outcome*. IJROBP. 2010;76:S155-S160. PMID 20171512.

## P1 — QUANTEC organ-specific normal-tissue evidence

These papers are required before BioRT promotes the corresponding adult conventional-fractionation endpoint from "experimental" to "reference".

- [ ] Brain — Lawrence YR, et al. *Radiation dose-volume effects in the brain*. PMID 20171513.
- [ ] Optic nerves/chiasm — Mayo C, et al. *Radiation dose-volume effects of optic nerves and chiasm*. PMID 20171514.
- [ ] Brainstem — Mayo C, Yorke E, Merchant TE. *Radiation associated brainstem injury*. PMID 20171516.
- [ ] Spinal cord — Kirkpatrick JP, et al. *Radiation dose-volume effects in the spinal cord*. PMID 20171517.
- [ ] Salivary glands/parotid — Deasy JO, et al. *Radiotherapy dose-volume effects on salivary gland function*. PMID 20171519.
- [ ] Hearing/cochlea — Bhandare N, et al. *Radiation therapy and hearing loss*. PMID 20171518.
- [ ] Lung — Marks LB, et al. *Radiation dose-volume effects in the lung*. PMID 20171521.
- [ ] Heart — Gagliardi G, et al. *Radiation dose-volume effects in the heart*. PMID 20171522.
- [ ] Esophagus — Werner-Wasik M, et al. *Radiation dose-volume effects in the esophagus*. PMID 20171523.
- [ ] Liver — Pan CC, et al. *Radiation-associated liver injury*. PMID 20171524.
- [ ] Stomach / small bowel — Kavanagh BD, et al. *Radiation dose-volume effects in the stomach and small bowel*. PMID 20171503.
- [ ] Kidney — Dawson LA, et al. *Radiation-associated kidney injury*. PMID 20171504.
- [ ] Urinary bladder — Viswanathan AN, et al. *Radiation dose-volume effects of the urinary bladder*. PMID 20171505.
- [ ] Rectum — Michalski JM, et al. *Radiation dose-volume effects in radiation-induced rectal injury*. PMID 20171506.
- [ ] Penile bulb — Roach M 3rd, et al. *Radiation dose-volume effects and the penile bulb*. PMID 20171507.

## P1 — first fully traceable NTCP endpoint: parotid function / xerostomia

BioRT should validate this endpoint first because the volume effect is near parallel (n≈1) in several datasets, making the full calculation chain easy to audit.

- [ ] Eisbruch A, et al. *Dose, volume, and function relationships in parotid salivary glands following conformal and intensity-modulated irradiation of head and neck cancer*. 1999. PMID 10524409.
  - Example historical fit: TD50 28.4 Gy, n=1, m=0.18; endpoint is salivary function.
- [ ] Roesink JM, et al. *Quantitative dose-volume response analysis of changes in parotid gland function after radiotherapy in the head-and-neck region*. 2001. PMID 11704314.
  - Time-dependent recovery; TD50 changes with follow-up.
- [ ] Semenenko VA, Li XA. *LKB NTCP model parameters for radiation pneumonitis and xerostomia based on combined analysis of published clinical data*. 2008. PMID 18199912.
  - Combined-analysis fit; endpoint and EQD2 handling must be preserved.
- [ ] Dijkema T, et al. *Large cohort dose-volume response analysis of parotid gland function after radiotherapy: IMRT versus conventional radiotherapy*. 2008. PMID 18472355.
- [ ] Dijkema T, et al. *Parotid gland function after radiotherapy: the combined Michigan and Utrecht experience*. 2010. PMID 20056347.
  - One-year endpoint; mean-dose fit TD50 39.9 Gy, m=0.40.
- [ ] Houweling AC, et al. *A comparison of dose-response models for the parotid gland in a large group of head-and-neck cancer patients*. 2010. PMID 20005639.
  - Useful for choosing mean-dose vs full LKB formulations.
- [ ] Deasy JO, et al. QUANTEC salivary gland review. PMID 20171519.
- [ ] Nutting CM, et al. PARSPORT phase III trial.
  - Clinical validation of parotid-sparing IMRT context.
- [ ] *Dose-response analysis of parotid gland function: what is the best measure of xerostomia?* 2013. PMID 23566529.
  - Demonstrates endpoint dependence of fitted TD50/m.

## P1 — TCP mathematical foundation

- [ ] Munro TR, Gilbert CW. *The relation between tumour lethal doses and the radiosensitivity of tumour cells*. Br J Radiol. 1961.
  - Poisson zero-surviving-clonogen basis.
- [ ] Källman P, Ågren A, Brahme A. *Tumour and normal tissue responses to fractionated non-uniform dose delivery*. Int J Radiat Biol. 1992;62:249-262. PMID 1355519.
  - Non-uniform dose, Poisson/response modeling.
- [ ] Okunieff P, Morgan D, Niemierko A, Suit HD. *Radiation dose-response of human tumors*. IJROBP. 1995;32:1227-1237. PMID 7607946.
  - Empirical TCD50/dose-response slopes from clinical datasets.
- [ ] Lind BK, et al. Poisson/LQ formulations used by historical biological-evaluation systems.
  - Needed before implementing a D50/gamma50/alpha-beta Poisson-LQ branch.
- [ ] TG-166 Appendix B.
  - Repopulation and interpatient heterogeneity extensions.
- [ ] van Leeuwen CM, et al. *The alfa and beta of tumours: a review of parameters of the linear-quadratic model, derived from clinical radiotherapy studies*. Radiat Oncol. 2018;13:96. PMID 29769103.
  - Required for selecting alpha/beta by tumour site/histology/model, not as a universal table.

### Disease-specific TCP literature to curate after the mathematical branch is fixed

Do **not** create a generic parameter row only from an anatomical PTV name. For every disease model we need diagnosis/histology/stage/endpoint and a model-specific parameter set.

Initial priority:
- [ ] Prostate — randomized-trial fractionation/dose-response literature; Vogelius & Bentzen meta-analyses (PMID 29485063; PMID 31987958), plus model-specific TCP papers.
- [ ] Head & neck SCC — Poisson/LQ local-control and accelerated-repopulation literature.
- [ ] NSCLC — local-control/TCP literature separated into conventional RT and SBRT.
- [ ] Cervix — local-control models including overall treatment time / repopulation.
- [ ] Breast — local-control/fractionation models; distinguish microscopic/adjuvant control from gross disease.
- [ ] Rectal cancer — preoperative/definitive context must be separated.
- [ ] Glioma / glioblastoma — endpoint and target definition specific.
- [ ] Brain metastases — histology and SRS/fSRS context specific.
- [ ] Melanoma — do not reuse generic brain-metastasis parameters without a disease-specific source.

## P2 — high dose per fraction / SRS / SBRT

- [ ] Grimm J, Marks LB, Jackson A, et al. *High Dose per Fraction, Hypofractionated Treatment Effects in the Clinic (HyTEC): An Overview*. IJROBP. 2021;110:1-10. PMID 33864823.
- [ ] Organ-specific HyTEC papers relevant to each implemented SBRT/SRS endpoint.
  - Liver SBRT: Miften M, et al. *Radiation Dose-Volume Effects for Liver SBRT*. PMID 29482870.
  - Lung SBRT toxicity / pneumonitis.
  - CNS/brain radionecrosis.
  - Spinal cord / cauda equina.
  - Optic apparatus.
  - Brachial plexus.
  - Chest wall/ribs.
  - Major vessels / reirradiation where applicable.
- [ ] AAPM TG-101 for SBRT physical dose constraints and QA context.
- [ ] LQ-model high-dose-per-fraction limitation papers (Brenner vs Kirkpatrick/Meyer/Marks debate).
  - BioRT must expose when a model is extrapolated beyond its validation domain.

## P2 — pediatrics

- [ ] Constine LS, et al. *A User's Guide and Summary of PENTEC*. IJROBP. 2024;119:321-337. PMID 37999712.
- [ ] Organ-specific PENTEC reviews only when pediatric calculations are enabled.
  - Lung: PMID 35525723.
  - Kidney: PMID 37452796.
  - Liver: PMID 37480885.
  - Hearing: PMID 37855793.
  - Thyroid: PMID 33810948.
  - Central endocrine: PMID 37269265.
  - Reproductive organs and other PENTEC endpoints as needed.

## P2 — post-QUANTEC / modern validation

For each endpoint selected from QUANTEC, search for:
- external validation studies;
- modern IMRT/VMAT/proton cohorts;
- post-QUANTEC systematic reviews/meta-analyses;
- endpoint-specific alpha/beta/fractionation analyses;
- clinically important covariates (chemotherapy, baseline organ function, age, smoking, diabetes, etc.).

Examples already identified:
- [ ] Brand DH, et al. *Estimates of Alpha/Beta Ratios for Individual Late Rectal Toxicity Endpoints: An Analysis of the CHHiP Trial*. PMID 33412260.
- [ ] Systematic post-QUANTEC review of prostate late toxicity. PMID 30125635.

## Required metadata to extract from every accepted parameter paper

For every model parameter set entered into BioRT, record:

- model family and exact equation;
- organ/tumour entity;
- target/OAR structure definition;
- complication/control endpoint;
- endpoint grade and scoring system;
- endpoint time point;
- cohort size and disease context;
- treatment technique;
- fractionation and whether dose was physical or LQ/EQD2-corrected;
- parameter estimates and confidence intervals;
- fitting constraints/assumptions (for example fixed n=1);
- validation cohort, if any;
- primary DOI/PMID;
- implementation status: legacy / reference / validated / experimental.

## What is intentionally not accepted

- Averaging TD50/m/n from different time points.
- Combining alpha, alpha/beta, SF2, N0, TCD50 and gamma50 from unrelated publications into one synthetic tumour model.
- Using Emami/Burman parameter fits as a modern default without labeling them as legacy.
- Applying conventional-fractionation parameters automatically to SRS/SBRT.
- Computing TCP from an arbitrary PTV name without diagnosis/histology/target-context metadata.
