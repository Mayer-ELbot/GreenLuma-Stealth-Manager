<p align="center">
  <img src="assets/banner.png" alt="GreenLuma Stealth Manager Banner" width="100%" />
</p>

# 🛡️ GreenLuma Stealth Manager (Open-Source Companion & GUI Hub)

<p align="center">
  <img src="https://img.shields.io/badge/Version-v1.6.1-10B981?style=for-the-badge&logo=git&logoColor=white" alt="Version 1.6.1" />
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="Windows" />
  <img src="https://img.shields.io/badge/Type-GUI%20%26%20Automation%20Helper-8B5CF6?style=for-the-badge" alt="Companion Tool" />
  <img src="https://img.shields.io/badge/Architecture-100%25%20Native%20WPF%20Desktop-06B6D4?style=for-the-badge" alt="Native WPF" />
  <img src="https://img.shields.io/badge/Languages-English%20%7C%20العربية-EC4899?style=for-the-badge" alt="Bilingual" />
  <img src="https://img.shields.io/badge/License-MIT-F59E0B?style=for-the-badge" alt="License" />
</p>

---

> [!IMPORTANT]
> ### ⚠️ Critical Clarification & Disclaimer / إخلاء مسؤولية وتوضيح هام
> 
> **English:**  
> **GreenLuma Stealth Manager is an independent, open-source companion utility and graphical user interface (GUI). It is NOT GreenLuma itself.**  
> - **Credits:** GreenLuma and its stealth hook payloads are created and maintained by their original authors (**Steam006** and the **CS.RIN.RU** community).
> - **Purpose:** This project does not contain, reverse-engineer, or claim ownership over GreenLuma. Instead, it serves as an **ergonomic desktop manager** that replaces tedious manual steps (creating `AppList` text files, converting AppIDs, running cache-wiping tools, copying DLLs back and forth, and manually cleaning directories) with a streamlined, modern 1-click dashboard.
> - **Educational Use:** This software is provided strictly for educational and personal library management purposes. It is not affiliated with, authorized, or endorsed by Valve Corporation or Steam.
> 
> ---
> 
> **العربية:**  
> **هذا البرنامج (GreenLuma Stealth Manager) هو أداة مساعدة وواجهة رسومية (GUI Manager) مفتوحة المصدر، وليس هو برنامج GreenLuma نفسه!**  
> - **الحقوق:** برنامج GreenLuma الأصلي وملفات الحقن الخاصة به من تطوير وبرمجة أصحابها الأصليين (**Steam006** ومجتمع **CS.RIN.RU**).
> - **هدف البرنامج:** تم تصميم هذه الأداة لتكون **مساعداً شاملاً ومريحاً** للمستخدم لتوفير عناء الخطوات اليدوية المعقدة (مثل كتابة أرقام الـ AppID في ملفات نصية، مسح كاش ستيم يدوياً، نسخ وتسمية ملفات الـ DLL، والبحث عن إضافات الألعاب)، حيث تجمع كل هذه العمليات في واجهة عصرية وسهلة بضغطة زر واحدة.
> - **إخلاء مسؤولية:** البرنامج مخصص للأغراض التعليمية والتنظيمية فقط، ولا يتبع بأي شكل من الأشكال لشركة Valve أو منصة Steam.

---

