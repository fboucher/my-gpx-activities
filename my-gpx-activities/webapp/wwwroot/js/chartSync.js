let elevationChart = null;
let paceChart = null;
let hrChart = null;
let cadenceChart = null;
let syncMarker = null;

window.initActivityCharts = function(trackData, mapId) {
    // trackData: [lat, lon, ele_or_null, hr_or_null, ts_ms_or_null, cadence_or_null (optional)]
    destroyActivityCharts();

    const hasElevation = trackData.some(p => p[2] != null);
    const hasHR = trackData.some(p => p[3] != null);
    const hasTimestamps = trackData.some(p => p[4] != null);
    const hasCadence = trackData.some(p => p.length > 5 && p[5] != null);

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
    // Returns { data: downsampled array, indices: original indices for each downsampled point }
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
    
    // Store downsampling index maps for reverse lookup (chart index -> trackData index)
    let dsIndexMaps = { elevation: null, pace: null, hr: null, cadence: null };

    // Haversine distance between two lat/lon points in meters
    function haversine(lat1, lon1, lat2, lon2) {
        const R = 6371000;
        const toRad = x => x * Math.PI / 180;
        const dLat = toRad(lat2 - lat1);
        const dLon = toRad(lon2 - lon1);
        const a = Math.sin(dLat/2)**2 + Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLon/2)**2;
        return R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    }

    // Compute pace (min/km) per trackpoint with 10-point rolling average
    function computePace() {
        const rawPace = new Array(trackData.length).fill(null);
        for (let i = 1; i < trackData.length; i++) {
            const p0 = trackData[i - 1], p1 = trackData[i];
            if (p0[4] == null || p1[4] == null) continue;
            const dtSec = (p1[4] - p0[4]) / 1000;
            if (dtSec <= 0) continue;
            const dist = haversine(p0[0], p0[1], p1[0], p1[1]);
            if (dist <= 0) continue;
            const mps = dist / dtSec;
            const minPerKm = 1000 / (mps * 60);
            rawPace[i] = minPerKm <= 30 ? minPerKm : null; // clamp outliers (stopped/GPS noise)
        }
        // Rolling average over ~10 points (±5 window)
        const smoothed = new Array(trackData.length).fill(null);
        for (let i = 0; i < trackData.length; i++) {
            const win = [];
            for (let j = Math.max(0, i - 4); j <= Math.min(trackData.length - 1, i + 5); j++) {
                if (rawPace[j] != null) win.push(rawPace[j]);
            }
            if (win.length > 0) smoothed[i] = win.reduce((a, b) => a + b, 0) / win.length;
        }
        return smoothed;
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

    function makeHoverHandler(charts, indexMap) {
        return function(evt, active) {
            if (!active || active.length === 0) return;
            const chartIdx = active[0].index;
            // Map chart index back to original trackData index
            const originalIdx = indexMap ? indexMap[chartIdx] : chartIdx;
            // Map marker — use original trackData index
            if (syncMarker && trackData[originalIdx]) {
                syncMarker.setLatLng([trackData[originalIdx][0], trackData[originalIdx][1]]);
            }
            // Sync crosshair on all charts
            charts.forEach(c => {
                if (!c) return;
                const meta = c.getDatasetMeta(0);
                if (meta.data[chartIdx]) {
                    c._activeCrosshairX = meta.data[chartIdx].x;
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
        dsIndexMaps.elevation = ds.indices;
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
                    interaction: {
                        mode: 'index',
                        axis: 'x',
                        intersect: false
                    },
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
        dsIndexMaps.hr = ds.indices;
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
                    interaction: {
                        mode: 'index',
                        axis: 'x',
                        intersect: false
                    },
                    plugins: { legend: { display: false }, crosshair: {} },
                    scales: { x: { ticks: { maxTicksLimit: 10 } } },
                    onHover: null
                }
            });
        }
    }

    // Create pace chart (requires timestamps)
    if (hasTimestamps) {
        const paceVals = computePace();
        const ds = downsample(paceVals, 500);
        dsIndexMaps.pace = ds.indices;
        const dsLabels = ds.indices.map(i => labels[i]);
        const ctx = document.getElementById('paceChart');
        if (ctx) {
            paceChart = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: dsLabels,
                    datasets: [{
                        label: 'Pace (min/km)',
                        data: ds.data,
                        borderColor: 'rgb(234, 179, 8)',
                        backgroundColor: 'rgba(234, 179, 8, 0.1)',
                        fill: true,
                        pointRadius: 0,
                        tension: 0.3,
                        spanGaps: true
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: false,
                    interaction: {
                        mode: 'index',
                        axis: 'x',
                        intersect: false
                    },
                    plugins: { legend: { display: false }, crosshair: {} },
                    scales: {
                        x: { ticks: { maxTicksLimit: 10 } },
                        y: {
                            reverse: true,
                            ticks: {
                                callback: v => {
                                    if (v == null) return '';
                                    const m = Math.floor(v);
                                    const s = Math.round((v - m) * 60).toString().padStart(2, '0');
                                    return `${m}:${s}`;
                                }
                            }
                        }
                    },
                    onHover: null
                }
            });
        }
    }

    // Create cadence chart (element index 5 — null-safe for older 5-element data)
    if (hasCadence) {
        const cadVals = trackData.map(p => p.length > 5 ? p[5] : null);
        const ds = downsample(cadVals, 500);
        dsIndexMaps.cadence = ds.indices;
        const dsLabels = ds.indices.map(i => labels[i]);
        const ctx = document.getElementById('cadenceChart');
        if (ctx) {
            cadenceChart = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: dsLabels,
                    datasets: [{
                        label: 'Cadence (rpm)',
                        data: ds.data,
                        borderColor: 'rgb(168, 85, 247)',
                        backgroundColor: 'rgba(168, 85, 247, 0.1)',
                        fill: true,
                        pointRadius: 0,
                        tension: 0.3,
                        spanGaps: true
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: false,
                    interaction: {
                        mode: 'index',
                        axis: 'x',
                        intersect: false
                    },
                    plugins: { legend: { display: false }, crosshair: {} },
                    scales: { x: { ticks: { maxTicksLimit: 10 } } },
                    onHover: null
                }
            });
        }
    }

    // Now wire up hover handlers with references to all charts
    const allCharts = [elevationChart, paceChart, hrChart, cadenceChart].filter(c => c != null);
    // Use the first available index map (all should be consistent since they come from same trackData)
    const indexMap = dsIndexMaps.elevation || dsIndexMaps.pace || dsIndexMaps.hr || dsIndexMaps.cadence;
    const handler = makeHoverHandler(allCharts, indexMap);
    allCharts.forEach(c => {
        c.options.onHover = handler;
        c.update();
    });

    // Create sync marker on Leaflet map (the map variable is `window._leafletMap`)
    if (window._leafletMap && trackData.length > 0) {
        syncMarker = L.circleMarker([trackData[0][0], trackData[0][1]], {
            radius: 8,
            color: '#2563eb',
            fillColor: '#2563eb',
            fillOpacity: 0.8
        }).addTo(window._leafletMap);
    }
};

window.destroyActivityCharts = function() {
    if (elevationChart) { elevationChart.destroy(); elevationChart = null; }
    if (paceChart) { paceChart.destroy(); paceChart = null; }
    if (hrChart) { hrChart.destroy(); hrChart = null; }
    if (cadenceChart) { cadenceChart.destroy(); cadenceChart = null; }
    if (syncMarker && window._leafletMap) { window._leafletMap.removeLayer(syncMarker); syncMarker = null; }
};
