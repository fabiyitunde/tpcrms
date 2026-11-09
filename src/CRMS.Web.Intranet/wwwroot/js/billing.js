// Charts for Billing (ApexCharts). One render call takes the whole model from Blazor.
window.billing = (function () {
    const charts = {};
    const categoryColors = {
        Identity: '#3b82f6', CreditBureau: '#2e7d32', BusinessBureau: '#14b8a6', FraudCheck: '#f59e0b',
        Advisory: '#8b5cf6', PartnerApi: '#f97316', Platform: '#64748b',
    };
    const outcomeColors = { Booked: '#2e7d32', Declined: '#ef4444', Cancelled: '#94a3b8', Received: '#3b82f6' };
    const base = {
        chart: { fontFamily: 'Inter, sans-serif', toolbar: { show: false }, animations: { enabled: true, speed: 450 }, background: 'transparent' },
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

    function money(v, ccy) { return (ccy || 'NGN') + ' ' + Number(v || 0).toLocaleString('en-NG', { maximumFractionDigits: 0 }); }

    function render(m) {
        const ccy = m.currency;

        // Due by month, stacked by category.
        const categories = m.categories; // [{ key, name }]
        make('chart-months', {
            chart: { type: 'bar', height: 300, stacked: true },
            series: categories.map(c => ({ name: c.name, data: m.months.map(x => Math.round((x.byCategory[c.key] || 0) * 100) / 100) })),
            colors: categories.map(c => categoryColors[c.key] || '#64748b'),
            xaxis: { categories: m.months.map(x => x.label), labels: { style: { colors: '#6b7280' } } },
            yaxis: { labels: { style: { colors: '#6b7280' }, formatter: v => Number(v).toLocaleString('en-NG', { maximumFractionDigits: 0 }) } },
            plotOptions: { bar: { columnWidth: '55%', borderRadius: 4 } },
            tooltip: { y: { formatter: v => money(v, ccy) } },
        });

        // Loans by flow and outcome for the month.
        make('chart-loans', {
            chart: { type: 'bar', height: 300, stacked: true },
            series: [
                { name: 'Received', data: m.loans.map(l => l.received) },
                { name: 'Booked', data: m.loans.map(l => l.booked) },
                { name: 'Declined', data: m.loans.map(l => l.declined) },
                { name: 'Cancelled', data: m.loans.map(l => l.cancelled) },
            ],
            colors: [outcomeColors.Received, outcomeColors.Booked, outcomeColors.Declined, outcomeColors.Cancelled],
            xaxis: { categories: m.loans.map(l => l.flow), labels: { style: { colors: '#6b7280' } } },
            yaxis: { labels: { style: { colors: '#6b7280' }, formatter: v => Math.round(v) }, forceNiceScale: true },
            plotOptions: { bar: { columnWidth: '45%', borderRadius: 4 } },
        });

        // Category share of the month's amount.
        const share = m.byCategory.filter(c => c.amount > 0);
        if (share.length) {
            make('chart-share', {
                chart: { type: 'donut', height: 260 },
                series: share.map(c => Math.round(c.amount * 100) / 100),
                labels: share.map(c => c.name),
                colors: share.map(c => categoryColors[c.key] || '#64748b'),
                plotOptions: { pie: { donut: { size: '68%', labels: { show: true, total: { show: true, label: 'Due', formatter: w => money(w.globals.seriesTotals.reduce((a, b) => a + b, 0), ccy) } } } } },
                tooltip: { y: { formatter: v => money(v, ccy) } },
            });
        } else {
            destroyOne('chart-share');
        }

        // Calls by service for the month.
        const services = m.byService.slice(0, 10);
        make('chart-services', {
            chart: { type: 'bar', height: Math.max(200, 34 * services.length + 60) },
            series: [{ name: 'Successful', data: services.map(s => s.count - s.failed) }, { name: 'Failed', data: services.map(s => s.failed) }],
            colors: ['#2e7d32', '#ef4444'],
            chartOptions: {},
            plotOptions: { bar: { horizontal: true, barHeight: '60%', borderRadius: 3 } },
            xaxis: { categories: services.map(s => s.name), labels: { style: { colors: '#6b7280' }, formatter: v => Math.round(v) } },
            yaxis: { labels: { style: { colors: '#6b7280' }, maxWidth: 220 } },
            legend: { position: 'top' },
        });
        if (!services.length) destroyOne('chart-services');
    }

    function destroyOne(id) {
        if (charts[id]) { try { charts[id].destroy(); } catch (e) { } delete charts[id]; }
    }

    function destroy() {
        for (const k of Object.keys(charts)) destroyOne(k);
    }

    return { render, destroy };
})();
