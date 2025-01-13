const connection = new signalR.HubConnectionBuilder()
    .withUrl("/metrics-hub")
    .withAutomaticReconnect()
    .build();

connection.on("MetricsUpdate", (data) => {
    updateMetricsDisplay(data);
    updateCharts(data);
});

function updateMetricsDisplay(data) {
    // Update system metrics
    document.getElementById("cpu-usage").textContent = 
        `${data.system.cpuUsage.toFixed(1)}%`;
    document.getElementById("memory-usage").textContent = 
        formatBytes(data.system.memoryUsage);
    document.getElementById("thread-count").textContent = 
        data.system.threadCount;

    // Update request metrics
    document.getElementById("total-requests").textContent = 
        data.requests.total;
    document.getElementById("error-rate").textContent = 
        `${((data.requests.errors / data.requests.total) * 100).toFixed(2)}%`;
    document.getElementById("avg-duration").textContent = 
        `${data.requests.averageDuration.toFixed(2)}ms`;

    // Update cache metrics
    const hitRate = data.cache.hits / (data.cache.hits + data.cache.misses) * 100;
    document.getElementById("cache-hit-rate").textContent = 
        `${hitRate.toFixed(1)}%`;
    document.getElementById("cache-size").textContent = 
        formatBytes(data.cache.size);

    // Update timestamps
    document.getElementById("last-update").textContent = 
        new Date(data.timestamp).toLocaleTimeString();
}

function updateCharts(data) {
    // Update request rate chart
    Plotly.extendTraces("requestChart", {
        y: [[data.requests.total]],
        x: [[new Date(data.timestamp)]]
    }, [0]);

    // Update error rate chart
    Plotly.extendTraces("errorChart", {
        y: [[data.requests.errors]],
        x: [[new Date(data.timestamp)]]
    }, [0]);

    // Update latency chart
    Plotly.extendTraces("latencyChart", {
        y: [[data.requests.averageDuration]],
        x: [[new Date(data.timestamp)]]
    }, [0]);

    // Limit the number of points shown
    const maxPoints = 100;
    if (document.getElementById("requestChart").data[0].x.length > maxPoints) {
        Plotly.relayout("requestChart", {
            xaxis: {
                range: [
                    new Date(data.timestamp - 5 * 60 * 1000),
                    new Date(data.timestamp)
                ]
            }
        });
    }
}

function formatBytes(bytes) {
    const units = ['B', 'KB', 'MB', 'GB'];
    let value = bytes;
    let unitIndex = 0;
    
    while (value >= 1024 && unitIndex < units.length - 1) {
        value /= 1024;
        unitIndex++;
    }
    
    return `${value.toFixed(1)} ${units[unitIndex]}`;
}

// Start the connection
connection.start()
    .then(() => console.log("Connected to metrics hub"))
    .catch(err => console.error("Error connecting to metrics hub:", err)); 