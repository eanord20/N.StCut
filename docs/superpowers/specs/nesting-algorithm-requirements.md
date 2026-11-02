# Nesting Algorithm Requirements — N.StCut

## Цель

Определить требования к алгоритму раскроя N.StCut и эталонные тестовые сценарии, на которых проверяется соответствие реализации критериям качества.

---

## 1. Функциональные требования

### 1.1. Входные данные

- Список деталей `IReadOnlyList<Part>` с `Quantity`, `MaterialId`, `Bounds`, `Area`, `GrainMode`.
- Список материалов `IReadOnlyList<SheetMaterial>` с `Id`, `Length`, `Width`, `HasGrain`, `GrainDirection`.
- Настройки `NestingOptions`:
  - `PartGapMm`
  - `SheetMarginMm`
  - `Resolution`
  - `MaxSearchPasses`
  - `NoImprovementStopCount`
  - `AcceptableMaxUnplaced`
  - `AcceptableMaxSheets`
  - `PlacementHeuristic`
  - `OrientationPreference`
  - `PackStrategyIndex`
  - `LogFilePath`

### 1.2. Выходные данные

- `NestingResult`:
  - `Sheets` — размещённые листы с `PlacedPart`.
  - `Unplaced` — неразмещённые экземпляры.
  - `TotalUtilizationPercent`.
  - `LayoutHash`.

### 1.3. Hard Constraints

Алгоритм должен гарантировать:

- Все размещённые детали внутри рабочей области листа с учётом `SheetMargin`.
- Никакие две детали не пересекаются с учётом `PartGap` и `Resolution`.
- `GrainMode` и `SheetMaterial.HasGrain` соблюдены.
- Число размещённых экземпляров каждой детали не превышает `Quantity`.
- `LayoutHash` одинаковый для одинакового входа и результата.

### 1.4. Метрики, которые должен вычислять движок

Алгоритм (или связанные сервисы) должны предоставлять:

- `NestingQualityMetrics.Compute(NestingResult)`:
  - `UnplacedCount`
  - `SheetCount`
  - `TrueUtilizationPercent`
  - `UsedBoundingArea`
  - `MaxUsedDimensionSum`
  - `ResidualQualityScore`
  - `UnplacedFillability`
  - `GeometricCompatibilityScore`
  - `TotalCutLength`
  - `VisualQualityScore`

### 1.5. Сравнение результатов

- `NestingResultComparison.IsBetter(candidate, current)` использует `NestingQualityScore` в соответствии с `nesting-objective-function.md`.
- `MultiPassNestingEngine` выбирает лучший результат из всех проходов с помощью этого сравнения.
- `BoundingBoxShelfEngine` при выборе `PackingStrategy` внутри группы материалов должен использовать ту же логику (или максимально близкую, применимую к одной группе).

### 1.6. Детерминизм

- При фиксированных `parts`, `materials`, `options` результат всегда одинаковый.
- `LayoutHash` должен быть стабильным.
- Любые random/псевдослучайные перестановки используют `DeterministicPermutationSource`.

### 1.7. Логирование проходов

`MultiPassNestingEngine.LastPassLog` должен содержать для каждого прохода:

- `Pass`, `Sheets`, `Unplaced`, `UtilizationPercent`, `DurationMs`.
- `PackStrategyIndex`, `PlacementHeuristic`, `OrientationPreference`.
- `LayoutHash`, `UsedBoundingArea`.
- `ResidualQualityScore`, `GeometricCompatibilityScore`, `TotalCutLength`, `VisualQualityScore`.

---

## 2. Эталонные тестовые сценарии

Каждый сценарий включает исходные данные, ожидаемое поведение, метрики и критерий победы.

### Scenario A — Identical parts / Material efficiency

**Исходные данные:**

- Лист: 2800 × 2070 мм.
- 40 одинаковых прямоугольных деталей 400 × 300 мм, `Allow90`.

**Ожидаемое поведение:**

- Все детали укладываются на 1 лист.
- Высокий `Utilization` (~80%+).
- `UnplacedCount` = 0.

**Метрики:**

- `SheetCount` = 1.
- `UsedBoundingArea` минимально возможно.
- `ResidualQualityScore` — остатки должны быть в основном крупными прямоугольниками.

**Критерий победы:**

`SheetCount` = 1, `UnplacedCount` = 0, максимальный `Utilization`, затем минимальный `UsedBoundingArea`.

---

### Scenario B — Various sizes / Compactness vs sheet count

**Исходные данные:**

