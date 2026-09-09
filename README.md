# N.StCut

Windows WPF-приложение для автоматического раскроя DXF-плоских деталей на листы.

## Возможности

- Загрузка деталей из DXF с корректным учётом контура, отверстий, единиц измерения и поворотов.
- Автоматический поиск DXF-файлов по папке поиска.
- Многошаговый детерминированный раскрой с учётом направления волокна материала.
- Экспорт результата в DXF и отчёт в DOCX.
- Поддержка светлой и тёмной темы, украинской и английской локализации.

## Технологии

- .NET 8
- WPF + MaterialDesignThemes
- netDxf 3.0.1
- Xceed.Words.NET / DocX
- SkiaSharp
- xUnit

## Сборка и запуск

```powershell
dotnet build src/NStCut.sln
```

Запуск:

```powershell
dotnet run --project src/NStCut.App/NStCut.App.csproj
```

## Тестирование

```powershell
dotnet test src/NStCut.App.Tests/NStCut.App.Tests.csproj
dotnet test src/NStCut.Nesting.Tests/NStCut.Nesting.Tests.csproj
dotnet test src/NStCut.Reports.Tests/NStCut.Reports.Tests.csproj
dotnet test src/NStCut.IntegrationTests/NStCut.IntegrationTests.csproj
dotnet test src/NStCut.Dxf.Tests/NStCut.Dxf.Tests.csproj
dotnet test src/NStCut.Core.Tests/NStCut.Core.Tests.csproj
```

## Структура решения

| Проект | Назначение |
|---|---|
| `NStCut.Core` | Геометрия, модели, контракты, метрики качества. |
| `NStCut.Dxf` | Чтение/экспорт DXF. |
| `NStCut.Nesting` | Алгоритмы раскроя. |
| `NStCut.Reports` | Генерация DOCX-отчётов. |
| `NStCut.App` | WPF UI, ViewModels и сервисы приложения. |
| `*.Tests` / `IntegrationTests` | Юнит- и интеграционные тесты. |

## Документация

- `AGENTS.md` — правила проекта и команды сборки/тестирования.
- `docs/Refactoring_Plan.md` — план рефакторинга.
- `docs/Implementation_Log.md` — журнал изменений по этапам (RF-001…RF-031, NSC-011…NSC-014).
- `docs/superpowers/` — дизайн-документы, спецификации и аудиты.

## Ветка разработки

- `feat/nstcut-v1.0`

## Текущий статус

- ✅ Рефакторинг Stage 1–4 завершён.
- ✅ Реализованы NSC-011…NSC-014: корпус DXF, истинная площадь/периметр, жизненный цикл результата, отмена операций.
- Сборка без предупреждений, все тесты проходят.
