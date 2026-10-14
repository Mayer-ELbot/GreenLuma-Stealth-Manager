// GreenLuma Modern Dashboard Client Logic with Full Customization

const state = {
    games: [],
    settings: {
        customSteamPath: '',
        mode: 'standard',
        cleanTiming: 'on_exit',
        clearAppCache: false,
        steamArgs: '-inhibitbootstrap',
        theme: 'emerald',
        viewMode: 'grid',
        lang: 'ar',
        autoKillSteam: true
    },
    status: {
        steamDir: null,
        steamRunning: false,
        injected: false,
        activeMode: 'standard',
        totalGames: 0,
        enabledGames: 0
    },
    searchDebounce: null,
    pollInterval: null
};

// DOM Elements
const elements = {
    // Header & Status
    steamStatusPill: document.getElementById('steamStatusPill'),
    steamStatusText: document.getElementById('steamStatusText'),
    steamPathText: document.getElementById('steamPathText'),
    injectionStatusText: document.getElementById('injectionStatusText'),
    steamArgsDisplay: document.getElementById('steamArgsDisplay'),
    gamesCountBadge: document.getElementById('gamesCountBadge'),
    libraryTotalBadge: document.getElementById('libraryTotalBadge'),
    btnRefreshStatus: document.getElementById('btnRefreshStatus'),
    btnCleanAll: document.getElementById('btnCleanAll'),
    btnOpenSettings: document.getElementById('btnOpenSettings'),

    // Launch & Controls
    modeSelect: document.getElementById('modeSelect'),
    cleanTimingSelect: document.getElementById('cleanTimingSelect'),
    checkAppCache: document.getElementById('checkAppCache'),
    btnLaunchSteam: document.getElementById('btnLaunchSteam'),
    btnStopSteam: document.getElementById('btnStopSteam'),

    // Add & Search
    gameInput: document.getElementById('gameInput'),
    btnAddGame: document.getElementById('btnAddGame'),
    searchDropdown: document.getElementById('searchDropdown'),
    btnOpenBulkModal: document.getElementById('btnOpenBulkModal'),
    btnViewAppListIni: document.getElementById('btnViewAppListIni'),

    // Library & Filter
    filterInput: document.getElementById('filterInput'),
    gamesGrid: document.getElementById('gamesGrid'),
    emptyState: document.getElementById('emptyState'),
    btnViewGrid: document.getElementById('btnViewGrid'),
    btnViewCompact: document.getElementById('btnViewCompact'),

    // Settings Modal
    settingsModal: document.getElementById('settingsModal'),
    btnCloseSettings: document.getElementById('btnCloseSettings'),
    btnCancelSettings: document.getElementById('btnCancelSettings'),
    btnSaveSettings: document.getElementById('btnSaveSettings'),
    detectedPathHint: document.getElementById('detectedPathHint'),
    settingCustomSteamPath: document.getElementById('settingCustomSteamPath'),
    settingSteamArgs: document.getElementById('settingSteamArgs'),
    settingCleanTiming: document.getElementById('settingCleanTiming'),
    settingAutoKillSteam: document.getElementById('settingAutoKillSteam'),
    settingLang: document.getElementById('settingLang'),
    bulkImportTextarea: document.getElementById('bulkImportTextarea'),
    btnSubmitBulk: document.getElementById('btnSubmitBulk'),
    btnCopyAppListIni: document.getElementById('btnCopyAppListIni'),
    btnClearAllGames: document.getElementById('btnClearAllGames'),

    // Raw Modal
    rawAppListModal: document.getElementById('rawAppListModal'),
    rawAppListContent: document.getElementById('rawAppListContent'),
    btnCloseRawModal: document.getElementById('btnCloseRawModal'),
    btnCloseRawModal2: document.getElementById('btnCloseRawModal2'),
    btnCopyRawIni: document.getElementById('btnCopyRawIni'),

    // Toast
    toastContainer: document.getElementById('toastContainer')
};

// --- Initialization ---
document.addEventListener('DOMContentLoaded', async () => {
    await fetchSettings();
    await fetchStatus();
    await fetchGames();
    setupEventListeners();

    // Poll status every 2.5 seconds
    state.pollInterval = setInterval(fetchStatus, 2500);
});