- Лист: 2800 × 2070 мм.
- Детали разного размера:
  - 1200 × 600 × 2
  - 1000 × 500 × 2
  - 800 × 700 × 1
  - 600 × 400 × 3
  - 500 × 300 × 4
  - 400 × 300 × 5
  - 300 × 200 × 6

**Ожидаемое поведение:**

- Укладывается в 2 листа, `UnplacedCount` = 0.
- Лучший layout — тот, где крупные детали образуют компактное ядро, а мелкие заполняют промежутки.

**Метрики:**

- `SheetCount` = 2.
- `UsedBoundingArea` должен быть минимальным.
- `ResidualQualityScore` — меньше long-thin waste и мелкой фрагментации.
- `GeometricCompatibilityScore` — выше у плотного раскроя.

**Критерий победы:**

Сначала `SheetCount`, затем `Utilization`, затем `UsedBoundingArea`, затем `ResidualQualityScore`.

---

### Scenario C — Large + many small / Small-part integration

**Исходные данные:**

- Лист: 2800 × 2070 мм.
- 1 деталь 1800 × 1000 мм.
- 30 деталей 200 × 200 мм.

**Ожидаемое поведение:**

- Все детали на 1 листе.
- Мелкие детали должны заполнять свободные области вокруг крупной детали, а не оставаться неразмещёнными из-за порядка сортировки.

**Метрики:**

- `SheetCount` = 1, `UnplacedCount` = 0.
- `UnplacedFillability` = 0.
- `FragmentationCount` не должен быть высоким.

**Критерий победы:**

`UnplacedCount` = 0, минимум `SheetCount`, затем наименьшая `UsedBoundingArea` и `UnplacedFillability`.

---

### Scenario D — Parts that benefit from rotation / Grain & orientation

**Исходные данные:**

- Лист: 2800 × 2070 мм.
- 900 × 400 × 5
- 700 × 300 × 5
- 1200 × 500 × 2
- 500 × 900 × 3

**Ожидаемое поведение:**

- Повороты уменьшают число листов.
- Лучший результат имеет меньше листов за счёт правильных ориентаций.

**Метрики:**

- `SheetCount` с поворотами меньше, чем без.
- `TrueUtilizationPercent` выше.

**Критерий победы:**

Минимум `SheetCount`, затем максимум `Utilization`, затем `UsedBoundingArea`.

---

### Scenario E — Almost full sheet / Residual quality

**Исходные данные:**

- Лист: 2800 × 2070 мм.
- 1800 × 1000 × 1
- 900 × 800 × 1
- 400 × 300 × 4

**Ожидаемое поведение:**

- Все детали на 1 листе.
- Остатки должны быть крупными и пригодными, а не раздробленными на мелкие кусочки.

**Метрики:**

- `SheetCount` = 1, `UnplacedCount` = 0.
- `ResidualQualityScore` максимален.
- `LongThinWasteCount` и `DeepPocketCount` минимальны.

**Критерий победы:**

При `SheetCount` = 1 и `UnplacedCount` = 0 выбирается layout с максимальным `ResidualQualityScore`.

---

### Scenario F — Many parts needing multiple sheets / Sheet count priority

**Исходные данные:**

- Лист: 2800 × 2070 мм.
- 80 деталей 500 × 400 мм.

**Ожидаемое поведение:**

- Минимальное число листов (расчётное ≈ 4).
- Все детали размещены.

**Метрики:**

- `SheetCount` минимально.
- `Utilization` высокий.

**Критерий победы:**

Минимум `SheetCount`, затем максимум `Utilization`.

---

### Scenario G — Mixed realistic / Visual quality and overall balance

**Исходные данные:**

- Лист: 2800 × 2070 мм.
- 1200 × 600 × 2
- 1000 × 500 × 2
- 800 × 700 × 1
- 600 × 400 × 4
- 400 × 300 × 10
- 200 × 200 × 20

**Ожидаемое поведение:**

- 2 листа, `UnplacedCount` = 0.
- Раскрой должен выглядеть «собранным»: крупные детали образуют базу, мелкие заполняют пустоты, остатки регулярны.

**Метрики:**

- `SheetCount` = 2, `UnplacedCount` = 0.
- `VisualQualityScore` высокий.
- `ResidualQualityScore` высокий.
- `ContactLength` высокий.

**Критерий победы:**

Лексикографический: `SheetCount`, `Utilization`, `UsedBoundingArea`, `ResidualQualityScore`, `VisualQualityScore`.

---

### Scenario H — L-shaped parts / Concave waste

**Исходные данные:**

- Лист: 2000 × 1000 мм.
- 2 L-образных детали (например, 800 × 800 с вырезом 400 × 400).
- 4 прямоугольные детали 300 × 300 мм, которые физически помещаются в вырез L-образной детали.

