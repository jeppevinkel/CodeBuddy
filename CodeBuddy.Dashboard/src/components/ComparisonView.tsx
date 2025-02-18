import React from 'react';
import {
    Paper,
    Typography,
    Table,
    TableBody,
    TableCell,
    TableContainer,
    TableHead,
    TableRow,
    Box,
    Chip
} from '@mui/material';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import RemoveIcon from '@mui/icons-material/Remove';

interface MetricComparison {
    name: string;
    baseline: number;
    current: number;
    percentageChange: number;
    unit: string;
    isHigherBetter: boolean;
}

interface ComparisonViewProps {
    comparison: {
        language: string;
        testType: string;
        metrics: MetricComparison[];
        timestamp: string;
        baselineDate: string;
    };
}

export const ComparisonView: React.FC<ComparisonViewProps> = ({ comparison }) => {
    const getChangeIndicator = (change: number, isHigherBetter: boolean) => {
        const isImprovement = (change > 0 && isHigherBetter) || (change < 0 && !isHigherBetter);
        const color = Math.abs(change) < 1 ? 'default' : (isImprovement ? 'success' : 'error');
        
        let icon = <RemoveIcon />;
        if (Math.abs(change) >= 1) {
            icon = change > 0 ? <ArrowUpwardIcon /> : <ArrowDownwardIcon />;
        }

        return (
            <Chip
                icon={icon}
                label={`${change > 0 ? '+' : ''}${change.toFixed(2)}%`}
                color={color}
                size="small"
            />
        );
    };

    return (
        <Paper elevation={3} sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
                Performance Comparison
            </Typography>
            <Box sx={{ mb: 2 }}>
                <Typography variant="subtitle2" color="text.secondary">
                    Language: {comparison.language}
                </Typography>
                <Typography variant="subtitle2" color="text.secondary">
                    Test Type: {comparison.testType}
                </Typography>
                <Typography variant="subtitle2" color="text.secondary">
                    Baseline: {new Date(comparison.baselineDate).toLocaleDateString()}
                </Typography>
                <Typography variant="subtitle2" color="text.secondary">
                    Current: {new Date(comparison.timestamp).toLocaleDateString()}
                </Typography>
            </Box>
            <TableContainer>
                <Table size="small">
                    <TableHead>
                        <TableRow>
                            <TableCell>Metric</TableCell>
                            <TableCell align="right">Baseline</TableCell>
                            <TableCell align="right">Current</TableCell>
                            <TableCell align="right">Change</TableCell>
                        </TableRow>
                    </TableHead>
                    <TableBody>
                        {comparison.metrics.map((metric) => (
                            <TableRow key={metric.name}>
                                <TableCell component="th" scope="row">
                                    {metric.name}
                                </TableCell>
                                <TableCell align="right">
                                    {metric.baseline.toFixed(2)} {metric.unit}
                                </TableCell>
                                <TableCell align="right">
                                    {metric.current.toFixed(2)} {metric.unit}
                                </TableCell>
                                <TableCell align="right">
                                    {getChangeIndicator(metric.percentageChange, metric.isHigherBetter)}
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </TableContainer>
        </Paper>
    );
};