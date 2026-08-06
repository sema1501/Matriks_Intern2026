import React, { useEffect, useRef, useState } from 'react';
import { init, dispose } from 'klinecharts';
import { getKlines, subscribeKline } from '../../services/binanceService';

const ChartModule = ({ 
    symbol = 'BTCUSDT',
    isEmaActive,
    isRsiActive,
    emaPeriod 
}) => {
    const chartContainerRef = useRef(null);
    const chartRef = useRef(null);
    const drawingOverlayIdRef = useRef(null);
    const trendLineIdsRef = useRef([]);
    const [contextMenu, setContextMenu] = useState({
        visible: false,
        x: 0,
        y: 0,
        overlayId: null
    });
    const menuRef = useRef(null);
    const longPressTimerRef = useRef(null);
    const lastMousePosRef = useRef({ x: 0, y: 0 });
    const hoveredOverlayRef = useRef(null);
    const [activeTab, setActiveTab] = useState('1h');
    const [chartType, setChartType] = useState('candle_solid');
    const [error, setError] = useState(null);
    const [loading, setLoading] = useState(false);
    const [isMobile, setIsMobile] = useState(window.innerWidth <= 768);
    const [isDarkMode, setIsDarkMode] = useState(true);
    const [isDrawingTrendLine, setIsDrawingTrendLine] = useState(false);
    const [trendLineCount, setTrendLineCount] = useState(0);

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
        const handleGlobalMouseMove = (e) => {
            lastMousePosRef.current = { x: e.clientX, y: e.clientY };
        };
        const handleGlobalTouchStart = (e) => {
            if (e.touches && e.touches.length > 0) {
                lastMousePosRef.current = { x: e.touches[0].clientX, y: e.touches[0].clientY };
            }
        };

        window.addEventListener('mousemove', handleGlobalMouseMove);
        window.addEventListener('touchstart', handleGlobalTouchStart);

        return () => {
            window.removeEventListener('mousemove', handleGlobalMouseMove);
            window.removeEventListener('touchstart', handleGlobalTouchStart);
        };
    }, []);

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


    useEffect(() => {
        if (!chartRef.current) return;

        
        if (isEmaActive) {
            chartRef.current.removeIndicator('candle_pane', 'EMA');
            chartRef.current.createIndicator(
                { name: 'EMA', calcParams: [emaPeriod] },
                true, 
                { id: 'candle_pane' } 
            );
        } else {
            chartRef.current.removeIndicator('candle_pane', 'EMA');
        }

        
        if (isRsiActive) {
            chartRef.current.createIndicator(
                { name: 'RSI', calcParams: [14] },
                false, 
                { id: 'pane_rsi' } 
            );
        } else {
            chartRef.current.removeIndicator('pane_rsi', 'RSI');
        }

    }, [isEmaActive, isRsiActive, emaPeriod]);
    
    useEffect(() => {
        const handleClickOutside = (event) => {
            if (menuRef.current && !menuRef.current.contains(event.target)) {
                setContextMenu((prev) => ({ ...prev, visible: false }));
            }
        };

        if (contextMenu.visible) {
            document.addEventListener('mousedown', handleClickOutside);
            document.addEventListener('touchstart', handleClickOutside); 
        }

        return () => {
            document.removeEventListener('mousedown', handleClickOutside);
            document.removeEventListener('touchstart', handleClickOutside);
        };
    }, [contextMenu.visible]);

    const handleMenuAction = (actionType) => {
        if (!chartRef.current || !contextMenu.overlayId) return;

        const overlayId = contextMenu.overlayId;
        const overlay = chartRef.current.getOverlayById(overlayId);
        
        if (!overlay) {
            setContextMenu(prev => ({ ...prev, visible: false }));
            return;
        }

       
        if (actionType === 'setUptrend' || actionType === 'setDowntrend') {
            const isUptrend = actionType === 'setUptrend';
            chartRef.current.overrideOverlay({
                id: overlayId,
                ext: { ...(overlay.ext || {}), type: isUptrend ? 'UPTREND' : 'DOWNTREND' },
                styles: {
                    line: { color: isUptrend ? '#10B981' : '#EF4444' } 
                }
            });
        } 
        
        else if (actionType === 'extendRight' || actionType === 'extendLeft') {
            const currentPoints = overlay.points;
            
            
            let isRight = false;
            let isLeft = false;

            if (overlay.name === 'straightLine') {
                isRight = true;
                isLeft = true;
            } else if (overlay.name === 'rayLine') {
                
                if (currentPoints[0].timestamp <= currentPoints[1].timestamp) {
                    isRight = true;
                } else {
                    isLeft = true;
                }
            }

            
            if (actionType === 'extendRight') isRight = true;
            if (actionType === 'extendLeft') isLeft = true;

            
            let targetOverlayName = 'segment';
            
            
            let targetPoints = [...currentPoints].sort((a, b) => a.timestamp - b.timestamp);

            if (isRight && isLeft) {
                targetOverlayName = 'straightLine'; 
            } else if (isRight) {
                targetOverlayName = 'rayLine'; 
            } else if (isLeft) {
                targetOverlayName = 'rayLine'; 
                targetPoints = [targetPoints[1], targetPoints[0]]; 
            }

            
            if (overlay.name === targetOverlayName && 
                currentPoints[0].timestamp === targetPoints[0].timestamp) {
                setContextMenu(prev => ({ ...prev, visible: false }));
                return;
            }

            const currentExt = overlay.ext || {};
            const isUptrend = currentExt.type === 'UPTREND';
            const isDowntrend = currentExt.type === 'DOWNTREND';
            let lineColor = '#2962FF'; 
            if (isUptrend) lineColor = '#10B981';
            if (isDowntrend) lineColor = '#EF4444';

            
            chartRef.current.removeOverlay(overlayId);

            
            const newOverlayId = chartRef.current.createOverlay({
                name: targetOverlayName,
                groupId: 'trend-lines',
                points: targetPoints, 
                mode: 'weak', 
                ext: currentExt, 
                styles: {
                    line: { color: lineColor }
                },
                onMouseEnter: ({ overlay: newOverlay }) => {
                    hoveredOverlayRef.current = newOverlay.id;
                    return true;
                },
                onMouseLeave: () => {
                    hoveredOverlayRef.current = null;
                    return true;
                },
                onRightClick: () => true,
                onPressed: ({ overlay: newOverlay }) => {
                    longPressTimerRef.current = setTimeout(() => {
                        setContextMenu({
                            visible: true,
                            x: Math.min(lastMousePosRef.current.x, window.innerWidth - 190),
                            y: Math.min(lastMousePosRef.current.y, window.innerHeight - 180),
                            overlayId: newOverlay.id
                        });
                    }, 500); 
                    return true;
                },
                onMouseUp: () => { if (longPressTimerRef.current) clearTimeout(longPressTimerRef.current); },
                onMouseMove: () => { if (longPressTimerRef.current) clearTimeout(longPressTimerRef.current); }
            });
            
            if (typeof newOverlayId === 'string') {
                hoveredOverlayRef.current = newOverlayId; 
                const index = trendLineIdsRef.current.indexOf(overlayId);
                if (index !== -1) {
                    trendLineIdsRef.current[index] = newOverlayId;
                }
            }
        }
        
        setContextMenu(prev => ({ ...prev, visible: false }));
    };
    const cancelTrendLineDrawing = () => {
        if (!chartRef.current) {
            setIsDrawingTrendLine(false);
            drawingOverlayIdRef.current = null;
            return;
        }
        if (drawingOverlayIdRef.current) {
            chartRef.current.removeOverlay(drawingOverlayIdRef.current);
            drawingOverlayIdRef.current = null;
        }
        chartRef.current.setZoomEnabled(true);
        chartRef.current.setScrollEnabled(true);
        setIsDrawingTrendLine(false);
    };

    const toggleTrendLineDrawing = () => {
        if (!chartRef.current) return;
        if (isDrawingTrendLine) {
            cancelTrendLineDrawing();
            return;
        }

        const overlayId = chartRef.current.createOverlay(
            {
                name: 'segment',
                groupId: 'trend-lines',
                mode: 'weak',
                onMouseEnter: ({ overlay }) => {
                    hoveredOverlayRef.current = overlay.id;
                    return true;
                },
                
                onMouseLeave: () => {
                    hoveredOverlayRef.current = null;
                    return true;
                },
                
                onRightClick: () => {
                    return true; 
                },

                onPressed: ({ overlay }) => {
                    longPressTimerRef.current = setTimeout(() => {
                        setContextMenu({
                            visible: true,
                            x: lastMousePosRef.current.x,
                            y: lastMousePosRef.current.y,
                            overlayId: overlay.id
                        });
                    }, 500); 
                    return true;
                },
                onMouseUp: () => { if (longPressTimerRef.current) clearTimeout(longPressTimerRef.current); },
                onMouseMove: () => { if (longPressTimerRef.current) clearTimeout(longPressTimerRef.current); },
                onDrawEnd: (event) => {
                    const id = event.overlay?.id || overlayId;
                    if (id) {
                        trendLineIdsRef.current.push(id);
                        setTrendLineCount(trendLineIdsRef.current.length);
                    }
                    drawingOverlayIdRef.current = null;
                    setIsDrawingTrendLine(false);
                    chartRef.current.setZoomEnabled(true);
                    chartRef.current.setScrollEnabled(true);
                    return true;
                }
            }
        );

        if (typeof overlayId === 'string') {
            drawingOverlayIdRef.current = overlayId;
        }
        setIsDrawingTrendLine(true);
    };

    const deleteLastTrendLine = () => {
        if (!chartRef.current) return;
        const ids = trendLineIdsRef.current;
        const lastId = ids[ids.length - 1];
        if (!lastId) return;
        chartRef.current.removeOverlay(lastId);
        ids.pop();
        setTrendLineCount(ids.length);
    };

    const clearAllTrendLines = () => {
        if (!chartRef.current) return;
        cancelTrendLineDrawing();
        chartRef.current.removeOverlay({ groupId: 'trend-lines' });
        trendLineIdsRef.current = [];
        setTrendLineCount(0);
    };

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
                    gridTemplateColumns: isMobile ? 'repeat(2, minmax(0, 1fr))' : 'repeat(4, minmax(0, 1fr))',
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

            <div style={{
                display: 'flex',
                flexDirection: isMobile ? 'column' : 'row',
                gap: '10px',
                justifyContent: 'space-between',
                alignItems: isMobile ? 'stretch' : 'center',
                marginBottom: '15px'
            }}>
                <div style={{
                    display: 'flex',
                    flexDirection: isMobile ? 'column' : 'row',
                    gap: '10px',
                    flexWrap: 'wrap'
                }}>
                    <button
                        onClick={toggleTrendLineDrawing}
                        style={themeStyles.button(isDrawingTrendLine)}
                    >
                        {isDrawingTrendLine ? 'Trend Çizgisi İptal' : 'Trend Çizgisi Çiz'}
                    </button>
                    <button
                        onClick={deleteLastTrendLine}
                        style={themeStyles.button(false)}
                    >
                        Son Çizgiyi Sil
                    </button>
                    <button
                        onClick={clearAllTrendLines}
                        style={themeStyles.button(false)}
                    >
                        Tüm Çizgileri Temizle
                    </button>
                </div>
                <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap', width: isMobile ? '100%' : 'auto', justifyContent: isMobile ? 'flex-start' : 'flex-end' }}>
                    <div style={{ color: isDarkMode ? '#CBD5E1' : '#475569', fontSize: isMobile ? '12px' : '14px' }}>
                        {trendLineCount} trend çizgisi kayıtlı
                    </div>
                </div>
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
                onContextMenu={(e) => {
                    e.preventDefault(); 
                    if (hoveredOverlayRef.current) {
                        
                        const clampedX = Math.min(e.clientX, window.innerWidth - 190);
                        const clampedY = Math.min(e.clientY, window.innerHeight - 180);

                        setContextMenu({
                            visible: true,
                            x: clampedX,
                            y: clampedY,
                            overlayId: hoveredOverlayRef.current
                        });
                    }
                }}
                style={{
                    width: '100%',
                    height: isMobile ? '320px' : '450px',
                    display: 'block',
                    position: 'relative'
                }}
            />
              {contextMenu.visible && (
                <div
                    ref={menuRef}
                    style={{
                        position: 'fixed',
                        top: contextMenu.y,
                        left: contextMenu.x,
                        backgroundColor: isDarkMode ? '#1e293b' : '#ffffff',
                        border: `1px solid ${isDarkMode ? '#334155' : '#e2e8f0'}`,
                        boxShadow: '0 4px 12px rgba(0, 0, 0, 0.15)',
                        borderRadius: '6px',
                        padding: '4px 0',
                        zIndex: 9999, 
                        minWidth: '180px',
                        display: 'flex',
                        flexDirection: 'column'
                    }}
                >
                    {[
                        { label: 'Sağa Uzat', action: 'extendRight' },
                        { label: 'Sola Uzat', action: 'extendLeft' },
                        { label: 'Yükselen Trende Çevir', action: 'setUptrend' },
                        { label: 'Düşen Trende Çevir', action: 'setDowntrend' }
                    ].map((item, index) => (
                        <button
                            key={index}
                            onClick={(e) => {
                                e.stopPropagation(); 
                                handleMenuAction(item.action);
                            }}
                            style={{
                                background: 'transparent',
                                border: 'none',
                                padding: '10px 15px',
                                textAlign: 'left',
                                color: isDarkMode ? '#f8fafc' : '#0f172a',
                                fontSize: '13px',
                                cursor: 'pointer',
                                width: '100%',
                                borderBottom: index === 1 ? `1px solid ${isDarkMode ? '#334155' : '#e2e8f0'}` : 'none'
                            }}
                            onMouseOver={(e) => e.target.style.backgroundColor = isDarkMode ? '#334155' : '#f1f5f9'}
                            onMouseOut={(e) => e.target.style.backgroundColor = 'transparent'}
                        >
                            {item.label}
                        </button>
                    ))}
                </div>
            )}
        </div>
    );
};

export default ChartModule;