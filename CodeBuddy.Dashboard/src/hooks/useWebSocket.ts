import { useState, useEffect, useRef } from 'react';

interface WebSocketOptions {
    reconnectAttempts?: number;
    reconnectInterval?: number;
    onOpen?: () => void;
    onClose?: () => void;
    onError?: (error: Event) => void;
}

export function useWebSocket<T>(
    url: string,
    options: WebSocketOptions = {}
) {
    const [latestData, setLatestData] = useState<T | null>(null);
    const [isConnected, setIsConnected] = useState(false);
    const [error, setError] = useState<Event | null>(null);
    
    const ws = useRef<WebSocket | null>(null);
    const reconnectCount = useRef(0);
    const maxReconnectAttempts = options.reconnectAttempts || 5;
    const reconnectInterval = options.reconnectInterval || 5000;

    const connect = () => {
        try {
            ws.current = new WebSocket(url);

            ws.current.onopen = () => {
                setIsConnected(true);
                reconnectCount.current = 0;
                options.onOpen?.();
            };

            ws.current.onclose = () => {
                setIsConnected(false);
                options.onClose?.();

                // Attempt to reconnect
                if (reconnectCount.current < maxReconnectAttempts) {
                    reconnectCount.current += 1;
                    setTimeout(connect, reconnectInterval);
                }
            };

            ws.current.onerror = (err: Event) => {
                setError(err);
                options.onError?.(err);
            };

            ws.current.onmessage = (event: MessageEvent) => {
                try {
                    const parsedData = JSON.parse(event.data);
                    setLatestData(parsedData);
                } catch (err) {
                    console.error('Failed to parse WebSocket message:', err);
                }
            };
        } catch (err) {
            console.error('Failed to establish WebSocket connection:', err);
        }
    };

    useEffect(() => {
        connect();

        return () => {
            if (ws.current) {
                ws.current.close();
            }
        };
    }, [url]);

    return {
        latestData,
        isConnected,
        error
    };
}