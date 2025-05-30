const PROXY_CONFIG = [
  {
    'context': [
      '/hub/',
      '/UserAccount/',
      '/WebServer/',
      '/TechnicalAnalyzer/',
      '/LlmAssistant/',
    ],
    'target': 'https://127.0.0.1:5001',
    'secure': false, // A backend server running on HTTPS with an invalid certificate will not be accepted by default. If you want to, you need to set secure: false.
    'ws': true,
    'changeOrigin': true,
    'logLevel': 'debug'
  }
];

module.exports = PROXY_CONFIG;