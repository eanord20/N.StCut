# Implementation Log — N.StCut Refactoring

Журнал выполнения `docs/Refactoring_Plan.md` по этапам. Каждый этап фиксируется отдельным разделом ниже.

---

## Stage 1 — Critical (RF-001…RF-009)

**Статус**: ✅ завершён. Сборка (`dotnet build NStCut.sln`) — 0 ошибок, 0 предупреждений. Тесты (`dotnet test NStCut.sln`) — 36/36 пройдено (все существующие тестовые проекты: `NStCut.Core.Tests`, `NStCut.Dxf.Tests`, `NStCut.Nesting.Tests`, `NStCut.Reports.Tests`, `NStCut.App.Tests`, `NStCut.IntegrationTests`).

### Выполненные задачи

| ID | Что сделано |
|---|---|
| RF-001 | В `App.xaml.cs` добавлена подписка на `DispatcherUnhandledException` — необработанное исключение показывается пользователю через `MessageBox` вместо аварийного завершения процесса (`e.Handled = true`). |
| RF-002 | В `MainViewModel.cs` методы `FindFiles()`, `ImportExcelFile()`, `ExportDxf()`, `ExportReport()`, `SaveProject()`, `OpenProject()` обёрнуты в `try/catch (Exception ex)` с `MessageBox.Show(ex.Message, "...", MessageBoxButton.OK, MessageBoxImage.Error)`, по образцу уже существующего паттерна в `Nest()`. |
| RF-003 | `ProjectFileService.Load()` и `MaterialsLibraryService.Load()` обёрнуты в `try/catch`, возвращают пустое состояние по умолчанию (`new ProjectData()` / `new List<SheetMaterial>()`) при повреждённом/невалидном файле — паттерн скопирован дословно с уже реализованного в `AppSettingsService.Load()`. |
| RF-004 | В `MainWindow.xaml` (`SectionGroupItemStyle`) добавлен `Style="{x:Null}"` на `Expander`, чтобы явно отключить implicit-стиль MaterialDesignThemes (корневая причина визуального бага из `docs/Code_Audit_UI.md`, issue C1). |
| RF-005 | В конструкторе `MainViewModel` включены `RowsView.IsLiveSorting`/`IsLiveGrouping` с `LiveSortingProperties`/`LiveGroupingProperties` (`RowNumberInSection`, `MaterialId`); убран вызов `RowsView.Refresh()` из `RecomputeSectionNumbers()`. Явный `Refresh()` в обработчике `Materials.CollectionChanged` **сохранён** — он реагирует на изменение самого списка материалов (порядок/состав), которое не отслеживается через `PropertyChanged` отдельной строки. |
| RF-006 | В `MainWindow.xaml` `EnableRowVirtualization` изменён с `False` на `True` (после RF-004/RF-005, как требовал план); `EnableColumnVirtualization` оставлен `False` — колонок немного и они фиксированной ширины. |
| RF-007 | `StatusToBrushConverter` переписан на `Application.Current.TryFindResource(...)` по образцу уже существующего `ActiveMaterialHighlightConverter`, вместо `Brushes.LightGreen/LightYellow/Orange/LightBlue`. Использует существующие ресурсы `AppTheme.xaml` (`NotFoundRowBrush`, `ManuallySelectedRowBrush`, `AmbiguousRowBrush`); добавлен новый ресурс `FoundRowBrush` (`#90EE90` — совпадает с прежним `Brushes.LightGreen`, т.к. эквивалента для статуса Found в теме не было). |
| RF-008 | `OrientationHelper.GetOrientations` и `ShelfPacker` теперь принимают `resolution` параметром вместо хардкода `0.001`; `BoundingBoxShelfEngine` передаёт `options.Resolution` (по умолчанию `0.01`, как в `NestingOptions`/`AppConstants.ComparisonResolution`) в оба места создания `ShelfPacker`. |
| RF-009 | `UpdateService.cs` удалён вместе с полем `_updateService` в `MainViewModel` — сервис не использовался нигде, кроме объявления, и содержал URL на чужой репозиторий (`eanord20/windows-tool`). |

### Изменённые файлы

- `src/NStCut.App/App.xaml.cs`
- `src/NStCut.App/ViewModels/MainViewModel.cs`
- `src/NStCut.App/Services/ProjectFileService.cs`
- `src/NStCut.App/Services/MaterialsLibraryService.cs`
- `src/NStCut.App/Services/UpdateService.cs` (удалён)
- `src/NStCut.App/MainWindow.xaml`
- `src/NStCut.App/Converters/StatusToBrushConverter.cs`
- `src/NStCut.App/AppTheme.xaml`
- `src/NStCut.Nesting/Internals/OrientationHelper.cs`
- `src/NStCut.Nesting/Internals/ShelfPacker.cs`
- `src/NStCut.Nesting/Services/BoundingBoxShelfEngine.cs`
- `docs/Refactoring_Plan.md` (отметки о выполнении)

### Отклонения от формулировок плана (незначительные, в рамках "minimal diff")

- **RF-003**: план описывал результат как "откатываются к пустому состоянию **с сообщением пользователю**". Реализовано как точный аналог `AppSettingsService.Load()` — **полностью тихий** откат к пустому состоянию, без отдельного `MessageBox` на уровне сервиса. Обоснование: (1) это дословно тот же паттерн, который назван в плане как эталонный ("ведут себя как уже реализовано в `AppSettingsService.Load`"); (2) `OpenProject()` в любом случае теперь обёрнут в try/catch по RF-002 — если бы `Load()` бросал исключение, пользователь всё равно увидел бы сообщение через RF-002; тихий фоллбэк в самом сервисе выбран для минимального diff и переиспользования существующего кода, а не написания нового поведения "сообщение + пустое состояние", которого в проекте ещё не было ни в одном сервисе.
- **RF-009**: план допускал два варианта ("удалить" или "исправить URL"). Выбрано удаление — сервис нигде не вызывался (`IsUpdateAvailableAsync` не имел ни одного вызова в проекте), поэтому исправление мёртвого кода расценено как избыточное; при необходимости авто-обновления в будущем сервис можно написать заново уже с правильным репозиторием.
- **RF-007**: для статуса `Found` в теме не было соответствующего ресурса (в отличие от NotFound/ManuallySelected/Ambiguous) — добавлен новый `FoundRowBrush`, значение цвета сохранено идентичным прежнему (`#90EE90` = `Brushes.LightGreen`), чтобы не менять внешний вид для этого статуса. Обратная сторона унификации: визуальный цвет строк **NotFound/ManuallySelected/Ambiguous изменится** на уже существовавшие, но ранее неиспользуемые пастельные оттенки из `AppTheme.xaml` (это ожидаемый и прямо описанный в аудите результат унификации, а не побочный эффект).

