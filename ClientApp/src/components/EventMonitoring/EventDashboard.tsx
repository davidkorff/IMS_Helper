import React, { useState, useEffect } from 'react';
import {
    LineChart,
    BarChart,
    Card,
    Title,
    Text,
    Tab,
    TabList,
    Grid,
    Metric,
    Legend,
    AreaChart
} from '@tremor/react';
import { useEventMetrics } from '../../hooks/useEventMetrics';
import { EventTypeSelector } from './EventTypeSelector';
import { MetricsTimeRange } from './MetricsTimeRange';

interface EventMetrics {
    eventType: string;
    throughput: number;
    errorRate: number;
    averageLatency: number;
    subscriptionDeliveryRates: Record<string, number>;
    timeSeriesData: Array<{
        timestamp: string;
        throughput: number;
        errors: number;
        latency: number;
    }>;
}

export const EventDashboard: React.FC = () => {
    const [selectedEventType, setSelectedEventType] = useState<string>('all');
    const [timeRange, setTimeRange] = useState<string>('1h');
    const { metrics, isLoading, error } = useEventMetrics(selectedEventType, timeRange);

    if (isLoading) return <div>Loading metrics...</div>;
    if (error) return <div>Error loading metrics: {error.message}</div>;

    return (
        <div className="p-4 space-y-6">
            <div className="flex justify-between items-center">
                <Title>Event Processing Dashboard</Title>
                <div className="flex space-x-4">
                    <EventTypeSelector
                        value={selectedEventType}
                        onChange={setSelectedEventType}
                    />
                    <MetricsTimeRange
                        value={timeRange}
                        onChange={setTimeRange}
                    />
                </div>
            </div>

            <Grid numCols={3} className="gap-6">
                <Card>
                    <Title>Event Throughput</Title>
                    <Metric>{metrics.throughput.toFixed(2)} evt/sec</Metric>
                </Card>
                <Card>
                    <Title>Error Rate</Title>
                    <Metric color={metrics.errorRate > 0.05 ? 'red' : 'green'}>
                        {(metrics.errorRate * 100).toFixed(2)}%
                    </Metric>
                </Card>
                <Card>
                    <Title>Average Latency</Title>
                    <Metric>{metrics.averageLatency.toFixed(2)} ms</Metric>
                </Card>
            </Grid>

            <Card>
                <Title>Event Processing Timeline</Title>
                <AreaChart
                    data={metrics.timeSeriesData}
                    index="timestamp"
                    categories={["throughput", "errors", "latency"]}
                    colors={["blue", "red", "yellow"]}
                    valueFormatter={(number) => `${number.toFixed(2)}`}
                    yAxisWidth={40}
                    showLegend
                />
            </Card>

            <div className="grid grid-cols-2 gap-6">
                <Card>
                    <Title>Subscription Delivery Rates</Title>
                    <BarChart
                        data={Object.entries(metrics.subscriptionDeliveryRates).map(
                            ([id, rate]) => ({
                                subscription: id,
                                rate: rate * 100
                            })
                        )}
                        index="subscription"
                        categories={["rate"]}
                        colors={["blue"]}
                        valueFormatter={(number) => `${number.toFixed(1)}%`}
                    />
                </Card>
                <Card>
                    <Title>Event Type Distribution</Title>
                    <Legend
                        categories={Object.keys(metrics.eventTypeDistribution)}
                        colors={["blue", "green", "yellow", "red", "purple"]}
                    />
                    <DonutChart
                        data={Object.entries(metrics.eventTypeDistribution).map(
                            ([type, count]) => ({
                                name: type,
                                value: count
                            })
                        )}
                        category="value"
                        index="name"
                    />
                </Card>
            </div>
        </div>
    );
}; 