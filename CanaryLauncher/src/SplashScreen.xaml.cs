using System;
using System.Windows;
using System.IO;
using System.Net;
using System.Windows.Threading;
using System.Net.Http;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Reflection;
using Ionic.Zip;
using Newtonsoft.Json;
using LauncherConfig;

namespace Launcher
{
	public partial class SplashScreen : Window
	{
		static string launcherConfigUrl = "https://raw.githubusercontent.com/redProjt/redprojt-client/master/launcher_config.json";
		static ClientConfig clientConfig = null;
		static string newLauncherVersion = null;
		static string launcherExecutable = null;
		static string newLauncherUrl = null;
		static string packageFolder = null;

		WebClient webClient = new WebClient();
		DispatcherTimer timer = new DispatcherTimer();

		public SplashScreen()
		{
			InitializeComponent();

			 // Load launcher config file if URL is valid and connection is successful
			if (CheckUrlAndConnection(launcherConfigUrl))
			{
				clientConfig = ClientConfig.loadFromFile(launcherConfigUrl);
				newLauncherVersion = clientConfig.launcherVersion;
				launcherExecutable = clientConfig.launcherExecutable;
				newLauncherUrl = clientConfig.newLauncherUrl;
				packageFolder = clientConfig.packageFolder;
			}

			TryDownloadOrOpenLauncher();
		}

		public void TryDownloadOrOpenLauncher()
		{
			if (File.Exists(GetLauncherPath() + "/" + launcherExecutable) && File.Exists(GetLauncherPath() + "/launcher_config.json") && GetProgramVersion(GetLauncherPath()) == newLauncherVersion) {
				StartClientLauncher();
			} else {
				TaskDownloadClientLauncher(newLauncherUrl);
			}
		}

		public async void TemporizedSplashScreen(object sender, EventArgs e)
		{
			if (File.Exists(GetLauncherPath() + "/launcher.zip")) {
				unpackage(GetLauncherPath() + "/launcher.zip", ExtractExistingFileAction.OverwriteSilently);
			}

			CreateShortcut();
			if (!File.Exists(GetLauncherPath() + "/launcher_config.json"))
			{
				string localPath = Path.Combine(GetLauncherPath(), "launcher_config.json");
				await webClient.DownloadFileTaskAsync(launcherConfigUrl, localPath);
			}

			if (File.Exists(GetLauncherPath() + "/launcher.zip")) {
				File.Delete(GetLauncherPath() + "/launcher.zip");
			}
			StartClientLauncher();
		}

		private async void UnzipSecure()
		{
			if (File.Exists(GetLauncherPath() + "/launcher.zip")) {
				unpackage(GetLauncherPath() + "/launcher.zip", ExtractExistingFileAction.OverwriteSilently);
			}

			CreateShortcut();
			string localPath = Path.Combine(GetLauncherPath(), "launcher_config.json");
			await webClient.DownloadFileTaskAsync(launcherConfigUrl, localPath);
			if (File.Exists(GetLauncherPath() + "/launcher.zip")) {
				File.Delete(GetLauncherPath() + "/launcher.zip");
			}
		}

		private bool CheckUrlAndConnection(string url)
		{
			Uri uri;
			if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
			{
				// URL is not valid
				//MessageBox.Show("The URL provided is not valid.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			using (var client = new HttpClient())
			{
				HttpResponseMessage response;
				try
				{
					response = client.GetAsync(uri).Result;
				}
				catch (AggregateException)
				{
					// Connection error occurred
					//MessageBox.Show("Connection error ocurred.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return false;
				}

				if (response.IsSuccessStatusCode)
				{
					// URL and connection are valid
					return true;
				}
				else
				{
					// Connection successful, but URL is not valid or file not found
					//MessageBox.Show("The URL provided is valid, but the file was not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
					return false;
				}
			}
		}

		private string GetLauncherPath(bool onlyBaseDirectory = false)
		{
			string launcherPath = "";
			if (string.IsNullOrEmpty(packageFolder) || onlyBaseDirectory) {
				launcherPath = AppDomain.CurrentDomain.BaseDirectory.ToString();
			} else {
				launcherPath = AppDomain.CurrentDomain.BaseDirectory.ToString() + "/" + packageFolder;
			}
			
			return launcherPath;
		}

		string GetProgramVersion(string path)
		{
			string json = GetLauncherPath() + "/launcher_config.json";
			using (StreamReader stream = new StreamReader(json))
			{
				dynamic jsonString = stream.ReadToEnd();
				dynamic jsonDeserialized = JsonConvert.DeserializeObject(jsonString);
				return jsonDeserialized.launcherVersion;
			}
		}

		private bool StartClientLauncher()
		{
			if (File.Exists(GetLauncherPath() + "/" + launcherExecutable)) {
				Process.Start(GetLauncherPath() + "/" + launcherExecutable);
				this.Close();
				return true;
			}
			return false;
		}

		private void unpackage(string path, ExtractExistingFileAction existingFileAction)
		{
			using (ZipFile modZip = ZipFile.Read(path))
			{
				foreach (ZipEntry zipEntry in modZip)
				{
					zipEntry.Extract(GetLauncherPath(), existingFileAction);
				}
			}
		}

		static void CreateShortcut()
		{
			string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
			string shortcutPath = Path.Combine(desktopPath, "RedProject.lnk");
			Type t = Type.GetTypeFromProgID("WScript.Shell");
			dynamic shell = Activator.CreateInstance(t);
			var lnk = shell.CreateShortcut(shortcutPath);
			try
			{
				lnk.TargetPath = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
				lnk.Description = "RedProject";
				lnk.Save();
			}
			finally
			{
				System.Runtime.InteropServices.Marshal.FinalReleaseComObject(lnk);
			}
		}

		private void TaskDownloadClientLauncher(string url)
		{
			Directory.CreateDirectory(GetLauncherPath());
			webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadClientLauncherCompleted);
			webClient.DownloadFileAsync(new Uri(url), GetLauncherPath() + "/launcher.zip");
		}

		private void DownloadClientLauncherCompleted(object sender, AsyncCompletedEventArgs e)
		{
			if (e.Error == null)
			{
				timer.Tick += new EventHandler(TemporizedSplashScreen);
				timer.Interval = new TimeSpan(0, 0, 0);
				timer.Start();
			}
			else
			{
				System.Windows.MessageBox.Show("There was a problem downloading the launcher, please contact the administrator.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
			}
		}
	}
}
