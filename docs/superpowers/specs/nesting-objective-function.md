# Nesting Objective Function — N.StCut

## Цель

Превратить критерии из `nesting-quality-specification.md` в однозначную, детерминированную функцию сравнения двух раскроев, которую можно реализовать в `NestingResultComparison`.

---

## Почему не weighted score

Weighted score выглядит соблазнительно:

```
Score = w1*N_sheets + w2*WasteArea + w3*Fragmentation + ...
```

Но для N.StCut он непригоден по нескольким причинам:

1. **Разные единицы измерения.** `N_sheets` — целое число, `WasteArea` — мм², `CutLength` — мм, `Fragmentation` — штуки. Их нельзя непосредственно складывать без искусственной нормализации, которая будет зависеть от масштаба деталей и листов.
2. **Нелинейные эффекты.** Один дополнительный лист обычно важнее, чем любое улучшение utilization внутри уже использованных листов. Weighted score не гарантирует этот приоритет.
3. **Конфликты критериев.** Например, больший `ContactLength` часто означает более плотный раскрой, но иногда увеличивает количество мелких «закутков» и ухудшает remnant value. Weighted score скрывает такие компромиссы.

### Почему lexicographic

Lexicographic optimization сравнивает результаты по приоритету: сначала самый важный критерий, и только при равенстве переходит к следующему. Это:

- понятно оператору;
- не требует подбора весов;
- гарантирует строгую иерархию критериев;
- детерминировано при явных tie-break.

---

## Итоговая objective function

Функция сравнения `Better(A, B)` определена через вектор `NestingQualityScore`, упорядоченный от наиболее значимого к наименее значимому.

### Порядок критериев

| # | Критерий | Направление | Пояснение |
|---|----------|-------------|-----------|
| 1 | `Validity` / `UnplacedCount` | меньше | Hard constraint: все детали должны быть размещены. |
| 2 | `SheetCount` | меньше | Минимум листов — главная экономика. |
| 3 | `TrueUtilizationPercent` | больше | Эффективность использования материала. |
| 4 | `UsedBoundingArea` | меньше | Компактность: меньше площадь, занятая bounding-box. |
| 5 | `MaxUsedDimensionSum` | меньше | Предотвращает «растягивание» раскроя в одну сторону. |
| 6 | `ResidualQualityScore` | больше | Форма и полезность остатков, фрагментация, long-thin waste, deep pockets. |
| 7 | `UnplacedFillability` | меньше | Сколько неразмещённых деталей физически влезало бы в свободные места. |
| 8 | `GeometricCompatibilityScore` | больше | Плотность сопряжения деталей (contact length). |
| 9 | `TotalCutLength` | меньше | Производственная эффективность. |
| 10 | `VisualQualityScore` | больше | Композитный визуальный/остаточный тай-брейк. |
| 11 | `LayoutHash` | меньше (string) | Детерминированный финальный tie-break. |

### Преобразование в lower-is-better вектор

Для единообразия сравнения каждый higher-is-better критерий инвертируется:

```
V = (
    UnplacedCount,
    SheetCount,
    -TrueUtilizationPercent,
    UsedBoundingArea,
    MaxUsedDimensionSum,
    -ResidualQualityScore,
    UnplacedFillability,
    -GeometricCompatibilityScore,
    TotalCutLength,
    -VisualQualityScore,
    LayoutHash
)
```

`Better(A, B)` — это лексикографическое сравнение вектора `V(A)` и `V(B)`: первое ненулевое отличие определяет победителя. `LayoutHash` сравнивается как строка (`Ordinal`), чтобы гарантировать детерминизм.

### `NestingQualityScore` record

```csharp
public sealed record NestingQualityScore(
    int UnplacedCount,
    int SheetCount,
    double TrueUtilizationPercent,
    double UsedBoundingArea,
    double MaxUsedDimensionSum,
    double ResidualQualityScore,
    int UnplacedFillability,
    double GeometricCompatibilityScore,
    double TotalCutLength,
    double VisualQualityScore,
    string LayoutHash)
{
    public int CompareTo(NestingQualityScore? other)
    {
        // ... lexicographic comparison
    }
}
```

Сравнение `double` выполняется через `double.CompareTo` с точностью, заданной `NestingOptions.Resolution` / `AppConstants.ComparisonResolution`, чтобы избежать чувствительности к плавающей точке.

---

## Конфликты критериев и правила разрешения

### 1. Utilization vs Residual Quality

Иногда layout с чуть меньшим utilization может давать значительно более крупные и полезные остатки. Поскольку `Utilization` стоит раньше `ResidualQualityScore`, мы жертвуем residual quality **только тогда**, когда utilization практически равен. Это соответствует правилу: материал важнее, но при равном материале выбираем лучшие остатки.

### 2. Compactness vs Residual Quality

