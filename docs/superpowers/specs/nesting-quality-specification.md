# Nesting Quality Specification — N.StCut

## Цель

Определить, что в N.StCut считается **хорошим раскроем**, разложить это понятие на измеримые критерии и дать формальные определения, пригодные для реализации в коде и для ручного аудита.

> Главный принцип: **максимальный процент заполнения листа ≠ оптимальный раскрой**. Хороший раскрой должен быть одновременно компактным, экономичным, технологичным и пригодным для производства.

---

## Классификация критериев

| Класс | Что означает | Последствия нарушения |
|-------|--------------|----------------------|
| **HARD CONSTRAINT** | Любое решение, его нарушающее, недопустимо. | Раскрой отбраковывается (бесконечный штраф). |
| **PRIMARY OBJECTIVE** | Главные критерии оптимизации. | Напрямую влияют на стоимость и пригодность. |
| **SECONDARY OBJECTIVE** | Критерии, решающие между раскроями с близкими PRIMARY. | Определяют производственное качество. |
| **HEURISTIC / QUALITY FEATURE** | Признаки, сложно выразимые одной формулой, но важные для человеческой оценки. | Используются как тай-брейки и диагностика. |

---

## Обозначения

Для формализации используются следующие величины:

- `S = (L, W)` — лист материала, `L` — длина, `W` — ширина.
- `M` — технологический отступ от края листа (`SheetMargin`).
- `G` — минимальный зазор между деталями (`PartGap`).
- `R = (L − 2M, W − 2M)` — полезная (рабочая) область листа.
- `p_i` — деталь с bounding-box `b_i = (x_i, y_i, w_i, h_i)` в мировых координатах листа. `x_i, y_i` — левый нижний угол.
- `A(p_i)` — площадь детали. Если известен реальный контур (`Part.Contour`), используется истинная площадь; иначе `w_i * h_i`.
- `P(p_i)` — периметр детали. Если контур известен — периметр контура; иначе `2(w_i + h_i)`.
- `F(S)` — множество свободных прямоугольников (free rectangles) на листе после размещения всех деталей.
- `N_unplaced` — количество неразмещённых экземпляров деталей.
- `N_sheets` — количество использованных листов.

---

## A. Корректность — HARD CONSTRAINT

### A1. Отсутствие пересечений

Ни одна деталь не должна пересекаться с другой с учётом технологического зазора `G`.

```
Forall i != j:
  x_i + w_i + G <= x_j   OR   x_j + w_j + G <= x_i
  OR
  y_i + h_i + G <= y_j   OR   y_j + h_j + G <= y_i
```

> В текущем движке проверяется через `MaxRectsPacker._effectiveBounds` (`src/NStCut.Nesting/Internals/MaxRectsPacker.cs`).

### A2. Выход за границы листа

Каждая деталь целиком внутри рабочей области листа:

```
x_i >= M
y_i >= M
x_i + w_i <= L - M
y_i + h_i <= W - M
```

### A3. Технологический зазор

Расстояние между границами любых двух деталей не меньше `G`. Расстояние от детали до края листа не меньше `M` (отступ). Формально зазор учитывается в A1 и A2.

### A4. Количество деталей

Все экземпляры, требуемые входными `Part.Quantity`, должны быть размещены либо явно помечены как `Unplaced`.

```
Sum(placed copies of every part) + Unplaced(part) == part.Quantity
```

### A5. Ограничения поворота

Допустимые ориентации детали определяются `Part.GrainMode` и `SheetMaterial.HasGrain` (`src/NStCut.Core/Nesting/OrientationPolicy.cs`).

- `Fixed0` — только исходная ориентация.
- `FollowMaterial` — если материал имеет текстуру (`HasGrain == true`), поворот запрещён; иначе допустим 90°.
- `Allow90` — допустимы обе ориентации (если стороны не равны).

### A6. Текстура / направление материала

Если материал имеет направление текстуры (`GrainDirection`), деталь с `GrainMode.FollowMaterial` должна быть ориентирована вдоль разрешённого направления. На практике текущий код сводит это к запрету поворота при `HasGrain == true`.

---

## B. Использование материала — PRIMARY OBJECTIVE

### B1. Общая площадь деталей

```
TotalPartArea = Sum over all placed p_i of A(p_i)
```

### B2. Площадь использованного материала

```
UsedSheetArea = N_sheets * (L * W)
```

### B3. Площадь отходов

```
WasteArea = UsedSheetArea - TotalPartArea
```

### B4. Коэффициент использования материала

```
Utilization = TotalPartArea / UsedSheetArea * 100%
```

> **Важно:** `Utilization` — необходимый, но не достаточный показатель. Он не учитывает форму отходов, компактность и производственную пригодность. Для непрямоугольных деталей `A(p_i)` должна вычисляться по контуру, иначе utilization завышается (см. Document D).

---

## C. Количество листов — PRIMARY OBJECTIVE