**Ожидаемое поведение:**

- Идеально: мелкие прямоугольники размещаются в вырезах L-образных деталей, уменьшая число листов.
- В текущем bbox-движке: движок не знает реальных контуров, поэтому размещает детали как bounding-box. Сценарий показывает ограничение.

**Метрики:**

- `NestedInHoleCount` (целевое > 0, в bbox-движке = 0).
- `TrueUtilizationPercent` (по реальной площади контура) должен быть высоким при правильном размещении.

**Критерий победы:**

Минимум `SheetCount` и `UnplacedCount`, максимум `NestedInHoleCount`. Для текущей реализации тест документирует, что `NestedInHoleCount` = 0.

---

### Scenario I — Holes inside parts / Void utilization

**Исходные данные:**

- Лист: 2000 × 1000 мм.
- 1 крупная деталь 1000 × 1000 с круглым/прямоугольным отверстием 500 × 500 внутри.
- 8 деталей 200 × 200, которые могут поместиться в отверстие.

**Ожидаемое поведение:**

- Идеально: мелкие детали в отверстии, 1 лист.
- В bbox-движке: мелкие детали размещаются вне bounding-box крупной детали; если не хватает места, уходят на второй лист или в `Unplaced`.

**Метрики:**

- `NestedInHoleCount`.
- `SheetCount`.
- `UnplacedFillability` — если мелкие детали не размещены, но физически помещались бы в отверстие, метрика > 0.

**Критерий победы:**

Для целевого алгоритма: `SheetCount` = 1, `UnplacedCount` = 0. Для текущего bbox-движка: тест фиксирует `NestedInHoleCount` = 0 и `UnplacedFillability` > 0, если мелкие не влезают снаружи.

---

### Scenario J — Greedy trap / Human-better layout

**Исходные данные:**

- Лист: 1500 × 1000 мм.
- 1 деталь 1000 × 600.
- 6 деталей 400 × 300.
- 12 деталей 200 × 150.

**Ожидаемое поведение:**

- Простой greedy по убыванию площади может поставить 1000×600 в угол и заполнить только часть, забыв мелкие.
- Оптимальный layout группирует детали так, чтобы мелкие заполнили пустоты и осталось меньше отходов.

**Метрики:**

- `UnplacedCount` = 0.
- `UsedBoundingArea` меньше у хорошего layout.
- `ResidualQualityScore` выше у хорошего layout.
- `VisualQualityScore` выше.

**Критерий победы:**

Сначала `SheetCount`, затем `UnplacedCount`, затем `UsedBoundingArea`, `ResidualQualityScore`, `VisualQualityScore`.

---

## 3. Критерии приёмки runnable тестов

Для каждого сценария тест должен:

1. Запустить `MultiPassNestingEngine` с `MaxSearchPasses >= 40`.
2. Проверить `UnplacedCount` и `SheetCount`.
3. Вычислить `NestingQualityScore` через `NestingQualityMetrics`.
4. Сравнить `best.LayoutHash` с layout, построенным из `PreviewPartViewModel` / `SheetInstance.Parts` (для проверки консистентности UI).
5. Для сценариев H и I зафиксировать текущее ограничение bbox-движка.

---

## 4. API требования

### 4.1. `NestingQualityMetrics`

```csharp
public static class NestingQualityMetrics
{
    public static NestingQualityScore Compute(NestingResult result);
    public static NestingQualityScore Compute(IReadOnlyList<SheetInstance> sheets, int unplacedCount);
}
```

- Не мутирует `NestingResult`.
- Использует `SheetInstance.FreeRectangles`, если доступны.
- Fallback на `Bounds` для площади/периметра, если `Part.Contour == null`.

### 4.2. `NestingQualityScore`

```csharp
public sealed record NestingQualityScore(...)
    : IComparable<NestingQualityScore>
{
    public bool IsBetterThan(NestingQualityScore other);
}
```

### 4.3. `NestingResultComparison`

```csharp
public static class NestingResultComparison
{
    public static bool IsBetter(NestingResult candidate, NestingResult current);
}
```

- Внутренне вызывает `NestingQualityMetrics.Compute` и `NestingQualityScore.CompareTo`.
- LayoutHash — финальный tie-break.

---

## 5. Связь с документами

- `nesting-quality-specification.md` — определения критериев.
- `nesting-objective-function.md` — финальный порядок сравнения.
- `nesting-current-algorithm-audit.md` — проверка, как текущий код соответствует этим требованиям.
- `nesting-improvement-roadmap.md` — план устранения найденных gap.

---

## История изменений

- 2026-09-02 — создание документа; сценарии A–J.