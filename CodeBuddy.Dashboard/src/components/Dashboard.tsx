import React, { useEffect, useState } from 'react';
import { Box, Grid, Typography } from '@mui/material';
import MetricsChart from './MetricsChart';
import TrendAnalysis from './TrendAnalysis';
import AlertPanel from './AlertPanel';
import ComparisonView from './ComparisonView';
import FilterPanel from './FilterPanel';
import DrillDownPanel from './DrillDownPanel';
import { useWebSocket } from '../hooks/useWebSocket';
import { usePerformanceMetrics } from '../hooks/usePerformanceMetrics';

export const Dashboard: React.FC = () => {
    const [filter, setFilter] = useState({
        language: '',
        testType: '',
        startDate: null,
        endDate: null
    });

    const { 
        metrics,
        trends,
        alerts,
        comparison,
        isLoading,
        error
    } = usePerformanceMetrics(filter);

    const { latestMetrics } = useWebSocket('/ws/metrics');

    const handleFilterChange = (newFilter) => {
        setFilter(newFilter);
    };

    const handleDrillDown = (metricDetails) => {
        // Open drill-down panel with detailed analysis
    };

    const handleExport = async () => {
        const response = await fetch('/api/dashboard/export?' + new URLSearchParams(filter));
        const blob = await response.blob();
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'performance-report.json';
        a.click();
    };

    if (error) {
        return <Typography color="error">{error}</Typography>;
    }

    return (
        <Box sx={{ p: 3 }}>
            <Grid container spacing={3}>
                <Grid item xs={12}>
                    <FilterPanel onChange={handleFilterChange} />
                </Grid>
                
                <Grid item xs={12} md={8}>
                    <MetricsChart 
                        data={metrics} 
                        realTimeData={latestMetrics}
                        onDrillDown={handleDrillDown}
                    />
                </Grid>
                
                <Grid item xs={12} md={4}>
                    <AlertPanel alerts={alerts} />
                </Grid>
                
                <Grid item xs={12} md={6}>
                    <TrendAnalysis trends={trends} />
                </Grid>
                
                <Grid item xs={12} md={6}>
                    <ComparisonView comparison={comparison} />
                </Grid>
                
                <Grid item xs={12}>
                    <DrillDownPanel />
                </Grid>
            </Grid>
        </Box>
    );
};