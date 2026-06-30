import React, { createContext, useContext, useMemo } from 'react';
import { useBinancePrices } from '../hooks/useBinancePrices';

const BinanceContext = createContext(null);

export function BinanceProvider({ children }) {
    const binanceData = useBinancePrices();
    const value = useMemo(() => binanceData, [binanceData.prices, binanceData.connectionStatus]);

    return (
        <BinanceContext.Provider value={value}>
            {children}
        </BinanceContext.Provider>
    );
}

export function useBinance() {
    const context = useContext(BinanceContext);
    if (!context) {
        throw new Error('useBinance must be used within a BinanceProvider');
    }
    return context;
}