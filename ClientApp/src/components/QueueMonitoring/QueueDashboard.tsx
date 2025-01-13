import React, { useState, useEffect } from 'react';
import { 
    LineChart, 
    BarChart, 
    Card, 
    Metric, 
    Title, 
    TabList, 
    Tab 
} from '@tremor/react';
import { useQueueMetrics } from '../../hooks/useQueueMetrics';
import { QueueHealthStatus } from './QueueHealthStatus';
import { MessageTable } from './MessageTable';

interface QueueMetrics {
    messageCount: number;
    consumerCount: number;
    errorRate: number;
    processingLatency: number;
    throughput: number;
}

export const QueueDashboard: React.FC = () => {
    const [selectedQueue, setSelectedQueue] = useState<string>('');
    const [timeRange, setTimeRange] = useState<string>('1h');
    const { metrics, isLoading, error } = useQueueMetrics(selectedQueue, timeRange);

    if (isLoading) return <div>Loading...</div>;
    if (error) return <div>Error: {error.message}</div>;

    return (
        <div className="p-4 space-y-4">
            <div className="flex justify-between items-center">
                <Title>Queue Monitoring Dashboard</Title>
                <TabList
                    defaultValue="1h"
                    onValueChange={(value) => setTimeRange(value)}
                >
                    <Tab value="1h">1 Hour</Tab>
                    <Tab value="24h">24 Hours</Tab>
                    <Tab value="7d">7 Days</Tab>
                </TabList>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                <Card>
                    <Title>Message Count</Title>
                    <Metric>{metrics.messageCount}</Metric>
                </Card>
                <Card>
                    <Title>Active Consumers</Title>
                    <Metric>{metrics.consumerCount}</Metric>
                </Card>
                <Card>
                    <Title>Error Rate</Title>
                    <Metric>{(metrics.errorRate * 100).toFixed(2)}%</Metric>
                </Card>
                <Card>
                    <Title>Processing Latency</Title>
                    <Metric>{metrics.processingLatency.toFixed(2)}ms</Metric>
                </Card>
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
                <Card>
                    <Title>Message Throughput</Title>
                    <LineChart
                        data={metrics.throughputHistory}
                        index="timestamp"
                        categories={["throughput"]}
                        colors={["blue"]}
                    />
                </Card>
                <Card>
                    <Title>Error Distribution</Title>
                    <BarChart
                        data={metrics.errorDistribution}
                        index="error"
                        categories={["count"]}
                        colors={["red"]}
                    />
                </Card>
            </div>

            <Card>
                <Title>Queue Health Status</Title>
                <QueueHealthStatus metrics={metrics} />
            </Card>

            <Card>
                <Title>Recent Messages</Title>
                <MessageTable queueName={selectedQueue} />
            </Card>
        </div>
    );
}; 