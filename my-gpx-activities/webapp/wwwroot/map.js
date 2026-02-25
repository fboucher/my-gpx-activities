// GPX Map Visualization
let map = null;
let routeLayer = null;
let startMarker = null;
let endMarker = null;

window.initializeMap = (trackCoordinates) => {
    // Initialize map if not already done
    if (!map) {
        map = L.map('map').setView([45.377, -73.928], 13);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap contributors'
        }).addTo(map);
    }

    // Clear existing route and markers
    if (routeLayer) {
        map.removeLayer(routeLayer);
        routeLayer = null;
    }
    if (startMarker) {
        map.removeLayer(startMarker);
        startMarker = null;
    }
    if (endMarker) {
        map.removeLayer(endMarker);
        endMarker = null;
    }

    // Create route from coordinates array
    if (trackCoordinates && trackCoordinates.length > 0) {
        try {
            let bounds = null;

            // Build bounds from coordinates
            trackCoordinates.forEach(coord => {
                const [lat, lon] = coord;
                if (!isNaN(lat) && !isNaN(lon)) {
                    if (!bounds) {
                        bounds = L.latLngBounds([lat, lon], [lat, lon]);
                    } else {
                        bounds.extend([lat, lon]);
                    }
                }
            });

            if (trackCoordinates.length > 1) {
                // Create route polyline
                routeLayer = L.polyline(trackCoordinates, {
                    color: '#FF5722',
                    weight: 4,
                    opacity: 0.8
                }).addTo(map);

                // Add start marker (green)
                startMarker = L.marker(trackCoordinates[0], {
                    icon: L.divIcon({
                        className: 'start-marker',
                        html: '<div style="background: #4CAF50; border-radius: 50%; width: 20px; height: 20px; border: 3px solid white; box-shadow: 0 0 5px rgba(0,0,0,0.5);"></div>',
                        iconSize: [20, 20],
                        iconAnchor: [10, 10]
                    })
                }).addTo(map).bindPopup('Start');

                // Add end marker (red)
                endMarker = L.marker(trackCoordinates[trackCoordinates.length - 1], {
                    icon: L.divIcon({
                        className: 'end-marker',
                        html: '<div style="background: #F44336; border-radius: 50%; width: 20px; height: 20px; border: 3px solid white; box-shadow: 0 0 5px rgba(0,0,0,0.5);"></div>',
                        iconSize: [20, 20],
                        iconAnchor: [10, 10]
                    })
                }).addTo(map).bindPopup('Finish');

                // Fit map to route bounds
                if (bounds) {
                    map.fitBounds(bounds, { padding: [20, 20] });
                }
            } else if (trackCoordinates.length === 1) {
                // Single point - just center on it
                map.setView(trackCoordinates[0], 15);
                startMarker = L.marker(trackCoordinates[0]).addTo(map).bindPopup('Activity location');
            }
        } catch (error) {
            console.error('Error creating map route:', error);
            showFallbackMarker();
        }
    } else {
        showFallbackMarker();
    }

    // Force map resize
    setTimeout(() => {
        map.invalidateSize();
    }, 100);
};

function showFallbackMarker() {
    L.marker([45.377, -73.928]).addTo(map)
        .bindPopup('No route data available')
        .openPopup();
}

// Cleanup function for when component is disposed
window.destroyMap = () => {
    if (map) {
        map.remove();
        map = null;
        routeLayer = null;
        startMarker = null;
        endMarker = null;
    }
};

// ── Heat Map ─────────────────────────────────────────────────────────────────
let heatMap = null;
let heatMapLayers = [];

const sportColors = [
    '#E53935', '#8E24AA', '#1E88E5', '#00ACC1',
    '#43A047', '#FB8C00', '#F4511E', '#6D4C41',
    '#546E7A', '#D81B60'
];

function heatMapSportColor(sportType, colorMap) {
    if (colorMap[sportType]) return colorMap[sportType];
    const keys = Object.keys(colorMap);
    const color = sportColors[keys.length % sportColors.length];
    colorMap[sportType] = color;
    return color;
}

window.initializeHeatMap = (elementId) => {
    if (heatMap) {
        heatMap.remove();
        heatMap = null;
        heatMapLayers = [];
    }
    heatMap = L.map(elementId).setView([45.0, -73.0], 6);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap contributors'
    }).addTo(heatMap);
    setTimeout(() => heatMap.invalidateSize(), 100);
};

window.drawHeatMapTraces = (activities) => {
    if (!heatMap) return;

    heatMapLayers.forEach(l => heatMap.removeLayer(l));
    heatMapLayers = [];

    if (!activities || activities.length === 0) return;

    const colorMap = {};
    const bounds = L.latLngBounds([]);

    activities.forEach(activity => {
        const pts = activity.trackPoints;
        if (!pts || pts.length < 2) return;

        const color = heatMapSportColor(activity.sportType, colorMap);
        const latlngs = pts.map(p => [p[0], p[1]]);

        const polyline = L.polyline(latlngs, {
            color,
            weight: 2,
            opacity: 0.7
        }).bindTooltip(`${activity.activityName} (${activity.sportType})`, { sticky: true });

        polyline.addTo(heatMap);
        heatMapLayers.push(polyline);
        bounds.extend(latlngs);
    });

    if (bounds.isValid()) {
        heatMap.fitBounds(bounds, { padding: [20, 20] });
    }
};

window.destroyHeatMap = () => {
    if (heatMap) {
        heatMap.remove();
        heatMap = null;
        heatMapLayers = [];
    }
};
