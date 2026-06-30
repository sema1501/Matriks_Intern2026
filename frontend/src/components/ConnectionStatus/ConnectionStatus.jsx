import React from 'react';
import { useBinance } from '../../context/BinanceContext';
import './ConnectionStatus.css';

export default function ConnectionStatus() {
    const { connectionStatus } = useBinance();

    const renderStatus = () => {
        switch (connectionStatus) {
            case 'connected':
                return (
                    <div className="status-wrapper connected">
                        <span className="status-dot"></span>
                        <span className="status-text">Canlı</span>
                    </div>
                );
            case 'connecting':
                return (
                    <div className="status-wrapper connecting">
                        <span className="status-spinner">⏳</span>
                        <span className="status-text">Bağlanıyor</span>
                    </div>
                );
            case 'disconnected':
            default:
                return (
                    <div className="status-wrapper disconnected">
                        <span className="status-alert">⚠️</span>
                        <span className="status-text">Bağlantı Kesildi</span>
                    </div>
                );
        }
    };

    return <div className="connection-status-container">{renderStatus()}</div>;
}