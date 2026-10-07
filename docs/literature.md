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


## P0/P1 — modern literature update, 2016–2026

The classical LKB/EUD/TCP papers remain the mathematical foundation, but BioRT must not stop at them. Modern literature mainly changes **how parameter sets are selected, validated, recalibrated and transferred between cohorts/techniques**, rather than replacing the core equations with one universally superior model.

### Modern NTCP methodology, validation and transferability

- [ ] Cella L, et al. *Normal tissue complication probability (NTCP) models for modern radiation therapy*. 2019. PMID 31506196.
  - Review of NTCP in the setting of modern RT, including hypofractionation, ions, reirradiation and voxel/image-based approaches.
- [ ] Van den Bosch L, et al. *Key challenges in normal tissue complication probability model development and validation: towards a comprehensive strategy*. Radiother Oncol. 2020;148:151-156. PMID 32388149.
  - **P0 for BioRT model governance.** Covers overfitting, missing data, multicollinearity, generalisability, multiple toxicity grades/time points and validation strategy.
- [ ] *Generalizability assessment of head and neck cancer NTCP models based on the TRIPOD criteria*. 2020. PMID 32155505.
  - Shows that independent external validation of H&N NTCP models is uncommon; useful for deciding which published models are mature enough for a reference library.
- [ ] Wolff RF, et al. *PROBAST: A Tool to Assess the Risk of Bias and Applicability of Prediction Model Studies*. Ann Intern Med. 2019;170:51-58. PMID 30596875.
  - Methodological quality/risk-of-bias tool for prediction-model literature. BioRT literature extraction should record PROBAST-relevant issues for multivariable models.
- [ ] Tajiki S, et al. *A systematic review of the normal tissue complication probability models and parameters: Head and neck cancers treated with conformal radiotherapy*. Head Neck. 2023;45:3146-3156. PMID 37767820.
  - Important modern catalogue of H&N NTCP models/parameters and endpoints.
- [ ] Lee TF, et al. *Using meta-analysis and CNN-NLP to review and classify the medical literature for normal tissue complication probability in head and neck cancer*. Radiat Oncol. 2024;19:5. PMID 38195582.
  - Modern evidence-mapping/meta-analysis layer; useful for literature completeness, not a direct source of a single clinical coefficient set.
- [ ] *Tackling external validation challenges: experience with normal tissue complication probability (NTCP) models for head and neck cancer radiotherapy toxicities*. 2026. PMID 41874942.
  - Contemporary external-validation evidence; reinforces the need to compare training and validation cohorts and endpoint definitions.
- [ ] *External validation and updating of NTCP models for radiation pneumonitis: QUANTEC, Appelt, and a local simplified model*. 2026. PMID 42130623.
  - Important example of evaluating and recalibrating older/QUANTEC-era models in contemporary IMRT cohorts.
- [ ] *Multicenter External Validation of Normal Tissue Complication Probability Models for Radiation-Induced Primary Hypothyroidism in Head and Neck Cancer Survivors With Long-Term Endocrine Outcomes*. 2026. PMID 42409271.
  - Large multicentre example of discrimination, calibration, Brier score and clinical-utility assessment.

### Modern xerostomia / salivary-gland literature

These papers are especially relevant to the first BioRT endpoint. They should be reviewed **in addition to**, not instead of, the older LKB/parotid papers.

- [ ] Onjukka E, et al. *Modeling of Xerostomia After Radiotherapy for Head and Neck Cancer: A Registry Study*. Front Oncol. 2020;10:1647. PMID 32923404.
  - 753-patient real-world registry; late xerostomia at multiple time points; Cox-based models using total/contralateral parotid mean dose plus clinical factors.
- [ ] Tambas M, et al. *First experience with model-based selection of head and neck cancer patients for proton therapy*. Radiother Oncol. 2020;151:206-213. PMID 32768508.
  - Demonstrates real clinical use of NTCP differences for treatment-technique selection.
- [ ] *National Protocol for Model-Based Selection for Proton Therapy in Head and Neck Cancer*. 2021. PMID 34285961.
  - **High priority for BioRT decision-support design.** Shows how NTCP models can be embedded in a governed clinical selection protocol rather than used as free-standing percentages.
