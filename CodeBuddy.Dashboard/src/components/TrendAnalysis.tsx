import React from 'react';
import { Paper, Typography, Grid } from '@mui/material';
import { Line } from 'react-chartjs-2';

interface TrendAnalysisProps {
    trends: {
        language: string;
        testType: string;
        cpuUtilizationTrend: number[];
        memoryUsageTrend: number[];
        executionTimeTrend: number[];
        timestamps: string[];
    }[];
}

export const TrendAnalysis: React.FC<TrendAnalysisProps> = ({ trends }) => {
    const createChartData = (trend: typeof trends[0]) => ({
        labels: trend.timestamps,
        datasets: [
            {
                label: 'CPU Utilization Trend',
                data: trend.cpuUtilizationTrend,
                borderColor: 'rgb(75, 192, 192)',
                tension: 0.1
            },
            {
                label: 'Memory Usage Trend',
                data: trend.memoryUsageTrend,
                borderColor: 'rgb(255, 99, 132)',
                tension: 0.1
            },
            {
                label: 'Execution Time Trend',
                data: trend.executionTimeTrend,
                borderColor: 'rgb(153, 102, 255)',
                tension: 0.1
            }
        ]
    });

    const options = {
        responsive: true,
        plugins: {
            title: {
                display: true,
                text: 'Historical Performance Trends'
            },
            tooltip: {
                mode: 'index' as const,
                intersect: false,
            }
        },
        scales: {
            y: {
                beginAtZero: true
            }
        }
    };

    return (
        <Paper elevation={3} sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
                Trend Analysis
            </Typography>
            <Grid container spacing={3}>
                {trends.map((trend, index) => (
                    <Grid item xs={12} key={`${trend.language}-${trend.testType}`}>
                        <Typography variant="subtitle1">
                            {trend.language} - {trend.testType}
                        </Typography>
                        <Line data={createChartData(trend)} options={options} />
                    </Grid>
                ))}
            </Grid>
        </Paper>
    );
};