## 📑 Table of Contents / جدول المحتويات
- [📜 Release Changelog / سجل التحديثات](#-release-changelog--سجل-التحديثات)
- [🇬🇧 English Documentation](#-english-documentation)
  - [What This Tool Does](#-what-this-tool-does)
  - [Key Features](#-key-features)
  - [Quick Start Guide](#-quick-start-guide)
  - [How to Apply New GreenLuma Updates from CS.RIN.RU](#-how-to-apply-new-greenluma-updates-from-csrinru)
  - [How It Works (Under the Hood)](#-how-it-works-under-the-hood)
  - [Troubleshooting & FAQ](#-troubleshooting--faq)
- [🇸🇦 التوثيق باللغة العربية](#-التوثيق-باللغة-العربية)
  - [ما هي وظيفة هذه الأداة المساعدة؟](#-ما-هي-وظيفة-هذه-الأداة-المساعدة؟)
  - [المميزات الرئيسية](#-المميزات-الرئيسية)
  - [دليل التشغيل السريع](#-دليل-التشغيل-السريع)
  - [دليل تفصيلي: كيفية تطبيق وتحديث ملفات GreenLuma من منتدى CS.RIN.RU](#-دليل-تفصيلي-كيفية-تطبيق-وتحديث-ملفات-greenluma-من-منتدى-csrinru)
  - [كيف يعمل البرنامج تقنياً؟](#-كيف-يعمل-البرنامج-تقنياً؟)
  - [الأسئلة الشائعة وحلول المشاكل](#-الأسئلة-الشائعة-وحول-المشاكل)
- [🛠️ Building From Source](#-building-from-source)
- [⚖️ License](#-license)

---

## 📜 Release Changelog / سجل التحديثات

### 🚀 v1.6.1 (Official Release) - Steam Beta Self-Healing & Pattern 4 Protection
- **🛡️ Steam Beta Pre-Flight Interceptor**:
  - Automatically identifies whether Steam is enrolled in the Beta update channel (`package\beta`) before launching.
  - Halts the launch *prior* to DLL injection, completely preventing the notorious `User32: Failed to find pattern! (4)` crash.
- **⚡ 1-Click Auto-Revert to Official Stable**:
  - Automatically terminates lingering Steam instances, wipes injected `user32.dll` payloads, clears `package\beta`, and executes `steam.exe -clearbeta` to restore the official Steam stable release.
- **🩺 Steam Doctor Check #6**:
  - Real-time diagnostic monitor: `Steam Channel & Pattern Safety` with a direct `⚡ Revert Beta to Stable` action button.
- **⚙️ Settings & FAQ Integration**:
  - Added dedicated one-click repair tool in Settings modal and comprehensive troubleshooting entry in Quick Start Guide.

### 🎨 v1.6.0 - Zero-Collision Command Deck & Fluid GPU Animations
- **Zero-Collision Command Deck**: Ergonomic layout ensuring zero element overlaps across full HD, 2K, and 4K displays.
- **Dual Display Modes**: Seamless toggling between Full Dashboard and Compact Mini Tray Widget.
- **Fluid Micro-Interactions**: Easing animations, vector canvas donut gauges, and tactile button presses.
- **Smart AppList Priority**: Slot 90 base game ownership assignment with UTF-8 No-BOM enforcement.

### ⚡ v1.4.5 - Seamless 1-Click Smart Launch & Window Polish
- **Embedded Payload Engine**: Fully self-contained portable executable with zero external dependencies.
- **Bilingual Interface**: 1-Click instantaneous toggle between Arabic and English.

---

# 🇬🇧 English Documentation

## 💡 What This Tool Does

If you have ever used GreenLuma manually, you know the friction involved:
1. Looking up numeric Steam AppIDs on database sites.
2. Manually creating an `AppList` directory inside Steam.
3. Generating numbered text files (`0.txt`, `1.txt`...) or editing `AppList.ini` by hand.
4. Making sure text files don't have hidden UTF-8 BOM headers that corrupt the hook's parser.
5. Manually running cache cleaning tools (`DeleteSteamAppCache.exe`).
6. Copying stealth DLLs (`user32SF.dll` / `user32.dll`) into the Steam folder.
7. Remembering to manually delete these files after gaming so Steam returns to its clean, vanilla state.

**GreenLuma Stealth Manager automates every single one of these steps into a single, beautiful desktop application.**

---

## ✨ Key Features

- **🚀 1-Click Smart Launch**:
  - Closes any active Steam processes cleanly.
  - Automatically executes `DeleteSteamAppCache.exe` to wipe stale package caches.
  - Compiles your active library into a **clean UTF-8 (BOM-Free)** `AppList.ini` with base game Slot 90 priority.
  - Injects the stealth engine payload safely into the Steam directory.
  - Launches Steam natively without breaking modern Steam Families features.
- **🔍 Live Steam Store Search Engine**:
  - Type any game or DLC title (e.g. *Arc Raiders*, *Black Myth: Wukong*, *Elden Ring*) or enter a numeric AppID.
  - Instant autocomplete popup with official cover capsules, current price, and Steam Family Sharing status (`[ 👥 Family Share ]` or `[ 🚫 No Share ]`).
- **🧩 1-Click DLC Auto-Discovery**:
  - Base game cards include a **"+ Add DLCs"** button that automatically queries Steam's API and imports all official DLCs for that game into your library in seconds.
- **🛡️ Universal Stealth Engine**:
  - Uses the latest stealth architecture (`user32SF.dll` placed as `user32.dll`) providing full compatibility with modern Steam accounts and Steam Families.
  - Zero hardcoded dependency: drop new or custom GreenLuma DLLs into a `StealthMode/` folder or next to `GreenLumaManager.exe`, and the app prioritizes them automatically!
- **🛑 Automatic Stop & Clean Session**:
  - One click on **`🛑 Stop & Clean Files`** instantly terminates Steam and purges all injected artifacts (`user32.dll`, `user32SF.dll`, `AppList/`, `DeleteSteamAppCache.exe`), leaving your Steam installation 100% untouched.
- **🎨 Frameless Cyberpunk UI**:
  - High-performance, GPU-accelerated native WPF interface with a dark cyberpunk aesthetic.
  - Borderless window with smooth custom title bar controls (Minimize, Maximize, Close) and zero DWM title artifacts.
- **🌐 Dual-Language Support (English / العربية)**:
  - Full support for both English and Arabic with instant dynamic RTL (Right-to-Left) layout switching.
- **📦 Single Portable Executable**:
  - Completely self-contained (~500 KB). No installer, no background services, no Electron, and no Python runtime required.

---

## 🚀 Quick Start Guide

> [!WARNING]
> ### 📁 Mandatory Setup Rule: Create a Dedicated Folder!
> **Do NOT run `GreenLumaManager.exe` directly from your `Downloads` folder, from inside an unextracted zip/rar archive, or inside your `Steam` directory!**  
> 1. Create a dedicated folder (e.g. `C:\GreenLuma` or a folder on your Desktop).  
> 2. Place `GreenLumaManager.exe` inside it.  
> 3. Keep the `StealthMode/` folder beside `GreenLumaManager.exe`.

1. Download **`GreenLumaManager.exe`** from the [GitHub Releases](https://github.com/Mayer-ELbot/GreenLuma-Stealth-Manager/releases/latest) page.
2. Move it to your dedicated folder (e.g. `C:\GreenLuma\GreenLumaManager.exe`).
3. Run `GreenLumaManager.exe`. (The application will automatically detect your Steam installation directory).
4. Search for the games you wish to unlock using the search bar, or paste their AppIDs.
5. Click **`+ Add DLCs`** on any game card if you want all its expansions unlocked.
6. Click **`🚀 Launch Steam (GreenLuma)`**.
7. Steam will launch with your unlocked games in your library.
8. When you are done playing, open the Manager and click **`🛑 Stop & Clean Files`** (or close Steam) to restore your Steam folder to its completely original state.

---

## 🔄 How to Apply New GreenLuma Updates from CS.RIN.RU

When Valve pushes a major Steam client update, the original GreenLuma author (**Steam006**) publishes updated stealth files on the **CS.RIN.RU** community forums.  
**You do NOT need to wait for a new release of GreenLuma Stealth Manager or recompile anything!** The Manager is specifically engineered with hot-swappable payload detection.

### Step 1: Download the Update Archive
1. Visit the official GreenLuma thread on `cs.rin.ru`.
2. Download the latest release `.7z` or `.zip` archive (e.g. `GreenLuma 2026 Stealth Update`).
3. Extract the downloaded archive onto your computer.

### Step 2: Locate the Files
Inside the extracted folder, find the **`StealthMode`** subfolder. You will see:
- `user32SF.dll` ⭐ *(The modern universal stealth engine for Steam & Steam Families)*
- `user32.dll` *(Classic stealth hook)*
- `DeleteSteamAppCache.exe` *(Cache wiper)*

### Step 3: Place the Files beside `GreenLumaManager.exe`
Choose whichever placement structure you prefer:

#### Option A (Recommended & Cleanest — Subfolder):
Copy the entire `StealthMode` folder and place it directly in the same folder as `GreenLumaManager.exe`:
```text
📁 MyGreenLumaFolder/
├── 🚀 GreenLumaManager.exe
└── 📁 StealthMode/
    ├── 📄 user32SF.dll
    ├── 📄 user32.dll
    └── ⚙️ DeleteSteamAppCache.exe
```

#### Option B (Direct Placement):
Or copy the `.dll` and `.exe` files directly into the same folder as `GreenLumaManager.exe`:
```text
📁 MyGreenLumaFolder/
├── 🚀 GreenLumaManager.exe
├── 📄 user32SF.dll
├── 📄 user32.dll
└── ⚙️ DeleteSteamAppCache.exe
```

### Step 4: Verification in the App
Launch `GreenLumaManager.exe`. Look at the bottom-right status strip on the control deck:
- You will see: `🛡️ user32SF.dll (StealthMode)` or `🛡️ user32SF.dll (Local Override)`.
- This confirms that the Manager has detected and prioritized your new files over the internal embedded backup!
- Clicking **`🚀 Launch Steam (GreenLuma)`** will now automatically deploy and execute your new files into Steam seamlessly.

> [!TIP]
> **What about updating GreenLuma Stealth Manager itself?**  
> When new GUI features or bug fixes are published here on GitHub, the Manager will display a blue update banner at the top (or click **`🔄 Update`** in the header). It will automatically download, swap, and restart itself with one click.

---

## ⚙️ How It Works (Under the Hood)

| Step | Technical Implementation | Why It Matters |
| :--- | :--- | :--- |
| **BOM-Free Encoding** | Output uses `new UTF8Encoding(false)` | Standard .NET `Encoding.UTF8` writes a 3-byte Byte Order Mark (`0xEF, 0xBB, 0xBF`). GreenLuma's C++ INI parser fails on this BOM, causing games to show as unowned or "From family library". |
| **Slot 90 Priority** | Base game AppIDs are assigned to slot `90`, with DLCs following sequentially | Slot 90 is the primary account license override in modern Steam; prioritizing base games ensures the game is recognized as directly owned. |
| **AppCache Wipe** | Executes original `DeleteSteamAppCache.exe` with auto-dismiss | Clears cached `packageinfo.vdf` so Steam rebuilds package ownership fresh on boot. |
| **Native Launch** | Empty process arguments (no `-inhibitbootstrap`) | `-inhibitbootstrap` was required in legacy 2020 Steam, but breaks modern 2025/2026 Steam Families authentication. |

---

## ❓ Troubleshooting & FAQ

#### Q: The game appears with a "Purchase" button or says "From your Steam family library"?
> **Solution:** Ensure you use the primary **`🚀 Launch Steam (GreenLuma)`** button. The Manager ensures `AppList.ini` is written without UTF-8 BOM, puts the base game at Slot 90, and automatically clears the Steam appcache before launching.

#### Q: How do I update GreenLuma when a new version comes out?
> **Solution:** You don't need to wait for a new Manager release! Follow the [CS.RIN.RU Update Guide](#-how-to-apply-new-greenluma-updates-from-csrinru) above: drop the new `StealthMode/` folder or `user32SF.dll` next to `GreenLumaManager.exe`.

#### Q: Steam shows an error popup: "User32: Failed to find pattern! (4)"?
> **Solution:** This occurs when your Steam client is enrolled in the **Steam Beta** channel (`publicbeta`). Beta updates alter internal memory structures, breaking GreenLuma's signatures. Open **Steam Doctor** inside GreenLuma Manager and click **`⚡ Revert Beta to Stable`** (or launch via the app which prompts you to auto-revert). Steam will wipe the beta lock and download the official Stable release, permanently fixing the error.

---

# 🇸🇦 التوثيق باللغة العربية

## 💡 ما هي وظيفة هذه الأداة المساعدة؟

لكل من استخدم أداة GreenLuma بالطريقة اليدوية سابقاً، فإن المعاناة معروفة للجميع:
1. البحث عن رقم الـ AppID لكل لعبة ولكل إضافة يدوياً عبر المواقع.
2. إنشاء مجلد باسم `AppList` داخل مسار تثبيت ستيم.
3. إنشاء ملفات نصية مرقمة (`0.txt`, `1.txt`...) أو كتابة ملف `AppList.ini` سطراً بسطر.
4. الحذر الشديد من ترميز الملفات (UTF-8 BOM) الذي يجعل ستيم لا يتعرف على الألعاب أو يظهرها على أنها "مستعارة من العائلة".
5. تشغيل أداة مسح الكاش `DeleteSteamAppCache.exe` يدوياً في كل مرة.
6. نسخ وإعادة تسمية ملفات الـ DLL الخاصة بالتخفي داخل مجلد ستيم.
7. تذكر حذف هذه الملفات يدوياً بعد الانتهاء من اللعب ليعود ستيم نظيفاً تماماً.

**برنامج GreenLuma Stealth Manager يقوم بجميع هذه الخطوات بالكامل وبشكل آلي ومثالي وبضغطة زر واحدة فقط!**

---

## ✨ المميزات الرئيسية

- **🚀 زر التشغيل الذكي الموحد (1-Click Launch)**:
  - يقوم تلقائياً بإغلاق أي عملية نشطة لبرنامج ستيم.
  - يقوم بمسح كاش ستيم القديم تلقائياً عبر `DeleteSteamAppCache.exe`.
  - يكتب ملف `AppList.ini` بنظام ترميز نقي تماماً بدون BOM وبأولوية Slot 90 للألعاب الكاملة.
  - ينقل ملفات التخفي (`user32SF.dll` كـ `user32.dll`) لمجلد ستيم.
  - يفتح ستيم فوراً ببيئة نظيفة وجاهزة للعب مباشرة.
- **🔍 محرك بحث مباشر في متجر ستيم بالاسم**:
  - ابحث عن أي لعبة أو إضافة بالاسم المباشر (مثل: *Arc Raiders*, *Black Myth: Wukong*, *Elden Ring*) أو برقم الـ AppID.
  - تظهر قائمة اقتراحات فورية تحتوي على غلاف اللعبة الرسمي، السعر، وبادج يوضح هل تدعم اللعبة المشاركة العائلية أم لا (`[ 👥 عائلي ]` أو `[ 🚫 بدون عائلي ]`).
- **🧩 جلب جميع إضافات اللعبة بضغطة زر واحدة**:
  - يحتوي كل كارت لعبة على زر **"+ إضافة الـ DLCs"** يقوم بالاتصال بستيم وإضافة جميع إضافات اللعبة الرسمية لقائمتك في ثوانٍ.
- **🛡️ محرك تخفي موحد ومتوافق مع التحديثات**:
  - يعتمد محرك التخفي الأحدث لستيم (`user32SF.dll`) المتوافق مع نظام العائلات وجميع الألعاب.
  - إمكانية التحديث الذاتي: عند صدور أي تحديث لملفات GreenLuma، يكفي وضع الـ DLL الجديد في مجلد `StealthMode/` أو بجانب البرنامج وسيتعرف عليه تلقائياً.
- **🛑 إنهاء الجلسة والتنظيف الفوري**:
  - بضغطة زر واحدة على **`🛑 إيقاف وحذف الملفات`**، يتم إغلاق ستيم وحذف أي ملفات حقن فوراً من مجلد ستيم ليبقى جهازك وحسابك نظيفاً 100%.
- **🎨 واجهة احترافية داكنة خالية من الحواف**:
  - واجهة WPF سريعة للغاية تعمل بتسريع كارت الشاشة (GPU Acceleration).
  - بدون إطار ويندوز الأبيض العلوي المزعج، مع شريط أدوات تحكم خاص (تصغير، تكبير، إغلاق).
- **🌐 دعم كامل للغتين (العربية والإنجليزية)**:
  - واجهة كاملة باللغة العربية مع محاذاة من اليمين لليسار (RTL) مع إمكانية التبديل للإنجليزية فوراً.
- **📦 ملف تنفيذي محمول واحد**:
  - حجم البرنامج حوالي 500 كيلوبايت فقط، لا يحتاج لأي تثبيت أو برامج مساعدة (لا يحتاج متصفح أو بايثون).

---

## 🚀 دليل التشغيل السريع

> [!WARNING]
> ### 📁 القاعدة الإلزامية: إنشاء مجلد مستقل ومخصص للبرنامج أولاً!
> **إياك وتشغيل `GreenLumaManager.exe` من مجلد التنزيلات (Downloads) مباشرة، أو من داخل ملف مضغوط دون فك الضغط، أو بداخل مجلد Steam الرئيسي!**  
> 1. أنشئ مجلداً مستقلاً على جهازك (مثال: `C:\GreenLuma` أو مجلد على سطح المكتب).  
> 2. انقل ملف `GreenLumaManager.exe` داخله.  
> 3. ضع مجلد `StealthMode/` بجانب `GreenLumaManager.exe` في نفس المجلد.

1. حمّل أحدث نسخة من ملف **`GreenLumaManager.exe`** من صفحة الـ [Releases](https://github.com/Mayer-ELbot/GreenLuma-Stealth-Manager/releases/latest).
2. انقل الملف إلى مجلده المخصص (مثال: `C:\GreenLuma\GreenLumaManager.exe`).
3. افتح ملف `GreenLumaManager.exe` (سيتعرف البرنامج تلقائياً على مسار ستيم في جهازك).
4. ابحث عن الألعاب التي تريد فتحها في شريط البحث أو اكتب رقم الـ AppID.
5. اضغط على **"+ إضافة الـ DLCs"** لأي لعبة تريد فتح إضافاتها.
6. اضغط على الزر الأخضر الرئيسي: **`🚀 تشغيل ستيم مع GreenLuma`**.
7. سيفتح ستيم وستجد الألعاب في مكتبتك وجاهزة للتحميل واللعب.
8. بعد الانتهاء من اللعب، اضغط **`🛑 إيقاف وحذف الملفات`** في البرنامج ليعود ستيم أصلياً ونظيفاً تماماً.

---

## 🔄 دليل تفصيلي: كيفية تطبيق وتحديث ملفات GreenLuma من منتدى CS.RIN.RU

عندما تقوم شركة Valve بإصدار تحديث رئيسي لبرنامج Steam، يقوم المطور الأصلي لـ GreenLuma (**Steam006**) برفع ملفات تخفي محدثة على **منتدى CS.RIN.RU**.  
**الميزة الكبرى في هذا البرنامج (GreenLuma Stealth Manager) أنك لست بحاجة لانتظار أي تحديث للبرنامج نفسه لكي تستفيد من ملفات التخفي الجديدة!** فالبرنامج مصمم بذكاء ليتعرف فوراً على الملفات الأحدث الخارجية ويفضلها على الملفات المدمجة.

### الخطوة 1: تنزيل الملفات من منتدى CS.RIN.RU
1. ادخل إلى الموضوع الرسمي لـ GreenLuma على منتدى `cs.rin.ru`.
2. حمّل أحدث ملف مضغوط تم نشره (مثلاً: `GreenLuma 2026 Stealth Update.zip` أو `.7z`).
3. فك الضغط عن الملف في أي مكان على جهازك.

### الخطوة 2: تحديد الملفات المطلوبة
داخل المجلد المستخرج، ستجد مجلدات متعددة؛ ما يهمنا منها فقط هو مجلد **`StealthMode`**. بداخله ستجد الملفات التالية:
- **`user32SF.dll`** ⭐ *(المحرك الأساسي الأحدث لستيم ونظام عائلات ستيم)*
- **`user32.dll`** *(محرك التخفي الكلاسيكي البديل)*
- **`DeleteSteamAppCache.exe`** *(أداة مسح كاش الحزم)*

### الخطوة 3: وضع الملفات بجانب البرنامج
اختر إحدى الطريقتين التاليتين:

#### الطريقة الأولى (الأفضل والأنظف - مجلد StealthMode):
انسخ مجلد `StealthMode` المستخرج بالكامل، وضعه بجانب ملف `GreenLumaManager.exe` مباشرة:
```text
📁 مجلد GreenLuma الخاص بك/
├── 🚀 GreenLumaManager.exe
└── 📁 StealthMode/
    ├── 📄 user32SF.dll
    ├── 📄 user32.dll
    └── ⚙️ DeleteSteamAppCache.exe
```

#### الطريقة الثانية (وضع الملفات مباشرة بجانب البرنامج):
انسخ ملفات `user32SF.dll` و `user32.dll` و `DeleteSteamAppCache.exe` وضعها مباشرة في نفس المجلد الذي يحتوي على `GreenLumaManager.exe`:
```text
📁 مجلد GreenLuma الخاص بك/
├── 🚀 GreenLumaManager.exe
├── 📄 user32SF.dll
├── 📄 user32.dll
└── ⚙️ DeleteSteamAppCache.exe
```

### الخطوة 4: التأكد من قراءة البرنامج للتحديث
افتح برنامج `GreenLumaManager.exe`، وانظر إلى الشريط السفلي جهة اليمين:
- ستلاحظ ظهور عبارة: `🛡️ user32SF.dll (StealthMode)` أو `🛡️ user32SF.dll (Local Override)`.
- هذا يؤكد بنسبة 100% أن البرنامج تعرف تلقائياً على الملفات الخارجية الجديدة وقام بتفضيلها وتفعيلها بدلاً من الملفات المدمجة الاحتياطية!
- الآن فقط اضغط على **`🚀 تشغيل ستيم مع GreenLuma`** وسيقوم البرنامج بتجهيز وحقن الملفات الجديدة تلقائياً في ستيم وتشغيله فوراً.

> [!TIP]
> **ماذا عن تحديث برنامج GreenLuma Stealth Manager نفسه؟**  
> عند صدور أي ميزة جديدة أو تحسين لواجهة البرنامج على GitHub، سيظهر لك شريط أزرق أعلى البرنامج أو يمكنك الضغط على **`🔄 التحديثات`** وسيقوم البرنامج بتحديث نفسه تلقائياً دون أي خطوات يدوية.

---

## ⚙️ كيف يعمل البرنامج تقنياً؟

| الخاصية | التنفيذ البرمجي | الفائدة والأهمية |
| :--- | :--- | :--- |
| **منع ترميز BOM** | استخدام `new UTF8Encoding(false)` | كود C++ في GreenLuma يفشل في قراءة ملف `AppList.ini` إذا وُجدت حروف البداية المخفية لـ BOM (`EF BB BF`)، مما كان يسبب ظهور الألعاب كأنها مستعارة أو غير مملوكة. تم حل المشكلة نهائياً. |
| **أولوية Slot 90** | وضع معرف اللعبة الأساسية في الخانة 90 | في نظام ستيم الحديث، الخانة 90 هي الأساسية لتراخيص الحساب، ووضع اللعبة فيها أولاً يضمن التعرف عليها كلعبة مملوكة مباشرة. |
| **مسح الكاش الآلي** | تشغيل `DeleteSteamAppCache.exe` مع إغلاقه التلقائي | مسح ملف `packageinfo.vdf` القديم ليقوم ستيم بإعادة بنائه بالتراخيص الجديدة فور فتحه. |
| **إقلاع ستيم النظيف** | إطلاق ستيم مباشرة بدون `-inhibitbootstrap` | المعامل `-inhibitbootstrap` القديم أصبح يسبب مشاكل في نظام عائلات ستيم لعام 2025/2026، والتشغيل النظيف يضمن استقرار الاتصال. |

---

## ❓ الأسئلة الشائعة وحلول المشاكل

#### س: اللعبة تظهر في ستيم لكن مكتوب "شراء" (Purchase) أو "من مكتبة العائلة"؟
> **الحل:** استخدم الزر الرئيسي **`🚀 تشغيل ستيم مع GreenLuma`** حصراً؛ حيث يقوم البرنامج آلياً بمسح الكاش، وترتيب ملف `AppList.ini` بنظام خالٍ من BOM وبأولوية Slot 90 للعبة الأساسية، مما يضمن ظهورها كأنها مشتراة بحسابك مباشرة.

#### س: كيف أقوم بتحديث ملفات GreenLuma مستقبلاً عند صدور نسخة جديدة؟
> **الحل:** اتبع [دليل تحديثات CS.RIN.RU](#-دليل-تفصيلي-كيفية-تطبيق-وتحديث-ملفات-greenluma-من-منتدى-csrinru) الموضح بالأعلى: ضع مجلد `StealthMode/` أو ملف `user32SF.dll` بجانب ملف `GreenLumaManager.exe`.

#### س: هل البرنامج آمن على حساب ستيم؟
> **الحل:** نعم، فالبرنامج لا يقوم بأي تعديل دائم على ملفات ستيم، وملفات التخفي المؤقتة يتم حذفها فوراً عند الضغط على زر الإيقاف أو الخروج، ليعود مجلد ستيم أصلياً وخالياً من أي أثر.

#### س: تظهر لي نافذة خطأ صغيرة بعنوان "User32" ومكتوب داخلها "Failed to find pattern! (4)"؟
> **الحل:** هذا الخطأ يحدث عندما يكون ستيم مشتركاً في تحديثات البيتا (Steam Beta)؛ حيث تغير فالف بصمات الذاكرة فيفشل الحقن. الحل بضغطة زر واحدة: افتح **طبيب ستيم (Steam Doctor)** داخل البرنامج واضغط **`⚡ الخروج من بيتا ستيم (Revert to Stable)`** أو اضغط زر Launch وسيطلب منك البرنامج الرجوع للمستقر تلقائياً، وسيعمل كل شيء فوراً.

---

## 🛠️ Building From Source

You don't need Visual Studio installed. Any standard Windows machine can build the standalone executable in seconds using the included build script:

```cmd
git clone https://github.com/Mayer-ELbot/GreenLuma-Stealth-Manager.git
cd GreenLuma-Stealth-Manager
Build_EXE.bat
```

The resulting `GreenLumaManager.exe` will be generated in the root directory.

---

## ⚖️ License

Distributed under the **MIT License**. See [LICENSE](LICENSE) for more information.  
*All trademarks, logos, and brand names are the property of their respective owners.*