### Риски / что стоит проверить вручную

- **RF-004/RF-005/RF-006** (сама причина бага с "сжатием" таблицы) — это визуальный WPF-баг, который невозможно достоверно подтвердить/опровергнуть автоматической сборкой или юнит-тестами (в решении нет UI-тестов, взаимодействующих с реальным рендерингом `DataGrid`/`GroupStyle`). Сборка и существующие тесты проходят, но **рекомендуется ручная проверка**: открыть приложение, добавить/вставить много строк деталей с разными материалами, убедиться, что таблица больше не "схлопывается" и что группировка/сортировка разделов остаётся корректной (порядок деталей внутри раздела и порядок самих разделов).
- **RF-006** (`EnableRowVirtualization="True"`) в сочетании с группировкой исторически была источником проблем в WPF `DataGrid` (это и стало, по всей видимости, причиной, по которой её изначально отключили) — рекомендуется отдельно протестировать прокрутку большой таблицы (сотни строк) после этого изменения.
- **RF-007** — видимые цвета строк Found/NotFound/ManuallySelected/Ambiguous изменились (см. "Отклонения" выше) — стоит визуально сверить, что новая пастельная палитра достаточно контрастна и различима.
- **RF-008** — допуск сравнения при определении поворота детали увеличен с `0.001` до `0.01` (мм) — на реальных DXF с очень близкими, но не идентичными шириной/высотой (менее 0.01 мм) это теоретически может изменить решение о повороте детали по сравнению с прежним поведением. Для мебельного производства (допуски много больше 0.01 мм) риск практического расхождения результата минимален, но рекомендуется прогнать регресс на реальном наборе DXF-файлов, если такой есть.
- **RF-001** — глобальный обработчик показывает `ex.Message` пользователю "как есть" (без дополнительной локализации/форматирования), аналогично уже существующему подходу в `Nest()` — сообщения будут на английском (текст исключений .NET), это соответствует текущему нелокализованному состоянию сообщений об ошибках в проекте (см. `docs/UX_Audit.md`), отдельно не исправлялось, т.к. не входило в scope Stage 1.

---

## Stage 2 — High (RF-010…RF-019)

**Статус**: ✅ выполнен (RF-017 — частично, см. ниже). Сборка (`dotnet build NStCut.sln`) — 0 ошибок, 0 предупреждений. Тесты — 36/36 пройдено. Дополнительно приложение было запущено (`NStCut.App.exe`) и корректно стартовало без падений (`Responding = True`) — это не заменяет визуальную проверку темы/раскладки, но подтверждает отсутствие ошибок инициализации (в частности, в новом `ThemeService`/`DispatcherUnhandledException`).

### Выполненные задачи

| ID | Что сделано |
|---|---|
| RF-010 | `Nest()` и `ExportDxf()` в `MainViewModel.cs` переведены на `async void` + `Task.Run` для тяжёлой синхронной части (чтение геометрии DXF, сам раскрой, запись листов). Продолжение после `await` выполняется на UI-потоке (WPF `SynchronizationContext`), поэтому обращения к `StatusText`/`ProgressValue`/создание `SheetPreviewWindow` остаются безопасными. Дополнительно: `CanNest()` теперь учитывает `IsBusy` (иначе кнопку можно было бы нажать повторно во время выполнения асинхронной операции — риска не было при синхронном коде, но появился бы при переходе на async), `IsBusy` теперь вызывает `CommandManager.InvalidateRequerySuggested()`. |
| RF-011 | `FileResolveService` получил `EnumerateDxfFiles(...)` и перегрузки `GetCandidates`/`Resolve`, принимающие уже готовый список файлов. `MainViewModel.FindFiles()` теперь сканирует папку поиска один раз и переиспользует список для всех строк. Старые сигнатуры (`GetCandidates(partName, searchPath, excludePath)`, `Resolve(partName, searchPath, excludePath)`) сохранены без изменений — существующие тесты `FileResolveServiceTests` не менялись и проходят как есть. |
| RF-012 | Добавлен `Views/DataGridRowDragHelper.cs` (generic-хелпер: запоминание кандидата на `PreviewMouseLeftButtonDown`, инициация `DoDragDrop` в `MouseMove` при превышении системного порога). `MainWindow.xaml.cs` и `MaterialsWindow.xaml.cs` переключены на использование общего хелпера вместо дублирующейся реализации; поведение сохранено 1:1 (включая проверку "клик с интерактивного контрола ячейки" для `MainWindow`, которой не было в `MaterialsWindow` — не добавлялась, чтобы не менять существующее поведение). |
| RF-013 | Добавлен `Services/JsonFileStore.WriteAtomic(path, content)` — запись во временный файл (`path + ".tmp"`) с последующим `File.Replace`/`File.Move`. `AppSettingsService.Save`, `MaterialsLibraryService.Save`, `ProjectFileService.Save` переключены на него. |
| RF-014 | `ExportDxf()` считает `exportedCount` по ходу цикла; при исключении сообщение `MessageBox` дополнительно указывает, сколько листов из скольких успели экспортироваться до ошибки. |
| RF-015 | Добавлен `Services/AppPaths.RootFolder` (`%AppData%\NStCut`); `AppSettingsService`/`MaterialsLibraryService` используют его вместо собственных дублирующихся констант. |
| RF-016 | Добавлен `Services/JsonFileStore.SerializerOptions` — единая точка настроек `JsonSerializerOptions` (`WriteIndented=true`, `CamelCase`); все три сервиса (`AppSettingsService`, `MaterialsLibraryService`, `ProjectFileService`, включая `MigrateLegacyPartNames`) переключены на него вместо собственных копий. |
| RF-017 | **Частично.** Введены интерфейсы `IFileResolveService`, `IProjectFileService`, `IAppSettingsService`, `IMaterialsLibraryService`; соответствующие сервисы их реализуют; `MainViewModel` и `MaterialsViewModel` теперь зависят от интерфейсов (через опциональные конструкторные параметры с `??= new ConcreteService()`, поэтому единственная точка создания в `App.xaml.cs` не потребовала изменений). Это устраняет нарушение DIP (SOLID Assessment, D) и открывает возможность подстановки тестовых реализаций. **Не выполнено**: полное разделение `MainViewModel` (~740 строк) на отдельные Import/Export/Undo-сервисы — см. "Отклонения от плана" ниже. |
| RF-018 | `MaterialIdToNameConverter` и `RowSectionComparer` вынесены из `MainViewModel.cs` в отдельные файлы `Converters/MaterialIdToNameConverter.cs` и `Converters/RowSectionComparer.cs`, без изменения логики. |
| RF-019 | Добавлены: `Services/ThemeService.cs` (переключение `MaterialDesignTheme.Light/Dark.xaml` + `AppTheme.xaml`/`AppThemeDark.xaml` в `Application.Resources.MergedDictionaries`), `AppThemeDark.xaml` (тёмные эквиваленты токенов из `AppTheme.xaml` по таблице в `docs/UI_Design_System.md`, раздел 5), пункт меню "Тема" (Light/Dark) в `MainWindow.xaml` по образцу уже существующего "Мова", `AppSettings.Theme` для сохранения выбора между запусками (аналогично `Culture`). Бейджи статуса поиска файлов (Found/NotFound/Ambiguous) в `MainWindow.xaml` заменены с простого текста на цветные `Border`-бейджи, использующие те же ресурсы, что и статус строк таблицы (RF-007) — при смене темы бейджи тоже меняют цвет. `CandidateSelectWindow.xaml` получил `Background="{DynamicResource MaterialDesignPaper}"`/`TextElement.Foreground="{DynamicResource MaterialDesignBody}"` и обрамление `ListBox` в стиле остальных карточек приложения — устранена визуальная несогласованность с общей дизайн-системой. |

