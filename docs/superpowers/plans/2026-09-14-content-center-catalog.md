# Content Center Catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Создать из `E:\ПТЕ\Контент\Книга3.xlsx` новую проверяемую Excel-книгу с нормализованным украинским каталогом стандартных изделий для Autodesk Inventor Content Center.

**Architecture:** Одноразовый Python-конвейер читает исходную книгу без изменения, разбирает каждую строку в структурированную запись, консервативно объединяет подтверждённые дубли и формирует пять листов аудируемого результата. Правила терминологии, классификации, стандартов и рекомендуемых типоразмеров хранятся отдельно от логики разбора, чтобы каждое преобразование можно было проверить тестами.

**Tech Stack:** Python 3.11, openpyxl 3.1.5, unittest из стандартной библиотеки, Excel `.xlsx`.

## Global Constraints

- Исходный файл `E:\ПТЕ\Контент\Книга3.xlsx` не изменять.
- Результат создавать новым файлом в той же папке.
- Применять консервативную нормализацию: сомнительные позиции не объединять.
- Основной язык каталога — украинский технический.
- При подтверждённом соответствии DIN использовать как основной стандарт; ГОСТ/ISO/EN переносить в аналоги.
- Значимые класс прочности, материал, покрытие, исполнение и характеристики подшипников сохранять.
- Для рекомендуемых болтов, гаек и шайб использовать часто применяемые размеры М4–М24, количество `0` и источник `Рекомендуемая`.
- Не изменять код приложения N.StCut: обработчик является отдельным инструментом подготовки данных.

---

### Task 1: Модель данных и базовая нормализация

**Files:**
- Create: `tools/content_center_catalog/models.py`
- Create: `tools/content_center_catalog/rules.py`
- Create: `tools/content_center_catalog/normalize.py`
- Create: `tools/content_center_catalog/tests/test_normalize.py`

**Interfaces:**
- Consumes: исходные значения пяти столбцов книги.
- Produces: `CatalogItem`, `normalize_text(value: str) -> str`, `parse_item(source_name: str, display_name: str, quantity: int | float, source_standard: str | None) -> CatalogItem`.

- [ ] **Step 1: Написать тесты терминологии и очистки**

Проверить преобразования `Винт`→`Гвинт`, `Подшипник`→`Підшипник`, удаление `( )`, `S16`, `-6g`, повторов размера и сохранение `8.8`, `10.9`, `HV`, `2RS`, `C3`.

- [ ] **Step 2: Запустить тесты и подтвердить падение**

Run: `python -m unittest discover -s tools/content_center_catalog/tests -p "test_*.py" -v`
Expected: FAIL из-за отсутствующих модулей и функций.

- [ ] **Step 3: Реализовать модель и минимальные правила**

`CatalogItem` должен содержать поля `section`, `group`, `name`, `size`, `strength_class`, `primary_standard`, `analogs`, `quantity`, `review_status`, `source_kind`, `note`, а также внутренние `source_row`, `source_name` и `dedupe_key` для аудита.

- [ ] **Step 4: Запустить тесты**

Run: `python -m unittest discover -s tools/content_center_catalog/tests -p "test_*.py" -v`
Expected: PASS.

- [ ] **Step 5: Зафиксировать изменения**

```bash
git add tools/content_center_catalog/models.py tools/content_center_catalog/rules.py tools/content_center_catalog/normalize.py tools/content_center_catalog/tests/test_normalize.py
git commit -m "feat: add catalog item normalization"
```

### Task 2: Классификация и разбор стандартов

**Files:**
- Modify: `tools/content_center_catalog/rules.py`
- Modify: `tools/content_center_catalog/normalize.py`
- Create: `tools/content_center_catalog/tests/test_classification.py`

**Interfaces:**
- Consumes: `CatalogItem` и очищенное исходное наименование.
- Produces: `classify_item(name: str) -> tuple[str, str]`, `resolve_standards(name: str, source_standard: str | None) -> tuple[str, tuple[str, ...], str]`.

- [ ] **Step 1: Написать тесты классификации**

Покрыть болт, гвинт, гайку, шайбу, шпильку, шплінт, заклепку, різьбову заклепку, підшипник, шпонку, стопорное/упорное кільце, втулку, манжету, сальник, маслянку, заглушку, герметичний ввід и віброопору.

- [ ] **Step 2: Написать тесты приоритета стандартов**

Проверить `ГОСТ 7805-70 / DIN 933`→основной `DIN 933`, аналог `ГОСТ 7805-70`; отсутствие подтверждённого DIN оставляет ГОСТ основным; неоднозначность устанавливает `Требует проверки`.

- [ ] **Step 3: Запустить тесты и подтвердить падение**

Run: `python -m unittest tools.content_center_catalog.tests.test_classification -v`
Expected: FAIL.

- [ ] **Step 4: Реализовать таблицы классификации и подтверждённых соответствий**

Использовать только соответствия, явно присутствующие в исходной книге либо подтверждённые официальными/авторитетными справочными данными. Ограничения взаимозаменяемости сохранять в примечании.

- [ ] **Step 5: Запустить все тесты**

Run: `python -m unittest discover -s tools/content_center_catalog/tests -p "test_*.py" -v`
Expected: PASS.

- [ ] **Step 6: Зафиксировать изменения**

```bash
git add tools/content_center_catalog
git commit -m "feat: classify catalog items and standards"
```

### Task 3: Подшипники и консервативное объединение дублей

**Files:**
- Create: `tools/content_center_catalog/deduplicate.py`
- Modify: `tools/content_center_catalog/normalize.py`
- Create: `tools/content_center_catalog/tests/test_deduplicate.py`