Минимизация `N_sheets` — один из важнейших критериев, потому что целый неиспользованный лист стоит дороже, чем локальные отходы на уже раскроенном листе.

```
FewerSheetsIsBetter(N_sheetsA, N_sheetsB) = N_sheetsA < N_sheetsB
```

Алгоритм должен сначала стремиться уложить все детали в минимальное число листов, и только после этого улучшать utilization и другие secondary-критерии.

---

## D. Компактность раскроя — SECONDARY OBJECTIVE

Компактность показывает, насколько плотно занята рабочая область и насколько мало «пустот» внутри bounding-box раскроя.

### D1. Занятая bounding-область листа

```
UsedWidth(S)  = Max over placed p_i on sheet S of (x_i + w_i) - M
UsedHeight(S) = Max over placed p_i on sheet S of (y_i + h_i) - M
UsedBoundingArea(S) = UsedWidth(S) * UsedHeight(S)
TotalUsedBoundingArea = Sum over sheets of UsedBoundingArea(S)
```

### D2. Компактность по bounding-box

```
Compactness = TotalPartArea / TotalUsedBoundingArea
```

Больше — лучше. Два раскроя с одинаковой utilization могут иметь разную `Compactness`, если один более «собран» по углам, а другой растянут.

### D3. Максимальный использованный размер

```
MaxDim(S) = Max(UsedWidth(S), UsedHeight(S))
TotalMaxDim = Sum over sheets of MaxDim(S)
```

Меньше `TotalMaxDim` предпочтительнее: это означает, что раскрой не растягивается в одну сторону.

### D4. Кластеризация

Количество изолированных групп деталей на листе. Меньше групп — лучше. В текущем bbox-движке не измеряется отдельно, но косвенно отражается в `UsedBoundingArea`.

---

## E. Качество свободного пространства — SECONDARY OBJECTIVE

Свободная область листа должна быть **полезной**, а не просто большой по площади.

### E1. Свободные прямоугольники

`F(S)` — остаточные прямоугольники, которые MaxRects алгоритм оставляет после размещения. Каждый `f ∈ F(S)` имеет ширину `w_f`, высоту `h_f`, площадь `a_f = w_f * h_f`.

### E2. Фрагментация

```
FragmentationCount(S) = |F(S)|
```

Много мелких свободных областей хуже, чем одна большая, даже если суммарная площадь одинакова.

### E3. Long Thin Waste (длинные узкие полосы)

```
IsLongThin(f) = (max(w_f, h_f) / min(w_f, h_f) > AspectRatioThreshold)
                AND (min(w_f, h_f) < MinUsefulWidth)
LongThinWasteCount(S) = Count of f in F(S) where IsLongThin(f)
```

Пороги задаются в `AppConstants` / `NestingOptions` (например, `AspectRatioThreshold = 5.0`, `MinUsefulWidth = 200 mm`).

### E4. Deep Pocket (глубокие карманы)

Карман — это область, которая имеет достаточную площадь, но узкое «горло», не позволяющее вставить типовую деталь. В bbox-движке аппроксимируем как free rectangle, у которого:

```
IsDeepPocket(f) = (a_f > DeepPocketAreaThreshold)
                  AND (min(w_f, h_f) < DeepPocketMinDimension)
```

Точное определение требует анализа union of free rectangles / visibility, поэтому в текущей реализации — упрощённая версия.

### E5. Fillability / Residual Space Quality

Насколько свободная площадь пригодна для размещения **оставшихся** деталей.

Для каждой неразмещённой детали `u` и каждого `f ∈ F(S)`:

```
Fits(u, f) = Exists orientation (w_u, h_u) such that
             w_u + G <= w_f AND h_u + G <= h_f
UnplacedFillability = Count of u where Exists f: Fits(u, f)
```

`UnplacedFillability > 0` означает, что движок пропустил возможность разместить деталь в существующее свободное место.

---

## F. Размещение маленьких деталей — SECONDARY OBJECTIVE

Алгоритм не должен просто сортировать детали по убыванию площади и последовательно раскладывать. Мелкие детали должны заполнять промежутки, оставшиеся после крупных.

### F1. Small-Part Integration

Для каждого листа измеряется, какая доля свободных прямоугольников может быть заполнена мелкими деталями из входного набора:

```
SmallParts = { p | max(w_p, h_p) <= 0.5 * min(SheetLength, SheetWidth) }
FillableBySmall(f) = Exists p in SmallParts: Fits(p, f)
SmallPartIntegrationScore(S) = Sum of a_f over FillableBySmall(f) / TotalFreeArea(S)
```

Если много свободных областей подходят под мелкие детали, а сами детали не размещены там, оценка низкая.

### F2. Блокирование мелких деталей крупными

Если крупная деталь расположена так, что разделяет свободное пространство на мелкие несвязные фрагменты, в которых не помещаются оставшиеся детали, это считается плохим раскроем. Измеряется косвенно через `FragmentationCount` и `UnplacedFillability`.

