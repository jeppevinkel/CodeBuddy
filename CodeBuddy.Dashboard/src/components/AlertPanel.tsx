import React from 'react';
import {
    Paper,
    Typography,
    List,
    ListItem,
    ListItemText,
    ListItemIcon,
    Alert,
    Box
} from '@mui/material';
import ErrorIcon from '@mui/icons-material/Error';
import WarningIcon from '@mui/icons-material/Warning';
import InfoIcon from '@mui/icons-material/Info';

interface PerformanceAlert {
    id: string;
    type: string;
    message: string;
    severity: 'error' | 'warning' | 'info';
    timestamp: string;
    metric: {
        name: string;
        value: number;
        threshold: number;
    };
}

interface AlertPanelProps {
    alerts: PerformanceAlert[];
}

export const AlertPanel: React.FC<AlertPanelProps> = ({ alerts }) => {
    const getSeverityIcon = (severity: string) => {
        switch (severity) {
            case 'error':
                return <ErrorIcon color="error" />;
            case 'warning':
                return <WarningIcon color="warning" />;
            default:
                return <InfoIcon color="info" />;
        }
    };

    const getAlertSeverity = (severity: string): 'error' | 'warning' | 'info' => {
        switch (severity) {
            case 'error':
                return 'error';
            case 'warning':
                return 'warning';
            default:
                return 'info';
        }
    };

    return (
        <Paper elevation={3} sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
                Performance Alerts
            </Typography>
            {alerts.length === 0 ? (
                <Alert severity="success">No active alerts</Alert>
            ) : (
                <List>
                    {alerts.map((alert) => (
                        <ListItem key={alert.id}>
                            <ListItemIcon>
                                {getSeverityIcon(alert.severity)}
                            </ListItemIcon>
                            <ListItemText
                                primary={
                                    <Alert severity={getAlertSeverity(alert.severity)}>
                                        {alert.message}
                                    </Alert>
                                }
                                secondary={
                                    <Box sx={{ mt: 1 }}>
                                        <Typography variant="body2" color="text.secondary">
                                            {alert.metric.name}: {alert.metric.value} 
                                            (Threshold: {alert.metric.threshold})
                                        </Typography>
                                        <Typography variant="caption" color="text.secondary">
                                            {new Date(alert.timestamp).toLocaleString()}
                                        </Typography>
                                    </Box>
                                }
                            />
                        </ListItem>
                    ))}
                </List>
            )}
        </Paper>
    );
};