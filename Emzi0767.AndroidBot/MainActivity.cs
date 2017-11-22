using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Text;
using Android.Views;
using Android.Widget;
using AndroidPermission = Android.Content.PM.Permission;
using Environment = Android.OS.Environment;

namespace Emzi0767.AndroidBot
{
    [Activity(Label = "DSharpPlus Example Xamarin Bot", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        private Button BotCtl { get; set; }
        private TextView BotStatus { get; set; }
        private ListView BotLog { get; set; }

        private List<string> LogItems { get; set; }
        private LogItemAdapter LogItemsAdapter { get; set; }
        
        private DspXamarinBot Bot { get; set; }

        private const int PERMID_PERMISSION_REQUEST = 0;
        private const int PERMID_NOTIFICATION = 1;
        private AlertDialog Dialog { get; set; }
        private Notification ServiceNotification { get; set; }
        private Intent ServiceNotificationIntent { get; set; }
        private NotificationManager NotificationManager { get; set; }

        private Task InitTask { get; set; }
        private Task BotTask { get; set; }
        private CancellationTokenSource BotTaskCancellationTokenSource { get; set; }
        private CancellationToken BotTaskCancellationToken { get { return this.BotTaskCancellationTokenSource.Token; } }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);

            this.NotificationManager = this.GetSystemService(Context.NotificationService) as NotificationManager;

            var prms = new[] { Android.Manifest.Permission.ReadExternalStorage, Android.Manifest.Permission.WriteExternalStorage, Android.Manifest.Permission.AccessWifiState, Android.Manifest.Permission.AccessNetworkState };
            var rprms = new List<string>();
            foreach (var prm in prms)
            {
                var pstate = this.CheckSelfPermission(prm);
                if (pstate == AndroidPermission.Denied)
                    rprms.Add(prm);
            }

            if (rprms.Count > 0)
                this.RequestPermissions(rprms.ToArray(), PERMID_PERMISSION_REQUEST);
            else
                this.DoInit();
        }

        private void DoInit()
        {
            var cm = (ConnectivityManager)GetSystemService(ConnectivityService);
            var ci = cm.ActiveNetworkInfo;
            if (ci.Type != ConnectivityType.Wifi)
            {
                this.RunOnUiThread(() =>
                {
                    var adb = new AlertDialog.Builder(this);
                    adb.SetTitle(Resource.String.data_dialog_title);
                    adb.SetMessage(Resource.String.data_dialog_message);
                    adb.SetPositiveButton(Resource.String.data_dialog_yes, delegate { this.CloseDialog(); this.ContinueInit(); });
                    adb.SetNegativeButton(Resource.String.data_dialog_no, delegate { this.Finish(); });
                    this.Dialog = adb.Create();
                    this.Dialog.Show();
                });
            }
            else
                this.ContinueInit();
        }

