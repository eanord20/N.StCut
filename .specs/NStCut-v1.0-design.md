# N.StCut v1.0 — Design Specification

## 1. Context & Goal

**N.StCut** — настольное Windows-приложение для автоматического раскроя листового материала по DXF-развёрткам.

Цель MVP: пользователь задаёт список деталей, назначает каждой DXF-файл и материал, указывает размеры листов с учётом текстуры, после чего программа автоматически формирует оптимальный раскрой и экспортирует:

- DXF-файл на каждый использованный лист;
- DOCX-отчёт с таблицами и миниатюрами раскроя.

Интерфейс списка деталей повторяет NS_PDF_Merge: вставка из Excel, поиск файлов, статусы, Undo, контекстное меню строки. Перетаскивание строк и копирование в буфер в MVP не требуются.

Принятые решения:

- Стек: **WPF .NET 8 + MaterialDesignThemes + netDxf + Xceed DocX**.
- Архитектура: **многослойная** (`Core`, `Dxf`, `Nesting`, `Reports`, `App`).
- Алгоритм MVP: **bounding-box shelf packing** с поворотами 0/90°.
- В v1.1 алгоритм заменяется на полигональный (NFP) с вложением в отверстия без изменения UI.
- Единицы: **миллиметры**.
- Зазор между деталями и отступ от края по умолчанию: **5 мм**, настраиваются.
- Поиск DXF: рекурсивно по папке поиска; нормализованное имя файла сравнивается с нормализованным `Part.Name`; при отсутствии точного совпадения — fallback по первому токену имени (как в NS_PDF_Merge).
- Локализация: каркас + ресурсы **EN/UA** (RU не добавлять).
- Автообновление: каркас сервиса, проверяет релизы `eanord20/windows-tool`, ожидает ассет `NStCut_Setup.exe`.

---

## 2. Data Structures & Interfaces

### 2.1 Core models

```csharp
public sealed class Part
{
    public string Name { get; set; } = string.Empty;          // Part Name / обозначение
    public int Quantity { get; set; } = 1;                    // Qty
    public string MaterialId { get; set; } = string.Empty;    // Id назначенного материала
    public string DxfPath { get; set; } = string.Empty;       // найденный/выбранный файл
    public PartStatus Status { get; set; } = PartStatus.Pending;
    public bool IsManuallyResolved { get; set; }              // файл выбран вручную
    public GrainMode GrainMode { get; set; } = GrainMode.FollowMaterial;
    public Polygon2D? Contour { get; set; }                     // реальный контур (v1.1)
    public Rect Bounds { get; set; }                          // bounding-box в мм
    public double Area { get; set; }
}

public enum PartStatus { Pending, Found, ManuallySelected, NotFound, Ambiguous }

public enum GrainMode { FollowMaterial, Allow90, Fixed0 }
```

```csharp
public sealed class SheetMaterial
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;          // "Steel 2 mm"
    public double Length { get; set; }                         // мм
    public double Width { get; set; }                          // мм
    public double? Thickness { get; set; }                     // опционально
    public bool HasGrain { get; set; }                         // текстурный материал
    public GrainDirection GrainDirection { get; set; }         // Horizontal / Vertical
}

public enum GrainDirection { Horizontal, Vertical }
```

```csharp
public sealed class NestingOptions
{
    public double PartGapMm { get; set; } = 5.0;                // зазор между деталями
    public double SheetMarginMm { get; set; } = 5.0;          // отступ от края листа
    public double Resolution { get; set; } = 0.01;            // точность сравнения, мм
}
```

### 2.2 Nesting result

```csharp
public sealed class PlacedPart
{
    public Part Source { get; set; } = null!;
    public int CopyIndex { get; set; }                       // 1..Qty
    public Point Position { get; set; }                     // левый нижний угол bounding-box
    public double Rotation { get; set; }                    // 0 или 90
    public Rect Bounds { get; set; }
}

public sealed class SheetInstance
{
    public SheetMaterial Material { get; set; } = null!;
    public int Index { get; set; }                          // 1-based в рамках материала
    public List<PlacedPart> Parts { get; } = new();
    public double UtilizationPercent { get; set; }
    public double WasteArea { get; set; }
}

public sealed class NestingResult
{
    public List<SheetInstance> Sheets { get; } = new();
    public List<Part> Unplaced { get; } = new();
    public double TotalUtilizationPercent { get; set; }
}
```

### 2.3 Engine contract

```csharp
public interface INestingEngine
{
    NestingResult Nest(IReadOnlyList<Part> parts,
                       IReadOnlyList<SheetMaterial> materials,
                       NestingOptions options,
                       IProgress<double>? progress = null,
                       CancellationToken ct = default);
}
```