// --- API Calls ---
async function fetchSettings() {
    try {
        const res = await fetch('/api/settings');
        if (!res.ok) return;
        state.settings = await res.json();
        applySettingsToUI();
    } catch (err) {
        console.error('Failed to fetch settings:', err);
    }
}

async function saveSettings(updated) {
    try {
        const res = await fetch('/api/settings', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(updated)
        });
        if (res.ok) {
            const data = await res.json();
            state.settings = data.settings;
            applySettingsToUI();
            showToast('تم حفظ الإعدادات بنجاح!', 'success');
            fetchStatus();
        }
    } catch (err) {
        showToast('خطأ أثناء حفظ الإعدادات', 'error');
    }
}

async function fetchStatus() {
    try {
        const res = await fetch('/api/status');
        if (!res.ok) return;
        const data = await res.json();
        state.status = data;
        updateStatusUI();
    } catch (err) {
        console.error('Failed to fetch status:', err);
    }
}

async function fetchGames() {
    try {
        const res = await fetch('/api/games');
        if (!res.ok) return;
        state.games = await res.json();
        renderGames();
    } catch (err) {
        console.error('Failed to fetch games:', err);
    }
}

async function addGame(appId, customName = '') {
    if (!appId) return;
    try {
        showToast('جاري جلب تفاصيل اللعبة من ستيم...', 'info');
        const res = await fetch('/api/games/add', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ appId, customName })
        });
        const data = await res.json();
        if (!res.ok) {
            showToast(data.error || 'فشل إضافة اللعبة', 'error');
            return;
        }

        elements.gameInput.value = '';
        elements.searchDropdown.classList.add('hidden');
        showToast(`تمت إضافة "${data.game.name}" بنجاح!`, 'success');
        fetchGames();
        fetchStatus();
    } catch (err) {
        showToast('خطأ أثناء إضافة اللعبة', 'error');
    }
}

async function addGameDLCs(appId, gameName) {
    try {
        showToast(`جاري جلب وفك إضافات (DLCs) لـ ${gameName}...`, 'info');
        const res = await fetch('/api/games/add-dlcs', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ appId })
        });
        const data = await res.json();
        if (res.ok) {
            showToast(`تمت إضافة ${data.addedCount} من إضافات (DLCs) لـ ${gameName}!`, 'success');
            fetchGames();
            fetchStatus();
        } else {
            showToast(data.error || 'فشل إضافة الـ DLCs', 'error');
        }
    } catch (err) {
        showToast('خطأ أثناء إضافة الـ DLCs', 'error');
    }
}

async function toggleGame(appId, enabled) {
    try {
        const res = await fetch('/api/games/toggle', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ appId, enabled })
        });
        if (res.ok) {
            const target = state.games.find(g => g.appId === appId);
            if (target) target.enabled = enabled;
            updateStatusUI();
            fetchStatus();
        }
    } catch (err) {
        console.error('Failed toggle:', err);
    }
}

async function deleteGame(appId, gameName) {
    if (!confirm(`هل أنت متأكد من حذف "${gameName}" من القائمة؟`)) return;
    try {
        const res = await fetch('/api/games/delete', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ appId })
        });
        if (res.ok) {
            showToast(`تم حذف "${gameName}"`, 'info');
            fetchGames();
            fetchStatus();
        }
    } catch (err) {
        showToast('فشل حذف اللعبة', 'error');
    }
}

async function launchSteam() {
    const mode = elements.modeSelect.value;
    const cleanTiming = elements.cleanTimingSelect.value;
    const clearAppCache = elements.checkAppCache.checked;
    const steamArgs = state.settings.steamArgs || '-inhibitbootstrap';

    elements.btnLaunchSteam.disabled = true;
    showToast('جاري حقن ملفات التخفي وتشغيل ستيم...', 'info');

    try {
        const res = await fetch('/api/launch', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ mode, cleanTiming, clearAppCache, steamArgs })
        });
        const data = await res.json();
        if (!res.ok) {
            showToast(data.error || 'فشل تشغيل ستيم', 'error');
            elements.btnLaunchSteam.disabled = false;
            return;
        }

        showToast('تم تشغيل ستيم بنجاح! الألعاب مفتوحة الآن.', 'success');
        fetchStatus();
    } catch (err) {
        showToast('خطأ أثناء تشغيل ستيم', 'error');
    } finally {
        elements.btnLaunchSteam.disabled = false;
    }
}