- [ ] Chao M, et al. *Cluster model incorporating heterogeneous dose distribution of partial parotid irradiation for radiotherapy induced xerostomia prediction with machine learning methods*. Acta Oncol. 2022;61:842-848. PMID 35527717.
  - Research direction showing potential information beyond simple mean dose; not a replacement for the validated classical branch.
- [ ] *Dose response modelling of secretory cell loss in salivary glands using PSMA PET*. Radiother Oncol. 2022. PMID 36368471.
  - Objective voxel-level functional imaging evidence for salivary-gland dose response; useful for future spatial/functional extensions.
- [ ] Mavroidis P, et al. *NTCP modelling of xerostomia after radiotherapy for oropharyngeal cancer using the PRO-CTCAE and CTCAE scoring systems at different time-points post-RT*. Phys Med. 2023;116:103169. PMID 37989042.
  - **High priority.** Fits LKB and other NTCP models at 6–24 months and explicitly demonstrates dependence on scoring system, structure grouping and follow-up time.
- [ ] Chu H, et al. *Three-Dimensional Deep Learning Normal Tissue Complication Probability Model to Predict Late Xerostomia in Patients With Head and Neck Cancer*. IJROBP. 2025;121:269-280. PMID 39147208.
  - International two-institution cohort; compares 3D multimodal DL against a conventional xerostomia NTCP model. Keep as an exploratory/advanced branch, not the initial reference implementation.
- [ ] Dalqvist E, et al. *Validated prediction of xerostomia in a real-world population: a step toward model-guided radiotherapy*. 2025. PMID 40823804.
  - **High priority external validation.** Shows the importance of recalibration and reports limited discrimination despite good recalibrated calibration; directly relevant to how BioRT should label model confidence.
- [ ] *Benchmarking of radiobiological NTCP models in head and neck radiotherapy using independent computational pipelines: an institutional validation study with machine learning augmentation*. 2026. PMID 42453164.
  - Useful primarily as an implementation/independent-pipeline validation example; small event numbers mean it should not be treated as a definitive parameter source.

### Modern TCP / radiobiology literature

- [ ] van Leeuwen CM, et al. *The alfa and beta of tumours: a review of parameters of the linear-quadratic model, derived from clinical radiotherapy studies*. Radiat Oncol. 2018;13:96. PMID 29769103.
  - Already in the TCP foundation section; retained here because it is a core modern review. It reports strong heterogeneity and supports selecting LQ parameters by tumour site, histology, model and endpoint rather than using one generic alpha/beta.
- [ ] McMahon SJ. *The linear quadratic model: usage, interpretation and challenges*. Phys Med Biol. 2019;64:01TR01. PMID 30523903.
  - **P0 modern radiobiology reference.** Required before extending LQ/EQD2 to high-dose-per-fraction TCP/NTCP use.
- [ ] Chaikh A, et al. *Construction of radiobiological models as TCP and NTCP: from dose to clinical effects prediction*. Cancer Radiother. 2020;24:247-257. PMID 32220563.
  - Modern overview linking fractionation correction, gEUD and probability models; useful for architecture cross-checking.
- [ ] Royce TJ, et al. *Tumor Control Probability Modeling and Systematic Review of the Literature of Stereotactic Body Radiation Therapy for Prostate Cancer*. IJROBP. 2021;110:227-236. PMID 32900561.
  - Disease-specific modern TCP example with pooled clinical data and risk-stratified biochemical-control endpoint.
- [ ] Klement RJ, et al. *Estimation of the alpha/beta ratio of non-small cell lung cancer treated with stereotactic body radiotherapy*. Radiother Oncol. 2019. PMID 31431371.
  - Important for SBRT TCP/fractionation sensitivity and uncertainty of alpha/beta.
- [ ] *Tumor control probability modeling for stereotactic body radiation therapy of early-stage lung cancer using multiple bio-physical models*. 2017. PMID 27871671.
  - Useful model-comparison dataset; demonstrates model dependence and stage dependence in lung SBRT.
- [ ] Kutuva AR, et al. *Mathematical modeling of radiotherapy: impact of model selection on estimating minimum radiation dose for tumor control*. Front Oncol. 2023;13:1130966. PMID 37901317.
  - **High priority methodological paper:** explicitly demonstrates that estimated control dose depends on chosen mathematical model.