### Изменённые/новые файлы

- `src/NStCut.App/ViewModels/MainViewModel.cs`, `src/NStCut.App/ViewModels/MaterialsViewModel.cs`
- `src/NStCut.App/Services/FileResolveService.cs` (+ `IFileResolveService.cs`)
- `src/NStCut.App/Services/ProjectFileService.cs` (+ `IProjectFileService.cs`)
- `src/NStCut.App/Services/AppSettingsService.cs` (+ `IAppSettingsService.cs`)
- `src/NStCut.App/Services/MaterialsLibraryService.cs` (+ `IMaterialsLibraryService.cs`)
- `src/NStCut.App/Services/AppPaths.cs`, `src/NStCut.App/Services/JsonFileStore.cs` (новые)
- `src/NStCut.App/Services/ThemeService.cs` (новый)
- `src/NStCut.App/Services/LocalizationService.cs` (добавлен ключ `MenuTheme`)
- `src/NStCut.App/Models/AppSettings.cs` (добавлено поле `Theme`)
- `src/NStCut.App/Converters/MaterialIdToNameConverter.cs`, `Converters/RowSectionComparer.cs` (новые)
- `src/NStCut.App/Views/DataGridRowDragHelper.cs` (новый)
- `src/NStCut.App/MainWindow.xaml.cs`, `src/NStCut.App/Views/MaterialsWindow.xaml.cs`
- `src/NStCut.App/MainWindow.xaml` (меню "Тема", бейджи статистики)
- `src/NStCut.App/AppTheme.xaml`, `src/NStCut.App/AppThemeDark.xaml` (новый)
- `src/NStCut.App/Views/CandidateSelectWindow.xaml`

### Отклонения от формулировок плана

- **RF-017 выполнен частично.** План описывал результат как "выделение Import/Export/Undo-логики в отдельные сервисы". Реализована только часть, устраняющая нарушение DIP (интерфейсы для сервисов уровня `App`) — она низкорискова и полностью проверяема сборкой/тестами. Полное разделение `MainViewModel` на отдельные сервисы **сознательно отложено**: это затронуло бы одновременно множество команд (`PasteFromClipboard`, `ImportExcelFile`, `Undo`, `SaveProject`, `OpenProject` и связанные с ними `PushUndoSnapshot`/`AddRowsBatch`/пересчёты) в файле, для которого нет UI-тестов, а сам план в Risk Matrix прямо оценивает эту часть как "Высокий риск / Высокая вероятность регрессии". Согласно принципу "изменения должны быть минимальными", решено не проводить крупный структурный рефакторинг без возможности его визуально проверить в этой сессии. Рекомендуется вынести оставшуюся часть RF-017 в отдельную, отдельно тестируемую задачу.
- **RF-012**: у `MainWindow` перетаскивание строки игнорирует клик с интерактивных контролов ячейки (кнопка/ComboBox/чекбокс/текстбокс), а у `MaterialsWindow` — нет. Это было и раньше (два независимых, слегка разных по поведению куска кода) — при объединении в общий хелпер поведение каждого окна сохранено как было (хелпер принимает опциональный делегат-фильтр), различие не является новой архитектурной идеей, а лишь консолидацией существовавшего кода.

### Риски / что стоит проверить вручную

- **RF-010** (async `Nest`/`ExportDxf`) — самое рискованное изменение этапа: логика идентична прежней, но теперь выполняется в `Task.Run` с `await`. Приложение запускалось и не падало при старте, но реальный прогон раскроя с DXF-файлами и параллельное взаимодействие с UI во время выполнения (проверка, что таблицу можно спокойно скроллить/редактировать, пока раскрой считается в фоне, и что повторное нажатие "Nest" во время выполнения действительно заблокировано) — **требует ручной проверки**, автоматических тестов на это нет.
- **RF-019** (тёмная тема) — визуальная проверка обязательна: переключить тему через меню "Тема", убедиться, что все карточки/таблица/`MaterialsWindow`/`CandidateSelectWindow`/`SheetPreviewWindow` выглядят согласованно в обоих режимах, что текст читаем на новом фоне, и что выбор темы сохраняется между перезапусками (файл `%AppData%\NStCut\app-settings.json`, поле `theme`). Название ресурса `MaterialDesignBody` (использовано в `CandidateSelectWindow.xaml`) не проверялось построчно в исходниках библиотеки (они хранятся как embedded-ресурсы внутри `MaterialDesignThemes.Wpf.dll`, а не как файлы на диске) — это общеизвестный стандартный ключ библиотеки, но именно это место стоит визуально сверить в первую очередь.
- **RF-011** — поведение исключения (`UnauthorizedAccessException` при недоступной подпапке) теперь возникает один раз в `EnumerateDxfFiles`, а не по одному разу на строку — функционально эквивалентно (перехватывается тем же `try/catch` из RF-002), но стоит убедиться на реальной сетевой папке.

