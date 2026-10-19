// Backend BotSignalReason enum'u: Strategy = 0, StopLoss = 1, TakeProfit = 2.
// Bot sinyal listesi sayı, backtest raporu metin ("StopLoss") döndürür; ikisi de buradan geçer.
const REASONS = ['strategy', 'stopLoss', 'takeProfit'];

export const normalizeReason = (reason) => {
  if (reason == null) return 'strategy';
  if (typeof reason === 'string') {
    const match = REASONS.find((item) => item.toLowerCase() === reason.toLowerCase());
    return match || 'strategy';
  }
  return REASONS[Number(reason)] || 'strategy';
};

export const isRiskExit = (reason) => normalizeReason(reason) !== 'strategy';

export const reasonLabelKey = (reason) => `bot.reason.${normalizeReason(reason)}`;
