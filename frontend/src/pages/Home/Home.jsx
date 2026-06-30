import React from 'react';
import { useAuth } from '../../context/AuthContext';
import CryptoGrid from '../../components/CryptoGrid/CryptoGrid';

export default function Home() {
    const { user } = useAuth();

    return (
        <div style={{ padding: '20px' }}>
            <h2 style={{ textAlign: 'center', color: '#2d3748', marginBottom: '10px' }}>
                Hos geldin {user ? user.username : ''}!
            </h2>
            <p style={{ textAlign: 'center', color: '#718096', marginBottom: '30px' }}>
                CryptoTracker — kripto para takip uygulamasi.
            </p>

            <CryptoGrid />
        </div>
    );
}