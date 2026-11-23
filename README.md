# Software Design Document (SDD) — Графічний каталог персонажів (v1.0.0)

**Навчальний курс:** Об'єктно-орієнтоване програмування (Практичні заняття 13–18)  
**Тема проєкту:** Тема 1 — Графічний каталог персонажів (Rick & Morty API)  
**Версія документа:** 1.0.0 (Фінальний реліз)  
**Технологічний стек:** C# 12, .NET 8.0 (`net8.0` / `net8.0-windows`), WPF, System.Text.Json, HttpClient  

---

## 1. Вступ (Introduction)

### 1.1. Призначення системи
**«Графічний каталог персонажів»** — це модульний настільний застосунок для Windows із підтримкою як графічного (WPF), так і командного (CLI) інтерфейсів. Система дозволяє завантажувати дані про персонажів із публічного REST API (`https://rickandmortyapi.com/api/character`), виконувати їх аналітичну обробку й ранжування, створювати власних персонажів, кешувати портрети на локальному диску для зменшення навантаження на мережу та переглядати картки у зручній візуальній вітрині.

### 1.2. Область застосування та цілі фіналізації (ПР 18)
У межах фінального етапу розробки (Практичне заняття 18) виконано:
1. Узгодження всіх чотирьох підсистем (CLI, бізнес-логіка, постійність даних, нативний GUI) через єдині контракти та моделі представлення (`CharacterViewModel`).
2. Додавання автоматизованого модуля верифікації (`OopLab.Verification`), який перевіряє парсинг JSON, математику рейтингу, збереження/відновлення стану з диска та файлове кешування зображень.
3. Оформлення повного дизайн-документа відповідно до інженерних стандартів проєктування ПЗ.
4. Налаштування відтворюваної `Release`-збірки (`win-x64`) у вигляді автономного пакета з виконуваними файлами (`.exe` та `.dll`).

---

## 2. Загальна архітектура системи (System Architecture)

Застосунок побудований за **тришаровою модульною архітектурою** (Layered Architecture) із дотриманням принципу інверсії залежностей (Dependency Inversion Principle): шари представлення (`OopLab.Gui`, `OopLab.Cli`) та шар доступу до даних (`OopLab.Data`) залежать від абстракцій і моделей ядра (`OopLab.Core`), а не один від одного.

```
┌──────────────────────────────────┐    ┌──────────────────────────────────┐
│   ШАР ПРЕДСТАВЛЕННЯ (GUI)        │    │   ШАР ПРЕДСТАВЛЕННЯ (CLI)        │
│   OopLab.Gui.exe (WPF)           │    │   OopLab.Cli.exe (Console)       │
│  • MainWindow (вітрина карток)   │    │  • CliFacade (єдина точка входу) │
│  • CharacterDetailWindow         │    │  • ICommandStrategy (3 стратегії)│
│  • LocalImageSourceConverter     │    │  • ConsoleCharacterRenderer      │
└────────────────┬─────────────────┘    └────────────────┬─────────────────┘
                 │                                       │
                 └───────────────────┬───────────────────┘
                                     │ споживають CharacterViewModel
                                     ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                 ШАР БІЗНЕС-ЛОГІКИ — OopLab.Core.dll                      │
│  • Доменна модель: CharacterProfile (реалізує IRenderable<T>)            │
│  • Модель представлення: CharacterViewModel                              │
│  • Мапер і математика: CharacterViewModelMapper                          │
│  • Парсер зовнішнього API: RickAndMortyApiJsonParser                     │
│  • Специфікація запитів: CharacterFilterCriteria                         │
│  • Прикладний сервіс: CharacterCatalogService                            │
│  • Інтерфейси: ICharacterRepository, IRemoteCharacterProvider,           │
│                IImageCacheStorage, IRenderer<T>                          │
└────────────────────────────────────┬─────────────────────────────────────┘
                                     ▲ реалізує інтерфейси Core
                                     │
┌────────────────────────────────────┴─────────────────────────────────────┐
│           ШАР ПОСТІЙНОСТІ ТА БУФЕРИЗАЦІЇ — OopLab.Data.dll               │
│  • AppStoragePaths (%LocalAppData%\OopLab.CharacterCatalog\)             │
│  • JsonCharacterRepository (catalog_state.json)                          │
│  • DiskImageCacheStorage (portraits_cache\<id>.jpeg)                     │
│  • RickAndMortyApiClient (HttpClient + резервний JSON-fallback)          │
└──────────────────────────────────────────────────────────────────────────┘
```

