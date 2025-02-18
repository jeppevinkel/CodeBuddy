import React from 'react';
import {
    Paper,
    Typography,
    Grid,
    TextField,
    MenuItem,
    Button,
    Box
} from '@mui/material';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDateFns } from '@mui/x-date-pickers/AdapterDateFns';

interface FilterPanelProps {
    languages: string[];
    testTypes: string[];
    onChange: (filters: FilterState) => void;
}

interface FilterState {
    language: string;
    testType: string;
    startDate: Date | null;
    endDate: Date | null;
}

export const FilterPanel: React.FC<FilterPanelProps> = ({ languages, testTypes, onChange }) => {
    const [filters, setFilters] = React.useState<FilterState>({
        language: '',
        testType: '',
        startDate: null,
        endDate: null
    });

    const handleChange = (field: keyof FilterState, value: any) => {
        const newFilters = {
            ...filters,
            [field]: value
        };
        setFilters(newFilters);
        onChange(newFilters);
    };

    const handleReset = () => {
        const resetFilters = {
            language: '',
            testType: '',
            startDate: null,
            endDate: null
        };
        setFilters(resetFilters);
        onChange(resetFilters);
    };

    return (
        <Paper elevation={3} sx={{ p: 2 }}>
            <Typography variant="h6" gutterBottom>
                Filters
            </Typography>
            <Grid container spacing={2} alignItems="center">
                <Grid item xs={12} sm={6} md={3}>
                    <TextField
                        select
                        fullWidth
                        label="Language"
                        value={filters.language}
                        onChange={(e) => handleChange('language', e.target.value)}
                    >
                        <MenuItem value="">All Languages</MenuItem>
                        {languages.map((lang) => (
                            <MenuItem key={lang} value={lang}>
                                {lang}
                            </MenuItem>
                        ))}
                    </TextField>
                </Grid>
                <Grid item xs={12} sm={6} md={3}>
                    <TextField
                        select
                        fullWidth
                        label="Test Type"
                        value={filters.testType}
                        onChange={(e) => handleChange('testType', e.target.value)}
                    >
                        <MenuItem value="">All Test Types</MenuItem>
                        {testTypes.map((type) => (
                            <MenuItem key={type} value={type}>
                                {type}
                            </MenuItem>
                        ))}
                    </TextField>
                </Grid>
                <LocalizationProvider dateAdapter={AdapterDateFns}>
                    <Grid item xs={12} sm={6} md={2}>
                        <DatePicker
                            label="Start Date"
                            value={filters.startDate}
                            onChange={(date) => handleChange('startDate', date)}
                            maxDate={filters.endDate || undefined}
                        />
                    </Grid>
                    <Grid item xs={12} sm={6} md={2}>
                        <DatePicker
                            label="End Date"
                            value={filters.endDate}
                            onChange={(date) => handleChange('endDate', date)}
                            minDate={filters.startDate || undefined}
                        />
                    </Grid>
                </LocalizationProvider>
                <Grid item xs={12} md={2}>
                    <Box display="flex" justifyContent="flex-end">
                        <Button
                            variant="outlined"
                            onClick={handleReset}
                            sx={{ mr: 1 }}
                        >
                            Reset
                        </Button>
                    </Box>
                </Grid>
            </Grid>
        </Paper>
    );
};