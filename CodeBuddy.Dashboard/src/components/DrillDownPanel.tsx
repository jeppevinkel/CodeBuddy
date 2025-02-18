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
    Tabs,
    Tab,
    Grid
} from '@mui/material';
import { Line } from 'react-chartjs-2';

interface DrillDownPanelProps {
    data?: any;
    isOpen: boolean;
    onClose: () => void;
}

interface TabPanelProps {
    children?: React.ReactNode;
    index: number;
    value: number;
}

const TabPanel = (props: TabPanelProps) => {
    const { children, value, index, ...other } = props;
    return (
        <div hidden={value !== index} {...other}>
            {value === index && <Box sx={{ p: 3 }}>{children}</Box>}
        </div>
    );
};

export const DrillDownPanel: React.FC<DrillDownPanelProps> = ({ data, isOpen, onClose }) => {
    const [tabValue, setTabValue] = React.useState(0);

    if (!data || !isOpen) return null;

    const handleTabChange = (event: React.SyntheticEvent, newValue: number) => {
        setTabValue(newValue);
    };

    const detailedMetrics = {
        labels: data.timeline.map((t: any) => new Date(t.timestamp).toLocaleTimeString()),
        datasets: [
            {
                label: 'CPU Usage Breakdown',
                data: data.timeline.map((t: any) => t.cpuBreakdown),
                borderColor: 'rgb(75, 192, 192)',
            },
            {
                label: 'Memory Allocation',
                data: data.timeline.map((t: any) => t.memoryAllocation),
                borderColor: 'rgb(255, 99, 132)',
            },
            {
                label: 'GC Collection Time',
                data: data.timeline.map((t: any) => t.gcCollectionTime),
                borderColor: 'rgb(153, 102, 255)',
            }
        ]
    };

    return (
        <Paper elevation={3} sx={{ p: 2, position: 'absolute', top: '10%', left: '10%', right: '10%', bottom: '10%', zIndex: 1000 }}>
            <Typography variant="h6" gutterBottom>
                Detailed Performance Analysis
            </Typography>

            <Tabs value={tabValue} onChange={handleTabChange}>
                <Tab label="Overview" />
                <Tab label="Resource Usage" />
                <Tab label="Timeline" />
                <Tab label="Anomalies" />
            </Tabs>

            <TabPanel value={tabValue} index={0}>
                <Grid container spacing={3}>
                    <Grid item xs={12} md={6}>
                        <TableContainer>
                            <Table>
                                <TableHead>
                                    <TableRow>
                                        <TableCell>Metric</TableCell>
                                        <TableCell>Value</TableCell>
                                        <TableCell>Status</TableCell>
                                    </TableRow>
                                </TableHead>
                                <TableBody>
                                    <TableRow>
                                        <TableCell>Peak CPU Usage</TableCell>
                                        <TableCell>{data.peakCpuUsage}%</TableCell>
                                        <TableCell>{data.cpuStatus}</TableCell>
                                    </TableRow>
                                    <TableRow>
                                        <TableCell>Memory Leak Probability</TableCell>
                                        <TableCell>{data.memoryLeakProbability}%</TableCell>
                                        <TableCell>{data.memoryStatus}</TableCell>
                                    </TableRow>
                                    <TableRow>
                                        <TableCell>Response Time</TableCell>
                                        <TableCell>{data.responseTime}ms</TableCell>
                                        <TableCell>{data.responseTimeStatus}</TableCell>
                                    </TableRow>
                                </TableBody>
                            </Table>
                        </TableContainer>
                    </Grid>
                    <Grid item xs={12} md={6}>
                        <Line data={detailedMetrics} />
                    </Grid>
                </Grid>
            </TabPanel>

            <TabPanel value={tabValue} index={1}>
                <Grid container spacing={3}>
                    <Grid item xs={12}>
                        <Typography variant="subtitle1">Resource Usage Breakdown</Typography>
                        <TableContainer>
                            <Table>
                                <TableHead>
                                    <TableRow>
                                        <TableCell>Resource</TableCell>
                                        <TableCell>Current</TableCell>
                                        <TableCell>Average</TableCell>
                                        <TableCell>Peak</TableCell>
                                        <TableCell>Threshold</TableCell>
                                    </TableRow>
                                </TableHead>
                                <TableBody>
                                    {data.resourceMetrics.map((metric: any) => (
                                        <TableRow key={metric.name}>
                                            <TableCell>{metric.name}</TableCell>
                                            <TableCell>{metric.current}</TableCell>
                                            <TableCell>{metric.average}</TableCell>
                                            <TableCell>{metric.peak}</TableCell>
                                            <TableCell>{metric.threshold}</TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                        </TableContainer>
                    </Grid>
                </Grid>
            </TabPanel>

            <TabPanel value={tabValue} index={2}>
                <Grid container spacing={3}>
                    <Grid item xs={12}>
                        <Typography variant="subtitle1">Performance Timeline</Typography>
                        <Line
                            data={{
                                labels: data.timeline.map((t: any) => new Date(t.timestamp).toLocaleTimeString()),
                                datasets: [
                                    {
                                        label: 'Execution Time',
                                        data: data.timeline.map((t: any) => t.executionTime),
                                        borderColor: 'rgb(75, 192, 192)',
                                    }
                                ]
                            }}
                        />
                    </Grid>
                </Grid>
            </TabPanel>

            <TabPanel value={tabValue} index={3}>
                <Grid container spacing={3}>
                    <Grid item xs={12}>
                        <Typography variant="subtitle1">Detected Anomalies</Typography>
                        <TableContainer>
                            <Table>
                                <TableHead>
                                    <TableRow>
                                        <TableCell>Timestamp</TableCell>
                                        <TableCell>Type</TableCell>
                                        <TableCell>Description</TableCell>
                                        <TableCell>Severity</TableCell>
                                    </TableRow>
                                </TableHead>
                                <TableBody>
                                    {data.anomalies.map((anomaly: any) => (
                                        <TableRow key={anomaly.timestamp}>
                                            <TableCell>{new Date(anomaly.timestamp).toLocaleString()}</TableCell>
                                            <TableCell>{anomaly.type}</TableCell>
                                            <TableCell>{anomaly.description}</TableCell>
                                            <TableCell>{anomaly.severity}</TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                        </TableContainer>
                    </Grid>
                </Grid>
            </TabPanel>
        </Paper>
    );
};