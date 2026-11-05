// Charts for Admin > Partner API (ApexCharts). One render call takes the whole model from Blazor,
// so the page makes a single interop round-trip per refresh.
window.partnerApi = (function () {
    const charts = {};
    const green = ['#1a5f2a', '#2e7d32', '#4ade80', '#86efac'];
    const palette = ['#2e7d32', '#3b82f6', '#f59e0b', '#8b5cf6', '#14b8a6', '#ef4444', '#f97316', '#64748b'];
    const ratingColors = { VeryLow: '#1a5f2a', Low: '#4ade80', Medium: '#f59e0b', High: '#f97316', VeryHigh: '#ef4444' };
    const outcomeColors = { OK: '#2e7d32', NO_RECORD: '#94a3b8', UNAVAILABLE: '#ef4444', NOT_ALLOWED: '#f59e0b', ERROR: '#b91c1c' };
    const base = {
        chart: { fontFamily: 'Inter, sans-serif', toolbar: { show: false }, animations: { enabled: true, speed: 500 }, background: 'transparent' },
        grid: { borderColor: '#eef2f7', strokeDashArray: 4, padding: { left: 8, right: 8 } },
        dataLabels: { enabled: false },
        legend: { position: 'bottom', fontSize: '12px', markers: { radius: 12 } },
        tooltip: { theme: 'light' },
    };

    function merge(a, b) {
        const out = Array.isArray(a) ? a.slice() : Object.assign({}, a);
        for (const k of Object.keys(b)) {
            out[k] = b[k] && typeof b[k] === 'object' && !Array.isArray(b[k]) && a && typeof a[k] === 'object' && !Array.isArray(a[k]) ? merge(a[k], b[k]) : b[k];
        }
        return out;
    }

    function make(id, options) {
        const el = document.getElementById(id);
        if (!el || typeof ApexCharts === 'undefined') return;
        if (charts[id]) { try { charts[id].destroy(); } catch (e) { } }
        charts[id] = new ApexCharts(el, merge(base, options));
        charts[id].render();
    }

    function spark(id, data, color) {
        make(id, {
            chart: { type: 'area', height: 46, sparkline: { enabled: true } },
            series: [{ name: 'value', data: data }],
            stroke: { curve: 'smooth', width: 2 },
            fill: { type: 'gradient', gradient: { shadeIntensity: 1, opacityFrom: 0.35, opacityTo: 0.02, stops: [0, 100] } },
            colors: [color],
            tooltip: { enabled: false },
        });
    }

    // Integer axis: whole-number ticks, never a repeated label on a small range.
    function intAxis(values) {
        const max = Math.max(4, ...values, 0);
        const step = Math.ceil(max / 4);
        return { min: 0, max: step * 4, tickAmount: 4, labels: { style: { colors: '#6b7280' }, formatter: v => Math.round(v) } };
    }

    function money(v, ccy) { return (ccy || 'NGN') + ' ' + Number(v || 0).toLocaleString('en-NG', { maximumFractionDigits: 0 }); }

    function render(m) {
        const dates = m.daily.map(d => d.date);
        spark('spark-checks', m.daily.map(d => d.submitted), '#2e7d32');
        spark('spark-completion', m.daily.map(d => d.submitted ? Math.round(100 * d.completed / d.submitted) : 0), '#3b82f6');
        spark('spark-calls', m.daily.map(d => d.billedCalls), '#8b5cf6');
        spark('spark-amount', m.daily.map(d => d.amount), '#f59e0b');
        spark('spark-failed', m.daily.map(d => d.failed), '#ef4444');

        const consumerSeries = m.consumerNames.length
            ? m.consumerNames.map(n => ({ name: n, data: m.daily.map(d => d.byConsumer[n] || 0) }))
            : [{ name: 'Checks', data: m.daily.map(d => d.submitted) }];
        make('chart-daily', {
            chart: { type: 'area', height: 300, stacked: true },
            series: consumerSeries,
            colors: palette,
            stroke: { curve: 'smooth', width: 2 },
            fill: { type: 'gradient', gradient: { opacityFrom: 0.45, opacityTo: 0.05 } },
            xaxis: { categories: dates, type: 'datetime', labels: { datetimeUTC: false, style: { colors: '#6b7280' } }, axisBorder: { show: false }, axisTicks: { show: false } },
            yaxis: { labels: { style: { colors: '#6b7280' }, formatter: v => Math.round(v) }, min: 0, forceNiceScale: true },
            tooltip: { x: { format: 'dd MMM yyyy' }, shared: true, intersect: false },
            noData: { text: 'No checks in this period' },
        });

        make('chart-outcomes', {
            chart: { type: 'donut', height: 300 },
            series: m.sectionOutcomes.map(o => o.count),
            labels: m.sectionOutcomes.map(o => o.name.replace('_', ' ')),
            colors: m.sectionOutcomes.map(o => outcomeColors[o.name] || '#cbd5e1'),
            stroke: { width: 2, colors: ['#fff'] },
            plotOptions: { pie: { donut: { size: '70%', labels: { show: true, total: { show: true, label: 'Section answers', fontSize: '12px', color: '#6b7280', formatter: w => w.globals.seriesTotals.reduce((a, b) => a + b, 0) } } } } },
            dataLabels: { enabled: false },
            noData: { text: 'No completed checks' },
        });

        make('chart-ratings', {
            chart: { type: 'bar', height: 240 },
            series: [{ name: 'Checks', data: m.advisoryRatings.map(r => r.count) }],
            xaxis: { categories: m.advisoryRatings.map(r => r.name.replace(/([a-z])([A-Z])/g, '$1 $2') + ' risk'), labels: { style: { colors: '#6b7280' } }, axisBorder: { show: false } },
            yaxis: intAxis(m.advisoryRatings.map(r => r.count)),
            colors: m.advisoryRatings.map(r => ratingColors[r.name] || '#94a3b8'),
            plotOptions: { bar: { distributed: true, borderRadius: 6, columnWidth: '55%' } },
            legend: { show: false },
            noData: { text: 'No advisories scored' },
        });

        make('chart-bureau', {
            chart: { type: 'bar', height: 240 },
            series: [{ name: 'Checks', data: m.bureauUsage.map(b => b.count) }],
            xaxis: Object.assign(intAxis(m.bureauUsage.map(b => b.count)), { categories: m.bureauUsage.map(b => b.name) }),
            yaxis: { labels: { style: { colors: '#6b7280' } } },
            colors: m.bureauUsage.map(b => b.name.endsWith('reused') ? '#86efac' : '#2e7d32'),
            plotOptions: { bar: { horizontal: true, distributed: true, borderRadius: 6, barHeight: '55%' } },
            legend: { show: false },
            noData: { text: 'No bureau calls' },
        });

        const cb = m.callbacks;
        const pct = (n) => cb.requested ? Math.round(100 * n / cb.requested) : 0;
        make('chart-callbacks', {
            chart: { type: 'radialBar', height: 260 },
            series: [pct(cb.deliveredFirstAttempt), pct(cb.delivered)],
            labels: ['First attempt', 'Eventually'],
            colors: ['#4ade80', '#2e7d32'],
            plotOptions: { radialBar: { hollow: { size: '45%' }, track: { background: '#f3f4f6' }, dataLabels: { name: { fontSize: '12px', color: '#6b7280' }, value: { fontSize: '20px', fontWeight: 600, formatter: v => v + '%' }, total: { show: true, label: 'Delivered', formatter: () => pct(cb.delivered) + '%' } } } },
            legend: { show: true, position: 'bottom' },
        });

        const days = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
        make('chart-heatmap', {
            chart: { type: 'heatmap', height: 260 },
            series: m.heatmap.map((row, i) => ({ name: days[i], data: row.map((v, h) => ({ x: (h < 10 ? '0' : '') + h + ':00', y: v })) })).reverse(),
            colors: ['#2e7d32'],
            plotOptions: { heatmap: { radius: 3, shadeIntensity: 0.6, colorScale: { ranges: [{ from: 0, to: 0, color: '#f3f4f6', name: 'none' }] } } },
            xaxis: { labels: { rotate: 0, style: { colors: '#9ca3af', fontSize: '10px' }, formatter: v => (parseInt(v, 10) % 3 === 0 ? v : '') } },
            yaxis: { labels: { style: { colors: '#6b7280' } } },
            legend: { show: false },
            tooltip: { y: { formatter: v => v + ' check' + (v === 1 ? '' : 's') } },
        });
    }

    function destroy() {
        for (const k of Object.keys(charts)) { try { charts[k].destroy(); } catch (e) { } delete charts[k]; }
    }

    async function copyText(text) {
        try { await navigator.clipboard.writeText(text); return true; } catch (e) { return false; }
    }

    return { render, destroy, copyText, money };
})();
