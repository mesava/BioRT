# TCP mathematical foundation

BioRT implements TCP model families separately. Parameters from one family must not be silently inserted into another.

The first mathematical layer follows AAPM TG-166, Section II.D.

## General non-uniform target formulation

TG-166 writes TCP for a non-uniform target as a volume-weighted product:

```text
TCP = product_i P(D_i)^v_i
```

where `v_i` is the relative volume of a differential DVH bin and `D_i` is its dose.

BioRT stores cumulative DVHs, so differential fractional volumes are reconstructed before TCP evaluation.

## LQ Poisson formulation

TG-166 Eq. (6):

```text
P(D_i) =
exp(
  -exp(
    e*gamma
    - alpha*D_i
    - beta*D_i^2/N
  )
)
```

where `N` is the number of fractions.

Given `D50`, normalized dose-response gradient `gamma`, and `alpha/beta`, TG-166 Eqs. (7) and (8) give:

```text
C = e*gamma - ln(ln(2))

alpha =
C / [ D50 * (1 + 2/(alpha/beta)) ]

beta =
C / [ D50 * (alpha/beta + 2) ]
```

The derived values satisfy the requested `alpha/beta` ratio by construction.

## Linear-Poisson / LQED2 formulation

TG-166 Eq. (9):

```text
P(D_i) =
exp(
  -exp(
    e*gamma
    - (D_i/D50) *
      (e*gamma - ln(ln(2)))
  )
)
```

When physical dose in every differential DVH bin is first converted to `LQED2` with the same `alpha/beta`, TG-166 states that this formulation is equivalent to the LQ Poisson formulation above.

BioRT implements both formulations independently and has an automated regression test that requires them to agree on the same heterogeneous synthetic DVH.

This gives us an important end-to-end check of:
- cumulative-to-differential DVH conversion;
- the LQ fractionation engine;
- the TCP Poisson implementation.

## Empirical logistic formulation

TG-166 Eq. (10), citing Okunieff et al.:

```text
P(D_i) =
exp[(D_i-D50)/k] /
(1 + exp[(D_i-D50)/k])

k = D50 / (4*gamma)
```

BioRT combines the local probability over differential target volumes using the same volume-weighted product.

This model is not the same as:
- the Poisson/LQ model;
- the linear-Poisson model;
- a gEUD/Niemierko wrapper;
- a model with explicit repopulation or interpatient heterogeneity.

Each will remain a distinct model family.

## Current analytical QA

The automated tests verify:

```text
uniform D = D50 -> TCP = 0.5
```

for:
- LQ Poisson;
- linear-Poisson + EQD2;
- empirical logistic.

They also verify:
- alpha/beta recovery from derived alpha and beta;
- monotonic increase of TCP with dose;
- reduction of TCP when a cold target subvolume is introduced;
- LQ-Poisson == linear-Poisson+EQD2 under identical assumptions;
- invalid fraction counts are rejected.

## Important limitations

This PR establishes model mathematics only. It deliberately does **not** promote the existing legacy `tcp_ntcp_params.json` tumor rows to validated runtime models.

A disease-specific TCP parameter record still requires:
- exact tumor entity;
- histology;
- gross vs microscopic disease / treatment intent;
- target structure definition;
- endpoint (local control, biochemical control, etc.);
- follow-up horizon;
- fractionation domain;
- model family used to fit the parameters;
- source and validation status.

The same `D50`, `gamma`, `alpha/beta`, `alpha`, `SF2`, `N0`, `Tpot` and `Tk` values must never be assembled from unrelated papers into one synthetic model.

## Sources

- AAPM Task Group 166. *The Use and QA of Biologically Related Models for Treatment Planning*. AAPM Report No. 166, 2012.
- Lind BK, Mavroidis P, Hyödynmaa S, Kappas C. *Optimization of the dose level for a given treatment plan to maximize the complication-free tumor cure*. Acta Oncol. 1999;38(6):787-798. PMID 10522770.
- Okunieff P, Morgan D, Niemierko A, Suit HD. *Radiation dose-response of human tumors*. IJROBP. 1995;32(4):1227-1237. PMID 7607946.