async function stopSteam() {
    elements.btnStopSteam.disabled = true;
    showToast('جاري إغلاق ستيم وحذف كل ملفات التخفي...', 'info');

    try {
        const res = await fetch('/api/stop', { method: 'POST' });
        const data = await res.json();
        if (res.ok) {
            showToast('تم إغلاق ستيم وحذف كل الملفات. مجلدك نظيف 100%!', 'success');
            fetchStatus();
        } else {
            showToast(data.error || 'فشل إيقاف ستيم', 'error');
        }
    } catch (err) {
        showToast('خطأ أثناء إيقاف ستيم', 'error');
    } finally {
        elements.btnStopSteam.disabled = false;
    }
}

async function cleanAllArtifacts() {
    if (!confirm('سيتم فحص مجلد ستيم ومسح أي ملفات قديمة لـ GreenLuma وإعادة ستيم للوضع الأصلي. هل تريد المتابعة؟')) return;

    showToast('جاري تنظيف مجلد ستيم بالكامل...', 'info');
    try {
        const res = await fetch('/api/clean-all', { method: 'POST' });
        const data = await res.json();
        if (res.ok) {
            const count = data.purged ? data.purged.length : 0;
            showToast(`تم تنظيف ستيم بنجاح! تم مسح ${count} ملف/مجلد.`, 'success');
            fetchStatus();
        }
    } catch (err) {
        showToast('خطأ أثناء التنظيف', 'error');
    }
}

async function showRawAppList() {
    try {
        const res = await fetch('/api/applist/raw');
        if (!res.ok) return;
        const data = await res.json();
        elements.rawAppListContent.textContent = data.content || '[Empty]';
        elements.rawAppListModal.classList.remove('hidden');
    } catch (err) {
        showToast('تعذر جلب ملف AppList.ini', 'error');
    }
}

// --- UI Updates ---
function applySettingsToUI() {
    const s = state.settings;

    // Apply theme attribute to body
    document.body.setAttribute('data-theme', s.theme || 'emerald');

    // Apply view mode
    document.body.setAttribute('data-view', s.viewMode || 'grid');
    elements.btnViewGrid.classList.toggle('active', s.viewMode === 'grid');
    elements.btnViewCompact.classList.toggle('active', s.viewMode === 'compact');

    // Language
    if (s.lang === 'en') {
        document.body.classList.add('lang-en');
        document.documentElement.setAttribute('lang', 'en');
        document.documentElement.setAttribute('dir', 'ltr');
    } else {
        document.body.classList.remove('lang-en');
        document.documentElement.setAttribute('lang', 'ar');
        document.documentElement.setAttribute('dir', 'rtl');
    }

    // Modal fields
    elements.settingCustomSteamPath.value = s.customSteamPath || '';
    elements.settingSteamArgs.value = s.steamArgs || '-inhibitbootstrap';
    elements.settingCleanTiming.value = s.cleanTiming || 'on_exit';
    elements.settingAutoKillSteam.checked = s.autoKillSteam !== false;
    elements.settingLang.value = s.lang || 'ar';

    // Main controls
    elements.modeSelect.value = s.mode || 'standard';
    elements.cleanTimingSelect.value = s.cleanTiming || 'on_exit';
    elements.checkAppCache.checked = Boolean(s.clearAppCache);
    elements.steamArgsDisplay.textContent = s.steamArgs || '-inhibitbootstrap';

    // Theme picker options
    document.querySelectorAll('.theme-option').forEach(opt => {
        opt.classList.toggle('active', opt.getAttribute('data-theme') === s.theme);
    });
}

