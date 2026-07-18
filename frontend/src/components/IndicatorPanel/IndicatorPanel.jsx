import { useState } from 'react';
import './IndicatorPanel.css';

const clampNumber = (value, min, max, fallback) => {
    const parsed = Number(value);
    if (!Number.isFinite(parsed)) return fallback;
    return Math.min(max, Math.max(min, parsed));
};

export default function IndicatorPanel({
    isEmaActive,
    onEmaToggle,
    emaPeriod,
    onEmaPeriodChange,
    isRsiActive,
    onRsiToggle,
    isBollActive,
    onBollToggle,
    bollPeriod,
    onBollPeriodChange,
    bollStdDev,
    onBollStdDevChange,
}) {
    const [isExpanded, setIsExpanded] = useState(true);
    const activeCount = [isEmaActive, isRsiActive, isBollActive].filter(Boolean).length;

    return (
        <section className={`indicator-panel ${isExpanded ? 'indicator-panel--expanded' : ''}`}>
            <button
                type="button"
                className="indicator-panel__header"
                onClick={() => setIsExpanded((current) => !current)}
                aria-expanded={isExpanded}
                aria-controls="indicator-panel-content"
            >
                <span>
                    <strong>İndikatör Paneli</strong>
                    <small>{activeCount} aktif</small>
                </span>
                <span className="indicator-panel__chevron" aria-hidden="true">⌄</span>
            </button>

            <div id="indicator-panel-content" className="indicator-panel__content">
                <article className={`indicator-card ${isEmaActive ? 'indicator-card--active' : ''}`}>
                    <label className="indicator-card__toggle">
                        <input
                            type="checkbox"
                            checked={isEmaActive}
                            onChange={(event) => onEmaToggle(event.target.checked)}
                        />
                        <span>
                            <strong>EMA</strong>
                            <small>Üstel hareketli ortalama</small>
                        </span>
                    </label>
                    {isEmaActive && (
                        <label className="indicator-field">
                            <span>Periyot</span>
                            <input
                                type="number"
                                min="1"
                                max="200"
                                step="1"
                                value={emaPeriod}
                                onChange={(event) => onEmaPeriodChange(clampNumber(event.target.value, 1, 200, 12))}
                            />
                        </label>
                    )}
                </article>

                <article className={`indicator-card ${isRsiActive ? 'indicator-card--active' : ''}`}>
                    <label className="indicator-card__toggle">
                        <input
                            type="checkbox"
                            checked={isRsiActive}
                            onChange={(event) => onRsiToggle(event.target.checked)}
                        />
                        <span>
                            <strong>RSI</strong>
                            <small>Göreceli güç endeksi (14)</small>
                        </span>
                    </label>
                </article>

                <article className={`indicator-card ${isBollActive ? 'indicator-card--active' : ''}`}>
                    <label className="indicator-card__toggle">
                        <input
                            type="checkbox"
                            checked={isBollActive}
                            onChange={(event) => onBollToggle(event.target.checked)}
                        />
                        <span>
                            <strong>BOLL</strong>
                            <small>Bollinger üst, orta ve alt bantları</small>
                        </span>
                    </label>
                    {isBollActive && (
                        <div className="indicator-card__fields">
                            <label className="indicator-field">
                                <span>Periyot</span>
                                <input
                                    type="number"
                                    min="2"
                                    max="200"
                                    step="1"
                                    value={bollPeriod}
                                    onChange={(event) => onBollPeriodChange(clampNumber(event.target.value, 2, 200, 20))}
                                />
                            </label>
                            <label className="indicator-field">
                                <span>Standart sapma</span>
                                <input
                                    type="number"
                                    min="0.1"
                                    max="10"
                                    step="0.1"
                                    value={bollStdDev}
                                    onChange={(event) => onBollStdDevChange(clampNumber(event.target.value, 0.1, 10, 2))}
                                />
                            </label>
                        </div>
                    )}
                </article>
            </div>
        </section>
    );
}
