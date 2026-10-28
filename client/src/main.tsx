import React from 'react';
import ReactDOM from 'react-dom/client';
import AccountGate from './AccountGate';
import './styles.css';
import { registerPwa } from './pwa';

registerPwa();

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode><AccountGate /></React.StrictMode>,
);
