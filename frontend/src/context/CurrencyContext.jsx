import { createContext, useContext, useEffect, useMemo, useState } from "react";

const CurrencyContext = createContext(null);

const DEFAULT_USD_TRY_RATE = 32.5;

export function CurrencyProvider({ children }) {
  const [currency, setCurrency] = useState(() => {
    return localStorage.getItem("currency") || "USD";
  });

  const [usdTryRate, setUsdTryRate] = useState(DEFAULT_USD_TRY_RATE);
  const [rateLoading, setRateLoading] = useState(false);
  const [rateError, setRateError] = useState("");

  useEffect(() => {
    localStorage.setItem("currency", currency);
  }, [currency]);

  useEffect(() => {
    async function fetchUsdTryRate() {
      try {
        setRateLoading(true);
        setRateError("");

        const response = await fetch("https://open.er-api.com/v6/latest/USD");

        if (!response.ok) {
          throw new Error("Currency API request failed.");
        }

        const data = await response.json();
        const tryRate = data?.rates?.TRY;

        if (!tryRate || Number.isNaN(Number(tryRate))) {
          throw new Error("TRY rate was not found.");
        }

        setUsdTryRate(Number(tryRate));
      } catch (error) {
        console.error("USD/TRY rate could not be fetched:", error);
        setRateError(
          "Kur bilgisi alınamadı. TRY fiyatları yaklaşık kur ile gösteriliyor."
        );
        setUsdTryRate(DEFAULT_USD_TRY_RATE);
      } finally {
        setRateLoading(false);
      }
    }

    fetchUsdTryRate();
  }, []);

  function changeCurrency(newCurrency) {
    if (newCurrency !== "USD" && newCurrency !== "TRY") {
      return;
    }

    setCurrency(newCurrency);
  }

  function toggleCurrency() {
    setCurrency((currentCurrency) =>
      currentCurrency === "USD" ? "TRY" : "USD"
    );
  }

  function convertUsdToTry(usdValue) {
    const numericValue = Number(usdValue);

    if (Number.isNaN(numericValue)) {
      return 0;
    }

    return numericValue * usdTryRate;
  }

  function convertUsdToSelectedCurrency(usdValue) {
    const numericValue = Number(usdValue);

    if (Number.isNaN(numericValue)) {
      return 0;
    }

    if (currency === "TRY") {
      return numericValue * usdTryRate;
    }

    return numericValue;
  }

  function formatUsd(usdValue) {
    const numericValue = Number(usdValue);

    if (Number.isNaN(numericValue)) {
      return "$0.00";
    }

    return new Intl.NumberFormat("en-US", {
      style: "currency",
      currency: "USD",
      maximumFractionDigits: numericValue < 1 ? 6 : 2,
    }).format(numericValue);
  }

  function formatTry(tryValue) {
    const numericValue = Number(tryValue);

    if (Number.isNaN(numericValue)) {
      return "₺0.00";
    }

    return new Intl.NumberFormat("tr-TR", {
      style: "currency",
      currency: "TRY",
      maximumFractionDigits: numericValue < 1 ? 6 : 2,
    }).format(numericValue);
  }

  function formatPrice(usdValue) {
    if (currency === "TRY") {
      return formatTry(convertUsdToTry(usdValue));
    }

    return formatUsd(usdValue);
  }

  const value = useMemo(
    () => ({
      currency,
      usdTryRate,
      rateLoading,
      rateError,
      changeCurrency,
      toggleCurrency,
      convertUsdToTry,
      convertUsdToSelectedCurrency,
      formatUsd,
      formatTry,
      formatPrice,
    }),
    [currency, usdTryRate, rateLoading, rateError]
  );

  return (
    <CurrencyContext.Provider value={value}>
      {children}
    </CurrencyContext.Provider>
  );
}

export function useCurrency() {
  const context = useContext(CurrencyContext);

  if (!context) {
    throw new Error("useCurrency must be used inside CurrencyProvider.");
  }

  return context;
}