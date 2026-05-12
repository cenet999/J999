using BootstrapBlazor.Components;
using TelegramBotBase.Attributes;
using TelegramBotBase.Base;
using TelegramBotBase.Controls.Hybrid;
using TelegramBotBase.Controls.Inline;
using TelegramBotBase.Form;
using TelegramBotBase.Args;
using TelegramBotBase;
using FreeSql;
using TelegramBotBase.Interfaces;
using Telegram.Bot.Types.Enums;
using J9_Admin.TelegramBot;

namespace J9_Admin.TelegramBot
{
    /// <summary>
    /// 机器人的开始表单 - 用户首次与机器人交互时显示
    /// </summary>
    public class StartForm : GroupForm
    {
        // 各种服务实例
        private readonly ILogger<StartForm> _logger;
        private readonly MessageHandler _messageHandler;

        /// <summary>
        /// 构造函数 - 通过服务定位器获取依赖的服务
        /// </summary>
        public StartForm()
        {
            // 通过服务定位器获取服务实例
            _logger = ServiceLocator.ServiceProvider?.GetService<ILogger<StartForm>>();
            _messageHandler = ServiceLocator.ServiceProvider?.GetService<MessageHandler>();

            if (_messageHandler == null)
            {
                throw new InvalidOperationException("无法获取 MessageHandler 服务实例");
            }
        }

        /// <summary>
        /// 处理普通消息
        /// </summary>
        /// <param name="message">消息结果对象</param>
        public override async Task OnMessage(MessageResult message)
        {
            try
            {
                // 使用消息处理服务统一处理所有消息
                await _messageHandler.HandleMessageAsync(Device, message);
            }
            catch (Exception ex)
            {
                // 记录错误日志
                _logger?.LogInformation($"处理消息失败: {ex.Message}");
                await DeviceHelper.SendTempMessageAsync(Device, "处理消息时出现错误，请稍后重试");
            }
        }
    }
}