        private void ContinueInit()
        { 
            this.BotCtl = (Button)this.FindViewById(Resource.Id.botctl);
            this.BotStatus = (TextView)this.FindViewById(Resource.Id.botstatus);
            this.BotLog = (ListView)this.FindViewById(Resource.Id.botlog);

            this.LogItems = new List<string>();
            this.LogItemsAdapter = new LogItemAdapter(this.LogItems, (LayoutInflater)this.GetSystemService(Context.LayoutInflaterService));
            this.BotLog.Adapter = this.LogItemsAdapter;
            this.BotCtl.Click += this.OnBotCtlClick;
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] AndroidPermission[] grantResults)
        {
            switch (requestCode)
            {
                case PERMID_PERMISSION_REQUEST:
                    foreach (var gr in grantResults)
                        if (gr == AndroidPermission.Denied)
                        {
                            Toast.MakeText(this, Resource.String.permission_fail, ToastLength.Long).Show();
                            this.Finish();
                            return;
                        }
                    this.DoInit();
                    break;
            }
        }

        private void CloseDialog()
        {
            if (this.Dialog != null)
            {
                this.RunOnUiThread(() => this.Dialog.Dismiss());
                this.Dialog = null;
            }
        }

        public void OnClick(View v)
        {
            if (v.Id == Resource.Id.botctl)
                this.OnBotCtlClick(this, new EventArgs());
        }

        private void OnBotCtlClick(object sender, EventArgs e)
        {
            this.BotCtl.Enabled = false;
            
            if (this.Bot == null)
                this.InitTask = Task.Run(() => this.StartBot()).ContinueWith(t => this.Bot_LogMessage($"Failed to initialize the bot: {t.Exception}"), TaskContinuationOptions.OnlyOnFaulted);
            else
                this.StopBot();
        }

        private void StartBot()
        {
            this.RunOnUiThread(() =>
            {
                Toast.MakeText(this, Resource.String.botrun_starting, ToastLength.Short).Show();

                var nb = new Notification.Builder(this);
                nb.SetContentTitle(this.GetString(Resource.String.servicenotif_botrunning));
                nb.SetContentText(this.GetString(Resource.String.servicenotif_botopen));
                nb.SetSmallIcon(Resource.Drawable.Iconmini);
                nb.SetOngoing(true);
                nb.SetAutoCancel(false);

                this.ServiceNotification = nb.Build();

                this.ServiceNotificationIntent = new Intent(this, this.GetType());
                this.ServiceNotificationIntent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
                var intent = PendingIntent.GetActivity(this, 0, this.ServiceNotificationIntent, 0);

                this.ServiceNotification.SetLatestEventInfo(this, this.GetString(Resource.String.servicenotif_botrunning), this.GetString(Resource.String.servicenotif_botopen), intent);
                this.ServiceNotification.Flags |= NotificationFlags.NoClear | NotificationFlags.OngoingEvent;

                this.NotificationManager.Notify(PERMID_NOTIFICATION, this.ServiceNotification);
            });

            var pth = Environment.ExternalStorageDirectory.AbsolutePath;
            pth = Path.Combine(pth, "emzi0767", "companioncube");

            if (!Directory.Exists(pth))
                Directory.CreateDirectory(pth);

            var utf8 = new UTF8Encoding(false);
            var aid = Settings.System.GetString(this.ContentResolver, Settings.Secure.AndroidId);
            var akey = utf8.GetBytes(aid);
            var aiv = new byte[16];
            var rng = new RNGCryptoServiceProvider();
            var cc = 0ul;

            pth = Path.Combine(pth, "settings.bin");
            if (File.Exists(pth))
                using (var fs = File.OpenRead(pth))
                using (var br = new BinaryReader(fs))
                    aiv = br.ReadBytes(aiv.Length);
            else
                rng.GetBytes(aiv);

            cc = BitConverter.ToUInt16(aiv, 0);

            using (var sha256 = SHA256Managed.Create())
                for (var i = 0u; i < cc; i++)
                    akey = sha256.ComputeHash(akey);

            var token = "";

            using (var aes = RijndaelManaged.Create())
            {
                if (!File.Exists(pth))
                {
                    this.RunOnUiThread(() =>
                    {
                        var dm = Resources.DisplayMetrics;

                        var et = new EditText(this)
                        {
                            InputType = InputTypes.ClassText | InputTypes.TextVariationPassword
                        };
                        et.Hint = this.GetString(Resource.String.token_dialog_placeholder);
                        et.LayoutParameters = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);

                        var fl = new FrameLayout(this);
                        fl.SetPadding((int)(20 * dm.Density), 0, (int)(20 * dm.Density), 0);
                        fl.AddView(et);

                        var adb = new AlertDialog.Builder(this);
                        adb.SetTitle(Resource.String.token_dialog_title);
                        adb.SetMessage(Resource.String.token_dialog_message);
                        adb.SetView(fl);
                        adb.SetPositiveButton(Resource.String.token_dialog_confirm, delegate { this.CloseDialog(); token = et.Text; this.ContinueStartup(token, akey, aiv, pth); });
                        adb.SetNegativeButton(Resource.String.token_dialog_cancel, delegate { this.Finish(); });
                        this.Dialog = adb.Create();
                        this.Dialog.Show();
                    });
                }
                else
                {
                    using (var aes_dec = aes.CreateDecryptor(akey, aiv))
                    using (var fs = File.OpenRead(pth))
                    using (var bw = new BinaryReader(fs))
                    using (var cs = new CryptoStream(fs, aes_dec, CryptoStreamMode.Read))
                    {
                        aiv = bw.ReadBytes(16);
                        var tkb = new byte[ushort.MaxValue];
                        var br = 0;
                        while ((br = cs.Read(tkb, 0, tkb.Length)) > 0)
                            token = string.Concat(token, utf8.GetString(tkb, 0, br));
                    }

                    this.ContinueStartup(token, akey, aiv, pth);
                }
            }
        }

        private void ContinueStartup(string token, byte[] akey, byte[] aiv, string pth)
        {
            var utf8 = new UTF8Encoding(false);

            using (var aes = RijndaelManaged.Create())
            using (var aes_enc = aes.CreateEncryptor(akey, aiv))
            using (var fs = File.Create(pth))
            using (var bw = new BinaryWriter(fs))
            using (var cs = new CryptoStream(fs, aes_enc, CryptoStreamMode.Write))
            {
                var tkb = utf8.GetBytes(token);
                bw.Write(aiv);
                cs.Write(tkb, 0, tkb.Length);
                cs.FlushFinalBlock();
            }

            this.Bot = new DspXamarinBot(token);
            this.Bot.LogMessage += this.Bot_LogMessage;

            this.LogItems.Clear();
            this.RunOnUiThread(() => this.LogItemsAdapter.NotifyDataSetChanged());

            this.BotTaskCancellationTokenSource = new CancellationTokenSource();
            this.BotTask = Task.Run(this.RunBot, this.BotTaskCancellationToken);
        }

        private async Task RunBot()
        {
            try
            {
                await this.Bot.StartAsync();

                this.RunOnUiThread(() =>
                {
                    this.BotCtl.Text = this.GetString(Resource.String.botctl_off);
                    this.BotCtl.Enabled = true;
                    this.BotStatus.Text = this.GetString(Resource.String.botstatus_on);
                    Toast.MakeText(this, Resource.String.botrun_running, ToastLength.Short).Show();
                });
            }
            catch (Exception ex)
            {
                this.RunOnUiThread(() => Toast.MakeText(this, string.Concat(this.GetString(Resource.String.botrun_running), ex.GetType(), ": ", ex.Message), ToastLength.Long).Show());
            }
        }

        private void StopBot()
        {
            Toast.MakeText(this, Resource.String.botrun_stopping, ToastLength.Short).Show();

            this.KillBot().GetAwaiter().GetResult();
        }

        private async Task KillBot()
        {
            this.BotTaskCancellationTokenSource.Cancel();

            await this.Bot.StopAsync();
            this.Bot = null;
            
            this.RunOnUiThread(() =>
            {
                this.NotificationManager.Cancel(PERMID_NOTIFICATION);
                this.ServiceNotification = null;
                this.BotCtl.Text = this.GetString(Resource.String.botctl_on);
                this.BotCtl.Enabled = true;
                this.BotStatus.Text = this.GetString(Resource.String.botstatus_off);
                Toast.MakeText(this, Resource.String.botrun_stopped, ToastLength.Short).Show();
            });
        }

        private void Bot_LogMessage(string formattedMessage)
        {
            this.RunOnUiThread(() =>
            {
                if (formattedMessage.Contains("\r\n"))
                    formattedMessage.Replace("\r", "");

                var msgs = formattedMessage.Split('\n');
                foreach (var msg in msgs)
                    this.LogItemsAdapter.Add(msg);

                this.LogItemsAdapter.NotifyDataSetChanged();
                this.BotLog.SmoothScrollToPosition(this.LogItemsAdapter.Count - 1);
            });
        }
    }
}