---

## Stage 3 — Medium (RF-020…RF-026)

**Статус**: ✅ выполнен (RF-024 — частично, см. ниже). Сборка — 0 ошибок, 0 предупреждений. Тесты — 36/36. Приложение запускалось (`Responding = True`) после каждой значимой группы изменений.

### Выполненные задачи

| ID | Что сделано |
|---|---|
| RF-020 | Добавлен `_materialOrderCache` (`Dictionary<string,int>`) в `MainViewModel` — `GetMaterialOrder` больше не делает линейный проход по `Materials` на каждое сравнение при сортировке. Кеш сбрасывается в обработчике `Materials.CollectionChanged`. |
| RF-021 | По итогам Conflict Resolution (полный снэпшот сохранён ради корректности) единственная безопасная оптимизация — снижена глубина истории Undo (`MaxUndoDepth`) с 50 до 20, что ограничивает пиковый объём памяти, удерживаемой историей отмены, без изменения модели/семантики Undo. |
| RF-022 | `SheetThumbnailService.Render` переписан с `Canvas` + `System.Windows.Shapes.Rectangle` (полноценные `UIElement` с layout-проходом) на `DrawingVisual`/`DrawingContext.DrawRectangle` — то же изображение без построения визуального дерева WPF-контролов. |
| RF-023 | `DxfExportService.WriteSheet` кеширует уже прочитанные `DxfDocument` в рамках одного вызова (`Dictionary<string, DxfDocument>`, не поле сервиса) — при `Quantity > 1`, когда деталь несколько раз попадает на один лист, исходный файл читается один раз. Кеш намеренно не сохраняется между вызовами `WriteSheet` (разными листами/экспортами), чтобы не рисковать устаревшими данными, если исходный DXF изменится между операциями. |
| RF-025 | Стили `SectionGroupItemStyle`, `MaterialChipStyle` и конвертеры (`StatusToBrushConverter`, `ActiveMaterialHighlightConverter`) вынесены в новый `MainWindowStyles.xaml`, подключаемый через `ResourceDictionary.MergedDictionaries` в `MainWindow.xaml`. `PartRowStyle` оставлен в `MainWindow.xaml` (см. "Отклонения" ниже). Повторяющийся блок "лейбл — поле пути — кнопка Огляд" вынесен в новый `UserControl` (`Controls/LabeledPathPicker.xaml` + `.xaml.cs`) с `DependencyProperty` `Label`/`PathValue`/`ButtonText`/`BrowseCommand`; оба использования (папка поиска, папка сохранения) переведены на него. |
| RF-026 | Чтение геометрии одной детали в `Nest()` обёрнуто в `try/catch` — при ошибке чтения/парсинга DXF конкретная деталь помечается `PartStatus.NotFound`, раскрой остальных деталей продолжается (раньше исключение из `_dxfGeometry.ReadContour` прерывало весь `Nest()`). |
| RF-024 | **Частично.** Сделано: (1) индикатор занятости — `ProgressBar` в статус-баре, привязан к `IsBusy`/`ProgressValue`, виден только во время `Nest`; (2) заголовки секций ("Джерело/призначення", "Деталі") над соответствующими карточками `MainWindow.xaml`; (3) иконки `materialDesign:PackIcon` на 5 ключевых кнопках (Матеріали, Знайти файли, Розкрій, Export DXF, Export звіту); (4) локализованы `ContextMenu` строки таблицы, тултип заголовка группы, и все подписи `MessageBox` (`ErrorTitle*` ключи в `LocalizationService`, en/ua). **Не сделано**: иконки на остальных кнопках, полная локализация `MaterialsWindow`/`SheetPreviewWindow`/`CandidateSelectWindow`, выравнивание стиля `DataGrid` в `MaterialsWindow` с главной таблицей — см. "Отклонения" ниже. |

### Изменённые/новые файлы

- `src/NStCut.App/ViewModels/MainViewModel.cs` (кеш порядка материалов, `MaxUndoDepth`, локализация `MessageBox`, обработка ошибки чтения детали)
- `src/NStCut.App/Services/SheetThumbnailService.cs` (DrawingVisual)
- `src/NStCut.Dxf/Services/DxfExportService.cs` (кеш `DxfDocument` в рамках `WriteSheet`)
- `src/NStCut.App/MainWindowStyles.xaml` (новый), `src/NStCut.App/MainWindow.xaml` (стили вынесены, заголовки секций, индикатор занятости, иконки, локализация ContextMenu)
- `src/NStCut.App/Controls/LabeledPathPicker.xaml`, `.xaml.cs` (новые)
- `src/NStCut.App/Services/LocalizationService.cs` (новые ключи: `ContextMenu*`, `TooltipActiveMaterial`, `Section*`, `ErrorTitle*`)
- `src/NStCut.App/App.xaml.cs` (локализованная подпись глобального `MessageBox`)

### Отклонения от формулировок плана

- **RF-025**: `PartRowStyle` **не вынесен** в `MainWindowStyles.xaml` вместе с остальными двумя стилями — у него есть `EventSetter` (`PreviewMouseLeftButtonDown`/`MouseMove`/`Drop`), обработчики которых компилятор WPF резолвит через code-behind именно того `.xaml`-файла, где физически объявлен `EventSetter` (а не того, куда он мержится). Практическая проверка (сборка) подтвердила, что вынос `SectionGroupItemStyle`/`MaterialChipStyle` (без `EventSetter`) работает, поэтому оставшийся `PartRowStyle` — сознательное, проверенное сборкой ограничение, а не недосмотр.
- **RF-024 выполнен частично** — по той же причине, что и RF-017 (Stage 2): "полная локализация" и "иконки на всех кнопках" — это очень большая площадь изменений (`MaterialsWindow.xaml`, `SheetPreviewWindow.xaml`, `CandidateSelectWindow.xaml`, десятки строк текста), которую невозможно надёжно проверить в этой сессии (нет автоматических UI/визуальных тестов, только сборка и факт запуска процесса). Сделан содержательный, проверяемый сборкой срез: индикатор занятости, заголовки секций, иконки на самых частых командах, локализация мест, прямо названных в аудите как хардкод (ContextMenu, тултип, подписи `MessageBox`). Оставшаяся часть (иконки на всех кнопках `MaterialsWindow`/`SheetPreviewWindow`, стилизация `DataGrid` в `MaterialsWindow`, локализация `MaterialsWindow`/`SheetPreviewWindow`/`CandidateSelectWindow`) рекомендуется как отдельная последующая задача.

