import { createApp } from 'vue'
import LogsApp from './LogsApp.vue'
import './styles.css'
import './logs.css'
import { registerApplicationShell } from './pwa'

createApp(LogsApp).mount('#logs-app')
void registerApplicationShell().catch(() => undefined)
