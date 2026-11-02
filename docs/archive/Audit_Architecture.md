# Architecture Audit — N.StCut

Аудит проведён как Senior C# / WPF Software Architect. Оценивается вся кодовая база (`NStCut.Core`, `NStCut.Dxf`, `NStCut.Nesting`, `NStCut.Reports`, `NStCut.App` + тестовые проекты). **Код не изменялся, ошибки не исправлялись — только анализ.**

---

# Общая оценка (0–10): 7/10

Проект построен по чистой многослойной структуре с явным разделением ответственности между сборками (`Core` не зависит ни от чего, `Dxf`/`Nesting`/`Reports` зависят только от `Core`, `App` зависит от всех через интерфейсы `Contracts`). Циклических зависимостей нет. Композиция объекта — простой "poor man's DI" в `App.xaml.cs`, что адекватно для приложения такого размера. Основные потери баллов — не в структуре сборок, а в деталях: несколько мест с хардкодом (цвета статуса строк, магические числа геометрической точности, дублирующийся URL-путь настроек), "God ViewModel" в `NStCut.App`, отсутствие DRY в drag&drop-коде и найденный ранее (см. `docs/Code_Audit_UI.md`) баг с `GroupStyle`/`Refresh()`. Заготовки под масштабирование (интерфейсы `INestingEngine`, `IDxfGeometryService`, `IDxfExportService`, `IReportService`) уже заложены и облегчат добавление новых алгоритмов/форматов.

---

## Сильные стороны

1. **Чёткое разделение сборок по слоям**: `NStCut.Core` (геометрия, модели, контракты) не имеет ни одной зависимости — идеальный "ядерный" слой, который могут переиспользовать `Dxf`, `Nesting`, `Reports`, `App`, не зная друг о друге.
2. **Инверсия зависимостей на границе Core ↔ Infrastructure**: `INestingEngine`, `IDxfGeometryService`, `IDxfExportService`, `IReportService` определены в `Core.Contracts` и реализуются в соответствующих сборках; `MainViewModel` принимает их через конструктор как интерфейсы, а не создаёт напрямую — DIP соблюдён на этом уровне (<ref_snippet file="D:\My_project\N.StCut\src\NStCut.App\ViewModels\MainViewModel.cs" lines="60-66" />).
3. **Отдельные тестовые проекты на каждый слой** (`NStCut.Core.Tests`, `NStCut.Dxf.Tests`, `NStCut.Nesting.Tests`, `NStCut.Reports.Tests`, `NStCut.App.Tests`, `NStCut.IntegrationTests`) — хорошая база для регрессионного тестирования при рефакторинге.
4. **Константы раскроя централизованы**: `AppConstants`/`DefaultNestingOptions` (<ref_file file="D:\My_project\N.StCut\src\NStCut.Core\Constants\AppConstants.cs" />) задают значения по умолчанию в одном месте, а не разбросаны по коду — хороший паттерн, частично не соблюдённый в другом месте (см. DRY Assessment, D2).
5. **Осознанная миграция формата проекта**: `ProjectFileService.MigrateLegacyPartNames` (<ref_snippet file="D:\My_project\N.StCut\src\NStCut.App\Services\ProjectFileService.cs" lines="34-52" />) аккуратно обрабатывает обратную совместимость старых `.nstcut`-файлов — признак зрелого отношения к сохранению данных пользователя.
6. **Геометрия трактуется как источник истины без мутации исходных данных**: `DxfGeometryService.ReadContour` только читает контур и возвращает новый `DxfContour`/`Rect`, не модифицирует исходный DXF — соответствует правилу проекта "Never modify original DXF entities".

---

## Critical Issues

### A1. Цвета статуса строки хардкожены в C# и дублируют (с расхождением) уже существующие, но неиспользуемые ресурсы темы

