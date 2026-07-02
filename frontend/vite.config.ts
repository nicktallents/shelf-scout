import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    vueDevTools(),
    VitePWA({
      registerType: 'autoUpdate',
      workbox: {
        // Shell/asset caching only in v1 (ADR 0008) — /api is never cached.
        navigateFallbackDenylist: [/^\/api\//],
      },
      manifest: {
        name: 'Shelf Scout',
        short_name: 'Shelf Scout',
        description: 'Track what is in your kitchen before it expires.',
        start_url: '/',
        display: 'standalone',
        background_color: '#ffffff',
        theme_color: '#1f7a5c',
        icons: [
          {
            src: '/icons/icon-192.png',
            sizes: '192x192',
            type: 'image/png',
          },
          {
            src: '/icons/icon-512.png',
            sizes: '512x512',
            type: 'image/png',
          },
          {
            src: '/icons/icon-512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable',
          },
        ],
      },
    }),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5222',
    },
  },
  build: {
    outDir: '../backend/ShelfScout.Api/wwwroot',
    emptyOutDir: true,
  },
})