### 2.1. Використані патерни проєктування
* **Repository (`ICharacterRepository` / `JsonCharacterRepository`):** ізолює бізнес-логіку від способу серіалізації та зберігання даних на диску.
* **Strategy (`ICommandStrategy`):** групує команди консольного інтерфейсу за призначенням (`InformationCommandStrategy`, `TransformativeCommandStrategy`, `InfrastructureCommandStrategy`), дозволяючи розширювати CLI без зміни ядра.
* **Facade (`CliFacade` та `CharacterCatalogService`):** надає спрощений уніфікований API для виконання операцій каталогу.
* **Specification (`CharacterFilterCriteria`):** інкапсулює правила комбінованої фільтрації та сортування персонажів.
* **Adapter / Mapper (`CharacterViewModelMapper`):** трансформує доменні сутності `CharacterProfile` у готові до відображення `CharacterViewModel`.

---

## 3. Детальний опис підсистем і бізнес-логіки

### 3.1. Парсинг зовнішнього API (`RickAndMortyApiJsonParser`)
Зовнішнє джерело даних `https://rickandmortyapi.com/api/character` повертає JSON-об'єкт із масивом `results`. Парсер виконує десеріалізацію за допомогою `System.Text.Json` у внутрішні DTO-класи (`RmPageResponseDto`, `RmCharacterDto`) та перетворює їх на доменну модель `CharacterProfile`:
* підраховує кількість появ персонажа у серіях (`EpisodeCount = dto.Episode.Count`);
* визначає рівень рідкості `Rarity` (від `1` до `5` зірок) залежно від кількості епізодів (`>= 40` → 5★, `>= 15` → 4★, `>= 5` → 3★, `>= 2` → 2★, інакше 1★);
* формує опис, світ походження (`OriginWorld`), поточну локацію (`CurrentLocation`) та URL портрета (`AvatarUrl`).

### 3.2. Математика характеристик та рейтингу (`CharacterViewModelMapper`)
Для кожного персонажа обчислюються ігрові/каталожні показники:
1. **Базове здоров'я (`BaseHp`) та атака (`BaseAtk`):**
   $$\text{BaseHp} = 9500 + \text{Rarity} \times 700 + \text{EpisodeCount} \times 25 + B_{\text{species}}$$
   $$\text{BaseAtk} = 200 + \text{Rarity} \times 30 + \text{EpisodeCount} \times 2 + A_{\text{species}}$$
   де для видів `Robot` та `Alien` додається бонус $B_{\text{species}} = 800$, $A_{\text{species}} = 25$.
2. **Інтегральний рейтинг популярності (`PopularityScore`):**
   $$\text{PopularityScore} = \text{round}\Big((\text{EpisodeCount} \times 14 + \text{BaseHp} \times 0.02 + \text{BaseAtk} \times 0.6) \times K_{\text{status}}\Big)$$
   де $K_{\text{status}} = 1.1$ для живих персонажів (`Alive`) і $1.0$ для решти.
3. **Ранг (`TierRank`):**
   * `S+` при $\text{PopularityScore} \ge 900$
   * `S` при $750 \le \text{PopularityScore} < 900$
   * `A` при $550 \le \text{PopularityScore} < 750$
   * `B` при $\text{PopularityScore} < 550$