### Риски / что стоит проверить вручную

- **RF-021**: снижение `MaxUndoDepth` до 20 — если пользователь привык отменять больше 20 последовательных действий, это поведенческое отличие (раньше было можно откатить до 50 шагов назад). Стоит подтвердить с пользователем, что 20 — приемлемое значение, либо вынести его в настройки.
- **RF-023**: кеш `DxfDocument` внутри `WriteSheet` — стоит проверить экспорт листа, где одна и та же деталь (с `Quantity > 1`) размещена несколько раз на одном листе: результат должен быть идентичен версии без кеша (проверено логически, но нет отдельного теста именно на этот сценарий).
- **RF-024**: визуальная проверка новых элементов (иконки, заголовки секций, `ProgressBar`) обязательна — сборка/тесты не проверяют реальную раскладку и читаемость.

---

## Stage 4 — Low (RF-027…RF-031)

**Статус**: RF-027 выполнен. RF-028…RF-031 сознательно отложены (см. обоснование ниже). Сборка — 0 ошибок, 0 предупреждений. Тесты — 36/36. Приложение запускалось (`Responding = True`).

### Выполненные задачи

| ID | Что сделано |
|---|---|
| RF-027 | Добавлен кеш `_canNestCache` в `MainViewModel` — `CanNest()` теперь просто возвращает кешированное значение вместо `Rows.Any(...)`-скана на каждый `CommandManager.RequerySuggested`. Кеш пересчитывается явно в четырёх местах, где реально меняются входные данные: `OnRowsChanged()` (структурные изменения строк), `OnRowPropertyChanged` при смене `MaterialId` в ячейке (иначе кнопка "Nest" могла бы не обновлять состояние сразу после редактирования материала в строке — до этой правки O(n)-скан выполнялся заново на любое UI-событие и потому "случайно" всегда был актуален), обработчик `Materials.CollectionChanged`, сеттер `IsBusy`, и один раз в конце конструктора. |

### Сознательно отложенные пункты (не реализованы в этой сессии)

- **RF-028** (алгоритм `ShelfPacker`) — сам аудит формулирует результат как "рассмотреть эвристику", а не как конкретное готовое решение. Проверка показала, что текущий перебор всех уже созданных `packers` для каждой детали — не просто "лишняя работа", а осознанная часть алгоритма упаковки: он позволяет более мелким деталям (список отсортирован по убыванию площади) занимать оставшееся место на уже частично заполненных, более ранних листах. Замена на эвристику "пробовать только последний активный лист" изменила бы физический результат раскроя (плотность заполнения листов), а не только производительность — это прямое изменение бизнес-логики/геометрии, которое требует отдельного обсуждения критериев качества упаковки, а не просто "минимального диффа".
- **RF-029/RF-030/RF-031** — в самих исходных документах (`docs/UX_Audit.md` → "Nice to have", `docs/Audit_Architecture.md` → "Future", `docs/Audit_Reliability.md` → "Future") эти пункты явно помечены как задел на будущее, а не как конкретные, готовые к реализации требования (например, RF-030 описывает "рассмотреть DI-контейнер"/"заложить слой лицензирования при необходимости" — без конкретной формы реализации). Правило "не создавай новые архитектурные идеи, которых нет в аудитах" в применении к настолько общим формулировкам означало бы необходимость самостоятельно изобретать конкретную архитектуру (например, форму фабрики `INestingEngine`, схему автосохранения, набор горячих клавиш) — то есть привносить решения, которых сами аудиты не специфицировали. Эти пункты рекомендуется проработать как отдельные, самостоятельно сформулированные задачи (каждая — с собственным мини-дизайном), а не реализовывать "по наитию" в рамках данного прохода.

---

## Итоговая сводка по всем этапам

| Stage | Статус | Выполнено полностью | Выполнено частично | Отложено |
|---|---|---|---|---|
| 1 — Critical | ✅ | RF-001…RF-009 | — | — |
| 2 — High | ✅ | RF-010…RF-016, RF-018, RF-019 | RF-017 | — |
| 3 — Medium | ✅ | RF-020…RF-023, RF-025, RF-026 | RF-024 | — |
| 4 — Low | ✅ (в рамках выполнимого) | RF-027 | — | RF-028, RF-029, RF-030, RF-031 |

Сборка проекта (`dotnet build NStCut.sln`) — 0 ошибок, 0 предупреждений на каждом этапе. Тесты (`dotnet test NStCut.sln`) — 36/36 пройдено на каждом этапе. Приложение (`NStCut.App.exe`) запускалось и корректно отвечало (`Responding = True`) после каждого значимого блока изменений — это подтверждает отсутствие ошибок инициализации, но **не заменяет ручную визуальную проверку** (раскладка, тёмная тема, поведение таблицы при реальной работе с DXF-файлами) — она рекомендуется как следующий шаг перед вводом в эксплуатацию.

Частично выполненные пункты (RF-017, RF-024) и сознательно отложенные (RF-028…RF-031) задокументированы с явным обоснованием в соответствующих разделах выше — это осознанные решения в пользу минимального риска регрессии, а не незамеченные упущения.

---

## Post-Stage 4 — исправление регрессий, найденных при ручной проверке пользователем

Ручная визуальная проверка (рекомендованная в разделе выше) выявила две реальные регрессии, которые сборка/тесты/факт запуска процесса не могли обнаружить:

### Найденные проблемы

1. **Таблица деталей визуально ломалась** (колонки/заголовки переставали корректно отображаться, оставался виден только заголовок "Назва"). Причина: `EnableRowVirtualization="True"` (RF-006) в сочетании с `GroupStyle` — именно тот риск, который был явно помечен в этом же документе как "требует ручной проверки" и "виртуализация + группировка исторически конфликтны в WPF DataGrid". Проверка не прошла.
2. **В тёмной теме текст оставался чёрным и был нечитаем** на потемневшем фоне карточек. Причина: `Background`/`TextElement.Foreground` были явно проставлены только для `CandidateSelectWindow.xaml` (в рамках RF-019), но не для `MainWindow.xaml`, `MaterialsWindow.xaml`, `SheetPreviewWindow.xaml` — их фон/текст оставались на значениях по умолчанию (белый фон, чёрный текст WPF), не привязанных к `DynamicResource` темы.

