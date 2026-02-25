let elevationChart = null;
let hrChart = null;
let syncMarker = null;

window.initActivityCharts = function(trackData, mapId) {
    // trackData is array of [lat, lon, ele_or_null, hr_or_null, ts_ms_or_null]
    destroyActivityCharts();

    const hasElevation = trackData.some(p => p[2] != null);
    const hasHR = trackData.some(p => p[3] != null);

    // Build time labels (seconds from start, formatted as mm:ss)
    const t0 = trackData.find(p => p[4] != null)?.[4] ?? 0;
    const labels = trackData.map(p => {
        if (p[4] == null) return '';
        const secs = Math.round((p[4] - t0) / 1000);
        const m = Math.floor(secs / 60);
        const s = secs % 60;
        return `${m}:${s.toString().padStart(2,'0')}`;
    });

    // Helper: reduce to ~500 points max for performance
    function downsample(arr, maxPoints) {
        if (arr.length <= maxPoints) return { data: arr, indices: arr.map((_, i) => i) };
        const step = arr.length / maxPoints;
        const data = [];
        const indices = [];
        for (let i = 0; i < maxPoints; i++) {
            const idx = Math.round(i * step);
            data.push(arr[idx]);
            indices.push(idx);
        }
        return { data, indices };
    }

    const crosshairPlugin = {
        id: 'crosshair',
        afterDraw(chart, args, opts) {
            if (chart._activeCrosshairX == null) return;
            const ctx = chart.ctx;
            const x = chart._activeCrosshairX;
            const top = chart.chartArea.top;
            const bottom = chart.chartArea.bottom;
            ctx.save();
            ctx.setLineDash([5, 3]);
            ctx.strokeStyle = 'rgba(100,100,100,0.5)';
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(x, top);
            ctx.lineTo(x, bottom);
            ctx.stroke();
            ctx.restore();
        }
    };
    Chart.register(crosshairPlugin);

    function makeHoverHandler(charts) {
        return function(evt, active) {
            if (!active || active.length === 0) return;
            const idx = active[0].index;
            // Map marker
            if (syncMarker && trackData[idx]) {
                syncMarker.setLatLng([trackData[idx][0], trackData[idx][1]]);
            }
            // Sync crosshair on all charts
            charts.forEach(c => {
                if (!c) return;
                const meta = c.getDatasetMeta(0);
                if (meta.data[idx]) {
                    c._activeCrosshairX = meta.data[idx].x;
                } else {
                    c._activeCrosshairX = null;
                }
                c.draw();
            });
        };
    }

    // Create elevation chart
    if (hasElevation) {
        const eleVals = trackData.map(p => p[2]);
        const ds = downsample(eleVals, 500);
        const dsLabels = ds.indices.map(i => labels[i]);
        const ctx = document.getElementById('elevationChart');
        if (ctx) {
            elevationChart = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: dsLabels,
                    datasets: [{
                        label: 'Elevation (m)',
                        data: ds.data,
                        borderColor: 'rgb(75, 192, 192)',
                        backgroundColor: 'rgba(75, 192, 192, 0.1)',
                        fill: true,
                        pointRadius: 0,
                        tension: 0.3
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: false,
                    plugins: { legend: { display: false }, crosshair: {} },
                    scales: { x: { ticks: { maxTicksLimit: 10 } } },
                    onHover: null  // set after both charts created
                }
            });
        }
    }

    // Create HR chart
    if (hasHR) {
        const hrVals = trackData.map(p => p[3]);
        const ds = downsample(hrVals, 500);
        const dsLabels = ds.indices.map(i => labels[i]);
        const ctx = document.getElementById('hrChart');
        if (ctx) {
            hrChart = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: dsLabels,
                    datasets: [{
                        label: 'Heart Rate (bpm)',
                        data: ds.data,
                        borderColor: 'rgb(255, 99, 132)',
                        backgroundColor: 'rgba(255, 99, 132, 0.1)',
                        fill: true,
                        pointRadius: 0,
                        tension: 0.3
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: false,
                    plugins: { legend: { display: false }, crosshair: {} },
                    scales: { x: { ticks: { maxTicksLimit: 10 } } },
                    onHover: null
                }
            });
        }
    }

    // Now wire up hover handlers with references to all charts
    const allCharts = [elevationChart, hrChart].filter(c => c != null);
    const handler = makeHoverHandler(allCharts);
    allCharts.forEach(c => {
        c.options.onHover = handler;
        c.update();
    });

    // Create sync marker on Leaflet map (the map variable is `window._leafletMap`)
    if (window._leafletMap && trackData.length > 0) {
        syncMarker = L.circleMarker([trackData[0][0], trackData[0][1]], {
            radius: 8,
            color: '#e74c3c',
            fillColor: '#e74c3c',
            fillOpacity: 0.8
        }).addTo(window._leafletMap);
    }
};

window.destroyActivityCharts = function() {
    if (elevationChart) { elevationChart.destroy(); elevationChart = null; }
    if (hrChart) { hrChart.destroy(); hrChart = null; }
    if (syncMarker && window._leafletMap) { window._leafletMap.removeLayer(syncMarker); syncMarker = null; }
};
