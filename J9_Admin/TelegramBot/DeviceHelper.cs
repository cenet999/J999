using TelegramBotBase.Base;
using TelegramBotBase.Form;
using TelegramBotBase.Interfaces;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace J9_Admin.TelegramBot
{
    public static class DeviceHelper
    {
        public static async Task SendTempMessageAsync(IDeviceSession device, string message, ButtonForm? buttonForm = null)
        {
            try
            {
                // 发送消息，根据是否有按钮表单选择不同的发送方式
                Message sentMessage = buttonForm != null
                    ? await device.Send(message, buttonForm, parseMode: ParseMode.Html)
                    : await device.Send(message, parseMode: ParseMode.Html);
            }
            catch (Exception ex)
            {
                // 记录错误日志
                Console.WriteLine($"发送临时消息失败: {ex.Message}");
                throw; // 重新抛出异常，让调用者处理
            }
        }


    }
}