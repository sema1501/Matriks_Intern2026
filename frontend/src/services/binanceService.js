const BINANCE_WS_BASE = 'wss://stream.binance.com:9443/stream?streams=';

export function buildStreamUrl(symbols) {
  const streams = symbols.map(s => `${s.toLowerCase()}@ticker`).join('/');
  return `${BINANCE_WS_BASE}${streams}`;
}
