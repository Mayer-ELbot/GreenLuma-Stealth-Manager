<p align="center">
  <img src="assets/banner.png" alt="GreenLuma 2026 Stealth Pro Hub Banner" width="100%" />
</p>

# 🛡️ GreenLuma 2026 - Stealth Pro Hub

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="Windows" />
  <img src="https://img.shields.io/badge/Architecture-100%25%20Native%20WPF%20Desktop-10B981?style=for-the-badge" alt="Native WPF" />
  <img src="https://img.shields.io/badge/Packaging-Single%20Portable%20EXE-06B6D4?style=for-the-badge" alt="Single EXE" />
  <img src="https://img.shields.io/badge/Languages-English%20%7C%20العربية-A855F7?style=for-the-badge" alt="Bilingual" />
  <img src="https://img.shields.io/badge/License-MIT-F59E0B?style=for-the-badge" alt="License" />
</p>

---

## 🌍 Language / اللغة
- [English Documentation](#-english-overview)
- [التوثيق باللغة العربية](#-دليل-الاستخدام-بالعربية)

---

# 🇬🇧 English Overview

**GreenLuma 2026 Stealth Pro Hub** is a 100% **Native Windows Desktop Application** (built with C# and WPF) designed to manage, unlock, and automate your GreenLuma stealth gaming sessions with zero external dependencies.

It features a custom dark cyberpunk frameless UI, live Steam store title search with autocomplete, real-time family sharing badges, and automatic session cleanup.

### 🌟 Key Highlights
- **100% Native Desktop App**: No web browsers, no HTML/Electron, no Python runtime. Runs directly as a native Windows desktop GUI with GPU hardware acceleration.
- **Single Portable Executable (`GreenLumaManager.exe`)**: All stealth materials (`user32.dll`, `user32SF.dll`, `DeleteSteamAppCache.exe`, and app graphics) are embedded directly inside the single EXE (~505 KB). Send this single file to friends and it runs out-of-the-box anywhere.
- **⚡ Smart Drop-in Stealth Updates**:
  - Whenever new GreenLuma stealth files are released, drop the new DLLs next to `GreenLumaManager.exe` or in a `StealthMode/` folder.
  - The application automatically detects and prioritizes the external files over the embedded ones!
- **🔍 Live Steam Store Search Engine**:
  - Search any game or DLC by name (e.g. *Arc Raiders*, *Black Myth: Wukong*, *Elden Ring*) or enter a numeric AppID.
  - Floating autocomplete suggestion popup shows title, cover capsule art, AppID, price, and instant Family Sharing support badges (`[ 👥 Family Share ]` or `[ 🚫 No Share ]`).
- **🏷️ Strict Game vs DLC Representation & 1-Click Toggle**:
  - Distinct badges: **`[ 🎮 GAME ]`** (Emerald) for base games and **`[ 🧩 DLC ]`** (Amber) for downloadable content.
  - Base games include a 1-click **"+ Add DLCs"** button that automatically pulls official Steam DLCs into your library.
  - **1-Click Badge Toggle**: Click the type badge on any card to instantly toggle between Game and DLC.
- **⚙️ Preferences & Settings Dialog**:
  - Customize Steam installation path, default stealth mode (Standard vs Steam Families), launch arguments (`-inhibitbootstrap`), and auto-cache cleaning.
- **🎨 Custom Frameless Cyberpunk Interface**:
  - Custom dark title bar with integrated Minimize, Maximize, and Close buttons.
  - Aggressive gaming physics: scale-up on hover, animated neon drop-shadow pulse on the launch button, and cascading waterfall card entrance animations.
- **🌐 Dual-Language Support (English First)**:
  - English default with instant toggle to **العربية (Arabic)** and full Right-to-Left (RTL) layout.
- **🛡️ 100% Clean Stealth Session Management**:
  - Unlock files are copied to the Steam directory only during an active session.
  - Monitors the Steam process and automatically wipes all injected files the moment you exit Steam.

---

### 🚀 Quick Start

1. Download **`GreenLumaManager.exe`** from the [Releases](https://github.com/marouanes/GreenLuma-Stealth-Manager/releases) tab.
2. Double-click **`GreenLumaManager.exe`**.
3. Search for your games using the search bar or enter AppIDs.
4. Select your mode:
   - **Standard Stealth (`user32.dll`)** - Recommended for standard Steam accounts.
   - **Steam Families (`user32SF.dll`)** - For accounts participating in Steam Family Sharing.
5. Click **⚡ LAUNCH GREENLUMA STEAM**.
6. When finished playing, simply close Steam. All injected files will be purged automatically.

---

### 🛠️ Building From Source

No Visual Studio installation is required. Any Windows machine with .NET Framework can compile the project in seconds using the included batch file:

1. Clone the repository:
   ```cmd
   git clone https://github.com/marouanes/GreenLuma-Stealth-Manager.git
   cd GreenLuma-Stealth-Manager
   ```
2. Run `Build_EXE.bat`:
   ```cmd
   Build_EXE.bat
   ```
3. The standalone `GreenLumaManager.exe` with embedded payloads will be built in the project root.

---

# 🇸🇦 دليل الاستخدام بالعربية

**GreenLuma 2026 Stealth Pro Hub** هو تطبيق **ويندوز مكتبي أصلي 100%** (مبني بلغة C# وتقنية WPF)، مصمم لتنظيم جلسات اللعب وحقن الألعاب بوضع التخفي مع التنظيف التلقائي بدون أي متصفح أو متطلبات تشغيل خارجية.

يتميز بواجهة ألعاب احترافية داكنة بدون إطار ويندوز التقليدي، ومحرك بحث فوري في متجر ستيم بالاسم، وبيان دعم المشاركة العائلية (Family Sharing)، وتنظيف تلقائي شامل للملفات.

### 🌟 أبرز المميزات
- **برنامج مكتبي أصلي (Native App)**: ليس متصفحاً ولا صفحة ويب، بل تطبيق ويندوز مكتبي فائق السرعة مع دعم تسريع الرسوميات (GPU Acceleration).
- **ملف تنفيذي محمول واحد (`GreenLumaManager.exe`)**: جميع ملفات التخفي الأساسية (`user32.dll`, `user32SF.dll`, `DeleteSteamAppCache.exe`) مدمجة داخل الملف (~505 KB). يمكنك إرسال هذا الملف بمفرده لأصدقائك وسيعمل معهم فوراً.
- **⚡ قراءة تحديثات التخفي الجديدة تلقائياً**:
  - عند صدور أي تحديث لملفات التخفي، ضع الملفات الجديدة بجانب ملف `GreenLumaManager.exe` أو داخل مجلد `StealthMode/`.
  - يتعرف البرنامج تلقائياً على الملفات الجديدة ويفضلها على الملفات المدمجة.
- **🔍 بحث فوري في متجر ستيم بالاسم (Steam Store Search)**:
  - ابحث عن أي لعبة أو إضافة بالاسم (مثل: *Arc Raiders*, *Black Myth: Wukong*, *Elden Ring*) أو أدخل رقم الـ AppID.
  - قائمة مقترحات منبثقة فورية تعرض غلاف اللعبة، الاسم، نوع المحتوى، وحالة المشاركة العائلية (`[ 👥 عائلي ]` أو `[ 🚫 بدون عائلي ]`).
- **🏷️ تمييز دقيق بين اللعبة والـ DLC مع إمكانية التبديل بنقرة واحدة**:
  - شارات واضحة: **`[ 🎮 لعبة كاملة ]`** بالأخضر الزمردي، و **`[ 🧩 DLC إضافة ]`** بالعنبر الذهبي.
  - زر **"+ إضافة الـ DLCs"** للألعاب الكاملة لجلب كافة إضافات اللعبة الرسمية من ستيم بضغطة واحدة.
  - **تبديل سريع بنقرة واحدة**: انقر على شارة النوع في أي كارت لتبديله فوراً بين لعبة وإضافة وحفظ التغيير.
- **⚙️ نافذة الإعدادات والتفضيلات المتقدمة**:
  - تحديد مسار Steam يدوياً عبر زر الاستعراض، اختيار وضع التشغيل الافتراضي، معلمات الإقلاع (`-inhibitbootstrap`)، وخيار مسح الكاش التلقائي.
- **🎨 واجهة ألعاب خالية من الإطار (Frameless Cyberpunk UI)**:
  - شريط علوي داكن مدمج مع أزرار التحكم الخاصة (تصغير، تكبير، إغلاق).
  - فيزياء وأنيميشنز ألعاب: تكبير الكروت عند المرور، نبض نيون لمفاعل زر الإطلاق، وتأثير الشلال المتتابع عند ظهور الكروت.
- **🌐 واجهة ثنائية اللغة (الإنجليزية أولاً مع دعم العربية الكامل)**:
  - تحويل فوري إلى **العربية** مع ضبط اتجاه الواجهة بالكامل (RTL).
- **🛡️ وضع التخفي والتنظيف الآمن 100%**:
  - يتم نسخ ملفات الفتح لمجلد Steam أثناء اللعب فقط.
  - بمجرد إغلاق Steam، يحذف البرنامج كافة ملفات الحقن فوراً ليبقى حسابك نظيفاً 100%.

---

### 🚀 خطوات التشغيل السريعة

1. حمّل ملف **`GreenLumaManager.exe`** من صفحة الـ [Releases](https://github.com/marouanes/GreenLuma-Stealth-Manager/releases).
2. شغل ملف **`GreenLumaManager.exe`**.
3. ابحث عن الألعاب باستخدام شريط البحث أو أدخل أرقام الـ AppID.
4. اختر وضع التشغيل:
   - **الوضع العادي (`user32.dll`)** للحسابات العادية.
   - **وضع عائلات ستيم (`user32SF.dll`)** إذا كنت تستخدم ميزة Steam Families.
5. اضغط **⚡ تشغيل ستيم مع GREENLUMA**.
6. عند الانتهاء من اللعب، أغلق ستيم كالمعتاد ليتم مسح جميع الملفات تلقائياً.

---

## ⚖️ License / الترخيص
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
