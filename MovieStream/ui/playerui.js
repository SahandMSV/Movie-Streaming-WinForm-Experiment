const post = (o) => window.chrome.webview.postMessage(o);

const u = document.getElementById('u');
const msg = document.getElementById('msg');
const pp = document.getElementById('pp');
const seek = document.getElementById('seek');
const timeEl = document.getElementById('time');

const topBar = document.getElementById('topBar');
const bottomBar = document.getElementById('bottomBar');

let dragging = false;
let currentIsPlaying = false;

const HIDE_AFTER_MS = 2500;
let hideTimer = 0;

function setMsg(text, isErr) {
    msg.textContent = text || '';
    msg.classList.toggle('err', !!isErr);
}

function pad(n) { return String(n).padStart(2, '0'); }

function fmt(ms) {
    ms = Math.max(0, ms | 0);
    const s = Math.floor(ms / 1000);
    const h = Math.floor(s / 3600);
    const m = Math.floor((s % 3600) / 60);
    const ss = s % 60;
    return h > 0 ? `${h}:${pad(m)}:${pad(ss)}` : `${m}:${pad(ss)}`;
}

function showBars() {
    topBar.classList.remove('hidden');
    bottomBar.classList.remove('hidden');
    scheduleHide();
}

function hideBars() {
    topBar.classList.add('hidden');
    bottomBar.classList.add('hidden');
}

function scheduleHide() {
    if (hideTimer) clearTimeout(hideTimer);
    hideTimer = setTimeout(hideBars, HIDE_AFTER_MS);
}

// Any activity shows bars
for (const evt of ['mousemove', 'pointermove', 'wheel', 'keydown', 'touchstart']) {
    window.addEventListener(evt, showBars, { passive: true });
}

// Keep bars visible while hovering them
topBar.addEventListener('pointerenter', showBars);
bottomBar.addEventListener('pointerenter', showBars);

document.getElementById('load').addEventListener('click', () => {
    setMsg('', false);
    showBars();
    post({ type: 'load', url: (u.value || '').trim() });
});

document.getElementById('remove').addEventListener('click', () => {
    setMsg('', false);
    showBars();
    post({ type: 'remove' });
});

pp.addEventListener('click', () => {
    showBars();
    post({ type: currentIsPlaying ? 'pause' : 'play' });
});

seek.addEventListener('pointerdown', () => {
    dragging = true;
    showBars();
    post({ type: 'seekStart' });
});

seek.addEventListener('pointerup', () => {
    dragging = false;
    showBars();
    post({ type: 'seekEnd', pos: Number(seek.value) / Number(seek.max) });
});

// Initial state request + start visible then hide later
post({ type: 'requestState' });
showBars();

window.chrome.webview.addEventListener('message', (ev) => {
    const m = ev.data || {};

    if (m.type === 'error') setMsg(m.message || 'Error', true);
    if (m.type === 'status') setMsg(m.message || '', false);

    if (m.type === 'state') {
        currentIsPlaying = !!m.isPlaying;
        pp.textContent = currentIsPlaying ? 'Pause' : 'Play';

        const hasLen = typeof m.lengthMs === 'number' && m.lengthMs > 0;
        timeEl.textContent = hasLen
            ? `${fmt(m.timeMs)} / ${fmt(m.lengthMs)}`
            : `${fmt(m.timeMs)} / --:--`;

        if (!dragging && typeof m.pos === 'number' && m.pos >= 0) {
            seek.value = String(Math.max(0, Math.min(seek.max, Math.round(m.pos * Number(seek.max)))));
        }

        if (!m.hasMedia) {
            seek.value = "0";
            timeEl.textContent = '00:00 / 00:00';
            pp.textContent = 'Play';
            currentIsPlaying = false;
        }
    }
});
