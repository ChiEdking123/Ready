(function () {
    const ready = window.ready = window.ready || {};

    // Ctrl/Cmd + S must create the idea instead of opening the webview's save dialog.
    document.addEventListener('keydown', function (event) {
        if (!(event.ctrlKey || event.metaKey) || event.altKey) return;
        if (event.key !== 's' && event.key !== 'S') return;
        // A keystroke that belongs to an IME composition must not save: the field still
        // holds pinyin that .NET has never seen, so the record would keep the old text.
        if (ready.isComposing || event.isComposing || event.keyCode === 229) return;
        event.preventDefault();
        event.stopPropagation();
        ready.clickSave();
    }, true);

    // The text fields sync on change, not on every keystroke, so a programmatic click
    // (Ctrl + S) would otherwise save the previous value: clicking with the mouse blurs
    // the field first, but a keyboard shortcut does not.
    // Also exposed as ready.flushActiveField: every action that leaves the form (navigate,
    // back, save, clear, exit) must run this first, otherwise the field that only syncs on
    // change still holds uncommitted text in the DOM and .NET saves the previous value.
    function flushActiveField() {
        // Mid-composition the DOM is ahead of .NET; dispatching change here would push
        // half a word out and then have the committed text written back over it.
        if (ready.isComposing) return;
        const active = document.activeElement;
        if (!active || !active.tagName) return;
        const tag = active.tagName;
        if (tag !== 'INPUT' && tag !== 'TEXTAREA') return;
        active.dispatchEvent(new Event('change', { bubbles: true }));
    }

    ready.flushActiveField = flushActiveField;

    ready.clickSave = function () {
        flushActiveField();
        const button = document.querySelector('.create-button');
        if (!button || button.disabled) return false;
        button.click();
        return true;
    };

    // ---------- IME composition guard ----------
    // @bind:event="oninput" writes every keystroke back to .NET. While a Chinese IME is
    // composing, the DOM holds the half-finished pinyin but the .NET field still holds the
    // last committed text, so the re-render writes the old value back over the composing
    // word. That resets the composition and the candidate window closes with it.
    //
    // The listener has to live on window, not document: Blazor's event delegator also
    // registers on document in the capture phase, and index.html loads blazor.webview.js
    // before app.js, so a document-level guard would run *after* Blazor has already
    // handled the keystroke. Window capture happens earlier in the same path.
    ready.isComposing = false;

    // The page also needs to know: a Blazor re-render writes the C# field value back into
    // the textarea, and that write alone closes the candidate window in Chromium even if
    // no keystroke ever reached .NET. So the composition is reported to the app, which
    // then holds off re-rendering until it ends.
    let imeNotifier = null;

    ready.registerImeNotifier = function (dotNetRef) {
        imeNotifier = dotNetRef;
    };

    function notifyIme(started) {
        if (!imeNotifier) return;
        imeNotifier.invokeMethodAsync(started ? 'BeginComposition' : 'EndComposition').catch(function () { });
    }

    function isTextField(node) {
        if (!node) return false;
        const tag = node.tagName;
        return tag === 'INPUT' || tag === 'TEXTAREA';
    }

    let pendingTarget = null;
    let pendingDelivery = 0;

    window.addEventListener('compositionstart', function (event) {
        ready.isComposing = true;
        if (!isTextField(event.target)) return;
        pendingTarget = null;
        window.clearTimeout(pendingDelivery);
        notifyIme(true);
    }, true);

    window.addEventListener('compositionend', function (event) {
        ready.isComposing = false;
        if (!isTextField(event.target)) return;
        notifyIme(false);

        const target = event.target;
        const data = event.data;

        // Chromium sends its own input event right after compositionend and that one
        // carries the committed text, so this is only a fallback for the case where it
        // does not arrive. Sending both would duplicate the phrase.
        pendingTarget = target;
        window.clearTimeout(pendingDelivery);
        pendingDelivery = window.setTimeout(function () {
            if (pendingTarget !== target) return;
            pendingTarget = null;
            target.dispatchEvent(new InputEvent('input', {
                bubbles: true,
                cancelable: false,
                inputType: 'insertText',
                data: data
            }));
        }, 0);
    }, true);

    window.addEventListener('input', function (event) {
        if (!isTextField(event.target)) return;

        // Swallow the composing keystrokes: .NET must not see pinyin-in-progress.
        if (event.isComposing) {
            event.stopImmediatePropagation();
            return;
        }

        // A genuine post-composition edit arrived, so the fallback replay is redundant.
        if (pendingTarget !== null) {
            window.clearTimeout(pendingDelivery);
            pendingTarget = null;
        }

        if (event.target.classList && event.target.classList.contains('summary-input')) {
            updateSummaryCount(event.target);
        }
    }, true);

    // Enter picks an IME candidate. Letting it reach @onkeydown would submit the tag
    // field in the middle of a word.
    window.addEventListener('keydown', function (event) {
        if (!ready.isComposing) return;
        if (event.key !== 'Enter') return;
        event.stopImmediatePropagation();
    }, true);

    // The summary field binds on change, so the character counter cannot come from
    // Blazor any more — it would need a re-render per keystroke. It is written straight
    // to the DOM instead, which never touches the composing text.
    function updateSummaryCount(target) {
        const counter = document.querySelector('.summary-count');
        if (!counter) return;
        const max = target.getAttribute('maxlength') || '300';
        counter.textContent = target.value.length + ' / ' + max;
        const ratio = max ? target.value.length / Number(max) : 0;
        counter.classList.toggle('near-limit', ratio >= 0.9);
    }

    window.addEventListener('input', function (event) {
        if (!event.target || !event.target.classList) return;
        if (!event.target.classList.contains('summary-input')) return;
        if (event.isComposing) return;
        updateSummaryCount(event.target);
    }, true);

    window.addEventListener('click', function (event) {
        const button = event.target && event.target.closest ? event.target.closest('[data-clear-action]') : null;
        if (!button) return;
        // The summary field only syncs on change, so .NET clears its own copy — the DOM
        // text has to be emptied here as well, otherwise the old text stays on screen.
        window.setTimeout(function () {
            const summary = document.querySelector('.summary-input');
            if (summary) {
                summary.value = '';
                updateSummaryCount(summary);
            }
            const references = document.querySelector('.references-input');
            if (references) references.value = '';
        }, 50);
    }, true);

    // Interface scale is a single multiplier on the root font size, so every rem
    // in app.css grows and shrinks together with the text.
    ready.setUiScale = function (percent) {
        const value = Number(percent);
        const scale = Number.isFinite(value) && value > 0 ? value / 100 : 1;
        document.documentElement.style.setProperty('--ui-scale', String(scale));
    };

    // ---------- Palette ----------
    // Google's Material 3 keeps one neutral ramp that carries the hue of the chosen
    // color. The palette is generated here instead of being hard-coded per accent so
    // surfaces, outlines and text all shift with the accent rather than staying beige.
    const ACCENTS = {
        coral: { hue: 18 },
        sage: { hue: 138 },
        blue: { hue: 210 },
        purple: { hue: 272 },
        yellow: { hue: 42 }
    };

    const LIGHT = {
        '--md-primary': (h) => hsl(h, 45, 40),
        '--md-on-primary': (h) => hsl(h, 40, 100),
        '--md-primary-container': (h) => hsl(h, 70, 90),
        '--md-on-primary-container': (h) => hsl(h, 60, 12),
        '--md-secondary-container': (h) => hsl(h, 40, 90),
        '--md-on-secondary-container': (h) => hsl(h, 22, 22),
        '--md-surface': (h) => hsl(h, 32, 98),
        '--md-surface-dim': (h) => hsl(h, 22, 92),
        '--md-surface-container-lowest': (h) => hsl(h, 30, 100),
        '--md-surface-container-low': (h) => hsl(h, 32, 96),
        '--md-surface-container': (h) => hsl(h, 30, 94),
        '--md-surface-container-high': (h) => hsl(h, 28, 91),
        '--md-surface-container-highest': (h) => hsl(h, 26, 88),
        '--md-on-surface': (h) => hsl(h, 18, 10),
        '--md-on-surface-variant': (h) => hsl(h, 14, 32),
        '--md-outline': (h) => hsl(h, 12, 50),
        '--md-outline-variant': (h) => hsl(h, 18, 82),
        '--md-inverse-surface': (h) => hsl(h, 14, 18),
        '--md-inverse-on-surface': (h) => hsl(h, 30, 94),
        '--md-scrim': () => 'rgba(0, 0, 0, 0.32)'
    };

    const DARK = {
        '--md-primary': (h) => hsl(h, 70, 80),
        '--md-on-primary': (h) => hsl(h, 60, 16),
        '--md-primary-container': (h) => hsl(h, 45, 28),
        '--md-on-primary-container': (h) => hsl(h, 70, 90),
        '--md-secondary-container': (h) => hsl(h, 18, 26),
        '--md-on-secondary-container': (h) => hsl(h, 26, 88),
        '--md-surface': (h) => hsl(h, 14, 8),
        '--md-surface-dim': (h) => hsl(h, 14, 8),
        '--md-surface-container-lowest': (h) => hsl(h, 16, 6),
        '--md-surface-container-low': (h) => hsl(h, 14, 11),
        '--md-surface-container': (h) => hsl(h, 13, 13),
        '--md-surface-container-high': (h) => hsl(h, 12, 18),
        '--md-surface-container-highest': (h) => hsl(h, 11, 22),
        '--md-on-surface': (h) => hsl(h, 24, 92),
        '--md-on-surface-variant': (h) => hsl(h, 16, 80),
        '--md-outline': (h) => hsl(h, 12, 60),
        '--md-outline-variant': (h) => hsl(h, 12, 30),
        '--md-inverse-surface': (h) => hsl(h, 24, 92),
        '--md-inverse-on-surface': (h) => hsl(h, 14, 18),
        '--md-scrim': () => 'rgba(0, 0, 0, 0.55)'
    };

    // Error stays red in every scheme; it must never pick up the accent hue.
    const ERROR_LIGHT = {
        '--md-error': '#ba1a1a',
        '--md-on-error': '#ffffff',
        '--md-error-container': '#ffdad6',
        '--md-on-error-container': '#410002'
    };

    const ERROR_DARK = {
        '--md-error': '#ffb4ab',
        '--md-on-error': '#690005',
        '--md-error-container': '#93000a',
        '--md-on-error-container': '#ffdad6'
    };

    function hsl(h, s, l) {
        return 'hsl(' + h + ' ' + s + '% ' + l + '%)';
    }

    ready.prefersDark = function () {
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    };

    // Applies the full Material 3 palette for a theme + accent pair and returns the
    // surface color, which the native window uses to paint its own background so no
    // light strip leaks around the web view in dark mode.
    ready.applyTheme = function (theme, accent) {
        const dark = theme === 'dark' || (theme === 'system' && ready.prefersDark());
        const key = Object.prototype.hasOwnProperty.call(ACCENTS, accent) ? accent : 'coral';
        const hue = ACCENTS[key].hue;
        const ramp = dark ? DARK : LIGHT;
        const errors = dark ? ERROR_DARK : ERROR_LIGHT;
        const root = document.documentElement;
        const targets = [root];
        const app = document.querySelector('.app');
        if (app) targets.push(app);

        for (const target of targets) {
            for (const token in ramp) target.style.setProperty(token, ramp[token](hue));
            for (const token in errors) target.style.setProperty(token, errors[token]);
        }

        root.style.colorScheme = dark ? 'dark' : 'light';
        if (app) app.style.colorScheme = dark ? 'dark' : 'light';

        const meta = document.querySelector('meta[name="theme-color"]');
        const surface = ramp['--md-surface'](hue);
        if (meta) meta.setAttribute('content', surface);
        return surface;
    };

    // ---------- Ctrl + wheel = interface scale ----------
    let wheelHandler = null;

    ready.registerScaleWheel = function (dotNetRef) {
        if (wheelHandler) document.removeEventListener('wheel', wheelHandler, { passive: false });

        let last = 0;
        wheelHandler = function (event) {
            if (!event.ctrlKey) return;
            event.preventDefault();
            const now = Date.now();
            if (now - last < 120) return;
            last = now;
            const direction = event.deltaY < 0 ? 1 : -1;
            dotNetRef.invokeMethodAsync('ScaleNudge', direction).catch(function () { });
        };
        document.addEventListener('wheel', wheelHandler, { passive: false });
    };

    ready.disposeScaleWheel = function () {
        if (!wheelHandler) return;
        document.removeEventListener('wheel', wheelHandler, { passive: false });
        wheelHandler = null;
    };


    // ---------- Dialog focus management ----------
    // A dialog that only sets aria-modal is not actually modal for the keyboard: Tab walks
    // straight out into the page behind the scrim, and Escape does nothing. The handlers
    // live here rather than in the component because focus is a DOM concern, and this keeps
    // the dialog markup free of a third-party focus-trap script.
    let dialogReturnFocus = null;
    let dialogKeyHandler = null;
    let dialogRoot = null;

    function focusables(root) {
        return Array.prototype.slice
            .call(root.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'))
            .filter(function (node) {
                return !node.disabled && node.offsetParent !== null;
            });
    }

    ready.openDialog = function (element) {
        if (!element) return;
        // Remember where the user was before the scrim appeared so closing the dialog does
        // not dump the caret at the top of the document.
        dialogReturnFocus = document.activeElement;
        dialogRoot = element;

        const items = focusables(element);
        // Focus the confirm button, i.e. the last one: the first button is always the
        // "cancel" escape hatch and should not be the default action for Enter.
        const target = items.length > 0 ? items[items.length - 1] : element;
        window.setTimeout(function () {
            try { target.focus({ preventScroll: true }); } catch (error) { }
        }, 0);

        if (dialogKeyHandler) document.removeEventListener('keydown', dialogKeyHandler, true);
        dialogKeyHandler = function (event) {
            if (event.key === 'Escape') {
                event.preventDefault();
                event.stopPropagation();
                // Let the component decide what dismissing means; a synthetic click on the
                // cancel button reuses the same path as tapping it.
                const cancel = dialogRoot && dialogRoot.querySelector('.modal-actions .button');
                if (cancel) cancel.click();
                return;
            }
            if (event.key !== 'Tab') return;
            const items = focusables(dialogRoot);
            if (items.length === 0) return;
            const first = items[0];
            const last = items[items.length - 1];
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            } else if (!dialogRoot.contains(document.activeElement)) {
                // Focus escaped (e.g. a re-render replaced the focused node): pull it back.
                event.preventDefault();
                first.focus();
            }
        };
        document.addEventListener('keydown', dialogKeyHandler, true);
    };

    ready.closeDialog = function () {
        if (dialogKeyHandler) document.removeEventListener('keydown', dialogKeyHandler, true);
        dialogKeyHandler = null;
        dialogRoot = null;
        const previous = dialogReturnFocus;
        dialogReturnFocus = null;
        if (!previous) return;
        window.setTimeout(function () {
            try { previous.focus({ preventScroll: true }); } catch (error) { }
        }, 0);
    };

})();