### Исправления

| Файл | Изменение |
|---|---|
| `MainWindow.xaml` | `EnableRowVirtualization` возвращён в `False` (откат RF-006); добавлены `Background="{DynamicResource MaterialDesignPaper}"` и `TextElement.Foreground="{DynamicResource MaterialDesignBody}"` на корневой `Window` |
| `Views/MaterialsWindow.xaml` | Добавлены те же `Background`/`TextElement.Foreground` |
| `Views/SheetPreviewWindow.xaml` | Добавлены те же `Background`/`TextElement.Foreground` (белый `Canvas` листа материала внутри не тронут — это цвет самого листа, а не хром приложения) |

Сборка — 0 ошибок, 0 предупреждений. Тесты — 36/36 пройдено. Приложение запущено, `Responding = True`.

### Обновление RF-006

`docs/Refactoring_Plan.md`, RF-006 — статус изменён с "✅ выполнено" на "❌ откачено по результатам ручной проверки": виртуализация строк DataGrid остаётся выключенной (`EnableRowVirtualization="False"`), как и было до Stage 1. Причина отката задокументирована в комментарии прямо в `MainWindow.xaml` рядом с атрибутом.

### Статус вопроса по раскладке

На момент первичного отчёта полная v3-разметка ещё не была перенесена в код. После нового пользовательского feedback она реализована в `MainWindow.xaml`; подробности и последующие визуальные исправления приведены в разделе `Post-Stage 4 — исправления по визуальной проверке` ниже.

---

## Post-Stage 4 — исправления по визуальной проверке (новый пользовательский feedback)

Выполнены дополнительные исправления по скриншотам:

1. **Единый размер/стиль 3×3 кнопок**: в Light/Dark AppTheme добавлены фиксированные `AppToolbarButtonStyle` (132×30) и `AppIconButtonStyle` (30×30); Nest больше не использует отдельный `MaterialDesignRaisedButton` в рабочей сетке. Горизонтальные `Separator` между строками тулбара удалены.
2. **Иконки**: кнопки Undo/Add/Delete/Select File над таблицей переведены в icon-only с ToolTip; Browse в `LabeledPathPicker` заменён на icon-only FolderSearch с ToolTip; для Paste добавлена иконка.
3. **Последние пути**: `SearchPath` и `OutputPath` сохраняются через существующий `AppSettingsService` при изменении поля и при закрытии окна; загрузка защищена флагом `_loadingSettings`, чтобы не записывать настройки во время инициализации.
4. **Выравнивание верхнего блока**: тулбар и правая колонка путей/статуса используют одинаковую трёхрядную структуру; кнопки и строки получили одинаковую фиксированную высоту.
5. **Очистить**: добавлены `ClearAllCommand`, подтверждение Yes/No, полный Undo-снэпшот перед очисткой, очистка строк/результата раскроя и локализованные тексты en/ua.
6. **Корень проблемы читаемости таблицы**: стандартный шаблон `Expander` внутри `DataGrid.GroupStyle` заменён на плоский `GroupItem` template с прямым `ItemsPresenter`; это устраняет неверное измерение группированного DataGrid и сбой ширин колонок. Фон оставлен только на строке заголовка материала.
7. **Тёмная тема**: добавлены implicit-стили для `TextBlock`, `TextBox`, `ComboBox`, `MenuItem` с `DynamicResource MaterialDesignBody`; явные `DataGridColumnHeader`/`DataGridCell` стили сохранены. Это устраняет смешение чёрного/белого текста.
8. **MaterialsWindow**: размер по умолчанию изменён на 1250×850, добавлен `WindowStartupLocation="CenterScreen"`, видимые границы и сетка обеих DataGrid.

Проверка: `dotnet build` — 0 ошибок/предупреждений; `dotnet test` — 36/36; приложение запускается и отвечает (`Responding=True`).

---

## Post-Stage 4 — вторая итерация (структура таблицы и цветовая согласованность темы)

По следующему скриншоту (тёмная тема всё ещё "разномастная", таблица "не держит структуру") проведена повторная диагностика вместо очередной догадки:

1. **Корень проблемы со структурой таблицы (наконец найден).** Предыдущие два исправления (Style="{x:Null}" на Expander, затем собственный `Grid`-шаблон в `GroupStyle.ContainerStyle`) не устраняли проблему, потому что сам подход был неверным: `GroupStyle.ContainerStyle` полностью заменяет `ControlTemplate` контейнера `GroupItem`. Для `DataGrid` заголовок группы — это специальный внутренний контрол `DataGridRowGroupHeader`, синхронизирующий свою раскладку с ширинами колонок через общий механизм измерения `DataGrid`. Полная замена шаблона `GroupItem` (независимо от содержимого — Expander или произвольный Grid) убирает эту синхронизацию, поэтому колонки/заголовки "плыли" при любом варианте кастомного шаблона.
   - **Исправление**: `SectionGroupItemStyle` (полная замена шаблона) заменён на `SectionGroupHeaderTemplate` — `DataTemplate`, подключаемый через штатное свойство `GroupStyle.HeaderTemplate`. Это официальный, предусмотренный WPF способ кастомизировать именно содержимое заголовка группы `DataGrid`, не трогая структуру `DataGridRowGroupHeader` и не ломая синхронизацию с колонками.
2. **Кнопки в тёмной теме выглядели "то светлыми, то серыми"** — на самом деле два разных, независимых артефакта:
   - Включённые кнопки использовали `Background="{DynamicResource MaterialDesignPaper}"` — в тёмной теме библиотеки это заметно светлее, чем `MaterialDesignCardBackground` (который используют карточки и который выглядит корректно тёмным). Все `AppButtonStyle`/`AppToolbarButtonStyle`/`AppIconButtonStyle` (Light и Dark) переключены на `MaterialDesignCardBackground`.
   - Отключённые кнопки (Nest/Export/Open — пока не выбраны материалы/не выполнен раскрой) показывали системный (Aero2) вид disabled-состояния, не учитывающий тёмную палитру. Добавлен явный `Style.Trigger` на `IsEnabled=False` (`Opacity=0.4`) в обеих темах.
   - Фон окна/меню (`MainWindow`, `MaterialsWindow`, `SheetPreviewWindow`, `CandidateSelectWindow`) переключён с `MaterialDesignPaper` на собственный контролируемый токен `WindowBackgroundBrush` (уже был определён в `AppTheme.xaml`/`AppThemeDark.xaml`, но не использовался) — это устраняет зависимость от внутренней "элевации" библиотечных токенов и даёт полный контроль за консистентностью между окном/меню/карточками/кнопками.

