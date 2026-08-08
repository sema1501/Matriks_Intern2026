import React, { createContext, useContext } from 'react';
import { useBinancePrices } from '../hooks/useBinancePrices';

const PriceContext = createContext();

export const PriceProvider = ({ children }) => {
  const binanceData = useBinancePrices();

  return (
    <PriceContext.Provider value={binanceData}>
      {children}
    </PriceContext.Provider>
  );
};

export const useGlobalPrices = () => useContext(PriceContext);
