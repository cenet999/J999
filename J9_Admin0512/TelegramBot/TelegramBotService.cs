using TelegramBotBase.Base;
using TelegramBotBase;
using TelegramBotBase.Builder;
using TelegramBotBase.Commands;
using Telegram.Bot;

namespace J9_Admin.TelegramBot
{
    /// <summary>
    /// Telegram Bot 后台服务
    /// 负责启动和管理 Telegram Bot 实例
    /// </summary>
    public class TelegramBotService : BackgroundService
    {
        private readonly ILogger<TelegramBotService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private BotBase? _bot;

        public TelegramBotService(ILogger<TelegramBotService> logger, IConfiguration configuration, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 后台服务执行方法
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("正在启动 Telegram Bot 服务...");

                // 从配置中获取 Bot Token
                var botToken = _configuration["TelegramBot:ApiKey"];
                if (string.IsNullOrEmpty(botToken))
                {
                    _logger.LogInformation("未找到 Telegram Bot API Key，请在 appsettings.json 中配置 TelegramBot:ApiKey");
                    return;
                }

                // 设置静态服务提供者（用于 StartForm 获取依赖）
                ServiceLocator.ServiceProvider = _serviceProvider;

                // 创建并配置 Bot
                _bot = BotBaseBuilder
                    .Create()
                    .WithAPIKey(botToken) // 设置 Bot Token
                    .DefaultMessageLoop() // 使用默认消息循环
                    .WithStartForm<StartForm>() // 设置开始表单
                    .NoProxy() // 不使用代理
                    .CustomCommands(commands =>
                    {
                        // 自定义命令配置
                        commands.Start("启动机器人"); // /start 命令描述
                        commands.Help("显示帮助信息"); // /help 命令描述
                    })
                    .UseJSON(Path.Combine(AppContext.BaseDirectory, "Configs", "bot_states.json")) // 使用 JSON 状态存储
                    .UseEnglish() // 使用英文（你也可以根据需要更改）
                    .UseSingleThread() // 使用单线程模式
                    .Build();

                // 启动 Bot
                await _bot.Start();
                _logger.LogInformation("Telegram Bot 服务启动成功");

                // 等待取消信号
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Telegram Bot 服务正在停止...");
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex, "Telegram Bot 服务启动失败");
            }
        }

        /// <summary>
        /// 服务停止时的清理工作
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("正在停止 Telegram Bot 服务...");

            if (_bot != null)
            {
                await _bot.Stop();
                _bot = null;
            }

            _logger.LogInformation("Telegram Bot 服务已停止");
            await base.StopAsync(cancellationToken);
        }

        /// <summary>
        /// 获取 Bot 实例（用于发送消息）
        /// </summary>
        public BotBase? GetBotInstance()
        {
            return _bot;
        }


    }

    /// <summary>
    /// 静态服务定位器（用于在无法使用依赖注入的地方获取服务）
    /// </summary>
    public static class ServiceLocator
    {
        public static IServiceProvider? ServiceProvider { get; set; }
    }
}
