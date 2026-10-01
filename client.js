/*
 * YouTubeHome client script.
 * Loaded in the Jellyfin web client (see README for injection options). It:
 *   1. asks /YouTubeHome/Config whether to take over the home page,
 *   2. fetches /YouTubeHome/Feed,
 *   3. renders a YouTube-style grid + rows into the home view.
 * NOTE: the DOM hooks (#indexPage, .tabContent, 'viewshow') are jellyfin-web internals and may need
 * a tweak after major web-client updates.
 */
(function () {
    'use strict';

    var ROOT_ID = 'yth-root';
    var BODY_CLASS = 'yth-active';
    var cfg = null;
    var loading = false;

    // ---------- styles ----------
    var css = [
        'body.' + BODY_CLASS + ' #indexPage .tabContent, body.' + BODY_CLASS + ' #indexPage .homeSectionsContainer { display:none !important; }',
        '#' + ROOT_ID + ' { padding: 1rem 3% 5rem; }',
        '.yth-title { font-size:1.3rem; margin:1.6rem 0 .8rem; }',
        '.yth-grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(260px,1fr)); gap:1.4rem 1rem; }',
        '.yth-row { display:flex; gap:1rem; overflow-x:auto; padding-bottom:.6rem; scroll-snap-type:x proximity; }',
        '.yth-row .yth-card { flex:0 0 280px; scroll-snap-align:start; }',
        '.yth-card { color:inherit; text-decoration:none; display:block; }',
        '.yth-thumb { position:relative; aspect-ratio:16/9; border-radius:12px; overflow:hidden; background:#222; }',
        '.yth-thumb img { width:100%; height:100%; object-fit:cover; display:block; }',
        '.yth-dur { position:absolute; right:6px; bottom:6px; background:rgba(0,0,0,.8); color:#fff; font-size:.75rem; padding:1px 5px; border-radius:4px; }',
        '.yth-prog { position:absolute; left:0; right:0; bottom:0; height:3px; background:rgba(255,255,255,.3); }',
        '.yth-prog > i { display:block; height:100%; background:#e00; }',
        '.yth-name { margin:.55rem 0 .15rem; font-weight:600; line-height:1.25; display:-webkit-box; -webkit-line-clamp:2; -webkit-box-orient:vertical; overflow:hidden; }',
        '.yth-chan { opacity:.65; font-size:.85rem; }'
    ].join('\n');
    var styleEl = document.createElement('style');
    styleEl.textContent = css;
    document.head.appendChild(styleEl);

    // ---------- helpers ----------
    function fmtDuration(ticks) {
        if (!ticks) { return ''; }
        var s = Math.round(ticks / 1e7), h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60), sec = s % 60;
        var pad = function (n) { return n < 10 ? '0' + n : '' + n; };
        return h > 0 ? h + ':' + pad(m) + ':' + pad(sec) : m + ':' + pad(sec);
    }

    function imageUrl(item) {
        var tags = item.ImageTags || {};
        var type = tags.Primary ? 'Primary' : (tags.Thumb ? 'Thumb' : null);
        if (type) {
            return ApiClient.getUrl('Items/' + item.Id + '/Images/' + type, { maxWidth: 480, quality: 85, tag: tags[type] });
        }
        if (item.BackdropImageTags && item.BackdropImageTags.length) {
            return ApiClient.getUrl('Items/' + item.Id + '/Images/Backdrop/0', { maxWidth: 480, tag: item.BackdropImageTags[0] });
        }
        return '';
    }

    function card(entry) {
        var item = entry.Item;
        var a = document.createElement('a');
        a.className = 'yth-card';
        // Standard Jellyfin item details route; the details page has the Play button.
        a.href = '#/details?id=' + item.Id + '&serverId=' + (item.ServerId || ApiClient.serverId());

        var thumb = document.createElement('div');
        thumb.className = 'yth-thumb';
        var src = imageUrl(item);
        if (src) {
            var img = document.createElement('img');
            img.loading = 'lazy';
            img.alt = '';
            img.src = src;
            thumb.appendChild(img);
        }
        var dur = fmtDuration(item.RunTimeTicks);
        if (dur) {
            var d = document.createElement('span');
            d.className = 'yth-dur';
            d.textContent = dur;
            thumb.appendChild(d);
        }
        var pct = item.UserData && item.UserData.PlayedPercentage;
        if (pct > 0 && pct < 100) {
            var p = document.createElement('div');
            p.className = 'yth-prog';
            p.innerHTML = '<i style="width:' + pct + '%"></i>';
            thumb.appendChild(p);
        }

        var name = document.createElement('div');
        name.className = 'yth-name';
        name.textContent = item.Name;
        var chan = document.createElement('div');
        chan.className = 'yth-chan';
        chan.textContent = entry.ChannelName;

        a.appendChild(thumb);
        a.appendChild(name);
        a.appendChild(chan);
        return a;
    }

    function render(root, feed) {
        root.innerHTML = '';
        (feed.Rows || []).forEach(function (row) {
            var h = document.createElement('h2');
            h.className = 'yth-title';
            h.textContent = row.Title;
            var wrap = document.createElement('div');
            wrap.className = row.Kind === 'recommended' ? 'yth-grid' : 'yth-row';
            row.Entries.forEach(function (e) { wrap.appendChild(card(e)); });
            root.appendChild(h);
            root.appendChild(wrap);
        });
    }

    // ---------- mounting ----------
    function mount() {
        var page = document.querySelector('#indexPage');
        if (!page || loading || !window.ApiClient) { return; }
        loading = true;
        var root = document.getElementById(ROOT_ID);
        if (!root) {
            root = document.createElement('div');
            root.id = ROOT_ID;
            page.appendChild(root);
        }
        document.body.classList.add(BODY_CLASS);
        ApiClient.getJSON(ApiClient.getUrl('YouTubeHome/Feed'))
            .then(function (feed) { render(root, feed); })
            .catch(function (err) { console.error('[YouTubeHome] feed failed', err); unmount(); })
            .then(function () { loading = false; });
    }

    function unmount() {
        document.body.classList.remove(BODY_CLASS);
        var root = document.getElementById(ROOT_ID);
        if (root) { root.remove(); }
    }

    function isHome() {
        return /^#\/home(\.html)?(\?|$)/.test(location.hash) || location.hash === '' || location.hash === '#/';
    }

    function refresh() {
        if (!window.ApiClient || !ApiClient.accessToken()) { return; }  // not logged in yet
        var go = function () {
            if (cfg && cfg.Enabled && cfg.ReplaceHomePage && isHome()) { mount(); } else { unmount(); }
        };
        if (cfg) { go(); return; }
        ApiClient.getJSON(ApiClient.getUrl('YouTubeHome/Config'))
            .then(function (c) { cfg = c; go(); })
            .catch(function () { /* plugin disabled or not authorised: stay out of the way */ });
    }

    // Re-run on every SPA view change; the shuffle is re-rolled each time the home view is shown.
    document.addEventListener('viewshow', function () { refresh(); });
    window.addEventListener('hashchange', function () { refresh(); });

    // Manual hook if you prefer a custom tab / page: YouTubeHome.mount(containerElement)
    window.YouTubeHome = {
        mount: function (container) {
            ApiClient.getJSON(ApiClient.getUrl('YouTubeHome/Feed')).then(function (f) { render(container, f); });
        }
    };
})();
