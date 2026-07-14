import React, { useEffect, useRef, useState } from 'react';
import { init, dispose } from 'klinecharts';
import { getKlines, subscribeKline } from '../../services/binanceService';

const ChartModule = ({ symbol = 'BTCUSDT' }) => {
    const chartContainerRef = useRef(null);
    const chartRef = useRef(null);
    const [activeTab, setActiveTab] = useState('1h');
    const [chartType, setChartType] = useState('candle_solid');
    const [error, setError] = useState(null);
    const [loading, setLoading] = useState(false);
    const [isMobile, setIsMobile] = useState(window.innerWidth <= 768);
    const [isDarkMode, setIsDarkMode] = useState(true);

    const intervals = [
        { label: '1 Saat', value: '1h', binanceInterval: '1m', limit: 60 },
        { label: '1 Gün', value: '1d', binanceInterval: '15m', limit: 96 },
        { label: '1 Hafta', value: '1w', binanceInterval: '1h', limit: 168 },
        { label: '1 Ay', value: '1M', binanceInterval: '4h', limit: 180 }
    ];

    const getPrecisionByPrice = (price) => {
        if (!price || price <= 0) return 2;
        if (price < 0.1) return 5;
        if (price < 1) return 4;
        if (price < 10) return 3;
        return 2;
    };

    useEffect(() => {
        const checkTheme = () => {
            const htmlTheme = document.documentElement.getAttribute('data-theme');
            const bodyClass = document.body.className;
            const isDark = htmlTheme === 'dark' || bodyClass.includes('dark') || !htmlTheme && !bodyClass.includes('light');
            setIsDarkMode(isDark);
        };

        checkTheme();

        const observer = new MutationObserver(checkTheme);
        observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme', 'class'] });
        observer.observe(document.body, { attributes: true, attributeFilter: ['class'] });

        const handleResize = () => {
            const mobile = window.innerWidth <= 768;
            setIsMobile(mobile);
            if (chartRef.current) {
                chartRef.current.resize();
            }
        };

        window.addEventListener('resize', handleResize);

        const handleResizeError = (e) => {
            if (e.message?.includes('ResizeObserver') || e.message?.includes('loop limit exceeded')) {
                const resizeObserverErrDiv = document.getElementById('webpack-dev-server-client-overlay');
                if (resizeObserverErrDiv) {
                    resizeObserverErrDiv.style.display = 'none';
                }
                e.stopImmediatePropagation();
            }
        };

        window.addEventListener('error', handleResizeError);

        if (chartContainerRef.current && !chartRef.current) {
            chartRef.current = init(chartContainerRef.current);
        }

        return () => {
            observer.disconnect();
            window.removeEventListener('resize', handleResize);
            window.removeEventListener('error', handleResizeError);
            if (chartRef.current) {
                dispose(chartContainerRef.current);
                chartRef.current = null;
            }
        };
    }, []);

    useEffect(() => {
        if (!chartRef.current) return;

        const gridColor = isDarkMode ? '#2B2E3A' : '#E2E8F0';
        const textColor = isDarkMode ? '#929AA5' : '#4A5568';

        chartRef.current.setStyles({
            grid: {
                show: true,
                horizontal: { color: gridColor },
                vertical: { color: gridColor }
            },
            xAxis: {
                tickText: { color: textColor }
            },
            yAxis: {
                inside: false,
                marginRight: 4,
                tickText: { color: textColor }
            }
        });
    }, [isDarkMode]);

    useEffect(() => {
        let wsInstance = null;
        const activeConfig = intervals.find(item => item.value === activeTab);
        if (!activeConfig) return;

        const startChartAndSocket = async () => {
            if (!chartRef.current) return;
            setLoading(true);
            setError(null);

            try {
                const data = await getKlines(symbol, activeConfig.binanceInterval, activeConfig.limit);
                if (data && data.length > 0) {
                    const samplePrice = data[data.length - 1].close;
                    const precision = getPrecisionByPrice(samplePrice);

                    if (typeof chartRef.current.setPriceVolumePrecision === 'function') {
                        chartRef.current.setPriceVolumePrecision(precision, 0);
                    }

                    const gridColor = isDarkMode ? '#2B2E3A' : '#E2E8F0';
                    const textColor = isDarkMode ? '#929AA5' : '#4A5568';

                    if (typeof chartRef.current.setStyles === 'function') {
                        chartRef.current.setStyles({
                            grid: {
                                show: true,
                                horizontal: { color: gridColor },
                                vertical: { color: gridColor }
                            },
                            technicalIndicator: {
                                precision: precision
                            },
                            candle: {
                                type: chartType,
                                pricePrecision: precision,
                                volumePrecision: 0,
                                tooltip: {
                                    text: { color: textColor },
                                    rect: {
                                        paddingLeft: 4,
                                        paddingRight: 4,
                                        paddingTop: 4,
                                        paddingBottom: 4
                                    }
                                }
                            },
                            xAxis: {
                                tickText: { color: textColor }
                            },
                            yAxis: {
                                inside: false,
                                marginRight: 4,
                                tickText: { color: textColor }
                            }
                        });
                    }

                    if (typeof chartRef.current.applyNewData === 'function') {
                        chartRef.current.applyNewData(data);
                    }
                    if (typeof chartRef.current.resize === 'function') {
                        chartRef.current.resize();
                    }

                    wsInstance = subscribeKline(symbol, activeConfig.binanceInterval, (newData) => {
                        if (chartRef.current && typeof chartRef.current.updateData === 'function') {
                            chartRef.current.updateData(newData);
                        }
                    });
                } else {
                    setError('Binance borsasından geçmiş veri alınamadı.');
                }
            } catch (err) {
                setError(err.message || 'Grafik yüklenirken hata oluştu.');
            } finally {
                setLoading(false);
            }
        };

        startChartAndSocket();

        return () => {
            if (wsInstance) {
                wsInstance.close();
            }
        };
    }, [symbol, activeTab, isDarkMode]);

    useEffect(() => {
        if (chartRef.current && typeof chartRef.current.setStyles === 'function') {
            chartRef.current.setStyles({
                candle: {
                    type: chartType
                }
            });
        }
    }, [chartType]);

    const toggleChartType = () => {
        setChartType((prev) => (prev === 'candle_solid' ? 'ohlc' : 'candle_solid'));
    };

    const themeStyles = {
        container: {
            backgroundColor: isDarkMode ? '#131722' : '#FFFFFF',
            padding: isMobile ? '10px' : '20px',
            borderRadius: '24px',
            color: isDarkMode ? '#FFFFFF' : '#1E293B',
            position: 'relative',
            border: isDarkMode ? '1px solid rgba(255, 255, 255, 0.08)' : '1px solid rgba(0, 0, 0, 0.06)',
            boxShadow: isDarkMode ? 'none' : '0 10px 25px -5px rgba(0, 0, 0, 0.05)',
            transition: 'background-color 0.3s ease, border-color 0.3s ease, color 0.3s ease'
        },
        button: (isActive) => ({
            backgroundColor: isActive ? '#2962FF' : (isDarkMode ? '#2A2E39' : '#F1F5F9'),
            color: isActive ? '#FFFFFF' : (isDarkMode ? '#FFFFFF' : '#475569'),
            border: 'none',
            padding: isMobile ? '8px 4px' : '8px 16px',
            fontSize: isMobile ? '12px' : '14px',
            borderRadius: '8px',
            cursor: 'pointer',
            fontWeight: 'bold',
            textAlign: 'center',
            transition: 'all 0.2s ease'
        }),
        toggleBtn: {
            backgroundColor: isDarkMode ? '#2A2E39' : '#F1F5F9',
            color: isDarkMode ? '#FFFFFF' : '#475569',
            border: 'none',
            padding: '8px 16px',
            fontSize: isMobile ? '12px' : '14px',
            borderRadius: '8px',
            cursor: 'pointer',
            fontWeight: 'bold',
            width: isMobile ? '100%' : 'auto',
            transition: 'all 0.2s ease'
        }
    };

    return (
        <div style={themeStyles.container}>
            <div style={{
                display: 'flex',
                flexDirection: isMobile ? 'column' : 'row',
                gap: '10px',
                justifyContent: 'space-between',
                alignItems: isMobile ? 'stretch' : 'center',
                marginBottom: '15px'
            }}>
                <div style={{
                    display: 'grid',
                    gridTemplateColumns: 'repeat(4, 1fr)',
                    gap: '5px'
                }}>
                    {intervals.map((item) => (
                        <button
                            key={item.value}
                            onClick={() => setActiveTab(item.value)}
                            style={themeStyles.button(activeTab === item.value)}
                        >
                            {item.label}
                        </button>
                    ))}
                </div>

                <button
                    onClick={toggleChartType}
                    style={themeStyles.toggleBtn}
                >
                    Görünüm: {chartType === 'candle_solid' ? 'Mum' : 'OHLC Bar'}
                </button>
            </div>

            {error && (
                <div style={{ color: '#FF5252', padding: '10px', marginBottom: '10px', backgroundColor: 'rgba(255, 82, 82, 0.1)', borderRadius: '4px', textAlign: 'center' }}>
                    {error}
                </div>
            )}

            {loading && (
                <div style={{ position: 'absolute', top: isMobile ? '110px' : '70px', left: '30px', color: '#AAA', fontSize: '14px', zIndex: 10 }}>
                    Güncelleniyor...
                </div>
            )}

            <div
                ref={chartContainerRef}
                style={{
                    width: '100%',
                    height: isMobile ? '320px' : '450px',
                    display: 'block',
                    position: 'relative'
                }}
            />
        </div>
    );
};

export default ChartModule;