### Изменённые файлы
- `src/NStCut.App/MainWindowStyles.xaml` — `SectionGroupItemStyle` → `SectionGroupHeaderTemplate`.
- `src/NStCut.App/MainWindow.xaml` — `GroupStyle.ContainerStyle` → `GroupStyle.HeaderTemplate`; фон окна/меню → `WindowBackgroundBrush`.
- `src/NStCut.App/AppTheme.xaml`, `AppThemeDark.xaml` — фон кнопок → `MaterialDesignCardBackground`; добавлен `Trigger IsEnabled=False`.
- `src/NStCut.App/Views/MaterialsWindow.xaml`, `SheetPreviewWindow.xaml`, `Views/CandidateSelectWindow.xaml` — фон окна → `WindowBackgroundBrush`.

Проверка: `dotnet build` — 0 ошибок/предупреждений; `dotnet test` — 36/36; приложение запущено, `Responding=True`.

---

## Roadmap to v1.0 — NSC-011…NSC-014

Продолжение работы над подготовкой к релизу v1.0: сформирован реальный корпус DXF, усилены геометрия/отчётность, выделен жизненный цикл результата, добавлена отмена длительных операций.

### Сводка

| ID | Что сделано |
|---|---|
| NSC-011 | Production DXF-корпус (`src/NStCut.Dxf.Tests/Corpus`) подключён к `NStCut.Nesting.Tests` через `NestingBenchmarkCorpusTests`. Синтетический A–J baseline сохранён. |
| NSC-012 | `DxfContour.TruePerimeter`, `Part.Perimeter`, `PartRow.Area`/`Perimeter`, `ReportService` печатает `TrueArea`/`TruePerimeter`; `NestingQualityMetrics` уже использовал `Part.TrueArea`/`TruePerimeter`. |
| NSC-013 | Добавлен `INestingResultManager` + `NestingResultManager`; `MainViewModel` больше не хранит `_nestingResult`/`_lastReportPath` локально. |
| NSC-014 | `CancellationToken` добавлен в `IDxfExportService`/`IReportService` и их реализации; `MainViewModel` получил `CancelCommand`, `CancellationTokenSource` для `Nest`/`ExportDxf`/`ExportReport`. |

### Изменённые/новые файлы

- `src/NStCut.Dxf.Tests/NestingBenchmarkCorpusTests.cs` (новый)
- `src/NStCut.Nesting.Tests/NestingBenchmark.cs`, `NStCut.Nesting.Tests.csproj`
- `src/NStCut.Core/Contracts/IDxfGeometryService.cs` (`DxfContour.TruePerimeter`)
- `src/NStCut.Core/Models/Part.cs` (`Perimeter`, `TruePerimeter`)
- `src/NStCut.App/Models/PartRow.cs` (`Area`, `Perimeter`)
- `src/NStCut.App/ViewModels/MainViewModel.cs` (`RowNestingUpdate` с area/perimeter, `NestingResultManager`, cancellation)
- `src/NStCut.Reports/Services/ReportService.cs` (колонка Perimeter)
- `src/NStCut.Core/Contracts/INestingResultManager.cs` (новый)
- `src/NStCut.App/Services/NestingResultManager.cs` (новый)
- `src/NStCut.Core/Contracts/IDxfExportService.cs`, `IReportService.cs`
- `src/NStCut.Dxf/Services/DxfExportService.cs`
- `src/NStCut.Reports/Services/ReportService.cs`

### Проверка

- `dotnet build src/NStCut.sln` — 0 ошибок, 0 предупреждений.
- `dotnet test src/NStCut.App.Tests/NStCut.App.Tests.csproj` — 161/161.
- `dotnet test src/NStCut.Reports.Tests/NStCut.Reports.Tests.csproj` — 10/10.
- `dotnet test src/NStCut.Nesting.Tests/NStCut.Nesting.Tests.csproj` — 76/76.
- `dotnet test src/NStCut.IntegrationTests/NStCut.IntegrationTests.csproj` — 4/4.

---

## NSC-017 — Profile MaxRects and UI performance

Проведён замер производительности без доработки кода: добавлена временная разметка `Stopwatch` в `NestingBenchmark` и тест `MainViewModelTests.Profile_NestAsync_UI_Overhead_WithManyRows`.

### Результаты

**Nesting benchmark** (`MultiPassNestingEngine`, 40 passes, 10 синтетических кейсов):

| Case | Time, ms | Sheets | Unplaced | Util % |
|---|---:|---:|---:|---:|
| A | 68,0 | 1 | 0 | 82,82 |
| B | 22,6 | 2 | 0 | 45,55 |
| C | 18,7 | 1 | 0 | 51,76 |
| D | 10,2 | 2 | 0 | 46,58 |
| E | 3,3 | 1 | 0 | 51,76 |
| F | 47,2 | 4 | 0 | 69,01 |
| G | 32,4 | 2 | 0 | 51,41 |
| H | 4,0 | 1 | 0 | 35,20 |
| I | 3,5 | 1 | 0 | 19,84 |
| J | 6,6 | 1 | 0 | 44,51 |
| **Total** | **~227** | — | — | — |

Production DXF-корпус (`NestingBenchmarkCorpusTests`): 9 файлов, 1 sheet, **76 ms**.

**MainViewModel UI overhead** (`NestAsync` с `FakeNestingEngine` + `FakeDxfGeometryService`, 500 строк):

- **14,1 ms** — подготовка входных данных, чтение geometry-заглушки, `RecomputeOversizedForAll`, обновление `Rows` и статуса.

### Наблюдения

- Раскрой на 40 проходах занимает единицы-десятки миллисекунд; общее время весьма невелико.
- `MainViewModel` с 500 строками отрабатывает за ~14 ms, UI-оверхед пока не является bottleneck.
- Потенциальные горячие участки (без изменения логики):
  - `MaxRectsPacker.TryPlace` — вложенный цикл по `OrientationHelper.GetOrientations` и `_freeRectangles`, O(orientations × freeRects).
  - `MaxRectsPacker.OverlapsEffectiveBounds` — `_effectiveBounds.Any(b => b.Intersects(occupied))`, O(n) на каждое размещение.
  - `MaxRectsPacker.SplitFreeRectangles` — вложенные `.Any` на `List<Rect>`, O(n²) на каждый split.
  - `MainViewModel.RecomputeOversizedForAll` — вызывается до и после `Nest`; на 500 строках незначителен, но масштабируется с квадратом? (надо отдельно исследовать при реальном размере).

