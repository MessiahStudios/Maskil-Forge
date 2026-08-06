import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  build: {
    rollupOptions: {
      input: {
        editor: 'index.html',
        logs: 'logs.html',
      },
    },
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5072',
    },
  },
})
