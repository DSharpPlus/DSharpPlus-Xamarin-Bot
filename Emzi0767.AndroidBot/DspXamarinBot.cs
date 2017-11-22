using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Android.OS;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Net.WebSocket;

namespace Emzi0767.AndroidBot
{
    public class DspXamarinBot
    {
        public DiscordClient Discord { get; }
        public CommandsNextExtension Commands { get; }
        public InteractivityExtension Interactivity { get; }

        private Timer DiscordStatusTimer { get; set; }

        public DspXamarinBot(string token)
        {
            // instantiate the client
            this.Discord = new DiscordClient(new DiscordConfiguration
            {
                Token = token,
                TokenType = TokenType.Bot,

                AutoReconnect = true,
                GatewayCompressionLevel = GatewayCompressionLevel.Payload,

                LogLevel = LogLevel.Debug,
                UseInternalLogHandler = false
            });

            // set the ws implementation
            this.Discord.SetWebSocketClient<WebSocket4NetCoreClient>();

            // register all events
            this.Discord.DebugLogger.LogMessageReceived += this.OnLogMessage;
            this.Discord.SocketErrored += this.Discord_SocketErrored;
            this.Discord.ClientErrored += this.Discord_ClientErrored;
            this.Discord.GuildAvailable += this.Discord_GuildAvailable;
            this.Discord.Ready += this.Discord_Ready;

            // enable cnext
            this.Commands = this.Discord.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefix = "X:",
                EnableMentionPrefix = true,

                EnableDms = true,
                EnableDefaultHelp = true
            });

            // register commands
            this.Commands.RegisterCommands(Assembly.GetExecutingAssembly());

            // register cnext event handlers
            this.Commands.CommandExecuted += this.Commands_CommandExecuted;
            this.Commands.CommandErrored += this.Commands_CommandErrored;

            // enable interactivity
            this.Discord.UseInteractivity(new InteractivityConfiguration());
        }

        private Task Discord_SocketErrored(SocketErrorEventArgs e)
        {
            this.Discord.DebugLogger.LogMessage(LogLevel.Error, "CCPortable", e.Exception.ToString(), DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Discord_ClientErrored(ClientErrorEventArgs e)
        {
            this.Discord.DebugLogger.LogMessage(LogLevel.Error, "CCPortable", e.Exception.ToString(), DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Discord_GuildAvailable(GuildCreateEventArgs e)
        {
            this.Discord.DebugLogger.LogMessage(LogLevel.Info, "CCPortable", string.Concat("Guild available: ", e.Guild.Name), DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Discord_Ready(ReadyEventArgs e)
        {
            this.DiscordStatusTimer = new Timer(this.DiscordStatusTimerCallback, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            this.Discord.DebugLogger.LogMessage(LogLevel.Info, "CCPortable", "Ready", DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Commands_CommandExecuted(CommandExecutionEventArgs e)
        {
            this.Discord.DebugLogger.LogMessage(LogLevel.Debug, "CCPortable", string.Concat(e.Context.User.Username, "#", e.Context.User.Discriminator, " executed ", e.Command.QualifiedName, " in #", e.Context.Channel.Name), DateTime.Now);
            return Task.CompletedTask;
        }

        private Task Commands_CommandErrored(CommandErrorEventArgs e)
        {
            if (e.Exception is CommandNotFoundException)
                return Task.CompletedTask;

            this.Discord.DebugLogger.LogMessage(LogLevel.Error, "CommandsNext", string.Concat(e.Exception.GetType(), ": ", e.Exception.Message), DateTime.Now);

            var ms = e.Exception.Message;
            var st = e.Exception.StackTrace;

            ms = ms.Length > 1000 ? ms.Substring(0, 1000) : ms;
            st = st.Length > 1000 ? st.Substring(0, 1000) : st;

            var embed = new DiscordEmbedBuilder
            {
                Color = new DiscordColor(0xFF0000),
                Title = "An exception occured when executing a command",
                Description = string.Concat("`", e.Exception.GetType(), "` occured when executing `", e.Command.QualifiedName, "`."),
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    IconUrl = Discord.CurrentUser.AvatarUrl,
                    Text = Discord.CurrentUser.Username
                },
                Timestamp = DateTime.UtcNow
            };
            
            embed.AddField("Message", ms, false)
            .AddField("Stack Trace", Formatter.BlockCode(st));

            return e.Context.Channel.SendMessageAsync("\u200b", embed: embed.Build());
        }

        public Task StartAsync()
            => this.Discord.ConnectAsync();

        public Task StopAsync()
        {
            this.DiscordStatusTimer?.Dispose();
            this.DiscordStatusTimer = null;

            return this.Discord.DisconnectAsync();
        }

        private void DiscordStatusTimerCallback(object _)
        {
            try
            {
                this.Discord.UpdateStatusAsync(new DiscordActivity(string.Concat("on ", Build.Manufacturer, " ", Build.Model, ", ", Build.CpuAbi), ActivityType.Playing)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                this.Discord.DebugLogger.LogMessage(LogLevel.Error, "Companion Cube", string.Concat("Failed to set status (", ex.GetType().ToString(), ": ", ex.Message, ")"), DateTime.Now);
            }
        }

        private void OnLogMessage(object sender, DebugLogMessageEventArgs e)
        {
            if (this.LogMessage == null)
                return;

            var msg = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [{1,5}] [{2,10}] {3}", e.Timestamp, e.Level, e.Application, e.Message);
            this.LogMessage(msg);
        }

        public event LogMessageEventHandler LogMessage;
    }
}