function updateStatusUI() {
    const s = state.status;

    // Steam Path
    elements.steamPathText.textContent = s.steamDir || 'غير مكتشف';
    elements.detectedPathHint.textContent = s.steamDir || 'غير مكتشف';

    // Injection & Session Status
    if (s.injected) {
        elements.steamStatusPill.classList.add('active');
        elements.steamStatusText.textContent = s.steamRunning ? 'ستيم قيد التشغيل (محقون)' : 'الملفات محقونة في ستيم';
        elements.injectionStatusText.textContent = 'محقون (GreenLuma مفعل)';
        elements.injectionStatusText.className = 'info-value text-emerald';

        elements.btnLaunchSteam.classList.add('hidden');
        elements.btnStopSteam.classList.remove('hidden');
    } else {
        elements.steamStatusPill.classList.remove('active');
        elements.steamStatusText.textContent = s.steamRunning ? 'ستيم يعمل (الوضع الأصلي)' : 'ستيم مغلق (المجلد نظيف)';
        elements.injectionStatusText.textContent = 'غير محقون (آمن 100%)';
        elements.injectionStatusText.className = 'info-value';

        elements.btnLaunchSteam.classList.remove('hidden');
        elements.btnStopSteam.classList.add('hidden');
    }

    // Badges
    const enabledCount = state.games.filter(g => g.enabled).length;
    elements.gamesCountBadge.textContent = `${enabledCount} ألعاب مفعلة`;
    elements.libraryTotalBadge.textContent = state.games.length;
}

function renderGames() {
    const filterQuery = elements.filterInput.value.toLowerCase().trim();
    const filtered = state.games.filter(g => 
        g.name.toLowerCase().includes(filterQuery) || 
        String(g.appId).includes(filterQuery)
    );

    elements.gamesGrid.innerHTML = '';

    if (filtered.length === 0) {
        elements.emptyState.classList.remove('hidden');
        return;
    }

    elements.emptyState.classList.add('hidden');

    filtered.forEach(game => {
        const card = document.createElement('div');
        card.className = `game-card ${game.enabled ? '' : 'disabled'}`;
        card.id = `game-${game.appId}`;

        const isDLC = Boolean(game.isDLC);
        const hasDLCs = game.dlcCount && game.dlcCount > 0 && !isDLC;

        card.innerHTML = `
            <div class="game-poster-wrapper">
                <img class="game-poster" src="${game.image}" alt="${escapeHtml(game.name)}" 
                     onerror="this.src='https://placehold.co/460x215/0d111a/10b981?text=App+${game.appId}'" loading="lazy">
            </div>
            <div class="game-content">
                <div class="game-header-info">
                    <div class="game-title" title="${escapeHtml(game.name)}">${escapeHtml(game.name)}</div>
                    <div class="game-meta">
                        <span class="appid-pill">ID: ${game.appId}</span>
                        ${isDLC ? '<span class="dlc-tag">إضافة DLC</span>' : ''}
                        ${hasDLCs ? `<button class="dlc-btn-pill" data-appid="${game.appId}" title="فتح جميع إضافات اللعبة">+ فتح الـ DLCs (${game.dlcCount})</button>` : ''}
                    </div>
                </div>
                <div class="game-actions-bar">
                    <label class="switch" title="${game.enabled ? 'تعطيل اللعبة' : 'تفعيل اللعبة'}">
                        <input type="checkbox" ${game.enabled ? 'checked' : ''} data-appid="${game.appId}">
                        <span class="slider"></span>
                    </label>
                    <div class="card-btns">
                        <a href="https://store.steampowered.com/app/${game.appId}" target="_blank" 
                           class="btn-card-action" title="فتح صفحة ستيم">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" y1="14" x2="21" y2="3"/></svg>
                        </a>
                        <button class="btn-card-action delete-btn" data-appid="${game.appId}" 
                                data-name="${escapeHtml(game.name)}" title="حذف من القائمة">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>
                        </button>
                    </div>
                </div>
            </div>
        `;

        // Switch toggle
        const switchInput = card.querySelector('input[type="checkbox"]');
        switchInput.addEventListener('change', (e) => {
            const isChecked = e.target.checked;
            card.classList.toggle('disabled', !isChecked);
            toggleGame(game.appId, isChecked);
        });

        // DLC unlock button
        const dlcBtn = card.querySelector('.dlc-btn-pill');
        if (dlcBtn) {
            dlcBtn.addEventListener('click', () => {
                addGameDLCs(game.appId, game.name);
            });
        }

        // Delete button
        const deleteBtn = card.querySelector('.delete-btn');
        deleteBtn.addEventListener('click', () => {
            deleteGame(game.appId, game.name);
        });

        elements.gamesGrid.appendChild(card);
    });

    updateStatusUI();
}