---

## G. Взаимное сопряжение деталей — SECONDARY OBJECTIVE

Хороший раскрой использует параллельные стороны и плотное прилегание деталей (с учётом зазора).

### G1. Contact Length

```
Contact(i, j) = Длина общего участка границ двух bounding-box,
                если расстояние между ними <= G + Resolution
TotalContactLength = Sum over all i < j of Contact(i, j)
```

Больше `TotalContactLength` означает более плотное сопряжение и, как правило, меньше отходов.

### G2. Parallel Edge Alignment

Доля деталей, ориентированных параллельно краям листа или друг другу (0°/90°). В текущем движке детали уже ориентированы 0°/90°, поэтому этот критерий автоматически удовлетворяется.

---

## H. Nested-in-hole — HEURISTIC / QUALITY FEATURE

Размещение одной детали внутри отверстия или внутренней свободной области другой детали.

```
NestedInHole(p_inside, p_outside) =
  BoundingBox(p_inside) ⊂ BoundingBox(p_inside of p_outside)
  AND RealContour(p_inside) ⊂ RealHole(p_outside)
```

> **Статус в N.StCut:** текущий движок работает только с bounding-box и не знает реальных контуров (`Part.Contour` не используется при размещении). Поэтому `NestedInHole` не поддерживается и должна быть задокументирована как отсутствующая возможность. Метрика `NestedInHoleCount` = 0 для всех текущих результатов.

---

## I. Качество остатков — SECONDARY OBJECTIVE

Не весь leftover одинаков. Свободные прямоугольники классифицируются по полезности.

### I1. Remnant Value

Для каждого free rectangle `f`:

```
if w_f >= MinUsefulLength AND h_f >= MinUsefulWidth AND aspectRatio(f) <= MaxUsefulAspectRatio:
    class = Valuable
    value = a_f
elif w_f >= MinUsefulLength OR h_f >= MinUsefulWidth:
    class = PotentiallyUseful
    value = a_f * 0.5
else:
    class = Scrap
    value = 0
```

```
TotalRemnantValue(S) = Sum of value(f) over all f in F(S)
```

### I2. Scrap Ratio

```
ScrapRatio(S) = Sum of a_f for Scrap(f) / TotalFreeArea(S)
```

Меньше — лучше.

---

## J. Производственная эффективность — SECONDARY / HEURISTIC

### J1. Общая длина реза

```
TotalCutLength = Sum over all placed p_i of P(p_i)
```

Меньше `TotalCutLength` уменьшает время резки и износ инструмента.

### J2. Количество отдельных контуров резки

Каждый замкнутый контур детали требует отдельного прохода. Для bbox-движка эквивалентно количеству размещённых деталей.

### J3. Nesting vs Toolpath

`TotalCutLength` — это **nesting**-метрика. Оптимизация последовательности резки (toolpath) — отдельная задача и не должна смешиваться с раскроем.

---

## K. Геометрическая плотность — SECONDARY OBJECTIVE

### K1. Average Part Density

```
Density(S) = TotalPartArea(S) / (UsedWidth(S) * UsedHeight(S))
```

### K2. Minimum Part Distance

Минимальное расстояние между границами любых двух деталей. В хорошем раскрое оно должно быть равно технологическому зазору `G`, но не сильно больше (иначе есть лишние пустоты).

---

## L. Visual Quality — HEURISTIC / COMPOSITE

Визуальная оценка оператора отражает совокупность:

- крупные детали образуют компактное ядро;
- мелкие детали заполняют промежутки;
- нет длинных пустых полос и глубоких карманов;
- остатки имеют разумную форму;
- раскрой имеет понятную, «собранную» структуру.

Для автоматизации `VisualQualityScore` определяется как композит:

```
VisualQualityScore =
  w1 * Compactness
  + w2 * (1 - FragmentationPenalty)
  + w3 * RemnantValueRatio
  + w4 * (1 - LongThinWastePenalty)
```

> В качестве финального тай-брейка между раскроями с одинаковыми PRIMARY/SECONDARY метриками `VisualQualityScore` используется как субъективно-объективная оценка. Конкретные веса/порядок определяются в Document B (Nesting Objective Function).

---

## Связь с кодом

- `Part` / `PlacedPart` / `SheetInstance` — `src/NStCut.Core/Models/`.
- `OrientationPolicy` — `src/NStCut.Core/Nesting/OrientationPolicy.cs`.
- `MaxRectsPacker` — `src/NStCut.Nesting/Internals/MaxRectsPacker.cs`.
- `NestingResult` / `NestingPassResult` — `src/NStCut.Core/Models/NestingResult.cs`, `src/NStCut.Core/Nesting/NestingPassResult.cs`.

---

## История изменений

- 2026-09-02 — создание документа в рамках формализации критериев качества раскроя.