MVP-реализация:

```csharp
public sealed class BoundingBoxShelfEngine : INestingEngine
{
    public NestingResult Nest(...) { /* shelf packing по bounding-box */ }
}
```

v1.1 добавляет:

```csharp
public sealed class PolygonNestingEngine : INestingEngine { }
```

### 2.4 DXF contract

```csharp
public interface IDxfGeometryService
{
    /// <summary>Читает DXF и возвращает ограничивающий прямоугольник и (в будущем) контур.</summary>
    DxfContour ReadContour(string path);
}

public sealed class DxfContour
{
    public Rect Bounds { get; set; }
    public Polygon2D? Polygon { get; set; }
}
```

```csharp
public interface IDxfExportService
{
    void WriteSheet(string outputPath, SheetInstance sheet, double gap, double margin);
}
```

### 2.5 Report contract

```csharp
public interface IReportService
{
    void GenerateReport(string outputPath,
                        string projectName,
                        IReadOnlyList<Part> parts,
                        IReadOnlyList<SheetInstance> sheets,
                        IReadOnlyList<byte[]> sheetThumbnails);
}
```

---

## 3. Execution Steps

### 3.1 Инициализация проекта

1. Создать папку `D:\My_project\N.StCut\src`.
2. Создать решение `NStCut.sln` и проекты:
   - `NStCut.Core` (class library, net8.0);
   - `NStCut.Dxf` (class library, net8.0) → ссылка на `Core` + `netDxf`;
   - `NStCut.Nesting` (class library, net8.0) → ссылка на `Core`;
   - `NStCut.Reports` (class library, net8.0) → ссылка на `Core` + `Xceed.Document.NET`;
   - `NStCut.App` (WPF, net8.0-windows) → ссылки на `Core`, `Dxf`, `Nesting`, `Reports` + `MaterialDesignThemes`.
3. Добавить `.gitignore` для C# / WPF.

### 3.2 Core

1. Добавить геометрические примитивы: `Point`, `Rect`, `Polygon2D`.
2. Добавить модели: `Part`, `SheetMaterial`, `NestingOptions`, `PlacedPart`, `SheetInstance`, `NestingResult`.
3. Добавить контракты: `INestingEngine`, `IDxfGeometryService`, `IDxfExportService`, `IReportService`.
4. Добавить константы: `AppConstants`, `DefaultNestingOptions`.

### 3.3 Dxf

1. Установить `netDxf` в `NStCut.Dxf`.
2. Реализовать `DxfGeometryService.ReadContour`:
   - открыть `DxfDocument.Load(path)`;
   - извлечь `LwPolyline`, `Line`, `Arc`, `Circle`;
   - для MVP вернуть `Bounds` по `netDxf.Extents` или вычислить min/max всех вершин;
   - `Polygon` оставить `null` для MVP.
3. Реализовать `DxfExportService.WriteSheet`:
   - создать новый `DxfDocument`;
   - для каждого `PlacedPart` загрузить исходный DXF, клонировать примитивы, перенести на `Position`, повернуть на 90° при необходимости;
   - добавить рамку листа (`LwPolyline` по углам);
   - сохранить.

### 3.4 Nesting

1. Реализовать `BoundingBoxShelfEngine`:
   - сгруппировать детали по `MaterialId`;
   - для каждой группы раскрыть `Quantity` в отдельные экземпляры;
   - учесть `GrainMode` и `HasGrain`:
     - `Fixed0` — только 0°;
     - `Allow90` — всегда 0°/90° (даже для текстурного материала — ответственность пользователя);
     - `FollowMaterial` — ориентация детали совпадает с направлением текстуры материала; для текстурного материала повороты запрещены, для нетекстурного эквивалентно `Allow90`.
   - отсортировать копии деталей по убыванию площади bounding-box;
   - shelf packing: размещать слева направо по полке; если не помещается по ширине — новая полка; если полка выходит за высоту листа — новый лист;
   - к эффективным размерам детали прибавить `PartGap` по обеим сторонам (`effectiveW = w + gap, effectiveH = h + gap`); usable-область листа уменьшается на `SheetMargin` с каждой стороны;
   - вернуть `NestingResult` с `Unplaced`, если деталь не влезает даже на отдельный лист с учётом gap/margin.

### 3.5 Reports

