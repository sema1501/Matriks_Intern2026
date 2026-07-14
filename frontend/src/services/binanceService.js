import axios from 'axios';

const BINANCE_BASE_URL = 'https://api1.binance.com/api/v3';
const BINANCE_WS_BASE = 'wss://stream.binance.com:9443/ws';

export function buildStreamUrl(symbols) {
    const streams = symbols.map(s => `${s.toLowerCase()}@ticker`).join('/');
    return `wss://stream.binance.com:9443/stream?streams=${streams}`;
}

export const getKlines = async (symbol = 'BTCUSDT', interval = '1h', limit = 500) => {
    try {
        let cleanSymbol = symbol.toUpperCase().replace('-', '').replace('_', '');
        if (!cleanSymbol.endsWith('USDT')) {
            cleanSymbol = `${cleanSymbol}USDT`;
        }

        const response = await axios.get(`${BINANCE_BASE_URL}/klines`, {
            params: {
                symbol: cleanSymbol,
                interval,
                limit,
            },
        });

        if (!response.data || !Array.isArray(response.data)) {
            return [];
        }

        return response.data.map((candle) => ({
            timestamp: Number(candle[0]),
            open: parseFloat(candle[1]),
            high: parseFloat(candle[2]),
            low: parseFloat(candle[3]),
            close: parseFloat(candle[4]),
            volume: parseFloat(candle[5]),
        }));
    } catch (error) {
        throw new Error(error.response?.data?.msg || 'Binance veri çekme hatası oluştu.');
    }
};

export const subscribeKline = (symbol, interval, onMessage) => {
    let cleanSymbol = symbol.toUpperCase().replace('-', '').replace('_', '');
    if (!cleanSymbol.endsWith('USDT')) {
        cleanSymbol = `${cleanSymbol}USDT`;
    }

    const wsUrl = `${BINANCE_WS_BASE}/${cleanSymbol.toLowerCase()}@kline_${interval}`;
    const ws = new WebSocket(wsUrl);

    ws.onmessage = (event) => {
        try {
            const msg = JSON.parse(event.data);
            if (msg && msg.k) {
                const k = msg.k;
                const formattedData = {
                    timestamp: Number(k.t),
                    open: parseFloat(k.o),
                    high: parseFloat(k.h),
                    low: parseFloat(k.l),
                    close: parseFloat(k.c),
                    volume: parseFloat(k.v)
                };
                onMessage(formattedData);
            }
        } catch (err) {
            console.error('WS parse error:', err);
        }
    };

    const safeClose = () => {
        if (ws.readyState === WebSocket.CONNECTING) {
            ws.onopen = () => {
                ws.close();
            };
        } else if (ws.readyState === WebSocket.OPEN) {
            ws.close();
        }
    };

    return {
        close: safeClose
    };
};