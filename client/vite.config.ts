import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import fs from 'node:fs';

const certificatePath = process.env.GGD_HTTPS_CERT_PATH;
const keyPath = process.env.GGD_HTTPS_KEY_PATH;
const https = certificatePath && keyPath
  ? { cert: fs.readFileSync(certificatePath), key: fs.readFileSync(keyPath) }
  : undefined;

export default defineConfig({
  plugins: [react()],
  server: { host: '0.0.0.0', port: 5173, https },
  preview: { host: '0.0.0.0', port: 5173, https },
});