**Interfaces:**
- Consumes: `list[CatalogItem]`.
- Produces: `normalize_bearing(name: str) -> tuple[str, str]`, `deduplicate(items: list[CatalogItem]) -> tuple[list[CatalogItem], list[DuplicateAudit]]`.

- [ ] **Step 1: Написать тесты подшипников**

Проверить удаление производителей при сохранении обозначений `6205`, `6205-2RS`, `6205-ZZ`, `6205/C3` как разных позиций.

- [ ] **Step 2: Написать тесты дублей**

Проверить суммирование эквивалентных болтов с `S16` и `6g`, трёх форм записи шайбы DIN 125 и запрет объединения при различии класса прочности, покрытия или исполнения.

- [ ] **Step 3: Запустить тесты и подтвердить падение**

Run: `python -m unittest tools.content_center_catalog.tests.test_deduplicate -v`
Expected: FAIL.

- [ ] **Step 4: Реализовать ключи и аудит объединения**

Ключ должен включать тип, нормализованный размер, основной стандарт, класс прочности и все обнаруженные значимые характеристики. Неуверенные записи получают уникальный ключ исходной строки.

- [ ] **Step 5: Запустить все тесты**

Run: `python -m unittest discover -s tools/content_center_catalog/tests -p "test_*.py" -v`
Expected: PASS.

- [ ] **Step 6: Зафиксировать изменения**

```bash
git add tools/content_center_catalog
git commit -m "feat: add conservative catalog deduplication"
```

### Task 4: Рекомендуемые размеры и формирование книги

**Files:**
- Create: `tools/content_center_catalog/recommendations.py`
- Create: `tools/content_center_catalog/workbook.py`
- Create: `tools/content_center_catalog/main.py`
- Create: `tools/content_center_catalog/tests/test_workbook.py`

**Interfaces:**
- Consumes: путь исходной книги и нормализованные позиции.
- Produces: `recommended_items(existing: list[CatalogItem]) -> list[CatalogItem]`, `build_workbook(source_path: Path, output_path: Path) -> BuildSummary`.

- [ ] **Step 1: Написать тесты рекомендаций**

Проверить диапазон М4–М24, отсутствие полного декартова произведения длин, отсутствие уже имеющихся позиций, количество `0`, источник `Рекомендуемая` и DIN как основной стандарт.

- [ ] **Step 2: Написать тест структуры книги**

Проверить точные имена пяти листов, порядок 11 столбцов каталога, автофильтры, закрепление заголовка, числовой формат количества и наличие записей аудита.

- [ ] **Step 3: Запустить тесты и подтвердить падение**

Run: `python -m unittest tools.content_center_catalog.tests.test_workbook -v`
Expected: FAIL.

- [ ] **Step 4: Реализовать рекомендации**

Использовать распространённые диаметры `М4, М5, М6, М8, М10, М12, М14, М16, М18, М20, М24`; для болтов добавлять ограниченный практический ряд длин, пропорциональный диаметру, а гайки и шайбы — по одному типоразмеру на диаметр. Не добавлять позицию, если такой нормализованный ключ уже существует.

- [ ] **Step 5: Реализовать создание книги**

Копировать исходный лист значениями и оформлением; каталог сортировать по разделу, группе, типоразмеру и стандарту; добавить листы дублей, проверки и аналогов. Записывать сначала во временный файл и заменять только новый целевой файл после успешного сохранения.

- [ ] **Step 6: Запустить все тесты**

Run: `python -m unittest discover -s tools/content_center_catalog/tests -p "test_*.py" -v`
Expected: PASS.

- [ ] **Step 7: Зафиксировать изменения**

```bash
git add tools/content_center_catalog
git commit -m "feat: generate Content Center workbook"
```

### Task 5: Обработка исходной книги и контроль результата

**Files:**
- Read: `E:\ПТЕ\Контент\Книга3.xlsx`
- Create: `E:\ПТЕ\Контент\Книга3_Content_Center.xlsx`

**Interfaces:**
- Consumes: `build_workbook(Path(...), Path(...))`.
- Produces: итоговую книгу и сводку количества исходных, объединённых, рекомендуемых и требующих проверки строк.

- [ ] **Step 1: Запустить обработку**

Run: `python -m tools.content_center_catalog.main "E:\ПТЕ\Контент\Книга3.xlsx" "E:\ПТЕ\Контент\Книга3_Content_Center.xlsx"`
Expected: новый файл создан; исходный файл не изменён.

- [ ] **Step 2: Проверить контрольные суммы**

Сравнить SHA-256 исходного файла до и после обработки. Сумма `Количество` для строк `Использовалась` должна равняться исходной сумме числовых количеств; рекомендуемые строки не должны менять сумму.

- [ ] **Step 3: Проверить книгу повторным чтением**

Открыть результат через openpyxl, проверить пять листов, заголовки, отсутствие повреждённых формул/значений, количество строк и уникальность подтверждённых ключей.

- [ ] **Step 4: Выполнить выборочную предметную проверку**

Проверить примеры болтов ГОСТ 7798-70, шайб DIN 125, высокопрочных болтов DIN 6914, подшипников с суффиксами и минимум по три позиции каждого крупного раздела. Все сомнения должны находиться на листе `Требует проверки`.

- [ ] **Step 5: Сообщить итоговую сводку**

Указать путь результата, число исходных и итоговых позиций, количество объединённых групп дублей, рекомендуемых строк, строк ручной проверки и подтверждение неизменности исходного файла.
