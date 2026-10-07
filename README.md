# BioRT

**BioRT** — исследовательский веб-инструмент для физической и радиобиологической оценки планов лучевой терапии по DICOM RT.

> FOR DECISION SUPPORT ONLY. NOT FOR PRIMARY CLINICAL DECISIONS.

## Что уже реализовано

- Локальный browser-side импорт RTPLAN / RTDOSE / RTSTRUCT без отправки DICOM на сервер BioRT.
- DVH и объёмы структур.
- PTV: D2, D98, D95, D50, HI, CI, GI.
- Клинические критерии: Dmean, Dmax, Dxx, Dcc, Vxx (% и cm³).
- NTCP с provenance-aware модельной библиотекой и проверкой применимости модели.
- TCP по TG-166 и source-specific модельной библиотеке.
- LQ / BED / EQD2 и model-specific fractionation handling.
- `clinical_context.json` для переменных, которых нет в DICOM.
- Общий `PlanAnalysisService` для Web и console regression harness.
- Детерминированный SHA-256 commissioning fingerprint для строгого сравнения Web ↔ Console.
- TG-166 математические/аналитические regression tests.
- GitHub Actions для scientific CI, Web CI и GitHub Pages.

## Сайт

Основной пользовательский интерфейс BioRT — **Blazor WebAssembly website**:

```text
https://mesava.github.io/BioRT/
```

`BioRT.Web` локально в памяти браузера принимает:
- RTPLAN;
- RTDOSE;
- RTSTRUCT;
- optional Monaco criteria JSON;
- optional `clinical_context.json`.

После импорта браузер выполняет тот же общий вычислительный путь, что и `BioRT.App`:

```text
DICOM
  -> stream-based import
  -> PlanAnalysisService
  -> DVH / physical metrics
  -> clinical criteria
  -> NTCP / TCP
  -> warnings / provenance
```

Web UI выводит физические метрики плана, PASS/FAIL клинических критериев, NTCP/TCP с идентификатором модели и provenance, а также commissioning fingerprint.

DICOM не должен уходить на внешний сервер только ради расчёта: целевая архитектура оставляет RTPLAN / RTDOSE / RTSTRUCT локально в памяти браузера.

До завершения независимого browser-side commissioning используйте только синтетические или деидентифицированные клинические данные.

Подробнее:
- `docs/web-architecture.md`
- `docs/web-console-commissioning.md`

## Проекты

```text
BioRT.Core   — DVH, физические метрики, TCP/NTCP, fractionation, model engines
BioRT.IO     — DICOM / stream / файловый ввод-вывод
BioRT.App    — локальный console regression/commissioning harness
BioRT.Web    — основной пользовательский Web UI
BioRT.Tests  — scientific and regression tests
```

## Научная модель

BioRT не использует идею «один орган → один коэффициент».

Каждая TCP/NTCP запись связывает:
- model family и equation;
- endpoint;
- time point;
- target/OAR definition;
- fractionation context;
- parameter set;
- PMID/DOI;
- runtime/applicability status.

Параметры из разных публикаций не смешиваются в одну синтетическую модель.

Основные документы:
- `docs/literature.md`
- `docs/tcp-model-foundation.md`
- `docs/tcp-parameter-library.md`
- `docs/fractionation-engine.md`
- `docs/tg166-benchmark.md`

## Commissioning Web ↔ Console

`BioRT.Web` и `BioRT.App` используют один `PlanAnalysisService`, но разные входные пути DICOM.

Для одного и того же деидентифицированного RTPLAN / RTDOSE / RTSTRUCT + JSON начальный строгий критерий приёмки:

```text
web fingerprint == console fingerprint
```

Fingerprint включает геометрию RTDOSE, рассчитанные DVH, объёмы структур, PTV-метрики, clinical criteria, NTCP/TCP и warnings; Patient ID намеренно исключён.

Подробная процедура: `docs/web-console-commissioning.md`.

## Локальный запуск сайта

Требуется .NET 9 SDK.

```bash
dotnet run --project BioRT.Web/BioRT.Web.csproj
```

## Конфиденциальность и статус

BioRT остаётся исследовательским/commissioning-инструментом и не заявляет соответствие конкретному регуляторному режиму медицинского ПО или хранения медицинских данных.

До завершения независимого commissioning на деидентифицированных клинических наборах результаты не должны использоваться как первичное основание для клинического решения.