// --- Autocomplete Search ---
async function handleSearchInput(e) {
    const val = e.target.value.trim();

    if (val.length < 2 || /^\d+$/.test(val)) {
        elements.searchDropdown.classList.add('hidden');
        return;
    }

    clearTimeout(state.searchDebounce);
    state.searchDebounce = setTimeout(async () => {
        try {
            const res = await fetch(`/api/search?q=${encodeURIComponent(val)}`);
            if (!res.ok) return;
            const items = await res.json();
            renderSearchResults(items);
        } catch (err) {
            console.error('Search error:', err);
        }
    }, 300);
}

function renderSearchResults(items) {
    if (!items || items.length === 0) {
        elements.searchDropdown.classList.add('hidden');
        return;
    }

    elements.searchDropdown.innerHTML = '';
    items.forEach(item => {
        const div = document.createElement('div');
        div.className = 'search-item';
        div.innerHTML = `
            <img class="search-thumb" src="${item.image}" onerror="this.style.display='none'">
            <div class="search-info">
                <div class="search-title">${escapeHtml(item.name)}</div>
                <div class="search-appid">AppID: ${item.appId}</div>
            </div>
        `;
        div.addEventListener('click', () => {
            addGame(item.appId, item.name);
        });
        elements.searchDropdown.appendChild(div);
    });

    elements.searchDropdown.classList.remove('hidden');
}

