# BioRT

**BioRT** — исследовательский веб-инструмент для физической и радиобиологической оценки планов лучевой терапии по DICOM RT.

> FOR DECISION SUPPORT ONLY. NOT FOR PRIMARY CLINICAL DECISIONS.

## Что уже реализовано

- RTPLAN / RTDOSE / RTSTRUCT импорт в текущем локальном пайплайне.
- DVH и объёмы структур.
- PTV: D2, D98, D95, D50, HI, CI, GI.
- Клинические критерии: Dmean, Dmax, Dxx, Dcc, Vxx (% и cm³).
- NTCP с provenance-aware модельной библиотекой.
- TCP по TG-166 и source-specific модельной библиотеке.
- LQ / BED / EQD2 и model-specific fractionation handling.
- `clinical_context.json` для переменных, которых нет в DICOM.
- TG-166 математические/аналитические regression tests.

## Сайт

Основной пользовательский интерфейс BioRT теперь развивается как **Blazor WebAssembly website**:

```text
BioRT.Web
```

После включения GitHub Pages целевой адрес:

```text
https://mesava.github.io/BioRT/
```

Первый web milestone уже показывает модельный каталог TCP/NTCP непосредственно из рабочих JSON-библиотек проекта.

Пациентский DICOM пока **не обрабатывается публичным web UI**. Следующий этап — stream-based DICOM importer и вычисление полностью в браузере.

Почему именно так: DICOM не должен уходить на внешний сервер только ради расчёта. Целевая архитектура оставляет RTPLAN / RTDOSE / RTSTRUCT локально в памяти браузера.

Подробнее: `docs/web-architecture.md`.

## Проекты

```text
BioRT.Core   — DVH, физические метрики, TCP/NTCP, fractionation, model engines
BioRT.IO     — DICOM / файловый ввод-вывод
BioRT.App    — локальный Windows/console regression harness
BioRT.Web    — основной пользовательский web UI
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

## Локальный запуск сайта

Требуется .NET 9 SDK.

```bash
dotnet run --project BioRT.Web/BioRT.Web.csproj
```

## Конфиденциальность

До завершения browser-side DICOM commissioning публичную версию сайта следует использовать только с синтетическими или деидентифицированными данными пациента.

BioRT не заявляет соответствие конкретному регуляторному режиму хранения медицинских данных.