### 3.3. Постійність даних та система буферизації портретів (`OopLab.Data`)
Усі файли застосунку зберігаються у стандартному каталозі профілю користувача Windows `%LocalAppData%\OopLab.CharacterCatalog\`:
* **`catalog_state.json`** — серіалізований стан усіх синхронізованих та створених користувачем персонажів. При повторному запуску дані миттєво відновлюються з диска. Користувацькі персонажі (`IsCustom = true`) захищені від перезапису під час синхронізації з API.
* **`portraits_cache\<id>.jpeg`** — файловий кеш портретів, реалізований у `DiskImageCacheStorage`. Перед виконанням HTTP-запиту система перевіряє наявність локального файлу на диску: якщо портрет вже закешований, він завантажується миттєво без звернення до сервера, що мінімізує мережевий трафік і дозволяє переглядати картки офлайн.

### 3.4. Командний інтерфейс (`OopLab.Cli`)
Підтримує інтерактивний режим (`catalog> `) та запуск окремих команд із параметрами:
* **Інформаційні:** `list [--status=...] [--species=...] [--cached] [--sort=power|episodes|name]`, `info <ID|ім'я>`, `help`.
* **Трансформативні:** `create` (покроковий інтерактивний діалог створення нового персонажа з валідацією вводу, автоматичним кешуванням портрета та збереженням у JSON).
* **Інфраструктурні:** `sync [--limit=20] [--no-images]`, `import <ID|ім'я>`, `cache <ID|ім'я> | --all [--force]`, `cache-info`, `cache-clear`.

### 3.5. Нативний графічний інтерфейс (`OopLab.Gui`)
* **Головне вікно (`MainWindow.xaml`):** побудоване на основі `DockPanel`, панелі інструментів, фільтрів у реальному часі (пошук за назвою/світом, фільтр за статусом, сортування, чекбокс «Тільки з локальним фото в кеші») та прокручуваної вітрини карток (`ScrollViewer` + `ItemsControl` + `WrapPanel`).
* **Конвертер зображень (`LocalImageSourceConverter`):** завантажує портрет із локального кешу (`BitmapCacheOption.OnLoad` без блокування файлу на диску) або за URL, якщо файл ще не закешований.
* **Вікно деталей (`CharacterDetailWindow.xaml`):** відкривається при натисканні кнопки «Детальніше» на картці та відображає повну біографію, характеристики, локальний шлях до закешованого портрета і статус кешування.

---

## 4. Розгортання та верифікація релізу (Deployment & Verification)

### 4.1. Структура релізного пакета (`OopLab.CharacterCatalog-v1.0-win-x64.zip`)
```
OopLab.CharacterCatalog-v1.0-win-x64/
 ├── OopLab.Gui.exe               # Виконуваний файл графічного інтерфейсу (WPF)
 ├── OopLab.Gui.dll               # Збірка шару представлення GUI
 ├── OopLab.Core.dll              # Бібліотека бізнес-логіки
 ├── OopLab.Data.dll              # Бібліотека постійності даних та кешу
 ├── OopLab.Gui.deps.json         # Маніфест залежностей .NET
 ├── OopLab.Gui.runtimeconfig.json# Конфігурація середовища виконання (.NET 8.0)
 ├── DESIGN_DOCUMENT.md           # Повний дизайн-документ проєкту
 ├── README.md                    # Документація репозиторію
 ├── ІНСТРУКЦІЯ_ЗАПУСКУ.txt       # Стисла пам'ятка для користувача
 └── cli/
      ├── OopLab.Cli.exe          # Консольний виконуваний файл (CLI)
      ├── OopLab.Cli.dll          # Збірка консольного інтерфейсу
      ├── OopLab.Core.dll         # Спільна бібліотека бізнес-логіки
      └── OopLab.Data.dll         # Спільна бібліотека постійності даних
```

### 4.2. Системні вимоги
* **ОС:** Windows 10 (1809+) або Windows 11 (`x64`).
* **Середовище виконання:** [.NET 8.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0).
* **Для збірки з вихідного коду:** .NET 8.0 SDK.