// --- Event Listeners ---
function setupEventListeners() {
    // Add game button & enter key
    elements.btnAddGame.addEventListener('click', () => {
        const val = elements.gameInput.value.trim();
        if (/^\d+$/.test(val)) {
            addGame(parseInt(val, 10));
        } else if (val) {
            showToast('يرجى إدخال رقم AppID صالح أو اختيار لعبة من القائمة المنبثقة', 'error');
        }
    });

    elements.gameInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            const val = elements.gameInput.value.trim();
            if (/^\d+$/.test(val)) {
                addGame(parseInt(val, 10));
            }
        }
    });

    elements.gameInput.addEventListener('input', handleSearchInput);

    // Close dropdown on click outside
    document.addEventListener('click', (e) => {
        if (!elements.gameInput.contains(e.target) && !elements.searchDropdown.contains(e.target)) {
            elements.searchDropdown.classList.add('hidden');
        }
    });

    // Quick chips
    document.querySelectorAll('.chip').forEach(chip => {
        chip.addEventListener('click', () => {
            const appId = parseInt(chip.getAttribute('data-appid'), 10);
            const name = chip.getAttribute('data-name');
            addGame(appId, name);
        });
    });

    // Filter input
    elements.filterInput.addEventListener('input', () => {
        renderGames();
    });

    // View toggles
    elements.btnViewGrid.addEventListener('click', () => {
        state.settings.viewMode = 'grid';
        saveSettings({ viewMode: 'grid' });
    });
    elements.btnViewCompact.addEventListener('click', () => {
        state.settings.viewMode = 'compact';
        saveSettings({ viewMode: 'compact' });
    });

    // Launch & Stop buttons
    elements.btnLaunchSteam.addEventListener('click', launchSteam);
    elements.btnStopSteam.addEventListener('click', stopSteam);
    elements.btnRefreshStatus.addEventListener('click', fetchStatus);
    elements.btnCleanAll.addEventListener('click', cleanAllArtifacts);

    // View raw AppList
    elements.btnViewAppListIni.addEventListener('click', showRawAppList);
    elements.btnCloseRawModal.addEventListener('click', () => elements.rawAppListModal.classList.add('hidden'));
    elements.btnCloseRawModal2.addEventListener('click', () => elements.rawAppListModal.classList.add('hidden'));
    elements.btnCopyRawIni.addEventListener('click', () => {
        navigator.clipboard.writeText(elements.rawAppListContent.textContent);
        showToast('تم نسخ محتوى AppList.ini إلى الحافظة!', 'success');
    });

    // Settings Modal
    elements.btnOpenSettings.addEventListener('click', () => {
        elements.settingsModal.classList.remove('hidden');
    });
    elements.btnOpenBulkModal.addEventListener('click', () => {
        elements.settingsModal.classList.remove('hidden');
        switchModalTab('tabAppList');
    });
    elements.btnCloseSettings.addEventListener('click', () => elements.settingsModal.classList.add('hidden'));
    elements.btnCancelSettings.addEventListener('click', () => elements.settingsModal.classList.add('hidden'));

    // Modal Tabs
    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            switchModalTab(btn.getAttribute('data-tab'));
        });
    });

    // Argument presets pills
    document.querySelectorAll('.arg-pill').forEach(pill => {
        pill.addEventListener('click', () => {
            const arg = pill.getAttribute('data-arg');
            let current = elements.settingSteamArgs.value.trim();
            if (!current.includes(arg)) {
                elements.settingSteamArgs.value = (current + ' ' + arg).trim();
            }
        });
    });

    // Theme options in settings
    document.querySelectorAll('.theme-option').forEach(opt => {
        opt.addEventListener('click', () => {
            document.querySelectorAll('.theme-option').forEach(o => o.classList.remove('active'));
            opt.classList.add('active');
            const theme = opt.getAttribute('data-theme');
            document.body.setAttribute('data-theme', theme);
        });
    });

    // Save Settings Button
    elements.btnSaveSettings.addEventListener('click', () => {
        const activeThemeOpt = document.querySelector('.theme-option.active');
        const theme = activeThemeOpt ? activeThemeOpt.getAttribute('data-theme') : 'emerald';

        const updated = {
            customSteamPath: elements.settingCustomSteamPath.value.trim(),
            steamArgs: elements.settingSteamArgs.value.trim(),
            cleanTiming: elements.settingCleanTiming.value,
            autoKillSteam: elements.settingAutoKillSteam.checked,
            lang: elements.settingLang.value,
            theme: theme,
            mode: elements.modeSelect.value,
            clearAppCache: elements.checkAppCache.checked
        };

        saveSettings(updated);
        elements.settingsModal.classList.add('hidden');
    });

    // Bulk Import submit
    elements.btnSubmitBulk.addEventListener('click', async () => {
        const text = elements.bulkImportTextarea.value.trim();
        if (!text) {
            showToast('يرجى لصق أرقام AppIDs في المربع أولاً', 'error');
            return;
        }

        showToast('جاري استيراد الألعاب وجلب بياناتها من ستيم...', 'info');
        try {
            const res = await fetch('/api/games/bulk', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ raw: text })
            });
            const data = await res.json();
            if (res.ok) {
                showToast(`تم استيراد ${data.addedCount} لعبة جديدة بنجاح!`, 'success');
                elements.bulkImportTextarea.value = '';
                elements.settingsModal.classList.add('hidden');
                fetchGames();
                fetchStatus();
            } else {
                showToast(data.error || 'فشل الاستيراد', 'error');
            }
        } catch (err) {
            showToast('خطأ أثناء استيراد الألعاب', 'error');
        }
    });

    // Copy AppList from Settings
    elements.btnCopyAppListIni.addEventListener('click', async () => {
        try {
            const res = await fetch('/api/applist/raw');
            const data = await res.json();
            navigator.clipboard.writeText(data.content || '');
            showToast('تم نسخ محتوى AppList.ini إلى الحافظة!', 'success');
        } catch (err) {
            showToast('فشل النسخ', 'error');
        }
    });

    // Clear All Games
    elements.btnClearAllGames.addEventListener('click', async () => {
        if (!confirm('تحذير: هل أنت متأكد من مسح جميع الألعاب من قائمتك؟')) return;
        try {
            const res = await fetch('/api/games/clear', { method: 'POST' });
            if (res.ok) {
                showToast('تم تفريغ قائمة الألعاب بالكامل', 'info');
                elements.settingsModal.classList.add('hidden');
                fetchGames();
                fetchStatus();
            }
        } catch (err) {
            showToast('فشل التفريغ', 'error');
        }
    });
}

function switchModalTab(tabId) {
    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.classList.toggle('active', btn.getAttribute('data-tab') === tabId);
    });
    document.querySelectorAll('.tab-pane').forEach(pane => {
        pane.classList.toggle('active', pane.id === tabId);
    });
}

// --- Toast Notifications ---
function showToast(message, type = 'info') {
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.textContent = message;

    elements.toastContainer.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transform = 'translateY(10px)';
        toast.style.transition = 'all 0.2s ease';
        setTimeout(() => toast.remove(), 250);
    }, 3500);
}

function escapeHtml(text) {
    if (!text) return '';
    return String(text)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}