`UsedBoundingArea` и `MaxUsedDimensionSum` идут перед `ResidualQualityScore`. Сперва выбираем компактный раскрой, затем смотрим на форму остатков. Это предотвращает выбор «красивых остатков» за счёт неэкономичного растягивания.

### 3. Geometric Compatibility vs Total Cut Length

`GeometricCompatibilityScore` идёт перед `TotalCutLength`. Более плотное сопряжение часто увеличивает общий периметр (cut length), но уменьшает отходы. Поскольку material efficiency уже учтена ранее, плотность сопряжения считается важнее длины реза.

### 4. Visual Quality vs LayoutHash

`VisualQualityScore` — последний критерий, который можно выразить числом. `LayoutHash` используется только для детерминированного разделения идентичных по всем метрикам раскроев и никогда не должен «улучшать» результат по сути.

---

## Формулы метрик

### TrueUtilizationPercent

```
TotalPartArea = Sum over all sheets of Sum over placed p of A(p)
TotalSheetArea = SheetCount * SheetLength * SheetWidth
TrueUtilizationPercent = TotalPartArea / TotalSheetArea * 100.0
```

`A(p)` = `Part.Contour?.Area ?? p.Bounds.Area`.

### UsedBoundingArea

```
UsedBoundingArea = Sum over sheets of (UsedWidth(S) * UsedHeight(S))
```

### MaxUsedDimensionSum

```
MaxUsedDimensionSum = Sum over sheets of Max(UsedWidth(S), UsedHeight(S))
```

### ResidualQualityScore

```
TotalFreeArea = Sum of a_f over all free rectangles f
ValuableArea = Sum of a_f where f is Valuable
UsefulArea = Sum of a_f where f is PotentiallyUseful
FragmentationPenalty = FragmentationCount * PenaltyPerFragment
LongThinPenalty = LongThinWasteCount * PenaltyPerLongThin
DeepPocketPenalty = DeepPocketCount * PenaltyPerDeepPocket

ResidualQualityScore =
    (ValuableArea * ValuableWeight
   + UsefulArea * UsefulWeight
   - FragmentationPenalty
   - LongThinPenalty
   - DeepPocketPenalty)
   / max(TotalFreeArea, 1.0)
```

> Если `TotalFreeArea == 0`, `ResidualQualityScore = 0`.
> Веса и пороги берутся из `AppConstants` / `NestingOptions`.

### UnplacedFillability

```
UnplacedFillability = Count of unplaced parts u where Exists free rectangle f and orientation o:
  o.Width + G <= f.Width AND o.Height + G <= f.Height
```

### GeometricCompatibilityScore

```
GeometricCompatibilityScore = TotalContactLength / (TotalPartPerimeter + epsilon)
```

Больше `TotalContactLength` при том же периметре означает более плотное сопряжение.

### TotalCutLength

```
TotalCutLength = Sum over all placed p of P(p)
```

`P(p)` = `Part.Contour?.Perimeter ?? 2*(w + h)`.

### VisualQualityScore

```
VisualQualityScore =
    Compactness * CompactnessWeight
  + RemnantValueRatio * RemnantWeight
  - FragmentationRatio * FragmentationWeight
  - LongThinRatio * LongThinWeight
```

Используется как финальный tie-break, когда все PRIMARY и SECONDARY метрики равны.

---

## Пример

Два layout для одного и того же набора деталей:

| Layout | Sheets | Utilization % | UsedBoundingArea | ResidualQualityScore | VisualQualityScore |
|--------|--------|---------------|------------------|----------------------|-------------------|
| A | 2 | 75.0 | 12 000 000 | 0.55 | 0.60 |
| B | 2 | 74.8 | 11 500 000 | 0.85 | 0.80 |

`A` выигрывает по utilization, но `B` — по `UsedBoundingArea` и `ResidualQualityScore`.

Сравнение:
- Sheets: равны.
- Utilization: A лучше (75.0 > 74.8).
- Остальные критерии не проверяются, потому что utilization — PRIMARY objective.

Если бы utilization был равен:
- UsedBoundingArea: B лучше (11 500 000 < 12 000 000).
- MaxDim: предположим равны.
- ResidualQualityScore: B лучше (0.85 > 0.55).

В этом случае `B` предпочтительнее, несмотря на чуть меньший utilization.

---

## Детерминизм

- Все сравнения `double` округлены до `Resolution`.
- `LayoutHash` вычисляется детерминированно (`src/NStCut.Core/Nesting/LayoutHash.cs`).
- `NestingQualityScore` не зависит от порядка проходов, только от конечного layout.

---

## Связь с кодом

- `NestingResultComparison` — `src/NStCut.Core/Nesting/NestingResultComparison.cs`.
- `NestingResult` / `SheetInstance` — `src/NStCut.Core/Models/`.
- `NestingQualityMetrics` / `NestingQualityScore` — будут созданы в `src/NStCut.Core/Nesting/`.

---

## История изменений

- 2026-09-02 — создание документа; выбрана лексикографическая objective function.