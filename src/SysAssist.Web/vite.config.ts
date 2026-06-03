import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    rolldownOptions: {
      output: {
        codeSplitting: {
          groups: [
            {
              name: 'react-vendor',
              test: /node_modules[\\/](react|react-dom|scheduler)[\\/]/,
              priority: 40,
            },
            {
              name: 'charts-vendor',
              test: /node_modules[\\/](recharts|victory-vendor|d3-[^\\/]+)[\\/]/,
              priority: 35,
            },
            {
              name: 'motion-vendor',
              test: /node_modules[\\/](framer-motion|motion|gsap)[\\/]/,
              priority: 30,
            },
            {
              name: 'icons-vendor',
              test: /node_modules[\\/]lucide-react[\\/]/,
              priority: 25,
            },
            {
              name: 'query-vendor',
              test: /node_modules[\\/](@tanstack|zustand)[\\/]/,
              priority: 20,
            },
            {
              name: 'vendor',
              test: /node_modules[\\/]/,
              priority: 10,
              maxSize: 450 * 1024,
            },
          ],
        },
      },
    },
  },
  server: {
    port: 5173,
  },
})
