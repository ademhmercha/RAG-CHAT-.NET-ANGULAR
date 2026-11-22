const target =
  process.env.API_URL ?? process.env.services__api__http__0 ?? 'http://localhost:5080';

module.exports = {
  '/api': {
    target,
    secure: false,
    changeOrigin: false,
  },
};