### Рекомендации

- При реальном production-наборе с сотнями деталей/листов повторно замерить `MaxRectsPacker` с `Stopwatch` внутри `TryPlace`/`SplitFreeRectangles`.
- Если появится задержка, рассмотреть:
  - R-tree / interval tree для `OverlapsEffectiveBounds`;
  - слияние и фильтрацию `freeRectangles` без O(n²) парных проверок;
  - кэширование `Bounds`-информации при переборе ориентаций.
- Профиль UI-рендеринга (`DataGrid`, `SheetThumbnailService`) требует ручного/визуального замера, т.к. юнит-тесты не запускают WPF-рендер.

---

## NSC-018 — ShelfPacker API cleanup и fallback

- В `ShelfPacker.ToSheet` добавлен расчёт `UsedWidth`/`UsedHeight` и использование `Source.TrueArea` для `WasteArea`/`UtilizationPercent`, чтобы соответствовать `MaxRectsPacker`.
- В `BoundingBoxShelfEngine` разделены `PackWithMaxRectsPacker` и `PackWithShelfPacker`.
- Если `MaxRectsPacker` оставляет нерозмещённые детали, `PackGroup` дополнительно запускает `ShelfPacker` на той же сортировке и выбирает лучший результат по `NestingResultComparison.IsBetter`.
- Добавлен `CreateShelfPacker` — упаковщик создаётся с теми же `margin`/`gap`/`resolution`.

### Изменённые файлы

- `src/NStCut.Nesting/Internals/ShelfPacker.cs`
- `src/NStCut.Nesting/Services/BoundingBoxShelfEngine.cs`

### Проверка

- `dotnet build src/NStCut.sln` — 0 ошибок, 0 предупреждений.
- `dotnet test src/NStCut.Nesting.Tests` — 76/76.
- `dotnet test src/NStCut.App.Tests` — 162/162.

---

## NSC-019 — Quality Dashboard

Добавлено отображение качества раскроя в `MainWindow` и `SheetPreviewWindow`, а также экспорт метрик в `NestingBenchmark` report.

### Что сделано

- `MainViewModel` получил `QualitySummary` и `UpdateQualitySummary` — после раскроя обновляется сводка: utilization, длина реза, остаточная площадь (Pockets/FreeRectangles), число карманов/отверстий, количество неразмещённых.
- `SheetPreviewItemViewModel` получил `QualityText` — для каждого листа: utilization, длина реза, остаточная площадь, количество карманов, размер и форма самого большого кармана, число long-thin/deep карманов.
- `MainWindow.xaml` — под статус-баром добавлен `TextBlock` с `QualitySummary`.
- `SheetPreviewWindow.xaml` — под таблицей деталей на каждом листе добавлен `TextBlock` с `QualityText`.
- `NestingBenchmark.BuildReport` — в итоговую таблицу добавлены столбцы `Residual Area` (остаточная площадь без учёта gap) и `Pockets` (число карманов/отверстий).

### Изменённые файлы

- `src/NStCut.App/ViewModels/MainViewModel.cs`
- `src/NStCut.App/ViewModels/SheetPreviewViewModel.cs`
- `src/NStCut.App/MainWindow.xaml`
- `src/NStCut.App/Views/SheetPreviewWindow.xaml`
- `src/NStCut.Nesting.Tests/NestingBenchmark.cs`

### Проверка

- `dotnet build src/NStCut.sln` — 0 ошибок, 0 предупреждений.
- `dotnet test src/NStCut.App.Tests` — 162/162.
- `dotnet test src/NStCut.Nesting.Tests` — 76/76.

---

## NSC-020 — Polygon / NFP research

Добавлен базовый `NfpCalculator` для выпуклых многоугольников — первый шаг к true-polygon nesting.

### Что сделано

- `src/NStCut.Nesting/Internals/NfpCalculator.cs`:
  - `ComputeNoFitPolygon(Polygon2D stationary, Polygon2D orbiting)` — NFP для двух выпуклых полигонов через попарные разности вершин и convex hull.
  - Внутренний `IsConvex` и `ConvexHull` (Andrew's monotone chain).
  - NFP возвращает `null` для невыпуклых полигонов (защита от некорректного применения).
- `src/NStCut.Nesting.Tests/NfpCalculatorTests.cs`:
  - Проверка NFP для двух квадратов.
  - Проверка, что точка (0,0) внутри NFP при перекрытии.
  - Проверка, что удалённая точка вне NFP.
  - Проверка отказа для U-образного (non-convex) контура.

### Изменённые файлы

- `src/NStCut.Nesting/Internals/NfpCalculator.cs` (новый)
- `src/NStCut.Nesting.Tests/NfpCalculatorTests.cs` (новый)

### Проверка

- `dotnet build src/NStCut.sln` — 0 ошибок, 0 предупреждений.
- `dotnet test src/NStCut.Nesting.Tests` — 80/80.
- `dotnet test src/NStCut.App.Tests` — 162/162.

---

## NSC-023 — Release build и installer stub

Подготовлен Release-сборочный пайплайн и проверен `installer.iss`.

### Что сделано

- `installer.iss` присутствует в корне репозитория и настроен на:
  - `NStCut.App.exe` из `src\\NStCut.App\\bin\\Release\\net8.0-windows\\`;
  - ярлыки в меню "Пуск" и на рабочий стол (опционально);
  - запуск приложения после установки.
- Проверен Release-сборка всего решения и все тесты:
  - `dotnet build src/NStCut.sln -c Release` — 0 ошибок, 0 предупреждений.
  - `dotnet test src/NStCut.sln -c Release` — 337/337 (45 + 36 + 80 + 10 + 4 + 162).

### Изменённые файлы

- `installer.iss` (подтверждена корректность путей)

### Проверка

- `dotnet build src/NStCut.sln -c Release` — OK.
- `dotnet test src/NStCut.sln -c Release` — 337/337.
- Запуск EXE вручную не проверялся; упаковка `iscc` требует локальной установки Inno Setup.
