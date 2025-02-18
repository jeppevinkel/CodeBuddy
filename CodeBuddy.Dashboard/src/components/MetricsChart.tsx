import React, { useEffect, useRef } from 'react';
import { Line } from 'react-chartjs-2';
import { Box, Paper, Typography } from '@mui/material';

interface MetricsChartProps {
    data: any[];
    realTimeData: any;
    onDrillDown: (details: any) => void;
}

const MetricsChart: React.FC<MetricsChartProps> = ({ data, realTimeData, onDrillDown }) => {
    const chartRef = useRef(null);

    const chartData = {
        labels: data.map(d => new Date(d.timestamp).toLocaleTimeString()),
        datasets: [
            {
                label: 'CPU Utilization (%)',
                data: data.map(d => d.cpuUtilization),
                borderColor: 'rgb(75, 192, 192)',
                tension: 0.1
            },
            {
                label: 'Memory Usage (%)',
                data: data.map(d => d.memoryUsage),
                borderColor: 'rgb(255, 99, 132)',
                tension: 0.1
            },
            {
                label: 'Execution Time (ms)',
                data: data.map(d => d.executionTime),
                borderColor: 'rgb(153, 102, 255)',
                tension: 0.1
            }
        ]
    };

    const options = {
        responsive: true,
        interaction: {
            mode: 'index' as const,
            intersect: false,
        },
        plugins: {
            tooltip: {
                callbacks: {
                    label: (context) => {
                        const label = context.dataset.label || '';
                        const value = context.parsed.y;
                        return `${label}: ${value.toFixed(2)}`;
                    }
                }
            }
        },
        onClick: (event, elements) => {
            if (elements.length > 0) {
                const index = elements[0].index;
                onDrillDown(data[index]);
            }
        },
        scales: {
            y: {
                beginAtZero: true
            }
        }
    };

    useEffect(() => {
        if (realTimeData && chartRef.current) {
            const chart = chartRef.current;
            // Update chart with real-time data
            chartData.labels.push(new Date(realTimeData.timestamp).toLocaleTimeString());
            chartData.datasets[0].data.push(realTimeData.cpuUtilization);
            chartData.datasets[1].data.push(realTimeData.memoryUsage);
            chartData.datasets[2].data.push(realTimeData.executionTime);
            
            // Remove oldest data point if we have too many
            if (chartData.labels.length > 50) {
                chartData.labels.shift();
                chartData.datasets.forEach(dataset => dataset.data.shift());
            }
            
            chart.update('quiet');
        }
    }, [realTimeData]);

    return (
        <Paper elevation={3} sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
                Performance Metrics
            </Typography>
            <Box sx={{ height: 400 }}>
                <Line ref={chartRef} data={chartData} options={options} />
            </Box>
        </Paper>
    );
};