- **Файл**: <ref_file file="D:\My_project\N.StCut\src\NStCut.App\Converters\StatusToBrushConverter.cs" />, строки 16–23
- **Элемент**: `StatusToBrushConverter.Convert` — `Brushes.LightGreen`, `Brushes.LightYellow`, `Brushes.Orange`, `Brushes.LightBlue`
- **Причина**: цвета для `PartStatus` (Found/ManuallySelected/NotFound/Ambiguous) заданы напрямую как статические `System.Windows.Media.Brushes.*` в коде конвертера, а не через `DynamicResource`/тему. При этом в <ref_file file="D:\My_project\N.StCut\src\NStCut.App\AppTheme.xaml" /> **уже существуют** именованные ресурсы с говорящими именами под ту же семантику — `NotFoundRowBrush` (#F7C7AC), `ManuallySelectedRowBrush` (#FFF7DC), `AmbiguousRowBrush` (#C3E0E5) — но они **нигде не используются** (проверено поиском по всему проекту `NStCut.App`: единственное совпадение — само объявление в `AppTheme.xaml`). Получились две параллельные, несогласованные системы цвета для одной и той же концепции.
- **Влияние**: (1) мёртвый код/ресурсы, вводящие в заблуждение при поддержке; (2) при попытке реализовать тёмную тему (см. `docs/UI_Design_System.md`, где палитра строится через `DynamicResource`) переключение темы **не изменит** цвета статуса строк, потому что они зашиты как константы в C#, а не берутся из `ResourceDictionary` — то есть эта архитектурная деталь прямо блокирует уже согласованную UX-концепцию.
- **Рекомендация**: переписать `StatusToBrushConverter` на `Application.Current.TryFindResource("...Brush")` (по аналогии с уже существующим `ActiveMaterialHighlightConverter`, см. ниже), используя существующие именованные ресурсы, либо удалить неиспользуемые ресурсы, если от них решено отказаться, — но не поддерживать две системы одновременно.

### A2. Магическое число геометрической точности `0.001` продублировано в двух местах и не совпадает с уже сконфигурированным `NestingOptions.Resolution` (0.01), который нигде не используется

- **Файлы**:
  - <ref_file file="D:\My_project\N.StCut\src\NStCut.Nesting\Internals\OrientationHelper.cs" />, строка 18: `Math.Abs(w - h) > 0.001`
  - <ref_file file="D:\My_project\N.StCut\src\NStCut.Nesting\Internals\ShelfPacker.cs" />, строка 58: `Math.Abs(w - part.Bounds.Width) < 0.001`
- **Причина**: в `NStCut.Core.Models.NestingOptions` уже есть настраиваемое поле `Resolution` (по умолчанию `0.01`, см. <ref_file file="D:\My_project\N.StCut\src\NStCut.Core\Models\NestingOptions.cs" /> и `AppConstants.ComparisonResolution`), явно предназначенное для допуска сравнения геометрии. Но ни `ShelfPacker`, ни `OrientationHelper` его не принимают и не используют — вместо этого оба независимо хардкодят собственное значение `0.001`, которое к тому же **не совпадает** со значением по умолчанию в `NestingOptions` (0.001 против 0.01).
- **Влияние**: прямое нарушение приоритета проекта №1 "Correct geometry" и №2 "Deterministic nesting" (см. `AGENTS.md` проекта) — если пользователь или разработчик в будущем поменяет `NestingOptions.Resolution` (например, из UI параметров раскроя), это **не повлияет** на решения о повороте детали или определении ориентации, потому что реальные сравнения используют захардкоженную константу, а не переданный `Resolution`. Это "мёртвая" настройка, создающая ложное ощущение конфигурируемости.
- **Рекомендация**: передавать `options.Resolution` в `ShelfPacker`/`OrientationHelper.GetOrientations` явным параметром вместо хардкода `0.001`, и убрать расхождение значений.

### A3. Путь настроек приложения ("NStCut" как имя папки в `%AppData%`) продублирован в двух независимых сервисах

- **Файлы**:
  - <ref_file file="D:\My_project\N.StCut\src\NStCut.App\Services\AppSettingsService.cs" />, строка 14: `Path.Combine(..., "NStCut", "app-settings.json")`
  - <ref_file file="D:\My_project\N.StCut\src\NStCut.App\Services\MaterialsLibraryService.cs" />, строка 14: `Path.Combine(..., "NStCut", "materials-library.json")`
- **Причина**: оба сервиса независимо жёстко прописывают одну и ту же папку `"NStCut"` в `%AppData%`, вместо использования общей константы/базового пути.
- **Влияние**: при необходимости переименовать папку данных приложения (например, для будущего лицензирования/мультипрофильности, см. раздел "Масштабируемость") нужно помнить о правке в двух не связанных друг с другом местах — источник рассинхронизации.
- **Рекомендация**: вынести `%AppData%\NStCut` в общую константу (например, `AppPaths.RootFolder` в `NStCut.App` или даже в `Core.Constants`, если потребуется доступ из других сборок).

### A4. `UpdateService` — нереализованная функциональность с хардкоженным внешним URL на чужой репозиторий

- **Файл**: <ref_file file="D:\My_project\N.StCut\src\NStCut.App\Services\UpdateService.cs" />, строка 9
- **Элемент**: `private const string ReleaseApiUrl = "https://api.github.com/repos/eanord20/windows-tool/releases/latest";`
- **Причина**: сервис создаётся в `MainViewModel` (`_updateService = new()`), но метод `IsUpdateAvailableAsync` **нигде не вызывается** — это мёртвый код. При этом URL указывает на репозиторий `eanord20/windows-tool`, который явно не является репозиторием N.StCut — похоже на скопированный шаблон/заглушку, оставшуюся от другого проекта.
- **Влияние**: помимо мёртвого кода — если этот сервис когда-нибудь включат "как есть", приложение будет проверять обновления по чужому GitHub-репозиторию, что является функциональной и потенциально репутационной ошибкой (запрос по сети на сторонний ресурс от имени N.StCut).
- **Рекомендация**: либо удалить неиспользуемый сервис до момента реальной реализации автообновления, либо вынести `ReleaseApiUrl` в конфигурацию и заменить на актуальный репозиторий проекта перед включением функциональности.

### A5. Отсутствие единого абстрактного слоя конфигурации путей/настроек (`AppSettingsService`, `MaterialsLibraryService`, `ProjectFileService` независимо реализуют похожую JSON-сериализацию)

- **Файлы**: <ref_file file="D:\My_project\N.StCut\src\NStCut.App\Services\AppSettingsService.cs" />, <ref_file file="D:\My_project\N.StCut\src\NStCut.App\Services\MaterialsLibraryService.cs" />, <ref_file file="D:\My_project\N.StCut\src\NStCut.App\Services\ProjectFileService.cs" />
- **Причина**: во всех трёх файлах повторяется один и тот же паттерн: собственный `static readonly JsonSerializerOptions` с одинаковыми настройками (`WriteIndented = true`, `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`), собственные `File.ReadAllText`/`File.WriteAllText`/`Directory.CreateDirectory`.
- **Влияние**: три независимые точки, которые нужно синхронно обновлять при изменении политики сериализации (например, добавление конвертеров enum-ов или включение `WriteIndented=false` для продакшена) — риск рассинхронизации поведения.
- **Рекомендация**: выделить общий `IJsonFileStore`/`JsonFileService` с одним источником `JsonSerializerOptions`, переиспользуемый всеми тремя сервисами.

---

## SOLID Assessment

| Принцип | Оценка | Комментарий |
|---|---|---|
| **S — Single Responsibility** | 5/10 | На уровне сборок — отлично (Core/Dxf/Nesting/Reports разделены). На уровне `MainViewModel` (<ref_file file="D:\My_project\N.StCut\src\NStCut.App\ViewModels\MainViewModel.cs" />, ~740 строк) — нарушение: один класс отвечает за состояние UI, файловые диалоги, Excel-импорт, буфер обмена, undo-стек, запуск раскроя, экспорт DXF/отчёта, локализацию настроек и хранит два вложенных вспомогательных класса (`MaterialIdToNameConverter`, `RowSectionComparer`, строки 711–741). |
| **O — Open/Closed** | 7/10 | Добавление нового алгоритма раскроя не требует правки существующего кода — достаточно новой реализации `INestingEngine` и замены в `App.xaml.cs` (композиция руками, не через конфигурацию, но расширяемо). То же для `IDxfGeometryService`/`IDxfExportService`/`IReportService`. Минус — переключение реализации происходит через правку кода в `App.xaml.cs`, а не через конфигурацию/DI-контейнер, что не критично для текущего размера проекта, но ограничивает расширяемость "без пересборки". |
| **L — Liskov Substitution** | 8/10 | Явных нарушений не найдено — иерархий наследования почти нет (проект построен в основном на интерфейсах и `sealed`-классах), поэтому риск нарушения LSP низкий по конструкции. |
| **I — Interface Segregation** | 8/10 | Интерфейсы в `Core.Contracts` узкие и однонаправленные (`INestingEngine.Nest`, `IDxfGeometryService.ReadContour`, `IDxfExportService.WriteSheet`, `IReportService.GenerateReport`) — каждый описывает ровно одну операцию, без "толстых" интерфейсов с лишними методами. |
| **D — Dependency Inversion** | 6/10 | На границе `App ↔ Core.Contracts` — соблюдается (см. Сильные стороны, п.2). Но `App`-сервисы (`FileResolveService`, `ProjectFileService`, `MaterialsLibraryService`, `UpdateService`, `AppSettingsService`) **не имеют интерфейсов** и создаются как конкретные типы напрямую в `MainViewModel` (`= new()`), что затрудняет unit-тестирование `MainViewModel` в изоляции (тесты вынуждены работать с реальной файловой системой/буфером обмена, если не тестировать приватные методы напрямую). |

---

## DRY Assessment

| # | Место дублирования | Файлы | Комментарий |
|---|---|---|---|
| D1 | Drag&drop-логика строки DataGrid | <ref_file file="D:\My_project\N.StCut\src\NStCut.App\MainWindow.xaml.cs" /> (37–68), <ref_file file="D:\My_project\N.StCut\src\NStCut.App\Views\MaterialsWindow.xaml.cs" /> (30–54) | Идентичная реализация "запомнить кандидата → проверить порог перемещения → `DoDragDrop`" для `PartRow` и `MaterialRow`. (Подробно — см. `docs/Code_Audit_UI.md`, issue C4.) |
| D2 | Магическое число геометрической точности `0.001` | `OrientationHelper.cs:18`, `ShelfPacker.cs:58` | См. Critical Issue A2 — расхождение с `NestingOptions.Resolution`. |
| D3 | Путь `"NStCut"` в `%AppData%` | `AppSettingsService.cs:14`, `MaterialsLibraryService.cs:14` | См. Critical Issue A3. |
| D4 | `JsonSerializerOptions` с одинаковыми настройками | `AppSettingsService.cs`, `MaterialsLibraryService.cs`, `ProjectFileService.cs` | См. Critical Issue A5. |
| D5 | Две системы цветов статуса | `StatusToBrushConverter.cs` (хардкод) vs `AppTheme.xaml` (неиспользуемые ресурсы) | См. Critical Issue A1. |
| D6 | Повторяющаяся XAML-разметка "лейбл — поле — кнопка Огляд" | `MainWindow.xaml`, строки 136-147 и 148-159 (пути поиска/сохранения) | Два блока `Grid` с идентичной структурой колонок `Auto/8/*/8/Auto`, отличаются только биндингами — кандидат на `DataTemplate`/`UserControl`. |
| D7 | `MaterialIdToNameConverter`/`RowSectionComparer` объявлены внутри `MainViewModel.cs`, а не в `Converters/` | `MainViewModel.cs:710-741` | Нарушает уже принятую в проекте структуру папок (`Converters/*.cs` для конвертеров), затрудняет поиск. |

---

## Architecture Scorecard

| Область | Оценка |
|---|---|
| Структура проекта / разделение сборок | 9/10 |
| Зависимости между слоями (Core → Infrastructure → UI) | 9/10 |
| MVVM (biding, ICommand, ObservableCollection) | 6/10 |
| SOLID (общая) | 7/10 |
| DRY | 6/10 |
| Конфигурация / отсутствие хардкода | 5/10 |
| Обработка ошибок | 6/10 (в основном `try/catch` вокруг `Nest()`, но `UpdateService` глотает все исключения молча) |
| Тестовое покрытие (наличие тестовых проектов на каждый слой) | 7/10 |
| Готовность к масштабированию (см. ниже) | 7/10 |

---

## Оценка готовности к масштабированию

- **Новые форматы/алгоритмы DXF-импорта**: готовность высокая — `IDxfGeometryService`/`IDxfExportService` уже абстрагируют работу с DXF от `Core`/`App`; для нового формата достаточно новой реализации в отдельной сборке.
- **Новые алгоритмы раскроя**: готовность высокая — `INestingEngine` уже единственная точка входа, `BoundingBoxShelfEngine` можно заменить/дополнить стратегиями без изменения `MainViewModel` (кроме точки создания в `App.xaml.cs`, где стоит рассмотреть простую фабрику/конфиг выбора алгоритма).
- **Сохранение проектов**: готовность средняя — `ProjectFileService` уже поддерживает миграцию старого формата, что хороший знак, но сериализация не абстрагирована (см. A5) — расширение формата в будущем потребует правок в нескольких местах одновременно.
- **Экспорт**: готовность высокая для DXF/отчётов (уже за интерфейсами); добавление нового формата экспорта (например, PDF) потребует нового `Contracts`-интерфейса по аналогии с `IReportService`.
- **Лицензирование**: готовность низкая — в проекте нет вообще никакой инфраструктуры для лицензирования (нет модуля проверки лицензии/триала, нет упоминаний в `Core`/`App`). `UpdateService` (A4) — единственный намёк на "внешнюю" инфраструктуру, но он не завершён и содержит чужой URL. Для лицензирования потребуется закладывать отдельный слой (например, `NStCut.Licensing`) с нуля.

---

## Рекомендации

### Critical
1. Устранить рассинхронизацию цветов статуса (A1) — увязать `StatusToBrushConverter` с ресурсами `AppTheme.xaml` (или удалить неиспользуемые ресурсы), особенно до реализации тёмной темы, согласованной в `docs/UI_Design_System.md`.
2. Устранить расхождение геометрической точности (A2) — передавать `NestingOptions.Resolution` в `ShelfPacker`/`OrientationHelper` вместо хардкода `0.001`, это напрямую влияет на корректность/детерминированность раскроя.
3. Разобраться с `UpdateService` (A4) — удалить как мёртвый код или заменить URL на реальный репозиторий проекта перед включением.
4. Устранить баг `GroupStyle`/`Expander`/`Refresh()`, описанный в `docs/Code_Audit_UI.md` (issue C1–C3) — напрямую влияет на юзабилити таблицы деталей.

### Important
1. Вынести общий путь `%AppData%\NStCut` в одну константу (A3).
2. Выделить общий JSON file-store сервис для `AppSettingsService`/`MaterialsLibraryService`/`ProjectFileService` (A5).
3. Устранить дублирование drag&drop-кода между `MainWindow.xaml.cs` и `MaterialsWindow.xaml.cs` (D1).
4. Разгрузить `MainViewModel`, выделив Import/Export/Undo-логику в отдельные сервисы за интерфейсами (для соблюдения SRP и DIP, упростит unit-тестирование).
5. Вынести `MaterialIdToNameConverter`/`RowSectionComparer` из `MainViewModel.cs` в `Converters/` (D7).

### Future
1. Заложить простую фабрику/конфигурацию выбора реализации `INestingEngine` (для будущих альтернативных алгоритмов раскроя) вместо прямого `new BoundingBoxShelfEngine()` в `App.xaml.cs`.
2. Спроектировать отдельный слой лицензирования (`NStCut.Licensing`) с интерфейсом-контрактом в `Core.Contracts`, по аналогии с уже существующими `IDxfGeometryService`/`INestingEngine`, когда потребуется эта функциональность.
3. Рассмотреть переход на DI-контейнер (например, `Microsoft.Extensions.DependencyInjection`), если количество сервисов/интерфейсов продолжит расти — текущий ручной composition root в `App.xaml.cs` пока приемлем, но масштабируется плохо.
4. Абстрагировать сервисы уровня `App` (`FileResolveService`, `ProjectFileService`, `MaterialsLibraryService`) за интерфейсами для повышения тестируемости `MainViewModel` (сейчас SOLID D-оценка снижена именно из-за этого, см. SOLID Assessment).

---

Документ создан для анализа — изменения в коде не вносились. Остановлено до подтверждения.