- [ ] *Re-evaluating the alpha/beta ratio in 2026: A systematic review and quantitative reappraisal in the era of molecular radiobiology*. 2026. PMID 42731947.
  - Current systematic update. Useful for uncertainty ranges and identifying where classical static alpha/beta assumptions remain defensible versus where they are unstable.

### Modern prediction-model governance for BioRT

For multivariable NTCP/TCP models (logistic, Cox, machine-learning, radiomics), BioRT should record not only the formula and coefficients but also:

- development vs internal validation vs independent external validation;
- calibration intercept/slope and calibration plot results;
- discrimination (AUC/C-index) where applicable;
- Brier score or other overall-performance measure;
- need for local recalibration;
- event count and effective sample size;
- missing-data handling;
- predictor definition and preprocessing;
- endpoint definition, grade and time horizon;
- treatment technique and era;
- model-update version.

**Rule:** a recent paper is not automatically a better source. A 2025–2026 model with poor external validity or weak calibration remains experimental; a well-defined older LKB fit may remain a better transparent benchmark for the classical branch.


## P1 — lung / radiation pneumonitis

BioRT now treats lung RP as a dedicated model domain rather than a generic organ row.

- [x] Semenenko VA, Li XA. *LKB NTCP model parameters for radiation pneumonitis and xerostomia based on combined analysis of published clinical data*. Phys Med Biol. 2008. PMID 18199912.
  - Total/paired lung symptomatic RP: TD50 29.9 Gy, m 0.41, n=1 fixed.
  - Ipsilateral lung: TD50 37.6 Gy, m 0.35, n=1 fixed.
  - Mean-dose EQD2 correction with alpha/beta=3 Gy for source cohorts using non-2-Gy daily fractions.
- [x] Marks LB, et al. *Radiation dose-volume effects in the lung*. QUANTEC. IJROBP. 2010. PMID 20171521.
  - Logistic MLD model: intercept -3.87, MLD coefficient 0.126/Gy.
  - Probit/Lyman fit to the same pooled response: TD50 31.4 Gy, m 0.45, n=1.
  - Conventional-fractionation guidance: V20 about <=30–35%, MLD <=20–23 Gy for symptomatic RP risk around <=20%.
- [x] Appelt AL, et al. *Towards individualized dose constraints: Adjusting the QUANTEC radiation pneumonitis model for clinical risk factors*. Acta Oncol. 2014. PMID 23957623.
  - Adds age, smoking, pulmonary comorbidity, tumor location and chemotherapy sequence to MLD.
  - Independently tested in 103 patients.
- [x] Niezink AGH, et al. *External validation of NTCP-models for radiation pneumonitis in lung cancer patients treated with chemoradiotherapy*. Radiother Oncol. 2023. PMID 37327975.
  - Prospective 612-patient modern RT cohort.
  - Original QUANTEC/Appelt required updating.
  - Final New-RP model uses MLD, continuous age, and current/recent-smoking status.
  - Source lung definition: Lungs-GTV; SBRT excluded.
- [x] De Ruysscher D, et al. *Diagnosis and treatment of radiation induced pneumonitis in patients with lung cancer: An ESTRO clinical practice guideline*. Radiother Oncol. 2025. PMID 40185160.
  - Modern clinical context for RP risk factors and dose-volume guidance.
  - Recommends both lungs minus GTV for MLD/V20 definition.
- [x] Chen Z, et al. *External validation and updating of NTCP models for radiation pneumonitis: QUANTEC, Appelt, and a local simplified model*. Front Oncol. 2026. PMID 42130623.
  - Contemporary IMRT/multimodal validation shows substantial calibration drift of historical models.
  - Model D preserves ranking in an external cohort but overestimates absolute risk.
  - BioRT does not implement Model D yet because the article text and displayed final coefficient specification are inconsistent about tumor-location inclusion.
- [x] Moiseenko V, et al. *Dose-Volume Predictors of Radiation Pneumonitis After Lung SBRT: Implications for Practice and Trial Design*. 2020. PMID 33163312.
  - Summarizes HyTEC-era SBRT guidance around MLD <8 Gy and V20 <10–15%; retained as SBRT-specific reference evidence, not as a conventional LKB extrapolation.

