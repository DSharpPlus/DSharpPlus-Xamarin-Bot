using System.Text;
using System.Threading.Tasks;
using Android.OS;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace Emzi0767.AndroidBot
{
    public class PortableCommands
    {
        [Command("platforminfo"), Aliases("pinfo", "osinfo"), Description("Gets information about the bot's platform.")]
        public async Task GetPlatformInfoAsync(CommandContext ctx)
        {
            var sb = new StringBuilder();

            sb.AppendLine("```less");

            sb.AppendLine("Companion Cube Portable platform information:");
            sb.AppendLine();

            sb.AppendFormat("OS:             | Android").AppendLine();
            sb.AppendFormat("OS Version:     | {0}", Build.VERSION.Release).AppendLine();
            sb.AppendFormat("CPU ABI:        | {0}, {1}", Build.CpuAbi, Build.CpuAbi2).AppendLine();
            sb.AppendFormat("Device:         | {0} {1}, {2}, {3}", Build.Manufacturer, Build.Model, Build.Device, Build.Brand).AppendLine();
            sb.AppendFormat("Product:        | {0}", Build.Product).AppendLine();
            sb.AppendFormat("Hardware:       | {0}, {1}", Build.Hardware, Build.Board).AppendLine();

            sb.Append("```");

            await ctx.RespondAsync(sb.ToString());
        }
    }
}