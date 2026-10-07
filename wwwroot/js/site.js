// PoultryPro - global UI helpers
(function () {
    document.addEventListener('DOMContentLoaded', function () {

        // Auto-dismiss legacy alerts and toasts
        document.querySelectorAll('.alert-dismissible, .pp-toast[data-autohide]').forEach(function (el) {
            setTimeout(function () {
                el.style.transition = 'opacity .5s';
                el.style.opacity = '0';
                setTimeout(function () { el.remove(); }, 500);
            }, 4500);
        });

        // Confirm dialogs: <form data-confirm="..."> or <button data-confirm="...">
        document.querySelectorAll('form[data-confirm]').forEach(function (form) {
            form.addEventListener('submit', function (e) { if (!confirm(form.dataset.confirm)) e.preventDefault(); });
        });
        document.querySelectorAll('button[data-confirm], a[data-confirm]').forEach(function (btn) {
            btn.addEventListener('click', function (e) { if (!confirm(btn.dataset.confirm)) e.preventDefault(); });
        });

        // Collapsible sidebar groups (state remembered per browser)
        var store = {};
        try { store = JSON.parse(localStorage.getItem('pp.sidebar') || '{}'); } catch (e) { store = {}; }
        document.querySelectorAll('.sidebar-section[data-key]').forEach(function (sec) {
            var key = sec.dataset.key;
            var hasActive = !!sec.querySelector('.nav-link.active');
            if (store[key] === 'c' && !hasActive) sec.classList.add('collapsed');
            var label = sec.querySelector('.sidebar-section-label');
            if (label) label.addEventListener('click', function () {
                sec.classList.toggle('collapsed');
                store[key] = sec.classList.contains('collapsed') ? 'c' : 'o';
                try { localStorage.setItem('pp.sidebar', JSON.stringify(store)); } catch (e) { }
            });
        });

        // Bootstrap tooltips
        if (window.bootstrap) {
            document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) { new bootstrap.Tooltip(el); });
        }

        // Table quick filter: <input data-table-filter="#tableId">
        document.querySelectorAll('input[data-table-filter]').forEach(function (inp) {
            var table = document.querySelector(inp.dataset.tableFilter);
            if (!table) return;
            inp.addEventListener('input', function () {
                var q = inp.value.toLowerCase();
                table.querySelectorAll('tbody tr').forEach(function (tr) {
                    tr.style.display = tr.textContent.toLowerCase().indexOf(q) >= 0 ? '' : 'none';
                });
            });
        });
    });

    // Chart.js defaults shared by every page
    if (window.Chart) {
        Chart.defaults.font.family = "'Inter', 'Segoe UI', system-ui, sans-serif";
        Chart.defaults.font.size = 11.5;
        Chart.defaults.color = '#64748b';
        Chart.defaults.plugins.legend.labels.boxWidth = 12;
        Chart.defaults.maintainAspectRatio = false;
    }

    // Shared palette for charts
    window.ppColors = {
        green: '#0d9e75', greenSoft: 'rgba(13,158,117,.15)',
        blue: '#3b82f6', blueSoft: 'rgba(59,130,246,.15)',
        amber: '#f59e0b', amberSoft: 'rgba(245,158,11,.15)',
        red: '#ef4444', redSoft: 'rgba(239,68,68,.15)',
        violet: '#8b5cf6', violetSoft: 'rgba(139,92,246,.15)',
        slate: '#64748b', slateSoft: 'rgba(100,116,139,.15)',
        series: ['#0d9e75', '#3b82f6', '#f59e0b', '#8b5cf6', '#ef4444', '#14b8a6', '#64748b', '#ec4899']
    };
    window.ppMoney = function (v) { return '৳' + Number(v || 0).toLocaleString('en-US', { maximumFractionDigits: 0 }); };
})();