1. Установить `Xceed.Document.NET` в `NStCut.Reports`.
2. Реализовать `ReportService.GenerateReport`:
   - создать `DocX.Create(outputPath)`;
   - заголовок: название проекта + дата;
   - таблица использованных материалов: материал, размер, количество листов;
   - таблица деталей: имя, материал, количество, площадь;
   - таблица листов: №, материал, заполнение %;
   - вставить PNG-миниатюры под таблицей.

### 3.6 App (WPF)

1. Создать `MainWindow.xaml` по образцу NS_PDF_Merge:
   - верхнее меню: Файл, Настройки, Справка;
   - две строки путей: `DXF Search Folder`, `Output Folder`;
   - панель кнопок: `Add Row`, `Find Files`, `Materials`, `Nest`, `Export DXF`, `Export Report`;
   - `DataGrid` с колонками: Qty, Part Name, DXF File, Material, Grain, Rotate, Status;
   - контекстное меню строки: удалить, выбрать DXF вручную, найти файл для строки;
   - статус-бар.
2. Реализовать `MainViewModel`:
   - `ObservableCollection<PartRow> Rows`;
   - команды: Add, Delete, Find, Nest, ExportDxf, ExportReport, Undo, PasteFromClipboard, ImportExcel, SelectFileManually;
   - Undo-стек через `Clone()`;
   - поиск файлов через `FileResolveService` с нормализацией имён.
3. Создать `MaterialsWindow` для редактирования списка материалов.
4. Создать `SheetPreviewWindow` с `TabControl`: вкладка на каждый лист, `Canvas` с деталями, подпись размеров/заполнения.
5. Добавить `LocalizationService` с ресурсами EN/UA.
6. Добавить каркас `UpdateService` для проверки GitHub Releases `eanord20/windows-tool`, ассет `NStCut_Setup.exe`.
7. Перетаскивание строк и копирование в буфер в MVP не реализуются.

### 3.7 Project save/load

1. Добавить `ProjectFileService`:
   - сохранять `.nstcut` как JSON (`parts`, `materials`, `searchPath`, `outputPath`, `options`);
   - загружать обратно.

### 3.8 Verification

1. Собрать решение: `dotnet build src/NStCut.sln -c Release`.
2. Запустить unit-тесты для `Core` и `Nesting` (если добавлены).
3. Проверить сценарий: 3 детали, 1 материал, автоматический раскрой, экспорт DXF + DOCX.

---

## 4. Edge Cases & Safety Rules

### 4.1 Edge cases

- **DXF не найден / повреждён**: строка получает статус `NotFound`; повреждённый файл — `NotFound` с записью в лог.
- **Поиск DXF**: точное совпадение нормализованного `Part.Name` с именем файла; fallback — первый токен `Part.Name` является префиксом имени файла (как в NS_PDF_Merge).
- **Несколько кандидатов**: статус `Ambiguous`; если пользователь отменяет выбор, строка остаётся `Ambiguous`.
- **Деталь больше листа**: деталь попадает в `NestingResult.Unplaced`; пользователь видит предупреждение.
- **Нет материалов или не назначен материал**: кнопка `Nest` отключена.
- **Quantity <= 0**: при импорте устанавливать в 1 и записывать предупреждение в лог; UI ограничивает ввод минимумом 1.
- **Отрицательные/нулевые размеры листа**: валидация в `MaterialsWindow`.
- **Повторяющиеся имена деталей**: разрешены, различаются по строке; дубли не помечаются, если не требуется.
- **Масштаб DXF**: в MVP единицы DXF принимаются за миллиметры. Конвертацию по `INSUNITS` отложить на v1.1.
- **Долгий раскрой**: передавать `IProgress<double>` и `CancellationToken` в движок, показывать `ProgressBar`.

### 4.2 Safety rules

- Не хардкодить размеры, цвета, зазоры и пути — использовать `AppConstants`, `NestingOptions`, ресурсы XAML.
- Не хранить секреты/пароли в коде; пути к папкам — в пользовательских настройках/проекте.
- Все COM-объекты не используются, поэтому `Marshal.ReleaseComObject` не требуется.
- DXF-файлы открывать только на чтение до момента экспорта; не модифицировать исходники.
- Все сервисы принимают абстракции (`IReadOnlyList`, интерфейсы), не завязываться на WPF-классы в `Core`/`Nesting`/`Dxf`/`Reports`.

### 4.3 Future-proofing

- `INestingEngine` позволяет подменить `BoundingBoxShelfEngine` на `PolygonNestingEngine`.
- `IDxfGeometryService` возвращает `Polygon2D?`, чтобы v1.1 использовать реальные контуры.
- `IReportService` принимает миниатюры как `byte[]`, не зависит от WPF-контролов.
